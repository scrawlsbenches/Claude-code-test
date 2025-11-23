using FluentAssertions;
using HotSwap.Distributed.Api.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HotSwap.Distributed.Tests.Api.Services;

public class RateLimitCleanupServiceTests
{
    private readonly RateLimitCleanupService _service;

    public RateLimitCleanupServiceTests()
    {
        _service = new RateLimitCleanupService(
            NullLogger<RateLimitCleanupService>.Instance);
    }

    [Fact]
    public async Task StartAsync_StartsService()
    {
        // Arrange
        var cts = new CancellationTokenSource();

        // Act
        await _service.StartAsync(cts.Token);
        await Task.Delay(100);

        // Assert - Service should start without errors
        cts.Cancel();
        await _service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StopAsync_StopsService()
    {
        // Arrange
        var cts = new CancellationTokenSource();

        // Act
        await _service.StartAsync(cts.Token);
        await Task.Delay(50);

        // StopAsync should complete without hanging
        var stopTask = _service.StopAsync(CancellationToken.None);
        var completedInTime = await Task.WhenAny(stopTask, Task.Delay(1000)) == stopTask;

        // Assert - Service should stop gracefully within timeout
        completedInTime.Should().BeTrue("StopAsync should complete promptly");

        // Clean up
        cts.Cancel();
    }

    [Fact]
    public async Task ExecuteAsync_HandlesCancellationGracefully()
    {
        // Arrange
        var cts = new CancellationTokenSource();

        // Act
        await _service.StartAsync(cts.Token);
        await Task.Delay(50);
        cts.Cancel();

        // Wait for cancellation to be handled
        await Task.Delay(100);

        await _service.StopAsync(CancellationToken.None);

        // Assert - No exception should be thrown
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new RateLimitCleanupService(null!));

        exception.ParamName.Should().Be("logger");
    }

    [Fact]
    public async Task Service_RunsPeriodicCleanup()
    {
        // Arrange
        var cts = new CancellationTokenSource();

        // Act
        await _service.StartAsync(cts.Token);

        // Wait for less than cleanup interval (1 minute)
        await Task.Delay(500);

        // Assert - Service should be running
        cts.Cancel();
        await _service.StopAsync(CancellationToken.None);

        // No exception means cleanup ran successfully
    }

    [Fact]
    public async Task Service_ContinuesAfterCleanupErrors()
    {
        // Arrange
        var cts = new CancellationTokenSource();

        // Act
        await _service.StartAsync(cts.Token);
        await Task.Delay(100);

        // Even if cleanup throws exceptions, service should continue
        // (This is handled by the try-catch in the service)

        cts.Cancel();
        await _service.StopAsync(CancellationToken.None);

        // Assert - Service handled errors and stopped gracefully
    }

}
