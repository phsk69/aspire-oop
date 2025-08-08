using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AspireDeezNuts.ApiService.Data;
using AspireDeezNuts.Shared.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace AspireDeezNuts.ApiService.Services;

public class JwtTokenService(
    IConfiguration configuration,
    UserManager<IdentityUser> userManager,
    AppIdentityDbContext dbContext,
    ILogger<JwtTokenService> logger) : ITokenService
{
    private readonly IConfiguration _configuration = configuration;
    private readonly UserManager<IdentityUser> _userManager = userManager;
    private readonly AppIdentityDbContext _dbContext = dbContext;
    private readonly ILogger<JwtTokenService> _logger = logger;

    public async Task<TokenResponse> GenerateTokensAsync(IdentityUser user)
    {
        var (Token, Expiry) = await GenerateAccessTokenAsync(user);
        var refreshToken = await GenerateRefreshTokenAsync(user);

        return new TokenResponse
        {
            AccessToken = Token,
            RefreshToken = refreshToken.Token,
            AccessTokenExpiry = Expiry,
            RefreshTokenExpiry = refreshToken.Expiry,
            UserId = user.Id
        };
    }

    public async Task<TokenResponse?> RefreshTokensAsync(string refreshToken)
    {
        var storedToken = await _dbContext.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == refreshToken && !rt.IsRevoked && rt.ExpiresAt > DateTime.UtcNow);

        if (storedToken == null)
        {
            _logger.LogWarning("Invalid or expired refresh token attempted");
            return null;
        }

        var user = await _userManager.FindByIdAsync(storedToken.UserId);
        if (user == null)
        {
            _logger.LogWarning("User not found for refresh token");
            return null;
        }

        // Revoke old refresh token
        storedToken.IsRevoked = true;
        storedToken.RevokedAt = DateTime.UtcNow;

        // Generate new tokens
        var newTokens = await GenerateTokensAsync(user);

        await _dbContext.SaveChangesAsync();

        return newTokens;
    }

    public async Task RevokeRefreshTokenAsync(string refreshToken)
    {
        var token = await _dbContext.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == refreshToken);

        if (token != null && !token.IsRevoked)
        {
            token.IsRevoked = true;
            token.RevokedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();
        }
    }

    private async Task<(string Token, DateTime Expiry)> GenerateAccessTokenAsync(IdentityUser user)
    {
        var jwtSettings = _configuration.GetSection("Jwt");
        var secretKey = jwtSettings["Secret"] ?? throw new InvalidOperationException("JWT Secret not configured");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.UserName ?? string.Empty),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };

        // Add roles to claims
        var roles = await _userManager.GetRolesAsync(user);
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var expiry = DateTime.UtcNow.AddMinutes(Convert.ToDouble(jwtSettings["AccessTokenExpiryMinutes"] ?? "15"));

        var token = new JwtSecurityToken(
            issuer: jwtSettings["Issuer"],
            audience: jwtSettings["Audience"],
            claims: claims,
            expires: expiry,
            signingCredentials: credentials
        );

        return (new JwtSecurityTokenHandler().WriteToken(token), expiry);
    }

    private async Task<(string Token, DateTime Expiry)> GenerateRefreshTokenAsync(IdentityUser user)
    {
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        var refreshToken = Convert.ToBase64String(randomBytes);

        var expiry = DateTime.UtcNow.AddDays(Convert.ToDouble(_configuration["Jwt:RefreshTokenExpiryDays"] ?? "7"));

        var token = new RefreshToken
        {
            Token = refreshToken,
            UserId = user.Id,
            ExpiresAt = expiry,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.RefreshTokens.Add(token);
        await _dbContext.SaveChangesAsync();

        return (refreshToken, expiry);
    }
}