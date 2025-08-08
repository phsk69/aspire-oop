using AspireDeezNuts.Shared.Models;

namespace AspireDeezNuts.Shared.Interfaces;

public interface IPostRepository : IRepository<Post, long>
{
    Task<List<Post>> GetByUserIdAsync(long userId);
    Task<List<Post>> GetByTitleAsync(string titleSearch);
}