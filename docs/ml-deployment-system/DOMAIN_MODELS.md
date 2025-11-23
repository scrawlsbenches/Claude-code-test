# Domain Models Reference

**Version:** 1.0.0
**Namespace:** `HotSwap.MLDeployment.Domain.Models`

---

## Table of Contents

1. [Model](#model)
2. [ModelVersion](#modelversion)
3. [Deployment](#deployment)
4. [InferenceRequest](#inferencerequest)
5. [ModelMetrics](#modelmetrics)
6. [Enumerations](#enumerations)
7. [Value Objects](#value-objects)

---

## Model

Represents a registered ML model.

**File:** `src/HotSwap.MLDeployment.Domain/Models/Model.cs`

```csharp
namespace HotSwap.MLDeployment.Domain.Models;

/// <summary>
/// Represents a machine learning model in the system.
/// </summary>
public class Model
{
    /// <summary>
    /// Unique model identifier (GUID format).
    /// </summary>
    public required string ModelId { get; set; }

    /// <summary>
    /// Model name (e.g., "fraud-detection", "recommendation-engine").
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Human-readable description (optional).
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// ML framework used (TensorFlow, PyTorch, scikit-learn, ONNX).
    /// </summary>
    public required ModelFramework Framework { get; set; }

    /// <summary>
    /// Model type (Classification, Regression, Clustering, etc.).
    /// </summary>
    public required ModelType Type { get; set; }

    /// <summary>
    /// Current active version in production.
    /// </summary>
    public string? ActiveVersion { get; set; }

    /// <summary>
    /// All versions of this model.
    /// </summary>
    public List<ModelVersion> Versions { get; set; } = new();

    /// <summary>
    /// Model owner (user or team).
    /// </summary>
    public required string Owner { get; set; }

    /// <summary>
    /// Tags for categorization and search.
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// Model creation timestamp (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last update timestamp (UTC).
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Whether the model is archived (soft delete).
    /// </summary>
    public bool IsArchived { get; set; } = false;

    /// <summary>
    /// Validates the model for required fields.
    /// </summary>
    public bool IsValid(out List<string> errors)
    {
        errors = new List<string>();

        if (string.IsNullOrWhiteSpace(ModelId))
            errors.Add("ModelId is required");

        if (string.IsNullOrWhiteSpace(Name))
            errors.Add("Name is required");
        else if (!System.Text.RegularExpressions.Regex.IsMatch(Name, @"^[a-z0-9-]+$"))
            errors.Add("Name must contain only lowercase alphanumeric characters and dashes");

        if (string.IsNullOrWhiteSpace(Owner))
            errors.Add("Owner is required");

        return errors.Count == 0;
    }
}
```

---

## ModelVersion

Represents a specific version of a model.

**File:** `src/HotSwap.MLDeployment.Domain/Models/ModelVersion.cs`

```csharp
namespace HotSwap.MLDeployment.Domain.Models;

/// <summary>
/// Represents a specific version of an ML model.
/// </summary>
public class ModelVersion
{
    /// <summary>
    /// Unique version identifier (GUID format).
    /// </summary>
    public required string VersionId { get; set; }

    /// <summary>
    /// Parent model identifier.
    /// </summary>
    public required string ModelId { get; set; }

    /// <summary>
    /// Semantic version (e.g., "1.0.0", "2.1.3").
    /// </summary>
    public required string Version { get; set; }

    /// <summary>
    /// Model artifact storage path (S3/MinIO path).
    /// </summary>
    public required string ArtifactPath { get; set; }

    /// <summary>
    /// Model artifact checksum (SHA-256).
    /// </summary>
    public required string Checksum { get; set; }

    /// <summary>
    /// Model artifact size in bytes.
    /// </summary>
    public long ArtifactSizeBytes { get; set; }

    /// <summary>
    /// Input schema (JSON Schema format).
    /// </summary>
    public required string InputSchema { get; set; }

    /// <summary>
    /// Output schema (JSON Schema format).
    /// </summary>
    public required string OutputSchema { get; set; }

    /// <summary>
    /// Training metadata (dataset, hyperparameters, metrics).
    /// </summary>
    public TrainingMetadata? Metadata { get; set; }

    /// <summary>
    /// Performance baseline metrics.
    /// </summary>
    public PerformanceBaseline? Baseline { get; set; }

    /// <summary>
    /// Current version status.
    /// </summary>
    public ModelVersionStatus Status { get; set; } = ModelVersionStatus.Registered;

    /// <summary>
    /// Admin user who approved this version (if approved).
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
    /// Whether this version is deprecated.
    /// </summary>
    public bool IsDeprecated { get; set; } = false;

    /// <summary>
    /// Validates the model version configuration.
    /// </summary>
    public bool IsValid(out List<string> errors)
    {
        errors = new List<string>();

        if (string.IsNullOrWhiteSpace(VersionId))
            errors.Add("VersionId is required");

        if (string.IsNullOrWhiteSpace(ModelId))
            errors.Add("ModelId is required");

        if (string.IsNullOrWhiteSpace(Version))
            errors.Add("Version is required");

        if (string.IsNullOrWhiteSpace(ArtifactPath))
            errors.Add("ArtifactPath is required");

        if (string.IsNullOrWhiteSpace(Checksum))
            errors.Add("Checksum is required");

        if (ArtifactSizeBytes <= 0)
            errors.Add("ArtifactSizeBytes must be positive");

        return errors.Count == 0;
    }
}

/// <summary>
/// Training metadata for a model version.
/// </summary>
public class TrainingMetadata
{
    public string? DatasetName { get; set; }
    public string? DatasetVersion { get; set; }
    public int TrainingSamples { get; set; }
    public int ValidationSamples { get; set; }
    public Dictionary<string, object> Hyperparameters { get; set; } = new();
    public Dictionary<string, double> TrainingMetrics { get; set; } = new();
    public DateTime TrainedAt { get; set; }
    public string? TrainingJob { get; set; }
}

/// <summary>
/// Performance baseline for validation.
/// </summary>
public class PerformanceBaseline
{
    public double Accuracy { get; set; }
    public double Precision { get; set; }
    public double Recall { get; set; }
    public double F1Score { get; set; }
    public double InferenceLatencyMs { get; set; }
    public long MemoryUsageBytes { get; set; }
}
```

---

## Deployment

Represents a model deployment.

**File:** `src/HotSwap.MLDeployment.Domain/Models/Deployment.cs`

```csharp
namespace HotSwap.MLDeployment.Domain.Models;

/// <summary>
/// Represents a model deployment.
/// </summary>
public class Deployment
{
    /// <summary>
    /// Unique deployment identifier (GUID format).
    /// </summary>
    public required string DeploymentId { get; set; }

    /// <summary>
    /// Model version being deployed.
    /// </summary>
    public required string VersionId { get; set; }

    /// <summary>
    /// Model identifier (for reference).
    /// </summary>
    public required string ModelId { get; set; }

    /// <summary>
    /// Deployment strategy.
    /// </summary>
    public required DeploymentStrategy Strategy { get; set; }

    /// <summary>
    /// Target environment (Development, Staging, Production).
    /// </summary>
    public required string Environment { get; set; }

    /// <summary>
    /// Strategy-specific configuration (JSON).
    /// </summary>
    public required string StrategyConfig { get; set; }

    /// <summary>
    /// Current deployment status.
    /// </summary>
    public DeploymentStatus Status { get; set; } = DeploymentStatus.Pending;

    /// <summary>
    /// Deployment progress (0-100).
    /// </summary>
    public int ProgressPercentage { get; set; } = 0;

    /// <summary>
    /// Current traffic percentage to new version (for Canary/A/B).
    /// </summary>
    public int TrafficPercentage { get; set; } = 0;

    /// <summary>
    /// Model servers assigned to this deployment.
    /// </summary>
    public List<string> AssignedServers { get; set; } = new();

    /// <summary>
    /// User who initiated the deployment.
    /// </summary>
    public required string InitiatedBy { get; set; }

    /// <summary>
    /// Deployment start timestamp (UTC).
    /// </summary>
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Deployment completion timestamp (UTC).
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Error message (if deployment failed).
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Performance validation results.
    /// </summary>
    public ValidationResult? ValidationResult { get; set; }

    /// <summary>
    /// Whether deployment was rolled back.
    /// </summary>
    public bool IsRolledBack { get; set; } = false;

    /// <summary>
    /// Rollback timestamp (UTC).
    /// </summary>
    public DateTime? RolledBackAt { get; set; }

    /// <summary>
    /// Validates the deployment configuration.
    /// </summary>
    public bool IsValid(out List<string> errors)
    {
        errors = new List<string>();

        if (string.IsNullOrWhiteSpace(DeploymentId))
            errors.Add("DeploymentId is required");

        if (string.IsNullOrWhiteSpace(VersionId))
            errors.Add("VersionId is required");

        if (string.IsNullOrWhiteSpace(ModelId))
            errors.Add("ModelId is required");

        if (string.IsNullOrWhiteSpace(Environment))
            errors.Add("Environment is required");

        if (TrafficPercentage < 0 || TrafficPercentage > 100)
            errors.Add("TrafficPercentage must be between 0 and 100");

        return errors.Count == 0;
    }
}

/// <summary>
/// Validation result for deployment.
/// </summary>
public class ValidationResult
{
    public bool Passed { get; set; }
    public List<string> Errors { get; set; } = new();
    public Dictionary<string, double> Metrics { get; set; } = new();
    public DateTime ValidatedAt { get; set; }
}
```

---

## InferenceRequest

Represents an inference request.

**File:** `src/HotSwap.MLDeployment.Domain/Models/InferenceRequest.cs`

```csharp
namespace HotSwap.MLDeployment.Domain.Models;

/// <summary>
/// Represents an inference request.
/// </summary>
public class InferenceRequest
{
    /// <summary>
    /// Unique request identifier (GUID format).
    /// </summary>
    public required string RequestId { get; set; }

    /// <summary>
    /// Model name for inference.
    /// </summary>
    public required string ModelName { get; set; }

    /// <summary>
    /// Specific version (optional, uses active if not specified).
    /// </summary>
    public string? Version { get; set; }

    /// <summary>
    /// Input features (JSON object).
    /// </summary>
    public required string Features { get; set; }

    /// <summary>
    /// Request metadata (headers, client info).
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();

    /// <summary>
    /// Request timestamp (UTC).
    /// </summary>
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Whether this is a batch request.
    /// </summary>
    public bool IsBatch { get; set; } = false;

    /// <summary>
    /// Whether this is an async request (long-running).
    /// </summary>
    public bool IsAsync { get; set; } = false;

    /// <summary>
    /// Current request status.
    /// </summary>
    public InferenceStatus Status { get; set; } = InferenceStatus.Pending;

    /// <summary>
    /// Prediction result (JSON object).
    /// </summary>
    public string? Prediction { get; set; }

    /// <summary>
    /// Prediction confidence/probability.
    /// </summary>
    public double? Confidence { get; set; }

    /// <summary>
    /// Inference latency in milliseconds.
    /// </summary>
    public double? LatencyMs { get; set; }

    /// <summary>
    /// Model version used for this prediction.
    /// </summary>
    public string? ModelVersionUsed { get; set; }

    /// <summary>
    /// Inference completion timestamp (UTC).
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Error message (if inference failed).
    /// </summary>
    public string? ErrorMessage { get; set; }
}
```

---

## ModelMetrics

Represents model performance metrics.

**File:** `src/HotSwap.MLDeployment.Domain/Models/ModelMetrics.cs`

```csharp
namespace HotSwap.MLDeployment.Domain.Models;

/// <summary>
/// Model performance metrics over a time window.
/// </summary>
public class ModelMetrics
{
    /// <summary>
    /// Model identifier.
    /// </summary>
    public required string ModelId { get; set; }

    /// <summary>
    /// Model version (optional, aggregates all versions if not specified).
    /// </summary>
    public string? Version { get; set; }

    /// <summary>
    /// Metrics time window start (UTC).
    /// </summary>
    public DateTime WindowStart { get; set; }

    /// <summary>
    /// Metrics time window end (UTC).
    /// </summary>
    public DateTime WindowEnd { get; set; }

    /// <summary>
    /// Total inference requests in window.
    /// </summary>
    public long TotalRequests { get; set; }

    /// <summary>
    /// Successful inferences.
    /// </summary>
    public long SuccessfulInferences { get; set; }

    /// <summary>
    /// Failed inferences.
    /// </summary>
    public long FailedInferences { get; set; }

    /// <summary>
    /// Average inference latency (milliseconds).
    /// </summary>
    public double AvgLatencyMs { get; set; }

    /// <summary>
    /// p50 latency (milliseconds).
    /// </summary>
    public double P50LatencyMs { get; set; }

    /// <summary>
    /// p95 latency (milliseconds).
    /// </summary>
    public double P95LatencyMs { get; set; }

    /// <summary>
    /// p99 latency (milliseconds).
    /// </summary>
    public double P99LatencyMs { get; set; }

    /// <summary>
    /// Throughput (requests per second).
    /// </summary>
    public double ThroughputRps { get; set; }

    /// <summary>
    /// Model accuracy (if ground truth available).
    /// </summary>
    public double? Accuracy { get; set; }

    /// <summary>
    /// Data drift score.
    /// </summary>
    public double? DriftScore { get; set; }

    /// <summary>
    /// Feature-level drift scores.
    /// </summary>
    public Dictionary<string, double> FeatureDrift { get; set; } = new();
}
```

---

## Enumerations

### ModelFramework

```csharp
namespace HotSwap.MLDeployment.Domain.Enums;

public enum ModelFramework
{
    TensorFlow,
    PyTorch,
    ScikitLearn,
    ONNX,
    XGBoost,
    LightGBM,
    Custom
}
```

### ModelType

```csharp
public enum ModelType
{
    Classification,
    Regression,
    Clustering,
    Recommendation,
    NaturalLanguageProcessing,
    ComputerVision,
    TimeSeries,
    Other
}
```

### DeploymentStrategy

```csharp
public enum DeploymentStrategy
{
    Canary,
    BlueGreen,
    ABTesting,
    Shadow,
    Rolling
}
```

### DeploymentStatus

```csharp
public enum DeploymentStatus
{
    Pending,
    Validating,
    Deploying,
    Active,
    Failed,
    RolledBack,
    Cancelled
}
```

### ModelVersionStatus

```csharp
public enum ModelVersionStatus
{
    Registered,
    Validating,
    Validated,
    Approved,
    Deployed,
    Deprecated,
    Archived
}
```

### InferenceStatus

```csharp
public enum InferenceStatus
{
    Pending,
    Processing,
    Completed,
    Failed,
    Timeout
}
```

---

## Validation Examples

### Model Validation

```csharp
var model = new Model
{
    ModelId = Guid.NewGuid().ToString(),
    Name = "fraud-detection",
    Framework = ModelFramework.TensorFlow,
    Type = ModelType.Classification,
    Owner = "ml-team@example.com"
};

if (!model.IsValid(out var errors))
{
    foreach (var error in errors)
    {
        Console.WriteLine($"Validation Error: {error}");
    }
}
```

### Deployment Validation

```csharp
var deployment = new Deployment
{
    DeploymentId = Guid.NewGuid().ToString(),
    VersionId = "version-123",
    ModelId = "model-456",
    Strategy = DeploymentStrategy.Canary,
    Environment = "Production",
    StrategyConfig = "{\"canaryPercentage\":10}",
    InitiatedBy = "admin@example.com"
};

if (!deployment.IsValid(out var errors))
{
    Console.WriteLine("Deployment validation failed:");
    errors.ForEach(Console.WriteLine);
}
```

---

**Last Updated:** 2025-11-23
**Namespace:** `HotSwap.MLDeployment.Domain`
