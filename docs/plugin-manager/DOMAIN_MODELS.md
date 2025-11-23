# Domain Models Reference

**Version:** 1.0.0
**Namespace:** `HotSwap.Distributed.Domain.Models.Plugins`

---

## Table of Contents

1. [Plugin](#plugin)
2. [PluginVersion](#pluginversion)
3. [PluginDeployment](#plugindeployment)
4. [Tenant](#tenant)
5. [PluginCapability](#plugincapability)
6. [PluginDependency](#plugindependency)
7. [PluginHealthCheck](#pluginhealthcheck)
8. [Enumerations](#enumerations)
9. [Value Objects](#value-objects)

---

## Plugin

Represents a plugin that extends platform functionality.

**File:** `src/HotSwap.Distributed.Domain/Models/Plugins/Plugin.cs`

```csharp
namespace HotSwap.Distributed.Domain.Models.Plugins;

/// <summary>
/// Represents a plugin in the platform.
/// </summary>
public class Plugin
{
    /// <summary>
    /// Unique plugin identifier (e.g., "payment-stripe").
    /// </summary>
    public required string PluginId { get; set; }

    /// <summary>
    /// Human-readable plugin name.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Plugin description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Plugin category.
    /// </summary>
    public PluginCategory Category { get; set; }

    /// <summary>
    /// Plugin author/publisher.
    /// </summary>
    public required string Publisher { get; set; }

    /// <summary>
    /// Plugin icon URL.
    /// </summary>
    public string? IconUrl { get; set; }

    /// <summary>
    /// Plugin documentation URL.
    /// </summary>
    public string? DocumentationUrl { get; set; }

    /// <summary>
    /// Plugin source repository URL.
    /// </summary>
    public string? RepositoryUrl { get; set; }

    /// <summary>
    /// Tags for plugin discovery.
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// Current status of the plugin.
    /// </summary>
    public PluginStatus Status { get; set; } = PluginStatus.Registered;

    /// <summary>
    /// Available versions of this plugin.
    /// </summary>
    public List<PluginVersion> Versions { get; set; } = new();

    /// <summary>
    /// Capabilities provided by this plugin.
    /// </summary>
    public List<PluginCapability> Capabilities { get; set; } = new();

    /// <summary>
    /// Plugin creation timestamp (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last update timestamp (UTC).
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Number of tenants using this plugin.
    /// </summary>
    public int InstallationCount { get; set; } = 0;

    /// <summary>
    /// Average rating (1-5 stars).
    /// </summary>
    public decimal AverageRating { get; set; } = 0;

    /// <summary>
    /// Validates the plugin metadata.
    /// </summary>
    public bool IsValid(out List<string> errors)
    {
        errors = new List<string>();

        if (string.IsNullOrWhiteSpace(PluginId))
            errors.Add("PluginId is required");
        else if (!System.Text.RegularExpressions.Regex.IsMatch(PluginId, @"^[a-z0-9-]+$"))
            errors.Add("PluginId must contain only lowercase letters, numbers, and dashes");

        if (string.IsNullOrWhiteSpace(Name))
            errors.Add("Name is required");

        if (string.IsNullOrWhiteSpace(Publisher))
            errors.Add("Publisher is required");

        if (Versions.Count == 0)
            errors.Add("At least one version is required");

        return errors.Count == 0;
    }

    /// <summary>
    /// Gets the latest stable version of the plugin.
    /// </summary>
    public PluginVersion? GetLatestStableVersion()
    {
        return Versions
            .Where(v => v.State == VersionState.Stable)
            .OrderByDescending(v => v.SemanticVersion)
            .FirstOrDefault();
    }
}
```

---

## PluginVersion

Represents a specific version of a plugin.

**File:** `src/HotSwap.Distributed.Domain/Models/Plugins/PluginVersion.cs`

```csharp
namespace HotSwap.Distributed.Domain.Models.Plugins;

/// <summary>
/// Represents a specific version of a plugin.
/// </summary>
public class PluginVersion
{
    /// <summary>
    /// Unique version identifier (GUID).
    /// </summary>
    public required string VersionId { get; set; }

    /// <summary>
    /// Parent plugin identifier.
    /// </summary>
    public required string PluginId { get; set; }

    /// <summary>
    /// Semantic version (e.g., "2.1.0").
    /// </summary>
    public required string SemanticVersion { get; set; }

    /// <summary>
    /// Version state (Alpha, Beta, Stable, Deprecated, Archived).
    /// </summary>
    public VersionState State { get; set; } = VersionState.Alpha;

    /// <summary>
    /// Runtime requirements (.NET version, dependencies).
    /// </summary>
    public PluginRuntime Runtime { get; set; } = new();

    /// <summary>
    /// Resource requirements (CPU, memory).
    /// </summary>
    public ResourceRequirements Resources { get; set; } = new();

    /// <summary>
    /// Plugin binary storage location (MinIO/S3 URL).
    /// </summary>
    public required string BinaryUrl { get; set; }

    /// <summary>
    /// Binary checksum (SHA256).
    /// </summary>
    public required string Checksum { get; set; }

    /// <summary>
    /// Digital signature for verification (optional).
    /// </summary>
    public string? Signature { get; set; }

    /// <summary>
    /// Configuration schema (JSON Schema format).
    /// </summary>
    public string? ConfigurationSchema { get; set; }

    /// <summary>
    /// Default configuration values.
    /// </summary>
    public Dictionary<string, string> DefaultConfiguration { get; set; } = new();

    /// <summary>
    /// Plugin dependencies.
    /// </summary>
    public List<PluginDependency> Dependencies { get; set; } = new();

    /// <summary>
    /// Release notes for this version.
    /// </summary>
    public string? ReleaseNotes { get; set; }

    /// <summary>
    /// Version creation timestamp (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Timestamp when version was promoted to stable (UTC).
    /// </summary>
    public DateTime? PromotedAt { get; set; }

    /// <summary>
    /// Timestamp when version was deprecated (UTC).
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

        if (string.IsNullOrWhiteSpace(SemanticVersion))
            errors.Add("SemanticVersion is required");
        else if (!IsValidSemanticVersion(SemanticVersion))
            errors.Add("SemanticVersion must follow format: major.minor.patch");

        if (string.IsNullOrWhiteSpace(BinaryUrl))
            errors.Add("BinaryUrl is required");

        if (string.IsNullOrWhiteSpace(Checksum))
            errors.Add("Checksum is required");

        return errors.Count == 0;
    }

    /// <summary>
    /// Validates semantic version format.
    /// </summary>
    private bool IsValidSemanticVersion(string version)
    {
        var pattern = @"^\d+\.\d+\.\d+$";
        return System.Text.RegularExpressions.Regex.IsMatch(version, pattern);
    }

    /// <summary>
    /// Checks if this version is stable and ready for production.
    /// </summary>
    public bool IsStable() => State == VersionState.Stable;

    /// <summary>
    /// Checks if this version is deprecated.
    /// </summary>
    public bool IsDeprecated() => State == VersionState.Deprecated;
}

/// <summary>
/// Plugin runtime requirements.
/// </summary>
public class PluginRuntime
{
    /// <summary>
    /// Runtime type (DotNet, Python, NodeJS, Container).
    /// </summary>
    public RuntimeType Type { get; set; } = RuntimeType.DotNet8;

    /// <summary>
    /// Runtime version required (e.g., "8.0" for .NET).
    /// </summary>
    public string Version { get; set; } = "8.0";

    /// <summary>
    /// Additional runtime dependencies.
    /// </summary>
    public List<string> Dependencies { get; set; } = new();
}

/// <summary>
/// Plugin resource requirements.
/// </summary>
public class ResourceRequirements
{
    /// <summary>
    /// Minimum CPU cores required.
    /// </summary>
    public decimal MinCpu { get; set; } = 0.1m;

    /// <summary>
    /// Maximum CPU cores allowed.
    /// </summary>
    public decimal MaxCpu { get; set; } = 2.0m;

    /// <summary>
    /// Minimum memory in MB.
    /// </summary>
    public int MinMemoryMB { get; set; } = 128;

    /// <summary>
    /// Maximum memory in MB.
    /// </summary>
    public int MaxMemoryMB { get; set; } = 2048;

    /// <summary>
    /// Storage required in MB.
    /// </summary>
    public int StorageMB { get; set; } = 100;
}
```

---

## PluginDeployment

Represents a plugin deployment to a tenant.

**File:** `src/HotSwap.Distributed.Domain/Models/Plugins/PluginDeployment.cs`

```csharp
namespace HotSwap.Distributed.Domain.Models.Plugins;

/// <summary>
/// Represents a deployment of a plugin to a tenant.
/// </summary>
public class PluginDeployment
{
    /// <summary>
    /// Unique deployment identifier.
    /// </summary>
    public required string DeploymentId { get; set; }

    /// <summary>
    /// Target tenant identifier.
    /// </summary>
    public required string TenantId { get; set; }

    /// <summary>
    /// Plugin being deployed.
    /// </summary>
    public required string PluginId { get; set; }

    /// <summary>
    /// Plugin version being deployed.
    /// </summary>
    public required string PluginVersion { get; set; }

    /// <summary>
    /// Target environment (Development, QA, Staging, Production).
    /// </summary>
    public required string Environment { get; set; }

    /// <summary>
    /// Deployment strategy used.
    /// </summary>
    public DeploymentStrategy Strategy { get; set; } = DeploymentStrategy.Direct;

    /// <summary>
    /// Strategy-specific configuration.
    /// </summary>
    public Dictionary<string, string> StrategyConfig { get; set; } = new();

    /// <summary>
    /// Tenant-specific plugin configuration.
    /// </summary>
    public Dictionary<string, string> Configuration { get; set; } = new();

    /// <summary>
    /// Current deployment status.
    /// </summary>
    public DeploymentStatus Status { get; set; } = DeploymentStatus.Pending;

    /// <summary>
    /// Deployment progress (0-100%).
    /// </summary>
    public int Progress { get; set; } = 0;

    /// <summary>
    /// Total plugin instances for this deployment.
    /// </summary>
    public int TotalInstances { get; set; } = 1;

    /// <summary>
    /// Number of healthy instances.
    /// </summary>
    public int HealthyInstances { get; set; } = 0;

    /// <summary>
    /// Number of unhealthy instances.
    /// </summary>
    public int UnhealthyInstances { get; set; } = 0;

    /// <summary>
    /// Health check configuration.
    /// </summary>
    public PluginHealthCheck? HealthCheck { get; set; }

    /// <summary>
    /// User who initiated the deployment.
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
    /// Deployment error message (if failed).
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Previous deployment ID (for rollback).
    /// </summary>
    public string? PreviousDeploymentId { get; set; }

    /// <summary>
    /// Approval status (if approval required).
    /// </summary>
    public ApprovalStatus ApprovalStatus { get; set; } = ApprovalStatus.NotRequired;

    /// <summary>
    /// User who approved the deployment.
    /// </summary>
    public string? ApprovedBy { get; set; }

    /// <summary>
    /// Approval timestamp (UTC).
    /// </summary>
    public DateTime? ApprovedAt { get; set; }

    /// <summary>
    /// Validates the deployment configuration.
    /// </summary>
    public bool IsValid(out List<string> errors)
    {
        errors = new List<string>();

        if (string.IsNullOrWhiteSpace(DeploymentId))
            errors.Add("DeploymentId is required");

        if (string.IsNullOrWhiteSpace(TenantId))
            errors.Add("TenantId is required");

        if (string.IsNullOrWhiteSpace(PluginId))
            errors.Add("PluginId is required");

        if (string.IsNullOrWhiteSpace(PluginVersion))
            errors.Add("PluginVersion is required");

        if (string.IsNullOrWhiteSpace(Environment))
            errors.Add("Environment is required");

        if (TotalInstances < 1)
            errors.Add("TotalInstances must be at least 1");

        return errors.Count == 0;
    }

    /// <summary>
    /// Checks if deployment is complete.
    /// </summary>
    public bool IsComplete() => Status == DeploymentStatus.Completed;

    /// <summary>
    /// Checks if deployment failed.
    /// </summary>
    public bool IsFailed() => Status == DeploymentStatus.Failed;

    /// <summary>
    /// Checks if all instances are healthy.
    /// </summary>
    public bool IsHealthy() => HealthyInstances == TotalInstances && UnhealthyInstances == 0;

    /// <summary>
    /// Calculates deployment duration.
    /// </summary>
    public TimeSpan? GetDuration()
    {
        if (CompletedAt.HasValue)
            return CompletedAt.Value - StartedAt;
        return DateTime.UtcNow - StartedAt;
    }
}
```

---

## Tenant

Represents a tenant in the multi-tenant system.

**File:** `src/HotSwap.Distributed.Domain/Models/Plugins/Tenant.cs`

```csharp
namespace HotSwap.Distributed.Domain.Models.Plugins;

/// <summary>
/// Represents a tenant in the multi-tenant platform.
/// </summary>
public class Tenant
{
    /// <summary>
    /// Unique tenant identifier (e.g., "tenant-acme-corp").
    /// </summary>
    public required string TenantId { get; set; }

    /// <summary>
    /// Tenant display name.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Tenant description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Kubernetes namespace for this tenant.
    /// </summary>
    public required string Namespace { get; set; }

    /// <summary>
    /// Current tenant status.
    /// </summary>
    public TenantStatus Status { get; set; } = TenantStatus.Active;

    /// <summary>
    /// Resource quotas for this tenant.
    /// </summary>
    public TenantQuotas Quotas { get; set; } = new();

    /// <summary>
    /// Allowed plugin categories for this tenant.
    /// </summary>
    public List<PluginCategory> AllowedCategories { get; set; } = new();

    /// <summary>
    /// Tenant-specific rate limits.
    /// </summary>
    public TenantRateLimits RateLimits { get; set; } = new();

    /// <summary>
    /// Tenant contact email.
    /// </summary>
    public string? ContactEmail { get; set; }

    /// <summary>
    /// Tenant subscription tier.
    /// </summary>
    public SubscriptionTier SubscriptionTier { get; set; } = SubscriptionTier.Free;

    /// <summary>
    /// Tenant creation timestamp (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last update timestamp (UTC).
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Number of plugins deployed to this tenant.
    /// </summary>
    public int PluginCount { get; set; } = 0;

    /// <summary>
    /// Current resource usage.
    /// </summary>
    public TenantResourceUsage ResourceUsage { get; set; } = new();

    /// <summary>
    /// Validates the tenant configuration.
    /// </summary>
    public bool IsValid(out List<string> errors)
    {
        errors = new List<string>();

        if (string.IsNullOrWhiteSpace(TenantId))
            errors.Add("TenantId is required");
        else if (!System.Text.RegularExpressions.Regex.IsMatch(TenantId, @"^[a-z0-9-]+$"))
            errors.Add("TenantId must contain only lowercase letters, numbers, and dashes");

        if (string.IsNullOrWhiteSpace(Name))
            errors.Add("Name is required");

        if (string.IsNullOrWhiteSpace(Namespace))
            errors.Add("Namespace is required");

        return errors.Count == 0;
    }

    /// <summary>
    /// Checks if tenant can deploy more plugins.
    /// </summary>
    public bool CanDeployPlugin() => PluginCount < Quotas.MaxPlugins;

    /// <summary>
    /// Checks if tenant is active.
    /// </summary>
    public bool IsActive() => Status == TenantStatus.Active;
}

/// <summary>
/// Tenant resource quotas.
/// </summary>
public class TenantQuotas
{
    /// <summary>
    /// Maximum number of plugins allowed.
    /// </summary>
    public int MaxPlugins { get; set; } = 10;

    /// <summary>
    /// Maximum CPU cores allowed (total across all plugins).
    /// </summary>
    public decimal MaxCpu { get; set; } = 4.0m;

    /// <summary>
    /// Maximum memory in GB.
    /// </summary>
    public int MaxMemoryGB { get; set; } = 8;

    /// <summary>
    /// Maximum storage in GB.
    /// </summary>
    public int MaxStorageGB { get; set; } = 50;

    /// <summary>
    /// Maximum plugin instances.
    /// </summary>
    public int MaxInstances { get; set; } = 20;
}

/// <summary>
/// Tenant rate limits.
/// </summary>
public class TenantRateLimits
{
    /// <summary>
    /// Max API requests per minute.
    /// </summary>
    public int MaxRequestsPerMinute { get; set; } = 1000;

    /// <summary>
    /// Max deployments per hour.
    /// </summary>
    public int MaxDeploymentsPerHour { get; set; } = 10;

    /// <summary>
    /// Max plugin registrations per day.
    /// </summary>
    public int MaxRegistrationsPerDay { get; set; } = 5;
}

/// <summary>
/// Current tenant resource usage.
/// </summary>
public class TenantResourceUsage
{
    /// <summary>
    /// Current CPU usage in cores.
    /// </summary>
    public decimal CurrentCpu { get; set; } = 0;

    /// <summary>
    /// Current memory usage in GB.
    /// </summary>
    public decimal CurrentMemoryGB { get; set; } = 0;

    /// <summary>
    /// Current storage usage in GB.
    /// </summary>
    public decimal CurrentStorageGB { get; set; } = 0;

    /// <summary>
    /// Current number of plugin instances.
    /// </summary>
    public int CurrentInstances { get; set; } = 0;
}
```

---

## PluginCapability

Represents a capability provided by a plugin.

**File:** `src/HotSwap.Distributed.Domain/Models/Plugins/PluginCapability.cs`

```csharp
namespace HotSwap.Distributed.Domain.Models.Plugins;

/// <summary>
/// Represents a capability (interface) provided by a plugin.
/// </summary>
public class PluginCapability
{
    /// <summary>
    /// Unique capability identifier (e.g., "IPaymentProcessor").
    /// </summary>
    public required string CapabilityId { get; set; }

    /// <summary>
    /// Human-readable capability name.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Capability description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Capability version (e.g., "1.0").
    /// </summary>
    public string Version { get; set; } = "1.0";

    /// <summary>
    /// Methods provided by this capability.
    /// </summary>
    public List<CapabilityMethod> Methods { get; set; } = new();

    /// <summary>
    /// Validates the capability definition.
    /// </summary>
    public bool IsValid(out List<string> errors)
    {
        errors = new List<string>();

        if (string.IsNullOrWhiteSpace(CapabilityId))
            errors.Add("CapabilityId is required");

        if (string.IsNullOrWhiteSpace(Name))
            errors.Add("Name is required");

        if (Methods.Count == 0)
            errors.Add("At least one method is required");

        return errors.Count == 0;
    }
}

/// <summary>
/// Represents a method in a capability interface.
/// </summary>
public class CapabilityMethod
{
    /// <summary>
    /// Method name (e.g., "ProcessPayment").
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Method description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Input parameters.
    /// </summary>
    public List<MethodParameter> Parameters { get; set; } = new();

    /// <summary>
    /// Return type.
    /// </summary>
    public required string ReturnType { get; set; }
}

/// <summary>
/// Represents a method parameter.
/// </summary>
public class MethodParameter
{
    /// <summary>
    /// Parameter name.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Parameter type.
    /// </summary>
    public required string Type { get; set; }

    /// <summary>
    /// Whether parameter is required.
    /// </summary>
    public bool Required { get; set; } = true;
}
```

---

## PluginDependency

Represents a dependency between plugins.

**File:** `src/HotSwap.Distributed.Domain/Models/Plugins/PluginDependency.cs`

```csharp
namespace HotSwap.Distributed.Domain.Models.Plugins;

/// <summary>
/// Represents a dependency of a plugin on another plugin or library.
/// </summary>
public class PluginDependency
{
    /// <summary>
    /// Dependency identifier (plugin ID or library name).
    /// </summary>
    public required string DependencyId { get; set; }

    /// <summary>
    /// Dependency type (Plugin, Library, Runtime).
    /// </summary>
    public DependencyType Type { get; set; } = DependencyType.Plugin;

    /// <summary>
    /// Version constraint (e.g., ">=1.0.0,<2.0.0").
    /// </summary>
    public required string VersionConstraint { get; set; }

    /// <summary>
    /// Whether this dependency is required.
    /// </summary>
    public bool Required { get; set; } = true;

    /// <summary>
    /// Validates the dependency specification.
    /// </summary>
    public bool IsValid(out List<string> errors)
    {
        errors = new List<string>();

        if (string.IsNullOrWhiteSpace(DependencyId))
            errors.Add("DependencyId is required");

        if (string.IsNullOrWhiteSpace(VersionConstraint))
            errors.Add("VersionConstraint is required");

        return errors.Count == 0;
    }

    /// <summary>
    /// Checks if a version satisfies this constraint.
    /// </summary>
    public bool SatisfiesConstraint(string version)
    {
        // Simplified version checking (production would use NuGet.Versioning library)
        return true; // TODO: Implement proper version constraint checking
    }
}
```

---

## PluginHealthCheck

Represents health check configuration for a plugin.

**File:** `src/HotSwap.Distributed.Domain/Models/Plugins/PluginHealthCheck.cs`

```csharp
namespace HotSwap.Distributed.Domain.Models.Plugins;

/// <summary>
/// Represents health check configuration for a plugin.
/// </summary>
public class PluginHealthCheck
{
    /// <summary>
    /// Health check type.
    /// </summary>
    public HealthCheckType Type { get; set; } = HealthCheckType.HttpEndpoint;

    /// <summary>
    /// HTTP endpoint to check (for HttpEndpoint type).
    /// </summary>
    public string? HttpEndpoint { get; set; }

    /// <summary>
    /// Expected HTTP status code (default: 200).
    /// </summary>
    public int ExpectedStatusCode { get; set; } = 200;

    /// <summary>
    /// Health check interval in seconds.
    /// </summary>
    public int IntervalSeconds { get; set; } = 30;

    /// <summary>
    /// Health check timeout in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 5;

    /// <summary>
    /// Number of consecutive failures before marking unhealthy.
    /// </summary>
    public int FailureThreshold { get; set; } = 3;

    /// <summary>
    /// Number of consecutive successes before marking healthy.
    /// </summary>
    public int SuccessThreshold { get; set; } = 1;

    /// <summary>
    /// Maximum error rate (0.0-1.0) before marking unhealthy.
    /// </summary>
    public decimal MaxErrorRate { get; set; } = 0.05m; // 5%

    /// <summary>
    /// Maximum response time in milliseconds.
    /// </summary>
    public int MaxResponseTimeMs { get; set; } = 1000;

    /// <summary>
    /// Validates the health check configuration.
    /// </summary>
    public bool IsValid(out List<string> errors)
    {
        errors = new List<string>();

        if (Type == HealthCheckType.HttpEndpoint && string.IsNullOrWhiteSpace(HttpEndpoint))
            errors.Add("HttpEndpoint is required for HttpEndpoint type");

        if (IntervalSeconds < 5 || IntervalSeconds > 300)
            errors.Add("IntervalSeconds must be between 5 and 300");

        if (TimeoutSeconds < 1 || TimeoutSeconds > 60)
            errors.Add("TimeoutSeconds must be between 1 and 60");

        if (FailureThreshold < 1 || FailureThreshold > 10)
            errors.Add("FailureThreshold must be between 1 and 10");

        if (MaxErrorRate < 0 || MaxErrorRate > 1)
            errors.Add("MaxErrorRate must be between 0 and 1");

        return errors.Count == 0;
    }
}
```

---

## Enumerations

### PluginCategory

**File:** `src/HotSwap.Distributed.Domain/Enums/PluginCategory.cs`

```csharp
namespace HotSwap.Distributed.Domain.Enums;

/// <summary>
/// Plugin categories for organization and discovery.
/// </summary>
public enum PluginCategory
{
    /// <summary>
    /// Payment processing (Stripe, PayPal, etc.).
    /// </summary>
    PaymentGateway,

    /// <summary>
    /// Authentication and SSO (SAML, OAuth, LDAP).
    /// </summary>
    Authentication,

    /// <summary>
    /// Reporting and analytics.
    /// </summary>
    Reporting,

    /// <summary>
    /// Communication (Email, SMS, Push notifications).
    /// </summary>
    Communication,

    /// <summary>
    /// External system integrations.
    /// </summary>
    Integration,

    /// <summary>
    /// UI customization (Themes, Widgets).
    /// </summary>
    UICustomization,

    /// <summary>
    /// Workflow automation.
    /// </summary>
    Workflow,

    /// <summary>
    /// Data processing and ETL.
    /// </summary>
    DataProcessing,

    /// <summary>
    /// Security and compliance.
    /// </summary>
    Security,

    /// <summary>
    /// Other/uncategorized plugins.
    /// </summary>
    Other
}
```

### PluginStatus

```csharp
namespace HotSwap.Distributed.Domain.Enums;

/// <summary>
/// Current status of a plugin.
/// </summary>
public enum PluginStatus
{
    /// <summary>
    /// Plugin registered but not yet validated.
    /// </summary>
    Registered,

    /// <summary>
    /// Plugin validated and ready for deployment.
    /// </summary>
    Validated,

    /// <summary>
    /// Plugin approved for production use (if approval required).
    /// </summary>
    Approved,

    /// <summary>
    /// Plugin is active and deployed to one or more tenants.
    /// </summary>
    Active,

    /// <summary>
    /// Plugin marked for removal, no new deployments allowed.
    /// </summary>
    Deprecated,

    /// <summary>
    /// Plugin removed from registry.
    /// </summary>
    Archived
}
```

### VersionState

```csharp
namespace HotSwap.Distributed.Domain.Enums;

/// <summary>
/// State of a plugin version.
/// </summary>
public enum VersionState
{
    /// <summary>
    /// Early testing, unstable.
    /// </summary>
    Alpha,

    /// <summary>
    /// Feature complete, testing.
    /// </summary>
    Beta,

    /// <summary>
    /// Production-ready, recommended.
    /// </summary>
    Stable,

    /// <summary>
    /// Scheduled for removal.
    /// </summary>
    Deprecated,

    /// <summary>
    /// No longer available for new deployments.
    /// </summary>
    Archived
}
```

### DeploymentStrategy

```csharp
namespace HotSwap.Distributed.Domain.Enums;

/// <summary>
/// Plugin deployment strategies.
/// </summary>
public enum DeploymentStrategy
{
    /// <summary>
    /// Deploy to all instances simultaneously.
    /// </summary>
    Direct,

    /// <summary>
    /// Progressive rollout with health checks.
    /// </summary>
    Canary,

    /// <summary>
    /// Deploy to inactive environment, then switch.
    /// </summary>
    BlueGreen,

    /// <summary>
    /// Deploy to instances one-by-one or in batches.
    /// </summary>
    Rolling,

    /// <summary>
    /// Deploy to subset for A/B testing.
    /// </summary>
    ABTest
}
```

### DeploymentStatus

```csharp
namespace HotSwap.Distributed.Domain.Enums;

/// <summary>
/// Current status of a plugin deployment.
/// </summary>
public enum DeploymentStatus
{
    /// <summary>
    /// Deployment queued, waiting to start.
    /// </summary>
    Pending,

    /// <summary>
    /// Deployment in progress.
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
    /// Deployment rolled back.
    /// </summary>
    RolledBack,

    /// <summary>
    /// Deployment paused (manual intervention).
    /// </summary>
    Paused
}
```

### TenantStatus

```csharp
namespace HotSwap.Distributed.Domain.Enums;

/// <summary>
/// Current status of a tenant.
/// </summary>
public enum TenantStatus
{
    /// <summary>
    /// Tenant is active and operational.
    /// </summary>
    Active,

    /// <summary>
    /// Tenant temporarily suspended.
    /// </summary>
    Suspended,

    /// <summary>
    /// Tenant marked for deletion.
    /// </summary>
    Deleted
}
```

### RuntimeType

```csharp
namespace HotSwap.Distributed.Domain.Enums;

/// <summary>
/// Plugin runtime types.
/// </summary>
public enum RuntimeType
{
    /// <summary>
    /// .NET 8.0 runtime.
    /// </summary>
    DotNet8,

    /// <summary>
    /// Python 3.11+ runtime.
    /// </summary>
    Python3,

    /// <summary>
    /// Node.js 20+ runtime.
    /// </summary>
    NodeJS,

    /// <summary>
    /// Container-based plugin (Docker).
    /// </summary>
    Container
}
```

### DependencyType

```csharp
namespace HotSwap.Distributed.Domain.Enums;

/// <summary>
/// Type of plugin dependency.
/// </summary>
public enum DependencyType
{
    /// <summary>
    /// Dependency on another plugin.
    /// </summary>
    Plugin,

    /// <summary>
    /// Dependency on a library (NuGet, npm, pip).
    /// </summary>
    Library,

    /// <summary>
    /// Dependency on a specific runtime.
    /// </summary>
    Runtime
}
```

### HealthCheckType

```csharp
namespace HotSwap.Distributed.Domain.Enums;

/// <summary>
/// Type of health check.
/// </summary>
public enum HealthCheckType
{
    /// <summary>
    /// HTTP endpoint check (GET /health).
    /// </summary>
    HttpEndpoint,

    /// <summary>
    /// Custom health check logic provided by plugin.
    /// </summary>
    Custom,

    /// <summary>
    /// Resource usage check (CPU, memory).
    /// </summary>
    ResourceUsage,

    /// <summary>
    /// Error rate monitoring.
    /// </summary>
    ErrorRate
}
```

### ApprovalStatus

```csharp
namespace HotSwap.Distributed.Domain.Enums;

/// <summary>
/// Approval status for plugin deployments.
/// </summary>
public enum ApprovalStatus
{
    /// <summary>
    /// No approval required for this deployment.
    /// </summary>
    NotRequired,

    /// <summary>
    /// Awaiting approval.
    /// </summary>
    Pending,

    /// <summary>
    /// Deployment approved.
    /// </summary>
    Approved,

    /// <summary>
    /// Deployment rejected.
    /// </summary>
    Rejected
}
```

### SubscriptionTier

```csharp
namespace HotSwap.Distributed.Domain.Enums;

/// <summary>
/// Tenant subscription tiers.
/// </summary>
public enum SubscriptionTier
{
    /// <summary>
    /// Free tier with limited resources.
    /// </summary>
    Free,

    /// <summary>
    /// Basic tier with standard resources.
    /// </summary>
    Basic,

    /// <summary>
    /// Professional tier with increased resources.
    /// </summary>
    Professional,

    /// <summary>
    /// Enterprise tier with maximum resources.
    /// </summary>
    Enterprise
}
```

---

## Value Objects

### DeploymentResult

**File:** `src/HotSwap.Distributed.Domain/ValueObjects/DeploymentResult.cs`

```csharp
namespace HotSwap.Distributed.Domain.ValueObjects;

/// <summary>
/// Result of a plugin deployment operation.
/// </summary>
public class DeploymentResult
{
    /// <summary>
    /// Whether deployment was successful.
    /// </summary>
    public bool Success { get; private set; }

    /// <summary>
    /// Deployment ID (if successful).
    /// </summary>
    public string? DeploymentId { get; private set; }

    /// <summary>
    /// Error message (if failed).
    /// </summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>
    /// List of instances deployed.
    /// </summary>
    public List<string> DeployedInstances { get; private set; } = new();

    /// <summary>
    /// Deployment timestamp (UTC).
    /// </summary>
    public DateTime Timestamp { get; private set; } = DateTime.UtcNow;

    public static DeploymentResult SuccessResult(string deploymentId, List<string> instances)
    {
        return new DeploymentResult
        {
            Success = true,
            DeploymentId = deploymentId,
            DeployedInstances = instances
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
    PluginId = "payment-stripe",
    Name = "Stripe Payment Processor",
    Publisher = "Acme Corp",
    Category = PluginCategory.PaymentGateway
};

if (!plugin.IsValid(out var errors))
{
    foreach (var error in errors)
    {
        Console.WriteLine($"Validation Error: {error}");
    }
}
```

### Tenant Validation

```csharp
var tenant = new Tenant
{
    TenantId = "tenant-acme-corp",
    Name = "Acme Corporation",
    Namespace = "acme-prod",
    Quotas = new TenantQuotas
    {
        MaxPlugins = 20,
        MaxCpu = 8.0m,
        MaxMemoryGB = 16
    }
};

if (!tenant.IsValid(out var errors))
{
    Console.WriteLine("Tenant validation failed:");
    errors.ForEach(Console.WriteLine);
}
```

---

**Last Updated:** 2025-11-23
**Namespace:** `HotSwap.Distributed.Domain.Models.Plugins`
