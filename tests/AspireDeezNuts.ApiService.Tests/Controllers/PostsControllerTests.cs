using AspireDeezNuts.ApiService.Data;
using AspireDeezNuts.ApiService.Tests.Builders;
using AspireDeezNuts.ApiService.Tests.Factories;
using AspireDeezNuts.ApiService.Tests.Repositories;
using AspireDeezNuts.Shared.Interfaces;
using AspireDeezNuts.Shared.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Net;

namespace AspireDeezNuts.ApiService.Tests.Controllers;

[TestClass]
public class PostsControllerTests
{
    public TestContext TestContext { get; set; } = null!;
    private WebApplicationFactory<Program>? _factory;
    private HttpClient? _client;
    private InMemoryPostRepository? _repository;
    private string? _jwtToken;

    [TestInitialize]
    public async Task Setup()
    {
        _repository = new InMemoryPostRepository();

        // Use configuration from local secrets file
        var testConfig = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddJsonFile("appsettings.Development.secrets.json", optional: false, reloadOnChange: false)
            .Build();

        _factory = new WebApplicationFactory<Program>()
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
                    // Use a unique database name for each test method
                    var databaseName = $"TestDb_PostsController_{Guid.NewGuid()}";

                    // Remove the existing DbContext registration
                    var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppMigrationDbContext>));
                    if (descriptor != null)
                    {
                        services.Remove(descriptor);
                    }

                    // Add test database
                    services.AddDbContext<AppMigrationDbContext>(options =>
                        options.UseInMemoryDatabase(databaseName));

                    // Replace the registered IPostRepository with our test implementation
                    var repoDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IPostRepository));
                    if (repoDescriptor != null)
                    {
                        services.Remove(repoDescriptor);
                    }

                    services.AddSingleton<IPostRepository>(_repository);
                    services.AddSingleton<IRepositoryFactory>(new TestRepositoryFactory(_repository));
                });
            });

        _client = _factory.CreateClient();

        // Authenticate and get JWT token
        await AuthenticateAsync();
    }

    [TestCleanup]
    public void Cleanup()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }

    private async Task AuthenticateAsync()
    {
        // Get the initial admin credentials from configuration
        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.Development.secrets.json", optional: false)
            .Build();

        var adminEmail = config["SeedData:InitialAdmin:Email"];
        var adminPassword = config["SeedData:InitialAdmin:Password"];

        var loginRequest = new { Email = adminEmail, Password = adminPassword };
        var response = await _client!.PostAsJsonAsync("/api/v1/auth/login", loginRequest, TestContext.CancellationTokenSource.Token);

        Assert.IsTrue(response.IsSuccessStatusCode, $"Authentication failed: {response.StatusCode}");

        var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>(TestContext.CancellationTokenSource.Token);
        Assert.IsNotNull(loginResponse?.AccessToken, "Failed to get access token");

        _jwtToken = loginResponse.AccessToken;
        _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _jwtToken);
    }

    private class LoginResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public int ExpiresIn { get; set; }
    }

    [TestMethod]
    public async Task GetPosts_WithoutAuth_ShouldReturnUnauthorized()
    {
        // Arrange - Remove authentication header
        _client!.DefaultRequestHeaders.Authorization = null;

        // Act
        var response = await _client.GetAsync("/api/v1/posts", TestContext.CancellationTokenSource.Token);

        // Assert
        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [TestMethod]
    public async Task GetPosts_WithAuth_ShouldReturnAllPosts()
    {
        // Arrange - using Builder Pattern
        var expectedPosts = PostCollectionBuilder.Create()
            .AddPostsForUser(1, 2, "User 1 Post")
            .AddPostsForUser(2, 1, "User 2 Post")
            .Build();

        _repository!.Clear();
        _repository.AddRange(expectedPosts);

        // Act
        var response = await _client!.GetAsync("/api/v1/posts", TestContext.CancellationTokenSource.Token);

        // Assert
        response.EnsureSuccessStatusCode();
        var posts = await response.Content.ReadFromJsonAsync<List<Post>>(TestContext.CancellationTokenSource.Token);

        Assert.IsNotNull(posts);
        Assert.AreEqual(expectedPosts.Count, posts.Count);
    }

    [TestMethod]
    public async Task GetPost_WithValidId_ShouldReturnPost()
    {
        // Arrange - using Builder Pattern
        var expectedPost = PostBuilder.Create()
            .WithId(1)
            .WithUserId(1)
            .WithTitle("Test Post")
            .WithBody("Test body")
            .Build();

        _repository!.Clear();
        await _repository.CreateAsync(expectedPost);

        // Act
        var response = await _client!.GetAsync("/api/v1/posts/1", TestContext.CancellationTokenSource.Token);

        // Assert
        response.EnsureSuccessStatusCode();
        var post = await response.Content.ReadFromJsonAsync<Post>(TestContext.CancellationTokenSource.Token);

        Assert.IsNotNull(post);
        Assert.AreEqual(expectedPost.Id, post.Id);
        Assert.AreEqual(expectedPost.Title, post.Title);
        Assert.AreEqual(expectedPost.Body, post.Body);
        Assert.AreEqual(expectedPost.UserId, post.UserId);
    }

    [TestMethod]
    public async Task GetPost_WithInvalidId_ShouldReturnNotFound()
    {
        // Arrange
        _repository!.Clear();

        // Act
        var response = await _client!.GetAsync("/api/v1/posts/999", TestContext.CancellationTokenSource.Token);

        // Assert
        Assert.AreEqual(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }

    [TestMethod]
    public async Task GetPostsByUser_ShouldReturnUserPosts()
    {
        // Arrange - using Builder Pattern to create posts for different users
        var user1Posts = PostCollectionBuilder.Create()
            .AddPostsForUser(1, 3, "User 1 Post")
            .Build();

        var user2Posts = PostCollectionBuilder.Create()
            .AddPostsForUser(2, 2, "User 2 Post")
            .Build();

        _repository!.Clear();
        _repository.AddRange(user1Posts);
        _repository.AddRange(user2Posts);

        // Act
        var response = await _client!.GetAsync("/api/v1/posts/user/1", TestContext.CancellationTokenSource.Token);

        // Assert
        response.EnsureSuccessStatusCode();
        var posts = await response.Content.ReadFromJsonAsync<List<Post>>(TestContext.CancellationTokenSource.Token);

        Assert.IsNotNull(posts);
        Assert.HasCount(3, posts);
        Assert.IsTrue(posts.All(p => p.UserId == 1));
    }

    [TestMethod]
    public async Task SearchPosts_WithValidTitle_ShouldReturnMatchingPosts()
    {
        // Arrange - using Builder Pattern
        var posts = PostCollectionBuilder.Create()
            .AddPost(builder => builder.WithTitle("JavaScript Tutorial"))
            .AddPost(builder => builder.WithTitle("Python Guide"))
            .AddPost(builder => builder.WithTitle("Advanced JavaScript"))
            .AddPost(builder => builder.WithTitle("C# Fundamentals"))
            .Build();

        _repository!.Clear();
        _repository.AddRange(posts);

        // Act
        var response = await _client!.GetAsync("/api/v1/posts/search?title=JavaScript", TestContext.CancellationTokenSource.Token);

        // Assert
        response.EnsureSuccessStatusCode();
        var matchingPosts = await response.Content.ReadFromJsonAsync<List<Post>>(TestContext.CancellationTokenSource.Token);

        Assert.IsNotNull(matchingPosts);
        Assert.HasCount(2, matchingPosts);
        Assert.IsTrue(matchingPosts.All(p => p.Title.Contains("JavaScript", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public async Task SearchPosts_WithEmptyTitle_ShouldReturnBadRequest()
    {
        // Act
        var response = await _client!.GetAsync("/api/v1/posts/search?title=", TestContext.CancellationTokenSource.Token);

        // Assert
        Assert.AreEqual(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod]
    public async Task SearchPosts_WithoutTitleParameter_ShouldReturnBadRequest()
    {
        // Act
        var response = await _client!.GetAsync("/api/v1/posts/search", TestContext.CancellationTokenSource.Token);

        // Assert
        Assert.AreEqual(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }
}

[TestClass]
public class RepositoryFactoryTests
{
    [TestMethod]
    public void TestRepositoryFactory_ShouldCreateInMemoryRepository()
    {
        // Arrange
        var factory = new TestRepositoryFactory();

        // Act
        var repository = factory.CreatePostRepository();

        // Assert
        Assert.IsInstanceOfType<InMemoryPostRepository>(repository);
    }

    [TestMethod]
    public void SeededTestRepositoryFactory_ShouldCreateRepositoryWithTestData()
    {
        // Arrange - using Builder Pattern
        var testPosts = PostCollectionBuilder.Create()
            .AddPostsForUser(1, 2, "Seeded Post")
            .Build();

        var factory = new SeededTestRepositoryFactory(testPosts);

        // Act
        var repository = factory.CreatePostRepository();
        var posts = repository.ReadAsync().Result;

        // Assert
        Assert.AreEqual(testPosts.Count, posts.Count);
        Assert.AreEqual("Seeded Post 1", posts.First().Title);
    }
}

[TestClass]
public class PostBuilderTests
{
    [TestMethod]
    public void PostBuilder_WithDefaultValues_ShouldCreateValidPost()
    {
        // Act
        var post = PostBuilder.Create().Build();

        // Assert
        Assert.AreEqual(1, post.Id);
        Assert.AreEqual(1, post.UserId);
        Assert.AreEqual("Default Test Post", post.Title);
        Assert.AreEqual("Default test post body content", post.Body);
    }

    [TestMethod]
    public void PostBuilder_WithCustomValues_ShouldCreateValidPost()
    {
        // Act
        var post = PostBuilder.Create()
            .WithId(42)
            .WithUserId(5)
            .WithTitle("Custom Title")
            .WithBody("Custom body content")
            .Build();

        // Assert
        Assert.AreEqual(42, post.Id);
        Assert.AreEqual(5, post.UserId);
        Assert.AreEqual("Custom Title", post.Title);
        Assert.AreEqual("Custom body content", post.Body);
    }

    [TestMethod]
    public void PostCollectionBuilder_ShouldCreateMultiplePosts()
    {
        // Act
        var posts = PostCollectionBuilder.Create()
            .AddPostsForUser(1, 3, "Test Post")
            .Build();

        // Assert
        Assert.HasCount(3, posts);
        Assert.IsTrue(posts.All(p => p.UserId == 1));
        Assert.AreEqual("Test Post 1", posts[0].Title);
        Assert.AreEqual("Test Post 3", posts[2].Title);
    }
}