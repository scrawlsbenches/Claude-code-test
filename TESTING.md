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

Integration tests verify complete workflows using in-memory alternatives (SQLite, MemoryDistributedCache, DeterministicMetricsProvider). These tests validate end-to-end scenarios with an in-memory API server.

**Key Features:**
- **No Docker Dependencies**: Uses SQLite in-memory instead of PostgreSQL, MemoryDistributedCache for caching
- **Deterministic Metrics**: Uses `DeterministicMetricsProvider` for consistent, predictable test behavior
- **Environment Parity**: Tests behave identically in local development and CI/CD environments
- **Zero Flakiness**: All tests produce consistent results across all platforms

#### Test Structure

```
tests/HotSwap.Distributed.IntegrationTests/
├── Fixtures/
│   ├── SharedIntegrationTestFixture.cs    # Shared test fixture (collection-level)
│   ├── IntegrationTestFactory.cs          # WebApplicationFactory with in-memory deps
│   ├── IntegrationTestCollection.cs       # xUnit collection definition
│   └── InMemoryDistributedLock.cs         # In-memory distributed lock implementation
├── Helpers/
│   ├── AuthHelper.cs                      # JWT token management
│   ├── ApiClientHelper.cs                 # API operation helpers
│   ├── TestDataBuilder.cs                 # Test data creation
│   └── DeterministicMetricsProvider.cs    # Deterministic metrics for canary tests
└── Tests/
    ├── BasicIntegrationTests.cs                    # Health, auth, clusters
    ├── DeploymentStrategyIntegrationTests.cs       # All 4 deployment strategies (Direct, Rolling, BlueGreen, Canary)
    ├── ApprovalWorkflowIntegrationTests.cs         # Approve/reject workflows
    ├── RollbackScenarioIntegrationTests.cs         # Rollback scenarios
    ├── ConcurrentDeploymentIntegrationTests.cs     # Concurrency & stress testing
    ├── MessagingIntegrationTests.cs                # Message queue system
    └── MultiTenantIntegrationTests.cs              # Multi-tenant features
```

**Total**: 69 integration tests across 7 test files (all passing, 0 flaky)

#### Prerequisites

Integration tests use in-memory alternatives and require **no external dependencies**:
- ✅ No Docker required
- ✅ No PostgreSQL required
- ✅ Only .NET 8 SDK required

#### Running Integration Tests Locally

**Important**: Integration tests run entirely in-memory and work on any platform with .NET 8 SDK.

```bash
# Run all integration tests (no Docker required)
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
Passed!  - Failed:     0, Passed:    69, Skipped:     0, Total:    69, Duration: ~15 s
```

**Note**: Integration tests run entirely in-memory with deterministic behavior - no external dependencies required.

#### Integration Test Details

##### 1. BasicIntegrationTests.cs

Tests fundamental API functionality:
- Health check endpoint returns 200 OK
- Authentication with 3 roles (Admin, Deployer, Viewer)
- JWT token generation and validation
- Cluster listing and retrieval
- Authorization (401 Unauthorized, 403 Forbidden)

```bash
dotnet test --filter "FullyQualifiedName~BasicIntegrationTests"
```

##### 2. DeploymentStrategyIntegrationTests.cs

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

##### 3. ApprovalWorkflowIntegrationTests.cs

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

##### 4. RollbackScenarioIntegrationTests.cs

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

##### 5. ConcurrentDeploymentIntegrationTests.cs

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

##### 6. MessagingIntegrationTests.cs

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

##### 7. MultiTenantIntegrationTests.cs

Tests multi-tenant system features:
- **Tenant creation** - Valid data, validation, multiple tenants
- **Tenant retrieval** - By ID, non-existent (404), list all
- **Tenant updates** - Update name, contact, metadata
- **Subscription management** - Upgrade/downgrade tiers
- **Tenant suspension** - Suspend and reactivate
- **Authorization** - Admin-only operations, require auth
- **Tenant isolation** - Unique domains and IDs

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
- Testcontainers pulls PostgreSQL 16 images
- Tests run in isolation with fresh containers
- Test logs uploaded as artifacts for debugging

**View Integration Test Results**:
1. Go to GitHub Actions tab
2. Click on latest workflow run
3. Check "integration-tests" job
4. Download "integration-test-logs" artifact if tests fail

#### In-Memory Test Architecture

Integration tests use in-memory alternatives for fast, deterministic testing:

**SQLite In-Memory Database**:
- Connection: `:memory:`
- Purpose: Audit log storage (replaces PostgreSQL)
- Lifecycle: Shared across all tests (fixture)
- Benefits: Zero configuration, instant startup, automatic cleanup

**MemoryDistributedCache**:
- Purpose: Distributed locking and caching
- Lifecycle: Shared across all tests
- Benefits: No external service, fast, deterministic

**DeterministicMetricsProvider**:
- Purpose: Canary deployment metrics simulation (replaces InMemoryMetricsProvider)
- Baseline: CPU 45%, Memory 60%, Latency 120ms, Error rate 0.5%
- Benefits: Consistent results across all environments, zero flakiness
- Configurable: Can inject unhealthy metrics for failure scenario testing

**Benefits**:
- ✅ No Docker or external dependencies required
- ✅ Fast test execution (~15 seconds for all 69 tests)
- ✅ Deterministic behavior - tests pass consistently
- ✅ Works on any platform with .NET 8 SDK
- ✅ Simpler CI/CD pipeline
- ✅ Easier local development

#### Troubleshooting Integration Tests

##### .NET SDK Not Found

```bash
# Error: dotnet: command not found
# Solution: Install .NET 8 SDK

# Verify installation
dotnet --version
# Expected: 8.0.x or later
```

##### SQLite Errors

```bash
# Error: SQLite database errors
# Solution: Ensure SQLite connection stays open during tests

# The test fixture manages the connection lifecycle
# Connection is opened in constructor and closed in DisposeAsync
```

##### Test Timeout

```bash
# Error: Tests timeout after 2 minutes
# Solution: Check for deadlocks or infinite loops

# Run with detailed logging
dotnet test --verbosity normal --logger "console;verbosity=detailed"
```

##### Flaky Tests

Integration tests should **never** be flaky due to deterministic behavior:
- ✅ DeterministicMetricsProvider ensures consistent metrics
- ✅ In-memory database has no external timing dependencies
- ✅ No network calls to external services

If you see flaky tests, it indicates a bug in the test or application code, not environment issues.

#### Writing New Integration Tests

Use existing test files as templates:

```csharp
[Collection("IntegrationTests")]
public class MyIntegrationTests : IAsyncLifetime
{
    private readonly SharedIntegrationTestFixture _fixture;
    private HttpClient? _client;
    private AuthHelper? _authHelper;
    private ApiClientHelper? _apiHelper;

    public MyIntegrationTests(SharedIntegrationTestFixture fixture)
    {
        _fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
    }

    public async Task InitializeAsync()
    {
        // Factory is already initialized by collection fixture
        _client = _fixture.Factory.CreateClient();
        _authHelper = new AuthHelper(_client);
        _apiHelper = new ApiClientHelper(_client);

        // Authenticate with required role
        var token = await _authHelper.GetAdminTokenAsync();
        _authHelper.AddAuthorizationHeader(_client, token);
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        // Factory is disposed by collection fixture, not here
        await Task.CompletedTask;
    }

    [Fact]
    public async Task MyTest_Scenario_ExpectedBehavior()
    {
        // Arrange
        var request = new CreateDeploymentRequest { /* ... */ };

        // Act
        var response = await _apiHelper!.CreateDeploymentAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.Status.Should().Be("Running");
    }
}
```

**Key Points:**
- Use `SharedIntegrationTestFixture` for all tests (injected via collection fixture)
- Factory is shared across all tests for performance
- Use `AuthHelper` and `ApiClientHelper` for common operations
- No Docker or external dependencies needed

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
