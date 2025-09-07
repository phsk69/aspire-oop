var builder = DistributedApplication.CreateBuilder(args);

// Set static ports only for local development (not Kubernetes)
var deploymentEnv = builder.Configuration["DEPLOYMENT_ENVIRONMENT"];
var isLocalDev = string.IsNullOrEmpty(deploymentEnv) || deploymentEnv != "Kubernetes";

// Configure API service
var apiService = builder.AddProject<Projects.AspireDeezNuts_ApiService>("aspire-deez-nuts-api");
if (isLocalDev)
{
    apiService.WithHttpsEndpoint(port: 7201, name: "api-https");
}

// Configure Web service
var webService = builder.AddProject<Projects.AspireDeezNuts_Web>("aspire-deez-nuts-web")
    .WithExternalHttpEndpoints()
    .WithReference(apiService);
if (isLocalDev)
{
    webService.WithHttpsEndpoint(port: 7071, name: "web-https");
}

// Configure Fuzz Testing service (only in local development)
if (isLocalDev)
{
    builder.AddProject<Projects.AspireDeezNuts_FuzzTesting>("aspire-deez-nuts-fuzz")
        .WithReference(apiService);
}

builder.Build().Run();
