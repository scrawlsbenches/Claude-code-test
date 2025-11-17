using FluentAssertions;
using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Orchestrator.Routing;
using Xunit;

namespace HotSwap.Distributed.Tests.Orchestrator;

public class ContentBasedRoutingStrategyTests
{
    private readonly ContentBasedRoutingStrategy _strategy;

    public ContentBasedRoutingStrategyTests()
    {
        _strategy = new ContentBasedRoutingStrategy();
    }

    private Message CreateTestMessage(
        string id = "msg-1",
        string topicName = "test.topic",
        Dictionary<string, string>? headers = null)
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
            Headers = headers ?? new Dictionary<string, string> { { "key", "value" } }
        };
    }

    private Subscription CreateTestSubscription(
        string id = "sub-1",
        string topicName = "test.topic",
        string consumerGroup = "group-1",
        bool isActive = true,
        MessageFilter? filter = null)
    {
        return new Subscription
        {
            SubscriptionId = id,
            TopicName = topicName,
            ConsumerGroup = consumerGroup,
            ConsumerEndpoint = $"http://consumer-{id}.example.com/webhook",
            Type = SubscriptionType.Push,
            IsActive = isActive,
            Filter = filter,
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
        strategyName.Should().Be("ContentBased");
    }

    [Fact]
    public async Task RouteAsync_WithNoFilters_ReturnsAllSubscriptions()
    {
        // Arrange - Subscriptions with no filters (should match all messages)
        var message = CreateTestMessage(headers: new Dictionary<string, string> { { "region", "us-west" } });
        var subscriptions = new List<Subscription>
        {
            CreateTestSubscription("sub-1", filter: null),
            CreateTestSubscription("sub-2", filter: null),
            CreateTestSubscription("sub-3", filter: null)
        };

        // Act
        var result = await _strategy.RouteAsync(message, subscriptions);

        // Assert
        result.Success.Should().BeTrue();
        result.ConsumerIds.Should().HaveCount(3);
        result.ConsumerIds.Should().Contain("sub-1");
        result.ConsumerIds.Should().Contain("sub-2");
        result.ConsumerIds.Should().Contain("sub-3");
    }

    [Fact]
    public async Task RouteAsync_WithSingleHeaderFilter_ReturnsMatchingSubscriptions()
    {
        // Arrange
        var message = CreateTestMessage(headers: new Dictionary<string, string> { { "region", "us-west" } });
        var subscriptions = new List<Subscription>
        {
            CreateTestSubscription("sub-1", filter: new MessageFilter
            {
                HeaderMatches = new Dictionary<string, string> { { "region", "us-west" } }
            }),
            CreateTestSubscription("sub-2", filter: new MessageFilter
            {
                HeaderMatches = new Dictionary<string, string> { { "region", "us-east" } }
            }),
            CreateTestSubscription("sub-3", filter: new MessageFilter
            {
                HeaderMatches = new Dictionary<string, string> { { "region", "us-west" } }
            })
        };

        // Act
        var result = await _strategy.RouteAsync(message, subscriptions);

        // Assert
        result.Success.Should().BeTrue();
        result.ConsumerIds.Should().HaveCount(2);
        result.ConsumerIds.Should().Contain("sub-1");
        result.ConsumerIds.Should().Contain("sub-3");
        result.ConsumerIds.Should().NotContain("sub-2"); // Different region
    }

    [Fact]
    public async Task RouteAsync_WithMultipleHeaderFilters_ReturnsOnlyFullMatches()
    {
        // Arrange - Message with multiple headers
        var message = CreateTestMessage(headers: new Dictionary<string, string>
        {
            { "region", "us-west" },
            { "environment", "production" },
            { "version", "2.0" }
        });

        var subscriptions = new List<Subscription>
        {
            // Matches all headers
            CreateTestSubscription("sub-1", filter: new MessageFilter
            {
                HeaderMatches = new Dictionary<string, string>
                {
                    { "region", "us-west" },
                    { "environment", "production" }
                }
            }),
            // Partial match (missing environment)
            CreateTestSubscription("sub-2", filter: new MessageFilter
            {
                HeaderMatches = new Dictionary<string, string>
                {
                    { "region", "us-west" },
                    { "environment", "staging" }
                }
            }),
            // Complete match
            CreateTestSubscription("sub-3", filter: new MessageFilter
            {
                HeaderMatches = new Dictionary<string, string>
                {
                    { "region", "us-west" },
                    { "environment", "production" },
                    { "version", "2.0" }
                }
            })
        };

        // Act
        var result = await _strategy.RouteAsync(message, subscriptions);

        // Assert
        result.Success.Should().BeTrue();
        result.ConsumerIds.Should().HaveCount(2);
        result.ConsumerIds.Should().Contain("sub-1"); // Partial match succeeds
        result.ConsumerIds.Should().Contain("sub-3"); // Full match succeeds
        result.ConsumerIds.Should().NotContain("sub-2"); // Wrong environment
    }

    [Fact]
    public async Task RouteAsync_WithNoMatchingFilters_ReturnsFailure()
    {
        // Arrange
        var message = CreateTestMessage(headers: new Dictionary<string, string> { { "region", "eu-central" } });
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
        var result = await _strategy.RouteAsync(message, subscriptions);

        // Assert
        result.Success.Should().BeFalse();
        result.ConsumerIds.Should().BeEmpty();
        result.ErrorMessage.Should().Contain("No matching consumers");
    }

    [Fact]
    public async Task RouteAsync_WithMixedFiltersAndNoFilters_ReturnsAllMatching()
    {
        // Arrange
        var message = CreateTestMessage(headers: new Dictionary<string, string> { { "priority", "high" } });
        var subscriptions = new List<Subscription>
        {
            CreateTestSubscription("sub-1", filter: null), // No filter - matches all
            CreateTestSubscription("sub-2", filter: new MessageFilter
            {
                HeaderMatches = new Dictionary<string, string> { { "priority", "high" } }
            }),
            CreateTestSubscription("sub-3", filter: new MessageFilter
            {
                HeaderMatches = new Dictionary<string, string> { { "priority", "low" } }
            }),
            CreateTestSubscription("sub-4", filter: null) // No filter - matches all
        };

        // Act
        var result = await _strategy.RouteAsync(message, subscriptions);

        // Assert
        result.Success.Should().BeTrue();
        result.ConsumerIds.Should().HaveCount(3);
        result.ConsumerIds.Should().Contain("sub-1"); // No filter
        result.ConsumerIds.Should().Contain("sub-2"); // Matching filter
        result.ConsumerIds.Should().Contain("sub-4"); // No filter
        result.ConsumerIds.Should().NotContain("sub-3"); // Non-matching filter
    }

    [Fact]
    public async Task RouteAsync_WithInactiveSubscriptions_FiltersOutInactive()
    {
        // Arrange
        var message = CreateTestMessage(headers: new Dictionary<string, string> { { "type", "order" } });
        var subscriptions = new List<Subscription>
        {
            CreateTestSubscription("sub-1", isActive: true, filter: new MessageFilter
            {
                HeaderMatches = new Dictionary<string, string> { { "type", "order" } }
            }),
            CreateTestSubscription("sub-2", isActive: false, filter: new MessageFilter
            {
                HeaderMatches = new Dictionary<string, string> { { "type", "order" } }
            }),
            CreateTestSubscription("sub-3", isActive: true, filter: new MessageFilter
            {
                HeaderMatches = new Dictionary<string, string> { { "type", "order" } }
            })
        };

        // Act
        var result = await _strategy.RouteAsync(message, subscriptions);

        // Assert
        result.Success.Should().BeTrue();
        result.ConsumerIds.Should().HaveCount(2);
        result.ConsumerIds.Should().Contain("sub-1");
        result.ConsumerIds.Should().Contain("sub-3");
        result.ConsumerIds.Should().NotContain("sub-2"); // Inactive
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
        result.StrategyName.Should().Be("ContentBased");
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
    public async Task RouteAsync_SetsSuccessToTrue_WhenMatchingConsumersFound()
    {
        // Arrange
        var message = CreateTestMessage(headers: new Dictionary<string, string> { { "status", "active" } });
        var subscriptions = new List<Subscription>
        {
            CreateTestSubscription("sub-1", filter: new MessageFilter
            {
                HeaderMatches = new Dictionary<string, string> { { "status", "active" } }
            })
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
        var message = CreateTestMessage(headers: new Dictionary<string, string> { { "category", "billing" } });
        var subscriptions = new List<Subscription>
        {
            CreateTestSubscription("sub-1", filter: new MessageFilter
            {
                HeaderMatches = new Dictionary<string, string> { { "category", "billing" } }
            }),
            CreateTestSubscription("sub-2", filter: new MessageFilter
            {
                HeaderMatches = new Dictionary<string, string> { { "category", "billing" } }
            })
        };

        // Act
        var result = await _strategy.RouteAsync(message, subscriptions);

        // Assert
        result.Reason.Should().NotBeNullOrWhiteSpace();
        result.Reason.Should().Contain("filter");
    }

    [Fact]
    public async Task RouteAsync_WithCancellationToken_CompletesSuccessfully()
    {
        // Arrange
        var message = CreateTestMessage();
        var subscriptions = new List<Subscription>
        {
            CreateTestSubscription("sub-1", filter: null)
        };
        var cts = new CancellationTokenSource();

        // Act
        var result = await _strategy.RouteAsync(message, subscriptions, cts.Token);

        // Assert
        result.Success.Should().BeTrue();
        result.ConsumerIds.Should().ContainSingle();
    }

    [Fact]
    public async Task RouteAsync_ReturnsMetadataWithFilterInfo()
    {
        // Arrange
        var message = CreateTestMessage(headers: new Dictionary<string, string> { { "app", "web" } });
        var subscriptions = new List<Subscription>
        {
            CreateTestSubscription("sub-1", filter: new MessageFilter
            {
                HeaderMatches = new Dictionary<string, string> { { "app", "web" } }
            }),
            CreateTestSubscription("sub-2", filter: null),
            CreateTestSubscription("sub-3", filter: new MessageFilter
            {
                HeaderMatches = new Dictionary<string, string> { { "app", "mobile" } }
            })
        };

        // Act
        var result = await _strategy.RouteAsync(message, subscriptions);

        // Assert
        result.Metadata.Should().NotBeNull();
        result.Metadata.Should().ContainKey("totalActive");
        result.Metadata!["totalActive"].Should().Be(3);
        result.Metadata.Should().ContainKey("matchedCount");
        result.Metadata["matchedCount"].Should().Be(2); // sub-1 and sub-2
    }

    [Fact]
    public async Task RouteAsync_WithEmptyHeaderFilters_MatchesAllMessages()
    {
        // Arrange - Filter with empty HeaderMatches (should match all)
        var message = CreateTestMessage(headers: new Dictionary<string, string> { { "any", "header" } });
        var subscriptions = new List<Subscription>
        {
            CreateTestSubscription("sub-1", filter: new MessageFilter
            {
                HeaderMatches = new Dictionary<string, string>() // Empty filter
            })
        };

        // Act
        var result = await _strategy.RouteAsync(message, subscriptions);

        // Assert
        result.Success.Should().BeTrue();
        result.ConsumerIds.Should().ContainSingle();
        result.ConsumerIds[0].Should().Be("sub-1");
    }

    [Fact]
    public async Task RouteAsync_PreservesSubscriptionOrder()
    {
        // Arrange
        var message = CreateTestMessage(headers: new Dictionary<string, string> { { "tier", "premium" } });
        var subscriptions = new List<Subscription>
        {
            CreateTestSubscription("sub-A", filter: null),
            CreateTestSubscription("sub-B", filter: new MessageFilter
            {
                HeaderMatches = new Dictionary<string, string> { { "tier", "premium" } }
            }),
            CreateTestSubscription("sub-C", filter: null),
            CreateTestSubscription("sub-D", filter: new MessageFilter
            {
                HeaderMatches = new Dictionary<string, string> { { "tier", "premium" } }
            })
        };

        // Act
        var result = await _strategy.RouteAsync(message, subscriptions);

        // Assert - Should maintain order: sub-A, sub-B, sub-C, sub-D
        result.ConsumerIds.Should().HaveCount(4);
        result.ConsumerIds[0].Should().Be("sub-A");
        result.ConsumerIds[1].Should().Be("sub-B");
        result.ConsumerIds[2].Should().Be("sub-C");
        result.ConsumerIds[3].Should().Be("sub-D");
    }

    [Fact]
    public async Task RouteAsync_WithComplexFiltering_HandlesMultipleScenarios()
    {
        // Arrange - Complex message with many headers
        var message = CreateTestMessage(headers: new Dictionary<string, string>
        {
            { "region", "us-west" },
            { "environment", "production" },
            { "tier", "premium" },
            { "version", "3.0" }
        });

        var subscriptions = new List<Subscription>
        {
            // Matches: region + environment
            CreateTestSubscription("sub-1", filter: new MessageFilter
            {
                HeaderMatches = new Dictionary<string, string>
                {
                    { "region", "us-west" },
                    { "environment", "production" }
                }
            }),
            // No match: wrong environment
            CreateTestSubscription("sub-2", filter: new MessageFilter
            {
                HeaderMatches = new Dictionary<string, string>
                {
                    { "region", "us-west" },
                    { "environment", "staging" }
                }
            }),
            // Matches: tier only
            CreateTestSubscription("sub-3", filter: new MessageFilter
            {
                HeaderMatches = new Dictionary<string, string>
                {
                    { "tier", "premium" }
                }
            }),
            // Matches: no filter
            CreateTestSubscription("sub-4", filter: null),
            // No match: missing header
            CreateTestSubscription("sub-5", filter: new MessageFilter
            {
                HeaderMatches = new Dictionary<string, string>
                {
                    { "customer", "vip" } // Message doesn't have this header
                }
            })
        };

        // Act
        var result = await _strategy.RouteAsync(message, subscriptions);

        // Assert
        result.Success.Should().BeTrue();
        result.ConsumerIds.Should().HaveCount(3);
        result.ConsumerIds.Should().Contain("sub-1"); // Matched region + environment
        result.ConsumerIds.Should().Contain("sub-3"); // Matched tier
        result.ConsumerIds.Should().Contain("sub-4"); // No filter
        result.ConsumerIds.Should().NotContain("sub-2"); // Wrong environment
        result.ConsumerIds.Should().NotContain("sub-5"); // Missing header
    }
}
