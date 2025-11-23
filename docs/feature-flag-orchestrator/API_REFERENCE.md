# Feature Flag Orchestrator API Reference

**Version:** 1.0.0
**Base URL:** `https://api.example.com/api/v1`
**Authentication:** JWT Bearer Token
**Content-Type:** `application/json`

---

## Table of Contents

1. [Authentication](#authentication)
2. [Flags API](#flags-api)
3. [Evaluation API](#evaluation-api)
4. [Rollouts API](#rollouts-api)
5. [Targets API](#targets-api)
6. [Experiments API](#experiments-api)
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
GET /api/v1/flags
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

---

## Flags API

### Create Flag

Create a new feature flag.

**Endpoint:** `POST /api/v1/flags`
**Authorization:** Developer, Admin
**Rate Limit:** 100 req/min per user

**Request:**
```http
POST /api/v1/flags
Authorization: Bearer {token}
Content-Type: application/json

{
  "name": "new-checkout-flow",
  "description": "New checkout experience with one-click purchase",
  "type": "Boolean",
  "defaultValue": "false",
  "environment": "Production",
  "tags": ["frontend", "checkout", "high-impact"]
}
```

**Request Fields:**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `name` | string | Yes | Flag name (alphanumeric, dots, dashes) |
| `description` | string | No | Human-readable description |
| `type` | string | Yes | `Boolean`, `String`, `Number`, `JSON` |
| `defaultValue` | string | Yes | Default value when no rules match |
| `environment` | string | No | `Development`, `Staging`, `Production` (default: `Development`) |
| `tags` | array | No | Tags for organization |

**Response 201 Created:**
```json
{
  "name": "new-checkout-flow",
  "description": "New checkout experience with one-click purchase",
  "type": "Boolean",
  "defaultValue": "false",
  "status": "Active",
  "environment": "Production",
  "tags": ["frontend", "checkout", "high-impact"],
  "createdAt": "2025-11-23T12:00:00Z",
  "createdBy": "admin@example.com"
}
```

**Error Responses:**
- `400 Bad Request` - Invalid flag name or default value
- `409 Conflict` - Flag already exists
- `429 Too Many Requests` - Rate limit exceeded

---

### List Flags

List all feature flags.

**Endpoint:** `GET /api/v1/flags`
**Authorization:** All roles
**Rate Limit:** 300 req/min per user

**Request:**
```http
GET /api/v1/flags?environment=Production&status=Active&tags=checkout&limit=20&offset=0
Authorization: Bearer {token}
```

**Query Parameters:**

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `environment` | string | No | - | Filter by environment |
| `status` | string | No | - | Filter by status (`Active`, `Inactive`, `Archived`) |
| `tags` | string | No | - | Filter by tag (comma-separated) |
| `limit` | int | No | 20 | Max flags to return (1-100) |
| `offset` | int | No | 0 | Pagination offset |

**Response 200 OK:**
```json
{
  "flags": [
    {
      "name": "new-checkout-flow",
      "type": "Boolean",
      "defaultValue": "false",
      "status": "Active",
      "environment": "Production",
      "createdAt": "2025-11-23T12:00:00Z"
    }
  ],
  "total": 42,
  "limit": 20,
  "offset": 0
}
```

---

### Get Flag Details

Get detailed information about a specific flag.

**Endpoint:** `GET /api/v1/flags/{name}`
**Authorization:** All roles
**Rate Limit:** 300 req/min per user

**Request:**
```http
GET /api/v1/flags/new-checkout-flow
Authorization: Bearer {token}
```

**Response 200 OK:**
```json
{
  "name": "new-checkout-flow",
  "description": "New checkout experience with one-click purchase",
  "type": "Boolean",
  "defaultValue": "false",
  "status": "Active",
  "environment": "Production",
  "tags": ["frontend", "checkout", "high-impact"],
  "createdAt": "2025-11-23T12:00:00Z",
  "updatedAt": "2025-11-23T12:00:00Z",
  "createdBy": "admin@example.com",
  "activeRollout": {
    "rolloutId": "rollout-123",
    "strategy": "Canary",
    "currentStageIndex": 1,
    "status": "InProgress"
  },
  "targets": [
    {
      "targetId": "target-1",
      "name": "Beta testers",
      "priority": 0,
      "value": "true"
    }
  ]
}
```

---

### Update Flag

Update flag configuration.

**Endpoint:** `PUT /api/v1/flags/{name}`
**Authorization:** Developer (non-prod), Admin (prod)
**Rate Limit:** 100 req/min per user

**Request:**
```http
PUT /api/v1/flags/new-checkout-flow
Authorization: Bearer {token}
Content-Type: application/json

{
  "description": "Updated description",
  "defaultValue": "true",
  "tags": ["frontend", "checkout", "high-impact", "approved"]
}
```

**Response 200 OK:**
```json
{
  "name": "new-checkout-flow",
  "description": "Updated description",
  "defaultValue": "true",
  "tags": ["frontend", "checkout", "high-impact", "approved"],
  "updatedAt": "2025-11-23T13:00:00Z",
  "updatedBy": "admin@example.com"
}
```

**Note:** Production flags require approval workflow for default value changes.

---

### Delete Flag

Delete a flag (soft delete).

**Endpoint:** `DELETE /api/v1/flags/{name}`
**Authorization:** Admin only
**Rate Limit:** 10 req/min per user

**Request:**
```http
DELETE /api/v1/flags/new-checkout-flow
Authorization: Bearer {token}
```

**Response 204 No Content**

**Note:** Flag is archived (soft deleted), not permanently removed.

---

## Evaluation API

### Evaluate Single Flag

Evaluate a flag for a specific context.

**Endpoint:** `GET /api/v1/flags/{name}/evaluate`
**Authorization:** SDK, Developer, Admin
**Rate Limit:** 10,000 req/min per API key

**Request:**
```http
GET /api/v1/flags/new-checkout-flow/evaluate?userId=user-123&country=US&tier=premium
Authorization: Bearer {sdk_api_key}
```

**Query Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `userId` | string | No | User identifier |
| `*` | string | No | Any context attribute (country, tier, etc.) |

**Response 200 OK:**
```json
{
  "flagName": "new-checkout-flow",
  "enabled": true,
  "value": "true",
  "variant": "treatment",
  "reason": "Canary rollout: user in 30% bucket",
  "timestamp": "2025-11-23T12:00:00Z",
  "fromCache": true
}
```

**Response Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `flagName` | string | Flag name |
| `enabled` | boolean | Whether flag is enabled |
| `value` | string | Flag value (type-specific) |
| `variant` | string | Variant name (for A/B testing) |
| `reason` | string | Evaluation reason (for debugging) |
| `timestamp` | datetime | Evaluation time (UTC) |
| `fromCache` | boolean | Whether result came from cache |

---

### Bulk Evaluate Flags

Evaluate multiple flags in a single request.

**Endpoint:** `POST /api/v1/flags/evaluate/bulk`
**Authorization:** SDK, Developer, Admin
**Rate Limit:** 5,000 req/min per API key

**Request:**
```http
POST /api/v1/flags/evaluate/bulk
Authorization: Bearer {sdk_api_key}
Content-Type: application/json

{
  "context": {
    "userId": "user-123",
    "attributes": {
      "country": "US",
      "tier": "premium"
    }
  },
  "flags": ["new-checkout-flow", "dark-mode", "premium-features"]
}
```

**Response 200 OK:**
```json
{
  "evaluations": [
    {
      "flagName": "new-checkout-flow",
      "enabled": true,
      "value": "true",
      "variant": "treatment",
      "reason": "Canary rollout: 30%"
    },
    {
      "flagName": "dark-mode",
      "enabled": false,
      "value": "false",
      "reason": "Default value"
    },
    {
      "flagName": "premium-features",
      "enabled": true,
      "value": "true",
      "reason": "User segment: premium tier"
    }
  ],
  "timestamp": "2025-11-23T12:00:00Z"
}
```

---

## Rollouts API

### Create Rollout

Create a new rollout strategy for a flag.

**Endpoint:** `POST /api/v1/flags/{name}/rollouts`
**Authorization:** Developer (non-prod), Admin (prod)
**Rate Limit:** 50 req/min per user

**Request:**
```http
POST /api/v1/flags/new-checkout-flow/rollouts
Authorization: Bearer {token}
Content-Type: application/json

{
  "strategy": "Canary",
  "stages": [
    {"percentage": 10, "duration": "PT1H", "healthCheck": true},
    {"percentage": 30, "duration": "PT2H", "healthCheck": true},
    {"percentage": 50, "duration": "PT4H", "healthCheck": true},
    {"percentage": 100, "duration": null}
  ],
  "rollbackOnError": true,
  "thresholds": {
    "errorRateThreshold": 0.05,
    "latencyP99Threshold": 100
  }
}
```

**Request Fields:**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `strategy` | string | Yes | `Direct`, `Canary`, `Percentage`, `UserSegment`, `TimeBased` |
| `stages` | array | Yes | Rollout stages (for Canary/Percentage) |
| `rollbackOnError` | boolean | No | Auto-rollback on anomaly (default: true) |
| `thresholds` | object | No | Metrics thresholds for health checks |

**Response 201 Created:**
```json
{
  "rolloutId": "rollout-abc123",
  "flagName": "new-checkout-flow",
  "strategy": "Canary",
  "currentStageIndex": 0,
  "status": "Pending",
  "stages": [
    {"percentage": 10, "duration": "PT1H"},
    {"percentage": 30, "duration": "PT2H"},
    {"percentage": 50, "duration": "PT4H"},
    {"percentage": 100}
  ],
  "createdAt": "2025-11-23T12:00:00Z"
}
```

---

### Progress Rollout

Manually progress rollout to next stage.

**Endpoint:** `POST /api/v1/flags/{name}/rollouts/{id}/progress`
**Authorization:** Admin only
**Rate Limit:** 30 req/min per user

**Request:**
```http
POST /api/v1/flags/new-checkout-flow/rollouts/rollout-abc123/progress
Authorization: Bearer {token}
```

**Response 200 OK:**
```json
{
  "rolloutId": "rollout-abc123",
  "currentStageIndex": 1,
  "status": "InProgress",
  "message": "Progressed to stage 1 (30%)"
}
```

---

### Rollback Rollout

Immediately rollback a rollout.

**Endpoint:** `POST /api/v1/flags/{name}/rollouts/{id}/rollback`
**Authorization:** Admin only
**Rate Limit:** 30 req/min per user

**Request:**
```http
POST /api/v1/flags/new-checkout-flow/rollouts/rollout-abc123/rollback
Authorization: Bearer {token}
Content-Type: application/json

{
  "reason": "High error rate detected"
}
```

**Response 200 OK:**
```json
{
  "rolloutId": "rollout-abc123",
  "status": "RolledBack",
  "rolledBackAt": "2025-11-23T13:00:00Z",
  "rollbackReason": "High error rate detected"
}
```

---

### Get Rollout Metrics

Get real-time metrics for a rollout.

**Endpoint:** `GET /api/v1/flags/{name}/rollouts/{id}/metrics`
**Authorization:** All roles
**Rate Limit:** 300 req/min per user

**Request:**
```http
GET /api/v1/flags/new-checkout-flow/rollouts/rollout-abc123/metrics
Authorization: Bearer {token}
```

**Response 200 OK:**
```json
{
  "rolloutId": "rollout-abc123",
  "currentStage": {
    "percentage": 30,
    "startedAt": "2025-11-23T13:00:00Z",
    "durationRemaining": "PT1H30M"
  },
  "metrics": {
    "errorRate": 0.02,
    "latencyP99": 45.2,
    "throughput": 1250,
    "evaluationsTotal": 150000
  },
  "health": "Healthy",
  "timestamp": "2025-11-23T14:00:00Z"
}
```

---

## Targets API

### Create Target

Create a targeting rule for a flag.

**Endpoint:** `POST /api/v1/flags/{name}/targets`
**Authorization:** Developer, Admin
**Rate Limit:** 100 req/min per user

**Request:**
```http
POST /api/v1/flags/new-checkout-flow/targets
Authorization: Bearer {token}
Content-Type: application/json

{
  "name": "Premium users",
  "priority": 0,
  "rules": [
    {
      "attribute": "tier",
      "operator": "Equals",
      "value": "premium"
    }
  ],
  "value": "true"
}
```

**Request Fields:**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `name` | string | No | Target name/description |
| `priority` | int | No | Rule priority (lower = higher priority, default: 0) |
| `rules` | array | Yes | Targeting rules (AND logic) |
| `value` | string | Yes | Value to return when rules match |

**Rule Operators:**
- `Equals`, `NotEquals`
- `In`, `NotIn` (value is JSON array)
- `Contains`, `StartsWith`, `EndsWith`
- `GreaterThan`, `LessThan`
- `Regex`

**Response 201 Created:**
```json
{
  "targetId": "target-xyz789",
  "flagName": "new-checkout-flow",
  "name": "Premium users",
  "priority": 0,
  "rules": [
    {
      "attribute": "tier",
      "operator": "Equals",
      "value": "premium"
    }
  ],
  "value": "true",
  "isActive": true,
  "createdAt": "2025-11-23T12:00:00Z"
}
```

---

### List Targets

List all targeting rules for a flag.

**Endpoint:** `GET /api/v1/flags/{name}/targets`
**Authorization:** All roles
**Rate Limit:** 300 req/min per user

**Request:**
```http
GET /api/v1/flags/new-checkout-flow/targets
Authorization: Bearer {token}
```

**Response 200 OK:**
```json
{
  "flagName": "new-checkout-flow",
  "targets": [
    {
      "targetId": "target-xyz789",
      "name": "Premium users",
      "priority": 0,
      "value": "true",
      "isActive": true
    },
    {
      "targetId": "target-abc123",
      "name": "Beta testers",
      "priority": 1,
      "value": "true",
      "isActive": true
    }
  ],
  "total": 2
}
```

---

### Update Target

Update a targeting rule.

**Endpoint:** `PUT /api/v1/flags/{name}/targets/{id}`
**Authorization:** Developer, Admin
**Rate Limit:** 100 req/min per user

**Request:**
```http
PUT /api/v1/flags/new-checkout-flow/targets/target-xyz789
Authorization: Bearer {token}
Content-Type: application/json

{
  "priority": 5,
  "isActive": false
}
```

**Response 200 OK:**
```json
{
  "targetId": "target-xyz789",
  "priority": 5,
  "isActive": false,
  "updatedAt": "2025-11-23T14:00:00Z"
}
```

---

### Delete Target

Delete a targeting rule.

**Endpoint:** `DELETE /api/v1/flags/{name}/targets/{id}`
**Authorization:** Developer, Admin
**Rate Limit:** 50 req/min per user

**Request:**
```http
DELETE /api/v1/flags/new-checkout-flow/targets/target-xyz789
Authorization: Bearer {token}
```

**Response 204 No Content**

---

## Experiments API

### Create Experiment

Create a new A/B test experiment.

**Endpoint:** `POST /api/v1/experiments`
**Authorization:** Developer, Admin
**Rate Limit:** 50 req/min per user

**Request:**
```http
POST /api/v1/experiments
Authorization: Bearer {token}
Content-Type: application/json

{
  "name": "Checkout Flow A/B Test",
  "hypothesis": "New checkout flow increases conversion by 15%",
  "flagName": "new-checkout-flow",
  "variants": [
    {
      "name": "control",
      "value": "false",
      "allocation": 50,
      "isControl": true
    },
    {
      "name": "treatment",
      "value": "true",
      "allocation": 50
    }
  ],
  "primaryMetric": "conversion_rate",
  "secondaryMetrics": ["cart_value", "time_to_checkout"],
  "sampleSize": 10000,
  "significanceLevel": 0.05
}
```

**Response 201 Created:**
```json
{
  "experimentId": "exp-abc123",
  "name": "Checkout Flow A/B Test",
  "flagName": "new-checkout-flow",
  "status": "Draft",
  "variants": [
    {"name": "control", "allocation": 50, "isControl": true},
    {"name": "treatment", "allocation": 50}
  ],
  "createdAt": "2025-11-23T12:00:00Z"
}
```

---

### Get Experiment Results

Get statistical results for an experiment.

**Endpoint:** `GET /api/v1/experiments/{id}/results`
**Authorization:** All roles
**Rate Limit:** 300 req/min per user

**Request:**
```http
GET /api/v1/experiments/exp-abc123/results
Authorization: Bearer {token}
```

**Response 200 OK:**
```json
{
  "experimentId": "exp-abc123",
  "name": "Checkout Flow A/B Test",
  "status": "Running",
  "sampleSize": 8542,
  "results": {
    "control": {
      "variant": "control",
      "sampleSize": 4271,
      "conversionRate": 0.052,
      "confidenceInterval": [0.048, 0.056]
    },
    "treatment": {
      "variant": "treatment",
      "sampleSize": 4271,
      "conversionRate": 0.061,
      "confidenceInterval": [0.057, 0.065]
    }
  },
  "statisticalSignificance": {
    "pValue": 0.003,
    "isSignificant": true,
    "confidenceLevel": 0.997,
    "relativeUplift": 0.173
  },
  "recommendation": "Winner: treatment (17.3% uplift, p=0.003)"
}
```

---

### Declare Winner

Declare a winning variant for an experiment.

**Endpoint:** `POST /api/v1/experiments/{id}/declare-winner`
**Authorization:** Admin only
**Rate Limit:** 10 req/min per user

**Request:**
```http
POST /api/v1/experiments/exp-abc123/declare-winner
Authorization: Bearer {token}
Content-Type: application/json

{
  "winningVariant": "treatment"
}
```

**Response 200 OK:**
```json
{
  "experimentId": "exp-abc123",
  "status": "WinnerDeclared",
  "winningVariant": "treatment",
  "declaredAt": "2025-11-23T15:00:00Z"
}
```

---

## Error Responses

All error responses follow a consistent format:

### Error Response Format

```json
{
  "error": {
    "code": "VALIDATION_ERROR",
    "message": "Flag validation failed",
    "details": [
      {
        "field": "defaultValue",
        "message": "Default value 'invalid' is not valid for type Boolean"
      }
    ],
    "traceId": "trace-xyz789",
    "timestamp": "2025-11-23T15:00:00Z"
  }
}
```

### Common Error Codes

| HTTP Status | Error Code | Description |
|-------------|------------|-------------|
| 400 | `VALIDATION_ERROR` | Request validation failed |
| 401 | `UNAUTHORIZED` | Missing or invalid JWT token |
| 403 | `FORBIDDEN` | Insufficient permissions |
| 404 | `NOT_FOUND` | Flag/resource not found |
| 409 | `CONFLICT` | Flag already exists |
| 429 | `RATE_LIMIT_EXCEEDED` | Too many requests |
| 500 | `INTERNAL_ERROR` | Internal server error |

---

## Rate Limiting

All endpoints are rate-limited based on user identity (JWT token) or API key.

### Rate Limit Headers

Every response includes rate limit headers:

```http
HTTP/1.1 200 OK
X-RateLimit-Limit: 10000
X-RateLimit-Remaining: 8542
X-RateLimit-Reset: 1700144400
Retry-After: 60
```

**Headers:**
- `X-RateLimit-Limit` - Max requests per window
- `X-RateLimit-Remaining` - Remaining requests
- `X-RateLimit-Reset` - Unix timestamp when limit resets
- `Retry-After` - Seconds to wait before retrying (429 only)

### Rate Limits by Endpoint

| Endpoint | Limit | Window |
|----------|-------|--------|
| `GET /api/v1/flags/{name}/evaluate` | 10,000 req | 1 minute |
| `POST /api/v1/flags/evaluate/bulk` | 5,000 req | 1 minute |
| `POST /api/v1/flags` | 100 req | 1 minute |
| `GET /api/v1/flags` | 300 req | 1 minute |
| `POST /api/v1/flags/{name}/rollouts` | 50 req | 1 minute |
| `POST /api/v1/flags/{name}/rollouts/{id}/rollback` | 30 req | 1 minute |

---

## Webhooks

For real-time flag updates, clients can register webhooks.

### Webhook Request Format

```http
POST https://your-service.example.com/webhook
Content-Type: application/json
X-Flag-Name: new-checkout-flow
X-Event-Type: flag.updated
X-Signature: sha256=...

{
  "eventType": "flag.updated",
  "flagName": "new-checkout-flow",
  "changes": {
    "defaultValue": {
      "old": "false",
      "new": "true"
    }
  },
  "timestamp": "2025-11-23T15:00:00Z"
}
```

### Webhook Response

Consumer must respond within 5 seconds:

**Success (200-299):**
```http
HTTP/1.1 200 OK
```

---

**API Version:** 1.0.0
**Last Updated:** 2025-11-23
**Support:** api-support@example.com
