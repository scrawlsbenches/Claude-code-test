using FluentAssertions;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Interfaces;
using HotSwap.Distributed.Orchestrator.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace HotSwap.Distributed.Tests.Services;

public class ResourceStabilizationServiceTests
{
    private readonly Mock<ILogger<ResourceStabilizationService>> _loggerMock;
    private readonly Mock<IMetricsProvider> _metricsProviderMock;

    public ResourceStabilizationServiceTests()
    {
        _loggerMock = new Mock<ILogger<ResourceStabilizationService>>();
        _metricsProviderMock = new Mock<IMetricsProvider>();
    }

    [Fact]
    public async Task WaitForStabilizationAsync_WithStableMetricsImmediately_ReturnsQuickly()
    {
        // Arrange
        var config = new ResourceStabilizationConfig
        {
            CpuDeltaThreshold = 10.0,
            MemoryDeltaThreshold = 10.0,
            LatencyDeltaThreshold = 15.0,
            PollingInterval = TimeSpan.FromMilliseconds(100),
            ConsecutiveStableChecks = 3,
            MinimumWaitTime = TimeSpan.FromMilliseconds(500),
            MaximumWaitTime = TimeSpan.FromSeconds(30)
        };

        var baseline = new ClusterMetricsSnapshot
        {
            Environment = "Production",
            TotalNodes = 2,
            AvgCpuUsage = 50.0,
            AvgMemoryUsage = 60.0,
            AvgLatency = 100.0,
            AvgErrorRate = 1.0
        };

        var nodeIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };

        // Setup stable metrics (within 10% of baseline)
        _metricsProviderMock
            .Setup(m => m.GetNodesMetricsAsync(
                It.IsAny<IEnumerable<Guid>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(nodeIds.Select(id => new NodeMetricsSnapshot
            {
                NodeId = id,
                CpuUsagePercent = 52.0,    // +4% from baseline (within 10% threshold)
                MemoryUsagePercent = 62.0,  // +3.3% from baseline (within 10% threshold)
                LatencyMs = 105.0,          // +5% from baseline (within 15% threshold)
                ErrorRate = 1.0
            }).ToList());

        var service = new ResourceStabilizationService(_loggerMock.Object, _metricsProviderMock.Object);

        // Act
        var startTime = DateTime.UtcNow;
        var result = await service.WaitForStabilizationAsync(
            nodeIds,
            baseline,
            config,
            CancellationToken.None);
        var elapsed = DateTime.UtcNow - startTime;

        // Assert
        result.IsStable.Should().BeTrue();
        result.ElapsedTime.Should().BeGreaterThanOrEqualTo(config.MinimumWaitTime); // Enforces minimum wait
        result.ElapsedTime.Should().BeLessThan(TimeSpan.FromSeconds(5)); // But much faster than 15 min fixed wait
        result.ConsecutiveStableChecks.Should().BeGreaterThanOrEqualTo(3); // At least 3, may be more due to minimum wait
    }

    [Fact]
    public async Task WaitForStabilizationAsync_WithUnstableMetrics_ReturnsAfterMaxTimeout()
    {
        // Arrange
        var config = new ResourceStabilizationConfig
        {
            CpuDeltaThreshold = 10.0,
            PollingInterval = TimeSpan.FromMilliseconds(100),
            ConsecutiveStableChecks = 3,
            MinimumWaitTime = TimeSpan.FromMilliseconds(200),
            MaximumWaitTime = TimeSpan.FromSeconds(1) // Short timeout for test
        };

        var baseline = new ClusterMetricsSnapshot
        {
            Environment = "Production",
            TotalNodes = 1,
            AvgCpuUsage = 50.0,
            AvgMemoryUsage = 60.0,
            AvgLatency = 100.0,
            AvgErrorRate = 1.0
        };

        var nodeIds = new List<Guid> { Guid.NewGuid() };

        // Setup unstable metrics (CPU keeps fluctuating above threshold)
        var callCount = 0;
        _metricsProviderMock
            .Setup(m => m.GetNodesMetricsAsync(
                It.IsAny<IEnumerable<Guid>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return new List<NodeMetricsSnapshot>
                {
                    new()
                    {
                        NodeId = nodeIds[0],
                        CpuUsagePercent = 50.0 + (callCount % 2 == 0 ? 15.0 : 5.0), // Fluctuates: unstable
                        MemoryUsagePercent = 60.0,
                        LatencyMs = 100.0
                    }
                };
            });

        var service = new ResourceStabilizationService(_loggerMock.Object, _metricsProviderMock.Object);

        // Act
        var result = await service.WaitForStabilizationAsync(
            nodeIds,
            baseline,
            config,
            CancellationToken.None);

        // Assert
        result.IsStable.Should().BeFalse();
        result.ElapsedTime.Should().BeGreaterThanOrEqualTo(config.MaximumWaitTime);
        result.TimeoutReached.Should().BeTrue();
    }

    [Fact]
    public async Task WaitForStabilizationAsync_WithGradualStabilization_WaitsForConsecutiveChecks()
    {
        // Arrange
        var config = new ResourceStabilizationConfig
        {
            CpuDeltaThreshold = 10.0,
            PollingInterval = TimeSpan.FromMilliseconds(50),
            ConsecutiveStableChecks = 3,
            MinimumWaitTime = TimeSpan.FromMilliseconds(100),
            MaximumWaitTime = TimeSpan.FromSeconds(10)
        };

        var baseline = new ClusterMetricsSnapshot
        {
            Environment = "Production",
            TotalNodes = 1,
            AvgCpuUsage = 50.0,
            AvgMemoryUsage = 60.0,
            AvgLatency = 100.0,
            AvgErrorRate = 1.0
        };

        var nodeIds = new List<Guid> { Guid.NewGuid() };

        // Metrics stabilize after 5 checks: 70 -> 65 -> 60 -> 52 -> 51 -> 51 (stable)
        var callCount = 0;
        var cpuValues = new[] { 70.0, 65.0, 60.0, 52.0, 51.0, 51.0, 51.0 };

        _metricsProviderMock
            .Setup(m => m.GetNodesMetricsAsync(
                It.IsAny<IEnumerable<Guid>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                var cpu = cpuValues[Math.Min(callCount, cpuValues.Length - 1)];
                callCount++;
                return new List<NodeMetricsSnapshot>
                {
                    new()
                    {
                        NodeId = nodeIds[0],
                        CpuUsagePercent = cpu,
                        MemoryUsagePercent = 60.0,
                        LatencyMs = 100.0
                    }
                };
            });

        var service = new ResourceStabilizationService(_loggerMock.Object, _metricsProviderMock.Object);

        // Act
        var result = await service.WaitForStabilizationAsync(
            nodeIds,
            baseline,
            config,
            CancellationToken.None);

        // Assert
        result.IsStable.Should().BeTrue();
        result.ConsecutiveStableChecks.Should().Be(3);
        callCount.Should().BeGreaterThanOrEqualTo(6); // At least 6 checks: 3 unstable + 3 stable
    }

    [Fact]
    public async Task WaitForStabilizationAsync_WithMemorySpike_ResetsConsecutiveCount()
    {
        // Arrange
        var config = new ResourceStabilizationConfig
        {
            MemoryDeltaThreshold = 10.0,
            PollingInterval = TimeSpan.FromMilliseconds(50),
            ConsecutiveStableChecks = 3,
            MinimumWaitTime = TimeSpan.FromMilliseconds(100),
            MaximumWaitTime = TimeSpan.FromSeconds(5)
        };

        var baseline = new ClusterMetricsSnapshot
        {
            Environment = "Production",
            TotalNodes = 1,
            AvgCpuUsage = 50.0,
            AvgMemoryUsage = 60.0,
            AvgLatency = 100.0,
            AvgErrorRate = 1.0
        };

        var nodeIds = new List<Guid> { Guid.NewGuid() };

        // Metrics: stable -> stable -> SPIKE -> stable -> stable -> stable
        var callCount = 0;
        var memoryValues = new[] { 62.0, 61.0, 75.0, 61.0, 60.5, 60.5 }; // Spike at index 2

        _metricsProviderMock
            .Setup(m => m.GetNodesMetricsAsync(
                It.IsAny<IEnumerable<Guid>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                var memory = memoryValues[Math.Min(callCount, memoryValues.Length - 1)];
                callCount++;
                return new List<NodeMetricsSnapshot>
                {
                    new()
                    {
                        NodeId = nodeIds[0],
                        CpuUsagePercent = 50.0,
                        MemoryUsagePercent = memory,
                        LatencyMs = 100.0
                    }
                };
            });

        var service = new ResourceStabilizationService(_loggerMock.Object, _metricsProviderMock.Object);

        // Act
        var result = await service.WaitForStabilizationAsync(
            nodeIds,
            baseline,
            config,
            CancellationToken.None);

        // Assert
        result.IsStable.Should().BeTrue();
        result.ConsecutiveStableChecks.Should().Be(3);
        callCount.Should().BeGreaterThanOrEqualTo(6); // Spike resets counter, needs more checks
    }

    [Fact]
    public async Task WaitForStabilizationAsync_WithCancellationToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var config = new ResourceStabilizationConfig
        {
            PollingInterval = TimeSpan.FromMilliseconds(100),
            ConsecutiveStableChecks = 3,
            MinimumWaitTime = TimeSpan.FromMilliseconds(200),
            MaximumWaitTime = TimeSpan.FromSeconds(60)
        };

        var baseline = new ClusterMetricsSnapshot { Environment = "Production", TotalNodes = 1, AvgCpuUsage = 50.0, AvgMemoryUsage = 60.0, AvgLatency = 100.0, AvgErrorRate = 1.0 };
        var nodeIds = new List<Guid> { Guid.NewGuid() };

        _metricsProviderMock
            .Setup(m => m.GetNodesMetricsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<NodeMetricsSnapshot>
            {
                new() { NodeId = nodeIds[0], CpuUsagePercent = 70.0 }
            });

        var service = new ResourceStabilizationService(_loggerMock.Object, _metricsProviderMock.Object);

        var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(300));

        // Act
        Func<Task> act = async () => await service.WaitForStabilizationAsync(
            nodeIds,
            baseline,
            config,
            cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task WaitForStabilizationAsync_EnforcesMinimumWaitTime()
    {
        // Arrange
        var config = new ResourceStabilizationConfig
        {
            CpuDeltaThreshold = 10.0,
            PollingInterval = TimeSpan.FromMilliseconds(10),  // Very fast polling
            ConsecutiveStableChecks = 2,                       // Only 2 checks needed
            MinimumWaitTime = TimeSpan.FromSeconds(1),         // But minimum is 1 second
            MaximumWaitTime = TimeSpan.FromSeconds(30)
        };

        var baseline = new ClusterMetricsSnapshot { Environment = "Production", TotalNodes = 1, AvgCpuUsage = 50.0, AvgMemoryUsage = 60.0, AvgLatency = 100.0, AvgErrorRate = 1.0 };
        var nodeIds = new List<Guid> { Guid.NewGuid() };

        // Stable immediately
        _metricsProviderMock
            .Setup(m => m.GetNodesMetricsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<NodeMetricsSnapshot>
            {
                new() { NodeId = nodeIds[0], CpuUsagePercent = 51.0, MemoryUsagePercent = 61.0, LatencyMs = 101.0 }
            });

        var service = new ResourceStabilizationService(_loggerMock.Object, _metricsProviderMock.Object);

        // Act
        var startTime = DateTime.UtcNow;
        var result = await service.WaitForStabilizationAsync(nodeIds, baseline, config, CancellationToken.None);
        var elapsed = DateTime.UtcNow - startTime;

        // Assert
        result.IsStable.Should().BeTrue();
        elapsed.Should().BeGreaterThanOrEqualTo(config.MinimumWaitTime);
    }
}
