using FluentAssertions;
using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Orchestrator.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HotSwap.Distributed.Tests.Orchestrator;

public class MessageRouterTests
{
    private readonly MessageRouter _router;

    public MessageRouterTests()
    {
        _router = new MessageRouter(NullLogger<MessageRouter>.Instance);
    }

    private Message CreateTestMessage(
        string id = "msg-1",
        string topicName = "test.topic",
        int priority = 0,
        Dictionary<string, string>? headers = null)
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
            Headers = headers ?? new Dictionary<string, string> { { "key", "value" } }
        };
    }

    private Topic CreateTestTopic(
        string name = "test.topic",
        TopicType type = TopicType.PubSub,
        Dictionary<string, string>? config = null)
    {
        return new Topic
        {
            Name = name,
            SchemaId = "schema-1",
            Type = type,
            Config = config ?? new Dictionary<string, string>(),
            CreatedAt = DateTime.UtcNow
        };
    }

    private Subscription CreateTestSubscription(
        string id = "sub-1",
        string topicName = "test.topic",
        bool isActive = true,
        MessageFilter? filter = null)
    {
        return new Subscription
        {
            SubscriptionId = id,
            TopicName = topicName,
            ConsumerGroup = $"group-{id}",
            ConsumerEndpoint = $"http://consumer-{id}.example.com/webhook",
            Type = SubscriptionType.Push,
            IsActive = isActive,
            Filter = filter,
            MaxRetries = 3,
            AckTimeout = TimeSpan.FromSeconds(30),
            CreatedAt = DateTime.UtcNow
        };
    }

    #region Topic Type Strategy Selection Tests

    [Fact]
    public async Task RouteMessageAsync_WithQueueTopic_UsesLoadBalancedStrategy()
    {
        // Arrange
        var message = CreateTestMessage();
        var topic = CreateTestTopic(type: TopicType.Queue);
        var subscriptions = new List<Subscription>
        {
            CreateTestSubscription("sub-1"),
            CreateTestSubscription("sub-2"),
            CreateTestSubscription("sub-3")
        };

        // Act - Route 3 messages to verify round-robin behavior
        var result1 = await _router.RouteMessageAsync(message, topic, subscriptions);
        var result2 = await _router.RouteMessageAsync(message, topic, subscriptions);
        var result3 = await _router.RouteMessageAsync(message, topic, subscriptions);

        // Assert - Should use LoadBalanced (round-robin)
        result1.Success.Should().BeTrue();
        result2.Success.Should().BeTrue();
        result3.Success.Should().BeTrue();

        result1.ConsumerIds.Should().ContainSingle();
        result2.ConsumerIds.Should().ContainSingle();
        result3.ConsumerIds.Should().ContainSingle();

        // Round-robin: sub-1, sub-2, sub-3
        result1.ConsumerIds[0].Should().Be("sub-1");
        result2.ConsumerIds[0].Should().Be("sub-2");
        result3.ConsumerIds[0].Should().Be("sub-3");

        result1.StrategyName.Should().Be("LoadBalanced");
    }

    [Fact]
    public async Task RouteMessageAsync_WithPubSubTopic_UsesFanOutStrategy()
    {
        // Arrange
        var message = CreateTestMessage();
        var topic = CreateTestTopic(type: TopicType.PubSub);
        var subscriptions = new List<Subscription>
        {
            CreateTestSubscription("sub-1"),
            CreateTestSubscription("sub-2"),
            CreateTestSubscription("sub-3")
        };

        // Act
        var result = await _router.RouteMessageAsync(message, topic, subscriptions);

        // Assert - Should use FanOut (broadcast to all)
        result.Success.Should().BeTrue();
        result.ConsumerIds.Should().HaveCount(3);
        result.ConsumerIds.Should().Contain("sub-1");
        result.ConsumerIds.Should().Contain("sub-2");
        result.ConsumerIds.Should().Contain("sub-3");
        result.StrategyName.Should().Be("FanOut");
    }

    #endregion

    #region Strategy Override Tests

    [Fact]
    public async Task RouteMessageAsync_WithDirectStrategyOverride_UsesDirectStrategy()
    {
        // Arrange
        var message = CreateTestMessage();
        var topic = CreateTestTopic(
            type: TopicType.Queue,
            config: new Dictionary<string, string> { { "routingStrategy", "Direct" } }
        );
        var subscriptions = new List<Subscription>
        {
            CreateTestSubscription("sub-1"),
            CreateTestSubscription("sub-2"),
            CreateTestSubscription("sub-3")
        };

        // Act - Route multiple messages
        var result1 = await _router.RouteMessageAsync(message, topic, subscriptions);
        var result2 = await _router.RouteMessageAsync(message, topic, subscriptions);

        // Assert - Should always use first consumer (Direct strategy)
        result1.ConsumerIds[0].Should().Be("sub-1");
        result2.ConsumerIds[0].Should().Be("sub-1");
        result1.StrategyName.Should().Be("Direct");
    }

    [Fact]
    public async Task RouteMessageAsync_WithPriorityStrategyOverride_UsesPriorityStrategy()
    {
        // Arrange
        var topic = CreateTestTopic(
            type: TopicType.Queue,
            config: new Dictionary<string, string> { { "routingStrategy", "Priority" } }
        );
        var subscriptions = new List<Subscription>
        {
            CreateTestSubscription("sub-1"),
            CreateTestSubscription("sub-2"),
            CreateTestSubscription("sub-3")
        };

        // Act - Route high-priority and low-priority messages
        var highPriorityMsg = CreateTestMessage("msg-high", priority: 9);
        var lowPriorityMsg = CreateTestMessage("msg-low", priority: 0);

        var highResult = await _router.RouteMessageAsync(highPriorityMsg, topic, subscriptions);
        var lowResult = await _router.RouteMessageAsync(lowPriorityMsg, topic, subscriptions);

        // Assert - High priority goes to first, low priority to last
        highResult.StrategyName.Should().Be("Priority");
        highResult.ConsumerIds[0].Should().Be("sub-1"); // First consumer for high priority
        lowResult.ConsumerIds[0].Should().Be("sub-3"); // Last consumer for low priority
    }

    [Fact]
    public async Task RouteMessageAsync_WithContentBasedStrategyOverride_UsesContentBasedStrategy()
    {
        // Arrange
        var message = CreateTestMessage(headers: new Dictionary<string, string> { { "region", "us-west" } });
        var topic = CreateTestTopic(
            type: TopicType.PubSub,
            config: new Dictionary<string, string> { { "routingStrategy", "ContentBased" } }
        );
        var subscriptions = new List<Subscription>
        {
            CreateTestSubscription("sub-1", filter: new MessageFilter
            {
                HeaderMatches = new Dictionary<string, string> { { "region", "us-west" } }
            }),
            CreateTestSubscription("sub-2", filter: new MessageFilter
            {
                HeaderMatches = new Dictionary<string, string> { { "region", "us-east" } }
            })
        };

        // Act
        var result = await _router.RouteMessageAsync(message, topic, subscriptions);

        // Assert - Should only route to matching filter
        result.StrategyName.Should().Be("ContentBased");
        result.ConsumerIds.Should().ContainSingle();
        result.ConsumerIds[0].Should().Be("sub-1"); // Only us-west matches
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public async Task RouteMessageAsync_WithNoSubscriptions_ReturnsFailure()
    {
        // Arrange
        var message = CreateTestMessage();
        var topic = CreateTestTopic();
        var subscriptions = new List<Subscription>();

        // Act
        var result = await _router.RouteMessageAsync(message, topic, subscriptions);

        // Assert
        result.Success.Should().BeFalse();
        result.ConsumerIds.Should().BeEmpty();
        result.ErrorMessage.Should().Contain("No active consumers");
    }

    [Fact]
    public async Task RouteMessageAsync_WithNullSubscriptionList_ReturnsFailure()
    {
        // Arrange
        var message = CreateTestMessage();
        var topic = CreateTestTopic();

        // Act
        var result = await _router.RouteMessageAsync(message, topic, null!);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("No active consumers");
    }

    [Fact]
    public async Task RouteMessageAsync_WithInvalidStrategyName_UsesDefaultStrategy()
    {
        // Arrange
        var message = CreateTestMessage();
        var topic = CreateTestTopic(
            type: TopicType.PubSub,
            config: new Dictionary<string, string> { { "routingStrategy", "InvalidStrategy" } }
        );
        var subscriptions = new List<Subscription>
        {
            CreateTestSubscription("sub-1"),
            CreateTestSubscription("sub-2")
        };

        // Act
        var result = await _router.RouteMessageAsync(message, topic, subscriptions);

        // Assert - Should fall back to default (FanOut for PubSub)
        result.Success.Should().BeTrue();
        result.StrategyName.Should().Be("FanOut");
        result.ConsumerIds.Should().HaveCount(2);
    }

    #endregion

    #region All Strategies Integration Tests

    [Fact]
    public async Task RouteMessageAsync_DirectStrategy_Integration()
    {
        // Arrange
        var message = CreateTestMessage();
        var topic = CreateTestTopic(config: new Dictionary<string, string> { { "routingStrategy", "Direct" } });
        var subscriptions = new List<Subscription>
        {
            CreateTestSubscription("sub-1"),
            CreateTestSubscription("sub-2")
        };

        // Act
        var result = await _router.RouteMessageAsync(message, topic, subscriptions);

        // Assert
        result.Success.Should().BeTrue();
        result.StrategyName.Should().Be("Direct");
        result.ConsumerIds.Should().ContainSingle();
        result.ConsumerIds[0].Should().Be("sub-1");
    }

    [Fact]
    public async Task RouteMessageAsync_FanOutStrategy_Integration()
    {
        // Arrange
        var message = CreateTestMessage();
        var topic = CreateTestTopic(type: TopicType.PubSub); // Default for PubSub
        var subscriptions = new List<Subscription>
        {
            CreateTestSubscription("sub-1"),
            CreateTestSubscription("sub-2"),
            CreateTestSubscription("sub-3")
        };

        // Act
        var result = await _router.RouteMessageAsync(message, topic, subscriptions);

        // Assert
        result.Success.Should().BeTrue();
        result.StrategyName.Should().Be("FanOut");
        result.ConsumerIds.Should().HaveCount(3);
    }

    [Fact]
    public async Task RouteMessageAsync_LoadBalancedStrategy_Integration()
    {
        // Arrange
        var message = CreateTestMessage();
        var topic = CreateTestTopic(type: TopicType.Queue); // Default for Queue
        var subscriptions = new List<Subscription>
        {
            CreateTestSubscription("sub-1"),
            CreateTestSubscription("sub-2")
        };

        // Act - Route 4 messages to verify even distribution
        var results = new List<RouteResult>();
        for (int i = 0; i < 4; i++)
        {
            results.Add(await _router.RouteMessageAsync(CreateTestMessage($"msg-{i}"), topic, subscriptions));
        }

        // Assert - Should alternate: sub-1, sub-2, sub-1, sub-2
        results[0].ConsumerIds[0].Should().Be("sub-1");
        results[1].ConsumerIds[0].Should().Be("sub-2");
        results[2].ConsumerIds[0].Should().Be("sub-1");
        results[3].ConsumerIds[0].Should().Be("sub-2");
    }

    [Fact]
    public async Task RouteMessageAsync_PriorityStrategy_Integration()
    {
        // Arrange
        var topic = CreateTestTopic(config: new Dictionary<string, string> { { "routingStrategy", "Priority" } });
        var subscriptions = new List<Subscription>
        {
            CreateTestSubscription("sub-1"),
            CreateTestSubscription("sub-2"),
            CreateTestSubscription("sub-3")
        };

        // Act
        var highMsg = CreateTestMessage("high", priority: 9);
        var medMsg = CreateTestMessage("med", priority: 5);
        var lowMsg = CreateTestMessage("low", priority: 0);

        var highResult = await _router.RouteMessageAsync(highMsg, topic, subscriptions);
        var medResult = await _router.RouteMessageAsync(medMsg, topic, subscriptions);
        var lowResult = await _router.RouteMessageAsync(lowMsg, topic, subscriptions);

        // Assert
        highResult.StrategyName.Should().Be("Priority");
        highResult.ConsumerIds[0].Should().Be("sub-1"); // High → first
        medResult.ConsumerIds[0].Should().Be("sub-1"); // Medium → round-robin starts at 0
        lowResult.ConsumerIds[0].Should().Be("sub-3"); // Low → last
    }

    [Fact]
    public async Task RouteMessageAsync_ContentBasedStrategy_Integration()
    {
        // Arrange
        var topic = CreateTestTopic(config: new Dictionary<string, string> { { "routingStrategy", "ContentBased" } });
        var subscriptions = new List<Subscription>
        {
            CreateTestSubscription("sub-1", filter: new MessageFilter
            {
                HeaderMatches = new Dictionary<string, string> { { "type", "order" } }
            }),
            CreateTestSubscription("sub-2", filter: new MessageFilter
            {
                HeaderMatches = new Dictionary<string, string> { { "type", "payment" } }
            }),
            CreateTestSubscription("sub-3", filter: null) // Matches all
        };

        // Act
        var orderMsg = CreateTestMessage(headers: new Dictionary<string, string> { { "type", "order" } });
        var paymentMsg = CreateTestMessage(headers: new Dictionary<string, string> { { "type", "payment" } });

        var orderResult = await _router.RouteMessageAsync(orderMsg, topic, subscriptions);
        var paymentResult = await _router.RouteMessageAsync(paymentMsg, topic, subscriptions);

        // Assert
        orderResult.StrategyName.Should().Be("ContentBased");
        orderResult.ConsumerIds.Should().HaveCount(2); // sub-1 and sub-3
        orderResult.ConsumerIds.Should().Contain("sub-1");
        orderResult.ConsumerIds.Should().Contain("sub-3");

        paymentResult.ConsumerIds.Should().HaveCount(2); // sub-2 and sub-3
        paymentResult.ConsumerIds.Should().Contain("sub-2");
        paymentResult.ConsumerIds.Should().Contain("sub-3");
    }

    #endregion

    #region Active/Inactive Subscription Tests

    [Fact]
    public async Task RouteMessageAsync_FiltersInactiveSubscriptions()
    {
        // Arrange
        var message = CreateTestMessage();
        var topic = CreateTestTopic(type: TopicType.PubSub);
        var subscriptions = new List<Subscription>
        {
            CreateTestSubscription("sub-1", isActive: true),
            CreateTestSubscription("sub-2", isActive: false),
            CreateTestSubscription("sub-3", isActive: true)
        };

        // Act
        var result = await _router.RouteMessageAsync(message, topic, subscriptions);

        // Assert
        result.Success.Should().BeTrue();
        result.ConsumerIds.Should().HaveCount(2);
        result.ConsumerIds.Should().Contain("sub-1");
        result.ConsumerIds.Should().Contain("sub-3");
        result.ConsumerIds.Should().NotContain("sub-2"); // Inactive
    }

    [Fact]
    public async Task RouteMessageAsync_WithAllInactiveSubscriptions_ReturnsFailure()
    {
        // Arrange
        var message = CreateTestMessage();
        var topic = CreateTestTopic();
        var subscriptions = new List<Subscription>
        {
            CreateTestSubscription("sub-1", isActive: false),
            CreateTestSubscription("sub-2", isActive: false)
        };

        // Act
        var result = await _router.RouteMessageAsync(message, topic, subscriptions);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("No active consumers");
    }

    #endregion

    #region Multiple Topics Tests

    [Fact]
    public async Task RouteMessageAsync_DifferentTopics_UseDifferentStrategies()
    {
        // Arrange
        var message = CreateTestMessage();
        var queueTopic = CreateTestTopic("queue.topic", TopicType.Queue);
        var pubsubTopic = CreateTestTopic("pubsub.topic", TopicType.PubSub);
        var subscriptions = new List<Subscription>
        {
            CreateTestSubscription("sub-1"),
            CreateTestSubscription("sub-2")
        };

        // Act
        var queueResult = await _router.RouteMessageAsync(message, queueTopic, subscriptions);
        var pubsubResult = await _router.RouteMessageAsync(message, pubsubTopic, subscriptions);

        // Assert
        queueResult.StrategyName.Should().Be("LoadBalanced");
        queueResult.ConsumerIds.Should().ContainSingle();

        pubsubResult.StrategyName.Should().Be("FanOut");
        pubsubResult.ConsumerIds.Should().HaveCount(2);
    }

    #endregion

    #region Metadata and Reason Tests

    [Fact]
    public async Task RouteMessageAsync_IncludesMetadata()
    {
        // Arrange
        var message = CreateTestMessage();
        var topic = CreateTestTopic();
        var subscriptions = new List<Subscription>
        {
            CreateTestSubscription("sub-1"),
            CreateTestSubscription("sub-2")
        };

        // Act
        var result = await _router.RouteMessageAsync(message, topic, subscriptions);

        // Assert
        result.Metadata.Should().NotBeNull();
        result.Metadata.Should().ContainKey("totalActive");
    }

    [Fact]
    public async Task RouteMessageAsync_IncludesReason()
    {
        // Arrange
        var message = CreateTestMessage();
        var topic = CreateTestTopic();
        var subscriptions = new List<Subscription>
        {
            CreateTestSubscription("sub-1")
        };

        // Act
        var result = await _router.RouteMessageAsync(message, topic, subscriptions);

        // Assert
        result.Reason.Should().NotBeNullOrWhiteSpace();
    }

    #endregion

    #region Thread Safety Tests

    [Fact]
    public async Task RouteMessageAsync_ConcurrentCalls_AreThreadSafe()
    {
        // Arrange
        var topic = CreateTestTopic(type: TopicType.Queue);
        var subscriptions = new List<Subscription>
        {
            CreateTestSubscription("sub-1"),
            CreateTestSubscription("sub-2"),
            CreateTestSubscription("sub-3")
        };

        // Act - Route 100 messages concurrently
        var tasks = Enumerable.Range(0, 100)
            .Select(i => _router.RouteMessageAsync(CreateTestMessage($"msg-{i}"), topic, subscriptions))
            .ToList();

        var results = await Task.WhenAll(tasks);

        // Assert - All should succeed
        results.Should().AllSatisfy(r => r.Success.Should().BeTrue());

        // Count distribution (should be roughly even)
        var counts = results
            .GroupBy(r => r.ConsumerIds[0])
            .ToDictionary(g => g.Key, g => g.Count());

        // Each consumer should get roughly 33 messages (100 / 3)
        counts["sub-1"].Should().BeInRange(30, 37);
        counts["sub-2"].Should().BeInRange(30, 37);
        counts["sub-3"].Should().BeInRange(30, 37);
    }

    #endregion
}
