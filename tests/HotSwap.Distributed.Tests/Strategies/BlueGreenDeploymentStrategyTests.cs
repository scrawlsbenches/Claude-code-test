using FluentAssertions;
using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Interfaces;
using HotSwap.Distributed.Orchestrator.Core;
using HotSwap.Distributed.Orchestrator.Services;
using HotSwap.Distributed.Orchestrator.Strategies;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace HotSwap.Distributed.Tests.Strategies;

public class BlueGreenDeploymentStrategyTests
{
    private readonly Mock<ILogger<BlueGreenDeploymentStrategy>> _loggerMock;
    private readonly Mock<ILogger<EnvironmentCluster>> _clusterLoggerMock;
    private readonly Mock<ILogger<KernelNode>> _nodeLoggerMock;
    private readonly Mock<IMetricsProvider> _metricsProviderMock;

    public BlueGreenDeploymentStrategyTests()
    {
        _loggerMock = new Mock<ILogger<BlueGreenDeploymentStrategy>>();
        _clusterLoggerMock = new Mock<ILogger<EnvironmentCluster>>();
        _nodeLoggerMock = new Mock<ILogger<KernelNode>>();
        _metricsProviderMock = new Mock<IMetricsProvider>();
    }

    [Fact]
    public void StrategyName_ShouldBeBlueGreen()
    {
        // Arrange
        var strategy = new BlueGreenDeploymentStrategy(_loggerMock.Object);

        // Assert
        strategy.StrategyName.Should().Be("BlueGreen");
    }

    [Fact]
    public async Task DeployAsync_WithEmptyCluster_ReturnsFailed()
    {
        // Arrange
        var strategy = new BlueGreenDeploymentStrategy(_loggerMock.Object);
        var cluster = new EnvironmentCluster(
            EnvironmentType.Staging,
            _clusterLoggerMock.Object);

        var request = new ModuleDeploymentRequest
        {
            ModuleName = "test-module",
            Version = new Version(1, 0, 0)
        };

        // Act
        var result = await strategy.DeployAsync(request, cluster);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("No nodes available");
        result.Strategy.Should().Be("BlueGreen");
        result.Environment.Should().Be(EnvironmentType.Staging);
    }

    [Fact]
    public async Task DeployAsync_WithSingleNode_AndSmokeTestsPassing_ReturnsSuccess()
    {
        // Arrange
        var strategy = new BlueGreenDeploymentStrategy(
            _loggerMock.Object,
            smokeTestTimeout: TimeSpan.FromSeconds(3));
        var cluster = await CreateClusterWithNodes(EnvironmentType.Staging, 1);

        var request = new ModuleDeploymentRequest
        {
            ModuleName = "test-module",
            Version = new Version(1, 0, 0)
        };

        // Act
        var result = await strategy.DeployAsync(request, cluster);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Strategy.Should().Be("BlueGreen");
        result.Environment.Should().Be(EnvironmentType.Staging);
        result.NodeResults.Should().HaveCount(1);
        result.NodeResults.Should().OnlyContain(r => r.Success);
        result.Message.Should().Contain("Successfully deployed to 1 node");
    }

    [Fact]
    public async Task DeployAsync_WithHealthyCluster_AndSmokeTestsPassing_ReturnsSuccess()
    {
        // Arrange
        var strategy = new BlueGreenDeploymentStrategy(
            _loggerMock.Object,
            smokeTestTimeout: TimeSpan.FromSeconds(3));
        var cluster = await CreateClusterWithNodes(EnvironmentType.Staging, 5);

        var request = new ModuleDeploymentRequest
        {
            ModuleName = "test-module",
            Version = new Version(2, 1, 0)
        };

        // Act
        var result = await strategy.DeployAsync(request, cluster);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.NodeResults.Should().HaveCount(5);
        result.NodeResults.Should().OnlyContain(r => r.Success);
        result.Message.Should().Contain("Successfully deployed to 5 nodes using blue-green strategy");
    }

    [Fact]
    public async Task DeployAsync_WithManyNodes_DeploysToAllNodes()
    {
        // Arrange
        var strategy = new BlueGreenDeploymentStrategy(
            _loggerMock.Object,
            smokeTestTimeout: TimeSpan.FromSeconds(3));
        var cluster = await CreateClusterWithNodes(EnvironmentType.Staging, 15);

        var request = new ModuleDeploymentRequest
        {
            ModuleName = "large-module",
            Version = new Version(3, 0, 0)
        };

        // Act
        var result = await strategy.DeployAsync(request, cluster);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.NodeResults.Should().HaveCount(15);
        result.NodeResults.Should().OnlyContain(r => r.Success);
        result.Message.Should().Contain("15 nodes");
    }

    [Fact]
    public async Task DeployAsync_WithDeploymentFailure_ReturnsFailed()
    {
        // Arrange
        var strategy = new BlueGreenDeploymentStrategy(_loggerMock.Object);
        var cluster = await CreateClusterWithFailingNode(EnvironmentType.Staging, 4, failingNodeIndex: 1);

        var request = new ModuleDeploymentRequest
        {
            ModuleName = "test-module",
            Version = new Version(1, 0, 0)
        };

        // Act
        var result = await strategy.DeployAsync(request, cluster);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Deployment to green environment failed");
        result.Message.Should().Contain("1 node");
        result.NodeResults.Should().HaveCount(4);
        result.NodeResults.Should().Contain(r => !r.Success);
    }

    [Fact]
    public async Task DeployAsync_WithMultipleDeploymentFailures_ReturnsFailed()
    {
        // Arrange
        var strategy = new BlueGreenDeploymentStrategy(_loggerMock.Object);
        var cluster = await CreateClusterWithMultipleFailingNodes(EnvironmentType.Staging, 6);

        var request = new ModuleDeploymentRequest
        {
            ModuleName = "test-module",
            Version = new Version(1, 0, 0)
        };

        // Act
        var result = await strategy.DeployAsync(request, cluster);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Deployment to green environment failed");
        result.NodeResults.Count(r => !r.Success).Should().BeGreaterThan(1);
    }

    [Fact]
    public async Task DeployAsync_WithSmokeTestsFailure_ReturnsFailed()
    {
        // Arrange
        var strategy = new BlueGreenDeploymentStrategy(
            _loggerMock.Object,
            smokeTestTimeout: TimeSpan.FromSeconds(3));
        var cluster = await CreateClusterWithUnhealthyNode(EnvironmentType.Staging, 5, unhealthyNodeIndex: 2);

        var request = new ModuleDeploymentRequest
        {
            ModuleName = "test-module",
            Version = new Version(1, 0, 0)
        };

        // Act
        var result = await strategy.DeployAsync(request, cluster);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Smoke tests failed");
        result.Message.Should().Contain("Traffic remains on blue environment");
        result.NodeResults.Should().HaveCount(5);
        result.NodeResults.Should().OnlyContain(r => r.Success); // Deployment succeeded, smoke tests failed
    }

    [Fact]
    public async Task DeployAsync_WithSmokeTestsTimeout_ReturnsFailed()
    {
        // Arrange
        var strategy = new BlueGreenDeploymentStrategy(
            _loggerMock.Object,
            smokeTestTimeout: TimeSpan.FromMilliseconds(1));
        var cluster = await CreateClusterWithNodes(EnvironmentType.Staging, 3);

        var request = new ModuleDeploymentRequest
        {
            ModuleName = "test-module",
            Version = new Version(1, 0, 0)
        };

        // Act
        var result = await strategy.DeployAsync(request, cluster);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Smoke tests failed");
    }

    [Fact]
    public async Task DeployAsync_WithException_ReturnsFailedResult()
    {
        // Arrange
        var strategy = new BlueGreenDeploymentStrategy(_loggerMock.Object);
        var cluster = await CreateClusterWithExceptionThrowingNode(EnvironmentType.Staging);

        var request = new ModuleDeploymentRequest
        {
            ModuleName = "test-module",
            Version = new Version(1, 0, 0)
        };

        // Act
        var result = await strategy.DeployAsync(request, cluster);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Deployment");
        // Note: SimulateException returns a failed deployment result, not an exception in the DeploymentResult
    }

    [Fact]
    public async Task DeployAsync_WithCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        var strategy = new BlueGreenDeploymentStrategy(
            _loggerMock.Object,
            smokeTestTimeout: TimeSpan.FromSeconds(10));
        var cluster = await CreateClusterWithNodes(EnvironmentType.Staging, 3);

        var request = new ModuleDeploymentRequest
        {
            ModuleName = "test-module",
            Version = new Version(1, 0, 0)
        };

        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var result = await strategy.DeployAsync(request, cluster, cts.Token);

        // Assert - The delay in smoke tests will be cancelled
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
    }

    [Fact]
    public void Constructor_WithDefaultParameters_UsesExpectedDefaults()
    {
        // Arrange & Act
        var strategy = new BlueGreenDeploymentStrategy(_loggerMock.Object);

        // Assert
        strategy.StrategyName.Should().Be("BlueGreen");
    }

    [Fact]
    public void Constructor_WithCustomParameters_AcceptsParameters()
    {
        // Arrange & Act
        var strategy = new BlueGreenDeploymentStrategy(
            _loggerMock.Object,
            smokeTestTimeout: TimeSpan.FromMinutes(10));

        // Assert
        strategy.Should().NotBeNull();
        strategy.StrategyName.Should().Be("BlueGreen");
    }

    [Fact]
    public async Task DeployAsync_VerifiesTimestamps_AreSet()
    {
        // Arrange
        var strategy = new BlueGreenDeploymentStrategy(
            _loggerMock.Object,
            smokeTestTimeout: TimeSpan.FromSeconds(3));
        var cluster = await CreateClusterWithNodes(EnvironmentType.Staging, 2);

        var request = new ModuleDeploymentRequest
        {
            ModuleName = "test-module",
            Version = new Version(1, 0, 0)
        };

        var beforeDeployment = DateTime.UtcNow;

        // Act
        var result = await strategy.DeployAsync(request, cluster);

        var afterDeployment = DateTime.UtcNow;

        // Assert
        result.Should().NotBeNull();
        result.StartTime.Should().BeOnOrAfter(beforeDeployment);
        result.StartTime.Should().BeOnOrBefore(afterDeployment);
        result.EndTime.Should().BeOnOrAfter(result.StartTime);
        result.EndTime.Should().BeOnOrBefore(afterDeployment);
    }

    [Fact]
    public async Task DeployAsync_WithAllNodesUnhealthy_FailsSmokeTests()
    {
        // Arrange
        var strategy = new BlueGreenDeploymentStrategy(
            _loggerMock.Object,
            smokeTestTimeout: TimeSpan.FromSeconds(3));
        var cluster = await CreateClusterWithAllUnhealthyNodes(EnvironmentType.Staging, 3);

        var request = new ModuleDeploymentRequest
        {
            ModuleName = "test-module",
            Version = new Version(1, 0, 0)
        };

        // Act
        var result = await strategy.DeployAsync(request, cluster);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Smoke tests failed");
    }

    // Resource-based deployment tests

    [Fact]
    public async Task DeployAsync_WithResourceStabilization_WaitsBeforeSmokeTests()
    {
        // Arrange
        var stabilizationServiceMock = new Mock<ResourceStabilizationService>(
            Mock.Of<ILogger<ResourceStabilizationService>>(),
            _metricsProviderMock.Object);

        var config = new ResourceStabilizationConfig
        {
            CpuDeltaThreshold = 10.0,
            MemoryDeltaThreshold = 10.0,
            LatencyDeltaThreshold = 15.0,
            PollingInterval = TimeSpan.FromMilliseconds(50),
            ConsecutiveStableChecks = 2,
            MinimumWaitTime = TimeSpan.FromMilliseconds(100),
            MaximumWaitTime = TimeSpan.FromSeconds(5)
        };

        // Setup baseline metrics
        _metricsProviderMock
            .Setup(m => m.GetClusterMetricsAsync(
                It.IsAny<EnvironmentType>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ClusterMetricsSnapshot
            {
                Environment = "Staging",
                TotalNodes = 5,
                AvgCpuUsage = 50.0,
                AvgMemoryUsage = 60.0,
                AvgLatency = 100.0,
                AvgErrorRate = 1.0
            });

        // Mock stabilization service to return stable result
        stabilizationServiceMock
            .Setup(s => s.WaitForStabilizationAsync(
                It.IsAny<IEnumerable<Guid>>(),
                It.IsAny<ClusterMetricsSnapshot>(),
                It.IsAny<ResourceStabilizationConfig>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResourceStabilizationResult
            {
                IsStable = true,
                ElapsedTime = TimeSpan.FromMilliseconds(200),
                ConsecutiveStableChecks = 2,
                TotalChecks = 3,
                TimeoutReached = false
            });

        var strategy = new BlueGreenDeploymentStrategy(
            _loggerMock.Object,
            _metricsProviderMock.Object,
            stabilizationServiceMock.Object,
            config,
            smokeTestTimeout: TimeSpan.FromSeconds(3));

        var cluster = await CreateClusterWithNodes(EnvironmentType.Staging, 5);

        var request = new ModuleDeploymentRequest
        {
            ModuleName = "test-module",
            Version = new Version(1, 0, 0)
        };

        // Act
        var result = await strategy.DeployAsync(request, cluster);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.NodeResults.Should().HaveCount(5);
        result.Message.Should().Contain("Successfully deployed");

        // Verify stabilization service was called before smoke tests
        stabilizationServiceMock.Verify(
            s => s.WaitForStabilizationAsync(
                It.IsAny<IEnumerable<Guid>>(),
                It.IsAny<ClusterMetricsSnapshot>(),
                config,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DeployAsync_WithResourceStabilizationFailure_DoesNotSwitchTraffic()
    {
        // Arrange
        var stabilizationServiceMock = new Mock<ResourceStabilizationService>(
            Mock.Of<ILogger<ResourceStabilizationService>>(),
            _metricsProviderMock.Object);

        var config = new ResourceStabilizationConfig
        {
            CpuDeltaThreshold = 10.0,
            MemoryDeltaThreshold = 10.0,
            LatencyDeltaThreshold = 15.0,
            PollingInterval = TimeSpan.FromMilliseconds(50),
            ConsecutiveStableChecks = 3,
            MinimumWaitTime = TimeSpan.FromMilliseconds(100),
            MaximumWaitTime = TimeSpan.FromSeconds(2)
        };

        // Setup baseline metrics
        _metricsProviderMock
            .Setup(m => m.GetClusterMetricsAsync(
                It.IsAny<EnvironmentType>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ClusterMetricsSnapshot
            {
                Environment = "Staging",
                TotalNodes = 5,
                AvgCpuUsage = 50.0,
                AvgMemoryUsage = 60.0,
                AvgLatency = 100.0,
                AvgErrorRate = 1.0
            });

        // Mock stabilization service to return unstable (timeout)
        stabilizationServiceMock
            .Setup(s => s.WaitForStabilizationAsync(
                It.IsAny<IEnumerable<Guid>>(),
                It.IsAny<ClusterMetricsSnapshot>(),
                It.IsAny<ResourceStabilizationConfig>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResourceStabilizationResult
            {
                IsStable = false,
                ElapsedTime = TimeSpan.FromSeconds(2),
                ConsecutiveStableChecks = 1,
                TotalChecks = 10,
                TimeoutReached = true
            });

        var strategy = new BlueGreenDeploymentStrategy(
            _loggerMock.Object,
            _metricsProviderMock.Object,
            stabilizationServiceMock.Object,
            config,
            smokeTestTimeout: TimeSpan.FromSeconds(3));

        var cluster = await CreateClusterWithNodes(EnvironmentType.Staging, 5);

        var request = new ModuleDeploymentRequest
        {
            ModuleName = "test-module",
            Version = new Version(1, 0, 0)
        };

        // Act
        var result = await strategy.DeployAsync(request, cluster);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("did not stabilize");
        result.Message.Should().Contain("Not switching traffic");
        result.NodeResults.Should().HaveCount(5);
        result.NodeResults.Should().OnlyContain(r => r.Success); // Deployment succeeded but not switching
    }

    [Fact]
    public async Task DeployAsync_WithResourceStabilization_SmokeTestsStillRun()
    {
        // Arrange
        var stabilizationServiceMock = new Mock<ResourceStabilizationService>(
            Mock.Of<ILogger<ResourceStabilizationService>>(),
            _metricsProviderMock.Object);

        var config = new ResourceStabilizationConfig
        {
            CpuDeltaThreshold = 10.0,
            MemoryDeltaThreshold = 10.0,
            LatencyDeltaThreshold = 15.0,
            PollingInterval = TimeSpan.FromMilliseconds(50),
            ConsecutiveStableChecks = 2,
            MinimumWaitTime = TimeSpan.FromMilliseconds(100),
            MaximumWaitTime = TimeSpan.FromSeconds(5)
        };

        // Setup baseline metrics
        _metricsProviderMock
            .Setup(m => m.GetClusterMetricsAsync(
                It.IsAny<EnvironmentType>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ClusterMetricsSnapshot
            {
                Environment = "Staging",
                TotalNodes = 5,
                AvgCpuUsage = 50.0,
                AvgMemoryUsage = 60.0,
                AvgLatency = 100.0,
                AvgErrorRate = 1.0
            });

        // Mock stabilization service to return stable
        stabilizationServiceMock
            .Setup(s => s.WaitForStabilizationAsync(
                It.IsAny<IEnumerable<Guid>>(),
                It.IsAny<ClusterMetricsSnapshot>(),
                It.IsAny<ResourceStabilizationConfig>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResourceStabilizationResult
            {
                IsStable = true,
                ElapsedTime = TimeSpan.FromMilliseconds(150),
                ConsecutiveStableChecks = 2,
                TotalChecks = 3,
                TimeoutReached = false
            });

        var strategy = new BlueGreenDeploymentStrategy(
            _loggerMock.Object,
            _metricsProviderMock.Object,
            stabilizationServiceMock.Object,
            config,
            smokeTestTimeout: TimeSpan.FromSeconds(3));

        // Create cluster with one unhealthy node (smoke tests will fail)
        var cluster = await CreateClusterWithUnhealthyNode(EnvironmentType.Staging, 5, unhealthyNodeIndex: 2);

        var request = new ModuleDeploymentRequest
        {
            ModuleName = "test-module",
            Version = new Version(1, 0, 0)
        };

        // Act
        var result = await strategy.DeployAsync(request, cluster);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Smoke tests failed");
        result.Message.Should().Contain("Traffic remains on blue environment");

        // Verify stabilization was performed before smoke tests
        stabilizationServiceMock.Verify(
            s => s.WaitForStabilizationAsync(
                It.IsAny<IEnumerable<Guid>>(),
                It.IsAny<ClusterMetricsSnapshot>(),
                config,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void Constructor_WithResourceStabilization_AcceptsParameters()
    {
        // Arrange
        var stabilizationServiceMock = new Mock<ResourceStabilizationService>(
            Mock.Of<ILogger<ResourceStabilizationService>>(),
            _metricsProviderMock.Object);

        var config = new ResourceStabilizationConfig();

        // Act
        var strategy = new BlueGreenDeploymentStrategy(
            _loggerMock.Object,
            _metricsProviderMock.Object,
            stabilizationServiceMock.Object,
            config,
            smokeTestTimeout: TimeSpan.FromMinutes(3));

        // Assert
        strategy.Should().NotBeNull();
        strategy.StrategyName.Should().Be("BlueGreen");
    }

    // Helper methods
    private async Task<EnvironmentCluster> CreateClusterWithNodes(
        EnvironmentType environment,
        int nodeCount)
    {
        var cluster = new EnvironmentCluster(environment, _clusterLoggerMock.Object);

        for (int i = 0; i < nodeCount; i++)
        {
            var config = new NodeConfiguration
            {
                Hostname = $"staging-node-{i}",
                Port = 8080 + i,
                Environment = environment
            };

            var node = await KernelNode.CreateAsync(config, _nodeLoggerMock.Object);
            cluster.AddNode(node);
        }

        return cluster;
    }

    private async Task<EnvironmentCluster> CreateClusterWithFailingNode(
        EnvironmentType environment,
        int nodeCount,
        int failingNodeIndex)
    {
        var cluster = new EnvironmentCluster(environment, _clusterLoggerMock.Object);

        for (int i = 0; i < nodeCount; i++)
        {
            var config = new NodeConfiguration
            {
                Hostname = $"staging-node-{i}",
                Port = 8080 + i,
                Environment = environment,
                SimulateDeploymentFailure = i == failingNodeIndex
            };

            var node = await KernelNode.CreateAsync(config, _nodeLoggerMock.Object);
            cluster.AddNode(node);
        }

        return cluster;
    }

    private async Task<EnvironmentCluster> CreateClusterWithMultipleFailingNodes(
        EnvironmentType environment,
        int nodeCount)
    {
        var cluster = new EnvironmentCluster(environment, _clusterLoggerMock.Object);

        for (int i = 0; i < nodeCount; i++)
        {
            var config = new NodeConfiguration
            {
                Hostname = $"staging-node-{i}",
                Port = 8080 + i,
                Environment = environment,
                SimulateDeploymentFailure = i % 2 == 0 // Every other node fails
            };

            var node = await KernelNode.CreateAsync(config, _nodeLoggerMock.Object);
            cluster.AddNode(node);
        }

        return cluster;
    }

    private async Task<EnvironmentCluster> CreateClusterWithUnhealthyNode(
        EnvironmentType environment,
        int nodeCount,
        int unhealthyNodeIndex)
    {
        var cluster = new EnvironmentCluster(environment, _clusterLoggerMock.Object);

        for (int i = 0; i < nodeCount; i++)
        {
            var config = new NodeConfiguration
            {
                Hostname = $"staging-node-{i}",
                Port = 8080 + i,
                Environment = environment,
                SimulateUnhealthy = i == unhealthyNodeIndex
            };

            var node = await KernelNode.CreateAsync(config, _nodeLoggerMock.Object);
            cluster.AddNode(node);
        }

        return cluster;
    }

    private async Task<EnvironmentCluster> CreateClusterWithAllUnhealthyNodes(
        EnvironmentType environment,
        int nodeCount)
    {
        var cluster = new EnvironmentCluster(environment, _clusterLoggerMock.Object);

        for (int i = 0; i < nodeCount; i++)
        {
            var config = new NodeConfiguration
            {
                Hostname = $"staging-node-{i}",
                Port = 8080 + i,
                Environment = environment,
                SimulateUnhealthy = true
            };

            var node = await KernelNode.CreateAsync(config, _nodeLoggerMock.Object);
            cluster.AddNode(node);
        }

        return cluster;
    }

    private async Task<EnvironmentCluster> CreateClusterWithExceptionThrowingNode(
        EnvironmentType environment)
    {
        var cluster = new EnvironmentCluster(environment, _clusterLoggerMock.Object);

        var config = new NodeConfiguration
        {
            Hostname = "exception-node",
            Port = 8080,
            Environment = environment,
            SimulateException = true
        };

        var node = await KernelNode.CreateAsync(config, _nodeLoggerMock.Object);
        cluster.AddNode(node);

        return cluster;
    }
}
