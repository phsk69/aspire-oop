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

## Required Configuration Setup

### API Service Secrets Configuration

**IMPORTANT**: Before running the application or tests, you must create the required secrets file for the API service.

#### Development Environment Setup

1. **Copy the template file:**
   ```bash
   cp src/AspireDeezNuts.ApiService/appsettings.Development.secrets.template.json \
      src/AspireDeezNuts.ApiService/appsettings.Development.secrets.json
   ```

2. **Update the secrets file** with your actual values:
   ```json
   {
     "Jwt": {
       "Secret": "YOUR-SUPER-SECRET-JWT-KEY-MINIMUM-32-CHARACTERS-LONG!"
     },
     "SeedData": {
       "InitialAdmin": {
         "Email": "admin@yourdomain.com",
         "Password": "YourSecureAdminPassword123!"
       }
     }
   }
   ```

   **Requirements:**
   - `Jwt.Secret`: Must be at least 32 characters long for security
   - `SeedData.InitialAdmin.Email`: Valid email format for the admin user
   - `SeedData.InitialAdmin.Password`: Strong password meeting complexity requirements

#### Test Environment Setup

**CRITICAL**: The test suite requires the same secrets file to be copied to the test project:

```bash
# Copy secrets file to test project
cp src/AspireDeezNuts.ApiService/appsettings.Development.secrets.json \
   tests/AspireDeezNuts.ApiService.Tests/appsettings.Development.secrets.json
```

**Why this is required:**
- Tests load actual configuration from the secrets file instead of using hardcoded values
- This ensures tests validate against the same authentication setup as production
- Test isolation is maintained while using realistic credentials

#### Security Notes

- ✅ **Template file** (`appsettings.Development.secrets.template.json`) is committed to git
- ❌ **Actual secrets files** (`appsettings.Development.secrets.json`) are gitignored and must be created locally
- 🔒 Never commit actual secrets to version control
- 🔄 Keep both development and test secrets files synchronized

#### Quick Setup Script

For convenience, you can use this one-liner to set up both secrets files:

```bash
# Copy template to development and test locations
cp src/AspireDeezNuts.ApiService/appsettings.Development.secrets.template.json \
   src/AspireDeezNuts.ApiService/appsettings.Development.secrets.json && \
cp src/AspireDeezNuts.ApiService/appsettings.Development.secrets.json \
   tests/AspireDeezNuts.ApiService.Tests/appsettings.Development.secrets.json
```

**Next steps after running the script:**
1. Edit `src/AspireDeezNuts.ApiService/appsettings.Development.secrets.json` with your actual values
2. Copy the updated file to the test project again to keep them synchronized

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
make test         # Run tests (requires secrets file in test project)
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

### Running Tests

**Prerequisites**: Ensure you have copied the secrets file to the test project (see [Test Environment Setup](#test-environment-setup)).

```bash
# Run all tests
make test

# Run specific test project
dotnet test tests/AspireDeezNuts.ApiService.Tests/

# Run specific test method
dotnet test --filter "TestMethodName"
```

**Important**: Tests will fail if the secrets file is missing from the test project. The test suite:
- Loads configuration from `appsettings.Development.secrets.json` in the test project
- Uses actual JWT secrets for authentication testing
- Creates isolated in-memory databases for each test method
- Validates against the same admin credentials configured in development

## Kubernetes Development

### Prerequisites

Before deploying to Kubernetes, you need to generate the required secrets file that contains authentication keys and configuration.

### Generating Required Secrets

#### JWT Secret Generation

Generate a secure JWT secret for API authentication:

```bash
# Generate a secure JWT secret (minimum 32 characters)
openssl rand -base64 32

# Create Kubernetes secret for JWT
kubectl create secret generic aspire-jwt-secret \
  --from-literal=JWT_SECRET="your-generated-secret-here"

# Or use the provided manifest (update the secret value first)
kubectl apply -f manifests/jwt-secret.yaml
```

#### Admin User Seed Secret

Configure the initial admin user for the application:

```bash
# Create admin seed secret with secure credentials
kubectl create secret generic aspire-admin-seed \
  --from-literal=SeedData__InitialAdmin__Email="admin@yourdomain.com" \
  --from-literal=SeedData__InitialAdmin__Password="YourSecurePassword123!"

# Or use the provided manifest (update credentials first)
kubectl apply -f manifests/admin-seed-secret.yaml
```

#### Dashboard Authentication Secret

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
- **AspireDeezNuts.ApiService**: Secured minimal API with JWT authentication, user management, posts management with authentication, comprehensive compliance logging, and OpenAPI support
- **AspireDeezNuts.Web**: Blazor Server application with authentication, user administration, authenticated posts display with pagination, and interactive components
- **AspireDeezNuts.Shared**: Shared class library containing common interfaces, models, and DTOs for user management

### Database Architecture

The project uses an **in-memory database** for both development and deployment scenarios. This design choice provides several benefits:

- **Flexible Development**: Rapid iteration without external database dependencies
- **Integration Testing**: Proper testing capabilities with realistic data persistence within application lifecycle
- **Deployment Simplicity**: Emulates actual deployment patterns while maintaining portability across environments
- **Rancher Desktop Compatibility**: Seamless local Kubernetes development without additional infrastructure setup

The in-memory approach allows for realistic authentication flows with JWT token persistence while keeping the development experience lightweight and the deployment process flexible.

### Technology Stack

- .NET 9.0 with C# 13
- .NET Aspire 9.X for distributed application orchestration
- Blazor Server-Side Rendering with interactive components
- Blazor Bootstrap 3.4.0 for UI components
- JWT Authentication with in-memory identity database
- Role-based access control (Admin and User roles)
- User management system with full CRUD operations
- Minimal APIs with OpenAPI/Swagger documentation
- Repository pattern with external API integration (JsonPlaceholder)
- Comprehensive compliance logging for all user actions
- OpenTelemetry for observability (metrics, tracing, logging)
- Hand-crafted Kubernetes manifests deployment
- Docker multi-stage builds with nerdctl

### Design Patterns Implementation Goals

This project aims to demonstrate practical implementation of GoF design patterns in a modern distributed application context, including:
- Creational patterns (Factory, Builder, Singleton)
- Structural patterns (Adapter, Decorator, Facade)
- Behavioral patterns (Observer, Strategy, Command)

Each pattern will be implemented with real-world scenarios that enhance the application's functionality while maintaining clean, maintainable code.

## Troubleshooting

### Common Setup Issues

#### "Configuration not found" or Authentication Failures

**Symptoms:**
- Application starts but login fails with admin credentials
- Tests fail with "Initial admin configuration not found or incomplete"
- JWT authentication errors

**Solution:**
1. Verify secrets file exists:
   ```bash
   # Check development secrets file
   ls -la src/AspireDeezNuts.ApiService/appsettings.Development.secrets.json
   
   # Check test secrets file (required for tests)
   ls -la tests/AspireDeezNuts.ApiService.Tests/appsettings.Development.secrets.json
   ```

2. If missing, copy from template:
   ```bash
   # Copy for development
   cp src/AspireDeezNuts.ApiService/appsettings.Development.secrets.template.json \
      src/AspireDeezNuts.ApiService/appsettings.Development.secrets.json
   
   # Copy for tests
   cp src/AspireDeezNuts.ApiService/appsettings.Development.secrets.json \
      tests/AspireDeezNuts.ApiService.Tests/appsettings.Development.secrets.json
   ```

3. Verify the secrets file contains valid JSON and meets requirements:
   - JWT Secret: minimum 32 characters
   - Admin email: valid email format
   - Admin password: meets complexity requirements

#### Test Failures

**Symptoms:**
- Tests pass individually but fail when run together
- "User should have been created" assertion failures
- Database isolation issues

**Solution:**
- Ensure both secrets files are identical and valid
- Tests use the `[DoNotParallelize]` attribute to prevent race conditions
- Each test gets a unique in-memory database for isolation

#### Kubernetes Deployment Issues

Refer to the [Kubernetes Development](#kubernetes-development) section for secrets and deployment troubleshooting.