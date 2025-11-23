# Troubleshooting Guide

**Version:** 1.0
**Last Updated:** 2025-11-23
**Purpose:** Quick reference for diagnosing and resolving common issues

---

## Quick Diagnosis Flowchart

```
Is the API responding?
├─ No  → See [API Not Responding](#api-not-responding)
└─ Yes → Is error rate > 5%?
          ├─ Yes → See [High Error Rate](#high-error-rate)
          └─ No  → Is latency > 1s?
                   ├─ Yes → See [High Latency](#high-latency)
                   └─ No  → See [Specific Issues](#specific-issues)
```

---

## Common Issues

### API Not Responding

**Symptoms:**
- `/health` endpoint returns 503 or timeout
- No pods running
- Connection refused errors

**Diagnosis:**

```bash
# 1. Check pod status
kubectl get pods -l app=hotswap-api

# 2. Check pod logs
kubectl logs -l app=hotswap-api --tail=50

# 3. Check events
kubectl get events --sort-by='.lastTimestamp' | tail -20
```

**Common Causes:**

| Cause | Solution |
|-------|----------|
| **Pods CrashLoopBackOff** | Check logs: `kubectl logs <pod> --previous` |
| **ImagePullBackOff** | Verify image exists: `docker pull <image>` |
| **OOMKilled** | Increase memory: `kubectl set resources deployment/hotswap-api --limits=memory=4Gi` |
| **Database unreachable** | Check PostgreSQL: `psql -h postgres.example.com -U hotswap` |
| **Vault sealed** | Unseal Vault: See [INCIDENT_RESPONSE_RUNBOOK.md](./INCIDENT_RESPONSE_RUNBOOK.md#vault-sealed-runbook) |

---

### High Error Rate

**Symptoms:**
- Error rate > 5% in Prometheus metrics
- Many 500 errors in logs

**Diagnosis:**

```bash
# 1. Check error distribution
kubectl logs -l app=hotswap-api | grep ERROR | cut -d: -f3 | sort | uniq -c | sort -rn

# 2. Check recent deployments
kubectl rollout history deployment/hotswap-api

# 3. Check database connections
psql -h postgres.example.com -U hotswap -c "SELECT count(*), state FROM pg_stat_activity GROUP BY state;"
```

**Common Causes:**

| Error Pattern | Cause | Solution |
|---------------|-------|----------|
| `Npgsql.NpgsqlException: Connection pool exhausted` | Too many DB connections | Increase pool size or restart API |
| `System.TimeoutException` | Downstream service slow | Check dependencies, increase timeout |
| `UnauthorizedException` | JWT validation failing | Check Vault, verify signing key |
| `InvalidOperationException: Cannot resolve scoped service` | DI lifetime mismatch | Check service registrations |

---

### High Latency

**Symptoms:**
- P95 latency > 1 second
- Slow API responses
- Timeout errors

**Diagnosis:**

```bash
# 1. Check resource usage
kubectl top pods -l app=hotswap-api

# 2. Check database slow queries
psql -h postgres.example.com -U hotswap -c "
SELECT query, calls, total_time, mean_time
FROM pg_stat_statements
ORDER BY mean_time DESC
LIMIT 10;"

# 3. Check lock contention
psql -h postgres.example.com -U hotswap -c "
SELECT blocked_locks.pid, blocked_activity.query
FROM pg_catalog.pg_locks blocked_locks
JOIN pg_catalog.pg_stat_activity blocked_activity ON blocked_activity.pid = blocked_locks.pid;"
```

**Common Causes:**

| Symptom | Cause | Solution |
|---------|-------|----------|
| High CPU usage | Too much load | Scale up: `kubectl scale deployment/hotswap-api --replicas=6` |
| High memory usage | Memory leak | Restart pods: `kubectl rollout restart deployment/hotswap-api` |
| Slow database queries | Missing indexes | Add index, optimize query |
| Lock contention | Concurrent deployments | Use PostgreSQL advisory locks (already implemented) |

---

## Specific Issues

### Deployment Stuck in "Running" State

**Symptoms:**
- Deployment shows "Running" for > 15 minutes
- No progress in logs

**Diagnosis:**

```bash
# Check deployment status
curl -H "Authorization: Bearer $TOKEN" https://api.example.com/api/v1/deployments/<execution-id>

# Check background job processor
kubectl logs -l app=hotswap-api | grep DeploymentJobProcessor
```

**Solution:**

```bash
# 1. Check if job is locked
psql -h postgres.example.com -U hotswap -c "
SELECT id, deployment_id, status, locked_until
FROM deployment_jobs
WHERE status = 'Running' AND locked_until > NOW();"

# 2. If locked_until is in the past, unlock it
psql -h postgres.example.com -U hotswap -c "
UPDATE deployment_jobs
SET status = 'Failed', locked_until = NULL
WHERE id = <job-id>;"

# 3. Restart background processor
kubectl rollout restart deployment/hotswap-api
```

---

### Approval Workflow Not Working

**Symptoms:**
- Approval requests not created
- Timeouts not triggering
- Approvals not updating status

**Diagnosis:**

```bash
# 1. Check approval requests in database
psql -h postgres.example.com -U hotswap -c "
SELECT deployment_execution_id, status, requested_at, timeout_at
FROM approval_requests
ORDER BY requested_at DESC
LIMIT 10;"

# 2. Check background service logs
kubectl logs -l app=hotswap-api | grep ApprovalServiceRefactored
```

**Solution:**

```bash
# If timeout not working:
# 1. Check timeout background service is running
kubectl get pods -l app=hotswap-api

# 2. Manually expire old requests
psql -h postgres.example.com -U hotswap -c "
UPDATE approval_requests
SET status = 'Expired'
WHERE status = 'Pending' AND timeout_at < NOW();"
```

---

### WebSocket Connections Failing

**Symptoms:**
- Real-time updates not working
- WebSocket connection errors in browser

**Diagnosis:**

```bash
# 1. Check SignalR hub
kubectl logs -l app=hotswap-api | grep SignalR

# 2. Test WebSocket connection
wscat -c wss://api.example.com/hubs/deployment

# 3. Check connection count
kubectl logs -l app=hotswap-api | grep "Client connected"
```

**Solution:**

| Issue | Fix |
|-------|-----|
| CORS errors | Add origin to CORS policy in Program.cs |
| Connection timeout | Increase KeepAliveInterval in SignalR config |
| Authentication fails | Verify JWT token in WebSocket handshake |

---

### PostgreSQL Connection Issues

**Symptoms:**
- Connection pool exhausted errors
- Timeout connecting to database
- SSL/TLS errors

**Diagnosis:**

```bash
# 1. Check active connections
psql -h postgres.example.com -U hotswap -c "
SELECT count(*), application_name, state
FROM pg_stat_activity
WHERE datname = 'hotswap'
GROUP BY application_name, state;"

# 2. Check max connections
psql -h postgres.example.com -U hotswap -c "SHOW max_connections;"

# 3. Test connection from pod
kubectl exec -it <pod-name> -- psql -h postgres.example.com -U hotswap -c "SELECT 1;"
```

**Solution:**

```bash
# Increase connection pool size (temporary)
kubectl set env deployment/hotswap-api ConnectionStrings__MaxPoolSize=200

# OR restart to release connections
kubectl rollout restart deployment/hotswap-api

# Long-term: Tune PostgreSQL
# Edit postgresql.conf:
# max_connections = 300
# shared_buffers = 4GB
```

---

### Vault Integration Issues

**Symptoms:**
- JWT signing key not found
- Secret rotation failing
- `VaultSharp.VaultClientException`

**Diagnosis:**

```bash
# 1. Check Vault status
vault status

# 2. Test secret read
vault kv get secret/hotswap/jwt-signing-key

# 3. Check API logs
kubectl logs -l app=hotswap-api | grep Vault
```

**Solution:**

| Error | Fix |
|-------|-----|
| Vault sealed | Unseal with 3 of 5 keys |
| Permission denied | Verify Vault token has correct policy |
| Secret not found | Create secret: `vault kv put secret/hotswap/jwt-signing-key value="..."` |
| Connection timeout | Check network, Vault service running |

---

### Metrics Not Showing in Grafana

**Symptoms:**
- Empty Grafana dashboards
- Prometheus not scraping
- Metrics endpoint returns 404

**Diagnosis:**

```bash
# 1. Test metrics endpoint
curl https://api.example.com/metrics

# 2. Check Prometheus targets
curl http://prometheus.example.com:9090/api/v1/targets | jq '.data.activeTargets[] | select(.labels.job=="hotswap-api")'

# 3. Check Prometheus scrape config
kubectl get configmap prometheus-config -o yaml
```

**Solution:**

```bash
# If endpoint returns 404:
# Verify /metrics is not behind authentication
# Check Program.cs: app.MapMetrics() is called

# If Prometheus not scraping:
# 1. Add service monitor
kubectl apply -f k8s/servicemonitor.yaml

# 2. Verify Prometheus can reach pod
kubectl exec -it prometheus-0 -- wget -O- http://hotswap-api:5000/metrics
```

---

## Performance Tuning

### Optimize Database Queries

```sql
-- Find slow queries
SELECT
    queryid,
    query,
    calls,
    total_time::numeric(10,2) as total_ms,
    (total_time / calls)::numeric(10,2) as avg_ms,
    rows
FROM pg_stat_statements
WHERE calls > 100
ORDER BY total_time DESC
LIMIT 20;

-- Find missing indexes
SELECT
    schemaname,
    tablename,
    seq_scan,
    seq_tup_read,
    idx_scan,
    seq_tup_read / seq_scan AS avg_seq_tup_read
FROM pg_stat_user_tables
WHERE seq_scan > 0
  AND seq_tup_read / seq_scan > 10000
ORDER BY seq_tup_read DESC;
```

### Reduce Memory Usage

```bash
# Check memory usage per pod
kubectl top pods -l app=hotswap-api

# If memory growing continuously (memory leak):
# 1. Enable heap dump on OOM
kubectl set env deployment/hotswap-api DOTNET_EnableHeapDump=1

# 2. Analyze heap dump
dotnet-dump analyze /tmp/coredump.<pid>

# 3. Restart pods regularly (workaround)
# Add to cron: kubectl rollout restart deployment/hotswap-api
```

---

## Diagnostic Commands

```bash
# == General Health ==
kubectl get all -n default
kubectl get events --sort-by='.lastTimestamp'
kubectl describe deployment hotswap-api

# == Logs ==
kubectl logs -f deployment/hotswap-api
kubectl logs deployment/hotswap-api --all-containers=true --since=1h

# == Resource Usage ==
kubectl top nodes
kubectl top pods
kubectl describe node <node-name> | grep -A 5 "Allocated resources"

# == Network ==
kubectl exec -it <pod> -- netstat -tulpn
kubectl exec -it <pod> -- curl -v http://postgres.example.com:5432

# == Database ==
psql -h postgres.example.com -U hotswap

# In psql:
\dt                  -- List tables
\d+ approval_requests  -- Describe table
\dx                  -- List extensions
SELECT version();    -- PostgreSQL version
SELECT pg_size_pretty(pg_database_size('hotswap'));  -- Database size

# == Prometheus ==
curl -s 'http://prometheus:9090/api/v1/query?query=up{job="hotswap-api"}' | jq .
curl -s 'http://prometheus:9090/api/v1/query?query=rate(http_req_duration_seconds_sum[5m])' | jq .
```

---

## Getting Help

If issue persists after troubleshooting:

1. **Gather Information:**
   ```bash
   # Create support bundle
   kubectl logs deployment/hotswap-api --tail=1000 > api-logs.txt
   kubectl describe deployment hotswap-api > deployment-info.txt
   kubectl get events > events.txt
   curl https://api.example.com/metrics > metrics.txt
   ```

2. **Open Issue:**
   - Slack: #hotswap-support
   - PagerDuty: Page on-call engineer
   - GitHub: Create issue with support bundle

3. **Include:**
   - Symptom description
   - Time when issue started
   - Recent changes (deployments, config)
   - Error messages (full stack traces)
   - Support bundle files

---

## References

- [Incident Response Runbook](./INCIDENT_RESPONSE_RUNBOOK.md)
- [Rollback Procedures](./ROLLBACK_PROCEDURES.md)
- [Operations Guide](./OPERATIONS_GUIDE.md)
- [Prometheus Metrics Guide](../PROMETHEUS_METRICS_GUIDE.md)

---

**Document Version:**

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2025-11-23 | Initial release |

**Next Review:** 2026-02-23
