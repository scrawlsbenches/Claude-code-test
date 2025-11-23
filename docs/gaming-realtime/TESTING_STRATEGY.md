# Testing Strategy

**Version:** 1.0.0
**Target Coverage:** 85%+
**Test Count:** 350+ tests
**Framework:** xUnit, Moq, FluentAssertions

---

## Overview

The gaming configuration system follows Test-Driven Development (TDD) with comprehensive test coverage.

### Test Pyramid

```
                 ▲
                / \
               /E2E\           6% - End-to-End Tests (20 tests)
              /_____\
             /       \
            /Integration\      14% - Integration Tests (50 tests)
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
5. [Player Experience Testing](#player-experience-testing)

---

## Unit Testing

**Target:** 280+ unit tests, 85%+ coverage

### Domain Models Tests

**File:** `tests/HotSwap.Gaming.Tests/Domain/GameConfigurationTests.cs`

```csharp
public class GameConfigurationTests
{
    [Fact]
    public void Configuration_WithValidData_PassesValidation()
    {
        // Arrange
        var config = new GameConfiguration
        {
            ConfigId = "cfg-123",
            Name = "weapon-balance-v2",
            GameId = "battle-royale",
            Configuration = "{\"weapons\":{\"rifle\":{\"damage\":45}}}",
            Version = "2.0.0",
            SchemaId = "balance-v1"
        };

        // Act
        var isValid = config.IsValid(out var errors);

        // Assert
        isValid.Should().BeTrue();
        errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("", "test-game", "{}", "1.0.0", "schema-1", "ConfigId is required")]
    [InlineData("cfg-1", "", "{}", "1.0.0", "schema-1", "GameId is required")]
    [InlineData("cfg-1", "test", "invalid-json", "1.0.0", "schema-1", "Configuration must be valid JSON")]
    [InlineData("cfg-1", "test", "{}", "invalid", "schema-1", "Version must be in semantic format")]
    public void Configuration_WithInvalidData_FailsValidation(
        string configId, string gameId, string config, string version,
        string schemaId, string expectedError)
    {
        // Arrange
        var configuration = new GameConfiguration
        {
            ConfigId = configId,
            GameId = gameId,
            Configuration = config,
            Version = version,
            SchemaId = schemaId,
            Name = "test"
        };

        // Act
        var isValid = configuration.IsValid(out var errors);

        // Assert
        isValid.Should().BeFalse();
        errors.Should().Contain(expectedError);
    }
}
```

**Estimated Tests:** 25+ tests per model (×6 models = 150 tests)

---

### Deployment Strategy Tests

**File:** `tests/HotSwap.Gaming.Tests/Strategies/CanaryDeploymentStrategyTests.cs`

```csharp
public class CanaryDeploymentStrategyTests
{
    private readonly CanaryDeploymentStrategy _strategy;
    private readonly Mock<IMetricsCollector> _mockMetrics;
    private readonly Mock<IConfigDistributor> _mockDistributor;

    public CanaryDeploymentStrategyTests()
    {
        _mockMetrics = new Mock<IMetricsCollector>();
        _mockDistributor = new Mock<IConfigDistributor>();
        _strategy = new CanaryDeploymentStrategy(
            _mockMetrics.Object,
            _mockDistributor.Object);
    }

    [Fact]
    public async Task DeployAsync_FirstPhase_Deploys10Percent()
    {
        // Arrange
        var config = CreateConfig();
        var servers = CreateServers(100);
        var deployment = CreateDeployment();

        // Act
        var result = await _strategy.DeployAsync(config, servers, deployment);

        // Assert
        result.Success.Should().BeTrue();
        deployment.CurrentPercentage.Should().Be(10);
        _mockDistributor.Verify(
            d => d.DistributeAsync(config, It.Is<List<GameServer>>(s => s.Count == 10)),
            Times.Once);
    }

    [Fact]
    public async Task DeployAsync_MetricsThresholdBreached_TriggersRollback()
    {
        // Arrange
        var config = CreateConfig();
        var servers = CreateServers(100);
        var deployment = CreateDeployment();

        var unhealthyMetrics = new PlayerMetrics
        {
            ChurnRate = 10.0, // 5% increase from baseline (5.0)
            ActivePlayers = 50000
        };

        _mockMetrics.Setup(m => m.CollectAsync(It.IsAny<string>()))
            .ReturnsAsync(unhealthyMetrics);

        // Act
        var result = await _strategy.DeployAsync(config, servers, deployment);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("threshold breached");
        deployment.Status.Should().Be(DeploymentStatus.RolledBack);
    }
}
```

**Estimated Tests:** 20+ tests per strategy (×4 strategies = 80 tests)

---

### Rollback Decision Tests

**File:** `tests/HotSwap.Gaming.Tests/Engine/RollbackDecisionEngineTests.cs`

```csharp
public class RollbackDecisionEngineTests
{
    private readonly RollbackDecisionEngine _engine;

    [Theory]
    [InlineData(3.5, 8.5, true)] // Churn rate: 3.5 → 8.5 (5% increase, should rollback)
    [InlineData(3.5, 5.0, false)] // Churn rate: 3.5 → 5.0 (1.5% increase, OK)
    [InlineData(0.1, 11.0, true)] // Crash rate: 0.1% → 11% (>10% increase, rollback)
    public void ShouldRollback_ChurnAndCrashRates_CorrectDecision(
        double baselineChurn, double currentChurn, bool shouldRollback)
    {
        // Arrange
        var baseline = new PlayerMetrics { ChurnRate = baselineChurn, CrashRate = 0.1 };
        var current = new PlayerMetrics { ChurnRate = currentChurn, CrashRate = 0.1 };
        var deployment = CreateDeployment();

        // Act
        var result = deployment.ShouldRollback(current, baseline);

        // Assert
        result.Should().Be(shouldRollback);
    }
}
```

**Estimated Tests:** 25+ tests

---

## Integration Testing

**Target:** 50+ integration tests

### Configuration Deployment E2E

**File:** `tests/HotSwap.Gaming.IntegrationTests/DeploymentFlowTests.cs`

```csharp
public class DeploymentFlowTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    [Fact]
    public async Task FullDeploymentFlow_CanaryStrategy_Succeeds()
    {
        // 1. Create configuration
        var createConfigResponse = await _client.PostAsJsonAsync(
            "/api/v1/game-configs",
            new {
                name = "test-config",
                gameId = "test-game",
                configType = "GameBalance",
                configuration = "{\"test\":true}",
                version = "1.0.0",
                schemaId = "test-schema"
            });

        createConfigResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var config = await createConfigResponse.Content.ReadFromJsonAsync<GameConfiguration>();

        // 2. Approve configuration (admin)
        var approveResponse = await _client.PostAsync(
            $"/api/v1/game-configs/{config.ConfigId}/approve",
            null);
        approveResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // 3. Create deployment
        var deployResponse = await _client.PostAsJsonAsync(
            "/api/v1/deployments",
            new {
                configId = config.ConfigId,
                strategy = "Canary",
                targetRegions = new[] { "NA-WEST" },
                autoProgressEnabled = false // Manual for testing
            });

        deployResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var deployment = await deployResponse.Content.ReadFromJsonAsync<ConfigDeployment>();

        // 4. Verify deployment status
        var statusResponse = await _client.GetAsync(
            $"/api/v1/deployments/{deployment.DeploymentId}");
        statusResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var deploymentStatus = await statusResponse.Content.ReadFromJsonAsync<ConfigDeployment>();
        deploymentStatus.Status.Should().Be(DeploymentStatus.InProgress);
        deploymentStatus.CurrentPercentage.Should().Be(10);
    }
}
```

**Estimated Tests:** 50+ integration tests

---

## End-to-End Testing

**Target:** 20+ E2E tests

### Player Experience Test

```csharp
public class PlayerExperienceE2ETests
{
    [Fact]
    public async Task PlayerSession_DuringConfigUpdate_UnaffectedByDeployment()
    {
        // Simulate active player session
        var player = await CreatePlayerSession("player-12345");

        // Deploy new configuration
        var deployment = await DeployConfigurationAsync("cfg-new");

        // Verify player session continues
        player.IsConnected.Should().BeTrue();
        player.SessionDuration.Should().BeGreaterThan(TimeSpan.FromMinutes(5));

        // Verify player receives new config on next session
        await player.ReconnectAsync();
        player.CurrentConfigVersion.Should().Be("cfg-new");
    }
}
```

---

## Performance Testing

### Load Testing

**Tool:** k6, Apache JMeter

```javascript
// k6 load test script
import http from 'k6/http';
import { check } from 'k6';

export let options = {
  stages: [
    { duration: '2m', target: 100 },  // Ramp up
    { duration: '5m', target: 100 },  // Steady
    { duration: '2m', target: 0 },    // Ramp down
  ],
};

export default function () {
  // Simulate config deployment
  let deployResponse = http.post('http://api.example.com/api/v1/deployments', {
    configId: 'cfg-123',
    strategy: 'Canary',
  });

  check(deployResponse, {
    'deployment created': (r) => r.status === 202,
    'deployment time < 1s': (r) => r.timings.duration < 1000,
  });
}
```

**Performance Targets:**
- Config deployment: < 60 seconds globally
- Rollback time: < 10 seconds
- API response time: p95 < 200ms
- Concurrent deployments: 100+

---

## Player Experience Testing

### Churn Rate Monitoring

```csharp
public class ChurnRateMonitoringTests
{
    [Fact]
    public async Task Deployment_WithHighChurnRate_TriggersAutoRollback()
    {
        // Arrange
        var deployment = await CreateDeployment("cfg-bad");

        // Simulate player churn
        await SimulatePlayerChurn(percentage: 8.0); // > 5% threshold

        // Act
        await Task.Delay(TimeSpan.FromSeconds(30)); // Wait for rollback detection

        // Assert
        var deploymentStatus = await GetDeploymentStatus(deployment.DeploymentId);
        deploymentStatus.Status.Should().Be(DeploymentStatus.RolledBack);
        deploymentStatus.RollbackReason.Should().Contain("churn rate");
    }
}
```

---

## CI/CD Integration

### GitHub Actions Workflow

```yaml
name: Run Tests

on: [push, pull_request]

jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v2

      - name: Setup .NET
        uses: actions/setup-dotnet@v1
        with:
          dotnet-version: '8.0.x'

      - name: Restore dependencies
        run: dotnet restore

      - name: Run unit tests
        run: dotnet test --filter "Category=Unit" --logger "trx;LogFileName=unit-tests.trx"

      - name: Run integration tests
        run: dotnet test --filter "Category=Integration" --logger "trx;LogFileName=integration-tests.trx"

      - name: Generate coverage report
        run: dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover

      - name: Upload coverage to Codecov
        uses: codecov/codecov-action@v2
        with:
          files: ./coverage.opencover.xml
```

---

**Document Version:** 1.0.0
**Last Updated:** 2025-11-23
