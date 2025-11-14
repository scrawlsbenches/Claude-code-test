# Testing Guide

## Overview

This document describes how to build, test, and validate the Distributed Kernel Orchestration System.

## Prerequisites

- .NET 8 SDK
- Docker and Docker Compose (for integration testing)
- Git

## Quick Start

### 1. Build the Solution

```bash
# Restore NuGet packages
dotnet restore

# Build in Release mode
dotnet build --configuration Release

# Or build specific project
dotnet build src/HotSwap.Distributed.Api/HotSwap.Distributed.Api.csproj
```

### 2. Run Unit Tests

```bash
# Run all tests
dotnet test

# Run with detailed output
dotnet test --verbosity normal

# Run with code coverage
dotnet test --collect:"XPlat Code Coverage"

# Run specific test class
dotnet test --filter "FullyQualifiedName~KernelNodeTests"
```

### 3. Integration Testing with Docker

```bash
# Build and start all services
docker-compose up -d

# Wait for services to be ready
sleep 10

# Test the API
curl http://localhost:5000/health

# View API documentation
open http://localhost:5000

# Test deployment endpoint
curl -X POST http://localhost:5000/api/v1/deployments \
  -H "Content-Type: application/json" \
  -d '{
    "moduleName": "test-module",
    "version": "1.0.0",
    "targetEnvironment": "Development",
    "requesterEmail": "test@example.com"
  }'

# Check logs
docker-compose logs -f orchestrator-api

# Stop services
docker-compose down
```

## Test Structure

```
tests/HotSwap.Distributed.Tests/
├── Core/
│   ├── KernelNodeTests.cs          # Node lifecycle tests
│   └── EnvironmentClusterTests.cs  # Cluster management tests
├── Strategies/
│   ├── DirectDeploymentStrategyTests.cs
│   ├── RollingDeploymentStrategyTests.cs
│   ├── BlueGreenDeploymentStrategyTests.cs
│   └── CanaryDeploymentStrategyTests.cs
├── Domain/
│   └── ModuleDescriptorTests.cs    # Domain model validation
└── Infrastructure/
    ├── TelemetryProviderTests.cs
    └── ModuleVerifierTests.cs
```

## Test Categories

### Unit Tests

Test individual components in isolation:

```bash
# Run unit tests only
dotnet test --filter "Category=Unit"
```

Example test:
```csharp
[Fact]
public async Task DeployAsync_WithHealthyCluster_ReturnsSuccess()
{
    // Arrange
    var strategy = new DirectDeploymentStrategy(_logger);
    var cluster = CreateTestCluster(nodeCount: 5);

    // Act
    var result = await strategy.DeployAsync(request, cluster);

    // Assert
    result.Success.Should().BeTrue();
    result.NodeResults.Should().HaveCount(5);
}
```

### Integration Tests

Test complete workflows:

```bash
# Run integration tests
dotnet test --filter "Category=Integration"
```

### Performance Tests

Measure deployment performance:

```bash
# Run performance tests
dotnet test --filter "Category=Performance"
```

## Code Coverage

### Generate Coverage Report

```bash
# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage"

# Install report generator
dotnet tool install -g dotnet-reportgenerator-globaltool

# Generate HTML report
reportgenerator \
  -reports:"**/coverage.cobertura.xml" \
  -targetdir:"coverage-report" \
  -reporttypes:Html

# View report
open coverage-report/index.html
```

### Coverage Goals

- Overall: > 80%
- Core components: > 90%
- Critical paths: 100%

## Manual Testing

### Test Deployment Pipeline

```bash
# 1. Start the system
docker-compose up -d

# 2. Create a deployment
EXECUTION_ID=$(curl -s -X POST http://localhost:5000/api/v1/deployments \
  -H "Content-Type: application/json" \
  -d '{
    "moduleName": "payment-processor",
    "version": "2.1.0",
    "targetEnvironment": "Production",
    "requesterEmail": "test@example.com"
  }' | jq -r '.executionId')

echo "Deployment ID: $EXECUTION_ID"

# 3. Check deployment status
curl http://localhost:5000/api/v1/deployments/$EXECUTION_ID | jq

# 4. View traces in Jaeger
open "http://localhost:16686/search?service=HotSwap.DistributedKernel"

# 5. Check cluster health
curl http://localhost:5000/api/v1/clusters/Production | jq

# 6. Get metrics
curl "http://localhost:5000/api/v1/clusters/Production/metrics" | jq
```

### Test Rollback

```bash
# Trigger rollback
curl -X POST http://localhost:5000/api/v1/deployments/$EXECUTION_ID/rollback

# Verify rollback
curl http://localhost:5000/api/v1/clusters/Production | jq '.nodes[].status'
```

### Test Different Strategies

#### Direct (Development)
```bash
curl -X POST http://localhost:5000/api/v1/deployments \
  -H "Content-Type: application/json" \
  -d '{"moduleName":"test","version":"1.0.0","targetEnvironment":"Development","requesterEmail":"test@example.com"}'
```

#### Rolling (QA)
```bash
curl -X POST http://localhost:5000/api/v1/deployments \
  -H "Content-Type: application/json" \
  -d '{"moduleName":"test","version":"1.0.0","targetEnvironment":"QA","requesterEmail":"test@example.com"}'
```

#### Canary (Production)
```bash
curl -X POST http://localhost:5000/api/v1/deployments \
  -H "Content-Type: application/json" \
  -d '{"moduleName":"test","version":"1.0.0","targetEnvironment":"Production","requesterEmail":"test@example.com"}'
```

## Load Testing

### Using k6

```javascript
// load-test.js
import http from 'k6/http';
import { check, sleep } from 'k6';

export let options = {
  stages: [
    { duration: '1m', target: 10 },
    { duration: '3m', target: 10 },
    { duration: '1m', target: 0 },
  ],
};

export default function() {
  let payload = JSON.stringify({
    moduleName: 'load-test-module',
    version: '1.0.0',
    targetEnvironment: 'Development',
    requesterEmail: 'loadtest@example.com'
  });

  let params = {
    headers: {
      'Content-Type': 'application/json',
    },
  };

  let res = http.post('http://localhost:5000/api/v1/deployments', payload, params);

  check(res, {
    'status is 202': (r) => r.status === 202,
    'has executionId': (r) => JSON.parse(r.body).executionId !== undefined,
  });

  sleep(1);
}
```

Run load test:
```bash
k6 run load-test.js
```

## Debugging

### View Logs

```bash
# API logs
docker-compose logs -f orchestrator-api

# All logs
docker-compose logs -f

# Follow specific service
docker-compose logs -f redis
docker-compose logs -f jaeger
```

### Attach Debugger

1. Run API locally:
```bash
cd src/HotSwap.Distributed.Api
dotnet run
```

2. Attach debugger in VS Code or Visual Studio
3. Set breakpoints
4. Send requests to http://localhost:5000

## Continuous Integration

GitHub Actions automatically:
1. Builds the solution
2. Runs all tests
3. Generates code coverage
4. Builds Docker image
5. Validates code formatting

See `.github/workflows/build-and-test.yml`

## Troubleshooting

### Build Failures

```bash
# Clean build artifacts
dotnet clean

# Restore packages
dotnet restore --force

# Rebuild
dotnet build --no-incremental
```

### Test Failures

```bash
# Run tests with detailed output
dotnet test --verbosity diagnostic

# Run specific failing test
dotnet test --filter "FullyQualifiedName~TestMethodName"
```

### Docker Issues

```bash
# Remove all containers and volumes
docker-compose down -v

# Rebuild images
docker-compose build --no-cache

# Start fresh
docker-compose up -d
```

## Performance Benchmarks

Expected performance:
- Development (3 nodes): < 30 seconds
- QA (5 nodes): < 5 minutes
- Staging (10 nodes): < 10 minutes
- Production (20 nodes): < 30 minutes (canary)

API latency:
- Health check: < 100ms
- Metrics query: < 200ms
- Deployment creation: < 500ms

## Best Practices

1. **Always run tests before committing**
   ```bash
   dotnet test && git commit
   ```

2. **Use code coverage to identify gaps**
   ```bash
   dotnet test --collect:"XPlat Code Coverage"
   ```

3. **Test with Docker before deployment**
   ```bash
   docker-compose up -d
   # Run integration tests
   docker-compose down
   ```

4. **Review Jaeger traces for performance issues**
   - Open http://localhost:16686
   - Look for slow operations
   - Identify bottlenecks

5. **Monitor cluster health during tests**
   ```bash
   watch -n 5 'curl -s http://localhost:5000/api/v1/clusters/Development | jq'
   ```

## Resources

- [.NET Testing Documentation](https://docs.microsoft.com/en-us/dotnet/core/testing/)
- [xUnit Documentation](https://xunit.net/)
- [FluentAssertions Documentation](https://fluentassertions.com/)
- [Moq Documentation](https://github.com/moq/moq4)
