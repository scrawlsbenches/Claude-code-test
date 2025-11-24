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
6. [Operations](#operations)
7. [Troubleshooting](#troubleshooting)
8. [Monitoring](#monitoring)

---

## Prerequisites

### Required Infrastructure

**Mandatory:**
- **.NET 8.0 Runtime** - Application runtime
- **PostgreSQL 15+** - Rule set and deployment storage
- **Redis 7+** (optional) - Distributed locks for exactly-once deployment

**Optional:**
- **Jaeger** - Distributed tracing
- **Prometheus** - Metrics collection
- **Grafana** - Metrics visualization

### Cloud Provider Prerequisites

**AWS:**
- AWS account with appropriate permissions
- IAM role/user with EC2 and VPC permissions
- AWS SDK credentials configured

**Azure:**
- Azure subscription
- Service principal with Network Contributor role
- Azure SDK credentials configured

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

# Start PostgreSQL and Jaeger
docker-compose -f docker-compose.firewall.yml up -d

# Verify services
docker-compose ps
```

**docker-compose.firewall.yml:**

```yaml
version: '3.8'

services:
  postgres:
    image: postgres:15-alpine
    ports:
      - "5432:5432"
    environment:
      POSTGRES_DB: firewall
      POSTGRES_USER: firewall_user
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

  prometheus:
    image: prom/prometheus:latest
    ports:
      - "9090:9090"
    volumes:
      - ./prometheus.yml:/etc/prometheus/prometheus.yml
      - prometheus-data:/prometheus

  grafana:
    image: grafana/grafana:latest
    ports:
      - "3000:3000"
    environment:
      GF_SECURITY_ADMIN_PASSWORD: ${GRAFANA_PASSWORD}
    volumes:
      - grafana-data:/var/lib/grafana

volumes:
  postgres-data:
  prometheus-data:
  grafana-data:
```

**Step 3: Configure Application**

```bash
# Create appsettings.Development.json
cat > src/HotSwap.Firewall.Api/appsettings.Development.json <<EOF
{
  "ConnectionStrings": {
    "PostgreSQL": "Host=localhost;Database=firewall;Username=firewall_user;Password=dev_password"
  },
  "Firewall": {
    "DefaultDeploymentStrategy": "Direct",
    "ValidationTimeoutSeconds": 30,
    "RollbackTimeoutSeconds": 10
  },
  "AWS": {
    "Region": "us-east-1",
    "AccessKeyId": "${AWS_ACCESS_KEY_ID}",
    "SecretAccessKey": "${AWS_SECRET_ACCESS_KEY}"
  },
  "Azure": {
    "TenantId": "${AZURE_TENANT_ID}",
    "ClientId": "${AZURE_CLIENT_ID}",
    "ClientSecret": "${AZURE_CLIENT_SECRET}",
    "SubscriptionId": "${AZURE_SUBSCRIPTION_ID}"
  },
  "OpenTelemetry": {
    "JaegerEndpoint": "http://localhost:14268/api/traces",
    "SamplingRate": 1.0
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "HotSwap.Firewall": "Debug"
    }
  }
}
EOF
```

**Step 4: Initialize Database**

```bash
# Run database migrations
dotnet ef database update --project src/HotSwap.Firewall.Infrastructure

# Or use SQL script
psql -h localhost -U firewall_user -d firewall -f db/migrations/001_firewall_schema.sql
```

**db/migrations/001_firewall_schema.sql:**

```sql
-- Rule sets table
CREATE TABLE rulesets (
    name VARCHAR(255) PRIMARY KEY,
    description TEXT,
    version VARCHAR(50) NOT NULL,
    environment VARCHAR(50) NOT NULL,
    target_type VARCHAR(50) NOT NULL,
    status VARCHAR(50) NOT NULL DEFAULT 'Draft',
    metadata JSONB,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP NOT NULL DEFAULT NOW(),
    created_by VARCHAR(255)
);

-- Firewall rules table
CREATE TABLE firewall_rules (
    rule_id VARCHAR(255) PRIMARY KEY,
    ruleset_name VARCHAR(255) NOT NULL REFERENCES rulesets(name) ON DELETE CASCADE,
    name VARCHAR(255) NOT NULL,
    description TEXT,
    action VARCHAR(50) NOT NULL,
    protocol VARCHAR(50) NOT NULL,
    source_address VARCHAR(255) NOT NULL,
    destination_address VARCHAR(255) NOT NULL,
    source_port VARCHAR(50) DEFAULT 'any',
    destination_port VARCHAR(50) DEFAULT 'any',
    priority INTEGER NOT NULL,
    enabled BOOLEAN DEFAULT true,
    log_enabled BOOLEAN DEFAULT false,
    ip_version VARCHAR(10) DEFAULT 'IPv4',
    tags JSONB,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP NOT NULL DEFAULT NOW()
);

-- Deployment targets table
CREATE TABLE deployment_targets (
    target_id VARCHAR(255) PRIMARY KEY,
    name VARCHAR(255) NOT NULL,
    provider_type VARCHAR(50) NOT NULL,
    environment VARCHAR(50) NOT NULL,
    region VARCHAR(100),
    provider_config JSONB,
    current_version VARCHAR(50),
    last_deployed_at TIMESTAMP,
    enabled BOOLEAN DEFAULT true,
    tags JSONB,
    created_at TIMESTAMP NOT NULL DEFAULT NOW()
);

-- Deployments table
CREATE TABLE deployments (
    deployment_id VARCHAR(255) PRIMARY KEY,
    ruleset_name VARCHAR(255) NOT NULL REFERENCES rulesets(name),
    version VARCHAR(50) NOT NULL,
    target_environment VARCHAR(50) NOT NULL,
    strategy VARCHAR(50) NOT NULL,
    status VARCHAR(50) NOT NULL DEFAULT 'Pending',
    target_ids JSONB,
    validation_checks JSONB,
    config JSONB,
    approval_id VARCHAR(255),
    approval_status VARCHAR(50) DEFAULT 'NotRequired',
    error_message TEXT,
    rollback_info JSONB,
    metrics JSONB,
    initiated_by VARCHAR(255),
    started_at TIMESTAMP,
    completed_at TIMESTAMP,
    created_at TIMESTAMP NOT NULL DEFAULT NOW()
);

-- Audit logs table
CREATE TABLE firewall_audit_logs (
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
CREATE INDEX idx_rules_ruleset ON firewall_rules(ruleset_name);
CREATE INDEX idx_rules_priority ON firewall_rules(ruleset_name, priority);
CREATE INDEX idx_deployments_status ON deployments(status);
CREATE INDEX idx_deployments_environment ON deployments(target_environment);
CREATE INDEX idx_audit_created_at ON firewall_audit_logs(created_at DESC);
CREATE INDEX idx_targets_environment ON deployment_targets(environment);
```

**Step 5: Run Application**

```bash
# Build and run
dotnet build
dotnet run --project src/HotSwap.Firewall.Api

# Application starts on:
# HTTP:  http://localhost:5000
# HTTPS: https://localhost:5001
# Swagger: http://localhost:5000/swagger
```

**Step 6: Verify Installation**

```bash
# Health check
curl http://localhost:5000/health

# Create test rule set
curl -X POST http://localhost:5000/api/v1/firewall/rulesets \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $(get_jwt_token)" \
  -d '{
    "name": "test-rules",
    "environment": "Development",
    "targetType": "CloudFirewall"
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

COPY ["src/HotSwap.Firewall.Api/HotSwap.Firewall.Api.csproj", "Api/"]
COPY ["src/HotSwap.Firewall.Orchestrator/HotSwap.Firewall.Orchestrator.csproj", "Orchestrator/"]
COPY ["src/HotSwap.Firewall.Infrastructure/HotSwap.Firewall.Infrastructure.csproj", "Infrastructure/"]
COPY ["src/HotSwap.Firewall.Domain/HotSwap.Firewall.Domain.csproj", "Domain/"]
RUN dotnet restore "Api/HotSwap.Firewall.Api.csproj"

COPY src/ .
RUN dotnet publish "Api/HotSwap.Firewall.Api.csproj" -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

# Create non-root user
RUN useradd -m -u 1000 firewall && \
    chown -R firewall:firewall /app

USER firewall

COPY --from=build /app/publish .

EXPOSE 5000
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
  CMD curl -f http://localhost:5000/health || exit 1

ENTRYPOINT ["dotnet", "HotSwap.Firewall.Api.dll"]
```

**Build and Push:**

```bash
# Build image
docker build -t your-registry/firewall-orchestrator:1.0.0 .

# Tag for registry
docker tag your-registry/firewall-orchestrator:1.0.0 your-registry/firewall-orchestrator:latest

# Push to registry
docker push your-registry/firewall-orchestrator:1.0.0
docker push your-registry/firewall-orchestrator:latest
```

---

## Kubernetes Deployment

### Namespace and ConfigMap

**namespace.yaml:**

```yaml
apiVersion: v1
kind: Namespace
metadata:
  name: firewall-orchestrator
```

**configmap.yaml:**

```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: firewall-config
  namespace: firewall-orchestrator
data:
  appsettings.json: |
    {
      "Firewall": {
        "DefaultDeploymentStrategy": "Canary",
        "ValidationTimeoutSeconds": 30,
        "RollbackTimeoutSeconds": 10
      },
      "OpenTelemetry": {
        "SamplingRate": 0.1
      }
    }
```

### Secrets

```bash
# Create secrets
kubectl create secret generic firewall-secrets \
  --from-literal=postgres-password=$(openssl rand -base64 32) \
  --from-literal=jwt-secret=$(openssl rand -base64 64) \
  --from-literal=aws-access-key-id=${AWS_ACCESS_KEY_ID} \
  --from-literal=aws-secret-access-key=${AWS_SECRET_ACCESS_KEY} \
  --from-literal=azure-client-secret=${AZURE_CLIENT_SECRET} \
  -n firewall-orchestrator
```

### PostgreSQL StatefulSet

**postgres-statefulset.yaml:**

```yaml
apiVersion: apps/v1
kind: StatefulSet
metadata:
  name: postgres
  namespace: firewall-orchestrator
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
          value: "firewall"
        - name: POSTGRES_USER
          value: "firewall_user"
        - name: POSTGRES_PASSWORD
          valueFrom:
            secretKeyRef:
              name: firewall-secrets
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
          storage: 50Gi
---
apiVersion: v1
kind: Service
metadata:
  name: postgres
  namespace: firewall-orchestrator
spec:
  ports:
  - port: 5432
    targetPort: 5432
  clusterIP: None
  selector:
    app: postgres
```

### Firewall Orchestrator Deployment

**firewall-deployment.yaml:**

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: firewall-orchestrator
  namespace: firewall-orchestrator
spec:
  replicas: 3
  selector:
    matchLabels:
      app: firewall-orchestrator
  template:
    metadata:
      labels:
        app: firewall-orchestrator
    spec:
      containers:
      - name: firewall-orchestrator
        image: your-registry/firewall-orchestrator:1.0.0
        ports:
        - containerPort: 5000
          name: http
        env:
        - name: ASPNETCORE_ENVIRONMENT
          value: "Production"
        - name: ConnectionStrings__PostgreSQL
          value: "Host=postgres;Database=firewall;Username=firewall_user;Password=$(POSTGRES_PASSWORD)"
        - name: POSTGRES_PASSWORD
          valueFrom:
            secretKeyRef:
              name: firewall-secrets
              key: postgres-password
        - name: JWT__Secret
          valueFrom:
            secretKeyRef:
              name: firewall-secrets
              key: jwt-secret
        - name: AWS__AccessKeyId
          valueFrom:
            secretKeyRef:
              name: firewall-secrets
              key: aws-access-key-id
        - name: AWS__SecretAccessKey
          valueFrom:
            secretKeyRef:
              name: firewall-secrets
              key: aws-secret-access-key
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
          name: firewall-config
---
apiVersion: v1
kind: Service
metadata:
  name: firewall-orchestrator
  namespace: firewall-orchestrator
spec:
  type: LoadBalancer
  ports:
  - port: 80
    targetPort: 5000
    name: http
  selector:
    app: firewall-orchestrator
---
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: firewall-orchestrator-hpa
  namespace: firewall-orchestrator
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: firewall-orchestrator
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

# Deploy PostgreSQL
kubectl apply -f postgres-statefulset.yaml

# Wait for PostgreSQL to be ready
kubectl wait --for=condition=ready pod -l app=postgres -n firewall-orchestrator --timeout=300s

# Run database migrations
kubectl run db-migrate --image=your-registry/firewall-orchestrator:1.0.0 \
  --env="ConnectionStrings__PostgreSQL=Host=postgres;Database=firewall;Username=firewall_user;Password=$(kubectl get secret firewall-secrets -n firewall-orchestrator -o jsonpath='{.data.postgres-password}' | base64 -d)" \
  --command -- dotnet ef database update \
  -n firewall-orchestrator

# Deploy Firewall Orchestrator
kubectl apply -f firewall-deployment.yaml

# Verify deployment
kubectl get pods -n firewall-orchestrator
kubectl get svc -n firewall-orchestrator

# Get service URL
kubectl get svc firewall-orchestrator -n firewall-orchestrator -o jsonpath='{.status.loadBalancer.ingress[0].ip}'
```

---

## Configuration

### Environment Variables

```bash
# Application
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://+:5000

# Database
ConnectionStrings__PostgreSQL=Host=postgres;Database=firewall;Username=firewall_user;Password=secure_password

# Firewall
Firewall__DefaultDeploymentStrategy=Canary
Firewall__ValidationTimeoutSeconds=30
Firewall__RollbackTimeoutSeconds=10

# AWS
AWS__Region=us-east-1
AWS__AccessKeyId=AKIAIOSFODNN7EXAMPLE
AWS__SecretAccessKey=wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY

# Azure
Azure__TenantId=your-tenant-id
Azure__ClientId=your-client-id
Azure__ClientSecret=your-client-secret
Azure__SubscriptionId=your-subscription-id

# OpenTelemetry
OpenTelemetry__JaegerEndpoint=http://jaeger:14268/api/traces
OpenTelemetry__SamplingRate=0.1

# JWT
JWT__Secret=your_jwt_secret_key_here
JWT__Issuer=https://firewall.example.com
JWT__Audience=firewall-api
JWT__ExpirationMinutes=60

# Rate Limiting
RateLimiting__CreateRuleSet=10
RateLimiting__Deploy=10
RateLimiting__Validate=100
```

---

## Operations

### Common Operations

**1. Check System Health:**

```bash
curl http://localhost:5000/health
```

**2. Create Rule Set:**

```bash
curl -X POST http://localhost:5000/api/v1/firewall/rulesets \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "web-server-rules",
    "environment": "Production",
    "targetType": "CloudFirewall"
  }'
```

**3. Deploy Rule Set:**

```bash
curl -X POST http://localhost:5000/api/v1/firewall/deployments \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "ruleSetName": "web-server-rules",
    "targetEnvironment": "Production",
    "strategy": "Canary",
    "targetIds": ["target-1", "target-2"]
  }'
```

**4. Check Deployment Status:**

```bash
curl http://localhost:5000/api/v1/firewall/deployments/{deployment-id} \
  -H "Authorization: Bearer $TOKEN"
```

**5. Rollback Deployment:**

```bash
curl -X POST http://localhost:5000/api/v1/firewall/deployments/{deployment-id}/rollback \
  -H "Authorization: Bearer $TOKEN" \
  -d '{"reason": "Connectivity issues"}'
```

---

## Troubleshooting

### Issue: Deployment Stuck

**Symptom:** Deployment status stuck at "InProgress"

**Solutions:**
1. Check deployment logs: `kubectl logs -l app=firewall-orchestrator -n firewall-orchestrator`
2. Verify target health: `curl http://localhost:5000/api/v1/firewall/targets`
3. Check validation failures in deployment details
4. Manually rollback if needed

### Issue: Provider Authentication Failed

**Symptom:** "Authentication failed" errors in logs

**Solutions:**
1. Verify cloud provider credentials in secrets
2. Check IAM/RBAC permissions
3. Verify credential expiration dates
4. Test credentials manually with provider CLI

---

## Monitoring

### Key Metrics

Monitor these metrics in Grafana:

- `firewall_deployments_total` - Total deployments
- `firewall_deployments_succeeded` - Successful deployments
- `firewall_deployments_failed` - Failed deployments
- `firewall_rollbacks_total` - Total rollbacks
- `firewall_deployment_duration_seconds` - Deployment time
- `firewall_rulesets_count` - Active rule sets

### Alerts

Configure these alerts:

1. **High Deployment Failure Rate:** > 10% failures
2. **Slow Deployments:** p95 deployment time > 5 minutes
3. **Frequent Rollbacks:** > 5 rollbacks per hour
4. **Target Unhealthy:** Any target health check failing

---

**Last Updated:** 2025-11-23
**Version:** 1.0.0
