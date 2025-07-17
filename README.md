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
make build        # Build the application
make run          # Run the AppHost (main entry point)
make dev          # Run with hot reload using dotnet watch
make clean        # Clean build artifacts
make format       # Format code
```

**Container & Kubernetes:**
```bash
make docker-build-api    # Build API service container
make docker-build-web    # Build Web application container
make docker-build        # Build both containers
make k8s-generate        # Generate Kubernetes manifests
make k8s-deploy          # Deploy to Kubernetes
make deploy              # Full pipeline: build → generate → deploy
make k8s-status          # Check deployment status
make k8s-clean           # Clean up Kubernetes resources
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

2. **Generate manifests:**
   ```bash
   make k8s-generate
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
make deploy  # Builds containers, generates manifests, and deploys
```

This uses the Aspirate tool to convert Aspire configuration into Kubernetes manifests automatically.

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