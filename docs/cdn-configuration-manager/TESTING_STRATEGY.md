# Testing Strategy

**Version:** 1.0.0
**Target Coverage:** 85%+
**Test Count:** 400+ tests
**Framework:** xUnit, Moq, FluentAssertions

---

## Overview

The CDN Configuration Manager follows **Test-Driven Development (TDD)** with comprehensive test coverage across all layers. This document outlines the testing strategy, test organization, and best practices.

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
4. [Performance Testing](#performance-testing)
5. [Smoke Testing](#smoke-testing)
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
- Deployment strategies (rollout algorithms)
- Configuration validators (syntax, safety, conflicts)
- Edge location management (health checks, capacity)
- Performance monitoring (metrics analysis, thresholds)
- Auto-rollback engine (trigger logic, rollback execution)

### Domain Models Tests

**File:** `tests/HotSwap.CDN.Tests/Domain/ConfigurationTests.cs`

```csharp
public class ConfigurationTests
{
    [Fact]
    public void Configuration_WithValidData_PassesValidation()
    {
        // Arrange
        var configuration = new Configuration
        {
            ConfigurationId = Guid.NewGuid().ToString(),
            Name = "static-assets-cache",
            Type = ConfigurationType.CacheRule,
            Content = "{\"pathPattern\":\"/assets/*\",\"ttl\":3600}",
            SchemaVersion = "1.0",
            CreatedBy = "admin@example.com"
        };

        // Act
        var isValid = configuration.IsValid(out var errors);

        // Assert
        isValid.Should().BeTrue();
        errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("", "test", "CacheRule", "{}", "1.0", "ConfigurationId is required")]
    [InlineData("cfg-1", "", "CacheRule", "{}", "1.0", "Name is required")]
    [InlineData("cfg-1", "test", "CacheRule", "", "1.0", "Content is required")]
    [InlineData("cfg-1", "test", "CacheRule", "{}", "", "SchemaVersion is required")]
    public void Configuration_WithMissingRequiredField_FailsValidation(
        string configId, string name, string type, string content, string schemaVersion, string expectedError)
    {
        // Arrange
        var configuration = new Configuration
        {
            ConfigurationId = configId,
            Name = name,
            Type = Enum.Parse<ConfigurationType>(type),
            Content = content,
            SchemaVersion = schemaVersion,
            CreatedBy = "admin@example.com"
        };

        // Act
        var isValid = configuration.IsValid(out var errors);

        // Assert
        isValid.Should().BeFalse();
        errors.Should().Contain(expectedError);
    }

    [Fact]
    public void Configuration_WithInvalidJSON_FailsValidation()
    {
        // Arrange
        var configuration = new Configuration
        {
            ConfigurationId = "cfg-1",
            Name = "test",
            Type = ConfigurationType.CacheRule,
            Content = "invalid json{",
            SchemaVersion = "1.0",
            CreatedBy = "admin@example.com"
        };

        // Act
        var isValid = configuration.IsValid(out var errors);

        // Assert
        isValid.Should().BeFalse();
        errors.Should().Contain("Content must be valid JSON");
    }

    [Theory]
    [InlineData("1.0.0", true)]
    [InlineData("1.2.3", true)]
    [InlineData("10.20.30", true)]
    [InlineData("1.0", false)]
    [InlineData("1", false)]
    [InlineData("abc", false)]
    public void Configuration_VersionFormat_ValidatesCorrectly(string version, bool shouldBeValid)
    {
        // Arrange
        var configuration = new Configuration
        {
            ConfigurationId = "cfg-1",
            Name = "test",
            Type = ConfigurationType.CacheRule,
            Content = "{}",
            SchemaVersion = "1.0",
            Version = version,
            CreatedBy = "admin@example.com"
        };

        // Act
        var isValid = configuration.IsValid(out var errors);

        // Assert
        isValid.Should().Be(shouldBeValid);
        if (!shouldBeValid)
        {
            errors.Should().Contain(e => e.Contains("semantic versioning"));
        }
    }
}
```

**Estimated Tests:** 20+ tests per domain model (×5 models = 100 tests)

---

### Edge Location Tests

**File:** `tests/HotSwap.CDN.Tests/Domain/EdgeLocationTests.cs`

```csharp
public class EdgeLocationTests
{
    [Fact]
    public void EdgeLocation_WithValidData_PassesValidation()
    {
        // Arrange
        var location = new EdgeLocation
        {
            LocationId = "us-east-1",
            Name = "US East (Virginia)",
            Region = "North America",
            CountryCode = "US",
            Endpoint = "https://cdn-us-east-1.example.com"
        };

        // Act
        var isValid = location.IsValid(out var errors);

        // Assert
        isValid.Should().BeTrue();
        errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("USA", false)]  // Too long
    [InlineData("U", false)]    // Too short
    [InlineData("US", true)]    // Valid
    [InlineData("GB", true)]    // Valid
    public void EdgeLocation_CountryCodeFormat_ValidatesCorrectly(string countryCode, bool shouldBeValid)
    {
        // Arrange
        var location = new EdgeLocation
        {
            LocationId = "test-1",
            Name = "Test Location",
            Region = "Test Region",
            CountryCode = countryCode,
            Endpoint = "https://cdn-test.example.com"
        };

        // Act
        var isValid = location.IsValid(out var errors);

        // Assert
        isValid.Should().Be(shouldBeValid);
        if (!shouldBeValid)
        {
            errors.Should().Contain(e => e.Contains("ISO 3166-1"));
        }
    }

    [Fact]
    public void IsHealthy_WithRecentHeartbeat_ReturnsTrue()
    {
        // Arrange
        var location = new EdgeLocation
        {
            LocationId = "us-east-1",
            Name = "US East",
            Region = "North America",
            CountryCode = "US",
            Endpoint = "https://cdn.example.com",
            LastHeartbeat = DateTime.UtcNow.AddSeconds(-30),
            Health = new EdgeLocationHealth { IsHealthy = true }
        };

        // Act
        var isHealthy = location.IsHealthy();

        // Assert
        isHealthy.Should().BeTrue();
    }

    [Fact]
    public void IsHealthy_WithStaleHeartbeat_ReturnsFalse()
    {
        // Arrange
        var location = new EdgeLocation
        {
            LocationId = "us-east-1",
            Name = "US East",
            Region = "North America",
            CountryCode = "US",
            Endpoint = "https://cdn.example.com",
            LastHeartbeat = DateTime.UtcNow.AddMinutes(-5),  // 5 minutes ago
            Health = new EdgeLocationHealth { IsHealthy = true }
        };

        // Act
        var isHealthy = location.IsHealthy();

        // Assert
        isHealthy.Should().BeFalse();
    }
}
```

---

### Deployment Strategy Tests

**File:** `tests/HotSwap.CDN.Tests/Deployment/RegionalCanaryStrategyTests.cs`

```csharp
public class RegionalCanaryStrategyTests
{
    private readonly RegionalCanaryStrategy _strategy;
    private readonly Mock<IEdgeLocationClient> _mockClient;
    private readonly Mock<IPerformanceMonitor> _mockMonitor;

    public RegionalCanaryStrategyTests()
    {
        _mockClient = new Mock<IEdgeLocationClient>();
        _mockMonitor = new Mock<IPerformanceMonitor>();
        _strategy = new RegionalCanaryStrategy(_mockClient.Object, _mockMonitor.Object);
    }

    [Fact]
    public async Task DeployAsync_WithHealthyCanary_PromotesToFullRollout()
    {
        // Arrange
        var deployment = CreateTestDeployment();
        var locations = CreateTestEdgeLocations(10);

        _mockMonitor
            .Setup(m => m.CollectMetricsAsync(It.IsAny<List<EdgeLocation>>()))
            .ReturnsAsync(new PerformanceSnapshot
            {
                CacheHitRate = 92.0,
                ErrorRate = 0.05,
                P99LatencyMs = 45.0
            });

        // Act
        var result = await _strategy.DeployAsync(deployment, locations);

        // Assert
        result.Success.Should().BeTrue();
        result.SuccessfulLocations.Should().HaveCount(10);
        
        // Verify all phases were executed
        _mockClient.Verify(c => c.PushConfigurationAsync(It.IsAny<string>(), It.IsAny<EdgeLocation>()), 
            Times.Exactly(10));
    }

    [Fact]
    public async Task DeployAsync_WithUnhealthyCanary_RollsBack()
    {
        // Arrange
        var deployment = CreateTestDeployment();
        var locations = CreateTestEdgeLocations(10);

        _mockMonitor
            .Setup(m => m.CollectMetricsAsync(It.IsAny<List<EdgeLocation>>()))
            .ReturnsAsync(new PerformanceSnapshot
            {
                CacheHitRate = 50.0,  // Below threshold
                ErrorRate = 0.05,
                P99LatencyMs = 45.0
            });

        // Act
        var result = await _strategy.DeployAsync(deployment, locations);

        // Assert
        result.Success.Should().BeFalse();
        result.Errors.Should().Contain("Canary failed health checks");
        
        // Verify rollback was triggered
        _mockClient.Verify(c => c.RollbackConfigurationAsync(It.IsAny<EdgeLocation>()), 
            Times.AtLeastOnce());
    }

    [Theory]
    [InlineData(10, 1)]  // 10 locations, 10% = 1 location
    [InlineData(100, 10)] // 100 locations, 10% = 10 locations
    [InlineData(5, 1)]   // 5 locations, 10% = 0.5 rounded to 1
    public async Task DeployAsync_CanaryPercentage_DeploysCorrectCount(int totalLocations, int expectedCanaryCount)
    {
        // Arrange
        var deployment = CreateTestDeployment(canaryPercentage: 10);
        var locations = CreateTestEdgeLocations(totalLocations);

        var canaryDeployCount = 0;
        _mockClient
            .Setup(c => c.PushConfigurationAsync(It.IsAny<string>(), It.IsAny<EdgeLocation>()))
            .Callback(() => canaryDeployCount++)
            .ReturnsAsync(new PushResult { Success = true });

        _mockMonitor
            .Setup(m => m.CollectMetricsAsync(It.IsAny<List<EdgeLocation>>()))
            .ReturnsAsync(new PerformanceSnapshot
            {
                CacheHitRate = 92.0,
                ErrorRate = 0.05,
                P99LatencyMs = 45.0
            });

        // Act
        await _strategy.DeployAsync(deployment, locations);

        // Assert - First phase should deploy to canary count
        canaryDeployCount.Should().BeGreaterOrEqualTo(expectedCanaryCount);
    }

    private Deployment CreateTestDeployment(int canaryPercentage = 10)
    {
        return new Deployment
        {
            DeploymentId = Guid.NewGuid().ToString(),
            ConfigurationId = "config-123",
            ConfigurationVersion = "1.0.0",
            Strategy = DeploymentStrategy.RegionalCanary,
            DeployedBy = "test@example.com",
            CanaryConfig = new CanaryConfig
            {
                InitialPercentage = canaryPercentage,
                MonitorDuration = TimeSpan.FromSeconds(1),  // Short for testing
                AutoPromote = true
            },
            RollbackConfig = new RollbackConfig
            {
                AutoRollback = true,
                CacheHitRateThreshold = 80.0,
                ErrorRateThreshold = 1.0,
                P99LatencyThresholdMs = 200.0
            }
        };
    }

    private List<EdgeLocation> CreateTestEdgeLocations(int count)
    {
        return Enumerable.Range(1, count)
            .Select(i => new EdgeLocation
            {
                LocationId = $"test-{i}",
                Name = $"Test Location {i}",
                Region = "Test Region",
                CountryCode = "US",
                Endpoint = $"https://cdn-test-{i}.example.com"
            })
            .ToList();
    }
}
```

**Estimated Tests:** 20+ tests per strategy (×5 strategies = 100 tests)

---

### Configuration Validator Tests

**File:** `tests/HotSwap.CDN.Tests/Validation/ConfigurationValidatorTests.cs`

```csharp
public class ConfigurationValidatorTests
{
    private readonly ConfigurationValidator _validator;

    public ConfigurationValidatorTests()
    {
        _validator = new ConfigurationValidator();
    }

    [Fact]
    public async Task ValidateAsync_CacheRule_WithValidTTL_PassesValidation()
    {
        // Arrange
        var configuration = new Configuration
        {
            ConfigurationId = "cfg-1",
            Name = "test-cache",
            Type = ConfigurationType.CacheRule,
            Content = "{\"pathPattern\":\"/assets/*\",\"ttl\":3600,\"cacheControl\":\"public, max-age=3600\"}",
            SchemaVersion = "1.0",
            CreatedBy = "admin@example.com"
        };

        // Act
        var result = await _validator.ValidateAsync(configuration);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData(-1, "TTL must be between 0 and 31536000")]
    [InlineData(31536001, "TTL must be between 0 and 31536000")]
    public async Task ValidateAsync_CacheRule_WithInvalidTTL_FailsValidation(int ttl, string expectedError)
    {
        // Arrange
        var configuration = new Configuration
        {
            ConfigurationId = "cfg-1",
            Name = "test-cache",
            Type = ConfigurationType.CacheRule,
            Content = $"{{\"pathPattern\":\"/assets/*\",\"ttl\":{ttl}}}",
            SchemaVersion = "1.0",
            CreatedBy = "admin@example.com"
        };

        // Act
        var result = await _validator.ValidateAsync(configuration);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Message.Contains(expectedError));
    }

    [Fact]
    public async Task ValidateAsync_WithOverlappingPathPattern_ReturnsWarning()
    {
        // Arrange - Assume existing configuration with /assets/*
        var existingConfig = new Configuration
        {
            ConfigurationId = "cfg-existing",
            Name = "existing-cache",
            Type = ConfigurationType.CacheRule,
            Content = "{\"pathPattern\":\"/assets/*\"}",
            SchemaVersion = "1.0",
            CreatedBy = "admin@example.com"
        };

        // New configuration with overlapping path
        var newConfig = new Configuration
        {
            ConfigurationId = "cfg-new",
            Name = "new-cache",
            Type = ConfigurationType.CacheRule,
            Content = "{\"pathPattern\":\"/assets/images/*\"}",
            SchemaVersion = "1.0",
            CreatedBy = "admin@example.com"
        };

        // Act
        var result = await _validator.ValidateAsync(newConfig);

        // Assert
        result.IsValid.Should().BeTrue();  // Not invalid, just warning
        result.Warnings.Should().Contain(w => w.Message.Contains("overlaps"));
    }
}
```

**Estimated Tests:** 30+ validation tests

---

## Integration Testing

**Target:** 60+ integration tests

### Scope

Test interactions between components with real dependencies (database, cache, external services).

**Integration Points to Test:**
- API → Orchestrator → Repository (full stack)
- Deployment strategies with real edge location simulators
- Metrics collection and storage
- Auto-rollback triggered by performance monitor
- Configuration versioning with database

### API Integration Tests

**File:** `tests/HotSwap.CDN.IntegrationTests/API/ConfigurationsControllerTests.cs`

```csharp
public class ConfigurationsControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly WebApplicationFactory<Program> _factory;

    public ConfigurationsControllerTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateConfiguration_WithValidData_Returns201Created()
    {
        // Arrange
        var configuration = new
        {
            name = "test-cache",
            type = "CacheRule",
            content = new
            {
                pathPattern = "/assets/*",
                ttl = 3600
            },
            schemaVersion = "1.0"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/configurations", configuration);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        
        var created = await response.Content.ReadFromJsonAsync<Configuration>();
        created.Should().NotBeNull();
        created!.ConfigurationId.Should().NotBeNullOrEmpty();
        created.Name.Should().Be("test-cache");
    }

    [Fact]
    public async Task GetConfigurations_ReturnsAllConfigurations()
    {
        // Arrange - Create test configurations
        await CreateTestConfigurationAsync("config-1");
        await CreateTestConfigurationAsync("config-2");

        // Act
        var response = await _client.GetAsync("/api/v1/configurations");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<ConfigurationListResponse>();
        result.Should().NotBeNull();
        result!.Configurations.Should().HaveCountGreaterOrEqualTo(2);
    }

    [Fact]
    public async Task DeployConfiguration_WithRegionalCanary_SucceedsAfterCanaryValidation()
    {
        // Arrange - Create configuration
        var configId = await CreateTestConfigurationAsync("canary-test");

        var deployment = new
        {
            configurationId = configId,
            strategy = "RegionalCanary",
            targetRegions = new[] { "North America" },
            canaryConfig = new
            {
                initialPercentage = 10,
                monitorDuration = "PT30S",
                autoPromote = true
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/deployments", deployment);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        
        var deploymentResult = await response.Content.ReadFromJsonAsync<Deployment>();
        deploymentResult.Should().NotBeNull();
        deploymentResult!.Status.Should().Be(DeploymentStatus.Pending);

        // Wait for canary to complete
        await Task.Delay(TimeSpan.FromSeconds(35));

        // Verify deployment completed
        var statusResponse = await _client.GetAsync($"/api/v1/deployments/{deploymentResult.DeploymentId}");
        var status = await statusResponse.Content.ReadFromJsonAsync<Deployment>();
        status!.Status.Should().BeOneOf(DeploymentStatus.Completed, DeploymentStatus.InProgress);
    }

    private async Task<string> CreateTestConfigurationAsync(string name)
    {
        var configuration = new
        {
            name,
            type = "CacheRule",
            content = new { pathPattern = $"/{name}/*", ttl = 3600 },
            schemaVersion = "1.0"
        };

        var response = await _client.PostAsJsonAsync("/api/v1/configurations", configuration);
        var created = await response.Content.ReadFromJsonAsync<Configuration>();
        return created!.ConfigurationId;
    }
}
```

**Estimated Tests:** 60+ integration tests

---

## End-to-End Testing

**Target:** 20+ E2E tests

### Scope

Test complete user workflows from API to edge locations.

**E2E Scenarios:**
1. Create configuration → Validate → Approve → Deploy → Verify on edge
2. Deploy with canary → Monitor → Auto-promote → Verify 100% rollout
3. Deploy with issue → Detect degradation → Auto-rollback → Verify rollback
4. Create configuration → Update (new version) → Deploy new version → Verify
5. Multi-region deployment → Monitor each region → Verify all regions

### E2E Test Example

**File:** `tests/HotSwap.CDN.E2ETests/Scenarios/CanaryDeploymentScenarioTests.cs`

```csharp
[Collection("E2E")]
public class CanaryDeploymentScenarioTests : IClassFixture<E2ETestFixture>
{
    private readonly E2ETestFixture _fixture;

    public CanaryDeploymentScenarioTests(E2ETestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CompleteCanaryDeploymentWorkflow_SuccessfullyDeploysConfiguration()
    {
        // Step 1: Create configuration
        var configId = await _fixture.CreateConfigurationAsync(new
        {
            name = "e2e-cache-test",
            type = "CacheRule",
            content = new { pathPattern = "/test/*", ttl = 7200 },
            schemaVersion = "1.0"
        });

        // Step 2: Validate configuration
        var validationResult = await _fixture.ValidateConfigurationAsync(configId);
        validationResult.IsValid.Should().BeTrue();

        // Step 3: Approve configuration (admin)
        await _fixture.ApproveConfigurationAsync(configId);

        // Step 4: Deploy with canary strategy
        var deploymentId = await _fixture.DeployConfigurationAsync(new
        {
            configurationId = configId,
            strategy = "RegionalCanary",
            targetRegions = new[] { "North America" },
            canaryConfig = new
            {
                initialPercentage = 10,
                monitorDuration = "PT1M",
                autoPromote = true
            }
        });

        // Step 5: Wait for canary phase
        await Task.Delay(TimeSpan.FromSeconds(70));

        // Step 6: Verify canary metrics are healthy
        var metrics = await _fixture.GetDeploymentMetricsAsync(deploymentId);
        metrics.CacheHitRate.Should().BeGreaterThan(80.0);

        // Step 7: Wait for full rollout
        await Task.Delay(TimeSpan.FromSeconds(70));

        // Step 8: Verify deployment completed successfully
        var deployment = await _fixture.GetDeploymentAsync(deploymentId);
        deployment.Status.Should().Be(DeploymentStatus.Completed);
        deployment.ProgressPercentage.Should().Be(100);

        // Step 9: Verify configuration active on all edge locations
        var edgeLocations = await _fixture.GetEdgeLocationsByRegionAsync("North America");
        foreach (var location in edgeLocations)
        {
            var activeConfigs = await _fixture.GetActiveConfigurationsAsync(location.LocationId);
            activeConfigs.Should().Contain(configId);
        }
    }
}
```

**Estimated Tests:** 20+ E2E tests

---

## Performance Testing

### Load Testing

**Tool:** k6, Apache JMeter, or NBomber

**Scenarios:**
1. Configuration API throughput (1000 req/sec)
2. Deployment concurrency (50 simultaneous deployments)
3. Metrics collection overhead (minimal impact)
4. Edge location scaling (100+ edge locations)

### Performance Test Example

```javascript
// k6 load test script
import http from 'k6/http';
import { check } from 'k6';

export let options = {
  vus: 100,
  duration: '5m',
  thresholds: {
    http_req_duration: ['p(95)<200', 'p(99)<500'],
    http_req_failed: ['rate<0.01'],
  },
};

export default function () {
  const url = 'https://api.example.com/api/v1/configurations';
  const token = 'Bearer ...';

  const response = http.get(url, {
    headers: { 'Authorization': token },
  });

  check(response, {
    'status is 200': (r) => r.status === 200,
    'response time < 200ms': (r) => r.timings.duration < 200,
  });
}
```

---

## Test Organization

### Project Structure

```
tests/
├── HotSwap.CDN.Tests/                 # Unit tests
│   ├── Domain/
│   │   ├── ConfigurationTests.cs
│   │   ├── EdgeLocationTests.cs
│   │   └── DeploymentTests.cs
│   ├── Deployment/
│   │   ├── DirectDeploymentStrategyTests.cs
│   │   ├── RegionalCanaryStrategyTests.cs
│   │   └── BlueGreenDeploymentStrategyTests.cs
│   ├── Validation/
│   │   └── ConfigurationValidatorTests.cs
│   └── Monitoring/
│       └── PerformanceMonitorTests.cs
├── HotSwap.CDN.IntegrationTests/      # Integration tests
│   ├── API/
│   │   ├── ConfigurationsControllerTests.cs
│   │   └── DeploymentsControllerTests.cs
│   └── Persistence/
│       └── PostgreSQLRepositoryTests.cs
└── HotSwap.CDN.E2ETests/               # End-to-end tests
    └── Scenarios/
        ├── CanaryDeploymentScenarioTests.cs
        └── MultiRegionDeploymentTests.cs
```

---

## TDD Workflow

### Red-Green-Refactor Cycle

**Step 1: Red (Write Failing Test)**
```csharp
[Fact]
public async Task DeployAsync_WithInvalidConfiguration_ThrowsException()
{
    // Arrange
    var deployment = CreateInvalidDeployment();

    // Act
    Func<Task> act = async () => await _strategy.DeployAsync(deployment, locations);

    // Assert
    await act.Should().ThrowAsync<ValidationException>();
}
```

**Step 2: Green (Make Test Pass)**
```csharp
public async Task<DeploymentResult> DeployAsync(Deployment deployment, List<EdgeLocation> locations)
{
    if (!deployment.IsValid(out var errors))
    {
        throw new ValidationException(string.Join(", ", errors));
    }

    // ... rest of implementation
}
```

**Step 3: Refactor (Improve Code)**
```csharp
// Extract validation to separate method
private void ValidateDeployment(Deployment deployment)
{
    if (!deployment.IsValid(out var errors))
    {
        throw new ValidationException(string.Join(", ", errors));
    }
}
```

---

## CI/CD Integration

### GitHub Actions Workflow

```yaml
name: CI/CD Pipeline

on:
  push:
    branches: [ main, develop ]
  pull_request:
    branches: [ main ]

jobs:
  test:
    runs-on: ubuntu-latest

    services:
      postgres:
        image: postgres:15
        env:
          POSTGRES_PASSWORD: test_password
        options: >-
          --health-cmd pg_isready
          --health-interval 10s
          --health-timeout 5s
          --health-retries 5

      redis:
        image: redis:7
        options: >-
          --health-cmd "redis-cli ping"
          --health-interval 10s
          --health-timeout 5s
          --health-retries 5

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

      - name: Run Unit Tests
        run: dotnet test tests/HotSwap.CDN.Tests/ --no-build --verbosity normal --collect:"XPlat Code Coverage"

      - name: Run Integration Tests
        run: dotnet test tests/HotSwap.CDN.IntegrationTests/ --no-build --verbosity normal
        env:
          ConnectionStrings__PostgreSQL: "Host=localhost;Database=cdn_test;Username=postgres;Password=test_password"
          ConnectionStrings__Redis: "localhost:6379"

      - name: Generate Coverage Report
        uses: codecov/codecov-action@v3
        with:
          files: ./coverage.xml
          flags: unittests
          fail_ci_if_error: true

      - name: Check Coverage Threshold
        run: |
          COVERAGE=$(grep -oP 'line-rate="\K[^"]+' coverage.xml | head -1)
          if (( $(echo "$COVERAGE < 0.85" | bc -l) )); then
            echo "Coverage $COVERAGE is below 85%"
            exit 1
          fi
```

---

## Best Practices

### 1. Test Naming Convention

Use descriptive test names that explain the scenario:
```csharp
[Fact]
public async Task DeployAsync_WhenCanaryFailsHealthCheck_RollsBackAutomatically()
```

### 2. Arrange-Act-Assert Pattern

Always structure tests with clear AAA sections:
```csharp
// Arrange
var deployment = CreateTestDeployment();

// Act
var result = await _strategy.DeployAsync(deployment, locations);

// Assert
result.Success.Should().BeFalse();
```

### 3. Use Test Fixtures for Shared Setup

```csharp
public class DeploymentStrategyTestFixture : IDisposable
{
    public Mock<IEdgeLocationClient> MockClient { get; }
    public Mock<IPerformanceMonitor> MockMonitor { get; }

    public DeploymentStrategyTestFixture()
    {
        MockClient = new Mock<IEdgeLocationClient>();
        MockMonitor = new Mock<IPerformanceMonitor>();
    }

    public void Dispose()
    {
        // Cleanup
    }
}
```

### 4. Test Edge Cases and Error Paths

Don't just test happy paths:
```csharp
[Theory]
[InlineData(0)]    // No edge locations
[InlineData(1)]    // Single edge location
[InlineData(1000)] // Many edge locations
public async Task DeployAsync_WithVariousLocationCounts_HandlesCorrectly(int count)
{
    // Test with different edge location counts
}
```

### 5. Use Realistic Test Data

Create test data that resembles production scenarios:
```csharp
private Configuration CreateRealisticConfiguration()
{
    return new Configuration
    {
        ConfigurationId = Guid.NewGuid().ToString(),
        Name = "production-cache-rule",
        Type = ConfigurationType.CacheRule,
        Content = JsonSerializer.Serialize(new
        {
            pathPattern = "/api/v1/*",
            ttl = 3600,
            cacheControl = "public, max-age=3600",
            varyHeaders = new[] { "Accept-Encoding", "Accept-Language" }
        }),
        SchemaVersion = "1.0",
        CreatedBy = "admin@example.com"
    };
}
```

---

## Success Criteria

**Testing is considered complete when:**

1. ✅ 320+ unit tests written and passing
2. ✅ 60+ integration tests written and passing
3. ✅ 20+ E2E tests written and passing
4. ✅ 85%+ code coverage achieved
5. ✅ All performance tests pass
6. ✅ CI/CD pipeline green
7. ✅ All critical paths tested
8. ✅ Edge cases covered
9. ✅ Error handling verified
10. ✅ Documentation updated

---

**Document Version:** 1.0.0
**Last Updated:** 2025-11-23
**Next Review:** After each sprint
