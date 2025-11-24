# Deployment Guide - Educational Lab Environment Manager

**Version:** 1.0.0
**Last Updated:** 2025-11-23
**Target Audience:** Platform Operators, DevOps Engineers

---

## Table of Contents

1. [Overview](#overview)
2. [Prerequisites](#prerequisites)
3. [Deployment Options](#deployment-options)
4. [Kubernetes Deployment](#kubernetes-deployment)
5. [Docker Compose Deployment](#docker-compose-deployment)
6. [Configuration](#configuration)
7. [Database Setup](#database-setup)
8. [Storage Setup](#storage-setup)
9. [Monitoring & Observability](#monitoring--observability)
10. [Backup & Recovery](#backup--recovery)
11. [Scaling](#scaling)
12. [Security Hardening](#security-hardening)
13. [Troubleshooting](#troubleshooting)

---

## Overview

The Educational Lab Environment Manager can be deployed in multiple configurations:

1. **Kubernetes (Production)** - Recommended for large deployments (1,000+ students)
2. **Docker Compose (Development/Small)** - Suitable for development or small courses (< 100 students)
3. **Hybrid** - API on Kubernetes, student environments on separate Docker hosts

### Architecture

```
┌─────────────────────────────────────────────────────────┐
│                   Load Balancer (Nginx)                  │
│                    (TLS Termination)                     │
└────────────────────────┬────────────────────────────────┘
                         │
         ┌───────────────┴───────────────┐
         │                               │
┌────────▼─────────┐          ┌──────────▼─────────┐
│  API Server      │          │  Web IDE Proxy     │
│  (ASP.NET Core)  │          │  (Nginx/Traefik)   │
└────────┬─────────┘          └──────────┬─────────┘
         │                               │
    ┌────┴────┐                    ┌─────▼──────┐
    │         │                    │            │
┌───▼──┐  ┌──▼────┐          ┌────▼────┐  ┌────▼────┐
│ PG   │  │ Redis │          │ Student │  │ Student │
│ SQL  │  │       │          │ Env 1   │  │ Env 2   │
└──────┘  └───────┘          │ (Docker)│  │ (Docker)│
                             └─────────┘  └─────────┘
```

---

## Prerequisites

### Hardware Requirements

**Minimum (Development):**
- 4 CPU cores
- 16 GB RAM
- 100 GB SSD storage
- 1 Gbps network

**Recommended (Production - 1,000 students):**
- 32 CPU cores (across multiple nodes)
- 128 GB RAM
- 1 TB SSD storage
- 10 Gbps network

### Software Requirements

- **Docker**: 24.0+ or **Podman**: 4.0+
- **Kubernetes**: 1.28+ (if using K8s deployment)
- **PostgreSQL**: 15+
- **Redis**: 7+
- **MinIO**: 2023+ or AWS S3
- **.NET Runtime**: 8.0+

---

## Deployment Options

### Option 1: Kubernetes (Production)

**Pros:**
- Auto-scaling
- High availability
- Resource isolation
- Multi-node support

**Cons:**
- Complex setup
- Higher operational overhead

**Use Cases:**
- Large courses (1,000+ students)
- Multiple courses
- Institution-wide deployment

---

### Option 2: Docker Compose (Development/Small)

**Pros:**
- Simple setup
- Easy to understand
- Fast deployment

**Cons:**
- Single-node only
- Manual scaling
- Limited HA options

**Use Cases:**
- Development environment
- Small courses (< 100 students)
- Testing

---

## Kubernetes Deployment

### Step 1: Create Namespace

```bash
kubectl create namespace lab-manager
kubectl config set-context --current --namespace=lab-manager
```

### Step 2: Deploy PostgreSQL

```yaml
# postgres.yaml
apiVersion: v1
kind: PersistentVolumeClaim
metadata:
  name: postgres-pvc
spec:
  accessModes:
    - ReadWriteOnce
  resources:
    requests:
      storage: 100Gi
---
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
          value: labmanager
        - name: POSTGRES_USER
          value: labadmin
        - name: POSTGRES_PASSWORD
          valueFrom:
            secretKeyRef:
              name: postgres-secret
              key: password
        ports:
        - containerPort: 5432
        volumeMounts:
        - name: postgres-storage
          mountPath: /var/lib/postgresql/data
      volumes:
      - name: postgres-storage
        persistentVolumeClaim:
          claimName: postgres-pvc
---
apiVersion: v1
kind: Service
metadata:
  name: postgres
spec:
  selector:
    app: postgres
  ports:
  - port: 5432
    targetPort: 5432
```

```bash
kubectl apply -f postgres.yaml
```

### Step 3: Deploy Redis

```yaml
# redis.yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: redis
spec:
  replicas: 1
  selector:
    matchLabels:
      app: redis
  template:
    metadata:
      labels:
        app: redis
    spec:
      containers:
      - name: redis
        image: redis:7-alpine
        ports:
        - containerPort: 6379
---
apiVersion: v1
kind: Service
metadata:
  name: redis
spec:
  selector:
    app: redis
  ports:
  - port: 6379
    targetPort: 6379
```

```bash
kubectl apply -f redis.yaml
```

### Step 4: Deploy MinIO (Object Storage)

```yaml
# minio.yaml
apiVersion: v1
kind: PersistentVolumeClaim
metadata:
  name: minio-pvc
spec:
  accessModes:
    - ReadWriteOnce
  resources:
    requests:
      storage: 500Gi
---
apiVersion: apps/v1
kind: Deployment
metadata:
  name: minio
spec:
  replicas: 1
  selector:
    matchLabels:
      app: minio
  template:
    metadata:
      labels:
        app: minio
    spec:
      containers:
      - name: minio
        image: minio/minio:latest
        args:
        - server
        - /data
        - --console-address
        - ":9001"
        env:
        - name: MINIO_ROOT_USER
          value: minioadmin
        - name: MINIO_ROOT_PASSWORD
          valueFrom:
            secretKeyRef:
              name: minio-secret
              key: password
        ports:
        - containerPort: 9000
        - containerPort: 9001
        volumeMounts:
        - name: minio-storage
          mountPath: /data
      volumes:
      - name: minio-storage
        persistentVolumeClaim:
          claimName: minio-pvc
---
apiVersion: v1
kind: Service
metadata:
  name: minio
spec:
  selector:
    app: minio
  ports:
  - name: api
    port: 9000
    targetPort: 9000
  - name: console
    port: 9001
    targetPort: 9001
```

```bash
kubectl apply -f minio.yaml
```

### Step 5: Deploy Lab Manager API

```yaml
# labmanager-api.yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: labmanager-api
spec:
  replicas: 3
  selector:
    matchLabels:
      app: labmanager-api
  template:
    metadata:
      labels:
        app: labmanager-api
    spec:
      containers:
      - name: api
        image: your-registry/labmanager-api:latest
        env:
        - name: ConnectionStrings__DefaultConnection
          value: "Host=postgres;Database=labmanager;Username=labadmin;Password=$(POSTGRES_PASSWORD)"
        - name: ConnectionStrings__Redis
          value: "redis:6379"
        - name: MinIO__Endpoint
          value: "minio:9000"
        - name: MinIO__AccessKey
          value: "minioadmin"
        - name: MinIO__SecretKey
          valueFrom:
            secretKeyRef:
              name: minio-secret
              key: password
        - name: ASPNETCORE_ENVIRONMENT
          value: "Production"
        ports:
        - containerPort: 5000
        resources:
          requests:
            cpu: "500m"
            memory: "1Gi"
          limits:
            cpu: "2000m"
            memory: "4Gi"
---
apiVersion: v1
kind: Service
metadata:
  name: labmanager-api
spec:
  selector:
    app: labmanager-api
  ports:
  - port: 80
    targetPort: 5000
```

```bash
kubectl apply -f labmanager-api.yaml
```

### Step 6: Deploy Ingress

```yaml
# ingress.yaml
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: labmanager-ingress
  annotations:
    cert-manager.io/cluster-issuer: "letsencrypt-prod"
    nginx.ingress.kubernetes.io/ssl-redirect: "true"
spec:
  ingressClassName: nginx
  tls:
  - hosts:
    - labs.example.com
    secretName: labmanager-tls
  rules:
  - host: labs.example.com
    http:
      paths:
      - path: /
        pathType: Prefix
        backend:
          service:
            name: labmanager-api
            port:
              number: 80
```

```bash
kubectl apply -f ingress.yaml
```

---

## Docker Compose Deployment

### Step 1: Create docker-compose.yml

```yaml
version: '3.8'

services:
  postgres:
    image: postgres:15-alpine
    environment:
      POSTGRES_DB: labmanager
      POSTGRES_USER: labadmin
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
    volumes:
      - postgres-data:/var/lib/postgresql/data
    ports:
      - "5432:5432"
    networks:
      - lab-network

  redis:
    image: redis:7-alpine
    ports:
      - "6379:6379"
    networks:
      - lab-network

  minio:
    image: minio/minio:latest
    command: server /data --console-address ":9001"
    environment:
      MINIO_ROOT_USER: minioadmin
      MINIO_ROOT_PASSWORD: ${MINIO_PASSWORD}
    volumes:
      - minio-data:/data
    ports:
      - "9000:9000"
      - "9001:9001"
    networks:
      - lab-network

  labmanager-api:
    image: your-registry/labmanager-api:latest
    environment:
      ConnectionStrings__DefaultConnection: "Host=postgres;Database=labmanager;Username=labadmin;Password=${POSTGRES_PASSWORD}"
      ConnectionStrings__Redis: "redis:6379"
      MinIO__Endpoint: "minio:9000"
      MinIO__AccessKey: "minioadmin"
      MinIO__SecretKey: "${MINIO_PASSWORD}"
      ASPNETCORE_ENVIRONMENT: "Production"
      ASPNETCORE_URLS: "http://+:5000"
    ports:
      - "5000:5000"
    depends_on:
      - postgres
      - redis
      - minio
    networks:
      - lab-network

  nginx:
    image: nginx:alpine
    volumes:
      - ./nginx.conf:/etc/nginx/nginx.conf:ro
      - ./ssl:/etc/nginx/ssl:ro
    ports:
      - "80:80"
      - "443:443"
    depends_on:
      - labmanager-api
    networks:
      - lab-network

volumes:
  postgres-data:
  minio-data:

networks:
  lab-network:
    driver: bridge
```

### Step 2: Create .env file

```bash
# .env
POSTGRES_PASSWORD=YourSecurePassword123!
MINIO_PASSWORD=YourMinIOPassword123!
```

### Step 3: Deploy

```bash
docker-compose up -d
```

---

## Configuration

### Application Configuration

Create `appsettings.Production.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=postgres;Database=labmanager;Username=labadmin;Password=${POSTGRES_PASSWORD}",
    "Redis": "redis:6379"
  },
  "Jwt": {
    "SecretKey": "${JWT_SECRET_KEY}",
    "Issuer": "labs.example.com",
    "Audience": "labs.example.com",
    "ExpirationMinutes": 480
  },
  "MinIO": {
    "Endpoint": "minio:9000",
    "AccessKey": "minioadmin",
    "SecretKey": "${MINIO_PASSWORD}",
    "BucketName": "lab-submissions",
    "UseSSL": false
  },
  "Docker": {
    "Endpoint": "unix:///var/run/docker.sock",
    "Network": "lab-network",
    "DefaultResourceQuota": {
      "CpuCores": 2.0,
      "MemoryGb": 4.0,
      "StorageGb": 10.0
    }
  },
  "Environment": {
    "AutoSuspendTimeoutMinutes": 30,
    "MaxActiveEnvironments": 5000,
    "WebIdeBaseUrl": "https://env.labs.example.com"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning"
    }
  }
}
```

---

## Database Setup

### Step 1: Run Migrations

```bash
# Using EF Core CLI
dotnet ef database update --project src/HotSwap.LabManager.Infrastructure

# Or using Docker
docker exec -it labmanager-api dotnet ef database update
```

### Step 2: Seed Initial Data

```bash
docker exec -it labmanager-api dotnet run -- seed
```

This will create:
- Default admin user
- Predefined resource templates
- Sample course data (if --sample flag provided)

---

## Monitoring & Observability

### Prometheus Metrics

Expose metrics endpoint:

```csharp
// Program.cs
app.MapMetrics(); // Expose /metrics endpoint
```

### Grafana Dashboards

Import dashboards from `/grafana/dashboards/`:
- `lab-manager-overview.json`
- `student-environments.json`
- `grading-performance.json`

### Jaeger Tracing

Configure OpenTelemetry to export to Jaeger:

```json
{
  "OpenTelemetry": {
    "JaegerEndpoint": "http://jaeger:14268/api/traces"
  }
}
```

---

## Backup & Recovery

### Database Backup

```bash
# Automated daily backups
kubectl create cronjob postgres-backup \
  --image=postgres:15-alpine \
  --schedule="0 2 * * *" \
  -- /bin/sh -c "pg_dump -h postgres -U labadmin labmanager > /backup/labmanager-$(date +%Y%m%d).sql"
```

### MinIO Backup

```bash
# Sync to S3 for offsite backup
mc mirror minio/lab-submissions s3/lab-submissions-backup
```

---

## Scaling

### Horizontal Scaling

Scale API servers:

```bash
kubectl scale deployment labmanager-api --replicas=5
```

### Auto-Scaling

```yaml
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: labmanager-api-hpa
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: labmanager-api
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

## Security Hardening

### Enable HTTPS/TLS

Use cert-manager for automatic TLS certificate management:

```bash
kubectl apply -f https://github.com/cert-manager/cert-manager/releases/download/v1.13.0/cert-manager.yaml
```

### Network Policies

Restrict traffic between pods:

```yaml
apiVersion: networking.k8s.io/v1
kind: NetworkPolicy
metadata:
  name: labmanager-network-policy
spec:
  podSelector:
    matchLabels:
      app: labmanager-api
  policyTypes:
  - Ingress
  - Egress
  ingress:
  - from:
    - podSelector:
        matchLabels:
          app: nginx-ingress
  egress:
  - to:
    - podSelector:
        matchLabels:
          app: postgres
    - podSelector:
        matchLabels:
          app: redis
```

---

## Troubleshooting

### Common Issues

#### Issue: Environments fail to provision

**Symptoms:** Environment status stuck at "Provisioning"

**Solution:**
```bash
# Check Docker daemon connectivity
docker ps

# Check Docker API endpoint in config
kubectl logs deployment/labmanager-api | grep Docker
```

#### Issue: High memory usage

**Symptoms:** API pods OOMKilled

**Solution:**
```bash
# Increase memory limits
kubectl set resources deployment labmanager-api --limits=memory=8Gi
```

#### Issue: Database connection timeout

**Symptoms:** "Npgsql.NpgsqlException: Timeout"

**Solution:**
```bash
# Increase connection pool size
# In appsettings.json:
"ConnectionStrings": {
  "DefaultConnection": "...;Maximum Pool Size=100;"
}
```

---

**Document Version:** 1.0.0
**Last Updated:** 2025-11-23
**Maintained By:** Platform Operations Team
