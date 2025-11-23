# Operations Guide - Distributed Kernel Orchestration System

**Version:** 1.0
**Last Updated:** 2025-11-23
**Audience:** DevOps, SRE, Operations Teams

---

## Table of Contents

1. [Overview](#overview)
2. [System Architecture](#system-architecture)
3. [Getting Started](#getting-started)
4. [Operational Procedures](#operational-procedures)
5. [Monitoring and Alerting](#monitoring-and-alerting)
6. [Runbooks](#runbooks)
7. [Performance Tuning](#performance-tuning)
8. [Security Operations](#security-operations)
9. [Disaster Recovery](#disaster-recovery)
10. [Appendices](#appendices)

---

## Overview

This guide provides comprehensive operational procedures for the Distributed Kernel Orchestration System. It covers day-to-day operations, incident response, troubleshooting, and disaster recovery.

### System Purpose

The Distributed Kernel Orchestration System enables:
- **Hot-swappable kernel deployments** across distributed clusters
- **Multi-strategy deployment patterns** (Direct, Rolling, Blue-Green, Canary)
- **Approval workflows** for staging and production deployments
- **Real-time monitoring** via Prometheus and WebSocket streaming
- **Multi-tenant isolation** for SaaS deployment models

### Key Components

| Component | Purpose | Technology |
|-----------|---------|------------|
| **API Server** | REST API for deployment operations | ASP.NET Core 8.0 |
| **Orchestrator** | Deployment pipeline execution | .NET 8.0 |
| **PostgreSQL** | Audit logs, approvals, jobs, messages | PostgreSQL 15+ |
| **Prometheus** | Metrics collection | /metrics endpoint |
| **Grafana** | Metrics visualization | Dashboard included |
| **Vault** | Secret management and rotation | HashiCorp Vault |

### Service Level Objectives (SLOs)

| Metric | Target | Measurement |
|--------|--------|-------------|
| API Availability | 99.9% | Monthly uptime |
| API Latency (p95) | < 500ms | /deployments endpoint |
| API Latency (p99) | < 1000ms | /deployments endpoint |
| Deployment Success Rate | > 95% | Succeeded / Total |
| Metrics Endpoint Latency | < 100ms | /metrics p95 |

---

## System Architecture

### High-Level Architecture

```
┌─────────────────┐
│   Load Balancer │
│   (Nginx/HAProxy)│
└────────┬────────┘
         │
    ┌────┴────┐
    │         │
┌───▼───┐ ┌──▼────┐
│ API 1 │ │ API 2 │  (Horizontal scaling)
└───┬───┘ └──┬────┘
    │        │
    └────┬───┘
         │
    ┌────▼──────────┐
    │  PostgreSQL   │  (Distributed locks, jobs, approvals)
    │  Cluster      │
    └───────────────┘
         │
    ┌────▼──────────┐
    │  HashiCorp    │  (Secret storage and rotation)
    │  Vault        │
    └───────────────┘
```

### Data Flow

**Deployment Request:**
```
User → API → DeploymentJobQueue → Background Processor → Orchestrator → Cluster Nodes
              (PostgreSQL)            (Async processing)   (Pipeline)
```

**Approval Workflow:**
```
User → API → ApprovalService → PostgreSQL → BackgroundService → Timeout/Auto-Reject
                                              (5-min interval)
```

### Critical Paths

1. **Deployment Creation** (Synchronous):
   - API receives POST /deployments
   - Validates request (JWT, rate limiting)
   - Creates deployment job in PostgreSQL
   - Returns 202 Accepted

2. **Deployment Execution** (Asynchronous):
   - Background processor claims job (FOR UPDATE SKIP LOCKED)
   - Orchestrator executes pipeline stages
   - Updates job status in PostgreSQL
   - Sends WebSocket notifications

3. **Metrics Scraping** (High frequency):
   - Prometheus scrapes /metrics every 15s
   - Must complete in < 100ms
   - No authentication required

---

## Getting Started

### Prerequisites

- Docker and Docker Compose (for local development)
- kubectl and Helm (for Kubernetes deployment)
- PostgreSQL 15+ client tools
- HashiCorp Vault CLI
- Prometheus and Grafana
- k6 (for load testing)

### Local Development Setup

```bash
# 1. Clone repository
git clone https://github.com/yourorg/hotswap-distributed.git
cd hotswap-distributed

# 2. Start infrastructure dependencies
docker-compose up -d postgres vault

# 3. Apply database migrations
cd src/HotSwap.Distributed.Infrastructure
dotnet ef database update

# 4. Configure secrets
export VAULT_ADDR="http://localhost:8200"
vault login
vault kv put secret/hotswap/jwt-signing-key value="your-secret-key"

# 5. Start API (Development mode)
cd ../HotSwap.Distributed.Api
dotnet run --configuration Debug

# 6. Verify health
curl http://localhost:5000/health
```

### Production Deployment

See: [Production Deployment Guide](./PRODUCTION_DEPLOYMENT.md)

---

## Operational Procedures

### Daily Operations

#### Health Check Procedure

**Frequency:** Every 15 minutes (automated)

```bash
# 1. Check API health
curl https://api.example.com/health

# Expected: 200 OK, {"status": "Healthy"}

# 2. Check PostgreSQL connection
curl https://api.example.com/health/database

# 3. Check Vault connection
curl https://api.example.com/health/vault
```

#### Monitoring Dashboard Review

**Frequency:** Daily (9:00 AM)

1. Open Grafana dashboard: `https://grafana.example.com/d/hotswap`
2. Review key metrics:
   - Request rate (should be stable, no sudden spikes)
   - Error rate (should be < 1%)
   - Latency p95 (should be < 500ms)
   - Active deployments (should be reasonable for time of day)
3. Check for alerts (0 critical alerts expected)

#### Log Review

**Frequency:** Daily (9:00 AM)

```bash
# Check for errors in last 24 hours
kubectl logs -l app=hotswap-api --since=24h | grep -i error | wc -l

# Review critical errors
kubectl logs -l app=hotswap-api --since=24h | grep -i "critical"
```

### Weekly Operations

#### Database Maintenance

**Frequency:** Weekly (Sunday 2:00 AM)

```bash
# 1. Vacuum PostgreSQL
psql -h postgres.example.com -U hotswap -c "VACUUM ANALYZE;"

# 2. Check table bloat
psql -h postgres.example.com -U hotswap -c "
SELECT schemaname, tablename, pg_size_pretty(pg_total_relation_size(schemaname||'.'||tablename)) AS size
FROM pg_tables
WHERE schemaname = 'public'
ORDER BY pg_total_relation_size(schemaname||'.'||tablename) DESC
LIMIT 10;
"

# 3. Review slow queries
psql -h postgres.example.com -U hotswap -c "
SELECT query, calls, total_time, mean_time
FROM pg_stat_statements
ORDER BY mean_time DESC
LIMIT 10;
"
```

#### Certificate Renewal Check

**Frequency:** Weekly (Monday 9:00 AM)

```bash
# Check certificate expiration
echo | openssl s_client -servername api.example.com -connect api.example.com:443 2>/dev/null | openssl x509 -noout -dates

# Expected: notAfter should be > 30 days from now
```

### Monthly Operations

#### Capacity Planning Review

**Frequency:** Monthly (First Monday)

1. Review resource usage trends:
   - CPU utilization (target: < 70% average)
   - Memory utilization (target: < 80% average)
   - Disk usage (target: < 75%)
   - Database connection pool (target: < 80% utilization)

2. Forecast capacity needs for next quarter

3. Plan scaling actions if needed

#### Security Patch Review

**Frequency:** Monthly (Second Tuesday)

```bash
# Check for outdated NuGet packages
dotnet list package --outdated

# Update packages (in staging first)
dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer --version <latest>

# Run security scan
dotnet list package --vulnerable
```

---

## Monitoring and Alerting

### Prometheus Metrics

See: [Prometheus Metrics Guide](../PROMETHEUS_METRICS_GUIDE.md)

### Key Dashboards

1. **System Overview**
   - Request rate, error rate, latency
   - Active deployments, queued jobs
   - Resource utilization

2. **Deployment Pipeline**
   - Deployments by status (Succeeded, Failed, Running)
   - Deployment duration histogram
   - Approval request metrics

3. **Infrastructure**
   - PostgreSQL connection pool
   - Cache hit rate
   - Lock contention metrics

### Alert Rules

#### Critical Alerts (Page immediately)

| Alert | Condition | Action |
|-------|-----------|--------|
| API Down | /health fails for 2 minutes | Runbook: [API Outage](#api-outage-runbook) |
| High Error Rate | Error rate > 5% for 5 minutes | Runbook: [High Error Rate](#high-error-rate-runbook) |
| Database Down | PostgreSQL unavailable | Runbook: [Database Outage](#database-outage-runbook) |
| Vault Sealed | Vault sealed or unreachable | Runbook: [Vault Sealed](#vault-sealed-runbook) |

#### Warning Alerts (Investigate during business hours)

| Alert | Condition | Action |
|-------|-----------|--------|
| High Latency | p95 > 1000ms for 10 minutes | Runbook: [High Latency](#high-latency-runbook) |
| Low Success Rate | Deployment success < 90% for 1 hour | Review deployment logs |
| High Queue Depth | > 100 pending jobs for 15 minutes | Check background processor |
| Certificate Expiring | SSL cert expires in < 30 days | Renew certificate |

---

## Runbooks

Detailed runbooks for common operational scenarios:

1. [Incident Response Runbook](./INCIDENT_RESPONSE_RUNBOOK.md) - General incident handling
2. [Rollback Procedures](./ROLLBACK_PROCEDURES.md) - Deployment rollback steps
3. [Troubleshooting Guide](./TROUBLESHOOTING_GUIDE.md) - Common issues and solutions
4. [Database Operations](./DATABASE_OPERATIONS.md) - PostgreSQL administration
5. [Secret Rotation](./SECRET_ROTATION_RUNBOOK.md) - HashiCorp Vault secret management
6. [Disaster Recovery](./DISASTER_RECOVERY.md) - Complete system recovery

---

## Performance Tuning

### API Server Tuning

**Configuration:** `appsettings.json`

```json
{
  "Kestrel": {
    "Limits": {
      "MaxConcurrentConnections": 1000,
      "MaxConcurrentUpgradedConnections": 1000,
      "MaxRequestBodySize": 10485760
    }
  },
  "RateLimiting": {
    "GlobalLimit": 1000,
    "DeploymentLimit": 10
  }
}
```

### PostgreSQL Tuning

**Configuration:** `postgresql.conf`

```ini
# Connection pooling
max_connections = 200

# Memory settings
shared_buffers = 4GB
effective_cache_size = 12GB
work_mem = 16MB

# Write-ahead log
wal_buffers = 16MB
checkpoint_completion_target = 0.9
```

### Load Testing

See: [Load Testing Suite](../../tests/LoadTests/README.md)

---

## Security Operations

### Access Control

- **Admin Role:** Full access (approve/reject, all CRUD operations)
- **Deployer Role:** Create deployments, view all, no approval powers
- **Viewer Role:** Read-only access to deployments and metrics

### Secret Rotation

See: [SECRET_ROTATION_GUIDE.md](../SECRET_ROTATION_GUIDE.md)

**Rotation Schedule:**
- JWT Signing Key: Every 90 days
- Database Credentials: Every 60 days
- Vault Root Token: Every 180 days

### Security Scanning

```bash
# Weekly vulnerability scan
dotnet list package --vulnerable

# Monthly dependency audit
dotnet restore --verbosity detailed
```

---

## Disaster Recovery

### Backup Strategy

| Component | Frequency | Retention | Location |
|-----------|-----------|-----------|----------|
| PostgreSQL | Daily at 2AM | 30 days | S3/MinIO |
| Vault Snapshots | Daily at 3AM | 90 days | S3/MinIO |
| Configuration | On change | Forever | Git repository |

### Recovery Time Objectives (RTO)

- **API Server:** < 15 minutes (redeploy pods)
- **PostgreSQL:** < 1 hour (restore from backup)
- **Vault:** < 30 minutes (unseal + restore)
- **Full System:** < 2 hours

### Recovery Point Objectives (RPO)

- **Database:** < 1 hour (daily backups + WAL archiving)
- **Vault:** < 24 hours (daily snapshots)

See: [Disaster Recovery Runbook](./DISASTER_RECOVERY.md)

---

## Appendices

### A. Useful Commands

```bash
# View all deployments
curl -H "Authorization: Bearer $TOKEN" https://api.example.com/api/v1/deployments

# Get deployment status
curl -H "Authorization: Bearer $TOKEN" https://api.example.com/api/v1/deployments/{executionId}

# Approve deployment
curl -X POST -H "Authorization: Bearer $TOKEN" \
  https://api.example.com/api/v1/approvals/deployments/{executionId}/approve

# View metrics
curl https://api.example.com/metrics
```

### B. Environment Variables

| Variable | Purpose | Example |
|----------|---------|---------|
| `ASPNETCORE_ENVIRONMENT` | Environment name | Production |
| `ConnectionStrings__DefaultConnection` | PostgreSQL connection | Host=pg.example.com;Database=hotswap |
| `Vault__Address` | Vault URL | https://vault.example.com:8200 |
| `Vault__Token` | Vault auth token | s.xxxxxxxxx |

### C. Port Reference

| Port | Service | Protocol |
|------|---------|----------|
| 5000 | API HTTP | HTTP |
| 5001 | API HTTPS | HTTPS |
| 5432 | PostgreSQL | TCP |
| 8200 | Vault | HTTPS |
| 9090 | Prometheus | HTTP |
| 3000 | Grafana | HTTP |

### D. File Locations

| File | Purpose | Path |
|------|---------|------|
| Configuration | API settings | `/etc/hotswap/appsettings.json` |
| Logs | Application logs | `/var/log/hotswap/` |
| Certificates | SSL/TLS certs | `/etc/hotswap/certs/` |
| Backups | Database backups | `/backup/postgresql/` |

---

## Support and Escalation

### On-Call Rotation

- **Primary:** DevOps Team (PagerDuty)
- **Secondary:** Platform Engineering Team
- **Escalation:** VP Engineering

### Contact Information

- **Slack Channel:** #hotswap-operations
- **Email:** [email protected]
- **PagerDuty:** https://yourorg.pagerduty.com

### External Vendors

- **PostgreSQL Support:** Crunchy Data
- **Vault Support:** HashiCorp
- **Cloud Provider:** Self-hosted

---

**Document Version History:**

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 2025-11-23 | Operations Team | Initial release |

**Next Review:** 2026-02-23
