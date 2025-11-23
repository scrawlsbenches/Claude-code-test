# Policy Deployment Strategies Guide

**Version:** 1.0.0
**Last Updated:** 2025-11-23

---

## Overview

The HotSwap Service Mesh Policy Manager supports 5 deployment strategies, each adapted from the existing deployment strategies in the kernel orchestration platform. These strategies determine how policies are rolled out to service mesh clusters, with safety guarantees and automatic rollback capabilities.

### Strategy Selection

| Use Case | Recommended Strategy | Rollout Time | Risk Level |
|----------|---------------------|--------------|------------|
| Development/Testing | Direct | Instant | Low |
| Production (low risk) | Rolling | 5-10 minutes | Low |
| Production (standard) | Canary | 15-30 minutes | Medium |
| Production (high confidence) | Blue-Green | 2-5 minutes | Medium |
| Comparative Testing | A/B Testing | Variable | High |

---

## 1. Direct Deployment Strategy

### Overview

**Pattern:** Immediate Full Rollout
**Latency:** < 5 seconds
**Complexity:** Low
**Risk:** High

**Use Case:** Development/testing environments where immediate feedback is needed.

### Behavior

- Deploys policy to **100% of instances** immediately
- No gradual rollout
- Fastest deployment path
- Used for non-production environments

### Algorithm

```csharp
public async Task<DeploymentResult> DeployAsync(Policy policy, ServiceMeshCluster cluster)
{
    // Validate policy
    var validation = await _validator.ValidateAsync(policy);
    if (!validation.IsValid)
        return DeploymentResult.Failure("Policy validation failed");

    // Apply policy to all instances
    await _meshAdapter.ApplyPolicyAsync(policy, cluster);

    // Verify deployment
    var deployed = await _meshAdapter.VerifyPolicyAsync(policy, cluster);
    if (!deployed)
        return DeploymentResult.Failure("Policy deployment verification failed");

    return DeploymentResult.Success(cluster.InstanceCount);
}
```

### Deployment Flow

```
1. Validate policy
2. Apply to all service instances (0% → 100%)
3. Verify deployment
4. Monitor metrics (5 minutes)
5. Complete or rollback
```

### Configuration

```json
{
  "policyId": "pol-123",
  "environment": "Development",
  "clusterId": "dev-cluster",
  "strategy": "Direct",
  "config": {
    "monitoringPeriod": "PT5M"
  }
}
```

### Performance Characteristics

- **Deployment Time:** < 5 seconds
- **Rollback Time:** < 10 seconds
- **Risk:** High (all instances affected immediately)
- **Suitable For:** Dev, Testing

### When to Use

✅ **Good For:**
- Development environments
- Testing/QA environments
- Low-traffic services
- Emergency fixes (with approval)

❌ **Not Good For:**
- Production environments
- High-traffic services
- Untested policy changes

---

## 2. Canary Deployment Strategy

### Overview

**Pattern:** Gradual Percentage Rollout
**Latency:** 15-30 minutes
**Complexity:** Medium
**Risk:** Low-Medium

**Use Case:** Production deployments where gradual rollout with monitoring is required.

### Behavior

- Routes specified percentage of traffic to new policy
- Monitor metrics at each stage
- Auto-promote or rollback based on metrics
- Standard production deployment strategy

### Canary Stages

```
10% → Monitor 5 min → 30% → Monitor 5 min → 50% → Monitor 5 min → 100%
```

### Algorithm

```csharp
public class CanaryDeploymentStrategy : IDeploymentStrategy
{
    private readonly int[] _canaryStages = { 10, 30, 50, 100 };
    private readonly TimeSpan _monitoringPeriod = TimeSpan.FromMinutes(5);

    public async Task<DeploymentResult> DeployAsync(Policy policy, ServiceMeshCluster cluster)
    {
        var baseline = await _metricsCollector.CollectMetricsAsync(cluster);

        foreach (var percentage in _canaryStages)
        {
            // Update traffic split
            await _meshAdapter.SetTrafficSplitAsync(policy, cluster, percentage);

            // Monitor for soak period
            await Task.Delay(_monitoringPeriod);

            // Collect canary metrics
            var canaryMetrics = await _metricsCollector.CollectMetricsAsync(cluster);

            // Check for degradation
            if (canaryMetrics.HasDegraded(baseline))
            {
                await RollbackAsync(policy, cluster);
                return DeploymentResult.Failure($"Metrics degraded at {percentage}% canary");
            }

            _logger.LogInformation($"Canary at {percentage}% successful, promoting...");
        }

        return DeploymentResult.Success(cluster.InstanceCount);
    }
}
```

### Deployment Flow

```
1. Collect baseline metrics
2. Deploy to 10% of instances
3. Monitor for 5 minutes
   - If metrics OK → promote to 30%
   - If metrics degraded → rollback
4. Repeat for 30%, 50%, 100%
5. Complete deployment
```

### Traffic Split Example (Istio)

```yaml
apiVersion: networking.istio.io/v1beta1
kind: VirtualService
metadata:
  name: user-service-canary
spec:
  hosts:
  - user-service
  http:
  - match:
    - headers:
        x-canary:
          exact: "true"
    route:
    - destination:
        host: user-service
        subset: canary
      weight: 10  # 10% canary
    - destination:
        host: user-service
        subset: stable
      weight: 90  # 90% stable
```

### Configuration

```json
{
  "policyId": "pol-123",
  "environment": "Production",
  "clusterId": "prod-us-east-1",
  "strategy": "Canary",
  "config": {
    "canaryPercentage": "10",
    "stages": [10, 30, 50, 100],
    "monitoringPeriod": "PT5M",
    "autoPromote": "true",
    "rollbackOnErrorRate": "5",
    "rollbackOnLatencyIncrease": "50"
  }
}
```

### Rollback Triggers

**Automatic Rollback When:**
- Error rate > 5% (baseline + 5%)
- P95 latency increase > 50%
- Connection failures > 10%
- Circuit breaker trips > threshold

### Performance Characteristics

- **Deployment Time:** 15-30 minutes (depends on stages)
- **Rollback Time:** < 10 seconds
- **Risk:** Low (limited blast radius)
- **Suitable For:** Production, High-traffic services

### When to Use

✅ **Good For:**
- Production deployments
- High-traffic services
- New policy types
- Critical services

❌ **Not Good For:**
- Emergency fixes (too slow)
- Development environments
- Services with low traffic (insufficient metrics)

---

## 3. Blue-Green Deployment Strategy

### Overview

**Pattern:** Instant Switch
**Latency:** 2-5 minutes
**Complexity:** Medium
**Risk:** Medium

**Use Case:** High-confidence changes where instant rollback is needed.

### Behavior

- Deploy new policy to "green" environment
- Keep existing policy in "blue" environment
- Validate green environment
- Instant traffic switch from blue to green
- Instant rollback to blue if issues detected

### Deployment Flow

```
1. Deploy policy to green environment (0% traffic)
2. Run smoke tests on green
3. Validate green environment (health checks, metrics)
4. Switch 100% traffic to green (instant)
5. Monitor green for 10 minutes
6. Decommission blue (or keep as rollback target)
```

### Algorithm

```csharp
public async Task<DeploymentResult> DeployAsync(Policy policy, ServiceMeshCluster cluster)
{
    // Deploy to green environment (no traffic)
    await _meshAdapter.DeployToGreenAsync(policy, cluster);

    // Run smoke tests
    var smokeTests = await RunSmokeTestsAsync(cluster);
    if (!smokeTests.Passed)
    {
        await CleanupGreenAsync(cluster);
        return DeploymentResult.Failure("Smoke tests failed on green");
    }

    // Validate green environment
    var validation = await ValidateGreenAsync(cluster);
    if (!validation.IsHealthy)
    {
        await CleanupGreenAsync(cluster);
        return DeploymentResult.Failure("Green environment validation failed");
    }

    // Instant switch to green
    await _meshAdapter.SwitchTrafficToGreenAsync(cluster);

    // Monitor for 10 minutes
    var metrics = await MonitorGreenAsync(cluster, TimeSpan.FromMinutes(10));
    if (metrics.HasDegraded())
    {
        await _meshAdapter.SwitchTrafficToBlueAsync(cluster);
        return DeploymentResult.Failure("Metrics degraded on green, rolled back to blue");
    }

    // Decommission blue
    await _meshAdapter.DecommissionBlueAsync(cluster);

    return DeploymentResult.Success(cluster.InstanceCount);
}
```

### Traffic Switch Example (Istio)

```yaml
# Before switch (blue active)
apiVersion: networking.istio.io/v1beta1
kind: VirtualService
metadata:
  name: user-service
spec:
  hosts:
  - user-service
  http:
  - route:
    - destination:
        host: user-service
        subset: blue
      weight: 100

---

# After switch (green active)
apiVersion: networking.istio.io/v1beta1
kind: VirtualService
metadata:
  name: user-service
spec:
  hosts:
  - user-service
  http:
  - route:
    - destination:
        host: user-service
        subset: green
      weight: 100
```

### Configuration

```json
{
  "policyId": "pol-123",
  "environment": "Production",
  "clusterId": "prod-us-east-1",
  "strategy": "BlueGreen",
  "config": {
    "smokeTestsEnabled": "true",
    "validationPeriod": "PT2M",
    "monitoringPeriod": "PT10M",
    "keepBlueForRollback": "true",
    "blueRetentionPeriod": "PT1H"
  }
}
```

### Performance Characteristics

- **Deployment Time:** 2-5 minutes
- **Switch Time:** < 5 seconds (instant)
- **Rollback Time:** < 5 seconds (instant)
- **Risk:** Medium (all instances switched at once)
- **Suitable For:** Production, High-confidence changes

### When to Use

✅ **Good For:**
- High-confidence policy changes
- Well-tested policies
- Services with good monitoring
- When instant rollback is critical

❌ **Not Good For:**
- Unproven policy changes
- First-time policy deployments
- Resource-constrained environments (2x resources)

---

## 4. Rolling Deployment Strategy

### Overview

**Pattern:** Instance-by-Instance Rollout
**Latency:** 5-10 minutes
**Complexity:** Low-Medium
**Risk:** Low

**Use Case:** Large fleets where progressive rollout is needed.

### Behavior

- Update instances one-by-one or in small batches
- Wait for health check before proceeding to next instance
- Gradual rollout across entire fleet
- Minimal blast radius at any point

### Rolling Stages

```
Instance 1 → Wait → Instance 2 → Wait → ... → Instance N
```

### Algorithm

```csharp
public async Task<DeploymentResult> DeployAsync(Policy policy, ServiceMeshCluster cluster)
{
    var instances = await _meshAdapter.GetServiceInstancesAsync(cluster);
    var batchSize = Math.Max(1, instances.Count / 10); // 10% per batch

    for (int i = 0; i < instances.Count; i += batchSize)
    {
        var batch = instances.Skip(i).Take(batchSize).ToList();

        // Apply policy to batch
        foreach (var instance in batch)
        {
            await _meshAdapter.ApplyPolicyToInstanceAsync(policy, instance);

            // Wait for health check
            var healthy = await WaitForHealthyAsync(instance, TimeSpan.FromSeconds(30));
            if (!healthy)
            {
                await RollbackAsync(policy, cluster);
                return DeploymentResult.Failure($"Instance {instance.Name} failed health check");
            }
        }

        // Monitor batch for 1 minute
        await Task.Delay(TimeSpan.FromMinutes(1));
    }

    return DeploymentResult.Success(instances.Count);
}
```

### Deployment Flow

```
1. Get all service instances
2. Calculate batch size (10% of total)
3. For each batch:
   a. Apply policy to batch
   b. Wait for health checks
   c. Monitor for 1 minute
   d. If OK, proceed to next batch
   e. If issues, rollback all
4. Complete deployment
```

### Configuration

```json
{
  "policyId": "pol-123",
  "environment": "Production",
  "clusterId": "prod-us-east-1",
  "strategy": "Rolling",
  "config": {
    "batchSize": "10",
    "batchDelay": "PT1M",
    "healthCheckTimeout": "PT30S",
    "maxConcurrent": "5"
  }
}
```

### Performance Characteristics

- **Deployment Time:** 5-10 minutes (100 instances)
- **Rollback Time:** < 30 seconds
- **Risk:** Low (limited instances affected at once)
- **Suitable For:** Large fleets, Production

### When to Use

✅ **Good For:**
- Large service fleets (100+ instances)
- Production environments
- Services with long startup times
- Progressive rollouts

❌ **Not Good For:**
- Small fleets (< 10 instances, use Canary)
- Emergency fixes (too slow)
- Services requiring instant updates

---

## 5. A/B Testing Deployment Strategy

### Overview

**Pattern:** Comparative Testing
**Latency:** Variable
**Complexity:** High
**Risk:** Medium

**Use Case:** Comparative testing of two policy versions to determine the better option.

### Behavior

- Deploy two policy versions (A and B) simultaneously
- Split traffic 50%-50% (or custom split)
- Collect metrics for both versions
- Compare performance metrics
- Promote winning version to 100%

### Deployment Flow

```
1. Deploy policy A (current) - 50% traffic
2. Deploy policy B (new) - 50% traffic
3. Monitor both for test period (e.g., 1 hour)
4. Collect and compare metrics:
   - Error rate
   - Latency (P50, P95, P99)
   - Throughput
   - Resource usage
5. Determine winner (better metrics)
6. Promote winner to 100%
7. Decommission loser
```

### Algorithm

```csharp
public async Task<DeploymentResult> DeployAsync(Policy policyA, Policy policyB, ServiceMeshCluster cluster)
{
    // Deploy both policies with 50-50 split
    await _meshAdapter.DeployWithTrafficSplitAsync(policyA, policyB, cluster, 50, 50);

    // Monitor for test period
    var testPeriod = TimeSpan.FromHours(1);
    await Task.Delay(testPeriod);

    // Collect metrics for both
    var metricsA = await _metricsCollector.CollectMetricsAsync(cluster, "policy-a");
    var metricsB = await _metricsCollector.CollectMetricsAsync(cluster, "policy-b");

    // Determine winner
    var winner = DetermineWinner(metricsA, metricsB);

    // Promote winner to 100%
    if (winner == "A")
    {
        await _meshAdapter.PromotePolicyAsync(policyA, cluster);
        return DeploymentResult.Success($"Policy A won with better metrics");
    }
    else
    {
        await _meshAdapter.PromotePolicyAsync(policyB, cluster);
        return DeploymentResult.Success($"Policy B won with better metrics");
    }
}

private string DetermineWinner(TrafficMetrics metricsA, TrafficMetrics metricsB)
{
    // Compare metrics (lower error rate, lower latency, higher throughput = better)
    var scoreA = CalculateScore(metricsA);
    var scoreB = CalculateScore(metricsB);

    return scoreA > scoreB ? "A" : "B";
}
```

### Traffic Split Example (Istio)

```yaml
apiVersion: networking.istio.io/v1beta1
kind: VirtualService
metadata:
  name: user-service-ab-test
spec:
  hosts:
  - user-service
  http:
  - match:
    - headers:
        x-user-id:
          regex: "^[0-4].*"  # Users starting with 0-4 → variant A
    route:
    - destination:
        host: user-service
        subset: variant-a
  - match:
    - headers:
        x-user-id:
          regex: "^[5-9].*"  # Users starting with 5-9 → variant B
    route:
    - destination:
        host: user-service
        subset: variant-b
```

### Configuration

```json
{
  "policyId": "pol-123",
  "environment": "Production",
  "clusterId": "prod-us-east-1",
  "strategy": "ABTesting",
  "config": {
    "policyAId": "pol-123",
    "policyBId": "pol-456",
    "trafficSplitA": "50",
    "trafficSplitB": "50",
    "testDuration": "PT1H",
    "comparisonMetrics": ["errorRate", "p95Latency", "throughput"],
    "autoPromoteWinner": "true"
  }
}
```

### Metrics Comparison

| Metric | Weight | Policy A | Policy B | Winner |
|--------|--------|----------|----------|--------|
| Error Rate | 40% | 0.5% | 0.3% | B |
| P95 Latency | 30% | 120ms | 150ms | A |
| Throughput | 20% | 1200 RPS | 1250 RPS | B |
| Resource Usage | 10% | 60% CPU | 55% CPU | B |

**Overall Winner:** Policy B (weighted score)

### Performance Characteristics

- **Deployment Time:** Variable (1 hour test period)
- **Switch Time:** < 10 seconds
- **Risk:** Medium (50% of traffic affected)
- **Suitable For:** Performance optimization, Feature testing

### When to Use

✅ **Good For:**
- Performance optimization comparisons
- Testing different configurations
- Determining best policy parameters
- Feature flag testing

❌ **Not Good For:**
- Standard deployments (use Canary)
- Emergency fixes
- Single policy deployments

---

## Strategy Comparison

| Strategy | Deployment Time | Rollback Time | Risk | Complexity | Use Case |
|----------|----------------|---------------|------|------------|----------|
| Direct | ⭐⭐⭐⭐⭐ (< 5s) | ⭐⭐⭐⭐ (< 10s) | ⭐ (High) | ⭐ (Low) | Dev/Testing |
| Canary | ⭐⭐ (15-30min) | ⭐⭐⭐⭐⭐ (< 10s) | ⭐⭐⭐⭐⭐ (Low) | ⭐⭐⭐ (Medium) | Production Standard |
| Blue-Green | ⭐⭐⭐⭐ (2-5min) | ⭐⭐⭐⭐⭐ (< 5s) | ⭐⭐⭐ (Medium) | ⭐⭐⭐ (Medium) | High Confidence |
| Rolling | ⭐⭐⭐ (5-10min) | ⭐⭐⭐⭐ (< 30s) | ⭐⭐⭐⭐ (Low) | ⭐⭐ (Low-Med) | Large Fleets |
| A/B Testing | ⭐ (Variable) | ⭐⭐⭐⭐ (< 10s) | ⭐⭐⭐ (Medium) | ⭐⭐⭐⭐⭐ (High) | Comparative Testing |

---

## Best Practices

### 1. Choose the Right Strategy

**Decision Tree:**
```
Is this production?
├─ No → Direct
└─ Yes
   ├─ Large fleet (100+ instances)? → Rolling
   ├─ Testing two variants? → A/B Testing
   ├─ High confidence change? → Blue-Green
   └─ Standard deployment → Canary (default)
```

### 2. Monitor Strategy Performance

Track metrics per strategy:
- Deployment success rate
- Average deployment duration
- Rollback frequency
- Metrics degradation incidents

### 3. Define Rollback Triggers

**Standard Rollback Triggers:**
- Error rate > baseline + 5%
- P95 latency > baseline × 1.5
- Connection failures > baseline × 2
- Circuit breaker trips > 100/min

### 4. Test Strategy Switching

Ensure zero downtime when changing strategies:
1. Complete current deployment
2. Switch to new strategy
3. Deploy next policy

---

**Last Updated:** 2025-11-23
**Version:** 1.0.0
