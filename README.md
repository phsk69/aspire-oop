# Aspire Object-Oriented Design Training

A .NET Aspire distributed application demonstrating object-oriented design patterns with Blazor Server frontend and minimal API backend. This project serves as a training ground for implementing design patterns from "Design Patterns: Elements of Reusable Object-Oriented Software" (Gang of Four book) while leveraging modern .NET Aspire orchestration and Kubernetes deployment capabilities.

## Prerequisites

- .NET 9 SDK
- For Kubernetes development:
  - Rancher Desktop set up for the nerdctl approach

## Initial Setup

### 1. Install Required Tools

```bash
# Install global .NET tools
make setup-tools

# Or manually:
dotnet tool install -g aspirate
```

### 2. Rancher Desktop Setup (for Kubernetes Development)

1. Install [Rancher Desktop](https://rancherdesktop.io/)
2. Configure Rancher Desktop:
   - Enable Kubernetes
   - Set container runtime to **nerdctl** (not dockerd)
   - Enable container engine integration

This provides:
- Local Kubernetes cluster
- nerdctl for container builds (Docker-compatible CLI)
- kubectl for cluster management

### 3. Trust .NET Development Certificates

```bash
dotnet dev-certs https --trust
```

## Development Workflow

### Basic Development

```bash
# Restore dependencies and build
make restore
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

### Local Development Cycle

1. **Build containers:**
   ```bash
   make docker-build
   ```

2. **Deploy to local Kubernetes:**
   ```bash
   make k8s-deploy
   ```

3. **Check status:**
   ```bash
   make k8s-status
   ```

### Full Deployment Pipeline

```bash
make deploy  # Builds containers and deploys to Kubernetes
```

The manifests in the `manifests/` directory are local and and should be created before deployment.

## Kubernetes Manifests

The `manifests/` directory contains Kubernetes deployment specifications for all services:

### Core Application Services
```bash
manifests/
├── api-service.yaml      # API service deployment and service
├── web-service.yaml      # Web application deployment and service  
├── dashboard.yaml        # Aspire dashboard deployment and service
├── configmap.yaml        # Environment configuration
├── secret.yaml           # Sensitive configuration (template)
├── pvc.yaml             # Persistent volume claims for data storage
└── kustomization.yaml   # Kustomize resource aggregation
```

### Deployment Examples

**API Service Structure:**

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: my-api-service
spec:
  replicas: 1
  template:
    spec:
      containers:
        - name: api
          image: my-api:latest
          ports:
            - containerPort: 8080
          envFrom:
            - configMapRef:
                name: app-config
            - secretRef:
                name: app-secrets
```

**Service Configuration:**

```yaml
apiVersion: v1
kind: Service
metadata:
  name: my-api-service
spec:
  selector:
    app: my-api-service
  ports:
    - port: 8080
      targetPort: 8080
```

**ConfigMap Pattern:**

```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: app-config
data:
  ASPNETCORE_URLS: "http://+:8080"
  DEPLOYMENT_ENVIRONMENT: "Kubernetes"
  OTEL_EXPORTER_OTLP_ENDPOINT: "http://dashboard:18889"
```

**Secret Template:**

```yaml
apiVersion: v1
kind: Secret
metadata:
  name: app-secrets
type: Opaque
stringData:
  ConnectionString: "Server=myserver;Database=mydb;User=myuser;Password=changeme"
  ApiKey: "your-api-key-here"
```

**Persistent Volume Claim:**

```yaml
apiVersion: v1
kind: PersistentVolumeClaim
metadata:
  name: app-data
spec:
  accessModes:
    - ReadWriteOnce
  resources:
    requests:
      storage: 1Gi
```

**Dashboard Deployment:**

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: aspire-dashboard
spec:
  replicas: 1
  selector:
    matchLabels:
      app: aspire-dashboard
  template:
    metadata:
      labels:
        app: aspire-dashboard
    spec:
      containers:
        - name: dashboard
          image: mcr.microsoft.com/dotnet/aspire-dashboard:9.0
          ports:
            - containerPort: 18888
              name: frontend
            - containerPort: 18889
              name: otlp
          envFrom:
            - configMapRef:
                name: dashboard-config
            - secretRef:
                name: dashboard-secrets
```

**Kustomization File:**

```yaml
apiVersion: kustomize.config.k8s.io/v1beta1
kind: Kustomization

resources:
  - secret.yaml
  - configmap.yaml
  - pvc.yaml
  - dashboard.yaml
  - api-service.yaml
  - web-service.yaml
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