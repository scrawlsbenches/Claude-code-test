# Deployment & Operations Guide

**Version:** 1.0.0
**Last Updated:** 2025-11-23

---

## Table of Contents

1. [Prerequisites](#prerequisites)
2. [Local Development Setup](#local-development-setup)
3. [Docker Deployment](#docker-deployment)
4. [Kubernetes Deployment](#kubernetes-deployment)
5. [Configuration Management](#configuration-management)
6. [Monitoring & Alerts](#monitoring--alerts)
7. [Backup & Recovery](#backup--recovery)
8. [Troubleshooting](#troubleshooting)

---

## Prerequisites

### Required Infrastructure

**Mandatory:**
- **.NET 8.0 Runtime** - Application runtime
- **PostgreSQL 15+** - Configuration storage, audit logs
- **Redis 7+** - Configuration cache

**Optional:**
- **Jaeger** - Distributed tracing
- **Prometheus** - Metrics collection
- **Grafana** - Metrics visualization
- **HashiCorp Vault** - Secret management

### System Requirements

**Application Server:**
- CPU: 2+ cores
- Memory: 4 GB+ RAM
- Disk: 20 GB+ SSD
- Network: 1 Gbps

**PostgreSQL:**
- CPU: 2+ cores
- Memory: 4 GB+ RAM
- Disk: 50 GB+ SSD

**Redis:**
- CPU: 2+ cores
- Memory: 8 GB+ RAM
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

# Start PostgreSQL, Redis, Jaeger
docker-compose -f docker-compose.config-service.yml up -d

# Verify services
docker-compose ps
```

**docker-compose.config-service.yml:**

```yaml
version: '3.8'

services:
  postgres:
    image: postgres:15-alpine
    ports:
      - "5432:5432"
    environment:
      POSTGRES_DB: config_service
      POSTGRES_USER: config_user
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
    volumes:
      - postgres-data:/var/lib/postgresql/data

  redis:
    image: redis:7-alpine
    ports:
      - "6379:6379"
    volumes:
      - redis-data:/data
    command: redis-server --appendonly yes

  jaeger:
    image: jaegertracing/all-in-one:1.52
    ports:
      - "16686:16686"  # Jaeger UI
      - "14268:14268"  # HTTP collector

volumes:
  postgres-data:
  redis-data:
```

**Step 3: Configure Application**

```bash
# Create appsettings.Development.json
cat > src/HotSwap.MultiTenantConfig.Api/appsettings.Development.json <<EOF
{
  "ConnectionStrings": {
    "PostgreSQL": "Host=localhost;Database=config_service;Username=config_user;Password=dev_password",
    "Redis": "localhost:6379"
  },
  "ConfigService": {
    "DefaultEnvironment": "Development",
    "CacheTTL": "PT5M",
    "MaxConfigsPerTenant": 1000
  },
  "OpenTelemetry": {
    "JaegerEndpoint": "http://localhost:14268/api/traces",
    "SamplingRate": 1.0
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "HotSwap.MultiTenantConfig": "Debug"
    }
  }
}
EOF
```

**Step 4: Initialize Database**

```bash
# Run database migrations
dotnet ef database update --project src/HotSwap.MultiTenantConfig.Infrastructure

# Or use SQL script
psql -h localhost -U config_user -d config_service -f db/migrations/001_initial_schema.sql
```

**db/migrations/001_initial_schema.sql:**

```sql
-- Tenants table
CREATE TABLE tenants (
    tenant_id VARCHAR(50) PRIMARY KEY,
    name VARCHAR(255) NOT NULL,
    tier VARCHAR(50) NOT NULL DEFAULT 'Free',
    status VARCHAR(50) NOT NULL DEFAULT 'Active',
    max_configurations INT NOT NULL DEFAULT 100,
    max_environments INT NOT NULL DEFAULT 4,
    contact_email VARCHAR(255),
    metadata JSONB,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP NOT NULL DEFAULT NOW(),
    deleted_at TIMESTAMP
);

-- Configurations table
CREATE TABLE configurations (
    config_id VARCHAR(50) PRIMARY KEY,
    tenant_id VARCHAR(50) NOT NULL REFERENCES tenants(tenant_id),
    key VARCHAR(255) NOT NULL,
    value TEXT NOT NULL,
    type VARCHAR(50) NOT NULL DEFAULT 'String',
    environment VARCHAR(50) NOT NULL DEFAULT 'Development',
    version INT NOT NULL DEFAULT 1,
    description TEXT,
    tags TEXT[],
    is_encrypted BOOLEAN NOT NULL DEFAULT FALSE,
    is_sensitive BOOLEAN NOT NULL DEFAULT FALSE,
    default_value TEXT,
    schema_id VARCHAR(50),
    created_by VARCHAR(255),
    updated_by VARCHAR(255),
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP NOT NULL DEFAULT NOW(),
    deleted_at TIMESTAMP,
    UNIQUE(tenant_id, key, environment)
);

CREATE INDEX idx_configs_tenant ON configurations(tenant_id, environment);
CREATE INDEX idx_configs_key ON configurations(key);

-- Configuration versions table
CREATE TABLE config_versions (
    version_id VARCHAR(50) PRIMARY KEY,
    config_id VARCHAR(50) NOT NULL REFERENCES configurations(config_id),
    version INT NOT NULL,
    value TEXT NOT NULL,
    previous_value TEXT,
    change_type VARCHAR(50) NOT NULL,
    change_description TEXT,
    changed_by VARCHAR(255) NOT NULL,
    environment VARCHAR(50) NOT NULL,
    ip_address VARCHAR(50),
    trace_id VARCHAR(255),
    created_at TIMESTAMP NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_versions_config ON config_versions(config_id, version DESC);

-- Approval requests table
CREATE TABLE approval_requests (
    approval_id VARCHAR(50) PRIMARY KEY,
    config_id VARCHAR(50) NOT NULL REFERENCES configurations(config_id),
    tenant_id VARCHAR(50) NOT NULL REFERENCES tenants(tenant_id),
    target_environment VARCHAR(50) NOT NULL,
    proposed_value TEXT NOT NULL,
    current_value TEXT,
    change_description TEXT,
    requested_by VARCHAR(255) NOT NULL,
    status VARCHAR(50) NOT NULL DEFAULT 'Pending',
    required_approval_level INT NOT NULL DEFAULT 1,
    current_approval_level INT NOT NULL DEFAULT 0,
    approvers JSONB,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    expires_at TIMESTAMP NOT NULL,
    resolved_at TIMESTAMP,
    resolved_by VARCHAR(255),
    resolved_comments TEXT,
    deployment_id VARCHAR(50)
);

CREATE INDEX idx_approvals_status ON approval_requests(status, expires_at);

-- Deployments table
CREATE TABLE deployments (
    deployment_id VARCHAR(50) PRIMARY KEY,
    config_id VARCHAR(50) NOT NULL REFERENCES configurations(config_id),
    tenant_id VARCHAR(50) NOT NULL REFERENCES tenants(tenant_id),
    environment VARCHAR(50) NOT NULL,
    strategy VARCHAR(50) NOT NULL,
    status VARCHAR(50) NOT NULL DEFAULT 'InProgress',
    value TEXT NOT NULL,
    previous_value TEXT,
    progress INT NOT NULL DEFAULT 0,
    canary_percentage INT,
    deployed_by VARCHAR(255) NOT NULL,
    started_at TIMESTAMP NOT NULL DEFAULT NOW(),
    completed_at TIMESTAMP,
    error_message TEXT,
    metrics JSONB,
    was_rolled_back BOOLEAN NOT NULL DEFAULT FALSE,
    rollback_reason TEXT,
    rolled_back_at TIMESTAMP
);

CREATE INDEX idx_deployments_tenant ON deployments(tenant_id, environment);
CREATE INDEX idx_deployments_status ON deployments(status, started_at DESC);

-- Audit logs table
CREATE TABLE audit_logs (
    log_id VARCHAR(50) PRIMARY KEY,
    event_type VARCHAR(100) NOT NULL,
    tenant_id VARCHAR(50),
    config_id VARCHAR(50),
    config_key VARCHAR(255),
    old_value TEXT,
    new_value TEXT,
    user_id VARCHAR(255),
    ip_address VARCHAR(50),
    trace_id VARCHAR(255),
    environment VARCHAR(50),
    details JSONB,
    timestamp TIMESTAMP NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_audit_logs_timestamp ON audit_logs(timestamp DESC);
CREATE INDEX idx_audit_logs_tenant ON audit_logs(tenant_id, timestamp DESC);
CREATE INDEX idx_audit_logs_event_type ON audit_logs(event_type);
```

**Step 5: Run Application**

```bash
# Build and run
dotnet build
dotnet run --project src/HotSwap.MultiTenantConfig.Api

# Application starts on:
# HTTP:  http://localhost:5000
# HTTPS: https://localhost:5001
# Swagger: http://localhost:5000/swagger
```

**Step 6: Verify Installation**

```bash
# Health check
curl http://localhost:5000/health

# Create test tenant
curl -X POST http://localhost:5000/api/v1/tenants \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $(get_jwt_token)" \
  -d '{
    "tenantId": "test-tenant",
    "name": "Test Tenant",
    "tier": "Enterprise"
  }'

# Create test configuration
curl -X POST http://localhost:5000/api/v1/configs \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $(get_jwt_token)" \
  -d '{
    "tenantId": "test-tenant",
    "key": "feature.test",
    "value": "true",
    "type": "Boolean",
    "environment": "Development"
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

COPY ["src/HotSwap.MultiTenantConfig.Api/HotSwap.MultiTenantConfig.Api.csproj", "Api/"]
COPY ["src/HotSwap.MultiTenantConfig.Orchestrator/HotSwap.MultiTenantConfig.Orchestrator.csproj", "Orchestrator/"]
COPY ["src/HotSwap.MultiTenantConfig.Infrastructure/HotSwap.MultiTenantConfig.Infrastructure.csproj", "Infrastructure/"]
COPY ["src/HotSwap.MultiTenantConfig.Domain/HotSwap.MultiTenantConfig.Domain.csproj", "Domain/"]
RUN dotnet restore "Api/HotSwap.MultiTenantConfig.Api.csproj"

COPY src/ .
RUN dotnet publish "Api/HotSwap.MultiTenantConfig.Api.csproj" -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

# Create non-root user
RUN useradd -m -u 1000 configservice && \
    chown -R configservice:configservice /app

USER configservice

COPY --from=build /app/publish .

EXPOSE 5000
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
  CMD curl -f http://localhost:5000/health || exit 1

ENTRYPOINT ["dotnet", "HotSwap.MultiTenantConfig.Api.dll"]
```

**Build and Push:**

```bash
# Build image
docker build -t your-registry/config-service:1.0.0 .

# Tag for registry
docker tag your-registry/config-service:1.0.0 your-registry/config-service:latest

# Push to registry
docker push your-registry/config-service:1.0.0
docker push your-registry/config-service:latest
```

### Docker Compose Production Deployment

```yaml
version: '3.8'

services:
  config-service:
    image: your-registry/config-service:1.0.0
    ports:
      - "5000:5000"
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ASPNETCORE_URLS: http://+:5000
      ConnectionStrings__PostgreSQL: "Host=postgres;Database=config_service;Username=config_user;Password=${POSTGRES_PASSWORD}"
      ConnectionStrings__Redis: "redis:6379"
      OpenTelemetry__JaegerEndpoint: "http://jaeger:14268/api/traces"
    depends_on:
      - postgres
      - redis
    networks:
      - config-network
    deploy:
      replicas: 3
      resources:
        limits:
          cpus: '2'
          memory: 4G
        reservations:
          cpus: '1'
          memory: 2G

  postgres:
    image: postgres:15-alpine
    environment:
      POSTGRES_DB: config_service
      POSTGRES_USER: config_user
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
    volumes:
      - postgres-data:/var/lib/postgresql/data
    networks:
      - config-network

  redis:
    image: redis:7-alpine
    command: redis-server --appendonly yes --maxmemory 4gb --maxmemory-policy allkeys-lru
    volumes:
      - redis-data:/data
    networks:
      - config-network

  jaeger:
    image: jaegertracing/all-in-one:1.52
    ports:
      - "16686:16686"
    networks:
      - config-network

  prometheus:
    image: prom/prometheus:latest
    volumes:
      - ./prometheus.yml:/etc/prometheus/prometheus.yml
      - prometheus-data:/prometheus
    ports:
      - "9090:9090"
    networks:
      - config-network

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
      - config-network

volumes:
  postgres-data:
  redis-data:
  prometheus-data:
  grafana-data:

networks:
  config-network:
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
docker-compose logs -f config-service
```

---

## Kubernetes Deployment

### ConfigMap and Secrets

**configmap.yaml:**

```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: config-service-config
  namespace: config-system
data:
  appsettings.json: |
    {
      "ConfigService": {
        "DefaultEnvironment": "Production",
        "CacheTTL": "PT5M",
        "MaxConfigsPerTenant": 1000
      },
      "OpenTelemetry": {
        "SamplingRate": 0.1
      }
    }
```

**secrets.yaml:**

```bash
# Create secrets
kubectl create secret generic config-service-secrets \
  --from-literal=postgres-password=$(openssl rand -base64 32) \
  --from-literal=redis-password=$(openssl rand -base64 32) \
  --from-literal=jwt-secret=$(openssl rand -base64 64) \
  -n config-system
```

### Deployment

**deployment.yaml:**

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: config-service
  namespace: config-system
spec:
  replicas: 3
  selector:
    matchLabels:
      app: config-service
  template:
    metadata:
      labels:
        app: config-service
    spec:
      containers:
      - name: config-service
        image: your-registry/config-service:1.0.0
        ports:
        - containerPort: 5000
          name: http
        env:
        - name: ASPNETCORE_ENVIRONMENT
          value: "Production"
        - name: ConnectionStrings__PostgreSQL
          value: "Host=postgres;Database=config_service;Username=config_user;Password=$(POSTGRES_PASSWORD)"
        - name: ConnectionStrings__Redis
          value: "redis:6379,password=$(REDIS_PASSWORD)"
        - name: POSTGRES_PASSWORD
          valueFrom:
            secretKeyRef:
              name: config-service-secrets
              key: postgres-password
        - name: REDIS_PASSWORD
          valueFrom:
            secretKeyRef:
              name: config-service-secrets
              key: redis-password
        - name: JWT_SECRET
          valueFrom:
            secretKeyRef:
              name: config-service-secrets
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
          name: config-service-config
---
apiVersion: v1
kind: Service
metadata:
  name: config-service
  namespace: config-system
spec:
  type: LoadBalancer
  ports:
  - port: 80
    targetPort: 5000
    name: http
  selector:
    app: config-service
---
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: config-service-hpa
  namespace: config-system
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: config-service
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

**Deploy to Kubernetes:**

```bash
# Create namespace
kubectl create namespace config-system

# Deploy resources
kubectl apply -f configmap.yaml
kubectl apply -f secrets.yaml
kubectl apply -f deployment.yaml

# Verify deployment
kubectl get pods -n config-system
kubectl get svc -n config-system

# Get service URL
kubectl get svc config-service -n config-system -o jsonpath='{.status.loadBalancer.ingress[0].ip}'
```

---

## Configuration Management

### Environment Variables

```bash
# Application
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://+:5000

# Database
ConnectionStrings__PostgreSQL=Host=postgres;Database=config_service;Username=config_user;Password=secure_password
ConnectionStrings__Redis=redis:6379,password=secure_password

# Config Service
ConfigService__DefaultEnvironment=Production
ConfigService__CacheTTL=PT5M
ConfigService__MaxConfigsPerTenant=1000

# OpenTelemetry
OpenTelemetry__JaegerEndpoint=http://jaeger:14268/api/traces
OpenTelemetry__SamplingRate=0.1

# JWT
JWT__Secret=your_jwt_secret_key_here
JWT__Issuer=https://config.example.com
JWT__Audience=config-service-api

# Rate Limiting
RateLimiting__ConfigGet=1000
RateLimiting__ConfigCreate=100
RateLimiting__Deployments=10
```

---

## Monitoring & Alerts

### Grafana Dashboard

**Key Metrics:**
- Configuration retrieval latency (p50, p95, p99)
- Cache hit rate
- Deployment success rate
- Active tenants and configurations
- Error rate

### Prometheus Alerts

```yaml
groups:
- name: config_service_alerts
  rules:
  - alert: HighConfigRetrievalLatency
    expr: histogram_quantile(0.99, config_get_duration_bucket) > 10
    for: 5m
    labels:
      severity: warning
    annotations:
      summary: "Config retrieval latency high (p99 > 10ms)"

  - alert: LowCacheHitRate
    expr: cache_hit_rate < 0.90
    for: 10m
    labels:
      severity: warning
    annotations:
      summary: "Cache hit rate below 90%"

  - alert: DeploymentFailureRate
    expr: rate(deployments_failed_total[5m]) > 0.1
    for: 5m
    labels:
      severity: critical
    annotations:
      summary: "Deployment failure rate > 10%"
```

---

## Backup & Recovery

### Database Backup

```bash
# Backup PostgreSQL
pg_dump -h postgres -U config_user config_service > backup_$(date +%Y%m%d_%H%M%S).sql

# Restore from backup
psql -h postgres -U config_user config_service < backup_20251123_120000.sql
```

### Redis Backup

```bash
# Redis automatically saves to disk (AOF enabled)
# Backup RDB file
docker exec redis redis-cli BGSAVE
docker cp redis:/data/dump.rdb ./backup/
```

---

## Troubleshooting

### Issue: High Latency

**Symptom:** p99 config retrieval > 10ms

**Solutions:**
1. Check cache hit rate
2. Increase Redis memory
3. Optimize database queries
4. Add database indexes

### Issue: Cache Misses

**Symptom:** Cache hit rate < 90%

**Solutions:**
1. Increase cache TTL
2. Implement cache warming
3. Check cache invalidation logic

### Issue: Deployment Failures

**Symptom:** Canary deployments failing

**Solutions:**
1. Review error logs
2. Adjust error thresholds
3. Check metrics collection
4. Verify approval workflow

---

**Last Updated:** 2025-11-23
**Version:** 1.0.0
