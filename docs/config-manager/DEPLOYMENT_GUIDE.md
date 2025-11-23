# Deployment & Operations Guide

**Version:** 1.0.0
**Last Updated:** 2025-11-23

---

## Table of Contents

1. [Prerequisites](#prerequisites)
2. [Local Development Setup](#local-development-setup)
3. [Docker Deployment](#docker-deployment)
4. [Kubernetes Deployment](#kubernetes-deployment)
5. [Configuration](#configuration)
6. [Monitoring Setup](#monitoring-setup)
7. [Operational Runbooks](#operational-runbooks)
8. [Troubleshooting](#troubleshooting)

---

## Prerequisites

### Required Infrastructure

**Mandatory:**
- **.NET 8.0 Runtime** - Application runtime
- **Redis 7+** - Config caching, distributed locks
- **PostgreSQL 15+** - Config storage, version history

**Optional:**
- **Jaeger** - Distributed tracing (recommended for production)
- **Prometheus** - Metrics collection
- **Grafana** - Metrics visualization

### System Requirements

**Config Manager Node:**
- CPU: 2+ cores
- Memory: 4 GB+ RAM
- Disk: 20 GB+ SSD
- Network: 1 Gbps

**Redis:**
- CPU: 2+ cores
- Memory: 4 GB+ RAM (depends on config volume)
- Disk: 20 GB+ SSD

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

# Start Redis, PostgreSQL, Jaeger
docker-compose -f docker-compose.configmanager.yml up -d

# Verify services
docker-compose ps
```

**docker-compose.configmanager.yml:**

```yaml
version: '3.8'

services:
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
      POSTGRES_DB: configmanager
      POSTGRES_USER: configmanager_user
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
  redis-data:
  postgres-data:
```

**Step 3: Configure Application**

```bash
# Create appsettings.Development.json
cat > src/HotSwap.ConfigManager.Api/appsettings.Development.json <<EOF
{
  "ConnectionStrings": {
    "Redis": "localhost:6379",
    "PostgreSQL": "Host=localhost;Database=configmanager;Username=configmanager_user;Password=dev_password"
  },
  "ConfigManager": {
    "DefaultStrategy": "Canary",
    "MaxConfigSize": 1048576,
    "DefaultPhaseInterval": "PT5M"
  },
  "OpenTelemetry": {
    "JaegerEndpoint": "http://localhost:14268/api/traces",
    "SamplingRate": 1.0
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "HotSwap.ConfigManager": "Debug"
    }
  }
}
EOF
```

**Step 4: Initialize Database**

```bash
# Run database migrations
dotnet ef database update --project src/HotSwap.ConfigManager.Infrastructure

# Or use SQL script
psql -h localhost -U configmanager_user -d configmanager -f db/migrations/001_initial_schema.sql
```

**db/migrations/001_initial_schema.sql:**

```sql
-- Config profiles table
CREATE TABLE config_profiles (
    name VARCHAR(255) PRIMARY KEY,
    description TEXT,
    environment VARCHAR(50) NOT NULL,
    service_type VARCHAR(50) NOT NULL DEFAULT 'Microservice',
    current_version VARCHAR(50),
    schema_id VARCHAR(255) NOT NULL,
    default_strategy VARCHAR(50) NOT NULL DEFAULT 'Canary',
    settings JSONB,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP NOT NULL DEFAULT NOW(),
    created_by VARCHAR(255),
    version_count INT NOT NULL DEFAULT 0,
    deployment_count INT NOT NULL DEFAULT 0
);

-- Config versions table
CREATE TABLE config_versions (
    config_name VARCHAR(255) NOT NULL,
    version VARCHAR(50) NOT NULL,
    config_data TEXT NOT NULL,
    schema_version VARCHAR(50) NOT NULL,
    description TEXT,
    tags TEXT[],
    metadata JSONB,
    config_hash VARCHAR(32),
    size_bytes BIGINT,
    deployment_count INT NOT NULL DEFAULT 0,
    is_deprecated BOOLEAN NOT NULL DEFAULT FALSE,
    deprecation_reason TEXT,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    created_by VARCHAR(255),
    PRIMARY KEY (config_name, version),
    FOREIGN KEY (config_name) REFERENCES config_profiles(name) ON DELETE CASCADE
);

-- Config deployments table
CREATE TABLE config_deployments (
    deployment_id VARCHAR(255) PRIMARY KEY,
    config_name VARCHAR(255) NOT NULL,
    config_version VARCHAR(50) NOT NULL,
    strategy VARCHAR(50) NOT NULL,
    target_instances TEXT[],
    status VARCHAR(50) NOT NULL DEFAULT 'Pending',
    progress_percentage INT NOT NULL DEFAULT 0,
    instance_status JSONB,
    config JSONB,
    health_check JSONB,
    started_at TIMESTAMP,
    completed_at TIMESTAMP,
    initiated_by VARCHAR(255),
    error_message TEXT,
    previous_version VARCHAR(50),
    was_rolled_back BOOLEAN NOT NULL DEFAULT FALSE,
    rolled_back_at TIMESTAMP,
    rollback_reason TEXT,
    FOREIGN KEY (config_name) REFERENCES config_profiles(name)
);

-- Service instances table
CREATE TABLE service_instances (
    instance_id VARCHAR(255) PRIMARY KEY,
    service_name VARCHAR(255) NOT NULL,
    hostname VARCHAR(255) NOT NULL,
    port INT NOT NULL,
    environment VARCHAR(50) NOT NULL,
    current_config_version VARCHAR(50),
    metadata JSONB,
    health JSONB,
    last_heartbeat TIMESTAMP NOT NULL DEFAULT NOW(),
    registered_at TIMESTAMP NOT NULL DEFAULT NOW(),
    version VARCHAR(50)
);

-- Config schemas table (reuse existing from messaging system or create new)
CREATE TABLE config_schemas (
    schema_id VARCHAR(255) PRIMARY KEY,
    schema_definition TEXT NOT NULL,
    version VARCHAR(50) NOT NULL,
    compatibility VARCHAR(50) NOT NULL DEFAULT 'None',
    status VARCHAR(50) NOT NULL DEFAULT 'Draft',
    approved_by VARCHAR(255),
    approved_at TIMESTAMP,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    deprecated_at TIMESTAMP
);

-- Audit log table
CREATE TABLE config_audit_logs (
    id BIGSERIAL PRIMARY KEY,
    event_type VARCHAR(100) NOT NULL,
    entity_type VARCHAR(100),
    entity_id VARCHAR(255),
    user_id VARCHAR(255),
    details JSONB,
    trace_id VARCHAR(255),
    created_at TIMESTAMP NOT NULL DEFAULT NOW()
);

-- Indexes
CREATE INDEX idx_config_versions_config_name ON config_versions(config_name);
CREATE INDEX idx_config_deployments_config_name ON config_deployments(config_name);
CREATE INDEX idx_config_deployments_status ON config_deployments(status);
CREATE INDEX idx_service_instances_service_name ON service_instances(service_name);
CREATE INDEX idx_service_instances_environment ON service_instances(environment);
CREATE INDEX idx_audit_logs_created_at ON config_audit_logs(created_at DESC);
CREATE INDEX idx_audit_logs_entity ON config_audit_logs(entity_type, entity_id);
```

**Step 5: Run Application**

```bash
# Build and run
dotnet build
dotnet run --project src/HotSwap.ConfigManager.Api

# Application starts on:
# HTTP:  http://localhost:5000
# HTTPS: https://localhost:5001
# Swagger: http://localhost:5000/swagger
```

**Step 6: Verify Installation**

```bash
# Health check
curl http://localhost:5000/health

# Create test config profile
curl -X POST http://localhost:5000/api/v1/configs \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $(get_jwt_token)" \
  -d '{
    "name": "test-service.dev",
    "environment": "Development",
    "schemaId": "test.v1",
    "defaultStrategy": "Direct"
  }'

# Upload test version
curl -X POST http://localhost:5000/api/v1/configs/test-service.dev/versions \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $(get_jwt_token)" \
  -d '{
    "version": "1.0.0",
    "configData": "{\"timeout\":\"30s\",\"maxRetries\":3}"
  }'
```

---

## Docker Deployment

### Build Docker Image

**Dockerfile:**

```dockerfile
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/HotSwap.ConfigManager.Api/HotSwap.ConfigManager.Api.csproj", "Api/"]
COPY ["src/HotSwap.ConfigManager.Orchestrator/HotSwap.ConfigManager.Orchestrator.csproj", "Orchestrator/"]
COPY ["src/HotSwap.ConfigManager.Infrastructure/HotSwap.ConfigManager.Infrastructure.csproj", "Infrastructure/"]
COPY ["src/HotSwap.ConfigManager.Domain/HotSwap.ConfigManager.Domain.csproj", "Domain/"]
RUN dotnet restore "Api/HotSwap.ConfigManager.Api.csproj"

COPY src/ .
RUN dotnet publish "Api/HotSwap.ConfigManager.Api.csproj" -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

# Create non-root user
RUN useradd -m -u 1000 configmanager && \
    chown -R configmanager:configmanager /app

USER configmanager

COPY --from=build /app/publish .

EXPOSE 5000
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
  CMD curl -f http://localhost:5000/health || exit 1

ENTRYPOINT ["dotnet", "HotSwap.ConfigManager.Api.dll"]
```

**Build and Push:**

```bash
# Build image
docker build -t your-registry/config-manager:1.0.0 .

# Tag for registry
docker tag your-registry/config-manager:1.0.0 your-registry/config-manager:latest

# Push to registry
docker push your-registry/config-manager:1.0.0
docker push your-registry/config-manager:latest
```

### Docker Compose Full Stack

**docker-compose.production.yml:**

```yaml
version: '3.8'

services:
  config-manager-api:
    image: your-registry/config-manager:1.0.0
    ports:
      - "5000:5000"
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ASPNETCORE_URLS: http://+:5000
      ConnectionStrings__Redis: redis:6379
      ConnectionStrings__PostgreSQL: "Host=postgres;Database=configmanager;Username=configmanager_user;Password=${POSTGRES_PASSWORD}"
      OpenTelemetry__JaegerEndpoint: "http://jaeger:14268/api/traces"
    depends_on:
      - redis
      - postgres
      - jaeger
    networks:
      - configmanager-network
    deploy:
      replicas: 3
      resources:
        limits:
          cpus: '2'
          memory: 4G
        reservations:
          cpus: '1'
          memory: 2G

  redis:
    image: redis:7-alpine
    command: redis-server --appendonly yes --maxmemory 2gb --maxmemory-policy allkeys-lru
    volumes:
      - redis-data:/data
    networks:
      - configmanager-network

  postgres:
    image: postgres:15-alpine
    environment:
      POSTGRES_DB: configmanager
      POSTGRES_USER: configmanager_user
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
      POSTGRES_MAX_CONNECTIONS: 200
    volumes:
      - postgres-data:/var/lib/postgresql/data
    networks:
      - configmanager-network

  jaeger:
    image: jaegertracing/all-in-one:1.52
    environment:
      COLLECTOR_ZIPKIN_HOST_PORT: ":9411"
      SPAN_STORAGE_TYPE: badger
      BADGER_EPHEMERAL: "false"
      BADGER_DIRECTORY_VALUE: "/badger/data"
      BADGER_DIRECTORY_KEY: "/badger/key"
    volumes:
      - jaeger-data:/badger
    ports:
      - "16686:16686"  # Jaeger UI
    networks:
      - configmanager-network

  prometheus:
    image: prom/prometheus:latest
    volumes:
      - ./prometheus.yml:/etc/prometheus/prometheus.yml
      - prometheus-data:/prometheus
    ports:
      - "9090:9090"
    networks:
      - configmanager-network

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
      - configmanager-network

volumes:
  redis-data:
  postgres-data:
  jaeger-data:
  prometheus-data:
  grafana-data:

networks:
  configmanager-network:
    driver: bridge
```

**Deploy:**

```bash
# Set environment variables
export POSTGRES_PASSWORD="$(openssl rand -base64 32)"
export GRAFANA_PASSWORD="$(openssl rand -base64 32)"

# Deploy stack
docker-compose -f docker-compose.production.yml up -d

# Verify deployment
docker-compose ps

# View logs
docker-compose logs -f config-manager-api
```

---

## Kubernetes Deployment

### Namespace and ConfigMap

**namespace.yaml:**

```yaml
apiVersion: v1
kind: Namespace
metadata:
  name: config-manager
```

**configmap.yaml:**

```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: config-manager-config
  namespace: config-manager
data:
  appsettings.json: |
    {
      "ConfigManager": {
        "DefaultStrategy": "Canary",
        "MaxConfigSize": 1048576,
        "DefaultPhaseInterval": "PT5M"
      },
      "OpenTelemetry": {
        "SamplingRate": 0.1
      }
    }
```

### Secrets

```bash
# Create secrets
kubectl create secret generic config-manager-secrets \
  --from-literal=redis-password=$(openssl rand -base64 32) \
  --from-literal=postgres-password=$(openssl rand -base64 32) \
  --from-literal=jwt-secret=$(openssl rand -base64 64) \
  -n config-manager
```

### Config Manager Deployment

**config-manager-deployment.yaml:**

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: config-manager-api
  namespace: config-manager
spec:
  replicas: 3
  selector:
    matchLabels:
      app: config-manager-api
  template:
    metadata:
      labels:
        app: config-manager-api
    spec:
      containers:
      - name: config-manager-api
        image: your-registry/config-manager:1.0.0
        ports:
        - containerPort: 5000
          name: http
        env:
        - name: ASPNETCORE_ENVIRONMENT
          value: "Production"
        - name: ConnectionStrings__Redis
          value: "redis:6379,password=$(REDIS_PASSWORD)"
        - name: ConnectionStrings__PostgreSQL
          value: "Host=postgres;Database=configmanager;Username=configmanager_user;Password=$(POSTGRES_PASSWORD)"
        - name: REDIS_PASSWORD
          valueFrom:
            secretKeyRef:
              name: config-manager-secrets
              key: redis-password
        - name: POSTGRES_PASSWORD
          valueFrom:
            secretKeyRef:
              name: config-manager-secrets
              key: postgres-password
        - name: JWT_SECRET
          valueFrom:
            secretKeyRef:
              name: config-manager-secrets
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
          name: config-manager-config
---
apiVersion: v1
kind: Service
metadata:
  name: config-manager-api
  namespace: config-manager
spec:
  type: LoadBalancer
  ports:
  - port: 80
    targetPort: 5000
    name: http
  selector:
    app: config-manager-api
---
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: config-manager-api-hpa
  namespace: config-manager
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: config-manager-api
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

# Deploy infrastructure (Redis, PostgreSQL - use StatefulSets)
kubectl apply -f redis-statefulset.yaml
kubectl apply -f postgres-statefulset.yaml

# Wait for infrastructure to be ready
kubectl wait --for=condition=ready pod -l app=redis -n config-manager --timeout=300s
kubectl wait --for=condition=ready pod -l app=postgres -n config-manager --timeout=300s

# Run database migrations
kubectl run db-migrate --image=your-registry/config-manager:1.0.0 \
  --env="ConnectionStrings__PostgreSQL=Host=postgres;Database=configmanager;Username=configmanager_user;Password=$(kubectl get secret config-manager-secrets -n config-manager -o jsonpath='{.data.postgres-password}' | base64 -d)" \
  --command -- dotnet ef database update \
  -n config-manager

# Deploy config manager API
kubectl apply -f config-manager-deployment.yaml

# Verify deployment
kubectl get pods -n config-manager
kubectl get svc -n config-manager

# Get service URL
kubectl get svc config-manager-api -n config-manager -o jsonpath='{.status.loadBalancer.ingress[0].ip}'
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
ConnectionStrings__PostgreSQL=Host=postgres;Database=configmanager;Username=configmanager_user;Password=secure_password

# Config Manager
ConfigManager__DefaultStrategy=Canary
ConfigManager__MaxConfigSize=1048576
ConfigManager__DefaultPhaseInterval=PT5M

# OpenTelemetry
OpenTelemetry__JaegerEndpoint=http://jaeger:14268/api/traces
OpenTelemetry__SamplingRate=0.1

# JWT
JWT__Secret=your_jwt_secret_key_here
JWT__Issuer=https://configmanager.example.com
JWT__Audience=configmanager-api
JWT__ExpirationMinutes=60

# Rate Limiting
RateLimiting__CreateConfig=10
RateLimiting__Deploy=20
RateLimiting__Heartbeat=120
```

---

## Monitoring Setup

### Prometheus Configuration

**prometheus.yml:**

```yaml
global:
  scrape_interval: 15s

scrape_configs:
  - job_name: 'config-manager'
    static_configs:
      - targets: ['config-manager-api:5000']
```

### Grafana Dashboard

Import the pre-built dashboard from `grafana/dashboards/config-manager-dashboard.json`

**Key Metrics:**
- Deployment count
- Deployment success rate
- Rollback count
- Config reload time (p50, p95, p99)
- Instance health status
- Active configurations

---

## Operational Runbooks

### Runbook 1: Handle Failed Deployment

**Symptoms:**
- Deployment status shows "Failed"
- Error message in deployment details

**Steps:**
1. Get deployment details:
   ```bash
   curl http://localhost:5000/api/v1/deployments/{id}
   ```

2. Check error message and instance status

3. Review logs:
   ```bash
   kubectl logs -n config-manager -l app=config-manager-api --tail=100
   ```

4. Rollback if needed:
   ```bash
   curl -X POST http://localhost:5000/api/v1/deployments/{id}/rollback \
     -H "Authorization: Bearer $TOKEN" \
     -d '{"reason":"Manual rollback due to deployment failure"}'
   ```

### Runbook 2: Emergency Rollback

**Symptoms:**
- High error rate after deployment
- Customer complaints

**Steps:**
1. Identify active deployment:
   ```bash
   curl http://localhost:5000/api/v1/deployments?status=InProgress
   ```

2. Pause deployment immediately:
   ```bash
   curl -X POST http://localhost:5000/api/v1/deployments/{id}/pause
   ```

3. Trigger rollback:
   ```bash
   curl -X POST http://localhost:5000/api/v1/deployments/{id}/rollback \
     -d '{"reason":"Emergency rollback - high error rate"}'
   ```

4. Verify rollback completion:
   ```bash
   curl http://localhost:5000/api/v1/deployments/{id}
   ```

5. Verify service health restored

---

## Troubleshooting

### Issue: Config deployment stuck

**Symptom:** Deployment shows "InProgress" but not completing

**Solutions:**
1. Check instance heartbeats
2. Verify health check endpoint accessibility
3. Review phase interval configuration
4. Check Redis connectivity (distributed locks)

### Issue: High rollback rate

**Symptom:** Many deployments rolling back automatically

**Solutions:**
1. Review health check thresholds (may be too strict)
2. Verify baseline metrics are correctly captured
3. Check for infrastructure issues (network latency spikes)
4. Consider increasing phase intervals

---

**Last Updated:** 2025-11-23
**Version:** 1.0.0
