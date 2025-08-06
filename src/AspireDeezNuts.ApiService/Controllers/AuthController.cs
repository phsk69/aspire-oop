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
            _logger.LogWarning("Login attempt for non-existent user: {Email}", request.Email);
            return Unauthorized(new { message = "Invalid email or password" });
        }

        var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);

        if (result.IsLockedOut)
        {
            _logger.LogWarning("User account locked out: {Email}", request.Email);
            return Unauthorized(new { message = "Account locked out. Please try again later." });
        }

        if (!result.Succeeded)
        {
            _logger.LogWarning("Invalid password for user: {Email}", request.Email);
            return Unauthorized(new { message = "Invalid email or password" });
        }

        _logger.LogInformation("User logged in successfully: {Email}", request.Email);
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
            _logger.LogWarning("Invalid refresh token attempted");
            return Unauthorized(new { message = "Invalid refresh token" });
        }

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
        _logger.LogInformation("User logged out: {Email}", userEmail);

        return Ok(new { message = "Logged out successfully" });
    }

    [HttpPost("register")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        _logger.LogInformation("Register endpoint called by user: {User}, IsAuthenticated: {IsAuth}, Claims: {Claims}", 
            User.Identity?.Name, 
            User.Identity?.IsAuthenticated,
            string.Join(", ", User.Claims.Select(c => $"{c.Type}={c.Value}")));

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

        _logger.LogInformation("New user registered: {Email} with role: {Role}", request.Email, request.Role ?? "User");

        return Ok(new { message = "User registered successfully", userId = user.Id });
    }

    [HttpGet("test")]
    [Authorize]
    public IActionResult TestAuth()
    {
        return Ok(new
        {
            message = "You are authenticated!",
            user = User.Identity?.Name,
            claims = User.Claims.Select(c => new { c.Type, c.Value })
        });
    }

    [HttpGet("test-admin")]
    [Authorize(Policy = "AdminOnly")]
    public IActionResult TestAdminAuth()
    {
        return Ok(new { message = "You are an admin!" });
    }
}