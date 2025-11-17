using FluentAssertions;
using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;
using Xunit;

namespace HotSwap.Distributed.Tests.Domain;

public class MessageTests
{
    [Fact]
    public void IsValid_WithValidMessage_ReturnsTrue()
    {
        // Arrange
        var message = new Message
        {
            MessageId = Guid.NewGuid().ToString(),
            TopicName = "test.topic",
            Payload = "{\"test\":\"data\"}",
            SchemaVersion = "1.0"
        };

        // Act
        var result = message.IsValid(out var errors);

        // Assert
        result.Should().BeTrue();
        errors.Should().BeEmpty();
    }

    [Fact]
    public void IsValid_WithMissingMessageId_ReturnsFalse()
    {
        // Arrange
        var message = new Message
        {
            MessageId = "",
            TopicName = "test.topic",
            Payload = "{\"test\":\"data\"}",
            SchemaVersion = "1.0"
        };

        // Act
        var result = message.IsValid(out var errors);

        // Assert
        result.Should().BeFalse();
        errors.Should().Contain("MessageId is required");
    }

    [Fact]
    public void IsValid_WithMissingTopicName_ReturnsFalse()
    {
        // Arrange
        var message = new Message
        {
            MessageId = Guid.NewGuid().ToString(),
            TopicName = "",
            Payload = "{\"test\":\"data\"}",
            SchemaVersion = "1.0"
        };

        // Act
        var result = message.IsValid(out var errors);

        // Assert
        result.Should().BeFalse();
        errors.Should().Contain("TopicName is required");
    }

    [Fact]
    public void IsValid_WithMissingPayload_ReturnsFalse()
    {
        // Arrange
        var message = new Message
        {
            MessageId = Guid.NewGuid().ToString(),
            TopicName = "test.topic",
            Payload = "",
            SchemaVersion = "1.0"
        };

        // Act
        var result = message.IsValid(out var errors);

        // Assert
        result.Should().BeFalse();
        errors.Should().Contain("Payload is required");
    }

    [Fact]
    public void IsValid_WithMissingSchemaVersion_ReturnsFalse()
    {
        // Arrange
        var message = new Message
        {
            MessageId = Guid.NewGuid().ToString(),
            TopicName = "test.topic",
            Payload = "{\"test\":\"data\"}",
            SchemaVersion = ""
        };

        // Act
        var result = message.IsValid(out var errors);

        // Assert
        result.Should().BeFalse();
        errors.Should().Contain("SchemaVersion is required");
    }

    [Fact]
    public void IsValid_WithNegativePriority_ReturnsFalse()
    {
        // Arrange
        var message = new Message
        {
            MessageId = Guid.NewGuid().ToString(),
            TopicName = "test.topic",
            Payload = "{\"test\":\"data\"}",
            SchemaVersion = "1.0",
            Priority = -1
        };

        // Act
        var result = message.IsValid(out var errors);

        // Assert
        result.Should().BeFalse();
        errors.Should().Contain("Priority must be between 0 and 9");
    }

    [Fact]
    public void IsValid_WithPriorityGreaterThan9_ReturnsFalse()
    {
        // Arrange
        var message = new Message
        {
            MessageId = Guid.NewGuid().ToString(),
            TopicName = "test.topic",
            Payload = "{\"test\":\"data\"}",
            SchemaVersion = "1.0",
            Priority = 10
        };

        // Act
        var result = message.IsValid(out var errors);

        // Assert
        result.Should().BeFalse();
        errors.Should().Contain("Priority must be between 0 and 9");
    }

    [Fact]
    public void IsValid_WithValidPriority_ReturnsTrue()
    {
        // Arrange
        var message = new Message
        {
            MessageId = Guid.NewGuid().ToString(),
            TopicName = "test.topic",
            Payload = "{\"test\":\"data\"}",
            SchemaVersion = "1.0",
            Priority = 5
        };

        // Act
        var result = message.IsValid(out var errors);

        // Assert
        result.Should().BeTrue();
        errors.Should().BeEmpty();
    }

    [Fact]
    public void IsValid_WithPayloadExceeding1MB_ReturnsFalse()
    {
        // Arrange
        var largePayload = new string('x', 1048577); // 1 MB + 1 byte
        var message = new Message
        {
            MessageId = Guid.NewGuid().ToString(),
            TopicName = "test.topic",
            Payload = largePayload,
            SchemaVersion = "1.0"
        };

        // Act
        var result = message.IsValid(out var errors);

        // Assert
        result.Should().BeFalse();
        errors.Should().Contain("Payload exceeds maximum size of 1 MB");
    }

    [Fact]
    public void IsValid_WithPayloadAtExactly1MB_ReturnsTrue()
    {
        // Arrange
        var largePayload = new string('x', 1048576); // Exactly 1 MB
        var message = new Message
        {
            MessageId = Guid.NewGuid().ToString(),
            TopicName = "test.topic",
            Payload = largePayload,
            SchemaVersion = "1.0"
        };

        // Act
        var result = message.IsValid(out var errors);

        // Assert
        result.Should().BeTrue();
        errors.Should().BeEmpty();
    }

    [Fact]
    public void IsValid_WithMultipleErrors_ReturnsAllErrors()
    {
        // Arrange
        var message = new Message
        {
            MessageId = "",
            TopicName = "",
            Payload = "",
            SchemaVersion = "",
            Priority = 15
        };

        // Act
        var result = message.IsValid(out var errors);

        // Assert
        result.Should().BeFalse();
        errors.Should().HaveCount(5);
        errors.Should().Contain("MessageId is required");
        errors.Should().Contain("TopicName is required");
        errors.Should().Contain("Payload is required");
        errors.Should().Contain("SchemaVersion is required");
        errors.Should().Contain("Priority must be between 0 and 9");
    }

    [Fact]
    public void IsExpired_WithNoExpirationSet_ReturnsFalse()
    {
        // Arrange
        var message = new Message
        {
            MessageId = Guid.NewGuid().ToString(),
            TopicName = "test.topic",
            Payload = "{\"test\":\"data\"}",
            SchemaVersion = "1.0",
            ExpiresAt = null
        };

        // Act
        var result = message.IsExpired();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsExpired_WithFutureExpiration_ReturnsFalse()
    {
        // Arrange
        var message = new Message
        {
            MessageId = Guid.NewGuid().ToString(),
            TopicName = "test.topic",
            Payload = "{\"test\":\"data\"}",
            SchemaVersion = "1.0",
            ExpiresAt = DateTime.UtcNow.AddMinutes(10)
        };

        // Act
        var result = message.IsExpired();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsExpired_WithPastExpiration_ReturnsTrue()
    {
        // Arrange
        var message = new Message
        {
            MessageId = Guid.NewGuid().ToString(),
            TopicName = "test.topic",
            Payload = "{\"test\":\"data\"}",
            SchemaVersion = "1.0",
            ExpiresAt = DateTime.UtcNow.AddMinutes(-10)
        };

        // Act
        var result = message.IsExpired();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Message_DefaultStatus_ShouldBePending()
    {
        // Arrange & Act
        var message = new Message
        {
            MessageId = Guid.NewGuid().ToString(),
            TopicName = "test.topic",
            Payload = "{\"test\":\"data\"}",
            SchemaVersion = "1.0"
        };

        // Assert
        message.Status.Should().Be(MessageStatus.Pending);
    }

    [Fact]
    public void Message_DefaultPriority_ShouldBeZero()
    {
        // Arrange & Act
        var message = new Message
        {
            MessageId = Guid.NewGuid().ToString(),
            TopicName = "test.topic",
            Payload = "{\"test\":\"data\"}",
            SchemaVersion = "1.0"
        };

        // Assert
        message.Priority.Should().Be(0);
    }

    [Fact]
    public void Message_DefaultDeliveryAttempts_ShouldBeZero()
    {
        // Arrange & Act
        var message = new Message
        {
            MessageId = Guid.NewGuid().ToString(),
            TopicName = "test.topic",
            Payload = "{\"test\":\"data\"}",
            SchemaVersion = "1.0"
        };

        // Assert
        message.DeliveryAttempts.Should().Be(0);
    }

    [Fact]
    public void Message_Headers_ShouldBeInitialized()
    {
        // Arrange & Act
        var message = new Message
        {
            MessageId = Guid.NewGuid().ToString(),
            TopicName = "test.topic",
            Payload = "{\"test\":\"data\"}",
            SchemaVersion = "1.0"
        };

        // Assert
        message.Headers.Should().NotBeNull();
        message.Headers.Should().BeEmpty();
    }

    [Fact]
    public void Message_Timestamp_ShouldBeSetToUtcNow()
    {
        // Arrange
        var before = DateTime.UtcNow;

        // Act
        var message = new Message
        {
            MessageId = Guid.NewGuid().ToString(),
            TopicName = "test.topic",
            Payload = "{\"test\":\"data\"}",
            SchemaVersion = "1.0"
        };

        var after = DateTime.UtcNow;

        // Assert
        message.Timestamp.Should().BeOnOrAfter(before);
        message.Timestamp.Should().BeOnOrBefore(after);
    }
}
