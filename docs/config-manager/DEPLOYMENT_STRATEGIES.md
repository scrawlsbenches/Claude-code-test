# Configuration Deployment Strategies Guide

**Version:** 1.0.0
**Last Updated:** 2025-11-23

---

## Overview

The HotSwap Configuration Manager supports 4 deployment strategies, each adapted from proven deployment patterns. Deployment strategies determine how configuration updates are rolled out to service instances.

### Strategy Selection

| Environment | Default Strategy | Use Case |
|-------------|------------------|----------|
| Development | Direct | Fast iteration, low risk |
| Staging | Rolling | QA testing, moderate risk |
| Production | Canary | Maximum safety, gradual rollout |
| Critical Services | Blue-Green | Instant switchover, easy rollback |

---

## 1. Canary Deployment Strategy

### Overview

**Pattern:** Progressive Percentage Rollout
**Risk Level:** Low
**Complexity:** Medium
**Rollback:** Automatic on health degradation

**Use Case:** Production deployments where minimizing risk is critical.

### Behavior

- Deploy to small percentage of instances first (10%)
- Monitor health metrics for specified interval
- Gradually increase percentage if healthy
- Automatic rollback if health degrades

### Deployment Phases

**Phase 1: Canary (10%)**
- Deploy to 10% of instances (minimum 1 instance)
- Monitor for 5 minutes
- Check error rate, latency, custom metrics

**Phase 2: Early Majority (30%)**
- If Phase 1 healthy, deploy to 30% of instances
- Monitor for 5 minutes
- Continue health checks

**Phase 3: Late Majority (50%)**
- If Phase 2 healthy, deploy to 50% of instances
- Monitor for 5 minutes
- Final pre-completion check

**Phase 4: Complete (100%)**
- If Phase 3 healthy, deploy to all remaining instances
- Final health verification
- Mark deployment complete

### Algorithm

```csharp
public async Task<DeploymentResult> DeployCanaryAsync(ConfigDeployment deployment)
{
    var phases = new[] { 10, 30, 50, 100 }; // Percentage of instances
    var previousPhaseInstances = new List<string>();

    foreach (var percentage in phases)
    {
        // Calculate instances for this phase
        var targetCount = (int)Math.Ceiling(deployment.TargetInstances.Count * percentage / 100.0);
        var phaseInstances = deployment.TargetInstances.Take(targetCount).ToList();
        var newInstances = phaseInstances.Except(previousPhaseInstances).ToList();

        // Deploy to new instances in this phase
        foreach (var instanceId in newInstances)
        {
            await DeployToInstanceAsync(deployment, instanceId);
        }

        // Wait for phase interval
        await Task.Delay(deployment.Config.PhaseInterval);

        // Check health of all deployed instances
        var health = await CheckDeploymentHealthAsync(deployment.DeploymentId);
        if (health.HasDegraded(deployment.HealthCheck))
        {
            // Automatic rollback
            await RollbackAsync(deployment.DeploymentId, "Health degradation detected");
            return DeploymentResult.Failure(deployment.DeploymentId, "Rolled back due to health issues");
        }

        previousPhaseInstances = phaseInstances;
    }

    return DeploymentResult.SuccessResult(deployment.DeploymentId, deployment.TargetInstances.Count);
}
```

### Configuration

```json
{
  "strategy": "Canary",
  "canaryPercentage": 10,
  "phaseInterval": "PT5M",
  "healthCheckEnabled": true,
  "autoPromote": true,
  "healthCheck": {
    "errorRateThreshold": 5.0,
    "latencyThreshold": 50.0,
    "autoRollback": true
  }
}
```

### Health Monitoring

**Metrics Tracked:**
- **Error Rate:** HTTP 5xx responses / total requests
- **p99 Latency:** 99th percentile response time
- **Availability:** Health check endpoint status
- **Custom Metrics:** Application-specific metrics

**Rollback Triggers:**
```
Baseline Error Rate: 1.5%
Current Error Rate: 7.0%
Increase: 5.5% (> 5% threshold) → ROLLBACK

Baseline p99 Latency: 200ms
Current p99 Latency: 350ms
Increase: 75% (> 50% threshold) → ROLLBACK
```

### Performance Characteristics

- **Deployment Time:** 15-30 minutes (4 phases × 5 min)
- **Risk Level:** Very Low (only 10% exposed initially)
- **Rollback Time:** < 60 seconds
- **Complexity:** Medium (requires health monitoring)

### When to Use

✅ **Good For:**
- Production deployments
- Critical services (payment, auth)
- Breaking configuration changes
- First-time config deployments
- Services with SLA requirements

❌ **Not Good For:**
- Development environments (use Direct)
- Non-critical changes
- Urgent hotfixes (use Blue-Green)
- Small instance counts (< 3 instances)

---

## 2. Blue-Green Deployment Strategy

### Overview

**Pattern:** Parallel Environment Switchover
**Risk Level:** Low
**Complexity:** High
**Rollback:** Instant (< 10 seconds)

**Use Case:** Production deployments requiring instant rollback capability.

### Behavior

- Deploy to "green" environment (inactive instances)
- Verify green environment health for specified period
- Switch traffic from "blue" (active) to "green"
- Keep blue environment warm for quick rollback

### Deployment Process

**Phase 1: Deploy to Green**
```
Blue (Active):  Instance-1 [v1.0.0] ← 100% traffic
                Instance-2 [v1.0.0]
                Instance-3 [v1.0.0]

Green (Inactive): Instance-4 [v1.1.0] ← Deploy new config
                  Instance-5 [v1.1.0]
                  Instance-6 [v1.1.0]
```

**Phase 2: Verify Green**
- Run health checks on green instances
- Monitor metrics for verification period (default: 10 minutes)
- Ensure error rate, latency within thresholds

**Phase 3: Switch Traffic**
```
Blue (Inactive):  Instance-1 [v1.0.0] ← 0% traffic
                  Instance-2 [v1.0.0]
                  Instance-3 [v1.0.0]

Green (Active):   Instance-4 [v1.1.0] ← 100% traffic
                  Instance-5 [v1.1.0]
                  Instance-6 [v1.1.0]
```

**Phase 4: Keep Blue Warm**
- Retain blue instances for specified duration (default: 30 minutes)
- Allows instant rollback if issues detected
- After duration, decommission blue instances

### Algorithm

```csharp
public async Task<DeploymentResult> DeployBlueGreenAsync(ConfigDeployment deployment)
{
    // Identify blue (current) and green (new) instances
    var blueInstances = deployment.TargetInstances.Where(i => IsActive(i)).ToList();
    var greenInstances = await ProvisionGreenInstancesAsync(blueInstances.Count);

    // Deploy to green instances
    foreach (var instanceId in greenInstances)
    {
        await DeployToInstanceAsync(deployment, instanceId);
    }

    // Verification period
    await Task.Delay(deployment.Config.VerificationPeriod);

    // Check green health
    var health = await CheckInstanceGroupHealthAsync(greenInstances);
    if (!health.IsHealthy())
    {
        await DecommissionInstancesAsync(greenInstances);
        return DeploymentResult.Failure(deployment.DeploymentId, "Green environment unhealthy");
    }

    // Switch traffic to green
    await SwitchTrafficAsync(blueInstances, greenInstances);

    // Keep blue warm for potential rollback
    await Task.Delay(deployment.Config.KeepBlueDuration ?? TimeSpan.FromMinutes(30));

    // Decommission blue
    await DecommissionInstancesAsync(blueInstances);

    return DeploymentResult.SuccessResult(deployment.DeploymentId, greenInstances.Count);
}
```

### Configuration

```json
{
  "strategy": "BlueGreen",
  "verificationPeriod": "PT10M",
  "keepBlueDuration": "PT30M",
  "healthCheckEnabled": true,
  "autoSwitch": true
}
```

### Rollback Process

**During Verification (before switch):**
- Cancel deployment
- Decommission green instances
- Keep blue instances active
- Rollback time: < 5 seconds

**After Switch (green active):**
- Switch traffic back to blue instances
- Blue instances already warm and ready
- Rollback time: < 10 seconds

### Performance Characteristics

- **Deployment Time:** 10-15 minutes (verification period)
- **Risk Level:** Very Low (easy rollback)
- **Rollback Time:** < 10 seconds
- **Complexity:** High (requires traffic switching)
- **Resource Cost:** 2× instances during deployment

### When to Use

✅ **Good For:**
- Mission-critical services
- Deployments requiring instant rollback
- Database configuration changes
- Services with strict SLAs
- Compliance-heavy environments

❌ **Not Good For:**
- Resource-constrained environments
- Small deployments (< 2 instances)
- Frequent configuration changes
- Development/staging environments

---

## 3. Rolling Deployment Strategy

### Overview

**Pattern:** Sequential Instance-by-Instance Rollout
**Risk Level:** Medium
**Complexity:** Low
**Rollback:** Manual (fast)

**Use Case:** Staging deployments with gradual rollout.

### Behavior

- Deploy to instances sequentially (or in small batches)
- Wait for health verification after each batch
- Continue to next batch if healthy
- Stop and rollback on any failure

### Deployment Process

**Batch 1:**
```
Instance-1: [v1.0.0] → [v1.1.0] ← Deploy
Instance-2: [v1.0.0]
Instance-3: [v1.0.0]
Instance-4: [v1.0.0]

Wait 2 minutes, check health
```

**Batch 2:**
```
Instance-1: [v1.1.0] ✓
Instance-2: [v1.0.0] → [v1.1.0] ← Deploy
Instance-3: [v1.0.0]
Instance-4: [v1.0.0]

Wait 2 minutes, check health
```

**Continue until all instances deployed...**

### Algorithm

```csharp
public async Task<DeploymentResult> DeployRollingAsync(ConfigDeployment deployment)
{
    var batchSize = deployment.Config.BatchSize;
    var totalBatches = (int)Math.Ceiling((double)deployment.TargetInstances.Count / batchSize);
    var deployedInstances = new List<string>();

    for (int i = 0; i < totalBatches; i++)
    {
        var batch = deployment.TargetInstances
            .Skip(i * batchSize)
            .Take(batchSize)
            .ToList();

        // Deploy to batch
        foreach (var instanceId in batch)
        {
            try
            {
                await DeployToInstanceAsync(deployment, instanceId);
                deployedInstances.Add(instanceId);
            }
            catch (Exception ex)
            {
                if (deployment.Config.StopOnFailure)
                {
                    await RollbackInstancesAsync(deployedInstances, deployment.PreviousVersion);
                    return DeploymentResult.Failure(deployment.DeploymentId,
                        $"Deployment failed on instance {instanceId}: {ex.Message}");
                }
            }
        }

        // Wait for batch interval
        await Task.Delay(deployment.Config.BatchInterval);

        // Check health
        if (deployment.HealthCheck.Enabled)
        {
            var health = await CheckInstanceHealthAsync(batch.Last());
            if (!health.IsHealthy())
            {
                await RollbackInstancesAsync(deployedInstances, deployment.PreviousVersion);
                return DeploymentResult.Failure(deployment.DeploymentId, "Health check failed");
            }
        }
    }

    return DeploymentResult.SuccessResult(deployment.DeploymentId, deployedInstances.Count);
}
```

### Configuration

```json
{
  "strategy": "Rolling",
  "batchSize": 2,
  "batchInterval": "PT2M",
  "stopOnFailure": true,
  "healthCheckEnabled": true
}
```

### Batch Size Strategies

**Batch Size = 1 (Safest):**
- Deploy to one instance at a time
- Slowest deployment
- Earliest failure detection

**Batch Size = 25% of instances:**
- Balanced speed and safety
- Good for medium deployments (10-50 instances)

**Batch Size = 50% of instances:**
- Faster deployment
- Higher risk
- Similar to canary but no gradual increase

### Performance Characteristics

- **Deployment Time:** Variable (batch size × batch interval)
  - 1 instance/2min = 20 minutes for 10 instances
  - 2 instances/2min = 10 minutes for 10 instances
- **Risk Level:** Medium (incremental exposure)
- **Rollback Time:** < 2 minutes (manual)
- **Complexity:** Low

### When to Use

✅ **Good For:**
- Staging environment deployments
- QA testing workflows
- Medium-sized deployments (5-50 instances)
- Services with graceful degradation
- Deployments with manual oversight

❌ **Not Good For:**
- Production critical services (use Canary)
- Very large deployments (> 100 instances)
- Time-sensitive deployments
- Fully automated pipelines

---

## 4. Direct Deployment Strategy

### Overview

**Pattern:** Simultaneous All-Instance Deployment
**Risk Level:** High
**Complexity:** Very Low
**Rollback:** Manual (fast)

**Use Case:** Development environments or low-risk changes.

### Behavior

- Deploy to ALL instances simultaneously
- Fastest deployment time
- No gradual rollout
- No automatic health monitoring (optional)

### Deployment Process

```
Before:
Instance-1: [v1.0.0]
Instance-2: [v1.0.0]
Instance-3: [v1.0.0]

Deploy simultaneously to all ↓

After:
Instance-1: [v1.1.0]
Instance-2: [v1.1.0]
Instance-3: [v1.1.0]

Time: < 15 seconds
```

### Algorithm

```csharp
public async Task<DeploymentResult> DeployDirectAsync(ConfigDeployment deployment)
{
    var deploymentTasks = deployment.TargetInstances
        .Select(instanceId => DeployToInstanceAsync(deployment, instanceId))
        .ToList();

    var results = await Task.WhenAll(deploymentTasks);

    var successCount = results.Count(r => r.Success);
    var failedCount = results.Count(r => !r.Success);

    if (failedCount > 0)
    {
        return DeploymentResult.PartialResult(
            deployment.DeploymentId,
            successCount,
            failedCount);
    }

    return DeploymentResult.SuccessResult(deployment.DeploymentId, successCount);
}
```

### Configuration

```json
{
  "strategy": "Direct",
  "healthCheckEnabled": false,
  "maxConcurrency": 50
}
```

### Performance Characteristics

- **Deployment Time:** < 15 seconds
- **Risk Level:** High (all instances affected)
- **Rollback Time:** < 30 seconds (manual)
- **Complexity:** Very Low

### When to Use

✅ **Good For:**
- Development environments
- Non-critical configuration changes
- Urgent hotfixes (with caution)
- Small deployments (< 5 instances)
- Configuration format changes (non-breaking)

❌ **Not Good For:**
- Production deployments
- Critical services
- Breaking changes
- First-time deployments
- Services with SLA requirements

---

## Strategy Comparison

| Strategy | Deployment Time | Risk Level | Complexity | Rollback Time | Best For |
|----------|----------------|------------|------------|---------------|----------|
| Canary | 15-30 minutes | ⭐⭐⭐⭐⭐ | ⭐⭐⭐ | < 60s | Production (critical) |
| Blue-Green | 10-15 minutes | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | < 10s | Production (instant rollback) |
| Rolling | Variable | ⭐⭐⭐ | ⭐⭐ | < 2min | Staging/QA |
| Direct | < 15 seconds | ⭐ | ⭐ | < 30s | Development |

---

## Health Monitoring Integration

### Health Metrics

All strategies (except Direct) support health monitoring:

**System Metrics:**
- Error rate (HTTP 5xx / total requests)
- Latency (p50, p95, p99)
- Availability (health endpoint)

**Custom Metrics:**
- Business metrics (e.g., order success rate)
- Application metrics (e.g., cache hit rate)

### Automatic Rollback Criteria

| Metric | Threshold | Action |
|--------|-----------|--------|
| Error Rate | > 5% increase from baseline | Auto-rollback |
| p99 Latency | > 50% increase from baseline | Auto-rollback |
| Health Check Failures | > 10% of checks failed | Auto-rollback |
| Custom Metric | User-defined threshold | Auto-rollback |

### Rollback Process

**Detection → Alert → Pause → Rollback → Verify**

1. **Detection:** Health metric exceeds threshold (< 60s)
2. **Alert:** Notification sent to operators
3. **Pause:** Stop ongoing deployment
4. **Rollback:** Deploy previous config version
5. **Verify:** Confirm health returns to baseline

**Total Rollback Time:** < 90 seconds

---

## Best Practices

### 1. Choose the Right Strategy

**Decision Tree:**
```
Is this production?
├─ Yes → Is instant rollback critical?
│  ├─ Yes → Blue-Green
│  └─ No → Canary
└─ No → Is this staging?
   ├─ Yes → Rolling
   └─ No → Direct
```

### 2. Configure Health Checks

Always enable health monitoring for production:
```json
{
  "healthCheck": {
    "enabled": true,
    "checkInterval": "PT30S",
    "errorRateThreshold": 5.0,
    "latencyThreshold": 50.0,
    "autoRollback": true
  }
}
```

### 3. Start with Canary

If unsure, default to Canary for production deployments. It provides the best balance of safety and deployment speed.

### 4. Test in Lower Environments

Always test configuration changes in Development → Staging → Production progression.

### 5. Monitor Deployment Metrics

Track these metrics for all deployments:
- Deployment duration
- Success rate
- Rollback rate
- Health check failures
- Time to rollback

---

## Troubleshooting

### Issue: Canary deployment stuck at 10%

**Symptom:** Health checks not progressing to next phase

**Solutions:**
1. Check health metric thresholds (may be too strict)
2. Verify baseline metrics are correctly captured
3. Review custom health check endpoint
4. Check phase interval configuration

### Issue: Blue-Green consumes too many resources

**Symptom:** High infrastructure costs during deployment

**Solutions:**
1. Reduce keepBlueDuration
2. Use smaller instance types for green environment
3. Consider Canary strategy instead
4. Schedule deployments during off-peak hours

### Issue: Rolling deployment takes too long

**Symptom:** Deployment exceeds maintenance window

**Solutions:**
1. Increase batch size (e.g., 2 → 5 instances)
2. Reduce batch interval (e.g., 2min → 1min)
3. Consider Direct strategy for non-critical changes
4. Deploy during extended maintenance window

---

## Custom Deployment Strategies

To implement a custom strategy:

### 1. Implement IDeploymentStrategy Interface

```csharp
public class CustomDeploymentStrategy : IDeploymentStrategy
{
    public string Name => "Custom";

    public async Task<DeploymentResult> DeployAsync(ConfigDeployment deployment)
    {
        // Your custom deployment logic here
        // ...
        return DeploymentResult.SuccessResult(deployment.DeploymentId, instanceCount);
    }
}
```

### 2. Register Strategy

```csharp
// In Program.cs
services.AddSingleton<IDeploymentStrategy, CustomDeploymentStrategy>();
```

### 3. Configure Deployment

```json
{
  "strategy": "Custom",
  "config": {
    "customParameter": "value"
  }
}
```

---

**Last Updated:** 2025-11-23
**Version:** 1.0.0
