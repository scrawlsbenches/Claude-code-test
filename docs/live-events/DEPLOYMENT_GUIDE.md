# Deployment & Operations Guide

**Version:** 1.0.0
**Last Updated:** 2025-11-23
**Target Platforms:** Docker, Kubernetes

---

## Table of Contents

1. [Prerequisites](#prerequisites)
2. [Local Development Setup](#local-development-setup)
3. [Production Deployment](#production-deployment)
4. [Configuration](#configuration)
5. [Monitoring & Alerts](#monitoring--alerts)
6. [Operational Runbooks](#operational-runbooks)
7. [Disaster Recovery](#disaster-recovery)

---

## Prerequisites

### Infrastructure Requirements

**Minimum Production Configuration:**

| Component | Specification | Notes |
|-----------|--------------|-------|
| **Application Servers** | 3× (4 vCPU, 8 GB RAM) | For high availability |
| **PostgreSQL** | 1× (4 vCPU, 16 GB RAM, 500 GB SSD) | Event storage |
| **Redis** | 3-node cluster (2 vCPU, 4 GB RAM each) | Event cache |
| **Load Balancer** | 2× (2 vCPU, 4 GB RAM) | HTTPS termination |
| **Monitoring** | 1× (2 vCPU, 8 GB RAM) | Prometheus + Grafana |

**Software Dependencies:**

- .NET 8.0 Runtime
- Docker 24.0+
- Kubernetes 1.28+ (if using Kubernetes)
- PostgreSQL 15+
- Redis 7+
- Nginx or HAProxy (load balancer)
- Prometheus + Grafana (monitoring)

---

## Local Development Setup

### 1. Clone Repository

```bash
git clone https://github.com/scrawlsbenches/Claude-code-test.git
cd Claude-code-test
```

### 2. Start Infrastructure (Docker Compose)

```bash
# Start PostgreSQL and Redis
docker-compose up -d postgres redis

# Verify services
docker-compose ps
```

**docker-compose.yml:**
```yaml
version: '3.8'

services:
  postgres:
    image: postgres:15
    environment:
      POSTGRES_DB: liveevents
      POSTGRES_USER: liveevents_user
      POSTGRES_PASSWORD: dev_password
    ports:
      - "5432:5432"
    volumes:
      - postgres_data:/var/lib/postgresql/data

  redis:
    image: redis:7
    ports:
      - "6379:6379"
    command: redis-server --appendonly yes
    volumes:
      - redis_data:/data

volumes:
  postgres_data:
  redis_data:
```

### 3. Configure Application

Create `appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "PostgreSQL": "Host=localhost;Database=liveevents;Username=liveevents_user;Password=dev_password",
    "Redis": "localhost:6379"
  },
  "Authentication": {
    "JwtSecret": "development-secret-key-change-in-production",
    "JwtExpirationMinutes": 60
  },
  "EventScheduler": {
    "Enabled": true,
    "ScheduleIntervalSeconds": 30
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

### 4. Run Database Migrations

```bash
# Install EF Core tools
dotnet tool install --global dotnet-ef

# Run migrations
cd src/HotSwap.LiveEvents.API
dotnet ef database update

# Verify tables created
psql -h localhost -U liveevents_user -d liveevents -c "\dt"
```

### 5. Run Application

```bash
cd src/HotSwap.LiveEvents.API
dotnet run

# Application starts at https://localhost:5001
```

### 6. Verify Setup

```bash
# Health check
curl https://localhost:5001/health

# Create test event
curl -X POST https://localhost:5001/api/v1/events \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer {token}" \
  -d '{
    "eventId": "test-event",
    "displayName": "Test Event",
    "startTime": "2025-12-01T00:00:00Z",
    "endTime": "2025-12-31T23:59:59Z",
    "configuration": {
      "rewards": {
        "dailyLoginBonus": 100
      }
    }
  }'
```

---

## Production Deployment

### Option 1: Docker Deployment

#### Build Docker Image

```bash
# Build image
docker build -t liveevents-api:1.0.0 -f src/HotSwap.LiveEvents.API/Dockerfile .

# Tag for registry
docker tag liveevents-api:1.0.0 your-registry.com/liveevents-api:1.0.0

# Push to registry
docker push your-registry.com/liveevents-api:1.0.0
```

#### Run with Docker Compose (Production)

**docker-compose.prod.yml:**
```yaml
version: '3.8'

services:
  liveevents-api:
    image: your-registry.com/liveevents-api:1.0.0
    ports:
      - "5000:80"
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ConnectionStrings__PostgreSQL: ${POSTGRES_CONNECTION_STRING}
      ConnectionStrings__Redis: ${REDIS_CONNECTION_STRING}
      Authentication__JwtSecret: ${JWT_SECRET}
    deploy:
      replicas: 3
      resources:
        limits:
          cpus: '2'
          memory: 4G
        reservations:
          cpus: '1'
          memory: 2G
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:80/health"]
      interval: 30s
      timeout: 10s
      retries: 3

  nginx:
    image: nginx:latest
    ports:
      - "443:443"
      - "80:80"
    volumes:
      - ./nginx.conf:/etc/nginx/nginx.conf
      - ./ssl:/etc/nginx/ssl
    depends_on:
      - liveevents-api
```

---

### Option 2: Kubernetes Deployment

#### Namespace Setup

```bash
kubectl create namespace liveevents
kubectl config set-context --current --namespace=liveevents
```

#### Create Secrets

```bash
# Database credentials
kubectl create secret generic postgres-credentials \
  --from-literal=connection-string="Host=postgres;Database=liveevents;Username=user;Password=***"

# Redis credentials
kubectl create secret generic redis-credentials \
  --from-literal=connection-string="redis:6379"

# JWT secret
kubectl create secret generic jwt-secret \
  --from-literal=secret="production-secret-key-***"
```

#### Deployment Manifest

**k8s/deployment.yaml:**
```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: liveevents-api
  namespace: liveevents
spec:
  replicas: 3
  selector:
    matchLabels:
      app: liveevents-api
  template:
    metadata:
      labels:
        app: liveevents-api
    spec:
      containers:
      - name: liveevents-api
        image: your-registry.com/liveevents-api:1.0.0
        ports:
        - containerPort: 80
        env:
        - name: ASPNETCORE_ENVIRONMENT
          value: "Production"
        - name: ConnectionStrings__PostgreSQL
          valueFrom:
            secretKeyRef:
              name: postgres-credentials
              key: connection-string
        - name: ConnectionStrings__Redis
          valueFrom:
            secretKeyRef:
              name: redis-credentials
              key: connection-string
        - name: Authentication__JwtSecret
          valueFrom:
            secretKeyRef:
              name: jwt-secret
              key: secret
        resources:
          requests:
            memory: "2Gi"
            cpu: "1"
          limits:
            memory: "4Gi"
            cpu: "2"
        livenessProbe:
          httpGet:
            path: /health
            port: 80
          initialDelaySeconds: 30
          periodSeconds: 30
        readinessProbe:
          httpGet:
            path: /health
            port: 80
          initialDelaySeconds: 10
          periodSeconds: 10
---
apiVersion: v1
kind: Service
metadata:
  name: liveevents-api
  namespace: liveevents
spec:
  selector:
    app: liveevents-api
  ports:
  - protocol: TCP
    port: 80
    targetPort: 80
  type: ClusterIP
---
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: liveevents-api
  namespace: liveevents
  annotations:
    cert-manager.io/cluster-issuer: "letsencrypt-prod"
    nginx.ingress.kubernetes.io/rate-limit: "1000"
spec:
  ingressClassName: nginx
  tls:
  - hosts:
    - api.liveevents.example.com
    secretName: liveevents-api-tls
  rules:
  - host: api.liveevents.example.com
    http:
      paths:
      - path: /
        pathType: Prefix
        backend:
          service:
            name: liveevents-api
            port:
              number: 80
```

#### Deploy to Kubernetes

```bash
# Apply configurations
kubectl apply -f k8s/deployment.yaml

# Verify deployment
kubectl get pods
kubectl get services
kubectl get ingress

# Check logs
kubectl logs -f deployment/liveevents-api

# Scale deployment
kubectl scale deployment liveevents-api --replicas=5
```

---

## Configuration

### Environment Variables

| Variable | Required | Default | Description |
|----------|----------|---------|-------------|
| `ASPNETCORE_ENVIRONMENT` | Yes | Production | Environment (Development/Staging/Production) |
| `ConnectionStrings__PostgreSQL` | Yes | - | PostgreSQL connection string |
| `ConnectionStrings__Redis` | Yes | - | Redis connection string |
| `Authentication__JwtSecret` | Yes | - | JWT signing secret |
| `Authentication__JwtExpirationMinutes` | No | 60 | JWT token expiration |
| `EventScheduler__Enabled` | No | true | Enable event scheduler |
| `EventScheduler__ScheduleIntervalSeconds` | No | 30 | Scheduler check interval |
| `Cache__DefaultTTLSeconds` | No | 60 | Default cache TTL |
| `RateLimiting__Enabled` | No | true | Enable rate limiting |

### Feature Flags

Configure via `appsettings.Production.json`:

```json
{
  "Features": {
    "ABTesting": true,
    "AutoRollback": true,
    "MetricsCollection": true,
    "EventNotifications": true
  }
}
```

---

## Monitoring & Alerts

### Prometheus Metrics

**Key Metrics Exposed:**

```
# Event metrics
liveevents_active_count - Currently active events
liveevents_deployments_total - Total deployments
liveevents_rollbacks_total - Total rollbacks
liveevents_player_queries_total - Total player queries

# Performance metrics
liveevents_activation_duration_seconds - Event activation latency
liveevents_query_duration_seconds - Player query latency
liveevents_cache_hit_rate - Redis cache hit rate

# Business metrics
liveevents_participants_total - Total event participants
liveevents_revenue_total - Total revenue (USD)
liveevents_engagement_rate - Participation rate
```

### Grafana Dashboards

#### Event Overview Dashboard

**Panels:**
- Active events count (gauge)
- Event participation rate (graph)
- Revenue uplift percentage (graph)
- Player queries per second (graph)
- Cache hit rate (graph)

**Import Dashboard:**
```bash
# Import from file
curl -X POST http://grafana:3000/api/dashboards/db \
  -H "Content-Type: application/json" \
  -d @dashboards/event-overview.json
```

#### Deployment Progress Dashboard

**Panels:**
- Deployment status (table)
- Regional deployment progress (heatmap)
- Rollback events timeline (graph)
- Engagement metrics per region (graph)

### Alerts Configuration

**Prometheus Alerts (alerts.yml):**

```yaml
groups:
  - name: liveevents
    interval: 30s
    rules:
      - alert: HighErrorRate
        expr: rate(liveevents_errors_total[5m]) > 0.05
        for: 2m
        labels:
          severity: critical
        annotations:
          summary: "High error rate detected"
          description: "Error rate is {{ $value }} (threshold: 0.05)"

      - alert: LowCacheHitRate
        expr: liveevents_cache_hit_rate < 0.80
        for: 5m
        labels:
          severity: warning
        annotations:
          summary: "Low cache hit rate"
          description: "Cache hit rate is {{ $value }} (threshold: 0.80)"

      - alert: EventActivationFailed
        expr: increase(liveevents_activation_failures_total[5m]) > 0
        labels:
          severity: critical
        annotations:
          summary: "Event activation failed"
          description: "{{ $value }} event activations failed in last 5 minutes"

      - alert: ParticipationRateDrop
        expr: liveevents_engagement_rate < 0.30
        for: 10m
        labels:
          severity: warning
        annotations:
          summary: "Participation rate dropped below 30%"
          description: "Current participation rate: {{ $value }}"
```

---

## Operational Runbooks

### Runbook 1: Event Activation Failure

**Symptoms:**
- Event scheduled to activate but remains in "Scheduled" state
- Error logs show activation failures
- Alert: `EventActivationFailed` triggered

**Investigation:**
1. Check event scheduler logs:
   ```bash
   kubectl logs -f deployment/liveevents-api | grep EventScheduler
   ```
2. Verify event state in database:
   ```sql
   SELECT event_id, state, start_time, end_time
   FROM live_events
   WHERE event_id = 'failing-event';
   ```
3. Check for conflicting events:
   ```sql
   SELECT event_id, state, start_time, end_time
   FROM live_events
   WHERE state = 'Active'
   AND start_time <= NOW()
   AND end_time >= NOW();
   ```

**Resolution:**
1. Manual activation:
   ```bash
   curl -X POST https://api.example.com/api/v1/events/failing-event/activate \
     -H "Authorization: Bearer {admin-token}"
   ```
2. If validation errors, fix event configuration:
   ```bash
   curl -X PUT https://api.example.com/api/v1/events/failing-event \
     -H "Content-Type: application/json" \
     -H "Authorization: Bearer {admin-token}" \
     -d '{ "startTime": "2025-12-01T12:00:00Z" }'
   ```

---

### Runbook 2: Deployment Rollback

**Symptoms:**
- Participation rate dropped > 20%
- Alert: `ParticipationRateDrop` triggered
- Negative player feedback spike

**Investigation:**
1. Check deployment metrics:
   ```bash
   curl https://api.example.com/api/v1/deployments/deploy-123 \
     -H "Authorization: Bearer {token}"
   ```
2. Review engagement metrics:
   ```bash
   curl https://api.example.com/api/v1/events/event-123/metrics \
     -H "Authorization: Bearer {token}"
   ```

**Resolution:**
1. Manual rollback:
   ```bash
   curl -X POST https://api.example.com/api/v1/deployments/deploy-123/rollback \
     -H "Content-Type: application/json" \
     -H "Authorization: Bearer {admin-token}" \
     -d '{ "reason": "Participation rate dropped 25%" }'
   ```
2. Verify rollback completed:
   ```bash
   curl https://api.example.com/api/v1/deployments/deploy-123 \
     -H "Authorization: Bearer {token}"
   ```

---

### Runbook 3: Cache Miss Spike

**Symptoms:**
- Cache hit rate < 80%
- Player query latency increased
- Alert: `LowCacheHitRate` triggered

**Investigation:**
1. Check Redis status:
   ```bash
   redis-cli -h redis-cluster ping
   redis-cli -h redis-cluster info stats
   ```
2. Check cache keys:
   ```bash
   redis-cli -h redis-cluster --scan --pattern "liveevent:*" | head -20
   ```
3. Check application logs:
   ```bash
   kubectl logs -f deployment/liveevents-api | grep "Cache miss"
   ```

**Resolution:**
1. Warm cache for active events:
   ```bash
   curl -X POST https://api.example.com/api/v1/admin/cache/warm \
     -H "Authorization: Bearer {admin-token}"
   ```
2. Increase cache TTL if necessary:
   ```bash
   kubectl set env deployment/liveevents-api Cache__DefaultTTLSeconds=120
   ```

---

## Disaster Recovery

### Backup Strategy

#### PostgreSQL Backup

**Daily Full Backup:**
```bash
# Automated backup (cron job)
0 2 * * * pg_dump -h postgres -U liveevents_user liveevents | gzip > /backups/liveevents_$(date +\%Y\%m\%d).sql.gz
```

**Point-in-Time Recovery:**
```bash
# Enable WAL archiving (postgresql.conf)
wal_level = replica
archive_mode = on
archive_command = 'cp %p /backups/wal_archive/%f'
```

#### Redis Persistence

**RDB + AOF:**
```bash
# Redis configuration
save 900 1
save 300 10
save 60 10000
appendonly yes
appendfsync everysec
```

### Recovery Procedures

#### Scenario 1: Database Corruption

1. Stop application:
   ```bash
   kubectl scale deployment liveevents-api --replicas=0
   ```
2. Restore from backup:
   ```bash
   gunzip < /backups/liveevents_20251123.sql.gz | psql -h postgres -U liveevents_user liveevents
   ```
3. Restart application:
   ```bash
   kubectl scale deployment liveevents-api --replicas=3
   ```

#### Scenario 2: Complete System Failure

1. Deploy fresh infrastructure
2. Restore PostgreSQL from latest backup
3. Restore Redis from RDB/AOF files
4. Deploy application from last known good image
5. Verify health checks pass
6. Route traffic to recovered system

---

**Document Version:** 1.0.0
**Last Updated:** 2025-11-23
**Support:** ops-team@example.com
