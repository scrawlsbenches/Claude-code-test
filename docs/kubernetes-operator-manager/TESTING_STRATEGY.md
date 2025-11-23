# Testing Strategy

**Version:** 1.0.0
**Target Coverage:** 85%+
**Test Count:** 350+ tests
**Framework:** xUnit, Moq, FluentAssertions

---

## Overview

The Kubernetes Operator Manager follows **Test-Driven Development (TDD)** with comprehensive test coverage across all layers. This document outlines the testing strategy, test organization, and best practices.

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
- Deployment strategies (algorithm implementations)
- Kubernetes client wrappers (mocked K8s operations)
- CRD compatibility validation
- Health check logic
- Rollback engine

### Domain Models Tests

**File:** `tests/HotSwap.Kubernetes.Tests/Domain/OperatorTests.cs`

```csharp
public class OperatorTests
{
    [Fact]
    public void Operator_WithValidData_PassesValidation()
    {
        // Arrange
        var @operator = new Operator
        {
            OperatorId = Guid.NewGuid().ToString(),
            Name = "cert-manager",
            Namespace = "cert-manager",
            ChartRepository = "https://charts.jetstack.io",
            ChartName = "cert-manager",
            CurrentVersion = "v1.13.0"
        };

        // Act
        var isValid = @operator.IsValid(out var errors);

        // Assert
        isValid.Should().BeTrue();
        errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("", "cert-manager", "https://charts.jetstack.io", "cert-manager", "OperatorId is required")]
    [InlineData("op-123", "", "https://charts.jetstack.io", "cert-manager", "Name is required")]
    [InlineData("op-123", "CertManager", "https://charts.jetstack.io", "cert-manager", "Name must be a valid Kubernetes name")]
    [InlineData("op-123", "cert-manager", "invalid-url", "cert-manager", "ChartRepository must be a valid URL")]
    public void Operator_WithInvalidData_FailsValidation(
        string operatorId, string name, string chartRepo, string chartName, string expectedError)
    {
        // Arrange
        var @operator = new Operator
        {
            OperatorId = operatorId,
            Name = name,
            Namespace = "cert-manager",
            ChartRepository = chartRepo,
            ChartName = chartName
        };

        // Act
        var isValid = @operator.IsValid(out var errors);

        // Assert
        isValid.Should().BeFalse();
        errors.Should().Contain(expectedError);
    }

    [Fact]
    public void HasUpdate_WhenVersionsDiffer_ReturnsTrue()
    {
        // Arrange
        var @operator = new Operator
        {
            OperatorId = "op-123",
            Name = "cert-manager",
            Namespace = "cert-manager",
            ChartRepository = "https://charts.jetstack.io",
            ChartName = "cert-manager",
            CurrentVersion = "v1.13.0",
            LatestVersion = "v1.14.0"
        };

        // Act
        var hasUpdate = @operator.HasUpdate();

        // Assert
        hasUpdate.Should().BeTrue();
    }
}
```

**Estimated Tests:** 25+ tests per domain model (×4 models = 100 tests)

---

### Deployment Strategy Tests

**File:** `tests/HotSwap.Kubernetes.Tests/Strategies/CanaryDeploymentStrategyTests.cs`

```csharp
public class CanaryDeploymentStrategyTests
{
    private readonly CanaryDeploymentStrategy _strategy;
    private readonly Mock<IKubernetesClient> _mockK8sClient;
    private readonly Mock<IHealthMonitor> _mockHealthMonitor;

    public CanaryDeploymentStrategyTests()
    {
        _mockK8sClient = new Mock<IKubernetesClient>();
        _mockHealthMonitor = new Mock<IHealthMonitor>();
        _strategy = new CanaryDeploymentStrategy(_mockK8sClient.Object, _mockHealthMonitor.Object);
    }

    [Fact]
    public async Task DeployAsync_WithThreeStages_DeploysIncrementally()
    {
        // Arrange
        var deployment = CreateDeployment();
        var clusters = CreateClusters(10);
        var config = new CanaryConfig
        {
            InitialPercentage = 10,
            IncrementPercentage = 30,
            EvaluationPeriod = TimeSpan.FromSeconds(1),
            SuccessThreshold = 0.95
        };
        deployment.StrategyConfig = JsonSerializer.Serialize(new { canaryConfig = config });

        _mockHealthMonitor
            .Setup(h => h.ValidateHealthAsync(It.IsAny<List<KubernetesCluster>>(), It.IsAny<string>()))
            .ReturnsAsync(new HealthValidationResult { SuccessRate = 1.0 });

        // Act
        var result = await _strategy.DeployAsync(deployment, clusters);

        // Assert
        result.Success.Should().BeTrue();
        result.SuccessfulClusters.Should().HaveCount(10);

        // Verify deployment happened in stages
        _mockK8sClient.Verify(
            k => k.DeployOperatorAsync(It.IsAny<KubernetesCluster>(), deployment),
            Times.Exactly(10)
        );

        // Verify health checks ran for each stage
        _mockHealthMonitor.Verify(
            h => h.ValidateHealthAsync(It.IsAny<List<KubernetesCluster>>(), deployment.OperatorName),
            Times.AtLeast(3) // 3 stages
        );
    }

    [Fact]
    public async Task DeployAsync_WhenHealthCheckFails_RollsBack()
    {
        // Arrange
        var deployment = CreateDeployment();
        var clusters = CreateClusters(10);
        var config = new CanaryConfig
        {
            InitialPercentage = 10,
            IncrementPercentage = 30,
            EvaluationPeriod = TimeSpan.FromSeconds(1),
            SuccessThreshold = 0.95
        };
        deployment.StrategyConfig = JsonSerializer.Serialize(new { canaryConfig = config });

        // First stage passes, second stage fails
        var healthCheckCallCount = 0;
        _mockHealthMonitor
            .Setup(h => h.ValidateHealthAsync(It.IsAny<List<KubernetesCluster>>(), It.IsAny<string>()))
            .ReturnsAsync(() =>
            {
                healthCheckCallCount++;
                return new HealthValidationResult
                {
                    SuccessRate = healthCheckCallCount == 1 ? 1.0 : 0.80 // Fail on second stage
                };
            });

        // Act
        var result = await _strategy.DeployAsync(deployment, clusters);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Canary validation failed");

        // Verify rollback was triggered
        _mockK8sClient.Verify(
            k => k.RollbackOperatorAsync(It.IsAny<KubernetesCluster>(), deployment),
            Times.AtLeastOnce()
        );
    }

    [Fact]
    public async Task CalculateCanaryStages_With10ClustersAnd10Percent_ReturnsCorrectStages()
    {
        // Arrange
        var clusters = CreateClusters(10);
        var config = new CanaryConfig
        {
            InitialPercentage = 10,
            IncrementPercentage = 20
        };

        // Act
        var stages = _strategy.CalculateCanaryStages(clusters, config);

        // Assert
        stages.Should().HaveCount(4); // 10%, 30%, 50%, 100%
        stages[0].Percentage.Should().Be(10);
        stages[0].Clusters.Should().HaveCount(1);
        stages[1].Percentage.Should().Be(30);
        stages[1].Clusters.Should().HaveCount(2);
        stages[2].Percentage.Should().Be(50);
        stages[2].Clusters.Should().HaveCount(2);
        stages[3].Percentage.Should().Be(100);
        stages[3].Clusters.Should().HaveCount(5);
    }
}
```

**Estimated Tests:** 20+ tests per strategy (×4 strategies = 80 tests)

---

### Kubernetes Client Tests

**File:** `tests/HotSwap.Kubernetes.Tests/Infrastructure/KubernetesClientTests.cs`

```csharp
public class KubernetesClientTests
{
    private readonly Mock<IKubernetes> _mockK8s;
    private readonly KubernetesClient _client;
    private readonly KubernetesCluster _cluster;

    public KubernetesClientTests()
    {
        _mockK8s = new Mock<IKubernetes>();
        _cluster = CreateTestCluster();
        _client = new KubernetesClient(_mockK8s.Object, _cluster);
    }

    [Fact]
    public async Task DeployOperatorAsync_WithValidHelmChart_Succeeds()
    {
        // Arrange
        var deployment = CreateDeployment();

        _mockK8s
            .Setup(k => k.CoreV1.ListNamespacedPodAsync(
                deployment.OperatorName,
                It.IsAny<string>(),
                null, null, null, null, null, null, null, null, false, null, default))
            .ReturnsAsync(new V1PodList { Items = new List<V1Pod>() });

        // Act
        var result = await _client.DeployOperatorAsync(_cluster, deployment);

        // Assert
        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task GetOperatorHealthAsync_WithHealthyPods_ReturnsHealthy()
    {
        // Arrange
        var operatorName = "cert-manager";
        var pods = new V1PodList
        {
            Items = new List<V1Pod>
            {
                CreateHealthyPod("pod-1"),
                CreateHealthyPod("pod-2"),
                CreateHealthyPod("pod-3")
            }
        };

        _mockK8s
            .Setup(k => k.CoreV1.ListNamespacedPodAsync(
                operatorName,
                It.IsAny<string>(),
                null, null, null, It.IsAny<string>(), null, null, null, null, false, null, default))
            .ReturnsAsync(pods);

        // Act
        var health = await _client.GetOperatorHealthAsync(operatorName);

        // Assert
        health.OverallHealth.Should().Be(HealthStatus.Healthy);
        health.ControllerPodHealth.ReadyPods.Should().Be(3);
        health.ControllerPodHealth.ExpectedPods.Should().Be(3);
        health.ControllerPodHealth.IsHealthy.Should().BeTrue();
    }

    [Fact]
    public async Task GetOperatorHealthAsync_WithCrashLoopPods_ReturnsUnhealthy()
    {
        // Arrange
        var operatorName = "cert-manager";
        var pods = new V1PodList
        {
            Items = new List<V1Pod>
            {
                CreateHealthyPod("pod-1"),
                CreateCrashLoopPod("pod-2"),
                CreateHealthyPod("pod-3")
            }
        };

        _mockK8s
            .Setup(k => k.CoreV1.ListNamespacedPodAsync(
                operatorName,
                It.IsAny<string>(),
                null, null, null, It.IsAny<string>(), null, null, null, null, false, null, default))
            .ReturnsAsync(pods);

        // Act
        var health = await _client.GetOperatorHealthAsync(operatorName);

        // Assert
        health.OverallHealth.Should().Be(HealthStatus.Unhealthy);
        health.ControllerPodHealth.CrashLoopPods.Should().Be(1);
        health.ControllerPodHealth.IsHealthy.Should().BeFalse();
    }

    private V1Pod CreateHealthyPod(string name)
    {
        return new V1Pod
        {
            Metadata = new V1ObjectMeta { Name = name },
            Status = new V1PodStatus
            {
                Phase = "Running",
                Conditions = new List<V1PodCondition>
                {
                    new V1PodCondition { Type = "Ready", Status = "True" }
                },
                ContainerStatuses = new List<V1ContainerStatus>
                {
                    new V1ContainerStatus
                    {
                        Ready = true,
                        RestartCount = 0,
                        State = new V1ContainerState { Running = new V1ContainerStateRunning() }
                    }
                }
            }
        };
    }

    private V1Pod CreateCrashLoopPod(string name)
    {
        return new V1Pod
        {
            Metadata = new V1ObjectMeta { Name = name },
            Status = new V1PodStatus
            {
                Phase = "Running",
                ContainerStatuses = new List<V1ContainerStatus>
                {
                    new V1ContainerStatus
                    {
                        Ready = false,
                        RestartCount = 5,
                        State = new V1ContainerState
                        {
                            Waiting = new V1ContainerStateWaiting { Reason = "CrashLoopBackOff" }
                        }
                    }
                }
            }
        };
    }
}
```

**Estimated Tests:** 25+ tests for Kubernetes client

---

### CRD Compatibility Tests

**File:** `tests/HotSwap.Kubernetes.Tests/Services/CRDCompatibilityValidatorTests.cs`

```csharp
public class CRDCompatibilityValidatorTests
{
    private readonly CRDCompatibilityValidator _validator;

    public CRDCompatibilityValidatorTests()
    {
        _validator = new CRDCompatibilityValidator();
    }

    [Fact]
    public void ValidateCompatibility_WithAddedOptionalField_IsCompatible()
    {
        // Arrange
        var oldSchema = @"{
            ""type"": ""object"",
            ""properties"": {
                ""name"": { ""type"": ""string"" }
            },
            ""required"": [""name""]
        }";

        var newSchema = @"{
            ""type"": ""object"",
            ""properties"": {
                ""name"": { ""type"": ""string"" },
                ""email"": { ""type"": ""string"" }
            },
            ""required"": [""name""]
        }";

        // Act
        var result = _validator.ValidateCompatibility(oldSchema, newSchema);

        // Assert
        result.IsCompatible.Should().BeTrue();
        result.BreakingChanges.Should().BeEmpty();
        result.NonBreakingChanges.Should().HaveCount(1);
        result.NonBreakingChanges[0].FieldPath.Should().Be("email");
    }

    [Fact]
    public void ValidateCompatibility_WithRemovedField_IsIncompatible()
    {
        // Arrange
        var oldSchema = @"{
            ""type"": ""object"",
            ""properties"": {
                ""name"": { ""type"": ""string"" },
                ""email"": { ""type"": ""string"" }
            },
            ""required"": [""name""]
        }";

        var newSchema = @"{
            ""type"": ""object"",
            ""properties"": {
                ""name"": { ""type"": ""string"" }
            },
            ""required"": [""name""]
        }";

        // Act
        var result = _validator.ValidateCompatibility(oldSchema, newSchema);

        // Assert
        result.IsCompatible.Should().BeFalse();
        result.BreakingChanges.Should().HaveCount(1);
        result.BreakingChanges[0].FieldPath.Should().Be("email");
        result.BreakingChanges[0].ChangeType.Should().Be(SchemaChangeType.Removed);
        result.RequiresApproval.Should().BeTrue();
    }

    [Fact]
    public void ValidateCompatibility_WithAddedRequiredField_IsIncompatible()
    {
        // Arrange
        var oldSchema = @"{
            ""type"": ""object"",
            ""properties"": {
                ""name"": { ""type"": ""string"" }
            },
            ""required"": [""name""]
        }";

        var newSchema = @"{
            ""type"": ""object"",
            ""properties"": {
                ""name"": { ""type"": ""string"" },
                ""email"": { ""type"": ""string"" }
            },
            ""required"": [""name"", ""email""]
        }";

        // Act
        var result = _validator.ValidateCompatibility(oldSchema, newSchema);

        // Assert
        result.IsCompatible.Should().BeFalse();
        result.BreakingChanges.Should().HaveCount(1);
        result.BreakingChanges[0].FieldPath.Should().Be("email");
        result.BreakingChanges[0].Description.Should().Contain("Required field added");
    }

    [Fact]
    public void ValidateCompatibility_WithChangedFieldType_IsIncompatible()
    {
        // Arrange
        var oldSchema = @"{
            ""type"": ""object"",
            ""properties"": {
                ""count"": { ""type"": ""integer"" }
            }
        }";

        var newSchema = @"{
            ""type"": ""object"",
            ""properties"": {
                ""count"": { ""type"": ""string"" }
            }
        }";

        // Act
        var result = _validator.ValidateCompatibility(oldSchema, newSchema);

        // Assert
        result.IsCompatible.Should().BeFalse();
        result.BreakingChanges.Should().HaveCount(1);
        result.BreakingChanges[0].ChangeType.Should().Be(SchemaChangeType.Modified);
        result.BreakingChanges[0].Description.Should().Contain("Field type changed");
    }
}
```

**Estimated Tests:** 20+ tests for CRD validation

---

## Integration Testing

**Target:** 50+ integration tests

### Scope

Test multiple components working together with real dependencies (Kubernetes clusters).

**Test Scenarios:**
1. Register cluster → Deploy operator → Validate health (happy path)
2. Deploy with Canary strategy → Health check failure → Automatic rollback
3. CRD compatibility validation → Approval workflow → Production deployment
4. Multi-cluster deployment → Partial failure → Rollback affected clusters
5. Blue-Green deployment → Traffic switch → Verify zero downtime

### End-to-End Deployment Test

**File:** `tests/HotSwap.Kubernetes.IntegrationTests/DeploymentFlowTests.cs`

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
    public async Task EndToEndFlow_RegisterCluster_DeployOperator_Success()
    {
        // Arrange - Register cluster
        var clusterRequest = new
        {
            name = "test-cluster",
            environment = "Development",
            kubeconfig = GetTestKubeconfig()
        };
        var clusterResponse = await _client.PostAsJsonAsync("/api/v1/clusters", clusterRequest);
        clusterResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // Arrange - Register operator
        var operatorRequest = new
        {
            name = "nginx-ingress",
            namespace = "ingress-nginx",
            chartRepository = "https://kubernetes.github.io/ingress-nginx",
            chartName = "ingress-nginx",
            currentVersion = "v4.7.0"
        };
        var operatorResponse = await _client.PostAsJsonAsync("/api/v1/operators", operatorRequest);
        operatorResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // Act - Deploy operator
        var deploymentRequest = new
        {
            operatorName = "nginx-ingress",
            targetVersion = "v4.8.0",
            strategy = "Direct",
            targetClusters = new[] { "test-cluster" }
        };
        var deploymentResponse = await _client.PostAsJsonAsync("/api/v1/deployments", deploymentRequest);
        var deploymentResult = await deploymentResponse.Content.ReadFromJsonAsync<DeploymentResponse>();

        // Assert - Deployment started
        deploymentResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);
        deploymentResult.Status.Should().Be("Planning");

        // Wait for deployment to complete
        await WaitForDeploymentCompletion(deploymentResult.DeploymentId);

        // Assert - Deployment completed
        var statusResponse = await _client.GetAsync($"/api/v1/deployments/{deploymentResult.DeploymentId}");
        var status = await statusResponse.Content.ReadFromJsonAsync<DeploymentResponse>();
        status.Status.Should().Be("Completed");

        // Assert - Operator health is healthy
        var healthResponse = await _client.GetAsync("/api/v1/clusters/test-cluster/operators/nginx-ingress/health");
        var health = await healthResponse.Content.ReadFromJsonAsync<OperatorHealthResponse>();
        health.OverallHealth.Should().Be("Healthy");
    }

    private async Task WaitForDeploymentCompletion(string deploymentId, int timeoutSeconds = 300)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed.TotalSeconds < timeoutSeconds)
        {
            var response = await _client.GetAsync($"/api/v1/deployments/{deploymentId}");
            var deployment = await response.Content.ReadFromJsonAsync<DeploymentResponse>();

            if (deployment.Status == "Completed" || deployment.Status == "Failed")
                return;

            await Task.Delay(TimeSpan.FromSeconds(5));
        }

        throw new TimeoutException($"Deployment {deploymentId} did not complete within {timeoutSeconds} seconds");
    }
}
```

**Estimated Tests:** 30+ integration tests

---

## End-to-End Testing

**Target:** 20+ E2E tests

### Scope

Test complete user workflows from API to Kubernetes clusters with all real components.

**E2E Test Scenarios:**
1. Complete operator deployment lifecycle (register → deploy → upgrade → rollback)
2. CRD schema evolution workflow (register CRD → detect breaking change → approve → deploy)
3. Multi-cluster canary deployment (10 clusters, progressive rollout)
4. Blue-Green deployment with traffic switch verification

**Estimated Tests:** 20+ E2E tests

---

## Performance Testing

### Throughput Test

```csharp
[Fact]
public async Task Throughput_10ConcurrentDeployments_Completes()
{
    // Arrange
    var deploymentRequests = Enumerable.Range(0, 10)
        .Select(i => CreateDeploymentRequest($"operator-{i}"))
        .ToList();

    // Act
    var stopwatch = Stopwatch.StartNew();
    var tasks = deploymentRequests
        .Select(req => _client.PostAsJsonAsync("/api/v1/deployments", req))
        .ToList();
    await Task.WhenAll(tasks);
    stopwatch.Stop();

    // Assert
    tasks.All(t => t.Result.IsSuccessStatusCode).Should().BeTrue();
    stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromMinutes(30));
}
```

---

## Smoke Testing

**Target:** 6 smoke tests (< 60 seconds)

```bash
#!/bin/bash
# run-smoke-tests.sh

echo "Running operator manager smoke tests..."

# 1. Health check
curl -f http://localhost:5000/health || exit 1

# 2. Register cluster
curl -f -X POST http://localhost:5000/api/v1/clusters \
  -H "Content-Type: application/json" \
  -d '{"name":"test","environment":"Development","kubeconfig":"..."}' || exit 1

# 3. Register operator
curl -f -X POST http://localhost:5000/api/v1/operators \
  -H "Content-Type: application/json" \
  -d '{"name":"test-operator","namespace":"default","chartRepository":"https://charts.example.com","chartName":"test"}' || exit 1

echo "✓ All smoke tests passed"
```

---

## Test Organization

### Project Structure

```
tests/
├── HotSwap.Kubernetes.Tests/              # Unit tests
│   ├── Domain/
│   │   ├── OperatorTests.cs                # 25 tests
│   │   ├── OperatorDeploymentTests.cs      # 20 tests
│   │   ├── KubernetesClusterTests.cs       # 20 tests
│   │   └── CustomResourceDefinitionTests.cs # 20 tests
│   ├── Strategies/
│   │   ├── CanaryDeploymentStrategyTests.cs    # 20 tests
│   │   ├── BlueGreenDeploymentStrategyTests.cs # 20 tests
│   │   ├── RollingDeploymentStrategyTests.cs   # 20 tests
│   │   └── DirectDeploymentStrategyTests.cs    # 15 tests
│   ├── Infrastructure/
│   │   ├── KubernetesClientTests.cs        # 25 tests
│   │   └── CRDCompatibilityValidatorTests.cs # 20 tests
│   └── Orchestrator/
│       ├── OperatorOrchestratorTests.cs    # 25 tests
│       └── RollbackEngineTests.cs          # 20 tests
├── HotSwap.Kubernetes.IntegrationTests/    # Integration tests
│   ├── DeploymentFlowTests.cs              # 15 tests
│   ├── CanaryDeploymentTests.cs            # 10 tests
│   ├── CRDValidationTests.cs               # 10 tests
│   └── RollbackTests.cs                    # 15 tests
└── HotSwap.Kubernetes.E2ETests/            # End-to-end tests
    ├── OperatorLifecycleTests.cs           # 10 tests
    └── MultiClusterDeploymentTests.cs      # 10 tests
```

---

## TDD Workflow

Follow Red-Green-Refactor cycle for ALL code changes.

### Example: Implementing Canary Deployment

**Step 1: 🔴 RED - Write Failing Test**

```csharp
[Fact]
public async Task DeployAsync_WithCanaryStrategy_DeploysInStages()
{
    // Arrange
    var strategy = new CanaryDeploymentStrategy(_mockK8sClient.Object);
    var deployment = CreateDeployment();
    var clusters = CreateClusters(10);

    // Act
    var result = await strategy.DeployAsync(deployment, clusters);

    // Assert
    result.Success.Should().BeTrue();
    result.SuccessfulClusters.Should().HaveCount(10);
}
```

Run test: **FAILS** ❌ (CanaryDeploymentStrategy doesn't exist)

**Step 2: 🟢 GREEN - Minimal Implementation**

```csharp
public class CanaryDeploymentStrategy : IDeploymentStrategy
{
    public async Task<DeploymentResult> DeployAsync(
        OperatorDeployment deployment,
        List<KubernetesCluster> clusters)
    {
        foreach (var cluster in clusters)
        {
            await DeployToClusterAsync(deployment, cluster);
        }
        return DeploymentResult.SuccessResult(clusters.Select(c => c.Name).ToList());
    }
}
```

Run test: **PASSES** ✓

**Step 3: 🔵 REFACTOR - Improve Implementation**

```csharp
public class CanaryDeploymentStrategy : IDeploymentStrategy
{
    public async Task<DeploymentResult> DeployAsync(
        OperatorDeployment deployment,
        List<KubernetesCluster> clusters)
    {
        var config = deployment.GetCanaryConfig();
        var stages = CalculateCanaryStages(clusters, config);

        foreach (var stage in stages)
        {
            foreach (var cluster in stage.Clusters)
            {
                await DeployToClusterAsync(deployment, cluster);
            }

            await Task.Delay(config.EvaluationPeriod);

            var health = await ValidateStageHealthAsync(stage.Clusters);
            if (health.SuccessRate < config.SuccessThreshold)
            {
                await RollbackStageAsync(stage.Clusters);
                return DeploymentResult.Failure("Canary validation failed");
            }
        }

        return DeploymentResult.SuccessResult(clusters.Select(c => c.Name).ToList());
    }
}
```

Run test: **PASSES** ✓

---

## CI/CD Integration

### GitHub Actions Workflow

```yaml
name: Operator Manager Tests

on:
  push:
    branches: [main, claude/*]
  pull_request:
    branches: [main]

jobs:
  test:
    runs-on: ubuntu-latest

    steps:
      - uses: actions/checkout@v3

      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '8.0.x'

      - name: Setup kind
        uses: helm/kind-action@v1.8.0
        with:
          cluster_name: test-cluster

      - name: Restore dependencies
        run: dotnet restore

      - name: Build
        run: dotnet build --no-restore

      - name: Run unit tests
        run: dotnet test tests/HotSwap.Kubernetes.Tests/ --no-build --verbosity normal

      - name: Run integration tests
        run: dotnet test tests/HotSwap.Kubernetes.IntegrationTests/ --no-build --verbosity normal

      - name: Run E2E tests
        run: dotnet test tests/HotSwap.Kubernetes.E2ETests/ --no-build --verbosity normal

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
