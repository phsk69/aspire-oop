using System.Text.Json;

namespace AspireDeezNuts.FuzzTesting.Tests;

public class AuthenticationFuzzTest(
    IHttpClientFactory httpClientFactory,
    ILogger<AuthenticationFuzzTest> logger) : IFuzzTest
{
    public string Name => "Authentication API Fuzz Test";

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
            // Test login endpoint with various fuzzing inputs
            iterations += await FuzzLoginEndpointAsync(client, errors, cancellationToken);

            // Test register endpoint with various fuzzing inputs
            iterations += await FuzzRegisterEndpointAsync(client, errors, cancellationToken);

            // Test refresh endpoint with malformed tokens
            iterations += await FuzzRefreshEndpointAsync(client, errors, cancellationToken);

            return new FuzzTestResult(
                IsSuccess: errors.Count == 0,
                Iterations: iterations,
                ErrorMessage: errors.Count > 0 ? string.Join("; ", errors) : null,
                Metadata: new Dictionary<string, object> { ["ErrorCount"] = errors.Count }
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Critical error during authentication fuzz testing");
            return new FuzzTestResult(false, iterations, $"Critical error: {ex.Message}");
        }
    }

    private async Task<int> FuzzLoginEndpointAsync(HttpClient client, List<string> errors, CancellationToken cancellationToken)
    {
        var iterations = 0;
        var fuzzInputs = GenerateFuzzInputs();

        foreach (var (username, password, email) in fuzzInputs)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            iterations++;

            try
            {
                var loginRequest = new
                {
                    Username = username,
                    Password = password
                };

                var response = await client.PostAsJsonAsync("/api/v1/auth/login", loginRequest, cancellationToken);

                // Check for unexpected status codes or crashes
                if ((int)response.StatusCode >= 500)
                {
                    errors.Add($"Server error on login with input: {JsonSerializer.Serialize(loginRequest)}");
                }
            }
            catch (HttpRequestException ex)
            {
                errors.Add($"HTTP error on login: {ex.Message}");
            }
            catch (TaskCanceledException)
            {
                // Timeout might indicate DoS vulnerability
                errors.Add($"Timeout on login - potential DoS vulnerability");
            }
        }

        return iterations;
    }

    private async Task<int> FuzzRegisterEndpointAsync(HttpClient client, List<string> errors, CancellationToken cancellationToken)
    {
        var iterations = 0;
        var fuzzInputs = GenerateFuzzInputs();

        foreach (var (username, password, email) in fuzzInputs)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            iterations++;

            try
            {
                var registerRequest = new
                {
                    Username = username,
                    Password = password,
                    Email = email
                };

                var response = await client.PostAsJsonAsync("/api/v1/auth/register", registerRequest, cancellationToken);

                if ((int)response.StatusCode >= 500)
                {
                    errors.Add($"Server error on register with input: {JsonSerializer.Serialize(registerRequest)}");
                }
            }
            catch (HttpRequestException ex)
            {
                errors.Add($"HTTP error on register: {ex.Message}");
            }
            catch (TaskCanceledException)
            {
                errors.Add($"Timeout on register - potential DoS vulnerability");
            }
        }

        return iterations;
    }

    private async Task<int> FuzzRefreshEndpointAsync(HttpClient client, List<string> errors, CancellationToken cancellationToken)
    {
        var iterations = 0;
        var malformedTokens = GenerateMalformedTokens();

        foreach (var token in malformedTokens)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            iterations++;

            try
            {
                var refreshRequest = new { RefreshToken = token };
                var response = await client.PostAsJsonAsync("/api/v1/auth/refresh", refreshRequest, cancellationToken);

                if ((int)response.StatusCode >= 500)
                {
                    errors.Add($"Server error on refresh with malformed token");
                }
            }
            catch (HttpRequestException ex)
            {
                errors.Add($"HTTP error on refresh: {ex.Message}");
            }
        }

        return iterations;
    }

    private List<(string username, string password, string email)> GenerateFuzzInputs()
    {
        return new List<(string, string, string)>
        {
            // SQL Injection attempts
            ("admin' OR '1'='1", "password", "test@test.com"),
            ("'; DROP TABLE users; --", "password", "test@test.com"),
            
            // XSS attempts
            ("<script>alert('XSS')</script>", "password", "test@test.com"),
            ("javascript:alert(1)", "password", "test@test.com"),
            
            // Buffer overflow attempts
            (new string('A', 10000), "password", "test@test.com"),
            ("username", new string('B', 10000), "test@test.com"),
            
            // Special characters
            ("user\0name", "pass\0word", "test@test.com"),
            ("user\r\nname", "password", "test@test.com"),
            
            // Unicode and encoding issues
            ("用户名", "密码", "test@test.com"),
            ("🔥💀🔥", "🔒🔑", "test@test.com"),
            
            // Empty and null-like values
            ("", "", ""),
            ("null", "null", "null"),
            ("undefined", "undefined", "undefined"),
            
            // Command injection
            ("$(whoami)", "password", "test@test.com"),
            ("`id`", "password", "test@test.com"),
            
            // Path traversal
            ("../../../etc/passwd", "password", "test@test.com"),
            ("..\\..\\..\\windows\\system32", "password", "test@test.com"),
            
            // Format string attacks
            ("%s%s%s%s%s", "%n%n%n%n", "test@test.com"),
            ("%x%x%x%x", "password", "test@test.com"),
            
            // LDAP injection
            ("admin)(|(password=*))", "password", "test@test.com"),
            ("*)(uid=*))(|(uid=*", "password", "test@test.com")
        };
    }

    private List<string> GenerateMalformedTokens()
    {
        return new List<string>
        {
            // Completely invalid tokens
            "not-a-token",
            "12345",
            "",
            
            // Malformed JWT-like strings
            "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9",
            "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.",
            "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..",
            ".....",
            
            // Very long strings
            new string('A', 10000),
            
            // Special characters
            "!@#$%^&*()",
            "\0\0\0\0",
            
            // SQL injection in token
            "'; DROP TABLE tokens; --",
            
            // Unicode
            "🔒🔑🔓"
        };
    }
}