using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using AspireDeezNuts.Web.Services;
using AspireDeezNuts.Web.Tests.Helpers;

namespace AspireDeezNuts.Web.Tests.Services;

[TestClass]
public class AuthorizedHttpMessageHandlerTests
{
    private TestHttpMessageHandler _innerHandler = null!;
    private AuthorizedHttpMessageHandler _handler = null!;
    private TestHttpContextAccessor _httpContextAccessor = null!;
    private TestLogger<AuthorizedHttpMessageHandler> _logger = null!;
    private HttpClient _httpClient = null!;

    [TestInitialize]
    public void Setup()
    {
        _innerHandler = new TestHttpMessageHandler();
        _httpContextAccessor = new TestHttpContextAccessor();
        _logger = new TestLogger<AuthorizedHttpMessageHandler>();

        _handler = new AuthorizedHttpMessageHandler(_httpContextAccessor, _logger)
        {
            InnerHandler = _innerHandler
        };

        _httpClient = new HttpClient(_handler)
        {
            BaseAddress = new Uri("https://test-api/")
        };
    }

    [TestCleanup]
    public void Cleanup()
    {
        _httpClient?.Dispose();
        _handler?.Dispose();
        _innerHandler?.Dispose();
    }

    [TestMethod]
    public async Task SendAsync_WithAuthenticatedUserAndToken_AddsBearerToken()
    {
        // Arrange
        _httpContextAccessor.SetAuthenticatedUser(
            "test@example.com",
            "user123",
            ["User"],
            "test-bearer-token"
        );

        _innerHandler.SetupResponse("/api/test", HttpStatusCode.OK, new { message = "success" });

        // Act
        var response = await _httpClient.GetAsync("/api/test", TestContext.CancellationTokenSource.Token);

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.HasCount(1, _innerHandler.Requests);

        var request = _innerHandler.Requests[0];
        Assert.IsNotNull(request.Headers.Authorization);
        Assert.AreEqual("Bearer", request.Headers.Authorization.Scheme);
        Assert.AreEqual("test-bearer-token", request.Headers.Authorization.Parameter);
    }

    [TestMethod]
    public async Task SendAsync_WithAuthenticatedUserNoToken_DoesNotAddBearerToken()
    {
        // Arrange
        _httpContextAccessor.SetAuthenticatedUser(
            "test@example.com",
            "user123",
            ["User"]
            // No access token
        );

        _innerHandler.SetupResponse("/api/test", HttpStatusCode.OK, new { message = "success" });

        // Act
        var response = await _httpClient.GetAsync("/api/test", TestContext.CancellationTokenSource.Token);

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.HasCount(1, _innerHandler.Requests);

        var request = _innerHandler.Requests[0];
        Assert.IsNull(request.Headers.Authorization);
    }

    [TestMethod]
    public async Task SendAsync_WithAnonymousUser_DoesNotAddBearerToken()
    {
        // Arrange
        _httpContextAccessor.SetAnonymousUser();
        _innerHandler.SetupResponse("/api/test", HttpStatusCode.OK, new { message = "success" });

        // Act
        var response = await _httpClient.GetAsync("/api/test", TestContext.CancellationTokenSource.Token);

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.HasCount(1, _innerHandler.Requests);

        var request = _innerHandler.Requests[0];
        Assert.IsNull(request.Headers.Authorization);
    }

    [TestMethod]
    public async Task SendAsync_WithNullHttpContext_DoesNotAddBearerToken()
    {
        // Arrange
        _httpContextAccessor.HttpContext = null;
        _innerHandler.SetupResponse("/api/test", HttpStatusCode.OK, new { message = "success" });

        // Act
        var response = await _httpClient.GetAsync("/api/test", TestContext.CancellationTokenSource.Token);

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.HasCount(1, _innerHandler.Requests);

        var request = _innerHandler.Requests[0];
        Assert.IsNull(request.Headers.Authorization);
    }

    [TestMethod]
    public async Task SendAsync_LogsHttpContextStatus()
    {
        // Arrange
        _httpContextAccessor.SetAuthenticatedUser(
            "test@example.com",
            "user123",
            ["User"],
            "test-token"
        );

        _innerHandler.SetupResponse("/api/test", HttpStatusCode.OK);

        // Act
        await _httpClient.GetAsync("/api/test", TestContext.CancellationTokenSource.Token);

        // Assert
        var infoLogs = _logger.LogEntries.Where(l => l.LogLevel == LogLevel.Information).ToList();

        // Should log that HttpContext is not null
        Assert.IsTrue(infoLogs.Any(l => l.Message.Contains("HttpContext is null: False")));

        // Should log that user is authenticated
        Assert.IsTrue(infoLogs.Any(l => l.Message.Contains("User authenticated: True")));

        // Should log user name
        Assert.IsTrue(infoLogs.Any(l => l.Message.Contains("User name: test@example.com")));

        // Should log that Bearer token was added
        Assert.IsTrue(infoLogs.Any(l => l.Message.Contains("Added Bearer token to request")));

        // Should log response status
        Assert.IsTrue(infoLogs.Any(l => l.Message.Contains("Response status: OK")));
    }

    [TestMethod]
    public async Task SendAsync_LogsWarning_WhenNoTokenFound()
    {
        // Arrange
        _httpContextAccessor.SetAuthenticatedUser(
            "test@example.com",
            "user123",
            ["User"]
            // No access token
        );

        _innerHandler.SetupResponse("/api/test", HttpStatusCode.OK);

        // Act
        await _httpClient.GetAsync("/api/test", TestContext.CancellationTokenSource.Token);

        // Assert
        var warningLogs = _logger.LogEntries.Where(l => l.LogLevel == LogLevel.Warning).ToList();
        Assert.IsTrue(warningLogs.Any(l => l.Message.Contains("No JWT token found in user claims")));
    }

    [TestMethod]
    public async Task SendAsync_WithEmptyToken_DoesNotAddBearerToken()
    {
        // Arrange
        _httpContextAccessor.SetAuthenticatedUser(
            "test@example.com",
            "user123",
            ["User"],
            ""  // Empty token
        );

        _innerHandler.SetupResponse("/api/test", HttpStatusCode.OK);

        // Act
        var response = await _httpClient.GetAsync("/api/test", TestContext.CancellationTokenSource.Token);

        // Assert
        var request = _innerHandler.Requests[0];
        Assert.IsNull(request.Headers.Authorization);

        // Should log warning about no token
        var warningLogs = _logger.LogEntries.Where(l => l.LogLevel == LogLevel.Warning).ToList();
        Assert.IsTrue(warningLogs.Any(l => l.Message.Contains("No JWT token found in user claims")));
    }

    [TestMethod]
    public async Task SendAsync_WithWhitespaceToken_AddsBearerToken()
    {
        // Arrange
        var whitespaceToken = "   ";
        _httpContextAccessor.SetAuthenticatedUser(
            "test@example.com",
            "user123",
            ["User"],
            whitespaceToken
        );

        _innerHandler.SetupResponse("/api/test", HttpStatusCode.OK);

        // Act
        var response = await _httpClient.GetAsync("/api/test", TestContext.CancellationTokenSource.Token);

        // Assert
        var request = _innerHandler.Requests[0];
        Assert.IsNotNull(request.Headers.Authorization);
        Assert.AreEqual("Bearer", request.Headers.Authorization.Scheme);
        Assert.AreEqual(whitespaceToken, request.Headers.Authorization.Parameter);

        // Should log that Bearer token was added (since string.IsNullOrEmpty allows whitespace)
        var infoLogs = _logger.LogEntries.Where(l => l.LogLevel == LogLevel.Information).ToList();
        Assert.IsTrue(infoLogs.Any(l => l.Message.Contains("Added Bearer token to request")));
    }

    [TestMethod]
    public async Task SendAsync_PreservesExistingAuthorizationHeader()
    {
        // Arrange
        _httpContextAccessor.SetAuthenticatedUser(
            "test@example.com",
            "user123",
            ["User"],
            "new-token"
        );

        _innerHandler.SetupResponse("/api/test", HttpStatusCode.OK);

        // Act
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/test");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", "existing-auth");

        var response = await _httpClient.SendAsync(request, TestContext.CancellationTokenSource.Token);

        // Assert
        var actualRequest = _innerHandler.Requests[0];
        Assert.IsNotNull(actualRequest.Headers.Authorization);
        Assert.AreEqual("Bearer", actualRequest.Headers.Authorization.Scheme);
        Assert.AreEqual("new-token", actualRequest.Headers.Authorization.Parameter);
    }

    [TestMethod]
    public async Task SendAsync_ReturnsCorrectStatusCode()
    {
        // Arrange
        _httpContextAccessor.SetAuthenticatedUser(
            "test@example.com",
            "user123",
            ["User"],
            "test-token"
        );

        _innerHandler.SetupResponse("/api/test", HttpStatusCode.NotFound);

        // Act
        var response = await _httpClient.GetAsync("/api/test", TestContext.CancellationTokenSource.Token);

        // Assert
        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);

        // Should log response status
        var infoLogs = _logger.LogEntries.Where(l => l.LogLevel == LogLevel.Information).ToList();
        Assert.IsTrue(infoLogs.Any(l => l.Message.Contains("Response status: NotFound")));
    }

    public TestContext TestContext { get; set; }
}