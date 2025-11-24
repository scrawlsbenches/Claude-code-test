# Deployment Guide - Research Cluster Configuration Manager

**Version:** 1.0.0
**Last Updated:** 2025-11-23

---

## Prerequisites

### Hardware Requirements

**Minimum (Development):**
- 4 CPU cores
- 16 GB RAM
- 100 GB SSD storage
- 1 Gbps network

**Recommended (Production - 100-node cluster):**
- 16 CPU cores (API servers)
- 64 GB RAM
- 500 GB SSD storage
- 10 Gbps network

### Software Requirements

- **HPC Scheduler**: Slurm 22+, PBS Pro 2021+, or SGE 8.1+
- **PostgreSQL**: 15+
- **Redis**: 7+
- **MinIO**: 2023+ or AWS S3
- **.NET Runtime**: 8.0+
- **Docker**: 24+ (optional, for containerized workflows)

---

## Kubernetes Deployment

### Step 1: Create Namespace
```bash
kubectl create namespace research-cluster
kubectl config set-context --current --namespace=research-cluster
```

### Step 2: Deploy PostgreSQL
```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: postgres
spec:
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
          value: researchcluster
        - name: POSTGRES_USER
          value: rcadmin
        - name: POSTGRES_PASSWORD
          valueFrom:
            secretKeyRef:
              name: postgres-secret
              key: password
```

### Step 3: Deploy Research Cluster API
```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: research-cluster-api
spec:
  replicas: 3
  selector:
    matchLabels:
      app: research-cluster-api
  template:
    spec:
      containers:
      - name: api
        image: your-registry/research-cluster-api:latest
        env:
        - name: ConnectionStrings__DefaultConnection
          value: "Host=postgres;Database=researchcluster;Username=rcadmin;Password=$(POSTGRES_PASSWORD)"
        - name: Slurm__Endpoint
          value: "https://slurm-controller:6817"
```

---

## Integration with HPC Scheduler

### Slurm Integration

**Configuration:**
```json
{
  "Slurm": {
    "Endpoint": "https://slurm-controller:6817",
    "AuthType": "JWT",
    "QueueCheckInterval": 30,
    "MetricsInterval": 60
  }
}
```

**Test Slurm Connection:**
```bash
# From API server
squeue --clusters=all
sbatch --test-only test-job.sh
```

### PBS Integration

**Configuration:**
```json
{
  "PBS": {
    "Endpoint": "pbs-server.local",
    "QueueCheckInterval": 30
  }
}
```

---

## Monitoring & Observability

### Prometheus Metrics

```yaml
# ServiceMonitor for Prometheus
apiVersion: monitoring.coreos.com/v1
kind: ServiceMonitor
metadata:
  name: research-cluster-metrics
spec:
  selector:
    matchLabels:
      app: research-cluster-api
  endpoints:
  - port: metrics
    path: /metrics
```

### Grafana Dashboards

Import dashboards:
- `grafana/dashboards/research-cluster-overview.json`
- `grafana/dashboards/job-metrics.json`
- `grafana/dashboards/cost-tracking.json`

---

## Backup & Recovery

### Database Backup
```bash
# Automated daily backups
kubectl create cronjob postgres-backup \
  --image=postgres:15-alpine \
  --schedule="0 2 * * *" \
  -- pg_dump -h postgres -U rcadmin researchcluster > /backup/rc-$(date +%Y%m%d).sql
```

---

## Scaling

### Horizontal Scaling
```bash
kubectl scale deployment research-cluster-api --replicas=5
```

### Auto-Scaling
```yaml
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: research-cluster-api-hpa
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: research-cluster-api
  minReplicas: 3
  maxReplicas: 10
  metrics:
  - type: Resource
    resource:
      name: cpu
      target:
        type: Utilization
        averageUtilization: 70
```

---

## Troubleshooting

### Issue: Jobs not submitting to Slurm

**Symptoms:** Jobs stuck in "Queued" status

**Solution:**
```bash
# Check Slurm connection
sinfo
squeue

# Check API logs
kubectl logs deployment/research-cluster-api | grep Slurm
```

### Issue: High API latency

**Symptoms:** API response time > 1s

**Solution:**
```bash
# Increase API replicas
kubectl scale deployment research-cluster-api --replicas=5

# Check database connection pool
kubectl logs deployment/research-cluster-api | grep "connection pool"
```

---

**Document Version:** 1.0.0
**Last Updated:** 2025-11-23
