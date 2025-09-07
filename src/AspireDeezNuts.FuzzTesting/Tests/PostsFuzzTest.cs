using System.Net.Http.Headers;
using System.Text.Json;

namespace AspireDeezNuts.FuzzTesting.Tests;

public class PostsFuzzTest(
    IHttpClientFactory httpClientFactory,
    ILogger<PostsFuzzTest> logger) : IFuzzTest
{
    public string Name => "Posts API Fuzz Test";

    public async Task<FuzzTestResult> ExecuteAsync(CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("ApiService");
        
        // Set the base address using service discovery format
        // This will be resolved by Aspire's service discovery to the actual API URL
        client.BaseAddress = new Uri("https+http://aspire-deez-nuts-api");
        
        var iterations = 0;
        var errors = new List<string>();

        try
        {
            // Get auth token for testing protected endpoints
            var token = await GetAuthTokenAsync(client, cancellationToken);
            if (token != null)
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            // Test GET posts with various query parameters
            iterations += await FuzzGetPostsEndpointAsync(client, errors, cancellationToken);
            
            // Test GET post by ID with various IDs
            iterations += await FuzzGetPostByIdEndpointAsync(client, errors, cancellationToken);
            
            // Test pagination with edge cases
            iterations += await FuzzPaginationEndpointAsync(client, errors, cancellationToken);

            return new FuzzTestResult(
                IsSuccess: errors.Count == 0,
                Iterations: iterations,
                ErrorMessage: errors.Count > 0 ? string.Join("; ", errors) : null,
                Metadata: new Dictionary<string, object> { ["ErrorCount"] = errors.Count }
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Critical error during posts fuzz testing");
            return new FuzzTestResult(false, iterations, $"Critical error: {ex.Message}");
        }
    }

    private async Task<string?> GetAuthTokenAsync(HttpClient client, CancellationToken cancellationToken)
    {
        try
        {
            // Create a test user and login
            var registerRequest = new { 
                Username = $"fuzztest_{Guid.NewGuid():N}", 
                Password = "TestPass123!",
                Email = "fuzz@test.com"
            };
            
            await client.PostAsJsonAsync("/api/v1/auth/register", registerRequest, cancellationToken);
            
            var loginRequest = new {
                registerRequest.Username,
                registerRequest.Password 
            };
            
            var response = await client.PostAsJsonAsync("/api/v1/auth/login", loginRequest, cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                var loginResponse = JsonSerializer.Deserialize<JsonElement>(content);
                if (loginResponse.TryGetProperty("token", out var token))
                {
                    return token.GetString();
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not obtain auth token for testing");
        }
        
        return null;
    }

    private async Task<int> FuzzGetPostsEndpointAsync(HttpClient client, List<string> errors, CancellationToken cancellationToken)
    {
        var iterations = 0;
        var fuzzParams = GenerateFuzzQueryParameters();

        foreach (var param in fuzzParams)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            iterations++;
            
            try
            {
                var response = await client.GetAsync($"/api/v1/posts?{param}", cancellationToken);
                
                if ((int)response.StatusCode >= 500)
                {
                    errors.Add($"Server error on GET posts with params: {param}");
                }
                
                // Check for information disclosure
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync(cancellationToken);
                    if (content.Contains("Exception") || content.Contains("StackTrace"))
                    {
                        errors.Add($"Potential information disclosure in response for params: {param}");
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                errors.Add($"HTTP error on GET posts: {ex.Message}");
            }
            catch (TaskCanceledException)
            {
                errors.Add($"Timeout on GET posts - potential DoS vulnerability");
            }
        }

        return iterations;
    }

    private async Task<int> FuzzGetPostByIdEndpointAsync(HttpClient client, List<string> errors, CancellationToken cancellationToken)
    {
        var iterations = 0;
        var fuzzIds = GenerateFuzzIds();

        foreach (var id in fuzzIds)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            iterations++;
            
            try
            {
                var response = await client.GetAsync($"/api/v1/posts/{id}", cancellationToken);
                
                if ((int)response.StatusCode >= 500)
                {
                    errors.Add($"Server error on GET post with ID: {id}");
                }
                
                // Check for SQL errors in response
                if (!response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync(cancellationToken);
                    if (content.Contains("SQL") || content.Contains("syntax") || content.Contains("database"))
                    {
                        errors.Add($"Potential SQL injection vulnerability with ID: {id}");
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                errors.Add($"HTTP error on GET post by ID: {ex.Message}");
            }
        }

        return iterations;
    }

    private async Task<int> FuzzPaginationEndpointAsync(HttpClient client, List<string> errors, CancellationToken cancellationToken)
    {
        var iterations = 0;
        var paginationParams = GenerateFuzzPaginationParameters();

        foreach (var param in paginationParams)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            iterations++;
            
            try
            {
                var response = await client.GetAsync($"/api/v1/posts?{param}", cancellationToken);
                
                if ((int)response.StatusCode >= 500)
                {
                    errors.Add($"Server error on pagination with params: {param}");
                }
                
                // Check for memory exhaustion with large page sizes
                if (param.Contains("pageSize=999999"))
                {
                    var memoryBefore = GC.GetTotalMemory(false);
                    await response.Content.ReadAsStringAsync(cancellationToken);
                    var memoryAfter = GC.GetTotalMemory(false);
                    
                    if (memoryAfter - memoryBefore > 100_000_000) // 100MB increase
                    {
                        errors.Add($"Potential memory exhaustion vulnerability with params: {param}");
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                errors.Add($"HTTP error on pagination: {ex.Message}");
            }
            catch (OutOfMemoryException)
            {
                errors.Add($"Out of memory with pagination params: {param}");
            }
        }

        return iterations;
    }

    private List<string> GenerateFuzzQueryParameters()
    {
        return new List<string>
        {
            // SQL injection attempts
            "userId=1' OR '1'='1",
            "title='; DROP TABLE posts; --",
            "userId=1 UNION SELECT * FROM users",
            
            // NoSQL injection
            "filter={'$ne': null}",
            "userId[$ne]=",
            
            // Command injection
            "sort=$(cat /etc/passwd)",
            "filter=`whoami`",
            
            // XSS attempts
            "search=<script>alert('XSS')</script>",
            "title=javascript:alert(document.cookie)",
            "<img src=x onerror=alert('XSS')>",
            
            // LDAP injection
            "userId=*)(|(objectClass=*))",
            
            // XML injection
            "filter=<?xml version=\"1.0\"?><!DOCTYPE foo [<!ENTITY xxe SYSTEM \"file:///etc/passwd\">]><foo>&xxe;</foo>",
            
            // Header injection
            "userId=1\r\nX-Injected-Header: malicious",
            
            // Format string
            "%s%s%s%s%n%n%n",
            
            // Path traversal
            "file=../../../../etc/passwd",
            
            // Buffer overflow
            $"search={new string('A', 50000)}",
            
            // Unicode and encoding
            "userId=\u202e\u0041\u0064\u006d\u0069\u006e",
            "title=用户测试🔥💀",
            
            // Logic errors
            "page=-1&pageSize=-1",
            "page=0&pageSize=0",
            "page=NaN&pageSize=Infinity"
        };
    }

    private List<string> GenerateFuzzIds()
    {
        return new List<string>
        {
            // SQL injection
            "1' OR '1'='1",
            "1; DROP TABLE posts; --",
            "1 UNION SELECT * FROM users",
            "1' AND 1=CAST((SELECT password FROM users LIMIT 1) AS INT)--",
            
            // NoSQL injection
            "{'$ne': null}",
            "{'$gt': ''}",
            
            // Integer overflow/underflow
            "-1",
            "0",
            "2147483647", // Max int32
            "2147483648", // Max int32 + 1
            "-2147483648", // Min int32
            "9223372036854775807", // Max int64
            
            // Type confusion
            "null",
            "undefined",
            "NaN",
            "Infinity",
            "true",
            "false",
            "[]",
            "{}",
            
            // Command injection
            "$(whoami)",
            "`id`",
            "|ls",
            
            // Path traversal
            "../posts/1",
            "..\\..\\..\\windows\\system32",
            
            // Format string
            "%x%x%x%x",
            "%s%s%s%s",
            "%n%n%n%n",
            
            // Special characters
            "\0",
            "\r\n",
            "\t",
            "\\",
            "'",
            "\"",
            
            // Unicode
            "๏̯͡๏",
            "﷽",
            "𝕳𝖊𝖑𝖑𝖔",
            
            // Very long string
            new string('9', 10000)
        };
    }

    private List<string> GenerateFuzzPaginationParameters()
    {
        return new List<string>
        {
            // Extreme values
            "page=999999999&pageSize=999999999",
            "page=-999999999&pageSize=-999999999",
            
            // Zero and negative
            "page=0&pageSize=0",
            "page=-1&pageSize=-1",
            
            // Type confusion
            "page=NaN&pageSize=NaN",
            "page=Infinity&pageSize=Infinity",
            "page=null&pageSize=undefined",
            
            // Decimal values
            "page=1.5&pageSize=10.7",
            "page=0.1&pageSize=0.1",
            
            // Scientific notation
            "page=1e10&pageSize=1e10",
            "page=1e-10&pageSize=1e-10",
            
            // Mixed valid/invalid
            "page=1&pageSize=NaN",
            "page=abc&pageSize=10",
            
            // Array injection
            "page[]=1&page[]=2&pageSize[]=10",
            
            // Object injection
            "page[offset]=0&page[limit]=10",
            
            // Memory exhaustion attempts
            "page=1&pageSize=2147483647",
            
            // Logic bombs
            "page=1&pageSize=1000000&fields=" + new string('a', 10000)
        };
    }
}