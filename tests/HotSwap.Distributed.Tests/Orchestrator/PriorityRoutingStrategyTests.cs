using FluentAssertions;
using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Orchestrator.Routing;
using Xunit;

namespace HotSwap.Distributed.Tests.Orchestrator;

public class PriorityRoutingStrategyTests
{
    private readonly PriorityRoutingStrategy _strategy;

    public PriorityRoutingStrategyTests()
    {
        _strategy = new PriorityRoutingStrategy();
    }

    private Message CreateTestMessage(
        string id = "msg-1",
        string topicName = "test.topic",
        int priority = 0)
    {
        return new Message
        {
            MessageId = id,
            TopicName = topicName,
            Payload = "{\"test\":\"data\"}",
            SchemaVersion = "1.0",
            Priority = priority,
            Status = MessageStatus.Pending,
            Timestamp = DateTime.UtcNow,
            Headers = new Dictionary<string, string> { { "key", "value" } }
        };
    }

    private Subscription CreateTestSubscription(
        string id = "sub-1",
        string topicName = "test.topic",
        string consumerGroup = "group-1",
        bool isActive = true)
    {
        return new Subscription
        {
            SubscriptionId = id,
            TopicName = topicName,
            ConsumerGroup = consumerGroup,
            ConsumerEndpoint = $"http://consumer-{id}.example.com/webhook",
            Type = SubscriptionType.Push,
            IsActive = isActive,
            MaxRetries = 3,
            AckTimeout = TimeSpan.FromSeconds(30),
            CreatedAt = DateTime.UtcNow
        };
    }

    [Fact]
    public void StrategyName_ReturnsCorrectName()
    {
        // Act
        var strategyName = _strategy.StrategyName;

        // Assert
        strategyName.Should().Be("Priority");
    }

    [Fact]
    public async Task RouteAsync_WithHighPriorityMessage_RoutesToFirstConsumer()
    {
        // Arrange - High priority (9)
        var message = CreateTestMessage("msg-1", priority: 9);
        var subscriptions = new List<Subscription>
        {
            CreateTestSubscription("sub-1"),
            CreateTestSubscription("sub-2"),
            CreateTestSubscription("sub-3")
        };

        // Act
        var result = await _strategy.RouteAsync(message, subscriptions);

        // Assert
        result.Success.Should().BeTrue();
        result.ConsumerIds.Should().ContainSingle();
        result.ConsumerIds[0].Should().Be("sub-1"); // First consumer for high priority
    }

    [Fact]
    public async Task RouteAsync_WithMediumPriorityMessage_UsesRoundRobin()
    {
        // Arrange - Medium priority (5)
        var subscriptions = new List<Subscription>
        {
            CreateTestSubscription("sub-1"),
            CreateTestSubscription("sub-2"),
            CreateTestSubscription("sub-3")
        };

        // Act - Route 3 medium-priority messages
        var result1 = await _strategy.RouteAsync(CreateTestMessage("msg-1", priority: 5), subscriptions);
        var result2 = await _strategy.RouteAsync(CreateTestMessage("msg-2", priority: 5), subscriptions);
        var result3 = await _strategy.RouteAsync(CreateTestMessage("msg-3", priority: 5), subscriptions);

        // Assert - Should use round-robin for medium priority
        result1.ConsumerIds[0].Should().Be("sub-1");
        result2.ConsumerIds[0].Should().Be("sub-2");
        result3.ConsumerIds[0].Should().Be("sub-3");
    }

    [Fact]
    public async Task RouteAsync_WithLowPriorityMessage_RoutesToLastConsumer()
    {
        // Arrange - Low priority (0)
        var message = CreateTestMessage("msg-1", priority: 0);
        var subscriptions = new List<Subscription>
        {
            CreateTestSubscription("sub-1"),
            CreateTestSubscription("sub-2"),
            CreateTestSubscription("sub-3")
        };

        // Act
        var result = await _strategy.RouteAsync(message, subscriptions);

        // Assert
        result.Success.Should().BeTrue();
        result.ConsumerIds.Should().ContainSingle();
        result.ConsumerIds[0].Should().Be("sub-3"); // Last consumer for low priority
    }

    [Fact]
    public async Task RouteAsync_WithNoSubscriptions_ReturnsFailure()
    {
        // Arrange
        var message = CreateTestMessage(priority: 5);
        var subscriptions = new List<Subscription>();

        // Act
        var result = await _strategy.RouteAsync(message, subscriptions);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ConsumerIds.Should().BeEmpty();
        result.ErrorMessage.Should().Contain("No active consumers");
        result.StrategyName.Should().Be("Priority");
    }

    [Fact]
    public async Task RouteAsync_WithAllInactiveSubscriptions_ReturnsFailure()
    {
        // Arrange
        var message = CreateTestMessage(priority: 5);
        var subscriptions = new List<Subscription>
        {
            CreateTestSubscription("sub-1", isActive: false),
            CreateTestSubscription("sub-2", isActive: false),
            CreateTestSubscription("sub-3", isActive: false)
        };

        // Act
        var result = await _strategy.RouteAsync(message, subscriptions);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ConsumerIds.Should().BeEmpty();
        result.ErrorMessage.Should().Contain("No active consumers");
    }

    [Fact]
    public async Task RouteAsync_WithMixedActiveInactive_RoutesOnlyToActive()
    {
        // Arrange
        var message = CreateTestMessage(priority: 5);
        var subscriptions = new List<Subscription>
        {
            CreateTestSubscription("sub-1", isActive: false),
            CreateTestSubscription("sub-2", isActive: true),
            CreateTestSubscription("sub-3", isActive: false),
            CreateTestSubscription("sub-4", isActive: true),
            CreateTestSubscription("sub-5", isActive: true)
        };

        // Act
        var result = await _strategy.RouteAsync(message, subscriptions);

        // Assert
        result.Success.Should().BeTrue();
        result.ConsumerIds.Should().ContainSingle();
        result.ConsumerIds[0].Should().BeOneOf("sub-2", "sub-4", "sub-5");
    }

    [Fact]
    public async Task RouteAsync_WithNullSubscriptionList_ReturnsFailure()
    {
        // Arrange
        var message = CreateTestMessage();
        List<Subscription>? subscriptions = null;

        // Act
        var result = await _strategy.RouteAsync(message, subscriptions!);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("No active consumers");
    }

    [Fact]
    public async Task RouteAsync_WithSingleSubscription_AlwaysReturnsSameSubscription()
    {
        // Arrange
        var subscriptions = new List<Subscription>
        {
            CreateTestSubscription("sub-1")
        };

        // Act - Route messages of different priorities
        var highResult = await _strategy.RouteAsync(CreateTestMessage("msg-1", priority: 9), subscriptions);
        var medResult = await _strategy.RouteAsync(CreateTestMessage("msg-2", priority: 5), subscriptions);
        var lowResult = await _strategy.RouteAsync(CreateTestMessage("msg-3", priority: 0), subscriptions);

        // Assert
        highResult.ConsumerIds[0].Should().Be("sub-1");
        medResult.ConsumerIds[0].Should().Be("sub-1");
        lowResult.ConsumerIds[0].Should().Be("sub-1");
    }

    [Fact]
    public async Task RouteAsync_SetsSuccessToTrue_WhenConsumersFound()
    {
        // Arrange
        var message = CreateTestMessage(priority: 5);
        var subscriptions = new List<Subscription>
        {
            CreateTestSubscription("sub-1"),
            CreateTestSubscription("sub-2")
        };

        // Act
        var result = await _strategy.RouteAsync(message, subscriptions);

        // Assert
        result.Success.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task RouteAsync_SetsReason_WhenSuccessful()
    {
        // Arrange
        var message = CreateTestMessage(priority: 9);
        var subscriptions = new List<Subscription>
        {
            CreateTestSubscription("sub-1"),
            CreateTestSubscription("sub-2")
        };

        // Act
        var result = await _strategy.RouteAsync(message, subscriptions);

        // Assert
        result.Reason.Should().NotBeNullOrWhiteSpace();
        result.Reason.Should().Contain("priority");
    }

    [Fact]
    public async Task RouteAsync_ReturnsOnlyOneConsumer()
    {
        // Arrange
        var message = CreateTestMessage(priority: 5);
        var subscriptions = new List<Subscription>
        {
            CreateTestSubscription("sub-1"),
            CreateTestSubscription("sub-2"),
            CreateTestSubscription("sub-3")
        };

        // Act
        var result = await _strategy.RouteAsync(message, subscriptions);

        // Assert - Priority strategy should return exactly one consumer
        result.ConsumerIds.Should().ContainSingle();
        result.GetConsumerCount().Should().Be(1);
    }

    [Fact]
    public async Task RouteAsync_WithCancellationToken_CompletesSuccessfully()
    {
        // Arrange
        var message = CreateTestMessage(priority: 5);
        var subscriptions = new List<Subscription>
        {
            CreateTestSubscription("sub-1"),
            CreateTestSubscription("sub-2")
        };
        var cts = new CancellationTokenSource();

        // Act
        var result = await _strategy.RouteAsync(message, subscriptions, cts.Token);

        // Assert
        result.Success.Should().BeTrue();
        result.ConsumerIds.Should().ContainSingle();
    }

    [Fact]
    public async Task RouteAsync_ReturnsMetadataWithPriorityInfo()
    {
        // Arrange
        var message = CreateTestMessage(priority: 9);
        var subscriptions = new List<Subscription>
        {
            CreateTestSubscription("sub-1"),
            CreateTestSubscription("sub-2"),
            CreateTestSubscription("sub-3")
        };

        // Act
        var result = await _strategy.RouteAsync(message, subscriptions);

        // Assert
        result.Metadata.Should().NotBeNull();
        result.Metadata.Should().ContainKey("totalActive");
        result.Metadata!["totalActive"].Should().Be(3);
        result.Metadata.Should().ContainKey("messagePriority");
        result.Metadata["messagePriority"].Should().Be(9);
    }

    [Fact]
    public async Task RouteAsync_PriorityTiers_HighPriorityAlwaysFirst()
    {
        // Arrange
        var subscriptions = new List<Subscription>
        {
            CreateTestSubscription("sub-1"),
            CreateTestSubscription("sub-2"),
            CreateTestSubscription("sub-3")
        };

        // Act - Route multiple high-priority messages
        var result1 = await _strategy.RouteAsync(CreateTestMessage("msg-1", priority: 9), subscriptions);
        var result2 = await _strategy.RouteAsync(CreateTestMessage("msg-2", priority: 8), subscriptions);
        var result3 = await _strategy.RouteAsync(CreateTestMessage("msg-3", priority: 7), subscriptions);

        // Assert - All high-priority messages should go to first consumer
        result1.ConsumerIds[0].Should().Be("sub-1");
        result2.ConsumerIds[0].Should().Be("sub-1");
        result3.ConsumerIds[0].Should().Be("sub-1");
    }

    [Fact]
    public async Task RouteAsync_PriorityTiers_LowPriorityAlwaysLast()
    {
        // Arrange
        var subscriptions = new List<Subscription>
        {
            CreateTestSubscription("sub-1"),
            CreateTestSubscription("sub-2"),
            CreateTestSubscription("sub-3")
        };

        // Act - Route multiple low-priority messages
        var result1 = await _strategy.RouteAsync(CreateTestMessage("msg-1", priority: 0), subscriptions);
        var result2 = await _strategy.RouteAsync(CreateTestMessage("msg-2", priority: 1), subscriptions);
        var result3 = await _strategy.RouteAsync(CreateTestMessage("msg-3", priority: 3), subscriptions);

        // Assert - All low-priority messages should go to last consumer
        result1.ConsumerIds[0].Should().Be("sub-3");
        result2.ConsumerIds[0].Should().Be("sub-3");
        result3.ConsumerIds[0].Should().Be("sub-3");
    }

    [Fact]
    public async Task RouteAsync_MediumPriorityRoundRobin_DistributesEvenly()
    {
        // Arrange
        var subscriptions = new List<Subscription>
        {
            CreateTestSubscription("sub-1"),
            CreateTestSubscription("sub-2"),
            CreateTestSubscription("sub-3")
        };

        // Act - Route 6 medium-priority messages (2 cycles)
        var results = new List<RouteResult>();
        for (int i = 0; i < 6; i++)
        {
            results.Add(await _strategy.RouteAsync(CreateTestMessage($"msg-{i}", priority: 5), subscriptions));
        }

        // Assert - Should cycle through: sub-1, sub-2, sub-3, sub-1, sub-2, sub-3
        results[0].ConsumerIds[0].Should().Be("sub-1");
        results[1].ConsumerIds[0].Should().Be("sub-2");
        results[2].ConsumerIds[0].Should().Be("sub-3");
        results[3].ConsumerIds[0].Should().Be("sub-1"); // Cycle repeats
        results[4].ConsumerIds[0].Should().Be("sub-2");
        results[5].ConsumerIds[0].Should().Be("sub-3");
    }
}
