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
}
