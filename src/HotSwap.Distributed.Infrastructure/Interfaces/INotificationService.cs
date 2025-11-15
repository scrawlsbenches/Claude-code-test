using HotSwap.Distributed.Domain.Models;

namespace HotSwap.Distributed.Infrastructure.Interfaces;

/// <summary>
/// Service for sending notifications (email, Slack, etc.) to users.
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Sends an approval request notification to approvers.
    /// </summary>
    /// <param name="approval">The approval request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SendApprovalRequestNotificationAsync(
        ApprovalRequest approval,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a notification when a deployment is approved.
    /// </summary>
    /// <param name="approval">The approval request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SendApprovalGrantedNotificationAsync(
        ApprovalRequest approval,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a notification when a deployment is rejected.
    /// </summary>
    /// <param name="approval">The approval request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SendApprovalRejectedNotificationAsync(
        ApprovalRequest approval,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a notification when an approval request expires.
    /// </summary>
    /// <param name="approval">The approval request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SendApprovalExpiredNotificationAsync(
        ApprovalRequest approval,
        CancellationToken cancellationToken = default);
}
