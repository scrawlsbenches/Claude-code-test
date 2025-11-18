# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files
COPY ["DistributedKernel.sln", "./"]
COPY ["src/HotSwap.Distributed.Domain/HotSwap.Distributed.Domain.csproj", "src/HotSwap.Distributed.Domain/"]
COPY ["src/HotSwap.Distributed.Infrastructure/HotSwap.Distributed.Infrastructure.csproj", "src/HotSwap.Distributed.Infrastructure/"]
COPY ["src/HotSwap.Distributed.Orchestrator/HotSwap.Distributed.Orchestrator.csproj", "src/HotSwap.Distributed.Orchestrator/"]
COPY ["src/HotSwap.Distributed.Api/HotSwap.Distributed.Api.csproj", "src/HotSwap.Distributed.Api/"]
COPY ["src/HotSwap.KnowledgeGraph.Domain/HotSwap.KnowledgeGraph.Domain.csproj", "src/HotSwap.KnowledgeGraph.Domain/"]
COPY ["src/HotSwap.KnowledgeGraph.Infrastructure/HotSwap.KnowledgeGraph.Infrastructure.csproj", "src/HotSwap.KnowledgeGraph.Infrastructure/"]
COPY ["src/HotSwap.KnowledgeGraph.QueryEngine/HotSwap.KnowledgeGraph.QueryEngine.csproj", "src/HotSwap.KnowledgeGraph.QueryEngine/"]
COPY ["tests/HotSwap.Distributed.Tests/HotSwap.Distributed.Tests.csproj", "tests/HotSwap.Distributed.Tests/"]
COPY ["tests/HotSwap.KnowledgeGraph.Tests/HotSwap.KnowledgeGraph.Tests.csproj", "tests/HotSwap.KnowledgeGraph.Tests/"]
COPY ["tests/HotSwap.Distributed.SmokeTests/HotSwap.Distributed.SmokeTests.csproj", "tests/HotSwap.Distributed.SmokeTests/"]
COPY ["examples/ApiUsageExample/ApiUsageExample.csproj", "examples/ApiUsageExample/"]

# Restore dependencies
RUN dotnet restore "DistributedKernel.sln"

# Copy source code
COPY . .

# Build
WORKDIR "/src/src/HotSwap.Distributed.Api"
RUN dotnet build "HotSwap.Distributed.Api.csproj" -c Release -o /app/build

# Test stage
FROM build AS test
WORKDIR /src
RUN dotnet test "DistributedKernel.sln" --configuration Release --no-restore --verbosity normal

# Publish
FROM build AS publish
WORKDIR "/src/src/HotSwap.Distributed.Api"
RUN dotnet publish "HotSwap.Distributed.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Install curl for health checks (as root before switching to appuser)
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

# Create non-root user
RUN useradd -m -u 1000 appuser && chown -R appuser:appuser /app
USER appuser

COPY --from=publish --chown=appuser:appuser /app/publish .

# Expose port
EXPOSE 8080

# Health check with longer start period to avoid race condition during app startup
HEALTHCHECK --interval=30s --timeout=3s --start-period=15s --retries=3 \
  CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "HotSwap.Distributed.Api.dll"]
