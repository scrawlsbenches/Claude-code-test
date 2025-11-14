using FluentAssertions;
using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Orchestrator.Core;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace HotSwap.Distributed.Tests.Core;

public class KernelNodeTests
{
    private readonly Mock<ILogger<KernelNode>> _loggerMock;

    public KernelNodeTests()
    {
        _loggerMock = new Mock<ILogger<KernelNode>>();
    }

    [Fact]
    public async Task CreateAsync_ShouldInitializeNodeSuccessfully()
    {
        // Arrange
        var config = new NodeConfiguration
        {
            Hostname = "test-node",
            Port = 8080,
            Environment = EnvironmentType.Development
        };

        // Act
        var node = await KernelNode.CreateAsync(config, _loggerMock.Object);

        // Assert
        node.Should().NotBeNull();
        node.Hostname.Should().Be("test-node");
        node.Port.Should().Be(8080);
        node.Environment.Should().Be(EnvironmentType.Development);
        node.Status.Should().Be(NodeStatus.Running);
    }

    [Fact]
    public async Task DeployModuleAsync_ShouldReturnSuccessResult()
    {
        // Arrange
        var config = new NodeConfiguration
        {
            Hostname = "test-node",
            Port = 8080,
            Environment = EnvironmentType.Development
        };

        var node = await KernelNode.CreateAsync(config, _loggerMock.Object);

        var request = new ModuleDeploymentRequest
        {
            ModuleName = "test-module",
            Version = new Version(1, 0, 0)
        };

        // Act
        var result = await node.DeployModuleAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.NodeId.Should().Be(node.NodeId);
        result.Hostname.Should().Be("test-node");
        result.Duration.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task GetHealthAsync_ShouldReturnHealthStatus()
    {
        // Arrange
        var config = new NodeConfiguration
        {
            Hostname = "test-node",
            Port = 8080,
            Environment = EnvironmentType.Development
        };

        var node = await KernelNode.CreateAsync(config, _loggerMock.Object);

        // Act
        var health = await node.GetHealthAsync();

        // Assert
        health.Should().NotBeNull();
        health.NodeId.Should().Be(node.NodeId);
        health.Status.Should().Be(NodeStatus.Running);
    }

    [Fact]
    public async Task PingAsync_ShouldReturnTrue()
    {
        // Arrange
        var config = new NodeConfiguration
        {
            Hostname = "test-node",
            Port = 8080,
            Environment = EnvironmentType.Development
        };

        var node = await KernelNode.CreateAsync(config, _loggerMock.Object);

        // Act
        var result = await node.PingAsync(TimeSpan.FromSeconds(5));

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task RollbackModuleAsync_ShouldReturnSuccessResult()
    {
        // Arrange
        var config = new NodeConfiguration
        {
            Hostname = "test-node",
            Port = 8080,
            Environment = EnvironmentType.Development
        };

        var node = await KernelNode.CreateAsync(config, _loggerMock.Object);

        // Act
        var result = await node.RollbackModuleAsync("test-module");

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.NodeId.Should().Be(node.NodeId);
    }

    [Fact]
    public async Task DisposeAsync_ShouldSetStatusToStopped()
    {
        // Arrange
        var config = new NodeConfiguration
        {
            Hostname = "test-node",
            Port = 8080,
            Environment = EnvironmentType.Development
        };

        var node = await KernelNode.CreateAsync(config, _loggerMock.Object);

        // Act
        await node.DisposeAsync();

        // Assert
        node.Status.Should().Be(NodeStatus.Stopped);
    }
}
