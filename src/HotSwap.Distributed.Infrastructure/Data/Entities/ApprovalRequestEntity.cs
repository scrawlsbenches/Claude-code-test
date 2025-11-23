using HotSwap.Distributed.Domain.Enums;

namespace HotSwap.Distributed.Infrastructure.Data.Entities;

/// <summary>
/// Database entity for approval requests.
/// Replaces static in-memory storage with durable PostgreSQL persistence.
/// </summary>
public class ApprovalRequestEntity
{
    /// <summary>
    /// Deployment execution ID (primary key).
    /// </summary>
    public Guid DeploymentExecutionId { get; set; }

    /// <summary>
    /// Unique approval request ID.
    /// </summary>
    public Guid ApprovalId { get; set; }

    /// <summary>
    /// Email of the user who requested the deployment.
    /// </summary>
    public string RequesterEmail { get; set; } = string.Empty;

    /// <summary>
    /// Target environment (Production, Staging, etc.).
    /// </summary>
    public string TargetEnvironment { get; set; } = string.Empty;

    /// <summary>
    /// Module name being deployed.
    /// </summary>
    public string ModuleName { get; set; } = string.Empty;

    /// <summary>
    /// Module version being deployed.
    /// </summary>
    public string ModuleVersion { get; set; } = string.Empty;

    /// <summary>
    /// Current approval status.
    /// </summary>
    public ApprovalStatus Status { get; set; }

    /// <summary>
    /// List of authorized approver emails (JSON array).
    /// </summary>
    public List<string> ApproverEmails { get; set; } = new();

    /// <summary>
    /// When the approval was requested.
    /// </summary>
    public DateTime RequestedAt { get; set; }

    /// <summary>
    /// When the approval expires if not responded to.
    /// </summary>
    public DateTime TimeoutAt { get; set; }

    /// <summary>
    /// When the approval was responded to (approved/rejected/expired).
    /// </summary>
    public DateTime? RespondedAt { get; set; }

    /// <summary>
    /// Email of the approver who responded.
    /// </summary>
    public string? RespondedByEmail { get; set; }

    /// <summary>
    /// Reason for the approval/rejection decision.
    /// </summary>
    public string? ResponseReason { get; set; }

    /// <summary>
    /// When the record was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When the record was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; }
}
