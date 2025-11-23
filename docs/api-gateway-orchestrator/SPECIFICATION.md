# HotSwap API Gateway Configuration Orchestrator - Technical Specification

**Version:** 1.0.0
**Date:** 2025-11-23
**Status:** Design Specification
**Authors:** Platform Architecture Team

---

## Table of Contents

1. [Executive Summary](#executive-summary)
2. [System Requirements](#system-requirements)
3. [Routing Patterns](#routing-patterns)
4. [Deployment Strategies](#deployment-strategies)
5. [Performance Requirements](#performance-requirements)
6. [Security Requirements](#security-requirements)
7. [Observability Requirements](#observability-requirements)
8. [Scalability Requirements](#scalability-requirements)

---

## Executive Summary

The HotSwap API Gateway Configuration Orchestrator provides enterprise-grade API gateway management built on the existing kernel orchestration platform. The system treats gateway configurations as hot-swappable modules, enabling zero-downtime updates and progressive deployment strategies.

### Key Innovations

1. **Hot-Swappable Configurations** - Gateway configs deployed via existing orchestration strategies
2. **Progressive Traffic Shifting** - Deployment strategies adapted for traffic management
3. **Full Request Tracing** - OpenTelemetry integration for end-to-end visibility
4. **Health-Based Routing** - Automatic traffic shifting based on backend health
5. **Zero Downtime** - Configuration updates without dropped requests

### Design Principles

1. **Leverage Existing Infrastructure** - Reuse authentication, tracing, metrics, approval workflows
2. **Clean Architecture** - Maintain 4-layer separation (API, Orchestrator, Infrastructure, Domain)
3. **Test-Driven Development** - 85%+ test coverage with comprehensive unit/integration tests
4. **Production-Ready** - Security, observability, and reliability from day one
5. **Performance First** - 50,000+ req/sec throughput, < 50ms p99 latency

---

## System Requirements

### Functional Requirements

#### FR-GW-001: Route Management
**Priority:** Critical
**Description:** System MUST support creating and managing gateway routes

**Requirements:**
- Create route with path pattern (supports wildcards)
- Associate multiple backend services
- Configure routing strategy (RoundRobin, Weighted, etc.)
- Set route-level timeouts and retries
- Enable/disable routes dynamically
- Version route configurations

**API Endpoint:**
```
POST /api/v1/gateway/routes
```

**Acceptance Criteria:**
- Path patterns validated (supports /path/*, /path/**, /path/{id})
- Backend URLs validated (HTTPS required in production)
- Route configuration persisted to database
- Changes applied without dropping active requests
- Old connections drained gracefully (30s timeout)

---

#### FR-GW-002: Backend Service Management
**Priority:** Critical
**Description:** System MUST support managing backend service definitions

**Requirements:**
- Register backend service with URL and health check
- Configure backend weight (for weighted routing)
- Set connection pool settings (min/max connections)
- Configure timeout settings (connect, read, write)
- Enable/disable backends dynamically
- Track backend health status

**API Endpoints:**
```
POST   /api/v1/gateway/backends
GET    /api/v1/gateway/backends
GET    /api/v1/gateway/backends/{id}
PUT    /api/v1/gateway/backends/{id}
DELETE /api/v1/gateway/backends/{id}
GET    /api/v1/gateway/backends/{id}/health
```

**Acceptance Criteria:**
- Backend URLs validated and reachable
- Health checks execute every 5 seconds
- Unhealthy backends automatically removed from rotation
- Connection pool size configurable (default: 10-100 connections)
- Backend state persisted (survives gateway restart)

---

#### FR-GW-003: Traffic Routing
**Priority:** Critical
**Description:** System MUST route incoming requests to appropriate backends

**Requirements:**
- Match request path to configured routes
- Select backend using configured strategy
- Proxy request to backend with original headers
- Return backend response to client
- Handle backend failures gracefully
- Support request/response transformation (optional)

**Routing Strategies:**
- **RoundRobin** - Distribute evenly across healthy backends
- **WeightedRoundRobin** - Distribute based on backend weights
- **LeastConnections** - Route to backend with fewest active connections
- **IPHash** - Sticky sessions based on client IP
- **HeaderBased** - Route based on request headers (A/B testing)

**Acceptance Criteria:**
- Request routing latency < 5ms (p99)
- Headers preserved (except hop-by-hop headers)
- Backend errors return appropriate status codes
- Circuit breaker opens after 5 consecutive failures
- Request timeout configurable (default: 30s)

---

#### FR-GW-004: Health Monitoring
**Priority:** Critical
**Description:** System MUST monitor backend service health

**Requirements:**
- Execute HTTP health checks periodically
- Track health check success/failure rate
- Mark backends as healthy/unhealthy based on checks
- Remove unhealthy backends from rotation
- Re-add backends when health restored
- Alert on persistent backend failures

**Health Check Types:**
- **HTTP** - GET request to health endpoint (e.g., /health)
- **TCP** - TCP connection test
- **Custom** - Execute custom health check script

**API Endpoints:**
```
GET  /api/v1/gateway/backends/{id}/health
POST /api/v1/gateway/health-checks
```

**Acceptance Criteria:**
- Health checks execute every 5 seconds
- 3 consecutive failures = unhealthy
- 2 consecutive successes = healthy again
- Health check timeout: 3 seconds
- Health status visible in metrics

---

#### FR-GW-005: Configuration Deployment
**Priority:** Critical
**Description:** System MUST support deploying configuration updates progressively

**Requirements:**
- Deploy new route configuration to gateway nodes
- Support canary deployment (10% → 50% → 100%)
- Support blue-green deployment (instant switch)
- Support rolling deployment (gradual rollout)
- Monitor metrics during deployment
- Automatic rollback on error spike

**Deployment Strategies:**

1. **Canary Deployment**
   - Deploy to 10% of traffic first
   - Monitor error rate and latency
   - Promote to 50%, then 100% if healthy
   - Rollback if error rate > baseline + 5%

2. **Blue-Green Deployment**
   - Deploy to "green" backend pool
   - Switch traffic instantly when ready
   - Keep "blue" pool for quick rollback

3. **Rolling Deployment**
   - Update gateway nodes one at a time
   - Verify health before next node
   - Complete rollout across all nodes

**API Endpoints:**
```
POST   /api/v1/gateway/deployments
GET    /api/v1/gateway/deployments
GET    /api/v1/gateway/deployments/{id}
POST   /api/v1/gateway/deployments/{id}/promote
POST   /api/v1/gateway/deployments/{id}/rollback
GET    /api/v1/gateway/deployments/{id}/metrics
```

**Acceptance Criteria:**
- Configuration deployed within 10 seconds
- Zero requests dropped during deployment
- Automatic rollback within 30 seconds on error spike
- Deployment history tracked for audit

---

#### FR-GW-006: Request/Response Transformation
**Priority:** High
**Description:** System SHOULD support transforming requests and responses

**Requirements:**
- Add/remove/modify request headers
- Add/remove/modify response headers
- URL rewriting (path transformation)
- Request body transformation (optional)
- Response body transformation (optional)

**Transformation Types:**
- **Header Injection** - Add authentication headers
- **Path Rewriting** - /api/v1/users → /users
- **Response Enrichment** - Add metadata to responses

**Configuration Example:**
```json
{
  "transformations": {
    "request": {
      "addHeaders": {
        "X-Gateway-Version": "1.0",
        "X-Request-ID": "${requestId}"
      },
      "removeHeaders": ["X-Internal-Token"],
      "rewritePath": "/api/v1/(.*) → /v1/$1"
    },
    "response": {
      "addHeaders": {
        "X-Response-Time": "${latencyMs}ms"
      }
    }
  }
}
```

**Acceptance Criteria:**
- Transformation latency < 2ms (p99)
- Transformations applied in correct order
- Variable substitution working (${requestId}, ${latencyMs})

---

#### FR-GW-007: Rate Limiting
**Priority:** High
**Description:** System MUST support rate limiting per route or client

**Requirements:**
- Configure rate limits per route
- Configure rate limits per client (IP or API key)
- Return 429 Too Many Requests when limit exceeded
- Support different rate limit windows (second, minute, hour)
- Distributed rate limiting (shared across gateway nodes)

**Rate Limit Types:**
- **Fixed Window** - N requests per time window
- **Sliding Window** - N requests in last T seconds
- **Token Bucket** - Burst allowance with refill rate

**Configuration Example:**
```json
{
  "rateLimit": {
    "type": "SlidingWindow",
    "requests": 100,
    "window": "1m",
    "keyBy": "clientIp"
  }
}
```

**Acceptance Criteria:**
- Rate limits enforced accurately (±5%)
- Rate limit state shared via Redis
- Rate limit headers returned (X-RateLimit-Limit, X-RateLimit-Remaining)
- Rate limiting latency < 1ms (p99)

---

#### FR-GW-008: Circuit Breaker
**Priority:** High
**Description:** System MUST implement circuit breaker pattern for backend failures

**Requirements:**
- Detect repeated backend failures
- Open circuit after threshold failures
- Return error immediately when circuit open (fail-fast)
- Half-open after cooldown period
- Close circuit when backend recovers

**Circuit Breaker States:**
- **Closed** - Normal operation, requests pass through
- **Open** - Backend failing, requests fail immediately (503 Service Unavailable)
- **Half-Open** - Testing recovery, allow some requests through

**Configuration:**
```json
{
  "circuitBreaker": {
    "failureThreshold": 5,
    "timeout": "30s",
    "halfOpenRequests": 3
  }
}
```

**State Transitions:**
```
Closed → Open: After 5 consecutive failures
Open → Half-Open: After 30 second timeout
Half-Open → Closed: After 3 successful requests
Half-Open → Open: If any request fails
```

**Acceptance Criteria:**
- Circuit opens after 5 consecutive failures
- Circuit breaker state tracked per backend
- Metrics exported (circuit state, failure count)
- Half-open requests limited to prevent backend overload

---

#### FR-GW-009: Request Retry
**Priority:** High
**Description:** System SHOULD retry failed requests with exponential backoff

**Requirements:**
- Retry on specific error codes (502, 503, 504)
- Exponential backoff between retries
- Maximum retry attempts configurable
- Retry only idempotent methods (GET, PUT, DELETE)
- Track retry metrics

**Retry Configuration:**
```json
{
  "retry": {
    "maxAttempts": 3,
    "initialDelay": "100ms",
    "maxDelay": "2s",
    "backoffMultiplier": 2,
    "retryableStatusCodes": [502, 503, 504]
  }
}
```

**Backoff Schedule:**
```
Attempt 1: Immediate
Attempt 2: 100ms delay
Attempt 3: 200ms delay
Attempt 4: 400ms delay
```

**Acceptance Criteria:**
- Retries only idempotent requests
- Exponential backoff implemented correctly
- Maximum delay respected
- Retry metrics exported (attempts, success rate)

---

## Routing Patterns

### 1. Simple Proxy

**Use Case:** Forward all requests to single backend

**Configuration:**
```json
{
  "name": "api-proxy",
  "path": "/api/*",
  "backends": [
    {
      "name": "api-server",
      "url": "http://api-server:8080",
      "weight": 100
    }
  ],
  "strategy": "Direct"
}
```

**Request Flow:**
```
Client → Gateway → Backend
```

---

### 2. Load Balancing

**Use Case:** Distribute traffic across multiple backends

**Configuration:**
```json
{
  "name": "users-api",
  "path": "/api/users/*",
  "backends": [
    {
      "name": "users-1",
      "url": "http://users-1:8080",
      "weight": 50
    },
    {
      "name": "users-2",
      "url": "http://users-2:8080",
      "weight": 50
    }
  ],
  "strategy": "WeightedRoundRobin"
}
```

**Request Flow:**
```
Client → Gateway → [users-1 (50%), users-2 (50%)]
```

---

### 3. Canary Deployment

**Use Case:** Test new version with small percentage of traffic

**Configuration:**
```json
{
  "name": "api-v2-canary",
  "path": "/api/v2/*",
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

**Traffic Distribution:**
```
90% → api-v2-stable
10% → api-v2-canary (new version)
```

**Promotion Path:**
```
10% → 25% → 50% → 75% → 100%
```

---

### 4. A/B Testing

**Use Case:** Route traffic based on user segment

**Configuration:**
```json
{
  "name": "feature-test",
  "path": "/api/features/*",
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

**Request Flow:**
```
Header: X-Feature-Variant: variant-a → feature-control
Header: X-Feature-Variant: variant-b → feature-test
No header → feature-control (default)
```

---

## Deployment Strategies

### Canary Deployment

**Overview:**
- Deploy new configuration to small percentage of traffic
- Monitor metrics during canary phase
- Promote or rollback based on metrics

**Phases:**
```
Phase 1: 10% traffic  → Monitor for 5 minutes
Phase 2: 50% traffic  → Monitor for 5 minutes
Phase 3: 100% traffic → Deployment complete
```

**Metrics to Monitor:**
- Error rate (5xx responses)
- Latency (p95, p99)
- Request success rate

**Automatic Rollback Conditions:**
- Error rate increase > 5% compared to baseline
- P99 latency increase > 50% compared to baseline
- Manual rollback triggered by admin

**Implementation:**
```csharp
public async Task<DeploymentResult> ExecuteCanaryDeployment(
    GatewayConfig newConfig,
    CanaryStrategy strategy)
{
    // Phase 1: 10% traffic
    await UpdateTrafficSplit(newConfig, percentage: 10);
    await MonitorMetrics(duration: TimeSpan.FromMinutes(5));

    if (await ShouldRollback())
        return await Rollback();

    // Phase 2: 50% traffic
    await UpdateTrafficSplit(newConfig, percentage: 50);
    await MonitorMetrics(duration: TimeSpan.FromMinutes(5));

    if (await ShouldRollback())
        return await Rollback();

    // Phase 3: 100% traffic
    await UpdateTrafficSplit(newConfig, percentage: 100);
    return DeploymentResult.Success();
}
```

---

### Blue-Green Deployment

**Overview:**
- Deploy to "green" environment (new version)
- Switch traffic instantly when ready
- Keep "blue" environment for quick rollback

**Phases:**
```
Phase 1: Deploy to Green (0% traffic)
Phase 2: Smoke test Green
Phase 3: Switch traffic to Green (100%)
Phase 4: Monitor Green
Phase 5: Decommission Blue (optional)
```

**Traffic Switching:**
```
Before: Blue (100%), Green (0%)
After:  Blue (0%),   Green (100%)
```

**Rollback:**
```
Switch traffic back to Blue (instant)
```

---

### Rolling Deployment

**Overview:**
- Update gateway nodes one at a time
- Verify health before proceeding to next node
- Gradual rollout across cluster

**Process:**
```
Node 1: Update config → Health check → Success
Node 2: Update config → Health check → Success
Node 3: Update config → Health check → Success
```

**Node Update Process:**
```csharp
public async Task UpdateNode(string nodeId, GatewayConfig newConfig)
{
    // 1. Mark node as draining
    await MarkNodeDraining(nodeId);

    // 2. Wait for active connections to finish (max 30s)
    await DrainConnections(nodeId, timeout: TimeSpan.FromSeconds(30));

    // 3. Apply new configuration
    await ApplyConfig(nodeId, newConfig);

    // 4. Health check
    if (!await IsHealthy(nodeId))
        throw new DeploymentException("Node health check failed");

    // 5. Mark node as active
    await MarkNodeActive(nodeId);
}
```

---

## Performance Requirements

### Throughput

| Metric | Target | Notes |
|--------|--------|-------|
| Single Gateway Node | 50,000 req/sec | HTTP/1.1 requests |
| 3-Node Cluster | 150,000 req/sec | Horizontal scaling |
| 10-Node Cluster | 500,000 req/sec | Full production scale |

### Latency

| Operation | p50 | p95 | p99 |
|-----------|-----|-----|-----|
| Proxy Overhead | 1ms | 3ms | 5ms |
| Health Check | 5ms | 10ms | 20ms |
| Config Reload | 50ms | 100ms | 200ms |
| Circuit Breaker Check | 0.1ms | 0.5ms | 1ms |

### Availability

| Metric | Target | Notes |
|--------|--------|-------|
| Gateway Uptime | 99.99% | 4 nines (52 min/year downtime) |
| Config Update Success | 99.9% | Rollback on failure |
| Backend Health Check | 99.5% | Some transient failures OK |

### Scalability

| Resource | Limit | Notes |
|----------|-------|-------|
| Max Routes | 10,000 | Per gateway cluster |
| Max Backends | 100,000 | Across all routes |
| Max Concurrent Requests | 1,000,000 | Per gateway node |
| Max Request Size | 10 MB | Configurable |
| Max Response Size | 10 MB | Configurable |

---

## Security Requirements

### Authentication

**Requirement:** All admin API endpoints MUST require JWT authentication

**Implementation:**
- Reuse existing JWT authentication middleware
- Validate token on every request
- Extract user identity and roles

### Authorization

**Role-Based Access Control:**

| Role | Permissions |
|------|-------------|
| **Admin** | Full access (create routes, deploy configs, rollback) |
| **Operator** | Deploy configs, view metrics, trigger rollback |
| **Viewer** | Read-only access (view routes, view metrics) |

**Endpoint Authorization:**
```
POST   /api/v1/gateway/routes              - Admin only
POST   /api/v1/gateway/deployments         - Admin, Operator
POST   /api/v1/gateway/deployments/{id}/rollback - Admin, Operator
GET    /api/v1/gateway/routes              - Admin, Operator, Viewer
```

### Transport Security

**Requirements:**
- HTTPS/TLS 1.2+ enforced (production)
- Backend connections use HTTPS (configurable)
- Certificate validation enabled
- HSTS headers sent

### Rate Limiting

**Requirements:**
- Prevent abuse of gateway management APIs
- Protect against DDoS
- Configurable per endpoint

**Limits (Production):**
```
Route Management:     10 req/min per user
Deployment:           5 req/min per user
Metrics/Health:       60 req/min per user
```

---

## Observability Requirements

### Distributed Tracing

**Requirement:** ALL requests MUST be traced end-to-end

**Spans:**
1. `gateway.request` - Incoming request
2. `gateway.route_match` - Route matching
3. `gateway.backend_select` - Backend selection
4. `gateway.proxy` - Proxy to backend
5. `gateway.response` - Response to client

**Trace Context:**
- Propagate W3C trace context to backends
- Link gateway and backend spans
- Include route metadata in span attributes

**Example Trace:**
```
Root Span: gateway.request
  ├─ Child: gateway.route_match (path=/api/users/123)
  ├─ Child: gateway.backend_select (backend=users-1)
  └─ Child: gateway.proxy
      └─ Child: backend.process (from users-1 service)
```

### Metrics

**Required Metrics:**

**Counters:**
- `gateway.requests.total` - Total requests received
- `gateway.requests.backend.total` - Requests to backends
- `gateway.requests.failed.total` - Failed requests (5xx)
- `gateway.circuit_breaker.open.total` - Circuit breaker opens
- `gateway.retries.total` - Request retries

**Histograms:**
- `gateway.request.duration` - Request latency
- `gateway.backend.duration` - Backend response time
- `gateway.proxy.overhead` - Gateway proxy overhead

**Gauges:**
- `gateway.routes.count` - Total routes configured
- `gateway.backends.count` - Total backends registered
- `gateway.backends.healthy` - Healthy backends
- `gateway.active_connections` - Active connections
- `gateway.circuit_breaker.state` - Circuit breaker state (0=closed, 1=open)

### Logging

**Requirements:**
- Structured logging (JSON format)
- Trace ID correlation
- Log levels: Debug, Info, Warning, Error
- Request/response logging (configurable)

**Example Log:**
```json
{
  "timestamp": "2025-11-23T12:00:00Z",
  "level": "INFO",
  "message": "Request proxied to backend",
  "traceId": "abc-123",
  "routeName": "api-v1-users",
  "backendName": "users-1",
  "method": "GET",
  "path": "/api/users/123",
  "statusCode": 200,
  "latencyMs": 45,
  "userId": "admin@example.com"
}
```

### Health Monitoring

**Requirements:**
- Gateway node health checks every 30 seconds
- Backend health checks every 5 seconds
- Metrics dashboard (Grafana)
- Alerting on anomalies

**Health Check Endpoint:**
```
GET /api/v1/gateway/health

Response:
{
  "status": "Healthy",
  "activeConnections": 1250,
  "routesConfigured": 45,
  "backendsHealthy": 38,
  "backendsTotal": 40,
  "cpuUsage": 42.5,
  "memoryUsage": 65.8,
  "lastConfigUpdate": "2025-11-23T11:55:00Z"
}
```

---

## Scalability Requirements

### Horizontal Scaling

**Requirements:**
- Add gateway nodes without downtime
- Automatic configuration synchronization
- Shared state via Redis
- Linear throughput increase

**Scaling Targets:**
```
1 Node   → 50K req/sec
3 Nodes  → 150K req/sec
10 Nodes → 500K req/sec
```

### Configuration Synchronization

**Requirements:**
- Configuration changes propagated to all nodes
- Eventual consistency acceptable (< 1 second)
- Configuration stored in PostgreSQL
- Configuration cached in Redis

**Sync Mechanism:**
```
Admin updates config → PostgreSQL → Redis pub/sub → All gateway nodes
```

### Connection Pooling

**Requirements:**
- Maintain connection pools to backends
- Pool size configurable per backend
- Connection reuse for performance
- Automatic pool scaling

**Pool Configuration:**
```json
{
  "connectionPool": {
    "minSize": 10,
    "maxSize": 100,
    "maxIdleTime": "5m",
    "connectionTimeout": "5s"
  }
}
```

### Resource Limits

**Per Gateway Node:**
- CPU: < 80% sustained
- Memory: < 75% of allocated
- File Descriptors: < 80% of limit
- Network: < 1 Gbps

**Auto-Scaling Triggers:**
- CPU > 70% for 5 minutes → Scale up
- Active connections > 80% capacity → Scale up
- CPU < 30% for 15 minutes → Scale down

---

## Non-Functional Requirements

### Reliability

- Gateway uptime: 99.99% (4 nines)
- Zero dropped requests during config updates
- Automatic failover < 10 seconds
- Configuration rollback < 30 seconds

### Maintainability

- Clean code (SOLID principles)
- 85%+ test coverage
- Comprehensive documentation
- Runbooks for common operations

### Testability

- Unit tests for all components
- Integration tests for routing flows
- Performance tests for load scenarios
- Chaos testing for failure scenarios

### Compliance

- Audit logging for all configuration changes
- Configuration approval workflow (production)
- Access control and authorization
- Data retention policies

---

## Dependencies

### Required Infrastructure

1. **Redis 7+** - Configuration caching, distributed locks, rate limiting
2. **PostgreSQL 15+** - Configuration storage, audit logs
3. **.NET 8.0 Runtime** - Application runtime
4. **Jaeger** - Distributed tracing (optional)
5. **Prometheus** - Metrics collection (optional)

### External Services

1. **Backend Services** - Services being proxied
2. **HashiCorp Vault (self-hosted)** / **Kubernetes Secrets** - Secret management
3. **SMTP Server** - Email notifications (approval workflow)

---

## Acceptance Criteria

**System is production-ready when:**

1. ✅ All functional requirements implemented
2. ✅ 85%+ test coverage achieved
3. ✅ Performance targets met (50K req/sec, < 50ms p99)
4. ✅ Security requirements satisfied (JWT, HTTPS, RBAC)
5. ✅ Observability complete (tracing, metrics, logging)
6. ✅ Documentation complete (API docs, deployment guide, runbooks)
7. ✅ Zero-downtime config update verified
8. ✅ Automatic rollback tested
9. ✅ Load testing passed (500K req/sec cluster)
10. ✅ Production deployment successful

---

**Document Version:** 1.0.0
**Last Updated:** 2025-11-23
**Next Review:** After Epic 1 Implementation
**Approval Status:** Pending Architecture Review
