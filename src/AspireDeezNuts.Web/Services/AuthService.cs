using System.Net.Http.Json;
using System.Text.Json;

namespace AspireDeezNuts.Web.Services;

public interface IAuthService
{
    Task<LoginResult> LoginAsync(string email, string password);
    Task LogoutAsync();
    Task<string?> GetTokenAsync();
}

public class AuthService(HttpClient httpClient, ILogger<AuthService> logger) : IAuthService
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly ILogger<AuthService> _logger = logger;
    private string? _cachedToken;

    public async Task<LoginResult> LoginAsync(string email, string password)
    {
        try
        {
            _logger.LogInformation("Sending login request to API for user: {Email}", email);
            var response = await _httpClient.PostAsJsonAsync("api/v1/auth/login", new { email, password });

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var loginResponse = JsonSerializer.Deserialize<LoginResponse>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

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
            _logger.LogError(ex, "Login error for user: {Email}", email);
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

public class LoginResponse
{
    public required string AccessToken { get; set; }
    public required string RefreshToken { get; set; }
    public int ExpiresIn { get; set; }
}