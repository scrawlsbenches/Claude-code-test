# Deployment & Operations Guide

**Version:** 1.0.0
**Last Updated:** 2025-11-23

---

## Table of Contents

1. [Prerequisites](#prerequisites)
2. [Local Development Setup](#local-development-setup)
3. [Docker Deployment](#docker-deployment)
4. [Kubernetes Deployment](#kubernetes-deployment)
5. [Model Storage Configuration](#model-storage-configuration)
6. [Monitoring Setup](#monitoring-setup)
7. [Operational Runbooks](#operational-runbooks)

---

## Prerequisites

### Required Infrastructure

**Mandatory:**
- **.NET 8.0 Runtime** - Application runtime
- **PostgreSQL 15+** - Model registry, deployment metadata
- **MinIO or S3** - Model artifact storage
- **Redis 7+** - Distributed locks, caching

**Optional:**
- **Jaeger** - Distributed tracing (recommended for production)
- **Prometheus** - Metrics collection
- **Grafana** - Metrics visualization
- **GPU Nodes** - For deep learning models (CUDA 11.8+)

### System Requirements

**Model Server:**
- CPU: 4+ cores (8+ for GPU inference)
- Memory: 8 GB+ RAM (16 GB+ for large models)
- Disk: 100 GB+ SSD (for model artifacts)
- GPU: NVIDIA GPU with 8 GB+ VRAM (for deep learning)
- Network: 1 Gbps

**MinIO/S3:**
- CPU: 4+ cores
- Memory: 8 GB+ RAM
- Disk: 500 GB+ SSD (depends on model count/size)

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

# Start PostgreSQL, Redis, MinIO, Jaeger
docker-compose -f docker-compose.ml-deployment.yml up -d

# Verify services
docker-compose ps
```

**docker-compose.ml-deployment.yml:**

```yaml
version: '3.8'

services:
  postgres:
    image: postgres:15-alpine
    ports:
      - "5432:5432"
    environment:
      POSTGRES_DB: ml_deployment
      POSTGRES_USER: ml_user
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

  minio:
    image: minio/minio:latest
    ports:
      - "9000:9000"
      - "9001:9001"
    environment:
      MINIO_ROOT_USER: ${MINIO_USER}
      MINIO_ROOT_PASSWORD: ${MINIO_PASSWORD}
    volumes:
      - minio-data:/data
    command: server /data --console-address ":9001"

  jaeger:
    image: jaegertracing/all-in-one:1.52
    ports:
      - "16686:16686"  # Jaeger UI
      - "14268:14268"  # Collector
    environment:
      COLLECTOR_ZIPKIN_HOST_PORT: ":9411"

volumes:
  postgres-data:
  redis-data:
  minio-data:
```

**Step 3: Configure Application**

```bash
# Create appsettings.Development.json
cat > src/HotSwap.MLDeployment.Api/appsettings.Development.json <<EOF
{
  "ConnectionStrings": {
    "PostgreSQL": "Host=localhost;Database=ml_deployment;Username=ml_user;Password=dev_password",
    "Redis": "localhost:6379"
  },
  "ModelStorage": {
    "Type": "MinIO",
    "Endpoint": "localhost:9000",
    "AccessKey": "${MINIO_USER}",
    "SecretKey": "${MINIO_PASSWORD}",
    "BucketName": "ml-models",
    "UseSSL": false
  },
  "MLDeployment": {
    "DefaultStrategy": "Canary",
    "MaxConcurrentDeployments": 5,
    "ModelWarmupTimeout": "PT2M"
  },
  "OpenTelemetry": {
    "JaegerEndpoint": "http://localhost:14268/api/traces",
    "SamplingRate": 1.0
  }
}
EOF
```

**Step 4: Initialize Database**

```bash
# Run database migrations
dotnet ef database update --project src/HotSwap.MLDeployment.Infrastructure

# Or use SQL script
psql -h localhost -U ml_user -d ml_deployment -f db/migrations/001_ml_schema.sql
```

**db/migrations/001_ml_schema.sql:**

```sql
-- Models table
CREATE TABLE models (
    model_id VARCHAR(255) PRIMARY KEY,
    name VARCHAR(255) NOT NULL UNIQUE,
    description TEXT,
    framework VARCHAR(50) NOT NULL,
    type VARCHAR(50) NOT NULL,
    owner VARCHAR(255) NOT NULL,
    active_version VARCHAR(255),
    is_archived BOOLEAN DEFAULT FALSE,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP NOT NULL DEFAULT NOW()
);

-- Model versions table
CREATE TABLE model_versions (
    version_id VARCHAR(255) PRIMARY KEY,
    model_id VARCHAR(255) NOT NULL REFERENCES models(model_id),
    version VARCHAR(50) NOT NULL,
    artifact_path VARCHAR(512) NOT NULL,
    checksum VARCHAR(64) NOT NULL,
    artifact_size_bytes BIGINT NOT NULL,
    input_schema TEXT NOT NULL,
    output_schema TEXT NOT NULL,
    status VARCHAR(50) NOT NULL DEFAULT 'Registered',
    approved_by VARCHAR(255),
    approved_at TIMESTAMP,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    is_deprecated BOOLEAN DEFAULT FALSE,
    UNIQUE(model_id, version)
);

-- Deployments table
CREATE TABLE deployments (
    deployment_id VARCHAR(255) PRIMARY KEY,
    version_id VARCHAR(255) NOT NULL REFERENCES model_versions(version_id),
    model_id VARCHAR(255) NOT NULL REFERENCES models(model_id),
    strategy VARCHAR(50) NOT NULL,
    environment VARCHAR(50) NOT NULL,
    strategy_config TEXT NOT NULL,
    status VARCHAR(50) NOT NULL DEFAULT 'Pending',
    progress_percentage INT DEFAULT 0,
    traffic_percentage INT DEFAULT 0,
    initiated_by VARCHAR(255) NOT NULL,
    started_at TIMESTAMP NOT NULL DEFAULT NOW(),
    completed_at TIMESTAMP,
    is_rolled_back BOOLEAN DEFAULT FALSE,
    rolled_back_at TIMESTAMP,
    error_message TEXT
);

CREATE INDEX idx_deployments_status ON deployments(status);
CREATE INDEX idx_deployments_environment ON deployments(environment);

-- Inference requests table (for metrics)
CREATE TABLE inference_requests (
    request_id VARCHAR(255) PRIMARY KEY,
    model_name VARCHAR(255) NOT NULL,
    version VARCHAR(50),
    features TEXT NOT NULL,
    prediction TEXT,
    confidence DOUBLE PRECISION,
    latency_ms DOUBLE PRECISION,
    status VARCHAR(50) NOT NULL,
    requested_at TIMESTAMP NOT NULL DEFAULT NOW(),
    completed_at TIMESTAMP
);

CREATE INDEX idx_inference_model ON inference_requests(model_name, requested_at DESC);
```

**Step 5: Create MinIO Bucket**

```bash
# Using MinIO client (mc)
mc alias set local http://localhost:9000 ${MINIO_USER} ${MINIO_PASSWORD}
mc mb local/ml-models
```

**Step 6: Run Application**

```bash
# Build and run
dotnet build
dotnet run --project src/HotSwap.MLDeployment.Api

# Application starts on:
# HTTP:  http://localhost:5000
# HTTPS: https://localhost:5001
# Swagger: http://localhost:5000/swagger
```

---

## Docker Deployment

### Build Docker Image

**Dockerfile:**

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/HotSwap.MLDeployment.Api/HotSwap.MLDeployment.Api.csproj", "Api/"]
COPY ["src/HotSwap.MLDeployment.Orchestrator/HotSwap.MLDeployment.Orchestrator.csproj", "Orchestrator/"]
COPY ["src/HotSwap.MLDeployment.Infrastructure/HotSwap.MLDeployment.Infrastructure.csproj", "Infrastructure/"]
COPY ["src/HotSwap.MLDeployment.Domain/HotSwap.MLDeployment.Domain.csproj", "Domain/"]
RUN dotnet restore "Api/HotSwap.MLDeployment.Api.csproj"

COPY src/ .
RUN dotnet publish "Api/HotSwap.MLDeployment.Api.csproj" -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

RUN useradd -m -u 1000 mluser && chown -R mluser:mluser /app
USER mluser

COPY --from=build /app/publish .

EXPOSE 5000
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
  CMD curl -f http://localhost:5000/health || exit 1

ENTRYPOINT ["dotnet", "HotSwap.MLDeployment.Api.dll"]
```

---

## Kubernetes Deployment

### Model Server Deployment

**ml-deployment.yaml:**

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: ml-deployment-api
  namespace: ml-system
spec:
  replicas: 3
  selector:
    matchLabels:
      app: ml-deployment-api
  template:
    metadata:
      labels:
        app: ml-deployment-api
    spec:
      containers:
      - name: ml-api
        image: your-registry/ml-deployment:1.0.0
        ports:
        - containerPort: 5000
          name: http
        env:
        - name: ASPNETCORE_ENVIRONMENT
          value: "Production"
        - name: ConnectionStrings__PostgreSQL
          valueFrom:
            secretKeyRef:
              name: ml-secrets
              key: postgres-connection
        - name: ModelStorage__AccessKey
          valueFrom:
            secretKeyRef:
              name: ml-secrets
              key: minio-access-key
        resources:
          limits:
            memory: "8Gi"
            cpu: "4"
          requests:
            memory: "4Gi"
            cpu: "2"
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
---
apiVersion: v1
kind: Service
metadata:
  name: ml-deployment-api
  namespace: ml-system
spec:
  type: LoadBalancer
  ports:
  - port: 80
    targetPort: 5000
  selector:
    app: ml-deployment-api
```

---

## Operational Runbooks

### Runbook 1: Deploy New Model Version

**Scenario:** Deploy new fraud detection model v2.0

**Steps:**

```bash
# 1. Upload model artifact to MinIO
mc cp fraud-detection-v2.0.tar.gz local/ml-models/fraud-detection/v2.0/

# 2. Register model version
curl -X POST http://localhost:5000/api/v1/models \
  -H "Authorization: Bearer $TOKEN" \
  -d '{
    "name": "fraud-detection",
    "version": "2.0.0",
    "framework": "TensorFlow",
    "artifactPath": "s3://ml-models/fraud-detection/v2.0/fraud-detection-v2.0.tar.gz"
  }'

# 3. Deploy with canary strategy
curl -X POST http://localhost:5000/api/v1/deployments \
  -H "Authorization: Bearer $TOKEN" \
  -d '{
    "modelId": "model-123",
    "strategy": "Canary",
    "environment": "Production",
    "strategyConfig": {"canaryPercentage": 10}
  }'

# 4. Monitor deployment
watch curl http://localhost:5000/api/v1/deployments/{deployment-id}

# 5. Rollback if needed
curl -X POST http://localhost:5000/api/v1/deployments/{deployment-id}/rollback
```

---

### Runbook 2: Handle Accuracy Degradation

**Scenario:** Model accuracy drops below threshold

**Detection:**
```bash
# Check Grafana alert or query metrics
curl http://localhost:5000/api/v1/models/model-123/metrics
```

**Response:**
```bash
# 1. Verify accuracy drop
# 2. Check for data drift
# 3. Rollback to previous version
# 4. Investigate root cause
# 5. Retrain model if needed
```

---

**Last Updated:** 2025-11-23
**Version:** 1.0.0
**Deployment Status:** Design Phase
