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
    }
}
