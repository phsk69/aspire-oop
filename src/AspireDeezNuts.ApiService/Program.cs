var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { 
        Title = "AspireDeezNuts API", 
        Version = "v1",
        Description = "An API demonstrating design patterns in .NET Aspire"
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
app.MapControllers();
app.MapDefaultEndpoints();

app.Run();
