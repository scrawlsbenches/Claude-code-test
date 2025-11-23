# Gateway Routing Configuration Guide

**Version:** 1.0.0
**Last Updated:** 2025-11-23

---

## Overview

The HotSwap API Gateway supports 5 routing strategies for backend selection. Each strategy determines how incoming requests are distributed across available healthy backends.

### Strategy Selection

| Strategy | Use Case | Complexity | Performance |
|----------|----------|------------|-------------|
| RoundRobin | Even load distribution | Low | Excellent |
| WeightedRoundRobin | Canary deployments, capacity-based routing | Medium | Excellent |
| LeastConnections | Dynamic load balancing | Medium | Good |
| IPHash | Sticky sessions | Low | Excellent |
| HeaderBased | A/B testing, feature flags | Medium | Good |

---

## 1. Round Robin Strategy

### Overview

**Pattern:** Even Distribution
**Latency:** Lowest
**Complexity:** Low

**Use Case:** Distribute requests evenly across all healthy backends.

### Behavior

- Routes to backends in sequential order
- Thread-safe counter wraps around at list end
- Skips unhealthy backends automatically
- No session affinity

### Algorithm

```csharp
public class RoundRobinStrategy : IRoutingStrategy
{
    private int _currentIndex = 0;

    public async Task<Backend> SelectBackendAsync(
        GatewayRoute route,
        List<Backend> backends,
        HttpRequest request)
    {
        var healthyBackends = backends.Where(b => b.IsAvailable()).ToList();

        if (healthyBackends.Count == 0)
            throw new NoHealthyBackendsException();

        // Thread-safe index increment
        var index = Interlocked.Increment(ref _currentIndex) % healthyBackends.Count;
        return healthyBackends[index];
    }
}
```

### Request Flow

```
Request 1 → Backend A
Request 2 → Backend B
Request 3 → Backend C
Request 4 → Backend A (wraps around)
Request 5 → Backend B
```

### Configuration

```json
{
  "routeId": "api-users",
  "pathPattern": "/api/users/*",
  "backends": [
    {
      "name": "users-1",
      "url": "http://users-1:8080",
      "weight": 100
    },
    {
      "name": "users-2",
      "url": "http://users-2:8080",
      "weight": 100
    },
    {
      "name": "users-3",
      "url": "http://users-3:8080",
      "weight": 100
    }
  ],
  "strategy": "RoundRobin"
}
```

### Performance Characteristics

- **Latency:** ~1ms (backend selection)
- **Throughput:** No limit (stateless except counter)
- **Scalability:** Excellent (O(1) selection)
- **Fault Tolerance:** Automatic unhealthy backend exclusion

### When to Use

✅ **Good For:**
- Homogeneous backends (same capacity)
- Simple load distribution
- Stateless applications
- High-throughput scenarios

❌ **Not Good For:**
- Backends with different capacities (use Weighted)
- Sticky sessions required (use IPHash)
- Dynamic load balancing (use LeastConnections)

---

## 2. Weighted Round Robin Strategy

### Overview

**Pattern:** Capacity-Based Distribution
**Latency:** Low
**Complexity:** Medium

**Use Case:** Distribute traffic based on backend capacity or for canary deployments.

### Behavior

- Routes based on backend weights (0-100)
- Higher weight = more traffic
- Perfect for canary deployments (e.g., 90% stable, 10% canary)
- Skips backends with weight=0

### Algorithm

```csharp
public class WeightedRoundRobinStrategy : IRoutingStrategy
{
    public async Task<Backend> SelectBackendAsync(
        GatewayRoute route,
        List<Backend> backends,
        HttpRequest request)
    {
        var healthyBackends = backends
            .Where(b => b.IsAvailable() && b.Weight > 0)
            .ToList();

        if (healthyBackends.Count == 0)
            throw new NoHealthyBackendsException();

        // Calculate total weight
        var totalWeight = healthyBackends.Sum(b => b.Weight);

        // Generate random number [0, totalWeight)
        var random = Random.Shared.Next(0, totalWeight);

        // Select backend based on weight ranges
        var cumulative = 0;
        foreach (var backend in healthyBackends)
        {
            cumulative += backend.Weight;
            if (random < cumulative)
                return backend;
        }

        return healthyBackends.Last(); // Fallback
    }
}
```

### Traffic Distribution Example

```
Backend A: weight=70 → receives ~70% of traffic
Backend B: weight=20 → receives ~20% of traffic
Backend C: weight=10 → receives ~10% of traffic (canary)
```

### Configuration

**Canary Deployment (10% canary):**

```json
{
  "routeId": "api-v2",
  "pathPattern": "/api/v2/*",
  "backends": [
    {
      "name": "api-v2-stable",
      "url": "http://api-v2-stable:8080",
      "weight": 90
    },
    {
      "name": "api-v2-canary",
      "url": "http://api-v2-canary:8080",
      "weight": 10
    }
  ],
  "strategy": "WeightedRoundRobin"
}
```

**Capacity-Based Routing:**

```json
{
  "backends": [
    {
      "name": "large-instance",
      "url": "http://large:8080",
      "weight": 60
    },
    {
      "name": "medium-instance",
      "url": "http://medium:8080",
      "weight": 30
    },
    {
      "name": "small-instance",
      "url": "http://small:8080",
      "weight": 10
    }
  ],
  "strategy": "WeightedRoundRobin"
}
```

### Canary Promotion Path

```
Phase 1: 90% stable, 10% canary → Monitor for 10 minutes
Phase 2: 75% stable, 25% canary → Monitor for 10 minutes
Phase 3: 50% stable, 50% canary → Monitor for 10 minutes
Phase 4: 25% stable, 75% canary → Monitor for 10 minutes
Phase 5: 0% stable, 100% canary → Canary becomes stable
```

### Performance Characteristics

- **Latency:** ~2ms (weight calculation)
- **Throughput:** No limit
- **Scalability:** Excellent (O(n) where n = backend count)
- **Fault Tolerance:** Automatic unhealthy backend exclusion

### When to Use

✅ **Good For:**
- Canary deployments
- Backends with different capacities
- Gradual traffic migration
- A/B testing with traffic control

❌ **Not Good For:**
- Sticky sessions (use IPHash)
- Precise per-request routing (use HeaderBased)

---

## 3. Least Connections Strategy

### Overview

**Pattern:** Dynamic Load Balancing
**Latency:** Medium
**Complexity:** Medium

**Use Case:** Route to backend with fewest active connections.

### Behavior

- Tracks active connections per backend
- Routes to backend with minimum connections
- Automatically balances load based on actual traffic
- Handles slow backends gracefully

### Algorithm

```csharp
public class LeastConnectionsStrategy : IRoutingStrategy
{
    public async Task<Backend> SelectBackendAsync(
        GatewayRoute route,
        List<Backend> backends,
        HttpRequest request)
    {
        var healthyBackends = backends
            .Where(b => b.IsAvailable())
            .ToList();

        if (healthyBackends.Count == 0)
            throw new NoHealthyBackendsException();

        // Select backend with minimum active connections
        var selected = healthyBackends
            .OrderBy(b => b.ActiveConnections)
            .ThenBy(b => Random.Shared.Next()) // Tie-breaker
            .First();

        // Increment connection count
        Interlocked.Increment(ref selected.ActiveConnections);

        return selected;
    }

    public void OnRequestComplete(Backend backend)
    {
        // Decrement connection count
        Interlocked.Decrement(ref backend.ActiveConnections);
    }
}
```

### Request Flow

```
Initial State:
Backend A: 0 connections
Backend B: 0 connections
Backend C: 0 connections

Request 1 → Backend A (A=1, B=0, C=0)
Request 2 → Backend B (A=1, B=1, C=0)
Request 3 → Backend C (A=1, B=1, C=1)
Request 4 → Backend A completes, Request 4 → Backend A (A=1, B=1, C=1)
```

### Configuration

```json
{
  "routeId": "api-slow-endpoints",
  "pathPattern": "/api/reports/*",
  "backends": [
    {
      "name": "worker-1",
      "url": "http://worker-1:8080"
    },
    {
      "name": "worker-2",
      "url": "http://worker-2:8080"
    },
    {
      "name": "worker-3",
      "url": "http://worker-3:8080"
    }
  ],
  "strategy": "LeastConnections"
}
```

### Performance Characteristics

- **Latency:** ~3ms (connection count check)
- **Throughput:** Excellent
- **Scalability:** Good (requires connection tracking)
- **Fault Tolerance:** Handles slow backends well

### When to Use

✅ **Good For:**
- Long-running requests (slow endpoints)
- Backends with varying response times
- Dynamic load balancing
- WebSocket connections

❌ **Not Good For:**
- Fast, uniform requests (overhead not worth it, use RoundRobin)
- Sticky sessions (use IPHash)

---

## 4. IP Hash Strategy

### Overview

**Pattern:** Consistent Hashing (Sticky Sessions)
**Latency:** Low
**Complexity:** Low

**Use Case:** Route requests from same client IP to same backend.

### Behavior

- Hashes client IP address
- Maps hash to backend index
- Provides sticky sessions (same client → same backend)
- Handles backend failures with fallback

### Algorithm

```csharp
public class IPHashStrategy : IRoutingStrategy
{
    public async Task<Backend> SelectBackendAsync(
        GatewayRoute route,
        List<Backend> backends,
        HttpRequest request)
    {
        var healthyBackends = backends
            .Where(b => b.IsAvailable())
            .ToList();

        if (healthyBackends.Count == 0)
            throw new NoHealthyBackendsException();

        // Get client IP
        var clientIP = request.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        // Hash IP to backend index
        var hash = GetHash(clientIP);
        var index = Math.Abs(hash) % healthyBackends.Count;

        return healthyBackends[index];
    }

    private int GetHash(string input)
    {
        return HashCode.Combine(input);
    }
}
```

### Request Flow

```
Client IP: 192.168.1.10 → Hash: 12345 → Backend A (always)
Client IP: 192.168.1.20 → Hash: 67890 → Backend C (always)
Client IP: 192.168.1.30 → Hash: 24680 → Backend B (always)

All subsequent requests from 192.168.1.10 → Backend A
```

### Configuration

```json
{
  "routeId": "session-required",
  "pathPattern": "/api/session/*",
  "backends": [
    {
      "name": "session-1",
      "url": "http://session-1:8080"
    },
    {
      "name": "session-2",
      "url": "http://session-2:8080"
    },
    {
      "name": "session-3",
      "url": "http://session-3:8080"
    }
  ],
  "strategy": "IPHash"
}
```

### Performance Characteristics

- **Latency:** ~1ms (hash calculation)
- **Throughput:** Excellent
- **Scalability:** Excellent (O(1) selection)
- **Fault Tolerance:** Rehashing when backends change

### Limitations

- Adding/removing backends changes hash distribution
- Not perfect for distributed caching (use consistent hashing for that)
- IP changes (mobile, VPN) break stickiness

### When to Use

✅ **Good For:**
- Session affinity required
- Stateful applications
- In-memory caching per backend
- WebSocket connections

❌ **Not Good For:**
- Stateless applications (unnecessary overhead)
- Frequent backend scaling (rehashing disrupts sessions)

---

## 5. Header-Based Strategy

### Overview

**Pattern:** Conditional Routing
**Latency:** Medium
**Complexity:** High

**Use Case:** Route based on request headers (A/B testing, feature flags).

### Behavior

- Evaluates request headers against routing rules
- Routes to specific backend based on header value
- Supports default backend (no match)
- Enables A/B testing and feature rollouts

### Algorithm

```csharp
public class HeaderBasedStrategy : IRoutingStrategy
{
    public async Task<Backend> SelectBackendAsync(
        GatewayRoute route,
        List<Backend> backends,
        HttpRequest request)
    {
        var config = route.StrategyConfig;
        var headerName = config.GetValueOrDefault("headerName", "X-Variant");
        var routingRules = config.GetValueOrDefault("routingRules", "{}");
        var defaultBackend = config.GetValueOrDefault("defaultBackend", "");

        // Check for header
        if (!request.Headers.TryGetValue(headerName, out var headerValue))
        {
            return GetBackendByName(backends, defaultBackend);
        }

        // Match header value to routing rule
        var rules = JsonSerializer.Deserialize<Dictionary<string, string>>(routingRules);
        if (rules.TryGetValue(headerValue, out var backendName))
        {
            return GetBackendByName(backends, backendName);
        }

        // No match, use default
        return GetBackendByName(backends, defaultBackend);
    }

    private Backend GetBackendByName(List<Backend> backends, string name)
    {
        var backend = backends.FirstOrDefault(b => b.Name == name && b.IsAvailable());
        if (backend == null)
            throw new NoHealthyBackendsException($"Backend '{name}' not found or unhealthy");
        return backend;
    }
}
```

### Request Flow

```
Request with X-Variant: control → backend-control
Request with X-Variant: test    → backend-test
Request with no header          → backend-control (default)
Request with X-Variant: unknown → backend-control (default)
```

### Configuration

**A/B Testing:**

```json
{
  "routeId": "feature-test",
  "pathPattern": "/api/features/*",
  "backends": [
    {
      "name": "feature-control",
      "url": "http://feature-control:8080"
    },
    {
      "name": "feature-test",
      "url": "http://feature-test:8080"
    }
  ],
  "strategy": "HeaderBased",
  "strategyConfig": {
    "headerName": "X-Feature-Variant",
    "routingRules": {
      "variant-a": "feature-control",
      "variant-b": "feature-test"
    },
    "defaultBackend": "feature-control"
  }
}
```

**Environment-Based Routing:**

```json
{
  "routeId": "multi-env",
  "pathPattern": "/api/*",
  "backends": [
    {
      "name": "prod-backend",
      "url": "http://prod:8080"
    },
    {
      "name": "staging-backend",
      "url": "http://staging:8080"
    },
    {
      "name": "dev-backend",
      "url": "http://dev:8080"
    }
  ],
  "strategy": "HeaderBased",
  "strategyConfig": {
    "headerName": "X-Environment",
    "routingRules": {
      "production": "prod-backend",
      "staging": "staging-backend",
      "development": "dev-backend"
    },
    "defaultBackend": "prod-backend"
  }
}
```

### Performance Characteristics

- **Latency:** ~2-3ms (header parsing)
- **Throughput:** Excellent
- **Scalability:** Good
- **Fault Tolerance:** Fallback to default backend

### When to Use

✅ **Good For:**
- A/B testing
- Feature flags
- Multi-tenant routing
- Environment-based routing
- API versioning

❌ **Not Good For:**
- Simple load balancing (use RoundRobin)
- Sticky sessions (use IPHash)

---

## Strategy Comparison

| Strategy | Latency | Throughput | Complexity | Session Affinity | Use Case |
|----------|---------|------------|------------|------------------|----------|
| RoundRobin | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐ | No | Even load distribution |
| WeightedRoundRobin | ⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐⭐ | No | Canary deployments |
| LeastConnections | ⭐⭐⭐ | ⭐⭐⭐⭐ | ⭐⭐⭐ | No | Dynamic load balancing |
| IPHash | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐ | Yes | Sticky sessions |
| HeaderBased | ⭐⭐⭐ | ⭐⭐⭐⭐ | ⭐⭐⭐⭐ | Conditional | A/B testing |

---

## Best Practices

### 1. Choose the Right Strategy

**Decision Tree:**
```
Need sticky sessions?
├─ Yes → IPHash
└─ No → Continue

Need canary deployments?
├─ Yes → WeightedRoundRobin
└─ No → Continue

Need A/B testing?
├─ Yes → HeaderBased
└─ No → Continue

Have long-running requests?
├─ Yes → LeastConnections
└─ No → RoundRobin (default)
```

### 2. Monitor Strategy Performance

Track metrics per strategy:
- Backend selection latency
- Request distribution across backends
- Failed backend selections
- Session affinity violations (IPHash)

### 3. Test Strategy Switching

Ensure zero downtime when changing strategies:
1. Deploy new configuration
2. Gracefully drain existing connections
3. Apply new strategy to new connections

### 4. Optimize for Your Workload

- **High Throughput:** RoundRobin or IPHash
- **Variable Request Duration:** LeastConnections
- **Progressive Rollouts:** WeightedRoundRobin
- **Complex Routing:** HeaderBased

---

## Troubleshooting

### Issue: Uneven Load Distribution

**Symptom:** Some backends receive significantly more traffic

**Solutions:**
1. Verify all backends have correct weights (WeightedRoundRobin)
2. Check for unhealthy backends (may cause skewed distribution)
3. Switch to LeastConnections for dynamic balancing
4. Monitor active connections per backend

### Issue: Session Affinity Broken

**Symptom:** Clients routed to different backends across requests

**Solutions:**
1. Verify using IPHash strategy
2. Check that client IP is consistent (not behind load balancer)
3. Ensure backends not frequently added/removed (causes rehashing)
4. Consider using cookie-based session affinity

### Issue: Slow Backend Gets Overloaded

**Symptom:** One backend consistently slow, but receives equal traffic

**Solutions:**
1. Switch from RoundRobin to LeastConnections
2. Or adjust weights in WeightedRoundRobin
3. Add health checks to detect slow backends
4. Increase circuit breaker sensitivity

---

**Last Updated:** 2025-11-23
**Version:** 1.0.0
