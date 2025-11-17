using FluentAssertions;
using HotSwap.Distributed.Domain.Models;
using Xunit;

namespace HotSwap.Distributed.Tests.Domain;

public class RouteResultTests
{
    [Fact]
    public void CreateSuccess_WithValidParameters_ReturnsSuccessResult()
    {
        // Arrange
        var strategyName = "LoadBalanced";
        var consumerIds = new List<string> { "consumer-1", "consumer-2" };
        var reason = "Round-robin selection";
        var metadata = new Dictionary<string, object> { { "index", 0 } };

        // Act
        var result = RouteResult.CreateSuccess(strategyName, consumerIds, reason, metadata);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.StrategyName.Should().Be(strategyName);
        result.ConsumerIds.Should().BeEquivalentTo(consumerIds);
        result.Reason.Should().Be(reason);
        result.Metadata.Should().ContainKey("index");
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void CreateSuccess_WithMinimalParameters_ReturnsSuccessResult()
    {
        // Arrange
        var strategyName = "Direct";
        var consumerIds = new List<string> { "consumer-1" };

        // Act
        var result = RouteResult.CreateSuccess(strategyName, consumerIds);

        // Assert
        result.Success.Should().BeTrue();
        result.StrategyName.Should().Be(strategyName);
        result.ConsumerIds.Should().ContainSingle();
        result.Reason.Should().BeNull();
        result.Metadata.Should().BeNull();
    }

    [Fact]
    public void CreateFailure_WithErrorMessage_ReturnsFailureResult()
    {
        // Arrange
        var strategyName = "Priority";
        var errorMessage = "No consumers available";

        // Act
        var result = RouteResult.CreateFailure(strategyName, errorMessage);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.StrategyName.Should().Be(strategyName);
        result.ConsumerIds.Should().BeEmpty();
        result.ErrorMessage.Should().Be(errorMessage);
    }

    [Fact]
    public void HasConsumers_WithConsumers_ReturnsTrue()
    {
        // Arrange
        var result = RouteResult.CreateSuccess("Direct", new List<string> { "consumer-1" });

        // Act
        var hasConsumers = result.HasConsumers();

        // Assert
        hasConsumers.Should().BeTrue();
    }

    [Fact]
    public void HasConsumers_WithNoConsumers_ReturnsFalse()
    {
        // Arrange
        var result = RouteResult.CreateFailure("Direct", "No consumers");

        // Act
        var hasConsumers = result.HasConsumers();

        // Assert
        hasConsumers.Should().BeFalse();
    }

    [Fact]
    public void GetConsumerCount_WithMultipleConsumers_ReturnsCorrectCount()
    {
        // Arrange
        var consumerIds = new List<string> { "consumer-1", "consumer-2", "consumer-3" };
        var result = RouteResult.CreateSuccess("FanOut", consumerIds);

        // Act
        var count = result.GetConsumerCount();

        // Assert
        count.Should().Be(3);
    }

    [Fact]
    public void GetConsumerCount_WithNoConsumers_ReturnsZero()
    {
        // Arrange
        var result = RouteResult.CreateFailure("Direct", "No consumers");

        // Act
        var count = result.GetConsumerCount();

        // Assert
        count.Should().Be(0);
    }

    [Fact]
    public void CreateSuccess_WithEmptyConsumerList_ReturnsSuccessWithNoConsumers()
    {
        // Arrange
        var strategyName = "ContentBased";
        var emptyList = new List<string>();

        // Act
        var result = RouteResult.CreateSuccess(strategyName, emptyList);

        // Assert
        result.Success.Should().BeTrue();
        result.HasConsumers().Should().BeFalse();
        result.GetConsumerCount().Should().Be(0);
    }

    [Fact]
    public void RouteResult_WithMetadata_StoresAndRetrievesCorrectly()
    {
        // Arrange
        var metadata = new Dictionary<string, object>
        {
            { "roundRobinIndex", 5 },
            { "priorityScore", 8.7 },
            { "consumerLoad", new List<int> { 10, 20, 15 } }
        };
        var result = RouteResult.CreateSuccess("LoadBalanced", new List<string> { "consumer-1" }, metadata: metadata);

        // Act & Assert
        result.Metadata.Should().NotBeNull();
        result.Metadata.Should().HaveCount(3);
        result.Metadata!["roundRobinIndex"].Should().Be(5);
        result.Metadata["priorityScore"].Should().Be(8.7);
        result.Metadata["consumerLoad"].Should().BeEquivalentTo(new List<int> { 10, 20, 15 });
    }

    [Fact]
    public void RouteResult_RequiredProperties_CanBeSet()
    {
        // Arrange & Act
        var result = new RouteResult
        {
            ConsumerIds = new List<string> { "test-consumer" },
            StrategyName = "TestStrategy",
            Success = true
        };

        // Assert
        result.ConsumerIds.Should().ContainSingle();
        result.StrategyName.Should().Be("TestStrategy");
        result.Success.Should().BeTrue();
    }
}
