using System.Text.Json;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using AspireDeezNuts.Shared.Models;

namespace AspireDeezNuts.Web.Services;

public interface IAuthService
{
    Task<LoginResult> LoginAsync(LoginRequest request);
    Task LogoutAsync();
    Task<string?> GetTokenAsync();
    Task<string?> RefreshTokenAsync();
    Task<LoginResponse?> GetLoginResponseAsync();
    Task<string?> RefreshTokenAsync(string refreshToken);
    Task<LoginResponse?> RefreshTokensAsync(string refreshToken);
}

public class AuthService(HttpClient httpClient, ILogger<AuthService> logger, IJsonSerializationService jsonService, IHttpContextAccessor httpContextAccessor, IAuthStateNotificationService authStateNotificationService, ITokenInfoService tokenInfoService, AuthenticationStateProvider authStateProvider) : IAuthService
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly ILogger<AuthService> _logger = logger;
    private readonly IJsonSerializationService _jsonService = jsonService;
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;
    private readonly IAuthStateNotificationService _authStateNotificationService = authStateNotificationService;
    private readonly ITokenInfoService _tokenInfoService = tokenInfoService;
    private readonly AuthenticationStateProvider _authStateProvider = authStateProvider;

    public async Task<LoginResult> LoginAsync(LoginRequest request)
    {
        try
        {
            _logger.LogInformation("Sending login request to API for user: {Email}", request.Email);
            var response = await _httpClient.PostAsJsonAsync("api/v1/auth/login", request);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var loginResponse = _jsonService.Deserialize<LoginResponse>(content);

                if (loginResponse != null)
                {
                    // Store tokens in HTTP context for request-scoped caching
                    var httpContext = _httpContextAccessor.HttpContext;
                    if (httpContext != null)
                    {
                        httpContext.Items["CachedAccessToken"] = loginResponse.AccessToken;
                        httpContext.Items["CachedRefreshToken"] = loginResponse.RefreshToken;
                        httpContext.Items["CachedLoginResponse"] = loginResponse;
                    }
                    _logger.LogInformation("User logged in successfully");
                    return new LoginResult { Success = true };
                }
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                return new LoginResult { Success = false, ErrorMessage = "Invalid email or password" };
            }

            return new LoginResult { Success = false, ErrorMessage = "Login failed" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Login error for user: {Email}", request.Email);
            return new LoginResult { Success = false, ErrorMessage = "An error occurred during login" };
        }
    }

    public async Task LogoutAsync()
    {
        try
        {
            // Get cached refresh token from request scope
            var httpContext = _httpContextAccessor.HttpContext;
            string? cachedRefreshToken = null;
            if (httpContext != null)
            {
                cachedRefreshToken = httpContext.Items["CachedRefreshToken"] as string;
            }
            
            // If no cached token, get from user claims
            if (string.IsNullOrEmpty(cachedRefreshToken) && httpContext?.User?.Identity?.IsAuthenticated == true)
            {
                cachedRefreshToken = httpContext.User.FindFirst("refresh_token")?.Value;
            }

            // Attempt to revoke the refresh token on the server
            if (!string.IsNullOrEmpty(cachedRefreshToken))
            {
                await _httpClient.PostAsJsonAsync("api/v1/auth/logout", new LogoutRequest 
                { 
                    RefreshToken = cachedRefreshToken 
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during logout API call");
        }
        finally
        {
            // Clear request-scoped cache
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext != null)
            {
                httpContext.Items.Remove("CachedAccessToken");
                httpContext.Items.Remove("CachedRefreshToken");
                httpContext.Items.Remove("CachedLoginResponse");
            }
            _logger.LogInformation("User logged out");
        }
    }

    public async Task<string?> GetTokenAsync()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext != null)
        {
            // First check request-scoped cache
            var cachedToken = httpContext.Items["CachedAccessToken"] as string;
            if (!string.IsNullOrEmpty(cachedToken))
            {
                return await Task.FromResult(cachedToken);
            }
            
            // Otherwise, get from current user claims in cookie
            if (httpContext.User?.Identity?.IsAuthenticated == true)
            {
                var accessTokenClaim = httpContext.User.FindFirst("access_token");
                if (accessTokenClaim != null)
                {
                    return await Task.FromResult(accessTokenClaim.Value);
                }
            }
        }
        
        return await Task.FromResult<string?>(null);
    }

    public async Task<LoginResponse?> GetLoginResponseAsync()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext != null)
        {
            var cachedResponse = httpContext.Items["CachedLoginResponse"] as LoginResponse;
            if (cachedResponse != null)
            {
                return await Task.FromResult(cachedResponse);
            }
        }
        return await Task.FromResult<LoginResponse?>(null);
    }

    public async Task<string?> RefreshTokenAsync()
    {
        try
        {
            _logger.LogInformation("Attempting to refresh JWT token using refresh token");
            
            // Get the refresh token from current user claims in the cookie
            var currentRefreshToken = GetCurrentRefreshToken();
            
            if (string.IsNullOrEmpty(currentRefreshToken))
            {
                _logger.LogWarning("No refresh token available in current user claims for refresh");
                return null;
            }

            _logger.LogInformation("Found refresh token in user claims, proceeding with refresh");

            var refreshRequest = new RefreshTokenRequest
            {
                RefreshToken = currentRefreshToken
            };

            var response = await _httpClient.PostAsJsonAsync("api/v1/auth/refresh", refreshRequest);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var refreshResponse = _jsonService.Deserialize<LoginResponse>(content);

                if (refreshResponse != null && !string.IsNullOrEmpty(refreshResponse.AccessToken))
                {
                    // Store refreshed tokens in request-scoped cache
                    var httpContext = _httpContextAccessor.HttpContext;
                    if (httpContext != null)
                    {
                        httpContext.Items["CachedAccessToken"] = refreshResponse.AccessToken;
                        httpContext.Items["CachedRefreshToken"] = refreshResponse.RefreshToken;
                        httpContext.Items["CachedLoginResponse"] = refreshResponse;
                    }
                    
                    // Force update the authentication state with new tokens first
                    var currentUser = _httpContextAccessor.HttpContext?.User;
                    if (currentUser != null && _authStateProvider is CustomRevalidatingAuthenticationStateProvider customProvider)
                    {
                        await customProvider.UpdateAuthenticationStateWithNewTokens(refreshResponse, currentUser);
                        _logger.LogInformation("Authentication state provider updated with new tokens");
                    }
                    
                    // Try to update the authentication cookie for manual refreshes (if possible)
                    await UpdateCookieAuthenticationAsync(refreshResponse);
                    
                    // Notify connected clients about the token refresh via SignalR
                    await NotifyTokenRefreshViaSignalR(refreshResponse);
                    
                    _logger.LogInformation("Token refreshed successfully - tokens cached, auth state updated, and SignalR notification sent");
                    return refreshResponse.AccessToken;
                }
            }
            else
            {
                _logger.LogWarning("Token refresh failed with status: {StatusCode}", response.StatusCode);
                // Clear request-scoped cache if refresh fails (likely expired)
                var httpContext = _httpContextAccessor.HttpContext;
                if (httpContext != null)
                {
                    httpContext.Items.Remove("CachedAccessToken");
                    httpContext.Items.Remove("CachedRefreshToken");
                    httpContext.Items.Remove("CachedLoginResponse");
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing token");
            return null;
        }
    }

    public async Task<string?> RefreshTokenAsync(string refreshToken)
    {
        try
        {
            _logger.LogInformation("Attempting to refresh JWT token using provided refresh token");
            
            var refreshRequest = new RefreshTokenRequest
            {
                RefreshToken = refreshToken
            };

            var response = await _httpClient.PostAsJsonAsync("api/v1/auth/refresh", refreshRequest);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var refreshResponse = _jsonService.Deserialize<LoginResponse>(content);

                if (refreshResponse != null && !string.IsNullOrEmpty(refreshResponse.AccessToken))
                {
                    _logger.LogInformation("Token refreshed successfully with provided refresh token");
                    return refreshResponse.AccessToken;
                }
            }
            else
            {
                _logger.LogWarning("Token refresh failed with status: {StatusCode}", response.StatusCode);
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing token with provided refresh token");
            return null;
        }
    }

    public async Task<LoginResponse?> RefreshTokensAsync(string refreshToken)
    {
        try
        {
            _logger.LogInformation("Attempting to refresh both access and refresh tokens using provided refresh token");
            
            var refreshRequest = new RefreshTokenRequest
            {
                RefreshToken = refreshToken
            };

            var response = await _httpClient.PostAsJsonAsync("api/v1/auth/refresh", refreshRequest);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var refreshResponse = _jsonService.Deserialize<LoginResponse>(content);

                if (refreshResponse != null && !string.IsNullOrEmpty(refreshResponse.AccessToken))
                {
                    _logger.LogInformation("Both access and refresh tokens refreshed successfully");
                    return refreshResponse;
                }
            }
            else
            {
                _logger.LogWarning("Token refresh failed with status: {StatusCode}", response.StatusCode);
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing tokens with provided refresh token");
            return null;
        }
    }


    private string? GetCurrentRefreshToken()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext != null)
        {
            // First check request-scoped cache for refreshed token
            var cachedRefreshToken = httpContext.Items["CachedRefreshToken"] as string;
            if (!string.IsNullOrEmpty(cachedRefreshToken))
            {
                _logger.LogInformation("Using cached refresh token from recent refresh");
                return cachedRefreshToken;
            }
            
            // Otherwise, get from current user claims in cookie
            if (httpContext.User?.Identity?.IsAuthenticated == true)
            {
                var refreshTokenClaim = httpContext.User.FindFirst("refresh_token");
                if (refreshTokenClaim != null)
                {
                    _logger.LogInformation("Found refresh_token claim in current user");
                    return refreshTokenClaim.Value;
                }
                else
                {
                    _logger.LogWarning("No refresh_token claim found in current user. Available claims: {Claims}", 
                        string.Join(", ", httpContext.User.Claims.Select(c => c.Type)));
                }
            }
            else
            {
                _logger.LogWarning("User is not authenticated");
            }
        }
        else
        {
            _logger.LogWarning("HttpContext is null");
        }
        
        return null;
    }

    private async Task UpdateCookieAuthenticationAsync(LoginResponse newTokens)
    {
        try
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null || !httpContext.User.Identity?.IsAuthenticated == true)
            {
                _logger.LogWarning("HttpContext is null or user not authenticated, cannot update authentication cookie");
                return;
            }

            // Check if response has already started - can't update cookies if it has
            if (httpContext.Response.HasStarted)
            {
                _logger.LogDebug("Response already started, deferring cookie update to next request");
                return;
            }

            var currentUser = httpContext.User;

            // Parse the new JWT token to get updated claims
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
            _logger.LogInformation("Authentication cookie updated with new tokens in AuthService");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating authentication cookie in AuthService");
        }
    }

    private async Task NotifyTokenRefreshViaSignalR(LoginResponse newTokens)
    {
        try
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext?.User?.Identity?.IsAuthenticated != true)
            {
                _logger.LogWarning("User not authenticated, cannot send SignalR notification");
                return;
            }

            var tokenInfo = _tokenInfoService.GetTokenInfo(newTokens.AccessToken);
            if (tokenInfo != null)
            {
                await _authStateNotificationService.NotifyTokenRefreshedAsync(
                    tokenInfo.UserId ?? httpContext.User.Identity.Name ?? "unknown",
                    tokenInfo);
                
                _logger.LogInformation("SignalR notification sent for token refresh to user: {UserId}", tokenInfo.UserId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending SignalR notification for token refresh");
        }
    }
}

public class LoginResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}