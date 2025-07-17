using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace AspireDeezNuts.ApiService.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class PostsController(HttpClient httpClient, ILogger<PostsController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Post>>> GetPosts()
    {
        try
        {
            logger.LogInformation("Fetching posts from JSONPlaceholder API");
            
            var response = await httpClient.GetAsync("https://jsonplaceholder.typicode.com/posts");
            response.EnsureSuccessStatusCode();
            
            var jsonContent = await response.Content.ReadAsStringAsync();
            var posts = JsonSerializer.Deserialize<Post[]>(jsonContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            logger.LogInformation("Successfully fetched {PostCount} posts", posts?.Length ?? 0);
            
            return Ok(posts ?? Array.Empty<Post>());
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Error fetching posts from external API");
            return StatusCode(500, "Failed to fetch posts from external service");
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Error parsing JSON response from external API");
            return StatusCode(500, "Failed to parse response from external service");
        }
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Post>> GetPost(int id)
    {
        try
        {
            logger.LogInformation("Fetching post {PostId} from JSONPlaceholder API", id);
            
            var response = await httpClient.GetAsync($"https://jsonplaceholder.typicode.com/posts/{id}");
            
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return NotFound($"Post with ID {id} not found");
            }
            
            response.EnsureSuccessStatusCode();
            
            var jsonContent = await response.Content.ReadAsStringAsync();
            var post = JsonSerializer.Deserialize<Post>(jsonContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            logger.LogInformation("Successfully fetched post {PostId}", id);
            
            return Ok(post);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Error fetching post {PostId} from external API", id);
            return StatusCode(500, "Failed to fetch post from external service");
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Error parsing JSON response for post {PostId}", id);
            return StatusCode(500, "Failed to parse response from external service");
        }
    }
}

public record Post(int Id, int UserId, string Title, string Body);