# Deployment & Operations Guide

**Version:** 1.0.0
**Last Updated:** 2025-11-23

---

## Table of Contents

1. [Prerequisites](#prerequisites)
2. [Local Development Setup](#local-development-setup)
3. [Docker Deployment](#docker-deployment)
4. [Kubernetes Deployment](#kubernetes-deployment)
5. [Runtime Image Building](#runtime-image-building)
6. [Configuration](#configuration)
7. [Monitoring Setup](#monitoring-setup)
8. [Operational Runbooks](#operational-runbooks)
9. [Troubleshooting](#troubleshooting)

---

## Prerequisites

### Required Infrastructure

**Mandatory:**
- **.NET 8.0 Runtime** - Application runtime
- **Docker / containerd** - Runtime containers for functions
- **Redis 7+** - State management, caching
- **PostgreSQL 15+** - Function metadata, deployments
- **MinIO / S3** - Function code storage

**Optional:**
- **Jaeger** - Distributed tracing (recommended for production)
- **Prometheus** - Metrics collection
- **Grafana** - Metrics visualization

### System Requirements

**Control Plane:**
- CPU: 2+ cores
- Memory: 4 GB+ RAM
- Disk: 20 GB+ SSD

**Runner Nodes:**
- CPU: 8+ cores (for parallel function execution)
- Memory: 16 GB+ RAM
- Disk: 50 GB+ SSD (for code caching)
- Docker Engine 24+

**MinIO / S3:**
- Disk: 100 GB+ for function code packages

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

# Install Docker
curl -fsSL https://get.docker.com | sh
sudo usermod -aG docker $USER
```

**Step 2: Start Infrastructure (Docker Compose)**

```bash
# Clone repository
git clone https://github.com/scrawlsbenches/Claude-code-test.git
cd Claude-code-test

# Start Redis, PostgreSQL, MinIO, Jaeger
docker-compose -f docker-compose.serverless.yml up -d

# Verify services
docker-compose ps
```

**docker-compose.serverless.yml:**

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
      POSTGRES_DB: serverless
      POSTGRES_USER: serverless_user
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
    volumes:
      - postgres-data:/var/lib/postgresql/data

  minio:
    image: minio/minio:latest
    ports:
      - "9000:9000"
      - "9001:9001"
    environment:
      MINIO_ROOT_USER: minioadmin
      MINIO_ROOT_PASSWORD: ${MINIO_PASSWORD}
    volumes:
      - minio-data:/data
    command: server /data --console-address ":9001"

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

volumes:
  redis-data:
  postgres-data:
  minio-data:
```

**Step 3: Initialize Database**

```bash
# Run database migrations
dotnet ef database update --project src/HotSwap.Serverless.Infrastructure

# Or use SQL script
psql -h localhost -U serverless_user -d serverless -f db/migrations/001_initial_schema.sql
```

**db/migrations/001_initial_schema.sql:**

```sql
-- Functions table
CREATE TABLE functions (
    name VARCHAR(64) PRIMARY KEY,
    description TEXT,
    runtime VARCHAR(50) NOT NULL,
    handler VARCHAR(255) NOT NULL,
    memory_size INT NOT NULL DEFAULT 256,
    timeout INT NOT NULL DEFAULT 30,
    environment JSONB,
    layers TEXT[],
    vpc_config JSONB,
    tags JSONB,
    published_version VARCHAR(50),
    owner_id VARCHAR(255) NOT NULL,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP NOT NULL DEFAULT NOW(),
    total_invocations BIGINT NOT NULL DEFAULT 0,
    last_invoked_at TIMESTAMP
);

-- Function versions table
CREATE TABLE function_versions (
    function_name VARCHAR(64) NOT NULL,
    version INT NOT NULL,
    code_sha256 VARCHAR(64) NOT NULL,
    code_size BIGINT NOT NULL,
    code_location VARCHAR(512) NOT NULL,
    runtime VARCHAR(50) NOT NULL,
    handler VARCHAR(255) NOT NULL,
    memory_size INT NOT NULL,
    timeout INT NOT NULL,
    environment JSONB,
    layers TEXT[],
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    created_by VARCHAR(255) NOT NULL,
    description TEXT,
    invocation_count BIGINT NOT NULL DEFAULT 0,
    status VARCHAR(50) NOT NULL DEFAULT 'Active',
    PRIMARY KEY (function_name, version),
    FOREIGN KEY (function_name) REFERENCES functions(name) ON DELETE CASCADE
);

-- Function aliases table
CREATE TABLE function_aliases (
    function_name VARCHAR(64) NOT NULL,
    alias_name VARCHAR(64) NOT NULL,
    version INT NOT NULL,
    routing_config JSONB,
    description TEXT,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP NOT NULL DEFAULT NOW(),
    PRIMARY KEY (function_name, alias_name),
    FOREIGN KEY (function_name) REFERENCES functions(name) ON DELETE CASCADE
);

-- Deployments table
CREATE TABLE deployments (
    deployment_id VARCHAR(64) PRIMARY KEY,
    function_name VARCHAR(64) NOT NULL,
    target_version INT NOT NULL,
    source_version INT,
    strategy VARCHAR(50) NOT NULL,
    config JSONB,
    status VARCHAR(50) NOT NULL DEFAULT 'Pending',
    progress DECIMAL(5,2) NOT NULL DEFAULT 0,
    current_phase VARCHAR(100),
    started_at TIMESTAMP NOT NULL DEFAULT NOW(),
    completed_at TIMESTAMP,
    deployed_by VARCHAR(255) NOT NULL,
    error_message TEXT,
    rollback_reason TEXT,
    metrics JSONB,
    FOREIGN KEY (function_name) REFERENCES functions(name) ON DELETE CASCADE
);

-- Runner nodes table
CREATE TABLE runner_nodes (
    node_id VARCHAR(64) PRIMARY KEY,
    hostname VARCHAR(255) NOT NULL,
    port INT NOT NULL DEFAULT 8080,
    health JSONB,
    resources JSONB,
    active_containers JSONB,
    last_heartbeat TIMESTAMP NOT NULL DEFAULT NOW(),
    started_at TIMESTAMP NOT NULL DEFAULT NOW(),
    metrics JSONB
);

-- Triggers table
CREATE TABLE triggers (
    trigger_id VARCHAR(64) PRIMARY KEY,
    function_name VARCHAR(64) NOT NULL,
    target_version VARCHAR(50) NOT NULL DEFAULT '$LATEST',
    type VARCHAR(50) NOT NULL,
    is_enabled BOOLEAN NOT NULL DEFAULT TRUE,
    config JSONB,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    last_executed_at TIMESTAMP,
    execution_count BIGINT NOT NULL DEFAULT 0,
    failure_count BIGINT NOT NULL DEFAULT 0,
    FOREIGN KEY (function_name) REFERENCES functions(name) ON DELETE CASCADE
);

-- Indexes
CREATE INDEX idx_functions_owner ON functions(owner_id);
CREATE INDEX idx_functions_runtime ON functions(runtime);
CREATE INDEX idx_versions_created_at ON function_versions(created_at DESC);
CREATE INDEX idx_deployments_function ON deployments(function_name);
CREATE INDEX idx_deployments_status ON deployments(status);
CREATE INDEX idx_triggers_function ON triggers(function_name);
CREATE INDEX idx_triggers_type ON triggers(type);
CREATE INDEX idx_runners_heartbeat ON runner_nodes(last_heartbeat DESC);
```

**Step 4: Initialize MinIO Buckets**

```bash
# Install MinIO client
wget https://dl.min.io/client/mc/release/linux-amd64/mc
chmod +x mc
sudo mv mc /usr/local/bin/

# Configure MinIO
mc alias set local http://localhost:9000 minioadmin ${MINIO_PASSWORD}

# Create function code bucket
mc mb local/function-code
mc policy set download local/function-code
```

**Step 5: Configure Application**

```bash
# Create appsettings.Development.json
cat > src/HotSwap.Serverless.Api/appsettings.Development.json <<EOF
{
  "ConnectionStrings": {
    "Redis": "localhost:6379",
    "PostgreSQL": "Host=localhost;Database=serverless;Username=serverless_user;Password=dev_password"
  },
  "ObjectStorage": {
    "Provider": "MinIO",
    "Endpoint": "localhost:9000",
    "AccessKey": "minioadmin",
    "SecretKey": "minioadmin123",
    "BucketName": "function-code",
    "UseSSL": false
  },
  "Docker": {
    "SocketPath": "unix:///var/run/docker.sock",
    "KeepContainersWarm": true,
    "ContainerIdleTimeout": "PT10M"
  },
  "OpenTelemetry": {
    "JaegerEndpoint": "http://localhost:14268/api/traces",
    "SamplingRate": 1.0
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "HotSwap.Serverless": "Debug"
    }
  }
}
EOF
```

**Step 6: Build Runtime Images**

```bash
# Build Node.js runtime images
cd runtimes/nodejs
docker build -t hotswap/runtime-node:16 -f Dockerfile.node16 .
docker build -t hotswap/runtime-node:18 -f Dockerfile.node18 .
docker build -t hotswap/runtime-node:20 -f Dockerfile.node20 .

# Build Python runtime images
cd ../python
docker build -t hotswap/runtime-python:3.9 -f Dockerfile.python39 .
docker build -t hotswap/runtime-python:3.10 -f Dockerfile.python310 .
docker build -t hotswap/runtime-python:3.11 -f Dockerfile.python311 .

# Build .NET runtime images
cd ../dotnet
docker build -t hotswap/runtime-dotnet:8 -f Dockerfile.dotnet8 .
```

**Step 7: Run Application**

```bash
# Build and run
dotnet build
dotnet run --project src/HotSwap.Serverless.Api

# Application starts on:
# HTTP:  http://localhost:5000
# HTTPS: https://localhost:5001
# Swagger: http://localhost:5000/swagger
```

**Step 8: Verify Installation**

```bash
# Health check
curl http://localhost:5000/health

# Create test function
curl -X POST http://localhost:5000/api/v1/functions \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $(get_jwt_token)" \
  -d '{
    "name": "hello-world",
    "runtime": "Node18",
    "handler": "index.handler",
    "memorySize": 256,
    "timeout": 30
  }'
```

---

## Docker Deployment

### Build Control Plane Image

**Dockerfile:**

```dockerfile
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/HotSwap.Serverless.Api/HotSwap.Serverless.Api.csproj", "Api/"]
COPY ["src/HotSwap.Serverless.Orchestrator/HotSwap.Serverless.Orchestrator.csproj", "Orchestrator/"]
COPY ["src/HotSwap.Serverless.Infrastructure/HotSwap.Serverless.Infrastructure.csproj", "Infrastructure/"]
COPY ["src/HotSwap.Serverless.Domain/HotSwap.Serverless.Domain.csproj", "Domain/"]
RUN dotnet restore "Api/HotSwap.Serverless.Api.csproj"

COPY src/ .
RUN dotnet publish "Api/HotSwap.Serverless.Api.csproj" -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

# Install Docker CLI (for container management)
RUN apt-get update && \
    apt-get install -y docker.io && \
    rm -rf /var/lib/apt/lists/*

# Create non-root user
RUN useradd -m -u 1000 serverless && \
    chown -R serverless:serverless /app

USER serverless

COPY --from=build /app/publish .

EXPOSE 5000 8080
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
  CMD curl -f http://localhost:5000/health || exit 1

ENTRYPOINT ["dotnet", "HotSwap.Serverless.Api.dll"]
```

**Build and Push:**

```bash
# Build image
docker build -t your-registry/serverless-platform:1.0.0 .

# Tag for registry
docker tag your-registry/serverless-platform:1.0.0 your-registry/serverless-platform:latest

# Push to registry
docker push your-registry/serverless-platform:1.0.0
docker push your-registry/serverless-platform:latest
```

### Docker Compose Production Stack

**docker-compose.production.yml:**

```yaml
version: '3.8'

services:
  control-plane:
    image: your-registry/serverless-platform:1.0.0
    ports:
      - "5000:5000"
      - "8080:8080"
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ASPNETCORE_URLS: http://+:5000
      ConnectionStrings__Redis: redis:6379
      ConnectionStrings__PostgreSQL: "Host=postgres;Database=serverless;Username=serverless_user;Password=${POSTGRES_PASSWORD}"
      ObjectStorage__Endpoint: "minio:9000"
      ObjectStorage__AccessKey: "minioadmin"
      ObjectStorage__SecretKey: "${MINIO_PASSWORD}"
      Docker__SocketPath: "unix:///var/run/docker.sock"
    volumes:
      - /var/run/docker.sock:/var/run/docker.sock
    depends_on:
      - redis
      - postgres
      - minio
    networks:
      - serverless-network
    deploy:
      replicas: 2
      resources:
        limits:
          cpus: '2'
          memory: 4G

  runner-node:
    image: your-registry/serverless-runner:1.0.0
    environment:
      RUNNER_ID: "${HOSTNAME}"
      CONTROL_PLANE_URL: "http://control-plane:8080"
    volumes:
      - /var/run/docker.sock:/var/run/docker.sock
    networks:
      - serverless-network
    deploy:
      replicas: 3
      resources:
        limits:
          cpus: '8'
          memory: 16G

  redis:
    image: redis:7-alpine
    command: redis-server --appendonly yes --maxmemory 4gb
    volumes:
      - redis-data:/data
    networks:
      - serverless-network

  postgres:
    image: postgres:15-alpine
    environment:
      POSTGRES_DB: serverless
      POSTGRES_USER: serverless_user
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
    volumes:
      - postgres-data:/var/lib/postgresql/data
    networks:
      - serverless-network

  minio:
    image: minio/minio:latest
    environment:
      MINIO_ROOT_USER: minioadmin
      MINIO_ROOT_PASSWORD: ${MINIO_PASSWORD}
    volumes:
      - minio-data:/data
    command: server /data --console-address ":9001"
    networks:
      - serverless-network

volumes:
  redis-data:
  postgres-data:
  minio-data:

networks:
  serverless-network:
    driver: bridge
```

---

## Kubernetes Deployment

### Namespace and Secrets

```bash
# Create namespace
kubectl create namespace serverless-platform

# Create secrets
kubectl create secret generic serverless-secrets \
  --from-literal=redis-password=$(openssl rand -base64 32) \
  --from-literal=postgres-password=$(openssl rand -base64 32) \
  --from-literal=minio-password=$(openssl rand -base64 32) \
  --from-literal=jwt-secret=$(openssl rand -base64 64) \
  -n serverless-platform
```

### Control Plane Deployment

**control-plane-deployment.yaml:**

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: control-plane
  namespace: serverless-platform
spec:
  replicas: 2
  selector:
    matchLabels:
      app: control-plane
  template:
    metadata:
      labels:
        app: control-plane
    spec:
      containers:
      - name: control-plane
        image: your-registry/serverless-platform:1.0.0
        ports:
        - containerPort: 5000
          name: http
        - containerPort: 8080
          name: runner-api
        env:
        - name: ASPNETCORE_ENVIRONMENT
          value: "Production"
        - name: ConnectionStrings__Redis
          value: "redis:6379,password=$(REDIS_PASSWORD)"
        - name: ConnectionStrings__PostgreSQL
          value: "Host=postgres;Database=serverless;Username=serverless_user;Password=$(POSTGRES_PASSWORD)"
        - name: REDIS_PASSWORD
          valueFrom:
            secretKeyRef:
              name: serverless-secrets
              key: redis-password
        - name: POSTGRES_PASSWORD
          valueFrom:
            secretKeyRef:
              name: serverless-secrets
              key: postgres-password
        volumeMounts:
        - name: docker-sock
          mountPath: /var/run/docker.sock
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
      - name: docker-sock
        hostPath:
          path: /var/run/docker.sock
---
apiVersion: v1
kind: Service
metadata:
  name: control-plane
  namespace: serverless-platform
spec:
  type: LoadBalancer
  ports:
  - port: 80
    targetPort: 5000
    name: http
  - port: 8080
    targetPort: 8080
    name: runner-api
  selector:
    app: control-plane
```

### Runner Node DaemonSet

**runner-daemonset.yaml:**

```yaml
apiVersion: apps/v1
kind: DaemonSet
metadata:
  name: runner-node
  namespace: serverless-platform
spec:
  selector:
    matchLabels:
      app: runner-node
  template:
    metadata:
      labels:
        app: runner-node
    spec:
      containers:
      - name: runner
        image: your-registry/serverless-runner:1.0.0
        env:
        - name: RUNNER_ID
          valueFrom:
            fieldRef:
              fieldPath: spec.nodeName
        - name: CONTROL_PLANE_URL
          value: "http://control-plane:8080"
        volumeMounts:
        - name: docker-sock
          mountPath: /var/run/docker.sock
        resources:
          limits:
            memory: "16Gi"
            cpu: "8"
          requests:
            memory: "8Gi"
            cpu: "4"
      volumes:
      - name: docker-sock
        hostPath:
          path: /var/run/docker.sock
```

---

## Runtime Image Building

### Node.js Runtime

**runtimes/nodejs/Dockerfile.node18:**

```dockerfile
FROM node:18-alpine

# Install bootstrap wrapper
COPY bootstrap-nodejs.js /runtime/bootstrap.js

# Set working directory for function code
WORKDIR /function

# Runtime entrypoint
ENTRYPOINT ["node", "/runtime/bootstrap.js"]
```

**runtimes/nodejs/bootstrap-nodejs.js:**

```javascript
const fs = require('fs');
const path = require('path');

// Load function handler
const handlerParts = process.env.HANDLER.split('.');
const handlerFile = handlerParts[0];
const handlerFunction = handlerParts[1];

const handler = require(`/function/${handlerFile}`)[handlerFunction];

// HTTP server for invocations
const http = require('http');

const server = http.createServer(async (req, res) => {
  if (req.method === 'POST' && req.url === '/invoke') {
    let body = '';
    req.on('data', chunk => body += chunk);
    req.on('end', async () => {
      try {
        const event = JSON.parse(body);
        const context = {
          requestId: req.headers['x-request-id'],
          functionName: process.env.FUNCTION_NAME,
          memoryLimitInMB: process.env.MEMORY_SIZE
        };
        
        const result = await handler(event, context);
        res.writeHead(200, { 'Content-Type': 'application/json' });
        res.end(JSON.stringify(result));
      } catch (error) {
        res.writeHead(500, { 'Content-Type': 'application/json' });
        res.end(JSON.stringify({ errorMessage: error.message }));
      }
    });
  } else {
    res.writeHead(404);
    res.end();
  }
});

server.listen(8080, () => {
  console.log('Runtime ready on port 8080');
});
```

### Python Runtime

**runtimes/python/Dockerfile.python311:**

```dockerfile
FROM python:3.11-slim

# Install bootstrap wrapper
COPY bootstrap-python.py /runtime/bootstrap.py

# Set working directory for function code
WORKDIR /function

# Runtime entrypoint
ENTRYPOINT ["python", "/runtime/bootstrap.py"]
```

**runtimes/python/bootstrap-python.py:**

```python
import json
import importlib.util
import os
import sys
from http.server import HTTPServer, BaseHTTPRequestHandler

# Load function handler
handler_parts = os.environ['HANDLER'].split('.')
handler_file = handler_parts[0]
handler_function = handler_parts[1]

spec = importlib.util.spec_from_file_location(handler_file, f'/function/{handler_file}.py')
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)
handler = getattr(module, handler_function)

class InvocationHandler(BaseHTTPRequestHandler):
    def do_POST(self):
        if self.path == '/invoke':
            content_length = int(self.headers['Content-Length'])
            body = self.rfile.read(content_length)
            
            try:
                event = json.loads(body)
                context = {
                    'request_id': self.headers.get('X-Request-Id'),
                    'function_name': os.environ.get('FUNCTION_NAME'),
                    'memory_limit_in_mb': os.environ.get('MEMORY_SIZE')
                }
                
                result = handler(event, context)
                
                self.send_response(200)
                self.send_header('Content-Type', 'application/json')
                self.end_headers()
                self.wfile.write(json.dumps(result).encode())
            except Exception as e:
                self.send_response(500)
                self.send_header('Content-Type', 'application/json')
                self.end_headers()
                error_response = {'errorMessage': str(e)}
                self.wfile.write(json.dumps(error_response).encode())
        else:
            self.send_response(404)
            self.end_headers()

server = HTTPServer(('0.0.0.0', 8080), InvocationHandler)
print('Runtime ready on port 8080')
server.serve_forever()
```

---

## Configuration

### Environment Variables

```bash
# Application
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://+:5000

# Database
ConnectionStrings__Redis=redis:6379,password=secure_password
ConnectionStrings__PostgreSQL=Host=postgres;Database=serverless;Username=serverless_user;Password=secure_password

# Object Storage
ObjectStorage__Provider=MinIO
ObjectStorage__Endpoint=minio:9000
ObjectStorage__AccessKey=minioadmin
ObjectStorage__SecretKey=secure_password
ObjectStorage__BucketName=function-code

# Docker
Docker__SocketPath=unix:///var/run/docker.sock
Docker__KeepContainersWarm=true
Docker__ContainerIdleTimeout=PT10M

# OpenTelemetry
OpenTelemetry__JaegerEndpoint=http://jaeger:14268/api/traces
OpenTelemetry__SamplingRate=0.1

# JWT
JWT__Secret=your_jwt_secret_key_here
JWT__Issuer=https://serverless.example.com
JWT__Audience=serverless-api
```

---

## Monitoring Setup

### Prometheus Configuration

**prometheus.yml:**

```yaml
global:
  scrape_interval: 15s
  evaluation_interval: 15s

scrape_configs:
  - job_name: 'serverless-platform'
    static_configs:
      - targets: ['control-plane:5000']
    metrics_path: /metrics
```

### Grafana Dashboard

Import dashboard JSON from `grafana/serverless-dashboard.json`:

**Key Panels:**
- Function invocations per second
- Cold start vs warm invocations
- P50/P95/P99 latency
- Error rate
- Active containers
- Runner node health

---

## Operational Runbooks

### Runbook 1: Scale Runner Nodes

**Scenario:** Need to handle increased load

**Steps:**
```bash
# Kubernetes
kubectl scale daemonset runner-node --replicas=5 -n serverless-platform

# Docker Compose
docker-compose -f docker-compose.production.yml up -d --scale runner-node=5
```

### Runbook 2: Rollback Deployment

**Scenario:** Deployed function has issues

**Steps:**
```bash
# Manual rollback via API
curl -X POST http://localhost:5000/api/v1/deployments/{deployment-id}/rollback \
  -H "Authorization: Bearer $TOKEN" \
  -d '{"reason": "High error rate"}'

# Or update alias directly
curl -X PUT http://localhost:5000/api/v1/functions/{name}/aliases/production \
  -H "Authorization: Bearer $TOKEN" \
  -d '{"version": 5}'
```

---

## Troubleshooting

### Issue: Function Invocations Failing

**Symptoms:**
- 500 errors on invocations
- "Container not found" errors

**Solutions:**
1. Check runner node health
2. Verify Docker daemon running
3. Check function code exists in MinIO
4. Review function logs

### Issue: High Cold Start Times

**Symptoms:**
- Cold starts > 1 second
- Poor user experience

**Solutions:**
1. Enable keep-warm policy
2. Increase pre-provisioned concurrency
3. Optimize runtime images (use Alpine)
4. Use smaller dependencies

---

**Last Updated:** 2025-11-23
**Version:** 1.0.0
**Deployment Status:** Design Specification
