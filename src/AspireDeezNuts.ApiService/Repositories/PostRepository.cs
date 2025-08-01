using AspireDeezNuts.Shared.Interfaces;
using AspireDeezNuts.Shared.Models;

namespace AspireDeezNuts.ApiService.Repositories;

public class PostRepository(HttpClient httpClient, ILogger<PostRepository> logger) : IPostRepository
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly ILogger<PostRepository> _logger = logger;
    private readonly string _apiUrl = "https://jsonplaceholder.typicode.com/posts";

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

    public List<Post> Read()
    {
        return ReadAsync().GetAwaiter().GetResult();
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

    public Post ReadById(long id)
    {
        var post = ReadByIdAsync(id).GetAwaiter().GetResult();
        return post ?? throw new InvalidOperationException($"Post with ID {id} not found");
    }

    public Post Create(Post entity)
    {
        throw new NotImplementedException("This is a read-only repository for demo purposes");
    }

    public Post Update(Post entity)
    {
        throw new NotImplementedException("This is a read-only repository for demo purposes");
    }

    public Post Delete(Post entity)
    {
        throw new NotImplementedException("This is a read-only repository for demo purposes");
    }

    public List<Post> GetByUserId(long userId)
    {
        var allPosts = Read();
        return [.. allPosts.Where(p => p.UserId == userId)];
    }

    public List<Post> GetByTitle(string titleSearch)
    {
        var allPosts = Read();
        return [.. allPosts.Where(p => p.Title.Contains(titleSearch, StringComparison.OrdinalIgnoreCase))];
    }
}