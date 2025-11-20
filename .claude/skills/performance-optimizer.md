# Performance Optimizer Skill

**Version:** 1.0.0
**Last Updated:** 2025-11-20
**Skill Type:** Performance & Scalability
**Estimated Time:** 2-3 days per optimization cycle
**Complexity:** Medium-High

---

## Purpose

Systematic performance optimization for .NET applications using load testing, profiling, and proven optimization patterns.

**Use this skill when:**
- Preparing for production load (Task #8: Load testing)
- Investigating slow endpoints or high latency
- Optimizing resource usage (CPU, memory, database)
- Establishing performance baselines
- Validating scalability requirements

**This skill addresses:**
- Task #8: Load Testing and Performance Benchmarks (2-3 days)
- Performance bottlenecks across the stack
- Scalability validation for production deployment

---

## Prerequisites

**Required Tools:**

- **k6** - Modern load testing tool (recommended)
- **dotnet-trace** - .NET performance profiling
- **dotnet-counters** - Real-time performance metrics
- **Optional**: BenchmarkDotNet for micro-benchmarks

**Install k6:**

```bash
# Linux
sudo gpg -k
sudo gpg --no-default-keyring --keyring /usr/share/keyrings/k6-archive-keyring.gpg --keyserver hkp://keyserver.ubuntu.com:80 --recv-keys C5AD17C747E3415A3642D57D77C6C491D6AC1D69
echo "deb [signed-by=/usr/share/keyrings/k6-archive-keyring.gpg] https://dl.k6.io/deb stable main" | sudo tee /etc/apt/sources.list.d/k6.list
sudo apt-get update
sudo apt-get install k6

# macOS
brew install k6

# Windows
choco install k6

# Verify
k6 version
```

**Install .NET performance tools:**

```bash
dotnet tool install --global dotnet-trace
dotnet tool install --global dotnet-counters
dotnet tool install --global dotnet-dump

# Verify
dotnet-trace --version
dotnet-counters --version
```

---

## Phase 1: Establish Performance Baseline

### Step 1.1: Identify Critical Endpoints

```bash
# Review API endpoints by criticality
# Priority order:
# 1. Deployment endpoints (most critical)
# 2. Health/metrics endpoints (high frequency)
# 3. Approval workflow endpoints
# 4. Authentication endpoints

# Example critical endpoints:
POST   /api/v1/deployments              # Create deployment
GET    /api/v1/deployments/{id}         # Check deployment status
POST   /api/v1/deployments/{id}/rollback
GET    /api/v1/health                   # Health check
POST   /api/v1/auth/login               # Authentication
```

### Step 1.2: Define Performance Requirements

**Create performance-requirements.md:**

```markdown
# Performance Requirements

## Response Time (95th percentile)
- Health check: <50ms
- Authentication: <200ms
- Deployment creation: <500ms
- Deployment status: <100ms
- Rollback: <1000ms

## Throughput
- Concurrent deployments: 50+
- API requests/sec: 1000+
- Health checks/sec: 100+

## Resource Limits
- Memory: <512MB per instance
- CPU: <70% average load
- Database connections: <50 per instance
```

### Step 1.3: Create k6 Load Test Scripts

**tests/k6/health-check-load.js:**

```javascript
import http from 'k6/http';
import { check, sleep } from 'k6';

export const options = {
  stages: [
    { duration: '30s', target: 10 },   // Ramp-up to 10 users
    { duration: '1m', target: 50 },    // Ramp-up to 50 users
    { duration: '2m', target: 50 },    // Stay at 50 users
    { duration: '30s', target: 0 },    // Ramp-down to 0
  ],
  thresholds: {
    http_req_duration: ['p(95)<50'],   // 95% of requests < 50ms
    http_req_failed: ['rate<0.01'],    // Error rate < 1%
  },
};

export default function () {
  const res = http.get('http://localhost:5000/health');
  
  check(res, {
    'status is 200': (r) => r.status === 200,
    'response time < 50ms': (r) => r.timings.duration < 50,
  });
  
  sleep(1);
}
```

**tests/k6/deployment-load.js:**

```javascript
import http from 'k6/http';
import { check, sleep } from 'k6';

const BASE_URL = 'http://localhost:5000';
let authToken;

export function setup() {
  // Authenticate once
  const loginRes = http.post(`${BASE_URL}/api/v1/auth/login`, JSON.stringify({
    username: 'admin',
    password: 'Admin123!',
  }), {
    headers: { 'Content-Type': 'application/json' },
  });
  
  return { token: loginRes.json('token') };
}

export const options = {
  stages: [
    { duration: '1m', target: 10 },    // Ramp-up
    { duration: '3m', target: 10 },    // Sustained load
    { duration: '30s', target: 0 },    // Ramp-down
  ],
  thresholds: {
    http_req_duration: ['p(95)<500'],  // 95% < 500ms
    http_req_failed: ['rate<0.05'],    // Error rate < 5%
  },
};

export default function (data) {
  const headers = {
    'Content-Type': 'application/json',
    'Authorization': `Bearer ${data.token}`,
  };
  
  // Create deployment
  const createRes = http.post(`${BASE_URL}/api/v1/deployments`, JSON.stringify({
    moduleName: 'test-module',
    version: '1.0.0',
    targetEnvironment: 'Development',
    strategy: 'Direct',
  }), { headers });
  
  check(createRes, {
    'deployment created': (r) => r.status === 202,
    'response time ok': (r) => r.timings.duration < 500,
  });
  
  if (createRes.status === 202) {
    const executionId = createRes.json('executionId');
    
    // Poll for completion (max 10 times)
    for (let i = 0; i < 10; i++) {
      sleep(1);
      
      const statusRes = http.get(`${BASE_URL}/api/v1/deployments/${executionId}`, { headers });
      
      if (statusRes.json('status') === 'Completed') {
        break;
      }
    }
  }
  
  sleep(2);
}
```

### Step 1.4: Run Baseline Tests

```bash
# Start application
dotnet run --project src/HotSwap.Distributed.Api/ &
API_PID=$!

# Wait for startup
sleep 5

# Run health check load test
k6 run tests/k6/health-check-load.js

# Expected output:
#   checks.........................: 100.00% ✓ 12000 ✗ 0
#   http_req_duration..............: avg=15ms min=5ms med=12ms max=45ms p(95)=25ms
#   http_reqs......................: 12000   100/s

# Run deployment load test
k6 run tests/k6/deployment-load.js

# Expected output:
#   checks.........................: 98.50%  ✓ 1970  ✗ 30
#   http_req_duration..............: avg=150ms min=50ms med=120ms max=450ms p(95)=350ms
#   http_reqs......................: 2000    33.3/s

# Save results
k6 run --out json=baseline-health.json tests/k6/health-check-load.js
k6 run --out json=baseline-deployment.json tests/k6/deployment-load.js

# Stop application
kill $API_PID
```

### Step 1.5: Document Baseline

**Create tests/k6/BASELINE.md:**

```markdown
# Performance Baseline

**Date:** 2025-11-20
**Version:** 1.0.0
**Environment:** Development (local)

## Health Check Endpoint
- **p95 latency:** 25ms ✅ (target: <50ms)
- **Throughput:** 100 req/s
- **Error rate:** 0%

## Deployment Endpoint
- **p95 latency:** 350ms ✅ (target: <500ms)
- **Throughput:** 33.3 req/s
- **Error rate:** 1.5% (30 failures in 2000 requests)

## Issues Identified
1. Deployment endpoint has 1.5% error rate (investigate)
2. Some deployment requests timeout after 10s polling
3. Memory usage increases during sustained load

## Next Steps
- Investigate deployment failures
- Optimize polling mechanism
- Profile memory usage
```

---

## Phase 2: Identify Bottlenecks

### Step 2.1: Profile with dotnet-trace

```bash
# Start application
dotnet run --project src/HotSwap.Distributed.Api/ &
API_PID=$!

# Start profiling
dotnet-trace collect --process-id $API_PID --providers Microsoft-Diagnostics-DiagnosticSource

# In another terminal, run load test
k6 run tests/k6/deployment-load.js

# Stop profiling (Ctrl+C)
# Output: trace.nettrace

# Analyze trace (open in Visual Studio or PerfView)
# Or convert to speedscope format:
dotnet-trace convert trace.nettrace --format speedscope
# Open speedscope.json at https://www.speedscope.app/
```

**Look for:**
- Methods with high CPU time
- Blocking I/O operations
- Database queries taking >100ms
- Allocations causing GC pressure

### Step 2.2: Monitor Real-Time Metrics

```bash
# Monitor counters during load test
dotnet-counters monitor --process-id $API_PID

# Watch for:
# - CPU Usage (%)             → Should be < 70%
# - Working Set (MB)          → Should be < 512MB
# - GC Heap Size (MB)         → Should be stable
# - Exception Count           → Should be 0
# - ThreadPool Queue Length   → Should be < 10
```

### Step 2.3: Database Profiling

**Enable EF Core logging:**

```csharp
// appsettings.Development.json
{
  "Logging": {
    "LogLevel": {
      "Microsoft.EntityFrameworkCore.Database.Command": "Information"
    }
  }
}
```

**Check slow queries:**

```bash
# Run load test and capture logs
dotnet run | grep "Executed DbCommand" | awk '{print $NF}' | sort -n | tail -10

# Identify queries taking >100ms
# Example output:
# 125ms → SELECT * FROM audit_logs WHERE timestamp > @p0
# 230ms → SELECT * FROM deployments ORDER BY created_at DESC
```

---

## Phase 3: Apply Optimizations

### Step 3.1: Common Optimization Patterns

**❌ Problem: N+1 Query**

```csharp
// SLOW: N+1 queries
public async Task<List<DeploymentResponse>> GetAllDeploymentsAsync()
{
    var deployments = await _context.Deployments.ToListAsync();
    
    // This triggers N additional queries (one per deployment)
    foreach (var deployment in deployments)
    {
        deployment.Approvals = await _context.Approvals
            .Where(a => a.DeploymentId == deployment.Id)
            .ToListAsync();
    }
    
    return deployments;
}

// FAST: Single query with Include
public async Task<List<DeploymentResponse>> GetAllDeploymentsAsync()
{
    var deployments = await _context.Deployments
        .Include(d => d.Approvals)  // Eager loading
        .ToListAsync();
    
    return deployments;
}
```

**❌ Problem: No Caching**

```csharp
// SLOW: Database hit on every request
public async Task<User?> GetUserAsync(string username)
{
    return await _context.Users
        .FirstOrDefaultAsync(u => u.Username == username);
}

// FAST: In-memory cache
private readonly IMemoryCache _cache;

public async Task<User?> GetUserAsync(string username)
{
    return await _cache.GetOrCreateAsync($"user:{username}", async entry =>
    {
        entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
        return await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
    });
}
```

**❌ Problem: Blocking Async**

```csharp
// SLOW: Blocking on async (deadlock risk)
public string GetDeploymentStatusSync(string id)
{
    var result = GetDeploymentStatusAsync(id).Result;  // Deadlock!
    return result.Status;
}

// FAST: Async all the way
public async Task<string> GetDeploymentStatusAsync(string id)
{
    var result = await _deploymentTracker.GetResultAsync(id);
    return result.Status.ToString();
}
```

**❌ Problem: Large Result Sets**

```csharp
// SLOW: Load all audit logs (millions of rows)
public async Task<List<AuditLog>> GetAuditLogsAsync()
{
    return await _context.AuditLogs.ToListAsync();  // OOM!
}

// FAST: Pagination
public async Task<PagedResult<AuditLog>> GetAuditLogsAsync(int page, int pageSize)
{
    var query = _context.AuditLogs.OrderByDescending(a => a.Timestamp);
    
    var totalCount = await query.CountAsync();
    var items = await query
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .ToListAsync();
    
    return new PagedResult<AuditLog>
    {
        Items = items,
        TotalCount = totalCount,
        Page = page,
        PageSize = pageSize
    };
}
```

### Step 3.2: Apply and Verify

```bash
# After each optimization:

# 1. Run load test
k6 run tests/k6/deployment-load.js

# 2. Compare with baseline
k6 run --out json=optimized-deployment.json tests/k6/deployment-load.js

# 3. Calculate improvement
# Before: p95=350ms, 33.3 req/s
# After:  p95=180ms, 55.5 req/s
# Improvement: 48.5% faster, 66% more throughput

# 4. Document in OPTIMIZATIONS.md
```

---

## Phase 4: Scalability Testing

### Step 4.1: Stress Test

```javascript
// tests/k6/stress-test.js
export const options = {
  stages: [
    { duration: '2m', target: 100 },   // Ramp to 100 users
    { duration: '5m', target: 100 },   // Stay at 100
    { duration: '2m', target: 200 },   // Push to 200
    { duration: '5m', target: 200 },   // Stay at 200
    { duration: '2m', target: 0 },     // Ramp down
  ],
};
```

**Run and monitor:**

```bash
# Terminal 1: Run stress test
k6 run tests/k6/stress-test.js

# Terminal 2: Monitor metrics
dotnet-counters monitor --process-id $(pgrep -f "dotnet.*Api")

# Look for:
# - When does throughput plateau?
# - When do errors start appearing?
# - When does memory spike?
# - When does CPU hit 100%?
```

### Step 4.2: Soak Test (Endurance)

```javascript
// tests/k6/soak-test.js
export const options = {
  stages: [
    { duration: '5m', target: 50 },    // Ramp to moderate load
    { duration: '4h', target: 50 },    // Sustain for 4 hours
    { duration: '5m', target: 0 },     // Ramp down
  ],
};
```

**Check for memory leaks:**

```bash
# Run soak test overnight
k6 run tests/k6/soak-test.js &

# Monitor memory every 15 minutes
while true; do
  echo "$(date): $(dotnet-counters monitor --process-id $(pgrep -f dotnet) --counters System.Runtime[working-set] -n 1 | grep working-set)"
  sleep 900
done

# Expected: Memory should stabilize, not grow linearly
# If memory grows → memory leak, investigate with dotnet-dump
```

---

## Phase 5: Production Monitoring

### Step 5.1: Add Performance Metrics

```csharp
// Infrastructure/Metrics/PerformanceMetricsProvider.cs
public class PerformanceMetricsProvider
{
    private readonly Histogram<double> _requestDuration;
    
    public PerformanceMetricsProvider(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create("HotSwap.Api");
        _requestDuration = meter.CreateHistogram<double>("http.server.request.duration", "ms");
    }
    
    public void RecordRequestDuration(string endpoint, double durationMs)
    {
        _requestDuration.Record(durationMs, new KeyValuePair<string, object?>("endpoint", endpoint));
    }
}
```

### Step 5.2: Configure Alerts

**Prometheus alert rules:**

```yaml
# prometheus-alerts.yml
groups:
  - name: performance
    rules:
      - alert: HighLatency
        expr: histogram_quantile(0.95, http_server_request_duration_bucket) > 500
        for: 5m
        annotations:
          summary: "p95 latency above 500ms"
      
      - alert: HighErrorRate
        expr: rate(http_server_requests_total{status=~"5.."}[5m]) > 0.05
        for: 2m
        annotations:
          summary: "Error rate above 5%"
```

---

## Success Criteria

- [ ] Baseline established for all critical endpoints
- [ ] Load tests passing with <5% error rate
- [ ] p95 latency meets requirements
- [ ] Throughput meets or exceeds targets
- [ ] Memory usage stable during soak test
- [ ] No memory leaks detected
- [ ] Optimizations documented
- [ ] Production monitoring configured
- [ ] Task #8 marked complete in TASK_LIST.md

---

**Last Updated:** 2025-11-20
**Version:** 1.0.0
**Completes:** Task #8 (Load Testing and Performance Benchmarks)
**Estimated ROI:** 2-3 days of optimization saves weeks of production issues
