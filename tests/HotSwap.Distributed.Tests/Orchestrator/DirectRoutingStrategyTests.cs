using FluentAssertions;
using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Orchestrator.Routing;
using Xunit;

namespace HotSwap.Distributed.Tests.Orchestrator;

public class DirectRoutingStrategyTests
{
    private readonly DirectRoutingStrategy _strategy;

    public DirectRoutingStrategyTests()
    {
        _strategy = new DirectRoutingStrategy();
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
        strategyName.Should().Be("Direct");
    }

    [Fact]
    public async Task RouteAsync_WithSingleSubscription_ReturnsSubscription()
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
        result.StrategyName.Should().Be("Direct");
    }

    [Fact]
    public async Task RouteAsync_WithMultipleSubscriptions_ReturnsFirstSubscription()
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
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.ConsumerIds.Should().ContainSingle();
        result.ConsumerIds[0].Should().Be("sub-1");
        result.StrategyName.Should().Be("Direct");
        result.Reason.Should().Contain("first available");
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
        result.StrategyName.Should().Be("Direct");
    }

    [Fact]
    public async Task RouteAsync_WithAllInactiveSubscriptions_ReturnsFailure()
    {
        // Arrange
        var message = CreateTestMessage();
        var subscriptions = new List<Subscription>
        {
            CreateTestSubscription("sub-1", isActive: false),
            CreateTestSubscription("sub-2", isActive: false)
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
    public async Task RouteAsync_WithMixedActiveInactive_ReturnsFirstActive()
    {
        // Arrange
        var message = CreateTestMessage();
        var subscriptions = new List<Subscription>
        {
            CreateTestSubscription("sub-1", isActive: false),
            CreateTestSubscription("sub-2", isActive: true),
            CreateTestSubscription("sub-3", isActive: true)
        };

        // Act
        var result = await _strategy.RouteAsync(message, subscriptions);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.ConsumerIds.Should().ContainSingle();
        result.ConsumerIds[0].Should().Be("sub-2");
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
    public async Task RouteAsync_SetsSuccessToTrue_WhenConsumerFound()
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
            CreateTestSubscription("sub-2")
        };

        // Act
        var result = await _strategy.RouteAsync(message, subscriptions);

        // Assert
        result.Reason.Should().NotBeNullOrWhiteSpace();
        result.Reason.Should().Contain("first available");
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
            CreateTestSubscription("sub-C")
        };

        // Act
        var result = await _strategy.RouteAsync(message, subscriptions);

        // Assert
        result.ConsumerIds[0].Should().Be("sub-A");
    }

    [Fact]
    public async Task RouteAsync_WithCancellationToken_CompletesSuccessfully()
    {
        // Arrange
        var message = CreateTestMessage();
        var subscriptions = new List<Subscription>
        {
            CreateTestSubscription("sub-1")
        };
        var cts = new CancellationTokenSource();

        // Act
        var result = await _strategy.RouteAsync(message, subscriptions, cts.Token);

        // Assert
        result.Success.Should().BeTrue();
        result.ConsumerIds[0].Should().Be("sub-1");
    }

    [Fact]
    public async Task RouteAsync_ReturnsMetadataWithSubscriptionInfo()
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
        result.Metadata.Should().NotBeNull();
        result.Metadata.Should().ContainKey("totalAvailable");
        result.Metadata!["totalAvailable"].Should().Be(2);
    }
}
