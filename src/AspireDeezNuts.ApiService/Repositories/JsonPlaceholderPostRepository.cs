using AspireDeezNuts.Shared.Interfaces;
using AspireDeezNuts.Shared.Models;
using Microsoft.Extensions.Options;

namespace AspireDeezNuts.ApiService.Repositories;

public class JsonPlaceholderPostRepository(HttpClient httpClient, ILogger<JsonPlaceholderPostRepository> logger, IOptions<JsonPlaceholderOptions> options) : IPostRepository
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly ILogger<JsonPlaceholderPostRepository> _logger = logger;
    private readonly string _apiUrl = $"{options.Value.BaseUrl}/posts";

    public async Task<List<Post>> ReadAsync()
    {
        try
        {
            var posts = await _httpClient.GetFromJsonAsync<List<Post>>(_apiUrl);
            return posts ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching posts");
            return [];
        }
    }


    public async Task<Post?> ReadByIdAsync(long id)
    {
        try
        {
            var post = await _httpClient.GetFromJsonAsync<Post>($"{_apiUrl}/{id}");
            return post;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching post with id {PostId}", id);
            return null;
        }
    }


    public Task<Post> CreateAsync(Post entity)
    {
        throw new NotImplementedException("This is a read-only repository for demo purposes");
    }

    public Task<Post> UpdateAsync(Post entity)
    {
        throw new NotImplementedException("This is a read-only repository for demo purposes");
    }

    public Task<Post> DeleteAsync(Post entity)
    {
        throw new NotImplementedException("This is a read-only repository for demo purposes");
    }

    public async Task<List<Post>> GetByUserIdAsync(long userId)
    {
        var allPosts = await ReadAsync();
        return [.. allPosts.Where(p => p.UserId == userId)];
    }

    public async Task<List<Post>> GetByTitleAsync(string titleSearch)
    {
        var allPosts = await ReadAsync();
        return [.. allPosts.Where(p => p.Title.Contains(titleSearch, StringComparison.OrdinalIgnoreCase))];
    }
}

public class JsonPlaceholderOptions
{
    public string BaseUrl { get; set; } = "https://jsonplaceholder.typicode.com";
    public int Timeout { get; set; } = 30;
}