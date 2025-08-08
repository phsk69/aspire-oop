using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.Extensions.Options;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using AspireDeezNuts.Shared.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace AspireDeezNuts.Web.Services;

public class CustomRevalidatingAuthenticationStateProvider(
    ILoggerFactory loggerFactory,
    IServiceScopeFactory scopeFactory,
    IOptions<AuthenticationOptions> authOptions) : RevalidatingServerAuthenticationStateProvider(loggerFactory)
{
    private readonly ILogger<CustomRevalidatingAuthenticationStateProvider> _logger = loggerFactory.CreateLogger<CustomRevalidatingAuthenticationStateProvider>();
    private readonly AuthenticationOptions _authOptions = authOptions.Value;
    private ClaimsPrincipal? _cachedPrincipal;

    protected override TimeSpan RevalidationInterval => TimeSpan.FromMinutes(_authOptions.TokenRefreshIntervalMinutes);

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        // If we have a cached principal from a recent token refresh, use it
        if (_cachedPrincipal != null)
        {
            _logger.LogInformation("Returning cached authentication state with updated tokens");
            return new AuthenticationState(_cachedPrincipal);
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
                _cachedPrincipal = null;
                
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
                
                // Try to refresh both tokens using the refresh token from claims
                var newTokens = await authService.RefreshTokensAsync(refreshTokenClaim.Value);
                
                if (newTokens != null && !string.IsNullOrEmpty(newTokens.AccessToken))
                {
                    _logger.LogInformation("Tokens refreshed successfully");
                    
                    // Update the cookie authentication principal first
                    var httpContextAccessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
                    var newPrincipal = await UpdateCookieAuthenticationAsync(newTokens, authenticationState.User, httpContextAccessor);
                    
                    if (newPrincipal != null)
                    {
                        // Cache the new principal so GetAuthenticationStateAsync returns it
                        _cachedPrincipal = newPrincipal;
                        
                        // Update the authentication state with the new principal
                        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(newPrincipal)));
                        
                        // Broadcast SignalR notification for automatic token refresh
                        await NotifyTokenRefreshViaSignalR(newTokens, newPrincipal, scope);
                        
                        _logger.LogInformation("Authentication cookie and state updated successfully, cached for future requests");
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
                    // Clear cached principal since token is invalid
                    _cachedPrincipal = null;
                    
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
            
            // Cache the new principal so GetAuthenticationStateAsync returns it
            _cachedPrincipal = newPrincipal;
            
            // Notify that authentication state changed
            NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(newPrincipal)));
            
            _logger.LogInformation("Authentication state updated with new token and cached for future requests");
            
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
            
            // Cache the new principal so GetAuthenticationStateAsync returns it
            _cachedPrincipal = newPrincipal;
            
            // Notify that authentication state changed
            NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(newPrincipal)));
            
            _logger.LogInformation("Authentication state updated with new access and refresh tokens and cached for future requests");
            
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
                _logger.LogWarning("Cannot update authentication cookie - response has already started. Token refresh succeeded but cookie not updated.");
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
                // Get user ID from claims for SignalR notification - try multiple claim types
                var userId = GetUserIdentifier(currentUser);
                
                if (!string.IsNullOrEmpty(userId))
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
                    _logger.LogWarning("Could not determine user ID for SignalR notification. Available claims: {Claims}", 
                        string.Join(", ", currentUser.Claims.Select(c => $"{c.Type}={c.Value}")));
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

    private static string? GetUserIdentifier(ClaimsPrincipal user)
    {
        // Try multiple claim types that could serve as user identifier
        return user.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? user.FindFirst(ClaimTypes.Name)?.Value
            ?? user.FindFirst(ClaimTypes.Email)?.Value
            ?? user.FindFirst("sub")?.Value
            ?? user.FindFirst("email")?.Value
            ?? user.FindFirst("preferred_username")?.Value;
    }
}