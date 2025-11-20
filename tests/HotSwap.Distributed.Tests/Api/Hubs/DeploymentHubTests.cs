using HotSwap.Distributed.Api.Hubs;
using Microsoft.AspNetCore.SignalR;
using Moq;
using Xunit;
using FluentAssertions;

namespace HotSwap.Distributed.Tests.Api.Hubs;

public class DeploymentHubTests
{
    private readonly Mock<IGroupManager> _mockGroups;
    private readonly Mock<HubCallerContext> _mockContext;
    private readonly DeploymentHub _hub;

    public DeploymentHubTests()
    {
        _mockGroups = new Mock<IGroupManager>();
        _mockContext = new Mock<HubCallerContext>();

        _hub = new DeploymentHub
        {
            Groups = _mockGroups.Object,
            Context = _mockContext.Object
        };
    }

    [Fact]
    public async Task SubscribeToDeployment_WithValidExecutionId_AddsConnectionToGroup()
    {
        // Arrange
        var executionId = "test-execution-123";
        var connectionId = "connection-456";
        _mockContext.Setup(x => x.ConnectionId).Returns(connectionId);

        // Act
        await _hub.SubscribeToDeployment(executionId);

        // Assert
        _mockGroups.Verify(
            x => x.AddToGroupAsync(connectionId, $"deployment-{executionId}", default),
            Times.Once);
    }

    [Fact]
    public async Task UnsubscribeFromDeployment_WithValidExecutionId_RemovesConnectionFromGroup()
    {
        // Arrange
        var executionId = "test-execution-123";
        var connectionId = "connection-456";
        _mockContext.Setup(x => x.ConnectionId).Returns(connectionId);

        // Act
        await _hub.UnsubscribeFromDeployment(executionId);

        // Assert
        _mockGroups.Verify(
            x => x.RemoveFromGroupAsync(connectionId, $"deployment-{executionId}", default),
            Times.Once);
    }

    [Fact]
    public async Task SubscribeToDeployment_WithEmptyExecutionId_ThrowsArgumentException()
    {
        // Arrange
        var emptyExecutionId = "";

        // Act
        Func<Task> act = async () => await _hub.SubscribeToDeployment(emptyExecutionId);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*executionId*");
    }

    [Fact]
    public async Task SubscribeToDeployment_WithNullExecutionId_ThrowsArgumentNullException()
    {
        // Arrange
        string? nullExecutionId = null;

        // Act
        Func<Task> act = async () => await _hub.SubscribeToDeployment(nullExecutionId!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithMessage("*executionId*");
    }

    [Fact]
    public async Task UnsubscribeFromDeployment_WithEmptyExecutionId_ThrowsArgumentException()
    {
        // Arrange
        var emptyExecutionId = "";

        // Act
        Func<Task> act = async () => await _hub.UnsubscribeFromDeployment(emptyExecutionId);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*executionId*");
    }

    [Fact]
    public async Task UnsubscribeFromDeployment_WithNullExecutionId_ThrowsArgumentNullException()
    {
        // Arrange
        string? nullExecutionId = null;

        // Act
        Func<Task> act = async () => await _hub.UnsubscribeFromDeployment(nullExecutionId!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithMessage("*executionId*");
    }

    [Fact]
    public async Task SubscribeToDeployment_CalledMultipleTimes_AddsToGroupMultipleTimes()
    {
        // Arrange
        var executionId1 = "test-execution-123";
        var executionId2 = "test-execution-456";
        var connectionId = "connection-789";
        _mockContext.Setup(x => x.ConnectionId).Returns(connectionId);

        // Act
        await _hub.SubscribeToDeployment(executionId1);
        await _hub.SubscribeToDeployment(executionId2);

        // Assert
        _mockGroups.Verify(
            x => x.AddToGroupAsync(connectionId, $"deployment-{executionId1}", default),
            Times.Once);
        _mockGroups.Verify(
            x => x.AddToGroupAsync(connectionId, $"deployment-{executionId2}", default),
            Times.Once);
    }

    [Fact]
    public async Task SubscribeToDeployment_WithSameExecutionIdMultipleTimes_AddsToGroupMultipleTimes()
    {
        // Arrange
        var executionId = "test-execution-123";
        var connectionId = "connection-456";
        _mockContext.Setup(x => x.ConnectionId).Returns(connectionId);

        // Act
        await _hub.SubscribeToDeployment(executionId);
        await _hub.SubscribeToDeployment(executionId);

        // Assert
        // SignalR allows duplicate subscriptions (idempotent at SignalR level)
        _mockGroups.Verify(
            x => x.AddToGroupAsync(connectionId, $"deployment-{executionId}", default),
            Times.Exactly(2));
    }

    [Fact]
    public async Task UnsubscribeFromDeployment_WithoutPriorSubscription_RemovesFromGroupSafely()
    {
        // Arrange
        var executionId = "test-execution-123";
        var connectionId = "connection-456";
        _mockContext.Setup(x => x.ConnectionId).Returns(connectionId);

        // Act
        await _hub.UnsubscribeFromDeployment(executionId);

        // Assert
        // Should not throw even if not previously subscribed
        _mockGroups.Verify(
            x => x.RemoveFromGroupAsync(connectionId, $"deployment-{executionId}", default),
            Times.Once);
    }

    [Fact]
    public async Task SubscribeToAllDeployments_AddsConnectionToAllDeploymentsGroup()
    {
        // Arrange
        var connectionId = "connection-123";
        _mockContext.Setup(x => x.ConnectionId).Returns(connectionId);

        // Act
        await _hub.SubscribeToAllDeployments();

        // Assert
        _mockGroups.Verify(
            x => x.AddToGroupAsync(connectionId, "all-deployments", default),
            Times.Once);
    }

    [Fact]
    public async Task UnsubscribeFromAllDeployments_RemovesConnectionFromAllDeploymentsGroup()
    {
        // Arrange
        var connectionId = "connection-123";
        _mockContext.Setup(x => x.ConnectionId).Returns(connectionId);

        // Act
        await _hub.UnsubscribeFromAllDeployments();

        // Assert
        _mockGroups.Verify(
            x => x.RemoveFromGroupAsync(connectionId, "all-deployments", default),
            Times.Once);
    }

    [Fact]
    public async Task SubscribeToAllDeployments_CalledMultipleTimes_AddsToGroupMultipleTimes()
    {
        // Arrange
        var connectionId = "connection-123";
        _mockContext.Setup(x => x.ConnectionId).Returns(connectionId);

        // Act
        await _hub.SubscribeToAllDeployments();
        await _hub.SubscribeToAllDeployments();

        // Assert
        // SignalR allows duplicate subscriptions (idempotent at SignalR level)
        _mockGroups.Verify(
            x => x.AddToGroupAsync(connectionId, "all-deployments", default),
            Times.Exactly(2));
    }

    [Fact]
    public async Task UnsubscribeFromAllDeployments_WithoutPriorSubscription_RemovesFromGroupSafely()
    {
        // Arrange
        var connectionId = "connection-123";
        _mockContext.Setup(x => x.ConnectionId).Returns(connectionId);

        // Act
        await _hub.UnsubscribeFromAllDeployments();

        // Assert
        // Should not throw even if not previously subscribed
        _mockGroups.Verify(
            x => x.RemoveFromGroupAsync(connectionId, "all-deployments", default),
            Times.Once);
    }
}
