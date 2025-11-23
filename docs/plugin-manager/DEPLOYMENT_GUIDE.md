# Deployment & Operations Guide

**Version:** 1.0.0
**Last Updated:** 2025-11-23

---

## Table of Contents

1. [Prerequisites](#prerequisites)
2. [Local Development Setup](#local-development-setup)
3. [MinIO Setup](#minio-setup)
4. [Docker Deployment](#docker-deployment)
5. [Kubernetes Deployment](#kubernetes-deployment)
6. [Migration Path](#migration-path)
7. [Configuration](#configuration)
8. [Monitoring Setup](#monitoring-setup)
9. [Operational Runbooks](#operational-runbooks)
10. [Troubleshooting](#troubleshooting)

---

## Prerequisites

### Required Infrastructure

**Mandatory:**
- **.NET 8.0 Runtime** - Application runtime
- **MinIO or S3** - Plugin binary storage
- **Redis 7+** - Distributed locks, caching
- **PostgreSQL 15+** - Plugin registry, audit logs

**Optional:**
- **Jaeger** - Distributed tracing (recommended for production)
- **Prometheus** - Metrics collection
- **Grafana** - Metrics visualization
- **HashiCorp Vault** - Secrets management (or Kubernetes Secrets)

### System Requirements

**Plugin Manager Node:**
- CPU: 2+ cores
- Memory: 4 GB+ RAM
- Disk: 20 GB+ SSD
- Network: 1 Gbps

**MinIO:**
- CPU: 2+ cores
- Memory: 8 GB+ RAM
- Disk: 100 GB+ SSD (for plugin binaries)

**PostgreSQL:**
- CPU: 2+ cores
- Memory: 4 GB+ RAM
- Disk: 50 GB+ SSD

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

**Step 2: Start Infrastructure (Docker Compose)**

```bash
# Clone repository
git clone https://github.com/scrawlsbenches/Claude-code-test.git
cd Claude-code-test

# Start MinIO, Redis, PostgreSQL, Jaeger
docker-compose -f docker-compose.plugin-manager.yml up -d

# Verify services
docker-compose ps
```

**docker-compose.plugin-manager.yml:**

```yaml
version: '3.8'

services:
  minio:
    image: minio/minio:latest
    ports:
      - "9000:9000"
      - "9001:9001"  # Console
    environment:
      MINIO_ROOT_USER: minioadmin
      MINIO_ROOT_PASSWORD: ${MINIO_PASSWORD}
    volumes:
      - minio-data:/data
    command: server /data --console-address ":9001"

  redis:
    image: redis:7-alpine
    ports:
      - "6379:6379"
    volumes:
      - redis-data:/data
    command: redis-server --appendonly yes

  postgres:
    image: postgres:15-alpine
    ports:
      - "5432:5432"
    environment:
      POSTGRES_DB: plugin_manager
      POSTGRES_USER: plugin_user
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
    volumes:
      - postgres-data:/var/lib/postgresql/data

  jaeger:
    image: jaegertracing/all-in-one:1.52
    ports:
      - "5775:5775/udp"
      - "6831:6831/udp"
      - "6832:6832/udp"
      - "5778:5778"
      - "16686:16686"  # Jaeger UI
      - "14268:14268"
      - "14250:14250"
      - "9411:9411"

volumes:
  minio-data:
  redis-data:
  postgres-data:
```

**Step 3: Configure Application**

```bash
# Create appsettings.Development.json
cat > src/HotSwap.Distributed.Api/appsettings.Development.json <<EOF
{
  "ConnectionStrings": {
    "Redis": "localhost:6379",
    "PostgreSQL": "Host=localhost;Database=plugin_manager;Username=plugin_user;Password=dev_password"
  },
  "MinIO": {
    "Endpoint": "localhost:9000",
    "AccessKey": "minioadmin",
    "SecretKey": "${MINIO_PASSWORD}",
    "BucketName": "plugins",
    "UseSSL": false
  },
  "PluginManager": {
    "MaxPluginSize": 104857600,
    "MaxSandboxInstances": 50,
    "SandboxTimeout": "PT5M",
    "DefaultDeploymentStrategy": "Canary"
  },
  "OpenTelemetry": {
    "JaegerEndpoint": "http://localhost:14268/api/traces",
    "SamplingRate": 1.0
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "HotSwap.Distributed": "Debug"
    }
  }
}
EOF
```

**Step 4: Initialize Database**

```bash
# Run database migrations
dotnet ef database update --project src/HotSwap.Distributed.Infrastructure

# Or use SQL script
psql -h localhost -U plugin_user -d plugin_manager -f db/migrations/001_initial_schema.sql
```

**db/migrations/001_initial_schema.sql:**

```sql
-- Plugin registry tables
CREATE TABLE plugins (
    plugin_id VARCHAR(255) PRIMARY KEY,
    name VARCHAR(255) UNIQUE NOT NULL,
    display_name VARCHAR(255) NOT NULL,
    description TEXT,
    type VARCHAR(100) NOT NULL,
    current_version VARCHAR(50),
    status VARCHAR(50) NOT NULL DEFAULT 'Draft',
    author VARCHAR(255),
    documentation_url TEXT,
    tags JSONB,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP NOT NULL DEFAULT NOW(),
    deprecated_at TIMESTAMP,
    deprecation_reason TEXT
);

CREATE TABLE plugin_versions (
    version_id VARCHAR(255) PRIMARY KEY,
    plugin_id VARCHAR(255) NOT NULL REFERENCES plugins(plugin_id) ON DELETE CASCADE,
    version VARCHAR(50) NOT NULL,
    release_notes TEXT,
    manifest JSONB NOT NULL,
    binary_path TEXT NOT NULL,
    checksum VARCHAR(255) NOT NULL,
    binary_size BIGINT NOT NULL,
    status VARCHAR(50) NOT NULL DEFAULT 'Draft',
    is_breaking_change BOOLEAN DEFAULT FALSE,
    approved_by VARCHAR(255),
    approved_at TIMESTAMP,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    deprecated_at TIMESTAMP,
    UNIQUE(plugin_id, version)
);

CREATE TABLE plugin_deployments (
    deployment_id VARCHAR(255) PRIMARY KEY,
    plugin_id VARCHAR(255) NOT NULL REFERENCES plugins(plugin_id),
    version VARCHAR(50) NOT NULL,
    environment VARCHAR(100) NOT NULL,
    strategy VARCHAR(100) NOT NULL,
    config JSONB,
    status VARCHAR(50) NOT NULL DEFAULT 'Pending',
    progress_percentage INT DEFAULT 0,
    initiated_by VARCHAR(255) NOT NULL,
    started_at TIMESTAMP,
    completed_at TIMESTAMP,
    previous_version VARCHAR(50),
    rolled_back_at TIMESTAMP,
    rollback_reason TEXT,
    error_message TEXT,
    affected_tenants INT DEFAULT 0,
    created_at TIMESTAMP NOT NULL DEFAULT NOW()
);

CREATE TABLE tenant_plugin_configs (
    config_id VARCHAR(255) PRIMARY KEY,
    tenant_id VARCHAR(255) NOT NULL,
    plugin_id VARCHAR(255) NOT NULL REFERENCES plugins(plugin_id),
    enabled BOOLEAN DEFAULT TRUE,
    pinned_version VARCHAR(50),
    configuration JSONB,
    secrets JSONB,
    quotas JSONB,
    enabled_by VARCHAR(255) NOT NULL,
    enabled_at TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP NOT NULL DEFAULT NOW(),
    UNIQUE(tenant_id, plugin_id)
);

CREATE TABLE plugin_health_checks (
    health_check_id VARCHAR(255) PRIMARY KEY,
    plugin_id VARCHAR(255) NOT NULL REFERENCES plugins(plugin_id),
    version VARCHAR(50) NOT NULL,
    environment VARCHAR(100) NOT NULL,
    type VARCHAR(50) NOT NULL,
    status VARCHAR(50) NOT NULL,
    checked_at TIMESTAMP NOT NULL DEFAULT NOW(),
    duration_ms BIGINT NOT NULL,
    error_message TEXT,
    metrics JSONB
);

-- Indexes
CREATE INDEX idx_plugins_name ON plugins(name);
CREATE INDEX idx_plugins_status ON plugins(status);
CREATE INDEX idx_plugin_versions_plugin_id ON plugin_versions(plugin_id);
CREATE INDEX idx_plugin_versions_status ON plugin_versions(status);
CREATE INDEX idx_plugin_deployments_plugin_id ON plugin_deployments(plugin_id);
CREATE INDEX idx_plugin_deployments_status ON plugin_deployments(status);
CREATE INDEX idx_plugin_deployments_environment ON plugin_deployments(environment);
CREATE INDEX idx_tenant_plugin_configs_tenant_id ON tenant_plugin_configs(tenant_id);
CREATE INDEX idx_tenant_plugin_configs_plugin_id ON tenant_plugin_configs(plugin_id);
CREATE INDEX idx_plugin_health_checks_plugin_id ON plugin_health_checks(plugin_id);
CREATE INDEX idx_plugin_health_checks_checked_at ON plugin_health_checks(checked_at DESC);

-- Audit log table
CREATE TABLE audit_logs (
    id BIGSERIAL PRIMARY KEY,
    event_type VARCHAR(100) NOT NULL,
    entity_type VARCHAR(100),
    entity_id VARCHAR(255),
    user_id VARCHAR(255),
    details JSONB,
    trace_id VARCHAR(255),
    created_at TIMESTAMP NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_audit_logs_created_at ON audit_logs(created_at DESC);
CREATE INDEX idx_audit_logs_entity ON audit_logs(entity_type, entity_id);
```

**Step 5: Initialize MinIO Bucket**

```bash
# Install MinIO client
wget https://dl.min.io/client/mc/release/linux-amd64/mc
chmod +x mc

# Configure MinIO
./mc alias set local http://localhost:9000 minioadmin ${MINIO_PASSWORD}

# Create plugins bucket
./mc mb local/plugins

# Enable versioning
./mc version enable local/plugins

# Verify bucket
./mc ls local
```

**Step 6: Run Application**

```bash
# Build and run
dotnet build
dotnet run --project src/HotSwap.Distributed.Api

# Application starts on:
# HTTP:  http://localhost:5000
# HTTPS: https://localhost:5001
# Swagger: http://localhost:5000/swagger
```

**Step 7: Verify Installation**

```bash
# Health check
curl http://localhost:5000/health

# Register test plugin
curl -X POST http://localhost:5000/api/v1/plugins \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $(get_jwt_token)" \
  -d '{
    "name": "test-plugin",
    "displayName": "Test Plugin",
    "type": "Other"
  }'
```

---

## MinIO Setup

### Production MinIO Configuration

**Install MinIO:**

```bash
# Download MinIO server
wget https://dl.min.io/server/minio/release/linux-amd64/minio
chmod +x minio

# Create data directory
mkdir -p /mnt/minio/data

# Start MinIO (production mode)
./minio server /mnt/minio/data \
  --address ":9000" \
  --console-address ":9001" \
  --certs-dir /etc/minio/certs
```

**Configure TLS:**

```bash
# Generate self-signed certificate (or use Let's Encrypt)
mkdir -p /etc/minio/certs
openssl req -new -x509 -days 365 -nodes \
  -out /etc/minio/certs/public.crt \
  -keyout /etc/minio/certs/private.key \
  -subj "/CN=minio.example.com"
```

**Create Bucket Policy:**

```bash
# Create bucket
mc mb local/plugins

# Create policy for plugin access
cat > plugin-policy.json <<EOF
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Principal": {
        "AWS": ["arn:aws:iam::plugin-manager:user/plugin-service"]
      },
      "Action": [
        "s3:GetObject",
        "s3:PutObject",
        "s3:DeleteObject"
      ],
      "Resource": ["arn:aws:s3:::plugins/*"]
    }
  ]
}
EOF

# Apply policy
mc admin policy add local plugin-access plugin-policy.json
```

---

## Docker Deployment

### Build Docker Image

**Dockerfile:**

```dockerfile
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/HotSwap.Distributed.Api/HotSwap.Distributed.Api.csproj", "Api/"]
COPY ["src/HotSwap.Distributed.Orchestrator/HotSwap.Distributed.Orchestrator.csproj", "Orchestrator/"]
COPY ["src/HotSwap.Distributed.Infrastructure/HotSwap.Distributed.Infrastructure.csproj", "Infrastructure/"]
COPY ["src/HotSwap.Distributed.Domain/HotSwap.Distributed.Domain.csproj", "Domain/"]
RUN dotnet restore "Api/HotSwap.Distributed.Api.csproj"

COPY src/ .
RUN dotnet publish "Api/HotSwap.Distributed.Api.csproj" -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

# Create non-root user
RUN useradd -m -u 1000 plugin-manager && \
    chown -R plugin-manager:plugin-manager /app

USER plugin-manager

COPY --from=build /app/publish .

EXPOSE 5000 5001
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
  CMD curl -f http://localhost:5000/health || exit 1

ENTRYPOINT ["dotnet", "HotSwap.Distributed.Api.dll"]
```

**Build and Push:**

```bash
# Build image
docker build -t your-registry/plugin-manager:1.0.0 .

# Tag for registry
docker tag your-registry/plugin-manager:1.0.0 your-registry/plugin-manager:latest

# Push to registry
docker push your-registry/plugin-manager:1.0.0
docker push your-registry/plugin-manager:latest
```

### Docker Compose Full Stack

**docker-compose.production.yml:**

```yaml
version: '3.8'

services:
  plugin-manager:
    image: your-registry/plugin-manager:1.0.0
    ports:
      - "5000:5000"
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ASPNETCORE_URLS: http://+:5000
      ConnectionStrings__Redis: redis:6379
      ConnectionStrings__PostgreSQL: "Host=postgres;Database=plugin_manager;Username=plugin_user;Password=${POSTGRES_PASSWORD}"
      MinIO__Endpoint: "minio:9000"
      MinIO__AccessKey: "minioadmin"
      MinIO__SecretKey: "${MINIO_PASSWORD}"
      OpenTelemetry__JaegerEndpoint: "http://jaeger:14268/api/traces"
    depends_on:
      - redis
      - postgres
      - minio
      - jaeger
    networks:
      - plugin-network
    deploy:
      replicas: 3
      resources:
        limits:
          cpus: '2'
          memory: 4G
        reservations:
          cpus: '1'
          memory: 2G

  minio:
    image: minio/minio:latest
    command: server /data --console-address ":9001"
    ports:
      - "9000:9000"
      - "9001:9001"
    environment:
      MINIO_ROOT_USER: minioadmin
      MINIO_ROOT_PASSWORD: ${MINIO_PASSWORD}
    volumes:
      - minio-data:/data
    networks:
      - plugin-network
    deploy:
      resources:
        limits:
          memory: 8G

  redis:
    image: redis:7-alpine
    command: redis-server --appendonly yes --maxmemory 4gb --maxmemory-policy allkeys-lru
    volumes:
      - redis-data:/data
    networks:
      - plugin-network
    deploy:
      resources:
        limits:
          memory: 8G

  postgres:
    image: postgres:15-alpine
    environment:
      POSTGRES_DB: plugin_manager
      POSTGRES_USER: plugin_user
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
      POSTGRES_MAX_CONNECTIONS: 200
    volumes:
      - postgres-data:/var/lib/postgresql/data
    networks:
      - plugin-network
    deploy:
      resources:
        limits:
          memory: 4G

  jaeger:
    image: jaegertracing/all-in-one:1.52
    environment:
      COLLECTOR_ZIPKIN_HOST_PORT: ":9411"
    ports:
      - "16686:16686"  # Jaeger UI
    networks:
      - plugin-network

  prometheus:
    image: prom/prometheus:latest
    volumes:
      - ./prometheus.yml:/etc/prometheus/prometheus.yml
      - prometheus-data:/prometheus
    ports:
      - "9090:9090"
    networks:
      - plugin-network

  grafana:
    image: grafana/grafana:latest
    environment:
      GF_SECURITY_ADMIN_PASSWORD: ${GRAFANA_PASSWORD}
    volumes:
      - grafana-data:/var/lib/grafana
      - ./grafana/dashboards:/etc/grafana/provisioning/dashboards
    ports:
      - "3000:3000"
    networks:
      - plugin-network

volumes:
  minio-data:
  redis-data:
  postgres-data:
  prometheus-data:
  grafana-data:

networks:
  plugin-network:
    driver: bridge
```

**Deploy:**

```bash
# Set environment variables
export POSTGRES_PASSWORD="$(openssl rand -base64 32)"
export MINIO_PASSWORD="$(openssl rand -base64 32)"
export GRAFANA_PASSWORD="$(openssl rand -base64 32)"

# Deploy stack
docker-compose -f docker-compose.production.yml up -d

# Verify deployment
docker-compose ps

# View logs
docker-compose logs -f plugin-manager
```

---

## Kubernetes Deployment

### Namespace and ConfigMap

**namespace.yaml:**

```yaml
apiVersion: v1
kind: Namespace
metadata:
  name: plugin-manager
```

**configmap.yaml:**

```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: plugin-manager-config
  namespace: plugin-manager
data:
  appsettings.json: |
    {
      "PluginManager": {
        "MaxPluginSize": 104857600,
        "MaxSandboxInstances": 50,
        "SandboxTimeout": "PT5M",
        "DefaultDeploymentStrategy": "Canary"
      },
      "OpenTelemetry": {
        "SamplingRate": 0.1
      }
    }
```

### Secrets

```bash
# Create secrets
kubectl create secret generic plugin-manager-secrets \
  --from-literal=redis-password=$(openssl rand -base64 32) \
  --from-literal=postgres-password=$(openssl rand -base64 32) \
  --from-literal=minio-access-key=minioadmin \
  --from-literal=minio-secret-key=$(openssl rand -base64 32) \
  --from-literal=jwt-secret=$(openssl rand -base64 64) \
  -n plugin-manager
```

### Plugin Manager Deployment

**plugin-manager-deployment.yaml:**

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: plugin-manager
  namespace: plugin-manager
spec:
  replicas: 3
  selector:
    matchLabels:
      app: plugin-manager
  template:
    metadata:
      labels:
        app: plugin-manager
    spec:
      containers:
      - name: plugin-manager
        image: your-registry/plugin-manager:1.0.0
        ports:
        - containerPort: 5000
          name: http
        env:
        - name: ASPNETCORE_ENVIRONMENT
          value: "Production"
        - name: ConnectionStrings__Redis
          value: "redis:6379,password=$(REDIS_PASSWORD)"
        - name: ConnectionStrings__PostgreSQL
          value: "Host=postgres;Database=plugin_manager;Username=plugin_user;Password=$(POSTGRES_PASSWORD)"
        - name: MinIO__Endpoint
          value: "minio:9000"
        - name: MinIO__AccessKey
          valueFrom:
            secretKeyRef:
              name: plugin-manager-secrets
              key: minio-access-key
        - name: MinIO__SecretKey
          valueFrom:
            secretKeyRef:
              name: plugin-manager-secrets
              key: minio-secret-key
        - name: REDIS_PASSWORD
          valueFrom:
            secretKeyRef:
              name: plugin-manager-secrets
              key: redis-password
        - name: POSTGRES_PASSWORD
          valueFrom:
            secretKeyRef:
              name: plugin-manager-secrets
              key: postgres-password
        - name: JWT_SECRET
          valueFrom:
            secretKeyRef:
              name: plugin-manager-secrets
              key: jwt-secret
        volumeMounts:
        - name: config
          mountPath: /app/appsettings.json
          subPath: appsettings.json
        livenessProbe:
          httpGet:
            path: /health
            port: 5000
          initialDelaySeconds: 30
          periodSeconds: 10
        readinessProbe:
          httpGet:
            path: /health
            port: 5000
          initialDelaySeconds: 10
          periodSeconds: 5
        resources:
          limits:
            memory: "4Gi"
            cpu: "2"
          requests:
            memory: "2Gi"
            cpu: "1"
      volumes:
      - name: config
        configMap:
          name: plugin-manager-config
---
apiVersion: v1
kind: Service
metadata:
  name: plugin-manager
  namespace: plugin-manager
spec:
  type: LoadBalancer
  ports:
  - port: 80
    targetPort: 5000
    name: http
  selector:
    app: plugin-manager
---
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: plugin-manager-hpa
  namespace: plugin-manager
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: plugin-manager
  minReplicas: 3
  maxReplicas: 10
  metrics:
  - type: Resource
    resource:
      name: cpu
      target:
        type: Utilization
        averageUtilization: 70
  - type: Resource
    resource:
      name: memory
      target:
        type: Utilization
        averageUtilization: 80
```

### Deploy to Kubernetes

```bash
# Create namespace
kubectl apply -f namespace.yaml

# Create ConfigMap and Secrets
kubectl apply -f configmap.yaml
kubectl apply -f secrets.yaml

# Deploy infrastructure (MinIO, Redis, PostgreSQL)
kubectl apply -f minio-statefulset.yaml
kubectl apply -f redis-statefulset.yaml
kubectl apply -f postgres-statefulset.yaml

# Wait for infrastructure
kubectl wait --for=condition=ready pod -l app=minio -n plugin-manager --timeout=300s
kubectl wait --for=condition=ready pod -l app=postgres -n plugin-manager --timeout=300s

# Run database migrations
kubectl run db-migrate --image=your-registry/plugin-manager:1.0.0 \
  --env="ConnectionStrings__PostgreSQL=Host=postgres;Database=plugin_manager;Username=plugin_user;Password=$(kubectl get secret plugin-manager-secrets -n plugin-manager -o jsonpath='{.data.postgres-password}' | base64 -d)" \
  --command -- dotnet ef database update \
  -n plugin-manager

# Deploy plugin manager
kubectl apply -f plugin-manager-deployment.yaml

# Verify deployment
kubectl get pods -n plugin-manager
kubectl get svc -n plugin-manager
```

---

## Configuration

### Environment Variables

```bash
# Application
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://+:5000;https://+:5001

# Database Connections
ConnectionStrings__Redis=redis:6379,password=secure_password
ConnectionStrings__PostgreSQL=Host=postgres;Database=plugin_manager;Username=plugin_user;Password=secure_password

# MinIO
MinIO__Endpoint=minio:9000
MinIO__AccessKey=minioadmin
MinIO__SecretKey=secure_password
MinIO__BucketName=plugins
MinIO__UseSSL=true

# Plugin Manager
PluginManager__MaxPluginSize=104857600
PluginManager__MaxSandboxInstances=50
PluginManager__SandboxTimeout=PT5M
PluginManager__DefaultDeploymentStrategy=Canary

# OpenTelemetry
OpenTelemetry__JaegerEndpoint=http://jaeger:14268/api/traces
OpenTelemetry__SamplingRate=0.1

# JWT
JWT__Secret=your_jwt_secret_key_here
JWT__Issuer=https://plugin-manager.example.com
JWT__Audience=plugin-manager-api
JWT__ExpirationMinutes=60
```

---

## Operational Runbooks

### Runbook 1: Plugin Deployment Failure

**Scenario:** Plugin deployment failed

**Steps:**
1. Check deployment status: `GET /api/v1/deployments/{id}`
2. Review error message in deployment response
3. Check health check logs: `GET /api/v1/plugins/{id}/health`
4. Review plugin logs in Jaeger (trace ID from deployment)
5. If fixable, update plugin and redeploy
6. If not fixable, rollback: `POST /api/v1/deployments/{id}/rollback`

### Runbook 2: Automatic Rollback Triggered

**Scenario:** System automatically rolled back a deployment

**Steps:**
1. Get deployment details: `GET /api/v1/deployments/{id}`
2. Review rollback reason in response
3. Check health metrics: `GET /api/v1/plugins/{id}/metrics`
4. Identify root cause (error rate spike, latency increase)
5. Fix plugin issue
6. Redeploy with fixes

### Runbook 3: Sandbox Timeout

**Scenario:** Sandbox execution timed out

**Steps:**
1. Check sandbox timeout configuration (default: 5 minutes)
2. Review plugin execution time requirements
3. If legitimate long-running operation, increase timeout
4. If infinite loop or hang, fix plugin code
5. Cleanup sandbox: `DELETE /api/v1/sandbox/{id}`

---

**Last Updated:** 2025-11-23
**Version:** 1.0.0
