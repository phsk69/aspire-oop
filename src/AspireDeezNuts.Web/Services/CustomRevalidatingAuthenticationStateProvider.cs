using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.Extensions.Options;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using AspireDeezNuts.Shared.Models;

namespace AspireDeezNuts.Web.Services;

public class CustomRevalidatingAuthenticationStateProvider(
    ILoggerFactory loggerFactory,
    IServiceScopeFactory scopeFactory,
    IOptions<AuthenticationOptions> authOptions) : RevalidatingServerAuthenticationStateProvider(loggerFactory)
{
    private readonly ILogger<CustomRevalidatingAuthenticationStateProvider> _logger = loggerFactory.CreateLogger<CustomRevalidatingAuthenticationStateProvider>();
    private readonly AuthenticationOptions _authOptions = authOptions.Value;

    protected override TimeSpan RevalidationInterval => TimeSpan.FromMinutes(_authOptions.TokenRefreshIntervalMinutes);

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
                    
                    // Update the authentication state with both new tokens
                    await UpdateAuthenticationStateWithNewTokens(newTokens, authenticationState.User);
                    return true;
                }
                else
                {
                    _logger.LogWarning("Token refresh failed");
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
            
            // Notify that authentication state changed
            NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(newPrincipal)));
            
            _logger.LogInformation("Authentication state updated with new token");
            
            // Small delay to ensure state propagation
            await Task.Delay(1);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating authentication state with new token");
        }
    }

    private async Task UpdateAuthenticationStateWithNewTokens(LoginResponse newTokens, ClaimsPrincipal currentUser)
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
            
            // Notify that authentication state changed
            NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(newPrincipal)));
            
            _logger.LogInformation("Authentication state updated with new access and refresh tokens");
            
            // Small delay to ensure state propagation
            await Task.Delay(1);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating authentication state with new tokens");
        }
    }
}