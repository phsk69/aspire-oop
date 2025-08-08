using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;

namespace AspireDeezNuts.Web.Services;

public interface ITokenInfoService
{
    TokenInfo? GetTokenInfo(string? token);
    bool IsTokenExpired(string? token);
    TimeSpan? GetTimeUntilExpiry(string? token);
}

public class TokenInfoService : ITokenInfoService
{
    public TokenInfo? GetTokenInfo(string? token)
    {
        if (string.IsNullOrEmpty(token))
            return null;

        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jsonToken = handler.ReadJwtToken(token);

            var userEmail = jsonToken.Claims.FirstOrDefault(x => x.Type == ClaimTypes.Name)?.Value;
            var userId = jsonToken.Claims.FirstOrDefault(x => x.Type == ClaimTypes.NameIdentifier)?.Value;
            var roles = jsonToken.Claims.Where(x => x.Type == ClaimTypes.Role).Select(x => x.Value).ToList();

            // Handle issued at time - JWT uses Unix timestamp
            var issuedAt = jsonToken.ValidFrom;
            var iatClaim = jsonToken.Claims.FirstOrDefault(x => x.Type == "iat")?.Value;
            if (iatClaim != null && long.TryParse(iatClaim, out var iatUnix))
            {
                issuedAt = DateTimeOffset.FromUnixTimeSeconds(iatUnix).DateTime;
            }

            return new TokenInfo
            {
                UserEmail = userEmail,
                UserId = userId,
                Roles = roles,
                IssuedAt = issuedAt,
                ExpiresAt = jsonToken.ValidTo,
                Issuer = jsonToken.Issuer,
                Audience = jsonToken.Audiences.FirstOrDefault(),
                Claims = jsonToken.Claims.ToDictionary(c => c.Type, c => c.Value)
            };
        }
        catch (Exception)
        {
            return null;
        }
    }

    public bool IsTokenExpired(string? token)
    {
        var tokenInfo = GetTokenInfo(token);
        if (tokenInfo == null)
            return true;

        return DateTime.UtcNow >= tokenInfo.ExpiresAt;
    }

    public TimeSpan? GetTimeUntilExpiry(string? token)
    {
        var tokenInfo = GetTokenInfo(token);
        if (tokenInfo == null)
            return null;

        var timeRemaining = tokenInfo.ExpiresAt - DateTime.UtcNow;
        return timeRemaining > TimeSpan.Zero ? timeRemaining : TimeSpan.Zero;
    }
}

public class TokenInfo
{
    public string? UserEmail { get; set; }
    public string? UserId { get; set; }
    public List<string> Roles { get; set; } = new();
    public DateTime IssuedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public string? Issuer { get; set; }
    public string? Audience { get; set; }
    public Dictionary<string, string> Claims { get; set; } = new();

    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    public TimeSpan TimeUntilExpiry => ExpiresAt - DateTime.UtcNow > TimeSpan.Zero 
        ? ExpiresAt - DateTime.UtcNow 
        : TimeSpan.Zero;
}