# Testing Strategy

**Version:** 1.0.0
**Target Coverage:** 85%+
**Test Count:** 400+ tests
**Framework:** xUnit, Moq, FluentAssertions, Docker for integration tests

---

## Overview

The serverless platform follows **Test-Driven Development (TDD)** with comprehensive test coverage across all layers. This document outlines the testing strategy, test organization, and best practices.

### Test Pyramid

```
                 ▲
                / \
               /E2E\           5% - End-to-End Tests (20 tests)
              /_____\
             /       \
            /Integration\      15% - Integration Tests (60 tests)
           /___________\
          /             \
         /  Unit Tests   \     80% - Unit Tests (320 tests)
        /_________________\
```

**Total Tests:** 400+ tests across all layers

---

## Table of Contents

1. [Unit Testing](#unit-testing)
2. [Integration Testing](#integration-testing)
3. [End-to-End Testing](#end-to-end-testing)
4. [Runtime Testing](#runtime-testing)
5. [Performance Testing](#performance-testing)
6. [Test Organization](#test-organization)
7. [TDD Workflow](#tdd-workflow)
8. [CI/CD Integration](#cicd-integration)

---

## Unit Testing

**Target:** 320+ unit tests, 85%+ code coverage

### Scope

Test individual components in isolation with mocked dependencies.

**Components to Test:**
- Domain models (validation, business logic)
- Deployment strategies (canary, blue-green, rolling)
- Code storage (MinIO integration)
- Container management (Docker SDK)
- Auto-scaling logic
- Metrics and tracing

### Domain Models Tests

**File:** `tests/HotSwap.Serverless.Tests/Domain/FunctionTests.cs`

```csharp
public class FunctionTests
{
    [Fact]
    public void Function_WithValidData_PassesValidation()
    {
        // Arrange
        var function = new Function
        {
            Name = "test-function",
            Runtime = Runtime.Node18,
            Handler = "index.handler",
            MemorySize = 512,
            Timeout = 30,
            OwnerId = "user-123"
        };

        // Act
        var isValid = function.IsValid(out var errors);

        // Assert
        isValid.Should().BeTrue();
        errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("", "index.handler", 512, 30, "Name is required")]
    [InlineData("test", "", 512, 30, "Handler is required")]
    [InlineData("test", "index.handler", 100, 30, "MemorySize must be 128-10240 MB in 64 MB increments")]
    [InlineData("test", "index.handler", 512, 0, "Timeout must be 1-900 seconds")]
    public void Function_WithInvalidData_FailsValidation(
        string name, string handler, int memorySize, int timeout, string expectedError)
    {
        // Arrange
        var function = new Function
        {
            Name = name,
            Runtime = Runtime.Node18,
            Handler = handler,
            MemorySize = memorySize,
            Timeout = timeout,
            OwnerId = "user-123"
        };

        // Act
        var isValid = function.IsValid(out var errors);

        // Assert
        isValid.Should().BeFalse();
        errors.Should().Contain(expectedError);
    }

    [Theory]
    [InlineData(128, 0.07)]
    [InlineData(512, 0.29)]
    [InlineData(1024, 0.57)]
    [InlineData(1792, 1.0)]
    [InlineData(3584, 2.0)]
    public void GetCpuAllocation_CalculatesCorrectly(int memorySize, double expectedCpu)
    {
        // Arrange
        var function = new Function
        {
            Name = "test",
            Runtime = Runtime.Node18,
            Handler = "index.handler",
            MemorySize = memorySize,
            OwnerId = "user-123"
        };

        // Act
        var cpu = function.GetCpuAllocation();

        // Assert
        cpu.Should().BeApproximately(expectedCpu, 0.01);
    }
}
```

**Estimated Tests:** 25+ tests per domain model (×5 models = 125 tests)

---

### Deployment Strategy Tests

**File:** `tests/HotSwap.Serverless.Tests/Strategies/CanaryDeploymentStrategyTests.cs`

```csharp
public class CanaryDeploymentStrategyTests
{
    private readonly Mock<IAliasRepository> _mockAliasRepo;
    private readonly Mock<IMetricsProvider> _mockMetrics;
    private readonly CanaryDeploymentStrategy _strategy;

    public CanaryDeploymentStrategyTests()
    {
        _mockAliasRepo = new Mock<IAliasRepository>();
        _mockMetrics = new Mock<IMetricsProvider>();
        _strategy = new CanaryDeploymentStrategy(_mockAliasRepo.Object, _mockMetrics.Object);
    }

    [Fact]
    public async Task DeployAsync_WithGoodMetrics_CompletesCanary()
    {
        // Arrange
        var deployment = CreateDeployment(canaryPercentage: 10, sourceVersion: 5, targetVersion: 6);
        var alias = CreateAlias("production", version: 5);
        
        _mockAliasRepo.Setup(r => r.GetAsync("test-function", "production"))
            .ReturnsAsync(alias);
        _mockMetrics.Setup(m => m.GetMetricsAsync("test-function"))
            .ReturnsAsync(new DeploymentMetrics { ErrorRate = 0.01, P99Duration = 200 });

        // Act
        var result = await _strategy.DeployAsync(deployment);

        // Assert
        result.Success.Should().BeTrue();
        _mockAliasRepo.Verify(r => r.UpdateAsync(It.IsAny<FunctionAlias>()), Times.AtLeast(3));
        alias.Version.Should().Be(6); // Fully deployed to v6
    }

    [Fact]
    public async Task DeployAsync_WithHighErrorRate_RollsBack()
    {
        // Arrange
        var deployment = CreateDeployment(
            canaryPercentage: 10,
            sourceVersion: 5,
            targetVersion: 6,
            rollbackOnErrorRate: 0.05
        );
        var alias = CreateAlias("production", version: 5);
        
        _mockAliasRepo.Setup(r => r.GetAsync("test-function", "production"))
            .ReturnsAsync(alias);
        _mockMetrics.Setup(m => m.GetMetricsAsync("test-function"))
            .ReturnsAsync(new DeploymentMetrics { ErrorRate = 0.08, P99Duration = 200 });

        // Act
        var result = await _strategy.DeployAsync(deployment);

        // Assert
        result.Success.Should().BeFalse();
        result.Status.Should().Be(DeploymentStatus.RolledBack);
        alias.Version.Should().Be(5); // Rolled back to v5
    }

    [Fact]
    public async Task DeployAsync_WithHighLatency_RollsBack()
    {
        // Arrange
        var deployment = CreateDeployment(
            canaryPercentage: 10,
            sourceVersion: 5,
            targetVersion: 6,
            rollbackOnLatencyP99: 1000
        );
        var alias = CreateAlias("production", version: 5);
        
        _mockAliasRepo.Setup(r => r.GetAsync("test-function", "production"))
            .ReturnsAsync(alias);
        _mockMetrics.Setup(m => m.GetMetricsAsync("test-function"))
            .ReturnsAsync(new DeploymentMetrics { ErrorRate = 0.01, P99Duration = 1500 });

        // Act
        var result = await _strategy.DeployAsync(deployment);

        // Assert
        result.Success.Should().BeFalse();
        result.RollbackReason.Should().Contain("latency");
        alias.Version.Should().Be(5);
    }
}
```

**Estimated Tests:** 20+ tests per strategy (×3 strategies = 60 tests)

---

### Container Management Tests

**File:** `tests/HotSwap.Serverless.Tests/Infrastructure/DockerContainerManagerTests.cs`

```csharp
public class DockerContainerManagerTests : IAsyncLifetime
{
    private readonly DockerContainerManager _manager;
    private readonly IDockerClient _dockerClient;

    public DockerContainerManagerTests()
    {
        _dockerClient = new DockerClientConfiguration(new Uri("unix:///var/run/docker.sock"))
            .CreateClient();
        _manager = new DockerContainerManager(_dockerClient);
    }

    [Fact]
    public async Task CreateContainerAsync_WithNodeRuntime_CreatesContainer()
    {
        // Arrange
        var functionName = "test-function";
        var version = 1;
        var runtime = Runtime.Node18;

        // Act
        var containerId = await _manager.CreateContainerAsync(functionName, version, runtime);

        // Assert
        containerId.Should().NotBeNullOrEmpty();
        
        // Verify container exists
        var container = await _dockerClient.Containers.InspectContainerAsync(containerId);
        container.Should().NotBeNull();
        container.Config.Image.Should().Contain("node:18");
    }

    [Fact]
    public async Task StartContainerAsync_StartsContainer()
    {
        // Arrange
        var containerId = await CreateTestContainer();

        // Act
        await _manager.StartContainerAsync(containerId);

        // Assert
        var container = await _dockerClient.Containers.InspectContainerAsync(containerId);
        container.State.Running.Should().BeTrue();
    }

    [Fact]
    public async Task GetContainerStatsAsync_ReturnsResourceUsage()
    {
        // Arrange
        var containerId = await CreateTestContainer();
        await _manager.StartContainerAsync(containerId);

        // Act
        var stats = await _manager.GetContainerStatsAsync(containerId);

        // Assert
        stats.Should().NotBeNull();
        stats.CpuUsage.Should().BeGreaterThanOrEqualTo(0);
        stats.MemoryUsage.Should().BeGreaterThan(0);
    }

    public async Task InitializeAsync()
    {
        // Pull required images before tests
        await PullImageAsync("node:18-alpine");
        await PullImageAsync("python:3.11-slim");
    }

    public Task DisposeAsync() => Task.CompletedTask;
}
```

**Estimated Tests:** 25+ tests for container management

---

## Integration Testing

**Target:** 60+ integration tests

### Scope

Test multiple components working together with real dependencies (Docker, MinIO, Redis).

**Test Scenarios:**
1. Create function → upload code → invoke (happy path)
2. Deploy canary → metrics trigger rollback
3. Cold start → warm invocation
4. HTTP trigger → function invocation
5. Scheduled trigger → periodic execution
6. Auto-scaling (scale up, scale down)

### End-to-End Function Lifecycle Test

**File:** `tests/HotSwap.Serverless.IntegrationTests/FunctionLifecycleTests.cs`

```csharp
[Collection("Integration")]
public class FunctionLifecycleTests : IClassFixture<TestServerFixture>
{
    private readonly HttpClient _client;
    private readonly TestServerFixture _fixture;

    public FunctionLifecycleTests(TestServerFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.CreateClient();
    }

    [Fact]
    public async Task CompleteLifecycle_CreateUploadDeploy Invoke_WorksEndToEnd()
    {
        // 1. Create function
        var createRequest = new
        {
            name = "integration-test-function",
            runtime = "Node18",
            handler = "index.handler",
            memorySize = 512,
            timeout = 30
        };
        var createResponse = await _client.PostAsJsonAsync("/api/v1/functions", createRequest);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // 2. Upload function code
        var codeZip = CreateTestFunctionZip(); // Returns base64-encoded ZIP
        var versionRequest = new
        {
            code = codeZip,
            description = "Initial version"
        };
        var versionResponse = await _client.PostAsJsonAsync(
            "/api/v1/functions/integration-test-function/versions",
            versionRequest
        );
        versionResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var versionResult = await versionResponse.Content.ReadFromJsonAsync<VersionResponse>();

        // 3. Create production alias
        var aliasRequest = new
        {
            aliasName = "production",
            version = versionResult.Version
        };
        await _client.PostAsJsonAsync(
            "/api/v1/functions/integration-test-function/aliases",
            aliasRequest
        );

        // 4. Invoke function (cold start)
        var invokeRequest = new
        {
            payload = "{\"message\":\"Hello World\"}",
            alias = "production"
        };
        var invokeResponse = await _client.PostAsJsonAsync(
            "/api/v1/functions/integration-test-function/invoke",
            invokeRequest
        );
        var invokeResult = await invokeResponse.Content.ReadFromJsonAsync<InvocationResult>();

        // Assert
        invokeResult.StatusCode.Should().Be(200);
        invokeResult.Body.Should().Contain("Hello World");
        invokeResult.WasColdStart.Should().BeTrue();

        // 5. Invoke again (warm start)
        var warmInvokeResponse = await _client.PostAsJsonAsync(
            "/api/v1/functions/integration-test-function/invoke",
            invokeRequest
        );
        var warmInvokeResult = await warmInvokeResponse.Content.ReadFromJsonAsync<InvocationResult>();
        
        warmInvokeResult.WasColdStart.Should().BeFalse();
        warmInvokeResult.ExecutionTime.Should().BeLessThan(invokeResult.ExecutionTime);
    }

    private string CreateTestFunctionZip()
    {
        // Create a simple Node.js function ZIP
        var code = @"
exports.handler = async (event) => {
    return {
        statusCode: 200,
        body: JSON.stringify({ message: event.message })
    };
};";
        
        using var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
        {
            var entry = archive.CreateEntry("index.js");
            using var entryStream = entry.Open();
            using var writer = new StreamWriter(entryStream);
            writer.Write(code);
        }
        
        return Convert.ToBase64String(memoryStream.ToArray());
    }
}
```

**Estimated Tests:** 40+ integration tests

---

## End-to-End Testing

**Target:** 20+ E2E tests

### Scope

Test complete user workflows from API to function execution with all real components.

**E2E Test Scenarios:**
1. Complete canary deployment with automatic rollback
2. Blue-green deployment with manual switch
3. HTTP trigger end-to-end (API Gateway → Function)
4. Scheduled trigger end-to-end (Cron → Function)
5. Auto-scaling verification (load → scale up → scale down)

### E2E Canary Deployment Test

```csharp
[Collection("E2E")]
public class CanaryDeploymentE2ETests : IClassFixture<E2ETestFixture>
{
    [Fact]
    public async Task CanaryDeployment_WithMetricsRollback_WorksEndToEnd()
    {
        // 1. Set up function with v1
        var functionName = await SetupFunctionWithVersion(version: 1);
        
        // 2. Upload v2 with intentional bug (high error rate)
        await UploadBuggyVersion(functionName, version: 2);
        
        // 3. Start canary deployment (10% → 50% → 100%)
        var deployment = await StartCanaryDeployment(functionName, targetVersion: 2);
        
        // 4. Wait for first canary phase (10%)
        await Task.Delay(TimeSpan.FromMinutes(1));
        
        // 5. Verify 10% traffic to v2
        var traffic = await GetTrafficDistribution(functionName);
        traffic[1].Should().BeApproximately(90, 5);
        traffic[2].Should().BeApproximately(10, 5);
        
        // 6. Invoke multiple times to trigger high error rate
        for (int i = 0; i < 100; i++)
        {
            await InvokeFunction(functionName);
        }
        
        // 7. Wait for metrics collection and rollback decision
        await Task.Delay(TimeSpan.FromMinutes(5));
        
        // 8. Verify automatic rollback to v1
        var deploymentStatus = await GetDeploymentStatus(deployment.DeploymentId);
        deploymentStatus.Status.Should().Be("RolledBack");
        deploymentStatus.RollbackReason.Should().Contain("error rate");
        
        // 9. Verify 100% traffic back to v1
        traffic = await GetTrafficDistribution(functionName);
        traffic[1].Should().Be(100);
        traffic.ContainsKey(2).Should().BeFalse();
    }
}
```

---

## Runtime Testing

**Target:** Test all runtime wrappers

### Runtime Test Matrix

| Runtime | Cold Start | Warm | Error Handling | Memory | Timeout |
|---------|-----------|------|----------------|--------|---------|
| Node 16 | ✓ | ✓ | ✓ | ✓ | ✓ |
| Node 18 | ✓ | ✓ | ✓ | ✓ | ✓ |
| Node 20 | ✓ | ✓ | ✓ | ✓ | ✓ |
| Python 3.8 | ✓ | ✓ | ✓ | ✓ | ✓ |
| Python 3.9 | ✓ | ✓ | ✓ | ✓ | ✓ |
| Python 3.10 | ✓ | ✓ | ✓ | ✓ | ✓ |
| Python 3.11 | ✓ | ✓ | ✓ | ✓ | ✓ |
| .NET 6 | ✓ | ✓ | ✓ | ✓ | ✓ |
| .NET 7 | ✓ | ✓ | ✓ | ✓ | ✓ |
| .NET 8 | ✓ | ✓ | ✓ | ✓ | ✓ |

### Runtime Test Example

```csharp
[Theory]
[InlineData(Runtime.Node16)]
[InlineData(Runtime.Node18)]
[InlineData(Runtime.Node20)]
[InlineData(Runtime.Python38)]
[InlineData(Runtime.Python39)]
[InlineData(Runtime.Python310)]
[InlineData(Runtime.Python311)]
public async Task Runtime_ColdStart_CompletesWithinTarget(Runtime runtime)
{
    // Arrange
    var function = await CreateTestFunction(runtime);
    var payload = "{\"test\":\"data\"}";

    // Act
    var stopwatch = Stopwatch.StartNew();
    var result = await InvokeFunction(function.Name, payload);
    stopwatch.Stop();

    // Assert
    result.Success.Should().BeTrue();
    result.WasColdStart.Should().BeTrue();
    
    var targetColdStart = runtime.GetEstimatedColdStartMs();
    stopwatch.ElapsedMilliseconds.Should().BeLessThan(targetColdStart + 100); // +100ms buffer
}
```

---

## Performance Testing

**Target:** Meet performance SLAs

### Performance Test Scenarios

**Scenario 1: Throughput Test**

```csharp
[Fact]
public async Task Throughput_1000InvocationsPerSecond_Achieved()
{
    // Arrange
    var functionName = await SetupFunction();
    var invocationCount = 10_000;
    var targetDuration = TimeSpan.FromSeconds(10); // 1K invocations/sec

    // Act
    var stopwatch = Stopwatch.StartNew();
    var tasks = Enumerable.Range(0, invocationCount)
        .Select(_ => InvokeFunction(functionName))
        .ToList();
    await Task.WhenAll(tasks);
    stopwatch.Stop();

    // Assert
    stopwatch.Elapsed.Should().BeLessThan(targetDuration);
    var throughput = invocationCount / stopwatch.Elapsed.TotalSeconds;
    throughput.Should().BeGreaterThan(1000);
}
```

**Scenario 2: Cold Start Latency**

```csharp
[Theory]
[InlineData(Runtime.Node18, 200)]
[InlineData(Runtime.Python311, 250)]
[InlineData(Runtime.Dotnet8, 400)]
public async Task ColdStart_MeetsLatencyTarget(Runtime runtime, int targetMs)
{
    // Arrange
    var function = await CreateTestFunction(runtime);
    
    // Act
    var stopwatch = Stopwatch.StartNew();
    var result = await InvokeFunction(function.Name);
    stopwatch.Stop();

    // Assert
    result.WasColdStart.Should().BeTrue();
    stopwatch.ElapsedMilliseconds.Should().BeLessThan(targetMs);
}
```

---

## Test Organization

### Project Structure

```
tests/
├── HotSwap.Serverless.Tests/              # Unit tests
│   ├── Domain/
│   │   ├── FunctionTests.cs               # 25 tests
│   │   ├── FunctionVersionTests.cs        # 20 tests
│   │   ├── FunctionAliasTests.cs          # 20 tests
│   │   ├── DeploymentTests.cs             # 20 tests
│   │   └── RunnerNodeTests.cs             # 15 tests
│   ├── Strategies/
│   │   ├── CanaryDeploymentStrategyTests.cs    # 20 tests
│   │   ├── BlueGreenDeploymentStrategyTests.cs # 15 tests
│   │   └── RollingDeploymentStrategyTests.cs   # 15 tests
│   ├── Infrastructure/
│   │   ├── MinIOCodeStorageTests.cs       # 20 tests
│   │   ├── DockerContainerManagerTests.cs # 25 tests
│   │   └── CodeLoaderTests.cs             # 15 tests
│   └── Orchestrator/
│       ├── InvocationManagerTests.cs      # 30 tests
│       └── AutoScalerTests.cs             # 20 tests
├── HotSwap.Serverless.IntegrationTests/   # Integration tests
│   ├── FunctionLifecycleTests.cs          # 15 tests
│   ├── DeploymentStrategyTests.cs         # 15 tests
│   ├── RuntimeTests.cs                    # 20 tests
│   └── TriggerTests.cs                    # 10 tests
└── HotSwap.Serverless.E2ETests/           # End-to-end tests
    ├── CanaryDeploymentE2ETests.cs        # 5 tests
    ├── HttpTriggerE2ETests.cs             # 5 tests
    └── AutoScalingE2ETests.cs             # 5 tests
```

---

## TDD Workflow

Follow Red-Green-Refactor cycle for ALL code changes.

### Example: Implementing Canary Deployment

**Step 1: 🔴 RED - Write Failing Test**

```csharp
[Fact]
public async Task DeployAsync_WithGoodMetrics_CompletesCanary()
{
    // Arrange
    var deployment = CreateDeployment(canaryPercentage: 10);
    var alias = CreateAlias("production", version: 5);
    
    // Act
    var result = await _strategy.DeployAsync(deployment);

    // Assert
    result.Success.Should().BeTrue();
    alias.Version.Should().Be(6);
}
```

Run test: **FAILS** ❌ (CanaryDeploymentStrategy doesn't exist)

**Step 2: 🟢 GREEN - Minimal Implementation**

```csharp
public class CanaryDeploymentStrategy : IDeploymentStrategy
{
    public async Task<DeploymentResult> DeployAsync(Deployment deployment)
    {
        // Minimal implementation to pass test
        var alias = await _aliasRepo.GetAsync(deployment.FunctionName, "production");
        alias.UpdateVersion(deployment.TargetVersion);
        await _aliasRepo.UpdateAsync(alias);
        return DeploymentResult.Success();
    }
}
```

Run test: **PASSES** ✓

**Step 3: 🔵 REFACTOR - Improve Implementation**

```csharp
public class CanaryDeploymentStrategy : IDeploymentStrategy
{
    public async Task<DeploymentResult> DeployAsync(Deployment deployment)
    {
        var config = deployment.Config;
        var alias = await _aliasRepo.GetAsync(deployment.FunctionName, "production");
        
        // Gradual rollout: 10% → 50% → 100%
        foreach (var percentage in config.CanaryIncrements)
        {
            alias.SetWeightedRouting(
                deployment.SourceVersion.Value,
                deployment.TargetVersion,
                percentage
            );
            await _aliasRepo.UpdateAsync(alias);
            await Task.Delay(config.CanaryDuration.Value);
            
            // Check metrics
            if (await ShouldRollback(deployment))
            {
                alias.UpdateVersion(deployment.SourceVersion.Value);
                await _aliasRepo.UpdateAsync(alias);
                return DeploymentResult.RolledBack("Metrics threshold breached");
            }
        }
        
        return DeploymentResult.Success();
    }
}
```

Run test: **PASSES** ✓

---

## CI/CD Integration

### GitHub Actions Workflow

```yaml
name: Serverless Platform Tests

on:
  push:
    branches: [main, claude/*]
  pull_request:
    branches: [main]

jobs:
  test:
    runs-on: ubuntu-latest

    services:
      redis:
        image: redis:7
        ports:
          - 6379:6379

      minio:
        image: minio/minio
        ports:
          - 9000:9000
        env:
          MINIO_ROOT_USER: minioadmin
          MINIO_ROOT_PASSWORD: minioadmin
        options: --health-cmd "curl -f http://localhost:9000/minio/health/live"

    steps:
      - uses: actions/checkout@v3

      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '8.0.x'

      - name: Restore dependencies
        run: dotnet restore

      - name: Build
        run: dotnet build --no-restore

      - name: Run unit tests
        run: dotnet test tests/HotSwap.Serverless.Tests/ --no-build --verbosity normal

      - name: Run integration tests
        run: dotnet test tests/HotSwap.Serverless.IntegrationTests/ --no-build --verbosity normal

      - name: Run E2E tests
        run: dotnet test tests/HotSwap.Serverless.E2ETests/ --no-build --verbosity normal

      - name: Upload coverage
        uses: codecov/codecov-action@v3
```

---

## Test Coverage Requirements

**Minimum Coverage:** 85%

**Coverage by Layer:**
- Domain: 95%+ (simple models, high coverage easy)
- Infrastructure: 80%+ (external dependencies)
- Orchestrator: 85%+ (core business logic)
- API: 80%+ (mostly integration tests)

**Measure Coverage:**
```bash
dotnet test --collect:"XPlat Code Coverage"
```

---

**Last Updated:** 2025-11-23
**Test Count:** 400+ tests
**Coverage Target:** 85%+
