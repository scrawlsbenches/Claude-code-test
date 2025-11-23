# Deployment & Operations Guide

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
9. [Operational Runbooks](#operational-runbooks)

---

## Prerequisites

### Required Infrastructure

**Mandatory:**
- **.NET 8.0 Runtime** - Application runtime
- **PostgreSQL 15+** - Operator metadata, CRD schemas, deployment history
- **Kubernetes Cluster(s)** - 1+ clusters for operator deployment

**Optional:**
- **Jaeger** - Distributed tracing (recommended for production)
- **Prometheus** - Metrics collection
- **Grafana** - Metrics visualization

### System Requirements

**Operator Manager API:**
- CPU: 2+ cores
- Memory: 4 GB+ RAM
- Disk: 20 GB+ SSD
- Network: 1 Gbps

**PostgreSQL:**
- CPU: 2+ cores
- Memory: 4 GB+ RAM
- Disk: 50 GB+ SSD

**Target Kubernetes Clusters:**
- Kubernetes v1.24+
- Helm 3.0+
- kubectl CLI access

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

# Install kubectl
curl -LO "https://dl.k8s.io/release/$(curl -L -s https://dl.k8s.io/release/stable.txt)/bin/linux/amd64/kubectl"
chmod +x kubectl
sudo mv kubectl /usr/local/bin/

# Install Helm
curl https://raw.githubusercontent.com/helm/helm/main/scripts/get-helm-3 | bash
```

**Step 2: Start Local Kubernetes Cluster**

```bash
# Option 1: kind (Kubernetes in Docker)
curl -Lo ./kind https://kind.sigs.k8s.io/dl/v0.20.0/kind-linux-amd64
chmod +x ./kind
sudo mv ./kind /usr/local/bin/kind

# Create kind cluster
kind create cluster --name operator-manager-dev

# Option 2: minikube
curl -LO https://storage.googleapis.com/minikube/releases/latest/minikube-linux-amd64
sudo install minikube-linux-amd64 /usr/local/bin/minikube

# Start minikube cluster
minikube start --cpus=4 --memory=8192
```

**Step 3: Start Infrastructure (Docker Compose)**

```bash
# Clone repository
git clone https://github.com/scrawlsbenches/Claude-code-test.git
cd Claude-code-test

# Start PostgreSQL and Jaeger
docker-compose -f docker-compose.operator-manager.yml up -d

# Verify services
docker-compose ps
```

**docker-compose.operator-manager.yml:**

```yaml
version: '3.8'

services:
  postgres:
    image: postgres:15-alpine
    ports:
      - "5432:5432"
    environment:
      POSTGRES_DB: operator_manager
      POSTGRES_USER: operator_user
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
  postgres-data:
```

**Step 4: Configure Application**

```bash
# Create appsettings.Development.json
cat > src/HotSwap.Kubernetes.Api/appsettings.Development.json <<EOF
{
  "ConnectionStrings": {
    "PostgreSQL": "Host=localhost;Database=operator_manager;Username=operator_user;Password=dev_password"
  },
  "KubernetesManager": {
    "DefaultTimeout": "PT5M",
    "MaxConcurrentDeployments": 10,
    "HealthCheckInterval": "PT30S"
  },
  "OpenTelemetry": {
    "JaegerEndpoint": "http://localhost:14268/api/traces",
    "SamplingRate": 1.0
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "HotSwap.Kubernetes": "Debug"
    }
  }
}
EOF
```

**Step 5: Initialize Database**

```bash
# Run database migrations
dotnet ef database update --project src/HotSwap.Kubernetes.Infrastructure

# Or use SQL script
psql -h localhost -U operator_user -d operator_manager -f db/migrations/001_initial_schema.sql
```

**db/migrations/001_initial_schema.sql:**

```sql
-- Operators table
CREATE TABLE operators (
    operator_id VARCHAR(255) PRIMARY KEY,
    name VARCHAR(255) UNIQUE NOT NULL,
    description TEXT,
    namespace VARCHAR(255) NOT NULL,
    chart_repository TEXT NOT NULL,
    chart_name VARCHAR(255) NOT NULL,
    current_version VARCHAR(50),
    latest_version VARCHAR(50),
    labels JSONB DEFAULT '{}',
    default_values JSONB DEFAULT '{}',
    deployed_cluster_count INT DEFAULT 0,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP NOT NULL DEFAULT NOW()
);

-- Clusters table
CREATE TABLE clusters (
    cluster_id VARCHAR(255) PRIMARY KEY,
    name VARCHAR(255) UNIQUE NOT NULL,
    description TEXT,
    environment VARCHAR(50) NOT NULL,
    kubeconfig_encrypted TEXT NOT NULL,
    api_server_url TEXT NOT NULL,
    kubernetes_version VARCHAR(50),
    node_count INT DEFAULT 0,
    health_status VARCHAR(50) DEFAULT 'Unknown',
    last_health_check_at TIMESTAMP,
    labels JSONB DEFAULT '{}',
    is_enabled BOOLEAN DEFAULT TRUE,
    registered_at TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP NOT NULL DEFAULT NOW()
);

-- Deployments table
CREATE TABLE deployments (
    deployment_id VARCHAR(255) PRIMARY KEY,
    operator_id VARCHAR(255) NOT NULL REFERENCES operators(operator_id),
    operator_name VARCHAR(255) NOT NULL,
    target_version VARCHAR(50) NOT NULL,
    previous_version VARCHAR(50),
    strategy VARCHAR(50) NOT NULL,
    target_clusters TEXT[] NOT NULL,
    status VARCHAR(50) NOT NULL DEFAULT 'Planning',
    strategy_config JSONB,
    helm_values JSONB DEFAULT '{}',
    initiated_by VARCHAR(255) NOT NULL,
    approval_status VARCHAR(50) DEFAULT 'NotRequired',
    approved_by VARCHAR(255),
    approved_at TIMESTAMP,
    started_at TIMESTAMP,
    completed_at TIMESTAMP,
    error_message TEXT,
    rollback_reason TEXT,
    rolled_back_at TIMESTAMP,
    auto_rollback_enabled BOOLEAN DEFAULT TRUE,
    created_at TIMESTAMP NOT NULL DEFAULT NOW()
);

-- CRDs table
CREATE TABLE crds (
    crd_id VARCHAR(255) PRIMARY KEY,
    name VARCHAR(255) NOT NULL,
    "group" VARCHAR(255) NOT NULL,
    versions TEXT[] NOT NULL,
    scope VARCHAR(50) NOT NULL DEFAULT 'Namespaced',
    operator_name VARCHAR(255) NOT NULL,
    operator_version VARCHAR(50) NOT NULL,
    schema_definition TEXT NOT NULL,
    schema_version VARCHAR(50) NOT NULL DEFAULT '1.0',
    status VARCHAR(50) NOT NULL DEFAULT 'Active',
    approval_status VARCHAR(50) DEFAULT 'NotRequired',
    approved_by VARCHAR(255),
    approved_at TIMESTAMP,
    deprecated_at TIMESTAMP,
    deprecation_reason TEXT,
    migration_guide TEXT,
    created_at TIMESTAMP NOT NULL DEFAULT NOW()
);

-- Cluster deployments (tracks per-cluster deployment status)
CREATE TABLE cluster_deployments (
    id SERIAL PRIMARY KEY,
    deployment_id VARCHAR(255) NOT NULL REFERENCES deployments(deployment_id),
    cluster_name VARCHAR(255) NOT NULL,
    status VARCHAR(50) NOT NULL DEFAULT 'Planning',
    started_at TIMESTAMP,
    completed_at TIMESTAMP,
    error_message TEXT,
    health_check_result JSONB
);

-- Indexes
CREATE INDEX idx_operators_name ON operators(name);
CREATE INDEX idx_clusters_environment ON clusters(environment);
CREATE INDEX idx_deployments_operator_id ON deployments(operator_id);
CREATE INDEX idx_deployments_status ON deployments(status);
CREATE INDEX idx_crds_operator_name ON crds(operator_name);
CREATE INDEX idx_cluster_deployments_deployment_id ON cluster_deployments(deployment_id);
```

**Step 6: Register Local Kubernetes Cluster**

```bash
# Get kubeconfig from kind or minikube
kind get kubeconfig --name operator-manager-dev > /tmp/kubeconfig.yaml
# OR
minikube kubectl -- config view --raw > /tmp/kubeconfig.yaml

# Base64 encode kubeconfig
KUBECONFIG_BASE64=$(cat /tmp/kubeconfig.yaml | base64 -w 0)

# Store securely
echo $KUBECONFIG_BASE64 > /tmp/kubeconfig.b64
```

**Step 7: Run Application**

```bash
# Build and run
dotnet build
dotnet run --project src/HotSwap.Kubernetes.Api

# Application starts on:
# HTTP:  http://localhost:5000
# HTTPS: https://localhost:5001
# Swagger: http://localhost:5000/swagger
```

**Step 8: Verify Installation**

```bash
# Health check
curl http://localhost:5000/health

# Register local cluster
curl -X POST http://localhost:5000/api/v1/clusters \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $(get_jwt_token)" \
  -d "{
    \"name\": \"local-dev\",
    \"description\": \"Local development cluster\",
    \"environment\": \"Development\",
    \"kubeconfig\": \"$(cat /tmp/kubeconfig.b64)\"
  }"

# Register an operator (cert-manager example)
curl -X POST http://localhost:5000/api/v1/operators \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $(get_jwt_token)" \
  -d '{
    "name": "cert-manager",
    "namespace": "cert-manager",
    "chartRepository": "https://charts.jetstack.io",
    "chartName": "cert-manager",
    "currentVersion": "v1.13.0"
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

COPY ["src/HotSwap.Kubernetes.Api/HotSwap.Kubernetes.Api.csproj", "Api/"]
COPY ["src/HotSwap.Kubernetes.Orchestrator/HotSwap.Kubernetes.Orchestrator.csproj", "Orchestrator/"]
COPY ["src/HotSwap.Kubernetes.Infrastructure/HotSwap.Kubernetes.Infrastructure.csproj", "Infrastructure/"]
COPY ["src/HotSwap.Kubernetes.Domain/HotSwap.Kubernetes.Domain.csproj", "Domain/"]
RUN dotnet restore "Api/HotSwap.Kubernetes.Api.csproj"

COPY src/ .
RUN dotnet publish "Api/HotSwap.Kubernetes.Api.csproj" -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

# Install kubectl and Helm
RUN apt-get update && apt-get install -y curl && \
    curl -LO "https://dl.k8s.io/release/v1.28.0/bin/linux/amd64/kubectl" && \
    chmod +x kubectl && mv kubectl /usr/local/bin/ && \
    curl https://raw.githubusercontent.com/helm/helm/main/scripts/get-helm-3 | bash && \
    apt-get clean && rm -rf /var/lib/apt/lists/*

# Create non-root user
RUN useradd -m -u 1000 opmanager && \
    chown -R opmanager:opmanager /app

USER opmanager

COPY --from=build /app/publish .

EXPOSE 5000
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
  CMD curl -f http://localhost:5000/health || exit 1

ENTRYPOINT ["dotnet", "HotSwap.Kubernetes.Api.dll"]
```

**Build and Push:**

```bash
# Build image
docker build -t your-registry/operator-manager:1.0.0 .

# Tag for registry
docker tag your-registry/operator-manager:1.0.0 your-registry/operator-manager:latest

# Push to registry
docker push your-registry/operator-manager:1.0.0
docker push your-registry/operator-manager:latest
```

### Docker Compose Full Stack

**docker-compose.production.yml:**

```yaml
version: '3.8'

services:
  operator-manager-api:
    image: your-registry/operator-manager:1.0.0
    ports:
      - "5000:5000"
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ASPNETCORE_URLS: http://+:5000
      ConnectionStrings__PostgreSQL: "Host=postgres;Database=operator_manager;Username=operator_user;Password=${POSTGRES_PASSWORD}"
      OpenTelemetry__JaegerEndpoint: "http://jaeger:14268/api/traces"
    depends_on:
      - postgres
      - jaeger
    networks:
      - operator-network
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
      POSTGRES_DB: operator_manager
      POSTGRES_USER: operator_user
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
      POSTGRES_MAX_CONNECTIONS: 200
    volumes:
      - postgres-data:/var/lib/postgresql/data
    networks:
      - operator-network
    deploy:
      resources:
        limits:
          memory: 4G

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
      - operator-network

  prometheus:
    image: prom/prometheus:latest
    volumes:
      - ./prometheus.yml:/etc/prometheus/prometheus.yml
      - prometheus-data:/prometheus
    ports:
      - "9090:9090"
    networks:
      - operator-network

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
      - operator-network

volumes:
  postgres-data:
  jaeger-data:
  prometheus-data:
  grafana-data:

networks:
  operator-network:
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
docker-compose logs -f operator-manager-api
```

---

## Kubernetes Deployment

### Namespace and ConfigMap

**namespace.yaml:**

```yaml
apiVersion: v1
kind: Namespace
metadata:
  name: operator-manager
```

**configmap.yaml:**

```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: operator-manager-config
  namespace: operator-manager
data:
  appsettings.json: |
    {
      "KubernetesManager": {
        "DefaultTimeout": "PT5M",
        "MaxConcurrentDeployments": 10,
        "HealthCheckInterval": "PT30S"
      },
      "OpenTelemetry": {
        "SamplingRate": 0.1
      }
    }
```

### Secrets

```bash
# Create secrets
kubectl create secret generic operator-manager-secrets \
  --from-literal=postgres-password=$(openssl rand -base64 32) \
  --from-literal=jwt-secret=$(openssl rand -base64 64) \
  -n operator-manager
```

### PostgreSQL StatefulSet

**postgres-statefulset.yaml:**

```yaml
apiVersion: apps/v1
kind: StatefulSet
metadata:
  name: postgres
  namespace: operator-manager
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
          value: "operator_manager"
        - name: POSTGRES_USER
          value: "operator_user"
        - name: POSTGRES_PASSWORD
          valueFrom:
            secretKeyRef:
              name: operator-manager-secrets
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
  namespace: operator-manager
spec:
  ports:
  - port: 5432
    targetPort: 5432
  clusterIP: None
  selector:
    app: postgres
```

### Operator Manager Deployment

**operator-manager-deployment.yaml:**

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: operator-manager-api
  namespace: operator-manager
spec:
  replicas: 3
  selector:
    matchLabels:
      app: operator-manager-api
  template:
    metadata:
      labels:
        app: operator-manager-api
    spec:
      serviceAccountName: operator-manager-sa
      containers:
      - name: operator-manager-api
        image: your-registry/operator-manager:1.0.0
        ports:
        - containerPort: 5000
          name: http
        env:
        - name: ASPNETCORE_ENVIRONMENT
          value: "Production"
        - name: ConnectionStrings__PostgreSQL
          value: "Host=postgres;Database=operator_manager;Username=operator_user;Password=$(POSTGRES_PASSWORD)"
        - name: POSTGRES_PASSWORD
          valueFrom:
            secretKeyRef:
              name: operator-manager-secrets
              key: postgres-password
        - name: JWT_SECRET
          valueFrom:
            secretKeyRef:
              name: operator-manager-secrets
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
          name: operator-manager-config
---
apiVersion: v1
kind: Service
metadata:
  name: operator-manager-api
  namespace: operator-manager
spec:
  type: LoadBalancer
  ports:
  - port: 80
    targetPort: 5000
    name: http
  selector:
    app: operator-manager-api
---
apiVersion: v1
kind: ServiceAccount
metadata:
  name: operator-manager-sa
  namespace: operator-manager
---
apiVersion: rbac.authorization.k8s.io/v1
kind: ClusterRole
metadata:
  name: operator-manager-role
rules:
- apiGroups: ["*"]
  resources: ["*"]
  verbs: ["get", "list", "watch", "create", "update", "patch", "delete"]
---
apiVersion: rbac.authorization.k8s.io/v1
kind: ClusterRoleBinding
metadata:
  name: operator-manager-binding
roleRef:
  apiGroup: rbac.authorization.k8s.io
  kind: ClusterRole
  name: operator-manager-role
subjects:
- kind: ServiceAccount
  name: operator-manager-sa
  namespace: operator-manager
---
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: operator-manager-hpa
  namespace: operator-manager
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: operator-manager-api
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
kubectl create secret generic operator-manager-secrets \
  --from-literal=postgres-password=$(openssl rand -base64 32) \
  --from-literal=jwt-secret=$(openssl rand -base64 64) \
  -n operator-manager

# Deploy PostgreSQL
kubectl apply -f postgres-statefulset.yaml

# Wait for PostgreSQL to be ready
kubectl wait --for=condition=ready pod -l app=postgres -n operator-manager --timeout=300s

# Run database migrations (job)
kubectl apply -f db-migration-job.yaml

# Deploy Operator Manager API
kubectl apply -f operator-manager-deployment.yaml

# Verify deployment
kubectl get pods -n operator-manager
kubectl get svc -n operator-manager

# Get service URL
kubectl get svc operator-manager-api -n operator-manager -o jsonpath='{.status.loadBalancer.ingress[0].ip}'
```

---

## Migration Path

### Phase 1: Parallel Deployment (Week 1)

**Goal:** Deploy operator manager alongside existing infrastructure

1. **Deploy Infrastructure:**
   ```bash
   kubectl apply -f namespace.yaml
   kubectl apply -f postgres-statefulset.yaml
   ```

2. **Deploy Operator Manager API:**
   ```bash
   kubectl apply -f operator-manager-deployment.yaml
   ```

3. **Register Existing Clusters:**
   ```bash
   # Register production clusters
   for cluster in prod-us-east prod-eu-west; do
     kubectl --context=$cluster config view --raw > /tmp/$cluster.kubeconfig
     curl -X POST https://operator-manager.example.com/api/v1/clusters \
       -H "Authorization: Bearer $TOKEN" \
       -d "{
         \"name\": \"$cluster\",
         \"environment\": \"Production\",
         \"kubeconfig\": \"$(cat /tmp/$cluster.kubeconfig | base64 -w 0)\"
       }"
   done
   ```

4. **Register Existing Operators:**
   ```bash
   # Register cert-manager
   curl -X POST https://operator-manager.example.com/api/v1/operators \
     -H "Authorization: Bearer $TOKEN" \
     -d '{
       "name": "cert-manager",
       "namespace": "cert-manager",
       "chartRepository": "https://charts.jetstack.io",
       "chartName": "cert-manager",
       "currentVersion": "v1.13.0"
     }'
   ```

### Phase 2: Gradual Adoption (Week 2-3)

**Goal:** Start using operator manager for non-critical operators

1. **Deploy Non-Critical Operators:**
   ```bash
   # Deploy external-dns using operator manager
   curl -X POST https://operator-manager.example.com/api/v1/deployments \
     -H "Authorization: Bearer $TOKEN" \
     -d '{
       "operatorName": "external-dns",
       "targetVersion": "v0.14.0",
       "strategy": "Rolling",
       "targetClusters": ["dev-1", "staging"]
     }'
   ```

2. **Monitor Deployments:**
   - Check deployment status via API
   - Verify health checks
   - Review Grafana dashboards

### Phase 3: Full Migration (Week 4)

**Goal:** Migrate all operator management to operator manager

1. **Migrate Critical Operators:**
   - Use Canary or Blue-Green strategies
   - Enable approval workflow for production
   - Monitor health metrics closely

2. **Retire Old Processes:**
   - Document old manual processes
   - Archive deployment scripts
   - Update runbooks

---

## Configuration

### Environment Variables

```bash
# Application
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://+:5000;https://+:5001

# Database
ConnectionStrings__PostgreSQL=Host=postgres;Database=operator_manager;Username=operator_user;Password=secure_password

# Kubernetes
KubernetesManager__DefaultTimeout=PT5M
KubernetesManager__MaxConcurrentDeployments=10
KubernetesManager__HealthCheckInterval=PT30S

# OpenTelemetry
OpenTelemetry__JaegerEndpoint=http://jaeger:14268/api/traces
OpenTelemetry__SamplingRate=0.1

# JWT
JWT__Secret=your_jwt_secret_key_here
JWT__Issuer=https://operator-manager.example.com
JWT__Audience=operator-manager-api
JWT__ExpirationMinutes=60
```

---

## Monitoring Setup

### Prometheus Configuration

**prometheus.yml:**

```yaml
global:
  scrape_interval: 15s

scrape_configs:
  - job_name: 'operator-manager'
    static_configs:
      - targets: ['operator-manager-api:5000']
    metrics_path: '/metrics'
```

### Grafana Dashboard

Import the pre-built dashboard from `grafana/operator-manager-dashboard.json`.

**Key Panels:**
- Deployment throughput (deployments/hour)
- Deployment success rate
- Operator health status (per cluster)
- Active deployments gauge
- Deployment duration histogram

---

## Troubleshooting

### Issue: Cluster Connection Failures

**Symptom:** Cannot connect to registered cluster

**Solutions:**
1. Verify kubeconfig validity: `kubectl --kubeconfig=/path/to/config cluster-info`
2. Check network connectivity to cluster API server
3. Verify service account permissions
4. Review operator manager logs: `kubectl logs -n operator-manager -l app=operator-manager-api`

### Issue: Deployment Stuck in Planning

**Symptom:** Deployment never progresses past Planning status

**Solutions:**
1. Check approval status if production deployment
2. Verify CRD compatibility validation passed
3. Check cluster health status
4. Review deployment logs in database

### Issue: Health Check Failures

**Symptom:** Operators marked as unhealthy despite working

**Solutions:**
1. Verify controller pods are actually healthy
2. Check webhook certificate expiration
3. Review health check configuration
4. Increase health check timeout

---

**Last Updated:** 2025-11-23
**Version:** 1.0.0
