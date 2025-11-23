# Payment Gateway Rule Manager Domain Models Reference

**Version:** 1.0.0
**Namespace:** `HotSwap.Distributed.Payment.Domain.Models`

---

## Table of Contents

1. [FraudRule](#fraudrule)
2. [Transaction](#transaction)
3. [RuleEvaluation](#ruleevaluation)
4. [FalsePositive](#falsepositive)

---

## FraudRule

Represents a fraud detection rule.

**File:** `src/HotSwap.Distributed.Payment.Domain/Models/FraudRule.cs`

```csharp
namespace HotSwap.Distributed.Payment.Domain.Models;

/// <summary>
/// Represents a fraud detection rule.
/// </summary>
public class FraudRule
{
    /// <summary>
    /// Unique rule identifier (e.g., "velocity-check-v2.0").
    /// </summary>
    public required string RuleId { get; set; }

    /// <summary>
    /// Rule type.
    /// </summary>
    public RuleType Type { get; set; }

    /// <summary>
    /// Deployment mode.
    /// </summary>
    public RuleMode Mode { get; set; } = RuleMode.Shadow;

    /// <summary>
    /// Payment processor this rule applies to.
    /// </summary>
    public required string Processor { get; set; }

    /// <summary>
    /// Rule configuration (JSON).
    /// </summary>
    public required Dictionary<string, object> Configuration { get; set; }

    /// <summary>
    /// Action to take when rule triggers.
    /// </summary>
    public RuleAction Action { get; set; } = RuleAction.Block;

    /// <summary>
    /// Risk score assigned by this rule (0-100).
    /// </summary>
    public int RiskScore { get; set; }

    /// <summary>
    /// Rule evaluation priority (higher = evaluated first).
    /// </summary>
    public int Priority { get; set; } = 100;

    /// <summary>
    /// Current traffic allocation percentage (0.0 to 1.0).
    /// </summary>
    public decimal TrafficPercentage { get; set; } = 0m;

    /// <summary>
    /// Rule status.
    /// </summary>
    public RuleStatus Status { get; set; } = RuleStatus.Pending;

    /// <summary>
    /// Deployment timestamp (UTC).
    /// </summary>
    public DateTime DeployedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Validates the rule configuration.
    /// </summary>
    public bool IsValid(out List<string> errors)
    {
        errors = new List<string>();

        if (string.IsNullOrWhiteSpace(RuleId))
            errors.Add("RuleId is required");

        if (string.IsNullOrWhiteSpace(Processor))
            errors.Add("Processor is required");

        if (RiskScore < 0 || RiskScore > 100)
            errors.Add("RiskScore must be between 0 and 100");

        if (TrafficPercentage < 0 || TrafficPercentage > 1)
            errors.Add("TrafficPercentage must be between 0 and 1");

        if (Configuration == null || Configuration.Count == 0)
            errors.Add("Configuration is required");

        return errors.Count == 0;
    }
}

public enum RuleType
{
    Velocity,
    Amount,
    Geolocation,
    DeviceFingerprint,
    Behavioral
}

public enum RuleMode
{
    Shadow,    // Observe only, don't block
    Active,    // Block transactions
    Disabled   // Inactive
}

public enum RuleAction
{
    Allow,
    Block,
    Review,    // Flag for manual review
    Challenge  // Request 3DS/2FA
}

public enum RuleStatus
{
    Pending,
    Deploying,
    Active,
    Disabled,
    RolledBack,
    Failed
}
```

---

## Transaction

Represents a payment transaction being evaluated.

```csharp
namespace HotSwap.Distributed.Payment.Domain.Models;

/// <summary>
/// Represents a payment transaction.
/// </summary>
public class Transaction
{
    /// <summary>
    /// Unique transaction identifier.
    /// </summary>
    public required string TransactionId { get; set; }

    /// <summary>
    /// Customer identifier.
    /// </summary>
    public required string CustomerId { get; set; }

    /// <summary>
    /// Transaction amount.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Currency code (ISO 4217).
    /// </summary>
    public required string Currency { get; set; }

    /// <summary>
    /// Payment method details.
    /// </summary>
    public required PaymentMethod PaymentMethod { get; set; }

    /// <summary>
    /// Billing address.
    /// </summary>
    public required Address BillingAddress { get; set; }

    /// <summary>
    /// Device fingerprint data.
    /// </summary>
    public DeviceFingerprint? DeviceFingerprint { get; set; }

    /// <summary>
    /// Transaction timestamp (UTC).
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Final fraud decision.
    /// </summary>
    public FraudDecision Decision { get; set; }

    /// <summary>
    /// Aggregated risk score (0-100).
    /// </summary>
    public int RiskScore { get; set; }

    /// <summary>
    /// Whether this was identified as a false positive.
    /// </summary>
    public bool IsFalsePositive { get; set; } = false;
}

public class PaymentMethod
{
    public required string Type { get; set; } // "card", "bank_account", etc.
    public string? Last4 { get; set; }
    public string? Brand { get; set; }
}

public class Address
{
    public string? Street { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public required string Country { get; set; }
    public string? ZipCode { get; set; }
}

public class DeviceFingerprint
{
    public required string DeviceId { get; set; }
    public required string IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public Geolocation? Geolocation { get; set; }
}

public class Geolocation
{
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
}

public enum FraudDecision
{
    Pending,
    Allow,
    Block,
    Review,
    Challenge
}
```

---

## RuleEvaluation

Represents the result of evaluating a rule against a transaction.

```csharp
namespace HotSwap.Distributed.Payment.Domain.Models;

/// <summary>
/// Result of evaluating a fraud rule against a transaction.
/// </summary>
public class RuleEvaluation
{
    /// <summary>
    /// Unique evaluation identifier.
    /// </summary>
    public required string EvaluationId { get; set; }

    /// <summary>
    /// Transaction being evaluated.
    /// </summary>
    public required string TransactionId { get; set; }

    /// <summary>
    /// Rule being evaluated.
    /// </summary>
    public required string RuleId { get; set; }

    /// <summary>
    /// Evaluation result.
    /// </summary>
    public RuleAction Result { get; set; }

    /// <summary>
    /// Risk score contribution from this rule (0-100).
    /// </summary>
    public int RiskScore { get; set; }

    /// <summary>
    /// Evaluation reason/explanation.
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// Whether rule was in shadow mode.
    /// </summary>
    public bool IsShadowMode { get; set; }

    /// <summary>
    /// Rule evaluation latency (milliseconds).
    /// </summary>
    public double LatencyMs { get; set; }

    /// <summary>
    /// Evaluation timestamp (UTC).
    /// </summary>
    public DateTime EvaluatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Additional metadata.
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}
```

---

## FalsePositive

Represents a detected false positive.

```csharp
namespace HotSwap.Distributed.Payment.Domain.Models;

/// <summary>
/// Represents a false positive detection.
/// </summary>
public class FalsePositive
{
    /// <summary>
    /// Unique false positive identifier.
    /// </summary>
    public required string FalsePositiveId { get; set; }

    /// <summary>
    /// Blocked transaction that was a false positive.
    /// </summary>
    public required string TransactionId { get; set; }

    /// <summary>
    /// Rule that caused the false positive.
    /// </summary>
    public required string RuleId { get; set; }

    /// <summary>
    /// How the false positive was detected.
    /// </summary>
    public FalsePositiveSource Source { get; set; }

    /// <summary>
    /// Confidence score (0.0 to 1.0).
    /// </summary>
    public decimal Confidence { get; set; }

    /// <summary>
    /// Customer impact (revenue blocked).
    /// </summary>
    public decimal RevenueImpact { get; set; }

    /// <summary>
    /// Detection timestamp (UTC).
    /// </summary>
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Whether rule was adjusted in response.
    /// </summary>
    public bool RuleAdjusted { get; set; } = false;

    /// <summary>
    /// Action taken.
    /// </summary>
    public string? ActionTaken { get; set; }
}

public enum FalsePositiveSource
{
    CustomerComplaint,
    ManualReview,
    HistoricalPattern,
    ChargebackAnalysis,
    MLPrediction
}
```

---

## RulePerformanceMetrics

Performance metrics for a fraud rule.

```csharp
namespace HotSwap.Distributed.Payment.Domain.Models;

/// <summary>
/// Performance metrics for a fraud rule.
/// </summary>
public class RulePerformanceMetrics
{
    /// <summary>
    /// Rule identifier.
    /// </summary>
    public required string RuleId { get; set; }

    /// <summary>
    /// Evaluation metrics.
    /// </summary>
    public EvaluationMetrics Evaluation { get; set; } = new();

    /// <summary>
    /// False positive metrics.
    /// </summary>
    public FalsePositiveMetrics FalsePositives { get; set; } = new();

    /// <summary>
    /// Fraud detection metrics.
    /// </summary>
    public FraudDetectionMetrics FraudDetection { get; set; } = new();

    /// <summary>
    /// Traffic metrics.
    /// </summary>
    public TrafficMetrics Traffic { get; set; } = new();

    /// <summary>
    /// Metrics timestamp (UTC).
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class EvaluationMetrics
{
    public long TotalEvaluations { get; set; }
    public long BlockDecisions { get; set; }
    public long AllowDecisions { get; set; }
    public double AvgLatency { get; set; }
    public double P99Latency { get; set; }
}

public class FalsePositiveMetrics
{
    public int EstimatedCount { get; set; }
    public decimal EstimatedRate { get; set; }
    public int ConfirmedCount { get; set; }
    public decimal ConfirmedRate { get; set; }
}

public class FraudDetectionMetrics
{
    public int EstimatedFraudBlocked { get; set; }
    public decimal EstimatedFraudRate { get; set; }
    public decimal Precision { get; set; }
    public decimal Recall { get; set; }
}

public class TrafficMetrics
{
    public decimal Percentage { get; set; }
    public long TransactionCount { get; set; }
    public decimal RevenueProtected { get; set; }
    public decimal RevenueBlocked { get; set; }
}
```

---

**Last Updated:** 2025-11-23
**Version:** 1.0.0
