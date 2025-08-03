using AspireDeezNuts.Web.Components;
using AspireDeezNuts.Web.Services;
using AspireDeezNuts.Shared.Models;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;
using System.ComponentModel.DataAnnotations;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddBlazorBootstrap();

// Add authentication services - Microsoft recommended approach
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "BlazorServerAuth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.LoginPath = "/login";
        options.LogoutPath = "/logout";
        options.AccessDeniedPath = "/login";
        options.ExpireTimeSpan = TimeSpan.FromHours(1);
        options.SlidingExpiration = true;
    });
builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHttpContextAccessor();

// Simple auth service for API calls only
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<AuthorizedHttpMessageHandler>();

// Add HttpClient for the API - configured for both local debugging and Kubernetes deployment
builder.Services.AddHttpClient<IAuthService, AuthService>("apiservice", client =>
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

// Add authenticated HttpClient for API calls
builder.Services.AddHttpClient("authenticated-api", client =>
{
    var deploymentEnv = builder.Configuration["DEPLOYMENT_ENVIRONMENT"];

    if (deploymentEnv == "Kubernetes")
    {
        var apiBaseUrl = builder.Configuration["API_BASE_URL"] ?? throw new InvalidOperationException("API_BASE_URL configuration is required for Kubernetes deployment.");
        client.BaseAddress = new Uri(apiBaseUrl);
    }
    else
    {
        var serviceUrl = builder.Configuration.GetConnectionString("aspire-deez-nuts-api");
        if (!string.IsNullOrEmpty(serviceUrl))
        {
            client.BaseAddress = new Uri(serviceUrl);
        }
        else
        {
            client.BaseAddress = new Uri("http://localhost:5137");
        }
    }
}).AddHttpMessageHandler<AuthorizedHttpMessageHandler>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

// Add login endpoint 
app.MapPost("/api/login", async (HttpContext context, IAuthService authService) =>
{
    var form = await context.Request.ReadFormAsync();
    var loginRequest = new LoginRequest
    {
        Email = form["Email"].ToString(),
        Password = form["Password"].ToString()
    };

    // Validate the request using data annotations
    var validationResults = new List<ValidationResult>();
    var validationContext = new ValidationContext(loginRequest);
    bool isValid = Validator.TryValidateObject(loginRequest, validationContext, validationResults, true);

    if (!isValid)
    {
        var errors = string.Join(", ", validationResults.Select(vr => vr.ErrorMessage));
        return Results.Redirect($"/login?error={Uri.EscapeDataString(errors)}");
    }

    var result = await authService.LoginAsync(loginRequest);

    if (result.Success)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, loginRequest.Email),
            new(ClaimTypes.Email, loginRequest.Email)
        };

        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity));

        return Results.Redirect("/");
    }

    return Results.Redirect("/login?error=Invalid credentials");
});

app.MapPost("/api/logout", async (HttpContext context) =>
{
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/");
});

// Also support GET for direct navigation
app.MapGet("/logout", async (HttpContext context) =>
{
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/");
});

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapDefaultEndpoints();

app.Run();
