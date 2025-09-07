using System.Text;
using AspireDeezNuts.ApiService.Data;
using AspireDeezNuts.ApiService.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Globalization;
using Microsoft.AspNetCore.Localization;

var builder = WebApplication.CreateBuilder(args);

// Add secret configuration file if it exists
var secretsPath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.Development.secrets.json");
if (File.Exists(secretsPath))
{
    builder.Configuration.AddJsonFile(secretsPath, optional: true, reloadOnChange: true);
}

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Configure database options
builder.Services.Configure<DatabaseOptions>(builder.Configuration.GetSection(DatabaseOptions.Database));
builder.Services.Configure<ConnectionStrings>(builder.Configuration.GetSection(ConnectionStrings.Section));
builder.Services.Configure<SeedDataOptions>(builder.Configuration.GetSection(SeedDataOptions.SeedData));
var databaseOptions = builder.Configuration.GetSection(DatabaseOptions.Database).Get<DatabaseOptions>() ?? new DatabaseOptions();
var connectionStrings = builder.Configuration.GetSection(ConnectionStrings.Section).Get<ConnectionStrings>() ?? new ConnectionStrings();

// Configure all three DbContexts
if (databaseOptions.UseInMemory)
{
    // All contexts use the same in-memory database for development
    var dbName = "AspireDeezNutsDb";

    builder.Services.AddDbContext<AppMigrationDbContext>(options =>
    {
        options.UseInMemoryDatabase(dbName);
        if (databaseOptions.EnableSensitiveDataLogging)
            options.EnableSensitiveDataLogging();
    });

    builder.Services.AddDbContext<AppReadWriteDbContext>(options =>
    {
        options.UseInMemoryDatabase(dbName);
        if (databaseOptions.EnableSensitiveDataLogging)
            options.EnableSensitiveDataLogging();
    });

    builder.Services.AddDbContext<AppReadOnlyDbContext>(options =>
    {
        options.UseInMemoryDatabase(dbName);
        if (databaseOptions.EnableSensitiveDataLogging)
            options.EnableSensitiveDataLogging();
        options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
    });
}
else if (databaseOptions.UsePostgreSql)
{
    // Migration context - for schema changes
    var migrationConnectionString = connectionStrings.GetMigrationConnectionString();
    if (!string.IsNullOrEmpty(migrationConnectionString) && migrationConnectionString != "DataSource=:memory:")
    {
        builder.Services.AddDbContext<AppMigrationDbContext>(options =>
        {
            options.UseNpgsql(migrationConnectionString, npgsqlOptions =>
            {
                npgsqlOptions.CommandTimeout(databaseOptions.CommandTimeout);
                if (databaseOptions.EnableRetryOnFailure)
                {
                    npgsqlOptions.EnableRetryOnFailure(
                        maxRetryCount: databaseOptions.MaxRetryCount,
                        maxRetryDelay: TimeSpan.FromSeconds(databaseOptions.MaxRetryDelay),
                        errorCodesToAdd: null);
                }
            });
            if (databaseOptions.EnableSensitiveDataLogging)
                options.EnableSensitiveDataLogging();
        });
    }
    else
    {
        builder.Services.AddDbContext<AppMigrationDbContext>(options =>
        {
            options.UseInMemoryDatabase("AspireDeezNutsDb");
            if (databaseOptions.EnableSensitiveDataLogging)
                options.EnableSensitiveDataLogging();
        });
    }

    // Read-Write context - for DML operations
    var readWriteConnectionString = connectionStrings.GetReadWriteConnectionString();
    if (!string.IsNullOrEmpty(readWriteConnectionString) && readWriteConnectionString != "DataSource=:memory:")
    {
        builder.Services.AddDbContext<AppReadWriteDbContext>(options =>
        {
            options.UseNpgsql(readWriteConnectionString, npgsqlOptions =>
            {
                npgsqlOptions.CommandTimeout(databaseOptions.CommandTimeout);
                if (databaseOptions.EnableRetryOnFailure)
                {
                    npgsqlOptions.EnableRetryOnFailure(
                        maxRetryCount: databaseOptions.MaxRetryCount,
                        maxRetryDelay: TimeSpan.FromSeconds(databaseOptions.MaxRetryDelay),
                        errorCodesToAdd: null);
                }
            });
            if (databaseOptions.EnableSensitiveDataLogging)
                options.EnableSensitiveDataLogging();
        });
    }
    else
    {
        builder.Services.AddDbContext<AppReadWriteDbContext>(options =>
        {
            options.UseInMemoryDatabase("AspireDeezNutsDb");
            if (databaseOptions.EnableSensitiveDataLogging)
                options.EnableSensitiveDataLogging();
        });
    }

    // Read-Only context - for SELECT operations
    var readOnlyConnectionString = connectionStrings.GetReadOnlyConnectionString();
    if (!string.IsNullOrEmpty(readOnlyConnectionString) && readOnlyConnectionString != "DataSource=:memory:")
    {
        builder.Services.AddDbContext<AppReadOnlyDbContext>(options =>
        {
            options.UseNpgsql(readOnlyConnectionString, npgsqlOptions =>
            {
                npgsqlOptions.CommandTimeout(databaseOptions.CommandTimeout);
                if (databaseOptions.EnableRetryOnFailure)
                {
                    npgsqlOptions.EnableRetryOnFailure(
                        maxRetryCount: databaseOptions.MaxRetryCount,
                        maxRetryDelay: TimeSpan.FromSeconds(databaseOptions.MaxRetryDelay),
                        errorCodesToAdd: null);
                }
            });
            if (databaseOptions.EnableSensitiveDataLogging)
                options.EnableSensitiveDataLogging();
            options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
        });
    }
    else
    {
        builder.Services.AddDbContext<AppReadOnlyDbContext>(options =>
        {
            options.UseInMemoryDatabase("AspireDeezNutsDb");
            if (databaseOptions.EnableSensitiveDataLogging)
                options.EnableSensitiveDataLogging();
            options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
        });
    }
}
else
{
    // Default to InMemory for all contexts
    var dbName = "AspireDeezNutsDb";

    builder.Services.AddDbContext<AppMigrationDbContext>(options =>
    {
        options.UseInMemoryDatabase(dbName);
        if (databaseOptions.EnableSensitiveDataLogging)
            options.EnableSensitiveDataLogging();
    });

    builder.Services.AddDbContext<AppReadWriteDbContext>(options =>
    {
        options.UseInMemoryDatabase(dbName);
        if (databaseOptions.EnableSensitiveDataLogging)
            options.EnableSensitiveDataLogging();
    });

    builder.Services.AddDbContext<AppReadOnlyDbContext>(options =>
    {
        options.UseInMemoryDatabase(dbName);
        if (databaseOptions.EnableSensitiveDataLogging)
            options.EnableSensitiveDataLogging();
        options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
    });
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
.AddEntityFrameworkStores<AppReadWriteDbContext>()
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

    // Add event handlers for debugging
    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogError("Authentication failed: {Error}", context.Exception?.Message);
            return Task.CompletedTask;
        },
        OnTokenValidated = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("Token validated for user: {User}", context.Principal?.Identity?.Name);
            return Task.CompletedTask;
        },
        OnChallenge = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogWarning("JWT Challenge: {Error} - {ErrorDescription}", context.Error, context.ErrorDescription);
            return Task.CompletedTask;
        }
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
builder.Services.AddScoped<IDataSeeder, DataSeeder>();
builder.Services.AddScoped<IIdentityDbService, IdentityDbContextService>();
builder.Services.AddScoped<IPostsDbService, PostsDbContextService>();
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
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token in the text input below.\n\nExample: Bearer eyJhbGciOiJIUzI1NiIs...",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
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
    var timeout = configuration.GetValue("ExternalApis:JsonPlaceholder:Timeout", 30);

    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(timeout);
});

// Configure localization to use British English
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var supportedCultures = new[] { new CultureInfo("en-DK") };
    options.DefaultRequestCulture = new RequestCulture("en-DK");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;
    options.FallBackToParentCultures = false;
    options.FallBackToParentUICultures = false;
});

var app = builder.Build();

// Ensure database is created and seeded
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    var dbOptions = configuration.GetSection(DatabaseOptions.Database).Get<DatabaseOptions>() ?? new DatabaseOptions();

    if (dbOptions.UseInMemory)
    {
        logger.LogInformation("Using InMemory database, ensuring created");
        var migrationContext = scope.ServiceProvider.GetRequiredService<AppMigrationDbContext>();
        await migrationContext.Database.EnsureCreatedAsync();
    }
    else if (dbOptions.UsePostgreSql)
    {
        try
        {
            logger.LogInformation("Checking PostgreSQL database connection");
            var migrationContext = scope.ServiceProvider.GetRequiredService<AppMigrationDbContext>();

            if (await migrationContext.Database.CanConnectAsync())
            {
                logger.LogInformation("PostgreSQL connection successful");

                if (dbOptions.AutoMigrateOnStartup)
                {
                    var pendingMigrations = await migrationContext.Database.GetPendingMigrationsAsync();
                    if (pendingMigrations.Any())
                    {
                        logger.LogInformation("Auto-migration enabled: Applying {Count} pending migrations", pendingMigrations.Count());
                        await migrationContext.Database.MigrateAsync();
                        logger.LogInformation("Database migrations completed successfully");
                    }
                    else
                    {
                        logger.LogInformation("Database is up to date");
                    }
                }
                else
                {
                    var pendingMigrations = await migrationContext.Database.GetPendingMigrationsAsync();
                    if (pendingMigrations.Any())
                    {
                        logger.LogWarning("Auto-migration disabled: {Count} pending migrations found. Run 'make ef-update-database' to apply them", pendingMigrations.Count());
                    }
                    else
                    {
                        logger.LogInformation("Database is up to date");
                    }
                }
            }
            else
            {
                logger.LogWarning("Cannot connect to PostgreSQL, using InMemory fallback");
                await migrationContext.Database.EnsureCreatedAsync();
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error connecting to PostgreSQL, using InMemory fallback");
            var migrationContext = scope.ServiceProvider.GetRequiredService<AppMigrationDbContext>();
            await migrationContext.Database.EnsureCreatedAsync();
        }
    }
    else
    {
        var migrationContext = scope.ServiceProvider.GetRequiredService<AppMigrationDbContext>();
        await migrationContext.Database.EnsureCreatedAsync();
    }

    // Run data seeder using the Identity service
    var identityService = scope.ServiceProvider.GetRequiredService<IIdentityDbService>();
    await identityService.SeedDataAsync();
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

// Configure localization middleware
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture("en-GB"),
    SupportedCultures = new[] { new CultureInfo("en-GB") },
    SupportedUICultures = new[] { new CultureInfo("en-GB") },
    FallBackToParentCultures = false,
    FallBackToParentUICultures = false
});

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapDefaultEndpoints();

app.Run();

// Make Program class accessible for testing
public partial class Program { }
