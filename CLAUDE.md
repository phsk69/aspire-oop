# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Developing guidelines

- Always provide null-safe code
- Always use primary constructors when able
- Always use async approaches for IO bound operations, and if applicable with proper retry mechanics
- Put shared env vars in configmap
- Cache JsonSerializerOptions instances to avoid CA1869 warnings

## Common Commands

This project uses a Makefile for common development tasks:

```bash
# Core Development
make restore      # Restore NuGet dependencies
make build        # Build the application
make run          # Run the AppHost (main entry point)
make dev          # Run with hot reload using dotnet watch
make clean        # Clean build artifacts
make format       # Format code

# Docker & Kubernetes Deployment
make docker-build-api    # Build API service container image using nerdctl
make docker-build-web    # Build Web application container image using nerdctl
make docker-build        # Build both containers
make k8s-deploy          # Deploy to Kubernetes cluster using Kustomize
make deploy              # Full pipeline: build → deploy
make k8s-status          # Check deployment status
make k8s-clean           # Clean up Kubernetes resources
make token               # Get Aspire Dashboard login token
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
- Configures health checks at `/health` and `/alive` (development only)
- Adds service discovery and HTTP resilience to all HttpClients

### Web (src/AspireDeezNuts.Web)

- Blazor Server-Side Rendering application
- Uses Blazor Bootstrap UI framework
- Interactive server components enabled
- Main pages: Home, Weather, Counter, Posts (with pattern demonstrations)
- Dual HTTP/HTTPS support in Kubernetes (LoadBalancer service)

### ApiService (src/AspireDeezNuts.ApiService)

- Posts controller demonstrating GoF patterns with cached JsonSerializerOptions
- OpenAPI/Swagger enabled in development
- HTTP-only communication in Kubernetes production
- Runs on ports 5137 (HTTP), 7201 (HTTPS) in development

## Current Production Architecture (Kubernetes)

### Service Communication Patterns

- **Web → API**: HTTP only (`http://aspire-deez-nuts-api:8080`)
- **All Services → Dashboard**: HTTP for telemetry (`http://aspire-dashboard:18889`)
- **External Access**: Web service LoadBalancer on ports 8080 (HTTP) and 8443 (HTTPS)

### ConfigMap Architecture

**Three Separate ConfigMaps for Service-Specific Configuration:**

1. **`aspire-these-nutz-config`** (Web Service):
   - Dual protocol: `ASPNETCORE_URLS: "https://+:8443;http://+:8080"`
   - HTTPS enabled with cert-manager certificates
   - Service discovery configuration
   - Full OpenTelemetry configuration

2. **`aspire-api-config`** (API Service):
   - HTTP only: `ASPNETCORE_URLS: "http://+:8080"`
   - No HTTPS configuration in production
   - OpenTelemetry enabled
   - Optimized for internal service communication

3. **`aspire-dashboard-config`** (Dashboard):
   - Dual protocol: `ASPNETCORE_URLS: "https://+:18443;http://+:18888"`
   - HTTPS enabled for secure dashboard access
   - OTLP endpoints for metrics collection

### Certificate Management

- **cert-manager**: Automatically generates certificates for all services
- **Web Service**: Uses HTTPS certificates for external LoadBalancer access
- **API Service**: HTTP-only in production (certificates available but not configured)
- **Dashboard**: Uses HTTPS certificates for secure management access

## Key Development Notes

1. **Service Communication Architecture**: 
   - **Kubernetes Production**: Web → API via HTTP (`http://aspire-deez-nuts-api:8080`)
   - **Local Development**: Web → API via HTTPS (`https://localhost:7201`)
   - **External Access**: Web service LoadBalancer on ports 8080 (HTTP) and 8443 (HTTPS)
   - **Internal Telemetry**: All services → Dashboard via HTTP (`http://aspire-dashboard:18889`)

2. **Service Discovery Configuration**: 
   - Manual service discovery using environment variables in Kubernetes
   - `DEPLOYMENT_ENVIRONMENT=Kubernetes` triggers specific API URL configuration
   - `API_BASE_URL` environment variable controls service-to-service communication
   - ServiceDefaults automatically adds service discovery to all HttpClients

3. **Health Checks**: All services expose `/health` (readiness) and `/alive` (liveness) endpoints via ServiceDefaults (only enabled in development mode)

4. **Observability**: OpenTelemetry fully operational across all services
   - Metrics, traces, and logs collected by Aspire Dashboard
   - All services properly instrumented with ILogger
   - Dashboard accessible for real-time monitoring

5. **Kubernetes Deployment**: Hand-crafted Kubernetes manifests in `/manifests` directory
   - Uses separate ConfigMaps for service-specific configuration
   - PVC for persistent storage (dashboard, web, api data)
   - cert-manager for automatic certificate generation
   - LoadBalancer service for external web access
   - ClusterIP services for internal communication

6. **Container Images**: Docker support with multi-stage builds for both API and Web services, using nerdctl in Rancher Desktop

7. **Development Setup**: For Kubernetes development, install Rancher Desktop with nerdctl container runtime

## Current Working State

### ✅ Fully Operational
- All services running and communicating properly
- External HTTPS access via LoadBalancer
- Internal HTTP service-to-service communication
- OpenTelemetry metrics, traces, and logs collection
- Certificate infrastructure with automatic renewal
- Persistent storage for all services

### ⚠️ Architecture Notes
- **Hybrid HTTP/HTTPS**: External HTTPS, internal HTTP for efficiency
- **Service-Specific Configuration**: Each service has tailored ConfigMap
- **Manual Service Discovery**: Environment-based configuration in Kubernetes
- **Development vs Production**: Different protocols for different environments

## Technology Stack
- .NET 9.0
- .NET Aspire 9.3.1
- Blazor with Server-Side Rendering
- Blazor Bootstrap 3.4.0
- Minimal APIs
- OpenTelemetry for observability
- Docker multi-stage builds
- Kubernetes with hand-crafted manifests
- cert-manager for certificate management
- Rancher Desktop for local K8s development

## Solution Structure
- Uses modern .slnx solution file format
- Four main projects: AppHost, Web, ApiService, ServiceDefaults
- Comprehensive Makefile for development workflow automation
- Hand-crafted Kubernetes manifests in `/manifests` directory
- Three separate ConfigMaps for service-specific configuration