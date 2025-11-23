# Domain Models Reference

**Version:** 1.0.0
**Namespace:** `HotSwap.Serverless.Domain.Models`

---

## Table of Contents

1. [Function](#function)
2. [FunctionVersion](#functionversion)
3. [FunctionAlias](#functionalias)
4. [Deployment](#deployment)
5. [RunnerNode](#runnernode)
6. [Trigger](#trigger)
7. [Enumerations](#enumerations)
8. [Value Objects](#value-objects)

---

## Function

Represents a serverless function definition.

**File:** `src/HotSwap.Serverless.Domain/Models/Function.cs`

```csharp
namespace HotSwap.Serverless.Domain.Models;

/// <summary>
/// Represents a serverless function.
/// </summary>
public class Function
{
    /// <summary>
    /// Unique function name (alphanumeric, dashes, max 64 chars).
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Human-readable description (optional).
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Function runtime (e.g., "Node18", "Python311", "Dotnet8").
    /// </summary>
    public required Runtime Runtime { get; set; }

    /// <summary>
    /// Function handler (e.g., "index.handler" for Node.js).
    /// Format varies by runtime.
    /// </summary>
    public required string Handler { get; set; }

    /// <summary>
    /// Memory allocation in MB (128, 256, 512, 1024, etc.).
    /// Min: 128 MB, Max: 10,240 MB, Increments: 64 MB
    /// </summary>
    public int MemorySize { get; set; } = 256;

    /// <summary>
    /// Execution timeout in seconds.
    /// Min: 1 second, Max: 900 seconds (15 minutes)
    /// </summary>
    public int Timeout { get; set; } = 30;

    /// <summary>
    /// Environment variables (key-value pairs, max 4 KB total).
    /// </summary>
    public Dictionary<string, string> Environment { get; set; } = new();

    /// <summary>
    /// Attached layer ARNs (max 5 layers).
    /// </summary>
    public List<string> Layers { get; set; } = new();

    /// <summary>
    /// VPC configuration (optional, for private resource access).
    /// </summary>
    public VpcConfig? VpcConfig { get; set; }

    /// <summary>
    /// Function tags (metadata, billing).
    /// </summary>
    public Dictionary<string, string> Tags { get; set; } = new();

    /// <summary>
    /// Current published version (e.g., "v5", "$LATEST").
    /// </summary>
    public string? PublishedVersion { get; set; }

    /// <summary>
    /// Owner user ID.
    /// </summary>
    public required string OwnerId { get; set; }

    /// <summary>
    /// Function creation timestamp (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last update timestamp (UTC).
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Total invocation count (lifetime).
    /// </summary>
    public long TotalInvocations { get; set; } = 0;

    /// <summary>
    /// Last invocation timestamp (UTC).
    /// </summary>
    public DateTime? LastInvokedAt { get; set; }

    /// <summary>
    /// Scaling configuration.
    /// </summary>
    public ScalingConfig ScalingConfig { get; set; } = new();

    /// <summary>
    /// Validates the function configuration.
    /// </summary>
    public bool IsValid(out List<string> errors)
    {
        errors = new List<string>();

        if (string.IsNullOrWhiteSpace(Name))
            errors.Add("Name is required");
        else if (!System.Text.RegularExpressions.Regex.IsMatch(Name, @"^[a-zA-Z0-9\-]{1,64}$"))
            errors.Add("Name must be alphanumeric with dashes, max 64 characters");

        if (string.IsNullOrWhiteSpace(Handler))
            errors.Add("Handler is required");

        if (MemorySize < 128 || MemorySize > 10240 || MemorySize % 64 != 0)
            errors.Add("MemorySize must be 128-10240 MB in 64 MB increments");

        if (Timeout < 1 || Timeout > 900)
            errors.Add("Timeout must be 1-900 seconds");

        if (Layers.Count > 5)
            errors.Add("Maximum 5 layers allowed");

        var envSize = Environment.Sum(kv => kv.Key.Length + kv.Value.Length);
        if (envSize > 4096)
            errors.Add("Environment variables exceed 4 KB limit");

        return errors.Count == 0;
    }

    /// <summary>
    /// Gets the CPU allocation based on memory size.
    /// CPU scales proportionally: 1 vCPU at 1,792 MB.
    /// </summary>
    public double GetCpuAllocation()
    {
        return MemorySize / 1792.0;
    }
}
```

---

## FunctionVersion

Represents an immutable function version with code.

**File:** `src/HotSwap.Serverless.Domain/Models/FunctionVersion.cs`

```csharp
namespace HotSwap.Serverless.Domain.Models;

/// <summary>
/// Represents an immutable function version.
/// </summary>
public class FunctionVersion
{
    /// <summary>
    /// Function name.
    /// </summary>
    public required string FunctionName { get; set; }

    /// <summary>
    /// Version number (auto-incremented: 1, 2, 3...).
    /// </summary>
    public int Version { get; set; }

    /// <summary>
    /// Version string (e.g., "v1", "v2", "$LATEST").
    /// </summary>
    public string VersionString => Version == 0 ? "$LATEST" : $"v{Version}";

    /// <summary>
    /// Code package SHA256 hash (for integrity verification).
    /// </summary>
    public required string CodeSha256 { get; set; }

    /// <summary>
    /// Code package size in bytes.
    /// </summary>
    public long CodeSize { get; set; }

    /// <summary>
    /// Storage location (S3/MinIO path).
    /// Format: "functions/{functionName}/versions/{version}/code.zip"
    /// </summary>
    public required string CodeLocation { get; set; }

    /// <summary>
    /// Runtime snapshot (at time of version creation).
    /// </summary>
    public required Runtime Runtime { get; set; }

    /// <summary>
    /// Handler snapshot.
    /// </summary>
    public required string Handler { get; set; }

    /// <summary>
    /// Memory size snapshot.
    /// </summary>
    public int MemorySize { get; set; }

    /// <summary>
    /// Timeout snapshot.
    /// </summary>
    public int Timeout { get; set; }

    /// <summary>
    /// Environment variables snapshot.
    /// </summary>
    public Dictionary<string, string> Environment { get; set; } = new();

    /// <summary>
    /// Layers snapshot.
    /// </summary>
    public List<string> Layers { get; set; } = new();

    /// <summary>
    /// Version creation timestamp (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Created by user ID.
    /// </summary>
    public required string CreatedBy { get; set; }

    /// <summary>
    /// Version description/changelog.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Invocation count for this version.
    /// </summary>
    public long InvocationCount { get; set; } = 0;

    /// <summary>
    /// Version status.
    /// </summary>
    public VersionStatus Status { get; set; } = VersionStatus.Active;

    /// <summary>
    /// Whether this version can be deleted.
    /// Cannot delete if actively deployed or has aliases.
    /// </summary>
    public bool CanDelete()
    {
        return Status != VersionStatus.Active || InvocationCount == 0;
    }
}
```

---

## FunctionAlias

Represents a mutable pointer to a function version.

**File:** `src/HotSwap.Serverless.Domain/Models/FunctionAlias.cs`

```csharp
namespace HotSwap.Serverless.Domain.Models;

/// <summary>
/// Represents a function alias (mutable pointer to versions).
/// </summary>
public class FunctionAlias
{
    /// <summary>
    /// Function name.
    /// </summary>
    public required string FunctionName { get; set; }

    /// <summary>
    /// Alias name (e.g., "production", "staging", "dev").
    /// </summary>
    public required string AliasName { get; set; }

    /// <summary>
    /// Primary version this alias points to.
    /// </summary>
    public required int Version { get; set; }

    /// <summary>
    /// Routing configuration (for canary/weighted deployments).
    /// Maps version → traffic percentage.
    /// Example: { "5": 90, "6": 10 } = 90% to v5, 10% to v6
    /// </summary>
    public Dictionary<int, double> RoutingConfig { get; set; } = new();

    /// <summary>
    /// Alias description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Alias creation timestamp (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last update timestamp (UTC).
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Updates the alias to point to a new version.
    /// </summary>
    public void UpdateVersion(int newVersion)
    {
        Version = newVersion;
        RoutingConfig.Clear();
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Sets weighted routing (for canary deployments).
    /// </summary>
    public void SetWeightedRouting(int primaryVersion, int canaryVersion, double canaryPercentage)
    {
        Version = primaryVersion;
        RoutingConfig = new Dictionary<int, double>
        {
            { primaryVersion, 100 - canaryPercentage },
            { canaryVersion, canaryPercentage }
        };
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Gets the version to route to based on weighted routing.
    /// </summary>
    public int GetRouteVersion(double randomValue)
    {
        if (RoutingConfig.Count == 0)
            return Version;

        double cumulative = 0;
        foreach (var (version, percentage) in RoutingConfig.OrderBy(x => x.Key))
        {
            cumulative += percentage;
            if (randomValue * 100 <= cumulative)
                return version;
        }

        return Version; // Fallback
    }
}
```

---

## Deployment

Represents a function deployment operation.

**File:** `src/HotSwap.Serverless.Domain/Models/Deployment.cs`

```csharp
namespace HotSwap.Serverless.Domain.Models;

/// <summary>
/// Represents a function deployment.
/// </summary>
public class Deployment
{
    /// <summary>
    /// Unique deployment ID (GUID).
    /// </summary>
    public required string DeploymentId { get; set; }

    /// <summary>
    /// Function name being deployed.
    /// </summary>
    public required string FunctionName { get; set; }

    /// <summary>
    /// Target version to deploy.
    /// </summary>
    public int TargetVersion { get; set; }

    /// <summary>
    /// Source version (current/previous version).
    /// </summary>
    public int? SourceVersion { get; set; }

    /// <summary>
    /// Deployment strategy.
    /// </summary>
    public DeploymentStrategy Strategy { get; set; } = DeploymentStrategy.AllAtOnce;

    /// <summary>
    /// Deployment configuration (strategy-specific settings).
    /// </summary>
    public DeploymentConfig Config { get; set; } = new();

    /// <summary>
    /// Current deployment status.
    /// </summary>
    public DeploymentStatus Status { get; set; } = DeploymentStatus.Pending;

    /// <summary>
    /// Deployment progress percentage (0-100).
    /// </summary>
    public double Progress { get; set; } = 0;

    /// <summary>
    /// Current deployment phase (for canary/rolling).
    /// </summary>
    public string? CurrentPhase { get; set; }

    /// <summary>
    /// Deployment start timestamp (UTC).
    /// </summary>
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Deployment completion timestamp (UTC).
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Deployed by user ID.
    /// </summary>
    public required string DeployedBy { get; set; }

    /// <summary>
    /// Deployment error message (if failed/rolled back).
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Rollback reason (if rolled back).
    /// </summary>
    public string? RollbackReason { get; set; }

    /// <summary>
    /// Health metrics snapshot at deployment time.
    /// </summary>
    public DeploymentMetrics? Metrics { get; set; }

    /// <summary>
    /// Whether deployment can be rolled back.
    /// </summary>
    public bool CanRollback()
    {
        return Status == DeploymentStatus.Completed && SourceVersion.HasValue;
    }

    /// <summary>
    /// Marks deployment as completed.
    /// </summary>
    public void MarkCompleted()
    {
        Status = DeploymentStatus.Completed;
        Progress = 100;
        CompletedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Marks deployment as failed.
    /// </summary>
    public void MarkFailed(string errorMessage)
    {
        Status = DeploymentStatus.Failed;
        ErrorMessage = errorMessage;
        CompletedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Marks deployment as rolled back.
    /// </summary>
    public void MarkRolledBack(string reason)
    {
        Status = DeploymentStatus.RolledBack;
        RollbackReason = reason;
        CompletedAt = DateTime.UtcNow;
    }
}

/// <summary>
/// Deployment configuration (strategy-specific settings).
/// </summary>
public class DeploymentConfig
{
    // Canary settings
    public double? CanaryPercentage { get; set; }
    public TimeSpan? CanaryDuration { get; set; }
    public List<double>? CanaryIncrements { get; set; }

    // Blue-Green settings
    public TimeSpan? TestDuration { get; set; }
    public bool? KeepBlue { get; set; }

    // Rolling settings
    public double? BatchSize { get; set; }
    public TimeSpan? BatchDuration { get; set; }

    // Rollback thresholds
    public double? RollbackOnErrorRate { get; set; }
    public double? RollbackOnLatencyP99 { get; set; }
    public double? RollbackOnSuccessRate { get; set; }
}

/// <summary>
/// Deployment metrics snapshot.
/// </summary>
public class DeploymentMetrics
{
    public long InvocationCount { get; set; }
    public long ErrorCount { get; set; }
    public double ErrorRate { get; set; }
    public double AvgDuration { get; set; }
    public double P99Duration { get; set; }
    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;
}
```

---

## RunnerNode

Represents a function execution worker node.

**File:** `src/HotSwap.Serverless.Domain/Models/RunnerNode.cs`

```csharp
namespace HotSwap.Serverless.Domain.Models;

/// <summary>
/// Represents a function runner node (execution worker).
/// </summary>
public class RunnerNode
{
    /// <summary>
    /// Unique node identifier (e.g., "runner-1").
    /// </summary>
    public required string NodeId { get; set; }

    /// <summary>
    /// Runner hostname or IP address.
    /// </summary>
    public required string Hostname { get; set; }

    /// <summary>
    /// Runner port number.
    /// </summary>
    public int Port { get; set; } = 8080;

    /// <summary>
    /// Current health status.
    /// </summary>
    public RunnerHealth Health { get; set; } = new();

    /// <summary>
    /// Resource capacity and usage.
    /// </summary>
    public RunnerResources Resources { get; set; } = new();

    /// <summary>
    /// Active function containers on this runner.
    /// Maps function name+version → container ID.
    /// </summary>
    public Dictionary<string, string> ActiveContainers { get; set; } = new();

    /// <summary>
    /// Last heartbeat timestamp (UTC).
    /// </summary>
    public DateTime LastHeartbeat { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Runner startup timestamp (UTC).
    /// </summary>
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Runner metrics.
    /// </summary>
    public RunnerMetrics Metrics { get; set; } = new();

    /// <summary>
    /// Checks if the runner is healthy based on heartbeat and health checks.
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
    /// Checks if runner can accept new invocations.
    /// </summary>
    public bool CanAcceptInvocations()
    {
        return IsHealthy() &&
               Resources.CpuUsage < 80 &&
               Resources.MemoryUsage < 80 &&
               ActiveContainers.Count < Resources.MaxContainers;
    }
}

/// <summary>
/// Runner health information.
/// </summary>
public class RunnerHealth
{
    public bool IsHealthy { get; set; } = true;
    public double CpuUsage { get; set; } = 0;
    public double MemoryUsage { get; set; } = 0;
    public int ActiveContainers { get; set; } = 0;
    public int QueuedInvocations { get; set; } = 0;
}

/// <summary>
/// Runner resource capacity and usage.
/// </summary>
public class RunnerResources
{
    public int TotalCpuCores { get; set; } = 8;
    public long TotalMemoryMB { get; set; } = 16384; // 16 GB
    public int MaxContainers { get; set; } = 100;
    public double CpuUsage { get; set; } = 0; // Percentage
    public double MemoryUsage { get; set; } = 0; // Percentage
}

/// <summary>
/// Runner performance metrics.
/// </summary>
public class RunnerMetrics
{
    public long TotalInvocations { get; set; } = 0;
    public long SuccessfulInvocations { get; set; } = 0;
    public long FailedInvocations { get; set; } = 0;
    public long ColdStarts { get; set; } = 0;
    public double AvgInvocationDuration { get; set; } = 0;
    public double AvgColdStartDuration { get; set; } = 0;
}
```

---

## Trigger

Represents an event trigger for a function.

**File:** `src/HotSwap.Serverless.Domain/Models/Trigger.cs`

```csharp
namespace HotSwap.Serverless.Domain.Models;

/// <summary>
/// Represents an event trigger for a function.
/// </summary>
public class Trigger
{
    /// <summary>
    /// Unique trigger ID (GUID).
    /// </summary>
    public required string TriggerId { get; set; }

    /// <summary>
    /// Function name this trigger invokes.
    /// </summary>
    public required string FunctionName { get; set; }

    /// <summary>
    /// Function version or alias to invoke.
    /// </summary>
    public string TargetVersion { get; set; } = "$LATEST";

    /// <summary>
    /// Trigger type.
    /// </summary>
    public TriggerType Type { get; set; }

    /// <summary>
    /// Whether the trigger is enabled.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Trigger configuration (type-specific settings).
    /// </summary>
    public TriggerConfig Config { get; set; } = new();

    /// <summary>
    /// Trigger creation timestamp (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last trigger execution timestamp (UTC).
    /// </summary>
    public DateTime? LastExecutedAt { get; set; }

    /// <summary>
    /// Total trigger executions.
    /// </summary>
    public long ExecutionCount { get; set; } = 0;

    /// <summary>
    /// Failed executions.
    /// </summary>
    public long FailureCount { get; set; } = 0;

    /// <summary>
    /// Validates the trigger configuration.
    /// </summary>
    public bool IsValid(out List<string> errors)
    {
        errors = new List<string>();

        if (string.IsNullOrWhiteSpace(FunctionName))
            errors.Add("FunctionName is required");

        if (Type == TriggerType.Http && string.IsNullOrWhiteSpace(Config.HttpPath))
            errors.Add("HttpPath is required for HTTP triggers");

        if (Type == TriggerType.Scheduled && string.IsNullOrWhiteSpace(Config.ScheduleExpression))
            errors.Add("ScheduleExpression is required for Scheduled triggers");

        if (Type == TriggerType.Queue && string.IsNullOrWhiteSpace(Config.QueueName))
            errors.Add("QueueName is required for Queue triggers");

        return errors.Count == 0;
    }
}

/// <summary>
/// Trigger configuration (type-specific settings).
/// </summary>
public class TriggerConfig
{
    // HTTP trigger settings
    public string? HttpPath { get; set; }
    public List<string>? HttpMethods { get; set; }
    public bool? CorsEnabled { get; set; }

    // Scheduled trigger settings
    public string? ScheduleExpression { get; set; }
    public string? Timezone { get; set; }

    // Queue trigger settings
    public string? QueueName { get; set; }
    public int? BatchSize { get; set; }
    public int? VisibilityTimeout { get; set; }

    // Stream trigger settings
    public string? StreamArn { get; set; }
    public string? StartingPosition { get; set; }
    public int? ParallelizationFactor { get; set; }
}
```

---

## Enumerations

### Runtime

**File:** `src/HotSwap.Serverless.Domain/Enums/Runtime.cs`

```csharp
namespace HotSwap.Serverless.Domain.Enums;

/// <summary>
/// Supported function runtimes.
/// </summary>
public enum Runtime
{
    // Node.js
    Node16,
    Node18,
    Node20,

    // Python
    Python38,
    Python39,
    Python310,
    Python311,

    // .NET
    Dotnet6,
    Dotnet7,
    Dotnet8,

    // Go
    Go119,
    Go120,
    Go121,

    // Java
    Java11,
    Java17,
    Java21,

    // Ruby (planned)
    Ruby30,
    Ruby31,
    Ruby32
}

/// <summary>
/// Runtime extensions.
/// </summary>
public static class RuntimeExtensions
{
    public static string GetImageName(this Runtime runtime)
    {
        return runtime switch
        {
            Runtime.Node16 => "node:16-alpine",
            Runtime.Node18 => "node:18-alpine",
            Runtime.Node20 => "node:20-alpine",
            Runtime.Python38 => "python:3.8-slim",
            Runtime.Python39 => "python:3.9-slim",
            Runtime.Python310 => "python:3.10-slim",
            Runtime.Python311 => "python:3.11-slim",
            Runtime.Dotnet6 => "mcr.microsoft.com/dotnet/runtime:6.0",
            Runtime.Dotnet7 => "mcr.microsoft.com/dotnet/runtime:7.0",
            Runtime.Dotnet8 => "mcr.microsoft.com/dotnet/runtime:8.0",
            Runtime.Go119 => "golang:1.19-alpine",
            Runtime.Go120 => "golang:1.20-alpine",
            Runtime.Go121 => "golang:1.21-alpine",
            Runtime.Java11 => "openjdk:11-jre-slim",
            Runtime.Java17 => "openjdk:17-jre-slim",
            Runtime.Java21 => "openjdk:21-jre-slim",
            _ => throw new ArgumentException($"Unknown runtime: {runtime}")
        };
    }

    public static int GetEstimatedColdStartMs(this Runtime runtime)
    {
        return runtime switch
        {
            Runtime.Node16 or Runtime.Node18 or Runtime.Node20 => 150,
            Runtime.Python38 or Runtime.Python39 or Runtime.Python310 or Runtime.Python311 => 200,
            Runtime.Dotnet6 or Runtime.Dotnet7 or Runtime.Dotnet8 => 400,
            Runtime.Go119 or Runtime.Go120 or Runtime.Go121 => 100,
            Runtime.Java11 or Runtime.Java17 or Runtime.Java21 => 550,
            _ => 250
        };
    }
}
```

### DeploymentStrategy

```csharp
namespace HotSwap.Serverless.Domain.Enums;

/// <summary>
/// Deployment strategy types.
/// </summary>
public enum DeploymentStrategy
{
    /// <summary>
    /// Deploy all at once (fastest, highest risk).
    /// </summary>
    AllAtOnce,

    /// <summary>
    /// Gradual canary deployment with automatic rollback.
    /// </summary>
    Canary,

    /// <summary>
    /// Blue-green deployment with instant switch.
    /// </summary>
    BlueGreen,

    /// <summary>
    /// Rolling deployment across runner nodes.
    /// </summary>
    Rolling,

    /// <summary>
    /// A/B testing deployment (manual promotion).
    /// </summary>
    ABTesting
}
```

### DeploymentStatus

```csharp
namespace HotSwap.Serverless.Domain.Enums;

/// <summary>
/// Deployment status.
/// </summary>
public enum DeploymentStatus
{
    Pending,
    InProgress,
    Completed,
    Failed,
    RolledBack,
    Cancelled
}
```

### TriggerType

```csharp
namespace HotSwap.Serverless.Domain.Enums;

/// <summary>
/// Event trigger types.
/// </summary>
public enum TriggerType
{
    /// <summary>
    /// HTTP API trigger (GET, POST, etc.).
    /// </summary>
    Http,

    /// <summary>
    /// Scheduled trigger (cron/rate expressions).
    /// </summary>
    Scheduled,

    /// <summary>
    /// Queue-based trigger (message queue).
    /// </summary>
    Queue,

    /// <summary>
    /// Stream-based trigger (Kafka, Kinesis).
    /// </summary>
    Stream
}
```

### VersionStatus

```csharp
namespace HotSwap.Serverless.Domain.Enums;

/// <summary>
/// Function version status.
/// </summary>
public enum VersionStatus
{
    Active,
    Deprecated,
    Archived
}
```

---

## Value Objects

### VpcConfig

**File:** `src/HotSwap.Serverless.Domain/ValueObjects/VpcConfig.cs`

```csharp
namespace HotSwap.Serverless.Domain.ValueObjects;

/// <summary>
/// VPC configuration for function execution.
/// </summary>
public class VpcConfig
{
    /// <summary>
    /// Subnet IDs (for multi-AZ deployment).
    /// </summary>
    public List<string> SubnetIds { get; set; } = new();

    /// <summary>
    /// Security group IDs.
    /// </summary>
    public List<string> SecurityGroupIds { get; set; } = new();

    /// <summary>
    /// VPC ID (optional, for validation).
    /// </summary>
    public string? VpcId { get; set; }
}
```

### ScalingConfig

```csharp
namespace HotSwap.Serverless.Domain.ValueObjects;

/// <summary>
/// Auto-scaling configuration.
/// </summary>
public class ScalingConfig
{
    public int MinInstances { get; set; } = 0;
    public int MaxInstances { get; set; } = 100;
    public int TargetConcurrency { get; set; } = 10;
    public TimeSpan ScaleUpCooldown { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan ScaleDownCooldown { get; set; } = TimeSpan.FromMinutes(5);
    public int PreProvisionedConcurrency { get; set; } = 0;
    public bool KeepWarmEnabled { get; set; } = false;
    public TimeSpan KeepWarmInterval { get; set; } = TimeSpan.FromMinutes(5);
}
```

### InvocationResult

```csharp
namespace HotSwap.Serverless.Domain.ValueObjects;

/// <summary>
/// Function invocation result.
/// </summary>
public class InvocationResult
{
    public bool Success { get; set; }
    public int StatusCode { get; set; }
    public string? Body { get; set; }
    public Dictionary<string, string> Headers { get; set; } = new();
    public int ExecutionTimeMs { get; set; }
    public int BilledDurationMs { get; set; }
    public int MemoryUsedMB { get; set; }
    public bool WasColdStart { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ErrorType { get; set; }
    public string? LogStreamName { get; set; }
    public string RequestId { get; set; } = Guid.NewGuid().ToString();
}
```

---

## Validation Examples

### Function Validation

```csharp
var function = new Function
{
    Name = "image-processor",
    Runtime = Runtime.Python311,
    Handler = "handler.process_image",
    MemorySize = 512,
    Timeout = 30,
    OwnerId = "user-123"
};

if (!function.IsValid(out var errors))
{
    foreach (var error in errors)
    {
        Console.WriteLine($"Validation Error: {error}");
    }
}
```

### Trigger Validation

```csharp
var trigger = new Trigger
{
    TriggerId = Guid.NewGuid().ToString(),
    FunctionName = "image-processor",
    Type = TriggerType.Http,
    Config = new TriggerConfig
    {
        HttpPath = "/api/process",
        HttpMethods = new List<string> { "POST" },
        CorsEnabled = true
    }
};

if (!trigger.IsValid(out var errors))
{
    Console.WriteLine("Trigger validation failed:");
    errors.ForEach(Console.WriteLine);
}
```

---

**Last Updated:** 2025-11-23
**Namespace:** `HotSwap.Serverless.Domain`
