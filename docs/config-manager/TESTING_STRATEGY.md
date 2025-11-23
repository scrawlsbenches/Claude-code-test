# Testing Strategy

**Version:** 1.0.0
**Target Coverage:** 85%+
**Test Count:** 350+ tests
**Framework:** xUnit, Moq, FluentAssertions

---

## Overview

The configuration manager follows **Test-Driven Development (TDD)** with comprehensive test coverage across all layers. This document outlines the testing strategy, test organization, and best practices.

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
5. [Smoke Testing](#smoke-testing)
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
- Deployment strategies (canary, blue-green, rolling, direct)
- Health monitoring (metrics tracking, rollback triggers)
- Schema validation (JSON Schema validation)
- Version management (diffing, rollback logic)

### Domain Models Tests

**File:** `tests/HotSwap.ConfigManager.Tests/Domain/ConfigProfileTests.cs`

```csharp
public class ConfigProfileTests
{
    [Fact]
    public void ConfigProfile_WithValidData_PassesValidation()
    {
        // Arrange
        var profile = new ConfigProfile
        {
            Name = "payment-service.production",
            SchemaId = "payment-config.v1",
            Environment = ConfigEnvironment.Production
        };

        // Act
        var isValid = profile.IsValid(out var errors);

        // Assert
        isValid.Should().BeTrue();
        errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("", "schema-1", "Name is required")]
    [InlineData("invalid name", "schema-1", "Name must contain only alphanumeric")]
    [InlineData("valid.name", "", "SchemaId is required")]
    public void ConfigProfile_WithInvalidData_FailsValidation(
        string name, string schemaId, string expectedError)
    {
        // Arrange
        var profile = new ConfigProfile
        {
            Name = name,
            SchemaId = schemaId,
            Environment = ConfigEnvironment.Production
        };

        // Act
        var isValid = profile.IsValid(out var errors);

        // Assert
        isValid.Should().BeFalse();
        errors.Should().Contain(e => e.Contains(expectedError));
    }

    [Fact]
    public void IsProduction_WhenEnvironmentIsProduction_ReturnsTrue()
    {
        // Arrange
        var profile = new ConfigProfile
        {
            Name = "test",
            SchemaId = "schema-1",
            Environment = ConfigEnvironment.Production
        };

        // Act
        var isProduction = profile.IsProduction();

        // Assert
        isProduction.Should().BeTrue();
    }
}
```

**Estimated Tests:** 20+ tests per domain model (×5 models = 100 tests)

---

### Deployment Strategy Tests

**File:** `tests/HotSwap.ConfigManager.Tests/Strategies/CanaryDeploymentStrategyTests.cs`

```csharp
public class CanaryDeploymentStrategyTests
{
    private readonly CanaryDeploymentStrategy _strategy;
    private readonly Mock<IConfigDeployer> _mockDeployer;
    private readonly Mock<IHealthMonitor> _mockHealthMonitor;

    public CanaryDeploymentStrategyTests()
    {
        _mockDeployer = new Mock<IConfigDeployer>();
        _mockHealthMonitor = new Mock<IHealthMonitor>();
        _strategy = new CanaryDeploymentStrategy(_mockDeployer.Object, _mockHealthMonitor.Object);
    }

    [Fact]
    public async Task DeployAsync_WithHealthyInstances_CompletesAllPhases()
    {
        // Arrange
        var deployment = CreateDeployment(instanceCount: 10);
        _mockHealthMonitor.Setup(h => h.CheckHealthAsync(It.IsAny<string>()))
            .ReturnsAsync(new DeploymentHealth { OverallStatus = HealthStatus.Healthy });

        // Act
        var result = await _strategy.DeployAsync(deployment);

        // Assert
        result.Success.Should().BeTrue();
        result.InstancesDeployed.Should().Be(10);
        _mockDeployer.Verify(d => d.DeployToInstanceAsync(It.IsAny<ConfigDeployment>(), It.IsAny<string>()),
            Times.Exactly(10));
    }

    [Fact]
    public async Task DeployAsync_WithHealthDegradation_TriggersRollback()
    {
        // Arrange
        var deployment = CreateDeployment(instanceCount: 10);
        var healthSequence = new Queue<HealthStatus>(new[]
        {
            HealthStatus.Healthy,  // Phase 1 (10%)
            HealthStatus.Degraded  // Phase 2 (30%) - triggers rollback
        });

        _mockHealthMonitor.Setup(h => h.CheckHealthAsync(It.IsAny<string>()))
            .ReturnsAsync(() => new DeploymentHealth { OverallStatus = healthSequence.Dequeue() });

        // Act
        var result = await _strategy.DeployAsync(deployment);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Rolled back");
        // Should only deploy Phase 1 (1 instance) and Phase 2 (3 instances) = 4 total
        _mockDeployer.Verify(d => d.DeployToInstanceAsync(It.IsAny<ConfigDeployment>(), It.IsAny<string>()),
            Times.AtMost(4));
    }

    [Fact]
    public async Task DeployAsync_WithPhaseInterval_WaitsBetweenPhases()
    {
        // Arrange
        var deployment = CreateDeployment(instanceCount: 10);
        deployment.Config.PhaseInterval = TimeSpan.FromSeconds(2);
        _mockHealthMonitor.Setup(h => h.CheckHealthAsync(It.IsAny<string>()))
            .ReturnsAsync(new DeploymentHealth { OverallStatus = HealthStatus.Healthy });

        var stopwatch = Stopwatch.StartNew();

        // Act
        await _strategy.DeployAsync(deployment);

        // Assert
        stopwatch.Elapsed.Should().BeGreaterThan(TimeSpan.FromSeconds(6)); // 4 phases × 2 seconds
    }

    private ConfigDeployment CreateDeployment(int instanceCount)
    {
        return new ConfigDeployment
        {
            DeploymentId = Guid.NewGuid().ToString(),
            ConfigName = "test-config",
            ConfigVersion = "1.0.0",
            TargetInstances = Enumerable.Range(1, instanceCount).Select(i => $"instance-{i}").ToList(),
            Config = new DeploymentConfig { CanaryPercentage = 10, PhaseInterval = TimeSpan.FromMilliseconds(100) },
            HealthCheck = new HealthCheckConfig { Enabled = true }
        };
    }
}
```

**Estimated Tests:** 15+ tests per strategy (×4 strategies = 60 tests)

---

### Health Monitoring Tests

**File:** `tests/HotSwap.ConfigManager.Tests/Monitoring/HealthMonitorTests.cs`

```csharp
public class HealthMonitorTests
{
    [Fact]
    public async Task CheckHealth_WhenErrorRateExceedsThreshold_ReturnsDegraded()
    {
        // Arrange
        var monitor = new HealthMonitor();
        var deployment = CreateDeployment();
        var health = new DeploymentHealth
        {
            DeploymentId = deployment.DeploymentId,
            BaselineErrorRate = 1.5,
            CurrentErrorRate = 7.0  // 5.5% increase (> 5% threshold)
        };

        // Act
        var hasDegraded = health.HasDegraded(deployment.HealthCheck);

        // Assert
        hasDegraded.Should().BeTrue();
    }

    [Fact]
    public async Task CheckHealth_WhenLatencyExceedsThreshold_ReturnsDegraded()
    {
        // Arrange
        var health = new DeploymentHealth
        {
            DeploymentId = "deploy-1",
            BaselineP99Latency = 200,
            CurrentP99Latency = 350  // 75% increase (> 50% threshold)
        };

        var config = new HealthCheckConfig
        {
            LatencyThreshold = 50.0
        };

        // Act
        var hasDegraded = health.HasDegraded(config);

        // Assert
        hasDegraded.Should().BeTrue();
    }

    [Fact]
    public void RecordCheck_AddsCheckToHistory()
    {
        // Arrange
        var health = new DeploymentHealth { DeploymentId = "deploy-1" };
        var checkResult = new HealthCheckResult
        {
            Status = HealthStatus.Healthy,
            ErrorRate = 1.5,
            P99Latency = 200
        };

        // Act
        health.RecordCheck(checkResult);

        // Assert
        health.CheckHistory.Should().HaveCount(1);
        health.CheckHistory.First().Should().Be(checkResult);
    }

    [Fact]
    public void RecordCheck_KeepsOnlyLast100Checks()
    {
        // Arrange
        var health = new DeploymentHealth { DeploymentId = "deploy-1" };

        // Act
        for (int i = 0; i < 150; i++)
        {
            health.RecordCheck(new HealthCheckResult { Status = HealthStatus.Healthy });
        }

        // Assert
        health.CheckHistory.Should().HaveCount(100);
    }
}
```

**Estimated Tests:** 25+ tests for health monitoring

---

### Version Management Tests

**File:** `tests/HotSwap.ConfigManager.Tests/Versioning/ConfigVersionTests.cs`

```csharp
public class ConfigVersionTests
{
    [Theory]
    [InlineData("1.0.0", true)]
    [InlineData("10.25.3", true)]
    [InlineData("1.0", false)]
    [InlineData("v1.0.0", false)]
    [InlineData("1.0.0-beta", false)]
    public void IsValid_ValidatesSemanticVersion(string version, bool expectedValid)
    {
        // Arrange
        var configVersion = new ConfigVersion
        {
            ConfigName = "test",
            Version = version,
            ConfigData = "{}",
            SchemaVersion = "1.0"
        };

        // Act
        var isValid = configVersion.IsValid(out var errors);

        // Assert
        isValid.Should().Be(expectedValid);
        if (!expectedValid)
        {
            errors.Should().Contain(e => e.Contains("semantic version"));
        }
    }

    [Fact]
    public void CalculateHash_GeneratesCorrectMD5Hash()
    {
        // Arrange
        var version = new ConfigVersion
        {
            ConfigName = "test",
            Version = "1.0.0",
            ConfigData = "{\"key\":\"value\"}",
            SchemaVersion = "1.0"
        };

        // Act
        version.CalculateHash();

        // Assert
        version.ConfigHash.Should().NotBeNullOrEmpty();
        version.ConfigHash.Length.Should().Be(32); // MD5 hash length
    }

    [Fact]
    public void ConfigData_ExceedingMaxSize_FailsValidation()
    {
        // Arrange
        var largeData = new string('x', 1048577); // 1 MB + 1 byte
        var version = new ConfigVersion
        {
            ConfigName = "test",
            Version = "1.0.0",
            ConfigData = largeData,
            SchemaVersion = "1.0"
        };

        // Act
        var isValid = version.IsValid(out var errors);

        // Assert
        isValid.Should().BeFalse();
        errors.Should().Contain(e => e.Contains("exceeds maximum size"));
    }
}
```

**Estimated Tests:** 20+ tests for version management

---

## Integration Testing

**Target:** 50+ integration tests

### Scope

Test multiple components working together with real dependencies (Redis, PostgreSQL).

**Test Scenarios:**
1. Create config → Upload version → Deploy (happy path)
2. Deploy with canary → Health degradation → Rollback
3. Blue-green deployment → Switch traffic → Verify
4. Rolling deployment → Instance failure → Stop and rollback
5. Schema validation → Breaking change → Approval workflow

### End-to-End Deployment Test

**File:** `tests/HotSwap.ConfigManager.IntegrationTests/DeploymentFlowTests.cs`

```csharp
[Collection("Integration")]
public class DeploymentFlowTests : IClassFixture<TestServerFixture>
{
    private readonly HttpClient _client;
    private readonly TestServerFixture _fixture;

    public DeploymentFlowTests(TestServerFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.CreateClient();
    }

    [Fact]
    public async Task EndToEndFlow_CreateConfigDeployRollback_WorksCorrectly()
    {
        // Arrange - Create config profile
        var profileRequest = new
        {
            name = "integration-test.production",
            environment = "Production",
            schemaId = "test-schema.v1",
            defaultStrategy = "Canary"
        };
        await _client.PostAsJsonAsync("/api/v1/configs", profileRequest);

        // Arrange - Upload version
        var versionRequest = new
        {
            version = "1.0.0",
            configData = "{\"timeout\":\"30s\",\"maxRetries\":3}",
            description = "Initial version"
        };
        await _client.PostAsJsonAsync("/api/v1/configs/integration-test.production/versions", versionRequest);

        // Arrange - Register instances
        var instanceIds = new List<string>();
        for (int i = 1; i <= 5; i++)
        {
            var instanceRequest = new
            {
                serviceName = "integration-test",
                hostname = $"10.0.1.{i}",
                port = 8080,
                environment = "Production"
            };
            var response = await _client.PostAsJsonAsync("/api/v1/instances", instanceRequest);
            var instance = await response.Content.ReadFromJsonAsync<InstanceResponse>();
            instanceIds.Add(instance.InstanceId);
        }

        // Act - Deploy configuration
        var deploymentRequest = new
        {
            configName = "integration-test.production",
            configVersion = "1.0.0",
            strategy = "Canary",
            targetInstances = instanceIds,
            config = new
            {
                canaryPercentage = 10,
                phaseInterval = "PT1S"
            },
            healthCheck = new
            {
                enabled = true,
                autoRollback = false
            }
        };
        var deployResponse = await _client.PostAsJsonAsync("/api/v1/deployments", deploymentRequest);
        var deployment = await deployResponse.Content.ReadFromJsonAsync<DeploymentResponse>();

        // Wait for deployment to complete
        await WaitForDeploymentCompletion(deployment.DeploymentId, TimeSpan.FromSeconds(30));

        // Assert - Verify deployment completed
        var statusResponse = await _client.GetAsync($"/api/v1/deployments/{deployment.DeploymentId}");
        var status = await statusResponse.Content.ReadFromJsonAsync<DeploymentDetailResponse>();
        status.Status.Should().Be("Completed");
        status.ProgressPercentage.Should().Be(100);

        // Act - Rollback deployment
        var rollbackRequest = new { reason = "Integration test rollback" };
        await _client.PostAsJsonAsync($"/api/v1/deployments/{deployment.DeploymentId}/rollback", rollbackRequest);

        // Assert - Verify rollback
        var rollbackStatus = await _client.GetAsync($"/api/v1/deployments/{deployment.DeploymentId}");
        var rolledBack = await rollbackStatus.Content.ReadFromJsonAsync<DeploymentDetailResponse>();
        rolledBack.Status.Should().Be("RolledBack");
        rolledBack.WasRolledBack.Should().BeTrue();
    }

    private async Task WaitForDeploymentCompletion(string deploymentId, TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < timeout)
        {
            var response = await _client.GetAsync($"/api/v1/deployments/{deploymentId}");
            var deployment = await response.Content.ReadFromJsonAsync<DeploymentDetailResponse>();

            if (deployment.Status == "Completed" || deployment.Status == "Failed" || deployment.Status == "RolledBack")
                return;

            await Task.Delay(TimeSpan.FromSeconds(1));
        }

        throw new TimeoutException($"Deployment {deploymentId} did not complete within {timeout}");
    }
}
```

**Estimated Tests:** 30+ integration tests

---

## End-to-End Testing

**Target:** 20+ E2E tests

### Scope

Test complete user workflows from API to database with all real components.

**E2E Test Scenarios:**
1. Complete configuration lifecycle (create → version → deploy → monitor → rollback)
2. Schema evolution workflow (register → validate → approve → use)
3. Multi-environment deployment (dev → staging → production)
4. Automatic rollback on health degradation

### E2E Test Example

```csharp
[Collection("E2E")]
public class ConfigurationLifecycleTests : IClassFixture<E2ETestFixture>
{
    [Fact]
    public async Task CompleteLifecycle_WithHealthMonitoring_WorksCorrectly()
    {
        // Arrange - Set up complete environment
        await SetupProductionEnvironment();
        var instances = await RegisterServiceInstances(count: 10);

        // Act - Create config and deploy
        await CreateConfigProfile("payment-service.production");
        await UploadConfigVersion("1.0.0", "{\"timeout\":\"30s\"}");
        var deployment = await DeployWithCanary("1.0.0", instances);

        // Assert - Verify canary phases
        await VerifyCanaryPhaseCompleted(deployment, phase: 1, expectedInstances: 1);
        await VerifyCanaryPhaseCompleted(deployment, phase: 2, expectedInstances: 3);

        // Act - Simulate health degradation
        await SimulateErrorRateIncrease(instances[0], errorRate: 10.0);

        // Assert - Verify automatic rollback
        await WaitForAutomaticRollback(deployment);
        await VerifyAllInstancesRolledBack(instances);
    }
}
```

---

## Performance Testing

**Target:** Meet SLA requirements

### Performance Test Scenarios

**Scenario 1: Deployment Speed Test**

```csharp
[Fact]
public async Task CanaryDeployment_10Instances_CompletesWithin60Seconds()
{
    // Arrange
    var deployment = CreateCanaryDeployment(instanceCount: 10);
    deployment.Config.PhaseInterval = TimeSpan.FromSeconds(5);

    // Act
    var stopwatch = Stopwatch.StartNew();
    var result = await _strategy.DeployAsync(deployment);
    stopwatch.Stop();

    // Assert
    result.Success.Should().BeTrue();
    stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(60));
}
```

**Scenario 2: Rollback Speed Test**

```csharp
[Fact]
public async Task Rollback_10Instances_CompletesWithin10Seconds()
{
    // Arrange
    var deployment = await CreateAndCompleteDeployment(instanceCount: 10);

    // Act
    var stopwatch = Stopwatch.StartNew();
    await _deploymentService.RollbackAsync(deployment.DeploymentId, "Test rollback");
    stopwatch.Stop();

    // Assert
    stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(10));
}
```

**Scenario 3: Instance Reload Speed Test**

```csharp
[Fact]
public async Task ConfigReload_SingleInstance_CompletesWithin5Seconds()
{
    // Arrange
    var instance = await RegisterInstance();
    var config = CreateTestConfig();

    // Act
    var stopwatch = Stopwatch.StartNew();
    await _configDeployer.DeployToInstanceAsync(config, instance.InstanceId);
    stopwatch.Stop();

    // Assert
    stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5));
}
```

---

## Smoke Testing

**Target:** 6 smoke tests (< 60 seconds)

Quick validation after deployment.

```bash
#!/bin/bash
# run-smoke-tests.sh

echo "Running configuration manager smoke tests..."

# 1. Health check
curl -f http://localhost:5000/health || exit 1

# 2. Create config profile
curl -f -X POST http://localhost:5000/api/v1/configs \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{"name":"smoke-test.dev","environment":"Development","schemaId":"test.v1"}' || exit 1

# 3. Upload config version
curl -f -X POST http://localhost:5000/api/v1/configs/smoke-test.dev/versions \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{"version":"1.0.0","configData":"{}"}' || exit 1

# 4. Register instance
curl -f -X POST http://localhost:5000/api/v1/instances \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{"serviceName":"smoke-test","hostname":"localhost","port":8080,"environment":"Development"}' || exit 1

echo "✓ All smoke tests passed"
```

---

## Test Organization

### Project Structure

```
tests/
├── HotSwap.ConfigManager.Tests/              # Unit tests
│   ├── Domain/
│   │   ├── ConfigProfileTests.cs             # 20 tests
│   │   ├── ConfigVersionTests.cs             # 20 tests
│   │   ├── ConfigDeploymentTests.cs          # 15 tests
│   │   └── ServiceInstanceTests.cs           # 15 tests
│   ├── Strategies/
│   │   ├── CanaryDeploymentStrategyTests.cs  # 15 tests
│   │   ├── BlueGreenStrategyTests.cs         # 15 tests
│   │   ├── RollingStrategyTests.cs           # 15 tests
│   │   └── DirectStrategyTests.cs            # 10 tests
│   ├── Monitoring/
│   │   ├── HealthMonitorTests.cs             # 25 tests
│   │   └── RollbackTriggerTests.cs           # 15 tests
│   └── Versioning/
│       ├── VersionManagerTests.cs            # 20 tests
│       └── DiffGeneratorTests.cs             # 15 tests
├── HotSwap.ConfigManager.IntegrationTests/    # Integration tests
│   ├── DeploymentFlowTests.cs                # 15 tests
│   ├── HealthMonitoringTests.cs              # 10 tests
│   ├── SchemaValidationTests.cs              # 10 tests
│   └── RollbackScenarioTests.cs              # 15 tests
└── HotSwap.ConfigManager.E2ETests/            # End-to-end tests
    ├── ConfigurationLifecycleTests.cs         # 10 tests
    ├── MultiEnvironmentTests.cs               # 5 tests
    └── AutoRollbackTests.cs                   # 5 tests
```

---

## TDD Workflow

Follow Red-Green-Refactor cycle for ALL code changes.

### Example: Implementing Canary Deployment

**Step 1: 🔴 RED - Write Failing Test**

```csharp
[Fact]
public async Task DeployAsync_WithCanaryStrategy_DeploysInPhases()
{
    // Arrange
    var strategy = new CanaryDeploymentStrategy(_mockDeployer.Object);
    var deployment = CreateDeployment(instanceCount: 10);

    // Act
    var result = await strategy.DeployAsync(deployment);

    // Assert
    result.Success.Should().BeTrue();
    result.InstancesDeployed.Should().Be(10);
}
```

Run test: **FAILS** ❌ (CanaryDeploymentStrategy doesn't exist)

**Step 2: 🟢 GREEN - Minimal Implementation**

```csharp
public class CanaryDeploymentStrategy : IDeploymentStrategy
{
    public async Task<DeploymentResult> DeployAsync(ConfigDeployment deployment)
    {
        // Minimal implementation to pass test
        foreach (var instance in deployment.TargetInstances)
        {
            await _deployer.DeployToInstanceAsync(deployment, instance);
        }
        return DeploymentResult.SuccessResult(deployment.DeploymentId, deployment.TargetInstances.Count);
    }
}
```

Run test: **PASSES** ✓

**Step 3: 🔵 REFACTOR - Improve Implementation**

```csharp
public async Task<DeploymentResult> DeployAsync(ConfigDeployment deployment)
{
    var phases = new[] { 10, 30, 50, 100 };
    // ... implement actual canary logic with health checks
}
```

Run test: **PASSES** ✓

---

## CI/CD Integration

### GitHub Actions Workflow

```yaml
name: Config Manager Tests

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

      postgres:
        image: postgres:15
        env:
          POSTGRES_PASSWORD: test_password
        ports:
          - 5432:5432

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
        run: dotnet test tests/HotSwap.ConfigManager.Tests/ --no-build --verbosity normal

      - name: Run integration tests
        run: dotnet test tests/HotSwap.ConfigManager.IntegrationTests/ --no-build --verbosity normal

      - name: Run E2E tests
        run: dotnet test tests/HotSwap.ConfigManager.E2ETests/ --no-build --verbosity normal

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
