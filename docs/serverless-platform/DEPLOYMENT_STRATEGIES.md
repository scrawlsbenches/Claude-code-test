# Function Deployment Strategies Guide

**Version:** 1.0.0
**Last Updated:** 2025-11-23

---

## Overview

The HotSwap Serverless Platform supports 5 deployment strategies, each adapted from the existing kernel orchestration platform. Deployment strategies determine how new function versions are rolled out to production with varying levels of risk and rollback capabilities.

### Strategy Selection

| Use Case | Recommended Strategy | Risk Level | Rollback Time |
|----------|---------------------|------------|---------------|
| Low-risk changes | All-At-Once | High | Manual, slow |
| Production deployments | Canary | Low | Automatic, fast |
| Critical functions | Blue-Green | Medium | Instant |
| Large-scale updates | Rolling | Medium | Progressive |
| A/B testing | A/B Testing | Low | Manual |

---

## 1. All-At-Once Deployment

### Overview

**Pattern:** Direct Replacement
**Latency:** Fastest (30-60 seconds)
**Complexity:** Low
**Risk:** Highest

**Use Case:** Development/staging environments, low-risk changes, immediate updates.

### Behavior

- Deploy new version immediately
- Replace all traffic to new version
- No gradual rollout
- No automatic rollback

### Algorithm

```csharp
public async Task<DeploymentResult> DeployAsync(Deployment deployment)
{
    var function = await _functionRepository.GetAsync(deployment.FunctionName);
    var alias = await _aliasRepository.GetAsync(deployment.FunctionName, "production");
    
    // Direct switch to new version
    alias.UpdateVersion(deployment.TargetVersion);
    await _aliasRepository.UpdateAsync(alias);
    
    return DeploymentResult.Success();
}
```

### Traffic Flow

```
Before:
production → v5 (100%)

After (immediate):
production → v6 (100%)
```

### Configuration

```json
{
  "functionName": "image-processor",
  "version": 6,
  "strategy": "AllAtOnce"
}
```

### Timeline

```
0:00  → Update production alias to v6
0:01  → All traffic routed to v6
0:02  → Deployment complete
```

### Performance Characteristics

- **Deployment Time:** ~30-60 seconds
- **Risk:** High (all traffic instantly on new version)
- **Rollback:** Manual update of alias (30 seconds)
- **Downtime:** Near-zero (alias update is atomic)

### When to Use

✅ **Good For:**
- Development and staging environments
- Low-traffic functions
- Trivial changes (configuration, bug fixes)
- Urgent hotfixes

❌ **Not Good For:**
- Production critical functions
- High-traffic functions
- Major version changes
- Unverified code changes

---

## 2. Canary Deployment

### Overview

**Pattern:** Gradual Traffic Shift with Monitoring
**Latency:** Moderate (10-30 minutes)
**Complexity:** Medium
**Risk:** Low

**Use Case:** Production deployments with automatic rollback on errors.

### Behavior

- Deploy new version to small percentage (10%)
- Monitor metrics for canary duration
- Gradually increase traffic: 10% → 50% → 100%
- Automatic rollback if metrics degrade

### Algorithm

```csharp
public async Task<DeploymentResult> DeployCanaryAsync(Deployment deployment)
{
    var config = deployment.Config;
    var alias = await _aliasRepository.GetAsync(deployment.FunctionName, "production");
    
    // Phase 1: 10% canary
    alias.SetWeightedRouting(
        primaryVersion: deployment.SourceVersion.Value,
        canaryVersion: deployment.TargetVersion,
        canaryPercentage: config.CanaryPercentage.Value
    );
    await _aliasRepository.UpdateAsync(alias);
    await Task.Delay(config.CanaryDuration.Value);
    
    // Check metrics
    var metrics = await _metricsProvider.GetMetricsAsync(deployment.FunctionName);
    if (metrics.ErrorRate > config.RollbackOnErrorRate ||
        metrics.P99Duration > config.RollbackOnLatencyP99)
    {
        // Rollback
        alias.UpdateVersion(deployment.SourceVersion.Value);
        await _aliasRepository.UpdateAsync(alias);
        return DeploymentResult.RolledBack("Metrics threshold breached");
    }
    
    // Phase 2: 50% canary
    alias.SetWeightedRouting(deployment.SourceVersion.Value, deployment.TargetVersion, 50);
    await _aliasRepository.UpdateAsync(alias);
    await Task.Delay(config.CanaryDuration.Value);
    
    // Check metrics again
    metrics = await _metricsProvider.GetMetricsAsync(deployment.FunctionName);
    if (metrics.ErrorRate > config.RollbackOnErrorRate ||
        metrics.P99Duration > config.RollbackOnLatencyP99)
    {
        alias.UpdateVersion(deployment.SourceVersion.Value);
        await _aliasRepository.UpdateAsync(alias);
        return DeploymentResult.RolledBack("Metrics threshold breached");
    }
    
    // Phase 3: 100% canary
    alias.UpdateVersion(deployment.TargetVersion);
    await _aliasRepository.UpdateAsync(alias);
    
    return DeploymentResult.Success();
}
```

### Traffic Flow

```
Phase 1 (0-10 min):
production → v5 (90%) + v6 (10%)

Phase 2 (10-20 min):
production → v5 (50%) + v6 (50%)

Phase 3 (20-30 min):
production → v6 (100%)
```

### Configuration

```json
{
  "functionName": "image-processor",
  "version": 6,
  "strategy": "Canary",
  "config": {
    "canaryPercentage": 10,
    "canaryDuration": "PT10M",
    "canaryIncrements": [10, 50, 100],
    "rollbackOnErrorRate": 0.05,
    "rollbackOnLatencyP99": 1000,
    "rollbackOnSuccessRate": 0.95
  }
}
```

### Canary Increments

**Standard (3 phases):**
```
10% → 50% → 100%
Duration: 10 min per phase = 30 min total
```

**Conservative (5 phases):**
```
5% → 10% → 25% → 50% → 100%
Duration: 10 min per phase = 50 min total
```

**Aggressive (2 phases):**
```
25% → 100%
Duration: 5 min per phase = 10 min total
```

### Rollback Thresholds

| Metric | Threshold | Action |
|--------|-----------|--------|
| Error Rate | > 5% | Automatic rollback |
| P99 Latency | > 1000ms | Automatic rollback |
| Success Rate | < 95% | Automatic rollback |
| Cold Start Rate | > 50% | Warning (no rollback) |

### Metrics Monitoring

**Collected Metrics:**
- Invocation count
- Error count and rate
- Success rate
- P50, P95, P99 latency
- Cold start count and rate
- Memory usage

**Example Metrics Check:**
```csharp
public bool ShouldRollback(DeploymentMetrics metrics, DeploymentConfig config)
{
    if (metrics.ErrorRate > config.RollbackOnErrorRate)
    {
        _logger.LogWarning("Error rate {ErrorRate}% exceeds threshold {Threshold}%",
            metrics.ErrorRate * 100, config.RollbackOnErrorRate * 100);
        return true;
    }
    
    if (metrics.P99Duration > config.RollbackOnLatencyP99)
    {
        _logger.LogWarning("P99 latency {Latency}ms exceeds threshold {Threshold}ms",
            metrics.P99Duration, config.RollbackOnLatencyP99);
        return true;
    }
    
    if (metrics.SuccessRate < config.RollbackOnSuccessRate)
    {
        _logger.LogWarning("Success rate {SuccessRate}% below threshold {Threshold}%",
            metrics.SuccessRate * 100, config.RollbackOnSuccessRate * 100);
        return true;
    }
    
    return false;
}
```

### Timeline

```
0:00  → Deploy v6, route 10% traffic
0:10  → Check metrics → PASS → Increase to 50%
0:20  → Check metrics → PASS → Increase to 100%
0:30  → Deployment complete
```

**Rollback Scenario:**
```
0:00  → Deploy v6, route 10% traffic
0:10  → Check metrics → FAIL (error rate 8% > 5%)
0:11  → Automatic rollback to v5 (100%)
0:12  → Deployment failed, v5 restored
```

### When to Use

✅ **Good For:**
- Production critical functions
- High-traffic functions
- Major version changes
- Unverified code changes
- Risk-averse deployments

❌ **Not Good For:**
- Urgent hotfixes (too slow)
- Development environments (unnecessary)
- Low-traffic functions (insufficient metrics)

---

## 3. Blue-Green Deployment

### Overview

**Pattern:** Environment Switching
**Latency:** Moderate (5-15 minutes)
**Complexity:** Medium
**Risk:** Medium

**Use Case:** Instant rollback capability, pre-production testing.

### Behavior

- Current version (v5) is "blue" (100% traffic)
- Deploy new version (v6) to "green" environment (0% traffic)
- Test green environment internally
- Switch traffic from blue to green (instant)
- Monitor green with 100% traffic
- Keep blue for instant rollback if needed
- Decommission blue after success period

### Algorithm

```csharp
public async Task<DeploymentResult> DeployBlueGreenAsync(Deployment deployment)
{
    var config = deployment.Config;
    var alias = await _aliasRepository.GetAsync(deployment.FunctionName, "production");
    
    // Current blue version
    var blueVersion = alias.Version;
    var greenVersion = deployment.TargetVersion;
    
    // Deploy green (0% traffic initially)
    _logger.LogInformation("Deploying green version {Version}", greenVersion);
    
    // Test green internally
    await Task.Delay(config.TestDuration.Value);
    var testResults = await _testRunner.RunInternalTestsAsync(deployment.FunctionName, greenVersion);
    if (!testResults.Success)
    {
        return DeploymentResult.Failed("Internal tests failed on green version");
    }
    
    // Switch to green (instant traffic switch)
    alias.UpdateVersion(greenVersion);
    await _aliasRepository.UpdateAsync(alias);
    _logger.LogInformation("Switched traffic to green version {Version}", greenVersion);
    
    // Monitor green
    await Task.Delay(TimeSpan.FromMinutes(5));
    var metrics = await _metricsProvider.GetMetricsAsync(deployment.FunctionName);
    if (metrics.ErrorRate > 0.05)
    {
        // Rollback to blue (instant)
        alias.UpdateVersion(blueVersion);
        await _aliasRepository.UpdateAsync(alias);
        _logger.LogWarning("Rolled back to blue version {Version}", blueVersion);
        return DeploymentResult.RolledBack("High error rate on green");
    }
    
    // Success - keep blue for 1 hour, then decommission
    if (config.KeepBlue.GetValueOrDefault(true))
    {
        await Task.Delay(TimeSpan.FromHours(1));
    }
    
    return DeploymentResult.Success();
}
```

### Traffic Flow

```
Initial State:
blue (v5)  → 100% traffic
green (v6) → 0% traffic (testing)

After Switch:
blue (v5)  → 0% traffic (kept for rollback)
green (v6) → 100% traffic

After Success:
green (v6) → 100% traffic
blue (v5)  → decommissioned
```

### Configuration

```json
{
  "functionName": "image-processor",
  "version": 6,
  "strategy": "BlueGreen",
  "config": {
    "testDuration": "PT5M",
    "switchType": "Instant",
    "keepBlue": true,
    "keepBlueDuration": "PT1H"
  }
}
```

### Timeline

```
0:00  → Deploy green (v6) with 0% traffic
0:05  → Run internal tests on green
0:10  → Tests pass → Switch to green (100% traffic)
0:15  → Monitor green metrics
1:10  → Success → Decommission blue (v5)
```

**Rollback Scenario:**
```
0:00  → Deploy green (v6) with 0% traffic
0:05  → Run internal tests on green
0:10  → Tests pass → Switch to green (100% traffic)
0:11  → High error rate detected
0:12  → Instant switch back to blue (v5)
```

### Internal Testing

**Test Suite:**
```csharp
public async Task<TestResult> RunInternalTestsAsync(string functionName, int version)
{
    // 1. Smoke test (invoke with sample payload)
    var smokeTest = await InvokeAsync(functionName, version, samplePayload);
    if (!smokeTest.Success) return TestResult.Failed("Smoke test failed");
    
    // 2. Load test (100 requests)
    var loadTest = await RunLoadTestAsync(functionName, version, requestCount: 100);
    if (loadTest.ErrorRate > 0.01) return TestResult.Failed("Load test error rate too high");
    
    // 3. Schema validation (if applicable)
    var schemaTest = await ValidateSchemaAsync(functionName, version);
    if (!schemaTest.Success) return TestResult.Failed("Schema validation failed");
    
    return TestResult.Success();
}
```

### When to Use

✅ **Good For:**
- Production functions requiring instant rollback
- Database migration functions (test on green first)
- Functions with complex integration tests
- Risk-averse deployments with thorough pre-testing

❌ **Not Good For:**
- Functions requiring gradual rollout (use Canary)
- Resource-constrained environments (double infrastructure)
- Stateful functions with migration concerns

---

## 4. Rolling Deployment

### Overview

**Pattern:** Progressive Node Updates
**Latency:** Moderate (15-30 minutes)
**Complexity:** Medium
**Risk:** Medium

**Use Case:** Large-scale updates across many runner nodes.

### Behavior

- Identify all runner nodes (e.g., 8 nodes)
- Update 25% of nodes (2 nodes) to new version
- Monitor metrics for batch duration (5 min)
- If metrics good → update next 25%
- Repeat until all nodes updated
- If metrics bad → stop rollout, rollback

### Algorithm

```csharp
public async Task<DeploymentResult> DeployRollingAsync(Deployment deployment)
{
    var config = deployment.Config;
    var runners = await _runnerRepository.GetAllHealthyAsync();
    var batchSize = (int)Math.Ceiling(runners.Count * (config.BatchSize.Value / 100.0));
    
    for (int i = 0; i < runners.Count; i += batchSize)
    {
        var batch = runners.Skip(i).Take(batchSize).ToList();
        
        // Update batch of runners
        foreach (var runner in batch)
        {
            await _runnerManager.UpdateFunctionVersionAsync(
                runner.NodeId,
                deployment.FunctionName,
                deployment.TargetVersion
            );
        }
        
        deployment.Progress = (i + batchSize) / (double)runners.Count * 100;
        await _deploymentRepository.UpdateAsync(deployment);
        
        // Wait for batch duration
        await Task.Delay(config.BatchDuration.Value);
        
        // Check metrics
        var metrics = await _metricsProvider.GetMetricsAsync(deployment.FunctionName);
        if (metrics.ErrorRate > 0.05)
        {
            // Rollback updated runners
            foreach (var runner in runners.Take(i + batchSize))
            {
                await _runnerManager.UpdateFunctionVersionAsync(
                    runner.NodeId,
                    deployment.FunctionName,
                    deployment.SourceVersion.Value
                );
            }
            return DeploymentResult.RolledBack("High error rate detected");
        }
    }
    
    return DeploymentResult.Success();
}
```

### Traffic Flow

```
Initial State (8 runners):
Runners 1-8 → v5

After Batch 1 (25%):
Runners 1-2 → v6
Runners 3-8 → v5

After Batch 2 (50%):
Runners 1-4 → v6
Runners 5-8 → v5

After Batch 3 (75%):
Runners 1-6 → v6
Runners 7-8 → v5

After Batch 4 (100%):
Runners 1-8 → v6
```

### Configuration

```json
{
  "functionName": "image-processor",
  "version": 6,
  "strategy": "Rolling",
  "config": {
    "batchSize": 25,
    "batchDuration": "PT5M",
    "rollbackOnFailure": true
  }
}
```

### Timeline

```
0:00  → Update runners 1-2 (25%)
0:05  → Check metrics → PASS → Update runners 3-4 (50%)
0:10  → Check metrics → PASS → Update runners 5-6 (75%)
0:15  → Check metrics → PASS → Update runners 7-8 (100%)
0:20  → Deployment complete
```

**Rollback Scenario:**
```
0:00  → Update runners 1-2 (25%)
0:05  → Check metrics → PASS → Update runners 3-4 (50%)
0:10  → Check metrics → FAIL (error rate 7%)
0:11  → Rollback runners 1-4 to v5
0:16  → Rollback complete
```

### Batch Sizing

| Runners | Batch Size | Batches | Duration (5 min/batch) |
|---------|------------|---------|------------------------|
| 4 | 25% (1) | 4 | 20 minutes |
| 8 | 25% (2) | 4 | 20 minutes |
| 16 | 25% (4) | 4 | 20 minutes |
| 8 | 50% (4) | 2 | 10 minutes |
| 8 | 12.5% (1) | 8 | 40 minutes |

### When to Use

✅ **Good For:**
- Large-scale deployments (many runners)
- Functions with stateful runners
- Progressive updates with safety checks
- Minimizing blast radius

❌ **Not Good For:**
- Single runner deployments (use All-At-Once)
- Functions requiring version consistency across runners
- Urgent deployments (too slow)

---

## 5. A/B Testing Deployment

### Overview

**Pattern:** Split Testing
**Latency:** Variable (hours to days)
**Complexity:** High
**Risk:** Low

**Use Case:** Compare two versions for performance, features, or business metrics.

### Behavior

- Deploy v1 (current) and v2 (new) simultaneously
- Split traffic: 50% v1, 50% v2 (or custom split)
- Run both for test duration (1 hour, 1 day, etc.)
- Compare metrics:
  - Technical: response time, error rate, resource usage
  - Business: conversion rate, user engagement, revenue
- Manual decision to promote winner
- Decommission loser

### Algorithm

```csharp
public async Task<DeploymentResult> DeployABTestAsync(Deployment deployment)
{
    var config = deployment.Config;
    var alias = await _aliasRepository.GetAsync(deployment.FunctionName, "production");
    
    // Set up A/B split
    alias.SetWeightedRouting(
        primaryVersion: deployment.SourceVersion.Value,
        canaryVersion: deployment.TargetVersion,
        canaryPercentage: 50 // 50/50 split
    );
    await _aliasRepository.UpdateAsync(alias);
    
    // Run test for duration
    await Task.Delay(config.TestDuration.Value);
    
    // Collect metrics for both versions
    var metricsV1 = await _metricsProvider.GetMetricsAsync(
        deployment.FunctionName, deployment.SourceVersion.Value);
    var metricsV2 = await _metricsProvider.GetMetricsAsync(
        deployment.FunctionName, deployment.TargetVersion);
    
    // Store comparison results
    deployment.Metrics = new DeploymentMetrics
    {
        // Store both version metrics for comparison
    };
    await _deploymentRepository.UpdateAsync(deployment);
    
    // Manual promotion (require explicit API call)
    deployment.Status = DeploymentStatus.AwaitingPromotion;
    return DeploymentResult.AwaitingManualPromotion();
}
```

### Traffic Flow

```
Test Period (1 hour - 1 day):
production → v1 (50%) + v2 (50%)

After Manual Promotion (v2 wins):
production → v2 (100%)
```

### Configuration

```json
{
  "functionName": "recommendation-engine",
  "version": 2,
  "strategy": "ABTesting",
  "config": {
    "versionA": 1,
    "versionB": 2,
    "trafficSplit": {
      "1": 50,
      "2": 50
    },
    "testDuration": "PT24H",
    "manualPromotion": true,
    "metricsToCompare": [
      "responseTime",
      "errorRate",
      "conversionRate",
      "userEngagement"
    ]
  }
}
```

### Metrics Comparison

| Metric | Version A (v1) | Version B (v2) | Winner |
|--------|----------------|----------------|--------|
| Avg Response Time | 145ms | 132ms | v2 ✓ |
| Error Rate | 0.2% | 0.3% | v1 ✓ |
| Conversion Rate | 2.5% | 2.8% | v2 ✓ |
| User Engagement | 65% | 70% | v2 ✓ |

**Decision:** Promote v2 (wins 3/4 metrics)

### Manual Promotion

```http
POST /api/v1/deployments/{id}/promote
Authorization: Bearer {token}
Content-Type: application/json

{
  "winningVersion": 2,
  "reason": "v2 shows 2.8% conversion rate vs 2.5% for v1"
}
```

### When to Use

✅ **Good For:**
- Testing new algorithms or features
- Measuring business impact (conversion, revenue)
- Comparing performance of different implementations
- Data-driven decision making

❌ **Not Good For:**
- Bug fixes (use Canary)
- Security patches (use All-At-Once)
- Backward-incompatible changes

---

## Strategy Comparison

| Strategy | Deployment Time | Risk | Rollback Speed | Complexity | Use Case |
|----------|----------------|------|----------------|------------|----------|
| All-At-Once | ⭐⭐⭐⭐⭐ | ⭐ | ⭐⭐⭐ | ⭐ | Dev/staging |
| Canary | ⭐⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐⭐⭐ | Production |
| Blue-Green | ⭐⭐⭐⭐ | ⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐⭐⭐ | Critical functions |
| Rolling | ⭐⭐⭐ | ⭐⭐⭐⭐ | ⭐⭐⭐ | ⭐⭐⭐ | Large-scale |
| A/B Testing | ⭐ | ⭐⭐⭐⭐⭐ | ⭐⭐ | ⭐⭐⭐⭐⭐ | Feature testing |

---

## Best Practices

### 1. Choose the Right Strategy

**Decision Tree:**
```
Is this a production deployment?
├─ Yes → Is it a critical function?
│  ├─ Yes → Blue-Green (instant rollback)
│  └─ No → Canary (gradual with auto-rollback)
└─ No → All-At-Once (fastest)

Do you need to compare versions?
└─ Yes → A/B Testing

Do you have many runner nodes?
└─ Yes → Rolling
```

### 2. Monitor During Deployments

Track these metrics:
- Error rate (target: < 1%)
- P99 latency (target: < 500ms)
- Success rate (target: > 99%)
- Cold start rate
- Resource usage (CPU, memory)

### 3. Define Rollback Thresholds

**Conservative (production):**
```json
{
  "rollbackOnErrorRate": 0.01,
  "rollbackOnLatencyP99": 500,
  "rollbackOnSuccessRate": 0.99
}
```

**Moderate (staging):**
```json
{
  "rollbackOnErrorRate": 0.05,
  "rollbackOnLatencyP99": 1000,
  "rollbackOnSuccessRate": 0.95
}
```

### 4. Test Deployments in Staging

Always test deployment strategies in staging first:
1. Deploy to staging with same strategy
2. Verify metrics collection working
3. Verify rollback triggers working
4. Then deploy to production

---

## Troubleshooting

### Issue: Canary Not Rolling Back

**Symptom:** Metrics exceed thresholds but no rollback

**Solutions:**
1. Check metrics provider is configured correctly
2. Verify rollback thresholds are set
3. Check deployment logs for errors
4. Manually trigger rollback if needed

### Issue: Blue-Green Switch Failed

**Symptom:** Traffic switch didn't complete

**Solutions:**
1. Check alias update succeeded
2. Verify runner nodes have new version
3. Check for network issues
4. Manually update alias if needed

### Issue: Rolling Deployment Stuck

**Symptom:** Deployment stuck at partial completion

**Solutions:**
1. Check runner node health
2. Verify all runners reachable
3. Check for resource constraints
4. Complete or rollback deployment manually

---

**Last Updated:** 2025-11-23
**Version:** 1.0.0
