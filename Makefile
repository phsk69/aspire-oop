.PHONY: build run clean restore test publish dev

# Default target
all: restore build

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
	rm -rf bin/ obj/

# Run tests
test:
	dotnet test --no-build --verbosity normal

# Publish the application
publish:
	dotnet publish -c Release -o ./publish

# Format code
format:
	dotnet format

# Create migration (usage: make migration NAME=MigrationName)
migration:
	dotnet ef migrations add $(NAME)

# Update database
update-db:
	dotnet ef database update