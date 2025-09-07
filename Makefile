.PHONY: build run clean restore test dev web-dev docker-build-api docker-build-web docker-build-fuzz docker-build k8s-generate k8s-deploy deploy k8s-status k8s-clean token format shell-api shell-web shell-fuzz update-minor ef-add-migration ef-update-database ef-remove-migration ef-drop-database ef-list-migrations ef-script-migration ef-reset-database ef-help

# Default target
all: restore build

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
	find . -type d \( -name "bin" -o -name "obj" \) -exec rm -rf {} + 2>/dev/null || true

# Run tests
test:
	dotnet test --verbosity normal

# Format code
format:
	dotnet format

# Update to latest minor/patch versions of dependencies
update-minor:
	dotnet outdated --version-lock major --upgrade

# Docker build commands (using names that match generated manifests)
docker-build-api:
	@echo "Building API service..."
	nerdctl build -t aspire-deez-nuts-api:latest -f src/AspireDeezNuts.ApiService/Dockerfile .
	nerdctl save aspire-deez-nuts-api:latest | nerdctl --namespace k8s.io load

docker-build-web:
	@echo "Building Web service..."
	nerdctl build -t aspire-deez-nuts-web:latest -f src/AspireDeezNuts.Web/Dockerfile .
	nerdctl save aspire-deez-nuts-web:latest | nerdctl --namespace k8s.io load

docker-build-fuzz:
	@echo "Building Fuzz Testing service..."
	nerdctl build -t aspire-deez-nuts-fuzz:latest -f src/AspireDeezNuts.FuzzTesting/Dockerfile .
	nerdctl save aspire-deez-nuts-fuzz:latest | nerdctl --namespace k8s.io load

docker-build: docker-build-api docker-build-web docker-build-fuzz

# Deploy to Kubernetes
k8s-deploy:
	kubectl apply -k ./manifests/
	kubectl rollout restart deployment/aspire-deez-nuts-api || true
	kubectl rollout restart deployment/aspire-deez-nuts-web || true
	kubectl rollout restart deployment/aspire-deez-nuts-fuzz || true
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

# Connect to Fuzz Testing service container shell
shell-fuzz:
	@echo "🔗 Connecting to Fuzz Testing service container..."
	@FUZZ_POD=$$(kubectl get pods -l app=aspire-deez-nuts-fuzz -o jsonpath='{.items[0].metadata.name}' 2>/dev/null); \
	if [ -z "$$FUZZ_POD" ]; then \
		echo "❌ No Fuzz Testing service pods found. Is the deployment running?"; \
		echo "Run 'make k8s-status' to check pod status"; \
		exit 1; \
	fi; \
	echo "📡 Connecting to pod: $$FUZZ_POD"; \
	kubectl exec -it $$FUZZ_POD -- /bin/bash

# Entity Framework Core Commands

# Add a new migration - Usage: make ef-add-migration NAME=MigrationName
ef-add-migration:
	@if [ -z "$(NAME)" ]; then \
		echo "❌ Migration name is required. Usage: make ef-add-migration NAME=MigrationName"; \
		exit 1; \
	fi
	@echo "📦 Adding migration: $(NAME)"
	cd src/AspireDeezNuts.ApiService && \
	dotnet ef migrations add $(NAME) --context AppMigrationDbContext

# Update database with latest migrations
ef-update-database:
	@echo "🔄 Updating database with latest migrations..."
	cd src/AspireDeezNuts.ApiService && \
	dotnet ef database update --context AppMigrationDbContext

# Remove the last migration
ef-remove-migration:
	@echo "🗑️  Removing last migration..."
	cd src/AspireDeezNuts.ApiService && \
	dotnet ef migrations remove --context AppMigrationDbContext

# Drop the database (WARNING: This will delete all data!)
ef-drop-database:
	@echo "⚠️  WARNING: This will drop the entire database and all data will be lost!"
	@echo "Are you sure you want to continue? Type 'yes' to confirm:"
	@read -r confirm; \
	if [ "$$confirm" = "yes" ]; then \
		echo "💥 Dropping database..."; \
		cd src/AspireDeezNuts.ApiService && \
		dotnet ef database drop --context AppMigrationDbContext --force; \
	else \
		echo "❌ Database drop cancelled."; \
	fi

# List all migrations
ef-list-migrations:
	@echo "📋 Listing all migrations..."
	cd src/AspireDeezNuts.ApiService && \
	dotnet ef migrations list --context AppMigrationDbContext

# Generate SQL script for migrations - Usage: make ef-script-migration [FROM=StartMigration] [TO=EndMigration]
ef-script-migration:
	@echo "📜 Generating SQL migration script..."
	@if [ -n "$(FROM)" ] && [ -n "$(TO)" ]; then \
		echo "📜 Generating script from $(FROM) to $(TO)..."; \
		cd src/AspireDeezNuts.ApiService && \
		dotnet ef migrations script $(FROM) $(TO) --context AppMigrationDbContext --output migrations-$(FROM)-to-$(TO).sql; \
		echo "✅ Script saved to: src/AspireDeezNuts.ApiService/migrations-$(FROM)-to-$(TO).sql"; \
	elif [ -n "$(FROM)" ]; then \
		echo "📜 Generating script from $(FROM) to latest..."; \
		cd src/AspireDeezNuts.ApiService && \
		dotnet ef migrations script $(FROM) --context AppMigrationDbContext --output migrations-$(FROM)-to-latest.sql; \
		echo "✅ Script saved to: src/AspireDeezNuts.ApiService/migrations-$(FROM)-to-latest.sql"; \
	else \
		echo "📜 Generating complete migration script..."; \
		cd src/AspireDeezNuts.ApiService && \
		dotnet ef migrations script --context AppMigrationDbContext --output complete-migrations.sql; \
		echo "✅ Script saved to: src/AspireDeezNuts.ApiService/complete-migrations.sql"; \
	fi

# Reset database (drop and recreate with latest migrations)
ef-reset-database: ef-drop-database ef-update-database
	@echo "✅ Database reset completed!"

# EF Core help
ef-help:
	@echo "🔧 Entity Framework Core Commands:"
	@echo ""
	@echo "Migration Management:"
	@echo "  make ef-add-migration NAME=MigrationName  - Add a new migration"
	@echo "  make ef-update-database                   - Apply migrations to database"
	@echo "  make ef-remove-migration                  - Remove the last migration"
	@echo "  make ef-list-migrations                   - List all migrations"
	@echo ""
	@echo "Database Management:"
	@echo "  make ef-drop-database                     - Drop the database (⚠️  DESTRUCTIVE)"
	@echo "  make ef-reset-database                    - Drop and recreate database"
	@echo ""
	@echo "SQL Script Generation:"
	@echo "  make ef-script-migration                  - Generate complete SQL script"
	@echo "  make ef-script-migration FROM=Start      - Generate script from specific migration"
	@echo "  make ef-script-migration FROM=Start TO=End - Generate script between migrations"
	@echo ""
	@echo "Examples:"
	@echo "  make ef-add-migration NAME=AddUserTable"
	@echo "  make ef-script-migration FROM=InitialCreate TO=AddUserTable"
