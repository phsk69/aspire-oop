using AspireDeezNuts.Web.Components;
using AspireDeezNuts.Web.Services;
using AspireDeezNuts.Shared.Models;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Components.Server;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Components.Authorization;
using System.Globalization;
using Microsoft.AspNetCore.Localization;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Add SignalR for real-time authentication state updates
builder.Services.AddSignalR();

// Configure Blazor Server with detailed errors in development
if (builder.Environment.IsDevelopment())
{
    builder.Services.Configure<CircuitOptions>(options =>
    {
        options.DetailedErrors = true;
    });
}
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

// Configure token refresh interval from settings
builder.Services.Configure<AspireDeezNuts.Web.Services.AuthenticationOptions>(options =>
{
    var intervalMinutes = builder.Configuration.GetValue("Authentication:TokenRefreshIntervalMinutes", 1);
    options.TokenRefreshIntervalMinutes = intervalMinutes;
});

// Replace default authentication state provider with custom revalidating one
builder.Services.AddScoped<AuthenticationStateProvider, CustomRevalidatingAuthenticationStateProvider>();

// Simple auth service for API calls only
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<AuthorizedHttpMessageHandler>();
builder.Services.AddSingleton<IJsonSerializationService, JsonSerializationService>();

// Toast notification service - Singleton so all components can share the same instance
builder.Services.AddSingleton<IToastService, ToastService>();

// Token information service for JWT token parsing
builder.Services.AddScoped<ITokenInfoService, TokenInfoService>();

// Rate limiting service for preventing rapid successive operations
builder.Services.AddSingleton<IRateLimitingService, RateLimitingService>();

// Authentication state notification service for SignalR broadcasting
builder.Services.AddScoped<IAuthStateNotificationService, AuthStateNotificationService>();

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
            // Running standalone - use HTTPS for API calls
            client.BaseAddress = new Uri("https://localhost:7201");
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
            client.BaseAddress = new Uri("https://localhost:7201");
        }
    }
}).AddHttpMessageHandler<AuthorizedHttpMessageHandler>();

// Configure localization to use British English
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var supportedCultures = new[] { new CultureInfo("en-GB") };
    options.DefaultRequestCulture = new RequestCulture("en-GB");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;
    options.FallBackToParentCultures = false;
    options.FallBackToParentUICultures = false;
});

var app = builder.Build();

// Configure localization middleware
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture("en-GB"),
    SupportedCultures = new[] { new CultureInfo("en-GB") },
    SupportedUICultures = new[] { new CultureInfo("en-GB") },
    FallBackToParentCultures = false,
    FallBackToParentUICultures = false
});

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
        // Get both tokens from the auth service
        var loginResponse = await authService.GetLoginResponseAsync();
        
        if (loginResponse != null && !string.IsNullOrEmpty(loginResponse.AccessToken))
        {
            // Parse claims from JWT token
            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(loginResponse.AccessToken);
            
            // Extract all claims from the JWT token
            var claims = jwtToken.Claims.ToList();
            
            // Add both tokens as claims for API calls and refresh
            claims.Add(new Claim("access_token", loginResponse.AccessToken));
            if (!string.IsNullOrEmpty(loginResponse.RefreshToken))
            {
                claims.Add(new Claim("refresh_token", loginResponse.RefreshToken));
            }
            
            // Ensure we have essential claims
            if (!claims.Any(c => c.Type == ClaimTypes.Name))
            {
                claims.Add(new Claim(ClaimTypes.Name, loginRequest.Email));
            }
            if (!claims.Any(c => c.Type == ClaimTypes.Email))
            {
                claims.Add(new Claim(ClaimTypes.Email, loginRequest.Email));
            }
            
            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity));
        }
        else
        {
            // Fallback if we can't get the token for some reason
            var claims = new List<Claim>
            {
                new(ClaimTypes.Name, loginRequest.Email),
                new(ClaimTypes.Email, loginRequest.Email)
            };
            
            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity));
        }

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

// Map SignalR hubs with authentication
app.MapHub<AspireDeezNuts.Web.Hubs.AuthHub>("/authHub").RequireAuthorization();

app.MapDefaultEndpoints();

app.Run();
