using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HotSwap.Distributed.Infrastructure.Data.Entities;

/// <summary>
/// Specialized audit event for authentication events.
/// Maps to authentication_audit_events table in PostgreSQL.
/// </summary>
[Table("authentication_audit_events")]
public class AuthenticationAuditEvent
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
    /// User ID (if authentication succeeded).
    /// </summary>
    [Column("user_id")]
    public Guid? UserId { get; set; }

    /// <summary>
    /// Username attempting authentication.
    /// </summary>
    [Required]
    [Column("username")]
    [MaxLength(255)]
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Authentication method used (e.g., 'JWT', 'BasicAuth', 'ApiKey').
    /// </summary>
    [Required]
    [Column("authentication_method")]
    [MaxLength(50)]
    public string AuthenticationMethod { get; set; } = "JWT";

    /// <summary>
    /// Result of the authentication attempt (e.g., 'Success', 'Failure', 'LockedOut').
    /// </summary>
    [Required]
    [Column("authentication_result")]
    [MaxLength(50)]
    public string AuthenticationResult { get; set; } = string.Empty;

    /// <summary>
    /// Reason for authentication failure (if applicable).
    /// </summary>
    [Column("failure_reason")]
    [MaxLength(255)]
    public string? FailureReason { get; set; }

    /// <summary>
    /// Whether a token was issued (for successful authentications).
    /// </summary>
    [Column("token_issued")]
    public bool TokenIssued { get; set; }

    /// <summary>
    /// Token expiration timestamp (UTC) if token was issued.
    /// </summary>
    [Column("token_expires_at")]
    public DateTime? TokenExpiresAt { get; set; }

    /// <summary>
    /// Source IP address of the authentication request.
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
    /// Geographic location (optional: Country/City).
    /// </summary>
    [Column("geo_location")]
    [MaxLength(255)]
    public string? GeoLocation { get; set; }

    /// <summary>
    /// Flag indicating suspicious authentication attempt.
    /// </summary>
    [Column("is_suspicious")]
    public bool IsSuspicious { get; set; }

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
