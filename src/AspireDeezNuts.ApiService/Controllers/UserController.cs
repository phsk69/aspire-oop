using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AspireDeezNuts.Shared.Models;

namespace AspireDeezNuts.ApiService.Controllers;

[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/v1/[controller]")]
public class UserController(
    UserManager<IdentityUser> userManager,
    RoleManager<IdentityRole> roleManager,
    ILogger<UserController> logger) : ControllerBase
{
    private readonly UserManager<IdentityUser> _userManager = userManager;
    private readonly RoleManager<IdentityRole> _roleManager = roleManager;
    private readonly ILogger<UserController> _logger = logger;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<UserDto>>> GetUsers()
    {
        try
        {
            var adminEmail = User.Identity?.Name;
            var adminUser = await _userManager.FindByEmailAsync(adminEmail ?? "");
            _logger.LogInformation("User: {UserId} - Requested all users list", adminUser?.Id ?? "Unknown");
            
            var users = await _userManager.Users.ToListAsync();
            var userDtos = new List<UserDto>();

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                userDtos.Add(new UserDto
                {
                    Id = user.Id,
                    Email = user.Email ?? string.Empty,
                    UserName = user.UserName ?? string.Empty,
                    EmailConfirmed = user.EmailConfirmed,
                    Roles = [.. roles]
                });
            }

            _logger.LogInformation("User: {UserId} - Successfully retrieved {UserCount} users", adminUser?.Id ?? "Unknown", userDtos.Count);
            return Ok(userDtos);
        }
        catch (Exception ex)
        {
            var adminEmail = User.Identity?.Name;
            var adminUser = await _userManager.FindByEmailAsync(adminEmail ?? "");
            _logger.LogError(ex, "User: {UserId} - Error fetching users", adminUser?.Id ?? "Unknown");
            return StatusCode(500, "An error occurred while fetching users");
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<UserDto>> GetUser(string id)
    {
        try
        {
            var adminEmail = User.Identity?.Name;
            var adminUser = await _userManager.FindByEmailAsync(adminEmail ?? "");
            _logger.LogInformation("User: {UserId} - Requested user details for ID {TargetUserId}", adminUser?.Id ?? "Unknown", id);
            
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                _logger.LogWarning("User: {UserId} - User with ID {TargetUserId} not found", adminUser?.Id ?? "Unknown", id);
                return NotFound($"User with ID {id} not found");
            }

            var roles = await _userManager.GetRolesAsync(user);
            var userDto = new UserDto
            {
                Id = user.Id,
                Email = user.Email ?? string.Empty,
                UserName = user.UserName ?? string.Empty,
                EmailConfirmed = user.EmailConfirmed,
                Roles = [.. roles]
            };

            _logger.LogInformation("User: {UserId} - Successfully retrieved user details for {TargetUserId}", adminUser?.Id ?? "Unknown", id);
            return Ok(userDto);
        }
        catch (Exception ex)
        {
            var adminEmail = User.Identity?.Name;
            var adminUser = await _userManager.FindByEmailAsync(adminEmail ?? "");
            _logger.LogError(ex, "User: {UserId} - Error fetching user {TargetUserId}", adminUser?.Id ?? "Unknown", id);
            return StatusCode(500, "An error occurred while fetching the user");
        }
    }

    [HttpPost]
    public async Task<ActionResult<UserDto>> CreateUser([FromBody] CreateUserDto createUserDto)
    {
        try
        {
            var adminEmail = User.Identity?.Name;
            var adminUser = await _userManager.FindByEmailAsync(adminEmail ?? "");
            _logger.LogInformation("User: {UserId} - Requested to create new user with email {Email}", adminUser?.Id ?? "Unknown", createUserDto.Email);
            
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("User: {UserId} - Invalid model state when creating user with email {Email}", adminUser?.Id ?? "Unknown", createUserDto.Email);
                return BadRequest(ModelState);
            }

            var existingUser = await _userManager.FindByEmailAsync(createUserDto.Email);
            if (existingUser != null)
            {
                _logger.LogWarning("User: {UserId} - User with email {Email} already exists", adminUser?.Id ?? "Unknown", createUserDto.Email);
                return Conflict($"User with email {createUserDto.Email} already exists");
            }

            var newUser = new IdentityUser
            {
                UserName = createUserDto.UserName,
                Email = createUserDto.Email,
                EmailConfirmed = true
            };

            var createResult = await _userManager.CreateAsync(newUser, createUserDto.Password);
            if (!createResult.Succeeded)
            {
                _logger.LogError("User: {UserId} - Failed to create user with email {Email}. Errors: {Errors}", adminUser?.Id ?? "Unknown", createUserDto.Email, string.Join(", ", createResult.Errors.Select(e => e.Description)));
                return BadRequest(createResult.Errors);
            }

            // Add role based on IsAdmin flag
            var role = createUserDto.IsAdmin ? "Admin" : "User";
            var roleResult = await _userManager.AddToRoleAsync(newUser, role);
            if (!roleResult.Succeeded)
            {
                _logger.LogWarning("Failed to add user {UserId} to role {Role}", newUser.Id, role);
            }

            var roles = await _userManager.GetRolesAsync(newUser);
            var userDto = new UserDto
            {
                Id = newUser.Id,
                Email = newUser.Email ?? string.Empty,
                UserName = newUser.UserName ?? string.Empty,
                EmailConfirmed = newUser.EmailConfirmed,
                Roles = [.. roles]
            };

            _logger.LogInformation("User: {UserId} - Successfully created user {NewUserId} with email {Email} and role {Role}", adminUser?.Id ?? "Unknown", newUser.Id, createUserDto.Email, role);
            return CreatedAtAction(nameof(GetUser), new { id = newUser.Id }, userDto);
        }
        catch (Exception ex)
        {
            var adminEmail = User.Identity?.Name;
            var adminUser = await _userManager.FindByEmailAsync(adminEmail ?? "");
            _logger.LogError(ex, "User: {UserId} - Error creating user with email {Email}", adminUser?.Id ?? "Unknown", createUserDto.Email);
            return StatusCode(500, "An error occurred while creating the user");
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult> UpdateUser(string id, [FromBody] UpdateUserDto updateUserDto)
    {
        try
        {
            var adminEmail = User.Identity?.Name;
            var adminUser = await _userManager.FindByEmailAsync(adminEmail ?? "");
            _logger.LogInformation("User: {UserId} - Requested to update user {TargetUserId}", adminUser?.Id ?? "Unknown", id);
            
            if (id != updateUserDto.Id)
            {
                _logger.LogWarning("User: {UserId} - User ID mismatch when updating user. URL ID: {UrlId}, DTO ID: {DtoId}", adminUser?.Id ?? "Unknown", id, updateUserDto.Id);
                return BadRequest("User ID mismatch");
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                _logger.LogWarning("User: {UserId} - User with ID {TargetUserId} not found for update", adminUser?.Id ?? "Unknown", id);
                return NotFound($"User with ID {id} not found");
            }

            // Update user properties if provided
            if (!string.IsNullOrEmpty(updateUserDto.Email))
            {
                user.Email = updateUserDto.Email;
            }

            if (!string.IsNullOrEmpty(updateUserDto.UserName))
            {
                user.UserName = updateUserDto.UserName;
            }

            if (updateUserDto.EmailConfirmed.HasValue)
            {
                user.EmailConfirmed = updateUserDto.EmailConfirmed.Value;
            }

            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                _logger.LogError("User: {UserId} - Failed to update user {TargetUserId}. Errors: {Errors}", adminUser?.Id ?? "Unknown", id, string.Join(", ", updateResult.Errors.Select(e => e.Description)));
                return BadRequest(updateResult.Errors);
            }

            _logger.LogInformation("User: {UserId} - Successfully updated user {TargetUserId}", adminUser?.Id ?? "Unknown", id);
            return NoContent();
        }
        catch (Exception ex)
        {
            var adminEmail = User.Identity?.Name;
            var adminUser = await _userManager.FindByEmailAsync(adminEmail ?? "");
            _logger.LogError(ex, "User: {UserId} - Error updating user {TargetUserId}", adminUser?.Id ?? "Unknown", id);
            return StatusCode(500, "An error occurred while updating the user");
        }
    }

    [HttpPost("{id}/roles")]
    public async Task<ActionResult> UpdateUserRole(string id, [FromBody] UserRoleUpdateDto roleUpdate)
    {
        try
        {
            var adminEmail = User.Identity?.Name;
            var adminUser = await _userManager.FindByEmailAsync(adminEmail ?? "");
            var action = roleUpdate.AddRole ? "add" : "remove";
            _logger.LogInformation("User: {UserId} - Requested to {Action} role {Role} for user {TargetUserId}", adminUser?.Id ?? "Unknown", action, roleUpdate.Role, id);
            
            if (id != roleUpdate.UserId)
            {
                _logger.LogWarning("User: {UserId} - User ID mismatch when updating role. URL ID: {UrlId}, DTO ID: {DtoId}", adminUser?.Id ?? "Unknown", id, roleUpdate.UserId);
                return BadRequest("User ID mismatch");
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                _logger.LogWarning("User: {UserId} - User with ID {TargetUserId} not found for role update", adminUser?.Id ?? "Unknown", id);
                return NotFound($"User with ID {id} not found");
            }

            // Check if role exists
            if (!await _roleManager.RoleExistsAsync(roleUpdate.Role))
            {
                _logger.LogWarning("User: {UserId} - Role {Role} does not exist when updating user {TargetUserId}", adminUser?.Id ?? "Unknown", roleUpdate.Role, id);
                return BadRequest($"Role {roleUpdate.Role} does not exist");
            }

            IdentityResult result;
            if (roleUpdate.AddRole)
            {
                result = await _userManager.AddToRoleAsync(user, roleUpdate.Role);
            }
            else
            {
                result = await _userManager.RemoveFromRoleAsync(user, roleUpdate.Role);
            }

            if (!result.Succeeded)
            {
                _logger.LogError("User: {UserId} - Failed to {Action} role {Role} for user {TargetUserId}. Errors: {Errors}", adminUser?.Id ?? "Unknown", action, roleUpdate.Role, id, string.Join(", ", result.Errors.Select(e => e.Description)));
                return BadRequest(result.Errors);
            }

            _logger.LogInformation("User: {UserId} - Successfully {Action}ed role {Role} for user {TargetUserId}", adminUser?.Id ?? "Unknown", action, roleUpdate.Role, id);
            return NoContent();
        }
        catch (Exception ex)
        {
            var adminEmail = User.Identity?.Name;
            var adminUser = await _userManager.FindByEmailAsync(adminEmail ?? "");
            _logger.LogError(ex, "User: {UserId} - Error updating role for user {TargetUserId}", adminUser?.Id ?? "Unknown", id);
            return StatusCode(500, "An error occurred while updating user role");
        }
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteUser(string id)
    {
        try
        {
            var adminEmail = User.Identity?.Name;
            var adminUser = await _userManager.FindByEmailAsync(adminEmail ?? "");
            _logger.LogInformation("User: {UserId} - Requested to delete user {TargetUserId}", adminUser?.Id ?? "Unknown", id);
            
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                _logger.LogWarning("User: {UserId} - User with ID {TargetUserId} not found for deletion", adminUser?.Id ?? "Unknown", id);
                return NotFound($"User with ID {id} not found");
            }

            // Prevent deleting the last admin
            var roles = await _userManager.GetRolesAsync(user);
            if (roles.Contains("Admin"))
            {
                var adminUsers = await _userManager.GetUsersInRoleAsync("Admin");
                if (adminUsers.Count <= 1)
                {
                    _logger.LogWarning("User: {UserId} - Cannot delete the last admin user {TargetUserId}", adminUser?.Id ?? "Unknown", id);
                    return BadRequest("Cannot delete the last admin user");
                }
            }

            var deleteResult = await _userManager.DeleteAsync(user);
            if (!deleteResult.Succeeded)
            {
                _logger.LogError("User: {UserId} - Failed to delete user {TargetUserId}. Errors: {Errors}", adminUser?.Id ?? "Unknown", id, string.Join(", ", deleteResult.Errors.Select(e => e.Description)));
                return BadRequest(deleteResult.Errors);
            }

            _logger.LogInformation("User: {UserId} - Successfully deleted user {TargetUserId}", adminUser?.Id ?? "Unknown", id);
            return NoContent();
        }
        catch (Exception ex)
        {
            var adminEmail = User.Identity?.Name;
            var adminUser = await _userManager.FindByEmailAsync(adminEmail ?? "");
            _logger.LogError(ex, "User: {UserId} - Error deleting user {TargetUserId}", adminUser?.Id ?? "Unknown", id);
            return StatusCode(500, "An error occurred while deleting the user");
        }
    }
}