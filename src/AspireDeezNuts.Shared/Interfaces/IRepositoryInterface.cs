namespace AspireDeezNuts.Shared.Interfaces;

public interface IRepository<T, K>
{
    Task<List<T>> ReadAsync();
    Task<T?> ReadByIdAsync(K id);
    Task<T> CreateAsync(T entity);
    Task<T> UpdateAsync(T entity);
    Task<T> DeleteAsync(T entity);
}
