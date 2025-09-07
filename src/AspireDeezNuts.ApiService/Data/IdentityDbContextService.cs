using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using AspireDeezNuts.Shared.Models;

namespace AspireDeezNuts.ApiService.Data;

// Service that manages the appropriate DbContext based on operation type
public class IdentityDbContextService(
    IOptions<DatabaseOptions> databaseOptions,
    IOptions<ConnectionStrings> connectionStrings,
    IOptions<SeedDataOptions> seedDataOptions,
    ILogger<IdentityDbContextService> logger,
    UserManager<IdentityUser> userManager,
    RoleManager<IdentityRole> roleManager) : IIdentityDbService
{
    private readonly DatabaseOptions _databaseOptions = databaseOptions.Value;
    private readonly ConnectionStrings _connectionStrings = connectionStrings.Value;
    private readonly SeedDataOptions _seedDataOptions = seedDataOptions.Value;

    // Read operations use ReadOnlyDbContext
    public async Task<IdentityUser?> FindUserByIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        await using var context = GetReadOnlyContext();
        return await context.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
    }

    public async Task<IdentityUser?> FindUserByNameAsync(string userName, CancellationToken cancellationToken = default)
    {
        await using var context = GetReadOnlyContext();
        return await context.Users.FirstOrDefaultAsync(u => u.UserName == userName, cancellationToken);
    }

    public async Task<IdentityUser?> FindUserByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        await using var context = GetReadOnlyContext();
        return await context.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
    }

    public async Task<IList<IdentityUser>> GetUsersAsync(CancellationToken cancellationToken = default)
    {
        await using var context = GetReadOnlyContext();
        return await context.Users.ToListAsync(cancellationToken);
    }

    public async Task<IList<IdentityUser>> GetUsersInRoleAsync(string roleName, CancellationToken cancellationToken = default)
    {
        return await userManager.GetUsersInRoleAsync(roleName);
    }

    public async Task<IList<string>> GetRolesAsync(IdentityUser user, CancellationToken cancellationToken = default)
    {
        return await userManager.GetRolesAsync(user);
    }

    public async Task<bool> IsInRoleAsync(IdentityUser user, string role, CancellationToken cancellationToken = default)
    {
        return await userManager.IsInRoleAsync(user, role);
    }

    public async Task<bool> RoleExistsAsync(string roleName, CancellationToken cancellationToken = default)
    {
        return await roleManager.RoleExistsAsync(roleName);
    }

    public async Task<bool> CheckPasswordAsync(IdentityUser user, string password, CancellationToken cancellationToken = default)
    {
        return await userManager.CheckPasswordAsync(user, password);
    }

    // Write operations use ReadWriteDbContext
    public async Task<IdentityResult> CreateUserAsync(IdentityUser user, string password, CancellationToken cancellationToken = default)
    {
        return await userManager.CreateAsync(user, password);
    }

    public async Task<IdentityResult> UpdateUserAsync(IdentityUser user, CancellationToken cancellationToken = default)
    {
        return await userManager.UpdateAsync(user);
    }

    public async Task<IdentityResult> DeleteUserAsync(IdentityUser user, CancellationToken cancellationToken = default)
    {
        return await userManager.DeleteAsync(user);
    }

    public async Task<IdentityResult> AddToRoleAsync(IdentityUser user, string role, CancellationToken cancellationToken = default)
    {
        return await userManager.AddToRoleAsync(user, role);
    }

    public async Task<IdentityResult> RemoveFromRoleAsync(IdentityUser user, string role, CancellationToken cancellationToken = default)
    {
        return await userManager.RemoveFromRoleAsync(user, role);
    }

    public async Task<IdentityResult> CreateRoleAsync(string roleName, CancellationToken cancellationToken = default)
    {
        return await roleManager.CreateAsync(new IdentityRole(roleName));
    }

    // Migration operations use MigrationDbContext
    public async Task MigrateAsync(CancellationToken cancellationToken = default)
    {
        await using var context = GetMigrationContext();
        await context.Database.MigrateAsync(cancellationToken);
        logger.LogInformation("Database migration completed successfully");
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
            logger.LogError(ex, "Cannot connect to database");
            return false;
        }
    }

    public async Task SeedDataAsync(CancellationToken cancellationToken = default)
    {
        // Seed data uses ReadWrite context
        await using var context = GetReadWriteContext();

        // Ensure roles exist
        if (!await roleManager.RoleExistsAsync("Admin"))
        {
            await roleManager.CreateAsync(new IdentityRole("Admin"));
            logger.LogInformation("Created Admin role");
        }

        if (!await roleManager.RoleExistsAsync("User"))
        {
            await roleManager.CreateAsync(new IdentityRole("User"));
            logger.LogInformation("Created User role");
        }

        // Seed admin user from configuration if available
        if (_seedDataOptions.InitialAdmin != null &&
            !string.IsNullOrEmpty(_seedDataOptions.InitialAdmin.Email) &&
            !string.IsNullOrEmpty(_seedDataOptions.InitialAdmin.Password))
        {
            var adminEmail = _seedDataOptions.InitialAdmin.Email;
            var existingAdmin = await userManager.FindByEmailAsync(adminEmail);

            if (existingAdmin == null)
            {
                var adminUser = new IdentityUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    EmailConfirmed = true
                };

                var result = await userManager.CreateAsync(adminUser, _seedDataOptions.InitialAdmin.Password);
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(adminUser, "Admin");
                    logger.LogInformation("Seeded admin user: {Email}", adminEmail);
                }
                else
                {
                    logger.LogWarning("Failed to create admin user {Email}: {Errors}",
                        adminEmail, string.Join(", ", result.Errors.Select(e => e.Description)));
                }
            }
            else
            {
                logger.LogInformation("Admin user {Email} already exists", adminEmail);
            }
        }

        // Seed regular user from configuration if available
        if (_seedDataOptions.InitialUser != null &&
            !string.IsNullOrEmpty(_seedDataOptions.InitialUser.Email) &&
            !string.IsNullOrEmpty(_seedDataOptions.InitialUser.Password))
        {
            var userEmail = _seedDataOptions.InitialUser.Email;
            var existingUser = await userManager.FindByEmailAsync(userEmail);

            if (existingUser == null)
            {
                var regularUser = new IdentityUser
                {
                    UserName = userEmail,
                    Email = userEmail,
                    EmailConfirmed = true
                };

                var result = await userManager.CreateAsync(regularUser, _seedDataOptions.InitialUser.Password);
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(regularUser, "User");
                    logger.LogInformation("Seeded regular user: {Email}", userEmail);
                }
                else
                {
                    logger.LogWarning("Failed to create regular user {Email}: {Errors}",
                        userEmail, string.Join(", ", result.Errors.Select(e => e.Description)));
                }
            }
            else
            {
                logger.LogInformation("Regular user {Email} already exists", userEmail);
            }
        }

        // Fallback: Create default admin if no seed data is configured
        if (_seedDataOptions.InitialAdmin == null ||
            string.IsNullOrEmpty(_seedDataOptions.InitialAdmin.Email))
        {
            var defaultAdminEmail = "admin@example.com";
            var existingAdmin = await userManager.FindByEmailAsync(defaultAdminEmail);

            if (existingAdmin == null)
            {
                var adminUser = new IdentityUser
                {
                    UserName = defaultAdminEmail,
                    Email = defaultAdminEmail,
                    EmailConfirmed = true
                };

                var result = await userManager.CreateAsync(adminUser, "Admin123!");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(adminUser, "Admin");
                    logger.LogInformation("Created fallback admin user: {Email}", defaultAdminEmail);
                }
            }
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
            var databaseName = "AspireDeezNutsIdentity";
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
                var databaseName = "AspireDeezNutsIdentity";
                optionsBuilder.UseInMemoryDatabase(databaseName);
                logger.LogWarning("No PostgreSQL connection string found, falling back to InMemory database");
            }
        }
        else
        {
            var databaseName = "AspireDeezNutsIdentity";
            optionsBuilder.UseInMemoryDatabase(databaseName);
            logger.LogInformation("Using InMemory database: {DatabaseName}", databaseName);
        }
    }
}