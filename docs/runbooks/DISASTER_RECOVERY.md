# Disaster Recovery Runbook

**Version:** 1.0
**Last Updated:** 2025-11-23
**Purpose:** Complete system recovery procedures for catastrophic failures

---

## Recovery Time & Point Objectives

| Component | RTO | RPO | Priority |
|-----------|-----|-----|----------|
| API Service | < 15 min | 0 (stateless) | P0 - Critical |
| PostgreSQL Database | < 1 hour | < 1 hour | P0 - Critical |
| Vault Secrets | < 30 min | < 24 hours | P0 - Critical |
| Prometheus Metrics | < 30 min | < 1 hour | P1 - High |
| Configuration | < 15 min | 0 (in Git) | P1 - High |

---

## Disaster Scenarios

### 1. Complete Infrastructure Loss

**Scenario:** Data center failure, all infrastructure destroyed

**Recovery Steps:**

```bash
# === Phase 1: Infrastructure Provisioning (15 min) ===

# 1. Provision new Kubernetes cluster
terraform apply -var="cluster_name=hotswap-dr" -var="region=us-east-2"

# 2. Verify cluster access
kubectl cluster-info

# 3. Deploy core infrastructure
kubectl apply -f k8s/namespace.yaml
kubectl apply -f k8s/network-policies.yaml

# === Phase 2: Restore Vault (30 min) ===

# 1. Deploy Vault
kubectl apply -f k8s/vault/

# 2. Initialize Vault (if backup unavailable)
vault operator init -key-shares=5 -key-threshold=3

# 3. Unseal Vault
vault operator unseal <key-1>
vault operator unseal <key-2>
vault operator unseal <key-3>

# 4. Restore secrets from backup
vault kv put secret/hotswap/jwt-signing-key value="$(cat /backup/jwt-key.txt)"
vault kv put secret/hotswap/db-password value="$(cat /backup/db-pass.txt)"

# === Phase 3: Restore PostgreSQL (1 hour) ===

# 1. Deploy PostgreSQL
kubectl apply -f k8s/postgresql/

# 2. Wait for ready
kubectl wait --for=condition=ready pod -l app=postgresql --timeout=300s

# 3. Restore from latest backup
pg_restore -h postgresql.example.com -U hotswap -d hotswap \
  < /backup/postgresql/hotswap-$(date -d yesterday +%Y%m%d).dump

# 4. Verify data
psql -h postgresql.example.com -U hotswap -c "
SELECT
  'audit_logs' as table, COUNT(*) as rows FROM audit_logs
UNION ALL
SELECT 'deployments', COUNT(*) FROM deployments;"

# === Phase 4: Deploy API (15 min) ===

# 1. Create ConfigMaps and Secrets
kubectl apply -f k8s/configmap.yaml
kubectl create secret generic hotswap-secrets \
  --from-literal=vault-token="$(cat /backup/vault-token.txt)"

# 2. Deploy API
kubectl apply -f k8s/deployment.yaml
kubectl apply -f k8s/service.yaml
kubectl apply -f k8s/ingress.yaml

# 3. Wait for ready
kubectl wait --for=condition=available deployment/hotswap-api --timeout=300s

# 4. Verify health
curl https://api.example.com/health

# === Phase 5: Restore Monitoring (30 min) ===

# 1. Deploy Prometheus
kubectl apply -f k8s/prometheus/

# 2. Deploy Grafana
kubectl apply -f k8s/grafana/

# 3. Import dashboards
curl -X POST https://grafana.example.com/api/dashboards/db \
  -H "Content-Type: application/json" \
  -d @grafana-dashboard.json
```

**Total Recovery Time:** ~2 hours

---

### 2. Database Corruption

**Scenario:** PostgreSQL data corruption, database unusable

**Recovery Steps:**

```bash
# 1. Stop API to prevent writes
kubectl scale deployment/hotswap-api --replicas=0

# 2. Assess corruption
psql -h postgresql.example.com -U hotswap -c "
SELECT pg_database.datname,
       pg_size_pretty(pg_database_size(pg_database.datname)) AS size
FROM pg_database;"

# 3. Drop corrupted database
psql -h postgresql.example.com -U postgres -c "DROP DATABASE hotswap;"

# 4. Create new database
psql -h postgresql.example.com -U postgres -c "CREATE DATABASE hotswap OWNER hotswap;"

# 5. Restore from backup
pg_restore -h postgresql.example.com -U hotswap -d hotswap \
  < /backup/postgresql/hotswap-latest.dump

# 6. Verify restoration
psql -h postgresql.example.com -U hotswap -c "
SELECT COUNT(*) FROM audit_logs;
SELECT COUNT(*) FROM deployments;
SELECT COUNT(*) FROM approval_requests;"

# 7. Run migrations (if schema changed)
cd src/HotSwap.Distributed.Infrastructure
dotnet ef database update

# 8. Restart API
kubectl scale deployment/hotswap-api --replicas=3

# 9. Verify functionality
curl -X POST https://api.example.com/api/v1/deployments \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"moduleName":"test","moduleVersion":"1.0.0","targetEnvironment":"Development"}'
```

---

### 3. Vault Data Loss

**Scenario:** Vault unsealing keys lost or vault data corrupted

**Recovery Steps:**

**If Vault Snapshots Available:**

```bash
# 1. Stop Vault
kubectl scale statefulset/vault --replicas=0

# 2. Restore snapshot
vault operator raft snapshot restore /backup/vault/snapshot-latest.snap

# 3. Start Vault
kubectl scale statefulset/vault --replicas=1

# 4. Unseal with existing keys
vault operator unseal <key-1>
vault operator unseal <key-2>
vault operator unseal <key-3>

# 5. Verify secrets
vault kv get secret/hotswap/jwt-signing-key
```

**If No Snapshots (Emergency):**

```bash
# 1. Generate new JWT signing key
openssl rand -base64 64 > /tmp/new-jwt-key.txt

# 2. Initialize new Vault
vault operator init -key-shares=5 -key-threshold=3 > /tmp/vault-init.txt

# 3. Store unseal keys securely!
cat /tmp/vault-init.txt | grep "Unseal Key"

# 4. Unseal
vault operator unseal <new-key-1>
vault operator unseal <new-key-2>
vault operator unseal <new-key-3>

# 5. Authenticate
vault login $(cat /tmp/vault-init.txt | grep "Root Token" | cut -d: -f2 | tr -d ' ')

# 6. Recreate secrets
vault kv put secret/hotswap/jwt-signing-key value="$(cat /tmp/new-jwt-key.txt)"
vault kv put secret/hotswap/db-password value="NEW_PASSWORD"

# 7. Update database password
psql -h postgresql.example.com -U postgres -c "ALTER USER hotswap PASSWORD 'NEW_PASSWORD';"

# 8. Restart API to pick up new secrets
kubectl rollout restart deployment/hotswap-api

# CRITICAL: All existing JWT tokens are now invalid!
# Users must re-authenticate.
```

---

### 4. Complete Git Repository Loss

**Scenario:** All code and configuration lost

**Recovery Steps:**

```bash
# 1. Clone from backup/mirror
git clone https://backup-git.example.com/hotswap-distributed.git

# OR restore from local backups
tar -xzf /backup/git/hotswap-$(date +%Y%m%d).tar.gz

# 2. Verify integrity
cd hotswap-distributed
git fsck --full
git log --oneline | head -20

# 3. Push to new repository
git remote add origin https://github.com/yourorg/hotswap-distributed.git
git push -u origin main

# 4. Redeploy from code
dotnet build --configuration Release
docker build -t hotswap-api:latest .
docker push hotswap-api:latest

# 5. Apply manifests
kubectl apply -f k8s/
```

---

## Backup Procedures

### Automated Daily Backups

**1. PostgreSQL Backup (2:00 AM UTC)**

```bash
#!/bin/bash
# /opt/backups/backup-postgresql.sh

BACKUP_DIR=/backup/postgresql
DATE=$(date +%Y%m%d)

# Create backup
pg_dump -h postgresql.example.com -U hotswap -Fc hotswap > $BACKUP_DIR/hotswap-$DATE.dump

# Upload to S3/MinIO
aws s3 cp $BACKUP_DIR/hotswap-$DATE.dump s3://hotswap-backups/postgresql/

# Retain last 30 days
find $BACKUP_DIR -name "hotswap-*.dump" -mtime +30 -delete

# Log
echo "$(date): PostgreSQL backup completed" >> /var/log/backups.log
```

**2. Vault Snapshot (3:00 AM UTC)**

```bash
#!/bin/bash
# /opt/backups/backup-vault.sh

BACKUP_DIR=/backup/vault
DATE=$(date +%Y%m%d)

# Create snapshot
vault operator raft snapshot save $BACKUP_DIR/snapshot-$DATE.snap

# Upload to S3/MinIO
aws s3 cp $BACKUP_DIR/snapshot-$DATE.snap s3://hotswap-backups/vault/

# Retain last 90 days
find $BACKUP_DIR -name "snapshot-*.snap" -mtime +90 -delete
```

**3. Git Repository Backup (4:00 AM UTC)**

```bash
#!/bin/bash
# /opt/backups/backup-git.sh

BACKUP_DIR=/backup/git
DATE=$(date +%Y%m%d)

# Clone bare repository
git clone --mirror https://github.com/yourorg/hotswap-distributed.git $BACKUP_DIR/hotswap-$DATE

# Create tarball
tar -czf $BACKUP_DIR/hotswap-$DATE.tar.gz -C $BACKUP_DIR hotswap-$DATE

# Upload to S3/MinIO
aws s3 cp $BACKUP_DIR/hotswap-$DATE.tar.gz s3://hotswap-backups/git/

# Retain last 90 days
find $BACKUP_DIR -name "hotswap-*.tar.gz" -mtime +90 -delete
```

---

## Backup Verification

**Monthly Backup Test (First Monday)**

```bash
# 1. Spin up test environment
terraform apply -var="cluster_name=hotswap-dr-test"

# 2. Restore from backups
pg_restore -h test-pg.example.com -U hotswap -d hotswap < /backup/postgresql/hotswap-latest.dump

# 3. Deploy API
kubectl apply -f k8s/ --namespace=dr-test

# 4. Run smoke tests
./tests/smoke-tests.sh --environment=dr-test

# 5. Document results
echo "$(date): DR test completed - Result: PASS" >> /var/log/dr-tests.log

# 6. Tear down test environment
terraform destroy -var="cluster_name=hotswap-dr-test"
```

---

## Emergency Contact Information

| Role | Contact | Availability |
|------|---------|--------------|
| On-Call Engineer | PagerDuty auto-dial | 24/7 |
| Database Admin | [email protected] | 24/7 |
| Infrastructure Lead | [email protected] | Business hours |
| CTO | [email protected] | Emergencies only |

**Escalation Path:**
1. On-Call Engineer (0-15 min)
2. Database Admin (if DB issue)
3. Infrastructure Lead (if infra issue)
4. CTO (if data loss or security breach)

---

## Post-Recovery Checklist

- [ ] All services healthy (`kubectl get pods`)
- [ ] Database restored and verified
- [ ] Vault secrets accessible
- [ ] API responding to requests
- [ ] Metrics flowing to Prometheus
- [ ] Grafana dashboards working
- [ ] Load balancer routing correctly
- [ ] SSL certificates valid
- [ ] Backup jobs running
- [ ] Monitoring alerts configured
- [ ] Post-mortem scheduled
- [ ] Incident documentation updated
- [ ] Stakeholders notified of recovery

---

## Lessons Learned Template

After every disaster recovery event, document:

1. **What Happened:**
   - Time of incident
   - Root cause
   - Impact (users affected, data lost)

2. **What Went Well:**
   - Procedures that worked
   - Tools that helped
   - Quick wins

3. **What Went Wrong:**
   - Procedures that failed
   - Missing documentation
   - Delays encountered

4. **Action Items:**
   - Runbook updates needed
   - Automation opportunities
   - Training required
   - Infrastructure improvements

---

## References

- [Incident Response Runbook](./INCIDENT_RESPONSE_RUNBOOK.md)
- [Rollback Procedures](./ROLLBACK_PROCEDURES.md)
- [Operations Guide](./OPERATIONS_GUIDE.md)
- [Backup Verification Procedures](./BACKUP_VERIFICATION.md)

---

**Document Version:**

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2025-11-23 | Initial release |

**Next Review:** 2026-02-23

**Next DR Test:** First Monday of each month
