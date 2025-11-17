using FluentAssertions;
using HotSwap.Distributed.Domain.Models;
using Xunit;

namespace HotSwap.Distributed.Tests.Domain;

public class MessageFilterTests
{
    [Fact]
    public void Matches_WithNoFilters_ReturnsTrue()
    {
        // Arrange
        var filter = new MessageFilter();
        var message = new Message
        {
            MessageId = Guid.NewGuid().ToString(),
            TopicName = "test.topic",
            Payload = "{\"test\":\"data\"}",
            SchemaVersion = "1.0"
        };

        // Act
        var result = filter.Matches(message);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Matches_WithMatchingHeader_ReturnsTrue()
    {
        // Arrange
        var filter = new MessageFilter
        {
            HeaderMatches = new Dictionary<string, string>
            {
                { "environment", "production" }
            }
        };

        var message = new Message
        {
            MessageId = Guid.NewGuid().ToString(),
            TopicName = "test.topic",
            Payload = "{\"test\":\"data\"}",
            SchemaVersion = "1.0",
            Headers = new Dictionary<string, string>
            {
                { "environment", "production" }
            }
        };

        // Act
        var result = filter.Matches(message);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Matches_WithNonMatchingHeader_ReturnsFalse()
    {
        // Arrange
        var filter = new MessageFilter
        {
            HeaderMatches = new Dictionary<string, string>
            {
                { "environment", "production" }
            }
        };

        var message = new Message
        {
            MessageId = Guid.NewGuid().ToString(),
            TopicName = "test.topic",
            Payload = "{\"test\":\"data\"}",
            SchemaVersion = "1.0",
            Headers = new Dictionary<string, string>
            {
                { "environment", "staging" }
            }
        };

        // Act
        var result = filter.Matches(message);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Matches_WithMissingHeader_ReturnsFalse()
    {
        // Arrange
        var filter = new MessageFilter
        {
            HeaderMatches = new Dictionary<string, string>
            {
                { "environment", "production" }
            }
        };

        var message = new Message
        {
            MessageId = Guid.NewGuid().ToString(),
            TopicName = "test.topic",
            Payload = "{\"test\":\"data\"}",
            SchemaVersion = "1.0"
        };

        // Act
        var result = filter.Matches(message);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Matches_WithMultipleMatchingHeaders_ReturnsTrue()
    {
        // Arrange
        var filter = new MessageFilter
        {
            HeaderMatches = new Dictionary<string, string>
            {
                { "environment", "production" },
                { "region", "us-east-1" }
            }
        };

        var message = new Message
        {
            MessageId = Guid.NewGuid().ToString(),
            TopicName = "test.topic",
            Payload = "{\"test\":\"data\"}",
            SchemaVersion = "1.0",
            Headers = new Dictionary<string, string>
            {
                { "environment", "production" },
                { "region", "us-east-1" }
            }
        };

        // Act
        var result = filter.Matches(message);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Matches_WithOneNonMatchingHeader_ReturnsFalse()
    {
        // Arrange
        var filter = new MessageFilter
        {
            HeaderMatches = new Dictionary<string, string>
            {
                { "environment", "production" },
                { "region", "us-east-1" }
            }
        };

        var message = new Message
        {
            MessageId = Guid.NewGuid().ToString(),
            TopicName = "test.topic",
            Payload = "{\"test\":\"data\"}",
            SchemaVersion = "1.0",
            Headers = new Dictionary<string, string>
            {
                { "environment", "production" },
                { "region", "us-west-2" } // Doesn't match
            }
        };

        // Act
        var result = filter.Matches(message);

        // Assert
        result.Should().BeFalse();
    }
}
