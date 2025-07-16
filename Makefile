.PHONY: build run clean restore test publish dev web-dev docker-build-api docker-build-web docker-build k8s-generate k8s-deploy deploy k8s-status k8s-clean setup-tools

# Default target
all: restore build

# Setup required tools
setup-tools:
	@echo "Installing required .NET tools..."
	dotnet tool install -g aspirate || echo "aspirate already installed"
	@echo "Tools installation complete!"
	@echo "Note: Ensure Rancher Desktop is installed and configured with nerdctl for Kubernetes development"

# Restore dependencies
restore:
	dotnet restore

# Build the application
build:
	dotnet build --no-restore

# Run the application
run:
	dotnet run --project src/AspireDeezNuts.AppHost/AspireDeezNuts.AppHost.csproj 

# Run in development mode with hot reload
dev:
	dotnet watch --project src/AspireDeezNuts.AppHost/AspireDeezNuts.AppHost.csproj

# Clean build artifacts
clean:
	dotnet clean
	find . -type d \( -name "bin" -o -name "obj" \) -exec rm -rf {} +

# Run tests
test:
	dotnet test --no-build --verbosity normal

# Publish the application
publish:
	dotnet publish -c Release -o ./publish

# Format code
format:
	dotnet format

# Docker build commands (using names that match generated manifests)
docker-build-api:
	nerdctl build -t aspire-deez-nuts-api:latest -f src/AspireDeezNuts.ApiService/Dockerfile .
	nerdctl save aspire-deez-nuts-api:latest | nerdctl --namespace k8s.io load

docker-build-web:
	nerdctl build -t aspire-deez-nuts-web:latest -f src/AspireDeezNuts.Web/Dockerfile .
	nerdctl save aspire-deez-nuts-web:latest | nerdctl --namespace k8s.io load

docker-build: docker-build-api docker-build-web

# Generate Kubernetes manifests
k8s-generate:
	cd src/AspireDeezNuts.AppHost && aspirate generate --project-path . --output-path ./k8s-manifests --non-interactive --disable-secrets --include-dashboard --skip-build --image-pull-policy IfNotPresent

# Deploy to Kubernetes
k8s-deploy:
	kubectl apply -k ./src/AspireDeezNuts.AppHost/k8s-manifests/
	kubectl rollout restart deployment/aspire-deez-nuts-api || true
	kubectl rollout restart deployment/aspire-deez-nuts-web || true

# Combined deployment
deploy: docker-build k8s-generate k8s-deploy

# Check deployment status
k8s-status:
	kubectl get pods,svc,deployments

# Clean up Kubernetes resources
k8s-clean:
	kubectl delete -k ./src/AspireDeezNuts.AppHost/k8s-manifests/ || true
	rm -rf ./src/AspireDeezNuts.AppHost/k8s-manifests
