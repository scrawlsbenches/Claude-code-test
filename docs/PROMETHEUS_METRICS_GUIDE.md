# Prometheus Metrics Exporter Guide

**Last Updated**: 2025-11-19
**Status**: Production Ready
**Task**: #7 from TASK_LIST.md

---

## Overview

The Distributed Kernel Orchestration API exposes comprehensive metrics via Prometheus-compatible `/metrics` endpoint. This includes both infrastructure metrics (ASP.NET Core, runtime, HTTP) and custom business metrics (deployments, approvals, rollbacks).

### Key Features

- ✅ **Prometheus-compatible** `/metrics` endpoint (OpenMetrics format)
- ✅ **Auto-instrumentation** for ASP.NET Core, HTTP clients, .NET runtime
- ✅ **Custom business metrics** for deployments, approvals, and operations
- ✅ **Multi-dimensional labels** for detailed filtering and aggregation
- ✅ **No authentication required** on `/metrics` (bypasses rate limiting)
- ✅ **Production-ready** configuration with sensible defaults

---

## Quick Start

### 1. Start the API

```bash
dotnet run --project src/HotSwap.Distributed.Api/
```

The API will log: `Prometheus metrics endpoint enabled at /metrics`

### 2. Access Metrics Endpoint

```bash
curl http://localhost:5000/metrics
```

### 3. Sample Output

```prometheus
# HELP deployments_started_total Total number of deployments started
# TYPE deployments_started_total counter
deployments_started_total{environment="Development",strategy="Direct",module="my-module"} 15

# HELP deployments_completed_total Total number of deployments completed successfully
# TYPE deployments_completed_total counter
deployments_completed_total{environment="Development",strategy="Direct",module="my-module"} 12

# HELP deployment_duration_seconds Duration of deployment operations
# TYPE deployment_duration_seconds histogram
deployment_duration_seconds_bucket{environment="Development",strategy="Direct",status="success",le="1"} 5
deployment_duration_seconds_bucket{environment="Development",strategy="Direct",status="success",le="5"} 10
deployment_duration_seconds_bucket{environment="Development",strategy="Direct",status="success",le="+Inf"} 12
deployment_duration_seconds_sum{environment="Development",strategy="Direct",status="success"} 23.456
deployment_duration_seconds_count{environment="Development",strategy="Direct",status="success"} 12

# HELP http_server_request_duration_seconds HTTP server request duration
# TYPE http_server_request_duration_seconds histogram
http_server_request_duration_seconds_bucket{http_request_method="GET",http_response_status_code="200",http_route="/api/v1/clusters",le="0.005"} 120
...

# HELP process_runtime_dotnet_gc_collections_count Number of garbage collections
# TYPE process_runtime_dotnet_gc_collections_count counter
process_runtime_dotnet_gc_collections_count{generation="gen0"} 45
process_runtime_dotnet_gc_collections_count{generation="gen1"} 12
process_runtime_dotnet_gc_collections_count{generation="gen2"} 3
```

---

## Metrics Reference

### 📊 Custom Business Metrics

All custom metrics are tracked by `DeploymentMetrics` class and exposed via `/metrics`.

#### Deployment Metrics

| Metric Name | Type | Description | Labels |
|-------------|------|-------------|--------|
| `deployments_started_total` | Counter | Total deployments started | `environment`, `strategy`, `module` |
| `deployments_completed_total` | Counter | Total deployments completed successfully | `environment`, `strategy`, `module` |
| `deployments_failed_total` | Counter | Total deployments that failed | `environment`, `strategy`, `module`, `reason` |
| `deployments_rolled_back_total` | Counter | Total deployments rolled back | `environment`, `module` |
| `deployment_duration_seconds` | Histogram | Duration of deployment operations | `environment`, `strategy`, `status` |

#### Approval Metrics

| Metric Name | Type | Description | Labels |
|-------------|------|-------------|--------|
| `approval_requests_total` | Counter | Total approval requests created | `environment`, `module` |
| `approvals_granted_total` | Counter | Total approvals granted | `environment`, `module`, `approver` |
| `approvals_rejected_total` | Counter | Total approvals rejected | `environment`, `module`, `approver`, `reason` |

#### Module and Node Metrics

| Metric Name | Type | Description | Labels |
|-------------|------|-------------|--------|
| `modules_deployed_total` | Counter | Total modules deployed | `environment`, `module` |
| `nodes_updated_total` | Counter | Total nodes updated | `environment`, `operation` |

**Operations:** `deploy`, `rollback`

---

### 🏗️ Infrastructure Metrics

Auto-instrumented by OpenTelemetry.

#### ASP.NET Core Metrics

| Metric Name | Description |
|-------------|-------------|
| `http_server_request_duration_seconds` | HTTP request duration (histogram) |
| `http_server_active_requests` | Number of active HTTP requests (gauge) |
| `http_server_request_body_size_bytes` | HTTP request body size (histogram) |
| `http_server_response_body_size_bytes` | HTTP response body size (histogram) |

**Labels:** `http_request_method`, `http_response_status_code`, `http_route`

#### .NET Runtime Metrics

| Metric Name | Description |
|-------------|-------------|
| `process_runtime_dotnet_gc_collections_count` | Garbage collection count by generation |
| `process_runtime_dotnet_gc_heap_size_bytes` | GC heap size |
| `process_runtime_dotnet_gc_pause_duration_seconds` | GC pause duration |
| `process_runtime_dotnet_thread_pool_threads_count` | Thread pool thread count |
| `process_runtime_dotnet_assemblies_count` | Number of loaded assemblies |
| `process_runtime_dotnet_exceptions_count` | Number of exceptions thrown |

#### Process Metrics

| Metric Name | Description |
|-------------|-------------|
| `process_cpu_time_seconds` | CPU time used by process |
| `process_memory_usage_bytes` | Memory usage by process |
| `process_network_io_bytes_transmitted` | Network bytes transmitted |
| `process_network_io_bytes_received` | Network bytes received |

---

## Prometheus Configuration

### Prometheus Server Setup

Create `prometheus.yml`:

```yaml
global:
  scrape_interval: 15s
  evaluation_interval: 15s

scrape_configs:
  - job_name: 'distributed-kernel-api'
    static_configs:
      - targets: ['localhost:5000']
    metrics_path: '/metrics'
    scrape_interval: 10s
    scrape_timeout: 5s
```

### Start Prometheus

**Using Docker:**

```bash
docker run -d \
  --name prometheus \
  -p 9090:9090 \
  -v $(pwd)/prometheus.yml:/etc/prometheus/prometheus.yml \
  prom/prometheus:latest
```

**Using Docker Compose:**

Add to `docker-compose.yml`:

```yaml
services:
  prometheus:
    image: prom/prometheus:latest
    container_name: prometheus
    ports:
      - "9090:9090"
    volumes:
      - ./prometheus.yml:/etc/prometheus/prometheus.yml
      - prometheus-data:/prometheus
    command:
      - '--config.file=/etc/prometheus/prometheus.yml'
      - '--storage.tsdb.path=/prometheus'
      - '--web.console.libraries=/usr/share/prometheus/console_libraries'
      - '--web.console.templates=/usr/share/prometheus/consoles'
    restart: unless-stopped

volumes:
  prometheus-data:
```

### Verify Scraping

1. Access Prometheus UI: http://localhost:9090
2. Go to **Status → Targets**
3. Verify `distributed-kernel-api` is **UP**
4. Query metrics: `deployments_started_total`

---

## Grafana Dashboard

### Grafana Setup

```bash
docker run -d \
  --name=grafana \
  -p 3000:3000 \
  -e "GF_SECURITY_ADMIN_PASSWORD=admin" \
  grafana/grafana:latest
```

### Add Prometheus Data Source

1. Access Grafana: http://localhost:3000 (admin/admin)
2. Go to **Configuration → Data Sources → Add data source**
3. Select **Prometheus**
4. URL: `http://prometheus:9090` (or `http://localhost:9090` if running locally)
5. Click **Save & Test**

### Import Dashboard JSON

Create `grafana-dashboard.json`:

```json
{
  "dashboard": {
    "title": "Distributed Kernel Orchestrator Metrics",
    "panels": [
      {
        "id": 1,
        "title": "Deployments Started (Last 24h)",
        "targets": [
          {
            "expr": "increase(deployments_started_total[24h])"
          }
        ],
        "type": "graph"
      },
      {
        "id": 2,
        "title": "Deployment Success Rate",
        "targets": [
          {
            "expr": "rate(deployments_completed_total[5m]) / rate(deployments_started_total[5m])"
          }
        ],
        "type": "gauge"
      },
      {
        "id": 3,
        "title": "Deployment Duration (p95)",
        "targets": [
          {
            "expr": "histogram_quantile(0.95, rate(deployment_duration_seconds_bucket[5m]))"
          }
        ],
        "type": "graph"
      },
      {
        "id": 4,
        "title": "Approval Requests by Environment",
        "targets": [
          {
            "expr": "sum by(environment) (approval_requests_total)"
          }
        ],
        "type": "piechart"
      },
      {
        "id": 5,
        "title": "HTTP Request Rate",
        "targets": [
          {
            "expr": "rate(http_server_request_duration_seconds_count[5m])"
          }
        ],
        "type": "graph"
      },
      {
        "id": 6,
        "title": "HTTP Request Duration (p95)",
        "targets": [
          {
            "expr": "histogram_quantile(0.95, rate(http_server_request_duration_seconds_bucket[5m]))"
          }
        ],
        "type": "graph"
      }
    ]
  }
}
```

**Import:**
1. Go to **Dashboards → Import**
2. Upload `grafana-dashboard.json`
3. Select Prometheus data source
4. Click **Import**

---

## Useful Prometheus Queries (PromQL)

### Deployment Queries

```promql
# Total deployments started (all time)
deployments_started_total

# Deployments started in last 1 hour
increase(deployments_started_total[1h])

# Deployment rate (per second) in last 5 minutes
rate(deployments_started_total[5m])

# Deployments by environment
sum by(environment) (deployments_started_total)

# Deployments by strategy
sum by(strategy) (deployments_started_total)

# Deployment success rate (%)
100 * (rate(deployments_completed_total[5m]) / rate(deployments_started_total[5m]))

# Failed deployments rate
rate(deployments_failed_total[5m])

# Deployment duration p50 (median)
histogram_quantile(0.50, rate(deployment_duration_seconds_bucket[5m]))

# Deployment duration p95
histogram_quantile(0.95, rate(deployment_duration_seconds_bucket[5m]))

# Deployment duration p99
histogram_quantile(0.99, rate(deployment_duration_seconds_bucket[5m]))

# Average deployment duration
rate(deployment_duration_seconds_sum[5m]) / rate(deployment_duration_seconds_count[5m])
```

### Approval Queries

```promql
# Approval requests created (last 24h)
increase(approval_requests_total[24h])

# Approval grant rate (%)
100 * (rate(approvals_granted_total[1h]) / rate(approval_requests_total[1h]))

# Approval rejection rate (%)
100 * (rate(approvals_rejected_total[1h]) / rate(approval_requests_total[1h]))

# Approvals by environment
sum by(environment) (approvals_granted_total)
```

### HTTP Performance Queries

```promql
# HTTP request rate (requests/sec)
rate(http_server_request_duration_seconds_count[5m])

# HTTP request rate by endpoint
sum by(http_route) (rate(http_server_request_duration_seconds_count[5m]))

# HTTP request rate by status code
sum by(http_response_status_code) (rate(http_server_request_duration_seconds_count[5m]))

# HTTP error rate (4xx + 5xx)
sum(rate(http_server_request_duration_seconds_count{http_response_status_code=~"4..|5.."}[5m]))

# HTTP latency p95
histogram_quantile(0.95, rate(http_server_request_duration_seconds_bucket[5m]))

# HTTP latency by endpoint
histogram_quantile(0.95, sum by(http_route, le) (rate(http_server_request_duration_seconds_bucket[5m])))
```

### .NET Runtime Queries

```promql
# GC collections (Gen 0, Gen 1, Gen 2)
rate(process_runtime_dotnet_gc_collections_count[5m])

# GC heap size (MB)
process_runtime_dotnet_gc_heap_size_bytes / 1024 / 1024

# Thread pool thread count
process_runtime_dotnet_thread_pool_threads_count

# Exceptions thrown rate
rate(process_runtime_dotnet_exceptions_count[5m])

# Memory usage (MB)
process_memory_usage_bytes / 1024 / 1024

# CPU usage (%)
rate(process_cpu_time_seconds[1m]) * 100
```

---

## Alert Rules

Create `alert-rules.yml`:

```yaml
groups:
  - name: deployment_alerts
    interval: 30s
    rules:
      # High deployment failure rate
      - alert: HighDeploymentFailureRate
        expr: |
          (rate(deployments_failed_total[5m]) / rate(deployments_started_total[5m])) > 0.1
        for: 5m
        labels:
          severity: warning
        annotations:
          summary: "High deployment failure rate detected"
          description: "Deployment failure rate is {{ $value | humanizePercentage }} (>10%)"

      # Slow deployments
      - alert: SlowDeployments
        expr: |
          histogram_quantile(0.95, rate(deployment_duration_seconds_bucket[5m])) > 300
        for: 10m
        labels:
          severity: warning
        annotations:
          summary: "Deployments are slow"
          description: "95th percentile deployment duration is {{ $value }}s (>5min)"

      # High HTTP error rate
      - alert: HighHTTPErrorRate
        expr: |
          sum(rate(http_server_request_duration_seconds_count{http_response_status_code=~"5.."}[5m]))
          / sum(rate(http_server_request_duration_seconds_count[5m])) > 0.05
        for: 5m
        labels:
          severity: critical
        annotations:
          summary: "High HTTP 5xx error rate"
          description: "HTTP 5xx error rate is {{ $value | humanizePercentage }} (>5%)"

      # High approval rejection rate
      - alert: HighApprovalRejectionRate
        expr: |
          (rate(approvals_rejected_total[1h]) / rate(approval_requests_total[1h])) > 0.5
        for: 1h
        labels:
          severity: info
        annotations:
          summary: "High approval rejection rate"
          description: "Approval rejection rate is {{ $value | humanizePercentage }} (>50%)"

      # Memory usage high
      - alert: HighMemoryUsage
        expr: |
          (process_memory_usage_bytes / 1024 / 1024 / 1024) > 2
        for: 10m
        labels:
          severity: warning
        annotations:
          summary: "High memory usage"
          description: "Memory usage is {{ $value }}GB (>2GB)"

      # GC pause time high
      - alert: HighGCPauseTime
        expr: |
          rate(process_runtime_dotnet_gc_pause_duration_seconds_sum[5m]) > 0.1
        for: 5m
        labels:
          severity: warning
        annotations:
          summary: "High GC pause time"
          description: "GC pause time is {{ $value }}s/s (>10% of time in GC)"
```

**Load alert rules in Prometheus:**

Update `prometheus.yml`:

```yaml
rule_files:
  - 'alert-rules.yml'

alerting:
  alertmanagers:
    - static_configs:
        - targets:
            - 'alertmanager:9093'
```

---

## Production Best Practices

### 1. Retention and Storage

**Prometheus:**
- Default retention: 15 days
- Increase for long-term storage: `--storage.tsdb.retention.time=90d`
- Monitor disk usage: Prometheus stores ~2-3 bytes per sample

**Long-term storage:** Use Thanos, Cortex, or VictoriaMetrics for multi-year retention.

### 2. High Availability

**Multiple Prometheus instances:**
- Run 2+ Prometheus instances scraping the same targets
- Use federation or load balancer for queries
- Ensures metrics collection continues during Prometheus restarts

### 3. Security

**Authentication:**
- `/metrics` endpoint has NO authentication (by design)
- Secure with firewall rules (allow only Prometheus server IP)
- Use HTTPS for Prometheus server

**Network isolation:**
- Run Prometheus in private network
- Use VPN/bastion host for Grafana access

### 4. Performance

**Scrape interval:**
- Default: 15s (suitable for most applications)
- Increase for high-cardinality metrics: 30s-60s
- Decrease for real-time monitoring: 5s-10s

**Cardinality management:**
- Limit unique label combinations (<100k per metric)
- Avoid high-cardinality labels (user IDs, request IDs)
- Use aggregation for high-volume metrics

### 5. Monitoring Prometheus

**Monitor the monitor:**
- Track Prometheus scrape duration: `scrape_duration_seconds`
- Track sample ingestion rate: `prometheus_tsdb_head_samples_appended_total`
- Alert on scrape failures: `up{job="distributed-kernel-api"} == 0`

---

## Troubleshooting

### Metrics Endpoint Not Accessible

**Problem:** `curl http://localhost:5000/metrics` returns 404

**Solution:**
1. Verify API is running: `curl http://localhost:5000/health`
2. Check logs for: `Prometheus metrics endpoint enabled at /metrics`
3. Verify OpenTelemetry package installed: `dotnet list package | grep Prometheus`

### No Custom Metrics Appearing

**Problem:** `/metrics` shows ASP.NET Core metrics but not deployment metrics

**Solution:**
1. Verify `DeploymentMetrics` is registered in `Program.cs`
2. Check meter name matches: `AddMeter(DeploymentMetrics.MeterName)`
3. Ensure metrics are being recorded (check application logs)
4. Query specific metric: `curl http://localhost:5000/metrics | grep deployments_started_total`

### Prometheus Not Scraping

**Problem:** Prometheus UI shows target as DOWN

**Solution:**
1. Check Prometheus logs: `docker logs prometheus`
2. Verify API is accessible from Prometheus: `curl http://distributed-kernel-api:5000/metrics`
3. Check `prometheus.yml` configuration (correct target, port)
4. Verify firewall allows Prometheus → API traffic

### High Memory Usage

**Problem:** API memory usage increases over time

**Solution:**
1. Check metric cardinality: `curl http://localhost:5000/metrics | wc -l`
2. Limit label values (avoid unbounded labels)
3. Monitor GC metrics: `process_runtime_dotnet_gc_heap_size_bytes`
4. Increase scrape interval if needed

---

## API Reference

### Metrics Endpoint

```http
GET /metrics HTTP/1.1
Host: localhost:5000
```

**Response:**

```http
HTTP/1.1 200 OK
Content-Type: application/openmetrics-text; version=1.0.0; charset=utf-8

# HELP deployments_started_total Total number of deployments started
# TYPE deployments_started_total counter
deployments_started_total{environment="Development",strategy="Direct",module="test-module"} 42
...
```

**Content-Type:** `application/openmetrics-text` (OpenMetrics format, Prometheus-compatible)

**Authentication:** None required (bypasses JWT authentication)

**Rate Limiting:** Bypassed (no rate limits on `/metrics`)

**Caching:** No caching (real-time metrics)

---

## Implementation Details

### DeploymentMetrics Class

**File:** `src/HotSwap.Distributed.Infrastructure/Metrics/DeploymentMetrics.cs`

**Usage example:**

```csharp
// Inject DeploymentMetrics
public class DeploymentPipeline
{
    private readonly DeploymentMetrics _metrics;

    public DeploymentPipeline(DeploymentMetrics metrics)
    {
        _metrics = metrics;
    }

    public async Task<DeploymentResult> ExecuteAsync(DeploymentRequest request)
    {
        var stopwatch = Stopwatch.StartNew();

        // Record deployment started
        _metrics.RecordDeploymentStarted(
            environment: request.Environment,
            strategy: request.Strategy.ToString(),
            moduleName: request.ModuleName);

        try
        {
            // Execute deployment...
            var result = await DoDeploymentAsync(request);

            // Record success
            _metrics.RecordDeploymentCompleted(
                environment: request.Environment,
                strategy: request.Strategy.ToString(),
                moduleName: request.ModuleName,
                durationSeconds: stopwatch.Elapsed.TotalSeconds);

            // Record modules deployed
            _metrics.RecordModulesDeployed(
                count: result.NodesDeployed,
                environment: request.Environment,
                moduleName: request.ModuleName);

            return result;
        }
        catch (Exception ex)
        {
            // Record failure
            _metrics.RecordDeploymentFailed(
                environment: request.Environment,
                strategy: request.Strategy.ToString(),
                moduleName: request.ModuleName,
                durationSeconds: stopwatch.Elapsed.TotalSeconds,
                reason: ex.GetType().Name);

            throw;
        }
    }
}
```

### Configuration

**File:** `src/HotSwap.Distributed.Api/Program.cs`

```csharp
// Register DeploymentMetrics
builder.Services.AddSingleton<DeploymentMetrics>();

// Configure OpenTelemetry with Prometheus
builder.Services.AddOpenTelemetry()
    .WithMetrics(metricsProviderBuilder =>
    {
        metricsProviderBuilder
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation()
            .AddProcessInstrumentation()
            .AddMeter(DeploymentMetrics.MeterName)
            .AddPrometheusExporter();
    });

// Map /metrics endpoint
app.MapPrometheusScrapingEndpoint();
```

---

## Next Steps

1. ✅ **Completed:** Prometheus metrics exporter implemented
2. ⏳ **Recommended:** Create Grafana dashboard and import to Grafana Cloud
3. ⏳ **Recommended:** Set up AlertManager for Prometheus alerts
4. ⏳ **Optional:** Integrate with long-term storage (Thanos/Cortex)
5. ⏳ **Optional:** Add custom metrics for canary health checks, rollback operations

---

## Related Documentation

- [TESTING.md](../TESTING.md) - Testing metrics collection
- [PROJECT_STATUS_REPORT.md](../PROJECT_STATUS_REPORT.md) - Production readiness status
- [TASK_LIST.md](../TASK_LIST.md) - Task #7 details

---

## References

- [OpenTelemetry Metrics](https://opentelemetry.io/docs/concepts/signals/metrics/)
- [Prometheus Documentation](https://prometheus.io/docs/)
- [Prometheus Best Practices](https://prometheus.io/docs/practices/)
- [PromQL Basics](https://prometheus.io/docs/prometheus/latest/querying/basics/)
- [Grafana Getting Started](https://grafana.com/docs/grafana/latest/getting-started/)
- [OpenTelemetry .NET Metrics](https://github.com/open-telemetry/opentelemetry-dotnet/tree/main/docs/metrics)

---

**Last Updated:** 2025-11-19
**Contributors:** Claude AI
**License:** MIT
