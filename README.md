# Aspire Object-Oriented Design Training

A .NET Aspire distributed application demonstrating object-oriented design patterns with Blazor Server frontend and minimal API backend. This project serves as a training ground for implementing design patterns from "Design Patterns: Elements of Reusable Object-Oriented Software" (Gang of Four book) while leveraging modern .NET Aspire orchestration and Kubernetes deployment capabilities.

## Prerequisites

- .NET 9 SDK
- For Kubernetes development:
  - Rancher Desktop set up for the nerdctl approach

## Initial Setup

### 1. Rancher Desktop Setup (for Kubernetes Development)

1. Install [Rancher Desktop](https://rancherdesktop.io/)
2. Configure Rancher Desktop:
   - Enable Kubernetes
   - Set container runtime to **nerdctl** (not dockerd)
   - Enable container engine integration

This provides:

- Local Kubernetes cluster
- nerdctl for container builds (Docker-compatible CLI)
- kubectl for cluster management

### 2. Trust .NET Development Certificates

```bash
dotnet dev-certs https --trust
```

## Development Workflow

### Basic Development

```bash
# Build
make build

# Run the application (Aspire AppHost)
make run

# Or run with hot reload
make dev
```

- The Aspire dashboard will be available at `https://localhost:17098`
- The web app at `https://localhost:7071`
- The API at `https://localhost:7201`

### Makefile Commands

**Core Development:**

```bash
make restore      # Restore NuGet dependencies
make build        # Clean and build the application
make run          # Run the AppHost (main entry point)
make dev          # Run with hot reload using dotnet watch
make clean        # Clean build artifacts (removes bin/obj directories)
make format       # Format code using dotnet format
make test         # Run tests (when tests are added)
make publish      # Publish release build to ./publish
```

**Container & Kubernetes:**

```bash
make docker-build-api    # Build API service container with nerdctl
make docker-build-web    # Build Web application container with nerdctl
make docker-build        # Build both containers
make k8s-deploy          # Deploy to Kubernetes (applies manifests and shows token)
make deploy              # Full pipeline: docker-build → k8s-deploy
make k8s-status          # Check deployment status (pods, services, deployments)
make k8s-clean           # Clean up Kubernetes resources
make token               # Get Aspire dashboard login token
```

### Running Individual Projects

```bash
dotnet run --project src/AspireDeezNuts.AppHost      # Aspire orchestrator
dotnet run --project src/AspireDeezNuts.Web          # Web application only
dotnet run --project src/AspireDeezNuts.ApiService   # API service only
```

## Kubernetes Development

### Prerequisites

Before deploying to Kubernetes, you need to generate the required secrets file that contains authentication keys and configuration.

### Generating Required Secrets

The deployment requires a `secret.yaml` file (excluded from git) containing base64-encoded values for Aspire dashboard authentication. Create this file manually:

```bash
# Create the secret.yaml file in the manifests directory
cat > manifests/secret.yaml << 'EOF'
apiVersion: v1
kind: Secret
metadata:
  name: aspire-these-nutz
type: Opaque
data:
  # Base64 encoded values - replace with your own values
  DASHBOARD__OTLP__PRIMARYAPIKEY: $(echo -n "your-api-key-here" | base64 -w 0)
  DASHBOARD__FRONTEND__AUTHMODE: $(echo -n "BrowserToken" | base64 -w 0)
  DASHBOARD__OTLP__AUTHMODE: $(echo -n "ApiKey" | base64 -w 0)
  OTEL_EXPORTER_OTLP_HEADERS: $(echo -n "x-otlp-api-key=your-api-key-here" | base64 -w 0)
EOF
```

Or generate with random API key:

```bash
# Generate a random API key
API_KEY="aspire-$(openssl rand -hex 8)"

# Create secret.yaml with generated values
cat > manifests/secret.yaml << EOF
apiVersion: v1
kind: Secret
metadata:
  name: aspire-these-nutz
type: Opaque
data:
  DASHBOARD__OTLP__PRIMARYAPIKEY: $(echo -n "$API_KEY" | base64 -w 0)
  DASHBOARD__FRONTEND__AUTHMODE: $(echo -n "BrowserToken" | base64 -w 0)
  DASHBOARD__OTLP__AUTHMODE: $(echo -n "ApiKey" | base64 -w 0)
  OTEL_EXPORTER_OTLP_HEADERS: $(echo -n "x-otlp-api-key=$API_KEY" | base64 -w 0)
EOF

echo "Generated secret with API key: $API_KEY"
```

**Important:** Keep your API key secure and never commit the `secret.yaml` file to version control.

### Local Development Cycle

1. **Generate secrets (first time only):**
   ```bash
   # Generate the required secret.yaml file (see above)
   ```

2. **Build containers:**
   ```bash
   make docker-build
   ```

3. **Deploy to local Kubernetes:**
   ```bash
   make k8s-deploy
   ```

4. **Check status:**
   ```bash
   make k8s-status
   ```

### Full Deployment Pipeline

```bash
make deploy  # Builds containers and deploys to Kubernetes
```

### Resource Management

- **Memory**: 256Mi requests, 512Mi limits for standard services
- **CPU**: 100m requests, 500m limits for standard services  
- **Storage**: Persistent volumes for DataProtection keys and application data
- **Configuration**: External configuration via ConfigMaps and Secrets

### Health Checks

All services expose health endpoints compatible with Kubernetes:
- `/health` - Readiness probe
- `/alive` - Liveness probe

## Project Architecture

### Current Structure

- **AspireDeezNuts.AppHost**: Aspire orchestrator managing service discovery and configuration
- **AspireDeezNuts.ServiceDefaults**: Shared configuration for OpenTelemetry, health checks, and service discovery
- **AspireDeezNuts.ApiService**: Minimal API with weather forecast endpoint and OpenAPI support
- **AspireDeezNuts.Web**: Blazor Server application with interactive components

### Technology Stack

- .NET 9.0 with C# 13
- .NET Aspire 9.3.1 for distributed application orchestration
- Blazor Server-Side Rendering with interactive components
- Minimal APIs with OpenAPI/Swagger documentation
- OpenTelemetry for observability (metrics, tracing, logging)
- Kubernetes deployment via Aspirate
- Docker multi-stage builds

### Design Patterns Implementation Goals

This project aims to demonstrate practical implementation of GoF design patterns in a modern distributed application context, including:
- Creational patterns (Factory, Builder, Singleton)
- Structural patterns (Adapter, Decorator, Facade)
- Behavioral patterns (Observer, Strategy, Command)

Each pattern will be implemented with real-world scenarios that enhance the application's functionality while maintaining clean, maintainable code.