using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Identity;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using AspireDeezNuts.Web.Services;
using Microsoft.Extensions.Options;

namespace AspireDeezNuts.Web.Services;

public class CustomRevalidatingAuthenticationStateProvider : RevalidatingServerAuthenticationStateProvider
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CustomRevalidatingAuthenticationStateProvider> _logger;

    public CustomRevalidatingAuthenticationStateProvider(
        ILoggerFactory loggerFactory,
        IServiceScopeFactory scopeFactory)
        : base(loggerFactory)
    {
        _scopeFactory = scopeFactory;
        _logger = loggerFactory.CreateLogger<CustomRevalidatingAuthenticationStateProvider>();
    }

    protected override TimeSpan RevalidationInterval => TimeSpan.FromMinutes(5); // Check every 5 minutes

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
                
                using var scope = _scopeFactory.CreateScope();
                var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
                
                // Try to refresh the token
                var newToken = await authService.RefreshTokenAsync();
                
                if (!string.IsNullOrEmpty(newToken))
                {
                    _logger.LogInformation("Token refreshed successfully");
                    
                    // Update the authentication state with new token
                    await UpdateAuthenticationStateWithNewToken(newToken, authenticationState.User);
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
            
            _logger.LogInformation("Authentication state updated with new token");
            
            // Small delay to ensure state propagation
            await Task.Delay(1);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating authentication state with new token");
        }
    }
}