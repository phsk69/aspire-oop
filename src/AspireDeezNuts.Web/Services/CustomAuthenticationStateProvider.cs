using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Components.Authorization;

namespace AspireDeezNuts.Web.Services;

public class CustomAuthenticationStateProvider(IAuthService authService, ILogger<CustomAuthenticationStateProvider> logger) : AuthenticationStateProvider
{
    private readonly IAuthService _authService = authService;
    private readonly ILogger<CustomAuthenticationStateProvider> _logger = logger;

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        try
        {
            _logger.LogInformation("Getting authentication state...");
            var token = await _authService.GetTokenAsync();

            if (string.IsNullOrEmpty(token))
            {
                _logger.LogInformation("No token found, returning anonymous user");
                return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
            }

            _logger.LogInformation("Token found, parsing claims...");
            var claims = ParseClaimsFromJwt(token);
            var identity = new ClaimsIdentity(claims, "jwt");
            var user = new ClaimsPrincipal(identity);

            _logger.LogInformation("Authentication state created for user: {UserName}", user.Identity?.Name ?? "Unknown");
            return new AuthenticationState(user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting authentication state");
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
        }
    }

    public Task NotifyUserAuthenticationAsync(string? token)
    {
        try
        {
            ClaimsPrincipal authenticatedUser;

            if (string.IsNullOrEmpty(token))
            {
                _logger.LogInformation("Notifying authentication state change: no token - anonymous user");
                authenticatedUser = new ClaimsPrincipal(new ClaimsIdentity());
            }
            else
            {
                _logger.LogInformation("Notifying authentication state change: parsing token...");
                var claims = ParseClaimsFromJwt(token);
                var identity = new ClaimsIdentity(claims, "jwt");
                authenticatedUser = new ClaimsPrincipal(identity);
                _logger.LogInformation("Notifying authentication state change for user: {UserName}", authenticatedUser.Identity?.Name ?? "Unknown");
            }

            var authState = Task.FromResult(new AuthenticationState(authenticatedUser));
            NotifyAuthenticationStateChanged(authState);
            _logger.LogInformation("Authentication state change notification sent successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error notifying authentication state change");
        }

        return Task.CompletedTask;
    }

    public Task NotifyUserLogoutAsync()
    {
        try
        {
            _logger.LogInformation("Notifying logout - setting anonymous user");
            var authState = Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity())));
            NotifyAuthenticationStateChanged(authState);
            _logger.LogInformation("Logout notification sent successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error notifying logout");
        }

        return Task.CompletedTask;
    }

    private static IEnumerable<Claim> ParseClaimsFromJwt(string jwt)
    {
        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(jwt);
        return token.Claims;
    }
}