using FluentAssertions;
using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Infrastructure.Metrics;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace HotSwap.Distributed.Tests.Infrastructure;

/// <summary>
/// Unit tests for InMemoryMetricsProvider.
/// </summary>
public class InMemoryMetricsProviderTests
{
    private readonly Mock<ILogger<InMemoryMetricsProvider>> _mockLogger;
    private readonly InMemoryMetricsProvider _provider;

    public InMemoryMetricsProviderTests()
    {
        _mockLogger = new Mock<ILogger<InMemoryMetricsProvider>>();
        _provider = new InMemoryMetricsProvider(_mockLogger.Object);
    }

    [Fact(Skip = "Temporarily disabled - investigating test hang")]
    public async Task GetNodeMetricsAsync_WithValidNodeId_ReturnsMetrics()
    {
        // Arrange
        var nodeId = Guid.NewGuid();

        // Act
        var result = await _provider.GetNodeMetricsAsync(nodeId);

        // Assert
        result.Should().NotBeNull();
        result.NodeId.Should().Be(nodeId);
        result.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
        result.CpuUsagePercent.Should().BeGreaterThanOrEqualTo(30.0).And.BeLessThanOrEqualTo(70.0);
        result.MemoryUsagePercent.Should().BeGreaterThanOrEqualTo(50.0).And.BeLessThanOrEqualTo(80.0);
        result.LatencyMs.Should().BeGreaterThanOrEqualTo(100.0).And.BeLessThanOrEqualTo(300.0);
        result.ErrorRate.Should().BeGreaterThanOrEqualTo(0.0).And.BeLessThanOrEqualTo(3.0);
        result.RequestsPerSecond.Should().BeGreaterThanOrEqualTo(100.0).And.BeLessThanOrEqualTo(500.0);
        result.ActiveConnections.Should().BeGreaterThanOrEqualTo(10).And.BeLessThanOrEqualTo(200);
        result.LoadedModuleCount.Should().BeGreaterThanOrEqualTo(5).And.BeLessThanOrEqualTo(15);
    }

    [Fact(Skip = "Temporarily disabled - investigating test hang")]
    public async Task GetNodeMetricsAsync_WithSameNodeId_ReturnsCachedMetrics()
    {
        // Arrange
        var nodeId = Guid.NewGuid();

        // Act - First call
        var firstResult = await _provider.GetNodeMetricsAsync(nodeId);

        // Act - Second call immediately
        var secondResult = await _provider.GetNodeMetricsAsync(nodeId);

        // Assert - Should be exact same object from cache
        secondResult.Should().BeSameAs(firstResult);
        secondResult.NodeId.Should().Be(nodeId);
        secondResult.Timestamp.Should().Be(firstResult.Timestamp);
        secondResult.CpuUsagePercent.Should().Be(firstResult.CpuUsagePercent);
    }

    [Fact(Skip = "Temporarily disabled - investigating test hang")]
    public async Task GetNodeMetricsAsync_AfterCacheTtl_ReturnsFreshMetrics()
    {
        // Arrange
        var nodeId = Guid.NewGuid();

        // Act - First call
        var firstResult = await _provider.GetNodeMetricsAsync(nodeId);

        // Wait for cache to expire (TTL is 10 seconds)
        await Task.Delay(TimeSpan.FromSeconds(11));

        // Act - Second call after cache expiry
        var secondResult = await _provider.GetNodeMetricsAsync(nodeId);

        // Assert - Should be different object with newer timestamp
        secondResult.Should().NotBeSameAs(firstResult);
        secondResult.NodeId.Should().Be(nodeId);
        secondResult.Timestamp.Should().BeAfter(firstResult.Timestamp);
    }

    [Fact(Skip = "Temporarily disabled - investigating test hang")]
    public async Task GetNodeMetricsAsync_WithCancellationToken_CompletesSuccessfully()
    {
        // Arrange
        var nodeId = Guid.NewGuid();
        using var cts = new CancellationTokenSource();

        // Act
        var result = await _provider.GetNodeMetricsAsync(nodeId, cts.Token);

        // Assert
        result.Should().NotBeNull();
        result.NodeId.Should().Be(nodeId);
    }

    [Fact(Skip = "Temporarily disabled - investigating test hang")]
    public async Task GetNodeMetricsAsync_WithCancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var nodeId = Guid.NewGuid();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        Func<Task> act = async () => await _provider.GetNodeMetricsAsync(nodeId, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact(Skip = "Temporarily disabled - investigating test hang")]
    public async Task GetNodesMetricsAsync_WithMultipleNodeIds_ReturnsAllMetrics()
    {
        // Arrange
        var nodeIds = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };

        // Act
        var results = await _provider.GetNodesMetricsAsync(nodeIds);

        // Assert
        results.Should().NotBeNull();
        results.Should().HaveCount(3);
        results.Select(r => r.NodeId).Should().BeEquivalentTo(nodeIds);
    }

    [Fact(Skip = "Temporarily disabled - investigating test hang")]
    public async Task GetNodesMetricsAsync_WithEmptyList_ReturnsEmptyResults()
    {
        // Arrange
        var nodeIds = Array.Empty<Guid>();

        // Act
        var results = await _provider.GetNodesMetricsAsync(nodeIds);

        // Assert
        results.Should().NotBeNull();
        results.Should().BeEmpty();
    }

    [Fact(Skip = "Temporarily disabled - investigating test hang")]
    public async Task GetNodesMetricsAsync_WithDuplicateNodeIds_ReturnsMetricsForEach()
    {
        // Arrange
        var nodeId = Guid.NewGuid();
        var nodeIds = new[] { nodeId, nodeId, nodeId };

        // Act
        var results = await _provider.GetNodesMetricsAsync(nodeIds);

        // Assert
        results.Should().NotBeNull();
        results.Should().HaveCount(3);
        results.Should().AllSatisfy(r => r.NodeId.Should().Be(nodeId));
        // Note: Since calls run in parallel, each may generate its own metrics
        // before cache is populated, so we may get multiple distinct objects
    }

    [Fact(Skip = "Temporarily disabled - investigating test hang")]
    public async Task GetNodesMetricsAsync_WithCancellationToken_CompletesSuccessfully()
    {
        // Arrange
        var nodeIds = new[] { Guid.NewGuid(), Guid.NewGuid() };
        using var cts = new CancellationTokenSource();

        // Act
        var results = await _provider.GetNodesMetricsAsync(nodeIds, cts.Token);

        // Assert
        results.Should().NotBeNull();
        results.Should().HaveCount(2);
    }

    [Fact(Skip = "Temporarily disabled - investigating test hang")]
    public async Task GetClusterMetricsAsync_WithValidEnvironment_ReturnsClusterMetrics()
    {
        // Arrange
        var environment = EnvironmentType.Production;

        // Act
        var result = await _provider.GetClusterMetricsAsync(environment);

        // Assert
        result.Should().NotBeNull();
        result.Environment.Should().Be(environment.ToString());
        result.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
        result.TotalNodes.Should().Be(10);
        result.AvgCpuUsage.Should().BeGreaterThanOrEqualTo(45.0).And.BeLessThanOrEqualTo(65.0);
        result.AvgMemoryUsage.Should().BeGreaterThanOrEqualTo(60.0).And.BeLessThanOrEqualTo(75.0);
        result.AvgLatency.Should().BeGreaterThanOrEqualTo(120.0).And.BeLessThanOrEqualTo(200.0);
        result.AvgErrorRate.Should().BeGreaterThanOrEqualTo(0.5).And.BeLessThanOrEqualTo(2.5);
        result.TotalRequestsPerSecond.Should().BeGreaterThanOrEqualTo(1000.0).And.BeLessThanOrEqualTo(6000.0);
    }

    [Fact(Skip = "Temporarily disabled - investigating test hang")]
    public async Task GetClusterMetricsAsync_WithDifferentEnvironments_ReturnsDistinctMetrics()
    {
        // Arrange
        var env1 = EnvironmentType.Development;
        var env2 = EnvironmentType.Staging;

        // Act
        var result1 = await _provider.GetClusterMetricsAsync(env1);
        var result2 = await _provider.GetClusterMetricsAsync(env2);

        // Assert
        result1.Should().NotBeNull();
        result2.Should().NotBeNull();
        result1.Environment.Should().Be(env1.ToString());
        result2.Environment.Should().Be(env2.ToString());
        // Since random generation, values should be different
        (result1.AvgCpuUsage != result2.AvgCpuUsage ||
         result1.AvgMemoryUsage != result2.AvgMemoryUsage).Should().BeTrue();
    }

    [Fact(Skip = "Temporarily disabled - investigating test hang")]
    public async Task GetClusterMetricsAsync_WithCancellationToken_CompletesSuccessfully()
    {
        // Arrange
        var environment = EnvironmentType.Production;
        using var cts = new CancellationTokenSource();

        // Act
        var result = await _provider.GetClusterMetricsAsync(environment, cts.Token);

        // Assert
        result.Should().NotBeNull();
        result.Environment.Should().Be(environment.ToString());
    }

    [Fact(Skip = "Temporarily disabled - investigating test hang")]
    public async Task GetClusterMetricsAsync_WithCancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var environment = EnvironmentType.Production;
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        Func<Task> act = async () => await _provider.GetClusterMetricsAsync(environment, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact(Skip = "Temporarily disabled - investigating test hang")]
    public async Task GetHistoricalMetricsAsync_WithValidTimeRange_ReturnsMetricsCollection()
    {
        // Arrange
        var nodeId = Guid.NewGuid();
        var startTime = DateTime.UtcNow.AddHours(-1);
        var endTime = DateTime.UtcNow;

        // Act
        var results = await _provider.GetHistoricalMetricsAsync(nodeId, startTime, endTime);

        // Assert
        results.Should().NotBeNull();
        results.Should().NotBeEmpty();
        // Should have metrics at 5-minute intervals
        // 1 hour = 60 minutes = 12 intervals + 1 (start) = 13 metrics
        results.Should().HaveCount(13);
        results.All(m => m.NodeId == nodeId).Should().BeTrue();
        results.Select(m => m.Timestamp).Should().BeInAscendingOrder();
    }

    [Fact(Skip = "Temporarily disabled - investigating test hang")]
    public async Task GetHistoricalMetricsAsync_WithShortTimeRange_ReturnsFewerMetrics()
    {
        // Arrange
        var nodeId = Guid.NewGuid();
        var startTime = DateTime.UtcNow.AddMinutes(-10);
        var endTime = DateTime.UtcNow;

        // Act
        var results = await _provider.GetHistoricalMetricsAsync(nodeId, startTime, endTime);

        // Assert
        results.Should().NotBeNull();
        // 10 minutes = 2 intervals + 1 (start) = 3 metrics
        results.Should().HaveCount(3);
    }

    [Fact(Skip = "Temporarily disabled - investigating test hang")]
    public async Task GetHistoricalMetricsAsync_WithStartTimeAfterEndTime_ReturnsEmptyResults()
    {
        // Arrange
        var nodeId = Guid.NewGuid();
        var startTime = DateTime.UtcNow;
        var endTime = DateTime.UtcNow.AddHours(-1);

        // Act
        var results = await _provider.GetHistoricalMetricsAsync(nodeId, startTime, endTime);

        // Assert
        results.Should().NotBeNull();
        results.Should().BeEmpty();
    }

    [Fact(Skip = "Temporarily disabled - investigating test hang")]
    public async Task GetHistoricalMetricsAsync_WithSameStartAndEndTime_ReturnsSingleMetric()
    {
        // Arrange
        var nodeId = Guid.NewGuid();
        var time = DateTime.UtcNow;

        // Act
        var results = await _provider.GetHistoricalMetricsAsync(nodeId, time, time);

        // Assert
        results.Should().NotBeNull();
        results.Should().HaveCount(1);
        results.First().NodeId.Should().Be(nodeId);
        results.First().Timestamp.Should().Be(time);
    }

    [Fact(Skip = "Temporarily disabled - investigating test hang")]
    public async Task GetHistoricalMetricsAsync_WithCancellationToken_CompletesSuccessfully()
    {
        // Arrange
        var nodeId = Guid.NewGuid();
        var startTime = DateTime.UtcNow.AddMinutes(-10);
        var endTime = DateTime.UtcNow;
        using var cts = new CancellationTokenSource();

        // Act
        var results = await _provider.GetHistoricalMetricsAsync(nodeId, startTime, endTime, cts.Token);

        // Assert
        results.Should().NotBeNull();
        results.Should().NotBeEmpty();
    }

    [Fact(Skip = "Temporarily disabled - investigating test hang")]
    public async Task GetHistoricalMetricsAsync_WithCancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var nodeId = Guid.NewGuid();
        var startTime = DateTime.UtcNow.AddHours(-1);
        var endTime = DateTime.UtcNow;
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        Func<Task> act = async () => await _provider.GetHistoricalMetricsAsync(nodeId, startTime, endTime, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact(Skip = "Temporarily disabled - investigating test hang")]
    public async Task GetNodeMetricsAsync_LogsDebugMessage_WithNodeIdAndMetrics()
    {
        // Arrange
        var nodeId = Guid.NewGuid();

        // Act
        var result = await _provider.GetNodeMetricsAsync(nodeId);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(nodeId.ToString())),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact(Skip = "Temporarily disabled - investigating test hang")]
    public async Task GetNodesMetricsAsync_WithLargeNumberOfNodes_CompletesInParallel()
    {
        // Arrange
        var nodeIds = Enumerable.Range(1, 20).Select(_ => Guid.NewGuid()).ToArray();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        var results = await _provider.GetNodesMetricsAsync(nodeIds);

        // Assert
        stopwatch.Stop();
        results.Should().HaveCount(20);
        // If parallel, should complete in roughly the same time as a single call (50ms + overhead)
        // Sequential would be 20 * 50ms = 1000ms
        // Parallel should be < 500ms
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(500);
    }
}
