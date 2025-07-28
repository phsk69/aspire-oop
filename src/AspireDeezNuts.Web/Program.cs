using AspireDeezNuts.Web.Components;
var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddBlazorBootstrap();

// Add HttpClient for the API - configured for both local debugging and Kubernetes deployment
builder.Services.AddHttpClient("apiservice", client =>
{
    var deploymentEnv = builder.Configuration["DEPLOYMENT_ENVIRONMENT"];
    
    if (deploymentEnv == "Kubernetes")
    {
        var apiBaseUrl = builder.Configuration["API_BASE_URL"] ?? throw new InvalidOperationException("API_BASE_URL configuration is required for Kubernetes deployment.");
        client.BaseAddress = new Uri(apiBaseUrl);
    }
    else
    {
        // When running with Aspire, service discovery will provide the URL
        // Otherwise fall back to direct URL
        var serviceUrl = builder.Configuration.GetConnectionString("aspire-deez-nuts-api");
        if (!string.IsNullOrEmpty(serviceUrl))
        {
            // Running with Aspire orchestration
            client.BaseAddress = new Uri(serviceUrl);
        }
        else
        {
            // Running standalone - use HTTP since Aspire only exposes HTTP
            client.BaseAddress = new Uri("http://localhost:5137");
        }
    }
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapDefaultEndpoints();

app.Run();
