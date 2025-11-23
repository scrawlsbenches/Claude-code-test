# HotSwap Distributed Kernel Orchestration Helm Chart

This Helm chart deploys the HotSwap Distributed Kernel Orchestration System to Kubernetes.

## Prerequisites

- Kubernetes 1.26+
- Helm 3.0+
- PV provisioner support in the underlying infrastructure (if using built-in PostgreSQL)
- HashiCorp Vault instance (self-hosted or external)

## Installation

### Quick Start

```bash
# Add the HotSwap Helm repository (if published)
helm repo add hotswap https://charts.hotswap.example.com
helm repo update

# Install with default values
helm install hotswap hotswap/hotswap

# Or install from local directory
helm install hotswap ./helm/hotswap
```

### Custom Installation

```bash
# Install with custom values
helm install hotswap ./helm/hotswap \
  --set replicaCount=5 \
  --set image.tag=v1.0.0 \
  --set ingress.enabled=true \
  --set ingress.hosts[0].host=hotswap.example.com

# Install with values file
helm install hotswap ./helm/hotswap -f custom-values.yaml
```

### Production Installation Example

```bash
helm install hotswap ./helm/hotswap \
  --namespace hotswap \
  --create-namespace \
  --values production-values.yaml \
  --set image.tag=v1.0.0 \
  --set postgresql.auth.password=$(openssl rand -base64 32) \
  --set vault.token=$(cat /path/to/vault-token.txt)
```

## Configuration

### Key Configuration Parameters

| Parameter | Description | Default |
|-----------|-------------|---------|
| `replicaCount` | Number of replicas | `3` |
| `image.repository` | Container image repository | `hotswap/hotswap-api` |
| `image.tag` | Container image tag | `""` (uses appVersion) |
| `image.pullPolicy` | Image pull policy | `IfNotPresent` |
| `service.type` | Kubernetes service type | `ClusterIP` |
| `ingress.enabled` | Enable ingress controller | `false` |
| `autoscaling.enabled` | Enable HPA | `true` |
| `autoscaling.minReplicas` | Minimum replicas | `3` |
| `autoscaling.maxReplicas` | Maximum replicas | `10` |
| `postgresql.enabled` | Use built-in PostgreSQL | `true` |
| `vault.address` | Vault server address | `http://vault:8200` |
| `vault.authMethod` | Vault auth method | `kubernetes` |

### Resource Configuration

```yaml
resources:
  limits:
    cpu: 2000m
    memory: 4Gi
  requests:
    cpu: 500m
    memory: 1Gi
```

### Database Configuration

#### Using Built-in PostgreSQL

```yaml
postgresql:
  enabled: true
  auth:
    username: hotswap
    password: "your-secure-password"
    database: hotswap
  primary:
    persistence:
      enabled: true
      size: 20Gi
```

#### Using External PostgreSQL

```yaml
postgresql:
  enabled: false

externalDatabase:
  host: postgresql.example.com
  port: 5432
  database: hotswap
  username: hotswap
  existingSecret: hotswap-db-secret
  existingSecretPasswordKey: password
```

### Vault Configuration

#### Kubernetes Auth (Recommended)

```yaml
vault:
  address: http://vault:8200
  authMethod: kubernetes
  role: hotswap
```

#### Token Auth

```yaml
vault:
  address: http://vault:8200
  authMethod: token
  token: "s.VAULT_TOKEN_HERE"
```

#### AppRole Auth

```yaml
vault:
  address: http://vault:8200
  authMethod: approle
  appRole:
    roleId: "role-id-here"
    secretId: "secret-id-here"
```

### Ingress Configuration

#### Basic Ingress

```yaml
ingress:
  enabled: true
  className: nginx
  hosts:
    - host: hotswap.example.com
      paths:
        - path: /
          pathType: Prefix
```

#### Ingress with TLS

```yaml
ingress:
  enabled: true
  className: nginx
  annotations:
    cert-manager.io/cluster-issuer: "letsencrypt-prod"
  hosts:
    - host: hotswap.example.com
      paths:
        - path: /
          pathType: Prefix
  tls:
    - secretName: hotswap-tls
      hosts:
        - hotswap.example.com
```

### Autoscaling Configuration

```yaml
autoscaling:
  enabled: true
  minReplicas: 3
  maxReplicas: 10
  targetCPUUtilizationPercentage: 70
  targetMemoryUtilizationPercentage: 80
```

## Upgrading

```bash
# Upgrade to a new version
helm upgrade hotswap ./helm/hotswap \
  --set image.tag=v1.1.0 \
  --reuse-values

# Upgrade with new values file
helm upgrade hotswap ./helm/hotswap -f updated-values.yaml
```

## Rollback

```bash
# List release history
helm history hotswap

# Rollback to previous version
helm rollback hotswap

# Rollback to specific revision
helm rollback hotswap 2
```

## Uninstallation

```bash
# Uninstall the release
helm uninstall hotswap

# Uninstall and delete namespace
helm uninstall hotswap --namespace hotswap
kubectl delete namespace hotswap
```

## Monitoring and Debugging

### View Pod Status

```bash
kubectl get pods -l app.kubernetes.io/name=hotswap
```

### View Logs

```bash
# View logs from all pods
kubectl logs -l app.kubernetes.io/name=hotswap -f

# View logs from specific pod
kubectl logs hotswap-6d7b5f8c9d-abcde -f
```

### Check Health

```bash
# Port forward to access health endpoint
kubectl port-forward svc/hotswap 8080:80

# Check health
curl http://localhost:8080/health
```

### Access Metrics

```bash
# Port forward to metrics endpoint
kubectl port-forward svc/hotswap 5000:5000

# View Prometheus metrics
curl http://localhost:5000/metrics
```

### Debug Pod Issues

```bash
# Describe pod
kubectl describe pod hotswap-6d7b5f8c9d-abcde

# Get pod events
kubectl get events --field-selector involvedObject.name=hotswap-6d7b5f8c9d-abcde

# Execute command in pod
kubectl exec -it hotswap-6d7b5f8c9d-abcde -- /bin/sh
```

## Testing

### Lint the Chart

```bash
helm lint ./helm/hotswap
```

### Dry Run Installation

```bash
helm install hotswap ./helm/hotswap \
  --dry-run \
  --debug \
  --values test-values.yaml
```

### Template Rendering

```bash
# Render templates locally
helm template hotswap ./helm/hotswap

# Render with custom values
helm template hotswap ./helm/hotswap -f custom-values.yaml
```

### Integration Testing

```bash
# Install to test namespace
helm install hotswap-test ./helm/hotswap \
  --namespace hotswap-test \
  --create-namespace \
  --wait \
  --timeout 5m

# Run smoke tests
kubectl run -it --rm test-client \
  --image=curlimages/curl:latest \
  --restart=Never \
  --namespace hotswap-test \
  -- curl http://hotswap-test:80/health

# Cleanup
helm uninstall hotswap-test --namespace hotswap-test
kubectl delete namespace hotswap-test
```

## Production Deployment Checklist

- [ ] Update `image.tag` to specific version (not `latest`)
- [ ] Configure external PostgreSQL database
- [ ] Set strong database password via secret
- [ ] Configure Vault authentication (prefer Kubernetes auth)
- [ ] Enable ingress with TLS
- [ ] Configure resource requests and limits
- [ ] Enable autoscaling (HPA)
- [ ] Configure pod disruption budget
- [ ] Set appropriate replica count (minimum 3)
- [ ] Configure affinity rules for pod spreading
- [ ] Enable Prometheus metrics and ServiceMonitor
- [ ] Set up backup for PostgreSQL (if using built-in)
- [ ] Configure CORS origins for production domains
- [ ] Review and adjust rate limiting settings
- [ ] Test rollback procedures
- [ ] Document disaster recovery procedures

## Security Considerations

1. **Secrets Management:**
   - Never commit secrets to version control
   - Use Kubernetes secrets or external secret managers
   - Rotate secrets regularly using HashiCorp Vault

2. **Network Policies:**
   - Implement network policies to restrict pod communication
   - Use ingress controller with WAF capabilities

3. **Pod Security:**
   - Run as non-root user (enforced by default)
   - Read-only root filesystem (enforced by default)
   - Drop all capabilities (enforced by default)

4. **TLS/SSL:**
   - Always use TLS in production
   - Use cert-manager for automatic certificate management
   - Configure HSTS headers

5. **Image Security:**
   - Use specific image tags, not `latest`
   - Scan images for vulnerabilities
   - Use private container registry

## Troubleshooting

### Pods Not Starting

```bash
# Check pod events
kubectl describe pod <pod-name>

# Common issues:
# - ImagePullBackOff: Check image repository and credentials
# - CrashLoopBackOff: Check logs and liveness probe configuration
# - Pending: Check resource requests and node capacity
```

### Database Connection Issues

```bash
# Verify database credentials
kubectl get secret hotswap-postgresql -o jsonpath='{.data.password}' | base64 -d

# Test database connection
kubectl exec -it deployment/hotswap -- /bin/sh
# Inside pod:
# psql -h hotswap-postgresql -U hotswap -d hotswap
```

### Vault Connection Issues

```bash
# Check Vault status
kubectl exec -it deployment/hotswap -- env | grep VAULT

# Verify Vault authentication
# For Kubernetes auth: Check serviceAccount configuration
# For token auth: Verify token secret exists
```

### High Memory Usage

```bash
# Check memory consumption
kubectl top pods -l app.kubernetes.io/name=hotswap

# Adjust memory limits if needed
helm upgrade hotswap ./helm/hotswap \
  --set resources.limits.memory=8Gi \
  --reuse-values
```

## Support

For issues and questions:
- GitHub Issues: https://github.com/yourorg/hotswap-distributed/issues
- Documentation: https://github.com/yourorg/hotswap-distributed
- Email: support@hotswap.example.com

## License

See [LICENSE](../../LICENSE) for details.
