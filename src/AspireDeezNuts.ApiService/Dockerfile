FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy project files
COPY ["src/AspireDeezNuts.ApiService/AspireDeezNuts.ApiService.csproj", "src/AspireDeezNuts.ApiService/"]
COPY ["src/AspireDeezNuts.ServiceDefaults/AspireDeezNuts.ServiceDefaults.csproj", "src/AspireDeezNuts.ServiceDefaults/"]

# Restore dependencies
RUN dotnet restore "src/AspireDeezNuts.ApiService/AspireDeezNuts.ApiService.csproj"

# Copy everything else
COPY . .

# Build
WORKDIR "/src/src/AspireDeezNuts.ApiService"
RUN dotnet build "AspireDeezNuts.ApiService.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Publish
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "AspireDeezNuts.ApiService.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Final stage
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "AspireDeezNuts.ApiService.dll"]