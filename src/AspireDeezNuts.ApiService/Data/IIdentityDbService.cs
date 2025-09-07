using Microsoft.AspNetCore.Identity;

namespace AspireDeezNuts.ApiService.Data;

public interface IIdentityDbService
{
    Task<IdentityResult> CreateUserAsync(IdentityUser user, string password, CancellationToken cancellationToken = default);
    Task<IdentityResult> UpdateUserAsync(IdentityUser user, CancellationToken cancellationToken = default);
    Task<IdentityResult> DeleteUserAsync(IdentityUser user, CancellationToken cancellationToken = default);
    Task<IdentityUser?> FindUserByIdAsync(string userId, CancellationToken cancellationToken = default);
    Task<IdentityUser?> FindUserByNameAsync(string userName, CancellationToken cancellationToken = default);
    Task<IdentityUser?> FindUserByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<IList<IdentityUser>> GetUsersAsync(CancellationToken cancellationToken = default);
    Task<IList<IdentityUser>> GetUsersInRoleAsync(string roleName, CancellationToken cancellationToken = default);
    Task<bool> CheckPasswordAsync(IdentityUser user, string password, CancellationToken cancellationToken = default);
    Task<IdentityResult> AddToRoleAsync(IdentityUser user, string role, CancellationToken cancellationToken = default);
    Task<IdentityResult> RemoveFromRoleAsync(IdentityUser user, string role, CancellationToken cancellationToken = default);
    Task<IList<string>> GetRolesAsync(IdentityUser user, CancellationToken cancellationToken = default);
    Task<bool> IsInRoleAsync(IdentityUser user, string role, CancellationToken cancellationToken = default);
    Task<IdentityResult> CreateRoleAsync(string roleName, CancellationToken cancellationToken = default);
    Task<bool> RoleExistsAsync(string roleName, CancellationToken cancellationToken = default);
    Task MigrateAsync(CancellationToken cancellationToken = default);
    Task<bool> CanConnectAsync(CancellationToken cancellationToken = default);
    Task SeedDataAsync(CancellationToken cancellationToken = default);
}