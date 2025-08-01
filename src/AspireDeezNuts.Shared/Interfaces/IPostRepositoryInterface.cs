using AspireDeezNuts.Shared.Models;

namespace AspireDeezNuts.Shared.Interfaces;

public interface IPostRepository : IRepository<Post, long>
{
    List<Post> GetByUserId(long userId);
    List<Post> GetByTitle(string titleSearch);
}