using Microsoft.EntityFrameworkCore;

namespace AspireDeezNuts.ApiService.Data;

public enum ConnectionType
{
    Migration,
    ReadWrite,
    ReadOnly
}

public interface IDbContextService<TContext> where TContext : DbContext
{
    TContext CreateContext(ConnectionType connectionType = ConnectionType.ReadWrite);
    Task<TContext> CreateContextAsync(ConnectionType connectionType = ConnectionType.ReadWrite, CancellationToken cancellationToken = default);
    bool IsInMemory { get; }
    string? GetConnectionString(ConnectionType connectionType);
}