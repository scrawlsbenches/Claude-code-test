# Testing Strategy

**Version:** 1.0.0
**Target Coverage:** 85%+
**Test Count:** 400+ tests
**Framework:** xUnit, Moq, FluentAssertions, Testcontainers

---

## Overview

The plugin manager system follows **Test-Driven Development (TDD)** with comprehensive test coverage across all layers.

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

## Unit Testing (320+ tests)

### Domain Models Tests

**File:** `tests/HotSwap.Distributed.Tests/Domain/Plugins/PluginTests.cs`

```csharp
public class PluginTests
{
    [Fact]
    public void Plugin_WithValidData_PassesValidation()
    {
        // Arrange
        var plugin = new Plugin
        {
            PluginId = "payment-stripe",
            Name = "Stripe Payment Processor",
            Publisher = "Acme Corp",
            Category = PluginCategory.PaymentGateway
        };

        // Act
        var isValid = plugin.IsValid(out var errors);

        // Assert
        isValid.Should().BeTrue();
        errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("Payment-Stripe", false)] // Uppercase not allowed
    [InlineData("payment_stripe", false)] // Underscore not allowed
    [InlineData("payment-stripe", true)]  // Valid
    public void PluginId_Validation_WorksCorrectly(string pluginId, bool shouldBeValid)
    {
        var plugin = new Plugin
        {
            PluginId = pluginId,
            Name = "Test",
            Publisher = "Test"
        };

        var isValid = plugin.IsValid(out var errors);

        isValid.Should().Be(shouldBeValid);
    }
}
```

### Deployment Strategy Tests

**File:** `tests/HotSwap.Distributed.Tests/Strategies/CanaryDeploymentStrategyTests.cs`

```csharp
public class CanaryDeploymentStrategyTests
{
    [Fact]
    public async Task Deploy_WithHealthyInstances_PromotesThroughAllStages()
    {
        // Arrange
        var strategy = new CanaryDeploymentStrategy(
            _healthMonitor.Object,
            _instanceProvider.Object);

        var deployment = CreateTestDeployment(stages: "10,30,50,100");

        _healthMonitor
            .Setup(h => h.CheckHealthAsync(It.IsAny<PluginDeployment>()))
            .ReturnsAsync(HealthCheckResult.Healthy());

        // Act
        var result = await strategy.DeployAsync(deployment);

        // Assert
        result.Success.Should().BeTrue();
        deployment.Progress.Should().Be(100);
        _healthMonitor.Verify(h => h.CheckHealthAsync(deployment), Times.Exactly(4));
    }

    [Fact]
    public async Task Deploy_WithUnhealthyInstances_RollsBackAutomatically()
    {
        // Arrange
        var strategy = new CanaryDeploymentStrategy(
            _healthMonitor.Object,
            _instanceProvider.Object);

        var deployment = CreateTestDeployment(stages: "10,30,50,100");

        _healthMonitor
            .SetupSequence(h => h.CheckHealthAsync(It.IsAny<PluginDeployment>()))
            .ReturnsAsync(HealthCheckResult.Healthy())  // 10% OK
            .ReturnsAsync(HealthCheckResult.Unhealthy("High error rate")); // 30% FAIL

        // Act
        var result = await strategy.DeployAsync(deployment);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("High error rate");
        _rollbackService.Verify(r => r.RollbackAsync(deployment), Times.Once);
    }
}
```

---

## Integration Testing (60+ tests)

### Plugin Registry Integration Tests

**File:** `tests/HotSwap.Distributed.IntegrationTests/PluginRegistryTests.cs`

```csharp
public class PluginRegistryIntegrationTests : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task RegisterPlugin_StoredAndRetrieved_Successfully()
    {
        // Arrange
        var registry = new PostgreSqlPluginRegistry(_connectionString);
        var plugin = CreateTestPlugin("payment-stripe");

        // Act
        await registry.RegisterAsync(plugin);
        var retrieved = await registry.GetAsync("payment-stripe");

        // Assert
        retrieved.Should().NotBeNull();
        retrieved.PluginId.Should().Be("payment-stripe");
        retrieved.Name.Should().Be(plugin.Name);
    }

    [Fact]
    public async Task SearchPlugins_WithCategory_ReturnsMatchingPlugins()
    {
        // Arrange
        var registry = new PostgreSqlPluginRegistry(_connectionString);
        await registry.RegisterAsync(CreateTestPlugin("payment-stripe", PluginCategory.PaymentGateway));
        await registry.RegisterAsync(CreateTestPlugin("auth-saml", PluginCategory.Authentication));

        // Act
        var results = await registry.SearchAsync(category: PluginCategory.PaymentGateway);

        // Assert
        results.Should().ContainSingle();
        results.First().PluginId.Should().Be("payment-stripe");
    }
}
```

### Plugin Storage Integration Tests

**File:** `tests/HotSwap.Distributed.IntegrationTests/PluginStorageTests.cs`

```csharp
public class PluginStorageIntegrationTests : IClassFixture<MinIOFixture>
{
    [Fact]
    public async Task UploadPlugin_WithChecksum_StoredCorrectly()
    {
        // Arrange
        var storage = new MinIOPluginStorage(_minioClient);
        var pluginData = Encoding.UTF8.GetBytes("test plugin binary");
        var checksum = ComputeSHA256(pluginData);

        // Act
        var url = await storage.UploadAsync("payment-stripe", "2.0.0", pluginData, checksum);
        var downloaded = await storage.DownloadAsync(url);

        // Assert
        downloaded.Should().Equal(pluginData);
    }

    [Fact]
    public async Task UploadPlugin_WithInvalidChecksum_ThrowsException()
    {
        // Arrange
        var storage = new MinIOPluginStorage(_minioClient);
        var pluginData = Encoding.UTF8.GetBytes("test plugin binary");
        var invalidChecksum = "invalid-checksum";

        // Act & Assert
        await storage.Invoking(s => s.UploadAsync("payment-stripe", "2.0.0", pluginData, invalidChecksum))
            .Should().ThrowAsync<InvalidChecksumException>();
    }
}
```

---

## End-to-End Testing (20+ tests)

### Full Deployment Flow Tests

**File:** `tests/HotSwap.Distributed.E2ETests/DeploymentFlowTests.cs`

```csharp
public class DeploymentFlowTests : IClassFixture<PlatformFixture>
{
    [Fact]
    public async Task FullDeploymentFlow_RegisterDeployMonitor_Success()
    {
        // Arrange
        var apiClient = _fixture.CreateAuthenticatedClient();

        // 1. Register plugin
        var registerRequest = new RegisterPluginRequest
        {
            PluginId = "payment-stripe",
            Name = "Stripe Payment Processor",
            Version = "2.0.0",
            BinaryUrl = await UploadTestPlugin()
        };
        var registerResponse = await apiClient.PostAsync("/api/v1/plugins", registerRequest);
        registerResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // 2. Deploy plugin
        var deployRequest = new DeployPluginRequest
        {
            PluginId = "payment-stripe",
            PluginVersion = "2.0.0",
            TenantId = "test-tenant",
            Strategy = "Canary"
        };
        var deployResponse = await apiClient.PostAsync("/api/v1/plugin-deployments", deployRequest);
        deployResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var deployment = await deployResponse.Content.ReadAsAsync<PluginDeployment>();

        // 3. Monitor deployment until complete
        var finalStatus = await PollUntilComplete(apiClient, deployment.DeploymentId);
        finalStatus.Status.Should().Be(DeploymentStatus.Completed);
        finalStatus.Progress.Should().Be(100);
        finalStatus.HealthyInstances.Should().Be(finalStatus.TotalInstances);
    }
}
```

---

## Security Testing

### Tenant Isolation Tests

**File:** `tests/HotSwap.Distributed.SecurityTests/TenantIsolationTests.cs`

```csharp
public class TenantIsolationTests
{
    [Fact]
    public async Task Tenant_CannotAccessOtherTenantPlugins()
    {
        // Arrange
        var tenant1Client = CreateClientForTenant("tenant-1");
        var tenant2Client = CreateClientForTenant("tenant-2");

        // Deploy plugin to tenant-1
        await DeployPluginToTenant("payment-stripe", "tenant-1");

        // Act - Try to access tenant-1 plugin from tenant-2
        var response = await tenant2Client.GetAsync("/api/v1/tenants/tenant-1/plugins");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Plugin_CannotAccessFilesOutsideSandbox()
    {
        // Arrange
        var maliciousPlugin = CreatePluginThatAccessesSystemFiles();

        // Act
        var result = await DeployPlugin(maliciousPlugin);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("sandbox violation");
    }
}
```

---

## Performance Testing

### Load Tests

**File:** `tests/HotSwap.Distributed.PerformanceTests/LoadTests.cs`

```csharp
public class LoadTests
{
    [Fact]
    public async Task Deploy_1000PluginsConcurrently_CompletesWithinSLA()
    {
        // Arrange
        var deploymentTasks = Enumerable.Range(0, 1000)
            .Select(i => DeployTestPluginAsync($"plugin-{i}"))
            .ToList();

        // Act
        var stopwatch = Stopwatch.StartNew();
        await Task.WhenAll(deploymentTasks);
        stopwatch.Stop();

        // Assert
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromMinutes(30));
        var failures = deploymentTasks.Count(t => t.Result.Status == DeploymentStatus.Failed);
        failures.Should().BeLessThan(10); // < 1% failure rate
    }

    [Fact]
    public async Task PluginActivation_CompletesUnderOneSecond()
    {
        // Arrange
        var plugin = await RegisterTestPlugin();

        // Act
        var stopwatch = Stopwatch.StartNew();
        await ActivatePlugin(plugin.PluginId);
        stopwatch.Stop();

        // Assert
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(1000);
    }
}
```

---

## TDD Workflow

### Red-Green-Refactor Cycle

1. **Red:** Write a failing test
```csharp
[Fact]
public void Plugin_WithMissingPublisher_FailsValidation()
{
    var plugin = new Plugin { PluginId = "test", Name = "Test" };
    var isValid = plugin.IsValid(out var errors);
    isValid.Should().BeFalse();
    errors.Should().Contain("Publisher is required");
}
```

2. **Green:** Write minimal code to pass
```csharp
public bool IsValid(out List<string> errors)
{
    errors = new List<string>();
    if (string.IsNullOrWhiteSpace(Publisher))
        errors.Add("Publisher is required");
    return errors.Count == 0;
}
```

3. **Refactor:** Improve code quality
```csharp
public bool IsValid(out List<string> errors)
{
    errors = ValidateRequiredFields();
    return errors.Count == 0;
}

private List<string> ValidateRequiredFields()
{
    var errors = new List<string>();
    if (string.IsNullOrWhiteSpace(PluginId))
        errors.Add("PluginId is required");
    if (string.IsNullOrWhiteSpace(Name))
        errors.Add("Name is required");
    if (string.IsNullOrWhiteSpace(Publisher))
        errors.Add("Publisher is required");
    return errors;
}
```

---

## CI/CD Integration

### GitHub Actions Workflow

```yaml
name: Test

on: [push, pull_request]

jobs:
  test:
    runs-on: ubuntu-latest
    
    services:
      postgres:
        image: postgres:15
        env:
          POSTGRES_PASSWORD: postgres
      redis:
        image: redis:7
      minio:
        image: minio/minio
        
    steps:
      - uses: actions/checkout@v3
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '8.0.x'
      
      - name: Run Unit Tests
        run: dotnet test --filter "Category=Unit" --logger "trx;LogFileName=unit-test-results.trx"
      
      - name: Run Integration Tests
        run: dotnet test --filter "Category=Integration" --logger "trx;LogFileName=integration-test-results.trx"
      
      - name: Run E2E Tests
        run: dotnet test --filter "Category=E2E" --logger "trx;LogFileName=e2e-test-results.trx"
      
      - name: Generate Coverage Report
        run: dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
      
      - name: Upload Coverage to Codecov
        uses: codecov/codecov-action@v3
```

---

## Test Coverage Goals

| Component | Target Coverage | Current Coverage |
|-----------|----------------|------------------|
| Domain Models | 95%+ | - |
| Deployment Strategies | 90%+ | - |
| Plugin Registry | 85%+ | - |
| Plugin Storage | 85%+ | - |
| API Controllers | 80%+ | - |
| Integration Tests | N/A | - |
| **Overall** | **85%+** | - |

---

**Last Updated:** 2025-11-23
**Version:** 1.0.0
