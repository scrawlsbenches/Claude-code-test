# Deployment & Operations Guide

**Version:** 1.0.0
**Last Updated:** 2025-11-23

---

## Table of Contents

1. [Prerequisites](#prerequisites)
2. [Local Development Setup](#local-development-setup)
3. [Production Deployment](#production-deployment)
4. [Monitoring & Alerting](#monitoring--alerting)
5. [Operational Runbooks](#operational-runbooks)
6. [Disaster Recovery](#disaster-recovery)

---

## Prerequisites

### Required Infrastructure

**Mandatory:**
- .NET 8.0 Runtime
- PostgreSQL 15+ (device registry, audit logs)
- Redis 7+ (distributed locks, caching)
- MinIO / S3 (firmware storage)

**Optional:**
- Jaeger (distributed tracing)
- Prometheus (metrics)
- Grafana (visualization)

### System Requirements

**Application Server:** 4+ cores, 8 GB RAM, 50 GB SSD
**PostgreSQL:** 4+ cores, 16 GB RAM, 500 GB SSD
**MinIO:** 2+ cores, 4 GB RAM, 1 TB SSD

---

## Local Development Setup

```bash
# Clone repository
git clone https://github.com/scrawlsbenches/Claude-code-test.git
cd Claude-code-test

# Start infrastructure
docker-compose -f docker-compose.medical-devices.yml up -d

# Initialize database
dotnet ef database update --project src/HotSwap.MedicalDevices.Infrastructure

# Run application
cd src/HotSwap.MedicalDevices.Api
dotnet run
```

Application will start at: https://localhost:5001

---

## Production Deployment

### Docker Deployment

```bash
# Build image
docker build -t medical-device-firmware-manager:1.0.0 .

# Run container
docker run -d --name medical-device-api -p 5001:5001 medical-device-firmware-manager:1.0.0
```

### Kubernetes Deployment

```bash
# Deploy to Kubernetes
kubectl create namespace medical-devices
kubectl apply -f k8s/deployment.yaml
kubectl apply -f k8s/service.yaml
```

---

## Monitoring & Alerting

### Prometheus Metrics

Key metrics:
- deployments_initiated_total
- deployments_completed_total
- rollbacks_initiated_total
- devices_registered_total
- devices_critical_count

### Grafana Dashboards

1. Medical Device Fleet Overview
2. Deployment Monitoring
3. Compliance Dashboard

### Critical Alerts

- HighDeploymentFailureRate
- DeviceCriticalHealth
- AuditLogTamperDetected

---

## Operational Runbooks

### Runbook 1: Deployment Failure

**Steps:**
1. Check deployment logs
2. Check device health
3. Initiate manual rollback if error rate > 5%
4. Investigate root cause
5. Generate incident report

### Runbook 2: Audit Log Tamper Detection

**Steps:**
1. CRITICAL - Notify regulatory affairs team immediately
2. Export affected audit logs
3. Validate tamper detection hash chain
4. Restore from backup if necessary
5. Generate FDA incident report
6. Investigate security breach

### Runbook 3: Firmware Rollback

**Steps:**
1. Identify deployment to rollback
2. Initiate rollback via API
3. Monitor rollback progress
4. Validate devices on previous firmware version
5. Notify affected hospitals
6. Document incident in audit log

---

## Disaster Recovery

### Backup Strategy

**Audit Logs (CRITICAL):**
- Frequency: Real-time + daily backup
- Retention: 7 years
- Storage: Immutable S3/Glacier

**Device Registry:**
- Frequency: Daily
- Retention: 30 days

**Firmware Binaries:**
- Frequency: Continuous
- Retention: Indefinite

### Recovery Procedures

**Database Failure:** Automatic failover to replica (RTO: 5 min, RPO: 0)
**Audit Log Restoration:** Restore from S3 backup
**Complete Data Center Loss:** Failover to DR site (RTO: 4 hours, RPO: 1 hour)

---

## Security Hardening

### Production Checklist

- [ ] HTTPS/TLS 1.3+ enforced
- [ ] Multi-factor authentication enabled
- [ ] RBAC policies configured
- [ ] Firmware encryption enabled (AES-256)
- [ ] Cryptographic signatures verified (RSA-4096)
- [ ] Audit log backups automated
- [ ] Tamper detection enabled
- [ ] 7-year retention configured
- [ ] Disaster recovery tested

---

**Last Updated:** 2025-11-23
**Operations Contact:** ops@example.com
**Regulatory Contact:** regulatory-affairs@example.com
