using HotSwap.Distributed.Domain.Models;

namespace HotSwap.Distributed.Orchestrator.Interfaces;

/// <summary>
/// Service for managing deployment approval workflows.
/// </summary>
public interface IApprovalService
{
    /// <summary>
    /// Creates a new approval request for a deployment.
    /// </summary>
    /// <param name="request">The approval request details.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created approval request.</returns>
    Task<ApprovalRequest> CreateApprovalRequestAsync(
        ApprovalRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Approves a deployment.
    /// </summary>
    /// <param name="decision">The approval decision.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated approval request.</returns>
    Task<ApprovalRequest> ApproveDeploymentAsync(
        ApprovalDecision decision,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Rejects a deployment.
    /// </summary>
    /// <param name="decision">The rejection decision.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated approval request.</returns>
    Task<ApprovalRequest> RejectDeploymentAsync(
        ApprovalDecision decision,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an approval request by deployment execution ID.
    /// </summary>
    /// <param name="deploymentExecutionId">The deployment execution ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The approval request, or null if not found.</returns>
    Task<ApprovalRequest?> GetApprovalRequestAsync(
        Guid deploymentExecutionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all pending approval requests.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of pending approval requests.</returns>
    Task<List<ApprovalRequest>> GetPendingApprovalsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Waits for an approval decision for a deployment.
    /// </summary>
    /// <param name="deploymentExecutionId">The deployment execution ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The final approval request (approved, rejected, or expired).</returns>
    Task<ApprovalRequest> WaitForApprovalAsync(
        Guid deploymentExecutionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Processes expired approval requests and auto-rejects them.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of approvals that were auto-rejected.</returns>
    Task<int> ProcessExpiredApprovalsAsync(
        CancellationToken cancellationToken = default);
}
