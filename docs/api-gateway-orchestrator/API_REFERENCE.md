# API Gateway Configuration Orchestrator - API Reference

**Version:** 1.0.0
**Base URL:** `https://api.example.com/api/v1`
**Authentication:** JWT Bearer Token
**Content-Type:** `application/json`

---

## Table of Contents

1. [Authentication](#authentication)
2. [Gateway Routes API](#gateway-routes-api)
3. [Backends API](#backends-api)
4. [Deployments API](#deployments-api)
5. [Health & Metrics API](#health--metrics-api)
6. [Error Responses](#error-responses)

---

## Authentication

All API endpoints (except `/health`) require JWT authentication.

### Get JWT Token

```http
POST /api/v1/authentication/login
Content-Type: application/json

{
  "username": "admin@example.com",
  "password": "Admin123!"
}

Response 200 OK:
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expiresAt": "2025-11-24T12:00:00Z",
  "user": {
    "username": "admin@example.com",
    "role": "Admin"
  }
}
```

### Use Token in Requests

```http
GET /api/v1/gateway/routes
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

---

## Gateway Routes API

### Create Route

Create a new gateway route.

**Endpoint:** `POST /api/v1/gateway/routes`
**Authorization:** Admin
**Rate Limit:** 10 req/min per user

**Request:**
```http
POST /api/v1/gateway/routes
Authorization: Bearer {token}
Content-Type: application/json

{
  "name": "api-users",
  "pathPattern": "/api/users/*",
  "methods": ["GET", "POST", "PUT", "DELETE"],
  "backends": [
    {
      "name": "users-service-1",
      "url": "http://users-1:8080",
      "weight": 50
    },
    {
      "name": "users-service-2",
      "url": "http://users-2:8080",
      "weight": 50
    }
  ],
  "strategy": "WeightedRoundRobin",
  "timeoutSeconds": 30,
  "isEnabled": true,
  "priority": 10,
  "rateLimit": {
    "type": "SlidingWindow",
    "requests": 1000,
    "window": "1m",
    "keyBy": "ClientIP"
  },
  "circuitBreaker": {
    "failureThreshold": 5,
    "timeout": "30s",
    "halfOpenRequests": 3
  },
  "retryPolicy": {
    "maxAttempts": 3,
    "initialDelay": "100ms",
    "maxDelay": "2s",
    "backoffMultiplier": 2.0,
    "retryableStatusCodes": [502, 503, 504]
  }
}
```

**Response 201 Created:**
```json
{
  "routeId": "route-7f8a9b1c-2d3e-4f5g",
  "name": "api-users",
  "pathPattern": "/api/users/*",
  "backends": [...],
  "strategy": "WeightedRoundRobin",
  "isEnabled": true,
  "version": "1.0",
  "createdAt": "2025-11-23T12:00:00Z",
  "updatedAt": "2025-11-23T12:00:00Z"
}
```

---

### List Routes

List all gateway routes with optional filtering.

**Endpoint:** `GET /api/v1/gateway/routes`
**Authorization:** Admin, Operator, Viewer
**Rate Limit:** 60 req/min per user

**Request:**
```http
GET /api/v1/gateway/routes?isEnabled=true&strategy=WeightedRoundRobin&limit=20&offset=0
Authorization: Bearer {token}
```

**Query Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `isEnabled` | boolean | No | Filter by enabled status |
| `strategy` | string | No | Filter by routing strategy |
| `limit` | int | No | Max routes to return (1-100, default: 20) |
| `offset` | int | No | Offset for pagination (default: 0) |

**Response 200 OK:**
```json
{
  "routes": [
    {
      "routeId": "route-123",
      "name": "api-users",
      "pathPattern": "/api/users/*",
      "strategy": "WeightedRoundRobin",
      "isEnabled": true,
      "backends": [...]
    }
  ],
  "total": 45,
  "limit": 20,
  "offset": 0
}
```

---

### Get Route

Get details of a specific route.

**Endpoint:** `GET /api/v1/gateway/routes/{routeId}`
**Authorization:** Admin, Operator, Viewer

**Response 200 OK:**
```json
{
  "routeId": "route-123",
  "name": "api-users",
  "pathPattern": "/api/users/*",
  "methods": ["GET", "POST", "PUT", "DELETE"],
  "backends": [...],
  "strategy": "WeightedRoundRobin",
  "timeoutSeconds": 30,
  "isEnabled": true,
  "priority": 10,
  "version": "1.0",
  "createdAt": "2025-11-23T12:00:00Z",
  "updatedAt": "2025-11-23T12:00:00Z",
  "statistics": {
    "totalRequests": 1250000,
    "failedRequests": 1250,
    "averageLatencyMs": 45.2,
    "p99LatencyMs": 120.5
  }
}
```

---

### Update Route

Update an existing route configuration.

**Endpoint:** `PUT /api/v1/gateway/routes/{routeId}`
**Authorization:** Admin
**Rate Limit:** 10 req/min per user

**Request:**
```http
PUT /api/v1/gateway/routes/route-123
Authorization: Bearer {token}
Content-Type: application/json

{
  "name": "api-users",
  "pathPattern": "/api/users/*",
  "backends": [
    {
      "name": "users-service-1",
      "url": "http://users-1:8080",
      "weight": 90
    },
    {
      "name": "users-service-2-canary",
      "url": "http://users-2-canary:8080",
      "weight": 10
    }
  ],
  "strategy": "WeightedRoundRobin",
  "version": "2.0"
}
```

**Response 200 OK:**
```json
{
  "routeId": "route-123",
  "name": "api-users",
  "version": "2.0",
  "updatedAt": "2025-11-23T12:05:00Z",
  "configChangeApplied": true
}
```

---

### Delete Route

Delete a gateway route (admin only).

**Endpoint:** `DELETE /api/v1/gateway/routes/{routeId}`
**Authorization:** Admin only
**Rate Limit:** 10 req/min per user

**Response 204 No Content**

---

## Backends API

### Create Backend

Register a new backend service.

**Endpoint:** `POST /api/v1/gateway/backends`
**Authorization:** Admin

**Request:**
```http
POST /api/v1/gateway/backends
Authorization: Bearer {token}
Content-Type: application/json

{
  "name": "users-service-3",
  "url": "http://users-3:8080",
  "weight": 100,
  "isEnabled": true,
  "healthCheck": {
    "type": "Http",
    "endpoint": "/health",
    "interval": "5s",
    "timeout": "3s",
    "unhealthyThreshold": 3,
    "healthyThreshold": 2,
    "expectedStatusCode": 200
  },
  "connectionPool": {
    "minSize": 10,
    "maxSize": 100,
    "maxIdleTime": "5m",
    "connectionTimeout": "5s"
  },
  "timeouts": {
    "connectTimeout": "5s",
    "readTimeout": "30s",
    "writeTimeout": "30s"
  }
}
```

**Response 201 Created:**
```json
{
  "backendId": "backend-abc123",
  "name": "users-service-3",
  "url": "http://users-3:8080",
  "healthStatus": "Unknown",
  "isEnabled": true,
  "createdAt": "2025-11-23T12:00:00Z"
}
```

---

### Get Backend Health

Get health status of a backend.

**Endpoint:** `GET /api/v1/gateway/backends/{backendId}/health`
**Authorization:** Admin, Operator, Viewer

**Response 200 OK:**
```json
{
  "backendId": "backend-abc123",
  "name": "users-service-3",
  "healthStatus": "Healthy",
  "lastHealthCheck": "2025-11-23T12:01:30Z",
  "consecutiveSuccesses": 12,
  "consecutiveFailures": 0,
  "activeConnections": 45,
  "totalRequests": 125000,
  "failedRequests": 125
}
```

---

## Deployments API

### Create Deployment

Deploy a new route configuration.

**Endpoint:** `POST /api/v1/gateway/deployments`
**Authorization:** Admin, Operator
**Rate Limit:** 5 req/min per user

**Request:**
```http
POST /api/v1/gateway/deployments
Authorization: Bearer {token}
Content-Type: application/json

{
  "routeId": "route-123",
  "configVersion": "2.0",
  "strategy": "Canary",
  "environment": "Production",
  "phases": [
    {
      "name": "Phase 1",
      "trafficPercentage": 10,
      "duration": "PT5M"
    },
    {
      "name": "Phase 2",
      "trafficPercentage": 50,
      "duration": "PT5M"
    },
    {
      "name": "Phase 3",
      "trafficPercentage": 100,
      "duration": "PT0S"
    }
  ]
}
```

**Response 201 Created:**
```json
{
  "deploymentId": "deploy-xyz789",
  "routeId": "route-123",
  "configVersion": "2.0",
  "strategy": "Canary",
  "status": "Pending",
  "approvalStatus": "Pending",
  "createdAt": "2025-11-23T12:00:00Z",
  "createdBy": "admin@example.com"
}
```

---

### Get Deployment Status

Get status and metrics of a deployment.

**Endpoint:** `GET /api/v1/gateway/deployments/{deploymentId}`
**Authorization:** Admin, Operator, Viewer

**Response 200 OK:**
```json
{
  "deploymentId": "deploy-xyz789",
  "routeId": "route-123",
  "configVersion": "2.0",
  "strategy": "Canary",
  "status": "InProgress",
  "currentPhaseIndex": 1,
  "phases": [
    {
      "name": "Phase 1",
      "trafficPercentage": 10,
      "status": "Completed",
      "startedAt": "2025-11-23T12:00:00Z",
      "completedAt": "2025-11-23T12:05:00Z"
    },
    {
      "name": "Phase 2",
      "trafficPercentage": 50,
      "status": "InProgress",
      "startedAt": "2025-11-23T12:05:00Z",
      "completedAt": null
    }
  ],
  "metrics": {
    "baselineErrorRate": 0.001,
    "currentErrorRate": 0.0012,
    "baselineP99Latency": 85.2,
    "currentP99Latency": 87.5,
    "totalRequests": 25000,
    "failedRequests": 30
  }
}
```

---

### Promote Deployment

Promote deployment to next phase.

**Endpoint:** `POST /api/v1/gateway/deployments/{deploymentId}/promote`
**Authorization:** Admin, Operator

**Request:**
```http
POST /api/v1/gateway/deployments/deploy-xyz789/promote
Authorization: Bearer {token}
Content-Type: application/json

{
  "targetPercentage": 100
}
```

**Response 200 OK:**
```json
{
  "deploymentId": "deploy-xyz789",
  "status": "InProgress",
  "currentPhaseIndex": 2,
  "message": "Promoted to phase 3 (100% traffic)"
}
```

---

### Rollback Deployment

Rollback a deployment to previous configuration.

**Endpoint:** `POST /api/v1/gateway/deployments/{deploymentId}/rollback`
**Authorization:** Admin, Operator

**Request:**
```http
POST /api/v1/gateway/deployments/deploy-xyz789/rollback
Authorization: Bearer {token}
Content-Type: application/json

{
  "reason": "Error rate exceeded threshold"
}
```

**Response 200 OK:**
```json
{
  "deploymentId": "deploy-xyz789",
  "status": "RolledBack",
  "rolledBackAt": "2025-11-23T12:10:00Z",
  "reason": "Error rate exceeded threshold",
  "previousConfigVersion": "1.0"
}
```

---

### Get Deployment Metrics

Get real-time metrics for a deployment.

**Endpoint:** `GET /api/v1/gateway/deployments/{deploymentId}/metrics`
**Authorization:** Admin, Operator, Viewer

**Response 200 OK:**
```json
{
  "deploymentId": "deploy-xyz789",
  "currentPhase": "Phase 2 (50% traffic)",
  "metrics": {
    "timeWindow": "Last 5 minutes",
    "stable": {
      "requestCount": 12500,
      "errorRate": 0.001,
      "p50Latency": 35.2,
      "p95Latency": 72.5,
      "p99Latency": 85.2
    },
    "canary": {
      "requestCount": 12500,
      "errorRate": 0.0012,
      "p50Latency": 36.1,
      "p95Latency": 74.2,
      "p99Latency": 87.5
    },
    "comparison": {
      "errorRateDelta": "+0.0002 (+20%)",
      "p99LatencyDelta": "+2.3ms (+2.7%)",
      "shouldRollback": false
    }
  }
}
```

---

## Health & Metrics API

### Gateway Health Check

Check overall gateway health.

**Endpoint:** `GET /api/v1/gateway/health`
**Authorization:** None (public endpoint)

**Response 200 OK:**
```json
{
  "status": "Healthy",
  "version": "1.0.0",
  "uptime": "PT48H30M",
  "activeConnections": 1250,
  "routesConfigured": 45,
  "backendsHealthy": 38,
  "backendsTotal": 40,
  "requestsPerSecond": 45230,
  "lastConfigUpdate": "2025-11-23T11:55:00Z"
}
```

---

### Gateway Metrics

Get gateway performance metrics.

**Endpoint:** `GET /api/v1/gateway/metrics`
**Authorization:** Admin, Operator, Viewer

**Response 200 OK:**
```json
{
  "timeWindow": "Last 1 hour",
  "totalRequests": 1625000,
  "successfulRequests": 1623375,
  "failedRequests": 1625,
  "errorRate": 0.001,
  "requestsPerSecond": 451.4,
  "latency": {
    "p50": 35.2,
    "p95": 72.5,
    "p99": 98.7
  },
  "backends": {
    "total": 40,
    "healthy": 38,
    "unhealthy": 2,
    "draining": 0
  },
  "circuitBreakers": {
    "closed": 38,
    "open": 2,
    "halfOpen": 0
  }
}
```

---

## Error Responses

### Standard Error Format

```json
{
  "error": {
    "code": "VALIDATION_ERROR",
    "message": "Invalid path pattern",
    "details": [
      "Path pattern must start with /",
      "Path pattern contains invalid characters"
    ],
    "traceId": "trace-abc123",
    "timestamp": "2025-11-23T12:00:00Z"
  }
}
```

### Common Error Codes

| Status Code | Error Code | Description |
|-------------|------------|-------------|
| 400 | VALIDATION_ERROR | Request validation failed |
| 401 | UNAUTHORIZED | Missing or invalid authentication token |
| 403 | FORBIDDEN | Insufficient permissions |
| 404 | NOT_FOUND | Resource not found |
| 409 | CONFLICT | Resource already exists |
| 429 | RATE_LIMIT_EXCEEDED | Too many requests |
| 500 | INTERNAL_SERVER_ERROR | Server error |
| 502 | BAD_GATEWAY | Backend service error |
| 503 | SERVICE_UNAVAILABLE | No healthy backends available |
| 504 | GATEWAY_TIMEOUT | Backend timeout |

---

**Last Updated:** 2025-11-23
**API Version:** 1.0.0
