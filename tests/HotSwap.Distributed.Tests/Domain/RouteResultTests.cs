using FluentAssertions;
using HotSwap.Distributed.Domain.Models;
using Xunit;

namespace HotSwap.Distributed.Tests.Domain;

public class RouteResultTests
{
    [Fact]
    public void CreateSuccess_WithConsumers_ShouldCreateSuccessfulResult()
    {
        var strategyName = "round-robin";
        var consumerIds = new List<string> { "consumer-1", "consumer-2", "consumer-3" };
        var reason = "Load balanced across 3 consumers";

        var result = RouteResult.CreateSuccess(strategyName, consumerIds, reason);

        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.StrategyName.Should().Be(strategyName);
        result.ConsumerIds.Should().BeEquivalentTo(consumerIds);
        result.Reason.Should().Be(reason);
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void CreateSuccess_WithMetadata_ShouldIncludeMetadata()
    {
        var strategyName = "priority-based";
        var consumerIds = new List<string> { "high-priority-consumer" };
        var metadata = new Dictionary<string, object>
        {
            { "priority_score", 100 },
            { "latency_ms", 50 }
        };

        var result = RouteResult.CreateSuccess(strategyName, consumerIds, metadata: metadata);

        result.Success.Should().BeTrue();
        result.Metadata.Should().NotBeNull();
        result.Metadata.Should().ContainKey("priority_score");
        result.Metadata!["priority_score"].Should().Be(100);
    }

    [Fact]
    public void CreateSuccess_WithEmptyConsumers_ShouldStillSucceed()
    {
        var strategyName = "broadcast";
        var consumerIds = new List<string>();

        var result = RouteResult.CreateSuccess(strategyName, consumerIds);

        result.Success.Should().BeTrue();
        result.ConsumerIds.Should().BeEmpty();
        result.HasConsumers().Should().BeFalse();
    }

    [Fact]
    public void CreateFailure_ShouldCreateFailedResult()
    {
        var strategyName = "sticky-session";
        var errorMessage = "No available consumers in the target group";

        var result = RouteResult.CreateFailure(strategyName, errorMessage);

        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.StrategyName.Should().Be(strategyName);
        result.ErrorMessage.Should().Be(errorMessage);
        result.ConsumerIds.Should().BeEmpty();
    }

    [Fact]
    public void HasConsumers_WithConsumers_ShouldReturnTrue()
    {
        var result = RouteResult.CreateSuccess("test", new List<string> { "consumer-1" });

        result.HasConsumers().Should().BeTrue();
    }

    [Fact]
    public void HasConsumers_WithEmptyList_ShouldReturnFalse()
    {
        var result = RouteResult.CreateSuccess("test", new List<string>());

        result.HasConsumers().Should().BeFalse();
    }

    [Fact]
    public void HasConsumers_WithNullList_ShouldReturnFalse()
    {
        var result = new RouteResult
        {
            Success = true,
            StrategyName = "test",
            ConsumerIds = null!
        };

        result.HasConsumers().Should().BeFalse();
    }

    [Fact]
    public void GetConsumerCount_WithMultipleConsumers_ShouldReturnCorrectCount()
    {
        var consumerIds = new List<string> { "c1", "c2", "c3", "c4", "c5" };
        var result = RouteResult.CreateSuccess("test", consumerIds);

        result.GetConsumerCount().Should().Be(5);
    }

    [Fact]
    public void GetConsumerCount_WithEmptyList_ShouldReturnZero()
    {
        var result = RouteResult.CreateSuccess("test", new List<string>());

        result.GetConsumerCount().Should().Be(0);
    }

    [Fact]
    public void GetConsumerCount_WithNullList_ShouldReturnZero()
    {
        var result = new RouteResult
        {
            Success = true,
            StrategyName = "test",
            ConsumerIds = null!
        };

        result.GetConsumerCount().Should().Be(0);
    }

    [Fact]
    public void RouteResult_CanSetAllProperties()
    {
        var result = new RouteResult
        {
            Success = true,
            StrategyName = "custom-strategy",
            ConsumerIds = new List<string> { "consumer-1" },
            Reason = "Custom routing logic",
            Metadata = new Dictionary<string, object> { { "key", "value" } },
            ErrorMessage = null
        };

        result.Success.Should().BeTrue();
        result.StrategyName.Should().Be("custom-strategy");
        result.ConsumerIds.Should().ContainSingle("consumer-1");
        result.Reason.Should().Be("Custom routing logic");
        result.Metadata.Should().ContainKey("key");
    }
}
