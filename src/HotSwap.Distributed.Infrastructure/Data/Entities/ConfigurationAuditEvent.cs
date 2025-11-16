using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HotSwap.Distributed.Infrastructure.Data.Entities;

/// <summary>
/// Specialized audit event for configuration changes.
/// Maps to configuration_audit_events table in PostgreSQL.
/// Future enhancement - not currently used.
/// </summary>
[Table("configuration_audit_events")]
public class ConfigurationAuditEvent
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
    /// Configuration key that was changed.
    /// </summary>
    [Required]
    [Column("configuration_key")]
    [MaxLength(255)]
    public string ConfigurationKey { get; set; } = string.Empty;

    /// <summary>
    /// Category of configuration (e.g., 'Pipeline', 'Security', 'Deployment').
    /// </summary>
    [Required]
    [Column("configuration_category")]
    [MaxLength(100)]
    public string ConfigurationCategory { get; set; } = string.Empty;

    /// <summary>
    /// Previous value before the change.
    /// </summary>
    [Column("old_value")]
    public string? OldValue { get; set; }

    /// <summary>
    /// New value after the change.
    /// </summary>
    [Column("new_value")]
    public string? NewValue { get; set; }

    /// <summary>
    /// User who made the configuration change.
    /// </summary>
    [Required]
    [Column("changed_by_user")]
    [MaxLength(255)]
    public string ChangedByUser { get; set; } = string.Empty;

    /// <summary>
    /// Reason for the configuration change.
    /// </summary>
    [Column("change_reason")]
    public string? ChangeReason { get; set; }

    /// <summary>
    /// User who approved the configuration change (if applicable).
    /// </summary>
    [Column("approved_by_user")]
    [MaxLength(255)]
    public string? ApprovedByUser { get; set; }

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
