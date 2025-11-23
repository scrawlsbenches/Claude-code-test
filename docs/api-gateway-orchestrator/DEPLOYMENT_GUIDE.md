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
- **Redis 7+** - Configuration caching, distributed locks, rate limiting
- **PostgreSQL 15+** - Route configuration storage

**Optional:**
- **Jaeger** - Distributed tracing (recommended for production)
- **Prometheus** - Metrics collection
- **Grafana** - Metrics visualization

### System Requirements

**Gateway Node:**
- CPU: 4+ cores
- Memory: 8 GB+ RAM
- Disk: 20 GB+ SSD
- Network: 10 Gbps (for high throughput)

**Redis:**
- CPU: 2+ cores
- Memory: 4 GB+ RAM
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
docker-compose -f docker-compose.gateway.yml up -d

# Verify services
docker-compose ps
```

**docker-compose.gateway.yml:**

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
      POSTGRES_DB: gateway
      POSTGRES_USER: gateway_user
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
    volumes:
      - postgres-data:/var/lib/postgresql/data

  jaeger:
    image: jaegertracing/all-in-one:1.52
    ports:
      - "16686:16686"  # Jaeger UI
      - "14268:14268"  # Traces endpoint

  test-backend:
    image: kennethreitz/httpbin
    ports:
      - "8080:80"

volumes:
  redis-data:
  postgres-data:
```

**Step 3: Configure Application**

```bash
# Create appsettings.Development.json
cat > src/HotSwap.Gateway.Api/appsettings.Development.json <<EOF
{
  "ConnectionStrings": {
    "Redis": "localhost:6379",
    "PostgreSQL": "Host=localhost;Database=gateway;Username=gateway_user;Password=dev_password"
  },
  "Gateway": {
    "Port": 8000,
    "MaxConcurrentConnections": 10000,
    "DefaultTimeoutSeconds": 30,
    "ConfigCacheTTL": "PT5M"
  },
  "OpenTelemetry": {
    "JaegerEndpoint": "http://localhost:14268/api/traces",
    "SamplingRate": 1.0
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "HotSwap.Gateway": "Debug"
    }
  }
}
EOF
```

**Step 4: Initialize Database**

```bash
# Run database migrations
dotnet ef database update --project src/HotSwap.Gateway.Infrastructure

# Or use SQL script
psql -h localhost -U gateway_user -d gateway -f db/migrations/001_initial_schema.sql
```

**db/migrations/001_initial_schema.sql:**

```sql
-- Gateway routes table
CREATE TABLE gateway_routes (
    route_id VARCHAR(255) PRIMARY KEY,
    name VARCHAR(255) NOT NULL,
    path_pattern VARCHAR(500) NOT NULL,
    methods TEXT[],
    backends JSONB NOT NULL,
    strategy VARCHAR(50) NOT NULL,
    strategy_config JSONB,
    timeout_seconds INT NOT NULL DEFAULT 30,
    is_enabled BOOLEAN NOT NULL DEFAULT true,
    priority INT NOT NULL DEFAULT 0,
    transformations JSONB,
    rate_limit JSONB,
    circuit_breaker JSONB,
    retry_policy JSONB,
    version VARCHAR(50) NOT NULL DEFAULT '1.0',
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_routes_path_pattern ON gateway_routes(path_pattern);
CREATE INDEX idx_routes_enabled ON gateway_routes(is_enabled);

-- Backends table
CREATE TABLE backends (
    backend_id VARCHAR(255) PRIMARY KEY,
    name VARCHAR(255) NOT NULL,
    url VARCHAR(500) NOT NULL,
    weight INT NOT NULL DEFAULT 100,
    is_enabled BOOLEAN NOT NULL DEFAULT true,
    health_status VARCHAR(50) NOT NULL DEFAULT 'Unknown',
    health_check JSONB,
    connection_pool JSONB,
    timeouts JSONB,
    last_health_check TIMESTAMP,
    active_connections INT NOT NULL DEFAULT 0,
    total_requests BIGINT NOT NULL DEFAULT 0,
    failed_requests BIGINT NOT NULL DEFAULT 0,
    created_at TIMESTAMP NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_backends_health_status ON backends(health_status);

-- Deployments table
CREATE TABLE deployments (
    deployment_id VARCHAR(255) PRIMARY KEY,
    route_id VARCHAR(255) NOT NULL,
    config_version VARCHAR(50) NOT NULL,
    strategy VARCHAR(50) NOT NULL,
    environment VARCHAR(50) NOT NULL,
    status VARCHAR(50) NOT NULL DEFAULT 'Pending',
    traffic_split JSONB,
    phases JSONB,
    current_phase_index INT NOT NULL DEFAULT 0,
    started_at TIMESTAMP,
    completed_at TIMESTAMP,
    metrics JSONB,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    created_by VARCHAR(255),
    approval_status VARCHAR(50) NOT NULL DEFAULT 'Pending',
    approved_by VARCHAR(255),
    approved_at TIMESTAMP,
    rolled_back_at TIMESTAMP,
    rollback_reason TEXT
);

CREATE INDEX idx_deployments_route_id ON deployments(route_id);
CREATE INDEX idx_deployments_status ON deployments(status);

-- Audit logs table
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

**Step 5: Run Application**

```bash
# Build and run
dotnet build
dotnet run --project src/HotSwap.Gateway.Api

# Application starts on:
# Gateway Proxy: http://localhost:8000
# Management API: http://localhost:5000
# Swagger: http://localhost:5000/swagger
```

**Step 6: Create Test Route**

```bash
# Create test route
curl -X POST http://localhost:5000/api/v1/gateway/routes \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $(get_jwt_token)" \
  -d '{
    "name": "test-route",
    "pathPattern": "/test/*",
    "backends": [
      {
        "name": "httpbin",
        "url": "http://localhost:8080"
      }
    ],
    "strategy": "RoundRobin"
  }'

# Test proxying
curl http://localhost:8000/test/get
```

---

## Docker Deployment

### Build Docker Image

**Dockerfile:**

```dockerfile
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/HotSwap.Gateway.Api/HotSwap.Gateway.Api.csproj", "Api/"]
COPY ["src/HotSwap.Gateway.Orchestrator/HotSwap.Gateway.Orchestrator.csproj", "Orchestrator/"]
COPY ["src/HotSwap.Gateway.Infrastructure/HotSwap.Gateway.Infrastructure.csproj", "Infrastructure/"]
COPY ["src/HotSwap.Gateway.Domain/HotSwap.Gateway.Domain.csproj", "Domain/"]
RUN dotnet restore "Api/HotSwap.Gateway.Api.csproj"

COPY src/ .
RUN dotnet publish "Api/HotSwap.Gateway.Api.csproj" -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

# Create non-root user
RUN useradd -m -u 1000 gateway && \
    chown -R gateway:gateway /app

USER gateway

COPY --from=build /app/publish .

EXPOSE 5000 8000
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
  CMD curl -f http://localhost:5000/api/v1/gateway/health || exit 1

ENTRYPOINT ["dotnet", "HotSwap.Gateway.Api.dll"]
```

**Build and Push:**

```bash
# Build image
docker build -t your-registry/api-gateway:1.0.0 .

# Push to registry
docker push your-registry/api-gateway:1.0.0
```

---

## Kubernetes Deployment

### Deployment Manifest

**k8s/deployment.yaml:**

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: api-gateway
  namespace: gateway
spec:
  replicas: 3
  selector:
    matchLabels:
      app: api-gateway
  template:
    metadata:
      labels:
        app: api-gateway
    spec:
      containers:
      - name: api-gateway
        image: your-registry/api-gateway:1.0.0
        ports:
        - name: api
          containerPort: 5000
        - name: proxy
          containerPort: 8000
        env:
        - name: ConnectionStrings__Redis
          value: "redis-service:6379"
        - name: ConnectionStrings__PostgreSQL
          valueFrom:
            secretKeyRef:
              name: gateway-secrets
              key: postgres-connection-string
        - name: Gateway__Port
          value: "8000"
        - name: OpenTelemetry__JaegerEndpoint
          value: "http://jaeger-collector:14268/api/traces"
        resources:
          requests:
            cpu: "2"
            memory: "4Gi"
          limits:
            cpu: "4"
            memory: "8Gi"
        livenessProbe:
          httpGet:
            path: /api/v1/gateway/health
            port: 5000
          initialDelaySeconds: 10
          periodSeconds: 30
        readinessProbe:
          httpGet:
            path: /api/v1/gateway/health
            port: 5000
          initialDelaySeconds: 5
          periodSeconds: 10
---
apiVersion: v1
kind: Service
metadata:
  name: api-gateway
  namespace: gateway
spec:
  type: LoadBalancer
  ports:
  - name: api
    port: 5000
    targetPort: 5000
  - name: proxy
    port: 8000
    targetPort: 8000
  selector:
    app: api-gateway
```

**Deploy:**

```bash
kubectl apply -f k8s/namespace.yaml
kubectl apply -f k8s/secrets.yaml
kubectl apply -f k8s/deployment.yaml

# Verify deployment
kubectl get pods -n gateway
kubectl get svc -n gateway
```

---

## Configuration

### Environment Variables

| Variable | Description | Default | Required |
|----------|-------------|---------|----------|
| `ConnectionStrings__Redis` | Redis connection string | localhost:6379 | Yes |
| `ConnectionStrings__PostgreSQL` | PostgreSQL connection string | - | Yes |
| `Gateway__Port` | Gateway proxy port | 8000 | No |
| `Gateway__MaxConcurrentConnections` | Max concurrent connections | 10000 | No |
| `Gateway__DefaultTimeoutSeconds` | Default backend timeout | 30 | No |
| `OpenTelemetry__JaegerEndpoint` | Jaeger endpoint | - | No |
| `OpenTelemetry__SamplingRate` | Trace sampling rate (0.0-1.0) | 0.1 | No |

---

## Monitoring Setup

### Prometheus Metrics

**prometheus.yml:**

```yaml
scrape_configs:
  - job_name: 'api-gateway'
    static_configs:
      - targets: ['api-gateway:5000']
    metrics_path: '/metrics'
    scrape_interval: 15s
```

### Grafana Dashboard

Import dashboard from `/docs/api-gateway-orchestrator/grafana-dashboard.json`

**Key Panels:**
- Request throughput (req/sec)
- Latency (p50, p95, p99)
- Error rate
- Active connections
- Backend health status
- Circuit breaker states

---

## Operational Runbooks

### Runbook 1: Deploy New Route Configuration

```bash
# 1. Create new route configuration
curl -X POST http://api-gateway:5000/api/v1/gateway/routes \
  -H "Authorization: Bearer $TOKEN" \
  -d @new-route.json

# 2. Create canary deployment
curl -X POST http://api-gateway:5000/api/v1/gateway/deployments \
  -H "Authorization: Bearer $TOKEN" \
  -d '{
    "routeId": "route-123",
    "configVersion": "2.0",
    "strategy": "Canary"
  }'

# 3. Monitor deployment
watch -n 5 'curl -s http://api-gateway:5000/api/v1/gateway/deployments/deploy-xyz/metrics | jq'

# 4. Promote or rollback based on metrics
curl -X POST http://api-gateway:5000/api/v1/gateway/deployments/deploy-xyz/promote
# OR
curl -X POST http://api-gateway:5000/api/v1/gateway/deployments/deploy-xyz/rollback
```

### Runbook 2: Handle Backend Failure

```bash
# 1. Check backend health
curl http://api-gateway:5000/api/v1/gateway/backends/backend-123/health

# 2. Disable unhealthy backend
curl -X PUT http://api-gateway:5000/api/v1/gateway/backends/backend-123 \
  -H "Authorization: Bearer $TOKEN" \
  -d '{"isEnabled": false}'

# 3. Verify traffic shifted to healthy backends
curl http://api-gateway:5000/api/v1/gateway/metrics

# 4. Fix backend and re-enable
curl -X PUT http://api-gateway:5000/api/v1/gateway/backends/backend-123 \
  -H "Authorization: Bearer $TOKEN" \
  -d '{"isEnabled": true}'
```

---

## Troubleshooting

### Issue: High Latency

**Symptoms:** p99 latency > 100ms

**Diagnostics:**
```bash
# Check backend latency
curl http://api-gateway:5000/api/v1/gateway/metrics | jq '.latency'

# Check active connections
curl http://api-gateway:5000/api/v1/gateway/health | jq '.activeConnections'

# Check circuit breaker states
curl http://api-gateway:5000/api/v1/gateway/metrics | jq '.circuitBreakers'
```

**Solutions:**
1. Increase backend connection pool size
2. Add more backend instances
3. Enable circuit breaker to fail-fast on slow backends
4. Switch to LeastConnections routing strategy

### Issue: Dropped Requests During Config Update

**Symptoms:** 502/503 errors during deployment

**Solutions:**
1. Increase connection draining timeout
2. Use graceful shutdown
3. Deploy with rolling strategy instead of direct
4. Monitor metrics during deployment

---

**Last Updated:** 2025-11-23
**Version:** 1.0.0
