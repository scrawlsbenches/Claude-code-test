# Domain Models Reference

**Version:** 1.0.0
**Namespace:** `HotSwap.MultiTenantConfig.Domain.Models`

---

## Table of Contents

1. [Tenant](#tenant)
2. [Configuration](#configuration)
3. [ConfigVersion](#configversion)
4. [ApprovalRequest](#approvalrequest)
5. [Deployment](#deployment)
6. [Enumerations](#enumerations)
7. [Value Objects](#value-objects)

---

## Tenant

Represents a tenant in the multi-tenant system.

**File:** `src/HotSwap.MultiTenantConfig.Domain/Models/Tenant.cs`

```csharp
namespace HotSwap.MultiTenantConfig.Domain.Models;

/// <summary>
/// Represents a tenant in the multi-tenant configuration system.
/// </summary>
public class Tenant
{
    /// <summary>
    /// Unique tenant identifier (alphanumeric, hyphens, 3-50 chars).
    /// </summary>
    public required string TenantId { get; set; }

    /// <summary>
    /// Human-readable tenant name.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Tenant subscription tier.
    /// </summary>
    public TenantTier Tier { get; set; } = TenantTier.Free;

    /// <summary>
    /// Current tenant status.
    /// </summary>
    public TenantStatus Status { get; set; } = TenantStatus.Active;

    /// <summary>
    /// Maximum number of configurations allowed for this tenant.
    /// </summary>
    public int MaxConfigurations { get; set; } = 100;

    /// <summary>
    /// Maximum number of environments allowed for this tenant.
    /// </summary>
    public int MaxEnvironments { get; set; } = 4;

    /// <summary>
    /// Tenant contact email for notifications.
    /// </summary>
    public string? ContactEmail { get; set; }

    /// <summary>
    /// Tenant metadata (key-value pairs).
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();

    /// <summary>
    /// Tenant creation timestamp (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last update timestamp (UTC).
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Soft delete timestamp (UTC). Null if not deleted.
    /// </summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>
    /// Validates the tenant configuration.
    /// </summary>
    public bool IsValid(out List<string> errors)
    {
        errors = new List<string>();

        if (string.IsNullOrWhiteSpace(TenantId))
            errors.Add("TenantId is required");
        else if (!System.Text.RegularExpressions.Regex.IsMatch(TenantId, @"^[a-zA-Z0-9-]{3,50}$"))
            errors.Add("TenantId must be 3-50 alphanumeric characters or hyphens");

        if (string.IsNullOrWhiteSpace(Name))
            errors.Add("Name is required");

        if (MaxConfigurations < 1)
            errors.Add("MaxConfigurations must be at least 1");

        if (MaxEnvironments < 1)
            errors.Add("MaxEnvironments must be at least 1");

        if (!string.IsNullOrEmpty(ContactEmail) && !IsValidEmail(ContactEmail))
            errors.Add("ContactEmail must be a valid email address");

        return errors.Count == 0;
    }

    /// <summary>
    /// Checks if the tenant is active.
    /// </summary>
    public bool IsActive() => Status == TenantStatus.Active && DeletedAt == null;

    /// <summary>
    /// Checks if the tenant can create more configurations.
    /// </summary>
    public bool CanCreateConfig(int currentConfigCount) => currentConfigCount < MaxConfigurations;

    private bool IsValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }
}
```

---

## Configuration

Represents a configuration key-value pair for a tenant.

**File:** `src/HotSwap.MultiTenantConfig.Domain/Models/Configuration.cs`

```csharp
namespace HotSwap.MultiTenantConfig.Domain.Models;

/// <summary>
/// Represents a configuration entry for a tenant.
/// </summary>
public class Configuration
{
    /// <summary>
    /// Unique configuration identifier (GUID format).
    /// </summary>
    public required string ConfigId { get; set; }

    /// <summary>
    /// Tenant identifier this configuration belongs to.
    /// </summary>
    public required string TenantId { get; set; }

    /// <summary>
    /// Configuration key (dot-notation: "feature.new_ui").
    /// </summary>
    public required string Key { get; set; }

    /// <summary>
    /// Configuration value (JSON string).
    /// </summary>
    public required string Value { get; set; }

    /// <summary>
    /// Value type (String, Number, Boolean, JSON).
    /// </summary>
    public ConfigValueType Type { get; set; } = ConfigValueType.String;

    /// <summary>
    /// Environment this configuration applies to.
    /// </summary>
    public ConfigEnvironment Environment { get; set; } = ConfigEnvironment.Development;

    /// <summary>
    /// Current version number.
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// Configuration description (optional).
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Tags for categorization (e.g., "feature-flag", "database").
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// Whether this configuration value is encrypted.
    /// </summary>
    public bool IsEncrypted { get; set; } = false;

    /// <summary>
    /// Whether this configuration is sensitive (e.g., API keys).
    /// </summary>
    public bool IsSensitive { get; set; } = false;

    /// <summary>
    /// Default value if environment-specific value not set.
    /// </summary>
    public string? DefaultValue { get; set; }

    /// <summary>
    /// Validation schema ID (optional).
    /// </summary>
    public string? SchemaId { get; set; }

    /// <summary>
    /// User who created this configuration.
    /// </summary>
    public string? CreatedBy { get; set; }

    /// <summary>
    /// User who last updated this configuration.
    /// </summary>
    public string? UpdatedBy { get; set; }

    /// <summary>
    /// Creation timestamp (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last update timestamp (UTC).
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Soft delete timestamp (UTC). Null if not deleted.
    /// </summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>
    /// Validates the configuration.
    /// </summary>
    public bool IsValid(out List<string> errors)
    {
        errors = new List<string>();

        if (string.IsNullOrWhiteSpace(ConfigId))
            errors.Add("ConfigId is required");

        if (string.IsNullOrWhiteSpace(TenantId))
            errors.Add("TenantId is required");

        if (string.IsNullOrWhiteSpace(Key))
            errors.Add("Key is required");
        else if (!System.Text.RegularExpressions.Regex.IsMatch(Key, @"^[a-zA-Z0-9._-]+$"))
            errors.Add("Key must contain only alphanumeric characters, dots, underscores, and hyphens");

        if (string.IsNullOrWhiteSpace(Value))
            errors.Add("Value is required");

        if (Version < 1)
            errors.Add("Version must be at least 1");

        // Type-specific validation
        if (!ValidateValueType(Value, Type, out string? typeError))
            errors.Add(typeError!);

        return errors.Count == 0;
    }

    /// <summary>
    /// Validates that the value matches the specified type.
    /// </summary>
    private bool ValidateValueType(string value, ConfigValueType type, out string? error)
    {
        error = null;
        try
        {
            switch (type)
            {
                case ConfigValueType.Boolean:
                    if (!bool.TryParse(value, out _))
                    {
                        error = "Value must be a valid boolean (true/false)";
                        return false;
                    }
                    break;

                case ConfigValueType.Number:
                    if (!double.TryParse(value, out _))
                    {
                        error = "Value must be a valid number";
                        return false;
                    }
                    break;

                case ConfigValueType.JSON:
                    try
                    {
                        System.Text.Json.JsonDocument.Parse(value);
                    }
                    catch
                    {
                        error = "Value must be valid JSON";
                        return false;
                    }
                    break;

                case ConfigValueType.String:
                    // String values are always valid
                    break;
            }
            return true;
        }
        catch (Exception ex)
        {
            error = $"Value validation failed: {ex.Message}";
            return false;
        }
    }

    /// <summary>
    /// Gets the typed value.
    /// </summary>
    public T? GetValue<T>()
    {
        if (string.IsNullOrWhiteSpace(Value))
            return default;

        try
        {
            if (typeof(T) == typeof(string))
                return (T)(object)Value;

            return System.Text.Json.JsonSerializer.Deserialize<T>(Value);
        }
        catch
        {
            return default;
        }
    }

    /// <summary>
    /// Checks if this configuration is active.
    /// </summary>
    public bool IsActive() => DeletedAt == null;
}
```

---

## ConfigVersion

Represents a version history entry for a configuration.

**File:** `src/HotSwap.MultiTenantConfig.Domain/Models/ConfigVersion.cs`

```csharp
namespace HotSwap.MultiTenantConfig.Domain.Models;

/// <summary>
/// Represents a version history entry for a configuration.
/// </summary>
public class ConfigVersion
{
    /// <summary>
    /// Unique version identifier (GUID format).
    /// </summary>
    public required string VersionId { get; set; }

    /// <summary>
    /// Configuration identifier this version belongs to.
    /// </summary>
    public required string ConfigId { get; set; }

    /// <summary>
    /// Version number (incremental: 1, 2, 3...).
    /// </summary>
    public int Version { get; set; }

    /// <summary>
    /// Configuration value at this version.
    /// </summary>
    public required string Value { get; set; }

    /// <summary>
    /// Previous value (null for version 1).
    /// </summary>
    public string? PreviousValue { get; set; }

    /// <summary>
    /// Change type (Created, Updated, Deleted, Rolled Back).
    /// </summary>
    public ConfigChangeType ChangeType { get; set; }

    /// <summary>
    /// Change description provided by user.
    /// </summary>
    public string? ChangeDescription { get; set; }

    /// <summary>
    /// User who made this change.
    /// </summary>
    public required string ChangedBy { get; set; }

    /// <summary>
    /// Environment this change applies to.
    /// </summary>
    public ConfigEnvironment Environment { get; set; }

    /// <summary>
    /// IP address of the user who made the change.
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// Trace ID for distributed tracing.
    /// </summary>
    public string? TraceId { get; set; }

    /// <summary>
    /// Version creation timestamp (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Calculates the difference between this version and another.
    /// </summary>
    public ConfigDiff GetDiff(ConfigVersion otherVersion)
    {
        return new ConfigDiff
        {
            FromVersion = otherVersion.Version,
            ToVersion = this.Version,
            OldValue = otherVersion.Value,
            NewValue = this.Value,
            ChangeType = this.ChangeType,
            ChangedAt = this.CreatedAt
        };
    }
}
```

---

## ApprovalRequest

Represents an approval request for a configuration change.

**File:** `src/HotSwap.MultiTenantConfig.Domain/Models/ApprovalRequest.cs`

```csharp
namespace HotSwap.MultiTenantConfig.Domain.Models;

/// <summary>
/// Represents an approval request for a configuration change.
/// </summary>
public class ApprovalRequest
{
    /// <summary>
    /// Unique approval request identifier (GUID format).
    /// </summary>
    public required string ApprovalId { get; set; }

    /// <summary>
    /// Configuration identifier this approval is for.
    /// </summary>
    public required string ConfigId { get; set; }

    /// <summary>
    /// Tenant identifier.
    /// </summary>
    public required string TenantId { get; set; }

    /// <summary>
    /// Target environment for deployment.
    /// </summary>
    public ConfigEnvironment TargetEnvironment { get; set; }

    /// <summary>
    /// New configuration value to be deployed.
    /// </summary>
    public required string ProposedValue { get; set; }

    /// <summary>
    /// Current configuration value (before change).
    /// </summary>
    public string? CurrentValue { get; set; }

    /// <summary>
    /// Change description provided by requester.
    /// </summary>
    public string? ChangeDescription { get; set; }

    /// <summary>
    /// User who requested this approval.
    /// </summary>
    public required string RequestedBy { get; set; }

    /// <summary>
    /// Current approval status.
    /// </summary>
    public ApprovalStatus Status { get; set; } = ApprovalStatus.Pending;

    /// <summary>
    /// Required approval level (1-3).
    /// </summary>
    public int RequiredApprovalLevel { get; set; } = 1;

    /// <summary>
    /// Current approval level achieved.
    /// </summary>
    public int CurrentApprovalLevel { get; set; } = 0;

    /// <summary>
    /// List of approvers.
    /// </summary>
    public List<Approver> Approvers { get; set; } = new();

    /// <summary>
    /// Approval request creation timestamp (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Approval deadline (UTC). Auto-reject after this time.
    /// </summary>
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddHours(72);

    /// <summary>
    /// Approval/rejection timestamp (UTC).
    /// </summary>
    public DateTime? ResolvedAt { get; set; }

    /// <summary>
    /// User who approved/rejected (for final level).
    /// </summary>
    public string? ResolvedBy { get; set; }

    /// <summary>
    /// Approval/rejection comments.
    /// </summary>
    public string? ResolvedComments { get; set; }

    /// <summary>
    /// Deployment ID (set after approval and deployment).
    /// </summary>
    public string? DeploymentId { get; set; }

    /// <summary>
    /// Checks if the approval request has expired.
    /// </summary>
    public bool IsExpired() => DateTime.UtcNow > ExpiresAt;

    /// <summary>
    /// Checks if the approval is pending.
    /// </summary>
    public bool IsPending() => Status == ApprovalStatus.Pending && !IsExpired();

    /// <summary>
    /// Checks if the approval has all required levels.
    /// </summary>
    public bool HasAllApprovals() => CurrentApprovalLevel >= RequiredApprovalLevel;

    /// <summary>
    /// Adds an approval at a specific level.
    /// </summary>
    public void AddApproval(string userId, int level, string? comments = null)
    {
        Approvers.Add(new Approver
        {
            UserId = userId,
            Level = level,
            Status = ApprovalStatus.Approved,
            Comments = comments,
            ApprovedAt = DateTime.UtcNow
        });

        CurrentApprovalLevel = Math.Max(CurrentApprovalLevel, level);

        if (HasAllApprovals())
        {
            Status = ApprovalStatus.Approved;
            ResolvedAt = DateTime.UtcNow;
            ResolvedBy = userId;
            ResolvedComments = comments;
        }
    }

    /// <summary>
    /// Rejects the approval request.
    /// </summary>
    public void Reject(string userId, string? comments = null)
    {
        Status = ApprovalStatus.Rejected;
        ResolvedAt = DateTime.UtcNow;
        ResolvedBy = userId;
        ResolvedComments = comments;

        Approvers.Add(new Approver
        {
            UserId = userId,
            Level = CurrentApprovalLevel + 1,
            Status = ApprovalStatus.Rejected,
            Comments = comments,
            ApprovedAt = DateTime.UtcNow
        });
    }
}

/// <summary>
/// Represents an approver in the approval chain.
/// </summary>
public class Approver
{
    /// <summary>
    /// User ID of the approver.
    /// </summary>
    public required string UserId { get; set; }

    /// <summary>
    /// Approval level (1-3).
    /// </summary>
    public int Level { get; set; }

    /// <summary>
    /// Approval status.
    /// </summary>
    public ApprovalStatus Status { get; set; }

    /// <summary>
    /// Approval comments (optional).
    /// </summary>
    public string? Comments { get; set; }

    /// <summary>
    /// Approval timestamp (UTC).
    /// </summary>
    public DateTime ApprovedAt { get; set; }
}
```

---

## Deployment

Represents a configuration deployment.

**File:** `src/HotSwap.MultiTenantConfig.Domain/Models/Deployment.cs`

```csharp
namespace HotSwap.MultiTenantConfig.Domain.Models;

/// <summary>
/// Represents a configuration deployment.
/// </summary>
public class Deployment
{
    /// <summary>
    /// Unique deployment identifier (GUID format).
    /// </summary>
    public required string DeploymentId { get; set; }

    /// <summary>
    /// Configuration identifier being deployed.
    /// </summary>
    public required string ConfigId { get; set; }

    /// <summary>
    /// Tenant identifier.
    /// </summary>
    public required string TenantId { get; set; }

    /// <summary>
    /// Target environment.
    /// </summary>
    public ConfigEnvironment Environment { get; set; }

    /// <summary>
    /// Deployment strategy used.
    /// </summary>
    public DeploymentStrategy Strategy { get; set; }

    /// <summary>
    /// Current deployment status.
    /// </summary>
    public DeploymentStatus Status { get; set; } = DeploymentStatus.InProgress;

    /// <summary>
    /// Configuration value being deployed.
    /// </summary>
    public required string Value { get; set; }

    /// <summary>
    /// Previous configuration value (for rollback).
    /// </summary>
    public string? PreviousValue { get; set; }

    /// <summary>
    /// Deployment progress (0-100%).
    /// </summary>
    public int Progress { get; set; } = 0;

    /// <summary>
    /// Current canary percentage (for canary deployments).
    /// </summary>
    public int? CanaryPercentage { get; set; }

    /// <summary>
    /// User who initiated this deployment.
    /// </summary>
    public required string DeployedBy { get; set; }

    /// <summary>
    /// Deployment start timestamp (UTC).
    /// </summary>
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Deployment completion timestamp (UTC).
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Deployment duration (calculated).
    /// </summary>
    public TimeSpan? Duration => CompletedAt.HasValue ? CompletedAt.Value - StartedAt : null;

    /// <summary>
    /// Error message (if deployment failed).
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Deployment metrics (error rate, latency).
    /// </summary>
    public DeploymentMetrics? Metrics { get; set; }

    /// <summary>
    /// Whether this deployment was rolled back.
    /// </summary>
    public bool WasRolledBack { get; set; } = false;

    /// <summary>
    /// Rollback reason (if rolled back).
    /// </summary>
    public string? RollbackReason { get; set; }

    /// <summary>
    /// Rollback timestamp (UTC).
    /// </summary>
    public DateTime? RolledBackAt { get; set; }

    /// <summary>
    /// Checks if the deployment is in progress.
    /// </summary>
    public bool IsInProgress() => Status == DeploymentStatus.InProgress;

    /// <summary>
    /// Checks if the deployment is complete.
    /// </summary>
    public bool IsComplete() => Status == DeploymentStatus.Completed;

    /// <summary>
    /// Checks if the deployment failed.
    /// </summary>
    public bool IsFailed() => Status == DeploymentStatus.Failed;

    /// <summary>
    /// Marks the deployment as complete.
    /// </summary>
    public void MarkComplete()
    {
        Status = DeploymentStatus.Completed;
        Progress = 100;
        CompletedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Marks the deployment as failed.
    /// </summary>
    public void MarkFailed(string errorMessage)
    {
        Status = DeploymentStatus.Failed;
        ErrorMessage = errorMessage;
        CompletedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Marks the deployment as rolled back.
    /// </summary>
    public void MarkRolledBack(string reason)
    {
        Status = DeploymentStatus.RolledBack;
        WasRolledBack = true;
        RollbackReason = reason;
        RolledBackAt = DateTime.UtcNow;
    }
}

/// <summary>
/// Deployment metrics for monitoring.
/// </summary>
public class DeploymentMetrics
{
    /// <summary>
    /// Error rate before deployment (%).
    /// </summary>
    public double BaselineErrorRate { get; set; }

    /// <summary>
    /// Error rate after deployment (%).
    /// </summary>
    public double CurrentErrorRate { get; set; }

    /// <summary>
    /// Response time p99 before deployment (ms).
    /// </summary>
    public double BaselineLatencyP99 { get; set; }

    /// <summary>
    /// Response time p99 after deployment (ms).
    /// </summary>
    public double CurrentLatencyP99 { get; set; }

    /// <summary>
    /// Number of requests processed.
    /// </summary>
    public long RequestCount { get; set; }

    /// <summary>
    /// Last metrics update timestamp (UTC).
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Checks if error rate has increased significantly.
    /// </summary>
    public bool HasErrorRateIncreased() => CurrentErrorRate > (BaselineErrorRate * 1.05); // 5% increase

    /// <summary>
    /// Checks if latency has increased significantly.
    /// </summary>
    public bool HasLatencyIncreased() => CurrentLatencyP99 > (BaselineLatencyP99 * 2.0); // 2x increase
}
```

---

## Enumerations

### TenantTier

```csharp
namespace HotSwap.MultiTenantConfig.Domain.Enums;

/// <summary>
/// Represents the tenant subscription tier.
/// </summary>
public enum TenantTier
{
    /// <summary>
    /// Free tier (limited features).
    /// </summary>
    Free,

    /// <summary>
    /// Professional tier.
    /// </summary>
    Pro,

    /// <summary>
    /// Enterprise tier (full features).
    /// </summary>
    Enterprise
}
```

### TenantStatus

```csharp
namespace HotSwap.MultiTenantConfig.Domain.Enums;

/// <summary>
/// Represents the tenant status.
/// </summary>
public enum TenantStatus
{
    /// <summary>
    /// Tenant is active and operational.
    /// </summary>
    Active,

    /// <summary>
    /// Tenant is suspended (temporarily disabled).
    /// </summary>
    Suspended,

    /// <summary>
    /// Tenant is marked for deletion.
    /// </summary>
    Deleted
}
```

### ConfigValueType

```csharp
namespace HotSwap.MultiTenantConfig.Domain.Enums;

/// <summary>
/// Represents the configuration value type.
/// </summary>
public enum ConfigValueType
{
    /// <summary>
    /// String value.
    /// </summary>
    String,

    /// <summary>
    /// Numeric value (integer or decimal).
    /// </summary>
    Number,

    /// <summary>
    /// Boolean value (true/false).
    /// </summary>
    Boolean,

    /// <summary>
    /// JSON object or array.
    /// </summary>
    JSON
}
```

### ConfigEnvironment

```csharp
namespace HotSwap.MultiTenantConfig.Domain.Enums;

/// <summary>
/// Represents the configuration environment.
/// </summary>
public enum ConfigEnvironment
{
    /// <summary>
    /// Development environment.
    /// </summary>
    Development,

    /// <summary>
    /// QA/Test environment.
    /// </summary>
    QA,

    /// <summary>
    /// Staging environment (pre-production).
    /// </summary>
    Staging,

    /// <summary>
    /// Production environment.
    /// </summary>
    Production
}
```

### ConfigChangeType

```csharp
namespace HotSwap.MultiTenantConfig.Domain.Enums;

/// <summary>
/// Represents the type of configuration change.
/// </summary>
public enum ConfigChangeType
{
    /// <summary>
    /// Configuration was created.
    /// </summary>
    Created,

    /// <summary>
    /// Configuration value was updated.
    /// </summary>
    Updated,

    /// <summary>
    /// Configuration was deleted.
    /// </summary>
    Deleted,

    /// <summary>
    /// Configuration was rolled back to a previous version.
    /// </summary>
    RolledBack,

    /// <summary>
    /// Configuration was promoted to another environment.
    /// </summary>
    Promoted
}
```

### ApprovalStatus

```csharp
namespace HotSwap.MultiTenantConfig.Domain.Enums;

/// <summary>
/// Represents the approval request status.
/// </summary>
public enum ApprovalStatus
{
    /// <summary>
    /// Approval is pending review.
    /// </summary>
    Pending,

    /// <summary>
    /// Approval request was approved.
    /// </summary>
    Approved,

    /// <summary>
    /// Approval request was rejected.
    /// </summary>
    Rejected,

    /// <summary>
    /// Approval request expired (auto-rejected).
    /// </summary>
    Expired
}
```

### DeploymentStrategy

```csharp
namespace HotSwap.MultiTenantConfig.Domain.Enums;

/// <summary>
/// Represents the deployment strategy.
/// </summary>
public enum DeploymentStrategy
{
    /// <summary>
    /// Direct deployment (immediate, all at once).
    /// </summary>
    Direct,

    /// <summary>
    /// Canary deployment (gradual rollout with monitoring).
    /// </summary>
    Canary,

    /// <summary>
    /// Blue-green deployment (zero-downtime switch).
    /// </summary>
    BlueGreen,

    /// <summary>
    /// Rolling deployment (progressive tenant rollout).
    /// </summary>
    Rolling
}
```

### DeploymentStatus

```csharp
namespace HotSwap.MultiTenantConfig.Domain.Enums;

/// <summary>
/// Represents the deployment status.
/// </summary>
public enum DeploymentStatus
{
    /// <summary>
    /// Deployment is in progress.
    /// </summary>
    InProgress,

    /// <summary>
    /// Deployment completed successfully.
    /// </summary>
    Completed,

    /// <summary>
    /// Deployment failed.
    /// </summary>
    Failed,

    /// <summary>
    /// Deployment was rolled back.
    /// </summary>
    RolledBack
}
```

---

## Value Objects

### ConfigDiff

**File:** `src/HotSwap.MultiTenantConfig.Domain/ValueObjects/ConfigDiff.cs`

```csharp
namespace HotSwap.MultiTenantConfig.Domain.ValueObjects;

/// <summary>
/// Represents the difference between two configuration versions.
/// </summary>
public class ConfigDiff
{
    /// <summary>
    /// Starting version number.
    /// </summary>
    public int FromVersion { get; set; }

    /// <summary>
    /// Ending version number.
    /// </summary>
    public int ToVersion { get; set; }

    /// <summary>
    /// Old configuration value.
    /// </summary>
    public string? OldValue { get; set; }

    /// <summary>
    /// New configuration value.
    /// </summary>
    public string? NewValue { get; set; }

    /// <summary>
    /// Type of change.
    /// </summary>
    public ConfigChangeType ChangeType { get; set; }

    /// <summary>
    /// When the change occurred.
    /// </summary>
    public DateTime ChangedAt { get; set; }

    /// <summary>
    /// Gets a human-readable description of the change.
    /// </summary>
    public string GetDescription()
    {
        return ChangeType switch
        {
            ConfigChangeType.Created => $"Created with value: {NewValue}",
            ConfigChangeType.Updated => $"Changed from '{OldValue}' to '{NewValue}'",
            ConfigChangeType.Deleted => $"Deleted (was: {OldValue})",
            ConfigChangeType.RolledBack => $"Rolled back from '{OldValue}' to '{NewValue}'",
            ConfigChangeType.Promoted => $"Promoted from '{OldValue}' to '{NewValue}'",
            _ => "Unknown change"
        };
    }
}
```

---

## Validation Examples

### Tenant Validation

```csharp
var tenant = new Tenant
{
    TenantId = "acme-corp",
    Name = "ACME Corporation",
    Tier = TenantTier.Enterprise,
    MaxConfigurations = 1000,
    MaxEnvironments = 4,
    ContactEmail = "admin@acme.com"
};

if (!tenant.IsValid(out var errors))
{
    foreach (var error in errors)
    {
        Console.WriteLine($"Validation Error: {error}");
    }
}
```

### Configuration Validation

```csharp
var config = new Configuration
{
    ConfigId = Guid.NewGuid().ToString(),
    TenantId = "acme-corp",
    Key = "feature.new_dashboard",
    Value = "true",
    Type = ConfigValueType.Boolean,
    Environment = ConfigEnvironment.Production
};

if (!config.IsValid(out var errors))
{
    Console.WriteLine("Configuration validation failed:");
    errors.ForEach(Console.WriteLine);
}

// Get typed value
bool featureEnabled = config.GetValue<bool>();
```

---

**Last Updated:** 2025-11-23
**Namespace:** `HotSwap.MultiTenantConfig.Domain`
