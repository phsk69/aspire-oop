# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Developing guidelines

- Always provide null-safe code
- Always use primary constructors when able
- Always use async approaches for IO bound operations, and if applicable with proper retry mechanics

## Common Commands

This project uses a Makefile for common development tasks:

```bash
# Setup
make setup-tools  # Install required .NET tools (aspirate)

# Core Development
make restore      # Restore NuGet dependencies
make build        # Build the application
make run          # Run the AppHost (main entry point)
make dev          # Run with hot reload using dotnet watch
make clean        # Clean build artifacts
make format       # Format code

# Docker & Kubernetes Deployment
make docker-build-api    # Build API service container image
make docker-build-web    # Build Web application container image
make docker-build        # Build both containers
make k8s-generate        # Generate Kubernetes manifests using Aspirate
make k8s-deploy          # Deploy to Kubernetes cluster
make deploy              # Full pipeline: build → generate → deploy
make k8s-status          # Check deployment status
make k8s-clean           # Clean up Kubernetes resources
```

To run a specific project directly:
```bash
dotnet run --project src/AspireDeezNuts.AppHost
dotnet run --project src/AspireDeezNuts.Web
dotnet run --project src/AspireDeezNuts.ApiService
```

## Architecture Overview

This is a .NET Aspire application with the following structure:

### AppHost (src/AspireDeezNuts.AppHost)

- The orchestrator/entry point for the distributed application
- Configured with proper service registrations and references
- Defines API service and Web application with service discovery

### ServiceDefaults (src/AspireDeezNuts.ServiceDefaults)

- Shared configuration for all services
- Provides OpenTelemetry setup (metrics, tracing, logging)
- Configures health checks at `/health` and `/alive`
- Adds service discovery and HTTP resilience

### Web (src/AspireDeezNuts.Web)

- Blazor Server-Side Rendering application
- Uses Blazor Bootstrap UI framework
- Interactive server components enabled
- Main pages: Home, Weather, Counter

### ApiService (src/AspireDeezNuts.ApiService)

- Minimal API with weather forecast endpoint
- OpenAPI/Swagger enabled in development
- Connected to Web project via service discovery
- Runs on ports 5137 (HTTP), 7201 (HTTPS) in development

## Key Development Notes

1. **Service Discovery**: The Web application communicates with the API service using service discovery (`https+http://apiservice`). Services are automatically resolved through Aspire's built-in service discovery.

2. **AppHost Configuration**: Services are properly registered in the AppHost:
   ```csharp
   var apiService = builder.AddProject<Projects.AspireDeezNuts_ApiService>("aspire-deez-nuts-api");
   
   builder.AddProject<Projects.AspireDeezNuts_Web>("aspire-deez-nuts-web")
       .WithExternalHttpEndpoints()
       .WithReference(apiService);
   ```

3. **Health Checks**: All services expose `/health` (readiness) and `/alive` (liveness) endpoints via ServiceDefaults

4. **Observability**: OpenTelemetry is pre-configured for metrics, tracing, and logging; use ILogger for structured logging

5. **Kubernetes Deployment**: The project uses Aspirate tool to generate Kubernetes manifests from Aspire configuration

6. **Container Images**: Docker support with multi-stage builds for both API and Web services

7. **Development Setup**: Run `make setup-tools` to install required .NET tools like Aspirate. For Kubernetes development, install Rancher Desktop with nerdctl container runtime.

## Technology Stack
- .NET 9.0
- .NET Aspire 9.3.1
- Blazor with Server-Side Rendering
- Blazor Bootstrap 3.4.0
- Minimal APIs
- OpenTelemetry for observability
- Aspirate for Kubernetes manifest generation
- Docker multi-stage builds

## Solution Structure
- Uses modern .slnx solution file format
- Four main projects: AppHost, Web, ApiService, ServiceDefaults
- Comprehensive Makefile for development workflow automation
- Kubernetes deployment pipeline with Aspirate integration