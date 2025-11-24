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
9. [Security Hardening](#security-hardening)

---

## Prerequisites

### Required Infrastructure

**Mandatory:**
- **.NET 8.0 Runtime** - Application runtime
- **Redis 7+** - Configuration caching, distributed locks
- **PostgreSQL 15+** - Configuration storage, versioning, audit logs

**Optional but Recommended:**
- **Jaeger** - Distributed tracing (recommended for production)
- **Prometheus** - Metrics collection
- **Grafana** - Metrics visualization
- **Nginx** - Reverse proxy / load balancer

### System Requirements

**Control Plane Node:**
- CPU: 4+ cores
- Memory: 8 GB+ RAM
- Disk: 50 GB+ SSD
- Network: 1 Gbps

**Edge Location Simulator (for testing):**
- CPU: 2+ cores
- Memory: 4 GB+ RAM
- Disk: 20 GB+ SSD

**Redis:**
- CPU: 2+ cores
- Memory: 8 GB+ RAM (depends on configuration count)
- Disk: 50 GB+ SSD

**PostgreSQL:**
- CPU: 4+ cores
- Memory: 8 GB+ RAM
- Disk: 100 GB+ SSD

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
docker-compose -f docker-compose.cdn.yml up -d

# Verify services
docker-compose ps
```

**docker-compose.cdn.yml:**

```yaml
version: '3.8'

services:
  redis:
    image: redis:7-alpine
    ports:
      - "6379:6379"
    volumes:
      - redis-data:/data
    command: redis-server --appendonly yes --maxmemory 2gb --maxmemory-policy allkeys-lru

  postgres:
    image: postgres:15-alpine
    ports:
      - "5432:5432"
    environment:
      POSTGRES_DB: cdn_config
      POSTGRES_USER: cdn_user
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
    volumes:
      - postgres-data:/var/lib/postgresql/data
      - ./db/migrations:/docker-entrypoint-initdb.d

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
    environment:
      COLLECTOR_ZIPKIN_HOST_PORT: ":9411"

  prometheus:
    image: prom/prometheus:latest
    ports:
      - "9090:9090"
    volumes:
      - ./monitoring/prometheus.yml:/etc/prometheus/prometheus.yml
      - prometheus-data:/prometheus
    command:
      - '--config.file=/etc/prometheus/prometheus.yml'
      - '--storage.tsdb.path=/prometheus'

  grafana:
    image: grafana/grafana:latest
    ports:
      - "3000:3000"
    environment:
      GF_SECURITY_ADMIN_PASSWORD: admin
      GF_USERS_ALLOW_SIGN_UP: false
    volumes:
      - grafana-data:/var/lib/grafana
      - ./monitoring/grafana/dashboards:/etc/grafana/provisioning/dashboards

  edge-simulator:
    build: ./edge-simulator
    ports:
      - "8080-8089:8080"  # 10 simulated edge locations
    environment:
      EDGE_COUNT: 10
      CONTROL_PLANE_URL: http://host.docker.internal:5000

volumes:
  redis-data:
  postgres-data:
  prometheus-data:
  grafana-data:
```

**Step 3: Configure Application**

```bash
# Create appsettings.Development.json
cat > src/HotSwap.CDN.Api/appsettings.Development.json <<EOF
{
  "ConnectionStrings": {
    "Redis": "localhost:6379",
    "PostgreSQL": "Host=localhost;Database=cdn_config;Username=cdn_user;Password=dev_password"
  },
  "CDN": {
    "ControlPlanePort": 5000,
    "MaxConfigurationSize": 1048576,
    "DefaultDeploymentStrategy": "RegionalCanary",
    "ConfigurationCacheTTL": "PT1H",
    "EdgeLocationHealthCheckInterval": "PT30S"
  },
  "Deployment": {
    "MaxConcurrentDeployments": 50,
    "DefaultCanaryPercentage": 10,
    "DefaultMonitorDuration": "PT5M",
    "DefaultRollbackThresholds": {
      "CacheHitRate": 80.0,
      "ErrorRate": 1.0,
      "P99LatencyMs": 200.0
    }
  },
  "OpenTelemetry": {
    "JaegerEndpoint": "http://localhost:14268/api/traces",
    "SamplingRate": 1.0
  },
  "Prometheus": {
    "Enabled": true,
    "Port": 9091
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "HotSwap.CDN": "Debug",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "RateLimiting": {
    "EnableRateLimiting": true,
    "PerUserLimits": {
      "ConfigurationCreate": "20/1m",
      "ConfigurationRead": "60/1m",
      "DeploymentCreate": "30/1m",
      "MetricsRead": "120/1m"
    }
  }
}
EOF
```

**Step 4: Initialize Database**

```bash
# Run database migrations
dotnet ef database update --project src/HotSwap.CDN.Infrastructure

# Or use SQL script
psql -h localhost -U cdn_user -d cdn_config -f db/migrations/001_initial_schema.sql
```

**db/migrations/001_initial_schema.sql:**

```sql
-- Configurations table
CREATE TABLE configurations (
    configuration_id VARCHAR(255) PRIMARY KEY,
    name VARCHAR(255) NOT NULL UNIQUE,
    type VARCHAR(50) NOT NULL,
    content TEXT NOT NULL,
    schema_version VARCHAR(50) NOT NULL,
    version VARCHAR(50) NOT NULL DEFAULT '1.0.0',
    description TEXT,
    status VARCHAR(50) NOT NULL DEFAULT 'Draft',
    created_by VARCHAR(255) NOT NULL,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP NOT NULL DEFAULT NOW(),
    approved_by VARCHAR(255),
    approved_at TIMESTAMP,
    is_deployed BOOLEAN DEFAULT FALSE,
    metadata JSONB DEFAULT '{}'::jsonb
);

CREATE INDEX idx_configurations_name ON configurations(name);
CREATE INDEX idx_configurations_type ON configurations(type);
CREATE INDEX idx_configurations_status ON configurations(status);
CREATE INDEX idx_configurations_created_at ON configurations(created_at DESC);

-- Edge locations table
CREATE TABLE edge_locations (
    location_id VARCHAR(255) PRIMARY KEY,
    name VARCHAR(255) NOT NULL,
    region VARCHAR(255) NOT NULL,
    country_code VARCHAR(2) NOT NULL,
    city VARCHAR(255),
    type VARCHAR(50) NOT NULL DEFAULT 'EdgePOP',
    endpoint VARCHAR(500) NOT NULL,
    is_active BOOLEAN DEFAULT TRUE,
    last_heartbeat TIMESTAMP,
    started_at TIMESTAMP DEFAULT NOW(),
    capacity_config JSONB DEFAULT '{}'::jsonb,
    metadata JSONB DEFAULT '{}'::jsonb
);

CREATE INDEX idx_edge_locations_region ON edge_locations(region);
CREATE INDEX idx_edge_locations_is_active ON edge_locations(is_active);

-- Deployments table
CREATE TABLE deployments (
    deployment_id VARCHAR(255) PRIMARY KEY,
    configuration_id VARCHAR(255) NOT NULL,
    configuration_version VARCHAR(50) NOT NULL,
    strategy VARCHAR(50) NOT NULL,
    status VARCHAR(50) NOT NULL DEFAULT 'Pending',
    progress_percentage INT DEFAULT 0,
    deployed_by VARCHAR(255) NOT NULL,
    started_at TIMESTAMP NOT NULL DEFAULT NOW(),
    completed_at TIMESTAMP,
    canary_config JSONB,
    rollback_config JSONB,
    metadata JSONB DEFAULT '{}'::jsonb,
    FOREIGN KEY (configuration_id) REFERENCES configurations(configuration_id)
);

CREATE INDEX idx_deployments_configuration_id ON deployments(configuration_id);
CREATE INDEX idx_deployments_status ON deployments(status);
CREATE INDEX idx_deployments_started_at ON deployments(started_at DESC);

-- Configuration versions table
CREATE TABLE configuration_versions (
    version_id VARCHAR(255) PRIMARY KEY,
    configuration_id VARCHAR(255) NOT NULL,
    version VARCHAR(50) NOT NULL,
    content TEXT NOT NULL,
    schema_version VARCHAR(50) NOT NULL,
    change_description TEXT,
    created_by VARCHAR(255) NOT NULL,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    is_deployed BOOLEAN DEFAULT FALSE,
    FOREIGN KEY (configuration_id) REFERENCES configurations(configuration_id)
);

CREATE INDEX idx_versions_configuration_id ON configuration_versions(configuration_id);
CREATE INDEX idx_versions_created_at ON configuration_versions(created_at DESC);

-- Deployment edge locations (many-to-many)
CREATE TABLE deployment_edge_locations (
    deployment_id VARCHAR(255) NOT NULL,
    edge_location_id VARCHAR(255) NOT NULL,
    status VARCHAR(50) NOT NULL DEFAULT 'Pending',
    deployed_at TIMESTAMP,
    error_message TEXT,
    PRIMARY KEY (deployment_id, edge_location_id),
    FOREIGN KEY (deployment_id) REFERENCES deployments(deployment_id),
    FOREIGN KEY (edge_location_id) REFERENCES edge_locations(location_id)
);

-- Configuration tags table
CREATE TABLE configuration_tags (
    configuration_id VARCHAR(255) NOT NULL,
    tag VARCHAR(100) NOT NULL,
    PRIMARY KEY (configuration_id, tag),
    FOREIGN KEY (configuration_id) REFERENCES configurations(configuration_id)
);

CREATE INDEX idx_tags_tag ON configuration_tags(tag);

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
CREATE INDEX idx_audit_logs_user ON audit_logs(user_id);

-- Metrics snapshots table (for performance monitoring)
CREATE TABLE metrics_snapshots (
    id BIGSERIAL PRIMARY KEY,
    deployment_id VARCHAR(255),
    edge_location_id VARCHAR(255),
    snapshot_type VARCHAR(50) NOT NULL,  -- 'pre_deployment', 'post_deployment', 'canary'
    cache_hit_rate DECIMAL(5,2),
    avg_latency_ms DECIMAL(10,2),
    p99_latency_ms DECIMAL(10,2),
    error_rate DECIMAL(5,2),
    requests_per_sec DECIMAL(10,2),
    timestamp TIMESTAMP NOT NULL DEFAULT NOW(),
    FOREIGN KEY (deployment_id) REFERENCES deployments(deployment_id),
    FOREIGN KEY (edge_location_id) REFERENCES edge_locations(location_id)
);

CREATE INDEX idx_metrics_deployment_id ON metrics_snapshots(deployment_id);
CREATE INDEX idx_metrics_timestamp ON metrics_snapshots(timestamp DESC);
```

**Step 5: Run Application**

```bash
# Build the application
dotnet build

# Run the API
cd src/HotSwap.CDN.Api
dotnet run

# API should be available at:
# - HTTP: http://localhost:5000
# - HTTPS: https://localhost:5001
# - Swagger UI: https://localhost:5001/swagger
```

**Step 6: Verify Setup**

```bash
# Check health endpoint
curl http://localhost:5000/health

# Should return:
# {
#   "status": "Healthy",
#   "checks": {
#     "postgres": "Healthy",
#     "redis": "Healthy"
#   }
# }

# Create test edge location
curl -X POST http://localhost:5000/api/v1/edge-locations \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer {your-token}" \
  -d '{
    "locationId": "test-local-1",
    "name": "Test Edge Local",
    "region": "Development",
    "countryCode": "US",
    "endpoint": "http://localhost:8080"
  }'

# Create test configuration
curl -X POST http://localhost:5000/api/v1/configurations \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer {your-token}" \
  -d '{
    "name": "test-cache-rule",
    "type": "CacheRule",
    "content": {
      "pathPattern": "/test/*",
      "ttl": 3600
    },
    "schemaVersion": "1.0"
  }'
```

---

## Docker Deployment

### Build Docker Image

**Dockerfile:**

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/HotSwap.CDN.Api/HotSwap.CDN.Api.csproj", "src/HotSwap.CDN.Api/"]
COPY ["src/HotSwap.CDN.Domain/HotSwap.CDN.Domain.csproj", "src/HotSwap.CDN.Domain/"]
COPY ["src/HotSwap.CDN.Infrastructure/HotSwap.CDN.Infrastructure.csproj", "src/HotSwap.CDN.Infrastructure/"]
COPY ["src/HotSwap.CDN.Orchestrator/HotSwap.CDN.Orchestrator.csproj", "src/HotSwap.CDN.Orchestrator/"]
RUN dotnet restore "src/HotSwap.CDN.Api/HotSwap.CDN.Api.csproj"
COPY . .
WORKDIR "/src/src/HotSwap.CDN.Api"
RUN dotnet build "HotSwap.CDN.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "HotSwap.CDN.Api.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "HotSwap.CDN.Api.dll"]
```

**Build and Run:**

```bash
# Build image
docker build -t cdn-config-manager:latest .

# Run container
docker run -d \
  --name cdn-config-manager \
  -p 5000:80 \
  -e ConnectionStrings__PostgreSQL="Host=postgres;Database=cdn_config;Username=cdn_user;Password=${DB_PASSWORD}" \
  -e ConnectionStrings__Redis="redis:6379" \
  --network cdn-network \
  cdn-config-manager:latest
```

---

## Kubernetes Deployment

### Kubernetes Manifests

**namespace.yaml:**

```yaml
apiVersion: v1
kind: Namespace
metadata:
  name: cdn-config-manager
```

**configmap.yaml:**

```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: cdn-config
  namespace: cdn-config-manager
data:
  CDN__ControlPlanePort: "80"
  CDN__MaxConfigurationSize: "1048576"
  CDN__DefaultDeploymentStrategy: "RegionalCanary"
  Deployment__MaxConcurrentDeployments: "50"
  OpenTelemetry__SamplingRate: "0.1"
```

**secret.yaml:**

```yaml
apiVersion: v1
kind: Secret
metadata:
  name: cdn-secrets
  namespace: cdn-config-manager
type: Opaque
stringData:
  postgres-connection: "Host=postgres-service;Database=cdn_config;Username=cdn_user;Password=your-password"
  redis-connection: "redis-service:6379"
  jwt-secret: "your-jwt-secret-key"
```

**deployment.yaml:**

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: cdn-config-manager
  namespace: cdn-config-manager
spec:
  replicas: 3
  selector:
    matchLabels:
      app: cdn-config-manager
  template:
    metadata:
      labels:
        app: cdn-config-manager
    spec:
      containers:
      - name: api
        image: cdn-config-manager:latest
        ports:
        - containerPort: 80
          name: http
        env:
        - name: ConnectionStrings__PostgreSQL
          valueFrom:
            secretKeyRef:
              name: cdn-secrets
              key: postgres-connection
        - name: ConnectionStrings__Redis
          valueFrom:
            secretKeyRef:
              name: cdn-secrets
              key: redis-connection
        envFrom:
        - configMapRef:
            name: cdn-config
        resources:
          requests:
            memory: "512Mi"
            cpu: "250m"
          limits:
            memory: "2Gi"
            cpu: "1000m"
        livenessProbe:
          httpGet:
            path: /health
            port: 80
          initialDelaySeconds: 30
          periodSeconds: 10
        readinessProbe:
          httpGet:
            path: /health
            port: 80
          initialDelaySeconds: 10
          periodSeconds: 5
```

**service.yaml:**

```yaml
apiVersion: v1
kind: Service
metadata:
  name: cdn-config-manager
  namespace: cdn-config-manager
spec:
  type: LoadBalancer
  ports:
  - port: 80
    targetPort: 80
    protocol: TCP
    name: http
  selector:
    app: cdn-config-manager
```

**Deploy to Kubernetes:**

```bash
# Apply manifests
kubectl apply -f namespace.yaml
kubectl apply -f configmap.yaml
kubectl apply -f secret.yaml
kubectl apply -f deployment.yaml
kubectl apply -f service.yaml

# Verify deployment
kubectl get pods -n cdn-config-manager
kubectl get svc -n cdn-config-manager

# View logs
kubectl logs -f deployment/cdn-config-manager -n cdn-config-manager
```

---

## Monitoring Setup

### Prometheus Configuration

**prometheus.yml:**

```yaml
global:
  scrape_interval: 15s

scrape_configs:
  - job_name: 'cdn-config-manager'
    static_configs:
      - targets: ['cdn-config-manager:9091']
    metrics_path: '/metrics'

  - job_name: 'edge-locations'
    static_configs:
      - targets: 
        - 'edge-us-east-1:9091'
        - 'edge-us-west-1:9091'
        - 'edge-eu-west-1:9091'
```

### Grafana Dashboards

**CDN Overview Dashboard:**
- Configuration count by type
- Active deployments
- Deployment success rate
- API request rate
- Error rate

**Edge Performance Dashboard:**
- Cache hit rate by location
- Latency (p50, p95, p99) by location
- Bandwidth utilization
- Error rate by location
- Active configurations per edge

**Deployment Dashboard:**
- Current deployments status
- Deployment duration histogram
- Canary promotion timeline
- Rollback events
- Performance comparison (before/after)

---

## Operational Runbooks

### Runbook 1: Deploy Configuration to Production

**When:** Deploying new CDN configuration

**Steps:**

1. **Create Configuration**
   ```bash
   curl -X POST https://api.example.com/api/v1/configurations \
     -H "Authorization: Bearer $TOKEN" \
     -H "Content-Type: application/json" \
     -d @configuration.json
   ```

2. **Validate Configuration**
   ```bash
   curl -X POST https://api.example.com/api/v1/configurations/{id}/validate \
     -H "Authorization: Bearer $TOKEN"
   ```

3. **Approve Configuration (Admin)**
   ```bash
   curl -X POST https://api.example.com/api/v1/configurations/{id}/approve \
     -H "Authorization: Bearer $ADMIN_TOKEN" \
     -d '{"notes": "Approved for production"}'
   ```

4. **Deploy with Canary Strategy**
   ```bash
   curl -X POST https://api.example.com/api/v1/deployments \
     -H "Authorization: Bearer $TOKEN" \
     -H "Content-Type: application/json" \
     -d '{
       "configurationId": "{id}",
       "strategy": "RegionalCanary",
       "targetRegions": ["North America"],
       "canaryConfig": {
         "initialPercentage": 10,
         "monitorDuration": "PT5M",
         "autoPromote": true
       }
     }'
   ```

5. **Monitor Deployment**
   - Watch Grafana "Deployment Dashboard"
   - Monitor metrics: cache hit rate, latency, errors
   - Wait for canary promotion

6. **Verify Completion**
   ```bash
   curl https://api.example.com/api/v1/deployments/{deploymentId} \
     -H "Authorization: Bearer $TOKEN"
   ```

---

### Runbook 2: Rollback Deployment

**When:** Configuration causes performance issues

**Steps:**

1. **Identify Problem Deployment**
   - Check Grafana dashboards
   - Look for metric degradation
   - Note deployment ID

2. **Immediate Rollback**
   ```bash
   curl -X POST https://api.example.com/api/v1/deployments/{id}/rollback \
     -H "Authorization: Bearer $TOKEN" \
     -d '{"reason": "Cache hit rate dropped to 55%"}'
   ```

3. **Verify Rollback**
   - Check deployment status: should be "RolledBack"
   - Verify metrics return to normal
   - Check edge locations for old configuration

4. **Investigate Root Cause**
   - Review configuration changes
   - Check edge location logs
   - Analyze performance metrics

5. **Document Incident**
   - Create incident report
   - Update configuration with fix
   - Re-validate and re-deploy

---

### Runbook 3: Add New Edge Location

**When:** Expanding CDN to new region

**Steps:**

1. **Register Edge Location**
   ```bash
   curl -X POST https://api.example.com/api/v1/edge-locations \
     -H "Authorization: Bearer $ADMIN_TOKEN" \
     -H "Content-Type: application/json" \
     -d '{
       "locationId": "ap-south-1",
       "name": "Asia Pacific (Mumbai)",
       "region": "Asia Pacific",
       "countryCode": "IN",
       "city": "Mumbai",
       "endpoint": "https://cdn-ap-south-1.example.com",
       "capacity": {
         "maxRequestsPerSec": 100000,
         "maxBandwidthMbps": 10000,
         "cacheStorageGB": 1000
       }
     }'
   ```

2. **Verify Health**
   ```bash
   curl https://api.example.com/api/v1/edge-locations/ap-south-1 \
     -H "Authorization: Bearer $TOKEN"
   ```

3. **Deploy Baseline Configuration**
   - Deploy standard cache rules
   - Deploy security rules
   - Deploy routing rules

4. **Monitor Initial Performance**
   - Watch metrics for 24 hours
   - Verify cache hit rate > 85%
   - Check latency < 50ms (p99)

5. **Update DNS/Load Balancer**
   - Add new edge location to routing
   - Start with 10% traffic
   - Gradually increase to 100%

---

## Troubleshooting

### Issue: Deployment Stuck in "InProgress"

**Symptoms:**
- Deployment status remains "InProgress" for > 30 minutes
- No progress percentage change

**Diagnosis:**
```bash
# Check deployment details
curl https://api.example.com/api/v1/deployments/{id}

# Check edge location health
curl https://api.example.com/api/v1/edge-locations

# Check application logs
kubectl logs -f deployment/cdn-config-manager -n cdn-config-manager
```

**Resolution:**
1. Check edge location connectivity
2. Verify edge locations are healthy
3. Check for network issues
4. Manually pause and resume deployment
5. If stuck > 1 hour, rollback and retry

---

### Issue: High Error Rate After Deployment

**Symptoms:**
- Error rate > 5% after deployment
- Automatic rollback triggered

**Diagnosis:**
```bash
# Get deployment metrics
curl https://api.example.com/api/v1/metrics/deployments/{id}

# Check edge location errors
curl https://api.example.com/api/v1/edge-locations/{id}/metrics
```

**Resolution:**
1. Review configuration changes
2. Check for syntax errors in rules
3. Verify origin server availability
4. Test configuration in staging first
5. Fix configuration and redeploy

---

### Issue: Configuration Not Propagating to Edge

**Symptoms:**
- Deployment shows "Completed"
- Edge location doesn't have new configuration

**Diagnosis:**
```bash
# Check active configurations on edge
curl https://edge-location.example.com/api/active-configurations

# Check deployment edge location status
curl https://api.example.com/api/v1/deployments/{id}/edge-locations
```

**Resolution:**
1. Verify edge location endpoint is correct
2. Check network connectivity to edge
3. Review edge location logs
4. Manually push configuration to edge
5. Verify configuration cache in Redis

---

## Security Hardening

### Production Checklist

- [ ] HTTPS/TLS enabled with valid certificates
- [ ] JWT authentication configured
- [ ] Rate limiting enabled
- [ ] RBAC policies configured
- [ ] Audit logging enabled
- [ ] Database connections encrypted
- [ ] Redis password protected
- [ ] Secrets stored in vault (not config files)
- [ ] CORS configured restrictively
- [ ] Security headers enabled
- [ ] Input validation on all endpoints
- [ ] SQL injection protection verified
- [ ] XSS protection enabled

### SSL/TLS Configuration

```json
{
  "Kestrel": {
    "Endpoints": {
      "Https": {
        "Url": "https://+:443",
        "Certificate": {
          "Path": "/etc/ssl/certs/cdn-config.pfx",
          "Password": "${CERT_PASSWORD}"
        }
      }
    }
  }
}
```

---

**Document Version:** 1.0.0
**Last Updated:** 2025-11-23
**Next Review:** Quarterly
