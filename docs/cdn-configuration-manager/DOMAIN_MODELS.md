# Domain Models Reference

**Version:** 1.0.0
**Namespace:** `HotSwap.CDN.Domain.Models`

---

## Table of Contents

1. [Configuration](#configuration)
2. [EdgeLocation](#edgelocation)
3. [Deployment](#deployment)
4. [ConfigurationVersion](#configurationversion)
5. [PerformanceMetrics](#performancemetrics)
6. [Enumerations](#enumerations)
7. [Value Objects](#value-objects)

---

## Configuration

Represents a CDN configuration (cache rule, routing rule, security rule, etc.).

**File:** `src/HotSwap.CDN.Domain/Models/Configuration.cs`

```csharp
namespace HotSwap.CDN.Domain.Models;

/// <summary>
/// Represents a CDN configuration that can be deployed to edge locations.
/// </summary>
public class Configuration
{
    /// <summary>
    /// Unique configuration identifier (GUID format).
    /// </summary>
    public required string ConfigurationId { get; set; }

    /// <summary>
    /// Human-readable configuration name.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Configuration type (CacheRule, RoutingRule, SecurityRule, etc.).
    /// </summary>
    public ConfigurationType Type { get; set; }

    /// <summary>
    /// Configuration content (JSON format).
    /// </summary>
    public required string Content { get; set; }

    /// <summary>
    /// Schema version for validation (e.g., "1.0", "2.0").
    /// </summary>
    public required string SchemaVersion { get; set; }

    /// <summary>
    /// Semantic version of this configuration (e.g., "1.2.3").
    /// </summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// Optional description of the configuration.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Tags for organization and filtering.
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// Current configuration status.
    /// </summary>
    public ConfigurationStatus Status { get; set; } = ConfigurationStatus.Draft;

    /// <summary>
    /// User who created the configuration.
    /// </summary>
    public required string CreatedBy { get; set; }

    /// <summary>
    /// Configuration creation timestamp (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last update timestamp (UTC).
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Admin user who approved the configuration (if approved).
    /// </summary>
    public string? ApprovedBy { get; set; }

    /// <summary>
    /// Approval timestamp (UTC).
    /// </summary>
    public DateTime? ApprovedAt { get; set; }

    /// <summary>
    /// Whether this configuration is currently deployed.
    /// </summary>
    public bool IsDeployed { get; set; } = false;

    /// <summary>
    /// List of edge location IDs where this config is deployed.
    /// </summary>
    public List<string> DeployedLocations { get; set; } = new();

    /// <summary>
    /// Configuration-specific metadata.
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();

    /// <summary>
    /// Validates the configuration for required fields and constraints.
    /// </summary>
    public bool IsValid(out List<string> errors)
    {
        errors = new List<string>();

        if (string.IsNullOrWhiteSpace(ConfigurationId))
            errors.Add("ConfigurationId is required");

        if (string.IsNullOrWhiteSpace(Name))
            errors.Add("Name is required");

        if (string.IsNullOrWhiteSpace(Content))
            errors.Add("Content is required");

        if (string.IsNullOrWhiteSpace(SchemaVersion))
            errors.Add("SchemaVersion is required");

        if (string.IsNullOrWhiteSpace(CreatedBy))
            errors.Add("CreatedBy is required");

        // Validate JSON format
        try
        {
            var json = System.Text.Json.JsonDocument.Parse(Content);
        }
        catch
        {
            errors.Add("Content must be valid JSON");
        }

        // Validate semantic version format
        if (!System.Text.RegularExpressions.Regex.IsMatch(Version, @"^\d+\.\d+\.\d+$"))
            errors.Add("Version must follow semantic versioning (e.g., 1.0.0)");

        return errors.Count == 0;
    }

    /// <summary>
    /// Checks if the configuration is approved for production deployment.
    /// </summary>
    public bool IsApproved() => Status == ConfigurationStatus.Approved;

    /// <summary>
    /// Checks if the configuration is deprecated.
    /// </summary>
    public bool IsDeprecated() => Status == ConfigurationStatus.Deprecated;
}
```

---

## EdgeLocation

Represents a CDN edge location (Point of Presence).

**File:** `src/HotSwap.CDN.Domain/Models/EdgeLocation.cs`

```csharp
namespace HotSwap.CDN.Domain.Models;

/// <summary>
/// Represents a CDN edge location (POP).
/// </summary>
public class EdgeLocation
{
    /// <summary>
    /// Unique edge location identifier (e.g., "us-east-1").
    /// </summary>
    public required string LocationId { get; set; }

    /// <summary>
    /// Human-readable location name (e.g., "US East (Virginia)").
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Geographic region (e.g., "North America", "Europe").
    /// </summary>
    public required string Region { get; set; }

    /// <summary>
    /// Country code (ISO 3166-1 alpha-2, e.g., "US", "GB").
    /// </summary>
    public required string CountryCode { get; set; }

    /// <summary>
    /// City name.
    /// </summary>
    public string? City { get; set; }

    /// <summary>
    /// Location type (EdgePOP, Shield, Origin).
    /// </summary>
    public LocationType Type { get; set; } = LocationType.EdgePOP;

    /// <summary>
    /// Edge location endpoint URL.
    /// </summary>
    public required string Endpoint { get; set; }

    /// <summary>
    /// Current health status.
    /// </summary>
    public EdgeLocationHealth Health { get; set; } = new();

    /// <summary>
    /// Performance metrics.
    /// </summary>
    public EdgeLocationMetrics Metrics { get; set; } = new();

    /// <summary>
    /// Last heartbeat timestamp (UTC).
    /// </summary>
    public DateTime LastHeartbeat { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Edge location startup timestamp (UTC).
    /// </summary>
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Whether the edge location is active and accepting traffic.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Capacity configuration.
    /// </summary>
    public EdgeCapacity Capacity { get; set; } = new();

    /// <summary>
    /// List of configuration IDs currently deployed to this location.
    /// </summary>
    public List<string> ActiveConfigurations { get; set; } = new();

    /// <summary>
    /// Location-specific metadata.
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();

    /// <summary>
    /// Checks if the edge location is healthy based on heartbeat and health checks.
    /// </summary>
    public bool IsHealthy()
    {
        // Unhealthy if no heartbeat in last 2 minutes
        if (DateTime.UtcNow - LastHeartbeat > TimeSpan.FromMinutes(2))
            return false;

        return Health.IsHealthy;
    }

    /// <summary>
    /// Updates the heartbeat timestamp.
    /// </summary>
    public void RecordHeartbeat()
    {
        LastHeartbeat = DateTime.UtcNow;
    }

    /// <summary>
    /// Validates the edge location configuration.
    /// </summary>
    public bool IsValid(out List<string> errors)
    {
        errors = new List<string>();

        if (string.IsNullOrWhiteSpace(LocationId))
            errors.Add("LocationId is required");

        if (string.IsNullOrWhiteSpace(Name))
            errors.Add("Name is required");

        if (string.IsNullOrWhiteSpace(Region))
            errors.Add("Region is required");

        if (string.IsNullOrWhiteSpace(CountryCode))
            errors.Add("CountryCode is required");
        else if (CountryCode.Length != 2)
            errors.Add("CountryCode must be ISO 3166-1 alpha-2 (2 characters)");

        if (string.IsNullOrWhiteSpace(Endpoint))
            errors.Add("Endpoint is required");
        else if (!Uri.IsWellFormedUriString(Endpoint, UriKind.Absolute))
            errors.Add("Endpoint must be a valid absolute URL");

        return errors.Count == 0;
    }
}

/// <summary>
/// Edge location health information.
/// </summary>
public class EdgeLocationHealth
{
    /// <summary>
    /// Overall health status.
    /// </summary>
    public bool IsHealthy { get; set; } = true;

    /// <summary>
    /// CPU usage percentage (0-100).
    /// </summary>
    public double CpuUsage { get; set; } = 0;

    /// <summary>
    /// Memory usage percentage (0-100).
    /// </summary>
    public double MemoryUsage { get; set; } = 0;

    /// <summary>
    /// Disk usage percentage (0-100).
    /// </summary>
    public double DiskUsage { get; set; } = 0;

    /// <summary>
    /// Network bandwidth utilization percentage (0-100).
    /// </summary>
    public double BandwidthUtilization { get; set; } = 0;

    /// <summary>
    /// Number of active connections.
    /// </summary>
    public int ActiveConnections { get; set; } = 0;
}

/// <summary>
/// Edge location performance metrics.
/// </summary>
public class EdgeLocationMetrics
{
    /// <summary>
    /// Total requests served by this edge location.
    /// </summary>
    public long RequestsTotal { get; set; } = 0;

    /// <summary>
    /// Cache hit count.
    /// </summary>
    public long CacheHits { get; set; } = 0;

    /// <summary>
    /// Cache miss count.
    /// </summary>
    public long CacheMisses { get; set; } = 0;

    /// <summary>
    /// Cache hit rate percentage (0-100).
    /// </summary>
    public double CacheHitRate => RequestsTotal > 0
        ? (double)CacheHits / RequestsTotal * 100
        : 0;

    /// <summary>
    /// Average response latency in milliseconds.
    /// </summary>
    public double AvgLatencyMs { get; set; } = 0;

    /// <summary>
    /// P99 response latency in milliseconds.
    /// </summary>
    public double P99LatencyMs { get; set; } = 0;

    /// <summary>
    /// Total bytes sent (outbound bandwidth).
    /// </summary>
    public long BytesSent { get; set; } = 0;

    /// <summary>
    /// Total bytes received (inbound bandwidth).
    /// </summary>
    public long BytesReceived { get; set; } = 0;

    /// <summary>
    /// Error count (4xx + 5xx responses).
    /// </summary>
    public long ErrorsTotal { get; set; } = 0;

    /// <summary>
    /// Error rate percentage (0-100).
    /// </summary>
    public double ErrorRate => RequestsTotal > 0
        ? (double)ErrorsTotal / RequestsTotal * 100
        : 0;
}

/// <summary>
/// Edge location capacity configuration.
/// </summary>
public class EdgeCapacity
{
    /// <summary>
    /// Maximum requests per second.
    /// </summary>
    public int MaxRequestsPerSec { get; set; } = 100000;

    /// <summary>
    /// Maximum bandwidth in Mbps.
    /// </summary>
    public int MaxBandwidthMbps { get; set; } = 10000;

    /// <summary>
    /// Maximum concurrent connections.
    /// </summary>
    public int MaxConnections { get; set; } = 100000;

    /// <summary>
    /// Cache storage size in GB.
    /// </summary>
    public int CacheStorageGB { get; set; } = 1000;
}
```

---

## Deployment

Represents a configuration deployment to edge locations.

**File:** `src/HotSwap.CDN.Domain/Models/Deployment.cs`

```csharp
namespace HotSwap.CDN.Domain.Models;

/// <summary>
/// Represents a configuration deployment to edge locations.
/// </summary>
public class Deployment
{
    /// <summary>
    /// Unique deployment identifier (GUID format).
    /// </summary>
    public required string DeploymentId { get; set; }

    /// <summary>
    /// Configuration ID being deployed.
    /// </summary>
    public required string ConfigurationId { get; set; }

    /// <summary>
    /// Configuration version being deployed.
    /// </summary>
    public required string ConfigurationVersion { get; set; }

    /// <summary>
    /// Deployment strategy (DirectDeployment, RegionalCanary, etc.).
    /// </summary>
    public DeploymentStrategy Strategy { get; set; }

    /// <summary>
    /// Target regions for deployment.
    /// </summary>
    public List<string> TargetRegions { get; set; } = new();

    /// <summary>
    /// Target edge location IDs (if specific locations specified).
    /// </summary>
    public List<string> TargetLocations { get; set; } = new();

    /// <summary>
    /// Current deployment status.
    /// </summary>
    public DeploymentStatus Status { get; set; } = DeploymentStatus.Pending;

    /// <summary>
    /// Canary configuration (if using canary strategy).
    /// </summary>
    public CanaryConfig? CanaryConfig { get; set; }

    /// <summary>
    /// Rollback configuration.
    /// </summary>
    public RollbackConfig RollbackConfig { get; set; } = new();

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
    /// Deployment duration in seconds.
    /// </summary>
    public double DurationSeconds => CompletedAt.HasValue
        ? (CompletedAt.Value - StartedAt).TotalSeconds
        : (DateTime.UtcNow - StartedAt).TotalSeconds;

    /// <summary>
    /// Deployment progress (0-100).
    /// </summary>
    public int ProgressPercentage { get; set; } = 0;

    /// <summary>
    /// Edge locations where deployment succeeded.
    /// </summary>
    public List<string> SuccessfulLocations { get; set; } = new();

    /// <summary>
    /// Edge locations where deployment failed.
    /// </summary>
    public List<string> FailedLocations { get; set; } = new();

    /// <summary>
    /// Deployment error messages (if any).
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// Performance metrics snapshot before deployment.
    /// </summary>
    public PerformanceSnapshot? PreDeploymentMetrics { get; set; }

    /// <summary>
    /// Performance metrics snapshot after deployment.
    /// </summary>
    public PerformanceSnapshot? PostDeploymentMetrics { get; set; }

    /// <summary>
    /// Deployment-specific metadata.
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();

    /// <summary>
    /// Validates the deployment configuration.
    /// </summary>
    public bool IsValid(out List<string> errors)
    {
        errors = new List<string>();

        if (string.IsNullOrWhiteSpace(DeploymentId))
            errors.Add("DeploymentId is required");

        if (string.IsNullOrWhiteSpace(ConfigurationId))
            errors.Add("ConfigurationId is required");

        if (string.IsNullOrWhiteSpace(ConfigurationVersion))
            errors.Add("ConfigurationVersion is required");

        if (string.IsNullOrWhiteSpace(DeployedBy))
            errors.Add("DeployedBy is required");

        if (TargetRegions.Count == 0 && TargetLocations.Count == 0)
            errors.Add("At least one target region or location must be specified");

        return errors.Count == 0;
    }

    /// <summary>
    /// Checks if the deployment is in a terminal state.
    /// </summary>
    public bool IsTerminal() => Status == DeploymentStatus.Completed
        || Status == DeploymentStatus.Failed
        || Status == DeploymentStatus.RolledBack;
}

/// <summary>
/// Canary deployment configuration.
/// </summary>
public class CanaryConfig
{
    /// <summary>
    /// Initial canary traffic percentage (e.g., 10).
    /// </summary>
    public int InitialPercentage { get; set; } = 10;

    /// <summary>
    /// Duration to monitor canary before promotion.
    /// </summary>
    public TimeSpan MonitorDuration { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Whether to automatically promote canary if healthy.
    /// </summary>
    public bool AutoPromote { get; set; } = true;

    /// <summary>
    /// Promotion increments (e.g., [10, 50, 100]).
    /// </summary>
    public List<int> PromotionSteps { get; set; } = new() { 10, 50, 100 };

    /// <summary>
    /// Current canary step index.
    /// </summary>
    public int CurrentStep { get; set; } = 0;
}

/// <summary>
/// Rollback configuration.
/// </summary>
public class RollbackConfig
{
    /// <summary>
    /// Whether automatic rollback is enabled.
    /// </summary>
    public bool AutoRollback { get; set; } = true;

    /// <summary>
    /// Cache hit rate threshold for rollback (e.g., 80%).
    /// </summary>
    public double CacheHitRateThreshold { get; set; } = 80.0;

    /// <summary>
    /// Error rate threshold for rollback (e.g., 1%).
    /// </summary>
    public double ErrorRateThreshold { get; set; } = 1.0;

    /// <summary>
    /// P99 latency threshold for rollback in milliseconds (e.g., 200).
    /// </summary>
    public double P99LatencyThresholdMs { get; set; } = 200.0;
}

/// <summary>
/// Performance metrics snapshot.
/// </summary>
public class PerformanceSnapshot
{
    /// <summary>
    /// Snapshot timestamp (UTC).
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Cache hit rate percentage.
    /// </summary>
    public double CacheHitRate { get; set; }

    /// <summary>
    /// Average latency in milliseconds.
    /// </summary>
    public double AvgLatencyMs { get; set; }

    /// <summary>
    /// P99 latency in milliseconds.
    /// </summary>
    public double P99LatencyMs { get; set; }

    /// <summary>
    /// Error rate percentage.
    /// </summary>
    public double ErrorRate { get; set; }

    /// <summary>
    /// Total requests per second.
    /// </summary>
    public double RequestsPerSec { get; set; }
}
```

---

## ConfigurationVersion

Represents a versioned configuration snapshot.

**File:** `src/HotSwap.CDN.Domain/Models/ConfigurationVersion.cs`

```csharp
namespace HotSwap.CDN.Domain.Models;

/// <summary>
/// Represents a versioned snapshot of a configuration.
/// </summary>
public class ConfigurationVersion
{
    /// <summary>
    /// Unique version identifier (GUID format).
    /// </summary>
    public required string VersionId { get; set; }

    /// <summary>
    /// Parent configuration ID.
    /// </summary>
    public required string ConfigurationId { get; set; }

    /// <summary>
    /// Semantic version number (e.g., "1.0.0").
    /// </summary>
    public required string Version { get; set; }

    /// <summary>
    /// Configuration content (immutable).
    /// </summary>
    public required string Content { get; set; }

    /// <summary>
    /// Schema version used for validation.
    /// </summary>
    public required string SchemaVersion { get; set; }

    /// <summary>
    /// Change description.
    /// </summary>
    public string? ChangeDescription { get; set; }

    /// <summary>
    /// User who created this version.
    /// </summary>
    public required string CreatedBy { get; set; }

    /// <summary>
    /// Version creation timestamp (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Whether this version is currently deployed.
    /// </summary>
    public bool IsDeployed { get; set; } = false;

    /// <summary>
    /// Tags applied to this version.
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// Compares this version with another version and returns differences.
    /// </summary>
    public VersionDiff CompareTo(ConfigurationVersion other)
    {
        return new VersionDiff
        {
            FromVersion = other.Version,
            ToVersion = this.Version,
            ContentChanged = this.Content != other.Content,
            SchemaVersionChanged = this.SchemaVersion != other.SchemaVersion,
            // Additional diff logic would go here
        };
    }
}

/// <summary>
/// Represents differences between two configuration versions.
/// </summary>
public class VersionDiff
{
    public required string FromVersion { get; set; }
    public required string ToVersion { get; set; }
    public bool ContentChanged { get; set; }
    public bool SchemaVersionChanged { get; set; }
    public List<string> Changes { get; set; } = new();
}
```

---

## Enumerations

### ConfigurationType

**File:** `src/HotSwap.CDN.Domain/Enums/ConfigurationType.cs`

```csharp
namespace HotSwap.CDN.Domain.Enums;

/// <summary>
/// Represents the type of a CDN configuration.
/// </summary>
public enum ConfigurationType
{
    /// <summary>
    /// Cache control rules (TTL, cache keys, purging).
    /// </summary>
    CacheRule,

    /// <summary>
    /// Origin routing and failover rules.
    /// </summary>
    RoutingRule,

    /// <summary>
    /// Security policies (WAF, rate limiting, geo-blocking).
    /// </summary>
    SecurityRule,

    /// <summary>
    /// SSL/TLS certificate configuration.
    /// </summary>
    SSLCertificate,

    /// <summary>
    /// HTTP response modification rules.
    /// </summary>
    ResponseModification
}
```

### ConfigurationStatus

```csharp
namespace HotSwap.CDN.Domain.Enums;

/// <summary>
/// Represents the status of a configuration in the approval workflow.
/// </summary>
public enum ConfigurationStatus
{
    /// <summary>
    /// Configuration is in draft state (not yet submitted for approval).
    /// </summary>
    Draft,

    /// <summary>
    /// Configuration is pending admin approval.
    /// </summary>
    PendingApproval,

    /// <summary>
    /// Configuration is approved for deployment.
    /// </summary>
    Approved,

    /// <summary>
    /// Configuration is deprecated (marked for removal).
    /// </summary>
    Deprecated,

    /// <summary>
    /// Configuration has been archived.
    /// </summary>
    Archived
}
```

### LocationType

```csharp
namespace HotSwap.CDN.Domain.Enums;

/// <summary>
/// Represents the type of an edge location.
/// </summary>
public enum LocationType
{
    /// <summary>
    /// Edge Point of Presence (serves end users).
    /// </summary>
    EdgePOP,

    /// <summary>
    /// Shield location (mid-tier cache).
    /// </summary>
    Shield,

    /// <summary>
    /// Origin server location.
    /// </summary>
    Origin
}
```

### DeploymentStrategy

```csharp
namespace HotSwap.CDN.Domain.Enums;

/// <summary>
/// Represents the deployment strategy for configuration rollout.
/// </summary>
public enum DeploymentStrategy
{
    /// <summary>
    /// Direct deployment to all target locations immediately.
    /// </summary>
    DirectDeployment,

    /// <summary>
    /// Regional canary deployment with progressive rollout.
    /// </summary>
    RegionalCanary,

    /// <summary>
    /// Blue-green deployment with instant traffic switch.
    /// </summary>
    BlueGreenDeployment,

    /// <summary>
    /// Rolling deployment region-by-region.
    /// </summary>
    RollingRegional,

    /// <summary>
    /// Geographic wave deployment based on time zones.
    /// </summary>
    GeographicWave
}
```

### DeploymentStatus

```csharp
namespace HotSwap.CDN.Domain.Enums;

/// <summary>
/// Represents the current status of a deployment.
/// </summary>
public enum DeploymentStatus
{
    /// <summary>
    /// Deployment is queued, awaiting execution.
    /// </summary>
    Pending,

    /// <summary>
    /// Deployment is currently in progress.
    /// </summary>
    InProgress,

    /// <summary>
    /// Deployment is in canary phase (partial rollout).
    /// </summary>
    Canary,

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
    /// Deployment was paused manually.
    /// </summary>
    Paused
}
```

---

## Value Objects

### DeploymentResult

**File:** `src/HotSwap.CDN.Domain/ValueObjects/DeploymentResult.cs`

```csharp
namespace HotSwap.CDN.Domain.ValueObjects;

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
    /// List of edge locations where deployment succeeded.
    /// </summary>
    public List<string> SuccessfulLocations { get; private set; } = new();

    /// <summary>
    /// List of edge locations where deployment failed.
    /// </summary>
    public List<string> FailedLocations { get; private set; } = new();

    /// <summary>
    /// Error messages (if deployment failed).
    /// </summary>
    public List<string> Errors { get; private set; } = new();

    /// <summary>
    /// Deployment timestamp (UTC).
    /// </summary>
    public DateTime Timestamp { get; private set; } = DateTime.UtcNow;

    public static DeploymentResult SuccessResult(string deploymentId, List<string> locations)
    {
        return new DeploymentResult
        {
            Success = true,
            DeploymentId = deploymentId,
            SuccessfulLocations = locations
        };
    }

    public static DeploymentResult PartialSuccess(string deploymentId, List<string> successful, List<string> failed)
    {
        return new DeploymentResult
        {
            Success = failed.Count == 0,
            DeploymentId = deploymentId,
            SuccessfulLocations = successful,
            FailedLocations = failed
        };
    }

    public static DeploymentResult Failure(string deploymentId, List<string> errors)
    {
        return new DeploymentResult
        {
            Success = false,
            DeploymentId = deploymentId,
            Errors = errors
        };
    }
}
```

---

## Validation Examples

### Configuration Validation

```csharp
var configuration = new Configuration
{
    ConfigurationId = Guid.NewGuid().ToString(),
    Name = "static-assets-cache",
    Type = ConfigurationType.CacheRule,
    Content = "{\"pathPattern\":\"/assets/*\",\"ttl\":3600}",
    SchemaVersion = "1.0",
    CreatedBy = "admin@example.com"
};

if (!configuration.IsValid(out var errors))
{
    foreach (var error in errors)
    {
        Console.WriteLine($"Validation Error: {error}");
    }
}
```

### EdgeLocation Validation

```csharp
var edgeLocation = new EdgeLocation
{
    LocationId = "us-east-1",
    Name = "US East (Virginia)",
    Region = "North America",
    CountryCode = "US",
    City = "Virginia",
    Endpoint = "https://cdn-us-east-1.example.com"
};

if (!edgeLocation.IsValid(out var errors))
{
    Console.WriteLine("Edge location validation failed:");
    errors.ForEach(Console.WriteLine);
}
```

### Deployment Validation

```csharp
var deployment = new Deployment
{
    DeploymentId = Guid.NewGuid().ToString(),
    ConfigurationId = "config-abc123",
    ConfigurationVersion = "1.0.0",
    Strategy = DeploymentStrategy.RegionalCanary,
    TargetRegions = new List<string> { "us-east-1", "us-west-1" },
    DeployedBy = "operator@example.com",
    CanaryConfig = new CanaryConfig
    {
        InitialPercentage = 10,
        AutoPromote = true
    }
};

if (!deployment.IsValid(out var errors))
{
    Console.WriteLine("Deployment validation failed:");
    errors.ForEach(Console.WriteLine);
}
```

---

**Last Updated:** 2025-11-23
**Namespace:** `HotSwap.CDN.Domain`
