using System.Net.Http.Headers;
using System.Text.Json;

namespace AspireDeezNuts.FuzzTesting.Tests;

public class UserManagementFuzzTest(
    IHttpClientFactory httpClientFactory,
    ILogger<UserManagementFuzzTest> logger) : IFuzzTest
{
    public string Name => "User Management API Fuzz Test";

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
            // First, get an admin token for testing protected endpoints
            var token = await GetAdminTokenAsync(client, cancellationToken);
            if (token != null)
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            // Test GET users with various query parameters
            iterations += await FuzzGetUsersEndpointAsync(client, errors, cancellationToken);
            
            // Test GET user by ID with various IDs
            iterations += await FuzzGetUserByIdEndpointAsync(client, errors, cancellationToken);
            
            // Test PUT update user with malformed data
            iterations += await FuzzUpdateUserEndpointAsync(client, errors, cancellationToken);
            
            // Test DELETE user with various IDs
            iterations += await FuzzDeleteUserEndpointAsync(client, errors, cancellationToken);

            return new FuzzTestResult(
                IsSuccess: errors.Count == 0,
                Iterations: iterations,
                ErrorMessage: errors.Count > 0 ? string.Join("; ", errors) : null,
                Metadata: new Dictionary<string, object> { ["ErrorCount"] = errors.Count }
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Critical error during user management fuzz testing");
            return new FuzzTestResult(false, iterations, $"Critical error: {ex.Message}");
        }
    }

    private async Task<string?> GetAdminTokenAsync(HttpClient client, CancellationToken cancellationToken)
    {
        try
        {
            // Try to login with default admin credentials (should be configured)
            var loginRequest = new { Username = "admin", Password = "Admin123!" };
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
            logger.LogWarning(ex, "Could not obtain admin token for testing");
        }
        
        return null;
    }

    private async Task<int> FuzzGetUsersEndpointAsync(HttpClient client, List<string> errors, CancellationToken cancellationToken)
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
                var response = await client.GetAsync($"/api/v1/users?{param}", cancellationToken);
                
                if ((int)response.StatusCode >= 500)
                {
                    errors.Add($"Server error on GET users with params: {param}");
                }
            }
            catch (HttpRequestException ex)
            {
                errors.Add($"HTTP error on GET users: {ex.Message}");
            }
            catch (TaskCanceledException)
            {
                errors.Add($"Timeout on GET users - potential DoS vulnerability");
            }
        }

        return iterations;
    }

    private async Task<int> FuzzGetUserByIdEndpointAsync(HttpClient client, List<string> errors, CancellationToken cancellationToken)
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
                var response = await client.GetAsync($"/api/v1/users/{id}", cancellationToken);
                
                if ((int)response.StatusCode >= 500)
                {
                    errors.Add($"Server error on GET user with ID: {id}");
                }
            }
            catch (HttpRequestException ex)
            {
                errors.Add($"HTTP error on GET user by ID: {ex.Message}");
            }
        }

        return iterations;
    }

    private async Task<int> FuzzUpdateUserEndpointAsync(HttpClient client, List<string> errors, CancellationToken cancellationToken)
    {
        var iterations = 0;
        var fuzzData = GenerateFuzzUserData();

        foreach (var data in fuzzData)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            iterations++;
            
            try
            {
                var response = await client.PutAsJsonAsync($"/api/v1/users/{data.id}", data.userData, cancellationToken);
                
                if ((int)response.StatusCode >= 500)
                {
                    errors.Add($"Server error on PUT user with data: {JsonSerializer.Serialize(data)}");
                }
            }
            catch (HttpRequestException ex)
            {
                errors.Add($"HTTP error on PUT user: {ex.Message}");
            }
        }

        return iterations;
    }

    private async Task<int> FuzzDeleteUserEndpointAsync(HttpClient client, List<string> errors, CancellationToken cancellationToken)
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
                var response = await client.DeleteAsync($"/api/v1/users/{id}", cancellationToken);
                
                if ((int)response.StatusCode >= 500)
                {
                    errors.Add($"Server error on DELETE user with ID: {id}");
                }
            }
            catch (HttpRequestException ex)
            {
                errors.Add($"HTTP error on DELETE user: {ex.Message}");
            }
        }

        return iterations;
    }

    private List<string> GenerateFuzzQueryParameters()
    {
        return new List<string>
        {
            // SQL injection in query params
            "search=admin' OR '1'='1",
            "sort=id; DROP TABLE users; --",
            
            // Command injection
            "filter=$(whoami)",
            "page=`rm -rf /`",
            
            // XSS in query params
            "name=<script>alert('XSS')</script>",
            "role=javascript:alert(1)",
            
            // Buffer overflow attempts
            $"search={new string('A', 10000)}",
            
            // Special characters
            "id=\0\0\0",
            "name=\r\n\r\n",
            
            // Negative and extreme values
            "page=-1&size=-1",
            "page=999999999&size=999999999",
            
            // Format string
            "format=%s%s%s%n%n",
            
            // Path traversal
            "file=../../../etc/passwd",
            
            // Unicode
            "search=🔥💀🔥"
        };
    }

    private List<string> GenerateFuzzIds()
    {
        return new List<string>
        {
            // SQL injection
            "1 OR 1=1",
            "'; DROP TABLE users; --",
            
            // Path traversal
            "../../../etc/passwd",
            "..\\..\\..\\windows\\system32",
            
            // Command injection
            "$(whoami)",
            "`id`",
            
            // XSS
            "<script>alert('XSS')</script>",
            
            // Special values
            "null",
            "undefined",
            "NaN",
            "-1",
            "0",
            "999999999",
            
            // Buffer overflow
            new string('A', 10000),
            
            // Special characters
            "\0",
            "\r\n",
            
            // Unicode
            "用户ID",
            "🔥"
        };
    }

    private List<(string id, object userData)> GenerateFuzzUserData()
    {
        var fuzzData = new List<(string, object)>();
        
        // Various malformed user objects
        fuzzData.Add(("1", new { 
            Username = "<script>alert('XSS')</script>",
            Email = "test@test.com",
            Roles = new[] { "Admin", "'; DROP TABLE users; --" }
        }));
        
        fuzzData.Add(("2", new { 
            Username = new string('A', 10000),
            Email = new string('B', 10000),
            Roles = new string[1000]
        }));
        
        fuzzData.Add(("3", new {
            Username = "user\0name",
            Email = "test\r\n@test.com",
            Roles = new[] { "\0", "\r\n" }
        }));
        
        fuzzData.Add(("4", new {
            Username = "$(whoami)",
            Email = "`id`@test.com",
            Password = "../../../etc/passwd"
        }));
        
        fuzzData.Add(("5", new {
            Username = "🔥💀🔥",
            Email = "用户@测试.com",
            Roles = new[] { "🔒", "🔑" }
        }));
        
        // Nested object injection
        fuzzData.Add(("6", new {
            Username = "test",
            Email = "test@test.com",
            __proto__ = new { isAdmin = true },
            constructor = new { prototype = new { isAdmin = true } }
        }));
        
        // Type confusion
        fuzzData.Add(("7", new {
            Username = 12345,
            Email = true,
            Roles = "NotAnArray"
        }));
        
        return fuzzData;
    }
}