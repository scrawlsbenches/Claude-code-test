# Testing Strategy

**Version:** 1.0.0
**Target Coverage:** 85%+
**Test Count:** 350+ tests
**Framework:** xUnit, Moq, FluentAssertions

---

## Overview

The WASM orchestrator follows **Test-Driven Development (TDD)** with comprehensive test coverage across all layers. This document outlines the testing strategy, test organization, and best practices.

### Test Pyramid

```
                 ▲
                / \
               /E2E\           5% - End-to-End Tests (20 tests)
              /_____\
             /       \
            /Integration\      15% - Integration Tests (50 tests)
           /___________\
          /             \
         /  Unit Tests   \     80% - Unit Tests (280 tests)
        /_________________\
```

**Total Tests:** 350+ tests across all layers

---

## Table of Contents

1. [Unit Testing](#unit-testing)
2. [Integration Testing](#integration-testing)
3. [End-to-End Testing](#end-to-end-testing)
4. [Performance Testing](#performance-testing)
5. [Security Testing](#security-testing)
6. [Test Organization](#test-organization)
7. [TDD Workflow](#tdd-workflow)
8. [CI/CD Integration](#cicd-integration)

---

## Unit Testing

**Target:** 280+ unit tests, 85%+ code coverage

### Scope

Test individual components in isolation with mocked dependencies.

**Components to Test:**
- Domain models (validation, business logic)
- WASM runtime integration (module loading, function invocation)
- Deployment strategies (canary, blue-green, rolling, regional, A/B)
- Module storage (MinIO integration)
- Interface registry (WIT parsing, compatibility checking)
- Edge node management (health monitoring, module distribution)

### Domain Models Tests

**File:** `tests/HotSwap.Wasm.Tests/Domain/WasmModuleTests.cs`

```csharp
public class WasmModuleTests
{
    [Fact]
    public void WasmModule_WithValidData_PassesValidation()
    {
        // Arrange
        var module = new WasmModule
        {
            ModuleId = "image-processor-v1.2.0",
            Name = "image-processor",
            Version = "1.2.0",
            BinaryPath = "s3://wasm-modules/image-processor/1.2.0/module.wasm",
            Checksum = "a".PadRight(64, 'b'), // Valid SHA-256
            SizeBytes = 5242880,
            RegisteredBy = "developer@example.com"
        };

        // Act
        var isValid = module.IsValid(out var errors);

        // Assert
        isValid.Should().BeTrue();
        errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("", "image-processor", "1.2.0", "ModuleId is required")]
    [InlineData("test-v1.0.0", "", "1.0.0", "Name is required")]
    [InlineData("test-v1.0.0", "test", "", "Version is required")]
    [InlineData("test-v1.0.0", "test", "1.0", "Version must follow semantic versioning")]
    public void WasmModule_WithInvalidData_FailsValidation(
        string moduleId, string name, string version, string expectedError)
    {
        // Arrange
        var module = new WasmModule
        {
            ModuleId = moduleId,
            Name = name,
            Version = version,
            BinaryPath = "s3://test",
            Checksum = "a".PadRight(64, 'b'),
            SizeBytes = 1000,
            RegisteredBy = "user@example.com"
        };

        // Act
        var isValid = module.IsValid(out var errors);

        // Assert
        isValid.Should().BeFalse();
        errors.Should().Contain(expectedError);
    }

    [Fact]
    public void GenerateModuleId_WithNameAndVersion_ReturnsCorrectFormat()
    {
        // Act
        var moduleId = WasmModule.GenerateModuleId("image-processor", "1.2.0");

        // Assert
        moduleId.Should().Be("image-processor-v1.2.0");
    }
}
```

**Estimated Tests:** 25+ tests per domain model (×4 models = 100 tests)

---

### WASM Runtime Tests

**File:** `tests/HotSwap.Wasm.Tests/Infrastructure/WasmtimeRuntimeHostTests.cs`

```csharp
public class WasmtimeRuntimeHostTests
{
    private readonly WasmtimeRuntimeHost _runtimeHost;
    private readonly byte[] _testWasmBinary;

    public WasmtimeRuntimeHostTests()
    {
        _runtimeHost = new WasmtimeRuntimeHost();
        _testWasmBinary = LoadTestWasmBinary("test-module.wasm");
    }

    [Fact]
    public async Task LoadModuleAsync_WithValidBinary_LoadsSuccessfully()
    {
        // Act
        var result = await _runtimeHost.LoadModuleAsync(_testWasmBinary);

        // Assert
        result.Success.Should().BeTrue();
        result.Instance.Should().NotBeNull();
    }

    [Fact]
    public async Task InvokeFunctionAsync_WithValidFunction_ReturnsResult()
    {
        // Arrange
        await _runtimeHost.LoadModuleAsync(_testWasmBinary);

        // Act
        var result = await _runtimeHost.InvokeFunctionAsync(
            "add",
            new object[] { 5, 3 }
        );

        // Assert
        result.Success.Should().BeTrue();
        result.ReturnValue.Should().Be("8");
    }

    [Fact]
    public async Task InvokeFunctionAsync_WithMemoryLimit_EnforcesLimit()
    {
        // Arrange
        var limits = new ResourceLimits { MaxMemoryMB = 10 };
        _runtimeHost.SetResourceLimits(limits);
        await _runtimeHost.LoadModuleAsync(_testWasmBinary);

        // Act
        var result = await _runtimeHost.InvokeFunctionAsync(
            "allocate_large_memory",
            new object[] { 20 } // Try to allocate 20 MB
        );

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Memory limit exceeded");
    }

    [Fact]
    public async Task InvokeFunctionAsync_WithTimeout_EnforcesTimeout()
    {
        // Arrange
        var limits = new ResourceLimits { MaxExecutionTimeSeconds = 1 };
        _runtimeHost.SetResourceLimits(limits);
        await _runtimeHost.LoadModuleAsync(_testWasmBinary);

        // Act
        var result = await _runtimeHost.InvokeFunctionAsync(
            "infinite_loop",
            Array.Empty<object>()
        );

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Timeout");
    }
}
```

**Estimated Tests:** 30+ tests (loading, invocation, resource limits, errors)

---

### Deployment Strategy Tests

**File:** `tests/HotSwap.Wasm.Tests/Orchestrator/CanaryDeploymentStrategyTests.cs`

```csharp
public class CanaryDeploymentStrategyTests
{
    private readonly CanaryDeploymentStrategy _strategy;
    private readonly Mock<IModuleDistributor> _mockDistributor;
    private readonly Mock<IHealthCheckService> _mockHealthCheck;

    public CanaryDeploymentStrategyTests()
    {
        _mockDistributor = new Mock<IModuleDistributor>();
        _mockHealthCheck = new Mock<IHealthCheckService>();
        _strategy = new CanaryDeploymentStrategy(
            _mockDistributor.Object,
            _mockHealthCheck.Object
        );
    }

    [Fact]
    public async Task ExecuteAsync_WithHealthyNodes_ProgressesThroughStages()
    {
        // Arrange
        var deployment = CreateTestDeployment(stages: new[] { 10, 25, 50, 100 });
        var targetNodes = CreateTestNodes(count: 10);

        _mockHealthCheck
            .Setup(h => h.RunHealthChecksAsync(It.IsAny<List<EdgeNode>>()))
            .ReturnsAsync(HealthCheckResult.AllHealthy());

        // Act
        var result = await _strategy.ExecuteAsync(deployment, targetNodes);

        // Assert
        result.Success.Should().BeTrue();
        _mockDistributor.Verify(
            d => d.DistributeModuleAsync(It.IsAny<string>(), It.Is<List<EdgeNode>>(n => n.Count == 1)),
            Times.Once
        ); // 10% of 10 nodes = 1 node
        _mockDistributor.Verify(
            d => d.DistributeModuleAsync(It.IsAny<string>(), It.Is<List<EdgeNode>>(n => n.Count == 10)),
            Times.Once
        ); // 100% of 10 nodes = 10 nodes
    }

    [Fact]
    public async Task ExecuteAsync_WithHealthCheckFailure_RollsBack()
    {
        // Arrange
        var deployment = CreateTestDeployment(stages: new[] { 10, 25 });
        var targetNodes = CreateTestNodes(count: 10);

        _mockHealthCheck
            .SetupSequence(h => h.RunHealthChecksAsync(It.IsAny<List<EdgeNode>>()))
            .ReturnsAsync(HealthCheckResult.AllHealthy()) // Stage 1 passes
            .ReturnsAsync(HealthCheckResult.Failed("High error rate")); // Stage 2 fails

        // Act
        var result = await _strategy.ExecuteAsync(deployment, targetNodes);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Health checks failed");
        deployment.Status.Should().Be(DeploymentStatus.RolledBack);
    }
}
```

**Estimated Tests:** 15+ tests per strategy (×5 strategies = 75 tests)

---

## Integration Testing

**Target:** 50+ integration tests

### Scope

Test interactions between multiple components with real dependencies (database, MinIO, WASM runtime).

**Components to Test:**
- Module registration → MinIO storage → PostgreSQL persistence
- Deployment orchestration → Module distribution → Health checks
- Interface registry → WIT parsing → Compatibility validation
- API endpoints → Business logic → Data persistence

### Module Storage Integration Tests

**File:** `tests/HotSwap.Wasm.IntegrationTests/Infrastructure/MinIOModuleStorageTests.cs`

```csharp
public class MinIOModuleStorageTests : IAsyncLifetime
{
    private readonly MinIOModuleStorage _storage;
    private readonly IMinioClient _minio;

    public MinIOModuleStorageTests()
    {
        _minio = new MinioClient()
            .WithEndpoint("localhost:9000")
            .WithCredentials("minioadmin", "minioadmin")
            .Build();

        _storage = new MinIOModuleStorage(_minio);
    }

    [Fact]
    public async Task UploadModuleAsync_WithValidBinary_StoresInMinIO()
    {
        // Arrange
        var moduleId = "test-module-v1.0.0";
        var binary = GenerateTestWasmBinary();

        // Act
        var result = await _storage.UploadModuleAsync(moduleId, binary);

        // Assert
        result.Success.Should().BeTrue();
        result.BinaryPath.Should().Contain("wasm-modules/test-module/1.0.0");

        // Verify binary stored
        var downloaded = await _storage.DownloadModuleAsync(moduleId);
        downloaded.Should().Equal(binary);
    }

    [Fact]
    public async Task DownloadModuleAsync_WithValidChecksum_ValidatesIntegrity()
    {
        // Arrange
        var moduleId = "test-module-v1.0.0";
        var binary = GenerateTestWasmBinary();
        await _storage.UploadModuleAsync(moduleId, binary);

        // Act
        var downloaded = await _storage.DownloadModuleAsync(moduleId);

        // Assert
        var expectedChecksum = ComputeSHA256(binary);
        var actualChecksum = ComputeSHA256(downloaded);
        actualChecksum.Should().Be(expectedChecksum);
    }

    public async Task InitializeAsync()
    {
        // Create test bucket
        var bucketExists = await _minio.BucketExistsAsync(
            new BucketExistsArgs().WithBucket("wasm-modules")
        );

        if (!bucketExists)
        {
            await _minio.MakeBucketAsync(
                new MakeBucketArgs().WithBucket("wasm-modules")
            );
        }
    }

    public async Task DisposeAsync()
    {
        // Cleanup test data
        await CleanupTestBucket();
    }
}
```

**Estimated Tests:** 10+ tests (upload, download, compression, checksum)

---

## End-to-End Testing

**Target:** 20+ E2E tests

### Scope

Test complete user workflows from API request to final result.

**Workflows to Test:**
- Module registration → Deployment → Execution
- Canary deployment → Health checks → Rollback
- Blue-Green deployment → Traffic switch
- Interface registration → Compatibility validation

### E2E Deployment Tests

**File:** `tests/HotSwap.Wasm.E2ETests/DeploymentWorkflowTests.cs`

```csharp
public class DeploymentWorkflowTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly WebApplicationFactory<Program> _factory;

    public DeploymentWorkflowTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", GetTestJwtToken());
    }

    [Fact]
    public async Task CompleteDeploymentWorkflow_FromRegistrationToExecution_Succeeds()
    {
        // Step 1: Register module
        var registerResponse = await _client.PostAsJsonAsync("/api/v1/wasm/modules", new
        {
            name = "test-module",
            version = "1.0.0",
            wasmBinary = Convert.ToBase64String(LoadTestWasmBinary()),
            wasiVersion = "preview2"
        });

        registerResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var module = await registerResponse.Content.ReadFromJsonAsync<WasmModule>();

        // Step 2: Create deployment
        var deploymentResponse = await _client.PostAsJsonAsync("/api/v1/wasm/deployments", new
        {
            moduleId = module.ModuleId,
            targetRegions = new[] { "us-east" },
            strategy = "Canary"
        });

        deploymentResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var deployment = await deploymentResponse.Content.ReadFromJsonAsync<ModuleDeployment>();

        // Step 3: Execute deployment
        var executeResponse = await _client.PostAsJsonAsync(
            $"/api/v1/wasm/deployments/{deployment.DeploymentId}/execute",
            new { approvedBy = "admin@example.com" }
        );

        executeResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);

        // Step 4: Wait for deployment completion
        await WaitForDeploymentCompletionAsync(deployment.DeploymentId);

        // Step 5: Execute function on deployed module
        var invokeResponse = await _client.PostAsJsonAsync("/api/v1/wasm/execute", new
        {
            moduleId = module.ModuleId,
            functionName = "test_function",
            parameters = new { input = "test" }
        });

        invokeResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await invokeResponse.Content.ReadFromJsonAsync<InvocationResult>();
        result.Success.Should().BeTrue();
    }

    private async Task WaitForDeploymentCompletionAsync(string deploymentId, int maxWaitSeconds = 60)
    {
        var stopwatch = Stopwatch.StartNew();

        while (stopwatch.Elapsed.TotalSeconds < maxWaitSeconds)
        {
            var response = await _client.GetAsync($"/api/v1/wasm/deployments/{deploymentId}");
            var deployment = await response.Content.ReadFromJsonAsync<ModuleDeployment>();

            if (deployment.IsCompleted())
                return;

            await Task.Delay(TimeSpan.FromSeconds(2));
        }

        throw new TimeoutException("Deployment did not complete within timeout period");
    }
}
```

**Estimated Tests:** 20+ tests (full workflows)

---

## Performance Testing

### Load Testing

**Tool:** k6 or Apache JMeter

**Scenarios:**
1. **Module Registration Load** - 10 concurrent module registrations
2. **Function Invocation Load** - 1000 req/sec sustained for 5 minutes
3. **Deployment Throughput** - 10 concurrent deployments
4. **Edge Node Health Checks** - 100 nodes reporting health every 30 seconds

**Performance Targets:**

| Metric | Target | Test |
|--------|--------|------|
| Module Load Time | <10ms (p99) | Load 100 modules, measure time |
| Function Invocation | <1ms (p50) | Invoke function 10,000 times |
| Deployment Time (Canary) | <20 min | Deploy to 100 nodes with canary |
| API Response Time | <100ms (p95) | GET /modules with 1000 modules |

### Performance Test Example

```javascript
// k6 load test script
import http from 'k6/http';
import { check, sleep } from 'k6';

export let options = {
  stages: [
    { duration: '30s', target: 100 }, // Ramp up to 100 users
    { duration: '5m', target: 100 },  // Stay at 100 users
    { duration: '30s', target: 0 },   // Ramp down
  ],
  thresholds: {
    http_req_duration: ['p(95)<100'], // 95% of requests under 100ms
    http_req_failed: ['rate<0.01'],   // Error rate under 1%
  },
};

export default function () {
  const token = 'your-jwt-token';
  const params = {
    headers: {
      'Authorization': `Bearer ${token}`,
      'Content-Type': 'application/json',
    },
  };

  // Execute WASM function
  const payload = JSON.stringify({
    moduleId: 'image-processor-v1.2.0',
    functionName: 'resize',
    parameters: { width: 800, height: 600 },
  });

  const res = http.post('https://api.example.com/api/v1/wasm/execute', payload, params);

  check(res, {
    'status is 200': (r) => r.status === 200,
    'response time < 100ms': (r) => r.timings.duration < 100,
  });

  sleep(1);
}
```

---

## Security Testing

### Authentication Tests

```csharp
[Fact]
public async Task API_WithoutAuthToken_Returns401()
{
    // Arrange
    var client = _factory.CreateClient();
    client.DefaultRequestHeaders.Authorization = null;

    // Act
    var response = await client.GetAsync("/api/v1/wasm/modules");

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
}
```

### Authorization Tests

```csharp
[Fact]
public async Task DeleteModule_WithDeveloperRole_Returns403()
{
    // Arrange
    var client = _factory.CreateClient();
    client.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("Bearer", GetDeveloperJwtToken());

    // Act
    var response = await client.DeleteAsync("/api/v1/wasm/modules/test-v1.0.0");

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
}
```

### WASM Sandboxing Tests

```csharp
[Fact]
public async Task WasmModule_AccessingHostFilesystem_IsDenied()
{
    // Arrange
    var maliciousWasm = LoadMaliciousWasmBinary("filesystem-access.wasm");
    await _runtimeHost.LoadModuleAsync(maliciousWasm);

    // Act
    var result = await _runtimeHost.InvokeFunctionAsync("read_host_file", new[] { "/etc/passwd" });

    // Assert
    result.Success.Should().BeFalse();
    result.ErrorMessage.Should().Contain("Access denied");
}
```

---

## Test Organization

### Directory Structure

```
tests/
├── HotSwap.Wasm.Tests/                  # Unit tests
│   ├── Domain/
│   │   ├── WasmModuleTests.cs
│   │   ├── EdgeNodeTests.cs
│   │   └── ResourceLimitsTests.cs
│   ├── Infrastructure/
│   │   ├── WasmtimeRuntimeHostTests.cs
│   │   └── MinIOModuleStorageTests.cs
│   ├── Orchestrator/
│   │   ├── CanaryDeploymentStrategyTests.cs
│   │   ├── BlueGreenDeploymentStrategyTests.cs
│   │   └── WasmDeploymentOrchestratorTests.cs
│   └── API/
│       ├── ModulesControllerTests.cs
│       └── DeploymentsControllerTests.cs
├── HotSwap.Wasm.IntegrationTests/       # Integration tests
│   ├── Storage/
│   │   └── MinIOModuleStorageTests.cs
│   ├── Database/
│   │   └── ModuleRepositoryTests.cs
│   └── Deployment/
│       └── DeploymentWorkflowTests.cs
├── HotSwap.Wasm.E2ETests/               # End-to-end tests
│   ├── DeploymentWorkflowTests.cs
│   └── ModuleLifecycleTests.cs
└── HotSwap.Wasm.PerformanceTests/       # Performance tests
    └── k6-scripts/
        ├── function-invocation-load.js
        └── deployment-throughput.js
```

---

## TDD Workflow

### Red-Green-Refactor Cycle

1. **Red** - Write failing test first
```csharp
[Fact]
public void WasmModule_IsValid_ReturnsTrueForValidModule()
{
    // This test will fail because IsValid() doesn't exist yet
    var module = new WasmModule { /* valid data */ };
    module.IsValid(out var errors).Should().BeTrue();
}
```

2. **Green** - Write minimal code to make test pass
```csharp
public bool IsValid(out List<string> errors)
{
    errors = new List<string>();
    return true; // Simplest implementation
}
```

3. **Refactor** - Improve code while keeping tests green
```csharp
public bool IsValid(out List<string> errors)
{
    errors = new List<string>();

    if (string.IsNullOrWhiteSpace(ModuleId))
        errors.Add("ModuleId is required");

    // ... additional validation

    return errors.Count == 0;
}
```

---

## CI/CD Integration

### GitHub Actions Workflow

```yaml
name: WASM Orchestrator Tests

on: [push, pull_request]

jobs:
  test:
    runs-on: ubuntu-latest

    services:
      postgres:
        image: postgres:15
        env:
          POSTGRES_PASSWORD: password
        ports:
          - 5432:5432

      minio:
        image: minio/minio
        env:
          MINIO_ROOT_USER: minioadmin
          MINIO_ROOT_PASSWORD: minioadmin
        ports:
          - 9000:9000

    steps:
      - uses: actions/checkout@v3

      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '8.0.x'

      - name: Restore dependencies
        run: dotnet restore

      - name: Run unit tests
        run: dotnet test tests/HotSwap.Wasm.Tests --logger "console;verbosity=detailed" --collect:"XPlat Code Coverage"

      - name: Run integration tests
        run: dotnet test tests/HotSwap.Wasm.IntegrationTests --logger "console;verbosity=detailed"

      - name: Run E2E tests
        run: dotnet test tests/HotSwap.Wasm.E2ETests --logger "console;verbosity=detailed"

      - name: Upload coverage to Codecov
        uses: codecov/codecov-action@v3
        with:
          files: ./coverage.xml
```

### Test Coverage Requirements

- **Minimum Coverage:** 85%
- **Pull Request Requirement:** No decrease in coverage
- **Coverage Report:** Generated on every PR
- **Exclusions:** Auto-generated code, third-party libraries

---

**Last Updated:** 2025-11-23
**Framework:** xUnit, Moq, FluentAssertions, k6
