# Deployment & Migration Guide

**Version:** 1.0.0
**Last Updated:** 2025-11-23

---

## Table of Contents

1. [Prerequisites](#prerequisites)
2. [Local Development Setup](#local-development-setup)
3. [Docker Deployment](#docker-deployment)
4. [Kubernetes Deployment](#kubernetes-deployment)
5. [Migration Path](#migration-path)
6. [Configuration](#configuration)
7. [Monitoring Setup](#monitoring-setup)
8. [Troubleshooting](#troubleshooting)

---

## Prerequisites

### Required Infrastructure

**Mandatory:**
- **.NET 8.0 Runtime** - Application runtime
- **Redis 7+** - Flag configuration cache, distributed locks
- **PostgreSQL 15+** - Flag persistence, experiments, audit logs

**Optional:**
- **Jaeger** - Distributed tracing (recommended for production)
- **Prometheus** - Metrics collection
- **Grafana** - Metrics visualization

### System Requirements

**Flag Service Node:**
- CPU: 2+ cores
- Memory: 2 GB+ RAM
- Disk: 10 GB+ SSD
- Network: 1 Gbps

**Redis:**
- CPU: 2+ cores
- Memory: 4 GB+ RAM
- Disk: 20 GB+ SSD

**PostgreSQL:**
- CPU: 2+ cores
- Memory: 4 GB+ RAM
- Disk: 20 GB+ SSD

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
docker-compose -f docker-compose.flags.yml up -d

# Verify services
docker-compose ps
```

**docker-compose.flags.yml:**

```yaml
version: '3.8'

services:
  redis:
    image: redis:7-alpine
    ports:
      - "6379:6379"
    volumes:
      - redis-data:/data
    command: redis-server --appendonly yes --maxmemory 2gb

  postgres:
    image: postgres:15-alpine
    ports:
      - "5432:5432"
    environment:
      POSTGRES_DB: feature_flags
      POSTGRES_USER: flags_user
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
    volumes:
      - postgres-data:/var/lib/postgresql/data

  jaeger:
    image: jaegertracing/all-in-one:1.52
    ports:
      - "5775:5775/udp"
      - "6831:6831/udp"
      - "16686:16686"  # Jaeger UI
      - "14268:14268"

volumes:
  redis-data:
  postgres-data:
```

**Step 3: Configure Application**

```bash
# Create appsettings.Development.json
cat > src/HotSwap.FeatureFlags.Api/appsettings.Development.json <<EOF
{
  "ConnectionStrings": {
    "Redis": "localhost:6379",
    "PostgreSQL": "Host=localhost;Database=feature_flags;Username=flags_user;Password=dev_password"
  },
  "FeatureFlags": {
    "CacheTTL": "PT60S",
    "EvaluationTimeout": "PT5S",
    "DefaultEnvironment": "Development"
  },
  "OpenTelemetry": {
    "JaegerEndpoint": "http://localhost:14268/api/traces",
    "SamplingRate": 1.0
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "HotSwap.FeatureFlags": "Debug"
    }
  }
}
EOF
```

**Step 4: Initialize Database**

```bash
# Run database migrations
dotnet ef database update --project src/HotSwap.FeatureFlags.Infrastructure

# Or use SQL script
psql -h localhost -U flags_user -d feature_flags -f db/migrations/001_feature_flags_schema.sql
```

**db/migrations/001_feature_flags_schema.sql:**

```sql
-- Feature flags table
CREATE TABLE feature_flags (
    name VARCHAR(255) PRIMARY KEY,
    description TEXT,
    type VARCHAR(50) NOT NULL,
    default_value TEXT NOT NULL,
    status VARCHAR(50) NOT NULL DEFAULT 'Active',
    environment VARCHAR(50) NOT NULL DEFAULT 'Development',
    tags JSONB,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP NOT NULL DEFAULT NOW(),
    created_by VARCHAR(255) NOT NULL,
    updated_by VARCHAR(255),
    archived_at TIMESTAMP
);

CREATE INDEX idx_flags_environment ON feature_flags(environment);
CREATE INDEX idx_flags_status ON feature_flags(status);
CREATE INDEX idx_flags_tags ON feature_flags USING gin(tags);

-- Rollouts table
CREATE TABLE rollouts (
    rollout_id VARCHAR(255) PRIMARY KEY,
    flag_name VARCHAR(255) NOT NULL REFERENCES feature_flags(name) ON DELETE CASCADE,
    strategy VARCHAR(50) NOT NULL,
    stages JSONB NOT NULL,
    current_stage_index INT NOT NULL DEFAULT 0,
    status VARCHAR(50) NOT NULL DEFAULT 'Pending',
    rollback_on_error BOOLEAN NOT NULL DEFAULT TRUE,
    thresholds JSONB,
    started_at TIMESTAMP,
    completed_at TIMESTAMP,
    rolled_back_at TIMESTAMP,
    rollback_reason TEXT,
    created_at TIMESTAMP NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_rollouts_flag_name ON rollouts(flag_name);
CREATE INDEX idx_rollouts_status ON rollouts(status);

-- Targets table
CREATE TABLE targets (
    target_id VARCHAR(255) PRIMARY KEY,
    flag_name VARCHAR(255) NOT NULL REFERENCES feature_flags(name) ON DELETE CASCADE,
    name VARCHAR(255),
    priority INT NOT NULL DEFAULT 0,
    rules JSONB NOT NULL,
    value TEXT NOT NULL,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMP NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_targets_flag_name ON targets(flag_name);
CREATE INDEX idx_targets_priority ON targets(priority);

-- Experiments table
CREATE TABLE experiments (
    experiment_id VARCHAR(255) PRIMARY KEY,
    name VARCHAR(255) NOT NULL,
    hypothesis TEXT,
    flag_name VARCHAR(255) NOT NULL REFERENCES feature_flags(name) ON DELETE CASCADE,
    variants JSONB NOT NULL,
    primary_metric VARCHAR(255),
    secondary_metrics JSONB,
    sample_size INT NOT NULL DEFAULT 10000,
    significance_level DECIMAL(3, 2) NOT NULL DEFAULT 0.05,
    status VARCHAR(50) NOT NULL DEFAULT 'Draft',
    started_at TIMESTAMP,
    ended_at TIMESTAMP,
    winning_variant VARCHAR(255),
    created_at TIMESTAMP NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_experiments_flag_name ON experiments(flag_name);
CREATE INDEX idx_experiments_status ON experiments(status);

-- Audit logs table
CREATE TABLE audit_logs (
    id BIGSERIAL PRIMARY KEY,
    event_type VARCHAR(100) NOT NULL,
    entity_type VARCHAR(100),
    entity_id VARCHAR(255),
    user_id VARCHAR(255),
    changes JSONB,
    trace_id VARCHAR(255),
    created_at TIMESTAMP NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_audit_logs_created_at ON audit_logs(created_at DESC);
CREATE INDEX idx_audit_logs_entity ON audit_logs(entity_type, entity_id);
CREATE INDEX idx_audit_logs_user ON audit_logs(user_id);
```

**Step 5: Run Application**

```bash
# Build and run
dotnet build
dotnet run --project src/HotSwap.FeatureFlags.Api

# Application starts on:
# HTTP:  http://localhost:5000
# HTTPS: https://localhost:5001
# Swagger: http://localhost:5000/swagger
```

**Step 6: Verify Installation**

```bash
# Health check
curl http://localhost:5000/health

# Create test flag
curl -X POST http://localhost:5000/api/v1/flags \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $(get_jwt_token)" \
  -d '{
    "name": "test-flag",
    "type": "Boolean",
    "defaultValue": "false"
  }'

# Evaluate test flag
curl http://localhost:5000/api/v1/flags/test-flag/evaluate?userId=user-123 \
  -H "Authorization: Bearer $(get_jwt_token)"
```

---

## Docker Deployment

### Build Docker Image

**Dockerfile:**

```dockerfile
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/HotSwap.FeatureFlags.Api/HotSwap.FeatureFlags.Api.csproj", "Api/"]
COPY ["src/HotSwap.FeatureFlags.Orchestrator/HotSwap.FeatureFlags.Orchestrator.csproj", "Orchestrator/"]
COPY ["src/HotSwap.FeatureFlags.Infrastructure/HotSwap.FeatureFlags.Infrastructure.csproj", "Infrastructure/"]
COPY ["src/HotSwap.FeatureFlags.Domain/HotSwap.FeatureFlags.Domain.csproj", "Domain/"]
RUN dotnet restore "Api/HotSwap.FeatureFlags.Api.csproj"

COPY src/ .
RUN dotnet publish "Api/HotSwap.FeatureFlags.Api.csproj" -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

# Create non-root user
RUN useradd -m -u 1000 flags && \
    chown -R flags:flags /app

USER flags

COPY --from=build /app/publish .

EXPOSE 5000
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
  CMD curl -f http://localhost:5000/health || exit 1

ENTRYPOINT ["dotnet", "HotSwap.FeatureFlags.Api.dll"]
```

**Build and Push:**

```bash
# Build image
docker build -t your-registry/feature-flags:1.0.0 .

# Tag for registry
docker tag your-registry/feature-flags:1.0.0 your-registry/feature-flags:latest

# Push to registry
docker push your-registry/feature-flags:1.0.0
docker push your-registry/feature-flags:latest
```

### Docker Compose Full Stack

**docker-compose.production.yml:**

```yaml
version: '3.8'

services:
  flag-service:
    image: your-registry/feature-flags:1.0.0
    ports:
      - "5000:5000"
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ASPNETCORE_URLS: http://+:5000
      ConnectionStrings__Redis: redis:6379
      ConnectionStrings__PostgreSQL: "Host=postgres;Database=feature_flags;Username=flags_user;Password=${POSTGRES_PASSWORD}"
      OpenTelemetry__JaegerEndpoint: "http://jaeger:14268/api/traces"
    depends_on:
      - redis
      - postgres
    networks:
      - flags-network
    deploy:
      replicas: 3
      resources:
        limits:
          cpus: '2'
          memory: 2G
        reservations:
          cpus: '1'
          memory: 1G

  redis:
    image: redis:7-alpine
    command: redis-server --appendonly yes --maxmemory 4gb --maxmemory-policy allkeys-lru
    volumes:
      - redis-data:/data
    networks:
      - flags-network

  postgres:
    image: postgres:15-alpine
    environment:
      POSTGRES_DB: feature_flags
      POSTGRES_USER: flags_user
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
    volumes:
      - postgres-data:/var/lib/postgresql/data
    networks:
      - flags-network

  jaeger:
    image: jaegertracing/all-in-one:1.52
    ports:
      - "16686:16686"
    networks:
      - flags-network

  prometheus:
    image: prom/prometheus:latest
    volumes:
      - ./prometheus.yml:/etc/prometheus/prometheus.yml
      - prometheus-data:/prometheus
    ports:
      - "9090:9090"
    networks:
      - flags-network

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
      - flags-network

volumes:
  redis-data:
  postgres-data:
  prometheus-data:
  grafana-data:

networks:
  flags-network:
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
docker-compose logs -f flag-service
```

---

## Kubernetes Deployment

### Namespace and ConfigMap

**namespace.yaml:**

```yaml
apiVersion: v1
kind: Namespace
metadata:
  name: feature-flags
```

**configmap.yaml:**

```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: flags-config
  namespace: feature-flags
data:
  appsettings.json: |
    {
      "FeatureFlags": {
        "CacheTTL": "PT60S",
        "EvaluationTimeout": "PT5S"
      },
      "OpenTelemetry": {
        "SamplingRate": 0.1
      }
    }
```

### Secrets

```bash
# Create secrets
kubectl create secret generic flags-secrets \
  --from-literal=redis-password=$(openssl rand -base64 32) \
  --from-literal=postgres-password=$(openssl rand -base64 32) \
  --from-literal=jwt-secret=$(openssl rand -base64 64) \
  -n feature-flags
```

### StatefulSets for Redis and PostgreSQL

**redis-statefulset.yaml:**

```yaml
apiVersion: apps/v1
kind: StatefulSet
metadata:
  name: redis
  namespace: feature-flags
spec:
  serviceName: redis
  replicas: 1
  selector:
    matchLabels:
      app: redis
  template:
    metadata:
      labels:
        app: redis
    spec:
      containers:
      - name: redis
        image: redis:7-alpine
        command: ["redis-server", "--appendonly", "yes", "--maxmemory", "4gb"]
        ports:
        - containerPort: 6379
          name: redis
        volumeMounts:
        - name: redis-data
          mountPath: /data
        resources:
          limits:
            memory: "4Gi"
            cpu: "2"
          requests:
            memory: "2Gi"
            cpu: "1"
  volumeClaimTemplates:
  - metadata:
      name: redis-data
    spec:
      accessModes: ["ReadWriteOnce"]
      resources:
        requests:
          storage: 20Gi
---
apiVersion: v1
kind: Service
metadata:
  name: redis
  namespace: feature-flags
spec:
  ports:
  - port: 6379
    targetPort: 6379
  clusterIP: None
  selector:
    app: redis
```

**postgres-statefulset.yaml:**

```yaml
apiVersion: apps/v1
kind: StatefulSet
metadata:
  name: postgres
  namespace: feature-flags
spec:
  serviceName: postgres
  replicas: 1
  selector:
    matchLabels:
      app: postgres
  template:
    metadata:
      labels:
        app: postgres
    spec:
      containers:
      - name: postgres
        image: postgres:15-alpine
        env:
        - name: POSTGRES_DB
          value: "feature_flags"
        - name: POSTGRES_USER
          value: "flags_user"
        - name: POSTGRES_PASSWORD
          valueFrom:
            secretKeyRef:
              name: flags-secrets
              key: postgres-password
        ports:
        - containerPort: 5432
          name: postgres
        volumeMounts:
        - name: postgres-data
          mountPath: /var/lib/postgresql/data
        resources:
          limits:
            memory: "4Gi"
            cpu: "2"
          requests:
            memory: "2Gi"
            cpu: "1"
  volumeClaimTemplates:
  - metadata:
      name: postgres-data
    spec:
      accessModes: ["ReadWriteOnce"]
      resources:
        requests:
          storage: 20Gi
---
apiVersion: v1
kind: Service
metadata:
  name: postgres
  namespace: feature-flags
spec:
  ports:
  - port: 5432
    targetPort: 5432
  clusterIP: None
  selector:
    app: postgres
```

### Feature Flags Service Deployment

**flags-deployment.yaml:**

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: flag-service
  namespace: feature-flags
spec:
  replicas: 3
  selector:
    matchLabels:
      app: flag-service
  template:
    metadata:
      labels:
        app: flag-service
    spec:
      containers:
      - name: flag-service
        image: your-registry/feature-flags:1.0.0
        ports:
        - containerPort: 5000
          name: http
        env:
        - name: ASPNETCORE_ENVIRONMENT
          value: "Production"
        - name: ConnectionStrings__Redis
          value: "redis:6379"
        - name: ConnectionStrings__PostgreSQL
          value: "Host=postgres;Database=feature_flags;Username=flags_user;Password=$(POSTGRES_PASSWORD)"
        - name: POSTGRES_PASSWORD
          valueFrom:
            secretKeyRef:
              name: flags-secrets
              key: postgres-password
        - name: JWT_SECRET
          valueFrom:
            secretKeyRef:
              name: flags-secrets
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
            memory: "2Gi"
            cpu: "2"
          requests:
            memory: "1Gi"
            cpu: "1"
      volumes:
      - name: config
        configMap:
          name: flags-config
---
apiVersion: v1
kind: Service
metadata:
  name: flag-service
  namespace: feature-flags
spec:
  type: LoadBalancer
  ports:
  - port: 80
    targetPort: 5000
    name: http
  selector:
    app: flag-service
---
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: flag-service-hpa
  namespace: feature-flags
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: flag-service
  minReplicas: 3
  maxReplicas: 10
  metrics:
  - type: Resource
    resource:
      name: cpu
      target:
        type: Utilization
        averageUtilization: 70
```

### Deploy to Kubernetes

```bash
# Create namespace
kubectl apply -f namespace.yaml

# Create ConfigMap and Secrets
kubectl apply -f configmap.yaml

# Deploy infrastructure
kubectl apply -f redis-statefulset.yaml
kubectl apply -f postgres-statefulset.yaml

# Wait for infrastructure
kubectl wait --for=condition=ready pod -l app=redis -n feature-flags --timeout=300s
kubectl wait --for=condition=ready pod -l app=postgres -n feature-flags --timeout=300s

# Run database migrations
kubectl run db-migrate --image=your-registry/feature-flags:1.0.0 \
  --env="ConnectionStrings__PostgreSQL=..." \
  --command -- dotnet ef database update \
  -n feature-flags

# Deploy flag service
kubectl apply -f flags-deployment.yaml

# Verify deployment
kubectl get pods -n feature-flags
kubectl get svc -n feature-flags

# Get service URL
kubectl get svc flag-service -n feature-flags -o jsonpath='{.status.loadBalancer.ingress[0].ip}'
```

---

## Migration Path

### Phase 1: Parallel Deployment (Week 1)

**Goal:** Deploy feature flag system alongside existing configuration

1. **Deploy Infrastructure:**
   ```bash
   docker-compose -f docker-compose.flags.yml up -d
   ```

2. **Create Initial Flags:**
   ```bash
   # Migrate existing feature toggles to flags
   curl -X POST http://localhost:5000/api/v1/flags \
     -d '{"name":"dark-mode","type":"Boolean","defaultValue":"false"}'
   ```

3. **Dual-Evaluation Pattern:**
   - Evaluate flags via new system AND check old config
   - Compare results for consistency
   - Monitor evaluation latency

### Phase 2: SDK Integration (Week 2)

**Goal:** Integrate SDKs into applications

1. **Install SDK:**
   ```bash
   dotnet add package HotSwap.FeatureFlags.Sdk
   ```

2. **Initialize SDK:**
   ```csharp
   var client = new FeatureFlagClient(apiKey: "your-api-key");
   var isEnabled = await client.EvaluateAsync("dark-mode", userId);
   ```

3. **Replace Old Config:**
   - Remove old feature toggle code
   - Use flag SDK for all evaluations

### Phase 3: Full Migration (Week 3-4)

**Goal:** Migrate all features and retire old system

1. **Enable Production Features:**
   - Approval workflow
   - Canary rollouts
   - Anomaly detection

2. **Production Monitoring:**
   - Prometheus metrics
   - Grafana dashboards
   - Alerting rules

---

## Configuration

### Environment Variables

```bash
# Application
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://+:5000

# Database Connections
ConnectionStrings__Redis=redis:6379
ConnectionStrings__PostgreSQL=Host=postgres;Database=feature_flags;...

# Feature Flags
FeatureFlags__CacheTTL=PT60S
FeatureFlags__EvaluationTimeout=PT5S

# OpenTelemetry
OpenTelemetry__JaegerEndpoint=http://jaeger:14268/api/traces
OpenTelemetry__SamplingRate=0.1

# JWT
JWT__Secret=your_jwt_secret
JWT__Issuer=https://flags.example.com

# Rate Limiting
RateLimiting__Evaluation=10000
RateLimiting__Management=100
```

---

## Monitoring Setup

### Prometheus Configuration

**prometheus.yml:**

```yaml
global:
  scrape_interval: 15s

scrape_configs:
  - job_name: 'feature-flags'
    static_configs:
      - targets: ['flag-service:5000']
```

### Grafana Dashboard

Import dashboard JSON: `grafana/dashboards/feature-flags.json`

**Key Panels:**
- Evaluation throughput (evals/sec)
- Evaluation latency (p50, p95, p99)
- Cache hit rate
- Active flags count
- Rollout progress

---

## Troubleshooting

### Issue: High evaluation latency

**Solution:**
1. Check Redis connection
2. Verify cache hit rate
3. Increase cache TTL
4. Scale Redis

### Issue: Inconsistent flag values

**Solution:**
1. Check user bucketing
2. Verify cache invalidation
3. Clear Redis cache

---

**Last Updated:** 2025-11-23
**Version:** 1.0.0
