# Testing Strategy

**Version:** 1.0.0
**Target Coverage:** 85%+
**Test Count:** 400+ tests
**Framework:** xUnit, Moq, FluentAssertions

---

## Overview

The Service Mesh Policy Manager follows **Test-Driven Development (TDD)** with comprehensive test coverage across all layers. This document outlines the testing strategy, test organization, and best practices.

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
5. [Test Organization](#test-organization)
6. [TDD Workflow](#tdd-workflow)

---

## Unit Testing

**Target:** 320+ unit tests, 85%+ code coverage

### Scope

Test individual components in isolation with mocked dependencies.

**Components to Test:**
- Domain models (validation, business logic)
- Deployment strategies (policy deployment algorithms)
- Service mesh adapters (Istio, Linkerd interactions)
- Policy validation (syntax, semantic, conflicts)
- Metrics collection and comparison

### Domain Models Tests

**File:** `tests/HotSwap.ServiceMesh.Tests/Domain/PolicyTests.cs`

```csharp
public class PolicyTests
{
    [Fact]
    public void Policy_WithValidData_PassesValidation()
    {
        // Arrange
        var policy = new Policy
        {
            PolicyId = Guid.NewGuid().ToString(),
            Name = "user-service-circuit-breaker",
            Type = PolicyType.DestinationRule,
            ServiceMesh = ServiceMeshType.Istio,
            TargetService = "user-service",
            Namespace = "production",
            OwnerId = "user-123",
            Spec = new PolicySpec
            {
                YamlConfig = "apiVersion: networking.istio.io/v1beta1\nkind: DestinationRule"
            }
        };

        // Act
        var isValid = policy.IsValid(out var errors);

        // Assert
        isValid.Should().BeTrue();
        errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("", "test-service", "PolicyId is required")]
    [InlineData("pol-1", "", "TargetService is required")]
    [InlineData("pol-1", "test", "Name is required")]
    public void Policy_WithMissingRequiredField_FailsValidation(
        string policyId, string targetService, string expectedError)
    {
        // Arrange
        var policy = new Policy
        {
            PolicyId = policyId,
            Name = string.IsNullOrEmpty(policyId) ? "test" : "",
            Type = PolicyType.VirtualService,
            ServiceMesh = ServiceMeshType.Istio,
            TargetService = targetService,
            OwnerId = "user-123",
            Spec = new PolicySpec { YamlConfig = "..." }
        };

        // Act
        var isValid = policy.IsValid(out var errors);

        // Assert
        isValid.Should().BeFalse();
        errors.Should().Contain(expectedError);
    }

    [Fact]
    public void CreateNewVersion_IncrementsVersionNumber()
    {
        // Arrange
        var policy = CreateValidPolicy();
        policy.Version = 3;

        // Act
        var newVersion = policy.CreateNewVersion();

        // Assert
        newVersion.Version.Should().Be(4);
        newVersion.PolicyId.Should().NotBe(policy.PolicyId);
        newVersion.Name.Should().Be(policy.Name);
    }
}
```

**Estimated Tests:** 20+ tests per domain model (×4 models = 80 tests)

---

### Deployment Strategy Tests

**File:** `tests/HotSwap.ServiceMesh.Tests/Strategies/CanaryDeploymentStrategyTests.cs`

```csharp
public class CanaryDeploymentStrategyTests
{
    private readonly Mock<IServiceMeshAdapter> _mockAdapter;
    private readonly Mock<IMetricsCollector> _mockMetrics;
    private readonly CanaryDeploymentStrategy _strategy;

    public CanaryDeploymentStrategyTests()
    {
        _mockAdapter = new Mock<IServiceMeshAdapter>();
        _mockMetrics = new Mock<IMetricsCollector>();
        _strategy = new CanaryDeploymentStrategy(_mockAdapter.Object, _mockMetrics.Object);
    }

    [Fact]
    public async Task DeployAsync_SuccessfulCanary_CompletesAllStages()
    {
        // Arrange
        var policy = CreateValidPolicy();
        var cluster = CreateTestCluster();
        var baselineMetrics = new TrafficMetrics { ErrorRate = 0.5, P95Latency = 100 };
        var goodMetrics = new TrafficMetrics { ErrorRate = 0.4, P95Latency = 95 };

        _mockMetrics.Setup(m => m.CollectMetricsAsync(cluster))
            .ReturnsAsync(goodMetrics);

        // Act
        var result = await _strategy.DeployAsync(policy, cluster);

        // Assert
        result.Success.Should().BeTrue();
        _mockAdapter.Verify(a => a.SetTrafficSplitAsync(policy, cluster, 10), Times.Once);
        _mockAdapter.Verify(a => a.SetTrafficSplitAsync(policy, cluster, 30), Times.Once);
        _mockAdapter.Verify(a => a.SetTrafficSplitAsync(policy, cluster, 50), Times.Once);
        _mockAdapter.Verify(a => a.SetTrafficSplitAsync(policy, cluster, 100), Times.Once);
    }

    [Fact]
    public async Task DeployAsync_MetricsDegraded_RollsBack()
    {
        // Arrange
        var policy = CreateValidPolicy();
        var cluster = CreateTestCluster();
        var baselineMetrics = new TrafficMetrics { ErrorRate = 0.5, P95Latency = 100 };
        var degradedMetrics = new TrafficMetrics { ErrorRate = 5.0, P95Latency = 200 };

        _mockMetrics.SetupSequence(m => m.CollectMetricsAsync(cluster))
            .ReturnsAsync(baselineMetrics)  // Initial baseline
            .ReturnsAsync(degradedMetrics);  // After 10% canary

        // Act
        var result = await _strategy.DeployAsync(policy, cluster);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("degraded");
        _mockAdapter.Verify(a => a.RollbackPolicyAsync(policy, cluster), Times.Once);
    }
}
```

**Estimated Tests:** 20+ tests per strategy (×5 strategies = 100 tests)

---

### Service Mesh Adapter Tests

**File:** `tests/HotSwap.ServiceMesh.Tests/Infrastructure/IstioAdapterTests.cs`

```csharp
public class IstioAdapterTests
{
    private readonly Mock<IKubernetes> _mockK8s;
    private readonly IstioAdapter _adapter;

    public IstioAdapterTests()
    {
        _mockK8s = new Mock<IKubernetes>();
        _adapter = new IstioAdapter(_mockK8s.Object);
    }

    [Fact]
    public async Task ApplyPolicyAsync_VirtualService_CreatesKubernetesResource()
    {
        // Arrange
        var policy = new Policy
        {
            PolicyId = "pol-123",
            Name = "user-service-vs",
            Type = PolicyType.VirtualService,
            ServiceMesh = ServiceMeshType.Istio,
            TargetService = "user-service",
            Namespace = "production",
            Spec = new PolicySpec { YamlConfig = GetValidVirtualServiceYaml() }
        };
        var cluster = CreateTestCluster();

        // Act
        await _adapter.ApplyPolicyAsync(policy, cluster);

        // Assert
        _mockK8s.Verify(k => k.CreateNamespacedCustomObjectAsync(
            It.IsAny<object>(),
            "networking.istio.io",
            "v1beta1",
            "production",
            "virtualservices"), Times.Once);
    }

    [Fact]
    public async Task ValidatePolicyAsync_InvalidYaml_ReturnsFalse()
    {
        // Arrange
        var policy = CreatePolicyWithInvalidYaml();
        var cluster = CreateTestCluster();

        // Act
        var result = await _adapter.ValidatePolicyAsync(policy, cluster);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }
}
```

**Estimated Tests:** 25+ tests for Istio, 20+ tests for Linkerd (45 tests)

---

### Policy Validator Tests

**File:** `tests/HotSwap.ServiceMesh.Tests/Validation/PolicyValidatorTests.cs`

```csharp
public class PolicyValidatorTests
{
    private readonly Mock<IPolicyRepository> _mockRepo;
    private readonly PolicyValidator _validator;

    public PolicyValidatorTests()
    {
        _mockRepo = new Mock<IPolicyRepository>();
        _validator = new PolicyValidator(_mockRepo.Object);
    }

    [Fact]
    public async Task ValidateAsync_ValidPolicy_ReturnsSuccess()
    {
        // Arrange
        var policy = CreateValidPolicy();

        // Act
        var result = await _validator.ValidateAsync(policy);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateAsync_ConflictingPolicy_ReturnsConflict()
    {
        // Arrange
        var policy = CreateValidPolicy();
        var existingPolicy = CreateConflictingPolicy();

        _mockRepo.Setup(r => r.ListPoliciesAsync(It.IsAny<PolicyFilter>()))
            .ReturnsAsync(new List<Policy> { existingPolicy });

        // Act
        var result = await _validator.ValidateAsync(policy);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Conflicts.Should().HaveCount(1);
        result.Conflicts[0].ConflictingPolicyId.Should().Be(existingPolicy.PolicyId);
    }
}
```

**Estimated Tests:** 25+ tests for validation

---

## Integration Testing

**Target:** 60+ integration tests

### Scope

Test multiple components working together with real dependencies (Kubernetes, Prometheus).

**Test Scenarios:**
1. Deploy policy → Verify in Kubernetes
2. Canary deployment → Monitor metrics → Auto-promote
3. Policy conflict detection
4. Rollback on metric degradation

### End-to-End Policy Deployment Test

**File:** `tests/HotSwap.ServiceMesh.IntegrationTests/PolicyDeploymentTests.cs`

```csharp
[Collection("Integration")]
public class PolicyDeploymentTests : IClassFixture<TestServerFixture>
{
    private readonly HttpClient _client;
    private readonly IKubernetes _k8s;

    public PolicyDeploymentTests(TestServerFixture fixture)
    {
        _client = fixture.CreateClient();
        _k8s = fixture.KubernetesClient;
    }

    [Fact]
    public async Task EndToEndFlow_CreateDeployValidate_WorksCorrectly()
    {
        // Arrange - Create policy
        var policyRequest = new
        {
            name = "test-circuit-breaker",
            type = "DestinationRule",
            serviceMesh = "Istio",
            targetService = "test-service",
            namespace = "default",
            spec = new
            {
                yamlConfig = GetValidDestinationRuleYaml()
            }
        };
        var policyResponse = await _client.PostAsJsonAsync("/api/v1/policies", policyRequest);
        var policy = await policyResponse.Content.ReadFromJsonAsync<PolicyResponse>();

        // Act - Deploy policy
        var deployRequest = new
        {
            policyId = policy.PolicyId,
            environment = "Development",
            clusterId = "test-cluster",
            strategy = "Direct"
        };
        var deployResponse = await _client.PostAsJsonAsync("/api/v1/deployments", deployRequest);
        var deployment = await deployResponse.Content.ReadFromJsonAsync<DeploymentResponse>();

        // Wait for deployment to complete
        await WaitForDeploymentCompletion(deployment.DeploymentId);

        // Assert - Verify policy in Kubernetes
        var k8sPolicy = await _k8s.GetNamespacedCustomObjectAsync(
            "networking.istio.io", "v1beta1", "default", "destinationrules", "test-circuit-breaker");
        k8sPolicy.Should().NotBeNull();

        // Assert - Deployment completed successfully
        var deploymentStatus = await _client.GetFromJsonAsync<DeploymentResponse>(
            $"/api/v1/deployments/{deployment.DeploymentId}");
        deploymentStatus.Status.Should().Be("Completed");
    }
}
```

**Estimated Tests:** 40+ integration tests

---

## End-to-End Testing

**Target:** 20+ E2E tests

### Scope

Test complete user workflows from API to Kubernetes with all real components.

**E2E Test Scenarios:**
1. Complete canary deployment lifecycle
2. Policy approval workflow
3. Automatic rollback on metric degradation
4. Multi-cluster policy deployment

### E2E Canary Deployment Test

```csharp
[Collection("E2E")]
public class CanaryDeploymentE2ETests : IClassFixture<E2ETestFixture>
{
    [Fact]
    public async Task CanaryDeployment_WithMonitoring_PromotesSuccessfully()
    {
        // Arrange - Set up test service
        await DeployTestService("user-service");

        // Arrange - Create circuit breaker policy
        var policy = await CreateCircuitBreakerPolicy("user-service");

        // Act - Deploy with canary strategy
        var deployment = await DeployPolicyWithCanary(policy.PolicyId, 10);

        // Wait for 10% canary stage
        await WaitForCanaryStage(deployment.DeploymentId, 10);

        // Assert - Verify traffic split
        var metrics = await GetDeploymentMetrics(deployment.DeploymentId);
        metrics.Current.ErrorRate.Should().BeLessThan(1.0);

        // Wait for auto-promotion to 100%
        await WaitForDeploymentCompletion(deployment.DeploymentId);

        // Assert - Verify full deployment
        var finalStatus = await GetDeploymentStatus(deployment.DeploymentId);
        finalStatus.Status.Should().Be("Completed");
        finalStatus.CanaryPercentage.Should().Be(100);
    }
}
```

---

## Performance Testing

**Target:** Meet SLA requirements

### Performance Test Scenarios

**Scenario 1: Policy Propagation Time**

```csharp
[Fact]
public async Task PolicyPropagation_1000Instances_CompletesWithin30Seconds()
{
    // Arrange
    var cluster = CreateClusterWith1000Instances();
    var policy = CreateValidPolicy();

    // Act
    var stopwatch = Stopwatch.StartNew();
    await _orchestrator.DeployAsync(policy, cluster);
    stopwatch.Stop();

    // Assert
    stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(30));
}
```

**Scenario 2: Rollback Time**

```csharp
[Fact]
public async Task Rollback_Production_CompletesWithin10Seconds()
{
    // Arrange
    var deployment = await CreateActiveDeployment();

    // Act
    var stopwatch = Stopwatch.StartNew();
    await _orchestrator.RollbackAsync(deployment);
    stopwatch.Stop();

    // Assert
    stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(10));
}
```

---

## Test Organization

### Project Structure

```
tests/
├── HotSwap.ServiceMesh.Tests/                # Unit tests
│   ├── Domain/
│   │   ├── PolicyTests.cs                    # 20 tests
│   │   ├── DeploymentTests.cs                # 15 tests
│   │   └── ValidationResultTests.cs          # 15 tests
│   ├── Strategies/
│   │   ├── DirectDeploymentStrategyTests.cs  # 10 tests
│   │   ├── CanaryDeploymentStrategyTests.cs  # 20 tests
│   │   ├── BlueGreenDeploymentStrategyTests.cs # 15 tests
│   │   ├── RollingDeploymentStrategyTests.cs # 15 tests
│   │   └── ABTestingDeploymentStrategyTests.cs # 15 tests
│   ├── Infrastructure/
│   │   ├── IstioAdapterTests.cs              # 25 tests
│   │   ├── LinkerdAdapterTests.cs            # 20 tests
│   │   ├── MetricsCollectorTests.cs          # 20 tests
│   │   └── PolicyValidatorTests.cs           # 25 tests
│   └── Orchestrator/
│       ├── DeploymentOrchestratorTests.cs    # 20 tests
│       └── RollbackOrchestratorTests.cs      # 20 tests
├── HotSwap.ServiceMesh.IntegrationTests/    # Integration tests
│   ├── PolicyDeploymentTests.cs              # 15 tests
│   ├── ValidationTests.cs                    # 10 tests
│   └── RollbackTests.cs                      # 15 tests
└── HotSwap.ServiceMesh.E2ETests/            # End-to-end tests
    ├── CanaryDeploymentE2ETests.cs           # 5 tests
    ├── ApprovalWorkflowE2ETests.cs           # 5 tests
    └── MultiClusterDeploymentE2ETests.cs     # 5 tests
```

---

## TDD Workflow

Follow Red-Green-Refactor cycle for ALL code changes.

### Example: Implementing Canary Deployment

**Step 1: 🔴 RED - Write Failing Test**

```csharp
[Fact]
public async Task DeployAsync_WithCanaryStages_PromotesGradually()
{
    // Arrange
    var strategy = new CanaryDeploymentStrategy(_mockAdapter.Object, _mockMetrics.Object);
    var policy = CreateValidPolicy();
    var cluster = CreateTestCluster();

    // Act
    var result = await strategy.DeployAsync(policy, cluster);

    // Assert
    result.Success.Should().BeTrue();
    _mockAdapter.Verify(a => a.SetTrafficSplitAsync(policy, cluster, 10), Times.Once);
    _mockAdapter.Verify(a => a.SetTrafficSplitAsync(policy, cluster, 30), Times.Once);
}
```

Run test: **FAILS** ❌ (CanaryDeploymentStrategy doesn't exist)

**Step 2: 🟢 GREEN - Minimal Implementation**

```csharp
public class CanaryDeploymentStrategy : IDeploymentStrategy
{
    public async Task<DeploymentResult> DeployAsync(Policy policy, ServiceMeshCluster cluster)
    {
        await _adapter.SetTrafficSplitAsync(policy, cluster, 10);
        await _adapter.SetTrafficSplitAsync(policy, cluster, 30);
        return DeploymentResult.Success();
    }
}
```

Run test: **PASSES** ✓

**Step 3: 🔵 REFACTOR - Improve Implementation**

```csharp
public class CanaryDeploymentStrategy : IDeploymentStrategy
{
    private readonly int[] _stages = { 10, 30, 50, 100 };

    public async Task<DeploymentResult> DeployAsync(Policy policy, ServiceMeshCluster cluster)
    {
        foreach (var percentage in _stages)
        {
            await _adapter.SetTrafficSplitAsync(policy, cluster, percentage);
            await MonitorMetricsAsync(cluster);
        }
        return DeploymentResult.Success();
    }
}
```

Run test: **PASSES** ✓

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
