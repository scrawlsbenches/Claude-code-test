# Domain Models Reference

**Version:** 1.0.0
**Namespace:** `HotSwap.EdgeAI.Domain.Models`

---

## Table of Contents

1. [AIModel](#aimodel)
2. [Distribution](#distribution)
3. [EdgeDevice](#edgedevice)
4. [ModelPackage](#modelpackage)
5. [ValidationReport](#validationreport)
6. [Enumerations](#enumerations)
7. [Value Objects](#value-objects)

---

## AIModel

Represents an AI model in the distribution system.

**File:** `src/HotSwap.EdgeAI.Domain/Models/AIModel.cs`

```csharp
namespace HotSwap.EdgeAI.Domain.Models;

/// <summary>
/// Represents an AI model for edge deployment.
/// </summary>
public class AIModel
{
    /// <summary>
    /// Unique model identifier (format: {name}-{version}).
    /// </summary>
    public required string ModelId { get; set; }

    /// <summary>
    /// Model name (e.g., "object-detection").
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Semantic version (e.g., "2.0.0").
    /// </summary>
    public required string Version { get; set; }

    /// <summary>
    /// ML framework (TensorFlow, PyTorch, ONNX).
    /// </summary>
    public ModelFramework Framework { get; set; }

    /// <summary>
    /// Model artifact URL (S3/MinIO).
    /// </summary>
    public required string ArtifactUrl { get; set; }

    /// <summary>
    /// Model artifact checksum (SHA256).
    /// </summary>
    public required string Checksum { get; set; }

    /// <summary>
    /// Model artifact size in bytes.
    /// </summary>
    public long ArtifactSize { get; set; }

    /// <summary>
    /// Target device type.
    /// </summary>
    public string? TargetDeviceType { get; set; }

    /// <summary>
    /// Minimum memory requirement (MB).
    /// </summary>
    public int MinMemoryMB { get; set; }

    /// <summary>
    /// Input schema (JSON).
    /// </summary>
    public string? InputSchema { get; set; }

    /// <summary>
    /// Output schema (JSON).
    /// </summary>
    public string? OutputSchema { get; set; }

    /// <summary>
    /// Model validation status.
    /// </summary>
    public ValidationStatus ValidationStatus { get; set; } = ValidationStatus.Pending;

    /// <summary>
    /// Upload timestamp (UTC).
    /// </summary>
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Uploader user ID.
    /// </summary>
    public string? UploadedBy { get; set; }

    /// <summary>
    /// Model tags for categorization.
    /// </summary>
    public Dictionary<string, string> Tags { get; set; } = new();
}
```

---

## Distribution

Represents a model distribution plan and execution.

**File:** `src/HotSwap.EdgeAI.Domain/Models/Distribution.cs`

```csharp
namespace HotSwap.EdgeAI.Domain.Models;

/// <summary>
/// Represents a model distribution plan.
/// </summary>
public class Distribution
{
    /// <summary>
    /// Unique distribution identifier.
    /// </summary>
    public required string DistributionId { get; set; }

    /// <summary>
    /// Model to distribute.
    /// </summary>
    public required string ModelId { get; set; }

    /// <summary>
    /// Model version.
    /// </summary>
    public required string ModelVersion { get; set; }

    /// <summary>
    /// Distribution strategy.
    /// </summary>
    public DistributionStrategy Strategy { get; set; }

    /// <summary>
    /// Device filter criteria.
    /// </summary>
    public DeviceFilter? Filter { get; set; }

    /// <summary>
    /// Success criteria for automatic rollback.
    /// </summary>
    public SuccessCriteria? SuccessCriteria { get; set; }

    /// <summary>
    /// Current distribution status.
    /// </summary>
    public DistributionStatus Status { get; set; } = DistributionStatus.Pending;

    /// <summary>
    /// Total devices targeted.
    /// </summary>
    public int DevicesTargeted { get; set; }

    /// <summary>
    /// Devices successfully updated.
    /// </summary>
    public int DevicesUpdated { get; set; }

    /// <summary>
    /// Devices that failed to update.
    /// </summary>
    public int DevicesFailed { get; set; }

    /// <summary>
    /// Distribution start time (UTC).
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// Distribution completion time (UTC).
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Created timestamp (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Creator user ID.
    /// </summary>
    public string? CreatedBy { get; set; }
}

/// <summary>
/// Device filter for targeting specific devices.
/// </summary>
public class DeviceFilter
{
    public string? Region { get; set; }
    public string? DeviceType { get; set; }
    public int? MinMemoryMB { get; set; }
    public Dictionary<string, string> Tags { get; set; } = new();
}

/// <summary>
/// Success criteria for automatic rollback.
/// </summary>
public class SuccessCriteria
{
    public int MaxLatencyMs { get; set; } = 100;
    public double MaxErrorRate { get; set; } = 0.01;
    public double MaxLatencyIncrease { get; set; } = 0.20;
}
```

---

## EdgeDevice

Represents an edge device in the system.

**File:** `src/HotSwap.EdgeAI.Domain/Models/EdgeDevice.cs`

```csharp
namespace HotSwap.EdgeAI.Domain.Models;

/// <summary>
/// Represents an edge device.
/// </summary>
public class EdgeDevice
{
    /// <summary>
    /// Unique device identifier.
    /// </summary>
    public required string DeviceId { get; set; }

    /// <summary>
    /// Device name.
    /// </summary>
    public string? DeviceName { get; set; }

    /// <summary>
    /// Device type (edge-camera, edge-sensor, etc.).
    /// </summary>
    public required string DeviceType { get; set; }

    /// <summary>
    /// Geographic region.
    /// </summary>
    public string? Region { get; set; }

    /// <summary>
    /// Device capabilities.
    /// </summary>
    public DeviceCapabilities Capabilities { get; set; } = new();

    /// <summary>
    /// Currently deployed model version.
    /// </summary>
    public string? CurrentModelVersion { get; set; }

    /// <summary>
    /// Device health status.
    /// </summary>
    public DeviceHealth Health { get; set; } = new();

    /// <summary>
    /// Device registration timestamp (UTC).
    /// </summary>
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last heartbeat timestamp (UTC).
    /// </summary>
    public DateTime LastHeartbeat { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Device tags.
    /// </summary>
    public Dictionary<string, string> Tags { get; set; } = new();
}

/// <summary>
/// Device hardware capabilities.
/// </summary>
public class DeviceCapabilities
{
    public int CpuCores { get; set; }
    public int MemoryMB { get; set; }
    public int StorageGB { get; set; }
    public bool HasGpu { get; set; }
    public string? GpuModel { get; set; }
}

/// <summary>
/// Device health information.
/// </summary>
public class DeviceHealth
{
    public bool IsOnline { get; set; } = true;
    public double CpuUsage { get; set; }
    public double MemoryUsage { get; set; }
    public double DiskUsage { get; set; }
}
```

---

## ValidationReport

Represents model validation results.

**File:** `src/HotSwap.EdgeAI.Domain/Models/ValidationReport.cs`

```csharp
namespace HotSwap.EdgeAI.Domain.Models;

/// <summary>
/// Represents model validation results.
/// </summary>
public class ValidationReport
{
    /// <summary>
    /// Model being validated.
    /// </summary>
    public required string ModelId { get; set; }

    /// <summary>
    /// Validation status.
    /// </summary>
    public ValidationStatus Status { get; set; }

    /// <summary>
    /// Performance metrics.
    /// </summary>
    public PerformanceMetrics Metrics { get; set; } = new();

    /// <summary>
    /// Validation errors (if any).
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// Validation timestamp (UTC).
    /// </summary>
    public DateTime ValidatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Model performance metrics.
/// </summary>
public class PerformanceMetrics
{
    public double LatencyP50Ms { get; set; }
    public double LatencyP95Ms { get; set; }
    public double LatencyP99Ms { get; set; }
    public double Throughput { get; set; }
    public int MemoryUsageMB { get; set; }
    public double? Accuracy { get; set; }
}
```

---

## Enumerations

### ModelFramework

```csharp
public enum ModelFramework
{
    TensorFlow,
    PyTorch,
    ONNX,
    TensorFlowLite,
    CoreML
}
```

### DistributionStrategy

```csharp
public enum DistributionStrategy
{
    Direct,
    RegionalRollout,
    Canary,
    ABTesting,
    ProgressiveRollout
}
```

### DistributionStatus

```csharp
public enum DistributionStatus
{
    Pending,
    InProgress,
    Completed,
    Failed,
    RolledBack
}
```

### ValidationStatus

```csharp
public enum ValidationStatus
{
    Pending,
    InProgress,
    Passed,
    Failed
}
```

---

**Last Updated:** 2025-11-23
**Version:** 1.0.0
