# Deployment & Operations Guide

**Version:** 1.0.0
**Last Updated:** 2025-11-23

---

## Table of Contents

1. [Prerequisites](#prerequisites)
2. [Local Development Setup](#local-development-setup)
3. [Docker Deployment](#docker-deployment)
4. [Kubernetes Deployment](#kubernetes-deployment)
5. [Game Server Integration](#game-server-integration)
6. [Monitoring Setup](#monitoring-setup)
7. [Operational Runbooks](#operational-runbooks)
8. [Troubleshooting](#troubleshooting)

---

## Prerequisites

### Required Infrastructure

**Mandatory:**
- **.NET 8.0 Runtime** - Application runtime
- **Redis 7+** - Configuration caching, session state
- **PostgreSQL 15+** - Configuration storage, metrics history
- **Game Servers** - Target servers for configuration deployment

**Optional:**
- **Jaeger** - Distributed tracing
- **Prometheus** - Metrics collection
- **Grafana** - Metrics visualization and dashboards

### System Requirements

**API Server:**
- CPU: 2+ cores
- Memory: 4 GB+ RAM
- Disk: 20 GB+ SSD
- Network: 1 Gbps

**Game Server:**
- CPU: 4+ cores
- Memory: 8 GB+ RAM
- Disk: 50 GB+ SSD
- Network: 1 Gbps

---

## Local Development Setup

### Quick Start

**Step 1: Start Infrastructure**

```bash
# Start Redis and PostgreSQL
docker-compose -f docker-compose.gaming.yml up -d

# Verify services
docker-compose ps
```

**docker-compose.gaming.yml:**

```yaml
version: '3.8'

services:
  redis:
    image: redis:7-alpine
    ports:
      - "6379:6379"
    volumes:
      - redis-data:/data

  postgres:
    image: postgres:15-alpine
    ports:
      - "5432:5432"
    environment:
      POSTGRES_DB: gaming
      POSTGRES_USER: gaming_user
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
    volumes:
      - postgres-data:/var/lib/postgresql/data

  jaeger:
    image: jaegertracing/all-in-one:1.52
    ports:
      - "16686:16686"  # Jaeger UI
      - "14268:14268"

volumes:
  redis-data:
  postgres-data:
```

**Step 2: Configure Application**

```bash
cat > src/HotSwap.Gaming.Api/appsettings.Development.json <<EOF
{
  "ConnectionStrings": {
    "Redis": "localhost:6379",
    "PostgreSQL": "Host=localhost;Database=gaming;Username=gaming_user;Password=dev_password"
  },
  "Gaming": {
    "DefaultEvaluationPeriod": "PT30M",
    "MaxConcurrentDeployments": 10,
    "RollbackThresholds": {
      "ChurnRateIncreaseMax": 5.0,
      "CrashRateIncreaseMax": 10.0
    }
  },
  "OpenTelemetry": {
    "JaegerEndpoint": "http://localhost:14268/api/traces"
  }
}
EOF
```

**Step 3: Initialize Database**

```bash
dotnet ef database update --project src/HotSwap.Gaming.Infrastructure
```

**Step 4: Run Application**

```bash
dotnet run --project src/HotSwap.Gaming.Api

# API available at:
# http://localhost:5000
# Swagger: http://localhost:5000/swagger
```

---

## Docker Deployment

### Build Docker Image

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/HotSwap.Gaming.Api/HotSwap.Gaming.Api.csproj", "Api/"]
COPY ["src/HotSwap.Gaming.Orchestrator/HotSwap.Gaming.Orchestrator.csproj", "Orchestrator/"]
COPY ["src/HotSwap.Gaming.Infrastructure/HotSwap.Gaming.Infrastructure.csproj", "Infrastructure/"]
COPY ["src/HotSwap.Gaming.Domain/HotSwap.Gaming.Domain.csproj", "Domain/"]

RUN dotnet restore "Api/HotSwap.Gaming.Api.csproj"
COPY src/ .
RUN dotnet publish "Api/HotSwap.Gaming.Api.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 5000
ENTRYPOINT ["dotnet", "HotSwap.Gaming.Api.dll"]
```

```bash
docker build -t your-registry/gaming-config-system:1.0.0 .
docker push your-registry/gaming-config-system:1.0.0
```

---

## Kubernetes Deployment

### Deployment Manifests

**gaming-config-deployment.yaml:**

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: gaming-config-api
  namespace: gaming
spec:
  replicas: 3
  selector:
    matchLabels:
      app: gaming-config-api
  template:
    metadata:
      labels:
        app: gaming-config-api
    spec:
      containers:
      - name: api
        image: your-registry/gaming-config-system:1.0.0
        ports:
        - containerPort: 5000
        env:
        - name: ConnectionStrings__Redis
          value: "redis-service:6379"
        - name: ConnectionStrings__PostgreSQL
          valueFrom:
            secretKeyRef:
              name: postgres-secret
              key: connection-string
        resources:
          limits:
            cpu: "2"
            memory: "4Gi"
          requests:
            cpu: "1"
            memory: "2Gi"
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
          initialDelaySeconds: 5
          periodSeconds: 5
---
apiVersion: v1
kind: Service
metadata:
  name: gaming-config-api-service
  namespace: gaming
spec:
  selector:
    app: gaming-config-api
  ports:
  - protocol: TCP
    port: 80
    targetPort: 5000
  type: LoadBalancer
```

**Deploy to Kubernetes:**

```bash
kubectl apply -f k8s/namespace.yaml
kubectl apply -f k8s/secrets.yaml
kubectl apply -f k8s/gaming-config-deployment.yaml

# Verify deployment
kubectl get pods -n gaming
kubectl logs -f deployment/gaming-config-api -n gaming
```

---

## Game Server Integration

### Game Server SDK

Game servers need to integrate with the configuration system to receive updates.

**Example C# SDK:**

```csharp
public class GameConfigClient
{
    private readonly HttpClient _httpClient;
    private readonly string _serverId;
    private GameConfiguration _currentConfig;

    public GameConfigClient(string apiBaseUrl, string serverId)
    {
        _httpClient = new HttpClient { BaseAddress = new Uri(apiBaseUrl) };
        _serverId = serverId;
    }

    public async Task RegisterServerAsync()
    {
        var serverInfo = new
        {
            ServerId = _serverId,
            Hostname = Environment.MachineName,
            IpAddress = GetLocalIpAddress(),
            Region = GetRegion(),
            MaxPlayers = 100
        };

        await _httpClient.PostAsJsonAsync("/api/v1/game-servers", serverInfo);
    }

    public async Task StartHeartbeatAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await _httpClient.PostAsync(
                $"/api/v1/game-servers/{_serverId}/heartbeat",
                null);

            await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
        }
    }

    public async Task<GameConfiguration> FetchConfigurationAsync()
    {
        var response = await _httpClient.GetAsync(
            $"/api/v1/game-servers/{_serverId}/configuration");

        return await response.Content.ReadFromJsonAsync<GameConfiguration>();
    }

    public async Task ApplyConfigurationAsync(GameConfiguration config)
    {
        // Apply config to game server
        ApplyWeaponBalance(config);
        ApplyEconomySettings(config);
        ApplyMatchmakingRules(config);

        _currentConfig = config;

        // Notify API that config was applied
        await _httpClient.PostAsync(
            $"/api/v1/game-servers/{_serverId}/config-applied/{config.ConfigId}",
            null);
    }
}
```

**Game Server Startup:**

```csharp
public class GameServer
{
    public async Task StartAsync()
    {
        var configClient = new GameConfigClient(
            "https://api.example.com",
            "srv-west-001");

        // Register server
        await configClient.RegisterServerAsync();

        // Start heartbeat
        _ = configClient.StartHeartbeatAsync(_cancellationToken);

        // Fetch initial configuration
        var config = await configClient.FetchConfigurationAsync();
        await configClient.ApplyConfigurationAsync(config);

        // Listen for configuration updates (SignalR/WebSocket)
        await ListenForConfigUpdatesAsync(configClient);
    }
}
```

---

## Monitoring Setup

### Grafana Dashboards

**Dashboard: Configuration Deployments**

```json
{
  "dashboard": {
    "title": "Game Configuration Deployments",
    "panels": [
      {
        "title": "Active Deployments",
        "targets": [
          {
            "expr": "sum(deployments_in_progress)"
          }
        ]
      },
      {
        "title": "Deployment Success Rate",
        "targets": [
          {
            "expr": "rate(deployments_completed_total[5m]) / rate(deployments_started_total[5m]) * 100"
          }
        ]
      },
      {
        "title": "Rollback Rate",
        "targets": [
          {
            "expr": "rate(deployments_rolledback_total[5m])"
          }
        ]
      },
      {
        "title": "Player Churn Rate",
        "targets": [
          {
            "expr": "avg(player_churn_rate)"
          }
        ]
      }
    ]
  }
}
```

### Alerting Rules

**Prometheus Alerts:**

```yaml
groups:
  - name: gaming_config_alerts
    rules:
      - alert: HighRollbackRate
        expr: rate(deployments_rolledback_total[5m]) > 0.1
        for: 5m
        labels:
          severity: warning
        annotations:
          summary: "High deployment rollback rate"
          description: "Rollback rate is {{ $value }} per second"

      - alert: PlayerChurnSpike
        expr: player_churn_rate > 10
        for: 10m
        labels:
          severity: critical
        annotations:
          summary: "Player churn rate spiked"
          description: "Churn rate is {{ $value }}%"
```

---

## Operational Runbooks

### Runbook: Emergency Rollback

**When:** Critical bug detected, player experience degraded

**Steps:**
1. Identify problematic deployment:
   ```bash
   curl https://api.example.com/api/v1/deployments?status=InProgress
   ```

2. Trigger immediate rollback:
   ```bash
   curl -X POST https://api.example.com/api/v1/deployments/{id}/rollback \
     -H "Authorization: Bearer $TOKEN" \
     -d '{"reason": "Critical bug: players unable to connect"}'
   ```

3. Verify rollback completed:
   ```bash
   curl https://api.example.com/api/v1/deployments/{id}
   ```

4. Monitor player metrics for recovery:
   - Check Grafana dashboard
   - Verify churn rate returning to normal
   - Monitor support tickets

5. Root cause analysis:
   - Review deployment logs
   - Analyze configuration diff
   - Identify bug cause

---

### Runbook: Slow Canary Progression

**When:** Canary deployment not progressing automatically

**Steps:**
1. Check deployment status:
   ```bash
   curl https://api.example.com/api/v1/deployments/{id}
   ```

2. Review metrics comparison:
   ```bash
   curl https://api.example.com/api/v1/deployments/{id}/metrics
   ```

3. Identify blocking metric:
   - Churn rate near threshold?
   - Crash rate elevated?
   - Session duration decreased?

4. Decision:
   - **If metrics acceptable:** Manual progression
     ```bash
     curl -X POST https://api.example.com/api/v1/deployments/{id}/progress
     ```
   - **If metrics concerning:** Rollback and investigate

---

## Troubleshooting

### Issue: Configuration Not Applied to Servers

**Symptoms:**
- Servers report old configuration version
- Players not seeing changes

**Diagnosis:**
```bash
# Check server status
curl https://api.example.com/api/v1/game-servers/{serverId}

# Check deployment distribution
curl https://api.example.com/api/v1/deployments/{id}/server-status
```

**Resolution:**
1. Verify server heartbeat is active
2. Check server configuration fetch logs
3. Manually trigger config push:
   ```bash
   curl -X POST https://api.example.com/api/v1/game-servers/{serverId}/push-config
   ```

---

### Issue: High Rollback Rate

**Symptoms:**
- Multiple deployments rolled back
- Rollback rate alert firing

**Diagnosis:**
- Review rollback reasons in deployment logs
- Check common metrics threshold breaches

**Resolution:**
1. Adjust rollback thresholds if too sensitive
2. Improve pre-deployment testing
3. Use A/B testing for risky changes

---

**Document Version:** 1.0.0
**Last Updated:** 2025-11-23
