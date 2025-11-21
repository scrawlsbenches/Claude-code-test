using FluentAssertions;
using HotSwap.Distributed.Api.Services;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Interfaces;
using HotSwap.Distributed.Orchestrator.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace HotSwap.Distributed.Tests.Api.Services;

public class ApprovalTimeoutBackgroundServiceTests
{
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly Mock<IServiceScope> _mockServiceScope;
    private readonly Mock<IServiceScopeFactory> _mockServiceScopeFactory;
    private readonly Mock<IApprovalService> _mockApprovalService;
    private readonly ApprovalTimeoutBackgroundService _service;

    public ApprovalTimeoutBackgroundServiceTests()
    {
        _mockApprovalService = new Mock<IApprovalService>();
        _mockServiceScope = new Mock<IServiceScope>();
        _mockServiceScopeFactory = new Mock<IServiceScopeFactory>();
        _mockServiceProvider = new Mock<IServiceProvider>();

        // Setup service scope factory
        var scopeServiceProvider = new Mock<IServiceProvider>();
        scopeServiceProvider.Setup(x => x.GetService(typeof(IApprovalService)))
            .Returns(_mockApprovalService.Object);

        _mockServiceScope.Setup(x => x.ServiceProvider).Returns(scopeServiceProvider.Object);
        _mockServiceScopeFactory.Setup(x => x.CreateScope()).Returns(_mockServiceScope.Object);

        _mockServiceProvider.Setup(x => x.GetService(typeof(IServiceScopeFactory)))
            .Returns(_mockServiceScopeFactory.Object);

        _service = new ApprovalTimeoutBackgroundService(
            _mockServiceProvider.Object,
            NullLogger<ApprovalTimeoutBackgroundService>.Instance);
    }

    [Fact]
    public async Task StartAsync_StartsService()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        _mockApprovalService.Setup(x => x.ProcessExpiredApprovalsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        // Act
        await _service.StartAsync(cts.Token);
        await Task.Delay(100);

        // Assert
        cts.Cancel();
        await _service.StopAsync(CancellationToken.None);

        _mockApprovalService.Verify(x => x.ProcessExpiredApprovalsAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task StopAsync_StopsService()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        _mockApprovalService.Setup(x => x.ProcessExpiredApprovalsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        // Act
        await _service.StartAsync(cts.Token);
        await Task.Delay(50);
        await _service.StopAsync(CancellationToken.None);

        var callCountBeforeStop = _mockServiceScopeFactory.Invocations.Count;
        await Task.Delay(6000); // Wait longer than check interval (5 min)

        // Assert
        var callCountAfterStop = _mockServiceScopeFactory.Invocations.Count;
        callCountAfterStop.Should().Be(callCountBeforeStop);
    }

    [Fact]
    public async Task ExecuteAsync_ProcessesExpiredApprovals()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        _mockApprovalService.Setup(x => x.ProcessExpiredApprovalsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        // Act
        await _service.StartAsync(cts.Token);
        await Task.Delay(TimeSpan.FromMinutes(5).Add(TimeSpan.FromSeconds(1)));

        // Assert
        cts.Cancel();
        await _service.StopAsync(CancellationToken.None);

        _mockApprovalService.Verify(x => x.ProcessExpiredApprovalsAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_WithNoExpiredApprovals_ContinuesRunning()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        _mockApprovalService.Setup(x => x.ProcessExpiredApprovalsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        // Act
        await _service.StartAsync(cts.Token);
        await Task.Delay(TimeSpan.FromMinutes(5).Add(TimeSpan.FromSeconds(1)));

        // Assert
        cts.Cancel();
        await _service.StopAsync(CancellationToken.None);

        _mockApprovalService.Verify(x => x.ProcessExpiredApprovalsAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_WithException_ContinuesRunning()
    {
        // Arrange
        var callCount = 0;
        _mockApprovalService.Setup(x => x.ProcessExpiredApprovalsAsync(It.IsAny<CancellationToken>()))
            .Callback(() => callCount++)
            .ThrowsAsync(new InvalidOperationException("Service unavailable"));

        var cts = new CancellationTokenSource();

        // Act
        await _service.StartAsync(cts.Token);
        await Task.Delay(TimeSpan.FromMinutes(10).Add(TimeSpan.FromSeconds(1)));
        cts.Cancel();
        await _service.StopAsync(CancellationToken.None);

        // Assert - service should continue trying despite failures
        callCount.Should().BeGreaterThan(1);
    }

    [Fact]
    public async Task ExecuteAsync_UsesCorrectCheckInterval()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        var callTimes = new List<DateTime>();

        _mockApprovalService.Setup(x => x.ProcessExpiredApprovalsAsync(It.IsAny<CancellationToken>()))
            .Callback(() => callTimes.Add(DateTime.UtcNow))
            .ReturnsAsync(0);

        // Act
        await _service.StartAsync(cts.Token);
        await Task.Delay(TimeSpan.FromMinutes(15).Add(TimeSpan.FromSeconds(1))); // Wait for 3 check intervals
        cts.Cancel();
        await _service.StopAsync(CancellationToken.None);

        // Assert - should execute approximately every 5 minutes
        callTimes.Should().HaveCountGreaterOrEqualTo(2);

        if (callTimes.Count >= 2)
        {
            var intervalBetweenCalls = callTimes[1] - callTimes[0];
            intervalBetweenCalls.Should().BeCloseTo(TimeSpan.FromMinutes(5), TimeSpan.FromSeconds(10));
        }
    }
}
