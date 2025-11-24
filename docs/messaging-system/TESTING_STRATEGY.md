# Testing Strategy

**Version:** 1.0.0
**Target Coverage:** 85%+
**Test Count:** 400+ tests
**Framework:** xUnit, Moq, FluentAssertions

---

## Overview

The messaging system follows **Test-Driven Development (TDD)** with comprehensive test coverage across all layers. This document outlines the testing strategy, test organization, and best practices.

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
- Routing strategies (message routing algorithms)
- Persistence layer (Redis, PostgreSQL interactions)
- Schema registry (validation, compatibility)
- Delivery logic (retry, deduplication)
- Consumer management (rebalancing, health)

### Domain Models Tests

**File:** `tests/HotSwap.Distributed.Tests/Domain/MessageTests.cs`

```csharp
public class MessageTests
{
    [Fact]
    public void Message_WithValidData_PassesValidation()
    {
        // Arrange
        var message = new Message
        {
            MessageId = Guid.NewGuid().ToString(),
            TopicName = "test.topic",
            Payload = "{\"data\":\"value\"}",
            SchemaVersion = "1.0"
        };

        // Act
        var isValid = message.IsValid(out var errors);

        // Assert
        isValid.Should().BeTrue();
        errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("", "test.topic", "{}", "1.0", "MessageId is required")]
    [InlineData("msg-1", "", "{}", "1.0", "TopicName is required")]
    [InlineData("msg-1", "test", "", "1.0", "Payload is required")]
    [InlineData("msg-1", "test", "{}", "", "SchemaVersion is required")]
    public void Message_WithMissingRequiredField_FailsValidation(
        string messageId, string topicName, string payload, string schemaVersion, string expectedError)
    {
        // Arrange
        var message = new Message
        {
            MessageId = messageId,
            TopicName = topicName,
            Payload = payload,
            SchemaVersion = schemaVersion
        };

        // Act
        var isValid = message.IsValid(out var errors);

        // Assert
        isValid.Should().BeFalse();
        errors.Should().Contain(expectedError);
    }

    [Fact]
    public void IsExpired_WhenTTLExceeded_ReturnsTrue()
    {
        // Arrange
        var message = new Message
        {
            MessageId = "msg-1",
            TopicName = "test",
            Payload = "{}",
            SchemaVersion = "1.0",
            ExpiresAt = DateTime.UtcNow.AddSeconds(-1)
        };

        // Act
        var isExpired = message.IsExpired();

        // Assert
        isExpired.Should().BeTrue();
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(5, true)]
    [InlineData(9, true)]
    [InlineData(10, false)]
    public void Message_PriorityValidation_ValidatesCorrectly(int priority, bool shouldBeValid)
    {
        // Arrange
        var message = new Message
        {
            MessageId = "msg-1",
            TopicName = "test",
            Payload = "{}",
            SchemaVersion = "1.0",
            Priority = priority
        };

        // Act
        var isValid = message.IsValid(out var errors);

        // Assert
        isValid.Should().Be(shouldBeValid);
        if (!shouldBeValid)
        {
            errors.Should().Contain(e => e.Contains("Priority"));
        }
    }
}
```

**Estimated Tests:** 20+ tests per domain model (×5 models = 100 tests)

---

### Routing Strategy Tests

**File:** `tests/HotSwap.Distributed.Tests/Routing/LoadBalancedRoutingStrategyTests.cs`

```csharp
public class LoadBalancedRoutingStrategyTests
{
    private readonly LoadBalancedRoutingStrategy _strategy;
    private readonly Mock<IMessageDelivery> _mockDelivery;

    public LoadBalancedRoutingStrategyTests()
    {
        _mockDelivery = new Mock<IMessageDelivery>();
        _strategy = new LoadBalancedRoutingStrategy(_mockDelivery.Object);
    }

    [Fact]
    public async Task RouteAsync_WithMultipleConsumers_UsesRoundRobin()
    {
        // Arrange
        var consumers = CreateConsumers(3);
        var messages = CreateMessages(6);

        // Act
        var results = new List<RouteResult>();
        foreach (var message in messages)
        {
            results.Add(await _strategy.RouteAsync(message, consumers));
        }

        // Assert
        results.Should().AllSatisfy(r => r.Success.Should().BeTrue());
        _mockDelivery.Verify(d => d.DeliverAsync(It.IsAny<Message>(), consumers[0]), Times.Exactly(2));
        _mockDelivery.Verify(d => d.DeliverAsync(It.IsAny<Message>(), consumers[1]), Times.Exactly(2));
        _mockDelivery.Verify(d => d.DeliverAsync(It.IsAny<Message>(), consumers[2]), Times.Exactly(2));
    }

    [Fact]
    public async Task RouteAsync_WithNoConsumers_ReturnsFailure()
    {
        // Arrange
        var message = CreateMessage();
        var consumers = new List<Consumer>();

        // Act
        var result = await _strategy.RouteAsync(message, consumers);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("No consumers available");
    }

    [Fact]
    public async Task RouteAsync_ConcurrentCalls_MaintainsRoundRobinOrder()
    {
        // Arrange
        var consumers = CreateConsumers(3);
        var tasks = Enumerable.Range(0, 100)
            .Select(i => _strategy.RouteAsync(CreateMessage($"msg-{i}"), consumers))
            .ToList();

        // Act
        await Task.WhenAll(tasks);

        // Assert
        _mockDelivery.Verify(d => d.DeliverAsync(It.IsAny<Message>(), It.IsAny<Consumer>()), Times.Exactly(100));
        // Each consumer should receive approximately 33 messages
        _mockDelivery.Verify(d => d.DeliverAsync(It.IsAny<Message>(), consumers[0]), Times.Between(30, 40, Moq.Range.Inclusive));
    }
}
```

**Estimated Tests:** 15+ tests per strategy (×5 strategies = 75 tests)

---

### Persistence Layer Tests


```csharp
{
    private readonly IConnectionMultiplexer _redis;

    {
        _redis = ConnectionMultiplexer.Connect("localhost:6379");
    }

    [Fact]
    public async Task StoreAsync_WithValidMessage_PersistsToRedis()
    {
        // Arrange
        var message = CreateMessage();

        // Act
        await _persistence.StoreAsync(message);

        // Assert
        var retrieved = await _persistence.RetrieveAsync(message.MessageId);
        retrieved.Should().NotBeNull();
        retrieved.MessageId.Should().Be(message.MessageId);
        retrieved.Payload.Should().Be(message.Payload);
    }

    [Fact]
    public async Task GetByTopicAsync_WithLimit_ReturnsCorrectCount()
    {
        // Arrange
        await StoreMessages("test.topic", count: 10);

        // Act
        var messages = await _persistence.GetByTopicAsync("test.topic", limit: 5);

        // Assert
        messages.Should().HaveCount(5);
    }

    [Fact]
    public async Task DeleteAsync_RemovesMessageFromRedis()
    {
        // Arrange
        var message = CreateMessage();
        await _persistence.StoreAsync(message);

        // Act
        await _persistence.DeleteAsync(message.MessageId);

        // Assert
        var retrieved = await _persistence.RetrieveAsync(message.MessageId);
        retrieved.Should().BeNull();
    }

    public async Task InitializeAsync()
    {
        // Clear test data before each test
        var server = _redis.GetServer(_redis.GetEndPoints().First());
        await server.FlushDatabaseAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;
}
```

**Estimated Tests:** 15+ tests for persistence layer

---

### Delivery Guarantee Tests

**File:** `tests/HotSwap.Distributed.Tests/Orchestrator/ExactlyOnceDeliveryTests.cs`

```csharp
public class ExactlyOnceDeliveryTests
{
    private readonly Mock<IDistributedLock> _mockLock;
    private readonly Mock<IDeduplicationStore> _mockDedup;
    private readonly ExactlyOnceDeliveryService _service;

    public ExactlyOnceDeliveryTests()
    {
        _mockLock = new Mock<IDistributedLock>();
        _mockDedup = new Mock<IDeduplicationStore>();
        _service = new ExactlyOnceDeliveryService(_mockLock.Object, _mockDedup.Object);
    }

    [Fact]
    public async Task DeliverAsync_WithNewMessage_DeliversOnce()
    {
        // Arrange
        var message = CreateMessage();
        message.Headers["IdempotencyKey"] = "test-key-1";

        _mockLock.Setup(l => l.AcquireAsync("test-key-1", It.IsAny<TimeSpan>()))
            .ReturnsAsync(new LockHandle("test-key-1"));
        _mockDedup.Setup(d => d.ExistsAsync("test-key-1"))
            .ReturnsAsync(false);

        // Act
        var result = await _service.DeliverAsync(message);

        // Assert
        result.Success.Should().BeTrue();
        _mockDedup.Verify(d => d.StoreAsync("test-key-1"), Times.Once);
    }

    [Fact]
    public async Task DeliverAsync_WithDuplicateMessage_DoesNotDeliver()
    {
        // Arrange
        var message = CreateMessage();
        message.Headers["IdempotencyKey"] = "duplicate-key";

        _mockDedup.Setup(d => d.ExistsAsync("duplicate-key"))
            .ReturnsAsync(true);

        // Act
        var result = await _service.DeliverAsync(message);

        // Assert
        result.Success.Should().BeTrue();
        result.WasDuplicate.Should().BeTrue();
        _mockLock.Verify(l => l.AcquireAsync(It.IsAny<string>(), It.IsAny<TimeSpan>()), Times.Never);
    }

    [Fact]
    public async Task DeliverAsync_ConcurrentDuplicates_OnlyOneSucceeds()
    {
        // Arrange
        var messages = Enumerable.Range(0, 10)
            .Select(i =>
            {
                var msg = CreateMessage($"msg-{i}");
                msg.Headers["IdempotencyKey"] = "same-key";
                return msg;
            })
            .ToList();

        var deliveryCount = 0;
        _mockDedup.Setup(d => d.ExistsAsync("same-key"))
            .ReturnsAsync(() => Interlocked.CompareExchange(ref deliveryCount, 1, 0) == 1);

        // Act
        var tasks = messages.Select(m => _service.DeliverAsync(m));
        var results = await Task.WhenAll(tasks);

        // Assert
        results.Count(r => !r.WasDuplicate).Should().Be(1);
        results.Count(r => r.WasDuplicate).Should().Be(9);
    }
}
```

**Estimated Tests:** 25+ tests for delivery guarantees

---

## Integration Testing

**Target:** 60+ integration tests

### Scope

Test multiple components working together with real dependencies (Redis, PostgreSQL).

**Test Scenarios:**
1. Publish → Queue → Consume → Ack (happy path)
2. Publish → Schema validation failure
3. Consume → Ack timeout → Requeue
4. Consumer crash → Rebalance
5. Broker upgrade → Zero message loss
6. Exactly-once delivery verification
7. DLQ message handling

### End-to-End Message Flow Test

**File:** `tests/HotSwap.Distributed.IntegrationTests/MessageFlowTests.cs`

```csharp
[Collection("Integration")]
public class MessageFlowTests : IClassFixture<TestServerFixture>
{
    private readonly HttpClient _client;
    private readonly TestServerFixture _fixture;

    public MessageFlowTests(TestServerFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.CreateClient();
    }

    [Fact]
    public async Task EndToEndFlow_PublishConsumeAck_WorksCorrectly()
    {
        // Arrange - Create topic
        var topicRequest = new
        {
            name = "integration.test",
            type = "Queue",
            schemaId = "test.schema.v1",
            deliveryGuarantee = "AtLeastOnce"
        };
        await _client.PostAsJsonAsync("/api/v1/topics", topicRequest);

        // Arrange - Create subscription
        var subscriptionRequest = new
        {
            topicName = "integration.test",
            consumerGroup = "test-consumer",
            type = "Pull"
        };
        await _client.PostAsJsonAsync("/api/v1/subscriptions", subscriptionRequest);

        // Act - Publish message
        var publishRequest = new
        {
            topicName = "integration.test",
            payload = "{\"test\":\"data\"}",
            schemaVersion = "1.0"
        };
        var publishResponse = await _client.PostAsJsonAsync("/api/v1/messages/publish", publishRequest);
        var publishResult = await publishResponse.Content.ReadFromJsonAsync<PublishResponse>();

        // Act - Consume message
        var consumeResponse = await _client.GetAsync($"/api/v1/messages/consume/integration.test?consumerGroup=test-consumer&limit=1");
        var consumeResult = await consumeResponse.Content.ReadFromJsonAsync<ConsumeResponse>();

        // Assert - Message received
        consumeResult.Messages.Should().HaveCount(1);
        var message = consumeResult.Messages[0];
        message.MessageId.Should().Be(publishResult.MessageId);
        message.Payload.Should().Be("{\"test\":\"data\"}");

        // Act - Acknowledge message
        var ackRequest = new { consumerGroup = "test-consumer" };
        var ackResponse = await _client.PostAsJsonAsync($"/api/v1/messages/{message.MessageId}/ack", ackRequest);

        // Assert - Ack successful
        ackResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - Message no longer in queue
        var consumeAgain = await _client.GetAsync($"/api/v1/messages/consume/integration.test?consumerGroup=test-consumer&limit=1");
        var consumeAgainResult = await consumeAgain.Content.ReadFromJsonAsync<ConsumeResponse>();
        consumeAgainResult.Messages.Should().BeEmpty();
    }

    [Fact]
    public async Task SchemaValidation_InvalidMessage_ReturnsError()
    {
        // Arrange
        var publishRequest = new
        {
            topicName = "integration.test",
            payload = "{\"invalid\":\"schema\"}",  // Invalid against schema
            schemaVersion = "1.0"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/messages/publish", publishRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        error.Error.Code.Should().Be("SCHEMA_VALIDATION_ERROR");
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
1. Complete deployment event lifecycle (publish → multiple consumers → ack)
2. Schema evolution workflow (register → detect breaking change → approve → use)
3. Consumer failure and recovery (consumer crash → rebalance → resume)
4. Broker upgrade with zero downtime (upgrade → verify no message loss)

### E2E Test Example

```csharp
[Collection("E2E")]
public class DeploymentEventLifecycleTests : IClassFixture<E2ETestFixture>
{
    [Fact]
    public async Task DeploymentEvent_MultipleConsumers_AllReceive()
    {
        // Arrange - Set up deployment event topic
        await SetupDeploymentEventTopic();

        // Arrange - Set up 3 consumers
        var notificationConsumer = await CreateConsumer("notification-service");
        var auditConsumer = await CreateConsumer("audit-service");
        var metricsConsumer = await CreateConsumer("metrics-service");

        // Act - Publish deployment event
        var deploymentEvent = new
        {
            event = "deployment.completed",
            executionId = "deploy-123",
            environment = "Production"
        };
        await PublishMessage("deployment.events", deploymentEvent);

        // Act - Consume from all consumers
        var notificationMsg = await ConsumeMessage(notificationConsumer);
        var auditMsg = await ConsumeMessage(auditConsumer);
        var metricsMsg = await ConsumeMessage(metricsConsumer);

        // Assert - All consumers received the event
        notificationMsg.Should().NotBeNull();
        auditMsg.Should().NotBeNull();
        metricsMsg.Should().NotBeNull();

        notificationMsg.Payload.Should().Contain("deploy-123");
        auditMsg.Payload.Should().Contain("deploy-123");
        metricsMsg.Payload.Should().Contain("deploy-123");

        // Act - Acknowledge from all consumers
        await AcknowledgeMessage(notificationMsg.MessageId, notificationConsumer);
        await AcknowledgeMessage(auditMsg.MessageId, auditConsumer);
        await AcknowledgeMessage(metricsMsg.MessageId, metricsConsumer);

        // Assert - Verify trace spans in Jaeger
        await AssertTraceExists("deployment.events", "deploy-123");
    }
}
```

---

## Performance Testing

**Target:** Meet SLA requirements

### Performance Test Scenarios

**Scenario 1: Throughput Test**

```csharp
[Fact]
public async Task Throughput_10KMessagesPerSecond_Achieved()
{
    // Arrange
    var messageCount = 100_000;
    var targetDuration = TimeSpan.FromSeconds(10); // 10K msg/sec

    // Act
    var stopwatch = Stopwatch.StartNew();
    var tasks = Enumerable.Range(0, messageCount)
        .Select(i => PublishMessage($"msg-{i}"))
        .ToList();
    await Task.WhenAll(tasks);
    stopwatch.Stop();

    // Assert
    stopwatch.Elapsed.Should().BeLessThan(targetDuration);
    var throughput = messageCount / stopwatch.Elapsed.TotalSeconds;
    throughput.Should().BeGreaterThan(10_000);
}
```

**Scenario 2: Latency Test**

```csharp
[Fact]
public async Task Latency_P99_LessThan100ms()
{
    // Arrange
    var messageCount = 1_000;
    var latencies = new List<double>();

    // Act
    for (int i = 0; i < messageCount; i++)
    {
        var stopwatch = Stopwatch.StartNew();
        await PublishAndConsumeMessage();
        stopwatch.Stop();
        latencies.Add(stopwatch.Elapsed.TotalMilliseconds);
    }

    // Assert
    var p50 = latencies.OrderBy(x => x).ElementAt((int)(messageCount * 0.50));
    var p95 = latencies.OrderBy(x => x).ElementAt((int)(messageCount * 0.95));
    var p99 = latencies.OrderBy(x => x).ElementAt((int)(messageCount * 0.99));

    p50.Should().BeLessThan(20);
    p95.Should().BeLessThan(50);
    p99.Should().BeLessThan(100);
}
```

---

## Smoke Testing

**Target:** 6 smoke tests (< 60 seconds)

Quick validation after deployment.

```bash
#!/bin/bash
# run-smoke-tests.sh

echo "Running messaging system smoke tests..."

# 1. Health check
curl -f http://localhost:5000/health || exit 1

# 2. Create topic
curl -f -X POST http://localhost:5000/api/v1/topics \
  -H "Content-Type: application/json" \
  -d '{"name":"smoke.test","type":"Queue","schemaId":"test.v1"}' || exit 1

# 3. Publish message
curl -f -X POST http://localhost:5000/api/v1/messages/publish \
  -H "Content-Type: application/json" \
  -d '{"topicName":"smoke.test","payload":"{}","schemaVersion":"1.0"}' || exit 1

echo "✓ All smoke tests passed"
```

---

## Test Organization

### Project Structure

```
tests/
├── HotSwap.Distributed.Tests/              # Unit tests
│   ├── Domain/
│   │   ├── MessageTests.cs                  # 20 tests
│   │   ├── TopicTests.cs                    # 15 tests
│   │   ├── SubscriptionTests.cs             # 15 tests
│   │   └── SchemaTests.cs                   # 15 tests
│   ├── Routing/
│   │   ├── DirectRoutingStrategyTests.cs    # 10 tests
│   │   ├── FanOutRoutingStrategyTests.cs    # 15 tests
│   │   ├── LoadBalancedRoutingStrategyTests.cs  # 15 tests
│   │   ├── PriorityRoutingStrategyTests.cs  # 12 tests
│   │   └── ContentBasedRoutingStrategyTests.cs  # 15 tests
│   ├── Infrastructure/
│   │   ├── SchemaValidatorTests.cs          # 20 tests
│   │   └── DeliveryServiceTests.cs          # 25 tests
│   └── Orchestrator/
│       ├── MessageRouterTests.cs            # 20 tests
│       └── ExactlyOnceDeliveryTests.cs      # 25 tests
├── HotSwap.Distributed.IntegrationTests/    # Integration tests
│   ├── MessageFlowTests.cs                  # 15 tests
│   ├── SchemaValidationTests.cs             # 10 tests
│   └── DeliveryGuaranteeTests.cs            # 15 tests
└── HotSwap.Distributed.E2ETests/            # End-to-end tests
    ├── DeploymentEventLifecycleTests.cs     # 5 tests
    ├── SchemaEvolutionTests.cs              # 5 tests
    └── BrokerUpgradeTests.cs                # 5 tests
```

---

## TDD Workflow

Follow Red-Green-Refactor cycle for ALL code changes.

### Example: Implementing Fan-Out Routing

**Step 1: 🔴 RED - Write Failing Test**

```csharp
[Fact]
public async Task RouteAsync_WithThreeConsumers_DeliversToAll()
{
    // Arrange
    var strategy = new FanOutRoutingStrategy(_mockDelivery.Object);
    var message = CreateMessage();
    var consumers = CreateConsumers(3);

    // Act
    var result = await strategy.RouteAsync(message, consumers);

    // Assert
    result.Success.Should().BeTrue();
    result.TargetConsumers.Should().HaveCount(3);
    _mockDelivery.Verify(d => d.DeliverAsync(message, consumers[0]), Times.Once);
    _mockDelivery.Verify(d => d.DeliverAsync(message, consumers[1]), Times.Once);
    _mockDelivery.Verify(d => d.DeliverAsync(message, consumers[2]), Times.Once);
}
```

Run test: **FAILS** ❌ (FanOutRoutingStrategy doesn't exist)

**Step 2: 🟢 GREEN - Minimal Implementation**

```csharp
public class FanOutRoutingStrategy : IRoutingStrategy
{
    public async Task<RouteResult> RouteAsync(Message message, List<Consumer> consumers)
    {
        foreach (var consumer in consumers)
        {
            await DeliverAsync(message, consumer);
        }
        return RouteResult.Success(consumers);
    }
}
```

Run test: **PASSES** ✓

**Step 3: 🔵 REFACTOR - Improve Implementation**

```csharp
public class FanOutRoutingStrategy : IRoutingStrategy
{
    public async Task<RouteResult> RouteAsync(Message message, List<Consumer> consumers)
    {
        if (consumers.Count == 0)
            return RouteResult.Failure("No consumers available");

        // Deliver to ALL consumers in parallel
        var deliveryTasks = consumers.Select(c => DeliverAsync(message, c));
        await Task.WhenAll(deliveryTasks);

        return RouteResult.Success(consumers);
    }
}
```

Run test: **PASSES** ✓

---

## CI/CD Integration

### GitHub Actions Workflow

```yaml
name: Messaging System Tests

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
        run: dotnet test tests/HotSwap.Distributed.Tests/ --no-build --verbosity normal

      - name: Run integration tests
        run: dotnet test tests/HotSwap.Distributed.IntegrationTests/ --no-build --verbosity normal

      - name: Run E2E tests
        run: dotnet test tests/HotSwap.Distributed.E2ETests/ --no-build --verbosity normal

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

**Last Updated:** 2025-11-16
**Test Count:** 400+ tests
**Coverage Target:** 85%+
