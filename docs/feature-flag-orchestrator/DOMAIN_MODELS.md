# Domain Models Reference

**Version:** 1.0.0
**Namespace:** `HotSwap.FeatureFlags.Domain.Models`

---

## Table of Contents

1. [FeatureFlag](#featureflag)
2. [Rollout](#rollout)
3. [Target](#target)
4. [Variant](#variant)
5. [Experiment](#experiment)
6. [FlagEvaluation](#flagevaluation)
7. [Enumerations](#enumerations)
8. [Value Objects](#value-objects)

---

## FeatureFlag

Represents a feature flag in the system.

**File:** `src/HotSwap.FeatureFlags.Domain/Models/FeatureFlag.cs`

```csharp
namespace HotSwap.FeatureFlags.Domain.Models;

/// <summary>
/// Represents a feature flag for progressive feature delivery.
/// </summary>
public class FeatureFlag
{
    /// <summary>
    /// Unique flag identifier (alphanumeric, dashes, dots).
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Human-readable description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Flag value type (Boolean, String, Number, JSON).
    /// </summary>
    public FlagType Type { get; set; } = FlagType.Boolean;

    /// <summary>
    /// Default value when no targeting rules match.
    /// </summary>
    public required string DefaultValue { get; set; }

    /// <summary>
    /// Current flag status.
    /// </summary>
    public FlagStatus Status { get; set; } = FlagStatus.Active;

    /// <summary>
    /// Environment where flag is deployed (Development, Staging, Production).
    /// </summary>
    public string Environment { get; set; } = "Development";

    /// <summary>
    /// Tags for organization (e.g., "frontend", "checkout", "experimental").
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// Creation timestamp (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last update timestamp (UTC).
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// User who created the flag.
    /// </summary>
    public required string CreatedBy { get; set; }

    /// <summary>
    /// User who last updated the flag.
    /// </summary>
    public string? UpdatedBy { get; set; }

    /// <summary>
    /// Archived timestamp (soft delete).
    /// </summary>
    public DateTime? ArchivedAt { get; set; }

    /// <summary>
    /// Active rollout strategy (if any).
    /// </summary>
    public Rollout? ActiveRollout { get; set; }

    /// <summary>
    /// Targeting rules for this flag.
    /// </summary>
    public List<Target> Targets { get; set; } = new();

    /// <summary>
    /// Variants for A/B testing (if applicable).
    /// </summary>
    public List<Variant>? Variants { get; set; }

    /// <summary>
    /// Validates the flag configuration.
    /// </summary>
    public bool IsValid(out List<string> errors)
    {
        errors = new List<string>();

        if (string.IsNullOrWhiteSpace(Name))
            errors.Add("Name is required");
        else if (!System.Text.RegularExpressions.Regex.IsMatch(Name, @"^[a-zA-Z0-9._-]+$"))
            errors.Add("Name must contain only alphanumeric characters, dots, dashes, and underscores");

        if (string.IsNullOrWhiteSpace(DefaultValue))
            errors.Add("DefaultValue is required");

        if (!ValidateDefaultValue(DefaultValue, Type))
            errors.Add($"DefaultValue is not valid for type {Type}");

        if (string.IsNullOrWhiteSpace(CreatedBy))
            errors.Add("CreatedBy is required");

        return errors.Count == 0;
    }

    /// <summary>
    /// Validates default value against flag type.
    /// </summary>
    private bool ValidateDefaultValue(string value, FlagType type)
    {
        try
        {
            return type switch
            {
                FlagType.Boolean => bool.TryParse(value, out _),
                FlagType.String => true,
                FlagType.Number => double.TryParse(value, out _),
                FlagType.JSON => System.Text.Json.JsonDocument.Parse(value) != null,
                _ => false
            };
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Checks if flag is active (not archived).
    /// </summary>
    public bool IsActive() => Status == FlagStatus.Active && !ArchivedAt.HasValue;

    /// <summary>
    /// Archives the flag (soft delete).
    /// </summary>
    public void Archive()
    {
        ArchivedAt = DateTime.UtcNow;
        Status = FlagStatus.Archived;
    }
}
```

---

## Rollout

Represents a rollout strategy for progressive feature delivery.

**File:** `src/HotSwap.FeatureFlags.Domain/Models/Rollout.cs`

```csharp
namespace HotSwap.FeatureFlags.Domain.Models;

/// <summary>
/// Represents a rollout strategy for progressive flag deployment.
/// </summary>
public class Rollout
{
    /// <summary>
    /// Unique rollout identifier (GUID format).
    /// </summary>
    public required string RolloutId { get; set; }

    /// <summary>
    /// Flag name this rollout applies to.
    /// </summary>
    public required string FlagName { get; set; }

    /// <summary>
    /// Rollout strategy type.
    /// </summary>
    public RolloutStrategy Strategy { get; set; } = RolloutStrategy.Canary;

    /// <summary>
    /// Rollout stages (for Canary and Percentage strategies).
    /// </summary>
    public List<RolloutStage> Stages { get; set; } = new();

    /// <summary>
    /// Current stage index (0-based).
    /// </summary>
    public int CurrentStageIndex { get; set; } = 0;

    /// <summary>
    /// Current rollout status.
    /// </summary>
    public RolloutStatus Status { get; set; } = RolloutStatus.Pending;

    /// <summary>
    /// Whether to automatically rollback on error.
    /// </summary>
    public bool RollbackOnError { get; set; } = true;

    /// <summary>
    /// Metrics thresholds for health checks.
    /// </summary>
    public MetricsThresholds? Thresholds { get; set; }

    /// <summary>
    /// Rollout start time (UTC).
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// Rollout completion time (UTC).
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Rollback timestamp (if rolled back).
    /// </summary>
    public DateTime? RolledBackAt { get; set; }

    /// <summary>
    /// Rollback reason.
    /// </summary>
    public string? RollbackReason { get; set; }

    /// <summary>
    /// Creation timestamp (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets current stage.
    /// </summary>
    public RolloutStage? GetCurrentStage()
    {
        if (CurrentStageIndex < 0 || CurrentStageIndex >= Stages.Count)
            return null;
        return Stages[CurrentStageIndex];
    }

    /// <summary>
    /// Advances to next rollout stage.
    /// </summary>
    public bool ProgressToNextStage()
    {
        if (CurrentStageIndex >= Stages.Count - 1)
            return false; // Already at last stage

        CurrentStageIndex++;
        return true;
    }

    /// <summary>
    /// Rolls back the rollout.
    /// </summary>
    public void Rollback(string reason)
    {
        Status = RolloutStatus.RolledBack;
        RolledBackAt = DateTime.UtcNow;
        RollbackReason = reason;
        CurrentStageIndex = 0; // Reset to 0%
    }

    /// <summary>
    /// Completes the rollout.
    /// </summary>
    public void Complete()
    {
        Status = RolloutStatus.Completed;
        CompletedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Validates the rollout configuration.
    /// </summary>
    public bool IsValid(out List<string> errors)
    {
        errors = new List<string>();

        if (string.IsNullOrWhiteSpace(RolloutId))
            errors.Add("RolloutId is required");

        if (string.IsNullOrWhiteSpace(FlagName))
            errors.Add("FlagName is required");

        if (Stages.Count == 0)
            errors.Add("At least one stage is required");

        // Validate stages are in ascending percentage order
        for (int i = 1; i < Stages.Count; i++)
        {
            if (Stages[i].Percentage <= Stages[i - 1].Percentage)
                errors.Add($"Stage {i} percentage must be greater than stage {i - 1}");
        }

        return errors.Count == 0;
    }
}

/// <summary>
/// Represents a single stage in a rollout.
/// </summary>
public class RolloutStage
{
    /// <summary>
    /// Percentage of users to enable (0-100).
    /// </summary>
    public int Percentage { get; set; }

    /// <summary>
    /// Duration to wait before progressing to next stage (ISO 8601 duration).
    /// </summary>
    public TimeSpan? Duration { get; set; }

    /// <summary>
    /// Whether to perform health check before progressing.
    /// </summary>
    public bool HealthCheck { get; set; } = true;

    /// <summary>
    /// Stage start time (UTC).
    /// </summary>
    public DateTime? StartedAt { get; set; }
}

/// <summary>
/// Metrics thresholds for rollout health checks.
/// </summary>
public class MetricsThresholds
{
    /// <summary>
    /// Maximum acceptable error rate increase (0-1, e.g., 0.05 = 5%).
    /// </summary>
    public double ErrorRateThreshold { get; set; } = 0.05;

    /// <summary>
    /// Maximum acceptable latency p99 increase (milliseconds).
    /// </summary>
    public double LatencyP99Threshold { get; set; } = 100;

    /// <summary>
    /// Minimum acceptable throughput (requests/sec).
    /// </summary>
    public double? MinThroughput { get; set; }
}
```

---

## Target

Represents targeting rules for flag evaluation.

**File:** `src/HotSwap.FeatureFlags.Domain/Models/Target.cs`

```csharp
namespace HotSwap.FeatureFlags.Domain.Models;

/// <summary>
/// Represents targeting rules for flag evaluation.
/// </summary>
public class Target
{
    /// <summary>
    /// Unique target identifier (GUID format).
    /// </summary>
    public required string TargetId { get; set; }

    /// <summary>
    /// Flag name this target applies to.
    /// </summary>
    public required string FlagName { get; set; }

    /// <summary>
    /// Target name/description.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Rule priority (lower = higher priority).
    /// </summary>
    public int Priority { get; set; } = 0;

    /// <summary>
    /// Targeting rules (AND logic).
    /// </summary>
    public List<TargetRule> Rules { get; set; } = new();

    /// <summary>
    /// Value to return when rules match.
    /// </summary>
    public required string Value { get; set; }

    /// <summary>
    /// Whether this target is active.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Creation timestamp (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Evaluates whether the context matches this target.
    /// </summary>
    public bool Matches(EvaluationContext context)
    {
        if (!IsActive)
            return false;

        // All rules must match (AND logic)
        return Rules.All(rule => rule.Matches(context));
    }
}

/// <summary>
/// Represents a single targeting rule.
/// </summary>
public class TargetRule
{
    /// <summary>
    /// Attribute name to evaluate (e.g., "userId", "country", "tier").
    /// </summary>
    public required string Attribute { get; set; }

    /// <summary>
    /// Operator for comparison.
    /// </summary>
    public RuleOperator Operator { get; set; } = RuleOperator.Equals;

    /// <summary>
    /// Value to compare against (can be single value or array).
    /// </summary>
    public required string Value { get; set; }

    /// <summary>
    /// Evaluates the rule against a context.
    /// </summary>
    public bool Matches(EvaluationContext context)
    {
        if (!context.Attributes.TryGetValue(Attribute, out var contextValue))
            return false; // Attribute not present

        return Operator switch
        {
            RuleOperator.Equals => contextValue == Value,
            RuleOperator.NotEquals => contextValue != Value,
            RuleOperator.In => IsInList(contextValue, Value),
            RuleOperator.NotIn => !IsInList(contextValue, Value),
            RuleOperator.Contains => contextValue.Contains(Value),
            RuleOperator.StartsWith => contextValue.StartsWith(Value),
            RuleOperator.EndsWith => contextValue.EndsWith(Value),
            RuleOperator.GreaterThan => CompareNumeric(contextValue, Value) > 0,
            RuleOperator.LessThan => CompareNumeric(contextValue, Value) < 0,
            RuleOperator.Regex => System.Text.RegularExpressions.Regex.IsMatch(contextValue, Value),
            _ => false
        };
    }

    private bool IsInList(string value, string listJson)
    {
        try
        {
            var list = System.Text.Json.JsonSerializer.Deserialize<List<string>>(listJson);
            return list?.Contains(value) ?? false;
        }
        catch
        {
            return false;
        }
    }

    private int CompareNumeric(string a, string b)
    {
        if (double.TryParse(a, out var aNum) && double.TryParse(b, out var bNum))
            return aNum.CompareTo(bNum);
        return string.Compare(a, b, StringComparison.Ordinal);
    }
}
```

---

## Variant

Represents a variant for A/B testing.

**File:** `src/HotSwap.FeatureFlags.Domain/Models/Variant.cs`

```csharp
namespace HotSwap.FeatureFlags.Domain.Models;

/// <summary>
/// Represents a variant for A/B testing.
/// </summary>
public class Variant
{
    /// <summary>
    /// Variant name (e.g., "control", "treatment").
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Variant value.
    /// </summary>
    public required string Value { get; set; }

    /// <summary>
    /// Allocation percentage (0-100).
    /// </summary>
    public int Allocation { get; set; } = 0;

    /// <summary>
    /// Variant description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Whether this variant is the control group.
    /// </summary>
    public bool IsControl { get; set; } = false;
}
```

---

## Experiment

Represents an A/B test experiment.

**File:** `src/HotSwap.FeatureFlags.Domain/Models/Experiment.cs`

```csharp
namespace HotSwap.FeatureFlags.Domain.Models;

/// <summary>
/// Represents an A/B test experiment.
/// </summary>
public class Experiment
{
    /// <summary>
    /// Unique experiment identifier (GUID format).
    /// </summary>
    public required string ExperimentId { get; set; }

    /// <summary>
    /// Experiment name.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Hypothesis being tested.
    /// </summary>
    public string? Hypothesis { get; set; }

    /// <summary>
    /// Associated flag name.
    /// </summary>
    public required string FlagName { get; set; }

    /// <summary>
    /// Experiment variants.
    /// </summary>
    public List<Variant> Variants { get; set; } = new();

    /// <summary>
    /// Primary metric to optimize.
    /// </summary>
    public string? PrimaryMetric { get; set; }

    /// <summary>
    /// Secondary metrics to track.
    /// </summary>
    public List<string> SecondaryMetrics { get; set; } = new();

    /// <summary>
    /// Target sample size.
    /// </summary>
    public int SampleSize { get; set; } = 10000;

    /// <summary>
    /// Statistical significance level (e.g., 0.05 for 95% confidence).
    /// </summary>
    public double SignificanceLevel { get; set; } = 0.05;

    /// <summary>
    /// Experiment status.
    /// </summary>
    public ExperimentStatus Status { get; set; } = ExperimentStatus.Draft;

    /// <summary>
    /// Start timestamp (UTC).
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// End timestamp (UTC).
    /// </summary>
    public DateTime? EndedAt { get; set; }

    /// <summary>
    /// Winning variant name (if declared).
    /// </summary>
    public string? WinningVariant { get; set; }

    /// <summary>
    /// Creation timestamp (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Validates the experiment configuration.
    /// </summary>
    public bool IsValid(out List<string> errors)
    {
        errors = new List<string>();

        if (string.IsNullOrWhiteSpace(Name))
            errors.Add("Name is required");

        if (string.IsNullOrWhiteSpace(FlagName))
            errors.Add("FlagName is required");

        if (Variants.Count < 2)
            errors.Add("At least 2 variants are required");

        var totalAllocation = Variants.Sum(v => v.Allocation);
        if (totalAllocation != 100)
            errors.Add($"Total variant allocation must equal 100% (currently {totalAllocation}%)");

        return errors.Count == 0;
    }
}
```

---

## FlagEvaluation

Represents the result of flag evaluation.

**File:** `src/HotSwap.FeatureFlags.Domain/Models/FlagEvaluation.cs`

```csharp
namespace HotSwap.FeatureFlags.Domain.Models;

/// <summary>
/// Represents the result of flag evaluation.
/// </summary>
public class FlagEvaluation
{
    /// <summary>
    /// Flag name.
    /// </summary>
    public required string FlagName { get; set; }

    /// <summary>
    /// Whether the flag is enabled for this context.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Flag value (type-specific).
    /// </summary>
    public required string Value { get; set; }

    /// <summary>
    /// Variant name (for A/B testing).
    /// </summary>
    public string? Variant { get; set; }

    /// <summary>
    /// Reason for this evaluation result (for debugging).
    /// </summary>
    public required string Reason { get; set; }

    /// <summary>
    /// Evaluation timestamp (UTC).
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Whether result came from cache.
    /// </summary>
    public bool FromCache { get; set; } = false;

    /// <summary>
    /// Evaluation context used.
    /// </summary>
    public EvaluationContext? Context { get; set; }
}
```

---

## Enumerations

### FlagType

**File:** `src/HotSwap.FeatureFlags.Domain/Enums/FlagType.cs`

```csharp
namespace HotSwap.FeatureFlags.Domain.Enums;

/// <summary>
/// Represents the value type of a feature flag.
/// </summary>
public enum FlagType
{
    /// <summary>
    /// Boolean flag (true/false).
    /// </summary>
    Boolean,

    /// <summary>
    /// String flag.
    /// </summary>
    String,

    /// <summary>
    /// Numeric flag (integer or decimal).
    /// </summary>
    Number,

    /// <summary>
    /// JSON object flag.
    /// </summary>
    JSON
}
```

### FlagStatus

```csharp
namespace HotSwap.FeatureFlags.Domain.Enums;

/// <summary>
/// Represents the status of a feature flag.
/// </summary>
public enum FlagStatus
{
    /// <summary>
    /// Flag is active and can be evaluated.
    /// </summary>
    Active,

    /// <summary>
    /// Flag is inactive (always returns default value).
    /// </summary>
    Inactive,

    /// <summary>
    /// Flag is archived (soft deleted).
    /// </summary>
    Archived
}
```

### RolloutStrategy

```csharp
namespace HotSwap.FeatureFlags.Domain.Enums;

/// <summary>
/// Represents a rollout strategy.
/// </summary>
public enum RolloutStrategy
{
    /// <summary>
    /// Immediate 100% rollout.
    /// </summary>
    Direct,

    /// <summary>
    /// Multi-stage canary rollout (10% → 30% → 50% → 100%).
    /// </summary>
    Canary,

    /// <summary>
    /// Gradual percentage increase.
    /// </summary>
    Percentage,

    /// <summary>
    /// Target specific user segments.
    /// </summary>
    UserSegment,

    /// <summary>
    /// Scheduled time-based activation.
    /// </summary>
    TimeBased
}
```

### RolloutStatus

```csharp
namespace HotSwap.FeatureFlags.Domain.Enums;

/// <summary>
/// Represents the status of a rollout.
/// </summary>
public enum RolloutStatus
{
    /// <summary>
    /// Rollout created but not started.
    /// </summary>
    Pending,

    /// <summary>
    /// Rollout in progress.
    /// </summary>
    InProgress,

    /// <summary>
    /// Rollout completed successfully.
    /// </summary>
    Completed,

    /// <summary>
    /// Rollout paused (manual intervention).
    /// </summary>
    Paused,

    /// <summary>
    /// Rollout rolled back due to errors.
    /// </summary>
    RolledBack
}
```

### RuleOperator

```csharp
namespace HotSwap.FeatureFlags.Domain.Enums;

/// <summary>
/// Represents a targeting rule operator.
/// </summary>
public enum RuleOperator
{
    /// <summary>
    /// Equality comparison.
    /// </summary>
    Equals,

    /// <summary>
    /// Inequality comparison.
    /// </summary>
    NotEquals,

    /// <summary>
    /// Value is in list.
    /// </summary>
    In,

    /// <summary>
    /// Value is not in list.
    /// </summary>
    NotIn,

    /// <summary>
    /// String contains substring.
    /// </summary>
    Contains,

    /// <summary>
    /// String starts with prefix.
    /// </summary>
    StartsWith,

    /// <summary>
    /// String ends with suffix.
    /// </summary>
    EndsWith,

    /// <summary>
    /// Numeric greater than.
    /// </summary>
    GreaterThan,

    /// <summary>
    /// Numeric less than.
    /// </summary>
    LessThan,

    /// <summary>
    /// Regular expression match.
    /// </summary>
    Regex
}
```

### ExperimentStatus

```csharp
namespace HotSwap.FeatureFlags.Domain.Enums;

/// <summary>
/// Represents the status of an A/B test experiment.
/// </summary>
public enum ExperimentStatus
{
    /// <summary>
    /// Experiment in draft state.
    /// </summary>
    Draft,

    /// <summary>
    /// Experiment is running.
    /// </summary>
    Running,

    /// <summary>
    /// Experiment completed.
    /// </summary>
    Completed,

    /// <summary>
    /// Experiment paused.
    /// </summary>
    Paused,

    /// <summary>
    /// Winner declared.
    /// </summary>
    WinnerDeclared
}
```

---

## Value Objects

### EvaluationContext

**File:** `src/HotSwap.FeatureFlags.Domain/ValueObjects/EvaluationContext.cs`

```csharp
namespace HotSwap.FeatureFlags.Domain.ValueObjects;

/// <summary>
/// Represents the context for flag evaluation.
/// </summary>
public class EvaluationContext
{
    /// <summary>
    /// User identifier.
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// Context attributes (e.g., country, tier, role).
    /// </summary>
    public Dictionary<string, string> Attributes { get; set; } = new();

    /// <summary>
    /// Gets hash code for user bucketing (deterministic).
    /// </summary>
    public int GetBucketHash()
    {
        var key = UserId ?? string.Join(":", Attributes.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}={kv.Value}"));
        return Math.Abs(key.GetHashCode());
    }

    /// <summary>
    /// Gets bucket percentage (0-100) for percentage-based targeting.
    /// </summary>
    public int GetBucketPercentage()
    {
        return GetBucketHash() % 100;
    }
}
```

---

## Validation Examples

### FeatureFlag Validation

```csharp
var flag = new FeatureFlag
{
    Name = "new-checkout-flow",
    DefaultValue = "false",
    Type = FlagType.Boolean,
    CreatedBy = "admin@example.com"
};

if (!flag.IsValid(out var errors))
{
    foreach (var error in errors)
    {
        Console.WriteLine($"Validation Error: {error}");
    }
}
```

### Rollout Validation

```csharp
var rollout = new Rollout
{
    RolloutId = Guid.NewGuid().ToString(),
    FlagName = "new-checkout-flow",
    Strategy = RolloutStrategy.Canary,
    Stages = new List<RolloutStage>
    {
        new() { Percentage = 10, Duration = TimeSpan.FromHours(1) },
        new() { Percentage = 30, Duration = TimeSpan.FromHours(2) },
        new() { Percentage = 100 }
    }
};

if (!rollout.IsValid(out var errors))
{
    Console.WriteLine("Rollout validation failed:");
    errors.ForEach(Console.WriteLine);
}
```

---

**Last Updated:** 2025-11-23
**Namespace:** `HotSwap.FeatureFlags.Domain`
