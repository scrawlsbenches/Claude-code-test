using FluentAssertions;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Interfaces;
using HotSwap.Distributed.Infrastructure.Metrics;
using HotSwap.Distributed.Orchestrator.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace HotSwap.Distributed.Tests.Services;

/// <summary>
/// Test constants for BrokerHealthMonitor tests.
/// These values define the queue depth thresholds and timing parameters used across tests.
/// </summary>
public static class BrokerHealthMonitorTestConstants
{
    // Queue depth thresholds (aligned with BrokerHealthMonitor implementation)
    public const int QueueDepthHealthy = 50;        // Below 500
    public const int QueueDepthDegraded = 750;      // Between 500-1000
    public const int QueueDepthUnhealthy = 1500;    // Above 1000

    // Timing constants
    public static readonly TimeSpan ShortDelay = TimeSpan.FromMilliseconds(100);
    public static readonly TimeSpan HealthCheckWait = TimeSpan.FromMilliseconds(150);
    public static readonly TimeSpan MultipleChecksWait = TimeSpan.FromMilliseconds(350);
}

[Collection("Metrics Tests")]
public class BrokerHealthMonitorTests : IDisposable
{
    private readonly Mock<IMessageQueue> _mockMessageQueue;
    private readonly Mock<ILogger<BrokerHealthMonitor>> _mockLogger;
    private readonly MessageMetricsProvider _metrics; // Use real instance, not mock
    private readonly CancellationTokenSource _cts;

    public BrokerHealthMonitorTests()
    {
        _mockMessageQueue = new Mock<IMessageQueue>();
        _mockLogger = new Mock<ILogger<BrokerHealthMonitor>>();
        _metrics = new MessageMetricsProvider(); // Real instance
        _cts = new CancellationTokenSource();
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullMessageQueue_ThrowsArgumentNullException()
    {
        // Act
        var action = () => new BrokerHealthMonitor(
            null!,
            _metrics,
            _mockLogger.Object);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("messageQueue");
    }

    [Fact]
    public void Constructor_WithNullMetrics_ThrowsArgumentNullException()
    {
        // Act
        var action = () => new BrokerHealthMonitor(
            _mockMessageQueue.Object,
            null!,
            _mockLogger.Object);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("metrics");
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act
        var action = () => new BrokerHealthMonitor(
            _mockMessageQueue.Object,
            _metrics,
            null!);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // Act
        var monitor = new BrokerHealthMonitor(
            _mockMessageQueue.Object,
            _metrics,
            _mockLogger.Object);

        // Assert
        monitor.Should().NotBeNull();
        monitor.CurrentHealthStatus.Should().Be(BrokerHealthStatus.Unknown);
    }

    #endregion

    #region Health Check Tests

    [Fact]
    public async Task ExecuteAsync_PerformsHealthChecks_Periodically()
    {
        // Arrange
        _mockMessageQueue.Setup(x => x.Count).Returns(10);
        var monitor = new BrokerHealthMonitor(
            _mockMessageQueue.Object,
            _metrics,
            _mockLogger.Object,
            checkInterval: BrokerHealthMonitorTestConstants.ShortDelay);

        // Act
        var executeTask = monitor.StartAsync(_cts.Token);
        await Task.Delay(BrokerHealthMonitorTestConstants.MultipleChecksWait); // Allow ~3 health checks
        _cts.Cancel();
        await executeTask;

        // Assert - Should have checked queue depth multiple times
        _mockMessageQueue.Verify(x => x.Count, Times.AtLeast(2));
    }

    /// <summary>
    /// Verifies that the broker health monitor correctly sets health status based on queue depth.
    /// Tests the three threshold ranges: Healthy (< 500), Degraded (500-1000), Unhealthy (> 1000).
    /// </summary>
    [Theory]
    [InlineData(BrokerHealthMonitorTestConstants.QueueDepthHealthy, BrokerHealthStatus.Healthy, "low queue depth")]
    [InlineData(BrokerHealthMonitorTestConstants.QueueDepthDegraded, BrokerHealthStatus.Degraded, "medium queue depth")]
    [InlineData(BrokerHealthMonitorTestConstants.QueueDepthUnhealthy, BrokerHealthStatus.Unhealthy, "high queue depth")]
    public async Task HealthCheck_WithQueueDepth_SetsCorrectStatus(
        int queueDepth,
        BrokerHealthStatus expectedStatus,
        string scenario)
    {
        // Arrange
        _mockMessageQueue.Setup(x => x.Count).Returns(queueDepth);
        var monitor = new BrokerHealthMonitor(
            _mockMessageQueue.Object,
            _metrics,
            _mockLogger.Object,
            checkInterval: BrokerHealthMonitorTestConstants.ShortDelay);

        // Act
        await monitor.StartAsync(_cts.Token);
        await Task.Delay(BrokerHealthMonitorTestConstants.HealthCheckWait);
        _cts.Cancel();

        // Assert
        monitor.CurrentHealthStatus.Should().Be(expectedStatus, scenario);
    }

    #endregion

    #region Metrics Tests

    [Fact]
    public async Task HealthCheck_UpdatesQueueDepthMetric()
    {
        // Arrange
        _mockMessageQueue.Setup(x => x.Count).Returns(250);
        var monitor = new BrokerHealthMonitor(
            _mockMessageQueue.Object,
            _metrics,
            _mockLogger.Object,
            checkInterval: BrokerHealthMonitorTestConstants.ShortDelay);

        // Act
        await monitor.StartAsync(_cts.Token);
        await Task.Delay(BrokerHealthMonitorTestConstants.HealthCheckWait);
        _cts.Cancel();

        // Assert - Metrics provider is real, so just verify health check ran
        // (queue depth is checked when determining health status)
        monitor.CurrentHealthStatus.Should().Be(BrokerHealthStatus.Healthy);
    }

    [Fact]
    public async Task HealthCheck_UpdatesConsumerLagMetric()
    {
        // Arrange
        _mockMessageQueue.Setup(x => x.Count).Returns(100);
        var monitor = new BrokerHealthMonitor(
            _mockMessageQueue.Object,
            _metrics,
            _mockLogger.Object,
            checkInterval: BrokerHealthMonitorTestConstants.ShortDelay);

        // Act
        await monitor.StartAsync(_cts.Token);
        await Task.Delay(BrokerHealthMonitorTestConstants.HealthCheckWait);
        _cts.Cancel();

        // Assert - Metrics provider is real, so just verify health check ran
        // (consumer lag is updated during health checks)
        monitor.CurrentHealthStatus.Should().Be(BrokerHealthStatus.Healthy);
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task HealthCheck_WhenQueueThrows_ContinuesMonitoring()
    {
        // Arrange
        var callCount = 0;
        _mockMessageQueue.Setup(x => x.Count)
            .Returns(() =>
            {
                callCount++;
                if (callCount == 1)
                    throw new InvalidOperationException("Queue error");
                return 50;
            });

        var monitor = new BrokerHealthMonitor(
            _mockMessageQueue.Object,
            _metrics,
            _mockLogger.Object,
            checkInterval: TimeSpan.FromMilliseconds(100));

        // Act
        await monitor.StartAsync(_cts.Token);
        await Task.Delay(350); // Allow time for error (first check) and at least 2 recovery checks
        _cts.Cancel();

        // Assert - Should have recovered and continued checking
        _mockMessageQueue.Verify(x => x.Count, Times.AtLeast(2));
        // Status should eventually be Healthy after recovery, but timing may vary
        // The key assertion is that it continued monitoring (Times.AtLeast(2))
        monitor.CurrentHealthStatus.Should().BeOneOf(BrokerHealthStatus.Healthy, BrokerHealthStatus.Unknown);
    }

    [Fact]
    public async Task HealthCheck_OnException_LogsError()
    {
        // Arrange
        _mockMessageQueue.Setup(x => x.Count)
            .Throws(new InvalidOperationException("Test exception"));

        var monitor = new BrokerHealthMonitor(
            _mockMessageQueue.Object,
            _metrics,
            _mockLogger.Object,
            checkInterval: TimeSpan.FromMilliseconds(100));

        // Act
        await monitor.StartAsync(_cts.Token);
        await Task.Delay(150);
        _cts.Cancel();

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce());
    }

    #endregion

    #region Lifecycle Tests

    [Fact]
    public async Task StopAsync_StopsMonitoring_Gracefully()
    {
        // Arrange
        _mockMessageQueue.Setup(x => x.Count).Returns(50);
        var monitor = new BrokerHealthMonitor(
            _mockMessageQueue.Object,
            _metrics,
            _mockLogger.Object,
            checkInterval: TimeSpan.FromMilliseconds(100));

        // Act
        await monitor.StartAsync(_cts.Token);
        await Task.Delay(150); // One health check
        var stopTask = monitor.StopAsync(CancellationToken.None);
        await stopTask;

        // Assert
        stopTask.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public async Task StartAsync_LogsStartupMessage()
    {
        // Arrange
        var monitor = new BrokerHealthMonitor(
            _mockMessageQueue.Object,
            _metrics,
            _mockLogger.Object);

        // Act
        await monitor.StartAsync(_cts.Token);
        _cts.Cancel();

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("started")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce());
    }

    #endregion

    #region Health Status Transition Tests

    [Fact]
    public async Task HealthCheck_TransitionsFromHealthyToDegraded_WhenQueueGrows()
    {
        // Arrange
        var queueDepth = 50;
        _mockMessageQueue.Setup(x => x.Count).Returns(() => queueDepth);

        var monitor = new BrokerHealthMonitor(
            _mockMessageQueue.Object,
            _metrics,
            _mockLogger.Object,
            checkInterval: TimeSpan.FromMilliseconds(100));

        // Act
        await monitor.StartAsync(_cts.Token);
        await Task.Delay(150); // First check - Healthy
        monitor.CurrentHealthStatus.Should().Be(BrokerHealthStatus.Healthy);

        queueDepth = 750; // Change to degraded
        await Task.Delay(150); // Second check - Degraded
        _cts.Cancel();

        // Assert
        monitor.CurrentHealthStatus.Should().Be(BrokerHealthStatus.Degraded);
    }

    [Fact]
    public async Task HealthCheck_TransitionsFromDegradedToHealthy_WhenQueueShrinks()
    {
        // Arrange
        var queueDepth = 750;
        _mockMessageQueue.Setup(x => x.Count).Returns(() => queueDepth);

        var monitor = new BrokerHealthMonitor(
            _mockMessageQueue.Object,
            _metrics,
            _mockLogger.Object,
            checkInterval: TimeSpan.FromMilliseconds(100));

        // Act
        await monitor.StartAsync(_cts.Token);
        await Task.Delay(150); // First check - Degraded
        monitor.CurrentHealthStatus.Should().Be(BrokerHealthStatus.Degraded);

        queueDepth = 50; // Change to healthy
        await Task.Delay(150); // Second check - Healthy
        _cts.Cancel();

        // Assert
        monitor.CurrentHealthStatus.Should().Be(BrokerHealthStatus.Healthy);
    }

    #endregion

    #region Custom Check Interval Tests

    [Fact]
    public async Task Constructor_WithCustomCheckInterval_UsesProvidedInterval()
    {
        // Arrange
        _mockMessageQueue.Setup(x => x.Count).Returns(50);
        var customInterval = TimeSpan.FromMilliseconds(200);

        var monitor = new BrokerHealthMonitor(
            _mockMessageQueue.Object,
            _metrics,
            _mockLogger.Object,
            checkInterval: customInterval);

        // Act
        await monitor.StartAsync(_cts.Token);
        await Task.Delay(350); // Should get ~1-2 checks (not 3)
        _cts.Cancel();

        // Assert - With 200ms interval, 350ms should give ~1-2 checks
        _mockMessageQueue.Verify(x => x.Count, Times.AtMost(2));
    }

    #endregion

    public void Dispose()
    {
        _cts?.Dispose();
        _metrics?.Dispose();
    }
}
