using FluentAssertions;
using HotSwap.Distributed.Api.Hubs;
using HotSwap.Distributed.Api.Services;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace HotSwap.Distributed.Tests.Api.Services;

public class SignalRDeploymentNotifierTests
{
    private readonly Mock<IHubContext<DeploymentHub>> _mockHubContext;
    private readonly Mock<IHubClients> _mockClients;
    private readonly Mock<IClientProxy> _mockClientProxy;
    private readonly Mock<ILogger<SignalRDeploymentNotifier>> _mockLogger;
    private readonly SignalRDeploymentNotifier _notifier;

    public SignalRDeploymentNotifierTests()
    {
        _mockHubContext = new Mock<IHubContext<DeploymentHub>>();
        _mockClients = new Mock<IHubClients>();
        _mockClientProxy = new Mock<IClientProxy>();
        _mockLogger = new Mock<ILogger<SignalRDeploymentNotifier>>();

        _mockHubContext.Setup(x => x.Clients).Returns(_mockClients.Object);

        _notifier = new SignalRDeploymentNotifier(_mockHubContext.Object, _mockLogger.Object);
    }

    private static PipelineExecutionState CreateTestState(string executionId, string status = "Running")
    {
        return new PipelineExecutionState
        {
            ExecutionId = Guid.Parse(executionId),
            Request = new DeploymentRequest
            {
                Module = new ModuleDescriptor
                {
                    Name = "test-module",
                    Version = new Version("1.0.0")
                },
                RequesterEmail = "test@example.com"
            },
            Status = status,
            CurrentStage = status
        };
    }

    [Fact]
    public async Task NotifyDeploymentStatusChanged_WithValidStatus_SendsToDeploymentGroup()
    {
        // Arrange
        var executionId = "00000000-0000-0000-0000-000000000001";
        var state = CreateTestState(executionId);

        _mockClients.Setup(x => x.Group($"deployment-{executionId}"))
            .Returns(_mockClientProxy.Object);
        _mockClients.Setup(x => x.Group("all-deployments"))
            .Returns(_mockClientProxy.Object);

        // Act
        await _notifier.NotifyDeploymentStatusChanged(executionId, state);

        // Assert
        _mockClients.Verify(
            x => x.Group($"deployment-{executionId}"),
            Times.Once);

        _mockClientProxy.Verify(
            x => x.SendCoreAsync(
                "DeploymentStatusChanged",
                It.Is<object[]>(args => args.Length == 1),
                default),
            Times.Exactly(2)); // Called twice: once for deployment group, once for all-deployments
    }

    [Fact]
    public async Task NotifyDeploymentStatusChanged_WithValidStatus_SendsToAllDeploymentsGroup()
    {
        // Arrange
        var executionId = "00000000-0000-0000-0000-000000000001";
        var state = CreateTestState(executionId, "Completed");

        _mockClients.Setup(x => x.Group($"deployment-{executionId}"))
            .Returns(_mockClientProxy.Object);
        _mockClients.Setup(x => x.Group("all-deployments"))
            .Returns(_mockClientProxy.Object);

        // Act
        await _notifier.NotifyDeploymentStatusChanged(executionId, state);

        // Assert
        _mockClients.Verify(
            x => x.Group("all-deployments"),
            Times.Once);

        _mockClientProxy.Verify(
            x => x.SendCoreAsync(
                "DeploymentStatusChanged",
                It.Is<object[]>(args => args.Length == 1),
                default),
            Times.Exactly(2)); // Called twice: once for deployment group, once for all-deployments
    }

    [Fact]
    public async Task NotifyDeploymentStatusChanged_WithNullExecutionId_ThrowsArgumentNullException()
    {
        // Arrange
        string? nullExecutionId = null;
        var state = new PipelineExecutionState
        {
            ExecutionId = Guid.NewGuid(),
            Request = new DeploymentRequest
            {
                Module = new ModuleDescriptor { Name = "test", Version = new Version("1.0.0") },
                RequesterEmail = "test@example.com"
            },
            Status = "Running"
        };

        // Act
        Func<Task> act = async () => await _notifier.NotifyDeploymentStatusChanged(nullExecutionId!, state);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithMessage("*executionId*");
    }

    [Fact]
    public async Task NotifyDeploymentStatusChanged_WithEmptyExecutionId_ThrowsArgumentException()
    {
        // Arrange
        var emptyExecutionId = "";
        var state = new PipelineExecutionState
        {
            ExecutionId = Guid.NewGuid(),
            Request = new DeploymentRequest
            {
                Module = new ModuleDescriptor { Name = "test", Version = new Version("1.0.0") },
                RequesterEmail = "test@example.com"
            },
            Status = "Running"
        };

        // Act
        Func<Task> act = async () => await _notifier.NotifyDeploymentStatusChanged(emptyExecutionId, state);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*executionId*");
    }

    [Fact]
    public async Task NotifyDeploymentStatusChanged_WithNullState_ThrowsArgumentNullException()
    {
        // Arrange
        var executionId = "00000000-0000-0000-0000-000000000001";
        PipelineExecutionState? nullState = null;

        // Act
        Func<Task> act = async () => await _notifier.NotifyDeploymentStatusChanged(executionId, nullState!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithMessage("*state*");
    }

    [Fact]
    public async Task NotifyDeploymentProgress_WithValidProgress_SendsToDeploymentGroup()
    {
        // Arrange
        var executionId = "00000000-0000-0000-0000-000000000001";
        var stage = "Building";
        var progress = 75;

        _mockClients.Setup(x => x.Group($"deployment-{executionId}"))
            .Returns(_mockClientProxy.Object);

        // Act
        await _notifier.NotifyDeploymentProgress(executionId, stage, progress);

        // Assert
        _mockClients.Verify(
            x => x.Group($"deployment-{executionId}"),
            Times.Once);

        _mockClientProxy.Verify(
            x => x.SendCoreAsync(
                "DeploymentProgress",
                It.Is<object[]>(args => args.Length == 1),
                default),
            Times.Once);
    }

    [Fact]
    public async Task NotifyDeploymentProgress_WithNullExecutionId_ThrowsArgumentNullException()
    {
        // Arrange
        string? nullExecutionId = null;
        var stage = "Building";
        var progress = 50;

        // Act
        Func<Task> act = async () => await _notifier.NotifyDeploymentProgress(nullExecutionId!, stage, progress);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithMessage("*executionId*");
    }

    [Fact]
    public async Task NotifyDeploymentProgress_WithEmptyExecutionId_ThrowsArgumentException()
    {
        // Arrange
        var emptyExecutionId = "";
        var stage = "Building";
        var progress = 50;

        // Act
        Func<Task> act = async () => await _notifier.NotifyDeploymentProgress(emptyExecutionId, stage, progress);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*executionId*");
    }

    [Fact]
    public async Task NotifyDeploymentProgress_WithNullStage_ThrowsArgumentNullException()
    {
        // Arrange
        var executionId = "00000000-0000-0000-0000-000000000001";
        string? nullStage = null;
        var progress = 50;

        // Act
        Func<Task> act = async () => await _notifier.NotifyDeploymentProgress(executionId, nullStage!, progress);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithMessage("*stage*");
    }

    [Fact]
    public async Task NotifyDeploymentProgress_WithNegativeProgress_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var executionId = "00000000-0000-0000-0000-000000000001";
        var stage = "Building";
        var negativeProgress = -1;

        // Act
        Func<Task> act = async () => await _notifier.NotifyDeploymentProgress(executionId, stage, negativeProgress);

        // Assert
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>()
            .WithMessage("*progress*");
    }

    [Fact]
    public async Task NotifyDeploymentProgress_WithProgressOver100_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var executionId = "00000000-0000-0000-0000-000000000001";
        var stage = "Building";
        var invalidProgress = 101;

        // Act
        Func<Task> act = async () => await _notifier.NotifyDeploymentProgress(executionId, stage, invalidProgress);

        // Assert
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>()
            .WithMessage("*progress*");
    }

    [Fact]
    public async Task NotifyDeploymentProgress_WithProgress100_Succeeds()
    {
        // Arrange
        var executionId = "00000000-0000-0000-0000-000000000001";
        var stage = "Completed";
        var progress = 100;

        _mockClients.Setup(x => x.Group($"deployment-{executionId}"))
            .Returns(_mockClientProxy.Object);

        // Act
        await _notifier.NotifyDeploymentProgress(executionId, stage, progress);

        // Assert
        _mockClientProxy.Verify(
            x => x.SendCoreAsync(
                "DeploymentProgress",
                It.Is<object[]>(args => args.Length == 1),
                default),
            Times.Once);
    }

    [Fact]
    public async Task NotifyDeploymentProgress_WithProgress0_Succeeds()
    {
        // Arrange
        var executionId = "00000000-0000-0000-0000-000000000001";
        var stage = "Starting";
        var progress = 0;

        _mockClients.Setup(x => x.Group($"deployment-{executionId}"))
            .Returns(_mockClientProxy.Object);

        // Act
        await _notifier.NotifyDeploymentProgress(executionId, stage, progress);

        // Assert
        _mockClientProxy.Verify(
            x => x.SendCoreAsync(
                "DeploymentProgress",
                It.Is<object[]>(args => args.Length == 1),
                default),
            Times.Once);
    }
}
