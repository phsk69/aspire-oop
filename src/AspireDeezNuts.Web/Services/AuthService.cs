using System.Text.Json;
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

public class AuthService(HttpClient httpClient, ILogger<AuthService> logger, IJsonSerializationService jsonService) : IAuthService
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly ILogger<AuthService> _logger = logger;
    private readonly IJsonSerializationService _jsonService = jsonService;
    private string? _cachedToken;
    private string? _cachedRefreshToken;
    private LoginResponse? _cachedLoginResponse;

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
                    _cachedToken = loginResponse.AccessToken;
                    _cachedRefreshToken = loginResponse.RefreshToken;
                    _cachedLoginResponse = loginResponse;
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
            // Attempt to revoke the refresh token on the server
            if (!string.IsNullOrEmpty(_cachedRefreshToken))
            {
                await _httpClient.PostAsJsonAsync("api/v1/auth/logout", new LogoutRequest 
                { 
                    RefreshToken = _cachedRefreshToken 
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during logout API call");
        }
        finally
        {
            _cachedToken = null;
            _cachedRefreshToken = null;
            _cachedLoginResponse = null;
            _logger.LogInformation("User logged out");
        }
    }

    public async Task<string?> GetTokenAsync()
    {
        return await Task.FromResult(_cachedToken);
    }

    public async Task<LoginResponse?> GetLoginResponseAsync()
    {
        return await Task.FromResult(_cachedLoginResponse);
    }

    public async Task<string?> RefreshTokenAsync()
    {
        try
        {
            _logger.LogInformation("Attempting to refresh JWT token using refresh token");
            
            if (string.IsNullOrEmpty(_cachedRefreshToken))
            {
                _logger.LogWarning("No cached refresh token available for refresh");
                return null;
            }

            var refreshRequest = new RefreshTokenRequest
            {
                RefreshToken = _cachedRefreshToken
            };

            var response = await _httpClient.PostAsJsonAsync("api/v1/auth/refresh", refreshRequest);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var refreshResponse = _jsonService.Deserialize<LoginResponse>(content);

                if (refreshResponse != null && !string.IsNullOrEmpty(refreshResponse.AccessToken))
                {
                    _cachedToken = refreshResponse.AccessToken;
                    _cachedRefreshToken = refreshResponse.RefreshToken; // Update refresh token too
                    _cachedLoginResponse = refreshResponse;
                    _logger.LogInformation("Token refreshed successfully");
                    return refreshResponse.AccessToken;
                }
            }
            else
            {
                _logger.LogWarning("Token refresh failed with status: {StatusCode}", response.StatusCode);
                // Clear tokens if refresh fails (likely expired)
                _cachedToken = null;
                _cachedRefreshToken = null;
                _cachedLoginResponse = null;
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
}

public class LoginResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}