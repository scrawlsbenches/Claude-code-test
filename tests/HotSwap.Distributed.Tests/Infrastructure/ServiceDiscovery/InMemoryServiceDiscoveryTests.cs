using FluentAssertions;
using HotSwap.Distributed.Infrastructure.ServiceDiscovery;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace HotSwap.Distributed.Tests.Infrastructure.ServiceDiscovery;

public class InMemoryServiceDiscoveryTests
{
    private readonly Mock<ILogger<InMemoryServiceDiscovery>> _mockLogger;
    private readonly InMemoryServiceDiscovery _serviceDiscovery;

    public InMemoryServiceDiscoveryTests()
    {
        _mockLogger = new Mock<ILogger<InMemoryServiceDiscovery>>();
        _serviceDiscovery = new InMemoryServiceDiscovery(_mockLogger.Object);
    }

    [Fact]
    public async Task RegisterNodeAsync_ShouldRegisterNode_Successfully()
    {
        // Arrange
        var registration = new NodeRegistration
        {
            NodeId = "node-1",
            Hostname = "localhost",
            Port = 5000,
            Environment = "Development",
            Metadata = new Dictionary<string, string> { { "region", "us-east-1" } },
            Tags = new List<string> { "api", "web" }
        };

        // Act
        await _serviceDiscovery.RegisterNodeAsync(registration);

        // Assert
        var node = await _serviceDiscovery.GetNodeAsync("node-1");
        node.Should().NotBeNull();
        node!.NodeId.Should().Be("node-1");
        node.Hostname.Should().Be("localhost");
        node.Port.Should().Be(5000);
        node.Environment.Should().Be("Development");
        node.IsHealthy.Should().BeTrue();
        node.Metadata.Should().ContainKey("region");
        node.Tags.Should().Contain("api");
    }

    [Fact]
    public async Task RegisterNodeAsync_WithMultipleNodes_ShouldRegisterAll()
    {
        // Arrange
        var registrations = new[]
        {
            new NodeRegistration { NodeId = "node-1", Hostname = "host1", Port = 5000, Environment = "Development" },
            new NodeRegistration { NodeId = "node-2", Hostname = "host2", Port = 5001, Environment = "Development" },
            new NodeRegistration { NodeId = "node-3", Hostname = "host3", Port = 5002, Environment = "Production" }
        };

        // Act
        foreach (var registration in registrations)
        {
            await _serviceDiscovery.RegisterNodeAsync(registration);
        }

        // Assert
        var allNodes = _serviceDiscovery.GetAllNodes();
        allNodes.Should().HaveCount(3);
    }

    [Fact]
    public async Task DeregisterNodeAsync_ShouldRemoveNode_Successfully()
    {
        // Arrange
        var registration = new NodeRegistration
        {
            NodeId = "node-1",
            Hostname = "localhost",
            Port = 5000,
            Environment = "Development"
        };
        await _serviceDiscovery.RegisterNodeAsync(registration);

        // Act
        await _serviceDiscovery.DeregisterNodeAsync("node-1");

        // Assert
        var node = await _serviceDiscovery.GetNodeAsync("node-1");
        node.Should().BeNull();
    }

    [Fact]
    public async Task DeregisterNodeAsync_WithNonExistentNode_ShouldNotThrow()
    {
        // Act
        var act = async () => await _serviceDiscovery.DeregisterNodeAsync("non-existent");

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DiscoverNodesAsync_ShouldReturnNodesForEnvironment()
    {
        // Arrange
        var registrations = new[]
        {
            new NodeRegistration { NodeId = "dev-1", Hostname = "dev-host1", Port = 5000, Environment = "Development" },
            new NodeRegistration { NodeId = "dev-2", Hostname = "dev-host2", Port = 5001, Environment = "Development" },
            new NodeRegistration { NodeId = "prod-1", Hostname = "prod-host1", Port = 5002, Environment = "Production" }
        };

        foreach (var registration in registrations)
        {
            await _serviceDiscovery.RegisterNodeAsync(registration);
        }

        // Act
        var devNodes = await _serviceDiscovery.DiscoverNodesAsync("Development");
        var prodNodes = await _serviceDiscovery.DiscoverNodesAsync("Production");

        // Assert
        devNodes.Should().HaveCount(2);
        devNodes.All(n => n.Environment == "Development").Should().BeTrue();
        prodNodes.Should().HaveCount(1);
        prodNodes.All(n => n.Environment == "Production").Should().BeTrue();
    }

    [Fact]
    public async Task DiscoverNodesAsync_WithCaseInsensitiveEnvironment_ShouldReturnNodes()
    {
        // Arrange
        var registration = new NodeRegistration
        {
            NodeId = "node-1",
            Hostname = "localhost",
            Port = 5000,
            Environment = "Development"
        };
        await _serviceDiscovery.RegisterNodeAsync(registration);

        // Act
        var nodes = await _serviceDiscovery.DiscoverNodesAsync("development");

        // Assert
        nodes.Should().HaveCount(1);
    }

    [Fact]
    public async Task DiscoverNodesAsync_WithNoMatchingEnvironment_ShouldReturnEmptyList()
    {
        // Arrange
        var registration = new NodeRegistration
        {
            NodeId = "node-1",
            Hostname = "localhost",
            Port = 5000,
            Environment = "Development"
        };
        await _serviceDiscovery.RegisterNodeAsync(registration);

        // Act
        var nodes = await _serviceDiscovery.DiscoverNodesAsync("Staging");

        // Assert
        nodes.Should().BeEmpty();
    }

    [Fact]
    public async Task GetNodeAsync_WithExistingNode_ShouldReturnNode()
    {
        // Arrange
        var registration = new NodeRegistration
        {
            NodeId = "node-1",
            Hostname = "localhost",
            Port = 5000,
            Environment = "Development"
        };
        await _serviceDiscovery.RegisterNodeAsync(registration);

        // Act
        var node = await _serviceDiscovery.GetNodeAsync("node-1");

        // Assert
        node.Should().NotBeNull();
        node!.NodeId.Should().Be("node-1");
    }

    [Fact]
    public async Task GetNodeAsync_WithNonExistentNode_ShouldReturnNull()
    {
        // Act
        var node = await _serviceDiscovery.GetNodeAsync("non-existent");

        // Assert
        node.Should().BeNull();
    }

    [Fact]
    public async Task UpdateHealthStatusAsync_ShouldUpdateHealthStatus()
    {
        // Arrange
        var registration = new NodeRegistration
        {
            NodeId = "node-1",
            Hostname = "localhost",
            Port = 5000,
            Environment = "Development"
        };
        await _serviceDiscovery.RegisterNodeAsync(registration);

        // Act
        await _serviceDiscovery.UpdateHealthStatusAsync("node-1", false);

        // Assert
        var node = await _serviceDiscovery.GetNodeAsync("node-1");
        node!.IsHealthy.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateHealthStatusAsync_WithHealthyToUnhealthy_ShouldReflectInDiscovery()
    {
        // Arrange
        var registration = new NodeRegistration
        {
            NodeId = "node-1",
            Hostname = "localhost",
            Port = 5000,
            Environment = "Development"
        };
        await _serviceDiscovery.RegisterNodeAsync(registration);

        // Act
        await _serviceDiscovery.UpdateHealthStatusAsync("node-1", false);
        var nodes = await _serviceDiscovery.DiscoverNodesAsync("Development");

        // Assert
        nodes.Should().HaveCount(1);
        nodes.First().IsHealthy.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateHealthStatusAsync_WithNonExistentNode_ShouldNotThrow()
    {
        // Act
        var act = async () => await _serviceDiscovery.UpdateHealthStatusAsync("non-existent", false);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task RegisterHealthCheckAsync_ShouldNotThrow()
    {
        // Arrange
        var registration = new NodeRegistration
        {
            NodeId = "node-1",
            Hostname = "localhost",
            Port = 5000,
            Environment = "Development"
        };
        await _serviceDiscovery.RegisterNodeAsync(registration);

        // Act
        var act = async () => await _serviceDiscovery.RegisterHealthCheckAsync(
            "node-1",
            "http://localhost:5000/health",
            10,
            5);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task GetHealthyNodesAsync_ShouldReturnOnlyHealthyNodes()
    {
        // Arrange
        var registrations = new[]
        {
            new NodeRegistration { NodeId = "healthy-1", Hostname = "host1", Port = 5000, Environment = "Development" },
            new NodeRegistration { NodeId = "healthy-2", Hostname = "host2", Port = 5001, Environment = "Development" },
            new NodeRegistration { NodeId = "unhealthy-1", Hostname = "host3", Port = 5002, Environment = "Development" }
        };

        foreach (var registration in registrations)
        {
            await _serviceDiscovery.RegisterNodeAsync(registration);
        }

        await _serviceDiscovery.UpdateHealthStatusAsync("unhealthy-1", false);

        // Act
        var healthyNodes = await _serviceDiscovery.GetHealthyNodesAsync("Development");

        // Assert
        healthyNodes.Should().HaveCount(2);
        healthyNodes.Should().AllSatisfy(n => n.IsHealthy.Should().BeTrue());
        healthyNodes.Should().NotContain(n => n.NodeId == "unhealthy-1");
    }

    [Fact]
    public async Task GetHealthyNodesAsync_WithAllUnhealthyNodes_ShouldReturnEmptyList()
    {
        // Arrange
        var registration = new NodeRegistration
        {
            NodeId = "node-1",
            Hostname = "localhost",
            Port = 5000,
            Environment = "Development"
        };
        await _serviceDiscovery.RegisterNodeAsync(registration);
        await _serviceDiscovery.UpdateHealthStatusAsync("node-1", false);

        // Act
        var healthyNodes = await _serviceDiscovery.GetHealthyNodesAsync("Development");

        // Assert
        healthyNodes.Should().BeEmpty();
    }

    [Fact]
    public async Task GetHealthyNodesAsync_WithNoNodes_ShouldReturnEmptyList()
    {
        // Act
        var healthyNodes = await _serviceDiscovery.GetHealthyNodesAsync("Development");

        // Assert
        healthyNodes.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllNodes_ShouldReturnAllRegisteredNodes()
    {
        // Arrange
        var registrations = new[]
        {
            new NodeRegistration { NodeId = "node-1", Hostname = "host1", Port = 5000, Environment = "Development" },
            new NodeRegistration { NodeId = "node-2", Hostname = "host2", Port = 5001, Environment = "Production" }
        };

        foreach (var registration in registrations)
        {
            await _serviceDiscovery.RegisterNodeAsync(registration);
        }

        // Act
        var allNodes = _serviceDiscovery.GetAllNodes();

        // Assert
        allNodes.Should().HaveCount(2);
    }

    [Fact]
    public async Task Clear_ShouldRemoveAllNodes()
    {
        // Arrange
        var registrations = new[]
        {
            new NodeRegistration { NodeId = "node-1", Hostname = "host1", Port = 5000, Environment = "Development" },
            new NodeRegistration { NodeId = "node-2", Hostname = "host2", Port = 5001, Environment = "Production" }
        };

        foreach (var registration in registrations)
        {
            await _serviceDiscovery.RegisterNodeAsync(registration);
        }

        // Act
        _serviceDiscovery.Clear();

        // Assert
        var allNodes = _serviceDiscovery.GetAllNodes();
        allNodes.Should().BeEmpty();
    }

    [Fact]
    public async Task LastHealthCheck_ShouldBeUpdated_OnDiscovery()
    {
        // Arrange
        var registration = new NodeRegistration
        {
            NodeId = "node-1",
            Hostname = "localhost",
            Port = 5000,
            Environment = "Development"
        };
        await _serviceDiscovery.RegisterNodeAsync(registration);
        await Task.Delay(10); // Small delay to ensure timestamp difference

        // Act
        var nodes = await _serviceDiscovery.DiscoverNodesAsync("Development");

        // Assert
        var node = nodes.First();
        node.LastHealthCheck.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task Metadata_ShouldBeCopied_NotShared()
    {
        // Arrange
        var metadata = new Dictionary<string, string> { { "key", "value" } };
        var registration = new NodeRegistration
        {
            NodeId = "node-1",
            Hostname = "localhost",
            Port = 5000,
            Environment = "Development",
            Metadata = metadata
        };
        await _serviceDiscovery.RegisterNodeAsync(registration);

        // Act
        metadata["key"] = "modified";
        var node = await _serviceDiscovery.GetNodeAsync("node-1");

        // Assert
        node!.Metadata["key"].Should().Be("value"); // Should not be modified
    }

    [Fact]
    public async Task Tags_ShouldBeCopied_NotShared()
    {
        // Arrange
        var tags = new List<string> { "tag1", "tag2" };
        var registration = new NodeRegistration
        {
            NodeId = "node-1",
            Hostname = "localhost",
            Port = 5000,
            Environment = "Development",
            Tags = tags
        };
        await _serviceDiscovery.RegisterNodeAsync(registration);

        // Act
        tags.Add("tag3");
        var node = await _serviceDiscovery.GetNodeAsync("node-1");

        // Assert
        node!.Tags.Should().HaveCount(2); // Should not include "tag3"
    }
}
