# Deployment & Migration Guide

**Version:** 1.0.0
**Last Updated:** 2025-11-16

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
- **Redis 7+** - Message persistence, distributed locks, deduplication
- **PostgreSQL 15+** - Schema registry, durable message storage

**Optional:**
- **Jaeger** - Distributed tracing (recommended for production)
- **Prometheus** - Metrics collection
- **Grafana** - Metrics visualization

### System Requirements

**Broker Node:**
- CPU: 2+ cores
- Memory: 4 GB+ RAM
- Disk: 20 GB+ SSD (for message persistence)
- Network: 1 Gbps

**Redis:**
- CPU: 2+ cores
- Memory: 8 GB+ RAM (depends on message volume)
- Disk: 50 GB+ SSD

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

# Start Redis, PostgreSQL, Jaeger
docker-compose -f docker-compose.messaging.yml up -d

# Verify services
docker-compose ps
```

**docker-compose.messaging.yml:**

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
      POSTGRES_DB: messaging
      POSTGRES_USER: messaging_user
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
  redis-data:
  postgres-data:
```

**Step 3: Configure Application**

```bash
# Create appsettings.Development.json
cat > src/HotSwap.Distributed.Api/appsettings.Development.json <<EOF
{
  "ConnectionStrings": {
    "Redis": "localhost:6379",
    "PostgreSQL": "Host=localhost;Database=messaging;Username=messaging_user;Password=dev_password"
  },
  "Messaging": {
    "BrokerPort": 5050,
    "DefaultDeliveryGuarantee": "AtLeastOnce",
    "DefaultRetentionPeriod": "P7D",
    "MaxMessageSize": 1048576
  },
  "OpenTelemetry": {
    "JaegerEndpoint": "http://localhost:14268/api/traces",
    "SamplingRate": 1.0
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "HotSwap.Distributed": "Debug"
    }
  }
}
EOF
```

**Step 4: Initialize Database**

```bash
# Run database migrations
dotnet ef database update --project src/HotSwap.Distributed.Infrastructure

# Or use SQL script
psql -h localhost -U messaging_user -d messaging -f db/migrations/001_initial_schema.sql
```

**db/migrations/001_initial_schema.sql:**

```sql
-- Schema registry tables
CREATE TABLE schemas (
    schema_id VARCHAR(255) PRIMARY KEY,
    schema_definition TEXT NOT NULL,
    version VARCHAR(50) NOT NULL,
    compatibility VARCHAR(50) NOT NULL DEFAULT 'None',
    status VARCHAR(50) NOT NULL DEFAULT 'Draft',
    approved_by VARCHAR(255),
    approved_at TIMESTAMP,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    deprecated_at TIMESTAMP
);

-- Audit log table (optional)
CREATE TABLE audit_logs (
    id BIGSERIAL PRIMARY KEY,
    event_type VARCHAR(100) NOT NULL,
    entity_type VARCHAR(100),
    entity_id VARCHAR(255),
    user_id VARCHAR(255),
    details JSONB,
    trace_id VARCHAR(255),
    created_at TIMESTAMP NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_audit_logs_created_at ON audit_logs(created_at DESC);
CREATE INDEX idx_audit_logs_entity ON audit_logs(entity_type, entity_id);
```

**Step 5: Run Application**

```bash
# Build and run
dotnet build
dotnet run --project src/HotSwap.Distributed.Api

# Application starts on:
# HTTP:  http://localhost:5000
# HTTPS: https://localhost:5001
# Swagger: http://localhost:5000/swagger
```

**Step 6: Verify Installation**

```bash
# Health check
curl http://localhost:5000/health

# Create test topic
curl -X POST http://localhost:5000/api/v1/topics \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $(get_jwt_token)" \
  -d '{
    "name": "test.topic",
    "type": "Queue",
    "schemaId": "test.v1",
    "deliveryGuarantee": "AtLeastOnce"
  }'

# Publish test message
curl -X POST http://localhost:5000/api/v1/messages/publish \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $(get_jwt_token)" \
  -d '{
    "topicName": "test.topic",
    "payload": "{\"test\":\"message\"}",
    "schemaVersion": "1.0"
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

COPY ["src/HotSwap.Distributed.Api/HotSwap.Distributed.Api.csproj", "Api/"]
COPY ["src/HotSwap.Distributed.Orchestrator/HotSwap.Distributed.Orchestrator.csproj", "Orchestrator/"]
COPY ["src/HotSwap.Distributed.Infrastructure/HotSwap.Distributed.Infrastructure.csproj", "Infrastructure/"]
COPY ["src/HotSwap.Distributed.Domain/HotSwap.Distributed.Domain.csproj", "Domain/"]
RUN dotnet restore "Api/HotSwap.Distributed.Api.csproj"

COPY src/ .
RUN dotnet publish "Api/HotSwap.Distributed.Api.csproj" -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

# Create non-root user
RUN useradd -m -u 1000 messaging && \
    chown -R messaging:messaging /app

USER messaging

COPY --from=build /app/publish .

EXPOSE 5000 5050
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
  CMD curl -f http://localhost:5000/health || exit 1

ENTRYPOINT ["dotnet", "HotSwap.Distributed.Api.dll"]
```

**Build and Push:**

```bash
# Build image
docker build -t your-registry/messaging-system:1.0.0 .

# Tag for registry
docker tag your-registry/messaging-system:1.0.0 your-registry/messaging-system:latest

# Push to registry
docker push your-registry/messaging-system:1.0.0
docker push your-registry/messaging-system:latest
```

### Docker Compose Full Stack

**docker-compose.production.yml:**

```yaml
version: '3.8'

services:
  messaging-api:
    image: your-registry/messaging-system:1.0.0
    ports:
      - "5000:5000"
      - "5050:5050"
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ASPNETCORE_URLS: http://+:5000
      ConnectionStrings__Redis: redis:6379
      ConnectionStrings__PostgreSQL: "Host=postgres;Database=messaging;Username=messaging_user;Password=${POSTGRES_PASSWORD}"
      OpenTelemetry__JaegerEndpoint: "http://jaeger:14268/api/traces"
    depends_on:
      - redis
      - postgres
      - jaeger
    networks:
      - messaging-network
    deploy:
      replicas: 3
      resources:
        limits:
          cpus: '2'
          memory: 4G
        reservations:
          cpus: '1'
          memory: 2G

  redis:
    image: redis:7-alpine
    command: redis-server --appendonly yes --maxmemory 4gb --maxmemory-policy allkeys-lru
    volumes:
      - redis-data:/data
    networks:
      - messaging-network
    deploy:
      resources:
        limits:
          memory: 8G

  postgres:
    image: postgres:15-alpine
    environment:
      POSTGRES_DB: messaging
      POSTGRES_USER: messaging_user
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
      POSTGRES_MAX_CONNECTIONS: 200
    volumes:
      - postgres-data:/var/lib/postgresql/data
    networks:
      - messaging-network
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
      - messaging-network

  prometheus:
    image: prom/prometheus:latest
    volumes:
      - ./prometheus.yml:/etc/prometheus/prometheus.yml
      - prometheus-data:/prometheus
    ports:
      - "9090:9090"
    networks:
      - messaging-network

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
      - messaging-network

volumes:
  redis-data:
  postgres-data:
  jaeger-data:
  prometheus-data:
  grafana-data:

networks:
  messaging-network:
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
docker-compose logs -f messaging-api
```

---

## Kubernetes Deployment

### Namespace and ConfigMap

**namespace.yaml:**

```yaml
apiVersion: v1
kind: Namespace
metadata:
  name: messaging-system
```

**configmap.yaml:**

```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: messaging-config
  namespace: messaging-system
data:
  appsettings.json: |
    {
      "Messaging": {
        "DefaultDeliveryGuarantee": "AtLeastOnce",
        "DefaultRetentionPeriod": "P7D",
        "MaxMessageSize": 1048576
      },
      "OpenTelemetry": {
        "SamplingRate": 0.1
      }
    }
```

### Secrets

```bash
# Create secrets
kubectl create secret generic messaging-secrets \
  --from-literal=redis-password=$(openssl rand -base64 32) \
  --from-literal=postgres-password=$(openssl rand -base64 32) \
  --from-literal=jwt-secret=$(openssl rand -base64 64) \
  -n messaging-system
```

### StatefulSets for Redis and PostgreSQL

**redis-statefulset.yaml:**

```yaml
apiVersion: apps/v1
kind: StatefulSet
metadata:
  name: redis
  namespace: messaging-system
spec:
  serviceName: redis
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
        command: ["redis-server", "--appendonly", "yes", "--requirepass", "$(REDIS_PASSWORD)"]
        env:
        - name: REDIS_PASSWORD
          valueFrom:
            secretKeyRef:
              name: messaging-secrets
              key: redis-password
        ports:
        - containerPort: 6379
          name: redis
        volumeMounts:
        - name: redis-data
          mountPath: /data
        resources:
          limits:
            memory: "8Gi"
            cpu: "2"
          requests:
            memory: "4Gi"
            cpu: "1"
  volumeClaimTemplates:
  - metadata:
      name: redis-data
    spec:
      accessModes: ["ReadWriteOnce"]
      resources:
        requests:
          storage: 50Gi
---
apiVersion: v1
kind: Service
metadata:
  name: redis
  namespace: messaging-system
spec:
  ports:
  - port: 6379
    targetPort: 6379
  clusterIP: None
  selector:
    app: redis
```

**postgres-statefulset.yaml:**

```yaml
apiVersion: apps/v1
kind: StatefulSet
metadata:
  name: postgres
  namespace: messaging-system
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
          value: "messaging"
        - name: POSTGRES_USER
          value: "messaging_user"
        - name: POSTGRES_PASSWORD
          valueFrom:
            secretKeyRef:
              name: messaging-secrets
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
  namespace: messaging-system
spec:
  ports:
  - port: 5432
    targetPort: 5432
  clusterIP: None
  selector:
    app: postgres
```

### Messaging API Deployment

**messaging-deployment.yaml:**

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: messaging-api
  namespace: messaging-system
spec:
  replicas: 3
  selector:
    matchLabels:
      app: messaging-api
  template:
    metadata:
      labels:
        app: messaging-api
    spec:
      containers:
      - name: messaging-api
        image: your-registry/messaging-system:1.0.0
        ports:
        - containerPort: 5000
          name: http
        - containerPort: 5050
          name: broker
        env:
        - name: ASPNETCORE_ENVIRONMENT
          value: "Production"
        - name: ConnectionStrings__Redis
          value: "redis:6379,password=$(REDIS_PASSWORD)"
        - name: ConnectionStrings__PostgreSQL
          value: "Host=postgres;Database=messaging;Username=messaging_user;Password=$(POSTGRES_PASSWORD)"
        - name: REDIS_PASSWORD
          valueFrom:
            secretKeyRef:
              name: messaging-secrets
              key: redis-password
        - name: POSTGRES_PASSWORD
          valueFrom:
            secretKeyRef:
              name: messaging-secrets
              key: postgres-password
        - name: JWT_SECRET
          valueFrom:
            secretKeyRef:
              name: messaging-secrets
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
          name: messaging-config
---
apiVersion: v1
kind: Service
metadata:
  name: messaging-api
  namespace: messaging-system
spec:
  type: LoadBalancer
  ports:
  - port: 80
    targetPort: 5000
    name: http
  - port: 5050
    targetPort: 5050
    name: broker
  selector:
    app: messaging-api
---
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: messaging-api-hpa
  namespace: messaging-system
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: messaging-api
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

# Deploy infrastructure
kubectl apply -f redis-statefulset.yaml
kubectl apply -f postgres-statefulset.yaml

# Wait for infrastructure to be ready
kubectl wait --for=condition=ready pod -l app=redis -n messaging-system --timeout=300s
kubectl wait --for=condition=ready pod -l app=postgres -n messaging-system --timeout=300s

# Run database migrations
kubectl run db-migrate --image=your-registry/messaging-system:1.0.0 \
  --env="ConnectionStrings__PostgreSQL=Host=postgres;Database=messaging;Username=messaging_user;Password=$(kubectl get secret messaging-secrets -n messaging-system -o jsonpath='{.data.postgres-password}' | base64 -d)" \
  --command -- dotnet ef database update \
  -n messaging-system

# Deploy messaging API
kubectl apply -f messaging-deployment.yaml

# Verify deployment
kubectl get pods -n messaging-system
kubectl get svc -n messaging-system

# Get service URL
kubectl get svc messaging-api -n messaging-system -o jsonpath='{.status.loadBalancer.ingress[0].ip}'
```

---

## Migration Path

### Phase 1: Parallel Deployment (Week 1)

**Goal:** Deploy messaging system alongside existing infrastructure

1. **Deploy Infrastructure:**
   ```bash
   # Deploy Redis, PostgreSQL, Jaeger
   docker-compose -f docker-compose.messaging.yml up -d
   ```

2. **Deploy Messaging API:**
   ```bash
   # Build and deploy messaging system
   docker build -t messaging-system:1.0.0 .
   docker run -d -p 5000:5000 messaging-system:1.0.0
   ```

3. **Create Initial Topics:**
   ```bash
   # Create deployment events topic
   curl -X POST http://localhost:5000/api/v1/topics \
     -H "Authorization: Bearer $TOKEN" \
     -d '{
       "name": "deployment.events",
       "type": "PubSub",
       "schemaId": "deployment.event.v1"
     }'
   ```

4. **Dual-Write Pattern:**
   - Publish events to BOTH messaging system AND existing webhooks
   - Validate message delivery
   - Compare latency and reliability

5. **Monitor Metrics:**
   - Message throughput
   - Delivery success rate
   - Consumer lag
   - System resource usage

### Phase 2: Gradual Migration (Week 2-3)

**Goal:** Migrate non-critical consumers to messaging system

1. **Migrate Non-Critical Notifications:**
   ```bash
   # Create subscriptions for non-critical consumers
   curl -X POST http://localhost:5000/api/v1/subscriptions \
     -H "Authorization: Bearer $TOKEN" \
     -d '{
       "topicName": "deployment.events",
       "consumerGroup": "email-notifications",
       "consumerEndpoint": "https://notifications.example.com/webhook",
       "type": "Push"
     }'
   ```

2. **Update Consumers:**
   - Point consumer applications to messaging API
   - Test message consumption
   - Verify acknowledgment flow

3. **Disable Old Webhooks:**
   - Remove webhook URLs from old system
   - Monitor for any missed messages

4. **Validate Schema Evolution:**
   - Register message schemas
   - Test backward compatibility
   - Verify approval workflow

### Phase 3: Full Migration (Week 4)

**Goal:** Migrate all events and retire old system

1. **Migrate All Events:**
   - Update all producers to use messaging API
   - Create all topics and subscriptions
   - Configure delivery guarantees

2. **Retire Old Webhooks:**
   - Remove old webhook infrastructure
   - Archive old event logs

3. **Enable Production Features:**
   - Schema approval workflow
   - Retention policies
   - Dead letter queues

4. **Production Monitoring:**
   - Set up Prometheus metrics
   - Configure Grafana dashboards
   - Set up alerting rules

### Rollback Plan

If issues arise during migration:

1. **Stop Dual-Write:**
   ```bash
   # Disable messaging system publishing
   kubectl scale deployment messaging-api --replicas=0 -n messaging-system
   ```

2. **Revert Consumers:**
   - Point consumers back to old webhook URLs
   - Verify old system still functional

3. **Preserve Messages:**
   - Export messages from Redis to backup
   - Retain audit logs for investigation

---

## Configuration

### Environment Variables

```bash
# Application
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://+:5000;https://+:5001

# Database Connections
ConnectionStrings__Redis=redis:6379,password=secure_password
ConnectionStrings__PostgreSQL=Host=postgres;Database=messaging;Username=messaging_user;Password=secure_password

# Messaging
Messaging__DefaultDeliveryGuarantee=AtLeastOnce
Messaging__DefaultRetentionPeriod=P7D
Messaging__MaxMessageSize=1048576

# OpenTelemetry
OpenTelemetry__JaegerEndpoint=http://jaeger:14268/api/traces
OpenTelemetry__SamplingRate=0.1

# JWT
JWT__Secret=your_jwt_secret_key_here
JWT__Issuer=https://messaging.example.com
JWT__Audience=messaging-api
JWT__ExpirationMinutes=60

# Rate Limiting
RateLimiting__Publish=100
RateLimiting__Consume=600
RateLimiting__Topics=10
```

---

**Last Updated:** 2025-11-16
**Version:** 1.0.0
**Deployment Status:** Design Phase
