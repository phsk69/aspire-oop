using AspireDeezNuts.FuzzTesting;
using AspireDeezNuts.FuzzTesting.Tests;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations
builder.AddServiceDefaults();

// Configure HttpClient for API testing using Aspire service discovery
// The service discovery will automatically resolve the URL in both local and Kubernetes environments
builder.Services.AddHttpClient("ApiService", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
})
.AddServiceDiscovery(); // This enables automatic service discovery

// Register fuzz testing services
builder.Services.AddSingleton<IFuzzTestRunner, FuzzTestRunner>();

// Register individual fuzz tests
builder.Services.AddSingleton<IFuzzTest, AuthenticationFuzzTest>();
builder.Services.AddSingleton<IFuzzTest, UserManagementFuzzTest>();
builder.Services.AddSingleton<IFuzzTest, PostsFuzzTest>();

// Register the hosted service
builder.Services.AddHostedService<FuzzTestingService>();

// Configure logging
builder.Logging.SetMinimumLevel(LogLevel.Information);

// Add health checks
builder.Services.AddHealthChecks()
    .AddCheck<FuzzTestingHealthCheck>("fuzz_testing_health");

var app = builder.Build();

// Map health check endpoints
app.MapDefaultEndpoints();

// Run the application
await app.RunAsync();