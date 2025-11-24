# Testing Strategy - Multi-Tenant Configuration Service

**Version:** 1.0.0
**Target Coverage:** 85%+
**Test Count:** 300+ tests
**Framework:** xUnit, Moq, FluentAssertions

---

## Overview

The Multi-Tenant Configuration Service follows **Test-Driven Development (TDD)** with comprehensive test coverage across all layers. This document outlines the testing strategy, test organization, and best practices.

### Test Pyramid

```
                 ▲
                / \
               /E2E\           7% - End-to-End Tests (20 tests)
              /_____\
             /       \
            /Integration\      13% - Integration Tests (40 tests)
           /___________\
          /             \
         /  Unit Tests   \     80% - Unit Tests (240 tests)
        /_________________\
```

**Total Tests:** 300+ tests across all layers

---

## Table of Contents

1. [Unit Testing](#unit-testing)
2. [Integration Testing](#integration-testing)
3. [End-to-End Testing](#end-to-end-testing)
4. [Performance Testing](#performance-testing)
5. [Test Organization](#test-organization)
6. [TDD Workflow](#tdd-workflow)
7. [CI/CD Integration](#cicd-integration)

---

## Unit Testing

**Target:** 240+ unit tests, 85%+ code coverage

### Scope

Test individual components in isolation with mocked dependencies.

**Components to Test:**
- Domain models (validation, business logic)
- Deployment strategies (deployment algorithms)
- Validation engine (configuration validation)
- Configuration caching
- Audit logging

### Domain Models Tests

**File:** `tests/HotSwap.MultiTenantConfig.Tests/Domain/TenantTests.cs`

```csharp
public class TenantTests
{
    [Fact]
    public void Tenant_WithValidData_PassesValidation()
    {
        // Arrange
        var tenant = new Tenant
        {
            TenantId = "acme-corp",
            Name = "ACME Corporation",
            Tier = TenantTier.Enterprise,
            MaxConfigurations = 1000,
            ContactEmail = "admin@acme.com"
        };

        // Act
        var isValid = tenant.IsValid(out var errors);

        // Assert
        isValid.Should().BeTrue();
        errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("", "ACME Corp", "TenantId is required")]
    [InlineData("a", "ACME Corp", "TenantId must be 3-50 alphanumeric")]
    [InlineData("acme@corp", "ACME Corp", "TenantId must be 3-50 alphanumeric")]
    [InlineData("acme-corp", "", "Name is required")]
    public void Tenant_WithInvalidData_FailsValidation(
        string tenantId, string name, string expectedError)
    {
        // Arrange
        var tenant = new Tenant
        {
            TenantId = tenantId,
            Name = name
        };

        // Act
        var isValid = tenant.IsValid(out var errors);

        // Assert
        isValid.Should().BeFalse();
        errors.Should().Contain(e => e.Contains(expectedError));
    }

    [Fact]
    public void IsActive_WhenStatusActive_ReturnsTrue()
    {
        // Arrange
        var tenant = new Tenant
        {
            TenantId = "acme-corp",
            Name = "ACME",
            Status = TenantStatus.Active
        };

        // Act
        var isActive = tenant.IsActive();

        // Assert
        isActive.Should().BeTrue();
    }

    [Fact]
    public void CanCreateConfig_WhenUnderQuota_ReturnsTrue()
    {
        // Arrange
        var tenant = new Tenant
        {
            TenantId = "acme-corp",
            Name = "ACME",
            MaxConfigurations = 100
        };

        // Act
        var canCreate = tenant.CanCreateConfig(currentConfigCount: 50);

        // Assert
        canCreate.Should().BeTrue();
    }

    [Fact]
    public void CanCreateConfig_WhenOverQuota_ReturnsFalse()
    {
        // Arrange
        var tenant = new Tenant
        {
            TenantId = "acme-corp",
            Name = "ACME",
            MaxConfigurations = 100
        };

        // Act
        var canCreate = tenant.CanCreateConfig(currentConfigCount: 100);

        // Assert
        canCreate.Should().BeFalse();
    }
}
```

**Estimated Tests:** 25+ tests for Tenant model

**File:** `tests/HotSwap.MultiTenantConfig.Tests/Domain/ConfigurationTests.cs`

```csharp
public class ConfigurationTests
{
    [Fact]
    public void Configuration_WithValidBooleanValue_PassesValidation()
    {
        // Arrange
        var config = new Configuration
        {
            ConfigId = Guid.NewGuid().ToString(),
            TenantId = "acme-corp",
            Key = "feature.new_dashboard",
            Value = "true",
            Type = ConfigValueType.Boolean,
            Environment = ConfigEnvironment.Production
        };

        // Act
        var isValid = config.IsValid(out var errors);

        // Assert
        isValid.Should().BeTrue();
        errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("invalid", ConfigValueType.Boolean, "Value must be a valid boolean")]
    [InlineData("abc", ConfigValueType.Number, "Value must be a valid number")]
    [InlineData("{invalid-json", ConfigValueType.JSON, "Value must be valid JSON")]
    public void Configuration_WithInvalidValueType_FailsValidation(
        string value, ConfigValueType type, string expectedError)
    {
        // Arrange
        var config = new Configuration
        {
            ConfigId = Guid.NewGuid().ToString(),
            TenantId = "acme-corp",
            Key = "test.key",
            Value = value,
            Type = type,
            Environment = ConfigEnvironment.Development
        };

        // Act
        var isValid = config.IsValid(out var errors);

        // Assert
        isValid.Should().BeFalse();
        errors.Should().Contain(e => e.Contains(expectedError));
    }

    [Fact]
    public void GetValue_WithBooleanType_ReturnsTypedValue()
    {
        // Arrange
        var config = new Configuration
        {
            ConfigId = Guid.NewGuid().ToString(),
            TenantId = "acme-corp",
            Key = "feature.enabled",
            Value = "true",
            Type = ConfigValueType.Boolean
        };

        // Act
        var value = config.GetValue<bool>();

        // Assert
        value.Should().BeTrue();
    }
}
```

**Estimated Tests:** 30+ tests for Configuration model

---

### Deployment Strategy Tests

**File:** `tests/HotSwap.MultiTenantConfig.Tests/Strategies/CanaryDeploymentStrategyTests.cs`

```csharp
public class CanaryDeploymentStrategyTests
{
    private readonly CanaryDeploymentStrategy _strategy;
    private readonly Mock<IConfigurationApplier> _mockApplier;
    private readonly Mock<IMetricsCollector> _mockMetrics;

    public CanaryDeploymentStrategyTests()
    {
        _mockApplier = new Mock<IConfigurationApplier>();
        _mockMetrics = new Mock<IMetricsCollector>();
        _strategy = new CanaryDeploymentStrategy(_mockApplier.Object, _mockMetrics.Object);
    }

    [Fact]
    public async Task DeployAsync_WithMultipleTenants_DeploysInStages()
    {
        // Arrange
        var config = CreateTestConfig();
        var tenants = CreateTestTenants(100);

        _mockMetrics.Setup(m => m.CollectMetricsAsync(It.IsAny<List<Tenant>>()))
            .ReturnsAsync(new DeploymentMetrics { CurrentErrorRate = 0.5 });

        // Act
        var result = await _strategy.DeployAsync(config, tenants);

        // Assert
        result.Success.Should().BeTrue();

        // Verify 10%, 25%, 50%, 100% deployments
        _mockApplier.Verify(a => a.ApplyConfigurationAsync(config, It.IsAny<Tenant>()),
            Times.Exactly(100));
    }

    [Fact]
    public async Task DeployAsync_WhenErrorRateExceeded_RollsBack()
    {
        // Arrange
        var config = CreateTestConfig();
        var tenants = CreateTestTenants(100);

        _mockMetrics.SetupSequence(m => m.CollectMetricsAsync(It.IsAny<List<Tenant>>()))
            .ReturnsAsync(new DeploymentMetrics { BaselineErrorRate = 0.5, CurrentErrorRate = 0.5 })
            .ReturnsAsync(new DeploymentMetrics { BaselineErrorRate = 0.5, CurrentErrorRate = 5.0 }); // 10x increase!

        // Act
        var result = await _strategy.DeployAsync(config, tenants);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Metrics threshold breached");

        // Verify rollback was called
        _mockApplier.Verify(a => a.RollbackAsync(config, It.IsAny<List<Tenant>>()),
            Times.Once());
    }

    [Fact]
    public async Task DeployAsync_WithNoTenants_ReturnsFailure()
    {
        // Arrange
        var config = CreateTestConfig();
        var tenants = new List<Tenant>();

        // Act
        var result = await _strategy.DeployAsync(config, tenants);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("No tenants available");
    }
}
```

**Estimated Tests:** 20+ tests per strategy (×4 strategies = 80 tests)

---

### Configuration Validation Tests

**File:** `tests/HotSwap.MultiTenantConfig.Tests/Infrastructure/ConfigValidationEngineTests.cs`

```csharp
public class ConfigValidationEngineTests
{
    private readonly ConfigValidationEngine _engine;

    public ConfigValidationEngineTests()
    {
        _engine = new ConfigValidationEngine();
    }

    [Fact]
    public async Task ValidateAsync_WithValidJSON_ReturnsSuccess()
    {
        // Arrange
        var config = new Configuration
        {
            ConfigId = "cfg-123",
            TenantId = "acme-corp",
            Key = "api.settings",
            Value = "{\"timeout\": 30, \"retries\": 3}",
            Type = ConfigValueType.JSON
        };

        // Act
        var result = await _engine.ValidateAsync(config);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateAsync_WithInvalidJSON_ReturnsErrors()
    {
        // Arrange
        var config = new Configuration
        {
            ConfigId = "cfg-123",
            TenantId = "acme-corp",
            Key = "api.settings",
            Value = "{invalid-json",
            Type = ConfigValueType.JSON
        };

        // Act
        var result = await _engine.ValidateAsync(config);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("valid JSON"));
    }

    [Theory]
    [InlineData("feature.new-ui", true)]
    [InlineData("database.connection_string", true)]
    [InlineData("api.v1.timeout", true)]
    [InlineData("invalid key", false)]
    [InlineData("key@invalid", false)]
    public async Task ValidateAsync_WithVariousKeys_ValidatesCorrectly(
        string key, bool shouldBeValid)
    {
        // Arrange
        var config = new Configuration
        {
            ConfigId = "cfg-123",
            TenantId = "acme-corp",
            Key = key,
            Value = "test",
            Type = ConfigValueType.String
        };

        // Act
        var result = await _engine.ValidateAsync(config);

        // Assert
        result.IsValid.Should().Be(shouldBeValid);
    }
}
```

**Estimated Tests:** 25+ tests for validation engine

---

## Integration Testing

**Target:** 40+ integration tests

### Scope

Test multiple components working together with real dependencies (Redis, PostgreSQL).

**Test Scenarios:**
1. Configuration CRUD with database persistence
2. Deployment workflow end-to-end
3. Approval workflow integration
4. Cache invalidation on updates
5. Audit log creation
6. Rollback functionality

### End-to-End Configuration Flow Test

**File:** `tests/HotSwap.MultiTenantConfig.IntegrationTests/ConfigurationFlowTests.cs`

```csharp
[Collection("Integration")]
public class ConfigurationFlowTests : IClassFixture<TestServerFixture>
{
    private readonly HttpClient _client;
    private readonly TestServerFixture _fixture;

    public ConfigurationFlowTests(TestServerFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.CreateClient();
    }

    [Fact]
    public async Task EndToEndFlow_CreateUpdatePromoteApprove_WorksCorrectly()
    {
        // Arrange - Create tenant
        var tenantRequest = new
        {
            tenantId = "test-tenant",
            name = "Test Tenant",
            tier = "Enterprise"
        };
        await _client.PostAsJsonAsync("/api/v1/tenants", tenantRequest);

        // Act - Create configuration
        var configRequest = new
        {
            tenantId = "test-tenant",
            key = "feature.test",
            value = "true",
            type = "Boolean",
            environment = "Development"
        };
        var createResponse = await _client.PostAsJsonAsync("/api/v1/configs", configRequest);
        var config = await createResponse.Content.ReadFromJsonAsync<Configuration>();

        // Act - Promote to production (requires approval)
        var promoteRequest = new
        {
            targetEnvironment = "Production",
            description = "Test promotion",
            requiresApproval = true
        };
        var promoteResponse = await _client.PostAsJsonAsync(
            $"/api/v1/configs/{config.ConfigId}/promote", promoteRequest);
        var approval = await promoteResponse.Content.ReadFromJsonAsync<ApprovalRequest>();

        // Assert - Approval created
        approval.Should().NotBeNull();
        approval.Status.Should().Be(ApprovalStatus.Pending);

        // Act - Approve request (Level 1)
        var approveRequest = new { comments = "Approved by manager" };
        await _client.PostAsJsonAsync($"/api/v1/approvals/{approval.ApprovalId}/approve",
            approveRequest);

        // Act - Approve request (Level 2 - final)
        await _client.PostAsJsonAsync($"/api/v1/approvals/{approval.ApprovalId}/approve",
            new { comments = "Approved by compliance" });

        // Assert - Deployment started
        var updatedApproval = await _client.GetFromJsonAsync<ApprovalRequest>(
            $"/api/v1/approvals/{approval.ApprovalId}");
        updatedApproval.Status.Should().Be(ApprovalStatus.Approved);
        updatedApproval.DeploymentId.Should().NotBeNull();

        // Assert - Configuration deployed to production
        var productionConfigs = await _client.GetFromJsonAsync<ConfigListResponse>(
            $"/api/v1/configs/tenant/test-tenant?environment=Production");
        productionConfigs.Configs.Should().Contain(c => c.Key == "feature.test");
    }
}
```

**Estimated Tests:** 40+ integration tests

---

## End-to-End Testing

**Target:** 20+ E2E tests

### Scope

Test complete user workflows from API to database with all real components.

**E2E Test Scenarios:**
1. Complete tenant onboarding (create tenant → create configs → deploy)
2. Configuration lifecycle (create → update → promote → approve → deploy → rollback)
3. Multi-tenant isolation (ensure tenant A can't access tenant B's configs)
4. Deployment strategy workflows (Canary, Blue-Green, Rolling)

### E2E Test Example

```csharp
[Collection("E2E")]
public class TenantOnboardingE2ETests : IClassFixture<E2ETestFixture>
{
    [Fact]
    public async Task TenantOnboarding_CompleteWorkflow_Succeeds()
    {
        // Arrange - Create tenant
        var tenant = await CreateTenantAsync("onboarding-test", "Onboarding Test Corp");

        // Act - Create multiple configurations
        await CreateConfigAsync(tenant.TenantId, "database.host", "db.example.com");
        await CreateConfigAsync(tenant.TenantId, "api.timeout", "30");
        await CreateConfigAsync(tenant.TenantId, "feature.beta", "false");

        // Act - Promote all to production
        var configs = await GetTenantConfigsAsync(tenant.TenantId);
        foreach (var config in configs)
        {
            await PromoteConfigAsync(config.ConfigId, "Production");
        }

        // Assert - All configs promoted and deployed
        var productionConfigs = await GetTenantConfigsAsync(
            tenant.TenantId, environment: "Production");
        productionConfigs.Should().HaveCount(3);

        // Assert - Audit trail exists
        var auditLogs = await GetAuditLogsAsync(tenant.TenantId);
        auditLogs.Should().Contain(l => l.EventType == "Created");
        auditLogs.Should().Contain(l => l.EventType == "Promoted");
    }
}
```

---

## Performance Testing

**Target:** Meet SLA requirements

### Performance Test Scenarios

**Scenario 1: Configuration Retrieval Latency**

```csharp
[Fact]
public async Task ConfigRetrieval_P99Latency_LessThan10ms()
{
    // Arrange
    var tenantId = "perf-test-tenant";
    var sampleSize = 1000;
    var latencies = new List<double>();

    // Act
    for (int i = 0; i < sampleSize; i++)
    {
        var stopwatch = Stopwatch.StartNew();
        await _client.GetAsync($"/api/v1/configs/tenant/{tenantId}");
        stopwatch.Stop();
        latencies.Add(stopwatch.Elapsed.TotalMilliseconds);
    }

    // Assert
    var p99 = latencies.OrderBy(x => x).ElementAt((int)(sampleSize * 0.99));
    p99.Should().BeLessThan(10);
}
```

**Scenario 2: Canary Deployment Duration**

```csharp
[Fact]
public async Task CanaryDeployment_100Tenants_CompletesIn30Minutes()
{
    // Arrange
    var config = CreateTestConfig();
    var tenants = CreateTestTenants(100);

    // Act
    var stopwatch = Stopwatch.StartNew();
    var result = await _strategy.DeployAsync(config, tenants);
    stopwatch.Stop();

    // Assert
    result.Success.Should().BeTrue();
    stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromMinutes(30));
}
```

---

## Test Organization

### Project Structure

```
tests/
├── HotSwap.MultiTenantConfig.Tests/                    # Unit tests
│   ├── Domain/
│   │   ├── TenantTests.cs                             # 25 tests
│   │   ├── ConfigurationTests.cs                       # 30 tests
│   │   ├── ConfigVersionTests.cs                       # 20 tests
│   │   └── ApprovalRequestTests.cs                     # 25 tests
│   ├── Strategies/
│   │   ├── DirectDeploymentStrategyTests.cs            # 15 tests
│   │   ├── CanaryDeploymentStrategyTests.cs            # 25 tests
│   │   ├── BlueGreenDeploymentStrategyTests.cs         # 20 tests
│   │   └── RollingDeploymentStrategyTests.cs           # 20 tests
│   ├── Infrastructure/
│   │   ├── ConfigValidationEngineTests.cs              # 25 tests
│   │   ├── ConfigCacheTests.cs                         # 20 tests
│   │   └── AuditLogServiceTests.cs                     # 20 tests
│   └── Orchestrator/
│       ├── ConfigOrchestratorTests.cs                  # 25 tests
│       └── ApprovalWorkflowTests.cs                    # 20 tests
├── HotSwap.MultiTenantConfig.IntegrationTests/        # Integration tests
│   ├── ConfigurationFlowTests.cs                       # 15 tests
│   ├── DeploymentWorkflowTests.cs                      # 15 tests
│   └── ApprovalWorkflowTests.cs                        # 10 tests
└── HotSwap.MultiTenantConfig.E2ETests/                # End-to-end tests
    ├── TenantOnboardingTests.cs                        # 10 tests
    └── DeploymentStrategyTests.cs                      # 10 tests
```

---

## TDD Workflow

Follow Red-Green-Refactor cycle for ALL code changes.

### Example: Implementing Canary Deployment

**Step 1: 🔴 RED - Write Failing Test**

```csharp
[Fact]
public async Task DeployAsync_WithMultipleTenants_DeploysIn10PercentStages()
{
    // Arrange
    var strategy = new CanaryDeploymentStrategy(_mockApplier.Object, _mockMetrics.Object);
    var config = CreateTestConfig();
    var tenants = CreateTestTenants(100);

    // Act
    var result = await strategy.DeployAsync(config, tenants);

    // Assert
    result.Success.Should().BeTrue();
    // Verify 10% stage deployed
    _mockApplier.Verify(a => a.ApplyConfigurationAsync(config, It.IsAny<Tenant>()),
        Times.AtLeast(10));
}
```

Run test: **FAILS** ❌ (CanaryDeploymentStrategy not implemented)

**Step 2: 🟢 GREEN - Minimal Implementation**

```csharp
public async Task<DeploymentResult> DeployAsync(Configuration config, List<Tenant> tenants)
{
    // Simple implementation to make test pass
    var stage1Tenants = tenants.Take(10).ToList();
    foreach (var tenant in stage1Tenants)
    {
        await _applier.ApplyConfigurationAsync(config, tenant);
    }
    return DeploymentResult.Success(stage1Tenants);
}
```

Run test: **PASSES** ✓

**Step 3: 🔵 REFACTOR - Full Implementation**

```csharp
public async Task<DeploymentResult> DeployAsync(Configuration config, List<Tenant> tenants)
{
    var stages = new[] { 10, 25, 50, 100 };

    foreach (var percentage in stages)
    {
        var stageTenants = tenants.Take((int)Math.Ceiling(tenants.Count * percentage / 100.0)).ToList();
        foreach (var tenant in stageTenants)
        {
            await _applier.ApplyConfigurationAsync(config, tenant);
        }

        await Task.Delay(TimeSpan.FromMinutes(5));
        var metrics = await _metrics.CollectMetricsAsync(stageTenants);

        if (metrics.HasIssues())
        {
            await RollbackAsync(config, stageTenants);
            return DeploymentResult.Failure("Metrics threshold breached");
        }
    }

    return DeploymentResult.Success(tenants);
}
```

Run test: **PASSES** ✓

---

## CI/CD Integration

### GitHub Actions Workflow

```yaml
name: Multi-Tenant Config Tests

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
        run: dotnet test tests/HotSwap.MultiTenantConfig.Tests/ --no-build --verbosity normal

      - name: Run integration tests
        run: dotnet test tests/HotSwap.MultiTenantConfig.IntegrationTests/ --no-build --verbosity normal

      - name: Run E2E tests
        run: dotnet test tests/HotSwap.MultiTenantConfig.E2ETests/ --no-build --verbosity normal

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
- API: 80%+ (integration tests)

**Measure Coverage:**
```bash
dotnet test --collect:"XPlat Code Coverage"
```

---

**Last Updated:** 2025-11-23
**Test Count:** 300+ tests
**Coverage Target:** 85%+
