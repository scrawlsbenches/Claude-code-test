# Domain Models Reference

**Version:** 1.0.0
**Namespace:** `HotSwap.Wasm.Domain.Models`

---

## Table of Contents

1. [WasmModule](#wasmmodule)
2. [EdgeNode](#edgenode)
3. [ModuleDeployment](#moduledeployment)
4. [WasiInterface](#wasiinterface)
5. [ResourceLimits](#resourcelimits)
6. [Enumerations](#enumerations)
7. [Value Objects](#value-objects)

---

## WasmModule

Represents a WebAssembly module registered in the system.

**File:** `src/HotSwap.Wasm.Domain/Models/WasmModule.cs`

```csharp
namespace HotSwap.Wasm.Domain.Models;

/// <summary>
/// Represents a WebAssembly module.
/// </summary>
public class WasmModule
{
    /// <summary>
    /// Unique module identifier (format: {name}-v{version}).
    /// </summary>
    public required string ModuleId { get; set; }

    /// <summary>
    /// Module name (alphanumeric, dashes, underscores).
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Semantic version (major.minor.patch).
    /// </summary>
    public required string Version { get; set; }

    /// <summary>
    /// Human-readable description (optional).
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// WASM binary storage path (MinIO object key).
    /// </summary>
    public required string BinaryPath { get; set; }

    /// <summary>
    /// SHA-256 checksum of WASM binary for integrity validation.
    /// </summary>
    public required string Checksum { get; set; }

    /// <summary>
    /// WASM binary size in bytes.
    /// </summary>
    public long SizeBytes { get; set; }

    /// <summary>
    /// WASI version supported by this module.
    /// </summary>
    public WasiVersion WasiVersion { get; set; } = WasiVersion.Preview2;

    /// <summary>
    /// Required WASI interfaces (e.g., "wasi:filesystem/types@0.2.0").
    /// </summary>
    public List<string> RequiredInterfaces { get; set; } = new();

    /// <summary>
    /// Exported functions (callable from host).
    /// </summary>
    public List<string> ExportedFunctions { get; set; } = new();

    /// <summary>
    /// Resource limits for module execution.
    /// </summary>
    public ResourceLimits Limits { get; set; } = new();

    /// <summary>
    /// Module metadata (build info, Git commit, etc.).
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();

    /// <summary>
    /// Module registration timestamp (UTC).
    /// </summary>
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// User who registered the module.
    /// </summary>
    public required string RegisteredBy { get; set; }

    /// <summary>
    /// Module status (Active, Deprecated, Disabled).
    /// </summary>
    public ModuleStatus Status { get; set; } = ModuleStatus.Active;

    /// <summary>
    /// Total deployments of this module version.
    /// </summary>
    public int DeploymentCount { get; set; } = 0;

    /// <summary>
    /// Total function invocations across all deployments.
    /// </summary>
    public long InvocationCount { get; set; } = 0;

    /// <summary>
    /// Validates the module for required fields and constraints.
    /// </summary>
    public bool IsValid(out List<string> errors)
    {
        errors = new List<string>();

        if (string.IsNullOrWhiteSpace(ModuleId))
            errors.Add("ModuleId is required");

        if (string.IsNullOrWhiteSpace(Name))
            errors.Add("Name is required");
        else if (!System.Text.RegularExpressions.Regex.IsMatch(Name, @"^[a-zA-Z0-9_-]+$"))
            errors.Add("Name must contain only alphanumeric characters, dashes, and underscores");

        if (string.IsNullOrWhiteSpace(Version))
            errors.Add("Version is required");
        else if (!IsValidSemanticVersion(Version))
            errors.Add("Version must follow semantic versioning (e.g., 1.2.3)");

        if (string.IsNullOrWhiteSpace(BinaryPath))
            errors.Add("BinaryPath is required");

        if (string.IsNullOrWhiteSpace(Checksum))
            errors.Add("Checksum is required");
        else if (Checksum.Length != 64) // SHA-256 produces 64 hex characters
            errors.Add("Checksum must be a valid SHA-256 hash (64 characters)");

        if (SizeBytes <= 0)
            errors.Add("SizeBytes must be greater than 0");
        else if (SizeBytes > 52428800) // 50 MB
            errors.Add("SizeBytes exceeds maximum allowed size of 50 MB");

        if (string.IsNullOrWhiteSpace(RegisteredBy))
            errors.Add("RegisteredBy is required");

        if (!Limits.IsValid(out var limitErrors))
            errors.AddRange(limitErrors);

        return errors.Count == 0;
    }

    /// <summary>
    /// Checks if the module is currently active.
    /// </summary>
    public bool IsActive() => Status == ModuleStatus.Active;

    /// <summary>
    /// Generates the module ID from name and version.
    /// </summary>
    public static string GenerateModuleId(string name, string version) => $"{name}-v{version}";

    /// <summary>
    /// Validates semantic versioning format.
    /// </summary>
    private bool IsValidSemanticVersion(string version)
    {
        var regex = new System.Text.RegularExpressions.Regex(@"^\d+\.\d+\.\d+$");
        return regex.IsMatch(version);
    }
}
```

---

## EdgeNode

Represents an edge computing node that hosts WASM modules.

**File:** `src/HotSwap.Wasm.Domain/Models/EdgeNode.cs`

```csharp
namespace HotSwap.Wasm.Domain.Models;

/// <summary>
/// Represents an edge computing node in the cluster.
/// </summary>
public class EdgeNode
{
    /// <summary>
    /// Unique node identifier (e.g., "edge-us-east-01").
    /// </summary>
    public required string NodeId { get; set; }

    /// <summary>
    /// Node hostname or IP address.
    /// </summary>
    public required string Hostname { get; set; }

    /// <summary>
    /// Node port number.
    /// </summary>
    public int Port { get; set; } = 8080;

    /// <summary>
    /// Geographic region (e.g., "us-east", "eu-central").
    /// </summary>
    public required string Region { get; set; }

    /// <summary>
    /// Availability zone within region (optional).
    /// </summary>
    public string? Zone { get; set; }

    /// <summary>
    /// WASM runtime type (Wasmtime, WasmEdge, Wasmer).
    /// </summary>
    public WasmRuntime Runtime { get; set; } = WasmRuntime.Wasmtime;

    /// <summary>
    /// WASM runtime version (e.g., "15.0.0").
    /// </summary>
    public required string RuntimeVersion { get; set; }

    /// <summary>
    /// Supported WASI version.
    /// </summary>
    public WasiVersion WasiVersion { get; set; } = WasiVersion.Preview2;

    /// <summary>
    /// Node capabilities (supported WASI interfaces).
    /// </summary>
    public List<string> Capabilities { get; set; } = new();

    /// <summary>
    /// Modules currently deployed on this node.
    /// </summary>
    public List<string> DeployedModules { get; set; } = new();

    /// <summary>
    /// Maximum modules this node can host.
    /// </summary>
    public int MaxModules { get; set; } = 1000;

    /// <summary>
    /// Node hardware specifications.
    /// </summary>
    public NodeHardware Hardware { get; set; } = new();

    /// <summary>
    /// Current health status.
    /// </summary>
    public NodeHealth Health { get; set; } = new();

    /// <summary>
    /// Performance metrics.
    /// </summary>
    public NodeMetrics Metrics { get; set; } = new();

    /// <summary>
    /// Last heartbeat timestamp (UTC).
    /// </summary>
    public DateTime LastHeartbeat { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Node startup timestamp (UTC).
    /// </summary>
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Node registration timestamp (UTC).
    /// </summary>
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Whether the node is enabled for deployments.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Checks if the node is healthy based on heartbeat and health checks.
    /// </summary>
    public bool IsHealthy()
    {
        if (!IsEnabled)
            return false;

        // Unhealthy if no heartbeat in last 2 minutes
        if (DateTime.UtcNow - LastHeartbeat > TimeSpan.FromMinutes(2))
            return false;

        return Health.IsHealthy;
    }

    /// <summary>
    /// Checks if the node has capacity for more modules.
    /// </summary>
    public bool HasCapacity() => DeployedModules.Count < MaxModules && IsHealthy();

    /// <summary>
    /// Updates the heartbeat timestamp.
    /// </summary>
    public void RecordHeartbeat()
    {
        LastHeartbeat = DateTime.UtcNow;
    }

    /// <summary>
    /// Adds a module to the deployed modules list.
    /// </summary>
    public bool AddModule(string moduleId)
    {
        if (DeployedModules.Count >= MaxModules)
            return false;

        if (!DeployedModules.Contains(moduleId))
        {
            DeployedModules.Add(moduleId);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Removes a module from the deployed modules list.
    /// </summary>
    public bool RemoveModule(string moduleId)
    {
        return DeployedModules.Remove(moduleId);
    }
}

/// <summary>
/// Node hardware specifications.
/// </summary>
public class NodeHardware
{
    /// <summary>
    /// CPU cores available.
    /// </summary>
    public int CpuCores { get; set; } = 4;

    /// <summary>
    /// Total memory in MB.
    /// </summary>
    public int TotalMemoryMB { get; set; } = 8192;

    /// <summary>
    /// Total disk space in GB.
    /// </summary>
    public int TotalDiskGB { get; set; } = 100;

    /// <summary>
    /// Operating system (Linux, Windows, macOS).
    /// </summary>
    public string OperatingSystem { get; set; } = "Linux";

    /// <summary>
    /// CPU architecture (x86_64, aarch64).
    /// </summary>
    public string Architecture { get; set; } = "x86_64";
}

/// <summary>
/// Node health information.
/// </summary>
public class NodeHealth
{
    /// <summary>
    /// Overall health status.
    /// </summary>
    public bool IsHealthy { get; set; } = true;

    /// <summary>
    /// Number of modules currently loaded.
    /// </summary>
    public int ModulesLoaded { get; set; } = 0;

    /// <summary>
    /// CPU usage percentage (0-100).
    /// </summary>
    public double CpuUsagePercent { get; set; } = 0;

    /// <summary>
    /// Memory usage in MB.
    /// </summary>
    public int MemoryUsageMB { get; set; } = 0;

    /// <summary>
    /// Disk usage in GB.
    /// </summary>
    public int DiskUsageGB { get; set; } = 0;

    /// <summary>
    /// Network bandwidth usage in Mbps.
    /// </summary>
    public double NetworkMbps { get; set; } = 0;
}

/// <summary>
/// Node performance metrics.
/// </summary>
public class NodeMetrics
{
    /// <summary>
    /// Total function invocations on this node.
    /// </summary>
    public long TotalInvocations { get; set; } = 0;

    /// <summary>
    /// Total failed invocations.
    /// </summary>
    public long FailedInvocations { get; set; } = 0;

    /// <summary>
    /// Average function invocation latency in milliseconds.
    /// </summary>
    public double AvgInvocationLatencyMs { get; set; } = 0;

    /// <summary>
    /// Average module initialization time in milliseconds.
    /// </summary>
    public double AvgModuleInitMs { get; set; } = 0;

    /// <summary>
    /// Current throughput in requests per second.
    /// </summary>
    public double ThroughputReqPerSec { get; set; } = 0;
}
```

---

## ModuleDeployment

Represents a WASM module deployment configuration and execution.

**File:** `src/HotSwap.Wasm.Domain/Models/ModuleDeployment.cs`

```csharp
namespace HotSwap.Wasm.Domain.Models;

/// <summary>
/// Represents a WASM module deployment.
/// </summary>
public class ModuleDeployment
{
    /// <summary>
    /// Unique deployment identifier (GUID format).
    /// </summary>
    public required string DeploymentId { get; set; }

    /// <summary>
    /// Module being deployed.
    /// </summary>
    public required string ModuleId { get; set; }

    /// <summary>
    /// Target regions for deployment.
    /// </summary>
    public List<string> TargetRegions { get; set; } = new();

    /// <summary>
    /// Target specific edge nodes (optional, overrides region selection).
    /// </summary>
    public List<string>? TargetNodes { get; set; }

    /// <summary>
    /// Deployment strategy type.
    /// </summary>
    public DeploymentStrategy Strategy { get; set; } = DeploymentStrategy.Rolling;

    /// <summary>
    /// Strategy-specific configuration (JSON serialized).
    /// </summary>
    public string StrategyConfig { get; set; } = "{}";

    /// <summary>
    /// Current deployment status.
    /// </summary>
    public DeploymentStatus Status { get; set; } = DeploymentStatus.Pending;

    /// <summary>
    /// Deployment progress percentage (0-100).
    /// </summary>
    public int ProgressPercent { get; set; } = 0;

    /// <summary>
    /// Nodes where deployment succeeded.
    /// </summary>
    public List<string> SucceededNodes { get; set; } = new();

    /// <summary>
    /// Nodes where deployment failed.
    /// </summary>
    public List<string> FailedNodes { get; set; } = new();

    /// <summary>
    /// Health check configuration.
    /// </summary>
    public HealthCheckConfig HealthCheck { get; set; } = new();

    /// <summary>
    /// Deployment creation timestamp (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// User who created the deployment.
    /// </summary>
    public required string CreatedBy { get; set; }

    /// <summary>
    /// Deployment execution start timestamp (UTC).
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// Deployment completion timestamp (UTC).
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// User who approved deployment execution (production only).
    /// </summary>
    public string? ApprovedBy { get; set; }

    /// <summary>
    /// Approval timestamp (UTC).
    /// </summary>
    public DateTime? ApprovedAt { get; set; }

    /// <summary>
    /// Error message if deployment failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Previous module version (for rollback).
    /// </summary>
    public string? PreviousModuleId { get; set; }

    /// <summary>
    /// Whether automatic rollback is enabled.
    /// </summary>
    public bool AutoRollbackEnabled { get; set; } = true;

    /// <summary>
    /// Validates the deployment configuration.
    /// </summary>
    public bool IsValid(out List<string> errors)
    {
        errors = new List<string>();

        if (string.IsNullOrWhiteSpace(DeploymentId))
            errors.Add("DeploymentId is required");

        if (string.IsNullOrWhiteSpace(ModuleId))
            errors.Add("ModuleId is required");

        if (TargetRegions.Count == 0 && (TargetNodes == null || TargetNodes.Count == 0))
            errors.Add("Either TargetRegions or TargetNodes must be specified");

        if (string.IsNullOrWhiteSpace(CreatedBy))
            errors.Add("CreatedBy is required");

        return errors.Count == 0;
    }

    /// <summary>
    /// Checks if the deployment is in a terminal state.
    /// </summary>
    public bool IsCompleted() => Status == DeploymentStatus.Completed ||
                                  Status == DeploymentStatus.Failed ||
                                  Status == DeploymentStatus.RolledBack;

    /// <summary>
    /// Calculates deployment duration.
    /// </summary>
    public TimeSpan? GetDuration()
    {
        if (StartedAt.HasValue && CompletedAt.HasValue)
            return CompletedAt.Value - StartedAt.Value;

        if (StartedAt.HasValue)
            return DateTime.UtcNow - StartedAt.Value;

        return null;
    }
}

/// <summary>
/// Health check configuration for deployment validation.
/// </summary>
public class HealthCheckConfig
{
    /// <summary>
    /// Health check function name to invoke.
    /// </summary>
    public string FunctionName { get; set; } = "health_check";

    /// <summary>
    /// Expected return value for healthy status.
    /// </summary>
    public string ExpectedResult { get; set; } = "OK";

    /// <summary>
    /// Health check timeout in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 5;

    /// <summary>
    /// Number of consecutive successes required.
    /// </summary>
    public int SuccessThreshold { get; set; } = 1;

    /// <summary>
    /// Number of consecutive failures to trigger rollback.
    /// </summary>
    public int FailureThreshold { get; set; } = 3;

    /// <summary>
    /// Whether health checks are enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;
}
```

---

## WasiInterface

Represents a WASI interface definition for compatibility validation.

**File:** `src/HotSwap.Wasm.Domain/Models/WasiInterface.cs`

```csharp
namespace HotSwap.Wasm.Domain.Models;

/// <summary>
/// Represents a WASI interface definition.
/// </summary>
public class WasiInterface
{
    /// <summary>
    /// Unique interface identifier (e.g., "wasi:filesystem/types@0.2.0").
    /// </summary>
    public required string InterfaceId { get; set; }

    /// <summary>
    /// Interface namespace (e.g., "wasi:filesystem").
    /// </summary>
    public required string Namespace { get; set; }

    /// <summary>
    /// Interface name (e.g., "types").
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Interface version (e.g., "0.2.0").
    /// </summary>
    public required string Version { get; set; }

    /// <summary>
    /// WIT (WebAssembly Interface Types) definition.
    /// </summary>
    public required string WitDefinition { get; set; }

    /// <summary>
    /// Human-readable description (optional).
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Interface compatibility mode.
    /// </summary>
    public InterfaceCompatibility Compatibility { get; set; } = InterfaceCompatibility.Backward;

    /// <summary>
    /// Current interface status (approval workflow).
    /// </summary>
    public InterfaceStatus Status { get; set; } = InterfaceStatus.Draft;

    /// <summary>
    /// Admin user who approved the interface (if approved).
    /// </summary>
    public string? ApprovedBy { get; set; }

    /// <summary>
    /// Approval timestamp (UTC).
    /// </summary>
    public DateTime? ApprovedAt { get; set; }

    /// <summary>
    /// Interface registration timestamp (UTC).
    /// </summary>
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// User who registered the interface.
    /// </summary>
    public required string RegisteredBy { get; set; }

    /// <summary>
    /// Interface deprecation timestamp (UTC).
    /// </summary>
    public DateTime? DeprecatedAt { get; set; }

    /// <summary>
    /// Deprecation reason (if deprecated).
    /// </summary>
    public string? DeprecationReason { get; set; }

    /// <summary>
    /// Migration guide URL (if deprecated).
    /// </summary>
    public string? MigrationGuide { get; set; }

    /// <summary>
    /// Validates the interface configuration.
    /// </summary>
    public bool IsValid(out List<string> errors)
    {
        errors = new List<string>();

        if (string.IsNullOrWhiteSpace(InterfaceId))
            errors.Add("InterfaceId is required");

        if (string.IsNullOrWhiteSpace(Namespace))
            errors.Add("Namespace is required");

        if (string.IsNullOrWhiteSpace(Name))
            errors.Add("Name is required");

        if (string.IsNullOrWhiteSpace(Version))
            errors.Add("Version is required");

        if (string.IsNullOrWhiteSpace(WitDefinition))
            errors.Add("WitDefinition is required");

        if (string.IsNullOrWhiteSpace(RegisteredBy))
            errors.Add("RegisteredBy is required");

        return errors.Count == 0;
    }

    /// <summary>
    /// Checks if the interface is approved for production use.
    /// </summary>
    public bool IsApproved() => Status == InterfaceStatus.Approved;

    /// <summary>
    /// Checks if the interface is deprecated.
    /// </summary>
    public bool IsDeprecated() => Status == InterfaceStatus.Deprecated;

    /// <summary>
    /// Generates the interface ID from namespace, name, and version.
    /// </summary>
    public static string GenerateInterfaceId(string @namespace, string name, string version)
    {
        return $"{@namespace}/{name}@{version}";
    }
}
```

---

## ResourceLimits

Represents resource limits for WASM module execution.

**File:** `src/HotSwap.Wasm.Domain/Models/ResourceLimits.cs`

```csharp
namespace HotSwap.Wasm.Domain.Models;

/// <summary>
/// Represents resource limits for WASM module execution.
/// </summary>
public class ResourceLimits
{
    /// <summary>
    /// Maximum memory in megabytes (1-512 MB).
    /// </summary>
    public int MaxMemoryMB { get; set; } = 128;

    /// <summary>
    /// Maximum CPU usage percentage (1-100).
    /// </summary>
    public int MaxCpuPercent { get; set; } = 50;

    /// <summary>
    /// Maximum execution time in seconds (1-300).
    /// </summary>
    public int MaxExecutionTimeSeconds { get; set; } = 30;

    /// <summary>
    /// Maximum number of concurrent invocations per module instance.
    /// </summary>
    public int MaxConcurrentInvocations { get; set; } = 100;

    /// <summary>
    /// Filesystem access mode.
    /// </summary>
    public FilesystemAccess FilesystemAccess { get; set; } = FilesystemAccess.ReadOnly;

    /// <summary>
    /// Allowed directories for filesystem access (if enabled).
    /// </summary>
    public List<string> AllowedDirectories { get; set; } = new();

    /// <summary>
    /// Network access mode.
    /// </summary>
    public NetworkAccess NetworkAccess { get; set; } = NetworkAccess.Disabled;

    /// <summary>
    /// Allowed hosts for network access (if enabled).
    /// </summary>
    public List<string> AllowedHosts { get; set; } = new();

    /// <summary>
    /// Environment variables available to the module.
    /// </summary>
    public Dictionary<string, string> EnvironmentVariables { get; set; } = new();

    /// <summary>
    /// Validates the resource limits.
    /// </summary>
    public bool IsValid(out List<string> errors)
    {
        errors = new List<string>();

        if (MaxMemoryMB < 1 || MaxMemoryMB > 512)
            errors.Add("MaxMemoryMB must be between 1 and 512");

        if (MaxCpuPercent < 1 || MaxCpuPercent > 100)
            errors.Add("MaxCpuPercent must be between 1 and 100");

        if (MaxExecutionTimeSeconds < 1 || MaxExecutionTimeSeconds > 300)
            errors.Add("MaxExecutionTimeSeconds must be between 1 and 300");

        if (MaxConcurrentInvocations < 1 || MaxConcurrentInvocations > 1000)
            errors.Add("MaxConcurrentInvocations must be between 1 and 1000");

        if (FilesystemAccess != FilesystemAccess.Disabled && AllowedDirectories.Count == 0)
            errors.Add("AllowedDirectories must be specified when filesystem access is enabled");

        if (NetworkAccess != NetworkAccess.Disabled && AllowedHosts.Count == 0)
            errors.Add("AllowedHosts must be specified when network access is enabled");

        return errors.Count == 0;
    }

    /// <summary>
    /// Creates default resource limits for production.
    /// </summary>
    public static ResourceLimits CreateProductionDefaults() => new()
    {
        MaxMemoryMB = 128,
        MaxCpuPercent = 50,
        MaxExecutionTimeSeconds = 30,
        MaxConcurrentInvocations = 100,
        FilesystemAccess = FilesystemAccess.ReadOnly,
        NetworkAccess = NetworkAccess.Disabled
    };

    /// <summary>
    /// Creates permissive resource limits for development.
    /// </summary>
    public static ResourceLimits CreateDevelopmentDefaults() => new()
    {
        MaxMemoryMB = 256,
        MaxCpuPercent = 80,
        MaxExecutionTimeSeconds = 60,
        MaxConcurrentInvocations = 50,
        FilesystemAccess = FilesystemAccess.ReadWrite,
        NetworkAccess = NetworkAccess.AllowList
    };
}
```

---

## Enumerations

### WasmRuntime

**File:** `src/HotSwap.Wasm.Domain/Enums/WasmRuntime.cs`

```csharp
namespace HotSwap.Wasm.Domain.Enums;

/// <summary>
/// Represents the WASM runtime implementation.
/// </summary>
public enum WasmRuntime
{
    /// <summary>
    /// Wasmtime (Bytecode Alliance) - recommended for production.
    /// </summary>
    Wasmtime,

    /// <summary>
    /// WasmEdge (CNCF) - highest performance, AI/ML extensions.
    /// </summary>
    WasmEdge,

    /// <summary>
    /// Wasmer - multi-language bindings.
    /// </summary>
    Wasmer
}
```

### WasiVersion

```csharp
namespace HotSwap.Wasm.Domain.Enums;

/// <summary>
/// Represents the WASI version supported.
/// </summary>
public enum WasiVersion
{
    /// <summary>
    /// WASI Preview 1 (original standard).
    /// </summary>
    Preview1,

    /// <summary>
    /// WASI Preview 2 (component model with WIT).
    /// </summary>
    Preview2
}
```

### ModuleStatus

```csharp
namespace HotSwap.Wasm.Domain.Enums;

/// <summary>
/// Represents the status of a WASM module.
/// </summary>
public enum ModuleStatus
{
    /// <summary>
    /// Module is active and available for deployment.
    /// </summary>
    Active,

    /// <summary>
    /// Module is deprecated (marked for removal).
    /// </summary>
    Deprecated,

    /// <summary>
    /// Module is disabled (not available for new deployments).
    /// </summary>
    Disabled
}
```

### DeploymentStrategy

```csharp
namespace HotSwap.Wasm.Domain.Enums;

/// <summary>
/// Represents the deployment strategy type.
/// </summary>
public enum DeploymentStrategy
{
    /// <summary>
    /// Progressive rollout (10% → 25% → 50% → 100%).
    /// </summary>
    Canary,

    /// <summary>
    /// Deploy new version alongside old, instant switch.
    /// </summary>
    BlueGreen,

    /// <summary>
    /// Sequential node updates with health checks.
    /// </summary>
    Rolling,

    /// <summary>
    /// Region-by-region deployment progression.
    /// </summary>
    Regional,

    /// <summary>
    /// Traffic splitting for A/B testing.
    /// </summary>
    ABTesting
}
```

### DeploymentStatus

```csharp
namespace HotSwap.Wasm.Domain.Enums;

/// <summary>
/// Represents the status of a deployment.
/// </summary>
public enum DeploymentStatus
{
    /// <summary>
    /// Deployment created, awaiting execution.
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
    /// Deployment paused (canary evaluation).
    /// </summary>
    Paused
}
```

### InterfaceCompatibility

```csharp
namespace HotSwap.Wasm.Domain.Enums;

/// <summary>
/// Represents interface compatibility mode.
/// </summary>
public enum InterfaceCompatibility
{
    /// <summary>
    /// No compatibility checks.
    /// </summary>
    None,

    /// <summary>
    /// New interface can work with old modules.
    /// </summary>
    Backward,

    /// <summary>
    /// Old interface can work with new modules.
    /// </summary>
    Forward,

    /// <summary>
    /// Bidirectional compatibility (both backward and forward).
    /// </summary>
    Full
}
```

### InterfaceStatus

```csharp
namespace HotSwap.Wasm.Domain.Enums;

/// <summary>
/// Represents the status of a WASI interface in the approval workflow.
/// </summary>
public enum InterfaceStatus
{
    /// <summary>
    /// Interface is in draft state (not yet submitted for approval).
    /// </summary>
    Draft,

    /// <summary>
    /// Interface is pending admin approval.
    /// </summary>
    PendingApproval,

    /// <summary>
    /// Interface is approved for production use.
    /// </summary>
    Approved,

    /// <summary>
    /// Interface is deprecated (marked for removal).
    /// </summary>
    Deprecated
}
```

### FilesystemAccess

```csharp
namespace HotSwap.Wasm.Domain.Enums;

/// <summary>
/// Represents filesystem access mode for WASM modules.
/// </summary>
public enum FilesystemAccess
{
    /// <summary>
    /// No filesystem access.
    /// </summary>
    Disabled,

    /// <summary>
    /// Read-only access to allowed directories.
    /// </summary>
    ReadOnly,

    /// <summary>
    /// Read/write access to allowed directories.
    /// </summary>
    ReadWrite
}
```

### NetworkAccess

```csharp
namespace HotSwap.Wasm.Domain.Enums;

/// <summary>
/// Represents network access mode for WASM modules.
/// </summary>
public enum NetworkAccess
{
    /// <summary>
    /// No network access.
    /// </summary>
    Disabled,

    /// <summary>
    /// Allow-list mode (only allowed hosts).
    /// </summary>
    AllowList,

    /// <summary>
    /// Unrestricted network access (use with caution).
    /// </summary>
    Unrestricted
}
```

---

## Value Objects

### DeploymentResult

**File:** `src/HotSwap.Wasm.Domain/ValueObjects/DeploymentResult.cs`

```csharp
namespace HotSwap.Wasm.Domain.ValueObjects;

/// <summary>
/// Result of a module deployment operation.
/// </summary>
public class DeploymentResult
{
    /// <summary>
    /// Whether deployment was successful.
    /// </summary>
    public bool Success { get; private set; }

    /// <summary>
    /// List of nodes where deployment succeeded.
    /// </summary>
    public List<string> SucceededNodes { get; private set; } = new();

    /// <summary>
    /// List of nodes where deployment failed.
    /// </summary>
    public List<string> FailedNodes { get; private set; } = new();

    /// <summary>
    /// Error message (if deployment failed).
    /// </summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>
    /// Deployment duration.
    /// </summary>
    public TimeSpan Duration { get; private set; }

    /// <summary>
    /// Deployment timestamp (UTC).
    /// </summary>
    public DateTime Timestamp { get; private set; } = DateTime.UtcNow;

    public static DeploymentResult SuccessResult(List<string> nodes, TimeSpan duration)
    {
        return new DeploymentResult
        {
            Success = true,
            SucceededNodes = nodes,
            Duration = duration
        };
    }

    public static DeploymentResult PartialSuccess(List<string> succeeded, List<string> failed, TimeSpan duration)
    {
        return new DeploymentResult
        {
            Success = false,
            SucceededNodes = succeeded,
            FailedNodes = failed,
            ErrorMessage = $"Partial deployment failure: {failed.Count} nodes failed",
            Duration = duration
        };
    }

    public static DeploymentResult Failure(string errorMessage, TimeSpan duration)
    {
        return new DeploymentResult
        {
            Success = false,
            ErrorMessage = errorMessage,
            Duration = duration
        };
    }
}
```

### InvocationResult

```csharp
namespace HotSwap.Wasm.Domain.ValueObjects;

/// <summary>
/// Result of a WASM function invocation.
/// </summary>
public class InvocationResult
{
    /// <summary>
    /// Whether invocation was successful.
    /// </summary>
    public bool Success { get; private set; }

    /// <summary>
    /// Function return value (JSON serialized).
    /// </summary>
    public string? ReturnValue { get; private set; }

    /// <summary>
    /// Error message (if invocation failed).
    /// </summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>
    /// Invocation duration in milliseconds.
    /// </summary>
    public double DurationMs { get; private set; }

    /// <summary>
    /// Memory used during invocation in bytes.
    /// </summary>
    public long MemoryUsedBytes { get; private set; }

    /// <summary>
    /// Invocation timestamp (UTC).
    /// </summary>
    public DateTime Timestamp { get; private set; } = DateTime.UtcNow;

    public static InvocationResult SuccessResult(string returnValue, double durationMs, long memoryUsed)
    {
        return new InvocationResult
        {
            Success = true,
            ReturnValue = returnValue,
            DurationMs = durationMs,
            MemoryUsedBytes = memoryUsed
        };
    }

    public static InvocationResult Failure(string errorMessage, double durationMs)
    {
        return new InvocationResult
        {
            Success = false,
            ErrorMessage = errorMessage,
            DurationMs = durationMs
        };
    }
}
```

---

## Validation Examples

### WasmModule Validation

```csharp
var module = new WasmModule
{
    ModuleId = "image-processor-v1.2.0",
    Name = "image-processor",
    Version = "1.2.0",
    BinaryPath = "s3://wasm-modules/image-processor/1.2.0/module.wasm",
    Checksum = "a1b2c3d4e5f6...",
    SizeBytes = 5242880, // 5 MB
    RegisteredBy = "developer@example.com"
};

if (!module.IsValid(out var errors))
{
    foreach (var error in errors)
    {
        Console.WriteLine($"Validation Error: {error}");
    }
}
```

### EdgeNode Capacity Check

```csharp
var node = new EdgeNode
{
    NodeId = "edge-us-east-01",
    Hostname = "edge01.us-east.example.com",
    Region = "us-east",
    RuntimeVersion = "15.0.0",
    RegisteredBy = "admin@example.com",
    MaxModules = 1000
};

if (node.HasCapacity())
{
    node.AddModule("image-processor-v1.2.0");
}
```

### ResourceLimits Configuration

```csharp
var limits = new ResourceLimits
{
    MaxMemoryMB = 256,
    MaxCpuPercent = 75,
    MaxExecutionTimeSeconds = 60,
    FilesystemAccess = FilesystemAccess.ReadOnly,
    AllowedDirectories = new List<string> { "/tmp", "/data" },
    NetworkAccess = NetworkAccess.AllowList,
    AllowedHosts = new List<string> { "api.example.com", "storage.example.com" }
};

if (!limits.IsValid(out var errors))
{
    Console.WriteLine("Resource limits validation failed:");
    errors.ForEach(Console.WriteLine);
}
```

---

**Last Updated:** 2025-11-23
**Namespace:** `HotSwap.Wasm.Domain`
