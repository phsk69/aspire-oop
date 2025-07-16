var builder = DistributedApplication.CreateBuilder(args);

var apiService = builder.AddProject<Projects.AspireDeezNuts_ApiService>("aspire-deez-nuts-api");

builder.AddProject<Projects.AspireDeezNuts_Web>("aspire-deez-nuts-web")
    .WithExternalHttpEndpoints()
    .WithReference(apiService);

builder.Build().Run();
