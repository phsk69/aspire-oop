using AspireDeezNuts.ApiService.Data;
using AspireDeezNuts.Shared.Models;
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
public class UserControllerTests
{
    public TestContext TestContext { get; set; } = null!;
    private static WebApplicationFactory<Program>? _sharedFactory;
    private HttpClient? _client;
    private string? _adminToken;

    [ClassInitialize]
    public static void ClassSetup(TestContext context)
    {
        var testConfig = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddJsonFile("appsettings.Development.secrets.json", optional: false, reloadOnChange: false)
            .Build();

        _sharedFactory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((context, config) =>
                {
                    config.Sources.Clear();
                    config.AddConfiguration(testConfig);
                });

                builder.ConfigureServices(services =>
                {
                    var databaseName = $"TestDb_UserController_{DateTimeOffset.UtcNow.Ticks}_{Guid.NewGuid()}";

                    var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppIdentityDbContext>));
                    if (descriptor != null)
                    {
                        services.Remove(descriptor);
                    }

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
        await GetAdminTokenAsync(TestContext.CancellationTokenSource.Token);
    }

    private async Task GetAdminTokenAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(100, cancellationToken);

        using var scope = _sharedFactory!.Services.CreateScope();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var adminEmail = config["SeedData:InitialAdmin:Email"];
        var adminPassword = config["SeedData:InitialAdmin:Password"];

        if (string.IsNullOrEmpty(adminEmail) || string.IsNullOrEmpty(adminPassword))
        {
            throw new InvalidOperationException("Admin credentials not found in configuration. Ensure appsettings.Development.secrets.json exists with SeedData:InitialAdmin section.");
        }

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

    #region GetUsers Tests

    [TestMethod]
    public async Task GetUsers_WithAdminRole_ShouldReturnAllUsers()
    {
        _client!.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _adminToken);

        var response = await _client.GetAsync("/api/v1/user", TestContext.CancellationTokenSource.Token);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var users = await response.Content.ReadFromJsonAsync<UserDto[]>(TestContext.CancellationTokenSource.Token);
        Assert.IsNotNull(users);
        Assert.IsGreaterThan(0, users.Length);
        Assert.IsTrue(users.Any(u => u.Roles?.Contains("Admin") == true));
    }

    [TestMethod]
    public async Task GetUsers_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        _client!.DefaultRequestHeaders.Authorization = null;

        var response = await _client.GetAsync("/api/v1/user", TestContext.CancellationTokenSource.Token);

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [TestMethod]
    public async Task GetUsers_WithUserRole_ShouldReturnForbidden()
    {
        var regularUser = new RegisterRequest
        {
            Email = "regular@test.com",
            Password = "RegularPass123!",
            Role = "User"
        };

        _client!.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _adminToken);
        await _client.PostAsJsonAsync("/api/v1/auth/register", regularUser, TestContext.CancellationTokenSource.Token);

        _client.DefaultRequestHeaders.Authorization = null;
        var loginRequest = new { Email = "regular@test.com", Password = "RegularPass123!" };
        var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", loginRequest, TestContext.CancellationTokenSource.Token);
        var loginContent = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>(TestContext.CancellationTokenSource.Token);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginContent?.AccessToken);

        var response = await _client.GetAsync("/api/v1/user", TestContext.CancellationTokenSource.Token);

        Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    #endregion

    #region GetUser Tests

    [TestMethod]
    public async Task GetUser_WithValidId_ShouldReturnUser()
    {
        _client!.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _adminToken);

        var usersResponse = await _client.GetAsync("/api/v1/user", TestContext.CancellationTokenSource.Token);
        var users = await usersResponse.Content.ReadFromJsonAsync<UserDto[]>(TestContext.CancellationTokenSource.Token);
        var userId = users!.First().Id;

        var response = await _client.GetAsync($"/api/v1/user/{userId}", TestContext.CancellationTokenSource.Token);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var user = await response.Content.ReadFromJsonAsync<UserDto>(TestContext.CancellationTokenSource.Token);
        Assert.IsNotNull(user);
        Assert.AreEqual(userId, user.Id);
    }

    [TestMethod]
    public async Task GetUser_WithInvalidId_ShouldReturnNotFound()
    {
        _client!.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _adminToken);

        var response = await _client.GetAsync("/api/v1/user/nonexistent-id", TestContext.CancellationTokenSource.Token);

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync(TestContext.CancellationTokenSource.Token);
        Assert.Contains("User with ID nonexistent-id not found", content);
    }

    [TestMethod]
    public async Task GetUser_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        _client!.DefaultRequestHeaders.Authorization = null;

        var response = await _client.GetAsync("/api/v1/user/some-id", TestContext.CancellationTokenSource.Token);

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion

    #region CreateUser Tests

    [TestMethod]
    public async Task CreateUser_WithValidData_ShouldCreateUser()
    {
        _client!.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _adminToken);

        var createUserDto = new CreateUserDto
        {
            Email = "newuser@test.com",
            UserName = "newuser",
            Password = "ValidPass123!",
            IsAdmin = false
        };

        var response = await _client.PostAsJsonAsync("/api/v1/user", createUserDto, TestContext.CancellationTokenSource.Token);

        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        var user = await response.Content.ReadFromJsonAsync<UserDto>(TestContext.CancellationTokenSource.Token);
        Assert.IsNotNull(user);
        Assert.AreEqual("newuser@test.com", user.Email);
        Assert.AreEqual("newuser", user.UserName);
        Assert.IsTrue(user.Roles?.Contains("User"));
    }

    [TestMethod]
    public async Task CreateUser_WithAdminFlag_ShouldCreateAdminUser()
    {
        _client!.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _adminToken);

        var createUserDto = new CreateUserDto
        {
            Email = "newadmin@test.com",
            UserName = "newadmin",
            Password = "ValidPass123!",
            IsAdmin = true
        };

        var response = await _client.PostAsJsonAsync("/api/v1/user", createUserDto, TestContext.CancellationTokenSource.Token);

        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        var user = await response.Content.ReadFromJsonAsync<UserDto>(TestContext.CancellationTokenSource.Token);
        Assert.IsNotNull(user);
        Assert.IsTrue(user.Roles?.Contains("Admin"));
    }

    [TestMethod]
    public async Task CreateUser_WithExistingEmail_ShouldReturnConflict()
    {
        _client!.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _adminToken);

        using var scope = _sharedFactory!.Services.CreateScope();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var adminEmail = config["SeedData:InitialAdmin:Email"]!;

        var createUserDto = new CreateUserDto
        {
            Email = adminEmail,
            UserName = "duplicate",
            Password = "ValidPass123!",
            IsAdmin = false
        };

        var response = await _client.PostAsJsonAsync("/api/v1/user", createUserDto, TestContext.CancellationTokenSource.Token);

        Assert.AreEqual(HttpStatusCode.Conflict, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync(TestContext.CancellationTokenSource.Token);
        Assert.Contains("already exists", content);
    }

    [TestMethod]
    public async Task CreateUser_WithInvalidPassword_ShouldReturnBadRequest()
    {
        _client!.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _adminToken);

        var createUserDto = new CreateUserDto
        {
            Email = "test@test.com",
            UserName = "testuser",
            Password = "weak",
            IsAdmin = false
        };

        var response = await _client.PostAsJsonAsync("/api/v1/user", createUserDto, TestContext.CancellationTokenSource.Token);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod]
    public async Task CreateUser_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        _client!.DefaultRequestHeaders.Authorization = null;

        var createUserDto = new CreateUserDto
        {
            Email = "test@test.com",
            UserName = "testuser",
            Password = "ValidPass123!",
            IsAdmin = false
        };

        var response = await _client.PostAsJsonAsync("/api/v1/user", createUserDto, TestContext.CancellationTokenSource.Token);

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion

    #region UpdateUser Tests

    [TestMethod]
    public async Task UpdateUser_WithValidData_ShouldUpdateUser()
    {
        _client!.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _adminToken);

        var createUserDto = new CreateUserDto
        {
            Email = "updateme@test.com",
            UserName = "updateme",
            Password = "ValidPass123!",
            IsAdmin = false
        };

        var createResponse = await _client.PostAsJsonAsync("/api/v1/user", createUserDto, TestContext.CancellationTokenSource.Token);
        var createdUser = await createResponse.Content.ReadFromJsonAsync<UserDto>(TestContext.CancellationTokenSource.Token);

        var updateUserDto = new UpdateUserDto
        {
            Id = createdUser!.Id,
            Email = "updated@test.com",
            UserName = "updateduser",
            EmailConfirmed = true
        };

        var response = await _client.PutAsJsonAsync($"/api/v1/user/{createdUser.Id}", updateUserDto, TestContext.CancellationTokenSource.Token);

        Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode);
    }

    [TestMethod]
    public async Task UpdateUser_WithIdMismatch_ShouldReturnBadRequest()
    {
        _client!.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _adminToken);

        var updateUserDto = new UpdateUserDto
        {
            Id = "different-id",
            Email = "updated@test.com"
        };

        var response = await _client.PutAsJsonAsync("/api/v1/user/some-id", updateUserDto, TestContext.CancellationTokenSource.Token);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync(TestContext.CancellationTokenSource.Token);
        Assert.Contains("User ID mismatch", content);
    }

    [TestMethod]
    public async Task UpdateUser_WithNonExistentId_ShouldReturnNotFound()
    {
        _client!.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _adminToken);

        var updateUserDto = new UpdateUserDto
        {
            Id = "nonexistent-id",
            Email = "updated@test.com"
        };

        var response = await _client.PutAsJsonAsync("/api/v1/user/nonexistent-id", updateUserDto, TestContext.CancellationTokenSource.Token);

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync(TestContext.CancellationTokenSource.Token);
        Assert.Contains("User with ID nonexistent-id not found", content);
    }

    #endregion

    #region PatchUser Tests

    [TestMethod]
    public async Task PatchUser_WithValidData_ShouldReturnUpdatedUser()
    {
        _client!.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _adminToken);

        var createUserDto = new CreateUserDto
        {
            Email = "patchme@test.com",
            UserName = "patchme",
            Password = "ValidPass123!",
            IsAdmin = false
        };

        var createResponse = await _client.PostAsJsonAsync("/api/v1/user", createUserDto, TestContext.CancellationTokenSource.Token);
        var createdUser = await createResponse.Content.ReadFromJsonAsync<UserDto>(TestContext.CancellationTokenSource.Token);

        var patchUserDto = new PatchUserDto
        {
            Email = "patched@test.com",
            UserName = "patcheduser"
        };

        var response = await _client.PatchAsJsonAsync($"/api/v1/user/{createdUser!.Id}", patchUserDto, TestContext.CancellationTokenSource.Token);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var updatedUser = await response.Content.ReadFromJsonAsync<UserDto>(TestContext.CancellationTokenSource.Token);
        Assert.IsNotNull(updatedUser);
        Assert.AreEqual("patched@test.com", updatedUser.Email);
        Assert.AreEqual("patcheduser", updatedUser.UserName);
        Assert.AreEqual(createdUser.Id, updatedUser.Id);
    }

    [TestMethod]
    public async Task PatchUser_WithPartialData_ShouldUpdateOnlyProvidedFields()
    {
        _client!.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _adminToken);

        var createUserDto = new CreateUserDto
        {
            Email = "partial@test.com",
            UserName = "partialuser",
            Password = "ValidPass123!",
            IsAdmin = false
        };

        var createResponse = await _client.PostAsJsonAsync("/api/v1/user", createUserDto, TestContext.CancellationTokenSource.Token);
        var createdUser = await createResponse.Content.ReadFromJsonAsync<UserDto>(TestContext.CancellationTokenSource.Token);

        var patchUserDto = new PatchUserDto
        {
            Email = "partialupdate@test.com"
            // UserName not provided - should remain unchanged
        };

        var response = await _client.PatchAsJsonAsync($"/api/v1/user/{createdUser!.Id}", patchUserDto, TestContext.CancellationTokenSource.Token);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var updatedUser = await response.Content.ReadFromJsonAsync<UserDto>(TestContext.CancellationTokenSource.Token);
        Assert.IsNotNull(updatedUser);
        Assert.AreEqual("partialupdate@test.com", updatedUser.Email);
        Assert.AreEqual("partialuser", updatedUser.UserName); // Should remain unchanged
    }

    [TestMethod]
    public async Task PatchUser_WithEmailConfirmed_ShouldUpdateEmailConfirmed()
    {
        _client!.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _adminToken);

        var createUserDto = new CreateUserDto
        {
            Email = "confirmme@test.com",
            UserName = "confirmme",
            Password = "ValidPass123!",
            IsAdmin = false
        };

        var createResponse = await _client.PostAsJsonAsync("/api/v1/user", createUserDto, TestContext.CancellationTokenSource.Token);
        var createdUser = await createResponse.Content.ReadFromJsonAsync<UserDto>(TestContext.CancellationTokenSource.Token);

        var patchUserDto = new PatchUserDto
        {
            EmailConfirmed = false
        };

        var response = await _client.PatchAsJsonAsync($"/api/v1/user/{createdUser!.Id}", patchUserDto, TestContext.CancellationTokenSource.Token);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var updatedUser = await response.Content.ReadFromJsonAsync<UserDto>(TestContext.CancellationTokenSource.Token);
        Assert.IsNotNull(updatedUser);
        Assert.IsFalse(updatedUser.EmailConfirmed);
        Assert.AreEqual(createdUser.Email, updatedUser.Email); // Should remain unchanged
        Assert.AreEqual(createdUser.UserName, updatedUser.UserName); // Should remain unchanged
    }

    [TestMethod]
    public async Task PatchUser_WithEmptyPatch_ShouldReturnOkWithoutChanges()
    {
        _client!.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _adminToken);

        var createUserDto = new CreateUserDto
        {
            Email = "nochange@test.com",
            UserName = "nochange",
            Password = "ValidPass123!",
            IsAdmin = false
        };

        var createResponse = await _client.PostAsJsonAsync("/api/v1/user", createUserDto, TestContext.CancellationTokenSource.Token);
        var createdUser = await createResponse.Content.ReadFromJsonAsync<UserDto>(TestContext.CancellationTokenSource.Token);

        var patchUserDto = new PatchUserDto(); // All fields null

        var response = await _client.PatchAsJsonAsync($"/api/v1/user/{createdUser!.Id}", patchUserDto, TestContext.CancellationTokenSource.Token);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var updatedUser = await response.Content.ReadFromJsonAsync<UserDto>(TestContext.CancellationTokenSource.Token);
        Assert.IsNotNull(updatedUser);
        Assert.AreEqual(createdUser.Email, updatedUser.Email);
        Assert.AreEqual(createdUser.UserName, updatedUser.UserName);
        Assert.AreEqual(createdUser.EmailConfirmed, updatedUser.EmailConfirmed);
    }

    [TestMethod]
    public async Task PatchUser_WithNonExistentId_ShouldReturnNotFound()
    {
        _client!.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _adminToken);

        var patchUserDto = new PatchUserDto
        {
            Email = "updated@test.com"
        };

        var response = await _client.PatchAsJsonAsync("/api/v1/user/nonexistent-id", patchUserDto, TestContext.CancellationTokenSource.Token);

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync(TestContext.CancellationTokenSource.Token);
        Assert.Contains("User with ID nonexistent-id not found", content);
    }

    [TestMethod]
    public async Task PatchUser_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        _client!.DefaultRequestHeaders.Authorization = null;

        var patchUserDto = new PatchUserDto
        {
            Email = "test@test.com"
        };

        var response = await _client.PatchAsJsonAsync("/api/v1/user/some-id", patchUserDto, TestContext.CancellationTokenSource.Token);

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [TestMethod]
    public async Task PatchUser_WithUserRole_ShouldReturnForbidden()
    {
        _client!.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _adminToken);

        // Create a regular user
        var regularUser = new RegisterRequest
        {
            Email = "regularpatch@test.com",
            Password = "RegularPass123!",
            Role = "User"
        };

        await _client.PostAsJsonAsync("/api/v1/auth/register", regularUser, TestContext.CancellationTokenSource.Token);

        // Login as regular user
        _client.DefaultRequestHeaders.Authorization = null;
        var loginRequest = new { Email = "regularpatch@test.com", Password = "RegularPass123!" };
        var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", loginRequest, TestContext.CancellationTokenSource.Token);
        var loginContent = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>(TestContext.CancellationTokenSource.Token);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginContent?.AccessToken);

        var patchUserDto = new PatchUserDto
        {
            Email = "test@test.com"
        };

        var response = await _client.PatchAsJsonAsync("/api/v1/user/some-id", patchUserDto, TestContext.CancellationTokenSource.Token);

        Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    #endregion

    #region UpdateUserRole Tests

    [TestMethod]
    public async Task UpdateUserRole_AddRole_ShouldAddRole()
    {
        _client!.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _adminToken);

        var createUserDto = new CreateUserDto
        {
            Email = "roletest@test.com",
            UserName = "roletest",
            Password = "ValidPass123!",
            IsAdmin = false
        };

        var createResponse = await _client.PostAsJsonAsync("/api/v1/user", createUserDto, TestContext.CancellationTokenSource.Token);
        var createdUser = await createResponse.Content.ReadFromJsonAsync<UserDto>(TestContext.CancellationTokenSource.Token);

        var roleUpdate = new UserRoleUpdateDto
        {
            UserId = createdUser!.Id,
            Role = "Admin",
            AddRole = true
        };

        var response = await _client.PostAsJsonAsync($"/api/v1/user/{createdUser.Id}/roles", roleUpdate, TestContext.CancellationTokenSource.Token);

        Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode);
    }

    [TestMethod]
    public async Task UpdateUserRole_RemoveRole_ShouldRemoveRole()
    {
        _client!.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _adminToken);

        var createUserDto = new CreateUserDto
        {
            Email = "adminremove@test.com",
            UserName = "adminremove",
            Password = "ValidPass123!",
            IsAdmin = true
        };

        var createResponse = await _client.PostAsJsonAsync("/api/v1/user", createUserDto, TestContext.CancellationTokenSource.Token);
        var createdUser = await createResponse.Content.ReadFromJsonAsync<UserDto>(TestContext.CancellationTokenSource.Token);

        var roleUpdate = new UserRoleUpdateDto
        {
            UserId = createdUser!.Id,
            Role = "Admin",
            AddRole = false
        };

        var response = await _client.PostAsJsonAsync($"/api/v1/user/{createdUser.Id}/roles", roleUpdate, TestContext.CancellationTokenSource.Token);

        Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode);
    }

    [TestMethod]
    public async Task UpdateUserRole_WithNonExistentRole_ShouldReturnBadRequest()
    {
        _client!.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _adminToken);

        var usersResponse = await _client.GetAsync("/api/v1/user", TestContext.CancellationTokenSource.Token);
        var users = await usersResponse.Content.ReadFromJsonAsync<UserDto[]>(TestContext.CancellationTokenSource.Token);
        var userId = users!.First().Id;

        var roleUpdate = new UserRoleUpdateDto
        {
            UserId = userId,
            Role = "NonExistentRole",
            AddRole = true
        };

        var response = await _client.PostAsJsonAsync($"/api/v1/user/{userId}/roles", roleUpdate, TestContext.CancellationTokenSource.Token);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync(TestContext.CancellationTokenSource.Token);
        Assert.Contains("Role NonExistentRole does not exist", content);
    }

    [TestMethod]
    public async Task UpdateUserRole_WithIdMismatch_ShouldReturnBadRequest()
    {
        _client!.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _adminToken);

        var roleUpdate = new UserRoleUpdateDto
        {
            UserId = "different-id",
            Role = "Admin",
            AddRole = true
        };

        var response = await _client.PostAsJsonAsync("/api/v1/user/some-id/roles", roleUpdate, TestContext.CancellationTokenSource.Token);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync(TestContext.CancellationTokenSource.Token);
        Assert.Contains("User ID mismatch", content);
    }

    [TestMethod]
    public async Task UpdateUserRole_WithNonExistentUser_ShouldReturnNotFound()
    {
        _client!.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _adminToken);

        var roleUpdate = new UserRoleUpdateDto
        {
            UserId = "nonexistent-id",
            Role = "Admin",
            AddRole = true
        };

        var response = await _client.PostAsJsonAsync("/api/v1/user/nonexistent-id/roles", roleUpdate, TestContext.CancellationTokenSource.Token);

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync(TestContext.CancellationTokenSource.Token);
        Assert.Contains("User with ID nonexistent-id not found", content);
    }

    #endregion

    #region DeleteUser Tests

    [TestMethod]
    public async Task DeleteUser_WithValidId_ShouldDeleteUser()
    {
        _client!.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _adminToken);

        var createUserDto = new CreateUserDto
        {
            Email = "deleteme@test.com",
            UserName = "deleteme",
            Password = "ValidPass123!",
            IsAdmin = false
        };

        var createResponse = await _client.PostAsJsonAsync("/api/v1/user", createUserDto, TestContext.CancellationTokenSource.Token);
        var createdUser = await createResponse.Content.ReadFromJsonAsync<UserDto>(TestContext.CancellationTokenSource.Token);

        var response = await _client.DeleteAsync($"/api/v1/user/{createdUser!.Id}", TestContext.CancellationTokenSource.Token);

        Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode);
    }

    [TestMethod]
    public async Task DeleteUser_WithNonExistentId_ShouldReturnNotFound()
    {
        _client!.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _adminToken);

        var response = await _client.DeleteAsync("/api/v1/user/nonexistent-id", TestContext.CancellationTokenSource.Token);

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync(TestContext.CancellationTokenSource.Token);
        Assert.Contains("User with ID nonexistent-id not found", content);
    }

    [TestMethod]
    public async Task DeleteUser_LastAdminUser_ShouldReturnBadRequest()
    {
        _client!.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _adminToken);

        // Get all users and find admins
        var usersResponse = await _client.GetAsync("/api/v1/user", TestContext.CancellationTokenSource.Token);
        var users = await usersResponse.Content.ReadFromJsonAsync<UserDto[]>(TestContext.CancellationTokenSource.Token);
        var adminUsers = users!.Where(u => u.Roles?.Contains("Admin") == true).ToArray();

        // If there are multiple admins, delete all but one first
        for (int i = 1; i < adminUsers.Length; i++)
        {
            await _client.DeleteAsync($"/api/v1/user/{adminUsers[i].Id}", TestContext.CancellationTokenSource.Token);
        }

        // Now try to delete the last admin - this should fail
        var lastAdminId = adminUsers[0].Id;
        var response = await _client.DeleteAsync($"/api/v1/user/{lastAdminId}", TestContext.CancellationTokenSource.Token);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync(TestContext.CancellationTokenSource.Token);
        Assert.Contains("Cannot delete the last admin user", content);
    }

    [TestMethod]
    public async Task DeleteUser_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        using var freshClient = _sharedFactory!.CreateClient();
        
        var response = await freshClient.DeleteAsync("/api/v1/user/some-id", TestContext.CancellationTokenSource.Token);

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion
}