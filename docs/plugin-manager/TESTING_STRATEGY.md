# Testing Strategy

**Version:** 1.0.0
**Target Coverage:** 85%+
**Test Count:** 350+ tests
**Framework:** xUnit, Moq, FluentAssertions

---

## Overview

The plugin manager follows **Test-Driven Development (TDD)** with comprehensive test coverage across all layers. This document outlines the testing strategy, test organization, and best practices.

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
- Deployment strategies (plugin deployment algorithms)
- Plugin loader (assembly loading/unloading)
- Storage layer (MinIO, PostgreSQL interactions)
- Health monitoring (health check logic)

### Domain Models Tests

**File:** `tests/HotSwap.Distributed.Tests/Domain/PluginTests.cs`

```csharp
public class PluginTests
{
    [Fact]
    public void Plugin_WithValidData_PassesValidation()
    {
        // Arrange
        var plugin = new Plugin
        {
            PluginId = Guid.NewGuid().ToString(),
            Name = "payment-processor-stripe",
            DisplayName = "Stripe Payment Processor",
            Type = PluginType.PaymentGateway
        };

        // Act
        var isValid = plugin.IsValid(out var errors);

        // Assert
        isValid.Should().BeTrue();
        errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("", "Name is required")]
    [InlineData("Payment_Processor", "Name must contain only lowercase alphanumeric characters and dashes")]
    [InlineData("PAYMENT-PROCESSOR", "Name must contain only lowercase alphanumeric characters and dashes")]
    public void Plugin_WithInvalidName_FailsValidation(string name, string expectedError)
    {
        // Arrange
        var plugin = new Plugin
        {
            PluginId = Guid.NewGuid().ToString(),
            Name = name,
            DisplayName = "Test Plugin",
            Type = PluginType.PaymentGateway
        };

        // Act
        var isValid = plugin.IsValid(out var errors);

        // Assert
        isValid.Should().BeFalse();
        errors.Should().Contain(expectedError);
    }

    [Fact]
    public void IsDeprecated_WhenStatusDeprecated_ReturnsTrue()
    {
        // Arrange
        var plugin = new Plugin
        {
            PluginId = "plugin-1",
            Name = "test-plugin",
            DisplayName = "Test Plugin",
            Type = PluginType.Other,
            Status = PluginStatus.Deprecated
        };

        // Act
        var isDeprecated = plugin.IsDeprecated();

        // Assert
        isDeprecated.Should().BeTrue();
    }
}
```

**Estimated Tests:** 25+ tests per domain model (×4 models = 100 tests)

---

### Deployment Strategy Tests

**File:** `tests/HotSwap.Distributed.Tests/Deployment/CanaryDeploymentStrategyTests.cs`

```csharp
public class CanaryDeploymentStrategyTests
{
    private readonly Mock<IPluginLoader> _mockLoader;
    private readonly Mock<IHealthMonitor> _mockHealthMonitor;
    private readonly Mock<IMetricsCollector> _mockMetrics;
    private readonly CanaryDeploymentStrategy _strategy;

    public CanaryDeploymentStrategyTests()
    {
        _mockLoader = new Mock<IPluginLoader>();
        _mockHealthMonitor = new Mock<IHealthMonitor>();
        _mockMetrics = new Mock<IMetricsCollector>();
        _strategy = new CanaryDeploymentStrategy(_mockLoader.Object, _mockHealthMonitor.Object, _mockMetrics.Object);
    }

    [Fact]
    public async Task DeployAsync_WithHealthyMetrics_ProgressesToCompletion()
    {
        // Arrange
        var plugin = CreateTestPlugin();
        var config = new CanaryConfig
        {
            InitialPercentage = 10,
            IncrementPercentage = 30,
            EvaluationPeriod = TimeSpan.FromSeconds(1),
            AutoRollback = true
        };

        _mockHealthMonitor.Setup(h => h.CheckHealthAsync(It.IsAny<List<string>>()))
            .ReturnsAsync(new HealthResult { IsHealthy = true });

        _mockMetrics.Setup(m => m.CollectMetricsAsync(It.IsAny<List<string>>()))
            .ReturnsAsync(new PluginMetrics { ErrorRate = 0.001, LatencyP99Ms = 100 });

        // Act
        var result = await _strategy.DeployAsync(plugin, config);

        // Assert
        result.Success.Should().BeTrue();
        result.AffectedTenants.Should().BeGreaterThan(0);
        _mockLoader.Verify(l => l.LoadPluginAsync(It.IsAny<string>(), It.IsAny<string>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task DeployAsync_WithHighErrorRate_TriggersRollback()
    {
        // Arrange
        var plugin = CreateTestPlugin();
        var config = new CanaryConfig
        {
            InitialPercentage = 10,
            IncrementPercentage = 30,
            EvaluationPeriod = TimeSpan.FromSeconds(1),
            AutoRollback = true,
            Thresholds = new CanaryThresholds { MaxErrorRate = 0.05 }
        };

        _mockHealthMonitor.Setup(h => h.CheckHealthAsync(It.IsAny<List<string>>()))
            .ReturnsAsync(new HealthResult { IsHealthy = true });

        _mockMetrics.Setup(m => m.CollectMetricsAsync(It.IsAny<List<string>>()))
            .ReturnsAsync(new PluginMetrics { ErrorRate = 0.10, LatencyP99Ms = 100 }); // High error rate

        // Act
        var result = await _strategy.DeployAsync(plugin, config);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Canary failed");
    }

    [Fact]
    public async Task DeployAsync_ProgressPercentageUpdatesCorrectly()
    {
        // Arrange
        var plugin = CreateTestPlugin();
        var config = new CanaryConfig
        {
            InitialPercentage = 10,
            IncrementPercentage = 45, // 10% → 55% → 100%
            EvaluationPeriod = TimeSpan.FromMilliseconds(100)
        };

        var progressUpdates = new List<int>();
        _strategy.OnProgressUpdate += percentage => progressUpdates.Add(percentage);

        _mockHealthMonitor.Setup(h => h.CheckHealthAsync(It.IsAny<List<string>>()))
            .ReturnsAsync(new HealthResult { IsHealthy = true });

        _mockMetrics.Setup(m => m.CollectMetricsAsync(It.IsAny<List<string>>()))
            .ReturnsAsync(new PluginMetrics { ErrorRate = 0.001, LatencyP99Ms = 100 });

        // Act
        await _strategy.DeployAsync(plugin, config);

        // Assert
        progressUpdates.Should().ContainInOrder(10, 55, 100);
    }
}
```

**Estimated Tests:** 20+ tests per strategy (×5 strategies = 100 tests)

---

### Plugin Loader Tests

**File:** `tests/HotSwap.Distributed.Tests/Infrastructure/AssemblyPluginLoaderTests.cs`

```csharp
public class AssemblyPluginLoaderTests : IAsyncLifetime
{
    private readonly AssemblyPluginLoader _loader;
    private readonly string _testPluginPath;

    public AssemblyPluginLoaderTests()
    {
        _loader = new AssemblyPluginLoader();
        _testPluginPath = Path.Combine(Path.GetTempPath(), "test-plugin.dll");
    }

    [Fact]
    public async Task LoadPluginAsync_WithValidPlugin_LoadsSuccessfully()
    {
        // Arrange
        await CreateTestPluginAssemblyAsync(_testPluginPath);

        // Act
        var result = await _loader.LoadPluginAsync("test-plugin", _testPluginPath);

        // Assert
        result.Success.Should().BeTrue();
        result.LoadedAssembly.Should().NotBeNull();
    }

    [Fact]
    public async Task UnloadPluginAsync_AfterLoad_UnloadsSuccessfully()
    {
        // Arrange
        await CreateTestPluginAssemblyAsync(_testPluginPath);
        await _loader.LoadPluginAsync("test-plugin", _testPluginPath);

        // Act
        var result = await _loader.UnloadPluginAsync("test-plugin");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task LoadPluginAsync_WithMissingFile_ReturnsFailure()
    {
        // Arrange
        var nonExistentPath = "/path/to/nonexistent/plugin.dll";

        // Act
        var result = await _loader.LoadPluginAsync("missing-plugin", nonExistentPath);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not found");
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (File.Exists(_testPluginPath))
            File.Delete(_testPluginPath);
    }
}
```

**Estimated Tests:** 20+ tests for plugin loader

---

### Health Monitor Tests

**File:** `tests/HotSwap.Distributed.Tests/Orchestrator/PluginHealthMonitorTests.cs`

```csharp
public class PluginHealthMonitorTests
{
    private readonly Mock<IPluginRegistry> _mockRegistry;
    private readonly Mock<IPluginLoader> _mockLoader;
    private readonly PluginHealthMonitor _monitor;

    public PluginHealthMonitorTests()
    {
        _mockRegistry = new Mock<IPluginRegistry>();
        _mockLoader = new Mock<IPluginLoader>();
        _monitor = new PluginHealthMonitor(_mockRegistry.Object, _mockLoader.Object);
    }

    [Fact]
    public async Task CheckHealthAsync_WithHealthyPlugin_ReturnsHealthy()
    {
        // Arrange
        var plugin = CreateTestPlugin();
        _mockLoader.Setup(l => l.ExecuteHealthCheckAsync(plugin.PluginId))
            .ReturnsAsync(new HealthCheckResult { Status = HealthStatus.Healthy });

        // Act
        var result = await _monitor.CheckHealthAsync(plugin.PluginId);

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
        result.CheckedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task CheckHealthAsync_WithUnhealthyPlugin_TriggersRollbackAfter3Failures()
    {
        // Arrange
        var plugin = CreateTestPlugin();
        var rollbackTriggered = false;

        _monitor.OnRollbackTriggered += (pluginId) => rollbackTriggered = true;

        _mockLoader.Setup(l => l.ExecuteHealthCheckAsync(plugin.PluginId))
            .ReturnsAsync(new HealthCheckResult { Status = HealthStatus.Unhealthy });

        // Act - 3 consecutive failures
        await _monitor.CheckHealthAsync(plugin.PluginId);
        await _monitor.CheckHealthAsync(plugin.PluginId);
        await _monitor.CheckHealthAsync(plugin.PluginId);

        // Assert
        rollbackTriggered.Should().BeTrue();
    }
}
```

**Estimated Tests:** 15+ tests for health monitoring

---

## Integration Testing

**Target:** 50+ integration tests

### Scope

Test multiple components working together with real dependencies (MinIO, PostgreSQL).

**Test Scenarios:**
1. Register plugin → Upload binary → Deploy (happy path)
2. Register plugin → Deploy → Health check failure → Rollback
3. Deploy canary → Monitor metrics → Progressive rollout
4. Tenant enable plugin → Execute plugin → Verify isolation
5. Sandbox create → Execute plugin → Cleanup

### End-to-End Plugin Deployment Test

**File:** `tests/HotSwap.Distributed.IntegrationTests/PluginDeploymentTests.cs`

```csharp
[Collection("Integration")]
public class PluginDeploymentTests : IClassFixture<TestServerFixture>
{
    private readonly HttpClient _client;
    private readonly TestServerFixture _fixture;

    public PluginDeploymentTests(TestServerFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.CreateClient();
    }

    [Fact]
    public async Task EndToEndFlow_RegisterUploadDeploy_WorksCorrectly()
    {
        // Arrange - Register plugin
        var registerRequest = new
        {
            name = "test-payment-processor",
            displayName = "Test Payment Processor",
            type = "PaymentGateway"
        };
        var registerResponse = await _client.PostAsJsonAsync("/api/v1/plugins", registerRequest);
        var plugin = await registerResponse.Content.ReadFromJsonAsync<PluginResponse>();

        // Arrange - Upload plugin binary
        var uploadContent = new MultipartFormDataContent();
        uploadContent.Add(new StringContent("1.0.0"), "version");
        uploadContent.Add(new StringContent("Test release"), "releaseNotes");
        uploadContent.Add(new ByteArrayContent(CreateTestPluginBinary()), "binary", "plugin.zip");

        var uploadResponse = await _client.PostAsync($"/api/v1/plugins/{plugin.PluginId}/versions", uploadContent);
        var version = await uploadResponse.Content.ReadFromJsonAsync<PluginVersionResponse>();

        // Act - Deploy plugin to Development
        var deployRequest = new
        {
            pluginId = plugin.PluginId,
            version = "1.0.0",
            environment = "Development",
            strategy = "Direct"
        };
        var deployResponse = await _client.PostAsJsonAsync("/api/v1/deployments", deployRequest);
        var deployment = await deployResponse.Content.ReadFromJsonAsync<DeploymentResponse>();

        // Wait for deployment to complete
        await WaitForDeploymentCompletionAsync(deployment.DeploymentId);

        // Assert - Deployment successful
        var statusResponse = await _client.GetAsync($"/api/v1/deployments/{deployment.DeploymentId}");
        var status = await statusResponse.Content.ReadFromJsonAsync<DeploymentResponse>();

        status.Status.Should().Be("Completed");
        status.ProgressPercentage.Should().Be(100);
    }

    [Fact]
    public async Task TenantPluginExecution_WithIsolation_WorksCorrectly()
    {
        // Arrange - Register and deploy plugin
        var plugin = await RegisterAndDeployTestPluginAsync();

        // Arrange - Enable plugin for Tenant A
        var tenantAConfig = new
        {
            configuration = new { apiKey = "tenant-a-key" }
        };
        await _client.PostAsJsonAsync($"/api/v1/tenants/tenant-a/plugins/{plugin.PluginId}/enable", tenantAConfig);

        // Arrange - Enable plugin for Tenant B
        var tenantBConfig = new
        {
            configuration = new { apiKey = "tenant-b-key" }
        };
        await _client.PostAsJsonAsync($"/api/v1/tenants/tenant-b/plugins/{plugin.PluginId}/enable", tenantBConfig);

        // Act - Execute plugin for both tenants
        var tenantAResult = await ExecutePluginForTenantAsync(plugin.PluginId, "tenant-a");
        var tenantBResult = await ExecutePluginForTenantAsync(plugin.PluginId, "tenant-b");

        // Assert - Both executions successful and isolated
        tenantAResult.Success.Should().BeTrue();
        tenantBResult.Success.Should().BeTrue();
        tenantAResult.Config["apiKey"].Should().Be("tenant-a-key");
        tenantBResult.Config["apiKey"].Should().Be("tenant-b-key");
    }
}
```

**Estimated Tests:** 35+ integration tests

---

## End-to-End Testing

**Target:** 20+ E2E tests

### Scope

Test complete user workflows from API to database with all real components.

**E2E Test Scenarios:**
1. Complete plugin lifecycle (register → upload → sandbox test → deploy → rollback)
2. Canary deployment with health monitoring (progressive rollout)
3. Multi-tenant plugin configuration (enable, configure, execute)
4. Blue-green deployment with zero downtime

### E2E Test Example

```csharp
[Collection("E2E")]
public class PluginLifecycleE2ETests : IClassFixture<E2ETestFixture>
{
    [Fact]
    public async Task CompletePluginLifecycle_FromRegistrationToProduction()
    {
        // 1. Register plugin
        var plugin = await RegisterPluginAsync("stripe-payment-processor");

        // 2. Upload v1.0.0
        var v1 = await UploadPluginVersionAsync(plugin.PluginId, "1.0.0");

        // 3. Sandbox test
        var sandboxResult = await TestInSandboxAsync(plugin.PluginId, "1.0.0");
        sandboxResult.Success.Should().BeTrue();

        // 4. Deploy to Development
        var devDeployment = await DeployAsync(plugin.PluginId, "1.0.0", "Development", "Direct");
        await WaitForDeploymentCompletionAsync(devDeployment.DeploymentId);

        // 5. Deploy to QA
        var qaDeployment = await DeployAsync(plugin.PluginId, "1.0.0", "QA", "Rolling");
        await WaitForDeploymentCompletionAsync(qaDeployment.DeploymentId);

        // 6. Request production approval
        var approvalRequest = await RequestProductionApprovalAsync(plugin.PluginId, "1.0.0");

        // 7. Approve deployment
        await ApproveDeploymentAsync(approvalRequest.ApprovalId);

        // 8. Deploy to Production (Canary)
        var prodDeployment = await DeployAsync(plugin.PluginId, "1.0.0", "Production", "Canary");
        await WaitForDeploymentCompletionAsync(prodDeployment.DeploymentId);

        // Assert - Deployment successful
        var finalStatus = await GetDeploymentStatusAsync(prodDeployment.DeploymentId);
        finalStatus.Status.Should().Be("Completed");
        finalStatus.ProgressPercentage.Should().Be(100);
    }
}
```

---

## Performance Testing

**Target:** Meet SLA requirements

### Performance Test Scenarios

**Scenario 1: Plugin Load Performance**

```csharp
[Fact]
public async Task PluginLoad_P99Latency_LessThan200ms()
{
    // Arrange
    var pluginCount = 100;
    var latencies = new List<double>();

    // Act
    for (int i = 0; i < pluginCount; i++)
    {
        var stopwatch = Stopwatch.StartNew();
        await LoadPluginAsync($"test-plugin-{i}");
        stopwatch.Stop();
        latencies.Add(stopwatch.Elapsed.TotalMilliseconds);
    }

    // Assert
    var p50 = latencies.OrderBy(x => x).ElementAt(50);
    var p95 = latencies.OrderBy(x => x).ElementAt(95);
    var p99 = latencies.OrderBy(x => x).ElementAt(99);

    p50.Should().BeLessThan(50);
    p95.Should().BeLessThan(150);
    p99.Should().BeLessThan(200);
}
```

**Scenario 2: Deployment Performance**

```csharp
[Fact]
public async Task DirectDeployment_CompletesWithin10Seconds()
{
    // Arrange
    var plugin = await RegisterTestPluginAsync();
    var version = await UploadTestPluginVersionAsync(plugin.PluginId);

    // Act
    var stopwatch = Stopwatch.StartNew();
    var deployment = await DeployAsync(plugin.PluginId, version.Version, "Development", "Direct");
    await WaitForDeploymentCompletionAsync(deployment.DeploymentId);
    stopwatch.Stop();

    // Assert
    stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(10));
}
```

---

## Security Testing

### Security Test Scenarios

**Scenario 1: Sandbox Isolation**

```csharp
[Fact]
public async Task SandboxExecution_CannotAccessProductionData()
{
    // Arrange
    var maliciousPlugin = CreateMaliciousPlugin(); // Tries to read /etc/passwd

    // Act
    var sandbox = await CreateSandboxAsync(maliciousPlugin.PluginId, maliciousPlugin.Version);
    var result = await ExecuteInSandboxAsync(sandbox.SandboxId, "ReadFile", new { path = "/etc/passwd" });

    // Assert
    result.Success.Should().BeFalse();
    result.ErrorMessage.Should().Contain("Access denied");
}
```

**Scenario 2: Tenant Isolation**

```csharp
[Fact]
public async Task PluginExecution_CannotAccessOtherTenantsData()
{
    // Arrange
    var plugin = await RegisterAndDeployTestPluginAsync();
    await EnablePluginForTenantAsync(plugin.PluginId, "tenant-a");
    await EnablePluginForTenantAsync(plugin.PluginId, "tenant-b");

    // Act - Tenant A tries to access Tenant B data
    var result = await ExecutePluginForTenantAsync(
        plugin.PluginId,
        "tenant-a",
        method: "ReadData",
        parameters: new { tenantId = "tenant-b" } // Try to access other tenant
    );

    // Assert
    result.Success.Should().BeFalse();
    result.ErrorMessage.Should().Contain("Access denied");
}
```

---

## Test Organization

### Project Structure

```
tests/
├── HotSwap.Distributed.Tests/                  # Unit tests
│   ├── Domain/
│   │   ├── PluginTests.cs                      # 25 tests
│   │   ├── PluginVersionTests.cs               # 20 tests
│   │   ├── PluginDeploymentTests.cs            # 20 tests
│   │   └── TenantPluginConfigTests.cs          # 15 tests
│   ├── Deployment/
│   │   ├── DirectDeploymentStrategyTests.cs    # 15 tests
│   │   ├── CanaryDeploymentStrategyTests.cs    # 20 tests
│   │   ├── BlueGreenDeploymentStrategyTests.cs # 15 tests
│   │   ├── RollingDeploymentStrategyTests.cs   # 15 tests
│   │   └── ABTestingDeploymentStrategyTests.cs # 15 tests
│   ├── Infrastructure/
│   │   ├── MinIOPluginStorageTests.cs          # 15 tests
│   │   ├── AssemblyPluginLoaderTests.cs        # 20 tests
│   │   └── SandboxExecutorTests.cs             # 20 tests
│   └── Orchestrator/
│       ├── PluginHealthMonitorTests.cs         # 15 tests
│       └── TenantPluginRouterTests.cs          # 15 tests
├── HotSwap.Distributed.IntegrationTests/       # Integration tests
│   ├── PluginDeploymentTests.cs                # 15 tests
│   ├── SandboxExecutionTests.cs                # 10 tests
│   └── MultiTenantIsolationTests.cs            # 10 tests
└── HotSwap.Distributed.E2ETests/               # End-to-end tests
    ├── PluginLifecycleE2ETests.cs              # 8 tests
    ├── CanaryDeploymentE2ETests.cs             # 6 tests
    └── MultiTenantE2ETests.cs                  # 6 tests
```

---

## TDD Workflow

Follow Red-Green-Refactor cycle for ALL code changes.

### Example: Implementing Canary Deployment

**Step 1: 🔴 RED - Write Failing Test**

```csharp
[Fact]
public async Task DeployAsync_WithCanaryConfig_ProgressivelyRollsOut()
{
    // Arrange
    var strategy = new CanaryDeploymentStrategy(_mockLoader.Object);
    var plugin = CreateTestPlugin();
    var config = new CanaryConfig { InitialPercentage = 10, IncrementPercentage = 20 };

    // Act
    var result = await strategy.DeployAsync(plugin, config);

    // Assert
    result.Success.Should().BeTrue();
}
```

Run test: **FAILS** ❌ (CanaryDeploymentStrategy doesn't exist)

**Step 2: 🟢 GREEN - Minimal Implementation**

```csharp
public class CanaryDeploymentStrategy : IDeploymentStrategy
{
    public async Task<DeploymentResult> DeployAsync(Plugin plugin, object config)
    {
        return DeploymentResult.Success(Guid.NewGuid().ToString(), 0);
    }
}
```

Run test: **PASSES** ✓

**Step 3: 🔵 REFACTOR - Improve Implementation**

```csharp
public class CanaryDeploymentStrategy : IDeploymentStrategy
{
    public async Task<DeploymentResult> DeployAsync(Plugin plugin, CanaryConfig config)
    {
        var currentPercentage = config.InitialPercentage;
        while (currentPercentage <= 100)
        {
            await DeployToPercentageAsync(plugin, currentPercentage);
            await Task.Delay(config.EvaluationPeriod);
            currentPercentage += config.IncrementPercentage;
        }
        return DeploymentResult.Success(Guid.NewGuid().ToString(), GetTenantCount());
    }
}
```

Run test: **PASSES** ✓

---

## CI/CD Integration

### GitHub Actions Workflow

```yaml
name: Plugin Manager Tests

on:
  push:
    branches: [main, claude/*]
  pull_request:
    branches: [main]

jobs:
  test:
    runs-on: ubuntu-latest

    services:
      minio:
        image: minio/minio:latest
        env:
          MINIO_ROOT_USER: minioadmin
          MINIO_ROOT_PASSWORD: minioadmin
        ports:
          - 9000:9000

      postgres:
        image: postgres:15
        env:
          POSTGRES_PASSWORD: test_password
        ports:
          - 5432:5432

      redis:
        image: redis:7
        ports:
          - 6379:6379

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
        run: dotnet test tests/HotSwap.Distributed.Tests/ --no-build --verbosity normal

      - name: Run integration tests
        run: dotnet test tests/HotSwap.Distributed.IntegrationTests/ --no-build --verbosity normal

      - name: Run E2E tests
        run: dotnet test tests/HotSwap.Distributed.E2ETests/ --no-build --verbosity normal

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
**Test Count:** 350+ tests
**Coverage Target:** 85%+
