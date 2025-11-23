# Incident Response Runbook

**Version:** 1.0
**Last Updated:** 2025-11-23
**Purpose:** Standard procedures for responding to production incidents

---

## Table of Contents

1. [Incident Severity Levels](#incident-severity-levels)
2. [Incident Response Process](#incident-response-process)
3. [Common Incidents](#common-incidents)
4. [Post-Incident Procedures](#post-incident-procedures)

---

## Incident Severity Levels

### SEV-1: Critical (Page immediately)

**Impact:** Complete service outage or data loss

**Examples:**
- API completely unavailable (all instances down)
- Database corruption or data loss
- Security breach or unauthorized access
- Vault sealed with no access to secrets

**Response Time:** Immediate (< 5 minutes)
**Escalation:** Page on-call, notify VP Engineering

### SEV-2: High (Urgent response)

**Impact:** Significant degradation affecting multiple users

**Examples:**
- High error rate (> 10%)
- Severe performance degradation (p95 > 2s)
- Single API instance down (others functioning)
- Approval workflow completely broken

**Response Time:** < 15 minutes
**Escalation:** Page on-call

### SEV-3: Medium (Normal business hours)

**Impact:** Limited degradation or feature not working

**Examples:**
- Moderate latency increase (p95 > 1s)
- Single deployment strategy failing
- WebSocket notifications not working
- Non-critical background job failures

**Response Time:** < 1 hour (during business hours)
**Escalation:** Slack notification

### SEV-4: Low (Scheduled maintenance)

**Impact:** Cosmetic issues or minor bugs

**Examples:**
- Log spam
- Missing documentation
- UI display issues
- Non-critical metrics missing

**Response Time:** Next business day
**Escalation:** Create ticket

---

## Incident Response Process

### 1. Detection and Alert

**Alert Sources:**
- Prometheus/Grafana alerts
- PagerDuty notifications
- User reports
- Automated health checks

### 2. Initial Response (< 5 minutes)

#### Step 1: Acknowledge the Alert

```bash
# Acknowledge in PagerDuty
pagerduty acknowledge --incident-id <ID>

# Post in Slack
/incident declare "API Error Rate High" --severity sev-2
```

#### Step 2: Assess Severity

Questions to ask:
- Is the service completely down or degraded?
- How many users are affected?
- Is there a workaround available?
- Is data at risk?

#### Step 3: Initial Triage

```bash
# 1. Check API health
curl https://api.example.com/health

# 2. Check recent deployments
kubectl get pods -l app=hotswap-api -o wide

# 3. Check error logs (last 5 minutes)
kubectl logs -l app=hotswap-api --since=5m | grep -i error | tail -50

# 4. Check Prometheus metrics
# Navigate to Grafana dashboard
```

### 3. Investigation and Mitigation (< 15 minutes)

#### Step 1: Gather Information

```bash
# System metrics
kubectl top pods -l app=hotswap-api

# Database status
psql -h postgres.example.com -U hotswap -c "SELECT version(); SELECT pg_is_in_recovery();"

# Recent changes
git log --since="1 hour ago" --oneline

# Deployment history
kubectl rollout history deployment/hotswap-api
```

#### Step 2: Identify Root Cause

Common patterns:
- **Sudden spike in errors** → Recent code deployment
- **Gradual degradation** → Resource exhaustion (memory leak, connection pool)
- **Complete outage** → Infrastructure issue (database, network)
- **Intermittent failures** → Rate limiting or distributed lock contention

#### Step 3: Immediate Mitigation

**Option A: Rollback Recent Deployment**
```bash
# Rollback to previous version
kubectl rollout undo deployment/hotswap-api

# Verify rollback
kubectl rollout status deployment/hotswap-api
```

**Option B: Scale Up Resources**
```bash
# Increase replica count
kubectl scale deployment/hotswap-api --replicas=6

# Increase resource limits
kubectl set resources deployment/hotswap-api -c=hotswap-api --limits=cpu=2,memory=4Gi
```

**Option C: Restart Affected Services**
```bash
# Restart all API pods (rolling restart)
kubectl rollout restart deployment/hotswap-api

# Restart single pod
kubectl delete pod <pod-name>
```

**Option D: Enable Maintenance Mode**
```bash
# Return 503 for all requests (if complete outage better than degraded state)
kubectl scale deployment/hotswap-api --replicas=0

# Update load balancer to show maintenance page
```

### 4. Resolution (< 1 hour)

#### Step 1: Implement Permanent Fix

```bash
# Create hotfix branch
git checkout -b hotfix/incident-$(date +%Y%m%d)

# Make changes
vim src/HotSwap.Distributed.Api/...

# Test locally
dotnet test

# Commit and push
git commit -m "fix: resolve incident #123 - <description>"
git push origin hotfix/incident-$(date +%Y%m%d)

# Deploy to staging
kubectl apply -f k8s/staging/

# Run smoke tests
./tests/smoke-tests.sh

# Deploy to production
kubectl apply -f k8s/production/
```

#### Step 2: Verify Resolution

```bash
# Check error rate (should return to < 1%)
curl https://api.example.com/metrics | grep http_req_failed

# Check latency (should be < 500ms p95)
curl https://api.example.com/metrics | grep http_req_duration

# Run health check
./scripts/health-check.sh

# Monitor for 15 minutes
watch -n 10 'kubectl get pods -l app=hotswap-api'
```

### 5. Communication

#### During Incident

**Update Frequency:**
- SEV-1: Every 15 minutes
- SEV-2: Every 30 minutes
- SEV-3: Every 1 hour

**Communication Channels:**
- Status page: https://status.example.com
- Slack: #incidents channel
- Email: [email protected]

**Status Update Template:**
```
**Incident Update - <timestamp>**
Severity: SEV-2
Status: Investigating / Identified / Mitigating / Resolved

Current Impact:
- <Description of user-facing impact>

What we know:
- <Summary of findings so far>

Next steps:
- <What we're doing next>

ETA to resolution: <best estimate>
```

#### After Resolution

**Final Update:**
```
**Incident Resolved - <timestamp>**
Severity: SEV-2
Duration: <start time> to <end time> (<total duration>)

Root Cause:
- <Brief description of root cause>

Resolution:
- <What we did to fix it>

Follow-up Actions:
- <Link to post-mortem>
- <Preventive measures being implemented>

Apologies for the disruption. Service is now fully operational.
```

---

## Common Incidents

### API Outage Runbook

**Symptoms:**
- `/health` endpoint returns 5xx or timeout
- All API pods in CrashLoopBackOff
- No traffic reaching API

**Investigation Steps:**

1. **Check pod status:**
```bash
kubectl get pods -l app=hotswap-api
kubectl describe pod <failing-pod>
kubectl logs <failing-pod> --tail=100
```

2. **Common causes:**
- Database migration failed (check migration logs)
- Vault unavailable/sealed (check vault status)
- Configuration error (check appsettings.json)
- Out of memory (check resource limits)

3. **Resolution:**

**If database migration failed:**
```bash
# Rollback migration
cd src/HotSwap.Distributed.Infrastructure
dotnet ef database update <previous-migration>

# Restart pods
kubectl rollout restart deployment/hotswap-api
```

**If Vault sealed:**
```bash
# Unseal Vault (requires 3 of 5 unseal keys)
vault operator unseal <key1>
vault operator unseal <key2>
vault operator unseal <key3>

# Verify unsealed
vault status

# Restart API pods
kubectl rollout restart deployment/hotswap-api
```

**If OOM:**
```bash
# Increase memory limits
kubectl set resources deployment/hotswap-api -c=hotswap-api --limits=memory=4Gi

# Check for memory leaks
kubectl top pods -l app=hotswap-api
```

### High Error Rate Runbook

**Symptoms:**
- Error rate > 5% for > 5 minutes
- Prometheus alert: `HighErrorRate`

**Investigation Steps:**

1. **Identify error types:**
```bash
# Check error distribution
kubectl logs -l app=hotswap-api --since=10m | grep "ERROR" | cut -d: -f4 | sort | uniq -c | sort -rn

# Common errors to look for:
# - Database connection errors (connection pool exhausted)
# - Timeout errors (downstream service slow)
# - Authentication errors (Vault issues)
# - Validation errors (bad requests from clients)
```

2. **Check dependencies:**
```bash
# PostgreSQL status
psql -h postgres.example.com -U hotswap -c "SELECT count(*) FROM pg_stat_activity;"

# Vault status
vault status

# Network connectivity
kubectl exec -it <api-pod> -- ping postgres.example.com
```

3. **Resolution:**

**If database connection pool exhausted:**
```bash
# Check active connections
psql -h postgres.example.com -U hotswap -c "
SELECT count(*), state
FROM pg_stat_activity
WHERE datname = 'hotswap'
GROUP BY state;
"

# Kill idle connections (if safe)
psql -h postgres.example.com -U hotswap -c "
SELECT pg_terminate_backend(pid)
FROM pg_stat_activity
WHERE datname = 'hotswap'
  AND state = 'idle'
  AND state_change < NOW() - INTERVAL '10 minutes';
"

# Increase connection pool size
# Edit appsettings.json: MaxPoolSize=200
kubectl rollout restart deployment/hotswap-api
```

**If timeout errors:**
```bash
# Check downstream services
curl -w "%{time_total}\n" -o /dev/null -s https://downstream.example.com/health

# Increase timeout (temporarily)
# Edit appsettings.json: HttpClientTimeout=30000
kubectl apply -f k8s/configmap.yaml
kubectl rollout restart deployment/hotswap-api
```

### Database Outage Runbook

**Symptoms:**
- API returns database connection errors
- PostgreSQL pod not ready
- Slow query performance

**Investigation Steps:**

1. **Check PostgreSQL status:**
```bash
# Pod status
kubectl get pods -l app=postgresql

# Database logs
kubectl logs -l app=postgresql --tail=100

# Connect to database
psql -h postgres.example.com -U hotswap

# Check replication lag (if using replicas)
psql -h postgres.example.com -U hotswap -c "SELECT * FROM pg_stat_replication;"
```

2. **Resolution:**

**If PostgreSQL pod crashed:**
```bash
# Check persistent volume
kubectl get pv,pvc

# Restart PostgreSQL
kubectl rollout restart statefulset/postgresql

# Wait for ready
kubectl wait --for=condition=ready pod -l app=postgresql --timeout=300s

# Verify data integrity
psql -h postgres.example.com -U hotswap -c "SELECT count(*) FROM audit_logs;"
```

**If connection limit reached:**
```bash
# Check max connections
psql -h postgres.example.com -U hotswap -c "SHOW max_connections;"

# Check active connections
psql -h postgres.example.com -U hotswap -c "
SELECT count(*), application_name
FROM pg_stat_activity
GROUP BY application_name;
"

# Increase max_connections (requires restart)
# Edit postgresql.conf: max_connections = 300
kubectl rollout restart statefulset/postgresql
```

### Vault Sealed Runbook

**Symptoms:**
- API logs show Vault errors
- Deployments fail with "secret not found"
- JWT token validation fails

**Investigation Steps:**

1. **Check Vault status:**
```bash
vault status

# If sealed: Sealed = true
# If unsealed: Sealed = false
```

2. **Resolution (Unseal Vault):**

**Get unseal keys (stored securely):**
```bash
# Retrieve from secure location (password manager, encrypted file)
# Requires 3 of 5 keys (Shamir secret sharing)

# Unseal Vault
vault operator unseal <key-1>
vault operator unseal <key-2>
vault operator unseal <key-3>

# Verify unsealed
vault status | grep Sealed
# Expected: Sealed          false
```

**Restart API to reconnect to Vault:**
```bash
kubectl rollout restart deployment/hotswap-api

# Monitor logs for Vault connection success
kubectl logs -f deployment/hotswap-api | grep Vault
```

**Prevent auto-sealing:**
```bash
# Check Vault audit logs for seal trigger
vault audit list

# Review seal history
vault read sys/seal-status
```

---

## Post-Incident Procedures

### 1. Post-Mortem (Within 48 hours)

**Template:** [POST_MORTEM_TEMPLATE.md](./POST_MORTEM_TEMPLATE.md)

**Required Sections:**
1. **Timeline** - Minute-by-minute incident timeline
2. **Root Cause** - Technical root cause analysis
3. **Impact** - User impact, duration, affected services
4. **Resolution** - Steps taken to resolve
5. **Action Items** - Preventive measures with owners and due dates

**Meeting:** Schedule 1-hour post-mortem meeting with:
- Incident Commander
- On-call engineer(s)
- Engineering leads
- Product owner (if user-facing impact)

### 2. Follow-Up Actions

**Immediate (< 1 week):**
- [ ] Implement monitoring gap fixes
- [ ] Add missing alerts
- [ ] Update runbooks with new learnings

**Short-term (< 1 month):**
- [ ] Implement preventive measures
- [ ] Improve error handling
- [ ] Add resilience patterns (circuit breakers, retries)

**Long-term (< 3 months):**
- [ ] Architectural improvements
- [ ] Capacity planning adjustments
- [ ] Process improvements

### 3. Knowledge Base Update

- Add incident to runbook index
- Update troubleshooting guide
- Share learnings in team meeting

### 4. Metrics Tracking

Track and report monthly:
- Number of incidents by severity
- Mean time to detect (MTTD)
- Mean time to resolve (MTTR)
- Repeat incidents (same root cause)

**Goal:** Reduce MTTR and prevent repeat incidents

---

## Escalation Contacts

### Primary On-Call
- **Slack:** @on-call-engineer
- **PagerDuty:** Auto-routed
- **Phone:** See PagerDuty app

### Secondary On-Call
- **Slack:** @platform-lead
- **Phone:** See PagerDuty app

### Executive Escalation
- **VP Engineering:** [Slack/Phone]
- **CTO:** [Slack/Phone]

---

## Tools and Resources

### Incident Tools
- **PagerDuty:** https://yourorg.pagerduty.com
- **Status Page:** https://status.example.com (update via API)
- **Grafana:** https://grafana.example.com/d/hotswap
- **Kibana:** https://kibana.example.com
- **Slack:** #incidents channel

### Reference Documentation
- [Operations Guide](./OPERATIONS_GUIDE.md)
- [Troubleshooting Guide](./TROUBLESHOOTING_GUIDE.md)
- [Rollback Procedures](./ROLLBACK_PROCEDURES.md)
- [Prometheus Metrics](../PROMETHEUS_METRICS_GUIDE.md)

---

**Document Version:**

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2025-11-23 | Initial release |

**Next Review:** 2026-02-23
