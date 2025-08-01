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

// Add repository
builder.Services.AddScoped<AspireDeezNuts.Shared.Interfaces.IPostRepository, AspireDeezNuts.ApiService.Repositories.PostRepository>();

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
