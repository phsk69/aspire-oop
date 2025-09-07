FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8443

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy global build configuration
COPY ["Directory.Build.props", "Directory.Build.props"]

# Copy project files
COPY ["src/AspireDeezNuts.FuzzTesting/AspireDeezNuts.FuzzTesting.csproj", "src/AspireDeezNuts.FuzzTesting/"]
COPY ["src/AspireDeezNuts.Shared/AspireDeezNuts.Shared.csproj", "src/AspireDeezNuts.Shared/"]
COPY ["src/AspireDeezNuts.ServiceDefaults/AspireDeezNuts.ServiceDefaults.csproj", "src/AspireDeezNuts.ServiceDefaults/"]

# Restore dependencies
RUN dotnet restore "src/AspireDeezNuts.FuzzTesting/AspireDeezNuts.FuzzTesting.csproj"

# Copy everything else
COPY . .

# Publish (includes build)
WORKDIR "/src/src/AspireDeezNuts.FuzzTesting"
RUN dotnet publish "AspireDeezNuts.FuzzTesting.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Final stage
FROM base AS final
WORKDIR /app

# Install ca-certificates for HTTPS support
RUN apt-get update && \
    apt-get install -y ca-certificates && \
    rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

# Script to trust cert-manager CA at runtime
RUN echo '#!/bin/bash\n\
if [ -f /tls/ca.crt ]; then\n\
    cp /tls/ca.crt /usr/local/share/ca-certificates/cert-manager-ca.crt\n\
    update-ca-certificates\n\
    echo "Cert-manager CA certificate installed and trusted"\n\
fi\n\
exec dotnet AspireDeezNuts.FuzzTesting.dll' > /entrypoint.sh && \
    chmod +x /entrypoint.sh

# Run as root for simplified certificate access
ENTRYPOINT ["/entrypoint.sh"]