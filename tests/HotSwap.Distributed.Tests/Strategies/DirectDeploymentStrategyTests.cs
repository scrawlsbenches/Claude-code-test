using FluentAssertions;
using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Orchestrator.Core;
using HotSwap.Distributed.Orchestrator.Strategies;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace HotSwap.Distributed.Tests.Strategies;

public class DirectDeploymentStrategyTests
{
    private readonly Mock<ILogger<DirectDeploymentStrategy>> _loggerMock;
    private readonly Mock<ILogger<EnvironmentCluster>> _clusterLoggerMock;
    private readonly Mock<ILogger<KernelNode>> _nodeLoggerMock;
    private readonly DirectDeploymentStrategy _strategy;

    public DirectDeploymentStrategyTests()
    {
        _loggerMock = new Mock<ILogger<DirectDeploymentStrategy>>();
        _clusterLoggerMock = new Mock<ILogger<EnvironmentCluster>>();
        _nodeLoggerMock = new Mock<ILogger<KernelNode>>();
        _strategy = new DirectDeploymentStrategy(_loggerMock.Object);
    }

    [Fact]
    public async Task DeployAsync_WithHealthyCluster_ReturnsSuccess()
    {
        // Arrange
        var cluster = new EnvironmentCluster(
            EnvironmentType.Development,
            _clusterLoggerMock.Object);

        // Add test nodes
        for (int i = 0; i < 3; i++)
        {
            var config = new NodeConfiguration
            {
                Hostname = $"test-node-{i}",
                Port = 8080 + i,
                Environment = EnvironmentType.Development
            };

            var node = await KernelNode.CreateAsync(
                config,
                _nodeLoggerMock.Object);

            cluster.AddNode(node);
        }

        var request = new ModuleDeploymentRequest
        {
            ModuleName = "test-module",
            Version = new Version(1, 0, 0)
        };

        // Act
        var result = await _strategy.DeployAsync(request, cluster);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Strategy.Should().Be("Direct");
        result.Environment.Should().Be(EnvironmentType.Development);
        result.NodeResults.Should().HaveCount(3);
        result.NodeResults.Should().OnlyContain(r => r.Success);
    }

    [Fact]
    public async Task DeployAsync_WithEmptyCluster_ReturnsFailed()
    {
        // Arrange
        var cluster = new EnvironmentCluster(
            EnvironmentType.Development,
            _clusterLoggerMock.Object);

        var request = new ModuleDeploymentRequest
        {
            ModuleName = "test-module",
            Version = new Version(1, 0, 0)
        };

        // Act
        var result = await _strategy.DeployAsync(request, cluster);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("No nodes available");
    }

    [Fact]
    public void StrategyName_ShouldBeDirect()
    {
        // Assert
        _strategy.StrategyName.Should().Be("Direct");
    }
}
