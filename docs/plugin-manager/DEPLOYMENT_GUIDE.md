# Plugin Manager Deployment Guide

**Version:** 1.0.0
**Last Updated:** 2025-11-23

---

## Table of Contents

1. [Prerequisites](#prerequisites)
2. [Infrastructure Setup](#infrastructure-setup)
3. [Installation](#installation)
4. [Configuration](#configuration)
5. [Operations](#operations)
6. [Troubleshooting](#troubleshooting)
7. [Monitoring](#monitoring)

---

## Prerequisites

### Required Infrastructure

| Component | Version | Purpose |
|-----------|---------|---------|
| Kubernetes | 1.28+ | Container orchestration |
| PostgreSQL | 15+ | Plugin metadata storage |
| Redis | 7+ | Caching, distributed locks |
| MinIO / S3 | Latest | Plugin binary storage |
| .NET Runtime | 8.0+ | Application runtime |

### Optional Components

| Component | Version | Purpose |
|-----------|---------|---------|
| Jaeger | Latest | Distributed tracing |
| Prometheus | Latest | Metrics collection |
| Grafana | Latest | Metrics visualization |
| HashiCorp Vault (self-hosted) / Kubernetes Secrets | Latest | Secret management |

### Resource Requirements

**Minimum (Development):**
- 4 CPU cores
- 8 GB RAM
- 50 GB storage

**Recommended (Production):**
- 16 CPU cores
- 32 GB RAM
- 500 GB storage (SSD)
- High-speed network (1 Gbps+)

---

## Infrastructure Setup

### 1. Kubernetes Cluster Setup

```bash
# Create namespace for plugin manager
kubectl create namespace plugin-manager

# Create namespaces for tenant isolation
kubectl create namespace tenants

# Apply resource quotas
kubectl apply -f k8s/resource-quotas.yaml
```

**resource-quotas.yaml:**
```yaml
apiVersion: v1
kind: ResourceQuota
metadata:
  name: tenant-quota
  namespace: tenants
spec:
  hard:
    requests.cpu: "100"
    requests.memory: 200Gi
    persistentvolumeclaims: "50"
    pods: "500"
```

### 2. PostgreSQL Setup

```bash
# Using Helm
helm repo add bitnami https://charts.bitnami.com/bitnami
helm install postgresql bitnami/postgresql \
  --namespace plugin-manager \
  --set auth.postgresPassword=<secure-password> \
  --set primary.persistence.size=100Gi

# Create database
kubectl exec -it postgresql-0 -n plugin-manager -- psql -U postgres
CREATE DATABASE pluginmanager;
CREATE USER pluginmgr WITH ENCRYPTED PASSWORD '<secure-password>';
GRANT ALL PRIVILEGES ON DATABASE pluginmanager TO pluginmgr;
```

### 3. Redis Setup

```bash
# Using Helm
helm install redis bitnami/redis \
  --namespace plugin-manager \
  --set auth.password=<secure-password> \
  --set master.persistence.size=20Gi
```

### 4. MinIO Setup

```bash
# Using Helm
helm install minio bitnami/minio \
  --namespace plugin-manager \
  --set auth.rootPassword=<secure-password> \
  --set persistence.size=500Gi

# Create bucket for plugins
mc alias set myminio http://minio:9000 admin <secure-password>
mc mb myminio/plugins
mc policy set download myminio/plugins
```

---

## Installation

### 1. Clone Repository

```bash
git clone https://github.com/your-org/hotswap-plugin-manager.git
cd hotswap-plugin-manager
```

### 2. Configure Secrets

```bash
# Create secrets for database connection
kubectl create secret generic db-secret \
  --from-literal=connectionString="Host=postgresql;Database=pluginmanager;Username=pluginmgr;Password=<password>" \
  --namespace plugin-manager

# Create secrets for Redis
kubectl create secret generic redis-secret \
  --from-literal=connectionString="redis:6379,password=<password>" \
  --namespace plugin-manager

# Create secrets for MinIO
kubectl create secret generic storage-secret \
  --from-literal=endpoint="http://minio:9000" \
  --from-literal=accessKey="admin" \
  --from-literal=secretKey="<password>" \
  --namespace plugin-manager
```

### 3. Deploy Application

```bash
# Apply Kubernetes manifests
kubectl apply -f k8s/deployment.yaml
kubectl apply -f k8s/service.yaml
kubectl apply -f k8s/ingress.yaml

# Verify deployment
kubectl get pods -n plugin-manager
kubectl logs -f deployment/plugin-manager -n plugin-manager
```

### 4. Run Database Migrations

```bash
# Run migrations
kubectl exec -it deployment/plugin-manager -n plugin-manager -- \
  dotnet HotSwap.Distributed.Migrations.dll

# Verify migrations
kubectl exec -it postgresql-0 -n plugin-manager -- \
  psql -U postgres -d pluginmanager -c "\dt"
```

---

## Configuration

### Environment Variables

**appsettings.Production.json:**
```json
{
  "ConnectionStrings": {
    "PostgreSQL": "${DB_CONNECTION_STRING}",
    "Redis": "${REDIS_CONNECTION_STRING}"
  },
  "Storage": {
    "Provider": "MinIO",
    "Endpoint": "${STORAGE_ENDPOINT}",
    "AccessKey": "${STORAGE_ACCESS_KEY}",
    "SecretKey": "${STORAGE_SECRET_KEY}",
    "BucketName": "plugins"
  },
  "Authentication": {
    "JwtSecret": "${JWT_SECRET}",
    "JwtIssuer": "plugin-manager",
    "JwtAudience": "plugin-api"
  },
  "RateLimiting": {
    "Enabled": true,
    "DefaultRequestsPerMinute": 100
  },
  "Telemetry": {
    "Jaeger": {
      "Enabled": true,
      "Endpoint": "http://jaeger:14268/api/traces"
    },
    "Prometheus": {
      "Enabled": true,
      "Port": 9090
    }
  },
  "Deployment": {
    "DefaultStrategy": "Canary",
    "HealthCheckInterval": 30,
    "MaxConcurrentDeployments": 10
  }
}
```

### Kubernetes ConfigMap

**configmap.yaml:**
```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: plugin-manager-config
  namespace: plugin-manager
data:
  ASPNETCORE_ENVIRONMENT: "Production"
  DEFAULT_DEPLOYMENT_STRATEGY: "Canary"
  HEALTH_CHECK_INTERVAL: "30"
  MAX_TENANTS: "1000"
  MAX_PLUGINS_PER_TENANT: "50"
```

---

## Operations

### Creating a Tenant

```bash
# Using API
curl -X POST https://api.example.com/api/v1/tenants \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "tenantId": "tenant-acme-corp",
    "name": "Acme Corporation",
    "namespace": "tenant-acme",
    "quotas": {
      "maxPlugins": 20,
      "maxCpu": 8.0,
      "maxMemoryGB": 16
    }
  }'

# Kubernetes resources are automatically created:
# - Namespace: tenant-acme
# - ResourceQuota
# - NetworkPolicies
# - ServiceAccount
```

### Registering a Plugin

```bash
# 1. Build plugin
cd my-plugin
dotnet build -c Release

# 2. Register plugin
curl -X POST https://api.example.com/api/v1/plugins \
  -H "Authorization: Bearer $TOKEN" \
  -F "pluginId=payment-stripe" \
  -F "name=Stripe Payment Processor" \
  -F "version=2.0.0" \
  -F "binary=@./bin/Release/net8.0/MyPlugin.dll" \
  -F "metadata=@./plugin-metadata.json"
```

### Deploying a Plugin

```bash
# Deploy with canary strategy
curl -X POST https://api.example.com/api/v1/plugin-deployments \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "pluginId": "payment-stripe",
    "pluginVersion": "2.0.0",
    "tenantId": "tenant-acme-corp",
    "environment": "Production",
    "strategy": "Canary",
    "strategyConfig": {
      "stages": "10,30,50,100",
      "stageInterval": "300"
    },
    "totalInstances": 10
  }'

# Monitor deployment
DEPLOYMENT_ID="<deployment-id-from-response>"
curl https://api.example.com/api/v1/plugin-deployments/$DEPLOYMENT_ID \
  -H "Authorization: Bearer $TOKEN"
```

### Rolling Back a Deployment

```bash
# Automatic rollback (triggered by health checks)
# Manual rollback
curl -X POST https://api.example.com/api/v1/plugin-deployments/$DEPLOYMENT_ID/rollback \
  -H "Authorization: Bearer $TOKEN" \
  -d '{"reason": "High error rate detected"}'
```

---

## Troubleshooting

### Common Issues

#### 1. Plugin Deployment Fails

**Symptoms:** Deployment status shows "Failed"

**Diagnosis:**
```bash
# Check deployment logs
kubectl logs -f deployment/plugin-manager -n plugin-manager | grep $DEPLOYMENT_ID

# Check plugin instances
kubectl get pods -n tenant-acme -l plugin=payment-stripe

# Check health checks
curl https://api.example.com/api/v1/plugin-deployments/$DEPLOYMENT_ID/health \
  -H "Authorization: Bearer $TOKEN"
```

**Common Causes:**
- Dependency conflicts
- Resource quota exceeded
- Health check failures
- Network connectivity issues

**Resolution:**
```bash
# Check tenant quotas
curl https://api.example.com/api/v1/tenants/tenant-acme-corp \
  -H "Authorization: Bearer $TOKEN"

# Validate dependencies
curl https://api.example.com/api/v1/plugin-deployments/validate-dependencies \
  -H "Authorization: Bearer $TOKEN" \
  -d '{"pluginId": "payment-stripe", "pluginVersion": "2.0.0", "tenantId": "tenant-acme-corp"}'
```

#### 2. Tenant Isolation Breach

**Symptoms:** Cross-tenant data access detected

**Diagnosis:**
```bash
# Check network policies
kubectl get networkpolicies -n tenant-acme

# Check pod labels and selectors
kubectl get pods -n tenant-acme --show-labels

# Review audit logs
kubectl logs -f deployment/plugin-manager -n plugin-manager | grep "SECURITY_VIOLATION"
```

**Resolution:**
```bash
# Recreate network policies
kubectl apply -f k8s/network-policies.yaml

# Verify isolation
kubectl exec -it <pod-name> -n tenant-acme -- curl http://tenant-other-service
# Should fail with connection timeout
```

#### 3. Performance Degradation

**Symptoms:** Slow API responses, high latency

**Diagnosis:**
```bash
# Check resource usage
kubectl top pods -n plugin-manager
kubectl top nodes

# Check database performance
kubectl exec -it postgresql-0 -n plugin-manager -- \
  psql -U postgres -d pluginmanager \
  -c "SELECT * FROM pg_stat_activity WHERE state = 'active';"

# Check Redis performance
kubectl exec -it redis-master-0 -n plugin-manager -- \
  redis-cli INFO stats
```

**Resolution:**
```bash
# Scale horizontally
kubectl scale deployment plugin-manager --replicas=5 -n plugin-manager

# Add database read replicas
helm upgrade postgresql bitnami/postgresql \
  --set replication.enabled=true \
  --set replication.readReplicas=2

# Enable caching
# Update ConfigMap to enable aggressive caching
```

---

## Monitoring

### Prometheus Metrics

**Key Metrics to Monitor:**

```prometheus
# Plugin deployments
plugin_deployments_total{status="completed"}
plugin_deployments_total{status="failed"}
plugin_deployments_duration_seconds

# Plugin health
plugin_health_check_total{status="healthy"}
plugin_health_check_total{status="unhealthy"}
plugin_health_check_duration_seconds

# Tenant metrics
tenants_active_total
plugins_per_tenant
resource_usage{tenant="tenant-acme-corp",resource="cpu"}

# System metrics
api_requests_total{endpoint="/api/v1/plugins",status="200"}
api_request_duration_seconds
```

### Grafana Dashboards

**Import Dashboard:**
```bash
# Import pre-built dashboard
kubectl apply -f k8s/grafana-dashboard.json
```

**Key Panels:**
1. Plugin Deployment Success Rate
2. Average Deployment Duration
3. Plugin Health Status
4. Tenant Resource Usage
5. API Response Times
6. Error Rates

### Alerting Rules

**Prometheus alerting rules:**
```yaml
groups:
  - name: plugin_manager_alerts
    rules:
      - alert: HighDeploymentFailureRate
        expr: rate(plugin_deployments_total{status="failed"}[5m]) > 0.1
        for: 5m
        annotations:
          summary: "High plugin deployment failure rate"
          
      - alert: PluginUnhealthy
        expr: plugin_health_check_total{status="unhealthy"} > 0
        for: 2m
        annotations:
          summary: "Plugin unhealthy for 2+ minutes"
          
      - alert: TenantQuotaExceeded
        expr: tenant_resource_usage / tenant_quota > 0.9
        annotations:
          summary: "Tenant approaching resource quota"
```

### Log Aggregation

**Fluent Bit configuration:**
```yaml
[INPUT]
    Name tail
    Path /var/log/containers/plugin-manager*.log
    Parser docker
    Tag plugin.manager

[FILTER]
    Name grep
    Match plugin.manager
    Regex log (ERROR|WARNING|SECURITY_VIOLATION)

[OUTPUT]
    Name elasticsearch
    Match *
    Host elasticsearch
    Port 9200
```

---

## Backup and Disaster Recovery

### Database Backup

```bash
# Automated daily backups
kubectl create -f k8s/cronjob-backup.yaml

# Manual backup
kubectl exec -it postgresql-0 -n plugin-manager -- \
  pg_dump -U postgres pluginmanager > backup-$(date +%Y%m%d).sql
```

### Plugin Binary Backup

```bash
# MinIO backup
mc mirror myminio/plugins /backup/plugins

# Automated backup to S3
mc mirror myminio/plugins s3/backup-bucket/plugins
```

### Disaster Recovery

```bash
# Restore database
kubectl exec -i postgresql-0 -n plugin-manager -- \
  psql -U postgres pluginmanager < backup-20251123.sql

# Restore plugin binaries
mc mirror /backup/plugins myminio/plugins

# Verify system health
kubectl get pods -n plugin-manager
curl https://api.example.com/health
```

---

## Scaling

### Horizontal Scaling

```bash
# Scale API servers
kubectl scale deployment plugin-manager --replicas=10 -n plugin-manager

# Scale database
helm upgrade postgresql bitnami/postgresql \
  --set replication.readReplicas=3

# Scale Redis cluster
helm upgrade redis bitnami/redis \
  --set cluster.enabled=true \
  --set cluster.slaveCount=3
```

### Auto-Scaling

**HorizontalPodAutoscaler:**
```yaml
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: plugin-manager-hpa
  namespace: plugin-manager
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: plugin-manager
  minReplicas: 3
  maxReplicas: 20
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
          averageUtilization: 75
```

---

## Security Hardening

### Network Policies

```yaml
apiVersion: networking.k8s.io/v1
kind: NetworkPolicy
metadata:
  name: tenant-isolation
  namespace: tenants
spec:
  podSelector:
    matchLabels:
      tenant: acme-corp
  policyTypes:
    - Ingress
    - Egress
  ingress:
    - from:
        - namespaceSelector:
            matchLabels:
              name: plugin-manager
  egress:
    - to:
        - podSelector:
            matchLabels:
              app: postgresql
```

### Pod Security Standards

```yaml
apiVersion: v1
kind: Namespace
metadata:
  name: tenants
  labels:
    pod-security.kubernetes.io/enforce: restricted
    pod-security.kubernetes.io/audit: restricted
    pod-security.kubernetes.io/warn: restricted
```

---

**Last Updated:** 2025-11-23
**Version:** 1.0.0
**Support:** devops@example.com
