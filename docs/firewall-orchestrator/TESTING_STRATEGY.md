# Testing Strategy

**Version:** 1.0.0
**Target Coverage:** 85%+
**Test Count:** 350+ tests
**Framework:** xUnit, Moq, FluentAssertions

---

## Overview

The firewall orchestrator follows **Test-Driven Development (TDD)** with comprehensive test coverage across all layers. This document outlines the testing strategy, test organization, and best practices.

### Test Pyramid

```
                 ▲
                / \
               /E2E\           5% - End-to-End Tests (18 tests)
              /_____\
             /       \
            /Integration\      15% - Integration Tests (52 tests)
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
5. [Test Organization](#test-organization)
6. [TDD Workflow](#tdd-workflow)
7. [CI/CD Integration](#cicd-integration)

---

## Unit Testing

**Target:** 280+ unit tests, 85%+ code coverage

### Scope

Test individual components in isolation with mocked dependencies.

**Components to Test:**
- Domain models (validation, business logic)
- Deployment strategies (deployment algorithms)
- Validation engine (rule validation)
- Provider adapters (firewall API interactions)
- Rollback orchestrator (rollback logic)

### Domain Models Tests

**File:** `tests/HotSwap.Firewall.Tests/Domain/FirewallRuleTests.cs`

```csharp
public class FirewallRuleTests
{
    [Fact]
    public void FirewallRule_WithValidData_PassesValidation()
    {
        // Arrange
        var rule = new FirewallRule
        {
            RuleId = Guid.NewGuid().ToString(),
            Name = "allow-https",
            Action = RuleAction.Allow,
            Protocol = Protocol.TCP,
            SourceAddress = "0.0.0.0/0",
            DestinationAddress = "10.0.1.0/24",
            DestinationPort = "443",
            Priority = 100
        };

        // Act
        var isValid = rule.IsValid(out var errors);

        // Assert
        isValid.Should().BeTrue();
        errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("", "allow-https", "RuleId is required")]
    [InlineData("rule-1", "", "Name is required")]
    [InlineData("rule-1", "test", "SourceAddress is required", "")]
    [InlineData("rule-1", "test", "DestinationAddress is required", "10.0.1.0/24", "")]
    public void FirewallRule_WithMissingRequiredField_FailsValidation(
        string ruleId, string name, string expectedError, 
        string sourceAddr = "0.0.0.0/0", string destAddr = "10.0.1.0/24")
    {
        // Arrange
        var rule = new FirewallRule
        {
            RuleId = ruleId,
            Name = name,
            SourceAddress = sourceAddr,
            DestinationAddress = destAddr,
            Action = RuleAction.Allow,
            Protocol = Protocol.TCP
        };

        // Act
        var isValid = rule.IsValid(out var errors);

        // Assert
        isValid.Should().BeFalse();
        errors.Should().Contain(expectedError);
    }

    [Theory]
    [InlineData("192.168.1.0/24", true)]
    [InlineData("10.0.0.1", true)]
    [InlineData("0.0.0.0/0", true)]
    [InlineData("::/0", true)]
    [InlineData("2001:db8::/32", true)]
    [InlineData("192.168.1.0/33", false)]
    [InlineData("invalid", false)]
    public void FirewallRule_CIDRValidation_WorksCorrectly(string cidr, bool shouldBeValid)
    {
        // Arrange
        var rule = new FirewallRule
        {
            RuleId = "rule-1",
            Name = "test",
            SourceAddress = cidr,
            DestinationAddress = "10.0.1.0/24",
            Action = RuleAction.Allow,
            Protocol = Protocol.TCP
        };

        // Act
        var isValid = rule.IsValid(out var errors);

        // Assert
        isValid.Should().Be(shouldBeValid);
    }

    [Fact]
    public void IsOverlyPermissive_DetectsPermissiveRules()
    {
        // Arrange
        var rule = new FirewallRule
        {
            RuleId = "rule-1",
            Name = "allow-all",
            Action = RuleAction.Allow,
            Protocol = Protocol.ALL,
            SourceAddress = "0.0.0.0/0",
            DestinationAddress = "0.0.0.0/0",
            DestinationPort = "any"
        };

        // Act
        var isPermissive = rule.IsOverlyPermissive();

        // Assert
        isPermissive.Should().BeTrue();
    }
}
```

**Estimated Tests:** 25+ tests per domain model (×4 models = 100 tests)

---

### Deployment Strategy Tests

**File:** `tests/HotSwap.Firewall.Tests/Strategies/CanaryDeploymentStrategyTests.cs`

```csharp
public class CanaryDeploymentStrategyTests
{
    private readonly Mock<IFirewallProvider> _mockProvider;
    private readonly Mock<IValidationEngine> _mockValidation;
    private readonly CanaryDeploymentStrategy _strategy;

    public CanaryDeploymentStrategyTests()
    {
        _mockProvider = new Mock<IFirewallProvider>();
        _mockValidation = new Mock<IValidationEngine>();
        _strategy = new CanaryDeploymentStrategy(_mockProvider.Object, _mockValidation.Object);
    }

    [Fact]
    public async Task DeployAsync_WithCanaryStrategy_DeploysInThreeStages()
    {
        // Arrange
        var ruleSet = CreateRuleSet();
        var targets = CreateTargets(20);
        
        _mockProvider.Setup(p => p.DeployRulesAsync(It.IsAny<RuleSet>(), It.IsAny<DeploymentTarget>()))
            .ReturnsAsync(DeploymentResult.Success());
        
        _mockValidation.Setup(v => v.ValidateConnectivityAsync(It.IsAny<List<DeploymentTarget>>()))
            .ReturnsAsync(ValidationResult.Success());

        // Act
        var result = await _strategy.DeployAsync(ruleSet, targets);

        // Assert
        result.Success.Should().BeTrue();
        
        // Verify Stage 1 (10% = 2 targets)
        _mockProvider.Verify(p => p.DeployRulesAsync(ruleSet, It.IsAny<DeploymentTarget>()), Times.Exactly(2));
        
        // Verify validation called after Stage 1
        _mockValidation.Verify(v => v.ValidateConnectivityAsync(It.Is<List<DeploymentTarget>>(t => t.Count == 2)), Times.Once);
    }

    [Fact]
    public async Task DeployAsync_WhenStage1Fails_RollsBackAutomatically()
    {
        // Arrange
        var ruleSet = CreateRuleSet();
        var targets = CreateTargets(20);
        
        _mockProvider.Setup(p => p.DeployRulesAsync(It.IsAny<RuleSet>(), It.IsAny<DeploymentTarget>()))
            .ReturnsAsync(DeploymentResult.Failure("Deployment failed"));

        // Act
        var result = await _strategy.DeployAsync(ruleSet, targets);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Canary stage failed");
        
        // Verify rollback called
        _mockProvider.Verify(p => p.RollbackAsync(It.IsAny<DeploymentTarget>(), It.IsAny<string>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task DeployAsync_WhenValidationFails_RollsBack()
    {
        // Arrange
        var ruleSet = CreateRuleSet();
        var targets = CreateTargets(20);
        
        _mockProvider.Setup(p => p.DeployRulesAsync(It.IsAny<RuleSet>(), It.IsAny<DeploymentTarget>()))
            .ReturnsAsync(DeploymentResult.Success());
        
        _mockValidation.Setup(v => v.ValidateConnectivityAsync(It.IsAny<List<DeploymentTarget>>()))
            .ReturnsAsync(ValidationResult.Failure(new ValidationError 
            { 
                Code = "CONNECTIVITY_FAILED", 
                Message = "Cannot reach endpoint" 
            }));

        // Act
        var result = await _strategy.DeployAsync(ruleSet, targets);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Connectivity validation failed");
        
        // Verify rollback called
        _mockProvider.Verify(p => p.RollbackAsync(It.IsAny<DeploymentTarget>(), It.IsAny<string>()), Times.AtLeastOnce);
    }
}
```

**Estimated Tests:** 20+ tests per strategy (×5 strategies = 100 tests)

---

### Validation Engine Tests

**File:** `tests/HotSwap.Firewall.Tests/Validation/ValidationEngineTests.cs`

```csharp
public class ValidationEngineTests
{
    private readonly ValidationEngine _engine;

    public ValidationEngineTests()
    {
        _engine = new ValidationEngine();
    }

    [Fact]
    public async Task ValidateRuleSetAsync_WithValidRules_ReturnsSuccess()
    {
        // Arrange
        var ruleSet = new RuleSet
        {
            Name = "test-rules",
            Environment = "Development",
            TargetType = TargetType.CloudFirewall,
            Rules = new List<FirewallRule>
            {
                new FirewallRule
                {
                    RuleId = "rule-1",
                    Name = "allow-https",
                    Action = RuleAction.Allow,
                    Protocol = Protocol.TCP,
                    SourceAddress = "0.0.0.0/0",
                    DestinationAddress = "10.0.1.0/24",
                    DestinationPort = "443",
                    Priority = 100
                }
            }
        };

        // Act
        var result = await _engine.ValidateRuleSetAsync(ruleSet);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateRuleSetAsync_DetectsConflictingRules()
    {
        // Arrange
        var ruleSet = new RuleSet
        {
            Name = "test-rules",
            Environment = "Development",
            TargetType = TargetType.CloudFirewall,
            Rules = new List<FirewallRule>
            {
                new FirewallRule
                {
                    RuleId = "rule-1",
                    Name = "allow-ssh",
                    Action = RuleAction.Allow,
                    Protocol = Protocol.TCP,
                    SourceAddress = "0.0.0.0/0",
                    DestinationAddress = "10.0.1.100",
                    DestinationPort = "22",
                    Priority = 100
                },
                new FirewallRule
                {
                    RuleId = "rule-2",
                    Name = "deny-ssh",
                    Action = RuleAction.Deny,
                    Protocol = Protocol.TCP,
                    SourceAddress = "0.0.0.0/0",
                    DestinationAddress = "10.0.1.100",
                    DestinationPort = "22",
                    Priority = 200
                }
            }
        };

        // Act
        var result = await _engine.ValidateRuleSetAsync(ruleSet);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Code == "RULE_CONFLICT");
    }

    [Fact]
    public async Task ValidateRuleSetAsync_DetectsShadowedRules()
    {
        // Arrange
        var ruleSet = new RuleSet
        {
            Name = "test-rules",
            Environment = "Development",
            TargetType = TargetType.CloudFirewall,
            Rules = new List<FirewallRule>
            {
                new FirewallRule
                {
                    RuleId = "rule-1",
                    Name = "allow-all",
                    Action = RuleAction.Allow,
                    Protocol = Protocol.TCP,
                    SourceAddress = "0.0.0.0/0",
                    DestinationAddress = "10.0.1.0/24",
                    DestinationPort = "any",
                    Priority = 100
                },
                new FirewallRule
                {
                    RuleId = "rule-2",
                    Name = "allow-https",
                    Action = RuleAction.Allow,
                    Protocol = Protocol.TCP,
                    SourceAddress = "0.0.0.0/0",
                    DestinationAddress = "10.0.1.0/24",
                    DestinationPort = "443",
                    Priority = 200
                }
            }
        };

        // Act
        var result = await _engine.ValidateRuleSetAsync(ruleSet);

        // Assert
        result.Warnings.Should().Contain(w => w.Code == "SHADOWED_RULE");
        result.Warnings.Should().Contain(w => w.Message.Contains("allow-https"));
    }
}
```

**Estimated Tests:** 30+ tests for validation engine

---

## Integration Testing

**Target:** 52+ integration tests

### Scope

Test multiple components working together with real dependencies (PostgreSQL, provider APIs).

**Test Scenarios:**
1. Create rule set → Add rules → Deploy → Validate (happy path)
2. Create rule set → Deploy with invalid rules → Fail
3. Deploy with canary strategy → Validation failure → Rollback
4. Deploy to AWS Security Groups → Verify rules applied
5. Approve deployment → Deploy → Complete

### End-to-End Deployment Test

**File:** `tests/HotSwap.Firewall.IntegrationTests/DeploymentFlowTests.cs`

```csharp
[Collection("Integration")]
public class DeploymentFlowTests : IClassFixture<TestServerFixture>
{
    private readonly HttpClient _client;

    public DeploymentFlowTests(TestServerFixture fixture)
    {
        _client = fixture.CreateClient();
    }

    [Fact]
    public async Task EndToEndFlow_CreateRuleSetDeployRollback_WorksCorrectly()
    {
        // Arrange - Create rule set
        var ruleSetRequest = new
        {
            name = "integration-test-rules",
            description = "Integration test rule set",
            environment = "Development",
            targetType = "CloudFirewall"
        };
        var createResponse = await _client.PostAsJsonAsync("/api/v1/firewall/rulesets", ruleSetRequest);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // Act - Add rules
        var ruleRequest = new
        {
            name = "allow-https",
            action = "Allow",
            protocol = "TCP",
            sourceAddress = "0.0.0.0/0",
            destinationAddress = "10.0.1.0/24",
            destinationPort = "443",
            priority = 100
        };
        var addRuleResponse = await _client.PostAsJsonAsync(
            "/api/v1/firewall/rulesets/integration-test-rules/rules", ruleRequest);
        addRuleResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // Act - Deploy
        var deployRequest = new
        {
            ruleSetName = "integration-test-rules",
            targetEnvironment = "Development",
            strategy = "Direct",
            targetIds = new[] { "test-target-1" }
        };
        var deployResponse = await _client.PostAsJsonAsync("/api/v1/firewall/deployments", deployRequest);
        var deployResult = await deployResponse.Content.ReadFromJsonAsync<DeploymentResponse>();

        // Assert - Deployment created
        deployResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);
        deployResult.DeploymentId.Should().NotBeNullOrEmpty();

        // Wait for deployment to complete
        await Task.Delay(TimeSpan.FromSeconds(5));

        // Act - Check deployment status
        var statusResponse = await _client.GetAsync($"/api/v1/firewall/deployments/{deployResult.DeploymentId}");
        var status = await statusResponse.Content.ReadFromJsonAsync<DeploymentStatusResponse>();

        // Assert - Deployment succeeded
        status.Status.Should().Be("Succeeded");

        // Act - Rollback
        var rollbackRequest = new { reason = "Integration test rollback" };
        var rollbackResponse = await _client.PostAsJsonAsync(
            $"/api/v1/firewall/deployments/{deployResult.DeploymentId}/rollback", rollbackRequest);

        // Assert - Rollback succeeded
        rollbackResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var rollbackResult = await rollbackResponse.Content.ReadFromJsonAsync<RollbackResponse>();
        rollbackResult.Rollback.Success.Should().BeTrue();
    }
}
```

**Estimated Tests:** 52+ integration tests

---

## End-to-End Testing

**Target:** 18+ E2E tests

### Scope

Test complete user workflows from API to firewall provider with all real components.

**E2E Test Scenarios:**
1. Complete deployment lifecycle (create → deploy → validate → complete)
2. Approval workflow (create → submit → approve → deploy)
3. Canary deployment with rollback
4. Blue-green deployment with traffic switch
5. Multi-target rolling deployment

---

## Performance Testing

**Target:** Meet SLA requirements

### Performance Test Scenarios

**Scenario 1: Deployment Latency Test**

```csharp
[Fact]
public async Task Deployment_SingleTarget_CompletesWithin30Seconds()
{
    // Arrange
    var ruleSet = CreateRuleSetWith1000Rules();
    var target = CreateTarget();

    // Act
    var stopwatch = Stopwatch.StartNew();
    await _deploymentOrchestrator.DeployAsync(ruleSet, new[] { target });
    stopwatch.Stop();

    // Assert
    stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(30));
}
```

**Scenario 2: Validation Performance Test**

```csharp
[Fact]
public async Task Validation_1000Rules_CompletesWithin5Seconds()
{
    // Arrange
    var ruleSet = CreateRuleSetWith1000Rules();

    // Act
    var stopwatch = Stopwatch.StartNew();
    await _validationEngine.ValidateRuleSetAsync(ruleSet);
    stopwatch.Stop();

    // Assert
    stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5));
}
```

---

## Test Organization

### Project Structure

```
tests/
├── HotSwap.Firewall.Tests/                      # Unit tests
│   ├── Domain/
│   │   ├── FirewallRuleTests.cs                 # 25 tests
│   │   ├── RuleSetTests.cs                      # 20 tests
│   │   └── DeploymentTests.cs                   # 15 tests
│   ├── Strategies/
│   │   ├── DirectDeploymentStrategyTests.cs     # 10 tests
│   │   ├── CanaryDeploymentStrategyTests.cs     # 20 tests
│   │   ├── BlueGreenDeploymentStrategyTests.cs  # 18 tests
│   │   ├── RollingDeploymentStrategyTests.cs    # 15 tests
│   │   └── ABDeploymentStrategyTests.cs         # 12 tests
│   ├── Validation/
│   │   ├── ValidationEngineTests.cs             # 30 tests
│   │   └── ConnectivityTestRunnerTests.cs       # 20 tests
│   └── Providers/
│       ├── AWSSecurityGroupAdapterTests.cs      # 25 tests
│       └── AzureNSGAdapterTests.cs              # 25 tests
├── HotSwap.Firewall.IntegrationTests/           # Integration tests
│   ├── DeploymentFlowTests.cs                   # 15 tests
│   ├── ValidationFlowTests.cs                   # 12 tests
│   └── ProviderIntegrationTests.cs              # 25 tests
└── HotSwap.Firewall.E2ETests/                   # End-to-end tests
    ├── CanaryDeploymentE2ETests.cs              # 5 tests
    ├── BlueGreenDeploymentE2ETests.cs           # 5 tests
    └── ApprovalWorkflowE2ETests.cs              # 8 tests
```

---

## TDD Workflow

Follow Red-Green-Refactor cycle for ALL code changes.

### Example: Implementing Canary Deployment

**Step 1: 🔴 RED - Write Failing Test**

```csharp
[Fact]
public async Task DeployAsync_WithCanaryStrategy_DeploysInThreeStages()
{
    // Arrange
    var strategy = new CanaryDeploymentStrategy(_mockProvider.Object);
    var ruleSet = CreateRuleSet();
    var targets = CreateTargets(20);

    // Act
    var result = await strategy.DeployAsync(ruleSet, targets);

    // Assert
    result.Success.Should().BeTrue();
    _mockProvider.Verify(p => p.DeployRulesAsync(It.IsAny<RuleSet>(), It.IsAny<DeploymentTarget>()), 
        Times.Exactly(2)); // Stage 1: 10% of 20 = 2
}
```

Run test: **FAILS** ❌ (CanaryDeploymentStrategy doesn't exist)

**Step 2: 🟢 GREEN - Minimal Implementation**

```csharp
public class CanaryDeploymentStrategy : IDeploymentStrategy
{
    public async Task<DeploymentResult> DeployAsync(RuleSet ruleSet, List<DeploymentTarget> targets)
    {
        var canaryTargets = targets.Take((int)(targets.Count * 0.1)).ToList();
        
        foreach (var target in canaryTargets)
        {
            await _provider.DeployRulesAsync(ruleSet, target);
        }
        
        return DeploymentResult.Success(canaryTargets);
    }
}
```

Run test: **PASSES** ✓

**Step 3: 🔵 REFACTOR - Improve Implementation**

```csharp
public class CanaryDeploymentStrategy : IDeploymentStrategy
{
    public async Task<DeploymentResult> DeployAsync(RuleSet ruleSet, List<DeploymentTarget> targets)
    {
        // Stage 1: 10%
        var stage1Targets = SelectCanaryTargets(targets, 0.1);
        var stage1Result = await DeployStageAsync(ruleSet, stage1Targets);
        if (!stage1Result.Success) return stage1Result;
        
        // Stage 2: 50%
        var stage2Targets = SelectCanaryTargets(targets, 0.5);
        var stage2Result = await DeployStageAsync(ruleSet, stage2Targets);
        if (!stage2Result.Success) return stage2Result;
        
        // Stage 3: 100%
        var stage3Targets = targets.Except(stage2Targets).ToList();
        return await DeployStageAsync(ruleSet, stage3Targets);
    }
}
```

Run test: **PASSES** ✓

---

## CI/CD Integration

### GitHub Actions Workflow

```yaml
name: Firewall Orchestrator Tests

on:
  push:
    branches: [main, claude/*]
  pull_request:
    branches: [main]

jobs:
  test:
    runs-on: ubuntu-latest

    services:
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
        run: dotnet test tests/HotSwap.Firewall.Tests/ --no-build --verbosity normal

      - name: Run integration tests
        run: dotnet test tests/HotSwap.Firewall.IntegrationTests/ --no-build --verbosity normal

      - name: Run E2E tests
        run: dotnet test tests/HotSwap.Firewall.E2ETests/ --no-build --verbosity normal

      - name: Upload coverage
        uses: codecov/codecov-action@v3
```

---

## Test Coverage Requirements

**Minimum Coverage:** 85%

**Coverage by Layer:**
- Domain: 95%+ (simple models, high coverage easy)
- Strategies: 90%+ (core deployment logic)
- Validation: 85%+ (business logic)
- Infrastructure: 80%+ (external dependencies)
- API: 80%+ (mostly integration tests)

**Measure Coverage:**
```bash
dotnet test --collect:"XPlat Code Coverage"
```

---

**Last Updated:** 2025-11-23
**Test Count:** 350+ tests
**Coverage Target:** 85%+
