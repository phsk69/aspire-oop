using AspireDeezNuts.Shared.Interfaces;
using AspireDeezNuts.Shared.Models;

namespace AspireDeezNuts.ApiService.Tests.Repositories;

/// <summary>
/// Strategy Pattern implementation: In-memory post repository for testing
/// Provides fast, predictable data access without external dependencies
/// </summary>
public class InMemoryPostRepository : IPostRepository
{
    private readonly List<Post> _posts;
    private long _nextId;

    public InMemoryPostRepository()
    {
        _posts = [];
        _nextId = 1;
        SeedTestData();
    }

    public InMemoryPostRepository(IEnumerable<Post> initialPosts)
    {
        _posts = [.. initialPosts];
        _nextId = _posts.Count > 0 ? _posts.Max(p => p.Id) + 1 : 1;
    }

    public Task<List<Post>> ReadAsync()
    {
        return Task.FromResult(_posts.ToList());
    }

    public Task<Post?> ReadByIdAsync(long id)
    {
        var post = _posts.FirstOrDefault(p => p.Id == id);
        return Task.FromResult(post);
    }

    public Task<Post> CreateAsync(Post entity)
    {
        var newPost = entity with { Id = _nextId++ };
        _posts.Add(newPost);
        return Task.FromResult(newPost);
    }

    public Task<Post> UpdateAsync(Post entity)
    {
        var index = _posts.FindIndex(p => p.Id == entity.Id);
        if (index == -1)
        {
            throw new InvalidOperationException($"Post with ID {entity.Id} not found");
        }

        _posts[index] = entity;
        return Task.FromResult(entity);
    }

    public Task<Post> DeleteAsync(Post entity)
    {
        var removed = _posts.RemoveAll(p => p.Id == entity.Id);
        if (removed == 0)
        {
            throw new InvalidOperationException($"Post with ID {entity.Id} not found");
        }

        return Task.FromResult(entity);
    }

    public Task<List<Post>> GetByUserIdAsync(long userId)
    {
        var userPosts = _posts.Where(p => p.UserId == userId).ToList();
        return Task.FromResult(userPosts);
    }

    public Task<List<Post>> GetByTitleAsync(string titleSearch)
    {
        var matchingPosts = _posts
            .Where(p => p.Title.Contains(titleSearch, StringComparison.OrdinalIgnoreCase))
            .ToList();
        return Task.FromResult(matchingPosts);
    }

    private void SeedTestData()
    {
        var testPosts = new[]
        {
            new Post(1, 1, "Test Post 1", "This is the body of test post 1"),
            new Post(2, 1, "Test Post 2", "This is the body of test post 2"),
            new Post(3, 2, "Another Post", "This is from user 2"),
            new Post(4, 2, "Fourth Post", "Another post from user 2"),
            new Post(5, 3, "Final Test Post", "This is the last test post")
        };

        _posts.AddRange(testPosts);
        _nextId = 6;
    }

    public void Clear()
    {
        _posts.Clear();
        _nextId = 1;
    }

    public void AddRange(IEnumerable<Post> posts)
    {
        _posts.AddRange(posts);
        _nextId = _posts.Count > 0 ? _posts.Max(p => p.Id) + 1 : 1;
    }
}