# Domain Models Reference

**Version:** 1.0.0
**Namespace:** `HotSwap.ConfigManager.Domain.Models`

---

## Table of Contents

1. [ConfigProfile](#configprofile)
2. [ConfigVersion](#configversion)
3. [ConfigDeployment](#configdeployment)
4. [ServiceInstance](#serviceinstance)
5. [ConfigSchema](#configschema)
6. [DeploymentHealth](#deploymenthealth)
7. [Enumerations](#enumerations)
8. [Value Objects](#value-objects)

---

## ConfigProfile

Represents a configuration profile for a service.

**File:** `src/HotSwap.ConfigManager.Domain/Models/ConfigProfile.cs`

```csharp
namespace HotSwap.ConfigManager.Domain.Models;

/// <summary>
/// Represents a configuration profile for a microservice.
/// </summary>
public class ConfigProfile
{
    /// <summary>
    /// Unique configuration profile name (e.g., "payment-service.production").
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Human-readable description (optional).
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Target environment (Development, Staging, Production).
    /// </summary>
    public ConfigEnvironment Environment { get; set; } = ConfigEnvironment.Development;

    /// <summary>
    /// Service type classification.
    /// </summary>
    public ServiceType ServiceType { get; set; } = ServiceType.Microservice;

    /// <summary>
    /// Current active configuration version.
    /// </summary>
    public string? CurrentVersion { get; set; }

    /// <summary>
    /// Schema ID for config validation.
    /// </summary>
    public required string SchemaId { get; set; }

    /// <summary>
    /// Default deployment strategy for this profile.
    /// </summary>
    public DeploymentStrategy DefaultStrategy { get; set; } = DeploymentStrategy.Canary;

    /// <summary>
    /// Profile-level configuration settings.
    /// </summary>
    public Dictionary<string, string> Settings { get; set; } = new();

    /// <summary>
    /// Profile creation timestamp (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last update timestamp (UTC).
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// User who created the profile.
    /// </summary>
    public string? CreatedBy { get; set; }

    /// <summary>
    /// Total number of versions.
    /// </summary>
    public int VersionCount { get; set; } = 0;

    /// <summary>
    /// Total number of deployments.
    /// </summary>
    public int DeploymentCount { get; set; } = 0;

    /// <summary>
    /// Validates the configuration profile.
    /// </summary>
    public bool IsValid(out List<string> errors)
    {
        errors = new List<string>();

        if (string.IsNullOrWhiteSpace(Name))
            errors.Add("Name is required");
        else if (!System.Text.RegularExpressions.Regex.IsMatch(Name, @"^[a-zA-Z0-9._-]+$"))
            errors.Add("Name must contain only alphanumeric characters, dots, dashes, and underscores");

        if (string.IsNullOrWhiteSpace(SchemaId))
            errors.Add("SchemaId is required");

        return errors.Count == 0;
    }

    /// <summary>
    /// Checks if profile is for production environment.
    /// </summary>
    public bool IsProduction() => Environment == ConfigEnvironment.Production;
}
```

---

## ConfigVersion

Represents a version of configuration data.

**File:** `src/HotSwap.ConfigManager.Domain/Models/ConfigVersion.cs`

```csharp
namespace HotSwap.ConfigManager.Domain.Models;

/// <summary>
/// Represents a specific version of configuration data.
/// </summary>
public class ConfigVersion
{
    /// <summary>
    /// Configuration profile name.
    /// </summary>
    public required string ConfigName { get; set; }

    /// <summary>
    /// Semantic version (e.g., "1.0.0", "1.2.3").
    /// </summary>
    public required string Version { get; set; }

    /// <summary>
    /// Configuration data (JSON format).
    /// </summary>
    public required string ConfigData { get; set; }

    /// <summary>
    /// Schema version used for validation.
    /// </summary>
    public required string SchemaVersion { get; set; }

    /// <summary>
    /// Version description/changelog.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Version tags (e.g., "stable", "beta", "deprecated").
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// Configuration metadata.
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();

    /// <summary>
    /// Version creation timestamp (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// User who created the version.
    /// </summary>
    public string? CreatedBy { get; set; }

    /// <summary>
    /// MD5 hash of ConfigData for integrity checking.
    /// </summary>
    public string? ConfigHash { get; set; }

    /// <summary>
    /// Size of ConfigData in bytes.
    /// </summary>
    public long SizeBytes { get; set; }

    /// <summary>
    /// Number of times this version has been deployed.
    /// </summary>
    public int DeploymentCount { get; set; } = 0;

    /// <summary>
    /// Whether this version is deprecated.
    /// </summary>
    public bool IsDeprecated { get; set; } = false;

    /// <summary>
    /// Deprecation reason (if deprecated).
    /// </summary>
    public string? DeprecationReason { get; set; }

    /// <summary>
    /// Validates the configuration version.
    /// </summary>
    public bool IsValid(out List<string> errors)
    {
        errors = new List<string>();

        if (string.IsNullOrWhiteSpace(ConfigName))
            errors.Add("ConfigName is required");

        if (string.IsNullOrWhiteSpace(Version))
            errors.Add("Version is required");
        else if (!IsValidSemanticVersion(Version))
            errors.Add("Version must be valid semantic version (e.g., 1.0.0)");

        if (string.IsNullOrWhiteSpace(ConfigData))
            errors.Add("ConfigData is required");

        if (string.IsNullOrWhiteSpace(SchemaVersion))
            errors.Add("SchemaVersion is required");

        if (ConfigData?.Length > 1048576) // 1 MB
            errors.Add("ConfigData exceeds maximum size of 1 MB");

        return errors.Count == 0;
    }

    /// <summary>
    /// Validates semantic version format.
    /// </summary>
    private bool IsValidSemanticVersion(string version)
    {
        var regex = new System.Text.RegularExpressions.Regex(@"^\d+\.\d+\.\d+$");
        return regex.IsMatch(version);
    }

    /// <summary>
    /// Calculates MD5 hash of ConfigData.
    /// </summary>
    public void CalculateHash()
    {
        using var md5 = System.Security.Cryptography.MD5.Create();
        var hash = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(ConfigData));
        ConfigHash = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }
}
```

---

## ConfigDeployment

Represents a configuration deployment operation.

**File:** `src/HotSwap.ConfigManager.Domain/Models/ConfigDeployment.cs`

```csharp
namespace HotSwap.ConfigManager.Domain.Models;

/// <summary>
/// Represents a configuration deployment to service instances.
/// </summary>
public class ConfigDeployment
{
    /// <summary>
    /// Unique deployment identifier (GUID).
    /// </summary>
    public required string DeploymentId { get; set; }

    /// <summary>
    /// Configuration profile name.
    /// </summary>
    public required string ConfigName { get; set; }

    /// <summary>
    /// Configuration version being deployed.
    /// </summary>
    public required string ConfigVersion { get; set; }

    /// <summary>
    /// Deployment strategy.
    /// </summary>
    public DeploymentStrategy Strategy { get; set; } = DeploymentStrategy.Canary;

    /// <summary>
    /// Target instance IDs.
    /// </summary>
    public List<string> TargetInstances { get; set; } = new();

    /// <summary>
    /// Current deployment status.
    /// </summary>
    public DeploymentStatus Status { get; set; } = DeploymentStatus.Pending;

    /// <summary>
    /// Deployment progress (0-100).
    /// </summary>
    public int ProgressPercentage { get; set; } = 0;

    /// <summary>
    /// Per-instance deployment status.
    /// </summary>
    public Dictionary<string, InstanceDeploymentStatus> InstanceStatus { get; set; } = new();

    /// <summary>
    /// Deployment strategy configuration.
    /// </summary>
    public DeploymentConfig Config { get; set; } = new();

    /// <summary>
    /// Health monitoring configuration.
    /// </summary>
    public HealthCheckConfig HealthCheck { get; set; } = new();

    /// <summary>
    /// Deployment start timestamp (UTC).
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// Deployment completion timestamp (UTC).
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// User who initiated the deployment.
    /// </summary>
    public string? InitiatedBy { get; set; }

    /// <summary>
    /// Deployment error message (if failed).
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Previous config version (for rollback).
    /// </summary>
    public string? PreviousVersion { get; set; }

    /// <summary>
    /// Whether deployment was rolled back.
    /// </summary>
    public bool WasRolledBack { get; set; } = false;

    /// <summary>
    /// Rollback timestamp (UTC).
    /// </summary>
    public DateTime? RolledBackAt { get; set; }

    /// <summary>
    /// Rollback reason.
    /// </summary>
    public string? RollbackReason { get; set; }

    /// <summary>
    /// Validates the deployment configuration.
    /// </summary>
    public bool IsValid(out List<string> errors)
    {
        errors = new List<string>();

        if (string.IsNullOrWhiteSpace(DeploymentId))
            errors.Add("DeploymentId is required");

        if (string.IsNullOrWhiteSpace(ConfigName))
            errors.Add("ConfigName is required");

        if (string.IsNullOrWhiteSpace(ConfigVersion))
            errors.Add("ConfigVersion is required");

        if (TargetInstances.Count == 0)
            errors.Add("TargetInstances must contain at least one instance");

        return errors.Count == 0;
    }

    /// <summary>
    /// Checks if deployment is in terminal state.
    /// </summary>
    public bool IsTerminal() => Status == DeploymentStatus.Completed ||
                                 Status == DeploymentStatus.Failed ||
                                 Status == DeploymentStatus.RolledBack;

    /// <summary>
    /// Calculates deployment duration.
    /// </summary>
    public TimeSpan? GetDuration()
    {
        if (!StartedAt.HasValue) return null;
        var endTime = CompletedAt ?? DateTime.UtcNow;
        return endTime - StartedAt.Value;
    }
}

/// <summary>
/// Per-instance deployment status.
/// </summary>
public class InstanceDeploymentStatus
{
    public string InstanceId { get; set; } = string.Empty;
    public DeploymentStatus Status { get; set; } = DeploymentStatus.Pending;
    public DateTime? DeployedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; } = 0;
}

/// <summary>
/// Deployment strategy configuration.
/// </summary>
public class DeploymentConfig
{
    /// <summary>
    /// Canary percentage (10-100).
    /// </summary>
    public int CanaryPercentage { get; set; } = 10;

    /// <summary>
    /// Phase interval for canary deployment.
    /// </summary>
    public TimeSpan PhaseInterval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Batch size for rolling deployment.
    /// </summary>
    public int BatchSize { get; set; } = 1;

    /// <summary>
    /// Batch interval for rolling deployment.
    /// </summary>
    public TimeSpan BatchInterval { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Verification period for blue-green.
    /// </summary>
    public TimeSpan VerificationPeriod { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Auto-promote to next phase (canary).
    /// </summary>
    public bool AutoPromote { get; set; } = true;

    /// <summary>
    /// Stop deployment on first failure.
    /// </summary>
    public bool StopOnFailure { get; set; } = true;
}

/// <summary>
/// Health check configuration.
/// </summary>
public class HealthCheckConfig
{
    /// <summary>
    /// Enable health monitoring.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Health check interval.
    /// </summary>
    public TimeSpan CheckInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Error rate threshold (percentage).
    /// </summary>
    public double ErrorRateThreshold { get; set; } = 5.0;

    /// <summary>
    /// Latency threshold increase (percentage).
    /// </summary>
    public double LatencyThreshold { get; set; } = 50.0;

    /// <summary>
    /// Custom health check endpoint.
    /// </summary>
    public string? HealthEndpoint { get; set; }

    /// <summary>
    /// Auto-rollback on health failure.
    /// </summary>
    public bool AutoRollback { get; set; } = true;
}
```

---

## ServiceInstance

Represents a registered service instance.

**File:** `src/HotSwap.ConfigManager.Domain/Models/ServiceInstance.cs`

```csharp
namespace HotSwap.ConfigManager.Domain.Models;

/// <summary>
/// Represents a registered microservice instance.
/// </summary>
public class ServiceInstance
{
    /// <summary>
    /// Unique instance identifier (GUID).
    /// </summary>
    public required string InstanceId { get; set; }

    /// <summary>
    /// Service name (matches ConfigProfile name prefix).
    /// </summary>
    public required string ServiceName { get; set; }

    /// <summary>
    /// Instance hostname or IP address.
    /// </summary>
    public required string Hostname { get; set; }

    /// <summary>
    /// Service port.
    /// </summary>
    public int Port { get; set; } = 8080;

    /// <summary>
    /// Environment (Development, Staging, Production).
    /// </summary>
    public ConfigEnvironment Environment { get; set; } = ConfigEnvironment.Development;

    /// <summary>
    /// Current configuration version.
    /// </summary>
    public string? CurrentConfigVersion { get; set; }

    /// <summary>
    /// Instance metadata.
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();

    /// <summary>
    /// Instance health status.
    /// </summary>
    public InstanceHealth Health { get; set; } = new();

    /// <summary>
    /// Last heartbeat timestamp (UTC).
    /// </summary>
    public DateTime LastHeartbeat { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Instance registration timestamp (UTC).
    /// </summary>
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Instance version/build number.
    /// </summary>
    public string? Version { get; set; }

    /// <summary>
    /// Checks if instance is healthy based on heartbeat.
    /// </summary>
    public bool IsHealthy()
    {
        // Unhealthy if no heartbeat in last 2 minutes
        if (DateTime.UtcNow - LastHeartbeat > TimeSpan.FromMinutes(2))
            return false;

        return Health.Status == HealthStatus.Healthy;
    }

    /// <summary>
    /// Updates the heartbeat timestamp.
    /// </summary>
    public void RecordHeartbeat()
    {
        LastHeartbeat = DateTime.UtcNow;
    }

    /// <summary>
    /// Gets the instance endpoint URL.
    /// </summary>
    public string GetEndpoint() => $"http://{Hostname}:{Port}";
}

/// <summary>
/// Instance health information.
/// </summary>
public class InstanceHealth
{
    /// <summary>
    /// Health status.
    /// </summary>
    public HealthStatus Status { get; set; } = HealthStatus.Healthy;

    /// <summary>
    /// Current error rate (percentage).
    /// </summary>
    public double ErrorRate { get; set; } = 0.0;

    /// <summary>
    /// Current p99 latency (milliseconds).
    /// </summary>
    public double P99Latency { get; set; } = 0.0;

    /// <summary>
    /// CPU usage percentage (0-100).
    /// </summary>
    public double CpuUsage { get; set; } = 0.0;

    /// <summary>
    /// Memory usage percentage (0-100).
    /// </summary>
    public double MemoryUsage { get; set; } = 0.0;

    /// <summary>
    /// Custom health metrics.
    /// </summary>
    public Dictionary<string, double> CustomMetrics { get; set; } = new();

    /// <summary>
    /// Last health check timestamp (UTC).
    /// </summary>
    public DateTime LastChecked { get; set; } = DateTime.UtcNow;
}
```

---

## ConfigSchema

Represents a configuration schema for validation.

**File:** `src/HotSwap.ConfigManager.Domain/Models/ConfigSchema.cs`

```csharp
namespace HotSwap.ConfigManager.Domain.Models;

/// <summary>
/// Represents a JSON schema for configuration validation.
/// </summary>
public class ConfigSchema
{
    /// <summary>
    /// Unique schema identifier (e.g., "payment-config.v1").
    /// </summary>
    public required string SchemaId { get; set; }

    /// <summary>
    /// JSON Schema definition.
    /// </summary>
    public required string SchemaDefinition { get; set; }

    /// <summary>
    /// Schema version number (e.g., "1.0", "2.0").
    /// </summary>
    public string Version { get; set; } = "1.0";

    /// <summary>
    /// Schema compatibility mode.
    /// </summary>
    public SchemaCompatibility Compatibility { get; set; } = SchemaCompatibility.None;

    /// <summary>
    /// Current schema status.
    /// </summary>
    public SchemaStatus Status { get; set; } = SchemaStatus.Draft;

    /// <summary>
    /// Admin user who approved the schema.
    /// </summary>
    public string? ApprovedBy { get; set; }

    /// <summary>
    /// Approval timestamp (UTC).
    /// </summary>
    public DateTime? ApprovedAt { get; set; }

    /// <summary>
    /// Schema creation timestamp (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Schema deprecation timestamp (UTC).
    /// </summary>
    public DateTime? DeprecatedAt { get; set; }

    /// <summary>
    /// Validates the schema configuration.
    /// </summary>
    public bool IsValid(out List<string> errors)
    {
        errors = new List<string>();

        if (string.IsNullOrWhiteSpace(SchemaId))
            errors.Add("SchemaId is required");

        if (string.IsNullOrWhiteSpace(SchemaDefinition))
            errors.Add("SchemaDefinition is required");

        if (string.IsNullOrWhiteSpace(Version))
            errors.Add("Version is required");

        // Validate JSON Schema format
        try
        {
            var json = System.Text.Json.JsonDocument.Parse(SchemaDefinition);
        }
        catch
        {
            errors.Add("SchemaDefinition must be valid JSON");
        }

        return errors.Count == 0;
    }

    /// <summary>
    /// Checks if the schema is approved for production use.
    /// </summary>
    public bool IsApproved() => Status == SchemaStatus.Approved;
}
```

---

## DeploymentHealth

Represents health metrics for a deployment.

**File:** `src/HotSwap.ConfigManager.Domain/Models/DeploymentHealth.cs`

```csharp
namespace HotSwap.ConfigManager.Domain.Models;

/// <summary>
/// Tracks health metrics during a deployment.
/// </summary>
public class DeploymentHealth
{
    /// <summary>
    /// Deployment ID.
    /// </summary>
    public required string DeploymentId { get; set; }

    /// <summary>
    /// Overall health status.
    /// </summary>
    public HealthStatus OverallStatus { get; set; } = HealthStatus.Healthy;

    /// <summary>
    /// Baseline error rate before deployment.
    /// </summary>
    public double BaselineErrorRate { get; set; }

    /// <summary>
    /// Current error rate during deployment.
    /// </summary>
    public double CurrentErrorRate { get; set; }

    /// <summary>
    /// Baseline p99 latency before deployment.
    /// </summary>
    public double BaselineP99Latency { get; set; }

    /// <summary>
    /// Current p99 latency during deployment.
    /// </summary>
    public double CurrentP99Latency { get; set; }

    /// <summary>
    /// Per-instance health metrics.
    /// </summary>
    public Dictionary<string, InstanceHealth> InstanceMetrics { get; set; } = new();

    /// <summary>
    /// Health check results history.
    /// </summary>
    public List<HealthCheckResult> CheckHistory { get; set; } = new();

    /// <summary>
    /// Last health check timestamp (UTC).
    /// </summary>
    public DateTime LastChecked { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Checks if health has degraded beyond threshold.
    /// </summary>
    public bool HasDegraded(HealthCheckConfig config)
    {
        // Error rate check
        var errorRateIncrease = ((CurrentErrorRate - BaselineErrorRate) / BaselineErrorRate) * 100;
        if (errorRateIncrease > config.ErrorRateThreshold)
            return true;

        // Latency check
        var latencyIncrease = ((CurrentP99Latency - BaselineP99Latency) / BaselineP99Latency) * 100;
        if (latencyIncrease > config.LatencyThreshold)
            return true;

        return false;
    }

    /// <summary>
    /// Records a health check result.
    /// </summary>
    public void RecordCheck(HealthCheckResult result)
    {
        CheckHistory.Add(result);
        LastChecked = result.Timestamp;

        // Keep only last 100 checks
        if (CheckHistory.Count > 100)
        {
            CheckHistory.RemoveRange(0, CheckHistory.Count - 100);
        }
    }
}

/// <summary>
/// Individual health check result.
/// </summary>
public class HealthCheckResult
{
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public HealthStatus Status { get; set; }
    public double ErrorRate { get; set; }
    public double P99Latency { get; set; }
    public string? Message { get; set; }
}
```

---

## Enumerations

### ConfigEnvironment

**File:** `src/HotSwap.ConfigManager.Domain/Enums/ConfigEnvironment.cs`

```csharp
namespace HotSwap.ConfigManager.Domain.Enums;

/// <summary>
/// Target environment for configuration.
/// </summary>
public enum ConfigEnvironment
{
    /// <summary>
    /// Development environment.
    /// </summary>
    Development,

    /// <summary>
    /// Staging/QA environment.
    /// </summary>
    Staging,

    /// <summary>
    /// Production environment.
    /// </summary>
    Production
}
```

### ServiceType

```csharp
namespace HotSwap.ConfigManager.Domain.Enums;

/// <summary>
/// Type of service being configured.
/// </summary>
public enum ServiceType
{
    /// <summary>
    /// Standard microservice.
    /// </summary>
    Microservice,

    /// <summary>
    /// API gateway service.
    /// </summary>
    Gateway,

    /// <summary>
    /// Background worker/job.
    /// </summary>
    Worker,

    /// <summary>
    /// Database service.
    /// </summary>
    Database,

    /// <summary>
    /// Message queue/broker.
    /// </summary>
    MessageBroker
}
```

### DeploymentStrategy

```csharp
namespace HotSwap.ConfigManager.Domain.Enums;

/// <summary>
/// Deployment strategy for configuration rollout.
/// </summary>
public enum DeploymentStrategy
{
    /// <summary>
    /// Gradual percentage-based rollout (10% → 30% → 50% → 100%).
    /// </summary>
    Canary,

    /// <summary>
    /// Deploy to green set, then switch traffic.
    /// </summary>
    BlueGreen,

    /// <summary>
    /// Instance-by-instance gradual rollout.
    /// </summary>
    Rolling,

    /// <summary>
    /// Deploy to all instances simultaneously.
    /// </summary>
    Direct
}
```

### DeploymentStatus

```csharp
namespace HotSwap.ConfigManager.Domain.Enums;

/// <summary>
/// Status of a configuration deployment.
/// </summary>
public enum DeploymentStatus
{
    /// <summary>
    /// Deployment queued, not started.
    /// </summary>
    Pending,

    /// <summary>
    /// Deployment in progress.
    /// </summary>
    InProgress,

    /// <summary>
    /// Deployment paused (manual intervention).
    /// </summary>
    Paused,

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
    RolledBack
}
```

### HealthStatus

```csharp
namespace HotSwap.ConfigManager.Domain.Enums;

/// <summary>
/// Health status of instance or deployment.
/// </summary>
public enum HealthStatus
{
    /// <summary>
    /// Instance is healthy.
    /// </summary>
    Healthy,

    /// <summary>
    /// Instance health is degraded but functional.
    /// </summary>
    Degraded,

    /// <summary>
    /// Instance is unhealthy.
    /// </summary>
    Unhealthy,

    /// <summary>
    /// Health status unknown.
    /// </summary>
    Unknown
}
```

### SchemaCompatibility

```csharp
namespace HotSwap.ConfigManager.Domain.Enums;

/// <summary>
/// Schema compatibility mode.
/// </summary>
public enum SchemaCompatibility
{
    /// <summary>
    /// No compatibility checks.
    /// </summary>
    None,

    /// <summary>
    /// New schema can validate old configs.
    /// </summary>
    Backward,

    /// <summary>
    /// Old schema can validate new configs.
    /// </summary>
    Forward,

    /// <summary>
    /// Bidirectional compatibility.
    /// </summary>
    Full
}
```

### SchemaStatus

```csharp
namespace HotSwap.ConfigManager.Domain.Enums;

/// <summary>
/// Status of a configuration schema.
/// </summary>
public enum SchemaStatus
{
    /// <summary>
    /// Schema in draft state.
    /// </summary>
    Draft,

    /// <summary>
    /// Schema pending approval.
    /// </summary>
    PendingApproval,

    /// <summary>
    /// Schema approved for use.
    /// </summary>
    Approved,

    /// <summary>
    /// Schema deprecated.
    /// </summary>
    Deprecated
}
```

---

## Value Objects

### DeploymentResult

**File:** `src/HotSwap.ConfigManager.Domain/ValueObjects/DeploymentResult.cs`

```csharp
namespace HotSwap.ConfigManager.Domain.ValueObjects;

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
    /// Deployment ID.
    /// </summary>
    public string DeploymentId { get; private set; } = string.Empty;

    /// <summary>
    /// Number of instances successfully deployed.
    /// </summary>
    public int InstancesDeployed { get; private set; }

    /// <summary>
    /// Number of instances that failed.
    /// </summary>
    public int InstancesFailed { get; private set; }

    /// <summary>
    /// Error message (if failed).
    /// </summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>
    /// Deployment timestamp (UTC).
    /// </summary>
    public DateTime Timestamp { get; private set; } = DateTime.UtcNow;

    public static DeploymentResult SuccessResult(string deploymentId, int instancesDeployed)
    {
        return new DeploymentResult
        {
            Success = true,
            DeploymentId = deploymentId,
            InstancesDeployed = instancesDeployed,
            InstancesFailed = 0
        };
    }

    public static DeploymentResult PartialResult(string deploymentId, int deployed, int failed)
    {
        return new DeploymentResult
        {
            Success = failed == 0,
            DeploymentId = deploymentId,
            InstancesDeployed = deployed,
            InstancesFailed = failed,
            ErrorMessage = $"{failed} instance(s) failed deployment"
        };
    }

    public static DeploymentResult Failure(string deploymentId, string errorMessage)
    {
        return new DeploymentResult
        {
            Success = false,
            DeploymentId = deploymentId,
            InstancesDeployed = 0,
            InstancesFailed = 0,
            ErrorMessage = errorMessage
        };
    }
}
```

---

## Validation Examples

### ConfigProfile Validation

```csharp
var profile = new ConfigProfile
{
    Name = "payment-service.production",
    SchemaId = "payment-config.v1",
    Environment = ConfigEnvironment.Production,
    ServiceType = ServiceType.Microservice
};

if (!profile.IsValid(out var errors))
{
    foreach (var error in errors)
    {
        Console.WriteLine($"Validation Error: {error}");
    }
}
```

### ConfigVersion Validation

```csharp
var version = new ConfigVersion
{
    ConfigName = "payment-service.production",
    Version = "1.0.0",
    ConfigData = "{\"maxRetries\":3,\"timeout\":\"30s\"}",
    SchemaVersion = "1.0"
};

version.CalculateHash();

if (!version.IsValid(out var errors))
{
    Console.WriteLine("Version validation failed:");
    errors.ForEach(Console.WriteLine);
}
```

---

**Last Updated:** 2025-11-23
**Namespace:** `HotSwap.ConfigManager.Domain`
