using AspireDeezNuts.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace AspireDeezNuts.ApiService.Data;

public interface IPostsDbService
{
    Task<List<Post>> GetPostsAsync(int skip = 0, int take = 10, CancellationToken cancellationToken = default);
    Task<Post?> GetPostByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Post> CreatePostAsync(Post post, CancellationToken cancellationToken = default);
    Task<Post?> UpdatePostAsync(int id, Post post, CancellationToken cancellationToken = default);
    Task<bool> DeletePostAsync(int id, CancellationToken cancellationToken = default);
    Task<int> GetPostsCountAsync(CancellationToken cancellationToken = default);
    Task<List<Post>> GetPostsByUserIdAsync(int userId, CancellationToken cancellationToken = default);
    Task<bool> PostExistsAsync(int id, CancellationToken cancellationToken = default);
    Task MigrateAsync(CancellationToken cancellationToken = default);
    Task<bool> CanConnectAsync(CancellationToken cancellationToken = default);
}