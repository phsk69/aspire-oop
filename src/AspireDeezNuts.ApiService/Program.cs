using System.Text;
using AspireDeezNuts.ApiService.Data;
using AspireDeezNuts.ApiService.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Add secret configuration file if it exists
var secretsPath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.Development.secrets.json");
if (File.Exists(secretsPath))
{
    builder.Configuration.AddJsonFile(secretsPath, optional: true, reloadOnChange: true);
}

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Configure Entity Framework with Identity
var useInMemoryDb = builder.Configuration.GetValue<bool>("UseInMemoryDatabase", true);
if (useInMemoryDb)
{
    builder.Services.AddDbContext<AppIdentityDbContext>(options =>
        options.UseInMemoryDatabase("IdentityDb"));
}
else
{
    // TODO: Add connection string for production database
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    builder.Services.AddDbContext<AppIdentityDbContext>(options =>
        options.UseSqlServer(connectionString));
}

// Add Identity services
builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
{
    // Password settings
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;

    // User settings
    options.User.RequireUniqueEmail = true;

    // Lockout settings
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.Lockout.MaxFailedAccessAttempts = 5;
})
.AddEntityFrameworkStores<AppIdentityDbContext>()
.AddDefaultTokenProviders();

// Configure JWT authentication
var jwtSettings = builder.Configuration.GetSection("Jwt");
var secretKey = jwtSettings["Secret"] ?? Environment.GetEnvironmentVariable("JWT_SECRET");
if (string.IsNullOrEmpty(secretKey))
{
    throw new InvalidOperationException("JWT Secret is not configured. Please set it in appsettings or environment variables.");
}

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
        ClockSkew = TimeSpan.Zero
    };
});

// Add authorization with policies
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
    options.AddPolicy("UserOnly", policy => policy.RequireRole("User"));
});

// Add services to the container.
builder.Services.AddScoped<ITokenService, JwtTokenService>();
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "AspireDeezNuts API",
        Version = "v1",
        Description = "An API demonstrating design patterns in .NET Aspire"
    });

    // Add JWT authentication to Swagger
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});
builder.Services.AddHttpClient();
builder.Services.AddControllers();

// Configure JsonPlaceholder options
builder.Services.Configure<AspireDeezNuts.ApiService.Repositories.JsonPlaceholderOptions>(
    builder.Configuration.GetSection("ExternalApis:JsonPlaceholder"));

// Add repository with configured HttpClient
builder.Services.AddHttpClient<AspireDeezNuts.Shared.Interfaces.IPostRepository, AspireDeezNuts.ApiService.Repositories.JsonPlaceholderPostRepository>((serviceProvider, client) =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var baseUrl = configuration["ExternalApis:JsonPlaceholder:BaseUrl"] ?? "https://jsonplaceholder.typicode.com";
    var timeout = configuration.GetValue<int>("ExternalApis:JsonPlaceholder:Timeout", 30);

    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(timeout);
});

var app = builder.Build();

// Ensure database is created and seeded
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
    dbContext.Database.EnsureCreated();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "AspireDeezNuts API v1");
        c.RoutePrefix = "swagger";
    });
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapDefaultEndpoints();

app.Run();

// Make Program class accessible for testing
public partial class Program { }
