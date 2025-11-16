using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HotSwap.Distributed.Infrastructure.Data.Entities;

/// <summary>
/// Main audit log entry for all system events.
/// Maps to audit_logs table in PostgreSQL.
/// </summary>
[Table("audit_logs")]
public class AuditLog
{
    /// <summary>
    /// Auto-incrementing primary key.
    /// </summary>
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    /// <summary>
    /// Unique event identifier (UUID).
    /// </summary>
    [Required]
    [Column("event_id")]
    public Guid EventId { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Timestamp when the event occurred (UTC).
    /// </summary>
    [Required]
    [Column("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Type of event (e.g., 'DeploymentStarted', 'ApprovalGranted').
    /// </summary>
    [Required]
    [Column("event_type")]
    [MaxLength(100)]
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// Category of event (e.g., 'Deployment', 'Approval', 'Authentication').
    /// </summary>
    [Required]
    [Column("event_category")]
    [MaxLength(50)]
    public string EventCategory { get; set; } = string.Empty;

    /// <summary>
    /// Severity level (e.g., 'Info', 'Warning', 'Error', 'Critical').
    /// </summary>
    [Required]
    [Column("severity")]
    [MaxLength(20)]
    public string Severity { get; set; } = "Info";

    /// <summary>
    /// User ID who triggered the event (if applicable).
    /// </summary>
    [Column("user_id")]
    public Guid? UserId { get; set; }

    /// <summary>
    /// Username of the user who triggered the event.
    /// </summary>
    [Column("username")]
    [MaxLength(255)]
    public string? Username { get; set; }

    /// <summary>
    /// Email of the user who triggered the event.
    /// </summary>
    [Column("user_email")]
    [MaxLength(255)]
    public string? UserEmail { get; set; }

    /// <summary>
    /// Type of resource affected (e.g., 'Module', 'Cluster', 'User').
    /// </summary>
    [Column("resource_type")]
    [MaxLength(100)]
    public string? ResourceType { get; set; }

    /// <summary>
    /// ID of the resource affected.
    /// </summary>
    [Column("resource_id")]
    [MaxLength(255)]
    public string? ResourceId { get; set; }

    /// <summary>
    /// Action performed (e.g., 'Create', 'Update', 'Delete', 'Approve').
    /// </summary>
    [Required]
    [Column("action")]
    [MaxLength(100)]
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// Result of the action (e.g., 'Success', 'Failure', 'Pending').
    /// </summary>
    [Required]
    [Column("result")]
    [MaxLength(50)]
    public string Result { get; set; } = "Success";

    /// <summary>
    /// Human-readable message describing the event.
    /// </summary>
    [Column("message")]
    public string? Message { get; set; }

    /// <summary>
    /// Additional structured metadata (stored as JSONB in PostgreSQL).
    /// </summary>
    [Column("metadata", TypeName = "jsonb")]
    public string? Metadata { get; set; }

    /// <summary>
    /// OpenTelemetry trace ID for distributed tracing correlation.
    /// </summary>
    [Column("trace_id")]
    [MaxLength(64)]
    public string? TraceId { get; set; }

    /// <summary>
    /// OpenTelemetry span ID for distributed tracing correlation.
    /// </summary>
    [Column("span_id")]
    [MaxLength(32)]
    public string? SpanId { get; set; }

    /// <summary>
    /// Source IP address of the request (IPv4 or IPv6).
    /// </summary>
    [Column("source_ip")]
    [MaxLength(45)]
    public string? SourceIp { get; set; }

    /// <summary>
    /// User agent string from the HTTP request.
    /// </summary>
    [Column("user_agent")]
    [MaxLength(500)]
    public string? UserAgent { get; set; }

    /// <summary>
    /// Record creation timestamp (UTC).
    /// </summary>
    [Required]
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties to specialized audit event tables
    public DeploymentAuditEvent? DeploymentEvent { get; set; }
    public ApprovalAuditEvent? ApprovalEvent { get; set; }
    public AuthenticationAuditEvent? AuthenticationEvent { get; set; }
    public ConfigurationAuditEvent? ConfigurationEvent { get; set; }
}
