using FluentAssertions;
using HotSwap.Distributed.Api.Controllers;
using HotSwap.Distributed.Api.Models;
using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Orchestrator.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace HotSwap.Distributed.Tests.Api;

public class ApprovalsControllerTests
{
    private readonly Mock<IApprovalService> _mockApprovalService;
    private readonly ApprovalsController _controller;

    public ApprovalsControllerTests()
    {
        _mockApprovalService = new Mock<IApprovalService>();
        _controller = new ApprovalsController(
            _mockApprovalService.Object,
            NullLogger<ApprovalsController>.Instance);
    }

    private ApprovalRequest CreateTestApprovalRequest(
        Guid? executionId = null,
        ApprovalStatus status = ApprovalStatus.Pending,
        DateTime? timeoutAt = null)
    {
        return new ApprovalRequest
        {
            ApprovalId = Guid.NewGuid(),
            DeploymentExecutionId = executionId ?? Guid.NewGuid(),
            ModuleName = "test-module",
            Version = new Version(1, 0, 0),
            TargetEnvironment = EnvironmentType.Production,
            RequesterEmail = "requester@test.com",
            Status = status,
            RequestedAt = DateTime.UtcNow.AddHours(-1),
            TimeoutAt = timeoutAt ?? DateTime.UtcNow.AddHours(23),
            ApproverEmails = new List<string> { "approver1@test.com", "approver2@test.com" }
        };
    }

    #region GetPendingApprovals Tests

    [Fact]
    public async Task GetPendingApprovals_WithMultiplePendingApprovals_ReturnsOk()
    {
        // Arrange
        var approvals = new List<ApprovalRequest>
        {
            CreateTestApprovalRequest(),
            CreateTestApprovalRequest()
        };

        _mockApprovalService.Setup(x => x.GetPendingApprovalsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(approvals);

        // Act
        var result = await _controller.GetPendingApprovals(CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var summaries = okResult.Value.Should().BeOfType<List<PendingApprovalSummary>>().Subject;
        summaries.Should().HaveCount(2);
        summaries.Should().AllSatisfy(s =>
        {
            s.ApprovalId.Should().NotBeEmpty();
            s.ModuleName.Should().Be("test-module");
            s.Version.Should().Be("1.0.0");
            s.TargetEnvironment.Should().Be("Production");
            s.RequesterEmail.Should().Be("requester@test.com");
        });
    }

    [Fact]
    public async Task GetPendingApprovals_WithNoPendingApprovals_ReturnsEmptyList()
    {
        // Arrange
        _mockApprovalService.Setup(x => x.GetPendingApprovalsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ApprovalRequest>());

        // Act
        var result = await _controller.GetPendingApprovals(CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var summaries = okResult.Value.Should().BeOfType<List<PendingApprovalSummary>>().Subject;
        summaries.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPendingApprovals_FormatsTimeRemaining_ForDaysAndHours()
    {
        // Arrange - More than 1 day remaining
        var approval = CreateTestApprovalRequest(timeoutAt: DateTime.UtcNow.AddDays(2).AddHours(3));

        _mockApprovalService.Setup(x => x.GetPendingApprovalsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ApprovalRequest> { approval });

        // Act
        var result = await _controller.GetPendingApprovals(CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var summaries = okResult.Value.Should().BeOfType<List<PendingApprovalSummary>>().Subject;
        summaries.Should().HaveCount(1);
        summaries[0].TimeRemaining.Should().MatchRegex(@"^\d+d \d+h$");
    }

    [Fact]
    public async Task GetPendingApprovals_FormatsTimeRemaining_ForHoursAndMinutes()
    {
        // Arrange - Less than 1 day, more than 1 hour remaining
        var approval = CreateTestApprovalRequest(timeoutAt: DateTime.UtcNow.AddHours(5).AddMinutes(30));

        _mockApprovalService.Setup(x => x.GetPendingApprovalsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ApprovalRequest> { approval });

        // Act
        var result = await _controller.GetPendingApprovals(CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var summaries = okResult.Value.Should().BeOfType<List<PendingApprovalSummary>>().Subject;
        summaries.Should().HaveCount(1);
        summaries[0].TimeRemaining.Should().MatchRegex(@"^\d+h \d+m$");
    }

    [Fact]
    public async Task GetPendingApprovals_FormatsTimeRemaining_ForMinutesOnly()
    {
        // Arrange - Less than 1 hour remaining
        var approval = CreateTestApprovalRequest(timeoutAt: DateTime.UtcNow.AddMinutes(45));

        _mockApprovalService.Setup(x => x.GetPendingApprovalsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ApprovalRequest> { approval });

        // Act
        var result = await _controller.GetPendingApprovals(CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var summaries = okResult.Value.Should().BeOfType<List<PendingApprovalSummary>>().Subject;
        summaries.Should().HaveCount(1);
        summaries[0].TimeRemaining.Should().MatchRegex(@"^\d+m$");
    }

    [Fact]
    public async Task GetPendingApprovals_FormatsTimeRemaining_AsExpired()
    {
        // Arrange - Already expired
        var approval = CreateTestApprovalRequest(timeoutAt: DateTime.UtcNow.AddHours(-1));

        _mockApprovalService.Setup(x => x.GetPendingApprovalsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ApprovalRequest> { approval });

        // Act
        var result = await _controller.GetPendingApprovals(CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var summaries = okResult.Value.Should().BeOfType<List<PendingApprovalSummary>>().Subject;
        summaries.Should().HaveCount(1);
        summaries[0].TimeRemaining.Should().Be("Expired");
    }

    #endregion

    #region GetApprovalRequest Tests

    [Fact]
    public async Task GetApprovalRequest_WithExistingApproval_ReturnsOk()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        var approval = CreateTestApprovalRequest(executionId);

        _mockApprovalService.Setup(x => x.GetApprovalRequestAsync(executionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(approval);

        // Act
        var result = await _controller.GetApprovalRequest(executionId, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApprovalResponse>().Subject;
        response.ApprovalId.Should().Be(approval.ApprovalId);
        response.DeploymentExecutionId.Should().Be(executionId);
        response.ModuleName.Should().Be("test-module");
        response.Version.Should().Be("1.0.0");
        response.TargetEnvironment.Should().Be("Production");
        response.RequesterEmail.Should().Be("requester@test.com");
        response.Status.Should().Be("Pending");
    }

    [Fact]
    public async Task GetApprovalRequest_WithApprovedStatus_ReturnsApprovedDetails()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        var approval = CreateTestApprovalRequest(executionId, ApprovalStatus.Approved);
        approval.RespondedAt = DateTime.UtcNow;
        approval.RespondedByEmail = "admin@test.com";
        approval.ResponseReason = "Looks good";

        _mockApprovalService.Setup(x => x.GetApprovalRequestAsync(executionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(approval);

        // Act
        var result = await _controller.GetApprovalRequest(executionId, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApprovalResponse>().Subject;
        response.Status.Should().Be("Approved");
        response.RespondedAt.Should().NotBeNull();
        response.RespondedBy.Should().Be("admin@test.com");
        response.ResponseReason.Should().Be("Looks good");
    }

    [Fact]
    public async Task GetApprovalRequest_WithNonExistentApproval_ThrowsKeyNotFoundException()
    {
        // Arrange
        var executionId = Guid.NewGuid();

        _mockApprovalService.Setup(x => x.GetApprovalRequestAsync(executionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ApprovalRequest?)null);

        // Act
        Func<Task> act = async () => await _controller.GetApprovalRequest(executionId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage($"Approval request not found for deployment {executionId}");
    }

    #endregion

    #region ApproveDeployment Tests

    [Fact]
    public async Task ApproveDeployment_WithValidRequest_ReturnsOk()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        var request = new ApprovalDecisionRequest
        {
            ApproverEmail = "admin@test.com",
            Reason = "Deployment approved"
        };

        var approval = CreateTestApprovalRequest(executionId, ApprovalStatus.Approved);
        approval.RespondedAt = DateTime.UtcNow;
        approval.RespondedByEmail = request.ApproverEmail;
        approval.ResponseReason = request.Reason;

        _mockApprovalService.Setup(x => x.ApproveDeploymentAsync(
                It.Is<ApprovalDecision>(d =>
                    d.DeploymentExecutionId == executionId &&
                    d.ApproverEmail == request.ApproverEmail &&
                    d.Approved == true &&
                    d.Reason == request.Reason),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(approval);

        // Act
        var result = await _controller.ApproveDeployment(executionId, request, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApprovalResponse>().Subject;
        response.Status.Should().Be("Approved");
        response.RespondedBy.Should().Be(request.ApproverEmail);
        response.ResponseReason.Should().Be(request.Reason);
        response.DeploymentExecutionId.Should().Be(executionId);

        _mockApprovalService.Verify(x => x.ApproveDeploymentAsync(
            It.Is<ApprovalDecision>(d => d.Approved == true),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ApproveDeployment_CreatesDecisionWithCorrectProperties()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        var request = new ApprovalDecisionRequest
        {
            ApproverEmail = "admin@test.com",
            Reason = "Security review passed"
        };

        var approval = CreateTestApprovalRequest(executionId, ApprovalStatus.Approved);

        ApprovalDecision? capturedDecision = null;
        _mockApprovalService.Setup(x => x.ApproveDeploymentAsync(
                It.IsAny<ApprovalDecision>(),
                It.IsAny<CancellationToken>()))
            .Callback<ApprovalDecision, CancellationToken>((d, ct) => capturedDecision = d)
            .ReturnsAsync(approval);

        // Act
        await _controller.ApproveDeployment(executionId, request, CancellationToken.None);

        // Assert
        capturedDecision.Should().NotBeNull();
        capturedDecision!.DeploymentExecutionId.Should().Be(executionId);
        capturedDecision.ApproverEmail.Should().Be(request.ApproverEmail);
        capturedDecision.Approved.Should().BeTrue();
        capturedDecision.Reason.Should().Be(request.Reason);
    }

    [Fact]
    public async Task ApproveDeployment_WithNullReason_AcceptsRequest()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        var request = new ApprovalDecisionRequest
        {
            ApproverEmail = "admin@test.com",
            Reason = null
        };

        var approval = CreateTestApprovalRequest(executionId, ApprovalStatus.Approved);

        _mockApprovalService.Setup(x => x.ApproveDeploymentAsync(
                It.IsAny<ApprovalDecision>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(approval);

        // Act
        var result = await _controller.ApproveDeployment(executionId, request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockApprovalService.Verify(x => x.ApproveDeploymentAsync(
            It.Is<ApprovalDecision>(d => d.Reason == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region RejectDeployment Tests

    [Fact]
    public async Task RejectDeployment_WithValidRequest_ReturnsOk()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        var request = new ApprovalDecisionRequest
        {
            ApproverEmail = "admin@test.com",
            Reason = "Failed security review"
        };

        var approval = CreateTestApprovalRequest(executionId, ApprovalStatus.Rejected);
        approval.RespondedAt = DateTime.UtcNow;
        approval.RespondedByEmail = request.ApproverEmail;
        approval.ResponseReason = request.Reason;

        _mockApprovalService.Setup(x => x.RejectDeploymentAsync(
                It.Is<ApprovalDecision>(d =>
                    d.DeploymentExecutionId == executionId &&
                    d.ApproverEmail == request.ApproverEmail &&
                    d.Approved == false &&
                    d.Reason == request.Reason),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(approval);

        // Act
        var result = await _controller.RejectDeployment(executionId, request, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApprovalResponse>().Subject;
        response.Status.Should().Be("Rejected");
        response.RespondedBy.Should().Be(request.ApproverEmail);
        response.ResponseReason.Should().Be(request.Reason);
        response.DeploymentExecutionId.Should().Be(executionId);

        _mockApprovalService.Verify(x => x.RejectDeploymentAsync(
            It.Is<ApprovalDecision>(d => d.Approved == false),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RejectDeployment_CreatesDecisionWithCorrectProperties()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        var request = new ApprovalDecisionRequest
        {
            ApproverEmail = "admin@test.com",
            Reason = "Deployment conflicts with maintenance window"
        };

        var approval = CreateTestApprovalRequest(executionId, ApprovalStatus.Rejected);

        ApprovalDecision? capturedDecision = null;
        _mockApprovalService.Setup(x => x.RejectDeploymentAsync(
                It.IsAny<ApprovalDecision>(),
                It.IsAny<CancellationToken>()))
            .Callback<ApprovalDecision, CancellationToken>((d, ct) => capturedDecision = d)
            .ReturnsAsync(approval);

        // Act
        await _controller.RejectDeployment(executionId, request, CancellationToken.None);

        // Assert
        capturedDecision.Should().NotBeNull();
        capturedDecision!.DeploymentExecutionId.Should().Be(executionId);
        capturedDecision.ApproverEmail.Should().Be(request.ApproverEmail);
        capturedDecision.Approved.Should().BeFalse();
        capturedDecision.Reason.Should().Be(request.Reason);
    }

    [Fact]
    public async Task RejectDeployment_WithNullReason_AcceptsRequest()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        var request = new ApprovalDecisionRequest
        {
            ApproverEmail = "admin@test.com",
            Reason = null
        };

        var approval = CreateTestApprovalRequest(executionId, ApprovalStatus.Rejected);

        _mockApprovalService.Setup(x => x.RejectDeploymentAsync(
                It.IsAny<ApprovalDecision>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(approval);

        // Act
        var result = await _controller.RejectDeployment(executionId, request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockApprovalService.Verify(x => x.RejectDeploymentAsync(
            It.Is<ApprovalDecision>(d => d.Reason == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion
}
