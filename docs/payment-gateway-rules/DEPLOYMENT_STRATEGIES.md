# Payment Gateway Rule Deployment Strategies

**Version:** 1.0.0
**Last Updated:** 2025-11-23

---

## Overview

This document describes the fraud rule deployment strategies, adapted from the core HotSwap deployment strategies to payment-specific requirements with emphasis on false positive minimization.

---

## Table of Contents

1. [Shadow Mode Strategy](#shadow-mode-strategy)
2. [Canary Deployment](#canary-deployment)
3. [A/B Testing Strategy](#ab-testing-strategy)
4. [Blue/Green Deployment](#bluegreen-deployment)
5. [Emergency Rollback](#emergency-rollback)

---

## Shadow Mode Strategy

### Purpose
Test fraud rules on live transactions without blocking any payments. Zero revenue impact, collect performance data.

### Configuration

```json
{
  "deploymentId": "rule-deploy-001",
  "strategy": "Shadow",
  "ruleId": "velocity-check-v2.0",
  "mode": "Shadow",
  "configuration": {
    "processor": "stripe",
    "duration": "P7D",
    "collectMetrics": true,
    "logAllDecisions": true
  },
  "promotionCriteria": {
    "minEvaluations": 10000,
    "maxFalsePositiveRate": 0.05,
    "minFraudDetectionRate": 0.90,
    "minPrecision": 0.85
  }
}
```

### Execution Flow

```
┌─────────────────────────────────────────────┐
│  1. Deploy Rule in Shadow Mode              │
│     - No transaction blocking               │
│     - Evaluate on 100% of traffic           │
│     - Log all decisions                     │
└──────────────┬──────────────────────────────┘
               │
               ▼
┌─────────────────────────────────────────────┐
│  2. Evaluate Every Transaction              │
│     - Run rule logic                        │
│     - Calculate "would block/allow"         │
│     - Assign risk score                     │
└──────────────┬──────────────────────────────┘
               │
               ▼
┌─────────────────────────────────────────────┐
│  3. Collect Performance Metrics             │
│     - Total evaluations                     │
│     - Block vs Allow decisions              │
│     - Evaluation latency                    │
│     - False positive estimates              │
└──────────────┬──────────────────────────────┘
               │
               ▼
┌─────────────────────────────────────────────┐
│  4. Analyze Against Actual Fraud            │
│     - Compare with chargeback data          │
│     - Calculate precision/recall            │
│     - Estimate false positive rate          │
└──────────────┬──────────────────────────────┘
               │
               ▼
┌─────────────────────────────────────────────┐
│  5. Generate Shadow Mode Report             │
│     - Performance summary                   │
│     - Recommended thresholds                │
│     - Promotion decision                    │
└──────────────┬──────────────────────────────┘
               │
               ▼
┌─────────────────────────────────────────────┐
│  6. Promote to Active (Canary)              │
│     - Start with 1% of traffic              │
└─────────────────────────────────────────────┘
```

### Success Criteria

- **Min Evaluations: 10,000** - Sufficient sample size
- **False Positive Rate < 5%** - Acceptable FP rate
- **Fraud Detection > 90%** - High fraud catch rate
- **Precision > 85%** - Blocks are accurate
- **Min 7 Days** - Covers weekly patterns

---

## Canary Deployment

### Purpose
Gradually apply fraud rule to increasing traffic percentages with automatic rollback on high false positives.

### Configuration

```json
{
  "deploymentId": "rule-deploy-002",
  "strategy": "Canary",
  "ruleId": "velocity-check-v2.0",
  "mode": "Active",
  "configuration": {
    "processor": "stripe",
    "stages": [
      {
        "name": "Micro",
        "percentage": 0.01,
        "minDuration": "PT1H",
        "maxFalsePositives": 5
      },
      {
        "name": "Small",
        "percentage": 0.10,
        "minDuration": "PT4H",
        "maxFalsePositives": 50
      },
      {
        "name": "Medium",
        "percentage": 0.25,
        "minDuration": "PT8H",
        "maxFalsePositives": 125
      },
      {
        "name": "Large",
        "percentage": 0.50,
        "minDuration": "P1D",
        "maxFalsePositives": 250
      },
      {
        "name": "Full",
        "percentage": 1.00,
        "minDuration": "P2D",
        "maxFalsePositives": 500
      }
    ],
    "autoProgress": true,
    "rollbackThresholds": {
      "maxFalsePositiveRate": 0.05,
      "maxRevenueImpact": 5000,
      "maxCustomerComplaints": 10
    }
  }
}
```

### Execution Flow

```
Shadow Mode (100% observe, 0% block)
         │
         ▼
Micro: 1% traffic ──── Monitor 1 hour ──── Validate
         │                                     │
         │                                     ├─ FP rate < 5%?
         │                                     ├─ Revenue impact OK?
         │                                     └─ Duration met?
         ▼
Small: 10% traffic ─── Monitor 4 hours ─── Validate
         │
         ▼
Medium: 25% traffic ── Monitor 8 hours ─── Validate
         │
         ▼
Large: 50% traffic ─── Monitor 1 day ──── Validate
         │
         ▼
Full: 100% traffic ──── Monitor 2 days ─── Complete
```

### Automatic Progression Logic

```csharp
public async Task<bool> ShouldProgressToNextStage(
    CanaryStage currentStage,
    RulePerformanceMetrics metrics)
{
    // Check minimum duration
    if (DateTime.UtcNow - currentStage.StartTime < currentStage.MinDuration)
        return false;

    // Check false positive rate
    var fpRate = metrics.FalsePositives.ConfirmedRate;
    if (fpRate > _config.RollbackThresholds.MaxFalsePositiveRate)
        return false;

    // Check absolute false positive count
    if (metrics.FalsePositives.ConfirmedCount > currentStage.MaxFalsePositives)
        return false;

    // Check revenue impact
    var revenueBlocked = metrics.Traffic.RevenueBlocked;
    if (revenueBlocked > _config.RollbackThresholds.MaxRevenueImpact)
        return false;

    // Check customer complaints
    var complaints = await _supportService.GetComplaintCountAsync(
        currentStage.StartTime,
        "TransactionBlocked");

    if (complaints > _config.RollbackThresholds.MaxCustomerComplaints)
        return false;

    return true;
}
```

---

## A/B Testing Strategy

### Purpose
Compare two rule variants side-by-side to determine which performs better.

### Configuration

```json
{
  "deploymentId": "rule-deploy-003",
  "strategy": "ABTest",
  "configuration": {
    "variantA": {
      "ruleId": "velocity-check-v2.0",
      "percentage": 0.50
    },
    "variantB": {
      "ruleId": "velocity-check-v2.1",
      "percentage": 0.50
    },
    "duration": "P14D",
    "successMetric": "FraudDetectionRate",
    "minimumSampleSize": 50000
  }
}
```

### Execution Flow

```
┌─────────────────────────────────────────────┐
│  Traffic Splitter (50/50)                   │
└──────────────┬─────────────┬────────────────┘
               │             │
               ▼             ▼
     ┌─────────────┐   ┌─────────────┐
     │  Variant A  │   │  Variant B  │
     │  (v2.0)     │   │  (v2.1)     │
     └──────┬──────┘   └──────┬──────┘
            │                 │
            ▼                 ▼
     ┌─────────────┐   ┌─────────────┐
     │  Metrics A  │   │  Metrics B  │
     │  FP: 3.2%   │   │  FP: 2.8%   │
     │  Fraud: 92% │   │  Fraud: 93% │
     └─────────────┘   └─────────────┘
                  │
                  ▼
          ┌───────────────┐
          │ Compare & Win │
          │  Variant B    │
          └───────────────┘
```

### Winner Selection

```csharp
public async Task<string> SelectWinningVariant(
    ABTestConfiguration config,
    Dictionary<string, RulePerformanceMetrics> variantMetrics)
{
    var variantA = variantMetrics["variantA"];
    var variantB = variantMetrics["variantB"];

    // Statistical significance test
    var isSignificant = PerformChiSquareTest(
        variantA.Evaluation.TotalEvaluations,
        variantB.Evaluation.TotalEvaluations,
        variantA.FraudDetection.EstimatedFraudBlocked,
        variantB.FraudDetection.EstimatedFraudBlocked);

    if (!isSignificant)
        return null; // Not enough data

    // Compare success metric
    if (config.SuccessMetric == "FraudDetectionRate")
    {
        return variantA.FraudDetection.EstimatedFraudRate >
               variantB.FraudDetection.EstimatedFraudRate
            ? "variantA"
            : "variantB";
    }

    // Compare false positive rate (lower is better)
    return variantA.FalsePositives.ConfirmedRate <
           variantB.FalsePositives.ConfirmedRate
        ? "variantA"
        : "variantB";
}
```

---

## Blue/Green Deployment

### Purpose
Instant rule set cutover with quick rollback capability.

### Configuration

```json
{
  "deploymentId": "rule-deploy-004",
  "strategy": "BlueGreen",
  "configuration": {
    "blueRuleSet": ["velocity-v1", "amount-v1", "geo-v1"],
    "greenRuleSet": ["velocity-v2", "amount-v2", "geo-v2"],
    "warmupDuration": "PT30S",
    "cutoverType": "Instant",
    "monitoringDuration": "PT10M",
    "autoRollback": true
  }
}
```

---

## Emergency Rollback

### Purpose
Instant rule deactivation on critical false positive threshold breach.

### Triggers

1. **False Positive Rate Spike**
   - FP rate exceeds threshold
   - Immediate traffic reduction to 0%

2. **Revenue Impact Threshold**
   - Blocked legitimate transactions exceed limit
   - Rollback to previous rule version

3. **Customer Complaint Surge**
   - Complaint rate exceeds baseline
   - Alert fraud team + reduce traffic

4. **Manual Intervention**
   - Fraud analyst forces rollback
   - Risk manager override

### Rollback Execution

```csharp
public async Task ExecuteEmergencyRollback(
    string ruleId,
    RollbackReason reason)
{
    var stopwatch = Stopwatch.StartNew();

    // 1. Set traffic allocation to 0%
    await _ruleService.SetTrafficAllocationAsync(ruleId, 0m);

    // 2. Switch to previous rule version (if available)
    var previousVersion = await _ruleService.GetPreviousVersionAsync(ruleId);
    if (previousVersion != null)
    {
        await _ruleService.ActivateAsync(previousVersion);
    }

    // 3. Release any transactions in review queue
    await _transactionService.ReleaseReviewQueueAsync(ruleId);

    // 4. Log rollback event
    await _auditService.LogRollbackAsync(new RollbackEvent
    {
        RuleId = ruleId,
        Reason = reason,
        ExecutionTime = stopwatch.Elapsed,
        Timestamp = DateTime.UtcNow
    });

    // 5. Send alerts
    await _alertService.SendCriticalAlertAsync(
        $"Rule rollback executed: {ruleId} - {reason}");

    stopwatch.Stop();
}
```

---

**Last Updated:** 2025-11-23
**Version:** 1.0.0
