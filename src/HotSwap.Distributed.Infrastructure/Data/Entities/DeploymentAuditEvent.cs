using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HotSwap.Distributed.Infrastructure.Data.Entities;

/// <summary>
/// Specialized audit event for deployment pipeline events.
/// Maps to deployment_audit_events table in PostgreSQL.
/// </summary>
[Table("deployment_audit_events")]
public class DeploymentAuditEvent
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
    /// Deployment execution ID (correlates to pipeline execution).
    /// </summary>
    [Required]
    [Column("deployment_execution_id")]
    public Guid DeploymentExecutionId { get; set; }

    /// <summary>
    /// Name of the module being deployed.
    /// </summary>
    [Required]
    [Column("module_name")]
    [MaxLength(255)]
    public string ModuleName { get; set; } = string.Empty;

    /// <summary>
    /// Version of the module being deployed.
    /// </summary>
    [Required]
    [Column("module_version")]
    [MaxLength(100)]
    public string ModuleVersion { get; set; } = string.Empty;

    /// <summary>
    /// Target environment (e.g., 'Development', 'QA', 'Staging', 'Production').
    /// </summary>
    [Required]
    [Column("target_environment")]
    [MaxLength(50)]
    public string TargetEnvironment { get; set; } = string.Empty;

    /// <summary>
    /// Deployment strategy used (e.g., 'Direct', 'Rolling', 'BlueGreen', 'Canary').
    /// </summary>
    [Column("deployment_strategy")]
    [MaxLength(100)]
    public string? DeploymentStrategy { get; set; }

    /// <summary>
    /// Pipeline stage (e.g., 'Build', 'Test', 'Security', 'Deploy', 'Validation').
    /// </summary>
    [Column("pipeline_stage")]
    [MaxLength(100)]
    public string? PipelineStage { get; set; }

    /// <summary>
    /// Status of the stage (e.g., 'Running', 'Succeeded', 'Failed', 'WaitingForApproval').
    /// </summary>
    [Column("stage_status")]
    [MaxLength(50)]
    public string? StageStatus { get; set; }

    /// <summary>
    /// Number of nodes targeted for deployment.
    /// </summary>
    [Column("nodes_targeted")]
    public int? NodesTargeted { get; set; }

    /// <summary>
    /// Number of nodes successfully deployed.
    /// </summary>
    [Column("nodes_deployed")]
    public int? NodesDeployed { get; set; }

    /// <summary>
    /// Number of nodes that failed deployment.
    /// </summary>
    [Column("nodes_failed")]
    public int? NodesFailed { get; set; }

    /// <summary>
    /// Stage start time (UTC).
    /// </summary>
    [Column("start_time")]
    public DateTime? StartTime { get; set; }

    /// <summary>
    /// Stage end time (UTC).
    /// </summary>
    [Column("end_time")]
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// Duration in milliseconds.
    /// </summary>
    [Column("duration_ms")]
    public int? DurationMs { get; set; }

    /// <summary>
    /// Error message if deployment failed.
    /// </summary>
    [Column("error_message")]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Full exception details if deployment failed.
    /// </summary>
    [Column("exception_details")]
    public string? ExceptionDetails { get; set; }

    /// <summary>
    /// Email of the person who requested the deployment.
    /// </summary>
    [Column("requester_email")]
    [MaxLength(255)]
    public string? RequesterEmail { get; set; }

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
