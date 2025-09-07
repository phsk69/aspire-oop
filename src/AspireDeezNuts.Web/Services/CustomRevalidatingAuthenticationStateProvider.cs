using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.Extensions.Options;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using AspireDeezNuts.Shared.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Collections.Concurrent;

namespace AspireDeezNuts.Web.Services;

public class CustomRevalidatingAuthenticationStateProvider(
    ILoggerFactory loggerFactory,
    IServiceScopeFactory scopeFactory,
    IOptions<AuthenticationOptions> authOptions,
    IHttpContextAccessor httpContextAccessor) : RevalidatingServerAuthenticationStateProvider(loggerFactory)
{
    private readonly ILogger<CustomRevalidatingAuthenticationStateProvider> _logger = loggerFactory.CreateLogger<CustomRevalidatingAuthenticationStateProvider>();
    private readonly AuthenticationOptions _authOptions = authOptions.Value;
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;
    
    // Track suspicious token usage patterns for audit logging
    private readonly ConcurrentDictionary<string, List<DateTime>> _tokenUsageAttempts = new();
    private readonly object _auditLock = new();

    protected override TimeSpan RevalidationInterval => TimeSpan.FromMinutes(_authOptions.TokenRefreshIntervalMinutes);

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        // Check for user-specific cached principal in HTTP context
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext != null)
        {
            var userId = GetUserIdFromContext(httpContext);
            if (!string.IsNullOrEmpty(userId))
            {
                var cachedPrincipal = httpContext.Items[$"CachedPrincipal_{userId}"] as ClaimsPrincipal;
                if (cachedPrincipal != null)
                {
                    _logger.LogInformation("Returning cached authentication state with updated tokens for user: {UserId}", userId);
                    return new AuthenticationState(cachedPrincipal);
                }
            }
        }
        
        // Otherwise use the base implementation
        return await base.GetAuthenticationStateAsync();
    }

    protected override async Task<bool> ValidateAuthenticationStateAsync(
        AuthenticationState authenticationState, CancellationToken cancellationToken)
    {
        if (!authenticationState.User.Identity?.IsAuthenticated ?? true)
            return false;

        try
        {
            // Get the current JWT token from claims
            var tokenClaim = authenticationState.User.Claims.FirstOrDefault(c => c.Type == "access_token");
            if (tokenClaim == null)
            {
                _logger.LogWarning("No access_token claim found");
                return false;
            }

            // Check if token is expired or close to expiring (within 5 minutes)
            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(tokenClaim.Value);
            var expirationTime = jwtToken.ValidTo;
            var timeUntilExpiry = expirationTime - DateTime.UtcNow;

            _logger.LogInformation("Token expires in {Minutes} minutes", timeUntilExpiry.TotalMinutes);

            // If token is already expired, immediately invalidate
            if (timeUntilExpiry.TotalMinutes <= 0)
            {
                _logger.LogWarning("Token has expired - clearing authentication");
                
                // Clear user-specific cached principal
                var userId = GetUserIdentifier(authenticationState.User);
                if (!string.IsNullOrEmpty(userId))
                {
                    ClearCachedPrincipalForUser(userId);
                }
                
                // Try to clear cookies if possible
                using (var scope = scopeFactory.CreateScope())
                {
                    var httpContextAccessor = scope.ServiceProvider.GetService<IHttpContextAccessor>();
                    if (httpContextAccessor?.HttpContext != null && !httpContextAccessor.HttpContext.Response.HasStarted)
                    {
                        try
                        {
                            await httpContextAccessor.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                            _logger.LogInformation("Cleared authentication cookie due to expired token");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error clearing authentication cookie");
                        }
                    }
                }
                
                // Notify that user is no longer authenticated
                var anonymousPrincipal = new ClaimsPrincipal(new ClaimsIdentity());
                NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(anonymousPrincipal)));
                
                return false;
            }

            if (timeUntilExpiry.TotalMinutes <= 10) // Refresh if less than 10 minutes left
            {
                _logger.LogInformation("Token needs refresh, attempting to refresh");
                
                // Get refresh token from claims
                var refreshTokenClaim = authenticationState.User.Claims.FirstOrDefault(c => c.Type == "refresh_token");
                if (refreshTokenClaim == null)
                {
                    _logger.LogWarning("No refresh_token claim found, cannot refresh");
                    return false;
                }
                
                using var scope = scopeFactory.CreateScope();
                var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
                
                // Validate refresh token ownership before processing
                var currentUserId = GetUserIdentifier(authenticationState.User);
                if (string.IsNullOrEmpty(currentUserId))
                {
                    _logger.LogWarning("Cannot validate token ownership - no user identifier found in claims");
                    LogSuspiciousActivity("MISSING_USER_ID", "Token refresh attempted without valid user identifier");
                    return false;
                }
                
                // Validate that the refresh token belongs to the current user context
                if (!ValidateRefreshTokenOwnership(refreshTokenClaim.Value, currentUserId))
                {
                    _logger.LogWarning("Refresh token ownership validation failed for user: {UserId}", currentUserId);
                    LogSuspiciousActivity(currentUserId, "Attempted to use refresh token that doesn't belong to current user");
                    return false;
                }
                
                // Try to refresh both tokens using the refresh token from claims
                var newTokens = await authService.RefreshTokensAsync(refreshTokenClaim.Value);
                
                if (newTokens != null && !string.IsNullOrEmpty(newTokens.AccessToken))
                {
                    _logger.LogInformation("Tokens refreshed successfully for user: {UserId}", currentUserId);
                    
                    // Validate that the new access token belongs to the same user
                    if (!ValidateNewTokenOwnership(newTokens.AccessToken, currentUserId))
                    {
                        _logger.LogError("New access token validation failed - token doesn't belong to user: {UserId}", currentUserId);
                        LogSuspiciousActivity(currentUserId, "Received access token that doesn't match current user identity");
                        return false;
                    }
                    
                    // Update the cookie authentication principal first
                    var httpContextAccessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
                    var newPrincipal = await UpdateCookieAuthenticationAsync(newTokens, authenticationState.User, httpContextAccessor);
                    
                    if (newPrincipal != null)
                    {
                        // Cache the new principal for this specific user
                        var userId = GetUserIdentifier(newPrincipal);
                        if (!string.IsNullOrEmpty(userId))
                        {
                            CachePrincipalForUser(userId, newPrincipal);
                        }
                        
                        // Update the authentication state with the new principal
                        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(newPrincipal)));
                        
                        // Broadcast SignalR notification for automatic token refresh
                        await NotifyTokenRefreshViaSignalR(newTokens, newPrincipal, scope);
                        
                        _logger.LogInformation("Authentication cookie and state updated successfully, cached for user: {UserId}", userId);
                        return true;
                    }
                    else
                    {
                        _logger.LogError("Failed to update authentication cookie");
                        return false;
                    }
                }
                else
                {
                    _logger.LogWarning("Token refresh failed - logging out user");
                    // Clear user-specific cached principal since token is invalid
                    var userId = GetUserIdentifier(authenticationState.User);
                    if (!string.IsNullOrEmpty(userId))
                    {
                        ClearCachedPrincipalForUser(userId);
                    }
                    
                    // Notify that user is no longer authenticated
                    var anonymousPrincipal = new ClaimsPrincipal(new ClaimsIdentity());
                    NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(anonymousPrincipal)));
                    
                    return false; // This will cause a re-authentication
                }
            }

            return true; // Token is still valid
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating authentication state");
            return false;
        }
    }

    private async Task UpdateAuthenticationStateWithNewToken(string newToken, ClaimsPrincipal currentUser)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(newToken);
            
            // Create new claims list with updated token
            var claims = jwtToken.Claims.ToList();
            claims.Add(new Claim("access_token", newToken));
            
            // Preserve essential claims from current user
            var existingNameClaim = currentUser.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name);
            var existingEmailClaim = currentUser.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email);
            var existingRefreshTokenClaim = currentUser.Claims.FirstOrDefault(c => c.Type == "refresh_token");
            
            if (existingNameClaim != null && !claims.Any(c => c.Type == ClaimTypes.Name))
            {
                claims.Add(existingNameClaim);
            }
            if (existingEmailClaim != null && !claims.Any(c => c.Type == ClaimTypes.Email))
            {
                claims.Add(existingEmailClaim);
            }
            if (existingRefreshTokenClaim != null && !claims.Any(c => c.Type == "refresh_token"))
            {
                claims.Add(existingRefreshTokenClaim);
            }

            var identity = new ClaimsIdentity(claims, currentUser.Identity!.AuthenticationType);
            var newPrincipal = new ClaimsPrincipal(identity);
            
            // Cache the new principal for this specific user
            var userId = GetUserIdentifier(newPrincipal);
            if (!string.IsNullOrEmpty(userId))
            {
                CachePrincipalForUser(userId, newPrincipal);
            }
            
            // Notify that authentication state changed
            NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(newPrincipal)));
            
            _logger.LogInformation("Authentication state updated with new token and cached for user: {UserId}", userId);
            
            // Small delay to ensure state propagation
            await Task.Delay(1);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating authentication state with new token");
        }
    }

    public async Task UpdateAuthenticationStateWithNewTokens(LoginResponse newTokens, ClaimsPrincipal currentUser)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(newTokens.AccessToken);
            
            // Create new claims list with updated tokens
            var claims = jwtToken.Claims.ToList();
            claims.Add(new Claim("access_token", newTokens.AccessToken));
            
            // Add the new refresh token
            if (!string.IsNullOrEmpty(newTokens.RefreshToken))
            {
                claims.Add(new Claim("refresh_token", newTokens.RefreshToken));
            }
            
            // Preserve essential claims from current user
            var existingNameClaim = currentUser.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name);
            var existingEmailClaim = currentUser.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email);
            
            if (existingNameClaim != null && !claims.Any(c => c.Type == ClaimTypes.Name))
            {
                claims.Add(existingNameClaim);
            }
            if (existingEmailClaim != null && !claims.Any(c => c.Type == ClaimTypes.Email))
            {
                claims.Add(existingEmailClaim);
            }

            var identity = new ClaimsIdentity(claims, currentUser.Identity!.AuthenticationType);
            var newPrincipal = new ClaimsPrincipal(identity);
            
            // Cache the new principal for this specific user
            var userId = GetUserIdentifier(newPrincipal);
            if (!string.IsNullOrEmpty(userId))
            {
                CachePrincipalForUser(userId, newPrincipal);
            }
            
            // Notify that authentication state changed
            NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(newPrincipal)));
            
            _logger.LogInformation("Authentication state updated with new access and refresh tokens and cached for user: {UserId}", userId);
            
            // Small delay to ensure state propagation
            await Task.Delay(1);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating authentication state with new tokens");
        }
    }

    private async Task<ClaimsPrincipal?> UpdateCookieAuthenticationAsync(LoginResponse newTokens, ClaimsPrincipal currentUser, IHttpContextAccessor httpContextAccessor)
    {
        try
        {
            var httpContext = httpContextAccessor.HttpContext;
            if (httpContext == null)
            {
                _logger.LogWarning("HttpContext is null, cannot update authentication cookie");
                return null;
            }

            // Check if response has already started - can't update cookies if it has
            if (httpContext.Response.HasStarted)
            {
                _logger.LogDebug("Response already started, deferring cookie update to next request. Token refresh succeeded.");
                // Still return the new principal even though we couldn't update the cookie
                // The in-memory state will be correct, just the cookie won't be updated until next request
                var handler = new JwtSecurityTokenHandler();
                var jwtToken = handler.ReadJwtToken(newTokens.AccessToken);
                var tempClaims = jwtToken.Claims.ToList();
                tempClaims.Add(new Claim("access_token", newTokens.AccessToken));
                if (!string.IsNullOrEmpty(newTokens.RefreshToken))
                {
                    tempClaims.Add(new Claim("refresh_token", newTokens.RefreshToken));
                }
                
                var tempIdentity = new ClaimsIdentity(tempClaims, currentUser.Identity!.AuthenticationType);
                return new ClaimsPrincipal(tempIdentity);
            }

            // Parse the new JWT token to get updated claims
            var jwtHandler = new JwtSecurityTokenHandler();
            var jwt = jwtHandler.ReadJwtToken(newTokens.AccessToken);
            
            // Create new claims list with updated tokens
            var claims = jwt.Claims.ToList();
            claims.Add(new Claim("access_token", newTokens.AccessToken));
            
            // Add the new refresh token
            if (!string.IsNullOrEmpty(newTokens.RefreshToken))
            {
                claims.Add(new Claim("refresh_token", newTokens.RefreshToken));
            }
            
            // Preserve essential claims from current user that might not be in JWT
            var existingNameClaim = currentUser.FindFirst(ClaimTypes.Name);
            var existingEmailClaim = currentUser.FindFirst(ClaimTypes.Email);
            
            if (existingNameClaim != null && !claims.Any(c => c.Type == ClaimTypes.Name))
            {
                claims.Add(existingNameClaim);
            }
            if (existingEmailClaim != null && !claims.Any(c => c.Type == ClaimTypes.Email))
            {
                claims.Add(existingEmailClaim);
            }

            // Create new principal and sign in to update the cookie
            var identity = new ClaimsIdentity(claims, currentUser.Identity!.AuthenticationType);
            var newPrincipal = new ClaimsPrincipal(identity);
            
            await httpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, newPrincipal);
            _logger.LogInformation("Authentication cookie updated with new tokens");
            
            return newPrincipal;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating authentication cookie");
            return null;
        }
    }

    private async Task NotifyTokenRefreshViaSignalR(LoginResponse newTokens, ClaimsPrincipal currentUser, IServiceScope scope)
    {
        try
        {
            var notificationService = scope.ServiceProvider.GetService<IAuthStateNotificationService>();
            var tokenInfoService = scope.ServiceProvider.GetService<ITokenInfoService>();
            
            if (notificationService != null && tokenInfoService != null && !string.IsNullOrEmpty(newTokens.AccessToken))
            {
                // Get user ID from claims for SignalR notification with validation
                var userId = GetUserIdentifier(currentUser);
                
                if (!string.IsNullOrEmpty(userId))
                {
                    // Validate user context before sending SignalR notification
                    if (ValidateUserContextForNotification(currentUser, userId))
                    {
                        // Parse the new token info for the notification
                        var tokenInfo = tokenInfoService.GetTokenInfo(newTokens.AccessToken);
                        if (tokenInfo != null)
                        {
                            _logger.LogInformation("Sending SignalR notification for automatic token refresh to user: {UserId}", userId);
                            await notificationService.NotifyTokenRefreshedAsync(userId, tokenInfo);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("User context validation failed for SignalR notification to user: {UserId}", userId);
                        LogSuspiciousActivity(userId, "Failed user context validation for SignalR token refresh notification");
                    }
                }
                else
                {
                    _logger.LogWarning("Could not determine user ID for SignalR notification - user identification failed");
                    LogSuspiciousActivity("UNKNOWN", "SignalR notification failed due to missing user identification");
                }
            }
            else
            {
                _logger.LogDebug("SignalR notification services not available or token is empty");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending SignalR notification for automatic token refresh");
        }
    }

    private string? GetUserIdentifier(ClaimsPrincipal user)
    {
        if (user?.Identity?.IsAuthenticated != true)
        {
            _logger.LogDebug("User is not authenticated - cannot get user identifier");
            return null;
        }
        
        // Try multiple claim types that could serve as user identifier, with validation
        var identifier = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? user.FindFirst(ClaimTypes.Name)?.Value
            ?? user.FindFirst(ClaimTypes.Email)?.Value
            ?? user.FindFirst("sub")?.Value
            ?? user.FindFirst("email")?.Value
            ?? user.FindFirst("preferred_username")?.Value;
        
        if (string.IsNullOrWhiteSpace(identifier))
        {
            _logger.LogWarning("Failed to resolve user identifier. Available claims: {Claims}", 
                string.Join(", ", user.Claims.Select(c => $"{c.Type}={c.Value.Substring(0, Math.Min(c.Value.Length, 10))}...")));
            LogSuspiciousActivity("UNKNOWN", "User authentication state missing valid identifier claims");
            return null;
        }
        
        // Validate identifier format (basic security check)
        if (identifier.Length > 256 || identifier.Contains("<") || identifier.Contains(">") || identifier.Contains("script"))
        {
            _logger.LogWarning("Invalid user identifier format detected: {IdentifierPrefix}", identifier.Substring(0, Math.Min(identifier.Length, 20)));
            LogSuspiciousActivity(identifier, "Potentially malicious user identifier format detected");
            return null;
        }
        
        return identifier;
    }

    private string? GetUserIdFromContext(HttpContext httpContext)
    {
        if (httpContext.User?.Identity?.IsAuthenticated == true)
        {
            return GetUserIdentifier(httpContext.User);
        }
        return null;
    }

    private void CachePrincipalForUser(string userId, ClaimsPrincipal principal)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext != null)
        {
            httpContext.Items[$"CachedPrincipal_{userId}"] = principal;
        }
    }

    private void ClearCachedPrincipalForUser(string userId)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext != null)
        {
            httpContext.Items.Remove($"CachedPrincipal_{userId}");
        }
    }
    
    private bool ValidateRefreshTokenOwnership(string refreshToken, string userId)
    {
        try
        {
            // Parse the refresh token to validate it belongs to the current user
            var handler = new JwtSecurityTokenHandler();
            
            // Check if the token can be read as JWT (refresh tokens might not always be JWT)
            if (!handler.CanReadToken(refreshToken))
            {
                // If not a JWT, we'll rely on the API service to validate ownership
                _logger.LogDebug("Refresh token is not a JWT, ownership validation will be done by API service");
                return true;
            }
            
            var jwtToken = handler.ReadJwtToken(refreshToken);
            var tokenUserId = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier || c.Type == "sub")?.Value;
            
            if (string.IsNullOrEmpty(tokenUserId))
            {
                _logger.LogWarning("Refresh token does not contain user identifier claim");
                return false;
            }
            
            var ownershipValid = string.Equals(tokenUserId, userId, StringComparison.Ordinal);
            if (!ownershipValid)
            {
                _logger.LogWarning("Refresh token user ID mismatch - token: {TokenUserId}, current: {CurrentUserId}", 
                    tokenUserId.Substring(0, Math.Min(tokenUserId.Length, 10)) + "...", 
                    userId.Substring(0, Math.Min(userId.Length, 10)) + "...");
            }
            
            return ownershipValid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating refresh token ownership for user: {UserId}", userId);
            return false;
        }
    }
    
    private bool ValidateNewTokenOwnership(string accessToken, string expectedUserId)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(accessToken);
            
            var tokenUserId = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier || c.Type == "sub")?.Value;
            
            if (string.IsNullOrEmpty(tokenUserId))
            {
                _logger.LogWarning("New access token does not contain user identifier claim");
                return false;
            }
            
            var ownershipValid = string.Equals(tokenUserId, expectedUserId, StringComparison.Ordinal);
            if (!ownershipValid)
            {
                _logger.LogError("New access token user ID mismatch - token: {TokenUserId}, expected: {ExpectedUserId}", 
                    tokenUserId.Substring(0, Math.Min(tokenUserId.Length, 10)) + "...", 
                    expectedUserId.Substring(0, Math.Min(expectedUserId.Length, 10)) + "...");
            }
            
            return ownershipValid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating new access token ownership for user: {ExpectedUserId}", expectedUserId);
            return false;
        }
    }
    
    private bool ValidateUserContextForNotification(ClaimsPrincipal user, string userId)
    {
        if (user?.Identity?.IsAuthenticated != true)
        {
            _logger.LogWarning("User context validation failed - user is not authenticated");
            return false;
        }
        
        // Validate that the user ID matches what we expect from the principal
        var principalUserId = GetUserIdentifier(user);
        if (string.IsNullOrEmpty(principalUserId) || !string.Equals(principalUserId, userId, StringComparison.Ordinal))
        {
            _logger.LogWarning("User context validation failed - principal user ID mismatch");
            return false;
        }
        
        // Additional validation: check for required claims
        var hasRequiredClaims = user.HasClaim(ClaimTypes.NameIdentifier, userId) || 
                               user.HasClaim("sub", userId) || 
                               user.HasClaim(ClaimTypes.Name, userId) || 
                               user.HasClaim(ClaimTypes.Email, userId);
        
        if (!hasRequiredClaims)
        {
            _logger.LogWarning("User context validation failed - missing required claims for user: {UserId}", userId);
            return false;
        }
        
        return true;
    }
    
    private void LogSuspiciousActivity(string userId, string activity)
    {
        lock (_auditLock)
        {
            var key = $"{userId}_{activity}";
            var now = DateTime.UtcNow;
            
            if (!_tokenUsageAttempts.TryGetValue(key, out var attempts))
            {
                attempts = [];
                _tokenUsageAttempts[key] = attempts;
            }
            
            attempts.Add(now);
            
            // Clean old attempts (older than 1 hour)
            attempts.RemoveAll(a => now - a > TimeSpan.FromHours(1));
            
            // Log suspicious patterns
            if (attempts.Count >= 3)
            {
                _logger.LogError("SECURITY ALERT: Repeated suspicious activity detected - User: {UserId}, Activity: {Activity}, Attempts: {Count} in last hour", 
                    userId, activity, attempts.Count);
            }
            else
            {
                _logger.LogWarning("Suspicious activity detected - User: {UserId}, Activity: {Activity}", userId, activity);
            }
        }
    }
}