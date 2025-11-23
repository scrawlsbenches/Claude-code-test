# Rollback Procedures

**Version:** 1.0
**Last Updated:** 2025-11-23
**Purpose:** Step-by-step procedures for rolling back failed deployments

---

## Table of Contents

1. [Overview](#overview)
2. [When to Rollback](#when-to-rollback)
3. [Rollback Strategies](#rollback-strategies)
4. [Rollback Procedures](#rollback-procedures)
5. [Post-Rollback Verification](#post-rollback-verification)
6. [Troubleshooting Failed Rollbacks](#troubleshooting-failed-rollbacks)

---

## Overview

The Distributed Kernel Orchestration System supports automated rollbacks when deployments fail. This guide covers manual rollback procedures for scenarios where automated rollback fails or when manual intervention is required.

### Automatic vs Manual Rollback

| Scenario | Rollback Type | Procedure |
|----------|---------------|-----------|
| Deployment failure (validation, health check) | Automatic | System rolls back automatically |
| Performance degradation detected | Manual | Follow this guide |
| Security vulnerability discovered | Manual | Follow this guide |
| Automated rollback failed | Manual | See [Troubleshooting](#troubleshooting-failed-rollbacks) |

---

## When to Rollback

### Critical Rollback Triggers

Execute rollback immediately if:

1. **Security Breach Detected**
   - CVE discovered in deployed version
   - Unauthorized access detected
   - Data leak identified

2. **System Instability**
   - Error rate > 10% for 5+ minutes
   - P99 latency > 5 seconds
   - Memory leak causing OOM errors

3. **Data Corruption**
   - Database inconsistencies detected
   - Data loss reported
   - Audit log violations

4. **Compliance Violation**
   - GDPR/HIPAA compliance issues
   - Regulatory requirement breach

### Non-Critical Rollback Triggers

Schedule rollback during maintenance window:

- Performance degradation (not critical)
- Feature not working as expected
- User complaints about UX
- Minor bugs discovered

---

## Rollback Strategies

### Strategy Selection Matrix

| Current Strategy | Rollback Method | Risk Level | Duration |
|------------------|-----------------|------------|----------|
| **Direct** | Redeploy previous version | 🔴 High | 2-5 min |
| **Rolling** | Reverse rolling update | 🟡 Medium | 5-15 min |
| **Blue-Green** | Switch traffic back to Blue | 🟢 Low | < 1 min |
| **Canary** | Remove canary, keep stable | 🟢 Low | 1-3 min |

---

## Rollback Procedures

### 1. Blue-Green Rollback (Fastest)

**Use when:** Original environment (Blue) is still running

**Steps:**

```bash
# 1. Verify Blue environment is healthy
curl https://api-blue.example.com/health
# Expected: 200 OK

# 2. Switch load balancer back to Blue
kubectl patch service hotswap-api -p '{"spec":{"selector":{"version":"blue"}}}'

# 3. Verify traffic switched
curl https://api.example.com/health
# Should route to Blue environment

# 4. Monitor error rate
watch -n 5 'curl -s https://api.example.com/metrics | grep http_req_failed'

# 5. Decommission Green environment (optional)
kubectl scale deployment/hotswap-api-green --replicas=0
```

**Duration:** < 1 minute
**Risk:** Low (instant switch)

---

### 2. Canary Rollback

**Use when:** Canary deployment showing issues

**Steps:**

```bash
# 1. Get current canary percentage
kubectl get deployment hotswap-api-canary -o json | jq '.spec.replicas'

# 2. Scale canary to 0 (remove bad version)
kubectl scale deployment/hotswap-api-canary --replicas=0

# 3. Verify stable version handling all traffic
kubectl get pods -l version=stable
# All pods should be Running

# 4. Check metrics
curl https://api.example.com/metrics | grep deployments_started_total

# 5. Delete canary deployment
kubectl delete deployment/hotswap-api-canary
```

**Duration:** 1-3 minutes
**Risk:** Low (stable version unaffected)

---

### 3. Rolling Update Rollback

**Use when:** Rolling update partially completed

**Steps:**

```bash
# 1. Check rollout history
kubectl rollout history deployment/hotswap-api

# Output example:
# REVISION  CHANGE-CAUSE
# 1         Initial deployment (v1.0.0)
# 2         Deploy v1.1.0 (current - FAILING)

# 2. Rollback to previous revision
kubectl rollout undo deployment/hotswap-api

# Or rollback to specific revision:
# kubectl rollout undo deployment/hotswap-api --to-revision=1

# 3. Watch rollback progress
kubectl rollout status deployment/hotswap-api

# 4. Verify pod versions
kubectl get pods -l app=hotswap-api -o jsonpath='{range .items[*]}{.metadata.name}{"\t"}{.spec.containers[0].image}{"\n"}{end}'

# 5. Check application health
for pod in $(kubectl get pods -l app=hotswap-api -o name); do
  kubectl exec $pod -- curl -f http://localhost:5000/health || echo "$pod UNHEALTHY"
done
```

**Duration:** 5-15 minutes (depends on pod count)
**Risk:** Medium (gradual rollout)

---

### 4. Direct Deployment Rollback

**Use when:** Single instance deployment or emergency rollback

**Steps:**

```bash
# 1. Identify previous working version
git log --oneline | head -10
# Find commit hash of last working version

# 2. Checkout previous version
git checkout <previous-commit-hash>

# 3. Build previous version
dotnet build --configuration Release

# 4. Stop current version
kubectl scale deployment/hotswap-api --replicas=0

# 5. Deploy previous version
kubectl apply -f k8s/deployment.yaml

# 6. Scale up
kubectl scale deployment/hotswap-api --replicas=3

# 7. Wait for pods ready
kubectl wait --for=condition=ready pod -l app=hotswap-api --timeout=300s

# 8. Verify deployment
curl https://api.example.com/health
```

**Duration:** 2-5 minutes
**Risk:** High (downtime during switch)

---

## Post-Rollback Verification

### Mandatory Checks

**1. Application Health**

```bash
# Check all pods are healthy
kubectl get pods -l app=hotswap-api

# Verify health endpoint
curl https://api.example.com/health
# Expected: {"status": "Healthy"}

# Check readiness
kubectl get deployment hotswap-api -o jsonpath='{.status.conditions[?(@.type=="Available")].status}'
# Expected: "True"
```

**2. Database Integrity**

```bash
# Connect to database
psql -h postgres.example.com -U hotswap

# Check row counts (compare to baseline)
SELECT
  'audit_logs' as table_name, COUNT(*) as rows FROM audit_logs
UNION ALL
SELECT 'deployments', COUNT(*) FROM deployments
UNION ALL
SELECT 'approval_requests', COUNT(*) FROM approval_requests;

# Verify no orphaned records
SELECT COUNT(*) FROM deployments WHERE status = 'Running' AND updated_at < NOW() - INTERVAL '1 hour';
# Expected: 0
```

**3. Metrics Validation**

```bash
# Check error rate
curl -s https://api.example.com/metrics | grep http_req_failed
# Expected: rate < 0.01 (1%)

# Check latency
curl -s https://api.example.com/metrics | grep http_req_duration
# Expected: p95 < 500ms

# Check active deployments
curl -s https://api.example.com/metrics | grep deployments_active
# Should match expected baseline
```

**4. Integration Tests**

```bash
# Run smoke tests
./scripts/smoke-tests.sh

# Expected: All tests pass
```

### Optional Checks

- Review logs for errors (last 15 minutes)
- Check WebSocket connections
- Verify approval workflow
- Test sample deployment (Development environment only)

---

## Troubleshooting Failed Rollbacks

### Issue 1: Rollback Stuck at Revision

**Symptoms:**
```bash
kubectl rollout status deployment/hotswap-api
# Waiting for deployment rollback to finish: 1 old replicas are pending termination
```

**Resolution:**

```bash
# 1. Force delete stuck pods
kubectl delete pod -l app=hotswap-api --force --grace-period=0

# 2. Restart deployment
kubectl rollout restart deployment/hotswap-api

# 3. If still stuck, recreate deployment
kubectl delete deployment hotswap-api
kubectl apply -f k8s/deployment-previous.yaml
```

---

### Issue 2: Database Migration Conflicts

**Symptoms:**
- Application fails to start after rollback
- Migration errors in logs

**Resolution:**

```bash
# 1. Check current migration version
dotnet ef database update --list

# 2. Rollback database to previous migration
dotnet ef database update <previous-migration-name>

# 3. Restart application
kubectl rollout restart deployment/hotswap-api

# 4. Verify migration state
psql -h postgres.example.com -U hotswap -c "SELECT * FROM __EFMigrationsHistory ORDER BY MigrationId DESC LIMIT 5;"
```

---

### Issue 3: Persistent Configuration Issues

**Symptoms:**
- Rollback successful but application behaves incorrectly
- Environment variable mismatch

**Resolution:**

```bash
# 1. Verify ConfigMap version
kubectl get configmap hotswap-config -o yaml

# 2. Restore previous ConfigMap
kubectl apply -f k8s/configmap-backup-$(date -d yesterday +%Y%m%d).yaml

# 3. Restart pods to pick up config
kubectl rollout restart deployment/hotswap-api

# 4. Verify config loaded
kubectl exec -it <pod-name> -- env | grep ASPNETCORE
```

---

### Issue 4: Load Balancer Not Switching

**Symptoms:**
- Traffic still routing to failed version
- Service selector not updating

**Resolution:**

```bash
# 1. Check service selector
kubectl describe service hotswap-api | grep Selector

# 2. Manually update selector
kubectl patch service hotswap-api -p '{"spec":{"selector":{"version":"<previous-version>"}}}'

# 3. Verify endpoints
kubectl get endpoints hotswap-api

# 4. Test traffic routing
for i in {1..10}; do
  curl -s https://api.example.com/health | jq .version
done
# All requests should return previous version
```

---

## Emergency Procedures

### Complete System Rollback

**Use when:** All environments affected, critical outage

```bash
# 1. Enable maintenance mode
kubectl scale deployment/hotswap-api --replicas=0

# 2. Restore from last known good state
git checkout <last-good-commit>
docker build -t hotswap-api:<last-good-version> .
docker push hotswap-api:<last-good-version>

# 3. Update deployment manifest
sed -i 's/image:.*/image: hotswap-api:<last-good-version>/' k8s/deployment.yaml

# 4. Deploy
kubectl apply -f k8s/deployment.yaml
kubectl scale deployment/hotswap-api --replicas=3

# 5. Restore database (if needed)
pg_restore -h postgres.example.com -U hotswap -d hotswap < /backup/hotswap-$(date -d yesterday +%Y%m%d).dump

# 6. Verify and disable maintenance mode
curl https://api.example.com/health
```

---

## Rollback Checklist

Use this checklist for every rollback:

- [ ] **Pre-Rollback**
  - [ ] Identify root cause of issue
  - [ ] Determine rollback strategy
  - [ ] Notify stakeholders (Slack, status page)
  - [ ] Create incident ticket
  - [ ] Take database backup (if schema changed)

- [ ] **During Rollback**
  - [ ] Execute rollback procedure
  - [ ] Monitor error rate and latency
  - [ ] Watch pod status
  - [ ] Check logs for errors

- [ ] **Post-Rollback**
  - [ ] Verify application health
  - [ ] Run smoke tests
  - [ ] Check database integrity
  - [ ] Update status page
  - [ ] Close incident ticket
  - [ ] Schedule post-mortem

- [ ] **Follow-Up**
  - [ ] Document rollback reason
  - [ ] Update runbook if needed
  - [ ] Fix root cause
  - [ ] Test fix in staging
  - [ ] Plan re-deployment

---

## Rollback Metrics

Track these metrics for every rollback:

| Metric | Target | Measurement |
|--------|--------|-------------|
| Time to Detect (TTD) | < 5 min | Alert to acknowledgment |
| Time to Decide (TTDEC) | < 10 min | Acknowledgment to rollback decision |
| Time to Rollback (TTR) | < 15 min | Decision to rollback complete |
| Mean Time to Recover (MTTR) | < 30 min | Total incident duration |

---

## References

- [Incident Response Runbook](./INCIDENT_RESPONSE_RUNBOOK.md)
- [Operations Guide](./OPERATIONS_GUIDE.md)
- [Troubleshooting Guide](./TROUBLESHOOTING_GUIDE.md)
- [Kubernetes Rollback Documentation](https://kubernetes.io/docs/concepts/workloads/controllers/deployment/#rolling-back-a-deployment)

---

**Document Version:**

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2025-11-23 | Initial release |

**Next Review:** 2026-02-23
