using Microsoft.AspNetCore.Mvc;
using AspireDeezNuts.Shared.Interfaces;
using AspireDeezNuts.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

namespace AspireDeezNuts.ApiService.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/[controller]")]
public class PostsController(
    IPostRepository postRepository,
    UserManager<IdentityUser> userManager,
    ILogger<PostsController> logger) : ControllerBase
{
    private readonly IPostRepository _postRepository = postRepository;
    private readonly UserManager<IdentityUser> _userManager = userManager;
    private readonly ILogger<PostsController> _logger = logger;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Post>>> GetPosts()
    {
        try
        {
            var userEmail = User.Identity?.Name;
            var user = await _userManager.FindByEmailAsync(userEmail ?? "");
            _logger.LogInformation("User: {UserId} - Fetching all posts", user?.Id ?? "Unknown");
            var posts = await _postRepository.ReadAsync();
            _logger.LogInformation("User: {UserId} - Successfully fetched {PostCount} posts", user?.Id ?? "Unknown", posts.Count);
            return Ok(posts);
        }
        catch (Exception ex)
        {
            var userEmail = User.Identity?.Name;
            var user = await _userManager.FindByEmailAsync(userEmail ?? "");
            _logger.LogError(ex, "User: {UserId} - Error fetching posts", user?.Id ?? "Unknown");
            return StatusCode(500, "Failed to fetch posts");
        }
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Post>> GetPost(int id)
    {
        try
        {
            var userEmail = User.Identity?.Name;
            var user = await _userManager.FindByEmailAsync(userEmail ?? "");
            _logger.LogInformation("User: {UserId} - Fetching post {PostId}", user?.Id ?? "Unknown", id);
            var post = await _postRepository.ReadByIdAsync(id);

            if (post == null)
            {
                _logger.LogWarning("User: {UserId} - Post {PostId} not found", user?.Id ?? "Unknown", id);
                return NotFound($"Post with ID {id} not found");
            }

            _logger.LogInformation("User: {UserId} - Successfully fetched post {PostId}", user?.Id ?? "Unknown", id);
            return Ok(post);
        }
        catch (Exception ex)
        {
            var userEmail = User.Identity?.Name;
            var user = await _userManager.FindByEmailAsync(userEmail ?? "");
            _logger.LogError(ex, "User: {UserId} - Error fetching post {PostId}", user?.Id ?? "Unknown", id);
            return StatusCode(500, "Failed to fetch post");
        }
    }

    [HttpGet("user/{userId:long}")]
    public async Task<ActionResult<IEnumerable<Post>>> GetPostsByUser(long userId)
    {
        try
        {
            var userEmail = User.Identity?.Name;
            var user = await _userManager.FindByEmailAsync(userEmail ?? "");
            _logger.LogInformation("User: {UserId} - Fetching posts for author {AuthorId}", user?.Id ?? "Unknown", userId);
            var posts = await _postRepository.GetByUserIdAsync(userId);
            _logger.LogInformation("User: {UserId} - Successfully fetched {PostCount} posts for author {AuthorId}", user?.Id ?? "Unknown", posts.Count, userId);
            return Ok(posts);
        }
        catch (Exception ex)
        {
            var userEmail = User.Identity?.Name;
            var user = await _userManager.FindByEmailAsync(userEmail ?? "");
            _logger.LogError(ex, "User: {UserId} - Error fetching posts for author {AuthorId}", user?.Id ?? "Unknown", userId);
            return StatusCode(500, "Failed to fetch posts");
        }
    }

    [HttpGet("search")]
    public async Task<ActionResult<IEnumerable<Post>>> SearchPosts([FromQuery] string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            var userEmail = User.Identity?.Name;
            var user = await _userManager.FindByEmailAsync(userEmail ?? "");
            _logger.LogWarning("User: {UserId} - Search posts attempted with empty title parameter", user?.Id ?? "Unknown");
            return BadRequest("Title search parameter is required");
        }

        try
        {
            var userEmail = User.Identity?.Name;
            var user = await _userManager.FindByEmailAsync(userEmail ?? "");
            _logger.LogInformation("User: {UserId} - Searching posts with title containing '{Title}'", user?.Id ?? "Unknown", title);
            var posts = await _postRepository.GetByTitleAsync(title);
            _logger.LogInformation("User: {UserId} - Found {PostCount} posts matching '{Title}'", user?.Id ?? "Unknown", posts.Count, title);
            return Ok(posts);
        }
        catch (Exception ex)
        {
            var userEmail = User.Identity?.Name;
            var user = await _userManager.FindByEmailAsync(userEmail ?? "");
            _logger.LogError(ex, "User: {UserId} - Error searching posts with title '{Title}'", user?.Id ?? "Unknown", title);
            return StatusCode(500, "Failed to search posts");
        }
    }
}