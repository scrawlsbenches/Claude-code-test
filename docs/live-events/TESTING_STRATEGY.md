# Testing Strategy

**Version:** 1.0.0
**Target Coverage:** 85%+
**Test Count:** 350+ tests
**Framework:** xUnit, Moq, FluentAssertions

---

## Overview

The live event system follows **Test-Driven Development (TDD)** with comprehensive test coverage across all layers. This document outlines the testing strategy, test organization, and best practices.

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
5. [TDD Workflow](#tdd-workflow)
6. [CI/CD Integration](#cicd-integration)

---

## Unit Testing

**Target:** 280+ unit tests, 85%+ code coverage

### Scope

Test individual components in isolation with mocked dependencies.

**Components to Test:**
- Domain models (validation, business logic)
- Rollout strategies (deployment algorithms)
- Event scheduler (lifecycle automation)
- Segmentation engine (player targeting)
- Metrics calculation (engagement, revenue, sentiment)

### Domain Models Tests

**File:** `tests/HotSwap.LiveEvents.Tests/Domain/LiveEventTests.cs`

```csharp
public class LiveEventTests
{
    [Fact]
    public void LiveEvent_WithValidData_PassesValidation()
    {
        // Arrange
        var liveEvent = new LiveEvent
        {
            EventId = "summer-fest-2025",
            DisplayName = "Summer Festival 2025",
            StartTime = DateTime.UtcNow.AddDays(7),
            EndTime = DateTime.UtcNow.AddDays(37),
            Configuration = new EventConfiguration
            {
                Rewards = new RewardConfiguration
                {
                    DailyLoginBonus = 100
                }
            },
            CreatedBy = "gamedesigner@example.com"
        };

        // Act
        var isValid = liveEvent.IsValid(out var errors);

        // Assert
        isValid.Should().BeTrue();
        errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("", "Summer Fest", "EventId is required")]
    [InlineData("INVALID-ID", "Summer Fest", "EventId must contain only lowercase")]
    [InlineData("summer-fest", "", "DisplayName is required")]
    public void LiveEvent_WithMissingRequiredField_FailsValidation(
        string eventId, string displayName, string expectedError)
    {
        // Arrange
        var liveEvent = new LiveEvent
        {
            EventId = eventId,
            DisplayName = displayName,
            StartTime = DateTime.UtcNow.AddDays(7),
            EndTime = DateTime.UtcNow.AddDays(37),
            Configuration = new EventConfiguration(),
            CreatedBy = "test@example.com"
        };

        // Act
        var isValid = liveEvent.IsValid(out var errors);

        // Assert
        isValid.Should().BeFalse();
        errors.Should().Contain(expectedError);
    }

    [Fact]
    public void IsActive_WhenEventIsRunning_ReturnsTrue()
    {
        // Arrange
        var liveEvent = new LiveEvent
        {
            EventId = "active-event",
            DisplayName = "Active Event",
            State = EventState.Active,
            StartTime = DateTime.UtcNow.AddHours(-1),
            EndTime = DateTime.UtcNow.AddHours(1),
            Configuration = new EventConfiguration(),
            CreatedBy = "test@example.com"
        };

        // Act
        var isActive = liveEvent.IsActive();

        // Assert
        isActive.Should().BeTrue();
    }

    [Theory]
    [InlineData(EventState.Draft, false)]
    [InlineData(EventState.Scheduled, false)]
    [InlineData(EventState.Active, true)]
    [InlineData(EventState.Paused, false)]
    [InlineData(EventState.Completed, false)]
    [InlineData(EventState.Cancelled, false)]
    public void IsActive_WithDifferentStates_ReturnsCorrectly(EventState state, bool expectedActive)
    {
        // Arrange
        var liveEvent = new LiveEvent
        {
            EventId = "test-event",
            DisplayName = "Test Event",
            State = state,
            StartTime = DateTime.UtcNow.AddHours(-1),
            EndTime = DateTime.UtcNow.AddHours(1),
            Configuration = new EventConfiguration(),
            CreatedBy = "test@example.com"
        };

        // Act
        var isActive = liveEvent.IsActive();

        // Assert
        isActive.Should().Be(expectedActive);
    }

    [Fact]
    public void CanPlayerParticipate_WhenPlayerInTargetSegment_ReturnsTrue()
    {
        // Arrange
        var liveEvent = new LiveEvent
        {
            EventId = "vip-event",
            DisplayName = "VIP Event",
            TargetSegments = new List<string> { "vip-tier-3", "vip-tier-2" },
            StartTime = DateTime.UtcNow.AddDays(7),
            EndTime = DateTime.UtcNow.AddDays(37),
            Configuration = new EventConfiguration(),
            CreatedBy = "test@example.com"
        };
        var playerSegments = new List<string> { "vip-tier-3", "active-player" };

        // Act
        var canParticipate = liveEvent.CanPlayerParticipate(playerSegments);

        // Assert
        canParticipate.Should().BeTrue();
    }
}
```

**Estimated Tests:** 25+ tests per domain model (×6 models = 150 tests)

---

### Rollout Strategy Tests

**File:** `tests/HotSwap.LiveEvents.Tests/Rollout/CanaryRolloutStrategyTests.cs`

```csharp
public class CanaryRolloutStrategyTests
{
    private readonly CanaryRolloutStrategy _strategy;
    private readonly Mock<IEventDeploymentService> _mockDeployment;
    private readonly Mock<IMetricsCollector> _mockMetrics;

    public CanaryRolloutStrategyTests()
    {
        _mockDeployment = new Mock<IEventDeploymentService>();
        _mockMetrics = new Mock<IMetricsCollector>();
        _strategy = new CanaryRolloutStrategy(_mockDeployment.Object, _mockMetrics.Object);
    }

    [Fact]
    public async Task ExecuteAsync_WithHealthyMetrics_CompletesAllBatches()
    {
        // Arrange
        var deployment = CreateDeployment(batches: new[] { 10, 30, 50, 100 });
        _mockMetrics.Setup(m => m.GetMetricsAsync(It.IsAny<string>(), It.IsAny<List<string>>()))
            .ReturnsAsync(new EventMetrics
            {
                Engagement = new EngagementMetrics { ParticipationRate = 0.45 }
            });

        // Act
        var result = await _strategy.ExecuteAsync(deployment);

        // Assert
        result.Success.Should().BeTrue();
        result.SuccessfulRegions.Should().BeEquivalentTo(deployment.Regions);
        _mockDeployment.Verify(m => m.DeployToBatchAsync(deployment, 10), Times.Once);
        _mockDeployment.Verify(m => m.DeployToBatchAsync(deployment, 30), Times.Once);
        _mockDeployment.Verify(m => m.DeployToBatchAsync(deployment, 50), Times.Once);
        _mockDeployment.Verify(m => m.DeployToBatchAsync(deployment, 100), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WithParticipationDrop_TriggersAutoRollback()
    {
        // Arrange
        var deployment = CreateDeployment(
            batches: new[] { 10, 30, 50, 100 },
            rollbackThreshold: new RollbackThreshold { ParticipationRateDrop = 0.2 }
        );

        // First batch healthy, second batch unhealthy
        _mockMetrics.SetupSequence(m => m.GetMetricsAsync(It.IsAny<string>(), It.IsAny<List<string>>()))
            .ReturnsAsync(new EventMetrics { Engagement = new EngagementMetrics { ParticipationRate = 0.45 } })
            .ReturnsAsync(new EventMetrics { Engagement = new EngagementMetrics { ParticipationRate = 0.20 } }); // 55% drop

        // Act
        var result = await _strategy.ExecuteAsync(deployment);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Participation rate dropped");
        _mockDeployment.Verify(m => m.RollbackAsync(deployment), Times.Once);
    }
}
```

**Estimated Tests:** 12+ tests per strategy (×5 strategies = 60 tests)

---

### Segmentation Engine Tests

**File:** `tests/HotSwap.LiveEvents.Tests/Segmentation/SegmentationEngineTests.cs`

```csharp
public class SegmentationEngineTests
{
    [Fact]
    public void EvaluateMembership_WhenPlayerMatchesCriteria_ReturnsTrue()
    {
        // Arrange
        var engine = new SegmentationEngine();
        var segment = new PlayerSegment
        {
            SegmentId = "vip-tier-3",
            DisplayName = "VIP Tier 3",
            Type = SegmentType.VIP,
            Criteria = new SegmentCriteria
            {
                LifetimeSpend = (100m, 500m),
                AccountAgeDays = (30, int.MaxValue)
            },
            CreatedBy = "test@example.com"
        };
        var player = new PlayerProfile
        {
            PlayerId = "player123",
            Level = 50,
            CreatedAt = DateTime.UtcNow.AddDays(-90),
            LifetimeSpend = 250m,
            LastLoginAt = DateTime.UtcNow.AddHours(-2),
            CountryCode = "US",
            Platform = "pc"
        };

        // Act
        var isMember = engine.EvaluateMembership(player, segment);

        // Assert
        isMember.Should().BeTrue();
    }

    [Theory]
    [InlineData(50, true)]   // $50 spend (below min)
    [InlineData(150, true)]  // $150 spend (in range)
    [InlineData(600, false)] // $600 spend (above max)
    public void EvaluateMembership_WithDifferentSpendLevels_ReturnsCorrectly(decimal spend, bool expectedMember)
    {
        // Test spend ranges
    }
}
```

**Estimated Tests:** 15+ tests for segmentation logic

---

## Integration Testing

**Target:** 50+ integration tests

### Scope

Test interactions between components with real dependencies (database, cache).

**Components to Test:**
- Event repository (PostgreSQL integration)
- Event cache (Redis integration)
- API endpoints (HTTP integration)
- Scheduler (background job integration)
- Metrics collector (Prometheus integration)

### Event Repository Tests

**File:** `tests/HotSwap.LiveEvents.IntegrationTests/Repository/EventRepositoryTests.cs`

```csharp
[Collection("Database")]
public class EventRepositoryTests : IAsyncLifetime
{
    private readonly PostgreSQLEventRepository _repository;
    private readonly IDbConnection _connection;

    public EventRepositoryTests()
    {
        _connection = new NpgsqlConnection("Host=localhost;Database=liveevents_test;");
        _repository = new PostgreSQLEventRepository(_connection);
    }

    [Fact]
    public async Task CreateEventAsync_WithValidEvent_StoresInDatabase()
    {
        // Arrange
        var liveEvent = CreateTestEvent("test-event-1");

        // Act
        await _repository.CreateEventAsync(liveEvent);
        var retrieved = await _repository.GetEventAsync("test-event-1");

        // Assert
        retrieved.Should().NotBeNull();
        retrieved.EventId.Should().Be("test-event-1");
        retrieved.DisplayName.Should().Be(liveEvent.DisplayName);
    }

    [Fact]
    public async Task GetActiveEventsAsync_WithRegionFilter_ReturnsOnlyActiveEvents()
    {
        // Arrange
        await _repository.CreateEventAsync(CreateTestEvent("active-1", EventState.Active));
        await _repository.CreateEventAsync(CreateTestEvent("draft-1", EventState.Draft));
        await _repository.CreateEventAsync(CreateTestEvent("active-2", EventState.Active));

        // Act
        var activeEvents = await _repository.GetActiveEventsAsync("us-east");

        // Assert
        activeEvents.Should().HaveCount(2);
        activeEvents.Should().OnlyContain(e => e.State == EventState.Active);
    }

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        await SeedDatabaseAsync();
    }

    public async Task DisposeAsync()
    {
        await CleanupDatabaseAsync();
        await _connection.CloseAsync();
    }
}
```

**Estimated Tests:** 30+ integration tests

---

## End-to-End Testing

**Target:** 20+ E2E tests

### Scope

Test complete user workflows from API to database.

**Workflows to Test:**
- Create event → Deploy → Activate → Query → Deactivate
- Create segment → Assign to event → Query player events
- Deploy with canary → Monitor metrics → Auto-rollback
- Create A/B test → Assign variants → Track metrics

### E2E Test Example

**File:** `tests/HotSwap.LiveEvents.E2ETests/EventLifecycleTests.cs`

```csharp
public class EventLifecycleTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public EventLifecycleTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CompleteEventLifecycle_CreatesDeploysActivatesAndQueries()
    {
        // 1. Create event
        var createRequest = new
        {
            eventId = "e2e-test-event",
            displayName = "E2E Test Event",
            type = "SeasonalPromotion",
            startTime = DateTime.UtcNow.AddMinutes(5),
            endTime = DateTime.UtcNow.AddHours(1),
            configuration = new { rewards = new { dailyLoginBonus = 100 } }
        };
        var createResponse = await _client.PostAsJsonAsync("/api/v1/events", createRequest);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // 2. Approve event
        var approveResponse = await _client.PostAsync("/api/v1/events/e2e-test-event/approve", null);
        approveResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // 3. Deploy event
        var deployRequest = new
        {
            eventId = "e2e-test-event",
            regions = new[] { "us-east" },
            strategy = "BlueGreen"
        };
        var deployResponse = await _client.PostAsJsonAsync("/api/v1/deployments", deployRequest);
        deployResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // 4. Activate event
        var activateResponse = await _client.PostAsync("/api/v1/events/e2e-test-event/activate", null);
        activateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // 5. Query player events
        var queryResponse = await _client.GetAsync("/api/v1/players/player123/events?region=us-east");
        queryResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var events = await queryResponse.Content.ReadFromJsonAsync<PlayerEventsResponse>();
        events.Events.Should().Contain(e => e.EventId == "e2e-test-event");
    }
}
```

**Estimated Tests:** 20+ E2E tests

---

## Performance Testing

### Load Tests

**Tool:** k6 or JMeter

**Scenarios:**

1. **Player Query Load Test**
   - Target: 50,000 queries/sec
   - Duration: 5 minutes
   - Success Rate: > 99.9%
   - p99 Latency: < 50ms

2. **Event Activation Load Test**
   - Target: 100 concurrent activations
   - Duration: 2 minutes
   - Success Rate: 100%
   - Activation Time: < 5 seconds

3. **Metrics Collection Load Test**
   - Target: 10,000 metric updates/sec
   - Duration: 10 minutes
   - Success Rate: > 99.9%

### Example k6 Script

```javascript
import http from 'k6/http';
import { check } from 'k6';

export let options = {
  stages: [
    { duration: '1m', target: 10000 }, // Ramp up to 10k users
    { duration: '3m', target: 10000 }, // Stay at 10k
    { duration: '1m', target: 0 },     // Ramp down
  ],
  thresholds: {
    http_req_duration: ['p(99)<50'], // 99% of requests under 50ms
    http_req_failed: ['rate<0.01'],  // Error rate < 1%
  },
};

export default function () {
  const res = http.get('https://api.example.com/api/v1/players/player123/events?region=us-east');
  check(res, {
    'status is 200': (r) => r.status === 200,
    'has events': (r) => JSON.parse(r.body).events.length > 0,
  });
}
```

---

## TDD Workflow

### Red-Green-Refactor Cycle

1. **Red:** Write a failing test
2. **Green:** Write minimal code to make it pass
3. **Refactor:** Clean up code while keeping tests green

### Example TDD Session

**Task:** Implement EventStateMachine

**Step 1 - Red:** Write failing test
```csharp
[Fact]
public void TransitionTo_FromDraftToScheduled_Succeeds()
{
    // Arrange
    var stateMachine = new EventStateMachine(EventState.Draft);

    // Act
    var result = stateMachine.TransitionTo(EventState.Scheduled);

    // Assert
    result.Should().BeTrue();
    stateMachine.CurrentState.Should().Be(EventState.Scheduled);
}
```

**Step 2 - Green:** Write minimal implementation
```csharp
public class EventStateMachine
{
    public EventState CurrentState { get; private set; }

    public EventStateMachine(EventState initialState)
    {
        CurrentState = initialState;
    }

    public bool TransitionTo(EventState newState)
    {
        // Minimal implementation
        if (CurrentState == EventState.Draft && newState == EventState.Scheduled)
        {
            CurrentState = newState;
            return true;
        }
        return false;
    }
}
```

**Step 3 - Refactor:** Generalize implementation
```csharp
public bool TransitionTo(EventState newState)
{
    if (IsValidTransition(CurrentState, newState))
    {
        CurrentState = newState;
        return true;
    }
    return false;
}

private bool IsValidTransition(EventState from, EventState to)
{
    var validTransitions = new Dictionary<EventState, List<EventState>>
    {
        { EventState.Draft, new() { EventState.Scheduled, EventState.Cancelled } },
        { EventState.Scheduled, new() { EventState.Active, EventState.Cancelled } },
        // ... more transitions
    };

    return validTransitions.ContainsKey(from) && validTransitions[from].Contains(to);
}
```

---

## CI/CD Integration

### GitHub Actions Workflow

```yaml
name: CI/CD

on: [push, pull_request]

jobs:
  test:
    runs-on: ubuntu-latest

    services:
      postgres:
        image: postgres:15
        env:
          POSTGRES_DB: liveevents_test
          POSTGRES_PASSWORD: test
        ports:
          - 5432:5432

      redis:
        image: redis:7
        ports:
          - 6379:6379

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
        run: dotnet test tests/HotSwap.LiveEvents.Tests --no-build --verbosity normal

      - name: Run Integration Tests
        run: dotnet test tests/HotSwap.LiveEvents.IntegrationTests --no-build --verbosity normal
        env:
          ConnectionStrings__PostgreSQL: "Host=localhost;Database=liveevents_test;Username=postgres;Password=test"
          ConnectionStrings__Redis: "localhost:6379"

      - name: Generate Coverage Report
        run: dotnet test --collect:"XPlat Code Coverage"

      - name: Upload Coverage to Codecov
        uses: codecov/codecov-action@v3
```

### Test Coverage Requirements

- **Minimum Coverage:** 85%
- **Critical Paths:** 95% (event activation, deployment, rollback)
- **Coverage Report:** Generated on every PR
- **PR Blocked:** If coverage drops below threshold

---

**Document Version:** 1.0.0
**Last Updated:** 2025-11-23
**Framework:** xUnit 2.5+, Moq 4.20+, FluentAssertions 6.12+
