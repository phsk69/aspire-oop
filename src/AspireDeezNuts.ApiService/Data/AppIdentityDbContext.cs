using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using AspireDeezNuts.Shared.Models;

namespace AspireDeezNuts.ApiService.Data;

// Migration context - used ONLY for database migrations and schema changes
// Uses dbo/migration account with DDL permissions
public class AppMigrationDbContext(DbContextOptions<AppMigrationDbContext> options)
    : IdentityDbContext<IdentityUser>(options)
{
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Post> Posts => Set<Post>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // RefreshToken configuration
        builder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Token).IsUnique();
            entity.HasIndex(e => e.UserId);
            entity.Property(e => e.Token).IsRequired().HasMaxLength(256);
            entity.Property(e => e.UserId).IsRequired();
            entity.ToTable("RefreshTokens");
        });

        // Post configuration
        builder.Entity<Post>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Body).IsRequired();
            entity.Property(e => e.UserId).IsRequired();
            entity.HasIndex(e => e.UserId);
            entity.ToTable("Posts");
        });

        // Seed data for development
        if (Database.IsInMemory())
        {
            SeedData(builder);
        }
    }

    private static void SeedData(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Post>().HasData(
            new Post(1, 1, "Welcome to Aspire", "This is the first post in our new system."),
            new Post(2, 1, "Entity Framework Integration", "We now support both PostgreSQL and in-memory databases."),
            new Post(3, 2, "Multiple Connection Strings", "The system supports migration, read-write, and read-only connection strings.")
        );
    }
}

// Read-Write context - used for all write operations and identity management
// Uses read-write account with INSERT, UPDATE, DELETE permissions
public class AppReadWriteDbContext(DbContextOptions<AppReadWriteDbContext> options)
    : IdentityDbContext<IdentityUser>(options)
{
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Post> Posts => Set<Post>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // RefreshToken configuration
        builder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Token).IsUnique();
            entity.HasIndex(e => e.UserId);
            entity.Property(e => e.Token).IsRequired().HasMaxLength(256);
            entity.Property(e => e.UserId).IsRequired();
            entity.ToTable("RefreshTokens");
        });

        // Post configuration
        builder.Entity<Post>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Body).IsRequired();
            entity.Property(e => e.UserId).IsRequired();
            entity.HasIndex(e => e.UserId);
            entity.ToTable("Posts");
        });

        // Seed data for development
        if (Database.IsInMemory())
        {
            SeedData(builder);
        }
    }

    private static void SeedData(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Post>().HasData(
            new Post(1, 1, "Welcome to Aspire", "This is the first post in our new system."),
            new Post(2, 1, "Entity Framework Integration", "We now support both PostgreSQL and in-memory databases."),
            new Post(3, 2, "Multiple Connection Strings", "The system supports migration, read-write, and read-only connection strings.")
        );
    }
}

// Read-Only context - used for all read-only queries
// Uses read-only account with only SELECT permissions
public class AppReadOnlyDbContext(DbContextOptions<AppReadOnlyDbContext> options)
    : IdentityDbContext<IdentityUser>(options)
{
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Post> Posts => Set<Post>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
        // Ensure this context is read-only at the EF Core level
        optionsBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // RefreshToken configuration
        builder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Token).IsUnique();
            entity.HasIndex(e => e.UserId);
            entity.Property(e => e.Token).IsRequired().HasMaxLength(256);
            entity.Property(e => e.UserId).IsRequired();
            entity.ToTable("RefreshTokens");
        });

        // Post configuration
        builder.Entity<Post>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Body).IsRequired();
            entity.Property(e => e.UserId).IsRequired();
            entity.HasIndex(e => e.UserId);
            entity.ToTable("Posts");
        });

        // Seed data for development
        if (Database.IsInMemory())
        {
            SeedData(builder);
        }
    }

    private static void SeedData(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Post>().HasData(
            new Post(1, 1, "Welcome to Aspire", "This is the first post in our new system."),
            new Post(2, 1, "Entity Framework Integration", "We now support both PostgreSQL and in-memory databases."),
            new Post(3, 2, "Multiple Connection Strings", "The system supports migration, read-write, and read-only connection strings.")
        );
    }

    // Override SaveChanges to prevent writes
    public override int SaveChanges()
    {
        throw new InvalidOperationException("This context is read-only.");
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        throw new InvalidOperationException("This context is read-only.");
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("This context is read-only.");
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("This context is read-only.");
    }
}