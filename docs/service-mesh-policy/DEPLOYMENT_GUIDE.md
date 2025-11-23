# Deployment & Operations Guide

**Version:** 1.0.0
**Last Updated:** 2025-11-23

---

## Table of Contents

1. [Prerequisites](#prerequisites)
2. [Local Development Setup](#local-development-setup)
3. [Kubernetes Deployment](#kubernetes-deployment)
4. [Service Mesh Setup](#service-mesh-setup)
5. [Configuration](#configuration)
6. [Monitoring Setup](#monitoring-setup)
7. [Operational Runbooks](#operational-runbooks)
8. [Troubleshooting](#troubleshooting)

---

## Prerequisites

### Required Infrastructure

**Mandatory:**
- **.NET 8.0 Runtime** - Application runtime
- **Kubernetes 1.26+** - Container orchestration
- **PostgreSQL 15+** - Policy and deployment storage
- **Istio 1.18+** OR **Linkerd 2.12+** - Service mesh

**Optional:**
- **Jaeger** - Distributed tracing (recommended for production)
- **Prometheus** - Metrics collection
- **Grafana** - Metrics visualization

### System Requirements

**Policy Manager Node:**
- CPU: 2+ cores
- Memory: 4 GB+ RAM
- Disk: 20 GB+ SSD

**Kubernetes Cluster:**
- Nodes: 3+ nodes
- CPU: 4+ cores per node
- Memory: 8 GB+ RAM per node
- Disk: 50 GB+ SSD per node

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
sudo install -o root -g root -m 0755 kubectl /usr/local/bin/kubectl

# Install kind (Kubernetes in Docker) for local testing
curl -Lo ./kind https://kind.sigs.k8s.io/dl/latest/kind-linux-amd64
chmod +x ./kind
sudo mv ./kind /usr/local/bin/kind
```

**Step 2: Create Local Kubernetes Cluster**

```bash
# Create kind cluster with Istio support
cat <<EOF | kind create cluster --config=-
kind: Cluster
apiVersion: kind.x-k8s.io/v1alpha4
name: service-mesh-dev
nodes:
- role: control-plane
  kubeadmConfigPatches:
  - |
    kind: InitConfiguration
    nodeRegistration:
      kubeletExtraArgs:
        node-labels: "ingress-ready=true"
  extraPortMappings:
  - containerPort: 80
    hostPort: 80
    protocol: TCP
  - containerPort: 443
    hostPort: 443
    protocol: TCP
- role: worker
- role: worker
EOF
```

**Step 3: Install Istio**

```bash
# Download Istio
curl -L https://istio.io/downloadIstio | sh -
cd istio-*
export PATH=$PWD/bin:$PATH

# Install Istio
istioctl install --set profile=demo -y

# Enable automatic sidecar injection
kubectl label namespace default istio-injection=enabled

# Verify installation
kubectl get pods -n istio-system
```

**Step 4: Install PostgreSQL**

```bash
# Install PostgreSQL via Helm
helm repo add bitnami https://charts.bitnami.com/bitnami
helm install postgresql bitnami/postgresql \
  --set auth.postgresPassword=dev_password \
  --set auth.database=servicemesh_policy \
  --namespace default

# Get PostgreSQL password
export POSTGRES_PASSWORD=$(kubectl get secret --namespace default postgresql -o jsonpath="{.data.postgres-password}" | base64 -d)
```

**Step 5: Configure Application**

```bash
# Create appsettings.Development.json
cat > src/HotSwap.ServiceMesh.Api/appsettings.Development.json <<EOF
{
  "ConnectionStrings": {
    "PostgreSQL": "Host=postgresql.default.svc.cluster.local;Database=servicemesh_policy;Username=postgres;Password=dev_password"
  },
  "ServiceMesh": {
    "DefaultMeshType": "Istio",
    "KubernetesConfigPath": "~/.kube/config"
  },
  "Monitoring": {
    "PrometheusEndpoint": "http://prometheus.istio-system.svc.cluster.local:9090"
  },
  "OpenTelemetry": {
    "JaegerEndpoint": "http://jaeger-collector.istio-system.svc.cluster.local:14268/api/traces",
    "SamplingRate": 1.0
  }
}
EOF
```

**Step 6: Initialize Database**

```bash
# Run database migrations
dotnet ef database update --project src/HotSwap.ServiceMesh.Infrastructure

# Or use SQL script
kubectl run -it --rm psql --image=postgres:15 --restart=Never -- \
  psql -h postgresql.default.svc.cluster.local -U postgres -d servicemesh_policy -f /migrations/001_initial_schema.sql
```

**Step 7: Run Application**

```bash
# Build and run
dotnet build
dotnet run --project src/HotSwap.ServiceMesh.Api

# Application starts on:
# HTTP:  http://localhost:5000
# Swagger: http://localhost:5000/swagger
```

---

## Kubernetes Deployment

### Namespace and ConfigMap

**namespace.yaml:**

```yaml
apiVersion: v1
kind: Namespace
metadata:
  name: service-mesh-policy
  labels:
    istio-injection: enabled
```

**configmap.yaml:**

```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: policy-manager-config
  namespace: service-mesh-policy
data:
  appsettings.json: |
    {
      "ServiceMesh": {
        "DefaultMeshType": "Istio",
        "PolicyPropagationTimeout": "PT5M"
      },
      "Deployment": {
        "CanaryStages": [10, 30, 50, 100],
        "MonitoringPeriod": "PT5M",
        "AutoPromote": true
      }
    }
```

### Secrets

```bash
# Create secrets
kubectl create secret generic policy-manager-secrets \
  --from-literal=postgres-password=$(openssl rand -base64 32) \
  --from-literal=jwt-secret=$(openssl rand -base64 64) \
  -n service-mesh-policy
```

### PostgreSQL StatefulSet

**postgres-statefulset.yaml:**

```yaml
apiVersion: apps/v1
kind: StatefulSet
metadata:
  name: postgres
  namespace: service-mesh-policy
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
          value: "servicemesh_policy"
        - name: POSTGRES_USER
          value: "postgres"
        - name: POSTGRES_PASSWORD
          valueFrom:
            secretKeyRef:
              name: policy-manager-secrets
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
  namespace: service-mesh-policy
spec:
  ports:
  - port: 5432
    targetPort: 5432
  clusterIP: None
  selector:
    app: postgres
```

### Policy Manager Deployment

**policy-manager-deployment.yaml:**

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: policy-manager
  namespace: service-mesh-policy
spec:
  replicas: 3
  selector:
    matchLabels:
      app: policy-manager
  template:
    metadata:
      labels:
        app: policy-manager
        version: v1
    spec:
      serviceAccountName: policy-manager
      containers:
      - name: policy-manager
        image: your-registry/service-mesh-policy-manager:1.0.0
        ports:
        - containerPort: 5000
          name: http
        env:
        - name: ASPNETCORE_ENVIRONMENT
          value: "Production"
        - name: ConnectionStrings__PostgreSQL
          value: "Host=postgres;Database=servicemesh_policy;Username=postgres;Password=$(POSTGRES_PASSWORD)"
        - name: POSTGRES_PASSWORD
          valueFrom:
            secretKeyRef:
              name: policy-manager-secrets
              key: postgres-password
        - name: JWT_SECRET
          valueFrom:
            secretKeyRef:
              name: policy-manager-secrets
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
          name: policy-manager-config
---
apiVersion: v1
kind: Service
metadata:
  name: policy-manager
  namespace: service-mesh-policy
spec:
  type: LoadBalancer
  ports:
  - port: 80
    targetPort: 5000
    name: http
  selector:
    app: policy-manager
---
apiVersion: v1
kind: ServiceAccount
metadata:
  name: policy-manager
  namespace: service-mesh-policy
---
apiVersion: rbac.authorization.k8s.io/v1
kind: ClusterRole
metadata:
  name: policy-manager
rules:
- apiGroups: ["networking.istio.io"]
  resources: ["virtualservices", "destinationrules", "gateways", "serviceentries"]
  verbs: ["get", "list", "watch", "create", "update", "patch", "delete"]
- apiGroups: ["security.istio.io"]
  resources: ["authorizationpolicies", "peerauthentications", "requestauthentications"]
  verbs: ["get", "list", "watch", "create", "update", "patch", "delete"]
- apiGroups: ["policy.linkerd.io"]
  resources: ["servers", "serverauthorizations", "httproutes", "trafficsplits"]
  verbs: ["get", "list", "watch", "create", "update", "patch", "delete"]
- apiGroups: [""]
  resources: ["pods", "services"]
  verbs: ["get", "list", "watch"]
---
apiVersion: rbac.authorization.k8s.io/v1
kind: ClusterRoleBinding
metadata:
  name: policy-manager
roleRef:
  apiGroup: rbac.authorization.k8s.io
  kind: ClusterRole
  name: policy-manager
subjects:
- kind: ServiceAccount
  name: policy-manager
  namespace: service-mesh-policy
```

### Deploy to Kubernetes

```bash
# Create namespace
kubectl apply -f namespace.yaml

# Create ConfigMap and Secrets
kubectl apply -f configmap.yaml

# Deploy PostgreSQL
kubectl apply -f postgres-statefulset.yaml

# Wait for PostgreSQL to be ready
kubectl wait --for=condition=ready pod -l app=postgres -n service-mesh-policy --timeout=300s

# Run database migrations
kubectl run db-migrate --image=your-registry/service-mesh-policy-manager:1.0.0 \
  --env="ConnectionStrings__PostgreSQL=Host=postgres;Database=servicemesh_policy;Username=postgres;Password=$(kubectl get secret policy-manager-secrets -n service-mesh-policy -o jsonpath='{.data.postgres-password}' | base64 -d)" \
  --command -- dotnet ef database update \
  -n service-mesh-policy

# Deploy Policy Manager
kubectl apply -f policy-manager-deployment.yaml

# Verify deployment
kubectl get pods -n service-mesh-policy
kubectl get svc -n service-mesh-policy

# Get service URL
kubectl get svc policy-manager -n service-mesh-policy -o jsonpath='{.status.loadBalancer.ingress[0].ip}'
```

---

## Service Mesh Setup

### Register Service Mesh Clusters

After deploying the Policy Manager, register your service mesh clusters:

```bash
# Register Istio cluster
curl -X POST http://policy-manager/api/v1/clusters \
  -H "Authorization: Bearer $TOKEN" \
  -d '{
    "name": "production-us-east",
    "environment": "Production",
    "meshType": "Istio",
    "meshVersion": "1.20.0",
    "kubernetesEndpoint": "https://k8s-prod-us-east.example.com"
  }'

# Register Linkerd cluster
curl -X POST http://policy-manager/api/v1/clusters \
  -H "Authorization: Bearer $TOKEN" \
  -d '{
    "name": "staging-eu-west",
    "environment": "Staging",
    "meshType": "Linkerd",
    "meshVersion": "2.14.0",
    "kubernetesEndpoint": "https://k8s-staging-eu-west.example.com"
  }'
```

---

## Configuration

### Environment Variables

```bash
# Application
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://+:5000

# Database
ConnectionStrings__PostgreSQL=Host=postgres;Database=servicemesh_policy;Username=postgres;Password=secure_password

# Service Mesh
ServiceMesh__DefaultMeshType=Istio
ServiceMesh__KubernetesConfigPath=/etc/kubernetes/config
ServiceMesh__PolicyPropagationTimeout=PT5M

# Deployment
Deployment__CanaryStages=[10,30,50,100]
Deployment__MonitoringPeriod=PT5M
Deployment__AutoPromote=true
Deployment__RollbackOnErrorRate=5
Deployment__RollbackOnLatencyIncrease=50

# Monitoring
Monitoring__PrometheusEndpoint=http://prometheus:9090
Monitoring__MetricsCollectionInterval=PT10S

# OpenTelemetry
OpenTelemetry__JaegerEndpoint=http://jaeger:14268/api/traces
OpenTelemetry__SamplingRate=0.1

# JWT
JWT__Secret=your_jwt_secret_key_here
JWT__Issuer=https://policy-manager.example.com
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
  - job_name: 'policy-manager'
    static_configs:
    - targets: ['policy-manager.service-mesh-policy.svc.cluster.local:5000']
    metrics_path: '/metrics'

  - job_name: 'istio-mesh'
    kubernetes_sd_configs:
    - role: endpoints
      namespaces:
        names:
        - istio-system
```

### Grafana Dashboard

Import the pre-built Grafana dashboard:

```bash
# Import dashboard from file
kubectl create configmap grafana-dashboard-policy-manager \
  --from-file=dashboard.json=grafana/policy-manager-dashboard.json \
  -n monitoring
```

**Dashboard Panels:**
1. Policy Deployment Rate
2. Deployment Success Rate
3. Canary Promotion Timeline
4. Rollback Events
5. Policy Propagation Latency
6. Active Policies by Cluster
7. Traffic Metrics Comparison

---

## Operational Runbooks

### Runbook 1: Deploy Policy to Production

**Scenario:** Deploy a new circuit breaker policy to production

**Steps:**

1. **Create Policy**
```bash
curl -X POST https://policy-manager/api/v1/policies \
  -H "Authorization: Bearer $TOKEN" \
  -d @circuit-breaker-policy.json
```

2. **Validate Policy**
```bash
curl -X POST https://policy-manager/api/v1/validation/validate \
  -H "Authorization: Bearer $TOKEN" \
  -d '{"policyId": "pol-123"}'
```

3. **Submit for Approval**
```bash
curl -X POST https://policy-manager/api/v1/policies/pol-123/submit \
  -H "Authorization: Bearer $TOKEN" \
  -d '{"notes": "Ready for production"}'
```

4. **Admin Approves Policy**
```bash
curl -X POST https://policy-manager/api/v1/policies/pol-123/approve \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -d '{"notes": "Approved for deployment"}'
```

5. **Deploy with Canary Strategy**
```bash
curl -X POST https://policy-manager/api/v1/deployments \
  -H "Authorization: Bearer $TOKEN" \
  -d '{
    "policyId": "pol-123",
    "environment": "Production",
    "clusterId": "prod-us-east-1",
    "strategy": "Canary",
    "config": {
      "canaryPercentage": "10",
      "autoPromote": "true"
    }
  }'
```

6. **Monitor Deployment**
```bash
# Watch deployment status
watch -n 5 'curl -s https://policy-manager/api/v1/deployments/dep-abc123 -H "Authorization: Bearer $TOKEN" | jq .status'
```

---

### Runbook 2: Emergency Rollback

**Scenario:** Production incident requires immediate rollback

**Steps:**

1. **Identify Deployment**
```bash
curl -s https://policy-manager/api/v1/deployments?environment=Production&status=InProgress \
  -H "Authorization: Bearer $TOKEN"
```

2. **Trigger Rollback**
```bash
curl -X POST https://policy-manager/api/v1/deployments/dep-abc123/rollback \
  -H "Authorization: Bearer $TOKEN" \
  -d '{"reason": "High error rate detected"}'
```

3. **Verify Rollback**
```bash
# Check deployment status
curl -s https://policy-manager/api/v1/deployments/dep-abc123 \
  -H "Authorization: Bearer $TOKEN" | jq .status
# Should show "RolledBack"
```

4. **Verify Service Metrics**
```bash
# Check service error rate
curl -s https://policy-manager/api/v1/services/prod-us-east-1/production/user-service/metrics \
  -H "Authorization: Bearer $TOKEN" | jq .errorRate
```

---

## Troubleshooting

### Issue: Policy Not Applying to Kubernetes

**Symptoms:**
- Deployment shows "Completed" but policy not visible in Kubernetes
- Error: "Failed to apply policy to cluster"

**Diagnosis:**
```bash
# Check Policy Manager logs
kubectl logs -n service-mesh-policy deployment/policy-manager

# Check RBAC permissions
kubectl auth can-i create virtualservices.networking.istio.io --as=system:serviceaccount:service-mesh-policy:policy-manager

# Check Kubernetes API connectivity
kubectl run -it --rm test --image=curlimages/curl --restart=Never -- \
  curl -k https://kubernetes.default.svc.cluster.local/api/v1/namespaces
```

**Solution:**
```bash
# Grant proper RBAC permissions
kubectl apply -f policy-manager-rbac.yaml

# Restart Policy Manager
kubectl rollout restart deployment/policy-manager -n service-mesh-policy
```

---

### Issue: Canary Deployment Not Progressing

**Symptoms:**
- Deployment stuck at 10% canary
- No automatic promotion after monitoring period

**Diagnosis:**
```bash
# Check deployment status
curl -s https://policy-manager/api/v1/deployments/dep-abc123 \
  -H "Authorization: Bearer $TOKEN" | jq

# Check metrics collection
curl -s https://policy-manager/api/v1/deployments/dep-abc123/metrics \
  -H "Authorization: Bearer $TOKEN" | jq
```

**Solution:**
```bash
# Manually promote canary
curl -X POST https://policy-manager/api/v1/deployments/dep-abc123/promote \
  -H "Authorization: Bearer $TOKEN" \
  -d '{"targetPercentage": 30}'

# If metrics unavailable, check Prometheus connectivity
kubectl port-forward -n istio-system svc/prometheus 9090:9090
curl http://localhost:9090/api/v1/query?query=istio_requests_total
```

---

### Issue: High Policy Propagation Latency

**Symptoms:**
- Policy propagation takes > 60 seconds
- Timeout errors during deployment

**Diagnosis:**
```bash
# Check cluster size
kubectl get pods --all-namespaces | wc -l

# Check Policy Manager CPU/memory
kubectl top pod -n service-mesh-policy

# Check Kubernetes API server latency
kubectl get --raw /metrics | grep apiserver_request_duration_seconds
```

**Solution:**
```bash
# Scale Policy Manager horizontally
kubectl scale deployment/policy-manager --replicas=5 -n service-mesh-policy

# Increase resource limits
kubectl set resources deployment/policy-manager -n service-mesh-policy \
  --limits=cpu=4,memory=8Gi \
  --requests=cpu=2,memory=4Gi
```

---

**Last Updated:** 2025-11-23
**Version:** 1.0.0
**Support:** policy-manager-support@example.com
