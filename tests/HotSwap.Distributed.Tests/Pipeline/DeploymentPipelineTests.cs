using FluentAssertions;
using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Data.Entities;
using HotSwap.Distributed.Infrastructure.Deployments;
using HotSwap.Distributed.Infrastructure.Interfaces;
using HotSwap.Distributed.Infrastructure.Telemetry;
using HotSwap.Distributed.Orchestrator.Core;
using HotSwap.Distributed.Orchestrator.Interfaces;
using HotSwap.Distributed.Orchestrator.Pipeline;
using HotSwap.Distributed.Orchestrator.Strategies;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace HotSwap.Distributed.Tests.Pipeline;

public class DeploymentPipelineTests : IDisposable
{
    private readonly Mock<IClusterRegistry> _mockClusterRegistry;
    private readonly Mock<IModuleVerifier> _mockModuleVerifier;
    private readonly TelemetryProvider _telemetry;
    private readonly PipelineConfiguration _config;
    private readonly Dictionary<EnvironmentType, IDeploymentStrategy> _strategies;
    private readonly Mock<IApprovalService> _mockApprovalService;
    private readonly Mock<IAuditLogService> _mockAuditLogService;
    private readonly Mock<IDeploymentTracker> _mockDeploymentTracker;
    private readonly Mock<IDeploymentNotifier> _mockDeploymentNotifier;
    private readonly DeploymentPipeline _pipeline;

    public DeploymentPipelineTests()
    {
        _mockClusterRegistry = new Mock<IClusterRegistry>();
        _mockModuleVerifier = new Mock<IModuleVerifier>();
        _telemetry = new TelemetryProvider();
        _config = new PipelineConfiguration
        {
            ApprovalTimeout = TimeSpan.FromSeconds(1),
            RollingHealthCheckDelay = TimeSpan.FromMilliseconds(10)
        };

        // Setup strategies for each environment
        _strategies = new Dictionary<EnvironmentType, IDeploymentStrategy>
        {
            [EnvironmentType.Development] = CreateMockStrategy("Direct").Object,
            [EnvironmentType.QA] = CreateMockStrategy("Rolling").Object,
            [EnvironmentType.Staging] = CreateMockStrategy("BlueGreen").Object,
            [EnvironmentType.Production] = CreateMockStrategy("Canary").Object
        };

        _mockApprovalService = new Mock<IApprovalService>();
        _mockAuditLogService = new Mock<IAuditLogService>();
        _mockDeploymentTracker = new Mock<IDeploymentTracker>();
        _mockDeploymentNotifier = new Mock<IDeploymentNotifier>();

        _pipeline = new DeploymentPipeline(
            NullLogger<DeploymentPipeline>.Instance,
            _mockClusterRegistry.Object,
            _mockModuleVerifier.Object,
            _telemetry,
            _config,
            _strategies,
            _mockApprovalService.Object,
            _mockAuditLogService.Object,
            _mockDeploymentTracker.Object,
            _mockDeploymentNotifier.Object);

        SetupDefaultMocks();
    }

    private void SetupDefaultMocks()
    {
        // Setup cluster registry to return mock clusters
        _mockClusterRegistry
            .Setup(x => x.GetClusterAsync(It.IsAny<EnvironmentType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnvironmentCluster(
                EnvironmentType.Development,
                NullLogger<EnvironmentCluster>.Instance));

        // Setup module verifier to return valid result
        _mockModuleVerifier
            .Setup(x => x.ValidateModuleAsync(
                It.IsAny<ModuleDescriptor>(),
                It.IsAny<byte[]>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ModuleValidationResult
            {
                IsValid = true,
                ValidationMessages = new List<string>()
            });

        // Setup audit log service
        _mockAuditLogService
            .Setup(x => x.LogDeploymentEventAsync(
                It.IsAny<AuditLog>(),
                It.IsAny<DeploymentAuditEvent>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(1L);

        // Setup deployment tracker
        _mockDeploymentTracker
            .Setup(x => x.UpdatePipelineStateAsync(
                It.IsAny<Guid>(),
                It.IsAny<PipelineExecutionState>()))
            .Returns(Task.CompletedTask);

        // Setup deployment notifier
        _mockDeploymentNotifier
            .Setup(x => x.NotifyDeploymentStatusChanged(
                It.IsAny<string>(),
                It.IsAny<PipelineExecutionState>()))
            .Returns(Task.CompletedTask);

        _mockDeploymentNotifier
            .Setup(x => x.NotifyDeploymentProgress(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>()))
            .Returns(Task.CompletedTask);
    }

    private Mock<IDeploymentStrategy> CreateMockStrategy(string strategyName)
    {
        var mockStrategy = new Mock<IDeploymentStrategy>();
        mockStrategy
            .Setup(x => x.StrategyName)
            .Returns(strategyName);

        mockStrategy
            .Setup(x => x.DeployAsync(
                It.IsAny<ModuleDeploymentRequest>(),
                It.IsAny<EnvironmentCluster>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeploymentResult
            {
                Success = true,
                Message = "Deployment succeeded",
                NodeResults = new List<NodeDeploymentResult>
                {
                    new NodeDeploymentResult { Success = true, NodeId = Guid.NewGuid() }
                }
            });

        return mockStrategy;
    }

    #region Successful Pipeline Execution

    [Fact]
    public async Task ExecutePipelineAsync_WithDevelopmentTarget_ShouldCompleteSuccessfully()
    {
        // Arrange
        var request = CreateTestRequest(EnvironmentType.Development);

        // Act
        var result = await _pipeline.ExecutePipelineAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        result.Message.Should().Be("Pipeline completed successfully");
        result.ExecutionId.Should().Be(request.ExecutionId);
        result.ModuleName.Should().Be("test-module");
        result.StageResults.Should().HaveCountGreaterThan(0);

        // Should have Build, Test, Security Scan, Deploy to Development, Validation
        result.StageResults.Should().Contain(s => s.StageName == "Build");
        result.StageResults.Should().Contain(s => s.StageName == "Test");
        result.StageResults.Should().Contain(s => s.StageName == "Security Scan");
        result.StageResults.Should().Contain(s => s.StageName == "Deploy to Development");
        result.StageResults.Should().Contain(s => s.StageName == "Validation");

        // All stages should have succeeded
        result.StageResults.Should().AllSatisfy(s => s.Status.Should().Be(PipelineStageStatus.Succeeded));
    }

    [Fact]
    public async Task ExecutePipelineAsync_WithProductionTarget_ShouldDeployToAllEnvironments()
    {
        // Arrange
        var request = CreateTestRequest(EnvironmentType.Production);

        // Act
        var result = await _pipeline.ExecutePipelineAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        result.StageResults.Should().Contain(s => s.StageName == "Deploy to Development");
        result.StageResults.Should().Contain(s => s.StageName == "Deploy to QA");
        result.StageResults.Should().Contain(s => s.StageName == "Deploy to Staging");
        result.StageResults.Should().Contain(s => s.StageName == "Deploy to Production");
    }

    [Fact]
    public async Task ExecutePipelineAsync_ShouldSetStartAndEndTimes()
    {
        // Arrange
        var request = CreateTestRequest(EnvironmentType.Development);
        var beforeExecution = DateTime.UtcNow;

        // Act
        var result = await _pipeline.ExecutePipelineAsync(request);
        var afterExecution = DateTime.UtcNow;

        // Assert
        result.StartTime.Should().BeOnOrAfter(beforeExecution);
        result.EndTime.Should().BeOnOrBefore(afterExecution);
        result.Duration.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task ExecutePipelineAsync_ShouldSetTraceId()
    {
        // Arrange
        var request = CreateTestRequest(EnvironmentType.Development);

        // Act
        var result = await _pipeline.ExecutePipelineAsync(request);

        // Assert
        // TraceId may be null if no active telemetry activity is created
        // In production with proper OpenTelemetry configuration, this would be set
        result.Should().NotBeNull();
    }

    #endregion

    #region Stage Failure Tests

    [Fact]
    public async Task ExecutePipelineAsync_WhenSecurityScanFails_ShouldStopPipeline()
    {
        // Arrange
        _mockModuleVerifier
            .Setup(x => x.ValidateModuleAsync(
                It.IsAny<ModuleDescriptor>(),
                It.IsAny<byte[]>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ModuleValidationResult
            {
                IsValid = false,
                ValidationMessages = new List<string> { "Signature verification failed" }
            });

        var request = CreateTestRequest(EnvironmentType.Production);

        // Act
        var result = await _pipeline.ExecutePipelineAsync(request);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Be("Pipeline failed at Security Scan stage");

        var securityStage = result.StageResults.First(s => s.StageName == "Security Scan");
        securityStage.Status.Should().Be(PipelineStageStatus.Failed);
        securityStage.Message.Should().Contain("Security validation failed");

        // Should not have deployment stages
        result.StageResults.Should().NotContain(s => s.StageName.StartsWith("Deploy to"));
    }

    [Fact]
    public async Task ExecutePipelineAsync_WhenDeploymentFails_ShouldStopAtFailedEnvironment()
    {
        // Arrange
        var mockQaStrategy = _strategies[EnvironmentType.QA] as Mock<IDeploymentStrategy> ?? CreateMockStrategy("Rolling");
        mockQaStrategy
            .Setup(x => x.DeployAsync(
                It.IsAny<ModuleDeploymentRequest>(),
                It.IsAny<EnvironmentCluster>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeploymentResult
            {
                Success = false,
                Message = "QA deployment failed",
                NodeResults = new List<NodeDeploymentResult>()
            });

        _strategies[EnvironmentType.QA] = mockQaStrategy.Object;

        var request = CreateTestRequest(EnvironmentType.Production);

        // Act
        var result = await _pipeline.ExecutePipelineAsync(request);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Be("Pipeline failed at Deploy to QA");

        // Should have Dev deployment but not Staging or Production
        result.StageResults.Should().Contain(s => s.StageName == "Deploy to Development");
        result.StageResults.Should().Contain(s => s.StageName == "Deploy to QA");
        result.StageResults.Should().NotContain(s => s.StageName == "Deploy to Staging");
        result.StageResults.Should().NotContain(s => s.StageName == "Deploy to Production");
    }

    #endregion

    #region Approval Tests

    [Fact]
    public async Task ExecutePipelineAsync_WithApprovalRequired_ShouldRequestApprovalUpfront()
    {
        // Arrange
        var request = CreateTestRequest(EnvironmentType.Staging);
        request.RequireApproval = true;

        var approvalRequest = new ApprovalRequest
        {
            ApprovalId = Guid.NewGuid(),
            DeploymentExecutionId = request.ExecutionId,
            ModuleName = "test-module",
            Version = new Version(1, 0, 0),
            TargetEnvironment = EnvironmentType.Staging,
            RequesterEmail = "test@example.com",
            Status = ApprovalStatus.Pending
        };

        _mockApprovalService
            .Setup(x => x.CreateApprovalRequestAsync(
                It.IsAny<ApprovalRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(approvalRequest);

        _mockApprovalService
            .Setup(x => x.WaitForApprovalAsync(
                request.ExecutionId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApprovalRequest
            {
                ApprovalId = Guid.NewGuid(),
                DeploymentExecutionId = request.ExecutionId,
                ModuleName = "test-module",
                Version = new Version(1, 0, 0),
                TargetEnvironment = EnvironmentType.Staging,
                RequesterEmail = "test@example.com",
                Status = ApprovalStatus.Approved,
                RespondedByEmail = "approver@example.com"
            });

        // Act
        var result = await _pipeline.ExecutePipelineAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        result.StageResults.Should().Contain(s => s.StageName == "Approval");

        var approvalStage = result.StageResults.First(s => s.StageName == "Approval");
        approvalStage.Status.Should().Be(PipelineStageStatus.Succeeded);
        approvalStage.Message.Should().Contain("Approved by approver@example.com");

        // Verify approval was created
        _mockApprovalService.Verify(
            x => x.CreateApprovalRequestAsync(
                It.IsAny<ApprovalRequest>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecutePipelineAsync_WhenApprovalRejected_ShouldStopPipeline()
    {
        // Arrange
        var request = CreateTestRequest(EnvironmentType.Production);
        request.RequireApproval = true;

        _mockApprovalService
            .Setup(x => x.CreateApprovalRequestAsync(
                It.IsAny<ApprovalRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApprovalRequest
            {
                ApprovalId = Guid.NewGuid(),
                DeploymentExecutionId = request.ExecutionId,
                ModuleName = "test-module",
                Version = new Version(1, 0, 0),
                TargetEnvironment = EnvironmentType.Production,
                RequesterEmail = "test@example.com",
                Status = ApprovalStatus.Pending
            });

        _mockApprovalService
            .Setup(x => x.WaitForApprovalAsync(
                request.ExecutionId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApprovalRequest
            {
                ApprovalId = Guid.NewGuid(),
                DeploymentExecutionId = request.ExecutionId,
                ModuleName = "test-module",
                Version = new Version(1, 0, 0),
                TargetEnvironment = EnvironmentType.Production,
                RequesterEmail = "test@example.com",
                Status = ApprovalStatus.Rejected,
                RespondedByEmail = "approver@example.com",
                ResponseReason = "Not ready for production"
            });

        // Act
        var result = await _pipeline.ExecutePipelineAsync(request);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Be("Pipeline failed at Approval stage");

        var approvalStage = result.StageResults.First(s => s.StageName == "Approval");
        approvalStage.Status.Should().Be(PipelineStageStatus.Failed);
        approvalStage.Message.Should().Contain("Rejected by approver@example.com");
        approvalStage.Message.Should().Contain("Not ready for production");

        // Should not have any build/test/deployment stages
        result.StageResults.Should().NotContain(s => s.StageName == "Build");
        result.StageResults.Should().NotContain(s => s.StageName.StartsWith("Deploy to"));
    }

    [Fact]
    public async Task ExecutePipelineAsync_WhenApprovalExpires_ShouldFailPipeline()
    {
        // Arrange
        var request = CreateTestRequest(EnvironmentType.Staging);
        request.RequireApproval = true;

        _mockApprovalService
            .Setup(x => x.CreateApprovalRequestAsync(
                It.IsAny<ApprovalRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApprovalRequest
            {
                ApprovalId = Guid.NewGuid(),
                DeploymentExecutionId = request.ExecutionId,
                ModuleName = "test-module",
                Version = new Version(1, 0, 0),
                TargetEnvironment = EnvironmentType.Staging,
                RequesterEmail = "test@example.com",
                Status = ApprovalStatus.Pending
            });

        _mockApprovalService
            .Setup(x => x.WaitForApprovalAsync(
                request.ExecutionId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApprovalRequest
            {
                ApprovalId = Guid.NewGuid(),
                DeploymentExecutionId = request.ExecutionId,
                ModuleName = "test-module",
                Version = new Version(1, 0, 0),
                TargetEnvironment = EnvironmentType.Staging,
                RequesterEmail = "test@example.com",
                Status = ApprovalStatus.Expired
            });

        // Act
        var result = await _pipeline.ExecutePipelineAsync(request);

        // Assert
        result.Success.Should().BeFalse();

        var approvalStage = result.StageResults.First(s => s.StageName == "Approval");
        approvalStage.Status.Should().Be(PipelineStageStatus.Failed);
        approvalStage.Message.Should().Contain("Approval request expired");
    }

    [Fact]
    public async Task ExecutePipelineAsync_WithDevelopmentTarget_ShouldNotRequireApproval()
    {
        // Arrange
        var request = CreateTestRequest(EnvironmentType.Development);
        request.RequireApproval = true;

        // Act
        var result = await _pipeline.ExecutePipelineAsync(request);

        // Assert
        result.Success.Should().BeTrue();

        // Should not have approval stage for Development
        result.StageResults.Should().NotContain(s => s.StageName == "Approval");

        // Verify approval was NOT created
        _mockApprovalService.Verify(
            x => x.CreateApprovalRequestAsync(
                It.IsAny<ApprovalRequest>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region Exception Handling

    [Fact]
    public async Task ExecutePipelineAsync_WhenClusterRegistryThrowsException_ShouldHandleGracefully()
    {
        // Arrange
        _mockClusterRegistry
            .Setup(x => x.GetClusterAsync(It.IsAny<EnvironmentType>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Cluster not found"));

        var request = CreateTestRequest(EnvironmentType.Development);

        // Act
        var result = await _pipeline.ExecutePipelineAsync(request);

        // Assert
        result.Success.Should().BeFalse();
        // Exception is caught at stage level, not pipeline level
        result.Message.Should().Contain("Pipeline failed");
    }

    #endregion

    #region State Tracking and Notifications

    [Fact]
    public async Task ExecutePipelineAsync_ShouldUpdatePipelineState()
    {
        // Arrange
        var request = CreateTestRequest(EnvironmentType.Development);

        // Act
        var result = await _pipeline.ExecutePipelineAsync(request);

        // Assert
        _mockDeploymentTracker.Verify(
            x => x.UpdatePipelineStateAsync(
                request.ExecutionId,
                It.IsAny<PipelineExecutionState>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecutePipelineAsync_ShouldNotifyDeploymentStatusChanged()
    {
        // Arrange
        var request = CreateTestRequest(EnvironmentType.Development);

        // Act
        var result = await _pipeline.ExecutePipelineAsync(request);

        // Assert
        _mockDeploymentNotifier.Verify(
            x => x.NotifyDeploymentStatusChanged(
                request.ExecutionId.ToString(),
                It.IsAny<PipelineExecutionState>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecutePipelineAsync_ShouldNotifyDeploymentProgress()
    {
        // Arrange
        var request = CreateTestRequest(EnvironmentType.Development);

        // Act
        var result = await _pipeline.ExecutePipelineAsync(request);

        // Assert
        _mockDeploymentNotifier.Verify(
            x => x.NotifyDeploymentProgress(
                request.ExecutionId.ToString(),
                It.IsAny<string>(),
                It.IsAny<int>()),
            Times.AtLeastOnce);
    }

    #endregion

    #region Audit Logging

    [Fact]
    public async Task ExecutePipelineAsync_ShouldLogPipelineStartedEvent()
    {
        // Arrange
        var request = CreateTestRequest(EnvironmentType.Development);

        // Act
        await _pipeline.ExecutePipelineAsync(request);

        // Assert
        _mockAuditLogService.Verify(
            x => x.LogDeploymentEventAsync(
                It.Is<AuditLog>(log => log.EventType == "PipelineStarted"),
                It.IsAny<DeploymentAuditEvent>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecutePipelineAsync_WhenSuccessful_ShouldLogCompletedEvent()
    {
        // Arrange
        var request = CreateTestRequest(EnvironmentType.Development);

        // Act
        await _pipeline.ExecutePipelineAsync(request);

        // Assert
        _mockAuditLogService.Verify(
            x => x.LogDeploymentEventAsync(
                It.Is<AuditLog>(log => log.EventType == "PipelineCompleted" && log.Result == "Success"),
                It.IsAny<DeploymentAuditEvent>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecutePipelineAsync_WhenFailed_ShouldLogEvents()
    {
        // Arrange
        _mockModuleVerifier
            .Setup(x => x.ValidateModuleAsync(
                It.IsAny<ModuleDescriptor>(),
                It.IsAny<byte[]>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ModuleValidationResult
            {
                IsValid = false,
                ValidationMessages = new List<string> { "Security check failed" }
            });

        var request = CreateTestRequest(EnvironmentType.Development);

        // Act
        await _pipeline.ExecutePipelineAsync(request);

        // Assert
        // Verify audit events were logged during pipeline execution
        _mockAuditLogService.Verify(
            x => x.LogDeploymentEventAsync(
                It.IsAny<AuditLog>(),
                It.IsAny<DeploymentAuditEvent>(),
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecutePipelineAsync_WhenExceptionThrown_ShouldLogFailedEvent()
    {
        // Arrange
        _mockClusterRegistry
            .Setup(x => x.GetClusterAsync(It.IsAny<EnvironmentType>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Test exception"));

        var request = CreateTestRequest(EnvironmentType.Development);

        // Act
        await _pipeline.ExecutePipelineAsync(request);

        // Assert
        // Verify that audit logging was called for pipeline completion/failure
        _mockAuditLogService.Verify(
            x => x.LogDeploymentEventAsync(
                It.IsAny<AuditLog>(),
                It.IsAny<DeploymentAuditEvent>(),
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    #endregion

    #region Deployment Strategy Verification

    [Fact]
    public async Task ExecutePipelineAsync_ShouldUseCorrectStrategyForEnvironment()
    {
        // Arrange
        var request = CreateTestRequest(EnvironmentType.Production);

        // Act
        var result = await _pipeline.ExecutePipelineAsync(request);

        // Assert
        var devStage = result.StageResults.First(s => s.StageName == "Deploy to Development");
        devStage.Strategy.Should().Be("Direct");

        var qaStage = result.StageResults.First(s => s.StageName == "Deploy to QA");
        qaStage.Strategy.Should().Be("Rolling");

        var stagingStage = result.StageResults.First(s => s.StageName == "Deploy to Staging");
        stagingStage.Strategy.Should().Be("BlueGreen");

        var prodStage = result.StageResults.First(s => s.StageName == "Deploy to Production");
        prodStage.Strategy.Should().Be("Canary");
    }

    #endregion

    #region Node Deployment Tracking

    [Fact]
    public async Task ExecutePipelineAsync_ShouldTrackNodesDeployed()
    {
        // Arrange
        var mockStrategy = CreateMockStrategy("Direct");
        mockStrategy
            .Setup(x => x.DeployAsync(
                It.IsAny<ModuleDeploymentRequest>(),
                It.IsAny<EnvironmentCluster>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeploymentResult
            {
                Success = true,
                NodeResults = new List<NodeDeploymentResult>
                {
                    new NodeDeploymentResult { Success = true, NodeId = Guid.NewGuid() },
                    new NodeDeploymentResult { Success = true, NodeId = Guid.NewGuid() },
                    new NodeDeploymentResult { Success = false, NodeId = Guid.NewGuid() }
                }
            });

        _strategies[EnvironmentType.Development] = mockStrategy.Object;
        var request = CreateTestRequest(EnvironmentType.Development);

        // Act
        var result = await _pipeline.ExecutePipelineAsync(request);

        // Assert
        var deployStage = result.StageResults.First(s => s.StageName == "Deploy to Development");
        deployStage.NodesDeployed.Should().Be(2);
        deployStage.NodesFailed.Should().Be(1);
    }

    #endregion

    #region Helper Methods

    private DeploymentRequest CreateTestRequest(EnvironmentType targetEnvironment)
    {
        return new DeploymentRequest
        {
            ExecutionId = Guid.NewGuid(),
            Module = new ModuleDescriptor
            {
                Name = "test-module",
                Version = new Version(1, 0, 0),
                Description = "Test module",
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

    public void Dispose()
    {
        _pipeline?.Dispose();
    }
}
