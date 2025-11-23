# Rollout Strategies Guide

**Version:** 1.0.0
**Last Updated:** 2025-11-23

---

## Overview

The HotSwap Feature Flag Orchestrator supports 5 rollout strategies, each adapted from the existing deployment strategies in the kernel orchestration platform. Rollout strategies determine how feature flags are progressively enabled for users.

### Strategy Selection

| Flag Type | Default Strategy | Use Case |
|-----------|------------------|----------|
| Boolean | Canary | Safe progressive rollout |
| Operational Kill Switch | Direct | Immediate enable/disable |
| Experimental Feature | UserSegment | Beta testing |
| Scheduled Launch | TimeBased | Coordinated releases |
| Gradual Rollout | Percentage | Linear increase over time |

---

## 1. Direct Rollout Strategy

### Overview

**Based On:** Direct Deployment Strategy
**Pattern:** Immediate activation
**Risk:** High
**Complexity:** Low

**Use Case:** Emergency kill switches, low-risk features, internal tools.

### Behavior

- Enables flag for **100% of users** immediately
- No gradual rollout
- Instant rollback if needed
- Fastest deployment path

### Algorithm

```csharp
public async Task<RolloutResult> RolloutAsync(FeatureFlag flag, EvaluationContext context)
{
    // Direct rollout - all users get the flag
    return new RolloutResult
    {
        Enabled = true,
        Value = flag.DefaultValue,
        Reason = "Direct rollout: 100% enabled"
    };
}
```

### Flag Evaluation Flow

```
Request → Evaluate Flag → Enabled for ALL users (100%)
```

### Configuration

```json
{
  "flagName": "kill-switch-payment-gateway",
  "strategy": "Direct",
  "enabled": true
}
```

### Performance Characteristics

- **Evaluation Latency:** ~0.5ms (fastest)
- **Rollout Time:** Immediate (< 5 seconds global propagation)
- **Rollback Time:** ~10 seconds
- **Risk:** High (affects all users immediately)

### When to Use

✅ **Good For:**
- Emergency kill switches
- Operational toggles
- Internal tools (no external users)
- Low-risk configuration changes
- Development/testing environments

❌ **Not Good For:**
- High-risk features
- User-facing changes
- Production deployments without testing
- Features requiring gradual validation

### Example: Emergency Kill Switch

```csharp
// Disable payment gateway immediately due to security issue
var flag = new FeatureFlag
{
    Name = "payment-gateway-enabled",
    Type = FlagType.Boolean,
    DefaultValue = "false", // Disable immediately
    Environment = "Production"
};

var rollout = new Rollout
{
    Strategy = RolloutStrategy.Direct,
    Enabled = false // Kill switch activated
};

// Result: Payment gateway disabled for ALL users within 5 seconds
```

---

## 2. Canary Rollout Strategy

### Overview

**Based On:** Canary Deployment Strategy
**Pattern:** Multi-stage progressive rollout
**Risk:** Low
**Complexity:** Medium

**Use Case:** High-risk features, major changes, production deployments.

### Behavior

- Start with small percentage (10%)
- Monitor metrics for configured duration
- Auto-progress to next stage if healthy
- Continue until 100% or rollback on error
- Deterministic user bucketing

### Algorithm

```csharp
public class CanaryRolloutStrategy : IRolloutStrategy
{
    public async Task<RolloutResult> RolloutAsync(FeatureFlag flag, EvaluationContext context)
    {
        var rollout = flag.ActiveRollout;
        var currentStage = rollout.GetCurrentStage();

        if (currentStage == null)
            return DefaultValue(flag);

        // Deterministic user bucketing (0-99)
        var userBucket = context.GetBucketPercentage();

        // User in rollout percentage?
        if (userBucket < currentStage.Percentage)
        {
            return new RolloutResult
            {
                Enabled = true,
                Value = flag.DefaultValue,
                Reason = $"Canary rollout: user in {currentStage.Percentage}% bucket"
            };
        }

        return DefaultValue(flag);
    }
}
```

### Rollout Flow

```
Stage 0: 10% → Monitor (1h) → Health Check
          ↓ Healthy
Stage 1: 30% → Monitor (2h) → Health Check
          ↓ Healthy
Stage 2: 50% → Monitor (4h) → Health Check
          ↓ Healthy
Stage 3: 100% → Complete

At any stage:
  ↓ Error Detected (error rate > threshold)
  Rollback to 0%
```

### Configuration

```json
{
  "strategy": "Canary",
  "stages": [
    {"percentage": 10, "duration": "PT1H", "healthCheck": true},
    {"percentage": 30, "duration": "PT2H", "healthCheck": true},
    {"percentage": 50, "duration": "PT4H", "healthCheck": true},
    {"percentage": 100, "duration": null}
  ],
  "rollbackOnError": true,
  "thresholds": {
    "errorRateThreshold": 0.05,
    "latencyP99Threshold": 100
  }
}
```

### Health Check Logic

```csharp
public async Task<bool> IsHealthyAsync(Rollout rollout)
{
    var metrics = await _metricsService.GetMetricsAsync(rollout.FlagName);

    // Check error rate increase
    if (metrics.ErrorRate > rollout.Thresholds.ErrorRateThreshold)
    {
        await RollbackAsync(rollout, "Error rate exceeded threshold");
        return false;
    }

    // Check latency increase
    if (metrics.LatencyP99 > rollout.Thresholds.LatencyP99Threshold)
    {
        await RollbackAsync(rollout, "Latency exceeded threshold");
        return false;
    }

    return true;
}
```

### User Bucketing (Deterministic)

```csharp
public int GetBucketPercentage(EvaluationContext context)
{
    var key = context.UserId ?? string.Join(":", context.Attributes.OrderBy(kv => kv.Key));
    var hash = Math.Abs(key.GetHashCode());
    return hash % 100; // 0-99
}
```

**Important:** Same user always gets same bucket (deterministic hashing).

### Performance Characteristics

- **Evaluation Latency:** ~1ms (cached)
- **Stage Transition:** Automatic after duration + health check
- **Rollback Time:** ~10 seconds (instant)
- **Risk:** Low (gradual exposure, automatic rollback)

### When to Use

✅ **Good For:**
- High-risk features (checkout flow, payment processing)
- Major UI changes
- Performance-sensitive features
- Production deployments
- Features with clear success metrics

❌ **Not Good For:**
- Emergency changes (use Direct)
- Features without metrics
- Internal tools (use Direct)
- Time-sensitive launches (use TimeBased)

### Example: New Checkout Flow

```csharp
var rollout = new Rollout
{
    FlagName = "new-checkout-flow",
    Strategy = RolloutStrategy.Canary,
    Stages = new List<RolloutStage>
    {
        new() { Percentage = 10, Duration = TimeSpan.FromHours(1) },
        new() { Percentage = 30, Duration = TimeSpan.FromHours(2) },
        new() { Percentage = 50, Duration = TimeSpan.FromHours(4) },
        new() { Percentage = 100 }
    },
    Thresholds = new MetricsThresholds
    {
        ErrorRateThreshold = 0.05, // 5% error rate max
        LatencyP99Threshold = 100   // 100ms p99 latency max
    }
};

// Timeline:
// T+0h: 10% of users see new checkout
// T+1h: Health check passes → 30% of users
// T+3h: Health check passes → 50% of users
// T+7h: Health check passes → 100% of users
```

---

## 3. Percentage Rollout Strategy

### Overview

**Based On:** Rolling Deployment Strategy
**Pattern:** Linear or exponential growth
**Risk:** Medium
**Complexity:** Low

**Use Case:** Gradual exposure increase without discrete stages.

### Behavior

- Increase percentage gradually over time
- Linear or exponential growth curve
- No health checks (continuous rollout)
- User bucketing by hash

### Algorithm

```csharp
public class PercentageRolloutStrategy : IRolloutStrategy
{
    public async Task<RolloutResult> RolloutAsync(FeatureFlag flag, EvaluationContext context)
    {
        var rollout = flag.ActiveRollout;
        var elapsedTime = DateTime.UtcNow - rollout.StartedAt.Value;
        var totalDuration = rollout.Duration;

        // Calculate current percentage based on curve
        var currentPercentage = CalculatePercentage(
            rollout.StartPercentage,
            rollout.EndPercentage,
            elapsedTime,
            totalDuration,
            rollout.Curve
        );

        var userBucket = context.GetBucketPercentage();

        if (userBucket < currentPercentage)
        {
            return new RolloutResult
            {
                Enabled = true,
                Value = flag.DefaultValue,
                Reason = $"Percentage rollout: {currentPercentage}%"
            };
        }

        return DefaultValue(flag);
    }

    private int CalculatePercentage(int start, int end, TimeSpan elapsed, TimeSpan total, string curve)
    {
        var progress = Math.Min(1.0, elapsed.TotalSeconds / total.TotalSeconds);

        return curve switch
        {
            "linear" => (int)(start + (end - start) * progress),
            "exponential" => (int)(start + (end - start) * Math.Pow(progress, 2)),
            _ => start
        };
    }
}
```

### Rollout Flow (Linear)

```
Day 0: 0%
Day 1: 14%
Day 2: 28%
Day 3: 42%
Day 4: 57%
Day 5: 71%
Day 6: 85%
Day 7: 100%
```

### Configuration

```json
{
  "strategy": "Percentage",
  "startPercentage": 0,
  "endPercentage": 100,
  "duration": "P7D",
  "curve": "linear"
}
```

### Growth Curves

**Linear:**
```
Percentage = start + (end - start) * (elapsed / total)
```

**Exponential:**
```
Percentage = start + (end - start) * (elapsed / total)²
```

**Visual:**
```
Linear:         Exponential:
100% |    /     100% |      ╱
 75% |   /       75% |     ╱
 50% |  /        50% |    ╱
 25% | /         25% |   ╱
  0% |/           0% |__╱
     0  7days        0  7days
```

### Performance Characteristics

- **Evaluation Latency:** ~1ms (cached)
- **Rollout Duration:** Configurable (hours to weeks)
- **Risk:** Medium (no automatic rollback)
- **Predictability:** High (smooth growth)

### When to Use

✅ **Good For:**
- Long-term rollouts (weeks)
- Low-risk features
- Gradual user exposure
- Features without clear health metrics
- Marketing campaigns

❌ **Not Good For:**
- High-risk features (use Canary)
- Features needing validation per stage
- Emergency rollouts (use Direct)

### Example: Dark Mode Feature

```csharp
var rollout = new Rollout
{
    FlagName = "dark-mode",
    Strategy = RolloutStrategy.Percentage,
    StartPercentage = 0,
    EndPercentage = 100,
    Duration = TimeSpan.FromDays(14), // 2 weeks
    Curve = "linear"
};

// Result: 7% increase per day for 14 days
```

---

## 4. User Segment Rollout Strategy

### Overview

**Based On:** Blue-Green Deployment (environment selection)
**Pattern:** Target-based activation
**Risk:** Low
**Complexity:** High

**Use Case:** Beta testing, premium features, regional rollouts.

### Behavior

- Enable for specific user segments
- Segment defined by attributes
- Multiple segments supported
- Priority-based evaluation

### Algorithm

```csharp
public class UserSegmentRolloutStrategy : IRolloutStrategy
{
    public async Task<RolloutResult> RolloutAsync(FeatureFlag flag, EvaluationContext context)
    {
        // Evaluate targets in priority order
        var matchingTarget = flag.Targets
            .Where(t => t.IsActive)
            .OrderBy(t => t.Priority)
            .FirstOrDefault(t => t.Matches(context));

        if (matchingTarget != null)
        {
            return new RolloutResult
            {
                Enabled = true,
                Value = matchingTarget.Value,
                Reason = $"User segment: {matchingTarget.Name}"
            };
        }

        return DefaultValue(flag);
    }
}
```

### Segment Evaluation Flow

```
Request → Evaluate Targets (priority order)
  ├─ Target 1 (priority 0): Beta Testers → Match? Yes → Enable
  ├─ Target 2 (priority 1): Premium Users → (not evaluated)
  └─ No match → Default Value
```

### Configuration

```json
{
  "strategy": "UserSegment",
  "targets": [
    {
      "name": "Beta Testers",
      "priority": 0,
      "rules": [
        {
          "attribute": "role",
          "operator": "Equals",
          "value": "beta-tester"
        }
      ],
      "value": "true"
    },
    {
      "name": "Premium Users",
      "priority": 1,
      "rules": [
        {
          "attribute": "tier",
          "operator": "In",
          "value": "[\"premium\", \"enterprise\"]"
        }
      ],
      "value": "true"
    },
    {
      "name": "US Users",
      "priority": 2,
      "rules": [
        {
          "attribute": "country",
          "operator": "Equals",
          "value": "US"
        }
      ],
      "value": "true"
    }
  ]
}
```

### Rule Operators

**Equality:**
- `Equals` - Exact match
- `NotEquals` - Not equal

**List:**
- `In` - Value in list
- `NotIn` - Value not in list

**String:**
- `Contains` - Substring match
- `StartsWith` - Prefix match
- `EndsWith` - Suffix match

**Numeric:**
- `GreaterThan` - Numeric >
- `LessThan` - Numeric <

**Pattern:**
- `Regex` - Regular expression match

### Performance Characteristics

- **Evaluation Latency:** ~5ms (rule evaluation)
- **Rollout Time:** Immediate
- **Risk:** Low (targeted users)
- **Flexibility:** High (complex targeting)

### When to Use

✅ **Good For:**
- Beta testing programs
- Premium/paid features
- Regional rollouts
- Internal testing
- Whitelisted users
- A/B testing cohorts

❌ **Not Good For:**
- Gradual percentage rollouts (use Percentage)
- Features for all users (use Direct)
- Time-based launches (use TimeBased)

### Example: Premium Feature

```csharp
var target = new Target
{
    Name = "Premium Users",
    Priority = 0,
    Rules = new List<TargetRule>
    {
        new()
        {
            Attribute = "tier",
            Operator = RuleOperator.In,
            Value = "[\"premium\", \"enterprise\"]"
        },
        new()
        {
            Attribute = "account_age_days",
            Operator = RuleOperator.GreaterThan,
            Value = "30"
        }
    },
    Value = "true"
};

// Result: Only premium/enterprise users with account > 30 days
```

---

## 5. Time-Based Rollout Strategy

### Overview

**Based On:** Scheduled Deployment
**Pattern:** Scheduled activation
**Risk:** Low
**Complexity:** Low

**Use Case:** Coordinated launches, time-limited promotions, scheduled events.

### Behavior

- Enable at specific start time
- Disable at end time (optional)
- Timezone-aware
- Automatic activation/deactivation

### Algorithm

```csharp
public class TimeBasedRolloutStrategy : IRolloutStrategy
{
    public async Task<RolloutResult> RolloutAsync(FeatureFlag flag, EvaluationContext context)
    {
        var rollout = flag.ActiveRollout;
        var now = DateTime.UtcNow;

        // Check if within time window
        var isActive = now >= rollout.StartTime &&
                      (rollout.EndTime == null || now < rollout.EndTime);

        if (isActive)
        {
            return new RolloutResult
            {
                Enabled = true,
                Value = flag.DefaultValue,
                Reason = $"Time-based rollout: active until {rollout.EndTime}"
            };
        }

        return new RolloutResult
        {
            Enabled = false,
            Value = flag.DefaultValue,
            Reason = "Time-based rollout: outside time window"
        };
    }
}
```

### Rollout Flow

```
Timeline:
   Before Start: Disabled for ALL users
   ↓
   Start Time: Enabled for ALL users
   ↓
   Active Period: Enabled for ALL users
   ↓
   End Time: Disabled for ALL users
```

### Configuration

```json
{
  "strategy": "TimeBased",
  "schedule": {
    "startTime": "2025-12-01T00:00:00Z",
    "endTime": "2025-12-31T23:59:59Z",
    "timezone": "America/New_York"
  }
}
```

### Timezone Handling

```csharp
public DateTime ConvertToUtc(DateTime localTime, string timezone)
{
    var timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById(timezone);
    return TimeZoneInfo.ConvertTimeToUtc(localTime, timeZoneInfo);
}
```

### Performance Characteristics

- **Evaluation Latency:** ~0.5ms (time comparison)
- **Rollout Time:** Exact (to the second)
- **Risk:** Low (predictable activation)
- **Precision:** Second-level accuracy

### When to Use

✅ **Good For:**
- Holiday promotions
- Limited-time features
- Coordinated launches (product releases)
- Event-driven features (Black Friday sales)
- Scheduled maintenance windows
- Time zone specific launches

❌ **Not Good For:**
- Gradual rollouts (use Percentage or Canary)
- Permanent features (use Direct)
- User-targeted features (use UserSegment)

### Example: Holiday Promotion

```csharp
var rollout = new Rollout
{
    FlagName = "black-friday-sale",
    Strategy = RolloutStrategy.TimeBased,
    StartTime = new DateTime(2025, 11, 29, 0, 0, 0, DateTimeKind.Utc),
    EndTime = new DateTime(2025, 12, 2, 23, 59, 59, DateTimeKind.Utc),
    Timezone = "America/New_York"
};

// Result: Sale active from Nov 29 to Dec 2 (EST)
```

---

## Strategy Comparison

| Strategy | Risk | Complexity | Duration | Auto-Rollback | Use Case |
|----------|------|------------|----------|---------------|----------|
| Direct | ⚠️ High | ⭐ Low | Immediate | ❌ No | Kill switches |
| Canary | ✅ Low | ⭐⭐ Medium | Hours-Days | ✅ Yes | Production features |
| Percentage | ⭐ Medium | ⭐ Low | Days-Weeks | ❌ No | Gradual rollouts |
| UserSegment | ✅ Low | ⭐⭐⭐ High | Immediate | ❌ No | Beta testing |
| TimeBased | ✅ Low | ⭐ Low | Scheduled | ❌ No | Promotions |

---

## Combined Strategies

You can combine strategies for complex scenarios:

### Example: Canary + UserSegment

```json
{
  "flagName": "new-checkout-flow",
  "targets": [
    {
      "name": "Internal Users (100%)",
      "priority": 0,
      "rules": [{"attribute": "email", "operator": "EndsWith", "value": "@company.com"}],
      "value": "true"
    }
  ],
  "rollout": {
    "strategy": "Canary",
    "stages": [
      {"percentage": 10, "duration": "PT1H"},
      {"percentage": 30, "duration": "PT2H"},
      {"percentage": 100}
    ]
  }
}
```

**Evaluation Logic:**
1. Check targets first (internal users → 100%)
2. If no target match, apply canary rollout
3. If outside canary percentage, return default

---

## Best Practices

### 1. Choose the Right Strategy

**Decision Tree:**
```
Is this an emergency?
├─ Yes → Direct
└─ No
    ├─ Is this a scheduled launch?
    │   └─ Yes → TimeBased
    └─ No
        ├─ Is this for specific users?
        │   └─ Yes → UserSegment
        └─ No
            ├─ Do you have health metrics?
            │   ├─ Yes → Canary
            │   └─ No → Percentage
```

### 2. Monitor Rollout Health

Track metrics per rollout:
- Error rate
- Latency (p50, p95, p99)
- Conversion rate
- User engagement
- Custom business metrics

### 3. Set Appropriate Thresholds

**Conservative (high-risk features):**
```json
{
  "errorRateThreshold": 0.01,
  "latencyP99Threshold": 50
}
```

**Moderate (medium-risk features):**
```json
{
  "errorRateThreshold": 0.05,
  "latencyP99Threshold": 100
}
```

### 4. Test Rollback Procedures

Regularly test rollback:
```bash
# Simulate anomaly
curl -X POST /api/v1/flags/my-flag/rollouts/rollout-123/rollback \
  -d '{"reason":"Simulated error rate spike"}'
```

---

## Troubleshooting

### Issue: User sees inconsistent flag values

**Symptom:** Same user gets different values on refresh

**Cause:** Non-deterministic bucketing

**Solution:**
- Ensure userId is passed consistently
- Use deterministic hashing (not random)
- Clear cache if bucketing logic changed

### Issue: Rollout not progressing

**Symptom:** Stuck at 10% for hours

**Cause:** Health check failing

**Solution:**
1. Check metrics: `GET /api/v1/flags/{name}/rollouts/{id}/metrics`
2. Review thresholds (may be too strict)
3. Manually progress: `POST /api/v1/flags/{name}/rollouts/{id}/progress`

### Issue: High evaluation latency

**Symptom:** Flag evaluation > 10ms

**Solutions:**
1. Check cache hit rate
2. Simplify targeting rules
3. Reduce rule count
4. Add caching layer

---

**Last Updated:** 2025-11-23
**Version:** 1.0.0
