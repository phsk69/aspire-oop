using AspireDeezNuts.ApiService.Data;
using AspireDeezNuts.Shared.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace AspireDeezNuts.ApiService.Tests.Controllers;

[TestClass]
public class AuthControllerTests
{
    public TestContext TestContext { get; set; } = null!;
    private WebApplicationFactory<Program>? _factory;
    private HttpClient? _client;
    private string? _adminToken;

    [TestInitialize]
    public async Task Setup()
    {
        var databaseName = $"TestDb_{Guid.NewGuid()}";
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Remove the existing DbContext registration
                    var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppIdentityDbContext>));
                    if (descriptor != null)
                    {
                        services.Remove(descriptor);
                    }

                    // Add a shared in-memory database for this test
                    services.AddDbContext<AppIdentityDbContext>(options =>
                        options.UseInMemoryDatabase(databaseName));
                });
            });

        _client = _factory.CreateClient();

        // Seed an admin user and get token for testing registration endpoint
        await SeedAdminUserAsync(TestContext.CancellationTokenSource.Token);
    }

    private async Task SeedAdminUserAsync(CancellationToken cancellationToken)
    {
        using var scope = _factory!.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        // Create Admin role
        if (!await roleManager.RoleExistsAsync("Admin"))
        {
            await roleManager.CreateAsync(new IdentityRole("Admin"));
        }

        // Create User role
        if (!await roleManager.RoleExistsAsync("User"))
        {
            await roleManager.CreateAsync(new IdentityRole("User"));
        }

        // Create admin user
        var adminUser = new IdentityUser
        {
            UserName = "admin@test.com",
            Email = "admin@test.com",
            EmailConfirmed = true
        };

        var createResult = await userManager.CreateAsync(adminUser, "Admin123!");
        if (!createResult.Succeeded)
        {
            throw new InvalidOperationException($"Failed to create admin user: {string.Join(", ", createResult.Errors.Select(e => e.Description))}");
        }
        
        var roleResult = await userManager.AddToRoleAsync(adminUser, "Admin");
        if (!roleResult.Succeeded)
        {
            throw new InvalidOperationException($"Failed to add admin role: {string.Join(", ", roleResult.Errors.Select(e => e.Description))}");
        }

        // Get admin token
        var loginRequest = new { Email = "admin@test.com", Password = "Admin123!" };
        var response = await _client!.PostAsJsonAsync("/api/v1/auth/login", loginRequest, cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Login failed with status {response.StatusCode}: {errorContent}");
        }
        
        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        var loginResponse = JsonSerializer.Deserialize<LoginResponse>(responseContent);
        _adminToken = loginResponse?.AccessToken;
    }

    [TestCleanup]
    public void Cleanup()
    {
        _client?.Dispose();
        _factory?.Dispose();
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
        
        // Verify user has User role
        using var scope = _factory!.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var user = await userManager.FindByEmailAsync("newuser2@test.com");
        var roles = await userManager.GetRolesAsync(user!);
        Assert.Contains("User", roles);
    }

    [TestMethod]
    public async Task Register_WithExistingEmail_ShouldReturnBadRequest()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = "admin@test.com", // Already exists from seed
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
        await _client.PostAsJsonAsync("/api/v1/auth/register", regularUser, TestContext.CancellationTokenSource.Token);

        // Login as regular user
        var loginRequest = new { Email = "regular@test.com", Password = "RegularPass123!" };
        var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", loginRequest, TestContext.CancellationTokenSource.Token);
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
        // Arrange
        var request = new LoginRequest
        {
            Email = "admin@test.com",
            Password = "Admin123!"
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
        // Arrange
        var request = new { Email = "admin@test.com" };

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
}