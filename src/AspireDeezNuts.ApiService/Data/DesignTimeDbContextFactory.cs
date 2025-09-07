using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AspireDeezNuts.ApiService.Data;

public class AppMigrationDbContextFactory : IDesignTimeDbContextFactory<AppMigrationDbContext>
{
    public AppMigrationDbContext CreateDbContext(string[] args)
    {
        // Create service collection for DI
        var services = new ServiceCollection();

        // Add logging
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        // Build configuration exactly like Program.cs does
        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables();

        // Add secret configuration file if it exists (same as Program.cs)
        var secretsPath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.Development.secrets.json");
        if (File.Exists(secretsPath))
        {
            builder.AddJsonFile(secretsPath, optional: true, reloadOnChange: false);
        }

        var configuration = builder.Build();

        // Configure options like Program.cs
        services.Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.Database));
        services.Configure<ConnectionStrings>(configuration.GetSection(ConnectionStrings.Section));

        var serviceProvider = services.BuildServiceProvider();
        var logger = serviceProvider.GetRequiredService<ILogger<AppMigrationDbContextFactory>>();
        var databaseOptions = serviceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
        var connectionStrings = serviceProvider.GetRequiredService<IOptions<ConnectionStrings>>().Value;

        logger.LogInformation("Starting DesignTimeDbContextFactory");
        logger.LogInformation("Secrets file exists: {SecretsExists}", File.Exists(secretsPath));
        logger.LogInformation("Database Provider: {Provider}", databaseOptions.Provider);
        logger.LogInformation("UsePostgreSql: {UsePostgreSql}", databaseOptions.UsePostgreSql);

        var optionsBuilder = new DbContextOptionsBuilder<AppMigrationDbContext>();

        if (databaseOptions.UsePostgreSql)
        {
            var connectionString = connectionStrings.GetMigrationConnectionString();
            logger.LogInformation("Migration Connection String: {HasConnection}",
                string.IsNullOrEmpty(connectionString) ? "NULL/EMPTY" : "Found");
            logger.LogInformation("Connection String Value: {ConnectionString}",
                string.IsNullOrEmpty(connectionString) ? "NULL" : connectionString.Substring(0, Math.Min(50, connectionString.Length)) + "...");

            if (!string.IsNullOrEmpty(connectionString) && connectionString != "DataSource=:memory:")
            {
                optionsBuilder.UseNpgsql(connectionString, npgsqlOptions =>
                {
                    npgsqlOptions.CommandTimeout(databaseOptions.CommandTimeout);
                    if (databaseOptions.EnableRetryOnFailure)
                    {
                        npgsqlOptions.EnableRetryOnFailure(
                            maxRetryCount: databaseOptions.MaxRetryCount,
                            maxRetryDelay: TimeSpan.FromSeconds(databaseOptions.MaxRetryDelay),
                            errorCodesToAdd: null);
                    }
                });
                logger.LogInformation("Using PostgreSQL for migrations with MigrationConnection from configuration");
            }
            else
            {
                // Fallback to read-write connection
                var fallbackConnectionString = connectionStrings.GetReadWriteConnectionString();
                if (!string.IsNullOrEmpty(fallbackConnectionString) && fallbackConnectionString != "DataSource=:memory:")
                {
                    optionsBuilder.UseNpgsql(fallbackConnectionString);
                    logger.LogWarning("Using PostgreSQL with ReadWrite connection string for migrations (fallback)");
                }
                else
                {
                    logger.LogError("No valid PostgreSQL connection string found for migrations");
                    throw new InvalidOperationException("No valid PostgreSQL connection string found for migrations. Please check your appsettings.Development.secrets.json file.");
                }
            }
        }
        else
        {
            logger.LogError("InMemory database provider is not supported for migrations");
            throw new InvalidOperationException("InMemory database provider is not supported for migrations. Please set Database:Provider to 'PostgreSQL' in appsettings.json.");
        }

        if (databaseOptions.EnableSensitiveDataLogging)
        {
            optionsBuilder.EnableSensitiveDataLogging();
            logger.LogInformation("Sensitive data logging enabled");
        }

        logger.LogInformation("DbContext configuration completed successfully");
        return new AppMigrationDbContext(optionsBuilder.Options);
    }
}