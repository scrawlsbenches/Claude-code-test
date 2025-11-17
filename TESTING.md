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

Integration tests verify complete workflows using real dependencies (PostgreSQL, Redis) via Testcontainers. These tests validate end-to-end scenarios with an in-memory API server.

#### Test Structure

```
tests/HotSwap.Distributed.IntegrationTests/
├── Fixtures/
│   ├── PostgreSqlContainerFixture.cs   # PostgreSQL 16 Testcontainer
│   ├── RedisContainerFixture.cs        # Redis 7 Testcontainer
│   └── IntegrationTestFactory.cs       # Custom WebApplicationFactory
├── Helpers/
│   ├── AuthHelper.cs                   # JWT token management
│   ├── ApiClientHelper.cs              # API operation helpers
│   └── TestDataBuilder.cs              # Test data creation
└── Tests/
    ├── BasicIntegrationTests.cs                    # 9 tests - Health, auth, clusters
    ├── DeploymentStrategyIntegrationTests.cs       # 9 tests - All 4 strategies
    ├── ApprovalWorkflowIntegrationTests.cs         # 10 tests - Approve/reject
    ├── RollbackScenarioIntegrationTests.cs         # 10 tests - Rollback workflows
    ├── ConcurrentDeploymentIntegrationTests.cs     # 8 tests - Concurrency & stress
    ├── MessagingIntegrationTests.cs                # 19 tests - Message queue system
    └── MultiTenantIntegrationTests.cs              # 17 tests - Multi-tenant features
```

**Total**: 82 integration tests across 6 test files

#### Prerequisites

Integration tests require Docker to run Testcontainers:

```bash
# Verify Docker is running
docker ps

# If Docker is not running, start it
# macOS/Windows: Start Docker Desktop
# Linux: sudo systemctl start docker
```

#### Running Integration Tests Locally

**Important**: Integration tests cannot run without Docker. They use Testcontainers to spin up real PostgreSQL and Redis instances.

```bash
# Run all integration tests (requires Docker)
dotnet test tests/HotSwap.Distributed.IntegrationTests/HotSwap.Distributed.IntegrationTests.csproj

# Run with detailed output
dotnet test tests/HotSwap.Distributed.IntegrationTests/ --verbosity normal

# Run specific test file
dotnet test --filter "FullyQualifiedName~BasicIntegrationTests"

# Run specific test method
dotnet test --filter "FullyQualifiedName~DeploymentStrategyIntegrationTests.DirectDeployment_ToDevelopmentEnvironment_CompletesSuccessfully"
```

**Expected Output**:
```
Passed!  - Failed:     0, Passed:    82, Skipped:     0, Total:    82, Duration: 45 s
```

**Note**: First run will be slower as Docker images are pulled (postgres:16-alpine, redis:7-alpine).

#### Integration Test Details

##### 1. BasicIntegrationTests.cs (9 tests)

Tests fundamental API functionality:
- Health check endpoint returns 200 OK
- Authentication with 3 roles (Admin, Deployer, Viewer)
- JWT token generation and validation
- Cluster listing and retrieval
- Authorization (401 Unauthorized, 403 Forbidden)

```bash
dotnet test --filter "FullyQualifiedName~BasicIntegrationTests"
```

##### 2. DeploymentStrategyIntegrationTests.cs (9 tests)

Tests all 4 deployment strategies based on target environment:
- **Direct Strategy** (Development) - Deploys to all nodes simultaneously
- **Rolling Strategy** (QA) - Deploys in batches with health checks
- **Blue-Green Strategy** (Staging) - Deploys to standby, then switches
- **Canary Strategy** (Production) - Gradual rollout with monitoring

Each strategy test verifies:
- Deployment completes successfully
- Correct strategy is applied
- Deployment stages execute in order
- Final status is "Succeeded"

```bash
dotnet test --filter "FullyQualifiedName~DeploymentStrategyIntegrationTests"
```

##### 3. ApprovalWorkflowIntegrationTests.cs (10 tests)

Tests approval/rejection workflows for production deployments:
- Deployment requiring approval creates pending approval request
- Approving deployment allows it to proceed and complete
- Rejecting deployment cancels it and stops execution
- Only Admin role can approve/reject (Deployer returns 403)
- Multiple deployments can be approved/rejected independently
- Deployments not requiring approval proceed immediately

```bash
dotnet test --filter "FullyQualifiedName~ApprovalWorkflowIntegrationTests"
```

##### 4. RollbackScenarioIntegrationTests.cs (10 tests)

Tests rollback functionality for failed/unwanted deployments:
- Rolling back successful deployment restores previous version
- Rolling back failed deployment works correctly
- Rollback of already-rolled-back deployment fails gracefully
- Rollback of in-progress deployment fails with BadRequest/Conflict
- Rollback of non-existent deployment returns 404 NotFound
- Only Deployer/Admin roles can rollback (Viewer returns 403)

```bash
dotnet test --filter "FullyQualifiedName~RollbackScenarioIntegrationTests"
```

##### 5. ConcurrentDeploymentIntegrationTests.cs (8 tests)

Tests system behavior under concurrent load:
- **Concurrent to different environments** - All succeed independently
- **Different modules to same environment** - All succeed with isolation
- **Respects concurrency limits** - Queues deployments when limit reached
- **Maintains isolation** - No data leakage between concurrent requests
- **Concurrent read/write** - Status queries and deployments don't conflict
- **High concurrency stress** - 20 simultaneous deployments remain stable
- **Concurrent approvals** - Multiple pending approvals handled correctly

```bash
dotnet test --filter "FullyQualifiedName~ConcurrentDeploymentIntegrationTests"
```

##### 6. MessagingIntegrationTests.cs (19 tests)

Tests messaging system (publish/consume/acknowledge):
- Message lifecycle (publish → retrieve → acknowledge → delete)
- Message creation with auto-ID generation
- Publishing to topics
- Retrieving messages by ID and by topic
- Acknowledging messages (status changes to Acknowledged)
- Deleting messages
- Message validation (required fields)
- Message priority levels
- Authorization requirements

```bash
dotnet test --filter "FullyQualifiedName~MessagingIntegrationTests"
```

##### 7. MultiTenantIntegrationTests.cs (17 tests)

Tests multi-tenant system features:
- **Tenant creation** (3 tests) - Valid data, validation, multiple tenants
- **Tenant retrieval** (3 tests) - By ID, non-existent (404), list all
- **Tenant updates** (1 test) - Update name, contact, metadata
- **Subscription management** (2 tests) - Upgrade/downgrade tiers
- **Tenant suspension** (2 tests) - Suspend and reactivate
- **Authorization** (2 tests) - Admin-only operations, require auth
- **Tenant isolation** (1 test) - Unique domains and IDs

```bash
dotnet test --filter "FullyQualifiedName~MultiTenantIntegrationTests"
```

#### CI/CD Integration

Integration tests run automatically in GitHub Actions CI/CD pipeline:

```yaml
# .github/workflows/build-and-test.yml

integration-tests:
  runs-on: ubuntu-latest
  needs: build-and-test

  steps:
  - name: Run integration tests
    run: dotnet test tests/HotSwap.Distributed.IntegrationTests/
```

**GitHub Actions Environment**:
- Ubuntu-latest runner (Docker pre-installed)
- Testcontainers pulls PostgreSQL 16 and Redis 7 images
- Tests run in isolation with fresh containers
- Test logs uploaded as artifacts for debugging

**View Integration Test Results**:
1. Go to GitHub Actions tab
2. Click on latest workflow run
3. Check "integration-tests" job
4. Download "integration-test-logs" artifact if tests fail

#### Testcontainers Architecture

Integration tests use [Testcontainers](https://dotnet.testcontainers.org/) to run real dependencies:

**PostgreSQL Container**:
- Image: postgres:16-alpine
- Database: testdb
- User: testuser
- Password: testpass
- Port: Random (to avoid conflicts)
- Lifecycle: Shared across all tests (fixture)

**Redis Container**:
- Image: redis:7-alpine
- Port: Random (to avoid conflicts)
- Lifecycle: Shared across all tests (fixture)

**Benefits**:
- Tests use real databases, not mocks
- Catches database-specific issues
- Tests connection pooling, transactions, concurrency
- Automatic cleanup after test run

#### Troubleshooting Integration Tests

##### Docker Not Running

```bash
# Error: Cannot connect to Docker daemon
# Solution: Start Docker

# macOS/Windows
# Start Docker Desktop application

# Linux
sudo systemctl start docker
```

##### Port Conflicts

Testcontainers uses random ports to avoid conflicts. If you see port-related errors:

```bash
# Check for processes using ports
lsof -i :5432  # PostgreSQL default
lsof -i :6379  # Redis default

# Kill conflicting processes or restart Docker
docker restart $(docker ps -q)
```

##### Containers Not Cleaned Up

```bash
# List running Testcontainers
docker ps --filter "label=org.testcontainers=true"

# Stop all Testcontainers
docker stop $(docker ps -q --filter "label=org.testcontainers=true")

# Remove all Testcontainers
docker rm $(docker ps -aq --filter "label=org.testcontainers=true")
```

##### Slow Test Execution

First run is slow due to image pulling:

```bash
# Pre-pull images to speed up first run
docker pull postgres:16-alpine
docker pull redis:7-alpine
```

Subsequent runs are faster (30-60 seconds for all 82 tests).

##### Out of Memory

Integration tests spawn multiple containers. Increase Docker memory:

- Docker Desktop → Settings → Resources → Memory: 4 GB minimum

#### Writing New Integration Tests

Use existing test files as templates:

```csharp
[Collection("IntegrationTests")]
public class MyIntegrationTests : IClassFixture<PostgreSqlContainerFixture>,
                                   IClassFixture<RedisContainerFixture>,
                                   IAsyncLifetime
{
    private readonly PostgreSqlContainerFixture _postgreSqlFixture;
    private readonly RedisContainerFixture _redisFixture;
    private IntegrationTestFactory? _factory;
    private HttpClient? _client;
    private AuthHelper? _authHelper;

    public async Task InitializeAsync()
    {
        _factory = new IntegrationTestFactory(_postgreSqlFixture, _redisFixture);
        await _factory.InitializeAsync();

        _client = _factory.CreateClient();
        _authHelper = new AuthHelper(_client);

        var token = await _authHelper.GetAdminTokenAsync();
        _authHelper.AddAuthorizationHeader(_client, token);
    }

    [Fact]
    public async Task MyTest_Scenario_ExpectedBehavior()
    {
        // Arrange
        var request = new CreateDeploymentRequest { /* ... */ };

        // Act
        var response = await _client!.PostAsJsonAsync("/api/v1/deployments", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}
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
