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

    /// <summary>
    /// Sends a notification when a secret is automatically rotated.
    /// </summary>
    /// <param name="recipients">Email addresses of administrators to notify.</param>
    /// <param name="secretId">The ID of the rotated secret.</param>
    /// <param name="previousVersion">The previous secret version.</param>
    /// <param name="newVersion">The new secret version.</param>
    /// <param name="rotationWindowEndsAt">When the rotation window ends (both versions valid until then).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SendSecretRotationNotificationAsync(
        IEnumerable<string> recipients,
        string secretId,
        int previousVersion,
        int newVersion,
        DateTime rotationWindowEndsAt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a warning notification when a secret is approaching expiration.
    /// </summary>
    /// <param name="recipients">Email addresses of administrators to notify.</param>
    /// <param name="secretId">The ID of the expiring secret.</param>
    /// <param name="daysRemaining">Number of days until expiration.</param>
    /// <param name="expiresAt">When the secret expires.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SendSecretExpirationWarningAsync(
        IEnumerable<string> recipients,
        string secretId,
        int daysRemaining,
        DateTime expiresAt,
        CancellationToken cancellationToken = default);
}
