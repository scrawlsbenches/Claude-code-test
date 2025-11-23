# Domain Models Reference

**Version:** 1.0.0
**Namespace:** `HotSwap.SchemaMigration.Domain.Models`

---

## Table of Contents

1. [Migration](#migration)
2. [DatabaseTarget](#databasetarget)
3. [MigrationExecution](#migrationexecution)
4. [PerformanceMetrics](#performancemetrics)
5. [MigrationApproval](#migrationapproval)
6. [Enumerations](#enumerations)
7. [Value Objects](#value-objects)

---

## Migration

Represents a database schema migration definition.

**File:** `src/HotSwap.SchemaMigration.Domain/Models/Migration.cs`

```csharp
namespace HotSwap.SchemaMigration.Domain.Models;

/// <summary>
/// Represents a database schema migration.
/// </summary>
public class Migration
{
    /// <summary>
    /// Unique migration identifier (GUID format).
    /// </summary>
    public required string MigrationId { get; set; }

    /// <summary>
    /// Migration name (e.g., "add_users_email_index").
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Human-readable description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Target database cluster ID.
    /// </summary>
    public required string TargetDatabaseId { get; set; }

    /// <summary>
    /// Forward migration SQL script.
    /// </summary>
    public required string MigrationScript { get; set; }

    /// <summary>
    /// Rollback SQL script (mandatory for production).
    /// </summary>
    public required string RollbackScript { get; set; }

    /// <summary>
    /// Migration strategy to use for rollout.
    /// </summary>
    public MigrationStrategy Strategy { get; set; } = MigrationStrategy.Phased;

    /// <summary>
    /// Current migration status.
    /// </summary>
    public MigrationStatus Status { get; set; } = MigrationStatus.Draft;

    /// <summary>
    /// Risk level assessment (Low, Medium, High).
    /// </summary>
    public RiskLevel RiskLevel { get; set; } = RiskLevel.Medium;

    /// <summary>
    /// Estimated execution time (seconds).
    /// </summary>
    public int? EstimatedDuration { get; set; }

    /// <summary>
    /// Dependencies (other migration IDs that must complete first).
    /// </summary>
    public List<string> Dependencies { get; set; } = new();

    /// <summary>
    /// Tags for categorization (e.g., "index", "performance", "security").
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// User who created the migration.
    /// </summary>
    public required string CreatedBy { get; set; }

    /// <summary>
    /// Creation timestamp (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last update timestamp (UTC).
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Validates the migration for required fields and constraints.
    /// </summary>
    public bool IsValid(out List<string> errors)
    {
        errors = new List<string>();

        if (string.IsNullOrWhiteSpace(MigrationId))
            errors.Add("MigrationId is required");

        if (string.IsNullOrWhiteSpace(Name))
            errors.Add("Name is required");
        else if (!System.Text.RegularExpressions.Regex.IsMatch(Name, @"^[a-z0-9_]+$"))
            errors.Add("Name must contain only lowercase letters, numbers, and underscores");

        if (string.IsNullOrWhiteSpace(TargetDatabaseId))
            errors.Add("TargetDatabaseId is required");

        if (string.IsNullOrWhiteSpace(MigrationScript))
            errors.Add("MigrationScript is required");

        if (string.IsNullOrWhiteSpace(RollbackScript))
            errors.Add("RollbackScript is required");

        if (MigrationScript.Length > 10_485_760) // 10 MB
            errors.Add("MigrationScript exceeds maximum size of 10 MB");

        return errors.Count == 0;
    }

    /// <summary>
    /// Checks if migration requires approval based on risk level.
    /// </summary>
    public bool RequiresApproval() => RiskLevel >= RiskLevel.Medium;
}
```

---

## DatabaseTarget

Represents a target database cluster for migrations.

**File:** `src/HotSwap.SchemaMigration.Domain/Models/DatabaseTarget.cs`

```csharp
namespace HotSwap.SchemaMigration.Domain.Models;

/// <summary>
/// Represents a target database cluster.
/// </summary>
public class DatabaseTarget
{
    /// <summary>
    /// Unique database identifier (GUID format).
    /// </summary>
    public required string DatabaseId { get; set; }

    /// <summary>
    /// Database cluster name (e.g., "production-db-cluster").
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Human-readable description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Database type (PostgreSQL, SqlServer, MySQL, Oracle).
    /// </summary>
    public DatabaseType Type { get; set; }

    /// <summary>
    /// Environment (Development, QA, Staging, Production).
    /// </summary>
    public EnvironmentType Environment { get; set; }

    /// <summary>
    /// Master database connection string (encrypted).
    /// </summary>
    public required string MasterConnectionString { get; set; }

    /// <summary>
    /// Replica database connection strings (encrypted).
    /// </summary>
    public List<ReplicaInfo> Replicas { get; set; } = new();

    /// <summary>
    /// Current health status.
    /// </summary>
    public DatabaseHealth Health { get; set; } = new();

    /// <summary>
    /// Database configuration settings.
    /// </summary>
    public Dictionary<string, string> Config { get; set; } = new();

    /// <summary>
    /// Registration timestamp (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last health check timestamp (UTC).
    /// </summary>
    public DateTime LastHealthCheck { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Validates the database target configuration.
    /// </summary>
    public bool IsValid(out List<string> errors)
    {
        errors = new List<string>();

        if (string.IsNullOrWhiteSpace(DatabaseId))
            errors.Add("DatabaseId is required");

        if (string.IsNullOrWhiteSpace(Name))
            errors.Add("Name is required");

        if (string.IsNullOrWhiteSpace(MasterConnectionString))
            errors.Add("MasterConnectionString is required");

        return errors.Count == 0;
    }

    /// <summary>
    /// Checks if database is healthy and ready for migrations.
    /// </summary>
    public bool IsHealthy() => Health.IsHealthy && Health.ReplicationLag < 30;
}

/// <summary>
/// Replica database information.
/// </summary>
public class ReplicaInfo
{
    public required string ReplicaId { get; set; }
    public required string ConnectionString { get; set; }
    public ReplicaRole Role { get; set; } = ReplicaRole.AsyncReplica;
    public bool IsHealthy { get; set; } = true;
    public double ReplicationLag { get; set; } = 0; // seconds
}

/// <summary>
/// Database health information.
/// </summary>
public class DatabaseHealth
{
    public bool IsHealthy { get; set; } = true;
    public int ActiveConnections { get; set; } = 0;
    public double CpuUsage { get; set; } = 0; // percentage
    public double DiskUsage { get; set; } = 0; // percentage
    public double ReplicationLag { get; set; } = 0; // seconds
    public DateTime LastCheck { get; set; } = DateTime.UtcNow;
}
```

---

## MigrationExecution

Represents a migration execution instance.

**File:** `src/HotSwap.SchemaMigration.Domain/Models/MigrationExecution.cs`

```csharp
namespace HotSwap.SchemaMigration.Domain.Models;

/// <summary>
/// Represents a migration execution instance.
/// </summary>
public class MigrationExecution
{
    /// <summary>
    /// Unique execution identifier (GUID format).
    /// </summary>
    public required string ExecutionId { get; set; }

    /// <summary>
    /// Parent migration identifier.
    /// </summary>
    public required string MigrationId { get; set; }

    /// <summary>
    /// Target database identifier.
    /// </summary>
    public required string DatabaseId { get; set; }

    /// <summary>
    /// Target database instance (master or replica ID).
    /// </summary>
    public required string TargetInstance { get; set; }

    /// <summary>
    /// Execution strategy used.
    /// </summary>
    public MigrationStrategy Strategy { get; set; }

    /// <summary>
    /// Current execution status.
    /// </summary>
    public ExecutionStatus Status { get; set; } = ExecutionStatus.Pending;

    /// <summary>
    /// Execution start timestamp (UTC).
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// Execution completion timestamp (UTC).
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Execution duration (seconds).
    /// </summary>
    public double? Duration => CompletedAt.HasValue && StartedAt.HasValue
        ? (CompletedAt.Value - StartedAt.Value).TotalSeconds
        : null;

    /// <summary>
    /// Number of rows affected by migration.
    /// </summary>
    public long? RowsAffected { get; set; }

    /// <summary>
    /// Performance metrics captured during execution.
    /// </summary>
    public List<PerformanceSnapshot> PerformanceSnapshots { get; set; } = new();

    /// <summary>
    /// Execution logs (SQL statements, errors, warnings).
    /// </summary>
    public List<ExecutionLog> Logs { get; set; } = new();

    /// <summary>
    /// Error message if execution failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Rollback execution ID (if rolled back).
    /// </summary>
    public string? RollbackExecutionId { get; set; }

    /// <summary>
    /// User who initiated the execution.
    /// </summary>
    public required string InitiatedBy { get; set; }

    /// <summary>
    /// Checks if execution is in terminal state (completed, failed, rolled back).
    /// </summary>
    public bool IsTerminal() => Status is ExecutionStatus.Succeeded
        or ExecutionStatus.Failed
        or ExecutionStatus.RolledBack;

    /// <summary>
    /// Checks if execution exceeded threshold and should rollback.
    /// </summary>
    public bool ShouldRollback(PerformanceBaseline baseline)
    {
        if (PerformanceSnapshots.Count == 0)
            return false;

        var latestSnapshot = PerformanceSnapshots.Last();

        // Rollback if query latency increased > 50%
        if (latestSnapshot.AvgQueryLatencyMs > baseline.AvgQueryLatencyMs * 1.5)
            return true;

        // Rollback if lock wait time > 5 seconds
        if (latestSnapshot.LockWaitTimeMs > 5000)
            return true;

        // Rollback if replication lag > 60 seconds
        if (latestSnapshot.ReplicationLagSeconds > 60)
            return true;

        return false;
    }
}

/// <summary>
/// Execution log entry.
/// </summary>
public class ExecutionLog
{
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public LogLevel Level { get; set; }
    public required string Message { get; set; }
    public string? SqlStatement { get; set; }
}
```

---

## PerformanceMetrics

Represents performance metrics captured during migration.

**File:** `src/HotSwap.SchemaMigration.Domain/Models/PerformanceMetrics.cs`

```csharp
namespace HotSwap.SchemaMigration.Domain.Models;

/// <summary>
/// Performance metrics snapshot at a point in time.
/// </summary>
public class PerformanceSnapshot
{
    /// <summary>
    /// Snapshot timestamp (UTC).
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Average query latency (milliseconds).
    /// </summary>
    public double AvgQueryLatencyMs { get; set; }

    /// <summary>
    /// 95th percentile query latency (milliseconds).
    /// </summary>
    public double P95QueryLatencyMs { get; set; }

    /// <summary>
    /// 99th percentile query latency (milliseconds).
    /// </summary>
    public double P99QueryLatencyMs { get; set; }

    /// <summary>
    /// Active database connections.
    /// </summary>
    public int ActiveConnections { get; set; }

    /// <summary>
    /// Lock wait time (milliseconds).
    /// </summary>
    public double LockWaitTimeMs { get; set; }

    /// <summary>
    /// Replication lag (seconds).
    /// </summary>
    public double ReplicationLagSeconds { get; set; }

    /// <summary>
    /// CPU usage percentage (0-100).
    /// </summary>
    public double CpuUsagePercent { get; set; }

    /// <summary>
    /// Disk I/O operations per second.
    /// </summary>
    public double DiskIopsPerSecond { get; set; }
}

/// <summary>
/// Performance baseline captured before migration.
/// </summary>
public class PerformanceBaseline
{
    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;
    public double AvgQueryLatencyMs { get; set; }
    public double P95QueryLatencyMs { get; set; }
    public double P99QueryLatencyMs { get; set; }
    public int AvgActiveConnections { get; set; }
    public double AvgCpuUsagePercent { get; set; }
}
```

---

## MigrationApproval

Represents an approval request for a migration.

**File:** `src/HotSwap.SchemaMigration.Domain/Models/MigrationApproval.cs`

```csharp
namespace HotSwap.SchemaMigration.Domain.Models;

/// <summary>
/// Represents a migration approval request.
/// </summary>
public class MigrationApproval
{
    /// <summary>
    /// Unique approval identifier (GUID format).
    /// </summary>
    public required string ApprovalId { get; set; }

    /// <summary>
    /// Migration identifier.
    /// </summary>
    public required string MigrationId { get; set; }

    /// <summary>
    /// User who submitted the approval request.
    /// </summary>
    public required string SubmittedBy { get; set; }

    /// <summary>
    /// Submission timestamp (UTC).
    /// </summary>
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Current approval status.
    /// </summary>
    public ApprovalStatus Status { get; set; } = ApprovalStatus.Pending;

    /// <summary>
    /// User who approved/rejected the request.
    /// </summary>
    public string? ReviewedBy { get; set; }

    /// <summary>
    /// Review timestamp (UTC).
    /// </summary>
    public DateTime? ReviewedAt { get; set; }

    /// <summary>
    /// Review notes/comments.
    /// </summary>
    public string? ReviewNotes { get; set; }

    /// <summary>
    /// Approval expiration (default: 7 days).
    /// </summary>
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddDays(7);

    /// <summary>
    /// Checks if approval has expired.
    /// </summary>
    public bool IsExpired() => DateTime.UtcNow > ExpiresAt;

    /// <summary>
    /// Checks if approval is in terminal state.
    /// </summary>
    public bool IsTerminal() => Status is ApprovalStatus.Approved or ApprovalStatus.Rejected;
}
```

---

## Enumerations

### MigrationStrategy

**File:** `src/HotSwap.SchemaMigration.Domain/Enums/MigrationStrategy.cs`

```csharp
namespace HotSwap.SchemaMigration.Domain.Enums;

/// <summary>
/// Migration rollout strategy.
/// </summary>
public enum MigrationStrategy
{
    /// <summary>
    /// Execute immediately on target database.
    /// </summary>
    Direct,

    /// <summary>
    /// Execute replica-by-replica, then master.
    /// </summary>
    Phased,

    /// <summary>
    /// Execute on 10% → 50% → 100% of replicas.
    /// </summary>
    Canary,

    /// <summary>
    /// Execute on shadow replica, then switch traffic.
    /// </summary>
    BlueGreen,

    /// <summary>
    /// Execute on shadow replica with production traffic replay.
    /// </summary>
    Shadow
}
```

### MigrationStatus

```csharp
namespace HotSwap.SchemaMigration.Domain.Enums;

/// <summary>
/// Migration lifecycle status.
/// </summary>
public enum MigrationStatus
{
    Draft,
    PendingApproval,
    Approved,
    InProgress,
    Succeeded,
    Failed,
    RolledBack
}
```

### ExecutionStatus

```csharp
namespace HotSwap.SchemaMigration.Domain.Enums;

/// <summary>
/// Migration execution status.
/// </summary>
public enum ExecutionStatus
{
    Pending,
    Running,
    Succeeded,
    Failed,
    RolledBack,
    Paused
}
```

### DatabaseType

```csharp
namespace HotSwap.SchemaMigration.Domain.Enums;

/// <summary>
/// Supported database types.
/// </summary>
public enum DatabaseType
{
    PostgreSQL,
    SqlServer,
    MySQL,
    Oracle
}
```

### EnvironmentType

```csharp
namespace HotSwap.SchemaMigration.Domain.Enums;

/// <summary>
/// Target environment types.
/// </summary>
public enum EnvironmentType
{
    Development,
    QA,
    Staging,
    Production
}
```

### RiskLevel

```csharp
namespace HotSwap.SchemaMigration.Domain.Enums;

/// <summary>
/// Migration risk assessment.
/// </summary>
public enum RiskLevel
{
    Low,     // Add nullable column, create index concurrently
    Medium,  // Add not-null column with default, add constraint
    High     // Drop column, rename table, modify data types
}
```

### ReplicaRole

```csharp
namespace HotSwap.SchemaMigration.Domain.Enums;

/// <summary>
/// Replica role in database cluster.
/// </summary>
public enum ReplicaRole
{
    Master,
    SyncReplica,
    AsyncReplica,
    ShadowReplica
}
```

### ApprovalStatus

```csharp
namespace HotSwap.SchemaMigration.Domain.Enums;

/// <summary>
/// Approval request status.
/// </summary>
public enum ApprovalStatus
{
    Pending,
    Approved,
    Rejected,
    Expired
}
```

---

## Value Objects

### ValidationResult

**File:** `src/HotSwap.SchemaMigration.Domain/ValueObjects/ValidationResult.cs`

```csharp
namespace HotSwap.SchemaMigration.Domain.ValueObjects;

/// <summary>
/// Result of a migration validation operation.
/// </summary>
public class ValidationResult
{
    public bool IsValid { get; private set; }
    public List<ValidationError> Errors { get; private set; } = new();
    public List<ValidationWarning> Warnings { get; private set; } = new();
    public DateTime ValidatedAt { get; private set; } = DateTime.UtcNow;

    public static ValidationResult Success()
    {
        return new ValidationResult { IsValid = true };
    }

    public static ValidationResult Failure(List<ValidationError> errors)
    {
        return new ValidationResult
        {
            IsValid = false,
            Errors = errors
        };
    }
}

public class ValidationError
{
    public required string Code { get; set; }
    public required string Message { get; set; }
    public string? SqlStatement { get; set; }
}

public class ValidationWarning
{
    public required string Message { get; set; }
    public string? Recommendation { get; set; }
}
```

---

**Last Updated:** 2025-11-23
**Namespace:** `HotSwap.SchemaMigration.Domain`
