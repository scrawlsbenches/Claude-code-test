# Service Mesh Policy Manager API Reference

**Version:** 1.0.0
**Base URL:** `https://api.example.com/api/v1`
**Authentication:** JWT Bearer Token
**Content-Type:** `application/json`

---

## Table of Contents

1. [Authentication](#authentication)
2. [Policies API](#policies-api)
3. [Deployments API](#deployments-api)
4. [Services API](#services-api)
5. [Clusters API](#clusters-api)
6. [Validation API](#validation-api)
7. [Error Responses](#error-responses)
8. [Rate Limiting](#rate-limiting)

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
GET /api/v1/policies
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

---

## Policies API

### Create Policy

Create a new service mesh policy.

**Endpoint:** `POST /api/v1/policies`
**Authorization:** Developer, Admin
**Rate Limit:** 10 req/min per user

**Request:**
```http
POST /api/v1/policies
Authorization: Bearer {token}
Content-Type: application/json

{
  "name": "user-service-circuit-breaker",
  "description": "Circuit breaker for user service",
  "type": "DestinationRule",
  "serviceMesh": "Istio",
  "targetService": "user-service",
  "namespace": "production",
  "spec": {
    "yamlConfig": "apiVersion: networking.istio.io/v1beta1\nkind: DestinationRule\nmetadata:\n  name: user-service\nspec:\n  host: user-service\n  trafficPolicy:\n    outlierDetection:\n      consecutiveErrors: 5\n      interval: 30s\n      baseEjectionTime: 30s"
  },
  "tags": ["circuit-breaker", "production", "user-service"]
}
```

**Request Fields:**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `name` | string | Yes | Policy name (alphanumeric, dots, dashes) |
| `description` | string | No | Human-readable description |
| `type` | string | Yes | PolicyType enum value |
| `serviceMesh` | string | Yes | `Istio` or `Linkerd` |
| `targetService` | string | Yes | Target service name |
| `namespace` | string | No | Kubernetes namespace (default: "default") |
| `spec` | object | Yes | PolicySpec with yamlConfig |
| `tags` | array | No | Tags for organization |

**Response 201 Created:**
```json
{
  "policyId": "pol-7f8a9b1c-2d3e-4f5g-6h7i-8j9k0l1m2n3o",
  "name": "user-service-circuit-breaker",
  "type": "DestinationRule",
  "serviceMesh": "Istio",
  "targetService": "user-service",
  "namespace": "production",
  "version": 1,
  "status": "Draft",
  "createdAt": "2025-11-23T12:00:00Z"
}
```

**Error Responses:**
- `400 Bad Request` - Invalid policy configuration or YAML syntax error
- `409 Conflict` - Policy with same name already exists
- `429 Too Many Requests` - Rate limit exceeded

---

### List Policies

List all policies with optional filtering.

**Endpoint:** `GET /api/v1/policies`
**Authorization:** All roles
**Rate Limit:** 60 req/min per user

**Request:**
```http
GET /api/v1/policies?serviceMesh=Istio&status=Approved&targetService=user-service
Authorization: Bearer {token}
```

**Query Parameters:**

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `serviceMesh` | string | No | - | Filter by mesh type (`Istio`, `Linkerd`) |
| `status` | string | No | - | Filter by status |
| `targetService` | string | No | - | Filter by target service |
| `type` | string | No | - | Filter by policy type |
| `tags` | string | No | - | Filter by tags (comma-separated) |
| `limit` | int | No | 20 | Max policies to return (1-100) |
| `offset` | int | No | 0 | Pagination offset |

**Response 200 OK:**
```json
{
  "policies": [
    {
      "policyId": "pol-123",
      "name": "user-service-circuit-breaker",
      "type": "DestinationRule",
      "serviceMesh": "Istio",
      "targetService": "user-service",
      "status": "Approved",
      "version": 3,
      "createdAt": "2025-11-20T10:00:00Z",
      "updatedAt": "2025-11-23T12:00:00Z"
    }
  ],
  "total": 42,
  "limit": 20,
  "offset": 0
}
```

---

### Get Policy Details

Get details about a specific policy.

**Endpoint:** `GET /api/v1/policies/{policyId}`
**Authorization:** All roles
**Rate Limit:** 60 req/min per user

**Request:**
```http
GET /api/v1/policies/pol-123
Authorization: Bearer {token}
```

**Response 200 OK:**
```json
{
  "policyId": "pol-123",
  "name": "user-service-circuit-breaker",
  "description": "Circuit breaker for user service",
  "type": "DestinationRule",
  "serviceMesh": "Istio",
  "targetService": "user-service",
  "namespace": "production",
  "spec": {
    "yamlConfig": "...",
    "parameters": {
      "consecutiveErrors": "5",
      "interval": "30s"
    }
  },
  "version": 3,
  "status": "Approved",
  "approvedBy": "admin@example.com",
  "approvedAt": "2025-11-23T11:00:00Z",
  "ownerId": "user-123",
  "tags": ["circuit-breaker", "production"],
  "createdAt": "2025-11-20T10:00:00Z",
  "updatedAt": "2025-11-23T12:00:00Z"
}
```

---

### Update Policy

Update an existing policy (creates new version).

**Endpoint:** `PUT /api/v1/policies/{policyId}`
**Authorization:** Owner, Admin
**Rate Limit:** 10 req/min per user

**Request:**
```http
PUT /api/v1/policies/pol-123
Authorization: Bearer {token}
Content-Type: application/json

{
  "description": "Updated description",
  "spec": {
    "yamlConfig": "..."
  },
  "tags": ["circuit-breaker", "production", "updated"]
}
```

**Response 200 OK:**
```json
{
  "policyId": "pol-456",
  "name": "user-service-circuit-breaker",
  "version": 4,
  "status": "Draft",
  "updatedAt": "2025-11-23T13:00:00Z"
}
```

**Note:** Update creates a new version. Original policy preserved.

---

### Delete Policy

Delete a policy (admin only, must not have active deployments).

**Endpoint:** `DELETE /api/v1/policies/{policyId}`
**Authorization:** Admin only
**Rate Limit:** 10 req/min per user

**Request:**
```http
DELETE /api/v1/policies/pol-123
Authorization: Bearer {token}
```

**Response 204 No Content**

**Error Responses:**
- `409 Conflict` - Policy has active deployments (undeploy first)

---

### Submit Policy for Approval

Submit policy for admin approval (required for production).

**Endpoint:** `POST /api/v1/policies/{policyId}/submit`
**Authorization:** Owner, Admin
**Rate Limit:** 10 req/min per user

**Request:**
```http
POST /api/v1/policies/pol-123/submit
Authorization: Bearer {token}
Content-Type: application/json

{
  "notes": "Ready for production deployment"
}
```

**Response 200 OK:**
```json
{
  "policyId": "pol-123",
  "status": "PendingApproval",
  "submittedAt": "2025-11-23T14:00:00Z"
}
```

---

### Approve Policy

Approve policy for production deployment (admin only).

**Endpoint:** `POST /api/v1/policies/{policyId}/approve`
**Authorization:** Admin only
**Rate Limit:** 10 req/min per user

**Request:**
```http
POST /api/v1/policies/pol-123/approve
Authorization: Bearer {token}
Content-Type: application/json

{
  "notes": "Reviewed and approved for production"
}
```

**Response 200 OK:**
```json
{
  "policyId": "pol-123",
  "status": "Approved",
  "approvedBy": "admin@example.com",
  "approvedAt": "2025-11-23T15:00:00Z"
}
```

---

## Deployments API

### Create Deployment

Deploy a policy to a cluster.

**Endpoint:** `POST /api/v1/deployments`
**Authorization:** Developer (dev/staging), Admin (production)
**Rate Limit:** 20 req/min per user

**Request:**
```http
POST /api/v1/deployments
Authorization: Bearer {token}
Content-Type: application/json

{
  "policyId": "pol-123",
  "environment": "Production",
  "clusterId": "prod-us-east-1",
  "strategy": "Canary",
  "config": {
    "canaryPercentage": "10",
    "promotionInterval": "5m",
    "autoPromote": "true"
  }
}
```

**Request Fields:**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `policyId` | string | Yes | Policy to deploy |
| `environment` | string | Yes | Target environment |
| `clusterId` | string | Yes | Target cluster ID |
| `strategy` | string | Yes | Deployment strategy |
| `config` | object | No | Strategy-specific configuration |

**Response 202 Accepted:**
```json
{
  "deploymentId": "dep-abc123",
  "policyId": "pol-123",
  "environment": "Production",
  "clusterId": "prod-us-east-1",
  "strategy": "Canary",
  "status": "Pending",
  "createdAt": "2025-11-23T16:00:00Z",
  "estimatedDuration": "PT15M"
}
```

---

### Get Deployment Status

Get real-time deployment status.

**Endpoint:** `GET /api/v1/deployments/{deploymentId}`
**Authorization:** All roles
**Rate Limit:** 100 req/min per user

**Request:**
```http
GET /api/v1/deployments/dep-abc123
Authorization: Bearer {token}
```

**Response 200 OK:**
```json
{
  "deploymentId": "dep-abc123",
  "policyId": "pol-123",
  "environment": "Production",
  "clusterId": "prod-us-east-1",
  "strategy": "Canary",
  "status": "InProgress",
  "canaryPercentage": 10,
  "instancesAffected": 100,
  "instancesSucceeded": 10,
  "instancesFailed": 0,
  "startedAt": "2025-11-23T16:00:05Z",
  "baselineMetrics": {
    "successRate": 99.5,
    "errorRate": 0.5,
    "p95Latency": 120.5
  },
  "currentMetrics": {
    "successRate": 99.6,
    "errorRate": 0.4,
    "p95Latency": 115.2
  }
}
```

---

### List Deployments

List all deployments with optional filtering.

**Endpoint:** `GET /api/v1/deployments`
**Authorization:** All roles
**Rate Limit:** 60 req/min per user

**Request:**
```http
GET /api/v1/deployments?environment=Production&status=InProgress
Authorization: Bearer {token}
```

**Query Parameters:**

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `environment` | string | - | Filter by environment |
| `status` | string | - | Filter by status |
| `clusterId` | string | - | Filter by cluster |
| `limit` | int | 20 | Max deployments to return |
| `offset` | int | 0 | Pagination offset |

**Response 200 OK:**
```json
{
  "deployments": [
    {
      "deploymentId": "dep-abc123",
      "policyId": "pol-123",
      "environment": "Production",
      "status": "InProgress",
      "strategy": "Canary",
      "startedAt": "2025-11-23T16:00:05Z"
    }
  ],
  "total": 15,
  "limit": 20,
  "offset": 0
}
```

---

### Promote Canary Deployment

Promote canary to next percentage or full rollout.

**Endpoint:** `POST /api/v1/deployments/{deploymentId}/promote`
**Authorization:** Developer, Admin
**Rate Limit:** 10 req/min per user

**Request:**
```http
POST /api/v1/deployments/dep-abc123/promote
Authorization: Bearer {token}
Content-Type: application/json

{
  "targetPercentage": 30
}
```

**Response 200 OK:**
```json
{
  "deploymentId": "dep-abc123",
  "canaryPercentage": 30,
  "status": "InProgress",
  "promotedAt": "2025-11-23T16:10:00Z"
}
```

---

### Rollback Deployment

Rollback a deployment to previous policy version.

**Endpoint:** `POST /api/v1/deployments/{deploymentId}/rollback`
**Authorization:** Developer, Admin
**Rate Limit:** 10 req/min per user

**Request:**
```http
POST /api/v1/deployments/dep-abc123/rollback
Authorization: Bearer {token}
Content-Type: application/json

{
  "reason": "High error rate detected"
}
```

**Response 200 OK:**
```json
{
  "deploymentId": "dep-abc123",
  "status": "RolledBack",
  "rolledBackAt": "2025-11-23T16:15:00Z",
  "reason": "High error rate detected"
}
```

---

### Get Deployment Metrics

Get detailed metrics for a deployment.

**Endpoint:** `GET /api/v1/deployments/{deploymentId}/metrics`
**Authorization:** All roles
**Rate Limit:** 60 req/min per user

**Request:**
```http
GET /api/v1/deployments/dep-abc123/metrics?window=PT1H
Authorization: Bearer {token}
```

**Response 200 OK:**
```json
{
  "deploymentId": "dep-abc123",
  "window": "PT1H",
  "baseline": {
    "successRate": 99.5,
    "errorRate": 0.5,
    "p50Latency": 45.2,
    "p95Latency": 120.5,
    "p99Latency": 250.0,
    "requestsPerSecond": 1250.0
  },
  "current": {
    "successRate": 99.6,
    "errorRate": 0.4,
    "p50Latency": 42.1,
    "p95Latency": 115.2,
    "p99Latency": 240.0,
    "requestsPerSecond": 1300.0
  },
  "comparison": {
    "successRateDelta": "+0.1%",
    "errorRateDelta": "-20.0%",
    "p95LatencyDelta": "-4.4%",
    "status": "Improved"
  }
}
```

---

## Services API

### List Services

List all services in service mesh.

**Endpoint:** `GET /api/v1/services`
**Authorization:** All roles
**Rate Limit:** 60 req/min per user

**Request:**
```http
GET /api/v1/services?clusterId=prod-us-east-1&namespace=production
Authorization: Bearer {token}
```

**Response 200 OK:**
```json
{
  "services": [
    {
      "name": "user-service",
      "namespace": "production",
      "clusterId": "prod-us-east-1",
      "instanceCount": 10,
      "activePolicies": [
        {
          "policyId": "pol-123",
          "name": "user-service-circuit-breaker",
          "type": "DestinationRule"
        }
      ],
      "health": "Healthy",
      "trafficMetrics": {
        "successRate": 99.5,
        "requestsPerSecond": 1250.0
      }
    }
  ],
  "total": 50
}
```

---

### Get Service Details

Get detailed information about a service.

**Endpoint:** `GET /api/v1/services/{clusterId}/{namespace}/{serviceName}`
**Authorization:** All roles
**Rate Limit:** 60 req/min per user

**Request:**
```http
GET /api/v1/services/prod-us-east-1/production/user-service
Authorization: Bearer {token}
```

**Response 200 OK:**
```json
{
  "name": "user-service",
  "namespace": "production",
  "clusterId": "prod-us-east-1",
  "instanceCount": 10,
  "activePolicies": [...],
  "health": "Healthy",
  "trafficMetrics": {...},
  "deploymentHistory": [...]
}
```

---

## Clusters API

### List Clusters

List all service mesh clusters.

**Endpoint:** `GET /api/v1/clusters`
**Authorization:** All roles
**Rate Limit:** 60 req/min per user

**Request:**
```http
GET /api/v1/clusters?environment=Production
Authorization: Bearer {token}
```

**Response 200 OK:**
```json
{
  "clusters": [
    {
      "clusterId": "prod-us-east-1",
      "name": "Production US East",
      "environment": "Production",
      "meshType": "Istio",
      "meshVersion": "1.20.0",
      "serviceCount": 50,
      "instanceCount": 500,
      "health": {
        "isHealthy": true,
        "kubernetesApiReachable": true,
        "controlPlaneHealthy": true,
        "metricsAvailable": true
      }
    }
  ],
  "total": 5
}
```

---

## Validation API

### Validate Policy

Validate policy configuration.

**Endpoint:** `POST /api/v1/validation/validate`
**Authorization:** Developer, Admin
**Rate Limit:** 30 req/min per user

**Request:**
```http
POST /api/v1/validation/validate
Authorization: Bearer {token}
Content-Type: application/json

{
  "policyId": "pol-123"
}
```

**Response 200 OK (Valid):**
```json
{
  "isValid": true,
  "warnings": [
    {
      "code": "HIGH_CONNECTION_LIMIT",
      "message": "Connection limit is very high (1000)",
      "path": "spec.trafficPolicy.connectionPool.tcp.maxConnections"
    }
  ],
  "affectedServices": ["user-service"],
  "estimatedInstancesAffected": 10,
  "validatedAt": "2025-11-23T17:00:00Z"
}
```

**Response 200 OK (Invalid):**
```json
{
  "isValid": false,
  "errors": [
    {
      "code": "INVALID_FIELD",
      "message": "Field 'consecutiveErrors' must be > 0",
      "path": "spec.trafficPolicy.outlierDetection.consecutiveErrors",
      "suggestion": "Set consecutiveErrors to at least 1"
    }
  ],
  "validatedAt": "2025-11-23T17:00:00Z"
}
```

---

### Dry-Run Deployment

Test deployment without actually applying changes.

**Endpoint:** `POST /api/v1/validation/dry-run`
**Authorization:** Developer, Admin
**Rate Limit:** 10 req/min per user

**Request:**
```http
POST /api/v1/validation/dry-run
Authorization: Bearer {token}
Content-Type: application/json

{
  "policyId": "pol-123",
  "environment": "Production",
  "clusterId": "prod-us-east-1"
}
```

**Response 200 OK:**
```json
{
  "success": true,
  "affectedServices": ["user-service"],
  "estimatedInstancesAffected": 10,
  "conflicts": [],
  "warnings": [],
  "dryRunAt": "2025-11-23T17:30:00Z"
}
```

---

## Error Responses

All error responses follow a consistent format:

```json
{
  "error": {
    "code": "VALIDATION_ERROR",
    "message": "Policy validation failed",
    "details": [
      {
        "field": "spec.yamlConfig",
        "message": "Invalid YAML syntax on line 5"
      }
    ],
    "traceId": "trace-xyz789",
    "timestamp": "2025-11-23T18:00:00Z"
  }
}
```

### Common Error Codes

| HTTP Status | Error Code | Description |
|-------------|------------|-------------|
| 400 | `VALIDATION_ERROR` | Request validation failed |
| 400 | `POLICY_VALIDATION_ERROR` | Policy configuration invalid |
| 401 | `UNAUTHORIZED` | Missing or invalid JWT token |
| 403 | `FORBIDDEN` | Insufficient permissions |
| 404 | `NOT_FOUND` | Resource not found |
| 409 | `CONFLICT` | Resource conflict (e.g., active deployments) |
| 429 | `RATE_LIMIT_EXCEEDED` | Too many requests |
| 500 | `INTERNAL_ERROR` | Internal server error |
| 503 | `SERVICE_UNAVAILABLE` | Service temporarily unavailable |

---

## Rate Limiting

Rate limits based on user identity (JWT token).

### Rate Limit Headers

```http
HTTP/1.1 200 OK
X-RateLimit-Limit: 60
X-RateLimit-Remaining: 45
X-RateLimit-Reset: 1700144400
Retry-After: 60
```

### Rate Limits by Endpoint

| Endpoint | Limit | Window |
|----------|-------|--------|
| `POST /api/v1/policies` | 10 req | 1 minute |
| `GET /api/v1/policies` | 60 req | 1 minute |
| `POST /api/v1/deployments` | 20 req | 1 minute |
| `GET /api/v1/deployments/{id}` | 100 req | 1 minute |
| `POST /api/v1/deployments/{id}/rollback` | 10 req | 1 minute |

---

**API Version:** 1.0.0
**Last Updated:** 2025-11-23
**Support:** api-support@example.com
