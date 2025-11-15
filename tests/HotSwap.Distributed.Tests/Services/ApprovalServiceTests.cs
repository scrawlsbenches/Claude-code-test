using FluentAssertions;
using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Interfaces;
using HotSwap.Distributed.Orchestrator.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace HotSwap.Distributed.Tests.Services;

public class ApprovalServiceTests
{
    private readonly Mock<ILogger<ApprovalService>> _mockLogger;
    private readonly Mock<INotificationService> _mockNotificationService;
    private readonly PipelineConfiguration _config;
    private readonly ApprovalService _approvalService;

    public ApprovalServiceTests()
    {
        _mockLogger = new Mock<ILogger<ApprovalService>>();
        _mockNotificationService = new Mock<INotificationService>();
        _config = new PipelineConfiguration
        {
            ApprovalTimeoutHours = 24
        };

        _approvalService = new ApprovalService(
            _mockLogger.Object,
            _config,
            _mockNotificationService.Object);
    }

    [Fact]
    public async Task CreateApprovalRequestAsync_ShouldCreateApprovalRequest()
    {
        // Arrange
        var request = new ApprovalRequest
        {
            DeploymentExecutionId = Guid.NewGuid(),
            ModuleName = "TestModule",
            Version = new Version(1, 0, 0),
            TargetEnvironment = EnvironmentType.Production,
            RequesterEmail = "requester@example.com",
            ApproverEmails = new List<string> { "approver@example.com" }
        };

        // Act
        var result = await _approvalService.CreateApprovalRequestAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.ApprovalId.Should().NotBeEmpty();
        result.Status.Should().Be(ApprovalStatus.Pending);
        result.TimeoutAt.Should().BeAfter(DateTime.UtcNow);
        result.TimeoutAt.Should().BeCloseTo(DateTime.UtcNow.AddHours(24), TimeSpan.FromSeconds(10));

        _mockNotificationService.Verify(
            x => x.SendApprovalRequestNotificationAsync(It.IsAny<ApprovalRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateApprovalRequestAsync_ShouldThrow_WhenDuplicateRequest()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        var request = new ApprovalRequest
        {
            DeploymentExecutionId = executionId,
            ModuleName = "TestModule",
            Version = new Version(1, 0, 0),
            TargetEnvironment = EnvironmentType.Production,
            RequesterEmail = "requester@example.com"
        };

        await _approvalService.CreateApprovalRequestAsync(request);

        var duplicateRequest = new ApprovalRequest
        {
            DeploymentExecutionId = executionId,
            ModuleName = "TestModule",
            Version = new Version(1, 0, 0),
            TargetEnvironment = EnvironmentType.Production,
            RequesterEmail = "requester@example.com"
        };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await _approvalService.CreateApprovalRequestAsync(duplicateRequest));
    }

    [Fact]
    public async Task ApproveDeploymentAsync_ShouldApproveDeployment()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        var approvalRequest = new ApprovalRequest
        {
            DeploymentExecutionId = executionId,
            ModuleName = "TestModule",
            Version = new Version(1, 0, 0),
            TargetEnvironment = EnvironmentType.Production,
            RequesterEmail = "requester@example.com",
            ApproverEmails = new List<string> { "approver@example.com" }
        };

        await _approvalService.CreateApprovalRequestAsync(approvalRequest);

        var decision = new ApprovalDecision
        {
            DeploymentExecutionId = executionId,
            ApproverEmail = "approver@example.com",
            Approved = true,
            Reason = "Looks good to deploy"
        };

        // Act
        var result = await _approvalService.ApproveDeploymentAsync(decision);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ApprovalStatus.Approved);
        result.RespondedByEmail.Should().Be("approver@example.com");
        result.ResponseReason.Should().Be("Looks good to deploy");
        result.RespondedAt.Should().NotBeNull();

        _mockNotificationService.Verify(
            x => x.SendApprovalGrantedNotificationAsync(It.IsAny<ApprovalRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ApproveDeploymentAsync_ShouldThrow_WhenUnauthorizedApprover()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        var approvalRequest = new ApprovalRequest
        {
            DeploymentExecutionId = executionId,
            ModuleName = "TestModule",
            Version = new Version(1, 0, 0),
            TargetEnvironment = EnvironmentType.Production,
            RequesterEmail = "requester@example.com",
            ApproverEmails = new List<string> { "authorized@example.com" }
        };

        await _approvalService.CreateApprovalRequestAsync(approvalRequest);

        var decision = new ApprovalDecision
        {
            DeploymentExecutionId = executionId,
            ApproverEmail = "unauthorized@example.com",
            Approved = true,
            Reason = "Trying to approve"
        };

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            async () => await _approvalService.ApproveDeploymentAsync(decision));
    }

    [Fact]
    public async Task RejectDeploymentAsync_ShouldRejectDeployment()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        var approvalRequest = new ApprovalRequest
        {
            DeploymentExecutionId = executionId,
            ModuleName = "TestModule",
            Version = new Version(1, 0, 0),
            TargetEnvironment = EnvironmentType.Production,
            RequesterEmail = "requester@example.com",
            ApproverEmails = new List<string> { "approver@example.com" }
        };

        await _approvalService.CreateApprovalRequestAsync(approvalRequest);

        var decision = new ApprovalDecision
        {
            DeploymentExecutionId = executionId,
            ApproverEmail = "approver@example.com",
            Approved = false,
            Reason = "Failed security review"
        };

        // Act
        var result = await _approvalService.RejectDeploymentAsync(decision);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ApprovalStatus.Rejected);
        result.RespondedByEmail.Should().Be("approver@example.com");
        result.ResponseReason.Should().Be("Failed security review");
        result.RespondedAt.Should().NotBeNull();

        _mockNotificationService.Verify(
            x => x.SendApprovalRejectedNotificationAsync(It.IsAny<ApprovalRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetApprovalRequestAsync_ShouldReturnApprovalRequest()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        var approvalRequest = new ApprovalRequest
        {
            DeploymentExecutionId = executionId,
            ModuleName = "TestModule",
            Version = new Version(1, 0, 0),
            TargetEnvironment = EnvironmentType.Production,
            RequesterEmail = "requester@example.com"
        };

        await _approvalService.CreateApprovalRequestAsync(approvalRequest);

        // Act
        var result = await _approvalService.GetApprovalRequestAsync(executionId);

        // Assert
        result.Should().NotBeNull();
        result!.DeploymentExecutionId.Should().Be(executionId);
        result.ModuleName.Should().Be("TestModule");
    }

    [Fact]
    public async Task GetApprovalRequestAsync_ShouldReturnNull_WhenNotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await _approvalService.GetApprovalRequestAsync(nonExistentId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetPendingApprovalsAsync_ShouldReturnOnlyPendingApprovals()
    {
        // Arrange
        var pendingId = Guid.NewGuid();
        var approvedId = Guid.NewGuid();

        var pendingRequest = new ApprovalRequest
        {
            DeploymentExecutionId = pendingId,
            ModuleName = "PendingModule",
            Version = new Version(1, 0, 0),
            TargetEnvironment = EnvironmentType.Production,
            RequesterEmail = "requester@example.com"
        };

        var approvedRequest = new ApprovalRequest
        {
            DeploymentExecutionId = approvedId,
            ModuleName = "ApprovedModule",
            Version = new Version(1, 0, 0),
            TargetEnvironment = EnvironmentType.Production,
            RequesterEmail = "requester@example.com"
        };

        await _approvalService.CreateApprovalRequestAsync(pendingRequest);
        await _approvalService.CreateApprovalRequestAsync(approvedRequest);

        await _approvalService.ApproveDeploymentAsync(new ApprovalDecision
        {
            DeploymentExecutionId = approvedId,
            ApproverEmail = "approver@example.com",
            Approved = true
        });

        // Act
        var pending = await _approvalService.GetPendingApprovalsAsync();

        // Assert
        pending.Should().HaveCount(1);
        pending.First().DeploymentExecutionId.Should().Be(pendingId);
    }

    [Fact]
    public async Task ProcessExpiredApprovalsAsync_ShouldExpireTimedOutRequests()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        var approvalRequest = new ApprovalRequest
        {
            DeploymentExecutionId = executionId,
            ModuleName = "TestModule",
            Version = new Version(1, 0, 0),
            TargetEnvironment = EnvironmentType.Production,
            RequesterEmail = "requester@example.com",
            TimeoutAt = DateTime.UtcNow.AddSeconds(-1) // Already expired
        };

        await _approvalService.CreateApprovalRequestAsync(approvalRequest);

        // Act
        var expiredCount = await _approvalService.ProcessExpiredApprovalsAsync();

        // Assert
        expiredCount.Should().Be(1);

        var request = await _approvalService.GetApprovalRequestAsync(executionId);
        request.Should().NotBeNull();
        request!.Status.Should().Be(ApprovalStatus.Expired);
        request.ResponseReason.Should().Contain("timeout");

        _mockNotificationService.Verify(
            x => x.SendApprovalExpiredNotificationAsync(It.IsAny<ApprovalRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ApprovalRequest_ShouldHaveCorrectPropertiesAfterCreation()
    {
        // Arrange
        var request = new ApprovalRequest
        {
            DeploymentExecutionId = Guid.NewGuid(),
            ModuleName = "TestModule",
            Version = new Version(1, 0, 0),
            TargetEnvironment = EnvironmentType.Staging,
            RequesterEmail = "requester@example.com",
            ApproverEmails = new List<string> { "approver1@example.com", "approver2@example.com" },
            Metadata = new Dictionary<string, string>
            {
                ["JiraTicket"] = "PROJ-123",
                ["ReleaseNotes"] = "Bug fixes"
            }
        };

        // Act
        var result = await _approvalService.CreateApprovalRequestAsync(request);

        // Assert
        result.IsPending.Should().BeTrue();
        result.IsResolved.Should().BeFalse();
        result.IsExpired.Should().BeFalse();
        result.Status.Should().Be(ApprovalStatus.Pending);
        result.ApproverEmails.Should().HaveCount(2);
        result.Metadata.Should().ContainKey("JiraTicket");
    }
}
