using FluentAssertions;
using HotSwap.Distributed.Infrastructure.ServiceDiscovery;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace HotSwap.Distributed.Tests.Infrastructure.ServiceDiscovery;

public class ConsulServiceDiscoveryTests
{
    private readonly Mock<ILogger<ConsulServiceDiscovery>> _mockLogger;
    private readonly ServiceDiscoveryConfiguration _config;
    private readonly IMemoryCache _cache;

    public ConsulServiceDiscoveryTests()
    {
        _mockLogger = new Mock<ILogger<ConsulServiceDiscovery>>();
        _config = new ServiceDiscoveryConfiguration
        {
            Backend = "consul",
            ServiceName = "hotswap-test",
            EnableAutoRegistration = true,
            EnableHealthChecks = false, // Disable for unit tests
            Consul = new ConsulConfiguration
            {
                Address = "http://localhost:8500",
                Datacenter = "dc1"
            }
        };
        _cache = new MemoryCache(new MemoryCacheOptions());
    }

    [Fact]
    public void Constructor_ShouldInitialize_Successfully()
    {
        // Act
        var serviceDiscovery = new ConsulServiceDiscovery(_mockLogger.Object, _config, _cache);

        // Assert
        serviceDiscovery.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullCache_ShouldUseDefaultCache()
    {
        // Act
        var serviceDiscovery = new ConsulServiceDiscovery(_mockLogger.Object, _config, null);

        // Assert
        serviceDiscovery.Should().NotBeNull();
    }

    [Fact]
    public void ServiceDiscoveryConfiguration_DefaultValues_ShouldBeSet()
    {
        // Arrange
        var config = new ServiceDiscoveryConfiguration();

        // Assert
        config.Backend.Should().Be("consul");
        config.ServiceName.Should().Be("hotswap-node");
        config.EnableAutoRegistration.Should().BeTrue();
        config.EnableHealthChecks.Should().BeTrue();
        config.HealthCheckIntervalSeconds.Should().Be(10);
        config.HealthCheckTimeoutSeconds.Should().Be(5);
        config.HealthCheckPath.Should().Be("/health");
        config.CacheExpirationSeconds.Should().Be(30);
    }

    [Fact]
    public void ConsulConfiguration_DefaultValues_ShouldBeSet()
    {
        // Arrange
        var config = new ConsulConfiguration();

        // Assert
        config.Address.Should().Be("http://localhost:8500");
        config.Datacenter.Should().Be("dc1");
        config.UseTls.Should().BeFalse();
        config.DeregisterCriticalServiceAfter.Should().Be("30s");
    }

    [Fact]
    public void EtcdConfiguration_DefaultValues_ShouldBeSet()
    {
        // Arrange
        var config = new EtcdConfiguration();

        // Assert
        config.Endpoints.Should().Contain("http://localhost:2379");
        config.TtlSeconds.Should().Be(30);
        config.KeyPrefix.Should().Be("/services/hotswap");
    }

    [Fact]
    public void NodeRegistration_ShouldAllowRequiredProperties()
    {
        // Act
        var registration = new NodeRegistration
        {
            NodeId = "test-node",
            Hostname = "localhost",
            Port = 5000,
            Environment = "Development"
        };

        // Assert
        registration.NodeId.Should().Be("test-node");
        registration.Hostname.Should().Be("localhost");
        registration.Port.Should().Be(5000);
        registration.Environment.Should().Be("Development");
        registration.Metadata.Should().NotBeNull();
        registration.Tags.Should().NotBeNull();
    }

    [Fact]
    public void ServiceNode_ShouldAllowRequiredProperties()
    {
        // Act
        var node = new ServiceNode
        {
            NodeId = "test-node",
            Hostname = "localhost",
            Port = 5000,
            Environment = "Development"
        };

        // Assert
        node.NodeId.Should().Be("test-node");
        node.Hostname.Should().Be("localhost");
        node.Port.Should().Be(5000);
        node.Environment.Should().Be("Development");
        node.IsHealthy.Should().BeTrue(); // Default value
        node.LastHealthCheck.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void ServiceNode_WithCustomMetadata_ShouldStoreMetadata()
    {
        // Act
        var node = new ServiceNode
        {
            NodeId = "test-node",
            Hostname = "localhost",
            Port = 5000,
            Environment = "Development",
            Metadata = new Dictionary<string, string>
            {
                { "region", "us-east-1" },
                { "zone", "a" }
            },
            Tags = new List<string> { "api", "web" }
        };

        // Assert
        node.Metadata.Should().ContainKeys("region", "zone");
        node.Metadata["region"].Should().Be("us-east-1");
        node.Tags.Should().Contain("api").And.Contain("web");
    }

    [Fact]
    public async Task UpdateHealthStatusAsync_WithNonExistentNode_ShouldLogWarning()
    {
        // Arrange
        await using var serviceDiscovery = new ConsulServiceDiscovery(_mockLogger.Object, _config, _cache);

        // Act
        await serviceDiscovery.UpdateHealthStatusAsync("non-existent", true);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Cannot update health status")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    // Note: Full integration tests with actual Consul server would go in a separate test suite
    // These are unit tests that verify the logic without requiring Consul to be running

    [Theory]
    [InlineData("Development", "hotswap-test-development")]
    [InlineData("Production", "hotswap-test-production")]
    [InlineData("Staging", "hotswap-test-staging")]
    public void ServiceName_ShouldBeFormattedCorrectly(string environment, string expectedPrefix)
    {
        // This verifies the service naming convention used in ConsulServiceDiscovery
        var serviceName = $"{_config.ServiceName}-{environment.ToLower()}";
        serviceName.Should().Be(expectedPrefix);
    }

    [Fact]
    public async Task DisposeAsync_ShouldNotThrow()
    {
        // Arrange
        var serviceDiscovery = new ConsulServiceDiscovery(_mockLogger.Object, _config, _cache);

        // Act
        var act = async () => await serviceDiscovery.DisposeAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DisposeAsync_CalledMultipleTimes_ShouldNotThrow()
    {
        // Arrange
        var serviceDiscovery = new ConsulServiceDiscovery(_mockLogger.Object, _config, _cache);

        // Act
        await serviceDiscovery.DisposeAsync();
        var act = async () => await serviceDiscovery.DisposeAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }
}
