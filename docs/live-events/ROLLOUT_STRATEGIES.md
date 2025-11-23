# Event Rollout Strategies Guide

**Version:** 1.0.0
**Last Updated:** 2025-11-23

---

## Overview

The HotSwap Live Event System supports 5 rollout strategies, each adapted from the existing deployment strategies in the kernel orchestration platform. Rollout strategies determine how events are deployed across geographic regions.

### Strategy Selection

| Event Type | Default Strategy | Use Case |
|------------|------------------|----------|
| SeasonalPromotion | Geographic | Time-zone aware activation |
| LimitedTimeOffer | Canary | Progressive rollout with safety |
| CompetitiveEvent | Rolling | Sequential region deployment |
| ContentRelease | Blue-Green | Instant switch with quick rollback |
| ABTestVariant | Segmented | Player cohort targeting |

---

## 1. Canary Rollout Strategy

### Overview

**Based On:** Canary Deployment Strategy
**Pattern:** Progressive Percentage Rollout
**Risk:** Low
**Complexity:** Medium

**Use Case:** Deploy events progressively with automated rollback on engagement drops.

### Behavior

- Deploy to **10% of region → monitor → 30% → 50% → 100%**
- Automatic rollback on metric threshold breach
- Configurable batch sizes and delays
- Regional health monitoring between batches

### Algorithm

```csharp
public async Task<DeploymentResult> ExecuteAsync(EventDeployment deployment)
{
    var batches = deployment.Configuration.CanaryBatches; // [10, 30, 50, 100]

    foreach (var batchPercentage in batches)
    {
        // Deploy to batch percentage
        await DeployToBatchAsync(deployment, batchPercentage);

        // Monitor metrics for configured delay
        await Task.Delay(deployment.Configuration.BatchDelay);

        // Check engagement metrics
        var metrics = await GetMetricsAsync(deployment.EventId, deployment.Regions);

        // Automatic rollback on threshold breach
        if (metrics.ParticipationRate < Threshold)
        {
            await RollbackAsync(deployment);
            return DeploymentResult.Failure("Participation rate dropped");
        }
    }

    return DeploymentResult.SuccessResult(deployment.DeploymentId, deployment.Regions);
}
```

### Message Flow

```
Deployment Start
  ├─ Batch 1 (10%) → Deploy → Monitor (5 min) → ✓ Pass
  ├─ Batch 2 (30%) → Deploy → Monitor (5 min) → ✓ Pass
  ├─ Batch 3 (50%) → Deploy → Monitor (5 min) → ✓ Pass
  └─ Batch 4 (100%) → Deploy → Monitor → ✓ Complete
```

### Configuration

```json
{
  "strategy": "Canary",
  "configuration": {
    "canaryBatches": [10, 30, 50, 100],
    "batchDelay": "PT5M",
    "autoRollback": {
      "participationRateDrop": 0.2,
      "errorRateIncrease": 0.05,
      "negativeFeedbackCount": 100
    }
  }
}
```

### Performance Characteristics

- **Deployment Time:** 20-30 minutes (4 batches × 5 min delay)
- **Risk Level:** Low (early detection of issues)
- **Rollback Time:** < 30 seconds
- **Player Impact:** Gradual (10% → 100%)

### When to Use

✅ **Good For:**
- New event types (untested)
- High-value promotional events
- Events with revenue impact
- Production deployments with unknown outcomes

❌ **Not Good For:**
- Time-sensitive launches
- Low-risk content updates
- Internal testing events

---

## 2. Blue-Green Rollout Strategy

### Overview

**Based On:** Blue-Green Deployment
**Pattern:** Instant Environment Switch
**Risk:** Medium
**Complexity:** Low

**Use Case:** Instant event activation with quick rollback capability.

### Behavior

- Deploy to "green" environment (inactive)
- Switch traffic to green instantly
- Keep blue environment for quick rollback
- No gradual rollout

### Algorithm

```csharp
public async Task<DeploymentResult> ExecuteAsync(EventDeployment deployment)
{
    // Deploy to green environment
    await DeployToGreenAsync(deployment);

    // Validate green environment
    var isHealthy = await ValidateGreenAsync(deployment);
    if (!isHealthy)
    {
        return DeploymentResult.Failure("Green environment validation failed");
    }

    // Switch traffic instantly
    await SwitchToGreenAsync(deployment);

    // Keep blue environment for rollback (30 minutes)
    await Task.Delay(TimeSpan.FromMinutes(30));
    await DecommissionBlueAsync(deployment);

    return DeploymentResult.SuccessResult(deployment.DeploymentId, deployment.Regions);
}
```

### Message Flow

```
Blue Environment (Active, v1.0)
  ↓
Green Environment (Deploy v2.0) → Validate → ✓ Healthy
  ↓
Switch Traffic (Blue → Green) → Instant
  ↓
Monitor Green (30 min) → Decommission Blue
```

### Configuration

```json
{
  "strategy": "BlueGreen",
  "configuration": {
    "targetEnvironment": "green",
    "switchDelay": "PT0S",
    "blueRetentionPeriod": "PT30M"
  }
}
```

### Performance Characteristics

- **Deployment Time:** 5-10 minutes
- **Switch Time:** Instant (< 1 second)
- **Risk Level:** Medium (instant impact)
- **Rollback Time:** < 5 seconds

### When to Use

✅ **Good For:**
- Time-sensitive launches (synchronized global release)
- Low-risk content updates
- Events with proven configurations
- Marketing campaigns with specific launch times

❌ **Not Good For:**
- Untested event types
- High-risk promotional changes
- Events requiring gradual player adoption

---

## 3. Rolling Rollout Strategy

### Overview

**Based On:** Rolling Deployment
**Pattern:** Sequential Region Deployment
**Risk:** Medium
**Complexity:** Medium

**Use Case:** Deploy events region-by-region with health monitoring.

### Behavior

- Deploy to regions sequentially (us-east → us-west → eu-central → ap-southeast)
- Monitor each region before proceeding to next
- Halt rollout on region failure
- Continue on region success

### Algorithm

```csharp
public async Task<DeploymentResult> ExecuteAsync(EventDeployment deployment)
{
    var successfulRegions = new List<string>();
    var failedRegions = new List<string>();

    foreach (var region in deployment.Configuration.RegionOrder)
    {
        // Deploy to region
        var result = await DeployToRegionAsync(deployment, region);

        if (result.Success)
        {
            successfulRegions.Add(region);

            // Monitor region health
            await Task.Delay(deployment.Configuration.RegionDelay);

            var isHealthy = await CheckRegionHealthAsync(region);
            if (!isHealthy)
            {
                await RollbackRegionAsync(deployment, region);
                failedRegions.Add(region);
                break; // Halt rollout
            }
        }
        else
        {
            failedRegions.Add(region);
            break; // Halt rollout
        }
    }

    if (failedRegions.Any())
        return DeploymentResult.PartialSuccess(deployment.DeploymentId, successfulRegions, failedRegions);

    return DeploymentResult.SuccessResult(deployment.DeploymentId, successfulRegions);
}
```

### Message Flow

```
Region Sequence
  ├─ us-east → Deploy → Monitor (10 min) → ✓ Healthy → Continue
  ├─ us-west → Deploy → Monitor (10 min) → ✓ Healthy → Continue
  ├─ eu-central → Deploy → Monitor (10 min) → ✗ Unhealthy → Halt
  └─ (Rollout halted, ap-southeast not deployed)
```

### Configuration

```json
{
  "strategy": "Rolling",
  "configuration": {
    "regionOrder": ["us-east", "us-west", "eu-central", "ap-southeast"],
    "regionDelay": "PT10M",
    "haltOnFailure": true
  }
}
```

### Performance Characteristics

- **Deployment Time:** 40-60 minutes (4 regions × 10 min delay)
- **Risk Level:** Medium (regional isolation)
- **Rollback Time:** < 30 seconds per region
- **Player Impact:** Sequential (regions activated one-by-one)

### When to Use

✅ **Good For:**
- Global events with regional testing
- Events requiring regional compliance checks
- Events with region-specific configurations
- Staged global rollouts

❌ **Not Good For:**
- Synchronized global launches
- Time-sensitive events
- Events requiring simultaneous activation

---

## 4. Geographic Rollout Strategy

### Overview

**Based On:** Time-Zone Aware Deployment
**Pattern:** Local Time Synchronization
**Risk:** Low
**Complexity:** High

**Use Case:** Activate events at same local time across regions (e.g., 12:00 PM everywhere).

### Behavior

- Activate event at same local time across regions
- Us-east: 12:00 EST, us-west: 12:00 PST, eu-central: 12:00 CET
- Automatic timezone conversion
- DST-aware scheduling

### Algorithm

```csharp
public async Task<DeploymentResult> ExecuteAsync(EventDeployment deployment)
{
    var localTime = TimeSpan.Parse(deployment.Configuration.LocalTime); // "12:00:00"
    var scheduledActivations = new List<Task>();

    foreach (var region in deployment.Regions)
    {
        // Get region timezone
        var timezone = await GetRegionTimezoneAsync(region);

        // Calculate activation time in region's local time
        var activationTime = CalculateLocalActivationTime(deployment.StartTime, localTime, timezone);

        // Schedule activation
        var delay = activationTime - DateTime.UtcNow;
        scheduledActivations.Add(Task.Run(async () =>
        {
            await Task.Delay(delay);
            await ActivateInRegionAsync(deployment, region);
        }));
    }

    // Wait for all activations
    await Task.WhenAll(scheduledActivations);

    return DeploymentResult.SuccessResult(deployment.DeploymentId, deployment.Regions);
}

private DateTime CalculateLocalActivationTime(DateTime baseDate, TimeSpan localTime, string timezone)
{
    var tz = TimeZoneInfo.FindSystemTimeZoneById(timezone);
    var localDate = TimeZoneInfo.ConvertTimeFromUtc(baseDate, tz);
    var activationDateTime = localDate.Date + localTime;
    return TimeZoneInfo.ConvertTimeToUtc(activationDateTime, tz);
}
```

### Message Flow

```
Global Event: "Daily Login Bonus at 12:00 PM Local Time"

Activations (UTC):
  ├─ us-east (EST): 12:00 PM EST = 17:00 UTC
  ├─ us-west (PST): 12:00 PM PST = 20:00 UTC
  ├─ eu-central (CET): 12:00 PM CET = 11:00 UTC
  └─ ap-southeast (JST): 12:00 PM JST = 03:00 UTC

Timeline:
  03:00 UTC → ap-southeast activates
  11:00 UTC → eu-central activates
  17:00 UTC → us-east activates
  20:00 UTC → us-west activates
```

### Configuration

```json
{
  "strategy": "Geographic",
  "configuration": {
    "localTime": "12:00:00",
    "regions": ["us-east", "us-west", "eu-central", "ap-southeast"],
    "dstHandling": "automatic"
  }
}
```

### Performance Characteristics

- **Deployment Time:** 24 hours max (timezone spread)
- **Risk Level:** Low (staggered activation)
- **Rollback Time:** < 30 seconds per region
- **Player Impact:** Same local time experience

### DST Handling

**Spring Forward (DST starts):**
- If activation falls in skipped hour (2:00 AM → 3:00 AM), schedule at 3:00 AM
- Example: 2:30 AM activation → scheduled at 3:00 AM

**Fall Back (DST ends):**
- If activation falls in repeated hour (1:00 AM → 1:00 AM), schedule at first occurrence
- Example: 1:30 AM activation → scheduled at first 1:30 AM

### When to Use

✅ **Good For:**
- Daily recurring events
- Time-sensitive promotions (flash sales at noon)
- Player retention events (login bonuses)
- Marketing campaigns with local time awareness

❌ **Not Good For:**
- Global synchronized launches
- Events not tied to local time
- Testing/development environments

---

## 5. Segmented Rollout Strategy

### Overview

**Based On:** Player Cohort Targeting
**Pattern:** Segment-Based Activation
**Risk:** Low
**Complexity:** High

**Use Case:** Deploy events to specific player segments (VIP, new players, inactive).

### Behavior

- Activate event for specific player segments only
- Support multiple targeting criteria
- Dynamic segment membership (recalculated in real-time)
- Segment exclusion rules

### Algorithm

```csharp
public async Task<DeploymentResult> ExecuteAsync(EventDeployment deployment)
{
    var targetSegments = deployment.TargetSegments;

    foreach (var segmentId in targetSegments)
    {
        var segment = await GetSegmentAsync(segmentId);

        // Activate event for segment
        await ActivateForSegmentAsync(deployment, segment);

        // Monitor segment engagement
        var metrics = await GetSegmentMetricsAsync(deployment.EventId, segmentId);

        // Log segment activation
        _logger.LogInformation($"Event {deployment.EventId} activated for segment {segmentId} ({segment.EstimatedPlayerCount} players)");
    }

    return DeploymentResult.SuccessResult(deployment.DeploymentId, deployment.Regions);
}
```

### Message Flow

```
Event: "VIP Weekend Bonus"

Target Segments:
  ├─ vip-tier-3 (45,000 players) → Activate
  ├─ vip-tier-2 (120,000 players) → Activate
  └─ vip-tier-1 (300,000 players) → Activate

Non-VIP Players: Event not visible
```

### Configuration

```json
{
  "strategy": "Segmented",
  "configuration": {
    "targetSegments": ["vip-tier-3", "vip-tier-2", "vip-tier-1"],
    "excludeSegments": ["banned-players"],
    "dynamicMembership": true
  }
}
```

### Performance Characteristics

- **Deployment Time:** 5-10 minutes
- **Risk Level:** Low (limited player impact)
- **Rollback Time:** < 30 seconds
- **Player Impact:** Targeted (specific cohorts only)

### Segment Types

**Pre-Defined Segments:**
- `vip-tier-{1,2,3}` - VIP players by spend
- `new-players` - Account age < 7 days
- `inactive-players` - Last login > 30 days
- `high-engagement` - Daily active for last 7 days
- `at-risk` - Declining engagement

**Custom Segments:**
- Defined by criteria (level, spend, platform, etc.)
- Recalculated on player query
- Membership cached for performance

### When to Use

✅ **Good For:**
- Player retention campaigns
- VIP exclusive events
- Re-engagement promotions
- A/B testing
- Personalized content

❌ **Not Good For:**
- Global events (all players)
- Seasonal promotions (broad audience)
- Time-sensitive launches

---

## Rollback Triggers

### Automatic Rollback

All strategies support automatic rollback based on metric thresholds:

**Engagement Metrics:**
- Participation rate drop > 20%
- Completion rate drop > 30%
- Session duration drop > 40%

**Technical Metrics:**
- Server error rate > 5%
- API latency p99 > 500ms
- Cache hit rate < 80%

**Sentiment Metrics:**
- Negative feedback > 100 complaints/hour
- NPS drop > 20 points
- Player support tickets > 50/hour

### Manual Rollback

**Rollback Endpoint:**
```
POST /api/v1/deployments/{deploymentId}/rollback
```

**Rollback Process:**
1. Stop current deployment
2. Deactivate event in affected regions
3. Restore previous configuration (if applicable)
4. Notify stakeholders (webhook, email)
5. Log rollback reason and metrics

**Rollback SLA:**
- Target: < 30 seconds
- Player impact: Minimal (event disappears from UI)
- Data loss: None (player progress preserved)

---

## Strategy Comparison

| Strategy | Deployment Time | Risk Level | Rollback Time | Complexity | Best For |
|----------|----------------|------------|---------------|------------|----------|
| **Canary** | 20-30 min | Low | < 30s | Medium | Untested events |
| **Blue-Green** | 5-10 min | Medium | < 5s | Low | Time-sensitive launches |
| **Rolling** | 40-60 min | Medium | < 30s | Medium | Regional testing |
| **Geographic** | 24 hours | Low | < 30s | High | Local time events |
| **Segmented** | 5-10 min | Low | < 30s | High | Player targeting |

---

## Best Practices

### 1. Choose the Right Strategy

- **Safety First:** Use Canary for high-risk events
- **Time-Sensitive:** Use Blue-Green for synchronized launches
- **Global Events:** Use Geographic for local time alignment
- **Targeted Events:** Use Segmented for player cohorts

### 2. Monitor Actively

- Watch engagement metrics during rollout
- Set up alerts for threshold breaches
- Have rollback plan ready
- Communicate with stakeholders

### 3. Test Thoroughly

- Test rollout in staging environment
- Validate timezone calculations
- Verify segment membership
- Test rollback procedures

### 4. Document Decisions

- Record why strategy was chosen
- Document rollback triggers
- Log deployment progress
- Preserve rollback history

---

**Document Version:** 1.0.0
**Last Updated:** 2025-11-23
**Next Review:** After First Production Rollout
