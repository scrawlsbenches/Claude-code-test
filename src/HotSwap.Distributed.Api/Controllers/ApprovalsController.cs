using HotSwap.Distributed.Api.Models;
using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Orchestrator.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotSwap.Distributed.Api.Controllers;

/// <summary>
/// API endpoints for managing deployment approvals.
/// Requires authentication. Approval/rejection requires Admin role.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
[Authorize] // All endpoints require authentication
public class ApprovalsController : ControllerBase
{
    private readonly IApprovalService _approvalService;
    private readonly ILogger<ApprovalsController> _logger;

    public ApprovalsController(
        IApprovalService approvalService,
        ILogger<ApprovalsController> logger)
    {
        _approvalService = approvalService;
        _logger = logger;
    }

    /// <summary>
    /// Gets all pending approval requests.
    /// Available to all authenticated users.
    /// </summary>
    /// <returns>List of pending approval requests</returns>
    /// <response code="200">Pending approvals retrieved</response>
    /// <response code="401">Unauthorized - authentication required</response>
    [HttpGet("pending")]
    [Authorize(Roles = "Viewer,Deployer,Admin")]
    [ProducesResponseType(typeof(List<PendingApprovalSummary>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetPendingApprovals(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Retrieving pending approval requests");

        var pendingApprovals = await _approvalService.GetPendingApprovalsAsync(cancellationToken);

        var response = pendingApprovals.Select(a => new PendingApprovalSummary
        {
            ApprovalId = a.ApprovalId,
            DeploymentExecutionId = a.DeploymentExecutionId,
            ModuleName = a.ModuleName,
            Version = a.Version.ToString(),
            TargetEnvironment = a.TargetEnvironment.ToString(),
            RequesterEmail = a.RequesterEmail,
            RequestedAt = a.RequestedAt,
            TimeoutAt = a.TimeoutAt,
            TimeRemaining = FormatTimeRemaining(a.TimeoutAt - DateTime.UtcNow)
        }).ToList();

        return Ok(response);
    }

    /// <summary>
    /// Gets approval request details for a deployment.
    /// Available to all authenticated users.
    /// </summary>
    /// <param name="executionId">Deployment execution ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Approval request details</returns>
    /// <response code="200">Approval request found</response>
    /// <response code="401">Unauthorized - authentication required</response>
    /// <response code="404">Approval request not found</response>
    [HttpGet("deployments/{executionId}")]
    [Authorize(Roles = "Viewer,Deployer,Admin")]
    [ProducesResponseType(typeof(ApprovalResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetApprovalRequest(
        Guid executionId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting approval request for deployment {ExecutionId}", executionId);

        var approval = await _approvalService.GetApprovalRequestAsync(executionId, cancellationToken);

        if (approval == null)
        {
            throw new KeyNotFoundException($"Approval request not found for deployment {executionId}");
        }

        var response = new ApprovalResponse
        {
            ApprovalId = approval.ApprovalId,
            DeploymentExecutionId = approval.DeploymentExecutionId,
            ModuleName = approval.ModuleName,
            Version = approval.Version.ToString(),
            TargetEnvironment = approval.TargetEnvironment.ToString(),
            RequesterEmail = approval.RequesterEmail,
            Status = approval.Status.ToString(),
            RequestedAt = approval.RequestedAt,
            RespondedAt = approval.RespondedAt,
            RespondedBy = approval.RespondedByEmail,
            ResponseReason = approval.ResponseReason,
            TimeoutAt = approval.TimeoutAt
        };

        return Ok(response);
    }

    /// <summary>
    /// Approves a deployment.
    /// Requires Admin role.
    /// </summary>
    /// <param name="executionId">Deployment execution ID</param>
    /// <param name="request">Approval decision</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated approval request</returns>
    /// <response code="200">Deployment approved</response>
    /// <response code="400">Invalid request</response>
    /// <response code="401">Unauthorized - authentication required</response>
    /// <response code="403">Forbidden - requires Admin role</response>
    /// <response code="404">Approval request not found</response>
    [HttpPost("deployments/{executionId}/approve")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApprovalResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ApproveDeployment(
        Guid executionId,
        [FromBody] ApprovalDecisionRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Processing approval for deployment {ExecutionId} by {Approver}",
            executionId, request.ApproverEmail);

        var decision = new ApprovalDecision
        {
            DeploymentExecutionId = executionId,
            ApproverEmail = request.ApproverEmail,
            Approved = true,
            Reason = request.Reason
        };

        var approval = await _approvalService.ApproveDeploymentAsync(decision, cancellationToken);

        var response = new ApprovalResponse
        {
            ApprovalId = approval.ApprovalId,
            DeploymentExecutionId = approval.DeploymentExecutionId,
            ModuleName = approval.ModuleName,
            Version = approval.Version.ToString(),
            TargetEnvironment = approval.TargetEnvironment.ToString(),
            RequesterEmail = approval.RequesterEmail,
            Status = approval.Status.ToString(),
            RequestedAt = approval.RequestedAt,
            RespondedAt = approval.RespondedAt,
            RespondedBy = approval.RespondedByEmail,
            ResponseReason = approval.ResponseReason,
            TimeoutAt = approval.TimeoutAt
        };

        return Ok(response);
    }

    /// <summary>
    /// Rejects a deployment.
    /// Requires Admin role.
    /// </summary>
    /// <param name="executionId">Deployment execution ID</param>
    /// <param name="request">Rejection decision</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated approval request</returns>
    /// <response code="200">Deployment rejected</response>
    /// <response code="400">Invalid request</response>
    /// <response code="401">Unauthorized - authentication required</response>
    /// <response code="403">Forbidden - requires Admin role</response>
    /// <response code="404">Approval request not found</response>
    [HttpPost("deployments/{executionId}/reject")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApprovalResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RejectDeployment(
        Guid executionId,
        [FromBody] ApprovalDecisionRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Processing rejection for deployment {ExecutionId} by {Approver}",
            executionId, request.ApproverEmail);

        var decision = new ApprovalDecision
        {
            DeploymentExecutionId = executionId,
            ApproverEmail = request.ApproverEmail,
            Approved = false,
            Reason = request.Reason
        };

        var approval = await _approvalService.RejectDeploymentAsync(decision, cancellationToken);

        var response = new ApprovalResponse
        {
            ApprovalId = approval.ApprovalId,
            DeploymentExecutionId = approval.DeploymentExecutionId,
            ModuleName = approval.ModuleName,
            Version = approval.Version.ToString(),
            TargetEnvironment = approval.TargetEnvironment.ToString(),
            RequesterEmail = approval.RequesterEmail,
            Status = approval.Status.ToString(),
            RequestedAt = approval.RequestedAt,
            RespondedAt = approval.RespondedAt,
            RespondedBy = approval.RespondedByEmail,
            ResponseReason = approval.ResponseReason,
            TimeoutAt = approval.TimeoutAt
        };

        return Ok(response);
    }

    /// <summary>
    /// Formats a timespan into a human-readable string.
    /// </summary>
    private static string FormatTimeRemaining(TimeSpan timeRemaining)
    {
        if (timeRemaining <= TimeSpan.Zero)
        {
            return "Expired";
        }

        if (timeRemaining.TotalDays >= 1)
        {
            return $"{(int)timeRemaining.TotalDays}d {timeRemaining.Hours}h";
        }

        if (timeRemaining.TotalHours >= 1)
        {
            return $"{(int)timeRemaining.TotalHours}h {timeRemaining.Minutes}m";
        }

        return $"{(int)timeRemaining.TotalMinutes}m";
    }
}
