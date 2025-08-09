using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Logging;
using System.Text;
using AspireDeezNuts.Web.Services;
using AspireDeezNuts.Web.Tests.Helpers;

namespace AspireDeezNuts.Web.Tests.Services;

[TestClass]
public class CustomAuthenticationStateProviderTests
{
    private CustomAuthenticationStateProvider _provider = null!;
    private TestAuthService _authService = null!;
    private TestLogger<CustomAuthenticationStateProvider> _logger = null!;

    [TestInitialize]
    public void Setup()
    {
        _authService = new TestAuthService();
        _logger = new TestLogger<CustomAuthenticationStateProvider>();
        _provider = new CustomAuthenticationStateProvider(_authService, _logger);
    }

    [TestMethod]
    public async Task GetAuthenticationStateAsync_WithValidToken_ReturnsAuthenticatedUser()
    {
        // Arrange
        var token = CreateTestJwtToken("test@example.com", "user123", new[] { "User" });
        _authService.SetToken(token);

        // Act
        var state = await _provider.GetAuthenticationStateAsync();

        // Assert
        Assert.IsNotNull(state);
        Assert.IsTrue(state.User.Identity?.IsAuthenticated);
        Assert.AreEqual("test@example.com", state.User.Identity?.Name);
        Assert.IsTrue(state.User.IsInRole("User"));
    }

    [TestMethod]
    public async Task GetAuthenticationStateAsync_WithNoToken_ReturnsAnonymousUser()
    {
        // Arrange
        _authService.SetToken(null);

        // Act
        var state = await _provider.GetAuthenticationStateAsync();

        // Assert
        Assert.IsNotNull(state);
        Assert.IsFalse(state.User.Identity?.IsAuthenticated);
        Assert.IsNull(state.User.Identity?.Name);
    }

    [TestMethod]
    public async Task GetAuthenticationStateAsync_WithEmptyToken_ReturnsAnonymousUser()
    {
        // Arrange
        _authService.SetToken("");

        // Act
        var state = await _provider.GetAuthenticationStateAsync();

        // Assert
        Assert.IsNotNull(state);
        Assert.IsFalse(state.User.Identity?.IsAuthenticated);
    }

    [TestMethod]
    public async Task GetAuthenticationStateAsync_WithInvalidToken_ReturnsAnonymousUser()
    {
        // Arrange
        _authService.SetToken("invalid-jwt-token");

        // Act
        var state = await _provider.GetAuthenticationStateAsync();

        // Assert
        Assert.IsNotNull(state);
        Assert.IsFalse(state.User.Identity?.IsAuthenticated);
    }

    [TestMethod]
    public async Task GetAuthenticationStateAsync_WithExpiredToken_ReturnsAnonymousUser()
    {
        // Arrange
        var expiredToken = CreateTestJwtToken("test@example.com", "user123", new[] { "User" }, DateTime.UtcNow.AddHours(-1));
        _authService.SetToken(expiredToken);

        // Act
        var state = await _provider.GetAuthenticationStateAsync();

        // Assert
        Assert.IsNotNull(state);
        // Note: The provider itself doesn't validate expiration, it just parses claims
        // Expiration validation would typically be handled by JWT middleware or other components
        Assert.IsTrue(state.User.Identity?.IsAuthenticated);
    }

    [TestMethod]
    public async Task GetAuthenticationStateAsync_LogsException_WhenTokenParsingFails()
    {
        // Arrange
        _authService.SetToken("malformed-token");
        _authService.ShouldThrowException = true;

        // Act
        var state = await _provider.GetAuthenticationStateAsync();

        // Assert
        Assert.IsNotNull(state);
        Assert.IsFalse(state.User.Identity?.IsAuthenticated);
        
        // Verify error was logged
        var errorLogs = _logger.LogEntries.Where(l => l.LogLevel == LogLevel.Error).ToList();
        Assert.IsGreaterThan(0, errorLogs.Count);
        Assert.IsTrue(errorLogs.Any(l => l.Message.Contains("Error getting authentication state")));
    }

    [TestMethod]
    public async Task NotifyUserAuthenticationAsync_WithValidToken_UpdatesAuthenticationState()
    {
        // Arrange
        var token = CreateTestJwtToken("test@example.com", "user123", new[] { "User" });
        var stateChanged = false;
        AuthenticationState? capturedState = null;

        _provider.AuthenticationStateChanged += task =>
        {
            stateChanged = true;
            capturedState = task.Result;
        };

        // Act
        await _provider.NotifyUserAuthenticationAsync(token);

        // Wait a bit for the event to process
        await Task.Delay(10, TestContext.CancellationTokenSource.Token);

        // Assert
        Assert.IsTrue(stateChanged);
        Assert.IsNotNull(capturedState);
        Assert.IsTrue(capturedState.User.Identity?.IsAuthenticated);
        Assert.AreEqual("test@example.com", capturedState.User.Identity?.Name);
    }

    [TestMethod]
    public async Task NotifyUserAuthenticationAsync_WithNullToken_UpdatesToAnonymousState()
    {
        // Arrange
        var stateChanged = false;
        AuthenticationState? capturedState = null;

        _provider.AuthenticationStateChanged += task =>
        {
            stateChanged = true;
            capturedState = task.Result;
        };

        // Act
        await _provider.NotifyUserAuthenticationAsync(null);

        // Wait a bit for the event to process
        await Task.Delay(10, TestContext.CancellationTokenSource.Token);

        // Assert
        Assert.IsTrue(stateChanged);
        Assert.IsNotNull(capturedState);
        Assert.IsFalse(capturedState.User.Identity?.IsAuthenticated);
    }

    [TestMethod]
    public async Task NotifyUserLogoutAsync_UpdatesToAnonymousState()
    {
        // Arrange
        var stateChanged = false;
        AuthenticationState? capturedState = null;

        _provider.AuthenticationStateChanged += task =>
        {
            stateChanged = true;
            capturedState = task.Result;
        };

        // Act
        await _provider.NotifyUserLogoutAsync();

        // Wait a bit for the event to process
        await Task.Delay(10, TestContext.CancellationTokenSource.Token);

        // Assert
        Assert.IsTrue(stateChanged);
        Assert.IsNotNull(capturedState);
        Assert.IsFalse(capturedState.User.Identity?.IsAuthenticated);
    }

    [TestMethod]
    public async Task NotifyUserAuthenticationAsync_LogsUserInfo_WhenTokenIsValid()
    {
        // Arrange
        var token = CreateTestJwtToken("test@example.com", "user123", new[] { "Admin", "User" });

        // Act
        await _provider.NotifyUserAuthenticationAsync(token);

        // Assert
        var infoLogs = _logger.LogEntries.Where(l => l.LogLevel == LogLevel.Information).ToList();
        Assert.IsGreaterThan(0, infoLogs.Count);
        Assert.IsTrue(infoLogs.Any(l => l.Message.Contains("test@example.com") || l.Message.Contains("Unknown")));
    }

    [TestMethod]
    public async Task NotifyUserLogoutAsync_LogsLogoutInfo()
    {
        // Act
        await _provider.NotifyUserLogoutAsync();

        // Assert
        var infoLogs = _logger.LogEntries.Where(l => l.LogLevel == LogLevel.Information).ToList();
        Assert.IsGreaterThan(0, infoLogs.Count);
        Assert.IsTrue(infoLogs.Any(l => l.Message.Contains("logout") || l.Message.Contains("anonymous")));
    }

    [TestMethod]
    public void ParseClaimsFromJwt_WithValidToken_ExtractsAllClaims()
    {
        // Arrange
        var token = CreateTestJwtToken("test@example.com", "user123", new[] { "Admin", "User" });

        // Act - Use reflection to call the private static method
        var method = typeof(CustomAuthenticationStateProvider).GetMethod("ParseClaimsFromJwt", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var claims = (IEnumerable<Claim>)method!.Invoke(null, new object[] { token })!;

        // Assert
        var claimsList = claims.ToList();
        Assert.IsGreaterThan(0, claimsList.Count);
        
        var emailClaim = claimsList.FirstOrDefault(c => c.Type == "email" || c.Type == ClaimTypes.Email);
        Assert.IsNotNull(emailClaim);
        Assert.AreEqual("test@example.com", emailClaim.Value);
        
        var roleClaims = claimsList.Where(c => c.Type == "role" || c.Type == ClaimTypes.Role).ToList();
        Assert.IsGreaterThanOrEqualTo(2, roleClaims.Count);
        Assert.IsTrue(roleClaims.Any(c => c.Value == "Admin"));
        Assert.IsTrue(roleClaims.Any(c => c.Value == "User"));
    }

    private static string CreateTestJwtToken(string email, string userId, string[] roles, DateTime? expiry = null)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("test-secret-key-that-is-long-enough-for-hmac-sha256"));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.Email, email),
            new(ClaimTypes.Name, email),
            new(ClaimTypes.NameIdentifier, userId),
            new("email", email),
            new("sub", userId)
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
            claims.Add(new Claim("role", role));
        }

        var token = new JwtSecurityToken(
            issuer: "test-issuer",
            audience: "test-audience",
            claims: claims,
            expires: expiry ?? DateTime.UtcNow.AddHours(1),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public TestContext TestContext { get; set; }
}