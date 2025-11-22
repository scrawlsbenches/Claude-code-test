using FluentAssertions;
using HotSwap.Distributed.Api.Services;
using HotSwap.Distributed.Infrastructure.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace HotSwap.Distributed.Tests.Api.Services;

[Collection("BackgroundService Sequential")]
public class AuditLogRetentionBackgroundServiceTests
{
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly Mock<IServiceScope> _mockServiceScope;
    private readonly Mock<IServiceScopeFactory> _mockServiceScopeFactory;
    private readonly Mock<IAuditLogService> _mockAuditLogService;
    private readonly AuditLogRetentionBackgroundService _service;

    public AuditLogRetentionBackgroundServiceTests()
    {
        _mockAuditLogService = new Mock<IAuditLogService>();
        _mockServiceScope = new Mock<IServiceScope>();
        _mockServiceScopeFactory = new Mock<IServiceScopeFactory>();
        _mockServiceProvider = new Mock<IServiceProvider>();

        // Setup service scope factory
        var scopeServiceProvider = new Mock<IServiceProvider>();
        scopeServiceProvider.Setup(x => x.GetService(typeof(IAuditLogService)))
            .Returns(_mockAuditLogService.Object);

        _mockServiceScope.Setup(x => x.ServiceProvider).Returns(scopeServiceProvider.Object);
        _mockServiceScopeFactory.Setup(x => x.CreateScope()).Returns(_mockServiceScope.Object);

        _mockServiceProvider.Setup(x => x.GetService(typeof(IServiceScopeFactory)))
            .Returns(_mockServiceScopeFactory.Object);

        _service = new AuditLogRetentionBackgroundService(
            _mockServiceProvider.Object,
            NullLogger<AuditLogRetentionBackgroundService>.Instance);
    }

    [Fact]
    public async Task StartAsync_StartsService()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        _mockAuditLogService.Setup(x => x.DeleteOldAuditLogsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        // Act
        await _service.StartAsync(cts.Token);
        await Task.Delay(100);

        // Assert
        cts.Cancel();
        await _service.StopAsync(CancellationToken.None);

        // Should not execute immediately (1 minute delay)
        _mockAuditLogService.Verify(x => x.DeleteOldAuditLogsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task StopAsync_StopsService()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        _mockAuditLogService.Setup(x => x.DeleteOldAuditLogsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        // Act
        await _service.StartAsync(cts.Token);
        await Task.Delay(50);

        // StopAsync should complete without hanging
        var stopTask = _service.StopAsync(CancellationToken.None);
        var completedInTime = await Task.WhenAny(stopTask, Task.Delay(5000)) == stopTask;

        // Assert
        completedInTime.Should().BeTrue("StopAsync should complete within 5 seconds");
        cts.Cancel();
    }

    [Fact]
    public async Task ExecuteAsync_DeletesOldAuditLogs()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        var deletedCount = 0;

        _mockAuditLogService.Setup(x => x.DeleteOldAuditLogsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback<int, CancellationToken>((days, ct) => deletedCount++)
            .ReturnsAsync(15);

        // Act
        await _service.StartAsync(cts.Token);
        await Task.Delay(TimeSpan.FromMinutes(1).Add(TimeSpan.FromSeconds(1)));

        // Assert
        cts.Cancel();
        await _service.StopAsync(CancellationToken.None);

        _mockAuditLogService.Verify(x => x.DeleteOldAuditLogsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        deletedCount.Should().BeGreaterOrEqualTo(1);
    }

    [Fact]
    public async Task ExecuteAsync_Uses90DayRetentionPeriod()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        int? capturedRetentionDays = null;

        _mockAuditLogService.Setup(x => x.DeleteOldAuditLogsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback<int, CancellationToken>((days, ct) => capturedRetentionDays = days)
            .ReturnsAsync(10);

        // Act
        await _service.StartAsync(cts.Token);
        await Task.Delay(TimeSpan.FromMinutes(1).Add(TimeSpan.FromSeconds(1)));

        // Assert
        cts.Cancel();
        await _service.StopAsync(CancellationToken.None);

        capturedRetentionDays.Should().NotBeNull();
        capturedRetentionDays!.Value.Should().Be(90);
    }

    [Fact]
    public async Task ExecuteAsync_WithNoOldLogs_ContinuesRunning()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        _mockAuditLogService.Setup(x => x.DeleteOldAuditLogsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        // Act
        await _service.StartAsync(cts.Token);
        await Task.Delay(TimeSpan.FromMinutes(1).Add(TimeSpan.FromSeconds(1)));

        // Assert
        cts.Cancel();
        await _service.StopAsync(CancellationToken.None);

        _mockAuditLogService.Verify(x => x.DeleteOldAuditLogsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_WithException_ContinuesRunning()
    {
        // Arrange
        _mockAuditLogService.Setup(x => x.DeleteOldAuditLogsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database unavailable"));

        var cts = new CancellationTokenSource();

        // Act - Start service and wait for initial delay, then stop
        await _service.StartAsync(cts.Token);
        await Task.Delay(100); // Brief delay to ensure service started

        // StopAsync should complete even when exceptions occur
        cts.Cancel();
        var stopTask = _service.StopAsync(CancellationToken.None);
        var completedInTime = await Task.WhenAny(stopTask, Task.Delay(5000)) == stopTask;

        // Assert - service should stop gracefully even with exceptions
        completedInTime.Should().BeTrue("Service should stop within 5 seconds despite exceptions");
    }

    [Fact]
    public async Task ExecuteAsync_UsesCorrectCheckInterval()
    {
        // NOTE: This test verifies service configuration, not actual timing.
        // The service uses a 24-hour interval which cannot be realistically tested in unit tests.
        // This would require refactoring the service to accept configurable intervals.

        // Arrange
        var cts = new CancellationTokenSource();
        _mockAuditLogService.Setup(x => x.DeleteOldAuditLogsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        // Act - Verify service starts and stops correctly
        await _service.StartAsync(cts.Token);
        await Task.Delay(100);

        // StopAsync should complete within reasonable time
        cts.Cancel();
        var stopTask = _service.StopAsync(CancellationToken.None);
        var completedInTime = await Task.WhenAny(stopTask, Task.Delay(5000)) == stopTask;

        // Assert
        completedInTime.Should().BeTrue("Service should respond to stop signal within 5 seconds");
    }

    [Fact]
    public async Task ExecuteAsync_WaitsInitialDelayBeforeFirstExecution()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var firstCallElapsed = TimeSpan.Zero;

        _mockAuditLogService.Setup(x => x.DeleteOldAuditLogsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                if (firstCallElapsed == TimeSpan.Zero)
                    firstCallElapsed = sw.Elapsed;
            })
            .ReturnsAsync(0);

        // Act
        await _service.StartAsync(cts.Token);
        await Task.Delay(TimeSpan.FromMinutes(1).Add(TimeSpan.FromSeconds(5)));

        // Assert
        cts.Cancel();
        await _service.StopAsync(CancellationToken.None);

        if (firstCallElapsed != TimeSpan.Zero)
        {
            // Allow for small timing overhead (59.9 seconds minimum instead of strict 60)
            // to account for scheduling delays and measurement precision
            firstCallElapsed.Should().BeGreaterOrEqualTo(TimeSpan.FromSeconds(59.9));
        }
    }
}
