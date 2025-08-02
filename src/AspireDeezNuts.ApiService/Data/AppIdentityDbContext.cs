using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AspireDeezNuts.ApiService.Data;

public class AppIdentityDbContext(DbContextOptions<AppIdentityDbContext> options)
    : IdentityDbContext<IdentityUser>(options)
{
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Configure RefreshToken entity
        builder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Token).IsUnique();
            entity.HasIndex(e => e.UserId);
            entity.Property(e => e.Token).IsRequired().HasMaxLength(256);
            entity.Property(e => e.UserId).IsRequired();
        });

        // Seed roles
        var adminRoleId = "a18be9c0-aa65-4af8-bd17-00bd9344e575";
        var userRoleId = "cfafcfcd-d796-43f4-8ac0-ead43bd7f709";

        builder.Entity<IdentityRole>().HasData(
            new IdentityRole
            {
                Id = adminRoleId,
                Name = "Admin",
                NormalizedName = "ADMIN"
            },
            new IdentityRole
            {
                Id = userRoleId,
                Name = "User",
                NormalizedName = "USER"
            }
        );

        // Seed users
        var adminUserId = "a18be9c0-aa65-4af8-bd17-00bd9344e576";
        var regularUserId = "cfafcfcd-d796-43f4-8ac0-ead43bd7f710";

        var hasher = new PasswordHasher<IdentityUser>();

        builder.Entity<IdentityUser>().HasData(
            new IdentityUser
            {
                Id = adminUserId,
                UserName = "admin@example.com",
                NormalizedUserName = "ADMIN@EXAMPLE.COM",
                Email = "admin@example.com",
                NormalizedEmail = "ADMIN@EXAMPLE.COM",
                EmailConfirmed = true,
                PasswordHash = hasher.HashPassword(null!, "AdminPass123!"),
                SecurityStamp = Guid.NewGuid().ToString()
            },
            new IdentityUser
            {
                Id = regularUserId,
                UserName = "user@example.com",
                NormalizedUserName = "USER@EXAMPLE.COM",
                Email = "user@example.com",
                NormalizedEmail = "USER@EXAMPLE.COM",
                EmailConfirmed = true,
                PasswordHash = hasher.HashPassword(null!, "UserPass123!"),
                SecurityStamp = Guid.NewGuid().ToString()
            }
        );

        // Assign roles to users
        builder.Entity<IdentityUserRole<string>>().HasData(
            new IdentityUserRole<string>
            {
                RoleId = adminRoleId,
                UserId = adminUserId
            },
            new IdentityUserRole<string>
            {
                RoleId = userRoleId,
                UserId = regularUserId
            }
        );
    }
}

public class RefreshToken
{
    public int Id { get; set; }
    public required string Token { get; set; }
    public required string UserId { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsRevoked { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RevokedAt { get; set; }
}