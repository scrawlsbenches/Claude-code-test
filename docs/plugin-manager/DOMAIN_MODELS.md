# Domain Models Reference

**Version:** 1.0.0
**Namespace:** `HotSwap.Distributed.Domain.Models`

---

## Table of Contents

1. [Plugin](#plugin)
2. [PluginVersion](#pluginversion)
3. [PluginDeployment](#plugindeployment)
4. [TenantPluginConfig](#tenantpluginconfig)
5. [PluginManifest](#pluginmanifest)
6. [PluginHealthCheck](#pluginhealthcheck)
7. [Enumerations](#enumerations)
8. [Value Objects](#value-objects)

---

## Plugin

Represents a plugin registered in the system.

**File:** `src/HotSwap.Distributed.Domain/Models/Plugin.cs`

```csharp
namespace HotSwap.Distributed.Domain.Models;

/// <summary>
/// Represents a plugin in the plugin management system.
/// </summary>
public class Plugin
{
    /// <summary>
    /// Unique plugin identifier (GUID format).
    /// </summary>
    public required string PluginId { get; set; }

    /// <summary>
    /// Plugin name (unique, lowercase, alphanumeric with dashes).
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Human-readable display name.
    /// </summary>
    public required string DisplayName { get; set; }

    /// <summary>
    /// Plugin description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Plugin type/category.
    /// </summary>
    public PluginType Type { get; set; }

    /// <summary>
    /// Current active version.
    /// </summary>
    public string? CurrentVersion { get; set; }

    /// <summary>
    /// Plugin author/developer.
    /// </summary>
    public string? Author { get; set; }

    /// <summary>
    /// Plugin documentation URL.
    /// </summary>
    public string? DocumentationUrl { get; set; }

    /// <summary>
    /// Plugin status.
    /// </summary>
    public PluginStatus Status { get; set; } = PluginStatus.Draft;

    /// <summary>
    /// Plugin tags for categorization.
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// Plugin creation timestamp (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last update timestamp (UTC).
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Plugin deprecation timestamp (UTC).
    /// </summary>
    public DateTime? DeprecatedAt { get; set; }

    /// <summary>
    /// Deprecation reason.
    /// </summary>
    public string? DeprecationReason { get; set; }

    /// <summary>
    /// Validates the plugin configuration.
    /// </summary>
    public bool IsValid(out List<string> errors)
    {
        errors = new List<string>();

        if (string.IsNullOrWhiteSpace(PluginId))
            errors.Add("PluginId is required");

        if (string.IsNullOrWhiteSpace(Name))
            errors.Add("Name is required");
        else if (!System.Text.RegularExpressions.Regex.IsMatch(Name, @"^[a-z0-9-]+$"))
            errors.Add("Name must contain only lowercase alphanumeric characters and dashes");

        if (string.IsNullOrWhiteSpace(DisplayName))
            errors.Add("DisplayName is required");

        return errors.Count == 0;
    }

    /// <summary>
    /// Checks if the plugin is deprecated.
    /// </summary>
    public bool IsDeprecated() => Status == PluginStatus.Deprecated;

    /// <summary>
    /// Checks if the plugin is active.
    /// </summary>
    public bool IsActive() => Status == PluginStatus.Active;
}
```

---

## PluginVersion

Represents a specific version of a plugin.

**File:** `src/HotSwap.Distributed.Domain/Models/PluginVersion.cs`

```csharp
namespace HotSwap.Distributed.Domain.Models;

/// <summary>
/// Represents a specific version of a plugin.
/// </summary>
public class PluginVersion
{
    /// <summary>
    /// Unique version identifier (GUID format).
    /// </summary>
    public required string VersionId { get; set; }

    /// <summary>
    /// Parent plugin identifier.
    /// </summary>
    public required string PluginId { get; set; }

    /// <summary>
    /// Semantic version (e.g., "1.5.0").
    /// </summary>
    public required string Version { get; set; }

    /// <summary>
    /// Release notes for this version.
    /// </summary>
    public string? ReleaseNotes { get; set; }

    /// <summary>
    /// Plugin manifest (metadata, dependencies, entry point).
    /// </summary>
    public required PluginManifest Manifest { get; set; }

    /// <summary>
    /// Binary storage location (MinIO/S3 path).
    /// </summary>
    public required string BinaryPath { get; set; }

    /// <summary>
    /// Binary checksum (SHA256).
    /// </summary>
    public required string Checksum { get; set; }

    /// <summary>
    /// Binary size in bytes.
    /// </summary>
    public long BinarySize { get; set; }

    /// <summary>
    /// Version status.
    /// </summary>
    public VersionStatus Status { get; set; } = VersionStatus.Draft;

    /// <summary>
    /// Breaking changes flag.
    /// </summary>
    public bool IsBreakingChange { get; set; } = false;

    /// <summary>
    /// Approved by (admin user).
    /// </summary>
    public string? ApprovedBy { get; set; }

    /// <summary>
    /// Approval timestamp (UTC).
    /// </summary>
    public DateTime? ApprovedAt { get; set; }

    /// <summary>
    /// Version creation timestamp (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Deprecated timestamp (UTC).
    /// </summary>
    public DateTime? DeprecatedAt { get; set; }

    /// <summary>
    /// Validates the plugin version.
    /// </summary>
    public bool IsValid(out List<string> errors)
    {
        errors = new List<string>();

        if (string.IsNullOrWhiteSpace(VersionId))
            errors.Add("VersionId is required");

        if (string.IsNullOrWhiteSpace(PluginId))
            errors.Add("PluginId is required");

        if (string.IsNullOrWhiteSpace(Version))
            errors.Add("Version is required");
        else if (!IsValidSemanticVersion(Version))
            errors.Add("Version must be valid semantic version (e.g., 1.5.0)");

        if (string.IsNullOrWhiteSpace(BinaryPath))
            errors.Add("BinaryPath is required");

        if (string.IsNullOrWhiteSpace(Checksum))
            errors.Add("Checksum is required");

        if (BinarySize <= 0)
            errors.Add("BinarySize must be greater than 0");

        if (BinarySize > 104857600) // 100 MB
            errors.Add("BinarySize exceeds maximum of 100 MB");

        return errors.Count == 0;
    }

    /// <summary>
    /// Validates semantic version format.
    /// </summary>
    private bool IsValidSemanticVersion(string version)
    {
        var regex = new System.Text.RegularExpressions.Regex(@"^\d+\.\d+\.\d+(-[a-zA-Z0-9.-]+)?$");
        return regex.IsMatch(version);
    }

    /// <summary>
    /// Checks if version is approved for production.
    /// </summary>
    public bool IsApproved() => Status == VersionStatus.Approved;
}
```

---

## PluginDeployment

Represents a plugin deployment to an environment.

**File:** `src/HotSwap.Distributed.Domain/Models/PluginDeployment.cs`

```csharp
namespace HotSwap.Distributed.Domain.Models;

/// <summary>
/// Represents a plugin deployment operation.
/// </summary>
public class PluginDeployment
{
    /// <summary>
    /// Unique deployment identifier (GUID format).
    /// </summary>
    public required string DeploymentId { get; set; }

    /// <summary>
    /// Plugin identifier.
    /// </summary>
    public required string PluginId { get; set; }

    /// <summary>
    /// Plugin version to deploy.
    /// </summary>
    public required string Version { get; set; }

    /// <summary>
    /// Target environment.
    /// </summary>
    public required string Environment { get; set; }

    /// <summary>
    /// Deployment strategy.
    /// </summary>
    public DeploymentStrategy Strategy { get; set; }

    /// <summary>
    /// Deployment configuration (strategy-specific).
    /// </summary>
    public Dictionary<string, string> Config { get; set; } = new();

    /// <summary>
    /// Current deployment status.
    /// </summary>
    public DeploymentStatus Status { get; set; } = DeploymentStatus.Pending;

    /// <summary>
    /// Deployment progress percentage (0-100).
    /// </summary>
    public int ProgressPercentage { get; set; } = 0;

    /// <summary>
    /// Initiated by (user identifier).
    /// </summary>
    public required string InitiatedBy { get; set; }

    /// <summary>
    /// Deployment start timestamp (UTC).
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// Deployment completion timestamp (UTC).
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Previous version (for rollback).
    /// </summary>
    public string? PreviousVersion { get; set; }

    /// <summary>
    /// Rollback timestamp (UTC).
    /// </summary>
    public DateTime? RolledBackAt { get; set; }

    /// <summary>
    /// Rollback reason.
    /// </summary>
    public string? RollbackReason { get; set; }

    /// <summary>
    /// Deployment error message.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Health check results.
    /// </summary>
    public List<PluginHealthCheck> HealthChecks { get; set; } = new();

    /// <summary>
    /// Affected tenant count.
    /// </summary>
    public int AffectedTenants { get; set; } = 0;

    /// <summary>
    /// Deployment creation timestamp (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Validates the deployment configuration.
    /// </summary>
    public bool IsValid(out List<string> errors)
    {
        errors = new List<string>();

        if (string.IsNullOrWhiteSpace(DeploymentId))
            errors.Add("DeploymentId is required");

        if (string.IsNullOrWhiteSpace(PluginId))
            errors.Add("PluginId is required");

        if (string.IsNullOrWhiteSpace(Version))
            errors.Add("Version is required");

        if (string.IsNullOrWhiteSpace(Environment))
            errors.Add("Environment is required");

        if (string.IsNullOrWhiteSpace(InitiatedBy))
            errors.Add("InitiatedBy is required");

        return errors.Count == 0;
    }

    /// <summary>
    /// Checks if deployment is in progress.
    /// </summary>
    public bool IsInProgress() => Status == DeploymentStatus.InProgress;

    /// <summary>
    /// Checks if deployment completed successfully.
    /// </summary>
    public bool IsCompleted() => Status == DeploymentStatus.Completed;

    /// <summary>
    /// Checks if deployment failed.
    /// </summary>
    public bool IsFailed() => Status == DeploymentStatus.Failed;

    /// <summary>
    /// Checks if deployment was rolled back.
    /// </summary>
    public bool IsRolledBack() => Status == DeploymentStatus.RolledBack;

    /// <summary>
    /// Calculates deployment duration.
    /// </summary>
    public TimeSpan? GetDuration()
    {
        if (StartedAt == null) return null;
        var endTime = CompletedAt ?? DateTime.UtcNow;
        return endTime - StartedAt.Value;
    }
}
```

---

## TenantPluginConfig

Represents tenant-specific plugin configuration.

**File:** `src/HotSwap.Distributed.Domain/Models/TenantPluginConfig.cs`

```csharp
namespace HotSwap.Distributed.Domain.Models;

/// <summary>
/// Represents tenant-specific plugin configuration.
/// </summary>
public class TenantPluginConfig
{
    /// <summary>
    /// Unique configuration identifier (GUID format).
    /// </summary>
    public required string ConfigId { get; set; }

    /// <summary>
    /// Tenant identifier.
    /// </summary>
    public required string TenantId { get; set; }

    /// <summary>
    /// Plugin identifier.
    /// </summary>
    public required string PluginId { get; set; }

    /// <summary>
    /// Plugin enabled for this tenant.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Pinned plugin version (null = use latest).
    /// </summary>
    public string? PinnedVersion { get; set; }

    /// <summary>
    /// Tenant-specific plugin configuration (JSON).
    /// </summary>
    public Dictionary<string, string> Configuration { get; set; } = new();

    /// <summary>
    /// Encrypted credentials/secrets for this plugin.
    /// </summary>
    public Dictionary<string, string> Secrets { get; set; } = new();

    /// <summary>
    /// Resource quotas for this tenant.
    /// </summary>
    public PluginQuotas Quotas { get; set; } = new();

    /// <summary>
    /// Configuration enabled by (user identifier).
    /// </summary>
    public required string EnabledBy { get; set; }

    /// <summary>
    /// Configuration enabled at (UTC).
    /// </summary>
    public DateTime EnabledAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last update timestamp (UTC).
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Validates the tenant plugin configuration.
    /// </summary>
    public bool IsValid(out List<string> errors)
    {
        errors = new List<string>();

        if (string.IsNullOrWhiteSpace(ConfigId))
            errors.Add("ConfigId is required");

        if (string.IsNullOrWhiteSpace(TenantId))
            errors.Add("TenantId is required");

        if (string.IsNullOrWhiteSpace(PluginId))
            errors.Add("PluginId is required");

        if (string.IsNullOrWhiteSpace(EnabledBy))
            errors.Add("EnabledBy is required");

        return errors.Count == 0;
    }
}

/// <summary>
/// Resource quotas for tenant plugin execution.
/// </summary>
public class PluginQuotas
{
    /// <summary>
    /// Maximum requests per minute.
    /// </summary>
    public int MaxRequestsPerMinute { get; set; } = 100;

    /// <summary>
    /// Maximum concurrent executions.
    /// </summary>
    public int MaxConcurrentExecutions { get; set; } = 10;

    /// <summary>
    /// Maximum execution time per request (seconds).
    /// </summary>
    public int MaxExecutionTimeSeconds { get; set; } = 30;

    /// <summary>
    /// Maximum memory usage (MB).
    /// </summary>
    public int MaxMemoryMB { get; set; } = 512;
}
```

---

## PluginManifest

Represents plugin metadata and dependencies.

**File:** `src/HotSwap.Distributed.Domain/Models/PluginManifest.cs`

```csharp
namespace HotSwap.Distributed.Domain.Models;

/// <summary>
/// Represents plugin manifest (metadata and dependencies).
/// </summary>
public class PluginManifest
{
    /// <summary>
    /// Plugin name.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Plugin version.
    /// </summary>
    public required string Version { get; set; }

    /// <summary>
    /// Entry point (DLL or class name).
    /// </summary>
    public required string EntryPoint { get; set; }

    /// <summary>
    /// Target framework (e.g., "net8.0").
    /// </summary>
    public required string TargetFramework { get; set; }

    /// <summary>
    /// Plugin author.
    /// </summary>
    public string? Author { get; set; }

    /// <summary>
    /// Plugin description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Plugin dependencies.
    /// </summary>
    public List<PluginDependency> Dependencies { get; set; } = new();

    /// <summary>
    /// Required permissions.
    /// </summary>
    public List<string> RequiredPermissions { get; set; } = new();

    /// <summary>
    /// Plugin capabilities.
    /// </summary>
    public List<string> Capabilities { get; set; } = new();

    /// <summary>
    /// Configuration schema (JSON Schema).
    /// </summary>
    public string? ConfigurationSchema { get; set; }

    /// <summary>
    /// Minimum platform version required.
    /// </summary>
    public string? MinPlatformVersion { get; set; }

    /// <summary>
    /// Validates the manifest.
    /// </summary>
    public bool IsValid(out List<string> errors)
    {
        errors = new List<string>();

        if (string.IsNullOrWhiteSpace(Name))
            errors.Add("Name is required");

        if (string.IsNullOrWhiteSpace(Version))
            errors.Add("Version is required");

        if (string.IsNullOrWhiteSpace(EntryPoint))
            errors.Add("EntryPoint is required");

        if (string.IsNullOrWhiteSpace(TargetFramework))
            errors.Add("TargetFramework is required");

        // Validate dependencies
        foreach (var dependency in Dependencies)
        {
            if (!dependency.IsValid(out var depErrors))
            {
                errors.AddRange(depErrors.Select(e => $"Dependency '{dependency.Name}': {e}"));
            }
        }

        return errors.Count == 0;
    }
}

/// <summary>
/// Represents a plugin dependency.
/// </summary>
public class PluginDependency
{
    /// <summary>
    /// Dependency name (plugin name or NuGet package).
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Dependency version constraint (e.g., ">= 1.5.0").
    /// </summary>
    public required string VersionConstraint { get; set; }

    /// <summary>
    /// Dependency type.
    /// </summary>
    public DependencyType Type { get; set; }

    /// <summary>
    /// Optional flag (can be missing).
    /// </summary>
    public bool Optional { get; set; } = false;

    /// <summary>
    /// Validates the dependency.
    /// </summary>
    public bool IsValid(out List<string> errors)
    {
        errors = new List<string>();

        if (string.IsNullOrWhiteSpace(Name))
            errors.Add("Name is required");

        if (string.IsNullOrWhiteSpace(VersionConstraint))
            errors.Add("VersionConstraint is required");

        return errors.Count == 0;
    }
}
```

---

## PluginHealthCheck

Represents plugin health check result.

**File:** `src/HotSwap.Distributed.Domain/Models/PluginHealthCheck.cs`

```csharp
namespace HotSwap.Distributed.Domain.Models;

/// <summary>
/// Represents a plugin health check result.
/// </summary>
public class PluginHealthCheck
{
    /// <summary>
    /// Health check identifier (GUID format).
    /// </summary>
    public required string HealthCheckId { get; set; }

    /// <summary>
    /// Plugin identifier.
    /// </summary>
    public required string PluginId { get; set; }

    /// <summary>
    /// Plugin version.
    /// </summary>
    public required string Version { get; set; }

    /// <summary>
    /// Environment where check was performed.
    /// </summary>
    public required string Environment { get; set; }

    /// <summary>
    /// Health check type.
    /// </summary>
    public HealthCheckType Type { get; set; }

    /// <summary>
    /// Health check status.
    /// </summary>
    public HealthStatus Status { get; set; }

    /// <summary>
    /// Check execution timestamp (UTC).
    /// </summary>
    public DateTime CheckedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Check duration in milliseconds.
    /// </summary>
    public long DurationMs { get; set; }

    /// <summary>
    /// Error message (if failed).
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Additional health metrics.
    /// </summary>
    public Dictionary<string, double> Metrics { get; set; } = new();

    /// <summary>
    /// Checks if health check passed.
    /// </summary>
    public bool IsPassed() => Status == HealthStatus.Healthy;

    /// <summary>
    /// Checks if health check failed.
    /// </summary>
    public bool IsFailed() => Status == HealthStatus.Unhealthy;
}
```

---

## Enumerations

### PluginType

**File:** `src/HotSwap.Distributed.Domain/Enums/PluginType.cs`

```csharp
namespace HotSwap.Distributed.Domain.Enums;

/// <summary>
/// Represents the type/category of a plugin.
/// </summary>
public enum PluginType
{
    /// <summary>
    /// Payment gateway integration.
    /// </summary>
    PaymentGateway,

    /// <summary>
    /// Authentication provider.
    /// </summary>
    AuthProvider,

    /// <summary>
    /// Notification channel.
    /// </summary>
    NotificationChannel,

    /// <summary>
    /// Data export/import.
    /// </summary>
    DataProcessor,

    /// <summary>
    /// Reporting engine.
    /// </summary>
    ReportingEngine,

    /// <summary>
    /// Workflow automation.
    /// </summary>
    WorkflowAutomation,

    /// <summary>
    /// API integration.
    /// </summary>
    ApiIntegration,

    /// <summary>
    /// Custom business logic.
    /// </summary>
    BusinessLogic,

    /// <summary>
    /// Other/miscellaneous.
    /// </summary>
    Other
}
```

### PluginStatus

```csharp
namespace HotSwap.Distributed.Domain.Enums;

/// <summary>
/// Represents the status of a plugin.
/// </summary>
public enum PluginStatus
{
    /// <summary>
    /// Plugin is in draft state (not published).
    /// </summary>
    Draft,

    /// <summary>
    /// Plugin is active and available.
    /// </summary>
    Active,

    /// <summary>
    /// Plugin is deprecated (marked for removal).
    /// </summary>
    Deprecated,

    /// <summary>
    /// Plugin is archived (no longer available).
    /// </summary>
    Archived
}
```

### VersionStatus

```csharp
namespace HotSwap.Distributed.Domain.Enums;

/// <summary>
/// Represents the status of a plugin version.
/// </summary>
public enum VersionStatus
{
    /// <summary>
    /// Version is in draft state.
    /// </summary>
    Draft,

    /// <summary>
    /// Version is pending approval.
    /// </summary>
    PendingApproval,

    /// <summary>
    /// Version is approved for use.
    /// </summary>
    Approved,

    /// <summary>
    /// Version is deprecated.
    /// </summary>
    Deprecated
}
```

### DeploymentStrategy

```csharp
namespace HotSwap.Distributed.Domain.Enums;

/// <summary>
/// Represents plugin deployment strategy.
/// </summary>
public enum DeploymentStrategy
{
    /// <summary>
    /// Direct deployment (all at once).
    /// </summary>
    Direct,

    /// <summary>
    /// Canary deployment (gradual rollout).
    /// </summary>
    Canary,

    /// <summary>
    /// Blue-green deployment (parallel environments).
    /// </summary>
    BlueGreen,

    /// <summary>
    /// Rolling deployment (instance by instance).
    /// </summary>
    Rolling,

    /// <summary>
    /// A/B testing deployment (traffic splitting).
    /// </summary>
    ABTesting
}
```

### DeploymentStatus

```csharp
namespace HotSwap.Distributed.Domain.Enums;

/// <summary>
/// Represents the status of a deployment.
/// </summary>
public enum DeploymentStatus
{
    /// <summary>
    /// Deployment is pending.
    /// </summary>
    Pending,

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
    RolledBack,

    /// <summary>
    /// Deployment is paused.
    /// </summary>
    Paused
}
```

### HealthCheckType

```csharp
namespace HotSwap.Distributed.Domain.Enums;

/// <summary>
/// Represents the type of health check.
/// </summary>
public enum HealthCheckType
{
    /// <summary>
    /// Startup health check.
    /// </summary>
    Startup,

    /// <summary>
    /// Liveness check (is plugin running).
    /// </summary>
    Liveness,

    /// <summary>
    /// Readiness check (is plugin ready to handle requests).
    /// </summary>
    Readiness,

    /// <summary>
    /// Performance check (latency, throughput).
    /// </summary>
    Performance
}
```

### HealthStatus

```csharp
namespace HotSwap.Distributed.Domain.Enums;

/// <summary>
/// Represents health status.
/// </summary>
public enum HealthStatus
{
    /// <summary>
    /// Plugin is healthy.
    /// </summary>
    Healthy,

    /// <summary>
    /// Plugin is degraded (warning).
    /// </summary>
    Degraded,

    /// <summary>
    /// Plugin is unhealthy.
    /// </summary>
    Unhealthy
}
```

### DependencyType

```csharp
namespace HotSwap.Distributed.Domain.Enums;

/// <summary>
/// Represents dependency type.
/// </summary>
public enum DependencyType
{
    /// <summary>
    /// Plugin dependency (other plugin).
    /// </summary>
    Plugin,

    /// <summary>
    /// NuGet package dependency.
    /// </summary>
    NuGetPackage,

    /// <summary>
    /// Native library dependency.
    /// </summary>
    NativeLibrary,

    /// <summary>
    /// Framework dependency.
    /// </summary>
    Framework
}
```

---

## Value Objects

### DeploymentResult

**File:** `src/HotSwap.Distributed.Domain/ValueObjects/DeploymentResult.cs`

```csharp
namespace HotSwap.Distributed.Domain.ValueObjects;

/// <summary>
/// Result of a deployment operation.
/// </summary>
public class DeploymentResult
{
    /// <summary>
    /// Whether deployment was successful.
    /// </summary>
    public bool Success { get; private set; }

    /// <summary>
    /// Deployment identifier.
    /// </summary>
    public string DeploymentId { get; private set; } = string.Empty;

    /// <summary>
    /// Error message (if failed).
    /// </summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>
    /// Affected tenant count.
    /// </summary>
    public int AffectedTenants { get; private set; }

    /// <summary>
    /// Deployment timestamp (UTC).
    /// </summary>
    public DateTime Timestamp { get; private set; } = DateTime.UtcNow;

    public static DeploymentResult SuccessResult(string deploymentId, int affectedTenants)
    {
        return new DeploymentResult
        {
            Success = true,
            DeploymentId = deploymentId,
            AffectedTenants = affectedTenants
        };
    }

    public static DeploymentResult Failure(string errorMessage)
    {
        return new DeploymentResult
        {
            Success = false,
            ErrorMessage = errorMessage
        };
    }
}
```

---

## Validation Examples

### Plugin Validation

```csharp
var plugin = new Plugin
{
    PluginId = Guid.NewGuid().ToString(),
    Name = "payment-processor-stripe",
    DisplayName = "Stripe Payment Processor",
    Type = PluginType.PaymentGateway
};

if (!plugin.IsValid(out var errors))
{
    foreach (var error in errors)
    {
        Console.WriteLine($"Validation Error: {error}");
    }
}
```

### Plugin Version Validation

```csharp
var version = new PluginVersion
{
    VersionId = Guid.NewGuid().ToString(),
    PluginId = "plugin-123",
    Version = "1.5.0",
    Manifest = new PluginManifest
    {
        Name = "payment-processor-stripe",
        Version = "1.5.0",
        EntryPoint = "StripePaymentProcessor.dll",
        TargetFramework = "net8.0"
    },
    BinaryPath = "plugins/payment-processor-stripe/1.5.0/plugin.zip",
    Checksum = "sha256:abc123...",
    BinarySize = 5242880
};

if (!version.IsValid(out var errors))
{
    Console.WriteLine("Version validation failed:");
    errors.ForEach(Console.WriteLine);
}
```

---

**Last Updated:** 2025-11-23
**Namespace:** `HotSwap.Distributed.Domain`
