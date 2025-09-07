using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AspireDeezNuts.ApiService.Data;

public abstract class DbContextServiceBase<TContext>(
    IConfiguration configuration,
    IServiceProvider serviceProvider,
    IOptions<DatabaseOptions> databaseOptions,
    ILogger logger) : IDbContextService<TContext>
    where TContext : DbContext
{
    protected readonly IConfiguration _configuration = configuration;
    protected readonly IServiceProvider _serviceProvider = serviceProvider;
    protected readonly DatabaseOptions _databaseOptions = databaseOptions.Value;
    protected readonly ILogger _logger = logger;

    public bool IsInMemory => _databaseOptions.UseInMemory;

    public string? GetConnectionString(ConnectionType connectionType)
    {
        if (_databaseOptions.UseInMemory)
            return null;

        var connectionStringName = connectionType switch
        {
            ConnectionType.Migration => "Migration",
            ConnectionType.ReadWrite => "ReadWrite",
            ConnectionType.ReadOnly => "ReadOnly",
            _ => "ReadWrite"
        };

        var connectionString = _configuration.GetConnectionString(connectionStringName);

        if (string.IsNullOrEmpty(connectionString))
        {
            _logger.LogWarning("Connection string '{ConnectionStringName}' not found, falling back to ReadWrite", connectionStringName);
            connectionString = _configuration.GetConnectionString("ReadWrite");
        }

        if (string.IsNullOrEmpty(connectionString) && !_databaseOptions.UseInMemory)
        {
            _logger.LogWarning("No connection strings found, falling back to InMemory database");
        }

        return connectionString;
    }

    public TContext CreateContext(ConnectionType connectionType = ConnectionType.ReadWrite)
    {
        var optionsBuilder = new DbContextOptionsBuilder<TContext>();
        ConfigureDbContext(optionsBuilder, connectionType);
        return CreateContextInstance(optionsBuilder.Options);
    }

    public Task<TContext> CreateContextAsync(ConnectionType connectionType = ConnectionType.ReadWrite, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(CreateContext(connectionType));
    }

    protected virtual void ConfigureDbContext(DbContextOptionsBuilder<TContext> optionsBuilder, ConnectionType connectionType)
    {
        if (_databaseOptions.EnableSensitiveDataLogging)
        {
            optionsBuilder.EnableSensitiveDataLogging();
        }

        optionsBuilder.LogTo(message => _logger.LogDebug(message));

        if (_databaseOptions.UseInMemory)
        {
            var databaseName = typeof(TContext).Name.Replace("DbContext", "");
            optionsBuilder.UseInMemoryDatabase(databaseName);
            _logger.LogInformation("Using InMemory database: {DatabaseName}", databaseName);
        }
        else if (_databaseOptions.UsePostgreSql)
        {
            var connectionString = GetConnectionString(connectionType);
            if (!string.IsNullOrEmpty(connectionString))
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
                _logger.LogInformation("Using PostgreSQL database with {ConnectionType} connection", connectionType);
            }
            else
            {
                var databaseName = typeof(TContext).Name.Replace("DbContext", "");
                optionsBuilder.UseInMemoryDatabase(databaseName);
                _logger.LogWarning("No PostgreSQL connection string found, falling back to InMemory database");
            }
        }
        else
        {
            var databaseName = typeof(TContext).Name.Replace("DbContext", "");
            optionsBuilder.UseInMemoryDatabase(databaseName);
            _logger.LogInformation("Using InMemory database: {DatabaseName}", databaseName);
        }
    }

    protected abstract TContext CreateContextInstance(DbContextOptions<TContext> options);
}