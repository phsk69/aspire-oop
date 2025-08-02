using Microsoft.AspNetCore.Mvc;
using AspireDeezNuts.Shared.Interfaces;
using AspireDeezNuts.Shared.Models;

namespace AspireDeezNuts.ApiService.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class PostsController(IPostRepository postRepository, ILogger<PostsController> logger) : ControllerBase
{
    private readonly IPostRepository _postRepository = postRepository;
    private readonly ILogger<PostsController> _logger = logger;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Post>>> GetPosts()
    {
        try
        {
            _logger.LogInformation("Fetching all posts");
            var posts = await _postRepository.ReadAsync();
            _logger.LogInformation("Successfully fetched {PostCount} posts", posts.Count);
            return Ok(posts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching posts");
            return StatusCode(500, "Failed to fetch posts");
        }
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Post>> GetPost(int id)
    {
        try
        {
            _logger.LogInformation("Fetching post {PostId}", id);
            var post = await _postRepository.ReadByIdAsync(id);
            
            if (post == null)
            {
                return NotFound($"Post with ID {id} not found");
            }
            
            _logger.LogInformation("Successfully fetched post {PostId}", id);
            return Ok(post);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching post {PostId}", id);
            return StatusCode(500, "Failed to fetch post");
        }
    }

    [HttpGet("user/{userId:int}")]
    public async Task<ActionResult<IEnumerable<Post>>> GetPostsByUser(int userId)
    {
        try
        {
            _logger.LogInformation("Fetching posts for user {UserId}", userId);
            var posts = await _postRepository.GetByUserIdAsync(userId);
            _logger.LogInformation("Successfully fetched {PostCount} posts for user {UserId}", posts.Count, userId);
            return Ok(posts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching posts for user {UserId}", userId);
            return StatusCode(500, "Failed to fetch posts");
        }
    }

    [HttpGet("search")]
    public async Task<ActionResult<IEnumerable<Post>>> SearchPosts([FromQuery] string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return BadRequest("Title search parameter is required");
        }

        try
        {
            _logger.LogInformation("Searching posts with title containing '{Title}'", title);
            var posts = await _postRepository.GetByTitleAsync(title);
            _logger.LogInformation("Found {PostCount} posts matching '{Title}'", posts.Count, title);
            return Ok(posts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching posts with title '{Title}'", title);
            return StatusCode(500, "Failed to search posts");
        }
    }
}