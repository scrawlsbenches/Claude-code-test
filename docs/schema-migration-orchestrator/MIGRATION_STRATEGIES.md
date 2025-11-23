# Database Migration Strategies Guide

**Version:** 1.0.0
**Last Updated:** 2025-11-23

---

## Overview

The HotSwap Schema Migration Orchestrator supports 5 migration strategies, each adapted from deployment strategies to handle database schema changes safely. Strategies determine how migrations are rolled out across database clusters.

### Strategy Selection

| Environment | Default Strategy | Use Case |
|------------|------------------|----------|
| Development | Direct | Fast iteration, immediate feedback |
| QA/Staging | Phased | Test progressive rollout |
| Production (low risk) | Phased | Standard production deployment |
| Production (high risk) | Canary | High-value tables, breaking changes |
| Mission Critical | Shadow | Zero-risk tolerance, compliance requirements |

---

## 1. Direct Migration Strategy

### Overview

**Pattern:** Single-step deployment
**Risk Level:** High
**Rollout Time:** ~1-5 minutes
**Use Case:** Development/QA environments, trivial changes

### Behavior

- Execute migration immediately on target database
- No phased rollout
- Fastest execution time
- No validation period between phases

### Algorithm

```csharp
public async Task<ExecutionResult> ExecuteAsync(Migration migration, DatabaseTarget database)
{
    // 1. Capture performance baseline
    var baseline = await CaptureBaselineAsync(database);
    
    // 2. Execute migration
    await ExecuteSqlAsync(database.MasterConnectionString, migration.MigrationScript);
    
    // 3. Monitor performance for 2 minutes
    for (int i = 0; i < 24; i++) // 24 × 5s = 2 minutes
    {
        var metrics = await CaptureMetricsAsync(database);
        if (metrics.ExceedsThreshold(baseline))
        {
            await RollbackAsync(migration, database);
            return ExecutionResult.Failure("Performance degradation detected");
        }
        await Task.Delay(TimeSpan.FromSeconds(5));
    }
    
    return ExecutionResult.Success();
}
```

### Example: Create Index

```sql
-- Migration Script
CREATE INDEX CONCURRENTLY idx_users_email ON users(email);

-- Rollback Script
DROP INDEX CONCURRENTLY IF EXISTS idx_users_email;
```

**Execution:**
1. Validate SQL syntax
2. Execute on target database
3. Monitor for 2 minutes
4. Complete if no issues

**Rollout Time:** 3-5 minutes

---

## 2. Phased Migration Strategy

### Overview

**Pattern:** Replica-first, master-last deployment
**Risk Level:** Medium
**Rollout Time:** ~30-60 minutes
**Use Case:** Production deployments, standard changes

### Behavior

- Execute on replicas first (read-only traffic)
- Validate performance on each replica
- Execute on master last (write traffic)
- Automatic rollback per phase
- Pause between phases for validation

### Algorithm

```csharp
public async Task<ExecutionResult> ExecuteAsync(Migration migration, DatabaseTarget database)
{
    var baseline = await CaptureBaselineAsync(database);
    var phases = new[] { database.Replicas, new[] { database.Master } };
    
    foreach (var phase in phases)
    {
        foreach (var instance in phase)
        {
            // Execute migration on instance
            await ExecuteSqlAsync(instance.ConnectionString, migration.MigrationScript);
            
            // Monitor for 5 minutes (replicas) or 10 minutes (master)
            var monitorDuration = instance.IsMaster ? 10 : 5;
            for (int i = 0; i < monitorDuration * 12; i++)
            {
                var metrics = await CaptureMetricsAsync(instance);
                if (metrics.ExceedsThreshold(baseline))
                {
                    await RollbackPhaseAsync(migration, phase);
                    return ExecutionResult.Failure($"Performance degradation on {instance.Name}");
                }
                await Task.Delay(TimeSpan.FromSeconds(5));
            }
        }
        
        // Pause 2 minutes between phases
        await Task.Delay(TimeSpan.FromMinutes(2));
    }
    
    return ExecutionResult.Success();
}
```

### Message Flow

```
Phase 1: Replicas
├─ Replica-1 → execute → monitor 5 min → ✓
├─ Replica-2 → execute → monitor 5 min → ✓
└─ Replica-3 → execute → monitor 5 min → ✓

[Pause 2 minutes]

Phase 2: Master
└─ Master → execute → monitor 10 min → ✓
```

### Example: Add Column

```sql
-- Migration Script
ALTER TABLE users ADD COLUMN last_login_at TIMESTAMP;

-- Rollback Script
ALTER TABLE users DROP COLUMN IF EXISTS last_login_at;
```

**Rollout Time:** 30-45 minutes (3 replicas + master)

---

## 3. Canary Migration Strategy

### Overview

**Pattern:** Progressive percentage rollout
**Risk Level:** Low
**Rollout Time:** ~60-90 minutes
**Use Case:** High-risk changes, large datasets

### Behavior

- Execute on 10% of replicas first (canary group)
- Monitor performance and replication lag
- Progressive rollout: 10% → 50% → 100%
- Longer validation periods
- Automatic rollback on any phase failure

### Algorithm

```csharp
public async Task<ExecutionResult> ExecuteAsync(Migration migration, DatabaseTarget database)
{
    var baseline = await CaptureBaselineAsync(database);
    var replicas = database.Replicas.OrderBy(r => r.TrafficWeight).ToList();
    
    // Phase 1: 10% canary
    var canaryReplicas = replicas.Take((int)(replicas.Count * 0.1)).ToList();
    await ExecutePhaseAsync(migration, canaryReplicas, baseline, monitorMinutes: 15);
    
    // Phase 2: 50% rollout
    var halfReplicas = replicas.Take((int)(replicas.Count * 0.5)).ToList();
    await ExecutePhaseAsync(migration, halfReplicas, baseline, monitorMinutes: 15);
    
    // Phase 3: 100% rollout
    await ExecutePhaseAsync(migration, replicas, baseline, monitorMinutes: 15);
    
    // Phase 4: Master
    await ExecutePhaseAsync(migration, new[] { database.Master }, baseline, monitorMinutes: 20);
    
    return ExecutionResult.Success();
}
```

### Rollout Phases

```
Phase 1: 10% Canary (1 replica)
└─ Replica-1 → execute → monitor 15 min → ✓

Phase 2: 50% Rollout (2 replicas total)
├─ Replica-1 (already done)
└─ Replica-2 → execute → monitor 15 min → ✓

Phase 3: 100% Rollout (4 replicas total)
├─ Replica-1, Replica-2 (already done)
├─ Replica-3 → execute → monitor 15 min → ✓
└─ Replica-4 → execute → monitor 15 min → ✓

Phase 4: Master
└─ Master → execute → monitor 20 min → ✓
```

**Rollout Time:** 60-90 minutes

---

## 4. Blue-Green Migration Strategy

### Overview

**Pattern:** Shadow deployment with traffic switch
**Risk Level:** Very Low
**Rollout Time:** ~45-60 minutes
**Use Case:** Zero-downtime requirements, reversible changes

### Behavior

- Execute on shadow replica (green)
- Validate performance without production traffic
- Switch traffic from current (blue) to new (green)
- Keep blue as instant rollback option
- **Prerequisites:** Load balancer or connection pooler

### Algorithm

```csharp
public async Task<ExecutionResult> ExecuteAsync(Migration migration, DatabaseTarget database)
{
    // 1. Create shadow replica (green)
    var greenReplica = await CloneReplicaAsync(database.Master);
    
    // 2. Execute migration on green
    await ExecuteSqlAsync(greenReplica.ConnectionString, migration.MigrationScript);
    
    // 3. Run performance tests on green
    var testResults = await RunPerformanceTestsAsync(greenReplica);
    if (!testResults.PassedAllTests)
    {
        await DeleteReplicaAsync(greenReplica);
        return ExecutionResult.Failure("Performance tests failed on green replica");
    }
    
    // 4. Switch traffic to green (via load balancer)
    await SwitchTrafficAsync(from: database.Master, to: greenReplica);
    
    // 5. Monitor production traffic on green for 15 minutes
    for (int i = 0; i < 180; i++)
    {
        var metrics = await CaptureMetricsAsync(greenReplica);
        if (metrics.HasIssues())
        {
            await SwitchTrafficAsync(from: greenReplica, to: database.Master);
            return ExecutionResult.Failure("Issues detected on green, rolled back to blue");
        }
        await Task.Delay(TimeSpan.FromSeconds(5));
    }
    
    // 6. Promote green to master, demote blue to replica
    await PromoteReplicaAsync(greenReplica);
    
    return ExecutionResult.Success();
}
```

**Rollout Time:** 45-60 minutes

---

## 5. Shadow Migration Strategy

### Overview

**Pattern:** Production traffic replay on shadow
**Risk Level:** Minimal
**Rollout Time:** ~2-4 hours
**Use Case:** Mission-critical systems, highest confidence

### Behavior

- Execute on shadow replica
- Replay production traffic to shadow (1-hour window)
- Compare performance metrics (shadow vs production)
- Rollout to production only if metrics acceptable
- Most conservative, highest confidence

### Algorithm

```csharp
public async Task<ExecutionResult> ExecuteAsync(Migration migration, DatabaseTarget database)
{
    // 1. Create shadow replica
    var shadowReplica = await CloneReplicaAsync(database.Master);
    
    // 2. Execute migration on shadow
    await ExecuteSqlAsync(shadowReplica.ConnectionString, migration.MigrationScript);
    
    // 3. Set up traffic replay (pgBadger, pt-query-digest)
    var trafficReplay = await StartTrafficReplayAsync(
        source: database.Master,
        target: shadowReplica,
        duration: TimeSpan.FromHours(1)
    );
    
    // 4. Wait for replay completion
    await trafficReplay.WaitForCompletionAsync();
    
    // 5. Compare metrics
    var productionMetrics = await GetMetricsSummaryAsync(database.Master, TimeSpan.FromHours(1));
    var shadowMetrics = await GetMetricsSummaryAsync(shadowReplica, TimeSpan.FromHours(1));
    
    var comparison = CompareMetrics(productionMetrics, shadowMetrics);
    if (comparison.ShadowPerformanceWorse())
    {
        await DeleteReplicaAsync(shadowReplica);
        return ExecutionResult.Failure($"Shadow performance degraded: {comparison.Details}");
    }
    
    // 6. Execute on production using Phased strategy
    return await new PhasedMigrationStrategy().ExecuteAsync(migration, database);
}
```

**Rollout Time:** 2-4 hours (1 hour replay + phased rollout)

---

## Strategy Comparison

| Strategy | Risk | Time | Complexity | Production Traffic Impact | Use When |
|----------|------|------|------------|--------------------------|----------|
| Direct | High | 5 min | Low | Immediate | Dev/QA only |
| Phased | Medium | 45 min | Medium | Gradual | Standard production |
| Canary | Low | 90 min | Medium | Very gradual | High-risk changes |
| Blue-Green | Very Low | 60 min | High | None (switch) | Zero-downtime critical |
| Shadow | Minimal | 4 hours | Very High | None (test first) | Mission-critical systems |

---

## Best Practices

### 1. Choose the Right Strategy

**Decision Tree:**
```
Is this a production deployment?
├─ No → Direct
└─ Yes → Is this a high-risk change?
    ├─ No → Phased
    └─ Yes → Is zero-downtime critical?
        ├─ No → Canary
        └─ Yes → Can you afford 4 hours?
            ├─ No → Blue-Green
            └─ Yes → Shadow
```

### 2. Monitor Strategy Performance

Track metrics per strategy:
- Execution duration
- Rollback rate
- Performance impact
- Success rate

### 3. Test Rollback Procedures

Ensure rollback scripts work:
```bash
# Test rollback in QA
POST /api/v1/migrations/{id}/execute
{
  "environment": "QA",
  "dryRun": false
}

# Immediately test rollback
POST /api/v1/migrations/{id}/executions/{execId}/rollback
```

---

**Last Updated:** 2025-11-23
**Version:** 1.0.0
