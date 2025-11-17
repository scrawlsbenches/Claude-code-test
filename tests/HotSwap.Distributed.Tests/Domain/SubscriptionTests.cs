using FluentAssertions;
using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;
using Xunit;

namespace HotSwap.Distributed.Tests.Domain;

public class SubscriptionTests
{
    [Fact]
    public void IsValid_WithValidPullSubscription_ReturnsTrue()
    {
        // Arrange
        var subscription = new Subscription
        {
            SubscriptionId = Guid.NewGuid().ToString(),
            TopicName = "test.topic",
            ConsumerGroup = "test-group",
            ConsumerEndpoint = "",
            Type = SubscriptionType.Pull
        };

        // Act
        var result = subscription.IsValid(out var errors);

        // Assert
        result.Should().BeTrue();
        errors.Should().BeEmpty();
    }

    [Fact]
    public void IsValid_WithValidPushSubscription_ReturnsTrue()
    {
        // Arrange
        var subscription = new Subscription
        {
            SubscriptionId = Guid.NewGuid().ToString(),
            TopicName = "test.topic",
            ConsumerGroup = "test-group",
            ConsumerEndpoint = "https://example.com/webhook",
            Type = SubscriptionType.Push
        };

        // Act
        var result = subscription.IsValid(out var errors);

        // Assert
        result.Should().BeTrue();
        errors.Should().BeEmpty();
    }

    [Fact]
    public void IsValid_WithMissingSubscriptionId_ReturnsFalse()
    {
        // Arrange
        var subscription = new Subscription
        {
            SubscriptionId = "",
            TopicName = "test.topic",
            ConsumerGroup = "test-group",
            ConsumerEndpoint = ""
        };

        // Act
        var result = subscription.IsValid(out var errors);

        // Assert
        result.Should().BeFalse();
        errors.Should().Contain("SubscriptionId is required");
    }

    [Fact]
    public void IsValid_WithMissingTopicName_ReturnsFalse()
    {
        // Arrange
        var subscription = new Subscription
        {
            SubscriptionId = Guid.NewGuid().ToString(),
            TopicName = "",
            ConsumerGroup = "test-group",
            ConsumerEndpoint = ""
        };

        // Act
        var result = subscription.IsValid(out var errors);

        // Assert
        result.Should().BeFalse();
        errors.Should().Contain("TopicName is required");
    }

    [Fact]
    public void IsValid_WithMissingConsumerGroup_ReturnsFalse()
    {
        // Arrange
        var subscription = new Subscription
        {
            SubscriptionId = Guid.NewGuid().ToString(),
            TopicName = "test.topic",
            ConsumerGroup = "",
            ConsumerEndpoint = ""
        };

        // Act
        var result = subscription.IsValid(out var errors);

        // Assert
        result.Should().BeFalse();
        errors.Should().Contain("ConsumerGroup is required");
    }

    [Fact]
    public void IsValid_PushSubscriptionWithoutEndpoint_ReturnsFalse()
    {
        // Arrange
        var subscription = new Subscription
        {
            SubscriptionId = Guid.NewGuid().ToString(),
            TopicName = "test.topic",
            ConsumerGroup = "test-group",
            ConsumerEndpoint = "",
            Type = SubscriptionType.Push
        };

        // Act
        var result = subscription.IsValid(out var errors);

        // Assert
        result.Should().BeFalse();
        errors.Should().Contain("ConsumerEndpoint is required for Push subscriptions");
    }

    [Fact]
    public void IsValid_PushSubscriptionWithInvalidUrl_ReturnsFalse()
    {
        // Arrange
        var subscription = new Subscription
        {
            SubscriptionId = Guid.NewGuid().ToString(),
            TopicName = "test.topic",
            ConsumerGroup = "test-group",
            ConsumerEndpoint = "not-a-url",
            Type = SubscriptionType.Push
        };

        // Act
        var result = subscription.IsValid(out var errors);

        // Assert
        result.Should().BeFalse();
        errors.Should().Contain("ConsumerEndpoint must be a valid absolute URL");
    }

    [Fact]
    public void IsValid_WithNegativeMaxRetries_ReturnsFalse()
    {
        // Arrange
        var subscription = new Subscription
        {
            SubscriptionId = Guid.NewGuid().ToString(),
            TopicName = "test.topic",
            ConsumerGroup = "test-group",
            ConsumerEndpoint = "",
            MaxRetries = -1
        };

        // Act
        var result = subscription.IsValid(out var errors);

        // Assert
        result.Should().BeFalse();
        errors.Should().Contain("MaxRetries must be between 0 and 10");
    }

    [Fact]
    public void IsValid_WithMaxRetriesGreaterThan10_ReturnsFalse()
    {
        // Arrange
        var subscription = new Subscription
        {
            SubscriptionId = Guid.NewGuid().ToString(),
            TopicName = "test.topic",
            ConsumerGroup = "test-group",
            ConsumerEndpoint = "",
            MaxRetries = 11
        };

        // Act
        var result = subscription.IsValid(out var errors);

        // Assert
        result.Should().BeFalse();
        errors.Should().Contain("MaxRetries must be between 0 and 10");
    }

    [Fact]
    public void IsValid_WithAckTimeoutLessThan1Second_ReturnsFalse()
    {
        // Arrange
        var subscription = new Subscription
        {
            SubscriptionId = Guid.NewGuid().ToString(),
            TopicName = "test.topic",
            ConsumerGroup = "test-group",
            ConsumerEndpoint = "",
            AckTimeout = TimeSpan.FromMilliseconds(500)
        };

        // Act
        var result = subscription.IsValid(out var errors);

        // Assert
        result.Should().BeFalse();
        errors.Should().Contain("AckTimeout must be between 1 second and 10 minutes");
    }

    [Fact]
    public void IsValid_WithAckTimeoutGreaterThan10Minutes_ReturnsFalse()
    {
        // Arrange
        var subscription = new Subscription
        {
            SubscriptionId = Guid.NewGuid().ToString(),
            TopicName = "test.topic",
            ConsumerGroup = "test-group",
            ConsumerEndpoint = "",
            AckTimeout = TimeSpan.FromMinutes(11)
        };

        // Act
        var result = subscription.IsValid(out var errors);

        // Assert
        result.Should().BeFalse();
        errors.Should().Contain("AckTimeout must be between 1 second and 10 minutes");
    }

    [Fact]
    public void Subscription_DefaultType_ShouldBePull()
    {
        // Arrange & Act
        var subscription = new Subscription
        {
            SubscriptionId = Guid.NewGuid().ToString(),
            TopicName = "test.topic",
            ConsumerGroup = "test-group",
            ConsumerEndpoint = ""
        };

        // Assert
        subscription.Type.Should().Be(SubscriptionType.Pull);
    }

    [Fact]
    public void Subscription_DefaultMaxRetries_ShouldBe3()
    {
        // Arrange & Act
        var subscription = new Subscription
        {
            SubscriptionId = Guid.NewGuid().ToString(),
            TopicName = "test.topic",
            ConsumerGroup = "test-group",
            ConsumerEndpoint = ""
        };

        // Assert
        subscription.MaxRetries.Should().Be(3);
    }

    [Fact]
    public void Subscription_DefaultAckTimeout_ShouldBe30Seconds()
    {
        // Arrange & Act
        var subscription = new Subscription
        {
            SubscriptionId = Guid.NewGuid().ToString(),
            TopicName = "test.topic",
            ConsumerGroup = "test-group",
            ConsumerEndpoint = ""
        };

        // Assert
        subscription.AckTimeout.Should().Be(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void Subscription_DefaultIsActive_ShouldBeTrue()
    {
        // Arrange & Act
        var subscription = new Subscription
        {
            SubscriptionId = Guid.NewGuid().ToString(),
            TopicName = "test.topic",
            ConsumerGroup = "test-group",
            ConsumerEndpoint = ""
        };

        // Assert
        subscription.IsActive.Should().BeTrue();
    }
}
