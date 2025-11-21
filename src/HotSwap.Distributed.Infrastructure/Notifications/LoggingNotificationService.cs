using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Interfaces;
using Microsoft.Extensions.Logging;

namespace HotSwap.Distributed.Infrastructure.Notifications;

/// <summary>
/// Notification service that logs notifications instead of sending emails.
/// In production, this would be replaced with a real email service (SendGrid, SMTP, etc.).
/// </summary>
public class LoggingNotificationService : INotificationService
{
    private readonly ILogger<LoggingNotificationService> _logger;

    public LoggingNotificationService(ILogger<LoggingNotificationService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task SendApprovalRequestNotificationAsync(
        ApprovalRequest approval,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "📧 [EMAIL] Approval Request Notification\n" +
            "To: {Approvers}\n" +
            "Subject: Approval Required: Deploy {ModuleName} v{Version} to {Environment}\n" +
            "---\n" +
            "A deployment requires your approval:\n" +
            "  Module: {ModuleName} v{Version}\n" +
            "  Environment: {Environment}\n" +
            "  Requester: {Requester}\n" +
            "  Approval ID: {ApprovalId}\n" +
            "  Deployment ID: {DeploymentId}\n" +
            "  Timeout: {Timeout}\n" +
            "\n" +
            "To approve or reject:\n" +
            "  POST /api/v1/approvals/deployments/{DeploymentId}/approve\n" +
            "  POST /api/v1/approvals/deployments/{DeploymentId}/reject\n" +
            "---",
            string.Join(", ", approval.ApproverEmails.Any() ? approval.ApproverEmails : new[] { "No specific approvers configured" }),
            approval.ModuleName,
            approval.Version,
            approval.TargetEnvironment,
            approval.ModuleName,
            approval.Version,
            approval.TargetEnvironment,
            approval.RequesterEmail,
            approval.ApprovalId,
            approval.DeploymentExecutionId,
            approval.TimeoutAt,
            approval.DeploymentExecutionId,
            approval.DeploymentExecutionId);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SendApprovalGrantedNotificationAsync(
        ApprovalRequest approval,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "📧 [EMAIL] Deployment Approved Notification\n" +
            "To: {Requester}\n" +
            "Subject: Approved: Deploy {ModuleName} v{Version} to {Environment}\n" +
            "---\n" +
            "Your deployment has been approved:\n" +
            "  Module: {ModuleName} v{Version}\n" +
            "  Environment: {Environment}\n" +
            "  Approved By: {Approver}\n" +
            "  Reason: {Reason}\n" +
            "  Deployment ID: {DeploymentId}\n" +
            "\n" +
            "The deployment will now proceed to {Environment}.\n" +
            "---",
            approval.RequesterEmail,
            approval.ModuleName,
            approval.Version,
            approval.TargetEnvironment,
            approval.ModuleName,
            approval.Version,
            approval.TargetEnvironment,
            approval.RespondedByEmail ?? "Unknown",
            approval.ResponseReason ?? "No reason provided",
            approval.DeploymentExecutionId,
            approval.TargetEnvironment);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SendApprovalRejectedNotificationAsync(
        ApprovalRequest approval,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "📧 [EMAIL] Deployment Rejected Notification\n" +
            "To: {Requester}\n" +
            "Subject: Rejected: Deploy {ModuleName} v{Version} to {Environment}\n" +
            "---\n" +
            "Your deployment has been rejected:\n" +
            "  Module: {ModuleName} v{Version}\n" +
            "  Environment: {Environment}\n" +
            "  Rejected By: {Approver}\n" +
            "  Reason: {Reason}\n" +
            "  Deployment ID: {DeploymentId}\n" +
            "\n" +
            "The deployment will not proceed to {Environment}.\n" +
            "---",
            approval.RequesterEmail,
            approval.ModuleName,
            approval.Version,
            approval.TargetEnvironment,
            approval.ModuleName,
            approval.Version,
            approval.TargetEnvironment,
            approval.RespondedByEmail ?? "Unknown",
            approval.ResponseReason ?? "No reason provided",
            approval.DeploymentExecutionId,
            approval.TargetEnvironment);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SendApprovalExpiredNotificationAsync(
        ApprovalRequest approval,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "📧 [EMAIL] Approval Expired Notification\n" +
            "To: {Requester}\n" +
            "Subject: Expired: Deploy {ModuleName} v{Version} to {Environment}\n" +
            "---\n" +
            "Your deployment approval request has expired:\n" +
            "  Module: {ModuleName} v{Version}\n" +
            "  Environment: {Environment}\n" +
            "  Deployment ID: {DeploymentId}\n" +
            "  Timeout: {Timeout}\n" +
            "\n" +
            "No approval decision was made within the timeout period.\n" +
            "The deployment has been automatically rejected.\n" +
            "---",
            approval.RequesterEmail,
            approval.ModuleName,
            approval.Version,
            approval.TargetEnvironment,
            approval.ModuleName,
            approval.Version,
            approval.TargetEnvironment,
            approval.DeploymentExecutionId,
            approval.TimeoutAt);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SendSecretRotationNotificationAsync(
        IEnumerable<string> recipients,
        string secretId,
        int previousVersion,
        int newVersion,
        DateTime rotationWindowEndsAt,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "📧 [EMAIL] Secret Rotation Notification\n" +
            "To: {Recipients}\n" +
            "Subject: IMPORTANT: Secret '{SecretId}' Has Been Automatically Rotated\n" +
            "---\n" +
            "A secret has been automatically rotated:\n" +
            "  Secret ID: {SecretId}\n" +
            "  Previous Version: {PreviousVersion}\n" +
            "  New Version: {NewVersion}\n" +
            "  Rotation Window Ends: {RotationWindowEndsAt:yyyy-MM-dd HH:mm:ss} UTC\n" +
            "\n" +
            "ACTION REQUIRED:\n" +
            "Both secret versions are valid until the rotation window ends.\n" +
            "Ensure all services are updated to use the new secret version\n" +
            "before {RotationWindowEndsAt:yyyy-MM-dd HH:mm:ss} UTC.\n" +
            "\n" +
            "After the rotation window, only the new version ({NewVersion}) will be valid.\n" +
            "---",
            string.Join(", ", recipients),
            secretId,
            secretId,
            previousVersion,
            newVersion,
            rotationWindowEndsAt,
            rotationWindowEndsAt,
            newVersion);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SendSecretExpirationWarningAsync(
        IEnumerable<string> recipients,
        string secretId,
        int daysRemaining,
        DateTime expiresAt,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "📧 [EMAIL] Secret Expiration Warning\n" +
            "To: {Recipients}\n" +
            "Subject: WARNING: Secret '{SecretId}' Expires in {DaysRemaining} Days\n" +
            "---\n" +
            "A secret is approaching expiration:\n" +
            "  Secret ID: {SecretId}\n" +
            "  Days Remaining: {DaysRemaining}\n" +
            "  Expiration Date: {ExpiresAt:yyyy-MM-dd HH:mm:ss} UTC\n" +
            "\n" +
            "ACTION REQUIRED:\n" +
            "Please rotate this secret before it expires to avoid service interruption.\n" +
            "\n" +
            "You can manually rotate the secret using the secret management API,\n" +
            "or enable automatic rotation by configuring a rotation policy.\n" +
            "---",
            string.Join(", ", recipients),
            secretId,
            daysRemaining,
            secretId,
            daysRemaining,
            expiresAt);

        return Task.CompletedTask;
    }
}
