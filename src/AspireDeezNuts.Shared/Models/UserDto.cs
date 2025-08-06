namespace AspireDeezNuts.Shared.Models;

public class UserDto
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public bool EmailConfirmed { get; set; }
    public string[]? Roles { get; set; }
}

public class CreateUserDto
{
    public string Email { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool IsAdmin { get; set; }
}

public class UpdateUserDto
{
    public string Id { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? UserName { get; set; }
    public bool? EmailConfirmed { get; set; }
}

public class UserRoleUpdateDto
{
    public string UserId { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool AddRole { get; set; }
}