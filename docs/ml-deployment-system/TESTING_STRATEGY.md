# Testing Strategy

**Version:** 1.0.0
**Target Coverage:** 85%+
**Test Count:** 350+ tests
**Framework:** xUnit, Moq, FluentAssertions

---

## Overview

The ML deployment system follows **Test-Driven Development (TDD)** with comprehensive test coverage across all layers.

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

## Unit Testing

**Target:** 280+ unit tests, 85%+ code coverage

### Model Domain Tests

**File:** `tests/HotSwap.MLDeployment.Tests/Domain/ModelTests.cs`

```csharp
public class ModelTests
{
    [Fact]
    public void Model_WithValidData_PassesValidation()
    {
        // Arrange
        var model = new Model
        {
            ModelId = Guid.NewGuid().ToString(),
            Name = "fraud-detection",
            Framework = ModelFramework.TensorFlow,
            Type = ModelType.Classification,
            Owner = "ml-team@example.com"
        };

        // Act
        var isValid = model.IsValid(out var errors);

        // Assert
        isValid.Should().BeTrue();
        errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("Fraud-Detection", false)] // Uppercase not allowed
    [InlineData("fraud_detection", false)] // Underscore not allowed
    [InlineData("fraud-detection", true)]  // Valid
    [InlineData("fraud123", true)]         // Valid
    public void Model_NameValidation_ValidatesCorrectly(string name, bool shouldBeValid)
    {
        var model = new Model
        {
            ModelId = "model-1",
            Name = name,
            Framework = ModelFramework.TensorFlow,
            Type = ModelType.Classification,
            Owner = "ml-team@example.com"
        };

        var isValid = model.IsValid(out var errors);

        isValid.Should().Be(shouldBeValid);
        if (!shouldBeValid)
        {
            errors.Should().Contain(e => e.Contains("Name"));
        }
    }
}
```

**Estimated Tests:** 25+ tests per domain model (×4 models = 100 tests)

---

### Deployment Strategy Tests

**File:** `tests/HotSwap.MLDeployment.Tests/Strategies/CanaryDeploymentTests.cs`

```csharp
public class CanaryDeploymentTests
{
    private readonly Mock<IModelServer> _mockServer;
    private readonly Mock<IMetricsCollector> _mockMetrics;
    private readonly CanaryDeploymentStrategy _strategy;

    public CanaryDeploymentTests()
    {
        _mockServer = new Mock<IModelServer>();
        _mockMetrics = new Mock<IMetricsCollector>();
        _strategy = new CanaryDeploymentStrategy(_mockServer.Object, _mockMetrics.Object);
    }

    [Fact]
    public async Task DeployAsync_WithGoodMetrics_CompletesSuccessfully()
    {
        // Arrange
        var newVersion = CreateModelVersion("2.0.0");
        var config = new CanaryConfig
        {
            InitialPercentage = 10,
            IncrementStep = 20,
            MonitoringDuration = TimeSpan.FromSeconds(1),
            Thresholds = new PerformanceThresholds
            {
                AccuracyDrop = 0.05,
                LatencyIncrease = 1.5
            }
        };

        _mockMetrics.Setup(m => m.GetAccuracy(It.IsAny<string>()))
            .ReturnsAsync(0.96); // Good accuracy
        _mockMetrics.Setup(m => m.GetLatency(It.IsAny<string>()))
            .ReturnsAsync(45.0); // Good latency

        // Act
        var result = await _strategy.DeployAsync(newVersion, config);

        // Assert
        result.Success.Should().BeTrue();
        result.TrafficPercentage.Should().Be(100);
        _mockServer.Verify(s => s.SetTrafficSplit(100, 0), Times.Once);
    }

    [Fact]
    public async Task DeployAsync_WithAccuracyDrop_RollsBack()
    {
        // Arrange
        var newVersion = CreateModelVersion("2.0.0");
        var config = new CanaryConfig
        {
            InitialPercentage = 10,
            Thresholds = new PerformanceThresholds { AccuracyDrop = 0.05 }
        };

        _mockMetrics.SetupSequence(m => m.GetAccuracy(It.IsAny<string>()))
            .ReturnsAsync(0.96)  // Baseline
            .ReturnsAsync(0.90); // Dropped below threshold

        // Act
        var result = await _strategy.DeployAsync(newVersion, config);

        // Assert
        result.Success.Should().BeFalse();
        result.IsRolledBack.Should().BeTrue();
        result.ErrorMessage.Should().Contain("Accuracy degraded");
    }

    [Fact]
    public async Task DeployAsync_ConcurrentRequests_MaintainsTrafficSplit()
    {
        // Test concurrent inference requests during deployment
        var tasks = Enumerable.Range(0, 1000)
            .Select(i => _strategy.RouteInferenceAsync(CreateRequest()))
            .ToList();

        await Task.WhenAll(tasks);

        // Verify traffic split is maintained
        var routedToNew = tasks.Count(t => t.Result.UsedVersion == "2.0.0");
        var expectedMin = 1000 * 0.09; // 9% (allowing 1% variance)
        var expectedMax = 1000 * 0.11; // 11%

        routedToNew.Should().BeInRange((int)expectedMin, (int)expectedMax);
    }
}
```

**Estimated Tests:** 20+ tests per strategy (×5 strategies = 100 tests)

---

## Integration Testing

**Target:** 50+ integration tests

### End-to-End Model Deployment Test

**File:** `tests/HotSwap.MLDeployment.IntegrationTests/DeploymentFlowTests.cs`

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
    public async Task EndToEndFlow_RegisterDeployInfer_WorksCorrectly()
    {
        // 1. Register model
        var registerRequest = new
        {
            name = "test-model",
            framework = "TensorFlow",
            type = "Classification",
            version = "1.0.0",
            artifactPath = "s3://models/test-model-v1.tar.gz",
            checksum = "sha256:abc123",
            inputSchema = "{\"type\":\"object\",\"properties\":{\"feature1\":{\"type\":\"number\"}}}",
            outputSchema = "{\"type\":\"object\",\"properties\":{\"prediction\":{\"type\":\"number\"}}}"
        };
        var registerResponse = await _client.PostAsJsonAsync("/api/v1/models", registerRequest);
        var model = await registerResponse.Content.ReadFromJsonAsync<ModelResponse>();

        // 2. Deploy model (Canary)
        var deployRequest = new
        {
            modelId = model.ModelId,
            versionId = model.Versions[0].VersionId,
            strategy = "Canary",
            environment = "Staging",
            strategyConfig = new
            {
                initialPercentage = 100, // Full deployment for testing
                thresholds = new { accuracyDrop = 0.05 }
            }
        };
        var deployResponse = await _client.PostAsJsonAsync("/api/v1/deployments", deployRequest);
        var deployment = await deployResponse.Content.ReadFromJsonAsync<DeploymentResponse>();

        // Wait for deployment to complete
        await WaitForDeploymentComplete(deployment.DeploymentId);

        // 3. Run inference
        var inferenceRequest = new
        {
            features = new { feature1 = 42.0 }
        };
        var inferenceResponse = await _client.PostAsJsonAsync($"/api/v1/inference/{model.Name}", inferenceRequest);
        var prediction = await inferenceResponse.Content.ReadFromJsonAsync<PredictionResponse>();

        // Assert
        prediction.Should().NotBeNull();
        prediction.ModelName.Should().Be("test-model");
        prediction.Version.Should().Be("1.0.0");
        prediction.Prediction.Should().NotBeNull();
    }
}
```

---

## End-to-End Testing

**Target:** 20+ E2E tests

### E2E Test Scenarios

1. Complete model lifecycle (register → deploy → inference → rollback)
2. Canary deployment with automatic rollback on accuracy drop
3. A/B testing with statistical significance
4. Shadow deployment validation
5. Data drift detection and alerting

---

## Performance Testing

### Throughput Test

```csharp
[Fact]
public async Task Throughput_1000RequestsPerSecond_Achieved()
{
    var requestCount = 10_000;
    var targetDuration = TimeSpan.FromSeconds(10); // 1K req/sec

    var stopwatch = Stopwatch.StartNew();
    var tasks = Enumerable.Range(0, requestCount)
        .Select(i => RunInferenceAsync($"req-{i}"))
        .ToList();
    await Task.WhenAll(tasks);
    stopwatch.Stop();

    stopwatch.Elapsed.Should().BeLessThan(targetDuration);
    var throughput = requestCount / stopwatch.Elapsed.TotalSeconds;
    throughput.Should().BeGreaterThan(1000);
}
```

---

## TDD Workflow

Follow Red-Green-Refactor cycle:

**Step 1: 🔴 RED - Write Failing Test**
```csharp
[Fact]
public async Task CanaryDeployment_IncreasesTraffic_Gradually()
{
    // Test fails (not implemented yet)
}
```

**Step 2: 🟢 GREEN - Minimal Implementation**
```csharp
public async Task<DeploymentResult> DeployAsync()
{
    // Minimal code to pass test
    return DeploymentResult.Success();
}
```

**Step 3: 🔵 REFACTOR - Improve Code**
```csharp
public async Task<DeploymentResult> DeployAsync()
{
    // Refactored with proper logic
    await SetTrafficSplit(10, 90);
    // ... rest of implementation
}
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
