using HotSwap.Distributed.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace HotSwap.Distributed.Infrastructure.Data;

/// <summary>
/// Entity Framework Core DbContext for audit logging.
/// Manages persistence of all audit events to PostgreSQL.
/// </summary>
public class AuditLogDbContext : DbContext
{
    public AuditLogDbContext(DbContextOptions<AuditLogDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Main audit log entries.
    /// </summary>
    public DbSet<AuditLog> AuditLogs { get; set; } = null!;

    /// <summary>
    /// Deployment pipeline audit events.
    /// </summary>
    public DbSet<DeploymentAuditEvent> DeploymentAuditEvents { get; set; } = null!;

    /// <summary>
    /// Approval workflow audit events.
    /// </summary>
    public DbSet<ApprovalAuditEvent> ApprovalAuditEvents { get; set; } = null!;

    /// <summary>
    /// Authentication audit events.
    /// </summary>
    public DbSet<AuthenticationAuditEvent> AuthenticationAuditEvents { get; set; } = null!;

    /// <summary>
    /// Configuration change audit events.
    /// </summary>
    public DbSet<ConfigurationAuditEvent> ConfigurationAuditEvents { get; set; } = null!;

    /// <summary>
    /// Approval requests for deployment workflows.
    /// </summary>
    public DbSet<ApprovalRequestEntity> ApprovalRequests { get; set; } = null!;

    /// <summary>
    /// Deployment jobs for transactional outbox pattern.
    /// </summary>
    public DbSet<DeploymentJobEntity> DeploymentJobs { get; set; } = null!;

    /// <summary>
    /// Message queue for PostgreSQL LISTEN/NOTIFY pattern.
    /// </summary>
    public DbSet<MessageEntity> Messages { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure AuditLog entity
        modelBuilder.Entity<AuditLog>(entity =>
        {
            // Primary key
            entity.HasKey(e => e.Id);

            // Indexes for performance
            entity.HasIndex(e => e.EventId)
                .IsUnique();

            entity.HasIndex(e => e.Timestamp)
                .HasDatabaseName("idx_audit_logs_timestamp")
                .IsDescending();

            entity.HasIndex(e => e.EventType)
                .HasDatabaseName("idx_audit_logs_event_type");

            entity.HasIndex(e => e.EventCategory)
                .HasDatabaseName("idx_audit_logs_category");

            entity.HasIndex(e => new { e.Username, e.UserEmail })
                .HasDatabaseName("idx_audit_logs_user");

            entity.HasIndex(e => e.TraceId)
                .HasDatabaseName("idx_audit_logs_trace_id");

            entity.HasIndex(e => new { e.ResourceType, e.ResourceId })
                .HasDatabaseName("idx_audit_logs_resource");

            // GIN index on JSONB metadata column (created via migration)
            // entity.HasIndex(e => e.Metadata)
            //     .HasMethod("gin");

            // Configure relationships
            entity.HasOne(e => e.DeploymentEvent)
                .WithOne(d => d.AuditLog)
                .HasForeignKey<DeploymentAuditEvent>(d => d.AuditLogId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.ApprovalEvent)
                .WithOne(a => a.AuditLog)
                .HasForeignKey<ApprovalAuditEvent>(a => a.AuditLogId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.AuthenticationEvent)
                .WithOne(a => a.AuditLog)
                .HasForeignKey<AuthenticationAuditEvent>(a => a.AuditLogId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.ConfigurationEvent)
                .WithOne(c => c.AuditLog)
                .HasForeignKey<ConfigurationAuditEvent>(c => c.AuditLogId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure DeploymentAuditEvent entity
        modelBuilder.Entity<DeploymentAuditEvent>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasIndex(e => e.DeploymentExecutionId)
                .HasDatabaseName("idx_deployment_audit_execution_id");

            entity.HasIndex(e => new { e.ModuleName, e.ModuleVersion })
                .HasDatabaseName("idx_deployment_audit_module");

            entity.HasIndex(e => e.TargetEnvironment)
                .HasDatabaseName("idx_deployment_audit_environment");

            entity.HasIndex(e => e.StageStatus)
                .HasDatabaseName("idx_deployment_audit_status");

            entity.HasIndex(e => e.StartTime)
                .HasDatabaseName("idx_deployment_audit_start_time")
                .IsDescending();
        });

        // Configure ApprovalAuditEvent entity
        modelBuilder.Entity<ApprovalAuditEvent>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasIndex(e => e.ApprovalId)
                .HasDatabaseName("idx_approval_audit_approval_id");

            entity.HasIndex(e => e.DeploymentExecutionId)
                .HasDatabaseName("idx_approval_audit_deployment_id");

            entity.HasIndex(e => e.ApprovalStatus)
                .HasDatabaseName("idx_approval_audit_status");

            entity.HasIndex(e => e.RequesterEmail)
                .HasDatabaseName("idx_approval_audit_requester");

            entity.HasIndex(e => e.DecisionByEmail)
                .HasDatabaseName("idx_approval_audit_approver");

            entity.HasIndex(e => e.TargetEnvironment)
                .HasDatabaseName("idx_approval_audit_environment");

            // Configure array column for approver emails
            entity.Property(e => e.ApproverEmails)
                .HasColumnType("text[]");
        });

        // Configure AuthenticationAuditEvent entity
        modelBuilder.Entity<AuthenticationAuditEvent>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasIndex(e => e.Username)
                .HasDatabaseName("idx_auth_audit_username");

            entity.HasIndex(e => e.AuthenticationResult)
                .HasDatabaseName("idx_auth_audit_result");

            entity.HasIndex(e => e.CreatedAt)
                .HasDatabaseName("idx_auth_audit_timestamp")
                .IsDescending();

            entity.HasIndex(e => e.SourceIp)
                .HasDatabaseName("idx_auth_audit_source_ip");

            // Partial index for suspicious authentication attempts
            entity.HasIndex(e => e.IsSuspicious)
                .HasDatabaseName("idx_auth_audit_suspicious")
                .HasFilter("is_suspicious = true");
        });

        // Configure ConfigurationAuditEvent entity
        modelBuilder.Entity<ConfigurationAuditEvent>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasIndex(e => e.ConfigurationKey)
                .HasDatabaseName("idx_config_audit_key");

            entity.HasIndex(e => e.ConfigurationCategory)
                .HasDatabaseName("idx_config_audit_category");

            entity.HasIndex(e => e.ChangedByUser)
                .HasDatabaseName("idx_config_audit_user");

            entity.HasIndex(e => e.CreatedAt)
                .HasDatabaseName("idx_config_audit_timestamp")
                .IsDescending();
        });

        // Configure ApprovalRequestEntity
        modelBuilder.Entity<ApprovalRequestEntity>(entity =>
        {
            entity.HasKey(e => e.DeploymentExecutionId);

            entity.HasIndex(e => e.ApprovalId)
                .IsUnique();

            entity.HasIndex(e => new { e.Status, e.TimeoutAt })
                .HasDatabaseName("idx_approval_requests_status_timeout");

            // Partial index for pending approvals that haven't expired
            entity.HasIndex(e => e.TimeoutAt)
                .HasDatabaseName("idx_approval_requests_pending")
                .HasFilter("status = 0"); // 0 = Pending in enum

            entity.HasIndex(e => e.RequesterEmail)
                .HasDatabaseName("idx_approval_requests_requester");

            entity.HasIndex(e => e.RequestedAt)
                .HasDatabaseName("idx_approval_requests_requested_at")
                .IsDescending();

            // Configure array column for approver emails
            entity.Property(e => e.ApproverEmails)
                .HasColumnType("text[]");

            // Configure enum as string
            entity.Property(e => e.Status)
                .HasConversion<string>();
        });

        // Configure DeploymentJobEntity
        modelBuilder.Entity<DeploymentJobEntity>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasIndex(e => e.DeploymentId)
                .IsUnique();

            entity.HasIndex(e => new { e.Status, e.NextRetryAt })
                .HasDatabaseName("idx_deployment_jobs_pending");

            // Partial index for jobs that can be picked up
            entity.HasIndex(e => e.CreatedAt)
                .HasDatabaseName("idx_deployment_jobs_claimable")
                .HasFilter("status IN ('Pending', 'Failed')");

            entity.HasIndex(e => e.LockedUntil)
                .HasDatabaseName("idx_deployment_jobs_lock")
                .HasFilter("status = 'Running'");

            // Configure enum as string
            entity.Property(e => e.Status)
                .HasConversion<string>();

            // Configure JSON payload column
            entity.Property(e => e.Payload)
                .HasColumnType("jsonb");
        });

        // Configure MessageEntity
        modelBuilder.Entity<MessageEntity>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasIndex(e => e.MessageId)
                .IsUnique();

            entity.HasIndex(e => new { e.Topic, e.Priority, e.CreatedAt })
                .HasDatabaseName("idx_messages_topic_priority");

            // Partial index for pending messages
            entity.HasIndex(e => new { e.Topic, e.Priority })
                .HasDatabaseName("idx_messages_pending")
                .HasFilter("status = 'Pending'")
                .IsDescending();

            entity.HasIndex(e => e.LockedUntil)
                .HasDatabaseName("idx_messages_lock")
                .HasFilter("status = 'Processing'");

            // Configure enum as string
            entity.Property(e => e.Status)
                .HasConversion<string>();

            // Configure JSON payload column
            entity.Property(e => e.Payload)
                .HasColumnType("jsonb");
        });
    }
}
