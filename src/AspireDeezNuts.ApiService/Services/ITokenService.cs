using Microsoft.AspNetCore.Identity;

namespace AspireDeezNuts.ApiService.Services;

public interface ITokenService
{
    Task<TokenResponse> GenerateTokensAsync(IdentityUser user);
    Task<TokenResponse?> RefreshTokensAsync(string refreshToken);
    Task RevokeRefreshTokenAsync(string refreshToken);
}

public class TokenResponse
{
    public required string AccessToken { get; set; }
    public required string RefreshToken { get; set; }
    public DateTime AccessTokenExpiry { get; set; }
    public DateTime RefreshTokenExpiry { get; set; }
    public string? UserId { get; set; }
}