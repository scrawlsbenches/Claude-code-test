using HotSwap.Distributed.Domain.Enums;

namespace HotSwap.Distributed.Domain.Models;

/// <summary>
/// Represents a deployment approval request.
/// </summary>
public class ApprovalRequest
{
    /// <summary>
    /// Unique identifier for the approval request.
    /// </summary>
    public Guid ApprovalId { get; set; } = Guid.NewGuid();

    /// <summary>
    /// The deployment execution ID that requires approval.
    /// </summary>
    public required Guid DeploymentExecutionId { get; set; }

    /// <summary>
    /// Module name being deployed.
    /// </summary>
    public required string ModuleName { get; set; }

    /// <summary>
    /// Module version being deployed.
    /// </summary>
    public required Version Version { get; set; }

    /// <summary>
    /// Target environment for deployment.
    /// </summary>
    public required EnvironmentType TargetEnvironment { get; set; }

    /// <summary>
    /// Email of the person who requested the deployment.
    /// </summary>
    public required string RequesterEmail { get; set; }

    /// <summary>
    /// List of approver email addresses who can approve this request.
    /// </summary>
    public List<string> ApproverEmails { get; set; } = new();

    /// <summary>
    /// Current status of the approval request.
    /// </summary>
    public ApprovalStatus Status { get; set; } = ApprovalStatus.Pending;

    /// <summary>
    /// When the approval request was created.
    /// </summary>
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the approval request was responded to (approved/rejected).
    /// </summary>
    public DateTime? RespondedAt { get; set; }

    /// <summary>
    /// Email of the approver who made the decision.
    /// </summary>
    public string? RespondedByEmail { get; set; }

    /// <summary>
    /// Reason provided for approval or rejection.
    /// </summary>
    public string? ResponseReason { get; set; }

    /// <summary>
    /// When the approval request will timeout and auto-reject.
    /// Defaults to 24 hours from creation.
    /// </summary>
    public DateTime TimeoutAt { get; set; }

    /// <summary>
    /// Additional metadata about the deployment.
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();

    /// <summary>
    /// Checks if the approval request has expired.
    /// </summary>
    public bool IsExpired => DateTime.UtcNow >= TimeoutAt && Status == ApprovalStatus.Pending;

    /// <summary>
    /// Checks if the approval request is still pending.
    /// </summary>
    public bool IsPending => Status == ApprovalStatus.Pending && !IsExpired;

    /// <summary>
    /// Checks if the approval request has been resolved (approved, rejected, or expired).
    /// </summary>
    public bool IsResolved => Status != ApprovalStatus.Pending || IsExpired;
}
