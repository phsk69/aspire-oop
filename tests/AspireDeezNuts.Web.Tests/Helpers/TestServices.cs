using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using BlazorBootstrap;
using AspireDeezNuts.Web.Services;
using AspireDeezNuts.Shared.Models;

namespace AspireDeezNuts.Web.Tests.Helpers;

public class TestJsonSerializationService : IJsonSerializationService
{
    private readonly JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public JsonSerializerOptions CamelCaseOptions => _options;

    public T? Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, _options);

    public string Serialize<T>(T value) => JsonSerializer.Serialize(value, _options);
}

public class TestAuthStateNotificationService : IAuthStateNotificationService
{
    public readonly List<(string UserId, TokenInfo TokenInfo)> TokenRefreshNotifications = [];
    public readonly List<(string UserId, bool IsAuthenticated)> AuthStateChangeNotifications = [];

    public Task NotifyTokenRefreshedAsync(string userId, TokenInfo tokenInfo)
    {
        TokenRefreshNotifications.Add((userId, tokenInfo));
        return Task.CompletedTask;
    }

    public Task NotifyAuthStateChangedAsync(string userId, bool isAuthenticated)
    {
        AuthStateChangeNotifications.Add((userId, isAuthenticated));
        return Task.CompletedTask;
    }
}

public class TestTokenInfoService : ITokenInfoService
{
    private readonly Dictionary<string, TokenInfo> _tokenInfos = [];

    public void SetTokenInfo(string token, TokenInfo info)
    {
        _tokenInfos[token] = info;
    }

    public TokenInfo? GetTokenInfo(string? token)
    {
        if (token == null) return null;

        _tokenInfos.TryGetValue(token, out var tokenInfo);
        return tokenInfo ?? new TokenInfo
        {
            UserId = "test-user",
            UserEmail = "test@example.com",
            Roles = ["User"],
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        };
    }

    public bool IsTokenExpired(string? token)
    {
        var tokenInfo = GetTokenInfo(token);
        return tokenInfo?.IsExpired ?? true;
    }

    public TimeSpan? GetTimeUntilExpiry(string? token)
    {
        var tokenInfo = GetTokenInfo(token);
        return tokenInfo?.TimeUntilExpiry;
    }
}

public class TestCustomAuthenticationStateProvider : AuthenticationStateProvider
{
    private AuthenticationState _currentState = new(new System.Security.Claims.ClaimsPrincipal());

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        return Task.FromResult(_currentState);
    }

    public void SetAuthenticationState(AuthenticationState state)
    {
        _currentState = state;
        NotifyAuthenticationStateChanged(Task.FromResult(state));
    }
}

public class TestAuthService : IAuthService
{
    private string? _token;
    public bool ShouldThrowException { get; set; }

    public void SetToken(string? token)
    {
        _token = token;
    }

    public async Task<LoginResult> LoginAsync(LoginRequest request)
    {
        await Task.CompletedTask;
        return new LoginResult { Success = true };
    }

    public async Task LogoutAsync()
    {
        await Task.CompletedTask;
    }

    public async Task<string?> GetTokenAsync()
    {
        if (ShouldThrowException)
        {
            throw new InvalidOperationException("Test exception");
        }
        return await Task.FromResult(_token);
    }

    public async Task<string?> RefreshTokenAsync()
    {
        return await Task.FromResult<string?>(null);
    }

    public async Task<LoginResponse?> GetLoginResponseAsync()
    {
        return await Task.FromResult<LoginResponse?>(null);
    }

    public async Task<string?> RefreshTokenAsync(string refreshToken)
    {
        return await Task.FromResult<string?>(null);
    }

    public async Task<LoginResponse?> RefreshTokensAsync(string refreshToken)
    {
        return await Task.FromResult<LoginResponse?>(null);
    }
}

public class TestNavigationManager : NavigationManager
{
    public TestNavigationManager() : base()
    {
        Initialize("https://localhost/", "https://localhost/");
    }

    public void SetUri(string uri)
    {
        var baseUri = "https://localhost/";
        var absoluteUri = uri.StartsWith("http") ? uri : new Uri(new Uri(baseUri), uri).ToString();

        // Use reflection to set the Uri property
        var uriProperty = typeof(NavigationManager).GetProperty("Uri");
        uriProperty?.SetValue(this, absoluteUri);
    }

    protected override void NavigateToCore(string uri, bool forceLoad)
    {
        SetUri(uri);
    }
}

public class TestToastService : IToastService
{
    public readonly List<ToastMessage> ToastMessages = [];

    public event Action<ToastMessage>? OnShow;

    public void ShowSuccess(string message, string? title = null)
    {
        var toast = new ToastMessage
        {
            Message = message,
            Title = title ?? "Success",
            Type = ToastType.Success,
            AutoHide = true
        };
        ToastMessages.Add(toast);
        OnShow?.Invoke(toast);
    }

    public void ShowInfo(string message, string? title = null)
    {
        var toast = new ToastMessage
        {
            Message = message,
            Title = title ?? "Information",
            Type = ToastType.Info,
            AutoHide = true
        };
        ToastMessages.Add(toast);
        OnShow?.Invoke(toast);
    }

    public void ShowWarning(string message, string? title = null)
    {
        var toast = new ToastMessage
        {
            Message = message,
            Title = title ?? "Warning",
            Type = ToastType.Warning,
            AutoHide = true
        };
        ToastMessages.Add(toast);
        OnShow?.Invoke(toast);
    }

    public void ShowError(string message, string? title = null)
    {
        var toast = new ToastMessage
        {
            Message = message,
            Title = title ?? "Error",
            Type = ToastType.Danger,
            AutoHide = true
        };
        ToastMessages.Add(toast);
        OnShow?.Invoke(toast);
    }

    public void Show(ToastMessage message)
    {
        ToastMessages.Add(message);
        OnShow?.Invoke(message);
    }
}