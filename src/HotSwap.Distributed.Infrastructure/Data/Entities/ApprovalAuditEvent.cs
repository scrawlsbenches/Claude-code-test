using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HotSwap.Distributed.Infrastructure.Data.Entities;

/// <summary>
/// Specialized audit event for approval workflow events.
/// Maps to approval_audit_events table in PostgreSQL.
/// </summary>
[Table("approval_audit_events")]
public class ApprovalAuditEvent
{
    /// <summary>
    /// Auto-incrementing primary key.
    /// </summary>
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    /// <summary>
    /// Foreign key to the parent audit log entry.
    /// </summary>
    [Required]
    [Column("audit_log_id")]
    public long AuditLogId { get; set; }

    /// <summary>
    /// Approval request unique identifier.
    /// </summary>
    [Required]
    [Column("approval_id")]
    public Guid ApprovalId { get; set; }

    /// <summary>
    /// Deployment execution ID that requires approval.
    /// </summary>
    [Required]
    [Column("deployment_execution_id")]
    public Guid DeploymentExecutionId { get; set; }

    /// <summary>
    /// Name of the module awaiting approval.
    /// </summary>
    [Required]
    [Column("module_name")]
    [MaxLength(255)]
    public string ModuleName { get; set; } = string.Empty;

    /// <summary>
    /// Version of the module awaiting approval.
    /// </summary>
    [Required]
    [Column("module_version")]
    [MaxLength(100)]
    public string ModuleVersion { get; set; } = string.Empty;

    /// <summary>
    /// Target environment for the deployment (e.g., 'Staging', 'Production').
    /// </summary>
    [Required]
    [Column("target_environment")]
    [MaxLength(50)]
    public string TargetEnvironment { get; set; } = string.Empty;

    /// <summary>
    /// Email of the person requesting the deployment.
    /// </summary>
    [Required]
    [Column("requester_email")]
    [MaxLength(255)]
    public string RequesterEmail { get; set; } = string.Empty;

    /// <summary>
    /// Array of authorized approver emails.
    /// </summary>
    [Column("approver_emails", TypeName = "text[]")]
    public string[]? ApproverEmails { get; set; }

    /// <summary>
    /// Approval status (e.g., 'Pending', 'Approved', 'Rejected', 'Expired').
    /// </summary>
    [Required]
    [Column("approval_status")]
    [MaxLength(50)]
    public string ApprovalStatus { get; set; } = "Pending";

    /// <summary>
    /// Email of the person who made the approval decision.
    /// </summary>
    [Column("decision_by_email")]
    [MaxLength(255)]
    public string? DecisionByEmail { get; set; }

    /// <summary>
    /// Timestamp when the decision was made (UTC).
    /// </summary>
    [Column("decision_at")]
    public DateTime? DecisionAt { get; set; }

    /// <summary>
    /// Reason provided for the approval/rejection decision.
    /// </summary>
    [Column("decision_reason")]
    public string? DecisionReason { get; set; }

    /// <summary>
    /// Timeout timestamp (UTC) - request auto-rejects after this time.
    /// </summary>
    [Required]
    [Column("timeout_at")]
    public DateTime TimeoutAt { get; set; }

    /// <summary>
    /// Computed column: true if currently expired and pending.
    /// Note: This is a generated column in PostgreSQL.
    /// </summary>
    [Column("is_expired")]
    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    public bool IsExpired { get; private set; }

    /// <summary>
    /// Additional structured metadata (stored as JSONB in PostgreSQL).
    /// </summary>
    [Column("metadata", TypeName = "jsonb")]
    public string? Metadata { get; set; }

    /// <summary>
    /// Record creation timestamp (UTC).
    /// </summary>
    [Required]
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property to parent audit log
    [ForeignKey(nameof(AuditLogId))]
    public AuditLog AuditLog { get; set; } = null!;
}
