.PHONY: build run clean restore test publish dev web-dev docker-build-api docker-build-web docker-build k8s-generate k8s-deploy deploy k8s-status k8s-clean setup-tools token format shell-api shell-web

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
	dotnet clean && dotnet build

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
	dotnet test --verbosity normal

# Publish the application
publish:
	dotnet publish -c Release -o ./publish

# Format code
format:
	dotnet format

# Docker build commands (using names that match generated manifests)
docker-build-api:
	@echo "Building API service..."
	nerdctl build -t aspire-deez-nuts-api:latest -f src/AspireDeezNuts.ApiService/Dockerfile .
	nerdctl save aspire-deez-nuts-api:latest | nerdctl --namespace k8s.io load

docker-build-web:
	@echo "Building Web service..."
	nerdctl build -t aspire-deez-nuts-web:latest -f src/AspireDeezNuts.Web/Dockerfile .
	nerdctl save aspire-deez-nuts-web:latest | nerdctl --namespace k8s.io load

docker-build: docker-build-api docker-build-web

# Deploy to Kubernetes
k8s-deploy:
	kubectl apply -k ./manifests/
	kubectl rollout restart deployment/aspire-deez-nuts-api || true
	kubectl rollout restart deployment/aspire-deez-nuts-web || true
	kubectl rollout restart deployment/aspire-dashboard || true
	@echo "Waiting for dashboard to be ready..."
	@kubectl wait --for=condition=ready pod -l app=aspire-dashboard --timeout=60s
	@sleep 3
	@echo "🔐 Dashboard Login Token:"
	@NEWEST_POD=$$(kubectl get pods -l app=aspire-dashboard --sort-by=.metadata.creationTimestamp -o jsonpath='{.items[-1].metadata.name}'); \
	kubectl logs $$NEWEST_POD | grep "Login to the dashboard" | tail -1 | sed 's/.*?t=//' | sed 's/. The URL.*//'

# Combined deployment
deploy: docker-build k8s-deploy

# Check deployment status
k8s-status:
	kubectl get pods,svc,deployments

# Get dashboard login token
token:
	@echo "🔐 Dashboard Login Token:"
	@NEWEST_POD=$$(kubectl get pods -l app=aspire-dashboard --sort-by=.metadata.creationTimestamp -o jsonpath='{.items[-1].metadata.name}'); \
	kubectl logs $$NEWEST_POD --since-time=$$(kubectl get pod $$NEWEST_POD -o jsonpath='{.metadata.creationTimestamp}') | grep "Login to the dashboard" | head -1 | sed 's/.*?t=//' | sed 's/. The URL.*//'

# Clean up Kubernetes resources
k8s-clean:
	kubectl delete -k ./manifests/ || true

# Connect to API service container shell
shell-api:
	@echo "🔗 Connecting to API service container..."
	@API_POD=$$(kubectl get pods -l app=aspire-deez-nuts-api -o jsonpath='{.items[0].metadata.name}' 2>/dev/null); \
	if [ -z "$$API_POD" ]; then \
		echo "❌ No API service pods found. Is the deployment running?"; \
		echo "Run 'make k8s-status' to check pod status"; \
		exit 1; \
	fi; \
	echo "📡 Connecting to pod: $$API_POD"; \
	kubectl exec -it $$API_POD -- /bin/bash

# Connect to Web service container shell
shell-web:
	@echo "🔗 Connecting to Web service container..."
	@WEB_POD=$$(kubectl get pods -l app=aspire-deez-nuts-web -o jsonpath='{.items[0].metadata.name}' 2>/dev/null); \
	if [ -z "$$WEB_POD" ]; then \
		echo "❌ No Web service pods found. Is the deployment running?"; \
		echo "Run 'make k8s-status' to check pod status"; \
		exit 1; \
	fi; \
	echo "📡 Connecting to pod: $$WEB_POD"; \
	kubectl exec -it $$WEB_POD -- /bin/bash
