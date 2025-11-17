using FluentAssertions;
using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Orchestrator.Routing;
using Xunit;

namespace HotSwap.Distributed.Tests.Orchestrator;

public class FanOutRoutingStrategyTests
{
    private readonly FanOutRoutingStrategy _strategy;

    public FanOutRoutingStrategyTests()
    {
        _strategy = new FanOutRoutingStrategy();
    }

    private Message CreateTestMessage(string id = "msg-1", string topicName = "test.topic")
    {
        return new Message
        {
            MessageId = id,
            TopicName = topicName,
            Payload = "{\"test\":\"data\"}",
            SchemaVersion = "1.0",
            Priority = 0,
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
        strategyName.Should().Be("FanOut");
    }

    [Fact]
    public async Task RouteAsync_WithSingleSubscription_ReturnsAllSubscriptions()
    {
        // Arrange
        var message = CreateTestMessage();
        var subscriptions = new List<Subscription>
        {
            CreateTestSubscription("sub-1")
        };

        // Act
        var result = await _strategy.RouteAsync(message, subscriptions);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.ConsumerIds.Should().ContainSingle();
        result.ConsumerIds[0].Should().Be("sub-1");
        result.StrategyName.Should().Be("FanOut");
    }

    [Fact]
    public async Task RouteAsync_WithMultipleSubscriptions_ReturnsAllSubscriptions()
    {
        // Arrange
        var message = CreateTestMessage();
        var subscriptions = new List<Subscription>
        {
            CreateTestSubscription("sub-1"),
            CreateTestSubscription("sub-2"),
            CreateTestSubscription("sub-3"),
            CreateTestSubscription("sub-4")
        };

        // Act
        var result = await _strategy.RouteAsync(message, subscriptions);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.ConsumerIds.Should().HaveCount(4);
        result.ConsumerIds.Should().Contain("sub-1");
        result.ConsumerIds.Should().Contain("sub-2");
        result.ConsumerIds.Should().Contain("sub-3");
        result.ConsumerIds.Should().Contain("sub-4");
        result.StrategyName.Should().Be("FanOut");
    }

    [Fact]
    public async Task RouteAsync_WithNoSubscriptions_ReturnsFailure()
    {
        // Arrange
        var message = CreateTestMessage();
        var subscriptions = new List<Subscription>();

        // Act
        var result = await _strategy.RouteAsync(message, subscriptions);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ConsumerIds.Should().BeEmpty();
        result.ErrorMessage.Should().Contain("No active consumers");
        result.StrategyName.Should().Be("FanOut");
    }

    [Fact]
    public async Task RouteAsync_WithAllInactiveSubscriptions_ReturnsFailure()
    {
        // Arrange
        var message = CreateTestMessage();
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
    public async Task RouteAsync_WithMixedActiveInactive_ReturnsOnlyActive()
    {
        // Arrange
        var message = CreateTestMessage();
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
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.ConsumerIds.Should().HaveCount(3);
        result.ConsumerIds.Should().Contain("sub-2");
        result.ConsumerIds.Should().Contain("sub-4");
        result.ConsumerIds.Should().Contain("sub-5");
        result.ConsumerIds.Should().NotContain("sub-1");
        result.ConsumerIds.Should().NotContain("sub-3");
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
    public async Task RouteAsync_SetsSuccessToTrue_WhenConsumersFound()
    {
        // Arrange
        var message = CreateTestMessage();
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
        var message = CreateTestMessage();
        var subscriptions = new List<Subscription>
        {
            CreateTestSubscription("sub-1"),
            CreateTestSubscription("sub-2"),
            CreateTestSubscription("sub-3")
        };

        // Act
        var result = await _strategy.RouteAsync(message, subscriptions);

        // Assert
        result.Reason.Should().NotBeNullOrWhiteSpace();
        result.Reason.Should().Contain("all");
        result.Reason.Should().Contain("3");
    }

    [Fact]
    public async Task RouteAsync_PreservesSubscriptionOrder()
    {
        // Arrange
        var message = CreateTestMessage();
        var subscriptions = new List<Subscription>
        {
            CreateTestSubscription("sub-A"),
            CreateTestSubscription("sub-B"),
            CreateTestSubscription("sub-C"),
            CreateTestSubscription("sub-D")
        };

        // Act
        var result = await _strategy.RouteAsync(message, subscriptions);

        // Assert
        result.ConsumerIds[0].Should().Be("sub-A");
        result.ConsumerIds[1].Should().Be("sub-B");
        result.ConsumerIds[2].Should().Be("sub-C");
        result.ConsumerIds[3].Should().Be("sub-D");
    }

    [Fact]
    public async Task RouteAsync_WithCancellationToken_CompletesSuccessfully()
    {
        // Arrange
        var message = CreateTestMessage();
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
        result.ConsumerIds.Should().HaveCount(2);
    }

    [Fact]
    public async Task RouteAsync_ReturnsMetadataWithSubscriptionInfo()
    {
        // Arrange
        var message = CreateTestMessage();
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
        result.Metadata.Should().ContainKey("broadcastCount");
        result.Metadata["broadcastCount"].Should().Be(3);
    }

    [Fact]
    public async Task RouteAsync_WithLargeNumberOfSubscriptions_ReturnsAll()
    {
        // Arrange
        var message = CreateTestMessage();
        var subscriptions = new List<Subscription>();
        for (int i = 0; i < 100; i++)
        {
            subscriptions.Add(CreateTestSubscription($"sub-{i}"));
        }

        // Act
        var result = await _strategy.RouteAsync(message, subscriptions);

        // Assert
        result.Success.Should().BeTrue();
        result.ConsumerIds.Should().HaveCount(100);
        result.GetConsumerCount().Should().Be(100);
    }

    [Fact]
    public async Task RouteAsync_WithDifferentTopics_StillRoutesToAll()
    {
        // Arrange
        var message = CreateTestMessage("msg-1", "orders.created");
        var subscriptions = new List<Subscription>
        {
            CreateTestSubscription("sub-1", "orders.created"),
            CreateTestSubscription("sub-2", "orders.created"),
            CreateTestSubscription("sub-3", "orders.created")
        };

        // Act
        var result = await _strategy.RouteAsync(message, subscriptions);

        // Assert
        result.Success.Should().BeTrue();
        result.ConsumerIds.Should().HaveCount(3);
    }

    [Fact]
    public async Task RouteAsync_ReturnsReasonWithCorrectCount()
    {
        // Arrange
        var message = CreateTestMessage();
        var subscriptions = new List<Subscription>
        {
            CreateTestSubscription("sub-1"),
            CreateTestSubscription("sub-2"),
            CreateTestSubscription("sub-3"),
            CreateTestSubscription("sub-4"),
            CreateTestSubscription("sub-5")
        };

        // Act
        var result = await _strategy.RouteAsync(message, subscriptions);

        // Assert
        result.Reason.Should().Contain("5");
        result.Reason.Should().Contain("consumer");
    }
}
