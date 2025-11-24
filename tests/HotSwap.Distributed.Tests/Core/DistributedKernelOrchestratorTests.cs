using FluentAssertions;
using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Data.Entities;
using HotSwap.Distributed.Infrastructure.Interfaces;
using HotSwap.Distributed.Orchestrator.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace HotSwap.Distributed.Tests.Core;

public class DistributedKernelOrchestratorTests : IAsyncDisposable
{
    private readonly Mock<ILogger<DistributedKernelOrchestrator>> _mockLogger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly Mock<IAuditLogService> _mockAuditLogService;
    private readonly DistributedKernelOrchestrator _orchestrator;

    public DistributedKernelOrchestratorTests()
    {
        _mockLogger = new Mock<ILogger<DistributedKernelOrchestrator>>();
        _loggerFactory = NullLoggerFactory.Instance;
        _mockAuditLogService = new Mock<IAuditLogService>();

        // Setup audit log service to return mock IDs
        _mockAuditLogService
            .Setup(x => x.LogDeploymentEventAsync(
                It.IsAny<AuditLog>(),
                It.IsAny<DeploymentAuditEvent>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(1L);

        _orchestrator = new DistributedKernelOrchestrator(
            _mockLogger.Object,
            _loggerFactory,
            auditLogService: _mockAuditLogService.Object);
    }

    #region InitializeClustersAsync Tests

    [Fact]
    public async Task InitializeClustersAsync_ShouldCreateClustersForAllEnvironments()
    {
        // Act
        await _orchestrator.InitializeClustersAsync();

        // Assert
        var allClusters = _orchestrator.GetAllClusters();

        allClusters.Should().HaveCount(4);
        allClusters.Should().ContainKey(EnvironmentType.Development);
        allClusters.Should().ContainKey(EnvironmentType.QA);
        allClusters.Should().ContainKey(EnvironmentType.Staging);
        allClusters.Should().ContainKey(EnvironmentType.Production);
    }

    [Fact]
    public async Task InitializeClustersAsync_ShouldCreateCorrectNumberOfNodesPerEnvironment()
    {
        // Act
        await _orchestrator.InitializeClustersAsync();

        // Assert
        var devCluster = _orchestrator.GetCluster(EnvironmentType.Development);
        var qaCluster = _orchestrator.GetCluster(EnvironmentType.QA);
        var stagingCluster = _orchestrator.GetCluster(EnvironmentType.Staging);
        var prodCluster = _orchestrator.GetCluster(EnvironmentType.Production);

        devCluster.NodeCount.Should().Be(3);
        qaCluster.NodeCount.Should().Be(5);
        stagingCluster.NodeCount.Should().Be(10);
        prodCluster.NodeCount.Should().Be(20);
    }

    [Fact]
    public async Task InitializeClustersAsync_WhenCalledTwice_ShouldNotReinitialize()
    {
        // Arrange
        await _orchestrator.InitializeClustersAsync();
        var firstClusters = _orchestrator.GetAllClusters();

        // Act
        await _orchestrator.InitializeClustersAsync();
        var secondClusters = _orchestrator.GetAllClusters();

        // Assert
        firstClusters.Should().BeSameAs(secondClusters);
    }

    [Fact]
    public async Task InitializeClustersAsync_ShouldCreateNodesWithCorrectConfiguration()
    {
        // Act
        await _orchestrator.InitializeClustersAsync();

        // Assert
        var devCluster = _orchestrator.GetCluster(EnvironmentType.Development);
        var nodes = devCluster.Nodes;

        nodes.Should().NotBeEmpty();
        nodes.Should().AllSatisfy(node =>
        {
            node.Should().NotBeNull();
            node.Environment.Should().Be(EnvironmentType.Development);
        });
    }

    #endregion

    #region GetCluster Tests

    [Fact]
    public async Task GetCluster_WithValidEnvironment_ShouldReturnCluster()
    {
        // Arrange
        await _orchestrator.InitializeClustersAsync();

        // Act
        var cluster = _orchestrator.GetCluster(EnvironmentType.Development);

        // Assert
        cluster.Should().NotBeNull();
        cluster.Environment.Should().Be(EnvironmentType.Development);
    }

    [Fact]
    public void GetCluster_WithInvalidEnvironment_ShouldThrowException()
    {
        // Arrange & Act
        Action act = () => _orchestrator.GetCluster(EnvironmentType.Development);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task GetClusterAsync_WithValidEnvironment_ShouldReturnCluster()
    {
        // Arrange
        await _orchestrator.InitializeClustersAsync();

        // Act
        var cluster = await _orchestrator.GetClusterAsync(EnvironmentType.QA);

        // Assert
        cluster.Should().NotBeNull();
        cluster.Environment.Should().Be(EnvironmentType.QA);
    }

    #endregion

    #region GetAllClusters Tests

    [Fact]
    public async Task GetAllClusters_ShouldReturnAllInitializedClusters()
    {
        // Arrange
        await _orchestrator.InitializeClustersAsync();

        // Act
        var allClusters = _orchestrator.GetAllClusters();

        // Assert
        allClusters.Should().HaveCount(4);
        allClusters.Values.Should().AllSatisfy(cluster =>
        {
            cluster.Should().NotBeNull();
            cluster.NodeCount.Should().BeGreaterThan(0);
        });
    }

    #endregion

    #region ExecuteDeploymentPipelineAsync Tests

    [Fact]
    public async Task ExecuteDeploymentPipelineAsync_WhenNotInitialized_ShouldThrowException()
    {
        // Arrange
        var request = CreateTestDeploymentRequest(EnvironmentType.Development);

        // Act
        Func<Task> act = async () => await _orchestrator.ExecuteDeploymentPipelineAsync(request);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not initialized*");
    }

    [Fact]
    public async Task ExecuteDeploymentPipelineAsync_WhenInitialized_ShouldReturnResult()
    {
        // Arrange
        await _orchestrator.InitializeClustersAsync();
        var request = CreateTestDeploymentRequest(EnvironmentType.Development);

        // Act
        var result = await _orchestrator.ExecuteDeploymentPipelineAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.ExecutionId.Should().Be(request.ExecutionId);
        result.ModuleName.Should().Be(request.Module.Name);
    }

    #endregion

    #region GetClusterHealthAsync Tests

    [Fact]
    public async Task GetClusterHealthAsync_WhenNotInitialized_ShouldThrowException()
    {
        // Act
        Func<Task> act = async () => await _orchestrator.GetClusterHealthAsync(EnvironmentType.Development);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not initialized*");
    }

    [Fact]
    public async Task GetClusterHealthAsync_WhenInitialized_ShouldReturnHealth()
    {
        // Arrange
        await _orchestrator.InitializeClustersAsync();

        // Act
        var health = await _orchestrator.GetClusterHealthAsync(EnvironmentType.Development);

        // Assert
        health.Should().NotBeNull();
        health.Environment.Should().Be("Development");
        health.TotalNodes.Should().Be(3);
        health.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task GetClusterHealthAsync_ForProductionCluster_ShouldReturnCorrectNodeCount()
    {
        // Arrange
        await _orchestrator.InitializeClustersAsync();

        // Act
        var health = await _orchestrator.GetClusterHealthAsync(EnvironmentType.Production);

        // Assert
        health.TotalNodes.Should().Be(20);
        health.Environment.Should().Be("Production");
    }

    #endregion

    #region RollbackDeploymentAsync Tests

    [Fact]
    public async Task RollbackDeploymentAsync_WhenNotInitialized_ShouldThrowException()
    {
        // Arrange
        var request = CreateTestDeploymentRequest(EnvironmentType.Development);

        // Act
        Func<Task> act = async () => await _orchestrator.RollbackDeploymentAsync(request);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not initialized*");
    }

    [Fact]
    public async Task RollbackDeploymentAsync_WhenInitialized_ShouldCompleteSuccessfully()
    {
        // Arrange
        await _orchestrator.InitializeClustersAsync();
        var request = CreateTestDeploymentRequest(EnvironmentType.Development);

        // Act - Should not throw
        await _orchestrator.RollbackDeploymentAsync(request);

        // Assert - Verify audit log was called
        _mockAuditLogService.Verify(
            x => x.LogDeploymentEventAsync(
                It.Is<AuditLog>(log => log.EventType == "RollbackCompleted"),
                It.IsAny<DeploymentAuditEvent>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RollbackDeploymentAsync_ShouldLogAuditEvent()
    {
        // Arrange
        await _orchestrator.InitializeClustersAsync();
        var request = CreateTestDeploymentRequest(EnvironmentType.Production);

        // Act
        await _orchestrator.RollbackDeploymentAsync(request);

        // Assert
        _mockAuditLogService.Verify(
            x => x.LogDeploymentEventAsync(
                It.Is<AuditLog>(log =>
                    log.EventType == "RollbackCompleted" &&
                    log.EventCategory == "Deployment" &&
                    log.Action == "Rollback"),
                It.Is<DeploymentAuditEvent>(evt =>
                    evt.ModuleName == "test-module" &&
                    evt.TargetEnvironment == "Production" &&
                    evt.PipelineStage == "Rollback"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region Disposal Tests

    [Fact]
    public async Task DisposeAsync_ShouldDisposeAllClusters()
    {
        // Arrange
        await _orchestrator.InitializeClustersAsync();

        // Act
        await _orchestrator.DisposeAsync();

        // Assert - Should not throw
        // Clusters are disposed, subsequent calls should not fail
        await _orchestrator.DisposeAsync(); // Double dispose should be safe
    }

    [Fact]
    public async Task DisposeAsync_WhenCalledMultipleTimes_ShouldNotThrow()
    {
        // Arrange
        await _orchestrator.InitializeClustersAsync();

        // Act
        await _orchestrator.DisposeAsync();
        await _orchestrator.DisposeAsync();
        await _orchestrator.DisposeAsync();

        // Assert - Should complete without exceptions
        Assert.True(true);
    }

    #endregion

    #region Helper Methods

    private DeploymentRequest CreateTestDeploymentRequest(EnvironmentType targetEnvironment)
    {
        return new DeploymentRequest
        {
            ExecutionId = Guid.NewGuid(),
            Module = new ModuleDescriptor
            {
                Name = "test-module",
                Version = new Version(1, 0, 0),
                Description = "Test module for rollback",
                Dependencies = new Dictionary<string, string>()
            },
            TargetEnvironment = targetEnvironment,
            RequesterEmail = "test@example.com",
            RequireApproval = false,
            Metadata = new Dictionary<string, string>(),
            CreatedAt = DateTime.UtcNow
        };
    }

    #endregion

    public async ValueTask DisposeAsync()
    {
        await _orchestrator.DisposeAsync();
    }
}
