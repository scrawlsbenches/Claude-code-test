# Operator Deployment Strategies Guide

**Version:** 1.0.0
**Last Updated:** 2025-11-23

---

## Overview

The Kubernetes Operator Manager supports 4 deployment strategies for safely rolling out operator updates across clusters. Each strategy provides different trade-offs between deployment speed, risk mitigation, and operational complexity.

### Strategy Selection

| Cluster Environment | Default Strategy | Use Case |
|---------------------|------------------|----------|
| Development | Direct | Fast iteration, minimal validation |
| Staging | Rolling | Environment progression testing |
| Production | Canary | Progressive rollout with risk mitigation |
| Critical Production | Blue-Green | Zero-downtime with instant rollback |

---

## 1. Direct Deployment Strategy

### Overview

**Pattern:** Immediate deployment to all clusters
**Risk:** High
**Speed:** Fastest
**Complexity:** Low

**Use Case:** Development and testing environments where fast iteration is more important than safety.

### Behavior

- Deploy operator to **all target clusters simultaneously**
- Minimal health validation
- No staged rollout
- Fastest time to deployment

### Algorithm

```csharp
public async Task<DeploymentResult> DeployAsync(
    OperatorDeployment deployment,
    List<KubernetesCluster> clusters)
{
    // Deploy to all clusters in parallel
    var deploymentTasks = clusters
        .Select(cluster => DeployToClusterAsync(deployment, cluster))
        .ToList();

    await Task.WhenAll(deploymentTasks);

    // Optional: Quick health check
    if (!deployment.SkipHealthChecks)
    {
        await ValidateHealthAsync(clusters);
    }

    return DeploymentResult.SuccessResult(
        clusters.Select(c => c.Name).ToList()
    );
}
```

### Deployment Flow

```
All Clusters (parallel execution)
├─ dev-cluster-1     → Deploy v2.0
├─ dev-cluster-2     → Deploy v2.0
├─ dev-cluster-3     → Deploy v2.0
└─ test-cluster      → Deploy v2.0

All deployments execute simultaneously
Wait for Helm install completion (2-3 minutes)
Optional health check
Complete
```

### Configuration

```json
{
  "deploymentId": "deploy-123",
  "operatorName": "cert-manager",
  "targetVersion": "v1.14.0",
  "strategy": "Direct",
  "directConfig": {
    "parallelClusters": 10,
    "skipHealthChecks": false,
    "timeout": "PT5M"
  },
  "targetClusters": [
    "dev-1",
    "dev-2",
    "test-1"
  ]
}
```

### Performance Characteristics

- **Deployment Time:** 2-3 minutes (single cluster time)
- **Total Time:** Same as single cluster (parallel execution)
- **Risk:** High (all clusters affected simultaneously)
- **Rollback Complexity:** High (must rollback all clusters)

### When to Use

✅ **Good For:**
- Development environments
- Testing and experimentation
- Non-production clusters
- Operators with minimal risk

❌ **Not Good For:**
- Production environments
- Critical operators (cert-manager, ingress controllers)
- Operators with CRD breaking changes
- Large operator deployments (50+ clusters)

---

## 2. Canary Deployment Strategy

### Overview

**Pattern:** Progressive percentage-based rollout
**Risk:** Low
**Speed:** Moderate
**Complexity:** High

**Use Case:** Production environments requiring gradual validation and risk mitigation.

### Behavior

- Deploy to small percentage of clusters initially
- Validate health metrics and operator performance
- Gradually increase percentage based on success
- Automatic rollback on failures

### Algorithm

```csharp
public async Task<DeploymentResult> DeployAsync(
    OperatorDeployment deployment,
    List<KubernetesCluster> clusters)
{
    var config = deployment.GetCanaryConfig();
    var stages = CalculateCanaryStages(clusters, config);

    foreach (var stage in stages)
    {
        _logger.LogInformation(
            "Canary Stage: Deploying to {Percentage}% of clusters ({Count} clusters)",
            stage.Percentage, stage.Clusters.Count
        );

        // Deploy to stage clusters
        foreach (var cluster in stage.Clusters)
        {
            await DeployToClusterAsync(deployment, cluster);
        }

        // Wait for evaluation period
        await Task.Delay(config.EvaluationPeriod);

        // Validate health
        var healthResults = await ValidateStageHealthAsync(stage.Clusters);
        var successRate = healthResults.GetSuccessRate();

        if (successRate < config.SuccessThreshold)
        {
            _logger.LogWarning(
                "Canary stage failed: Success rate {Rate}% < threshold {Threshold}%",
                successRate * 100, config.SuccessThreshold * 100
            );

            await RollbackAsync(deployment, stage.Clusters);
            return DeploymentResult.Failure($"Canary validation failed at {stage.Percentage}%");
        }

        _logger.LogInformation(
            "Canary stage passed: Success rate {Rate}%",
            successRate * 100
        );
    }

    return DeploymentResult.SuccessResult(clusters.Select(c => c.Name).ToList());
}

private List<CanaryStage> CalculateCanaryStages(
    List<KubernetesCluster> clusters,
    CanaryConfig config)
{
    var stages = new List<CanaryStage>();
    var totalClusters = clusters.Count;
    var deployedCount = 0;
    var percentage = config.InitialPercentage;

    while (deployedCount < totalClusters)
    {
        var clusterCount = (int)Math.Ceiling(totalClusters * (percentage / 100.0));
        clusterCount = Math.Min(clusterCount, totalClusters - deployedCount);

        var stageClusters = clusters
            .Skip(deployedCount)
            .Take(clusterCount)
            .ToList();

        stages.Add(new CanaryStage
        {
            Percentage = percentage,
            Clusters = stageClusters
        });

        deployedCount += clusterCount;
        percentage = Math.Min(percentage + config.IncrementPercentage, 100);
    }

    return stages;
}
```

### Deployment Flow

```
Stage 1: 10% of clusters (e.g., 2 out of 20 clusters)
├─ dev-cluster-1     → Deploy v2.0
├─ dev-cluster-2     → Deploy v2.0
├─ Wait 5 minutes (evaluation period)
├─ Health check validation
│   ├─ Controller pods: Healthy ✓
│   ├─ Webhook endpoints: Healthy ✓
│   ├─ CRD reconciliation: Healthy ✓
│   └─ Success rate: 100% >= 95% threshold ✓
└─ Continue to Stage 2

Stage 2: 30% of clusters (6 clusters total)
├─ staging-cluster-1 → Deploy v2.0
├─ staging-cluster-2 → Deploy v2.0
├─ staging-cluster-3 → Deploy v2.0
├─ staging-cluster-4 → Deploy v2.0
├─ Wait 5 minutes
├─ Health check validation
│   └─ Success rate: 100% >= 95% threshold ✓
└─ Continue to Stage 3

Stage 3: 60% of clusters (12 clusters total)
├─ prod-us-east-1    → Deploy v2.0
├─ prod-us-east-2    → Deploy v2.0
├─ ... (6 more clusters)
├─ Wait 5 minutes
├─ Health check validation
│   └─ Success rate: 100% >= 95% threshold ✓
└─ Continue to Stage 4

Stage 4: 100% of clusters (20 clusters total)
├─ Deploy to remaining 8 clusters
├─ Wait 5 minutes
├─ Final health validation
└─ Deployment Complete ✓
```

### Configuration

```json
{
  "deploymentId": "deploy-456",
  "operatorName": "istio-operator",
  "targetVersion": "v1.20.0",
  "strategy": "Canary",
  "canaryConfig": {
    "initialPercentage": 10,
    "incrementPercentage": 20,
    "evaluationPeriod": "PT5M",
    "successThreshold": 0.95,
    "autoRollbackEnabled": true,
    "healthCheckInterval": "PT30S"
  },
  "targetClusters": [
    "dev-1", "dev-2",
    "staging-1", "staging-2", "staging-3", "staging-4",
    "prod-us-east-1", "prod-us-east-2", /* ... */
  ]
}
```

### Health Check Validation

**Metrics Evaluated:**
1. **Controller Pod Health**
   - All pods in Running state
   - No crash loops (< 3 restarts)
   - Resource usage < 80%

2. **Webhook Health**
   - Endpoint responds with 200 OK
   - Latency < 1s
   - Certificate valid

3. **CRD Reconciliation**
   - Custom resources being reconciled
   - Error rate < 5%
   - No stale resources

**Success Threshold:**
- Default: 95% of health checks must pass
- Configurable per deployment

### Rollback Triggers

- Health check success rate < threshold
- Controller pod crash loops detected
- Webhook endpoint failures
- CRD reconciliation failures (> 5% error rate)
- Manual admin request

### Performance Characteristics

- **Deployment Time:** 15-30 minutes (depends on stages and evaluation periods)
- **Risk:** Low (incremental rollout with validation)
- **Rollback Speed:** Fast (only affected clusters rolled back)
- **Monitoring Overhead:** High (continuous health checks)

### When to Use

✅ **Good For:**
- Production environments
- Critical operators (ingress, service mesh, cert-manager)
- Operators with breaking CRD changes
- Large-scale deployments (20+ clusters)
- Risk-averse organizations

❌ **Not Good For:**
- Development environments (too slow)
- Single cluster deployments
- Non-critical operators with no CRDs

---

## 3. Blue-Green Deployment Strategy

### Overview

**Pattern:** Environment switching with parallel deployments
**Risk:** Very Low
**Speed:** Moderate
**Complexity:** High

**Use Case:** Zero-downtime deployments with instant rollback capability.

### Behavior

- Deploy new operator version to "green" namespace
- Validate health in green environment (parallel to blue)
- Switch traffic from blue to green
- Keep blue as rollback target for 24 hours

### Algorithm

```csharp
public async Task<DeploymentResult> DeployAsync(
    OperatorDeployment deployment,
    List<KubernetesCluster> clusters)
{
    var config = deployment.GetBlueGreenConfig();

    // Step 1: Deploy to Green Namespace
    _logger.LogInformation("Deploying to Green namespace: {Namespace}", config.GreenNamespace);

    foreach (var cluster in clusters)
    {
        await DeployToNamespaceAsync(
            deployment,
            cluster,
            config.GreenNamespace
        );
    }

    // Step 2: Validation Period
    _logger.LogInformation("Starting validation period: {Period}", config.ValidationPeriod);
    await Task.Delay(config.ValidationPeriod);

    // Step 3: Health Validation
    var healthResults = await ValidateHealthAsync(
        clusters,
        config.GreenNamespace
    );

    if (!healthResults.AllHealthy())
    {
        _logger.LogError("Green environment validation failed");
        await CleanupNamespaceAsync(clusters, config.GreenNamespace);
        return DeploymentResult.Failure("Green environment unhealthy");
    }

    // Step 4: Switch Traffic (if auto-switch enabled)
    if (config.AutoSwitch)
    {
        _logger.LogInformation("Switching traffic from Blue to Green");

        foreach (var cluster in clusters)
        {
            await SwitchTrafficAsync(
                cluster,
                config.BlueNamespace,
                config.GreenNamespace
            );
        }

        // Monitor for 5 minutes post-switch
        await Task.Delay(TimeSpan.FromMinutes(5));

        var postSwitchHealth = await ValidateHealthAsync(clusters, config.GreenNamespace);
        if (!postSwitchHealth.AllHealthy())
        {
            _logger.LogWarning("Post-switch health check failed, rolling back to Blue");
            await SwitchTrafficAsync(cluster, config.GreenNamespace, config.BlueNamespace);
            return DeploymentResult.Failure("Post-switch validation failed");
        }
    }

    // Step 5: Schedule Blue Cleanup (retain for 24 hours)
    await ScheduleCleanupAsync(clusters, config.BlueNamespace, TimeSpan.FromHours(24));

    return DeploymentResult.SuccessResult(clusters.Select(c => c.Name).ToList());
}

private async Task SwitchTrafficAsync(
    KubernetesCluster cluster,
    string fromNamespace,
    string toNamespace)
{
    var k8sClient = _clientFactory.GetClient(cluster);

    // Update CRD webhook configurations to point to green
    await k8sClient.UpdateWebhookEndpointsAsync(fromNamespace, toNamespace);

    // Scale down blue controllers to 0
    await k8sClient.ScaleDeploymentAsync(fromNamespace, replicas: 0);

    // Ensure green controllers are running
    await k8sClient.ScaleDeploymentAsync(toNamespace, replicas: 3);
}
```

### Deployment Flow

```
Phase 1: Green Deployment (parallel to existing Blue)
├─ Create operators-green namespace (if not exists)
├─ Deploy operator v2.0 to Green
│   ├─ Install Helm chart in operators-green namespace
│   ├─ Deploy CRDs (idempotent)
│   ├─ Deploy operator controllers
│   └─ Deploy webhooks (green URLs)
├─ Wait validation period (10 minutes)
└─ Validate Green Health
    ├─ Controller pods: Running ✓
    ├─ Webhook endpoints: Healthy ✓
    └─ CRD reconciliation: Active ✓

Phase 2: Traffic Switch (if validation passed)
├─ Update CRD webhooks from Blue to Green
│   └─ ValidatingWebhookConfiguration
│   └─ MutatingWebhookConfiguration
├─ Scale Blue controllers to 0 replicas
├─ Ensure Green controllers scaled to 3 replicas
└─ Monitor for 5 minutes
    └─ Post-switch health check ✓

Phase 3: Stabilization
├─ Blue namespace retained for 24 hours
├─ Monitor Green for issues
└─ Schedule Blue cleanup after retention period

Phase 4: Cleanup (after 24 hours)
├─ Delete Blue namespace
└─ Swap naming: Green becomes Blue for next deployment
```

### Configuration

```json
{
  "deploymentId": "deploy-789",
  "operatorName": "nginx-ingress",
  "targetVersion": "v4.8.0",
  "strategy": "BlueGreen",
  "blueGreenConfig": {
    "blueNamespace": "operators-blue",
    "greenNamespace": "operators-green",
    "validationPeriod": "PT10M",
    "autoSwitch": true,
    "retentionPeriod": "PT24H",
    "switchMonitoringPeriod": "PT5M"
  },
  "targetClusters": [
    "prod-us-east",
    "prod-eu-west"
  ]
}
```

### Namespace Strategy

**Initial State:**
```
operators-blue namespace:
  - cert-manager v1.13.0 (current production)
  - Controller pods: 3 replicas
  - Webhooks: Active

operators-green namespace:
  - Empty (will receive v1.14.0)
```

**After Green Deployment:**
```
operators-blue namespace:
  - cert-manager v1.13.0 (still active)
  - Controller pods: 3 replicas
  - Webhooks: Active

operators-green namespace:
  - cert-manager v1.14.0 (validating)
  - Controller pods: 3 replicas
  - Webhooks: Active but not receiving traffic
```

**After Traffic Switch:**
```
operators-blue namespace:
  - cert-manager v1.13.0 (standby for rollback)
  - Controller pods: 0 replicas (scaled down)
  - Webhooks: Inactive

operators-green namespace:
  - cert-manager v1.14.0 (production)
  - Controller pods: 3 replicas
  - Webhooks: Active and receiving traffic
```

### Instant Rollback

**Rollback Procedure:**
```csharp
public async Task RollbackAsync(KubernetesCluster cluster, string blueNamespace, string greenNamespace)
{
    _logger.LogWarning("Initiating Blue-Green rollback");

    // Switch traffic back to Blue
    await SwitchTrafficAsync(cluster, greenNamespace, blueNamespace);

    // Scale up Blue controllers (instant)
    await k8sClient.ScaleDeploymentAsync(blueNamespace, replicas: 3);

    // Wait for Blue to stabilize
    await Task.Delay(TimeSpan.FromSeconds(30));

    // Validate Blue health
    var health = await ValidateHealthAsync(cluster, blueNamespace);

    // Clean up failed Green deployment
    await CleanupNamespaceAsync(cluster, greenNamespace);

    _logger.LogInformation("Rollback completed in < 1 minute");
}
```

**Rollback Time:** < 1 minute (no redeployment required)

### Performance Characteristics

- **Deployment Time:** 12-15 minutes (validation + switch)
- **Risk:** Very Low (parallel environments, instant rollback)
- **Rollback Speed:** < 1 minute (just traffic switch)
- **Resource Overhead:** High (2x operator resources during deployment)

### When to Use

✅ **Good For:**
- Critical production operators (ingress controllers, service mesh)
- Zero-downtime requirements
- Operators serving live traffic
- High-risk upgrades with breaking changes
- Instant rollback requirements

❌ **Not Good For:**
- Resource-constrained clusters (requires 2x resources)
- Development/staging environments (unnecessary complexity)
- Single-namespace operators (conflicts)
- Operators without webhooks (less benefit)

---

## 4. Rolling Deployment Strategy

### Overview

**Pattern:** Cluster-by-cluster sequential deployment
**Risk:** Moderate
**Speed:** Slow
**Complexity:** Moderate

**Use Case:** Environment progression (Dev → Staging → Prod) with validation gates.

### Behavior

- Deploy to one cluster at a time
- Validate health before proceeding to next cluster
- Environment-based ordering (Dev first, Prod last)
- Pause between clusters for monitoring

### Algorithm

```csharp
public async Task<DeploymentResult> DeployAsync(
    OperatorDeployment deployment,
    List<KubernetesCluster> clusters)
{
    var config = deployment.GetRollingConfig();
    var orderedClusters = OrderClustersByPriority(clusters, config.ClusterOrder);
    var successfulClusters = new List<string>();
    var failedClusters = new List<string>();

    foreach (var cluster in orderedClusters)
    {
        _logger.LogInformation(
            "Rolling deployment: Deploying to cluster {Cluster} ({Index}/{Total})",
            cluster.Name,
            orderedClusters.IndexOf(cluster) + 1,
            orderedClusters.Count
        );

        // Deploy to cluster
        try
        {
            await DeployToClusterAsync(deployment, cluster);

            // Wait validation period
            await Task.Delay(config.ValidationPeriod);

            // Validate health
            var health = await ValidateClusterHealthAsync(cluster, deployment.OperatorName);

            if (!health.OverallHealth == HealthStatus.Healthy)
            {
                _logger.LogError(
                    "Health check failed for cluster {Cluster}",
                    cluster.Name
                );

                // Rollback this cluster
                await RollbackClusterAsync(cluster, deployment);
                failedClusters.Add(cluster.Name);

                // Stop rolling deployment
                break;
            }

            successfulClusters.Add(cluster.Name);
            _logger.LogInformation("Cluster {Cluster} deployment successful", cluster.Name);

            // Pause before next cluster
            if (config.PauseBetweenClusters > TimeSpan.Zero)
            {
                _logger.LogInformation("Pausing for {Duration} before next cluster", config.PauseBetweenClusters);
                await Task.Delay(config.PauseBetweenClusters);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Deployment failed for cluster {Cluster}", cluster.Name);
            failedClusters.Add(cluster.Name);
            break;
        }
    }

    if (failedClusters.Any())
    {
        return DeploymentResult.PartialSuccess(successfulClusters, failedClusters);
    }

    return DeploymentResult.SuccessResult(successfulClusters);
}

private List<KubernetesCluster> OrderClustersByPriority(
    List<KubernetesCluster> clusters,
    List<string> clusterOrder)
{
    // If explicit order provided, use it
    if (clusterOrder != null && clusterOrder.Any())
    {
        return clusterOrder
            .Select(name => clusters.FirstOrDefault(c => c.Name == name))
            .Where(c => c != null)
            .ToList();
    }

    // Default: Order by environment (Dev → Staging → Prod)
    return clusters
        .OrderBy(c => c.Environment)
        .ThenBy(c => c.Name)
        .ToList();
}
```

### Deployment Flow

```
Cluster 1: dev-cluster-1 (Development)
├─ Deploy operator v2.0
├─ Wait validation period (5 minutes)
├─ Health check validation
│   ├─ Controller pods: Healthy ✓
│   ├─ Webhook endpoints: Healthy ✓
│   └─ CRD reconciliation: Healthy ✓
├─ Success ✓
└─ Pause 2 minutes before next cluster

Cluster 2: dev-cluster-2 (Development)
├─ Deploy operator v2.0
├─ Wait 5 minutes
├─ Health check validation ✓
├─ Success ✓
└─ Pause 2 minutes

Cluster 3: staging-cluster (Staging)
├─ Deploy operator v2.0
├─ Wait 5 minutes
├─ Health check validation ✓
├─ Success ✓
└─ Pause 2 minutes

Cluster 4: prod-us-east (Production)
├─ Deploy operator v2.0
├─ Wait 5 minutes
├─ Health check validation ✓
├─ Success ✓
└─ Pause 2 minutes

Cluster 5: prod-eu-west (Production)
├─ Deploy operator v2.0
├─ Wait 5 minutes
├─ Health check validation ✓
└─ All clusters deployed successfully ✓
```

### Configuration

```json
{
  "deploymentId": "deploy-101",
  "operatorName": "external-dns",
  "targetVersion": "v0.14.0",
  "strategy": "Rolling",
  "rollingConfig": {
    "clusterOrder": [
      "dev-1",
      "dev-2",
      "staging",
      "prod-us-east",
      "prod-eu-west"
    ],
    "validationPeriod": "PT5M",
    "pauseBetweenClusters": "PT2M",
    "stopOnFailure": true
  },
  "targetClusters": [
    "dev-1",
    "dev-2",
    "staging",
    "prod-us-east",
    "prod-eu-west"
  ]
}
```

### Environment-Based Ordering

**Default Order (if no explicit order provided):**
```
1. Development clusters (alphabetically)
2. Staging clusters (alphabetically)
3. Production clusters (alphabetically)
```

**Example:**
```
dev-cluster-1       (Environment: Development, Priority: 1)
dev-cluster-2       (Environment: Development, Priority: 2)
staging-cluster-1   (Environment: Staging, Priority: 3)
staging-cluster-2   (Environment: Staging, Priority: 4)
prod-us-east        (Environment: Production, Priority: 5)
prod-eu-west        (Environment: Production, Priority: 6)
```

### Failure Handling

**Stop on Failure (default):**
- Deployment stops at first failed cluster
- Previous clusters remain on new version
- Failed cluster and remaining clusters stay on old version
- Operator can choose to rollback successful clusters or continue manually

**Continue on Failure (optional):**
- Deployment continues to next cluster even if one fails
- Useful for independent clusters
- Final result shows partial success

### Performance Characteristics

- **Deployment Time:** 35-45 minutes (5 clusters with 5 min validation + 2 min pauses)
- **Risk:** Moderate (sequential rollout, early failure detection)
- **Rollback Speed:** Moderate (rollback affected clusters)
- **Predictability:** High (deterministic order)

### When to Use

✅ **Good For:**
- Environment progression workflows (Dev → Staging → Prod)
- Testing operator updates across environments
- Clusters with different configurations
- Risk mitigation through staged rollout
- Production deployments with validation gates

❌ **Not Good For:**
- Large cluster counts (> 20 clusters, too slow)
- Development environments (too slow)
- Operators requiring simultaneous updates across all clusters

---

## Strategy Comparison

| Strategy | Speed | Risk | Complexity | Rollback | Best For |
|----------|-------|------|------------|----------|----------|
| **Direct** | ⭐⭐⭐⭐⭐ | ⭐ | ⭐ | ⭐⭐ | Development |
| **Canary** | ⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐ | ⭐⭐⭐⭐ | Production (large scale) |
| **Blue-Green** | ⭐⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | Critical production |
| **Rolling** | ⭐⭐ | ⭐⭐⭐ | ⭐⭐ | ⭐⭐⭐ | Environment progression |

---

## Best Practices

### 1. Choose the Right Strategy

**Decision Tree:**
```
Is this production?
├─ No → Use Direct for speed
└─ Yes
    ├─ Is operator critical (ingress, service mesh)?
    │   └─ Yes → Use Blue-Green for zero-downtime
    └─ How many clusters?
        ├─ < 5 clusters → Use Rolling for simplicity
        └─ > 5 clusters → Use Canary for risk mitigation
```

### 2. Configure Appropriate Timeouts

**Validation Periods:**
- Development: 2-3 minutes
- Staging: 5 minutes
- Production: 10-15 minutes

**Health Check Intervals:**
- During deployment: Every 30 seconds
- Post-deployment: Every 60 seconds

### 3. Monitor Strategy Performance

Track metrics per strategy:
- Deployment success rate
- Average deployment time
- Rollback frequency
- Health check success rate

### 4. Test Strategy Switching

Ensure operators can handle:
- Strategy changes between deployments
- Rollback to previous strategy
- Strategy-specific configuration updates

---

## Troubleshooting

### Issue: Canary Stage Failures

**Symptom:** Canary deployment fails at early stages

**Solutions:**
1. Review health check logs for specific failures
2. Reduce initial percentage (e.g., 5% instead of 10%)
3. Increase evaluation period for more data
4. Check operator logs in canary clusters

### Issue: Blue-Green Traffic Switch Failures

**Symptom:** Traffic switch causes service disruption

**Solutions:**
1. Verify webhook configurations updated correctly
2. Check DNS propagation for webhook URLs
3. Ensure green controllers fully ready before switch
4. Increase post-switch monitoring period

### Issue: Rolling Deployment Too Slow

**Symptom:** Rolling deployment takes too long for large cluster counts

**Solutions:**
1. Reduce validation period (if safe)
2. Reduce pause between clusters
3. Consider switching to Canary strategy for large scale
4. Deploy to multiple clusters in parallel (hybrid approach)

### Issue: Direct Deployment Causes Widespread Failures

**Symptom:** Direct deployment fails across all clusters

**Solutions:**
1. Switch to Canary or Rolling for better risk mitigation
2. Test in single cluster first using Direct
3. Implement mandatory health checks
4. Add pre-deployment validation (CRD compatibility)

---

**Last Updated:** 2025-11-23
**Version:** 1.0.0
