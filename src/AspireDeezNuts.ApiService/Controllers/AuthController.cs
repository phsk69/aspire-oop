using AspireDeezNuts.ApiService.Services;
using AspireDeezNuts.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AspireDeezNuts.ApiService.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class AuthController(
    UserManager<IdentityUser> userManager,
    SignInManager<IdentityUser> signInManager,
    ITokenService tokenService,
    ILogger<AuthController> logger) : ControllerBase
{
    private readonly UserManager<IdentityUser> _userManager = userManager;
    private readonly SignInManager<IdentityUser> _signInManager = signInManager;
    private readonly ITokenService _tokenService = tokenService;
    private readonly ILogger<AuthController> _logger = logger;

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null)
        {
            _logger.LogWarning("User: {Email} - Login attempt failed (user not found)", request.Email);
            return Unauthorized(new { message = "Invalid email or password" });
        }

        var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);

        if (result.IsLockedOut)
        {
            _logger.LogWarning("User: {UserId} - Account locked out during login attempt", user.Id);
            return Unauthorized(new { message = "Account locked out. Please try again later." });
        }

        if (!result.Succeeded)
        {
            _logger.LogWarning("User: {UserId} - Invalid password during login attempt", user.Id);
            return Unauthorized(new { message = "Invalid email or password" });
        }

        _logger.LogInformation("User: {UserId} - Successfully logged in", user.Id);
        var tokens = await _tokenService.GenerateTokensAsync(user);

        return Ok(new LoginResponse
        {
            AccessToken = tokens.AccessToken,
            RefreshToken = tokens.RefreshToken,
            ExpiresIn = (int)(tokens.AccessTokenExpiry - DateTime.UtcNow).TotalSeconds
        });
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var tokens = await _tokenService.RefreshTokensAsync(request.RefreshToken);
        if (tokens == null)
        {
            _logger.LogWarning("User: Unknown - Invalid refresh token attempted");
            return Unauthorized(new { message = "Invalid refresh token" });
        }

        _logger.LogInformation("User: {UserId} - Successfully refreshed tokens", tokens.UserId ?? "Unknown");
        return Ok(new LoginResponse
        {
            AccessToken = tokens.AccessToken,
            RefreshToken = tokens.RefreshToken,
            ExpiresIn = (int)(tokens.AccessTokenExpiry - DateTime.UtcNow).TotalSeconds
        });
    }

    [HttpPost("refresh-simple")]
    [Authorize]
    public async Task<IActionResult> RefreshSimpleToken()
    {
        var userEmail = User.Identity?.Name;
        if (string.IsNullOrEmpty(userEmail))
        {
            return Unauthorized(new { message = "Invalid user context" });
        }

        var user = await _userManager.FindByEmailAsync(userEmail);
        if (user == null)
        {
            return Unauthorized(new { message = "User not found" });
        }

        _logger.LogInformation("User: {UserId} - Token refresh requested", user.Id);
        var tokens = await _tokenService.GenerateTokensAsync(user);

        return Ok(new LoginResponse
        {
            AccessToken = tokens.AccessToken,
            RefreshToken = tokens.RefreshToken,
            ExpiresIn = (int)(tokens.AccessTokenExpiry - DateTime.UtcNow).TotalSeconds
        });
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest request)
    {
        if (!string.IsNullOrEmpty(request.RefreshToken))
        {
            await _tokenService.RevokeRefreshTokenAsync(request.RefreshToken);
        }

        var userEmail = User.Identity?.Name;
        var currentUser = await _userManager.FindByEmailAsync(userEmail ?? "");
        _logger.LogInformation("User: {UserId} - Successfully logged out", currentUser?.Id ?? "Unknown");

        return Ok(new { message = "Logged out successfully" });
    }

    [HttpPost("register")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var adminEmail = User.Identity?.Name;
        var adminUser = await _userManager.FindByEmailAsync(adminEmail ?? "");
        _logger.LogInformation("User: {AdminUserId} - Register endpoint called for new user: {Email}",
            adminUser?.Id ?? "Unknown", request.Email);

        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var existingUser = await _userManager.FindByEmailAsync(request.Email);
        if (existingUser != null)
        {
            return BadRequest(new { message = "Email already registered" });
        }

        var user = new IdentityUser
        {
            UserName = request.Email,
            Email = request.Email,
            EmailConfirmed = true // For development simplicity
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Errors.Select(e => e.Description) });
        }

        // Assign role
        if (!string.IsNullOrEmpty(request.Role) && (request.Role == "Admin" || request.Role == "User"))
        {
            await _userManager.AddToRoleAsync(user, request.Role);
        }
        else
        {
            await _userManager.AddToRoleAsync(user, "User"); // Default role
        }

        _logger.LogInformation("User: {AdminUserId} - Successfully registered new user: {NewUserId} with role: {Role}",
            adminUser?.Id ?? "Unknown", user.Id, request.Role ?? "User");

        return Ok(new { message = "User registered successfully", userId = user.Id });
    }

    [HttpGet("test")]
    [Authorize]
    public async Task<IActionResult> TestAuth()
    {
        var userEmail = User.Identity?.Name;
        var user = await _userManager.FindByEmailAsync(userEmail ?? "");
        _logger.LogInformation("User: {UserId} - Test authentication endpoint accessed", user?.Id ?? "Unknown");

        return Ok(new
        {
            message = "You are authenticated!",
            user = User.Identity?.Name,
            claims = User.Claims.Select(c => new { c.Type, c.Value })
        });
    }

    [HttpGet("test-admin")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> TestAdminAuth()
    {
        var adminEmail = User.Identity?.Name;
        var adminUser = await _userManager.FindByEmailAsync(adminEmail ?? "");
        _logger.LogInformation("User: {UserId} - Test admin authentication endpoint accessed", adminUser?.Id ?? "Unknown");

        return Ok(new { message = "You are an admin!" });
    }
}