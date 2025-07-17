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
        // Kubernetes deployment: use HTTP service discovery (HTTPS not working in K8s yet)
        client.BaseAddress = new Uri("http://aspire-deez-nuts-api:8080");
    }
    else
    {
        // Local debugging: use deterministic localhost URL with HTTPS
        client.BaseAddress = new Uri("https://localhost:7201");
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
