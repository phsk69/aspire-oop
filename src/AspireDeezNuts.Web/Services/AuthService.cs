using System.Text.Json;
using AspireDeezNuts.Shared.Models;

namespace AspireDeezNuts.Web.Services;

public interface IAuthService
{
    Task<LoginResult> LoginAsync(LoginRequest request);
    Task LogoutAsync();
    Task<string?> GetTokenAsync();
}

public class AuthService(HttpClient httpClient, ILogger<AuthService> logger) : IAuthService
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly ILogger<AuthService> _logger = logger;
    private string? _cachedToken;

    public async Task<LoginResult> LoginAsync(LoginRequest request)
    {
        try
        {
            _logger.LogInformation("Sending login request to API for user: {Email}", request.Email);
            var response = await _httpClient.PostAsJsonAsync("api/v1/auth/login", request);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var loginResponse = JsonSerializer.Deserialize<LoginResponse>(content);

                if (loginResponse != null)
                {
                    _cachedToken = loginResponse.AccessToken;
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
            // Attempt to revoke the token on the server
            if (!string.IsNullOrEmpty(_cachedToken))
            {
                await _httpClient.PostAsJsonAsync("api/v1/auth/logout", new { });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during logout API call");
        }
        finally
        {
            _cachedToken = null;
            _logger.LogInformation("User logged out");
        }
    }

    public async Task<string?> GetTokenAsync()
    {
        return await Task.FromResult(_cachedToken);
    }
}

public class LoginResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}