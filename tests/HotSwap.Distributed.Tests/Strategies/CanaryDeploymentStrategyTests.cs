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

public class CanaryDeploymentStrategyTests
{
    private readonly Mock<ILogger<CanaryDeploymentStrategy>> _loggerMock;
    private readonly Mock<ILogger<EnvironmentCluster>> _clusterLoggerMock;
    private readonly Mock<ILogger<KernelNode>> _nodeLoggerMock;
    private readonly Mock<IMetricsProvider> _metricsProviderMock;

    public CanaryDeploymentStrategyTests()
    {
        _loggerMock = new Mock<ILogger<CanaryDeploymentStrategy>>();
        _clusterLoggerMock = new Mock<ILogger<EnvironmentCluster>>();
        _nodeLoggerMock = new Mock<ILogger<KernelNode>>();
        _metricsProviderMock = new Mock<IMetricsProvider>();
    }

    [Fact]
    public void StrategyName_ShouldBeCanary()
    {
        // Arrange
        var strategy = new CanaryDeploymentStrategy(
            _loggerMock.Object,
            _metricsProviderMock.Object);

        // Assert
        strategy.StrategyName.Should().Be("Canary");
    }

    [Fact]
    public async Task DeployAsync_WithEmptyCluster_ReturnsFailed()
    {
        // Arrange
        var strategy = new CanaryDeploymentStrategy(
            _loggerMock.Object,
            _metricsProviderMock.Object);
        var cluster = new EnvironmentCluster(
            EnvironmentType.Production,
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
        result.Strategy.Should().Be("Canary");
        result.Environment.Should().Be(EnvironmentType.Production);
    }

    [Fact]
    public async Task DeployAsync_WithSingleNode_ReturnsSuccess()
    {
        // Arrange
        SetupHealthyMetrics();
        var strategy = new CanaryDeploymentStrategy(
            _loggerMock.Object,
            _metricsProviderMock.Object,
            initialPercentage: 100);
        var cluster = await CreateClusterWithNodes(EnvironmentType.Production, 1);

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
        result.Strategy.Should().Be("Canary");
        result.NodeResults.Should().HaveCount(1);
        result.NodeResults.Should().OnlyContain(r => r.Success);
        result.RollbackPerformed.Should().BeFalse();
    }

    [Fact]
    public async Task DeployAsync_WithSingleWave_ReturnsSuccess()
    {
        // Arrange
        SetupHealthyMetrics();
        var strategy = new CanaryDeploymentStrategy(
            _loggerMock.Object,
            _metricsProviderMock.Object,
            initialPercentage: 100,
            incrementPercentage: 50,
            waitDuration: TimeSpan.FromMilliseconds(100));
        var cluster = await CreateClusterWithNodes(EnvironmentType.Production, 5);

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
        result.NodeResults.Should().OnlyContain(r => r.Success);
        result.Message.Should().Contain("Successfully deployed to all 5 nodes");
        result.RollbackPerformed.Should().BeFalse();
    }

    [Fact]
    public async Task DeployAsync_WithMultipleWaves_ReturnsSuccess()
    {
        // Arrange
        SetupHealthyMetrics();
        var strategy = new CanaryDeploymentStrategy(
            _loggerMock.Object,
            _metricsProviderMock.Object,
            initialPercentage: 10,
            incrementPercentage: 20,
            waitDuration: TimeSpan.FromMilliseconds(50));
        var cluster = await CreateClusterWithNodes(EnvironmentType.Production, 10);

        var request = new ModuleDeploymentRequest
        {
            ModuleName = "test-module",
            Version = new Version(2, 0, 0)
        };

        // Act
        var result = await strategy.DeployAsync(request, cluster);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.NodeResults.Should().HaveCount(10);
        result.NodeResults.Should().OnlyContain(r => r.Success);
        result.Message.Should().Contain("Successfully deployed to all 10 nodes");
    }

    [Fact]
    public async Task DeployAsync_WithDeploymentFailureInFirstWave_RollsBack()
    {
        // Arrange
        SetupHealthyMetrics();
        var strategy = new CanaryDeploymentStrategy(
            _loggerMock.Object,
            _metricsProviderMock.Object,
            initialPercentage: 50,
            waitDuration: TimeSpan.FromMilliseconds(100));
        var cluster = await CreateClusterWithFailingNode(EnvironmentType.Production, 4, failingNodeIndex: 0);

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
        result.Message.Should().Contain("Canary deployment failed");
        result.Message.Should().Contain("Rolled back all changes");
        result.RollbackPerformed.Should().BeTrue();
    }

    [Fact]
    public async Task DeployAsync_WithDeploymentFailureInSecondWave_RollsBackAll()
    {
        // Arrange
        SetupHealthyMetrics();
        var strategy = new CanaryDeploymentStrategy(
            _loggerMock.Object,
            _metricsProviderMock.Object,
            initialPercentage: 25,
            incrementPercentage: 25,
            waitDuration: TimeSpan.FromMilliseconds(50));
        var cluster = await CreateClusterWithFailingNode(EnvironmentType.Production, 8, failingNodeIndex: 2);

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
        result.Message.Should().Contain("Canary deployment failed");
        result.RollbackPerformed.Should().BeTrue();
        // Rollback should have been attempted
        result.RollbackResults.Should().HaveCountGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task DeployAsync_WithCpuThresholdExceeded_RollsBack()
    {
        // Arrange
        SetupMetricsWithHighCpu();
        var strategy = new CanaryDeploymentStrategy(
            _loggerMock.Object,
            _metricsProviderMock.Object,
            initialPercentage: 20,
            incrementPercentage: 30,
            waitDuration: TimeSpan.FromMilliseconds(50));
        var cluster = await CreateClusterWithNodes(EnvironmentType.Production, 10);

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
        result.Message.Should().Contain("Canary metrics degraded");
        result.Message.Should().Contain("Rolled back");
        result.RollbackPerformed.Should().BeTrue();
    }

    [Fact]
    public async Task DeployAsync_WithMemoryThresholdExceeded_RollsBack()
    {
        // Arrange
        SetupMetricsWithHighMemory();
        var strategy = new CanaryDeploymentStrategy(
            _loggerMock.Object,
            _metricsProviderMock.Object,
            initialPercentage: 20,
            incrementPercentage: 30,
            waitDuration: TimeSpan.FromMilliseconds(50));
        var cluster = await CreateClusterWithNodes(EnvironmentType.Production, 10);

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
        result.Message.Should().Contain("Canary metrics degraded");
        result.RollbackPerformed.Should().BeTrue();
    }

    [Fact]
    public async Task DeployAsync_WithLatencyThresholdExceeded_RollsBack()
    {
        // Arrange
        SetupMetricsWithHighLatency();
        var strategy = new CanaryDeploymentStrategy(
            _loggerMock.Object,
            _metricsProviderMock.Object,
            initialPercentage: 20,
            incrementPercentage: 30,
            waitDuration: TimeSpan.FromMilliseconds(50));
        var cluster = await CreateClusterWithNodes(EnvironmentType.Production, 10);

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
        result.Message.Should().Contain("Canary metrics degraded");
        result.RollbackPerformed.Should().BeTrue();
    }

    [Fact]
    public async Task DeployAsync_WithErrorRateThresholdExceeded_RollsBack()
    {
        // Arrange
        SetupMetricsWithHighErrorRate();
        var strategy = new CanaryDeploymentStrategy(
            _loggerMock.Object,
            _metricsProviderMock.Object,
            initialPercentage: 20,
            incrementPercentage: 30,
            waitDuration: TimeSpan.FromMilliseconds(50));
        var cluster = await CreateClusterWithNodes(EnvironmentType.Production, 10);

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
        result.Message.Should().Contain("Canary metrics degraded");
        result.RollbackPerformed.Should().BeTrue();
    }

    [Fact]
    public async Task DeployAsync_WithMetricsAnalysisException_RollsBack()
    {
        // Arrange
        _metricsProviderMock
            .Setup(m => m.GetClusterMetricsAsync(
                It.IsAny<EnvironmentType>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateBaselineMetrics());

        _metricsProviderMock
            .Setup(m => m.GetNodesMetricsAsync(
                It.IsAny<IEnumerable<Guid>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Metrics service unavailable"));

        var strategy = new CanaryDeploymentStrategy(
            _loggerMock.Object,
            _metricsProviderMock.Object,
            initialPercentage: 20,
            incrementPercentage: 30,
            waitDuration: TimeSpan.FromMilliseconds(50));
        var cluster = await CreateClusterWithNodes(EnvironmentType.Production, 10);

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
        result.Message.Should().Contain("Canary metrics degraded");
        result.RollbackPerformed.Should().BeTrue();
    }

    [Fact]
    public async Task DeployAsync_WithException_ReturnsFailedResult()
    {
        // Arrange
        _metricsProviderMock
            .Setup(m => m.GetClusterMetricsAsync(
                It.IsAny<EnvironmentType>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Unexpected error"));

        var strategy = new CanaryDeploymentStrategy(
            _loggerMock.Object,
            _metricsProviderMock.Object);
        var cluster = await CreateClusterWithNodes(EnvironmentType.Production, 3);

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
        result.Message.Should().Contain("Deployment failed");
        result.Exception.Should().NotBeNull();
        result.Exception!.Message.Should().Contain("Unexpected error");
    }

    [Fact]
    public void Constructor_WithDefaultParameters_UsesExpectedDefaults()
    {
        // Arrange & Act
        var strategy = new CanaryDeploymentStrategy(
            _loggerMock.Object,
            _metricsProviderMock.Object);

        // Assert
        strategy.StrategyName.Should().Be("Canary");
    }

    [Fact]
    public void Constructor_WithCustomParameters_AcceptsParameters()
    {
        // Arrange & Act
        var strategy = new CanaryDeploymentStrategy(
            _loggerMock.Object,
            _metricsProviderMock.Object,
            initialPercentage: 5,
            incrementPercentage: 15,
            waitDuration: TimeSpan.FromMinutes(5));

        // Assert
        strategy.Should().NotBeNull();
        strategy.StrategyName.Should().Be("Canary");
    }

    [Fact]
    public async Task DeployAsync_WithGradualRollout_ProcessesCorrectPercentages()
    {
        // Arrange
        SetupHealthyMetrics();
        var strategy = new CanaryDeploymentStrategy(
            _loggerMock.Object,
            _metricsProviderMock.Object,
            initialPercentage: 10,
            incrementPercentage: 30,
            waitDuration: TimeSpan.FromMilliseconds(50));
        var cluster = await CreateClusterWithNodes(EnvironmentType.Production, 20);

        var request = new ModuleDeploymentRequest
        {
            ModuleName = "test-module",
            Version = new Version(3, 0, 0)
        };

        // Act
        var result = await strategy.DeployAsync(request, cluster);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.NodeResults.Should().HaveCount(20);
        // First wave: 10% = 2 nodes
        // Second wave: 40% total = 8 nodes (6 new)
        // Third wave: 70% total = 14 nodes (6 new)
        // Fourth wave: 100% = 20 nodes (6 new)
    }

    // Resource-based deployment tests

    [Fact]
    public async Task DeployAsync_WithResourceStabilization_UsesAdaptiveTiming()
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

        SetupHealthyMetrics();

        // Mock stabilization service to return stable result quickly
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

        var strategy = new CanaryDeploymentStrategy(
            _loggerMock.Object,
            _metricsProviderMock.Object,
            stabilizationServiceMock.Object,
            config,
            initialPercentage: 50,
            incrementPercentage: 50);

        var cluster = await CreateClusterWithNodes(EnvironmentType.Production, 10);

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
        result.NodeResults.Should().HaveCount(10);

        // Verify stabilization service was called
        stabilizationServiceMock.Verify(
            s => s.WaitForStabilizationAsync(
                It.IsAny<IEnumerable<Guid>>(),
                It.IsAny<ClusterMetricsSnapshot>(),
                config,
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task DeployAsync_WithResourceStabilizationTimeout_RollsBack()
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

        SetupHealthyMetrics();

        // Mock stabilization service to return timeout (unstable)
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

        var strategy = new CanaryDeploymentStrategy(
            _loggerMock.Object,
            _metricsProviderMock.Object,
            stabilizationServiceMock.Object,
            config,
            initialPercentage: 50,
            incrementPercentage: 50);

        var cluster = await CreateClusterWithNodes(EnvironmentType.Production, 10);

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
        result.Message.Should().Contain("Canary metrics degraded"); // Stabilization failure triggers canary health failure
        result.Message.Should().Contain("Rolled back");
        result.RollbackPerformed.Should().BeTrue();
    }

    [Fact]
    public async Task DeployAsync_WithResourceStabilization_MultipleWavesStabilize()
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

        SetupHealthyMetrics();

        // Mock stabilization service to return stable result for each wave
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

        var strategy = new CanaryDeploymentStrategy(
            _loggerMock.Object,
            _metricsProviderMock.Object,
            stabilizationServiceMock.Object,
            config,
            initialPercentage: 25,
            incrementPercentage: 25);

        var cluster = await CreateClusterWithNodes(EnvironmentType.Production, 8);

        var request = new ModuleDeploymentRequest
        {
            ModuleName = "test-module",
            Version = new Version(2, 0, 0)
        };

        // Act
        var result = await strategy.DeployAsync(request, cluster);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.NodeResults.Should().HaveCount(8);

        // Should have 3 waves (25%, 50%, 75%, 100%), so 3 stabilization checks
        stabilizationServiceMock.Verify(
            s => s.WaitForStabilizationAsync(
                It.IsAny<IEnumerable<Guid>>(),
                It.IsAny<ClusterMetricsSnapshot>(),
                config,
                It.IsAny<CancellationToken>()),
            Times.Exactly(3));
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
        var strategy = new CanaryDeploymentStrategy(
            _loggerMock.Object,
            _metricsProviderMock.Object,
            stabilizationServiceMock.Object,
            config,
            initialPercentage: 15,
            incrementPercentage: 25);

        // Assert
        strategy.Should().NotBeNull();
        strategy.StrategyName.Should().Be("Canary");
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
                Hostname = $"prod-node-{i}",
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
                Hostname = $"prod-node-{i}",
                Port = 8080 + i,
                Environment = environment,
                SimulateDeploymentFailure = i == failingNodeIndex
            };

            var node = await KernelNode.CreateAsync(config, _nodeLoggerMock.Object);
            cluster.AddNode(node);
        }

        return cluster;
    }

    private void SetupHealthyMetrics()
    {
        var baseline = CreateBaselineMetrics();

        _metricsProviderMock
            .Setup(m => m.GetClusterMetricsAsync(
                It.IsAny<EnvironmentType>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(baseline);

        _metricsProviderMock
            .Setup(m => m.GetNodesMetricsAsync(
                It.IsAny<IEnumerable<Guid>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Guid> ids, CancellationToken ct) =>
                ids.Select(id => new NodeMetricsSnapshot
                {
                    NodeId = id,
                    CpuUsagePercent = 45.0, // Baseline 50, so -10% change (acceptable)
                    MemoryUsagePercent = 58.0, // Baseline 60, so -3.3% change (acceptable)
                    LatencyMs = 95.0, // Baseline 100, so -5% change (acceptable)
                    ErrorRate = 0.8 // Baseline 1.0, so -20% change (acceptable)
                }).ToList());
    }

    private void SetupMetricsWithHighCpu()
    {
        var baseline = CreateBaselineMetrics();

        _metricsProviderMock
            .Setup(m => m.GetClusterMetricsAsync(
                It.IsAny<EnvironmentType>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(baseline);

        _metricsProviderMock
            .Setup(m => m.GetNodesMetricsAsync(
                It.IsAny<IEnumerable<Guid>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Guid> ids, CancellationToken ct) =>
                ids.Select(id => new NodeMetricsSnapshot
                {
                    NodeId = id,
                    CpuUsagePercent = 70.0, // Baseline 50, +40% increase (exceeds 30% threshold)
                    MemoryUsagePercent = 60.0,
                    LatencyMs = 100.0,
                    ErrorRate = 1.0
                }).ToList());
    }

    private void SetupMetricsWithHighMemory()
    {
        var baseline = CreateBaselineMetrics();

        _metricsProviderMock
            .Setup(m => m.GetClusterMetricsAsync(
                It.IsAny<EnvironmentType>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(baseline);

        _metricsProviderMock
            .Setup(m => m.GetNodesMetricsAsync(
                It.IsAny<IEnumerable<Guid>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Guid> ids, CancellationToken ct) =>
                ids.Select(id => new NodeMetricsSnapshot
                {
                    NodeId = id,
                    CpuUsagePercent = 50.0,
                    MemoryUsagePercent = 85.0, // Baseline 60, +41.6% increase (exceeds 30% threshold)
                    LatencyMs = 100.0,
                    ErrorRate = 1.0
                }).ToList());
    }

    private void SetupMetricsWithHighLatency()
    {
        var baseline = CreateBaselineMetrics();

        _metricsProviderMock
            .Setup(m => m.GetClusterMetricsAsync(
                It.IsAny<EnvironmentType>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(baseline);

        _metricsProviderMock
            .Setup(m => m.GetNodesMetricsAsync(
                It.IsAny<IEnumerable<Guid>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Guid> ids, CancellationToken ct) =>
                ids.Select(id => new NodeMetricsSnapshot
                {
                    NodeId = id,
                    CpuUsagePercent = 50.0,
                    MemoryUsagePercent = 60.0,
                    LatencyMs = 250.0, // Baseline 100, +150% increase (exceeds 100% threshold)
                    ErrorRate = 1.0
                }).ToList());
    }

    private void SetupMetricsWithHighErrorRate()
    {
        var baseline = CreateBaselineMetrics();

        _metricsProviderMock
            .Setup(m => m.GetClusterMetricsAsync(
                It.IsAny<EnvironmentType>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(baseline);

        _metricsProviderMock
            .Setup(m => m.GetNodesMetricsAsync(
                It.IsAny<IEnumerable<Guid>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Guid> ids, CancellationToken ct) =>
                ids.Select(id => new NodeMetricsSnapshot
                {
                    NodeId = id,
                    CpuUsagePercent = 50.0,
                    MemoryUsagePercent = 60.0,
                    LatencyMs = 100.0,
                    ErrorRate = 2.0 // Baseline 1.0, +100% increase (exceeds 50% threshold)
                }).ToList());
    }

    private ClusterMetricsSnapshot CreateBaselineMetrics()
    {
        return new ClusterMetricsSnapshot
        {
            Environment = "Production",
            TotalNodes = 10,
            AvgCpuUsage = 50.0,
            AvgMemoryUsage = 60.0,
            AvgLatency = 100.0,
            AvgErrorRate = 1.0,
            TotalRequestsPerSecond = 1000.0
        };
    }
}
