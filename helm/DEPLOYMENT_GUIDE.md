# HotSwap Kubernetes Deployment Guide

This guide provides complete instructions for deploying the HotSwap Distributed Kernel Orchestration System to Kubernetes using Helm.

## Table of Contents

1. [Prerequisites](#prerequisites)
2. [Quick Start](#quick-start)
3. [Environment-Specific Deployments](#environment-specific-deployments)
4. [Post-Deployment Verification](#post-deployment-verification)
5. [Configuration](#configuration)
6. [Troubleshooting](#troubleshooting)
7. [Maintenance](#maintenance)

## Prerequisites

### Required Tools

- **Kubernetes Cluster:** Version 1.26 or higher
- **Helm:** Version 3.0 or higher
- **kubectl:** Configured to access your cluster
- **HashiCorp Vault:** Self-hosted instance configured and accessible

### Installation

#### Install Helm

```bash
# Linux/macOS
curl https://raw.githubusercontent.com/helm/helm/main/scripts/get-helm-3 | bash

# Verify installation
helm version
```

#### Install kubectl

```bash
# Linux
curl -LO "https://dl.k8s.io/release/$(curl -L -s https://dl.k8s.io/release/stable.txt)/bin/linux/amd64/kubectl"
sudo install -o root -g root -m 0755 kubectl /usr/local/bin/kubectl

# Verify installation
kubectl version --client
```

### Cluster Requirements

- **Minimum Nodes:** 3 (for high availability)
- **Node Resources:** 4 CPU cores, 8GB RAM per node
- **Storage Class:** Available for PostgreSQL persistence
- **Ingress Controller:** nginx-ingress-controller (recommended)
- **Cert Manager:** For automatic TLS certificate management (optional)

## Quick Start

### 1. Create Namespace

```bash
kubectl create namespace hotswap
kubectl config set-context --current --namespace=hotswap
```

### 2. Create Secrets

#### Database Password

```bash
kubectl create secret generic hotswap-db-secret \
  --from-literal=password=$(openssl rand -base64 32) \
  --namespace=hotswap
```

#### Vault Token (if using token auth)

```bash
kubectl create secret generic hotswap-vault-token \
  --from-literal=token=s.YOUR_VAULT_TOKEN_HERE \
  --namespace=hotswap
```

### 3. Deploy with Helm

```bash
# Clone the repository
git clone https://github.com/yourorg/hotswap-distributed.git
cd hotswap-distributed

# Install the chart
helm install hotswap ./helm/hotswap \
  --namespace hotswap \
  --set image.tag=v1.0.0 \
  --wait \
  --timeout 10m
```

### 4. Verify Deployment

```bash
# Check pod status
kubectl get pods -n hotswap

# Check service status
kubectl get svc -n hotswap

# View logs
kubectl logs -f deployment/hotswap -n hotswap
```

## Environment-Specific Deployments

### Development Environment

```bash
helm install hotswap-dev ./helm/hotswap \
  --namespace hotswap-dev \
  --create-namespace \
  --values ./helm/hotswap/values-dev.yaml \
  --set image.tag=latest
```

**Characteristics:**
- Single replica
- In-memory PostgreSQL (no persistence)
- Debug logging enabled
- No autoscaling
- Port-forward access (no ingress)

**Access the application:**

```bash
kubectl port-forward svc/hotswap-dev 8080:80 -n hotswap-dev
# Visit http://localhost:8080/health
```

### Staging Environment

```bash
# Create secrets first
kubectl create namespace hotswap-staging
kubectl create secret generic hotswap-staging-db-secret \
  --from-literal=password=$(openssl rand -base64 32) \
  --namespace=hotswap-staging

# Deploy
helm install hotswap-staging ./helm/hotswap \
  --namespace hotswap-staging \
  --values ./helm/hotswap/values-staging.yaml \
  --set image.tag=v1.0.0-rc.1 \
  --wait
```

**Characteristics:**
- 2-5 replicas (autoscaling enabled)
- External PostgreSQL database
- Kubernetes Vault auth
- Ingress with staging TLS certificate
- Prometheus monitoring enabled

**Access the application:**

```bash
# Via ingress
curl https://staging.hotswap.example.com/health
```

### Production Environment

```bash
# Create namespace
kubectl create namespace hotswap-production

# Create database secret
kubectl create secret generic hotswap-prod-db-secret \
  --from-literal=password=YOUR_PRODUCTION_PASSWORD \
  --namespace=hotswap-production

# Verify Vault is accessible
kubectl run vault-test --rm -it --image=curlimages/curl:latest \
  --restart=Never \
  --namespace=hotswap-production \
  -- curl -k https://vault-prod.example.com/v1/sys/health

# Deploy
helm install hotswap ./helm/hotswap \
  --namespace hotswap-production \
  --values ./helm/hotswap/values-production.yaml \
  --set image.tag=v1.0.0 \
  --wait \
  --timeout 15m

# Verify deployment
kubectl rollout status deployment/hotswap -n hotswap-production
```

**Characteristics:**
- 5-20 replicas (aggressive autoscaling)
- External managed PostgreSQL
- Kubernetes Vault auth with production role
- Production TLS certificates
- Pod anti-affinity (spread across nodes)
- Dedicated node pools
- ServiceMonitor for Prometheus Operator

## Post-Deployment Verification

### 1. Health Checks

```bash
# Check application health
kubectl exec -it deployment/hotswap -n hotswap -- \
  curl http://localhost:5000/health

# Expected output:
# {"status":"Healthy","components":{"database":"Healthy","vault":"Healthy"}}
```

### 2. Database Connectivity

```bash
# Check database connection from pod
kubectl exec -it deployment/hotswap -n hotswap -- /bin/sh

# Inside pod:
# Test connection (if psql is available)
psql $ConnectionStrings__DefaultConnection -c "SELECT version();"
```

### 3. Vault Connectivity

```bash
kubectl exec -it deployment/hotswap -n hotswap -- env | grep VAULT
# Verify VAULT_ADDR and authentication variables are set
```

### 4. Metrics Validation

```bash
# Port-forward to metrics endpoint
kubectl port-forward svc/hotswap 5000:5000 -n hotswap

# Check Prometheus metrics
curl http://localhost:5000/metrics

# Verify custom metrics exist:
# - hotswap_deployments_started_total
# - hotswap_deployments_succeeded_total
# - hotswap_deployments_active
```

### 5. Smoke Tests

```bash
# Create a test deployment request
kubectl run test-client --rm -it --image=curlimages/curl:latest \
  --restart=Never \
  --namespace=hotswap \
  -- curl -X POST http://hotswap/api/v1/deployments \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -d '{
    "moduleName": "test-module",
    "moduleVersion": "1.0.0",
    "targetEnvironment": "Development"
  }'
```

## Configuration

### Custom Configuration

Create a custom values file:

```yaml
# custom-values.yaml
replicaCount: 4

resources:
  limits:
    cpu: 3000m
    memory: 6Gi
  requests:
    cpu: 1000m
    memory: 2Gi

config:
  rateLimiting:
    requestsPerMinute: 500
    requestsPerHour: 5000

vault:
  address: https://vault.example.com
  role: hotswap-custom
```

Deploy with custom configuration:

```bash
helm install hotswap ./helm/hotswap \
  --namespace hotswap \
  --values custom-values.yaml \
  --set image.tag=v1.0.0
```

### Override Values via Command Line

```bash
helm install hotswap ./helm/hotswap \
  --namespace hotswap \
  --set replicaCount=7 \
  --set resources.limits.memory=8Gi \
  --set autoscaling.maxReplicas=25 \
  --set image.tag=v1.0.0
```

## Troubleshooting

### Pods Not Starting

```bash
# Check pod events
kubectl describe pod <pod-name> -n hotswap

# Common issues and solutions:
# 1. ImagePullBackOff
kubectl get pods -n hotswap  # Check if image exists
# Solution: Verify image.repository and image.tag in values

# 2. CrashLoopBackOff
kubectl logs <pod-name> -n hotswap --previous
# Solution: Check application logs for startup errors

# 3. Pending (insufficient resources)
kubectl describe node
# Solution: Scale cluster or reduce resource requests
```

### Database Connection Failures

```bash
# Verify secret exists
kubectl get secret hotswap-db-secret -n hotswap -o yaml

# Check database password
kubectl get secret hotswap-db-secret -n hotswap \
  -o jsonpath='{.data.password}' | base64 -d

# Test database connectivity
kubectl run psql-test --rm -it --image=postgres:15 \
  --restart=Never \
  --namespace=hotswap \
  --env="PGPASSWORD=YOUR_PASSWORD" \
  -- psql -h postgres.example.com -U hotswap -d hotswap -c "SELECT 1;"
```

### Vault Authentication Issues

```bash
# Check Vault address accessibility
kubectl run vault-test --rm -it --image=curlimages/curl:latest \
  --restart=Never \
  --namespace=hotswap \
  -- curl -k $VAULT_ADDR/v1/sys/health

# For Kubernetes auth, verify service account
kubectl get serviceaccount hotswap -n hotswap -o yaml

# Check Vault role configuration
kubectl exec -it deployment/hotswap -n hotswap -- env | grep VAULT
```

### Ingress Not Working

```bash
# Check ingress status
kubectl get ingress -n hotswap
kubectl describe ingress hotswap -n hotswap

# Verify ingress controller is running
kubectl get pods -n ingress-nginx

# Check TLS certificate
kubectl get certificate -n hotswap
kubectl describe certificate hotswap-tls -n hotswap
```

## Maintenance

### Upgrading

```bash
# Upgrade to new version
helm upgrade hotswap ./helm/hotswap \
  --namespace hotswap \
  --set image.tag=v1.1.0 \
  --reuse-values \
  --wait

# Rollback if needed
helm rollback hotswap --namespace hotswap
```

### Scaling

```bash
# Manual scaling (if autoscaling disabled)
kubectl scale deployment hotswap --replicas=10 -n hotswap

# Update autoscaling limits
helm upgrade hotswap ./helm/hotswap \
  --namespace hotswap \
  --set autoscaling.maxReplicas=30 \
  --reuse-values
```

### Backup

```bash
# Backup PostgreSQL (if using built-in)
kubectl exec -it hotswap-postgresql-0 -n hotswap -- \
  pg_dump -U hotswap hotswap > hotswap-backup-$(date +%Y%m%d).sql

# Backup Helm values
helm get values hotswap -n hotswap > hotswap-values-backup.yaml
```

### Monitoring

```bash
# View metrics in Prometheus
kubectl port-forward svc/prometheus 9090:9090 -n monitoring
# Visit http://localhost:9090

# View Grafana dashboards
kubectl port-forward svc/grafana 3000:3000 -n monitoring
# Visit http://localhost:3000

# Check HPA status
kubectl get hpa -n hotswap
kubectl describe hpa hotswap -n hotswap
```

### Logs

```bash
# View all pod logs
kubectl logs -f deployment/hotswap -n hotswap --all-containers=true

# View logs from specific time
kubectl logs deployment/hotswap -n hotswap --since=1h

# Export logs to file
kubectl logs deployment/hotswap -n hotswap --all-containers=true > hotswap-logs.txt
```

## Uninstallation

### Remove Application

```bash
# Uninstall Helm release
helm uninstall hotswap --namespace hotswap

# Delete namespace (removes all resources)
kubectl delete namespace hotswap
```

### Cleanup PVCs (if using built-in PostgreSQL)

```bash
# List PVCs
kubectl get pvc -n hotswap

# Delete PVCs
kubectl delete pvc data-hotswap-postgresql-0 -n hotswap
```

## Additional Resources

- [Helm Chart README](./hotswap/README.md)
- [Operations Runbooks](../docs/runbooks/)
- [Troubleshooting Guide](../docs/runbooks/TROUBLESHOOTING_GUIDE.md)
- [Disaster Recovery](../docs/runbooks/DISASTER_RECOVERY.md)
- [Load Testing Guide](../tests/LoadTests/README.md)

## Support

For issues and questions:
- GitHub Issues: https://github.com/yourorg/hotswap-distributed/issues
- Documentation: https://github.com/yourorg/hotswap-distributed
- Email: support@hotswap.example.com
