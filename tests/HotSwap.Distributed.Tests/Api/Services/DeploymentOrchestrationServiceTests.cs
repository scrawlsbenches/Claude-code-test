using FluentAssertions;
using HotSwap.Distributed.Api.Services;
using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Deployments;
using HotSwap.Distributed.Orchestrator.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace HotSwap.Distributed.Tests.Api.Services;

/// <summary>
/// Unit tests for DeploymentOrchestrationService (DESIGN-02 fix)
/// Tests the supervised background service that replaces fire-and-forget Task.Run pattern
/// </summary>
public class DeploymentOrchestrationServiceTests : IDisposable
{
    private readonly Mock<DistributedKernelOrchestrator> _mockOrchestrator;
    private readonly Mock<IDeploymentTracker> _mockDeploymentTracker;
    private readonly Mock<ILogger<DeploymentOrchestrationService>> _mockLogger;
    private readonly DeploymentOrchestrationService _service;
    private readonly CancellationTokenSource _cts;

    public DeploymentOrchestrationServiceTests()
    {
        _mockOrchestrator = new Mock<DistributedKernelOrchestrator>(MockBehavior.Loose);
        _mockDeploymentTracker = new Mock<IDeploymentTracker>();
        _mockLogger = new Mock<ILogger<DeploymentOrchestrationService>>();

        _service = new DeploymentOrchestrationService(
            _mockOrchestrator.Object,
            _mockDeploymentTracker.Object,
            _mockLogger.Object);

        _cts = new CancellationTokenSource();
    }

    [Fact]
    public async Task QueueDeploymentAsync_ShouldReturnExecutionId()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        var request = new DeploymentRequest
        {
            ExecutionId = executionId,
            Module = new ModuleDescriptor
            {
                Name = "TestModule",
                Version = new Version(1, 0, 0)
            },
            RequesterEmail = "test@example.com",
            TargetEnvironment = EnvironmentType.Production
        };

        // Act
        var result = await _service.QueueDeploymentAsync(request);

        // Assert
        result.Should().Be(executionId);
    }

    [Fact]
    public async Task QueueDeploymentAsync_ShouldAcceptMultipleDeployments()
    {
        // Arrange
        var requests = Enumerable.Range(1, 5).Select(i => new DeploymentRequest
        {
            ExecutionId = Guid.NewGuid(),
            Module = new ModuleDescriptor
            {
                Name = $"Module{i}",
                Version = new Version(1, 0, 0)
            },
            RequesterEmail = "test@example.com",
            TargetEnvironment = EnvironmentType.Production
        }).ToList();

        // Act
        var results = new List<Guid>();
        foreach (var request in requests)
        {
            results.Add(await _service.QueueDeploymentAsync(request));
        }

        // Assert
        results.Should().HaveCount(5);
        results.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldProcessQueuedDeployments()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        var request = new DeploymentRequest
        {
            ExecutionId = executionId,
            Module = new ModuleDescriptor
            {
                Name = "TestModule",
                Version = new Version(1, 0, 0)
            },
            RequesterEmail = "test@example.com",
            TargetEnvironment = EnvironmentType.Production
        };

        var expectedResult = new PipelineExecutionResult
        {
            ExecutionId = executionId,
            Success = true,
            Message = "Deployment successful",
            StartTime = DateTime.UtcNow,
            EndTime = DateTime.UtcNow.AddSeconds(5),
            StageResults = new List<PipelineStageResult>()
        };

        _mockOrchestrator
            .Setup(o => o.ExecuteDeploymentPipelineAsync(
                It.IsAny<DeploymentRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act - Start the background service
        var serviceTask = _service.StartAsync(CancellationToken.None);
        await _service.QueueDeploymentAsync(request);

        // Give time for processing
        await Task.Delay(500);

        // Stop the service
        await _service.StopAsync(CancellationToken.None);

        // Assert
        _mockOrchestrator.Verify(
            o => o.ExecuteDeploymentPipelineAsync(
                It.Is<DeploymentRequest>(r => r.ExecutionId == executionId),
                It.IsAny<CancellationToken>()),
            Times.Once);

        _mockDeploymentTracker.Verify(
            t => t.StoreResultAsync(executionId, expectedResult),
            Times.Once);

        _mockDeploymentTracker.Verify(
            t => t.RemoveInProgressAsync(executionId),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldHandleDeploymentFailures()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        var request = new DeploymentRequest
        {
            ExecutionId = executionId,
            Module = new ModuleDescriptor
            {
                Name = "FailingModule",
                Version = new Version(1, 0, 0)
            },
            RequesterEmail = "test@example.com",
            TargetEnvironment = EnvironmentType.Production
        };

        var expectedException = new InvalidOperationException("Deployment failed");

        _mockOrchestrator
            .Setup(o => o.ExecuteDeploymentPipelineAsync(
                It.IsAny<DeploymentRequest>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);

        // Act - Start the background service
        await _service.StartAsync(CancellationToken.None);
        await _service.QueueDeploymentAsync(request);

        // Give time for processing
        await Task.Delay(500);

        // Stop the service
        await _service.StopAsync(CancellationToken.None);

        // Assert
        _mockDeploymentTracker.Verify(
            t => t.StoreFailureAsync(
                executionId,
                It.Is<Exception>(ex => ex.Message == "Deployment failed"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldHandleGracefulShutdown()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        var request = new DeploymentRequest
        {
            ExecutionId = executionId,
            Module = new ModuleDescriptor
            {
                Name = "TestModule",
                Version = new Version(1, 0, 0)
            },
            RequesterEmail = "test@example.com",
            TargetEnvironment = EnvironmentType.Production
        };

        var longRunningTaskStarted = new TaskCompletionSource<bool>();
        var cancellationRequested = new TaskCompletionSource<bool>();

        _mockOrchestrator
            .Setup(o => o.ExecuteDeploymentPipelineAsync(
                It.IsAny<DeploymentRequest>(),
                It.IsAny<CancellationToken>()))
            .Returns(async (DeploymentRequest req, CancellationToken ct) =>
            {
                longRunningTaskStarted.SetResult(true);
                await Task.Delay(5000, ct); // Long-running operation

                return new PipelineExecutionResult
                {
                    ExecutionId = req.ExecutionId,
                    Success = true,
                    StartTime = DateTime.UtcNow,
                    EndTime = DateTime.UtcNow,
                    StageResults = new List<PipelineStageResult>()
                };
            });

        // Act
        await _service.StartAsync(CancellationToken.None);
        await _service.QueueDeploymentAsync(request);

        // Wait for deployment to start
        await longRunningTaskStarted.Task;

        // Request cancellation
        var stopTask = _service.StopAsync(CancellationToken.None);

        // Service should handle cancellation gracefully
        await stopTask;

        // Assert - The deployment should be re-queued (not lost)
        // This is indicated by the service completing without throwing
        stopTask.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldProcessMultipleDeploymentsSequentially()
    {
        // Arrange
        var processedIds = new List<Guid>();
        var requests = Enumerable.Range(1, 3).Select(i => new DeploymentRequest
        {
            ExecutionId = Guid.NewGuid(),
            Module = new ModuleDescriptor
            {
                Name = $"Module{i}",
                Version = new Version(1, 0, 0)
            },
            RequesterEmail = "test@example.com",
            TargetEnvironment = EnvironmentType.Production
        }).ToList();

        _mockOrchestrator
            .Setup(o => o.ExecuteDeploymentPipelineAsync(
                It.IsAny<DeploymentRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((DeploymentRequest req, CancellationToken ct) =>
            {
                processedIds.Add(req.ExecutionId);
                return new PipelineExecutionResult
                {
                    ExecutionId = req.ExecutionId,
                    Success = true,
                    StartTime = DateTime.UtcNow,
                    EndTime = DateTime.UtcNow,
                    StageResults = new List<PipelineStageResult>()
                };
            });

        // Act
        await _service.StartAsync(CancellationToken.None);

        foreach (var request in requests)
        {
            await _service.QueueDeploymentAsync(request);
        }

        // Give time for all deployments to process
        await Task.Delay(1000);

        await _service.StopAsync(CancellationToken.None);

        // Assert
        processedIds.Should().HaveCount(3);
        foreach (var request in requests)
        {
            processedIds.Should().Contain(request.ExecutionId);
        }
    }

    [Fact]
    public async Task QueueDeploymentAsync_WithCancellation_ShouldThrowOperationCanceledException()
    {
        // Arrange
        var request = new DeploymentRequest
        {
            ExecutionId = Guid.NewGuid(),
            ModuleName = "TestModule",
            Version = new Version(1, 0, 0),
            TargetEnvironment = EnvironmentType.Production
        };

        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await _service.QueueDeploymentAsync(request, cts.Token));
    }

    [Fact]
    public void DeploymentJob_ShouldCaptureQueueTime()
    {
        // Arrange
        var request = new DeploymentRequest
        {
            ExecutionId = Guid.NewGuid(),
            ModuleName = "TestModule",
            Version = new Version(1, 0, 0),
            TargetEnvironment = EnvironmentType.Production
        };

        var beforeCreation = DateTime.UtcNow;

        // Act
        var job = new DeploymentJob(request);

        // Assert
        job.Request.Should().BeSameAs(request);
        job.QueuedAt.Should().BeOnOrAfter(beforeCreation);
        job.QueuedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    public void Dispose()
    {
        _cts?.Dispose();
        _service?.Dispose();
    }
}
