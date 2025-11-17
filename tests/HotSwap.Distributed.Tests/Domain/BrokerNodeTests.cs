using FluentAssertions;
using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;
using Xunit;

namespace HotSwap.Distributed.Tests.Domain;

public class BrokerNodeTests
{
    [Fact]
    public void IsHealthy_WithRecentHeartbeat_ReturnsTrue()
    {
        // Arrange
        var broker = new BrokerNode
        {
            NodeId = "broker-1",
            Hostname = "localhost",
            LastHeartbeat = DateTime.UtcNow.AddSeconds(-30)
        };

        // Act
        var result = broker.IsHealthy();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsHealthy_WithOldHeartbeat_ReturnsFalse()
    {
        // Arrange
        var broker = new BrokerNode
        {
            NodeId = "broker-1",
            Hostname = "localhost",
            LastHeartbeat = DateTime.UtcNow.AddMinutes(-3)
        };

        // Act
        var result = broker.IsHealthy();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsHealthy_WithUnhealthyStatus_ReturnsFalse()
    {
        // Arrange
        var broker = new BrokerNode
        {
            NodeId = "broker-1",
            Hostname = "localhost",
            LastHeartbeat = DateTime.UtcNow,
            Health = new BrokerHealth { IsHealthy = false }
        };

        // Act
        var result = broker.IsHealthy();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void RecordHeartbeat_UpdatesLastHeartbeat()
    {
        // Arrange
        var broker = new BrokerNode
        {
            NodeId = "broker-1",
            Hostname = "localhost",
            LastHeartbeat = DateTime.UtcNow.AddMinutes(-5)
        };

        var before = DateTime.UtcNow;

        // Act
        broker.RecordHeartbeat();

        var after = DateTime.UtcNow;

        // Assert
        broker.LastHeartbeat.Should().BeOnOrAfter(before);
        broker.LastHeartbeat.Should().BeOnOrBefore(after);
    }

    [Fact]
    public void BrokerNode_DefaultRole_ShouldBeReplica()
    {
        // Arrange & Act
        var broker = new BrokerNode
        {
            NodeId = "broker-1",
            Hostname = "localhost"
        };

        // Assert
        broker.Role.Should().Be(BrokerRole.Replica);
    }

    [Fact]
    public void BrokerNode_DefaultPort_ShouldBe5050()
    {
        // Arrange & Act
        var broker = new BrokerNode
        {
            NodeId = "broker-1",
            Hostname = "localhost"
        };

        // Assert
        broker.Port.Should().Be(5050);
    }

    [Fact]
    public void BrokerNode_AssignedTopics_ShouldBeInitialized()
    {
        // Arrange & Act
        var broker = new BrokerNode
        {
            NodeId = "broker-1",
            Hostname = "localhost"
        };

        // Assert
        broker.AssignedTopics.Should().NotBeNull();
        broker.AssignedTopics.Should().BeEmpty();
    }

    [Fact]
    public void BrokerHealth_DefaultIsHealthy_ShouldBeTrue()
    {
        // Arrange & Act
        var health = new BrokerHealth();

        // Assert
        health.IsHealthy.Should().BeTrue();
    }

    [Fact]
    public void BrokerHealth_DefaultQueueDepth_ShouldBeZero()
    {
        // Arrange & Act
        var health = new BrokerHealth();

        // Assert
        health.QueueDepth.Should().Be(0);
    }

    [Fact]
    public void BrokerHealth_DefaultActiveConsumers_ShouldBeZero()
    {
        // Arrange & Act
        var health = new BrokerHealth();

        // Assert
        health.ActiveConsumers.Should().Be(0);
    }

    [Fact]
    public void BrokerMetrics_DefaultMessagesPublished_ShouldBeZero()
    {
        // Arrange & Act
        var metrics = new BrokerMetrics();

        // Assert
        metrics.MessagesPublished.Should().Be(0);
    }

    [Fact]
    public void BrokerMetrics_DefaultMessagesDelivered_ShouldBeZero()
    {
        // Arrange & Act
        var metrics = new BrokerMetrics();

        // Assert
        metrics.MessagesDelivered.Should().Be(0);
    }

    [Fact]
    public void BrokerMetrics_DefaultThroughput_ShouldBeZero()
    {
        // Arrange & Act
        var metrics = new BrokerMetrics();

        // Assert
        metrics.ThroughputMsgPerSec.Should().Be(0);
    }
}
