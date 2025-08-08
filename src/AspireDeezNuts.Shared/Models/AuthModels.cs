using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace AspireDeezNuts.Shared.Models;

public class LoginRequest
{
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email address format")]
    public required string Email { get; set; }

    [Required(ErrorMessage = "Password is required")]
    [DataType(DataType.Password)]
    public required string Password { get; set; }
}

public class LoginResponse
{
    [JsonPropertyName("accessToken")]
    public required string AccessToken { get; set; }
    
    [JsonPropertyName("refreshToken")]
    public required string RefreshToken { get; set; }
    
    [JsonPropertyName("expiresIn")]
    public int ExpiresIn { get; set; }
}

public class RefreshTokenRequest
{
    public required string RefreshToken { get; set; }
}

public class LogoutRequest
{
    public string? RefreshToken { get; set; }
}

public class RegisterRequest
{
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email address format")]
    [StringLength(256, ErrorMessage = "Email must be at most 256 characters")]
    public required string Email { get; set; }

    [Required(ErrorMessage = "Password is required")]
    [StringLength(64, MinimumLength = 8, ErrorMessage = "Password must be between 8 and 64 characters")]
    [DataType(DataType.Password)]
    public required string Password { get; set; }

    [RegularExpression("^(Admin|User)$", ErrorMessage = "Role must be either 'Admin' or 'User'")]
    public string? Role { get; set; }
}

public class AuthErrorResponse
{
    public int Status { get; set; }
    public string? Title { get; set; }
    public Dictionary<string, string[]>? Errors { get; set; }
    public string? TraceId { get; set; }
    public string? Message { get; set; }
}

public class AuthSuccessResponse
{
    [JsonPropertyName("message")]
    public string? Message { get; set; }
    
    [JsonPropertyName("userId")]
    public string? UserId { get; set; }
}

public class RefreshToken
{
    public int Id { get; set; }
    public required string Token { get; set; }
    public required string UserId { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsRevoked { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RevokedAt { get; set; }
}