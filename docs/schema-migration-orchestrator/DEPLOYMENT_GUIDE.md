# Deployment & Operations Guide

**Version:** 1.0.0
**Last Updated:** 2025-11-23

---

## Table of Contents

1. [Prerequisites](#prerequisites)
2. [Local Development Setup](#local-development-setup)
3. [Docker Deployment](#docker-deployment)
4. [Kubernetes Deployment](#kubernetes-deployment)
5. [Database Setup](#database-setup)
6. [Configuration](#configuration)
7. [Monitoring Setup](#monitoring-setup)
8. [Operational Runbooks](#operational-runbooks)

---

## Prerequisites

### Required Infrastructure

**Mandatory:**
- **.NET 8.0 Runtime** - Application runtime
- **PostgreSQL 15+** - Migration metadata storage
- **Redis 7+** - Distributed locks, execution state
- **Target Databases** - PostgreSQL, SQL Server, MySQL, or Oracle clusters

**Optional:**
- **Jaeger** - Distributed tracing
- **Prometheus** - Metrics collection
- **Grafana** - Metrics visualization

### System Requirements

**Migration Service:**
- CPU: 2+ cores
- Memory: 4 GB+ RAM
- Disk: 20 GB+ SSD
- Network: 1 Gbps

---

## Local Development Setup

### Quick Start

**Step 1: Install Prerequisites**

```bash
# Install .NET 8.0 SDK
wget https://dot.net/v1/dotnet-install.sh
bash dotnet-install.sh --channel 8.0

# Verify installation
dotnet --version  # Should show 8.0.x
```

**Step 2: Start Infrastructure**

```bash
# Clone repository
git clone https://github.com/scrawlsbenches/Claude-code-test.git
cd Claude-code-test

# Start Redis, PostgreSQL, test database
docker-compose -f docker-compose.migration.yml up -d
```

**docker-compose.migration.yml:**

```yaml
version: '3.8'

services:
  redis:
    image: redis:7-alpine
    ports:
      - "6379:6379"
    volumes:
      - redis-data:/data

  postgres-metadata:
    image: postgres:15-alpine
    ports:
      - "5432:5432"
    environment:
      POSTGRES_DB: migrations
      POSTGRES_USER: migration_admin
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
    volumes:
      - metadata-db:/var/lib/postgresql/data

  postgres-target:
    image: postgres:15-alpine
    ports:
      - "5433:5432"
    environment:
      POSTGRES_DB: app
      POSTGRES_USER: app_user
      POSTGRES_PASSWORD: ${TARGET_DB_PASSWORD}
    volumes:
      - target-db:/var/lib/postgresql/data

volumes:
  redis-data:
  metadata-db:
  target-db:
```

**Step 3: Configure Application**

```json
{
  "ConnectionStrings": {
    "Redis": "localhost:6379",
    "MetadataDB": "Host=localhost;Database=migrations;Username=migration_admin;Password=dev_password"
  },
  "Migration": {
    "MaxConcurrentExecutions": 10,
    "DefaultExecutionTimeout": "PT30M",
    "PerformanceMonitoringInterval": "PT5S"
  }
}
```

**Step 4: Initialize Database**

```bash
# Run metadata database migrations
dotnet ef database update --project src/HotSwap.SchemaMigration.Infrastructure
```

**Step 5: Run Application**

```bash
dotnet run --project src/HotSwap.SchemaMigration.Api

# Application starts on:
# HTTP:  http://localhost:5000
# HTTPS: https://localhost:5001
# Swagger: http://localhost:5000/swagger
```

---

## Database Setup

### Register Target Database

```bash
curl -X POST http://localhost:5000/api/v1/databases \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "production-db-cluster",
    "type": "PostgreSQL",
    "environment": "Production",
    "masterConnectionString": "Host=master.db.example.com;Database=app;Username=migrator;Password=***",
    "replicas": [
      {
        "replicaId": "replica-1",
        "connectionString": "Host=replica1.db.example.com;Database=app;Username=migrator;Password=***",
        "role": "AsyncReplica"
      }
    ]
  }'
```

### Verify Database Health

```bash
curl http://localhost:5000/api/v1/databases/db-123/health \
  -H "Authorization: Bearer $TOKEN"

# Response:
# {
#   "status": "Healthy",
#   "connections": 15,
#   "replicationLag": 2.5,
#   "cpuUsage": 45.2,
#   "lastCheck": "2025-11-23T12:00:00Z"
# }
```

---

## Docker Deployment

### Build Docker Image

**Dockerfile:**

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/HotSwap.SchemaMigration.Api/HotSwap.SchemaMigration.Api.csproj", "Api/"]
COPY ["src/HotSwap.SchemaMigration.Orchestrator/HotSwap.SchemaMigration.Orchestrator.csproj", "Orchestrator/"]
COPY ["src/HotSwap.SchemaMigration.Infrastructure/HotSwap.SchemaMigration.Infrastructure.csproj", "Infrastructure/"]
COPY ["src/HotSwap.SchemaMigration.Domain/HotSwap.SchemaMigration.Domain.csproj", "Domain/"]
RUN dotnet restore "Api/HotSwap.SchemaMigration.Api.csproj"

COPY src/ .
RUN dotnet publish "Api/HotSwap.SchemaMigration.Api.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

RUN useradd -m -u 1000 migration && chown -R migration:migration /app
USER migration

COPY --from=build /app/publish .

EXPOSE 5000
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
  CMD curl -f http://localhost:5000/health || exit 1

ENTRYPOINT ["dotnet", "HotSwap.SchemaMigration.Api.dll"]
```

---

## Kubernetes Deployment

### Migration Service Deployment

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: migration-service
  namespace: schema-migration
spec:
  replicas: 2
  selector:
    matchLabels:
      app: migration-service
  template:
    metadata:
      labels:
        app: migration-service
    spec:
      containers:
      - name: migration-service
        image: your-registry/schema-migration:1.0.0
        ports:
        - containerPort: 5000
        env:
        - name: ConnectionStrings__MetadataDB
          valueFrom:
            secretKeyRef:
              name: migration-secrets
              key: metadata-db-connection
        resources:
          limits:
            memory: "4Gi"
            cpu: "2"
          requests:
            memory: "2Gi"
            cpu: "1"
        livenessProbe:
          httpGet:
            path: /health
            port: 5000
          initialDelaySeconds: 30
          periodSeconds: 10
```

---

## Configuration

### Environment Variables

```bash
# Application
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://+:5000

# Database Connections
ConnectionStrings__Redis=redis:6379,password=secure_password
ConnectionStrings__MetadataDB=Host=metadata-db;Database=migrations;Username=admin;Password=***

# Migration Settings
Migration__MaxConcurrentExecutions=10
Migration__DefaultExecutionTimeout=PT30M
Migration__PerformanceMonitoringInterval=PT5S

# OpenTelemetry
OpenTelemetry__JaegerEndpoint=http://jaeger:14268/api/traces

# JWT
JWT__Secret=your_jwt_secret_key
JWT__ExpirationMinutes=60
```

---

## Monitoring Setup

### Prometheus Metrics

```yaml
# prometheus.yml
scrape_configs:
  - job_name: 'schema-migration'
    static_configs:
      - targets: ['migration-service:5000']
```

### Grafana Dashboard

```json
{
  "dashboard": {
    "title": "Schema Migration Orchestrator",
    "panels": [
      {
        "title": "Migrations in Progress",
        "targets": [
          {
            "expr": "migrations_in_progress"
          }
        ]
      },
      {
        "title": "Migration Success Rate",
        "targets": [
          {
            "expr": "rate(migrations_succeeded_total[5m]) / rate(migrations_executed_total[5m])"
          }
        ]
      }
    ]
  }
}
```

---

## Operational Runbooks

### Runbook 1: Migration Stuck in Running State

**Symptoms:**
- Migration status shows "Running" for > 1 hour
- No progress in execution logs

**Diagnosis:**
```bash
# Check execution status
curl http://localhost:5000/api/v1/migrations/{id}/executions/{execId} \
  -H "Authorization: Bearer $TOKEN"

# Check database locks
psql -h target-db -c "SELECT * FROM pg_locks WHERE NOT granted;"
```

**Resolution:**
```bash
# Cancel long-running query
psql -h target-db -c "SELECT pg_cancel_backend(pid) FROM pg_stat_activity WHERE query LIKE '%CREATE INDEX%';"

# Rollback migration
curl -X POST http://localhost:5000/api/v1/migrations/{id}/executions/{execId}/rollback \
  -H "Authorization: Bearer $TOKEN" \
  -d '{"reason": "Manual rollback - stuck migration"}'
```

---

### Runbook 2: Performance Degradation After Migration

**Symptoms:**
- Query latency increased > 50%
- User complaints about slow queries

**Diagnosis:**
```bash
# Check query performance
psql -h target-db -c "SELECT query, mean_exec_time FROM pg_stat_statements ORDER BY mean_exec_time DESC LIMIT 10;"

# Check missing indexes
psql -h target-db -c "SELECT schemaname, tablename, attname FROM pg_stats WHERE n_distinct < 0 AND correlation < 0.5;"
```

**Resolution:**
```bash
# Rollback migration
curl -X POST http://localhost:5000/api/v1/migrations/{id}/executions/{execId}/rollback \
  -H "Authorization: Bearer $TOKEN"

# Review migration script
# Adjust index strategy (e.g., add WHERE clause, use partial index)
# Re-execute with Canary strategy
```

---

### Runbook 3: Automatic Rollback Triggered

**Symptoms:**
- Migration automatically rolled back
- Alert: "Migration rolled back due to performance degradation"

**Investigation:**
```bash
# Get execution details
curl http://localhost:5000/api/v1/migrations/{id}/executions/{execId} \
  -H "Authorization: Bearer $TOKEN"

# Check performance snapshots
curl http://localhost:5000/api/v1/migrations/{id}/executions/{execId}/metrics \
  -H "Authorization: Bearer $TOKEN"
```

**Next Steps:**
1. Review performance baseline vs actual metrics
2. Adjust migration strategy (e.g., switch to Shadow)
3. Optimize migration script
4. Re-execute during low-traffic hours

---

**Last Updated:** 2025-11-23
**Version:** 1.0.0
**Deployment Status:** Design Phase
