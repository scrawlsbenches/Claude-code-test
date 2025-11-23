# WASM Orchestrator Deployment Guide

**Version:** 1.0.0
**Last Updated:** 2025-11-23

---

## Table of Contents

1. [Prerequisites](#prerequisites)
2. [Infrastructure Setup](#infrastructure-setup)
3. [Application Deployment](#application-deployment)
4. [Edge Node Setup](#edge-node-setup)
5. [Configuration](#configuration)
6. [Monitoring & Observability](#monitoring--observability)
7. [Operational Runbooks](#operational-runbooks)
8. [Troubleshooting](#troubleshooting)
9. [Disaster Recovery](#disaster-recovery)

---

## Prerequisites

### System Requirements

**Orchestrator API Server:**
- .NET 8.0 Runtime
- CPU: 4+ cores
- Memory: 8 GB RAM minimum, 16 GB recommended
- Disk: 50 GB SSD
- OS: Linux (Ubuntu 22.04 LTS recommended), Windows Server 2022, or macOS

**Edge Nodes:**
- CPU: 2+ cores
- Memory: 4 GB RAM minimum, 8 GB recommended
- Disk: 20 GB SSD (for module caching)
- OS: Linux (Ubuntu 22.04 LTS recommended)
- Network: Low-latency connection to orchestrator API

**External Dependencies:**
- PostgreSQL 15+
- Redis 7+
- MinIO (or S3-compatible object storage)
- (Optional) Jaeger for distributed tracing
- (Optional) Prometheus + Grafana for metrics

---

## Infrastructure Setup

### 1. PostgreSQL Database

**Installation (Docker):**
```bash
docker run -d \
  --name postgres-wasm \
  -e POSTGRES_PASSWORD=your-secure-password \
  -e POSTGRES_DB=wasm_orchestrator \
  -p 5432:5432 \
  -v postgres-data:/var/lib/postgresql/data \
  postgres:15
```

**Database Schema:**
```bash
# Apply migrations
dotnet ef database update --project src/HotSwap.Wasm.Api
```

**Connection String:**
```json
{
  "ConnectionStrings": {
    "PostgreSQL": "Host=localhost;Port=5432;Database=wasm_orchestrator;Username=postgres;Password=your-secure-password"
  }
}
```

---

### 2. Redis Cache

**Installation (Docker):**
```bash
docker run -d \
  --name redis-wasm \
  -p 6379:6379 \
  -v redis-data:/data \
  redis:7 redis-server --appendonly yes
```

**Connection String:**
```json
{
  "Redis": {
    "ConnectionString": "localhost:6379"
  }
}
```

---

### 3. MinIO Object Storage

**Installation (Docker):**
```bash
docker run -d \
  --name minio-wasm \
  -p 9000:9000 \
  -p 9001:9001 \
  -e MINIO_ROOT_USER=minioadmin \
  -e MINIO_ROOT_PASSWORD=minioadmin-password \
  -v minio-data:/data \
  minio/minio server /data --console-address ":9001"
```

**Create WASM Modules Bucket:**
```bash
# Install MinIO client (mc)
wget https://dl.min.io/client/mc/release/linux-amd64/mc
chmod +x mc
./mc alias set local http://localhost:9000 minioadmin minioadmin-password

# Create bucket
./mc mb local/wasm-modules
./mc policy set public local/wasm-modules
```

**Configuration:**
```json
{
  "MinIO": {
    "Endpoint": "localhost:9000",
    "AccessKey": "minioadmin",
    "SecretKey": "minioadmin-password",
    "UseSSL": false,
    "BucketName": "wasm-modules"
  }
}
```

---

### 4. WASM Runtime Setup

**Install Wasmtime (for development/testing):**

```bash
# Linux/macOS
curl https://wasmtime.dev/install.sh -sSf | bash

# Windows (PowerShell)
iwr https://wasmtime.dev/install.ps1 -useb | iex

# Verify installation
wasmtime --version
```

The .NET application uses `Wasmtime.Dotnet` NuGet package, which includes the runtime.

---

### 5. Optional: Jaeger (Distributed Tracing)

**Installation (Docker):**
```bash
docker run -d \
  --name jaeger-wasm \
  -p 16686:16686 \
  -p 14268:14268 \
  jaegertracing/all-in-one:latest
```

**Access Jaeger UI:** http://localhost:16686

**Configuration:**
```json
{
  "OpenTelemetry": {
    "JaegerEndpoint": "http://localhost:14268/api/traces"
  }
}
```

---

### 6. Optional: Prometheus + Grafana

**Prometheus (Docker):**
```bash
docker run -d \
  --name prometheus-wasm \
  -p 9090:9090 \
  -v $(pwd)/prometheus.yml:/etc/prometheus/prometheus.yml \
  prom/prometheus
```

**prometheus.yml:**
```yaml
global:
  scrape_interval: 15s

scrape_configs:
  - job_name: 'wasm-orchestrator'
    static_configs:
      - targets: ['host.docker.internal:5000']
```

**Grafana (Docker):**
```bash
docker run -d \
  --name grafana-wasm \
  -p 3000:3000 \
  grafana/grafana
```

**Access Grafana:** http://localhost:3000 (admin/admin)

---

## Application Deployment

### Production Deployment

**1. Build Application:**
```bash
cd src/HotSwap.Wasm.Api
dotnet publish -c Release -o ../../publish
```

**2. Configure appsettings.Production.json:**
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "ConnectionStrings": {
    "PostgreSQL": "Host=postgres.example.com;Port=5432;Database=wasm_orchestrator;Username=app_user;Password=${DB_PASSWORD}"
  },
  "Redis": {
    "ConnectionString": "redis.example.com:6379,password=${REDIS_PASSWORD}"
  },
  "MinIO": {
    "Endpoint": "minio.example.com:9000",
    "AccessKey": "${MINIO_ACCESS_KEY}",
    "SecretKey": "${MINIO_SECRET_KEY}",
    "UseSSL": true,
    "BucketName": "wasm-modules"
  },
  "JWT": {
    "SecretKey": "${JWT_SECRET_KEY}",
    "Issuer": "HotSwap.Wasm.Api",
    "Audience": "HotSwap.Wasm.Clients",
    "ExpirationMinutes": 60
  },
  "Kestrel": {
    "Endpoints": {
      "Http": {
        "Url": "http://0.0.0.0:5000"
      },
      "Https": {
        "Url": "https://0.0.0.0:5001",
        "Certificate": {
          "Path": "/certs/cert.pfx",
          "Password": "${CERT_PASSWORD}"
        }
      }
    }
  }
}
```

**3. Deploy with Docker:**

**Dockerfile:**
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5000
EXPOSE 5001

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/HotSwap.Wasm.Api/HotSwap.Wasm.Api.csproj", "HotSwap.Wasm.Api/"]
RUN dotnet restore "HotSwap.Wasm.Api/HotSwap.Wasm.Api.csproj"
COPY src/ .
RUN dotnet build "HotSwap.Wasm.Api/HotSwap.Wasm.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "HotSwap.Wasm.Api/HotSwap.Wasm.Api.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "HotSwap.Wasm.Api.dll"]
```

**Build and Run:**
```bash
# Build image
docker build -t wasm-orchestrator:1.0.0 .

# Run container
docker run -d \
  --name wasm-orchestrator \
  -p 5000:5000 \
  -p 5001:5001 \
  -e DB_PASSWORD=secure-password \
  -e REDIS_PASSWORD=redis-password \
  -e MINIO_ACCESS_KEY=access-key \
  -e MINIO_SECRET_KEY=secret-key \
  -e JWT_SECRET_KEY=jwt-secret-key \
  -e CERT_PASSWORD=cert-password \
  -v /path/to/certs:/certs \
  wasm-orchestrator:1.0.0
```

**4. Deploy with Kubernetes:**

**deployment.yaml:**
```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: wasm-orchestrator
  namespace: wasm-system
spec:
  replicas: 3
  selector:
    matchLabels:
      app: wasm-orchestrator
  template:
    metadata:
      labels:
        app: wasm-orchestrator
    spec:
      containers:
      - name: api
        image: wasm-orchestrator:1.0.0
        ports:
        - containerPort: 5000
          name: http
        - containerPort: 5001
          name: https
        env:
        - name: DB_PASSWORD
          valueFrom:
            secretKeyRef:
              name: wasm-secrets
              key: db-password
        - name: REDIS_PASSWORD
          valueFrom:
            secretKeyRef:
              name: wasm-secrets
              key: redis-password
        - name: MINIO_ACCESS_KEY
          valueFrom:
            secretKeyRef:
              name: wasm-secrets
              key: minio-access-key
        - name: MINIO_SECRET_KEY
          valueFrom:
            secretKeyRef:
              name: wasm-secrets
              key: minio-secret-key
        - name: JWT_SECRET_KEY
          valueFrom:
            secretKeyRef:
              name: wasm-secrets
              key: jwt-secret-key
        resources:
          requests:
            memory: "4Gi"
            cpu: "2"
          limits:
            memory: "8Gi"
            cpu: "4"
        livenessProbe:
          httpGet:
            path: /health
            port: 5000
          initialDelaySeconds: 30
          periodSeconds: 10
        readinessProbe:
          httpGet:
            path: /health/ready
            port: 5000
          initialDelaySeconds: 10
          periodSeconds: 5
---
apiVersion: v1
kind: Service
metadata:
  name: wasm-orchestrator
  namespace: wasm-system
spec:
  selector:
    app: wasm-orchestrator
  ports:
  - name: http
    port: 80
    targetPort: 5000
  - name: https
    port: 443
    targetPort: 5001
  type: LoadBalancer
```

**Apply:**
```bash
kubectl create namespace wasm-system
kubectl create secret generic wasm-secrets \
  --from-literal=db-password=secure-password \
  --from-literal=redis-password=redis-password \
  --from-literal=minio-access-key=access-key \
  --from-literal=minio-secret-key=secret-key \
  --from-literal=jwt-secret-key=jwt-secret-key \
  -n wasm-system

kubectl apply -f deployment.yaml
```

---

## Edge Node Setup

### Edge Node Requirements

**Software:**
- WASM Runtime (Wasmtime, WasmEdge, or Wasmer)
- Edge Node Agent (custom application)
- Docker (optional, for containerized deployment)

### Edge Node Agent

**Purpose:** Receives module deployments from orchestrator, loads WASM modules, and executes functions.

**Key Responsibilities:**
- Register with orchestrator API
- Download WASM modules from MinIO
- Load modules into WASM runtime
- Execute function invocations
- Report health metrics

**Configuration:**

**edge-node-config.json:**
```json
{
  "NodeId": "edge-us-east-01",
  "Region": "us-east",
  "Zone": "us-east-1a",
  "OrchestratorApiUrl": "https://api.example.com",
  "Runtime": "Wasmtime",
  "RuntimeVersion": "15.0.0",
  "MaxModules": 1000,
  "HeartbeatInterval": "PT30S",
  "Hardware": {
    "CpuCores": 8,
    "TotalMemoryMB": 16384,
    "Architecture": "x86_64"
  }
}
```

**Deployment:**
```bash
# Install edge node agent
wget https://releases.example.com/wasm-edge-agent/v1.0.0/edge-agent-linux-x64
chmod +x edge-agent-linux-x64

# Run edge node agent
./edge-agent-linux-x64 --config edge-node-config.json

# Or run as systemd service
sudo cp edge-agent-linux-x64 /usr/local/bin/wasm-edge-agent
sudo cp wasm-edge-agent.service /etc/systemd/system/
sudo systemctl enable wasm-edge-agent
sudo systemctl start wasm-edge-agent
```

**systemd service:**

**/etc/systemd/system/wasm-edge-agent.service:**
```ini
[Unit]
Description=WASM Edge Node Agent
After=network.target

[Service]
Type=simple
User=wasm-agent
WorkingDirectory=/opt/wasm-edge-agent
ExecStart=/usr/local/bin/wasm-edge-agent --config /etc/wasm-edge-agent/config.json
Restart=always
RestartSec=10

[Install]
WantedBy=multi-user.target
```

---

## Configuration

### Environment-Specific Configuration

**Development:**
```json
{
  "Environment": "Development",
  "MinIO": {
    "Endpoint": "localhost:9000",
    "UseSSL": false
  },
  "RequireApproval": false
}
```

**Staging:**
```json
{
  "Environment": "Staging",
  "MinIO": {
    "Endpoint": "minio-staging.example.com:9000",
    "UseSSL": true
  },
  "RequireApproval": true
}
```

**Production:**
```json
{
  "Environment": "Production",
  "MinIO": {
    "Endpoint": "minio.example.com:9000",
    "UseSSL": true
  },
  "RequireApproval": true,
  "RateLimiting": {
    "ModuleRegistration": 10,
    "ModuleExecution": 1000
  }
}
```

---

## Monitoring & Observability

### Health Checks

**Orchestrator Health:**
```bash
curl https://api.example.com/health

# Expected response
{
  "status": "Healthy",
  "dependencies": {
    "postgresql": "Healthy",
    "redis": "Healthy",
    "minio": "Healthy"
  },
  "version": "1.0.0",
  "uptime": "PT72H"
}
```

**Edge Node Health:**
```bash
curl https://api.example.com/api/v1/wasm/nodes/edge-us-east-01/health

# Expected response
{
  "nodeId": "edge-us-east-01",
  "status": "Healthy",
  "modulesLoaded": 42,
  "cpuUsagePercent": 45.2,
  "memoryUsageMB": 8192,
  "lastHeartbeat": "2025-11-23T12:00:00Z"
}
```

### Metrics

**Key Metrics to Monitor:**

| Metric | Alert Threshold | Description |
|--------|----------------|-------------|
| `wasm.modules.registered.total` | - | Total modules registered |
| `wasm.deployments.failed.total` | > 10/hour | Failed deployments |
| `wasm.functions.invoked.total` | - | Total function invocations |
| `wasm.function.invoke.duration` (p99) | > 5ms | Function invocation latency |
| `wasm.edge_nodes.healthy` | < 90% of total | Healthy edge nodes |
| `wasm.module.load.duration` (p99) | > 20ms | Module load time |

### Alerts

**Prometheus Alert Rules:**

```yaml
groups:
  - name: wasm_orchestrator
    rules:
      - alert: HighDeploymentFailureRate
        expr: rate(wasm_deployments_failed_total[5m]) > 0.1
        for: 5m
        labels:
          severity: critical
        annotations:
          summary: "High deployment failure rate"
          description: "More than 10% of deployments failing in the last 5 minutes"

      - alert: UnhealthyEdgeNodes
        expr: (wasm_edge_nodes_healthy / wasm_edge_nodes_total) < 0.9
        for: 2m
        labels:
          severity: warning
        annotations:
          summary: "High number of unhealthy edge nodes"
          description: "Less than 90% of edge nodes are healthy"

      - alert: HighFunctionInvocationLatency
        expr: histogram_quantile(0.99, rate(wasm_function_invoke_duration_bucket[5m])) > 0.005
        for: 5m
        labels:
          severity: warning
        annotations:
          summary: "High function invocation latency"
          description: "p99 function invocation latency is above 5ms"
```

---

## Operational Runbooks

### Runbook 1: Deploy New WASM Module

**Scenario:** Deploy a new WASM module to production

**Steps:**
1. **Register Module (Developer)**
   ```bash
   curl -X POST https://api.example.com/api/v1/wasm/modules \
     -H "Authorization: Bearer $TOKEN" \
     -H "Content-Type: application/json" \
     -d @module-payload.json
   ```

2. **Create Deployment Configuration (Developer)**
   ```bash
   curl -X POST https://api.example.com/api/v1/wasm/deployments \
     -H "Authorization: Bearer $TOKEN" \
     -H "Content-Type: application/json" \
     -d @deployment-config.json
   ```

3. **Approve and Execute Deployment (Admin)**
   ```bash
   curl -X POST https://api.example.com/api/v1/wasm/deployments/{id}/execute \
     -H "Authorization: Bearer $ADMIN_TOKEN" \
     -H "Content-Type: application/json" \
     -d '{"approvedBy": "admin@example.com"}'
   ```

4. **Monitor Deployment Progress**
   ```bash
   # Check deployment status
   curl https://api.example.com/api/v1/wasm/deployments/{id} \
     -H "Authorization: Bearer $TOKEN"
   ```

5. **Verify Deployment Success**
   - Check deployment status = "Completed"
   - Verify all target nodes succeeded
   - Run health checks on deployed modules
   - Monitor error rates and latency

**Rollback Procedure (if needed):**
```bash
curl -X POST https://api.example.com/api/v1/wasm/deployments/{id}/rollback \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"reason": "High error rate detected"}'
```

---

### Runbook 2: Handle Edge Node Failure

**Scenario:** Edge node becomes unhealthy or unresponsive

**Detection:**
- Alert: "Edge node edge-us-east-01 has not sent heartbeat in 2 minutes"
- Monitoring dashboard shows node as "Unhealthy"

**Steps:**

1. **Verify Node Status**
   ```bash
   curl https://api.example.com/api/v1/wasm/nodes/edge-us-east-01/health \
     -H "Authorization: Bearer $TOKEN"
   ```

2. **Check Node Logs**
   ```bash
   ssh edge-us-east-01
   sudo journalctl -u wasm-edge-agent -n 100 --no-pager
   ```

3. **Restart Edge Node Agent**
   ```bash
   ssh edge-us-east-01
   sudo systemctl restart wasm-edge-agent
   ```

4. **If Restart Fails, Deregister Node Temporarily**
   ```bash
   # Mark node as disabled (prevents new deployments)
   curl -X PUT https://api.example.com/api/v1/wasm/nodes/edge-us-east-01 \
     -H "Authorization: Bearer $ADMIN_TOKEN" \
     -H "Content-Type: application/json" \
     -d '{"isEnabled": false}'
   ```

5. **Redistribute Modules to Other Nodes**
   - System automatically redistributes traffic to healthy nodes
   - Monitor other nodes for increased load

6. **Investigate Root Cause**
   - Check hardware health (CPU, memory, disk)
   - Check network connectivity
   - Review error logs

7. **Re-enable Node After Fix**
   ```bash
   curl -X PUT https://api.example.com/api/v1/wasm/nodes/edge-us-east-01 \
     -H "Authorization: Bearer $ADMIN_TOKEN" \
     -H "Content-Type: application/json" \
     -d '{"isEnabled": true}'
   ```

---

### Runbook 3: Emergency Rollback

**Scenario:** Deployed module causing production issues

**Detection:**
- High error rate (> 5%)
- High latency (p99 > 100ms)
- Customer complaints

**Immediate Actions:**

1. **Trigger Rollback**
   ```bash
   curl -X POST https://api.example.com/api/v1/wasm/deployments/{id}/rollback \
     -H "Authorization: Bearer $ADMIN_TOKEN" \
     -H "Content-Type: application/json" \
     -d '{"reason": "Production incident - high error rate"}'
   ```

2. **Monitor Rollback Progress**
   ```bash
   watch -n 2 'curl -s https://api.example.com/api/v1/wasm/deployments/{id} \
     -H "Authorization: Bearer $TOKEN" | jq .status'
   ```

3. **Verify Service Recovery**
   - Check error rates return to baseline
   - Check latency returns to normal
   - Verify customer-facing services operational

4. **Post-Incident Analysis**
   - Collect logs from failed deployment
   - Analyze error messages
   - Identify root cause
   - Create incident report

---

## Troubleshooting

### Issue: Module Registration Fails

**Symptoms:**
- 400 Bad Request error
- "Invalid WASM binary" message

**Diagnosis:**
```bash
# Validate WASM binary locally
wasmtime validate module.wasm

# Check binary size
ls -lh module.wasm

# Verify base64 encoding
base64 -d encoded-module.txt > decoded.wasm
wasmtime validate decoded.wasm
```

**Resolution:**
- Ensure WASM binary is valid WebAssembly format
- Check binary size does not exceed 50 MB
- Verify base64 encoding is correct
- Ensure all required exports are present

---

### Issue: Deployment Stuck in "InProgress"

**Symptoms:**
- Deployment status stuck at certain percentage
- No error messages

**Diagnosis:**
```bash
# Check deployment details
curl https://api.example.com/api/v1/wasm/deployments/{id} \
  -H "Authorization: Bearer $TOKEN" | jq .

# Check edge node health
curl https://api.example.com/api/v1/wasm/nodes \
  -H "Authorization: Bearer $TOKEN" | jq '.nodes[] | select(.region=="us-east")'
```

**Resolution:**
- Check if target edge nodes are healthy
- Verify network connectivity between orchestrator and edge nodes
- Check edge node logs for errors
- Consider manual rollback if stuck > 30 minutes

---

### Issue: High Function Invocation Latency

**Symptoms:**
- p99 latency > 5ms
- Slow API responses

**Diagnosis:**
```bash
# Check Prometheus metrics
curl http://localhost:9090/api/v1/query \
  -d 'query=histogram_quantile(0.99, rate(wasm_function_invoke_duration_bucket[5m]))'

# Check edge node resource usage
curl https://api.example.com/api/v1/wasm/nodes/edge-us-east-01/health \
  -H "Authorization: Bearer $TOKEN" | jq .cpuUsagePercent,.memoryUsageMB
```

**Resolution:**
- Check if edge nodes are overloaded (CPU > 80%)
- Add more edge nodes to distribute load
- Optimize WASM module (reduce complexity)
- Adjust resource limits (increase memory/CPU)

---

## Disaster Recovery

### Backup Strategy

**PostgreSQL Database:**
```bash
# Daily backup
pg_dump wasm_orchestrator > backup-$(date +%Y%m%d).sql

# Automated backup script
#!/bin/bash
BACKUP_DIR=/backups/postgres
DATE=$(date +%Y%m%d-%H%M%S)
pg_dump wasm_orchestrator | gzip > $BACKUP_DIR/wasm-orchestrator-$DATE.sql.gz

# Keep last 30 days
find $BACKUP_DIR -name "*.sql.gz" -mtime +30 -delete
```

**MinIO Modules:**
- Enable MinIO versioning
- Configure replication to backup region
- Daily snapshot to separate bucket

```bash
# MinIO bucket replication
mc replicate add local/wasm-modules \
  --remote-bucket backup/wasm-modules \
  --priority 1
```

### Recovery Procedures

**Database Recovery:**
```bash
# Restore from backup
psql wasm_orchestrator < backup-20251123.sql
```

**Module Recovery:**
```bash
# Restore from backup bucket
mc mirror backup/wasm-modules local/wasm-modules
```

**Full System Recovery:**
1. Restore PostgreSQL database
2. Restore MinIO modules
3. Restart orchestrator API
4. Verify edge nodes reconnect
5. Validate module registrations
6. Run smoke tests

---

**Last Updated:** 2025-11-23
**Version:** 1.0.0
