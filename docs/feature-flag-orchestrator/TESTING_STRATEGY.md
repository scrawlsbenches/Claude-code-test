# Testing Strategy

**Version:** 1.0.0
**Target Coverage:** 85%+
**Test Count:** 350+ tests
**Framework:** xUnit, Moq, FluentAssertions

---

## Overview

The feature flag system follows **Test-Driven Development (TDD)** with comprehensive test coverage across all layers. This document outlines the testing strategy, test organization, and best practices.

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
- Rollout strategies (progressive rollout algorithms)
- Evaluation engine (flag evaluation logic)
- Targeting rules (rule matching)
- Statistical analysis (A/B testing calculations)

### Domain Models Tests

**File:** `tests/HotSwap.FeatureFlags.Tests/Domain/FeatureFlagTests.cs`

```csharp
public class FeatureFlagTests
{
    [Fact]
    public void FeatureFlag_WithValidData_PassesValidation()
    {
        // Arrange
        var flag = new FeatureFlag
        {
            Name = "new-checkout-flow",
            Type = FlagType.Boolean,
            DefaultValue = "false",
            CreatedBy = "admin@example.com"
        };

        // Act
        var isValid = flag.IsValid(out var errors);

        // Assert
        isValid.Should().BeTrue();
        errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("", "Invalid name")]
    [InlineData("invalid name", "Invalid name")] // spaces not allowed
    [InlineData("flag@#$", "Invalid name")] // special chars not allowed
    public void FeatureFlag_WithInvalidName_FailsValidation(string name, string expectedError)
    {
        // Arrange
        var flag = new FeatureFlag
        {
            Name = name,
            Type = FlagType.Boolean,
            DefaultValue = "false",
            CreatedBy = "admin@example.com"
        };

        // Act
        var isValid = flag.IsValid(out var errors);

        // Assert
        isValid.Should().BeFalse();
        errors.Should().Contain(e => e.Contains("Name"));
    }

    [Theory]
    [InlineData(FlagType.Boolean, "true", true)]
    [InlineData(FlagType.Boolean, "false", true)]
    [InlineData(FlagType.Boolean, "invalid", false)]
    [InlineData(FlagType.Number, "123", true)]
    [InlineData(FlagType.Number, "123.45", true)]
    [InlineData(FlagType.Number, "abc", false)]
    [InlineData(FlagType.JSON, "{\"key\":\"value\"}", true)]
    [InlineData(FlagType.JSON, "invalid json", false)]
    public void ValidateDefaultValue_ValidatesCorrectly(FlagType type, string value, bool shouldBeValid)
    {
        // Arrange
        var flag = new FeatureFlag
        {
            Name = "test-flag",
            Type = type,
            DefaultValue = value,
            CreatedBy = "admin@example.com"
        };

        // Act
        var isValid = flag.IsValid(out var errors);

        // Assert
        isValid.Should().Be(shouldBeValid);
        if (!shouldBeValid)
        {
            errors.Should().Contain(e => e.Contains("DefaultValue"));
        }
    }

    [Fact]
    public void Archive_SetsArchivedAtAndStatus()
    {
        // Arrange
        var flag = new FeatureFlag
        {
            Name = "test-flag",
            Type = FlagType.Boolean,
            DefaultValue = "false",
            CreatedBy = "admin@example.com",
            Status = FlagStatus.Active
        };

        // Act
        flag.Archive();

        // Assert
        flag.ArchivedAt.Should().BeCloseTo(DateTime.UtcNow, precision: TimeSpan.FromSeconds(1));
        flag.Status.Should().Be(FlagStatus.Archived);
        flag.IsActive().Should().BeFalse();
    }
}
```

**Estimated Tests:** 20+ tests per domain model (×5 models = 100 tests)

---

### Rollout Strategy Tests

**File:** `tests/HotSwap.FeatureFlags.Tests/Rollout/CanaryRolloutStrategyTests.cs`

```csharp
public class CanaryRolloutStrategyTests
{
    private readonly Mock<IFlagCache> _mockCache;
    private readonly CanaryRolloutStrategy _strategy;

    public CanaryRolloutStrategyTests()
    {
        _mockCache = new Mock<IFlagCache>();
        _strategy = new CanaryRolloutStrategy(_mockCache.Object);
    }

    [Fact]
    public async Task RolloutAsync_Stage0_Enables10Percent()
    {
        // Arrange
        var flag = CreateFlagWithCanaryRollout();
        var contexts = CreateContexts(100); // 100 users

        // Act
        var results = new List<RolloutResult>();
        foreach (var context in contexts)
        {
            results.Add(await _strategy.RolloutAsync(flag, context));
        }

        // Assert
        var enabledCount = results.Count(r => r.Enabled);
        enabledCount.Should().BeInRange(8, 12); // ~10% (allow variance)
    }

    [Fact]
    public async Task RolloutAsync_SameUser_ConsistentResult()
    {
        // Arrange
        var flag = CreateFlagWithCanaryRollout();
        var context = new EvaluationContext { UserId = "user-123" };

        // Act
        var result1 = await _strategy.RolloutAsync(flag, context);
        var result2 = await _strategy.RolloutAsync(flag, context);
        var result3 = await _strategy.RolloutAsync(flag, context);

        // Assert
        result1.Enabled.Should().Be(result2.Enabled);
        result2.Enabled.Should().Be(result3.Enabled);
        // Same user always gets same result (deterministic)
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(1, 30)]
    [InlineData(2, 50)]
    [InlineData(3, 100)]
    public async Task RolloutAsync_DifferentStages_CorrectPercentages(int stageIndex, int expectedPercentage)
    {
        // Arrange
        var flag = CreateFlagWithCanaryRollout();
        flag.ActiveRollout.CurrentStageIndex = stageIndex;
        var contexts = CreateContexts(100);

        // Act
        var results = new List<RolloutResult>();
        foreach (var context in contexts)
        {
            results.Add(await _strategy.RolloutAsync(flag, context));
        }

        // Assert
        var enabledCount = results.Count(r => r.Enabled);
        var tolerance = expectedPercentage == 100 ? 0 : 3;
        enabledCount.Should().BeInRange(expectedPercentage - tolerance, expectedPercentage + tolerance);
    }

    [Fact]
    public async Task GetBucketPercentage_SameUserId_SameBucket()
    {
        // Arrange
        var context1 = new EvaluationContext { UserId = "user-123" };
        var context2 = new EvaluationContext { UserId = "user-123" };

        // Act
        var bucket1 = context1.GetBucketPercentage();
        var bucket2 = context2.GetBucketPercentage();

        // Assert
        bucket1.Should().Be(bucket2);
    }

    [Fact]
    public async Task GetBucketPercentage_Returns0To99()
    {
        // Arrange
        var contexts = CreateContexts(1000);

        // Act
        var buckets = contexts.Select(c => c.GetBucketPercentage()).ToList();

        // Assert
        buckets.Should().AllSatisfy(b => b.Should().BeInRange(0, 99));
        buckets.Distinct().Count().Should().BeGreaterThan(50); // Good distribution
    }
}
```

**Estimated Tests:** 20+ tests per strategy (×5 strategies = 100 tests)

---

### Evaluation Engine Tests

**File:** `tests/HotSwap.FeatureFlags.Tests/Orchestrator/EvaluationEngineTests.cs`

```csharp
public class EvaluationEngineTests
{
    private readonly Mock<IFlagRepository> _mockRepo;
    private readonly Mock<IFlagCache> _mockCache;
    private readonly EvaluationEngine _engine;

    public EvaluationEngineTests()
    {
        _mockRepo = new Mock<IFlagRepository>();
        _mockCache = new Mock<IFlagCache>();
        _engine = new EvaluationEngine(_mockRepo.Object, _mockCache.Object);
    }

    [Fact]
    public async Task EvaluateAsync_FlagNotFound_ReturnsDefaultValue()
    {
        // Arrange
        _mockCache.Setup(c => c.GetFlagAsync("nonexistent")).ReturnsAsync((FeatureFlag)null);
        _mockRepo.Setup(r => r.GetFlagAsync("nonexistent")).ReturnsAsync((FeatureFlag)null);
        var context = new EvaluationContext { UserId = "user-123" };

        // Act
        var result = await _engine.EvaluateAsync("nonexistent", context);

        // Assert
        result.Should().NotBeNull();
        result.Enabled.Should().BeFalse();
        result.Reason.Should().Contain("Flag not found");
    }

    [Fact]
    public async Task EvaluateAsync_CacheHit_UsesCachedValue()
    {
        // Arrange
        var flag = CreateTestFlag();
        _mockCache.Setup(c => c.GetFlagAsync(flag.Name)).ReturnsAsync(flag);
        var context = new EvaluationContext { UserId = "user-123" };

        // Act
        var result = await _engine.EvaluateAsync(flag.Name, context);

        // Assert
        result.FromCache.Should().BeTrue();
        _mockRepo.Verify(r => r.GetFlagAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task EvaluateAsync_TargetMatches_ReturnsTargetValue()
    {
        // Arrange
        var flag = CreateFlagWithTarget();
        _mockCache.Setup(c => c.GetFlagAsync(flag.Name)).ReturnsAsync(flag);
        var context = new EvaluationContext
        {
            UserId = "user-123",
            Attributes = new Dictionary<string, string> { { "tier", "premium" } }
        };

        // Act
        var result = await _engine.EvaluateAsync(flag.Name, context);

        // Assert
        result.Enabled.Should().BeTrue();
        result.Value.Should().Be("true");
        result.Reason.Should().Contain("User segment: Premium Users");
    }

    [Fact]
    public async Task EvaluateAsync_NoTargetMatch_ReturnsDefaultValue()
    {
        // Arrange
        var flag = CreateFlagWithTarget();
        _mockCache.Setup(c => c.GetFlagAsync(flag.Name)).ReturnsAsync(flag);
        var context = new EvaluationContext
        {
            UserId = "user-123",
            Attributes = new Dictionary<string, string> { { "tier", "free" } }
        };

        // Act
        var result = await _engine.EvaluateAsync(flag.Name, context);

        // Assert
        result.Enabled.Should().BeFalse();
        result.Value.Should().Be(flag.DefaultValue);
        result.Reason.Should().Contain("Default value");
    }
}
```

**Estimated Tests:** 25+ tests for evaluation engine

---

### Statistical Analysis Tests

**File:** `tests/HotSwap.FeatureFlags.Tests/Statistics/StatisticalAnalysisTests.cs`

```csharp
public class StatisticalAnalysisTests
{
    private readonly StatisticalAnalysisService _service;

    public StatisticalAnalysisTests()
    {
        _service = new StatisticalAnalysisService();
    }

    [Fact]
    public void ChiSquareTest_SignificantDifference_ReturnsLowPValue()
    {
        // Arrange
        var control = new VariantMetrics
        {
            SampleSize = 1000,
            Conversions = 50 // 5% conversion
        };
        var treatment = new VariantMetrics
        {
            SampleSize = 1000,
            Conversions = 100 // 10% conversion (2x better)
        };

        // Act
        var result = _service.ChiSquareTest(control, treatment);

        // Assert
        result.PValue.Should().BeLessThan(0.05); // Statistically significant
        result.IsSignificant.Should().BeTrue();
    }

    [Fact]
    public void ChiSquareTest_NoSignificantDifference_ReturnsHighPValue()
    {
        // Arrange
        var control = new VariantMetrics
        {
            SampleSize = 100,
            Conversions = 50
        };
        var treatment = new VariantMetrics
        {
            SampleSize = 100,
            Conversions = 52 // Tiny difference
        };

        // Act
        var result = _service.ChiSquareTest(control, treatment);

        // Assert
        result.PValue.Should().BeGreaterThan(0.05);
        result.IsSignificant.Should().BeFalse();
    }

    [Theory]
    [InlineData(100, 5, 100, 10, 0.173)] // 73% uplift
    [InlineData(1000, 50, 1000, 60, 0.20)] // 20% uplift
    public void CalculateRelativeUplift_CalculatesCorrectly(
        int controlSize, int controlConv, int treatmentSize, int treatmentConv, double expectedUplift)
    {
        // Arrange
        var control = new VariantMetrics { SampleSize = controlSize, Conversions = controlConv };
        var treatment = new VariantMetrics { SampleSize = treatmentSize, Conversions = treatmentConv };

        // Act
        var uplift = _service.CalculateRelativeUplift(control, treatment);

        // Assert
        uplift.Should().BeApproximately(expectedUplift, precision: 0.01);
    }
}
```

**Estimated Tests:** 20+ tests for statistical analysis

---

## Integration Testing

**Target:** 50+ integration tests

### Scope

Test multiple components working together with real dependencies (Redis, PostgreSQL).

**Test Scenarios:**
1. Create flag → Evaluate → Cache hit
2. Update flag → Cache invalidation → Fresh evaluation
3. Create rollout → Progress stages → Health check
4. Create experiment → Assign variants → Calculate significance
5. Anomaly detection → Automatic rollback

### End-to-End Flag Lifecycle Test

**File:** `tests/HotSwap.FeatureFlags.IntegrationTests/FlagLifecycleTests.cs`

```csharp
[Collection("Integration")]
public class FlagLifecycleTests : IClassFixture<TestServerFixture>
{
    private readonly HttpClient _client;

    public FlagLifecycleTests(TestServerFixture fixture)
    {
        _client = fixture.CreateClient();
    }

    [Fact]
    public async Task EndToEndFlow_CreateEvaluateUpdate_WorksCorrectly()
    {
        // Arrange - Create flag
        var createRequest = new
        {
            name = "integration-test-flag",
            type = "Boolean",
            defaultValue = "false",
            environment = "Development"
        };

        // Act - Create flag
        var createResponse = await _client.PostAsJsonAsync("/api/v1/flags", createRequest);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // Act - Evaluate flag (should be false)
        var evalResponse1 = await _client.GetAsync("/api/v1/flags/integration-test-flag/evaluate?userId=user-123");
        var evalResult1 = await evalResponse1.Content.ReadFromJsonAsync<FlagEvaluation>();

        // Assert - Initial evaluation
        evalResult1.Enabled.Should().BeFalse();
        evalResult1.Value.Should().Be("false");
        evalResult1.FromCache.Should().BeFalse(); // First eval (no cache)

        // Act - Evaluate again (should use cache)
        var evalResponse2 = await _client.GetAsync("/api/v1/flags/integration-test-flag/evaluate?userId=user-123");
        var evalResult2 = await evalResponse2.Content.ReadFromJsonAsync<FlagEvaluation>();

        // Assert - Cache hit
        evalResult2.FromCache.Should().BeTrue();

        // Act - Update flag default value
        var updateRequest = new { defaultValue = "true" };
        var updateResponse = await _client.PutAsJsonAsync("/api/v1/flags/integration-test-flag", updateRequest);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Wait for cache invalidation
        await Task.Delay(100);

        // Act - Evaluate after update
        var evalResponse3 = await _client.GetAsync("/api/v1/flags/integration-test-flag/evaluate?userId=user-123");
        var evalResult3 = await evalResponse3.Content.ReadFromJsonAsync<FlagEvaluation>();

        // Assert - Updated value
        evalResult3.Enabled.Should().BeTrue();
        evalResult3.Value.Should().Be("true");
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
1. Canary rollout lifecycle (10% → 30% → 50% → 100%)
2. A/B test with winner declaration
3. Anomaly detection → automatic rollback
4. Targeting rules with complex segments

### E2E Test Example

```csharp
[Collection("E2E")]
public class CanaryRolloutE2ETests : IClassFixture<E2ETestFixture>
{
    [Fact]
    public async Task CanaryRollout_FullLifecycle_WorksCorrectly()
    {
        // Arrange - Create flag
        await CreateFlag("canary-test-flag");

        // Arrange - Create canary rollout
        var rolloutRequest = new
        {
            strategy = "Canary",
            stages = new[]
            {
                new { percentage = 10, duration = "PT10S" }, // 10 seconds for test
                new { percentage = 30, duration = "PT10S" },
                new { percentage = 100 }
            },
            rollbackOnError = false
        };
        await CreateRollout("canary-test-flag", rolloutRequest);

        // Act - Evaluate for 100 users at stage 0 (10%)
        var results0 = await EvaluateForUsers("canary-test-flag", 100);
        var enabled0 = results0.Count(r => r.Enabled);

        // Assert - Stage 0
        enabled0.Should().BeInRange(8, 12); // ~10%

        // Wait for auto-progression to stage 1
        await Task.Delay(TimeSpan.FromSeconds(11));

        // Act - Evaluate for 100 users at stage 1 (30%)
        var results1 = await EvaluateForUsers("canary-test-flag", 100);
        var enabled1 = results1.Count(r => r.Enabled);

        // Assert - Stage 1
        enabled1.Should().BeInRange(27, 33); // ~30%

        // Wait for auto-progression to stage 2
        await Task.Delay(TimeSpan.FromSeconds(11));

        // Act - Evaluate for 100 users at stage 2 (100%)
        var results2 = await EvaluateForUsers("canary-test-flag", 100);
        var enabled2 = results2.Count(r => r.Enabled);

        // Assert - Stage 2 (100%)
        enabled2.Should().Be(100);

        // Verify rollout completed
        var rollout = await GetRollout("canary-test-flag");
        rollout.Status.Should().Be("Completed");
    }
}
```

---

## Performance Testing

**Target:** Meet SLA requirements

### Performance Test Scenarios

**Scenario 1: Evaluation Latency**

```csharp
[Fact]
public async Task Evaluation_CachedFlag_P99LessThan1ms()
{
    // Arrange
    var flag = await CreateAndCacheFlag("perf-test-flag");
    var context = new EvaluationContext { UserId = "user-123" };
    var latencies = new List<double>();

    // Act - 1000 evaluations
    for (int i = 0; i < 1000; i++)
    {
        var stopwatch = Stopwatch.StartNew();
        await _engine.EvaluateAsync(flag.Name, context);
        stopwatch.Stop();
        latencies.Add(stopwatch.Elapsed.TotalMilliseconds);
    }

    // Assert
    var p50 = latencies.OrderBy(x => x).ElementAt(500);
    var p95 = latencies.OrderBy(x => x).ElementAt(950);
    var p99 = latencies.OrderBy(x => x).ElementAt(990);

    p50.Should().BeLessThan(0.5);
    p95.Should().BeLessThan(0.8);
    p99.Should().BeLessThan(1.0);
}
```

**Scenario 2: Throughput Test**

```csharp
[Fact]
public async Task Throughput_100KEvalsPerSecond_Achieved()
{
    // Arrange
    var flag = await CreateAndCacheFlag("throughput-test");
    var evalCount = 100_000;
    var targetDuration = TimeSpan.FromSeconds(1);

    // Act
    var stopwatch = Stopwatch.StartNew();
    var tasks = Enumerable.Range(0, evalCount)
        .Select(i => EvaluateFlag(flag.Name, $"user-{i}"))
        .ToList();
    await Task.WhenAll(tasks);
    stopwatch.Stop();

    // Assert
    stopwatch.Elapsed.Should().BeLessThan(targetDuration);
    var throughput = evalCount / stopwatch.Elapsed.TotalSeconds;
    throughput.Should().BeGreaterThan(100_000);
}
```

---

## Test Organization

### Project Structure

```
tests/
├── HotSwap.FeatureFlags.Tests/              # Unit tests
│   ├── Domain/
│   │   ├── FeatureFlagTests.cs              # 20 tests
│   │   ├── RolloutTests.cs                  # 18 tests
│   │   ├── TargetTests.cs                   # 15 tests
│   │   ├── VariantTests.cs                  # 10 tests
│   │   └── ExperimentTests.cs               # 15 tests
│   ├── Rollout/
│   │   ├── DirectRolloutStrategyTests.cs    # 8 tests
│   │   ├── CanaryRolloutStrategyTests.cs    # 20 tests
│   │   ├── PercentageRolloutStrategyTests.cs # 15 tests
│   │   ├── UserSegmentRolloutStrategyTests.cs # 18 tests
│   │   └── TimeBasedRolloutStrategyTests.cs  # 12 tests
│   ├── Orchestrator/
│   │   ├── EvaluationEngineTests.cs         # 25 tests
│   │   ├── RolloutOrchestratorTests.cs      # 20 tests
│   │   └── VariantAssignmentTests.cs        # 15 tests
│   └── Statistics/
│       └── StatisticalAnalysisTests.cs      # 20 tests
├── HotSwap.FeatureFlags.IntegrationTests/   # Integration tests
│   ├── FlagLifecycleTests.cs                # 15 tests
│   ├── RolloutProgressionTests.cs           # 12 tests
│   └── AnomalyDetectionTests.cs             # 10 tests
└── HotSwap.FeatureFlags.E2ETests/           # End-to-end tests
    ├── CanaryRolloutE2ETests.cs             # 8 tests
    ├── ABTestingE2ETests.cs                 # 7 tests
    └── AutoRollbackE2ETests.cs              # 5 tests
```

---

## TDD Workflow

Follow Red-Green-Refactor cycle for ALL code changes.

### Example: Implementing Canary Rollout

**Step 1: 🔴 RED - Write Failing Test**

```csharp
[Fact]
public async Task RolloutAsync_Stage0_Enables10Percent()
{
    // Arrange
    var strategy = new CanaryRolloutStrategy();
    var flag = CreateFlagWithCanaryRollout();
    var contexts = CreateContexts(100);

    // Act
    var results = new List<RolloutResult>();
    foreach (var context in contexts)
    {
        results.Add(await strategy.RolloutAsync(flag, context));
    }

    // Assert
    var enabledCount = results.Count(r => r.Enabled);
    enabledCount.Should().BeInRange(8, 12); // ~10%
}
```

Run test: **FAILS** ❌ (CanaryRolloutStrategy doesn't exist)

**Step 2: 🟢 GREEN - Minimal Implementation**

```csharp
public class CanaryRolloutStrategy : IRolloutStrategy
{
    public async Task<RolloutResult> RolloutAsync(FeatureFlag flag, EvaluationContext context)
    {
        var currentStage = flag.ActiveRollout.GetCurrentStage();
        var userBucket = context.GetBucketPercentage();

        if (userBucket < currentStage.Percentage)
        {
            return new RolloutResult
            {
                Enabled = true,
                Value = flag.DefaultValue,
                Reason = $"Canary rollout: {currentStage.Percentage}%"
            };
        }

        return new RolloutResult
        {
            Enabled = false,
            Value = flag.DefaultValue,
            Reason = "Outside rollout percentage"
        };
    }
}
```

Run test: **PASSES** ✓

**Step 3: 🔵 REFACTOR - Improve Implementation**

Add error handling, logging, metrics...

---

## CI/CD Integration

### GitHub Actions Workflow

```yaml
name: Feature Flag Tests

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
        run: dotnet test tests/HotSwap.FeatureFlags.Tests/ --no-build --verbosity normal

      - name: Run integration tests
        run: dotnet test tests/HotSwap.FeatureFlags.IntegrationTests/ --no-build --verbosity normal

      - name: Run E2E tests
        run: dotnet test tests/HotSwap.FeatureFlags.E2ETests/ --no-build --verbosity normal

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
