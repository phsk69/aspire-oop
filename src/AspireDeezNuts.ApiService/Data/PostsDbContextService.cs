using AspireDeezNuts.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AspireDeezNuts.ApiService.Data;

// Service that manages Posts operations with appropriate DbContext based on operation type
public class PostsDbContextService(
    IOptions<DatabaseOptions> databaseOptions,
    IOptions<ConnectionStrings> connectionStrings,
    ILogger<PostsDbContextService> logger) : IPostsDbService
{
    private readonly DatabaseOptions _databaseOptions = databaseOptions.Value;
    private readonly ConnectionStrings _connectionStrings = connectionStrings.Value;

    // Read operations use ReadOnlyDbContext
    public async Task<List<Post>> GetPostsAsync(int skip = 0, int take = 10, CancellationToken cancellationToken = default)
    {
        await using var context = GetReadOnlyContext();
        return await context.Posts
            .OrderByDescending(p => p.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<Post?> GetPostByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var context = GetReadOnlyContext();
        return await context.Posts.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<int> GetPostsCountAsync(CancellationToken cancellationToken = default)
    {
        await using var context = GetReadOnlyContext();
        return await context.Posts.CountAsync(cancellationToken);
    }

    public async Task<List<Post>> GetPostsByUserIdAsync(int userId, CancellationToken cancellationToken = default)
    {
        await using var context = GetReadOnlyContext();
        return await context.Posts
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> PostExistsAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var context = GetReadOnlyContext();
        return await context.Posts.AnyAsync(p => p.Id == id, cancellationToken);
    }

    // Write operations use ReadWriteDbContext
    public async Task<Post> CreatePostAsync(Post post, CancellationToken cancellationToken = default)
    {
        await using var context = GetReadWriteContext();
        context.Posts.Add(post);
        await context.SaveChangesAsync(cancellationToken);
        return post;
    }

    public async Task<Post?> UpdatePostAsync(int id, Post post, CancellationToken cancellationToken = default)
    {
        await using var context = GetReadWriteContext();
        var existingPost = await context.Posts.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (existingPost == null)
            return null;

        // Remove the existing post and add the updated one
        context.Posts.Remove(existingPost);
        var updatedPost = new Post(id, post.UserId, post.Title, post.Body);
        context.Posts.Add(updatedPost);

        await context.SaveChangesAsync(cancellationToken);
        return updatedPost;
    }

    public async Task<bool> DeletePostAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var context = GetReadWriteContext();
        var post = await context.Posts.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (post == null)
            return false;

        context.Posts.Remove(post);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    // Migration operations use MigrationDbContext
    public async Task MigrateAsync(CancellationToken cancellationToken = default)
    {
        await using var context = GetMigrationContext();
        await context.Database.MigrateAsync(cancellationToken);
        logger.LogInformation("Posts database migration completed successfully");
    }

    public async Task<bool> CanConnectAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = GetReadOnlyContext();
            return await context.Database.CanConnectAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Cannot connect to Posts database");
            return false;
        }
    }

    // Helper methods to get the appropriate context
    private AppMigrationDbContext GetMigrationContext()
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppMigrationDbContext>();
        ConfigureDbContext(optionsBuilder, ConnectionType.Migration);
        return new AppMigrationDbContext(optionsBuilder.Options);
    }

    private AppReadWriteDbContext GetReadWriteContext()
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppReadWriteDbContext>();
        ConfigureDbContext(optionsBuilder, ConnectionType.ReadWrite);
        return new AppReadWriteDbContext(optionsBuilder.Options);
    }

    private AppReadOnlyDbContext GetReadOnlyContext()
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppReadOnlyDbContext>();
        ConfigureDbContext(optionsBuilder, ConnectionType.ReadOnly);
        return new AppReadOnlyDbContext(optionsBuilder.Options);
    }

    private void ConfigureDbContext<TContext>(DbContextOptionsBuilder<TContext> optionsBuilder, ConnectionType connectionType)
        where TContext : DbContext
    {
        if (_databaseOptions.EnableSensitiveDataLogging)
        {
            optionsBuilder.EnableSensitiveDataLogging();
        }

        optionsBuilder.LogTo(message => logger.LogDebug(message));

        if (_databaseOptions.UseInMemory)
        {
            var databaseName = "AspireDeezNutsPosts";
            optionsBuilder.UseInMemoryDatabase(databaseName);
            logger.LogInformation("Using InMemory database: {DatabaseName}", databaseName);
        }
        else if (_databaseOptions.UsePostgreSql)
        {
            var connectionString = connectionType switch
            {
                ConnectionType.Migration => _connectionStrings.GetMigrationConnectionString(),
                ConnectionType.ReadWrite => _connectionStrings.GetReadWriteConnectionString(),
                ConnectionType.ReadOnly => _connectionStrings.GetReadOnlyConnectionString(),
                _ => _connectionStrings.GetReadWriteConnectionString()
            };

            if (!string.IsNullOrEmpty(connectionString) && connectionString != "DataSource=:memory:")
            {
                optionsBuilder.UseNpgsql(connectionString, npgsqlOptions =>
                {
                    npgsqlOptions.CommandTimeout(_databaseOptions.CommandTimeout);
                    if (_databaseOptions.EnableRetryOnFailure)
                    {
                        npgsqlOptions.EnableRetryOnFailure(
                            maxRetryCount: _databaseOptions.MaxRetryCount,
                            maxRetryDelay: TimeSpan.FromSeconds(_databaseOptions.MaxRetryDelay),
                            errorCodesToAdd: null);
                    }
                });
                logger.LogInformation("Using PostgreSQL database with {ConnectionType} connection", connectionType);
            }
            else
            {
                var databaseName = "AspireDeezNutsPosts";
                optionsBuilder.UseInMemoryDatabase(databaseName);
                logger.LogWarning("No PostgreSQL connection string found, falling back to InMemory database");
            }
        }
        else
        {
            var databaseName = "AspireDeezNutsPosts";
            optionsBuilder.UseInMemoryDatabase(databaseName);
            logger.LogInformation("Using InMemory database: {DatabaseName}", databaseName);
        }
    }
}