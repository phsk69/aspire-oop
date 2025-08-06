using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AspireDeezNuts.ApiService.Services;

public interface IDataSeeder
{
    Task SeedAsync();
}

public class DataSeeder(
    UserManager<IdentityUser> userManager,
    RoleManager<IdentityRole> roleManager,
    IConfiguration configuration,
    ILogger<DataSeeder> logger) : IDataSeeder
{
    private readonly UserManager<IdentityUser> _userManager = userManager;
    private readonly RoleManager<IdentityRole> _roleManager = roleManager;
    private readonly IConfiguration _configuration = configuration;
    private readonly ILogger<DataSeeder> _logger = logger;

    public async Task SeedAsync()
    {
        try
        {
            await SeedRolesAsync();
            await SeedInitialAdminAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while seeding the database");
            throw;
        }
    }

    private async Task SeedRolesAsync()
    {
        string[] roleNames = ["Admin", "User"];
        
        foreach (var roleName in roleNames)
        {
            var roleExists = await _roleManager.RoleExistsAsync(roleName);
            if (!roleExists)
            {
                var result = await _roleManager.CreateAsync(new IdentityRole(roleName));
                if (result.Succeeded)
                {
                    _logger.LogInformation("Created role: {RoleName}", roleName);
                }
                else
                {
                    _logger.LogError("Failed to create role {RoleName}: {Errors}", 
                        roleName, string.Join(", ", result.Errors.Select(e => e.Description)));
                }
            }
        }
    }

    private async Task SeedInitialAdminAsync()
    {
        var adminUsers = await _userManager.GetUsersInRoleAsync("Admin");
        if (adminUsers.Any())
        {
            _logger.LogInformation("Admin user already exists, skipping initial admin seeding");
            return;
        }

        var seedConfig = _configuration.GetSection("SeedData:InitialAdmin");
        var adminEmail = seedConfig["Email"];
        var adminPassword = seedConfig["Password"];

        if (string.IsNullOrEmpty(adminEmail) || string.IsNullOrEmpty(adminPassword))
        {
            _logger.LogWarning("Initial admin configuration not found or incomplete. Skipping admin seeding");
            return;
        }

        var existingUser = await _userManager.FindByEmailAsync(adminEmail);
        if (existingUser != null)
        {
            _logger.LogInformation("User with email {Email} already exists, adding to Admin role", adminEmail);
            var addToRoleResult = await _userManager.AddToRoleAsync(existingUser, "Admin");
            if (!addToRoleResult.Succeeded)
            {
                _logger.LogError("Failed to add existing user to Admin role: {Errors}",
                    string.Join(", ", addToRoleResult.Errors.Select(e => e.Description)));
            }
            return;
        }

        var adminUser = new IdentityUser
        {
            UserName = adminEmail,
            Email = adminEmail,
            EmailConfirmed = true
        };

        var createResult = await _userManager.CreateAsync(adminUser, adminPassword);
        if (createResult.Succeeded)
        {
            var roleResult = await _userManager.AddToRoleAsync(adminUser, "Admin");
            if (roleResult.Succeeded)
            {
                _logger.LogInformation("Successfully created initial admin user: {Email}", adminEmail);
            }
            else
            {
                _logger.LogError("Created admin user but failed to assign Admin role: {Errors}",
                    string.Join(", ", roleResult.Errors.Select(e => e.Description)));
            }
        }
        else
        {
            _logger.LogError("Failed to create initial admin user: {Errors}",
                string.Join(", ", createResult.Errors.Select(e => e.Description)));
        }
    }
}