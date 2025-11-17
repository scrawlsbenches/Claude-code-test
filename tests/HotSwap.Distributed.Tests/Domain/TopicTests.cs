using FluentAssertions;
using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;
using Xunit;

namespace HotSwap.Distributed.Tests.Domain;

public class TopicTests
{
    [Fact]
    public void IsValid_WithValidTopic_ReturnsTrue()
    {
        // Arrange
        var topic = new Topic
        {
            Name = "test.topic",
            SchemaId = "test.schema.v1"
        };

        // Act
        var result = topic.IsValid(out var errors);

        // Assert
        result.Should().BeTrue();
        errors.Should().BeEmpty();
    }

    [Fact]
    public void IsValid_WithMissingName_ReturnsFalse()
    {
        // Arrange
        var topic = new Topic
        {
            Name = "",
            SchemaId = "test.schema.v1"
        };

        // Act
        var result = topic.IsValid(out var errors);

        // Assert
        result.Should().BeFalse();
        errors.Should().Contain("Name is required");
    }

    [Fact]
    public void IsValid_WithInvalidNameCharacters_ReturnsFalse()
    {
        // Arrange
        var topic = new Topic
        {
            Name = "test@topic",
            SchemaId = "test.schema.v1"
        };

        // Act
        var result = topic.IsValid(out var errors);

        // Assert
        result.Should().BeFalse();
        errors.Should().Contain("Name must contain only alphanumeric characters, dots, dashes, and underscores");
    }

    [Fact]
    public void IsValid_WithMissingSchemaId_ReturnsFalse()
    {
        // Arrange
        var topic = new Topic
        {
            Name = "test.topic",
            SchemaId = ""
        };

        // Act
        var result = topic.IsValid(out var errors);

        // Assert
        result.Should().BeFalse();
        errors.Should().Contain("SchemaId is required");
    }

    [Fact]
    public void IsValid_WithPartitionCountLessThan1_ReturnsFalse()
    {
        // Arrange
        var topic = new Topic
        {
            Name = "test.topic",
            SchemaId = "test.schema.v1",
            PartitionCount = 0
        };

        // Act
        var result = topic.IsValid(out var errors);

        // Assert
        result.Should().BeFalse();
        errors.Should().Contain("PartitionCount must be between 1 and 16");
    }

    [Fact]
    public void IsValid_WithPartitionCountGreaterThan16_ReturnsFalse()
    {
        // Arrange
        var topic = new Topic
        {
            Name = "test.topic",
            SchemaId = "test.schema.v1",
            PartitionCount = 17
        };

        // Act
        var result = topic.IsValid(out var errors);

        // Assert
        result.Should().BeFalse();
        errors.Should().Contain("PartitionCount must be between 1 and 16");
    }

    [Fact]
    public void IsValid_WithReplicationFactorLessThan1_ReturnsFalse()
    {
        // Arrange
        var topic = new Topic
        {
            Name = "test.topic",
            SchemaId = "test.schema.v1",
            ReplicationFactor = 0
        };

        // Act
        var result = topic.IsValid(out var errors);

        // Assert
        result.Should().BeFalse();
        errors.Should().Contain("ReplicationFactor must be between 1 and 3");
    }

    [Fact]
    public void IsValid_WithReplicationFactorGreaterThan3_ReturnsFalse()
    {
        // Arrange
        var topic = new Topic
        {
            Name = "test.topic",
            SchemaId = "test.schema.v1",
            ReplicationFactor = 4
        };

        // Act
        var result = topic.IsValid(out var errors);

        // Assert
        result.Should().BeFalse();
        errors.Should().Contain("ReplicationFactor must be between 1 and 3");
    }

    [Fact]
    public void IsValid_WithRetentionPeriodLessThan1Hour_ReturnsFalse()
    {
        // Arrange
        var topic = new Topic
        {
            Name = "test.topic",
            SchemaId = "test.schema.v1",
            RetentionPeriod = TimeSpan.FromMinutes(30)
        };

        // Act
        var result = topic.IsValid(out var errors);

        // Assert
        result.Should().BeFalse();
        errors.Should().Contain("RetentionPeriod must be at least 1 hour");
    }

    [Fact]
    public void GetDLQName_ReturnsTopicNameWithDLQSuffix()
    {
        // Arrange
        var topic = new Topic
        {
            Name = "test.topic",
            SchemaId = "test.schema.v1"
        };

        // Act
        var dlqName = topic.GetDLQName();

        // Assert
        dlqName.Should().Be("test.topic.dlq");
    }

    [Fact]
    public void Topic_DefaultType_ShouldBePubSub()
    {
        // Arrange & Act
        var topic = new Topic
        {
            Name = "test.topic",
            SchemaId = "test.schema.v1"
        };

        // Assert
        topic.Type.Should().Be(TopicType.PubSub);
    }

    [Fact]
    public void Topic_DefaultDeliveryGuarantee_ShouldBeAtLeastOnce()
    {
        // Arrange & Act
        var topic = new Topic
        {
            Name = "test.topic",
            SchemaId = "test.schema.v1"
        };

        // Assert
        topic.DeliveryGuarantee.Should().Be(DeliveryGuarantee.AtLeastOnce);
    }

    [Fact]
    public void Topic_DefaultRetentionPeriod_ShouldBe7Days()
    {
        // Arrange & Act
        var topic = new Topic
        {
            Name = "test.topic",
            SchemaId = "test.schema.v1"
        };

        // Assert
        topic.RetentionPeriod.Should().Be(TimeSpan.FromDays(7));
    }

    [Fact]
    public void Topic_DefaultPartitionCount_ShouldBe1()
    {
        // Arrange & Act
        var topic = new Topic
        {
            Name = "test.topic",
            SchemaId = "test.schema.v1"
        };

        // Assert
        topic.PartitionCount.Should().Be(1);
    }

    [Fact]
    public void Topic_DefaultReplicationFactor_ShouldBe2()
    {
        // Arrange & Act
        var topic = new Topic
        {
            Name = "test.topic",
            SchemaId = "test.schema.v1"
        };

        // Assert
        topic.ReplicationFactor.Should().Be(2);
    }
}
