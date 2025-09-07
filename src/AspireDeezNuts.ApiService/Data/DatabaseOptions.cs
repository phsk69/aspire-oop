namespace AspireDeezNuts.ApiService.Data;

public class DatabaseOptions
{
    public const string Database = "Database";
    
    public string Provider { get; set; } = "InMemory";
    public bool EnableSensitiveDataLogging { get; set; }
    public int CommandTimeout { get; set; } = 30;
    public bool EnableRetryOnFailure { get; set; } = true;
    public int MaxRetryCount { get; set; } = 3;
    public int MaxRetryDelay { get; set; } = 30;
    public bool AutoMigrateOnStartup { get; set; } = true;
    
    public bool UseInMemory => Provider.Equals("InMemory", StringComparison.OrdinalIgnoreCase);
    public bool UsePostgreSql => Provider.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase) || 
                                  Provider.Equals("Npgsql", StringComparison.OrdinalIgnoreCase);
}

public class ConnectionStrings
{
    public const string Section = "ConnectionStrings";
    
    // Migration connection string
    public string? MigrationConnection { get; set; }
    
    // Read-Write connection string
    public string? ReadWriteConnection { get; set; }
    
    // Read-Only connection string
    public string? ReadOnlyConnection { get; set; }
    
    // Fallback for in-memory or single connection scenarios
    public string? DefaultConnection { get; set; }
    
    // Helper methods to get appropriate connection string
    public string GetMigrationConnectionString() => 
        MigrationConnection ?? DefaultConnection ?? "DataSource=:memory:";
    
    public string GetReadWriteConnectionString() => 
        ReadWriteConnection ?? DefaultConnection ?? "DataSource=:memory:";
    
    public string GetReadOnlyConnectionString() => 
        ReadOnlyConnection ?? DefaultConnection ?? "DataSource=:memory:";
}