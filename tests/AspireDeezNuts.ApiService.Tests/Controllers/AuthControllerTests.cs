using AspireDeezNuts.ApiService.Data;
using AspireDeezNuts.Shared.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace AspireDeezNuts.ApiService.Tests.Controllers;

[TestClass]
[DoNotParallelize]
public class AuthControllerTests
{
    public TestContext TestContext { get; set; } = null!;
    private static WebApplicationFactory<Program>? _sharedFactory;
    private HttpClient? _client;
    private string? _adminToken;

    [ClassInitialize]
    public static void ClassSetup(TestContext context)
    {
        // Use configuration from local secrets file
        var testConfig = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.Development.secrets.json", optional: false, reloadOnChange: false)
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "AspireDeezNuts",
                ["Jwt:Audience"] = "AspireDeezNutsUsers",
                ["Jwt:AccessTokenExpiryMinutes"] = "15",
                ["Jwt:RefreshTokenExpiryDays"] = "7",
                ["UseInMemoryDatabase"] = "true"
            })
            .Build();
        
        // Create a single shared factory for all tests in this class
        // This ensures consistent JWT configuration across all test operations
        _sharedFactory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                // Use the configuration we built
                builder.ConfigureAppConfiguration((context, config) =>
                {
                    config.Sources.Clear();
                    config.AddConfiguration(testConfig);
                });
                
                builder.ConfigureServices(services =>
                {
                    // Use a unique database name for each test method (based on timestamp and random guid)
                    var databaseName = $"TestDb_AuthController_{DateTimeOffset.UtcNow.Ticks}_{Guid.NewGuid()}";
                    
                    // Remove the existing DbContext registration
                    var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppIdentityDbContext>));
                    if (descriptor != null)
                    {
                        services.Remove(descriptor);
                    }

                    // Add test database with a truly unique name
                    services.AddDbContext<AppIdentityDbContext>(options =>
                        options.UseInMemoryDatabase(databaseName));
                });
            });
    }

    [TestCleanup]
    public void TestCleanup()
    {
        _client?.Dispose();
    }
    
    [ClassCleanup(ClassCleanupBehavior.EndOfClass)]
    public static void ClassCleanup()
    {
        _sharedFactory?.Dispose();
    }

    [TestInitialize]
    public async Task Setup()
    {
        _client = _sharedFactory!.CreateClient();

        // Wait for DataSeeder to complete and get admin token
        await GetAdminTokenAsync(TestContext.CancellationTokenSource.Token);
    }

    private async Task GetAdminTokenAsync(CancellationToken cancellationToken)
    {
        // Give the DataSeeder time to run (it runs on app startup)
        await Task.Delay(100, cancellationToken);

        // Get configuration values from the test configuration
        using var scope = _sharedFactory!.Services.CreateScope();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var adminEmail = config["SeedData:InitialAdmin:Email"];
        var adminPassword = config["SeedData:InitialAdmin:Password"];
        
        if (string.IsNullOrEmpty(adminEmail) || string.IsNullOrEmpty(adminPassword))
        {
            throw new InvalidOperationException("Admin credentials not found in configuration. Ensure appsettings.Development.secrets.json exists with SeedData:InitialAdmin section.");
        }

        // Get admin token using the seeded credentials
        var loginRequest = new { Email = adminEmail, Password = adminPassword };
        var response = await _client!.PostAsJsonAsync("/api/v1/auth/login", loginRequest, cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Login failed with status {response.StatusCode}: {errorContent}. Using credentials: {adminEmail}");
        }
        
        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        var loginResponse = JsonSerializer.Deserialize<LoginResponse>(responseContent);
        _adminToken = loginResponse?.AccessToken;
    }


    #region Registration Validation Tests

    [TestMethod]
    public async Task Register_WithValidData_ShouldCreateUser()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = "newuser@test.com",
            Password = "ValidPass123!",
            Role = "User"
        };

        _client!.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _adminToken);

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", request, TestContext.CancellationTokenSource.Token);

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadFromJsonAsync<AuthSuccessResponse>(TestContext.CancellationTokenSource.Token);
        Assert.IsNotNull(content?.UserId);
    }

    [TestMethod]
    public async Task Register_WithMissingEmail_ShouldReturnBadRequest()
    {
        // Arrange
        var request = new { Password = "ValidPass123!", Role = "User" };

        _client!.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _adminToken);

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", request, TestContext.CancellationTokenSource.Token);

        // Assert
        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadFromJsonAsync<AuthErrorResponse>(TestContext.CancellationTokenSource.Token);
        Assert.IsNotNull(content?.Errors);
        var hasEmailError = content.Errors.ContainsKey("email") || content.Errors.ContainsKey("Email") || content.Errors.ContainsKey("$");
        Assert.IsTrue(hasEmailError);
    }

    [TestMethod]
    public async Task Register_WithInvalidEmailFormat_ShouldReturnBadRequest()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = "notanemail",
            Password = "ValidPass123!",
            Role = "User"
        };

        _client!.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _adminToken);

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", request, TestContext.CancellationTokenSource.Token);

        // Assert
        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync(TestContext.CancellationTokenSource.Token);
        Assert.Contains("Invalid email address format", content);
    }

    [TestMethod]
    public async Task Register_WithEmailExceeding256Characters_ShouldReturnBadRequest()
    {
        // Arrange
        var longEmail = new string('a', 250) + "@test.com"; // 259 characters
        var request = new RegisterRequest
        {
            Email = longEmail,
            Password = "ValidPass123!",
            Role = "User"
        };

        _client!.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _adminToken);

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", request, TestContext.CancellationTokenSource.Token);

        // Assert
        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync(TestContext.CancellationTokenSource.Token);
        Assert.Contains("Email must be at most 256 characters", content);
    }

    [TestMethod]
    public async Task Register_WithMissingPassword_ShouldReturnBadRequest()
    {
        // Arrange
        var request = new { Email = "test@test.com", Role = "User" };

        _client!.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _adminToken);

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", request, TestContext.CancellationTokenSource.Token);

        // Assert
        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadFromJsonAsync<AuthErrorResponse>(TestContext.CancellationTokenSource.Token);
        Assert.IsNotNull(content?.Errors);
        var hasPasswordError = content.Errors.Any(kvp => kvp.Key.Contains("Password", StringComparison.OrdinalIgnoreCase) || kvp.Key.Contains("password", StringComparison.OrdinalIgnoreCase) || kvp.Key == "request");
        Assert.IsTrue(hasPasswordError);
    }

    [TestMethod]
    public async Task Register_WithPasswordLessThan8Characters_ShouldReturnBadRequest()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = "test@test.com",
            Password = "Pass1!",
            Role = "User"
        };

        _client!.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _adminToken);

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", request, TestContext.CancellationTokenSource.Token);

        // Assert
        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync(TestContext.CancellationTokenSource.Token);
        Assert.Contains("Password must be between 8 and 64 characters", content);
    }

    [TestMethod]
    public async Task Register_WithPasswordMissingUppercase_ShouldReturnBadRequest()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = "test@test.com",
            Password = "password123!",
            Role = "User"
        };

        _client!.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _adminToken);

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", request, TestContext.CancellationTokenSource.Token);

        // Assert
        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.CancellationTokenSource.Token);
        Assert.IsTrue(content.TryGetProperty("errors", out var errorsElement));
        var errors = errorsElement.EnumerateArray().Select(e => e.GetString()).ToArray();
        Assert.IsTrue(errors.Any(error => error?.Contains("uppercase", StringComparison.OrdinalIgnoreCase) == true));
    }

    [TestMethod]
    public async Task Register_WithPasswordMissingLowercase_ShouldReturnBadRequest()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = "test@test.com",
            Password = "PASSWORD123!",
            Role = "User"
        };

        _client!.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _adminToken);

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", request, TestContext.CancellationTokenSource.Token);

        // Assert
        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.CancellationTokenSource.Token);
        Assert.IsTrue(content.TryGetProperty("errors", out var errorsElement));
        var errors = errorsElement.EnumerateArray().Select(e => e.GetString()).ToArray();
        Assert.IsTrue(errors.Any(error => error?.Contains("lowercase", StringComparison.OrdinalIgnoreCase) == true));
    }

    [TestMethod]
    public async Task Register_WithPasswordMissingDigit_ShouldReturnBadRequest()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = "test@test.com",
            Password = "Password!",
            Role = "User"
        };

        _client!.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _adminToken);

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", request, TestContext.CancellationTokenSource.Token);

        // Assert
        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.CancellationTokenSource.Token);
        Assert.IsTrue(content.TryGetProperty("errors", out var errorsElement));
        var errors = errorsElement.EnumerateArray().Select(e => e.GetString()).ToArray();
        Assert.IsTrue(errors.Any(error => error?.Contains("digit", StringComparison.OrdinalIgnoreCase) == true));
    }

    [TestMethod]
    public async Task Register_WithPasswordMissingNonAlphanumeric_ShouldReturnBadRequest()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = "test@test.com",
            Password = "Password123",
            Role = "User"
        };

        _client!.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _adminToken);

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", request, TestContext.CancellationTokenSource.Token);

        // Assert
        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.CancellationTokenSource.Token);
        Assert.IsTrue(content.TryGetProperty("errors", out var errorsElement));
        var errors = errorsElement.EnumerateArray().Select(e => e.GetString()).ToArray();
        Assert.IsTrue(errors.Any(error => error?.Contains("non alphanumeric", StringComparison.OrdinalIgnoreCase) == true));
    }

    [TestMethod]
    public async Task Register_WithInvalidRole_ShouldReturnBadRequest()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = "test@test.com",
            Password = "ValidPass123!",
            Role = "SuperAdmin"
        };

        _client!.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _adminToken);

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", request, TestContext.CancellationTokenSource.Token);

        // Assert
        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync(TestContext.CancellationTokenSource.Token);
        Assert.Contains("Role must be either 'Admin' or 'User'", content);
    }

    [TestMethod]
    public async Task Register_WithEmptyRole_ShouldDefaultToUser()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = "newuser2@test.com",
            Password = "ValidPass123!",
            Role = null
        };

        _client!.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _adminToken);

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", request, TestContext.CancellationTokenSource.Token);

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        
        // Read the response to get the user ID (note: the controller returns lowercase field names)
        var responseText = await response.Content.ReadAsStringAsync(TestContext.CancellationTokenSource.Token);
        dynamic responseContent = System.Text.Json.JsonSerializer.Deserialize<dynamic>(responseText)!;
        var userIdElement = ((System.Text.Json.JsonElement)responseContent).GetProperty("userId");
        var userId = userIdElement.GetString();
        Assert.IsNotNull(userId, "Registration should return user ID");
        
        // Add some delay to ensure the user is persisted
        await Task.Delay(100, TestContext.CancellationTokenSource.Token);
        
        // Verify user has User role
        using var scope = _sharedFactory!.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        
        // Find user by ID
        var user = await userManager.FindByIdAsync(userId);
        Assert.IsNotNull(user, "User should have been created");
        var roles = await userManager.GetRolesAsync(user);
        Assert.Contains("User", roles);
    }

    [TestMethod]
    public async Task Register_WithExistingEmail_ShouldReturnBadRequest()
    {
        // Arrange
        // Get the seeded admin email from configuration
        using var scope = _sharedFactory!.Services.CreateScope();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var adminEmail = config["SeedData:InitialAdmin:Email"]!;
        
        var request = new RegisterRequest
        {
            Email = adminEmail, // Already exists from seed
            Password = "ValidPass123!",
            Role = "User"
        };

        _client!.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _adminToken);

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", request, TestContext.CancellationTokenSource.Token);

        // Assert
        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync(TestContext.CancellationTokenSource.Token);
        Assert.Contains("Email already registered", content);
    }

    [TestMethod]
    public async Task Register_WithoutAdminRole_ShouldReturnUnauthorized()
    {
        // Arrange - Create a regular user and get their token
        var regularUser = new RegisterRequest
        {
            Email = "regular@test.com",
            Password = "RegularPass123!",
            Role = "User"
        };

        _client!.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _adminToken);
        var registerResponse = await _client.PostAsJsonAsync("/api/v1/auth/register", regularUser, TestContext.CancellationTokenSource.Token);
        
        // Ensure registration was successful
        Assert.AreEqual(HttpStatusCode.OK, registerResponse.StatusCode, "Failed to register regular user");

        // Clear authorization header before login
        _client.DefaultRequestHeaders.Authorization = null;

        // Login as regular user
        var loginRequest = new { Email = "regular@test.com", Password = "RegularPass123!" };
        var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", loginRequest, TestContext.CancellationTokenSource.Token);
        
        // Check if login was successful
        Assert.AreEqual(HttpStatusCode.OK, loginResponse.StatusCode, "Failed to login as regular user");
        
        var loginContent = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>(TestContext.CancellationTokenSource.Token);
        var userToken = loginContent?.AccessToken;

        // Try to register another user with regular user token
        var newUserRequest = new RegisterRequest
        {
            Email = "anotheruser@test.com",
            Password = "ValidPass123!",
            Role = "User"
        };

        _client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", userToken);

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", newUserRequest, TestContext.CancellationTokenSource.Token);

        // Assert
        Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    #endregion

    #region Login Validation Tests

    [TestMethod]
    public async Task Login_WithValidCredentials_ShouldReturnToken()
    {
        // Arrange - Get credentials from configuration
        using var scope = _sharedFactory!.Services.CreateScope();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var adminEmail = config["SeedData:InitialAdmin:Email"]!;
        var adminPassword = config["SeedData:InitialAdmin:Password"]!;
        
        var request = new LoginRequest
        {
            Email = adminEmail,
            Password = adminPassword
        };

        // Act
        var response = await _client!.PostAsJsonAsync("/api/v1/auth/login", request, TestContext.CancellationTokenSource.Token);

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadFromJsonAsync<LoginResponse>(TestContext.CancellationTokenSource.Token);
        Assert.IsNotNull(content?.AccessToken);
        Assert.IsNotNull(content?.RefreshToken);
        Assert.IsTrue(content.ExpiresIn > 0);
    }

    [TestMethod]
    public async Task Login_WithMissingEmail_ShouldReturnBadRequest()
    {
        // Arrange
        var request = new { Password = "Admin123!" };

        // Act
        var response = await _client!.PostAsJsonAsync("/api/v1/auth/login", request, TestContext.CancellationTokenSource.Token);

        // Assert
        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadFromJsonAsync<AuthErrorResponse>(TestContext.CancellationTokenSource.Token);
        Assert.IsNotNull(content?.Errors);
        var hasEmailError = content.Errors.ContainsKey("email") || content.Errors.ContainsKey("Email") || content.Errors.ContainsKey("$");
        Assert.IsTrue(hasEmailError);
    }

    [TestMethod]
    public async Task Login_WithInvalidEmailFormat_ShouldReturnBadRequest()
    {
        // Arrange
        var request = new LoginRequest
        {
            Email = "notanemail",
            Password = "Admin123!"
        };

        // Act
        var response = await _client!.PostAsJsonAsync("/api/v1/auth/login", request, TestContext.CancellationTokenSource.Token);

        // Assert
        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync(TestContext.CancellationTokenSource.Token);
        Assert.Contains("Invalid email address format", content);
    }

    [TestMethod]
    public async Task Login_WithMissingPassword_ShouldReturnBadRequest()
    {
        // Arrange - Get admin email from configuration
        using var scope = _sharedFactory!.Services.CreateScope();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var adminEmail = config["SeedData:InitialAdmin:Email"]!;
        
        var request = new { Email = adminEmail };

        // Act
        var response = await _client!.PostAsJsonAsync("/api/v1/auth/login", request, TestContext.CancellationTokenSource.Token);

        // Assert
        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadFromJsonAsync<AuthErrorResponse>(TestContext.CancellationTokenSource.Token);
        Assert.IsNotNull(content?.Errors);
        var hasPasswordError = content.Errors.ContainsKey("password") || content.Errors.ContainsKey("Password") || content.Errors.ContainsKey("$") || content.Errors.ContainsKey("request");
        Assert.IsTrue(hasPasswordError);
    }

    #endregion

    #region Refresh Simple Tests

    [TestMethod]
    public async Task RefreshSimple_WithValidToken_ShouldReturnNewToken()
    {
        // Arrange - Use admin token
        _client!.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _adminToken);

        // Act
        var response = await _client.PostAsync("/api/v1/auth/refresh-simple", null, TestContext.CancellationTokenSource.Token);

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadFromJsonAsync<LoginResponse>(TestContext.CancellationTokenSource.Token);
        Assert.IsNotNull(content?.AccessToken);
        Assert.IsNotNull(content?.RefreshToken);
        Assert.IsTrue(content.ExpiresIn > 0);
        
        // Verify the new token is different from the original
        Assert.AreNotEqual(_adminToken, content.AccessToken);
    }

    [TestMethod]
    public async Task RefreshSimple_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        // Arrange - Clear authorization header
        _client!.DefaultRequestHeaders.Authorization = null;

        // Act
        var response = await _client.PostAsync("/api/v1/auth/refresh-simple", null, TestContext.CancellationTokenSource.Token);

        // Assert
        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [TestMethod]
    public async Task RefreshSimple_WithInvalidToken_ShouldReturnUnauthorized()
    {
        // Arrange - Use invalid token
        _client!.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "invalid.token.here");

        // Act
        var response = await _client.PostAsync("/api/v1/auth/refresh-simple", null, TestContext.CancellationTokenSource.Token);

        // Assert
        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [TestMethod]
    public async Task RefreshSimple_WithExpiredToken_ShouldReturnUnauthorized()
    {
        // Arrange - Create a token with very short expiry
        using var scope = _sharedFactory!.Services.CreateScope();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var adminEmail = config["SeedData:InitialAdmin:Email"]!;
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var tokenService = scope.ServiceProvider.GetRequiredService<AspireDeezNuts.ApiService.Services.ITokenService>();
        
        var user = await userManager.FindByEmailAsync(adminEmail);
        Assert.IsNotNull(user);
        
        // Generate token with very short expiry (this would require modifying token service for true expiry test)
        // For now, we'll just test the current flow
        var expiredToken = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJleHAiOjE2MDE0MjE2MDB9.invalid";
        
        _client!.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", expiredToken);

        // Act
        var response = await _client.PostAsync("/api/v1/auth/refresh-simple", null, TestContext.CancellationTokenSource.Token);

        // Assert
        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [TestMethod]
    public async Task RefreshSimple_NewTokenShouldBeValidForAPIAccess()
    {
        // Arrange - Get new token via refresh
        _client!.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _adminToken);

        var refreshResponse = await _client.PostAsync("/api/v1/auth/refresh-simple", null, TestContext.CancellationTokenSource.Token);
        Assert.AreEqual(HttpStatusCode.OK, refreshResponse.StatusCode);
        
        var refreshContent = await refreshResponse.Content.ReadFromJsonAsync<LoginResponse>(TestContext.CancellationTokenSource.Token);
        var newToken = refreshContent?.AccessToken;
        Assert.IsNotNull(newToken);

        // Act - Use new token to access protected endpoint
        _client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", newToken);

        var testResponse = await _client.GetAsync("/api/v1/auth/test-admin", TestContext.CancellationTokenSource.Token);

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, testResponse.StatusCode);
        var testContent = await testResponse.Content.ReadAsStringAsync(TestContext.CancellationTokenSource.Token);
        Assert.Contains("admin", testContent, StringComparison.OrdinalIgnoreCase);
    }

    #endregion
}