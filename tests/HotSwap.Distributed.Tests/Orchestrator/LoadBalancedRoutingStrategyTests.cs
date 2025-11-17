using FluentAssertions;
using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Orchestrator.Routing;
using Xunit;

namespace HotSwap.Distributed.Tests.Orchestrator;

public class LoadBalancedRoutingStrategyTests
{
    private readonly LoadBalancedRoutingStrategy _strategy;

    public LoadBalancedRoutingStrategyTests()
    {
        _strategy = new LoadBalancedRoutingStrategy();
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
        strategyName.Should().Be("LoadBalanced");
    }

    [Fact]
    public async Task RouteAsync_WithSingleSubscription_AlwaysReturnsSameSubscription()
    {
        // Arrange
        var subscriptions = new List<Subscription>
        {
            CreateTestSubscription("sub-1")
        };

        // Act - Route multiple messages
        var result1 = await _strategy.RouteAsync(CreateTestMessage("msg-1"), subscriptions);
        var result2 = await _strategy.RouteAsync(CreateTestMessage("msg-2"), subscriptions);
        var result3 = await _strategy.RouteAsync(CreateTestMessage("msg-3"), subscriptions);

        // Assert
        result1.ConsumerIds[0].Should().Be("sub-1");
        result2.ConsumerIds[0].Should().Be("sub-1");
        result3.ConsumerIds[0].Should().Be("sub-1");
    }

    [Fact]
    public async Task RouteAsync_WithMultipleSubscriptions_DistributesRoundRobin()
    {
        // Arrange
        var subscriptions = new List<Subscription>
        {
            CreateTestSubscription("sub-1"),
            CreateTestSubscription("sub-2"),
            CreateTestSubscription("sub-3")
        };

        // Act - Route 6 messages (2 cycles)
        var results = new List<RouteResult>();
        for (int i = 0; i < 6; i++)
        {
            results.Add(await _strategy.RouteAsync(CreateTestMessage($"msg-{i}"), subscriptions));
        }

        // Assert - Should cycle through: sub-1, sub-2, sub-3, sub-1, sub-2, sub-3
        results[0].ConsumerIds[0].Should().Be("sub-1");
        results[1].ConsumerIds[0].Should().Be("sub-2");
        results[2].ConsumerIds[0].Should().Be("sub-3");
        results[3].ConsumerIds[0].Should().Be("sub-1"); // Cycle repeats
        results[4].ConsumerIds[0].Should().Be("sub-2");
        results[5].ConsumerIds[0].Should().Be("sub-3");
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
        result.StrategyName.Should().Be("LoadBalanced");
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
    public async Task RouteAsync_WithMixedActiveInactive_RoutesOnlyToActive()
    {
        // Arrange
        var subscriptions = new List<Subscription>
        {
            CreateTestSubscription("sub-1", isActive: true),
            CreateTestSubscription("sub-2", isActive: false),
            CreateTestSubscription("sub-3", isActive: true),
            CreateTestSubscription("sub-4", isActive: false),
            CreateTestSubscription("sub-5", isActive: true)
        };

        // Act - Route 6 messages
        var results = new List<RouteResult>();
        for (int i = 0; i < 6; i++)
        {
            results.Add(await _strategy.RouteAsync(CreateTestMessage($"msg-{i}"), subscriptions));
        }

        // Assert - Should only route to sub-1, sub-3, sub-5 in round-robin
        results[0].ConsumerIds[0].Should().Be("sub-1");
        results[1].ConsumerIds[0].Should().Be("sub-3");
        results[2].ConsumerIds[0].Should().Be("sub-5");
        results[3].ConsumerIds[0].Should().Be("sub-1"); // Cycle repeats
        results[4].ConsumerIds[0].Should().Be("sub-3");
        results[5].ConsumerIds[0].Should().Be("sub-5");

        // Inactive subscriptions should never be selected
        results.Should().NotContain(r => r.ConsumerIds.Contains("sub-2"));
        results.Should().NotContain(r => r.ConsumerIds.Contains("sub-4"));
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
        result.Reason.Should().Contain("round-robin");
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
        result.ConsumerIds.Should().ContainSingle();
    }

    [Fact]
    public async Task RouteAsync_ReturnsMetadataWithIndexInfo()
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
        result.Metadata.Should().ContainKey("selectedIndex");
        result.Metadata["selectedIndex"].Should().BeOfType<int>();
    }

    [Fact]
    public async Task RouteAsync_EvenlyDistributesAcrossConsumers()
    {
        // Arrange
        var subscriptions = new List<Subscription>
        {
            CreateTestSubscription("sub-1"),
            CreateTestSubscription("sub-2"),
            CreateTestSubscription("sub-3")
        };

        // Act - Route 30 messages
        var consumerCounts = new Dictionary<string, int>
        {
            { "sub-1", 0 },
            { "sub-2", 0 },
            { "sub-3", 0 }
        };

        for (int i = 0; i < 30; i++)
        {
            var result = await _strategy.RouteAsync(CreateTestMessage($"msg-{i}"), subscriptions);
            consumerCounts[result.ConsumerIds[0]]++;
        }

        // Assert - Each consumer should get exactly 10 messages
        consumerCounts["sub-1"].Should().Be(10);
        consumerCounts["sub-2"].Should().Be(10);
        consumerCounts["sub-3"].Should().Be(10);
    }

    [Fact]
    public async Task RouteAsync_WithTwoConsumers_AlternatesCorrectly()
    {
        // Arrange
        var subscriptions = new List<Subscription>
        {
            CreateTestSubscription("sub-1"),
            CreateTestSubscription("sub-2")
        };

        // Act - Route 10 messages
        var results = new List<RouteResult>();
        for (int i = 0; i < 10; i++)
        {
            results.Add(await _strategy.RouteAsync(CreateTestMessage($"msg-{i}"), subscriptions));
        }

        // Assert - Should alternate: sub-1, sub-2, sub-1, sub-2, ...
        for (int i = 0; i < 10; i++)
        {
            var expectedId = (i % 2 == 0) ? "sub-1" : "sub-2";
            results[i].ConsumerIds[0].Should().Be(expectedId);
        }
    }

    [Fact]
    public async Task RouteAsync_ReturnsOnlyOneConsumer()
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

        // Assert - Load balanced strategy should return exactly one consumer
        result.ConsumerIds.Should().ContainSingle();
        result.GetConsumerCount().Should().Be(1);
    }

    [Fact]
    public async Task RouteAsync_WithLargeNumberOfSubscriptions_DistributesEvenly()
    {
        // Arrange - 10 subscriptions
        var subscriptions = new List<Subscription>();
        for (int i = 0; i < 10; i++)
        {
            subscriptions.Add(CreateTestSubscription($"sub-{i}"));
        }

        // Act - Route 100 messages
        var consumerCounts = new Dictionary<string, int>();
        for (int i = 0; i < 10; i++)
        {
            consumerCounts[$"sub-{i}"] = 0;
        }

        for (int i = 0; i < 100; i++)
        {
            var result = await _strategy.RouteAsync(CreateTestMessage($"msg-{i}"), subscriptions);
            consumerCounts[result.ConsumerIds[0]]++;
        }

        // Assert - Each consumer should get exactly 10 messages
        foreach (var count in consumerCounts.Values)
        {
            count.Should().Be(10);
        }
    }

    [Fact]
    public async Task RouteAsync_MetadataIncludesCurrentIndex()
    {
        // Arrange
        var subscriptions = new List<Subscription>
        {
            CreateTestSubscription("sub-1"),
            CreateTestSubscription("sub-2"),
            CreateTestSubscription("sub-3")
        };

        // Act - Route 3 messages
        var result1 = await _strategy.RouteAsync(CreateTestMessage("msg-1"), subscriptions);
        var result2 = await _strategy.RouteAsync(CreateTestMessage("msg-2"), subscriptions);
        var result3 = await _strategy.RouteAsync(CreateTestMessage("msg-3"), subscriptions);

        // Assert - Indexes should increment
        result1.Metadata!["selectedIndex"].Should().Be(0); // First consumer
        result2.Metadata!["selectedIndex"].Should().Be(1); // Second consumer
        result3.Metadata!["selectedIndex"].Should().Be(2); // Third consumer
    }
}
