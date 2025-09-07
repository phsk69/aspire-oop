using System.Net;
using AspireDeezNuts.Web.Services;
using AspireDeezNuts.Web.Tests.Helpers;
using AspireDeezNuts.Shared.Models;

namespace AspireDeezNuts.Web.Tests.Services;

[TestClass]
public class AuthServiceTests
{
    private TestHttpMessageHandler _httpMessageHandler = null!;
    private HttpClient _httpClient = null!;
    private TestHttpContextAccessor _httpContextAccessor = null!;
    private AuthService _authService = null!;
    private TestLogger<AuthService> _logger = null!;
    private TestJsonSerializationService _jsonService = null!;
    private TestAuthStateNotificationService _authStateNotificationService = null!;
    private TestTokenInfoService _tokenInfoService = null!;
    private TestCustomAuthenticationStateProvider _authStateProvider = null!;

    [TestInitialize]
    public void Setup()
    {
        _httpMessageHandler = new TestHttpMessageHandler();
        _httpClient = new HttpClient(_httpMessageHandler)
        {
            BaseAddress = new Uri("https://test-api/")
        };

        _httpContextAccessor = new TestHttpContextAccessor();
        _logger = new TestLogger<AuthService>();
        _jsonService = new TestJsonSerializationService();
        _authStateNotificationService = new TestAuthStateNotificationService();
        _tokenInfoService = new TestTokenInfoService();
        _authStateProvider = new TestCustomAuthenticationStateProvider();

        _authService = new AuthService(
            _httpClient,
            _logger,
            _jsonService,
            _httpContextAccessor,
            _authStateNotificationService,
            _tokenInfoService,
            _authStateProvider
        );
    }

    [TestCleanup]
    public void Cleanup()
    {
        _httpClient?.Dispose();
        _httpMessageHandler?.Dispose();
    }

    [TestMethod]
    public async Task LoginAsync_WithValidCredentials_ReturnsSuccess()
    {
        // Arrange
        var loginRequest = new LoginRequest
        {
            Email = "test@example.com",
            Password = "TestPass123!"
        };

        var loginResponse = new LoginResponse
        {
            AccessToken = "test-access-token",
            RefreshToken = "test-refresh-token",
            ExpiresIn = 3600
        };

        _httpMessageHandler.SetupResponse("/api/v1/auth/login", HttpStatusCode.OK, loginResponse);
        _httpContextAccessor.SetAnonymousUser();

        // Act
        var result = await _authService.LoginAsync(loginRequest);

        // Assert
        Assert.IsTrue(result.Success);
        Assert.IsNull(result.ErrorMessage);

        // Verify HTTP call was made
        Assert.HasCount(1, _httpMessageHandler.Requests);
        var request = _httpMessageHandler.Requests[0];
        Assert.AreEqual("/api/v1/auth/login", request.RequestUri?.PathAndQuery);
        Assert.AreEqual(HttpMethod.Post, request.Method);

        // Verify tokens were cached in HTTP context
        Assert.AreEqual("test-access-token", _httpContextAccessor.HttpContext?.Items["CachedAccessToken"]);
        Assert.AreEqual("test-refresh-token", _httpContextAccessor.HttpContext?.Items["CachedRefreshToken"]);
    }

    [TestMethod]
    public async Task LoginAsync_WithInvalidCredentials_ReturnsFailure()
    {
        // Arrange
        var loginRequest = new LoginRequest
        {
            Email = "test@example.com",
            Password = "WrongPassword"
        };

        _httpMessageHandler.SetupResponse("/api/v1/auth/login", HttpStatusCode.Unauthorized);
        _httpContextAccessor.SetAnonymousUser();

        // Act
        var result = await _authService.LoginAsync(loginRequest);

        // Assert
        Assert.IsFalse(result.Success);
        Assert.AreEqual("Invalid email or password", result.ErrorMessage);

        // Verify no tokens were cached
        Assert.IsNull(_httpContextAccessor.HttpContext?.Items["CachedAccessToken"]);
        Assert.IsNull(_httpContextAccessor.HttpContext?.Items["CachedRefreshToken"]);
    }

    [TestMethod]
    public async Task LoginAsync_WithServerError_ReturnsGenericFailure()
    {
        // Arrange
        var loginRequest = new LoginRequest
        {
            Email = "test@example.com",
            Password = "TestPass123!"
        };

        _httpMessageHandler.SetupResponse("/api/v1/auth/login", HttpStatusCode.InternalServerError);
        _httpContextAccessor.SetAnonymousUser();

        // Act
        var result = await _authService.LoginAsync(loginRequest);

        // Assert
        Assert.IsFalse(result.Success);
        Assert.AreEqual("Login failed", result.ErrorMessage);
    }

    [TestMethod]
    public async Task GetTokenAsync_WithCachedToken_ReturnsCachedToken()
    {
        // Arrange
        _httpContextAccessor.SetAnonymousUser();
        _httpContextAccessor.HttpContext!.Items["CachedAccessToken"] = "cached-token";

        // Act
        var token = await _authService.GetTokenAsync();

        // Assert
        Assert.AreEqual("cached-token", token);
    }

    [TestMethod]
    public async Task GetTokenAsync_WithAuthenticatedUserNoCachedToken_ReturnsTokenFromClaims()
    {
        // Arrange
        _httpContextAccessor.SetAuthenticatedUser(
            "test@example.com",
            "user123",
            ["User"],
            "claim-token"
        );

        // Act
        var token = await _authService.GetTokenAsync();

        // Assert
        Assert.AreEqual("claim-token", token);
    }

    [TestMethod]
    public async Task GetTokenAsync_WithNoAuthentication_ReturnsNull()
    {
        // Arrange
        _httpContextAccessor.SetAnonymousUser();

        // Act
        var token = await _authService.GetTokenAsync();

        // Assert
        Assert.IsNull(token);
    }

    [TestMethod]
    public async Task LogoutAsync_WithRefreshToken_CallsLogoutEndpoint()
    {
        // Arrange
        _httpContextAccessor.SetAuthenticatedUser(
            "test@example.com",
            "user123",
            ["User"],
            "access-token",
            "refresh-token"
        );

        _httpMessageHandler.SetupResponse("/api/v1/auth/logout", HttpStatusCode.OK);

        // Act
        await _authService.LogoutAsync();

        // Assert
        Assert.HasCount(1, _httpMessageHandler.Requests);
        var request = _httpMessageHandler.Requests[0];
        Assert.AreEqual("/api/v1/auth/logout", request.RequestUri?.PathAndQuery);
        Assert.AreEqual(HttpMethod.Post, request.Method);

        // Verify cache was cleared
        Assert.IsFalse(_httpContextAccessor.HttpContext?.Items.ContainsKey("CachedAccessToken") ?? true);
        Assert.IsFalse(_httpContextAccessor.HttpContext?.Items.ContainsKey("CachedRefreshToken") ?? true);
    }

    [TestMethod]
    public async Task RefreshTokenAsync_WithValidRefreshToken_ReturnsNewToken()
    {
        // Arrange
        var refreshResponse = new LoginResponse
        {
            AccessToken = "new-access-token",
            RefreshToken = "new-refresh-token",
            ExpiresIn = 3600
        };

        _httpContextAccessor.SetAuthenticatedUser(
            "test@example.com",
            "user123",
            ["User"],
            "old-token",
            "refresh-token"
        );

        _httpMessageHandler.SetupResponse("/api/v1/auth/refresh", HttpStatusCode.OK, refreshResponse);

        // Act
        var newToken = await _authService.RefreshTokenAsync();

        // Assert
        Assert.AreEqual("new-access-token", newToken);

        // Verify new tokens were cached
        Assert.AreEqual("new-access-token", _httpContextAccessor.HttpContext?.Items["CachedAccessToken"]);
        Assert.AreEqual("new-refresh-token", _httpContextAccessor.HttpContext?.Items["CachedRefreshToken"]);
    }

    [TestMethod]
    public async Task RefreshTokenAsync_WithExpiredRefreshToken_ReturnsNull()
    {
        // Arrange
        _httpContextAccessor.SetAuthenticatedUser(
            "test@example.com",
            "user123",
            ["User"],
            "old-token",
            "expired-refresh-token"
        );

        _httpMessageHandler.SetupResponse("/api/v1/auth/refresh", HttpStatusCode.Unauthorized);

        // Act
        var newToken = await _authService.RefreshTokenAsync();

        // Assert
        Assert.IsNull(newToken);

        // Verify cache was cleared on failure
        Assert.IsFalse(_httpContextAccessor.HttpContext?.Items.ContainsKey("CachedAccessToken") ?? true);
        Assert.IsFalse(_httpContextAccessor.HttpContext?.Items.ContainsKey("CachedRefreshToken") ?? true);
    }

    [TestMethod]
    public async Task RefreshTokenAsync_WithNoRefreshToken_ReturnsNull()
    {
        // Arrange
        _httpContextAccessor.SetAuthenticatedUser(
            "test@example.com",
            "user123",
            ["User"],
            "access-token"
        // No refresh token
        );

        // Act
        var newToken = await _authService.RefreshTokenAsync();

        // Assert
        Assert.IsNull(newToken);

        // Verify no HTTP calls were made
        Assert.IsEmpty(_httpMessageHandler.Requests);
    }
}