# Configuration Manager API Reference

**Version:** 1.0.0
**Base URL:** `https://api.example.com/api/v1`
**Authentication:** JWT Bearer Token
**Content-Type:** `application/json`

---

## Table of Contents

1. [Authentication](#authentication)
2. [Config Profiles API](#config-profiles-api)
3. [Config Versions API](#config-versions-api)
4. [Deployments API](#deployments-api)
5. [Service Instances API](#service-instances-api)
6. [Schemas API](#schemas-api)
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
GET /api/v1/configs
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

---

## Config Profiles API

### Create Config Profile

Create a new configuration profile for a service.

**Endpoint:** `POST /api/v1/configs`
**Authorization:** Developer, Admin
**Rate Limit:** 10 req/min per user

**Request:**
```http
POST /api/v1/configs
Authorization: Bearer {token}
Content-Type: application/json

{
  "name": "payment-service.production",
  "description": "Production configuration for payment service",
  "environment": "Production",
  "serviceType": "Microservice",
  "schemaId": "payment-config.v1",
  "defaultStrategy": "Canary",
  "settings": {
    "alertEmail": "ops@example.com",
    "slackChannel": "#deployments"
  }
}
```

**Request Fields:**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `name` | string | Yes | Unique profile name (alphanumeric, dots, dashes) |
| `description` | string | No | Human-readable description |
| `environment` | string | Yes | `Development`, `Staging`, or `Production` |
| `serviceType` | string | No | `Microservice`, `Gateway`, `Worker`, `Database`, `MessageBroker` |
| `schemaId` | string | Yes | Schema ID for validation |
| `defaultStrategy` | string | No | `Canary`, `BlueGreen`, `Rolling`, or `Direct` (default: `Canary`) |
| `settings` | object | No | Profile-specific settings |

**Response 201 Created:**
```json
{
  "name": "payment-service.production",
  "description": "Production configuration for payment service",
  "environment": "Production",
  "serviceType": "Microservice",
  "schemaId": "payment-config.v1",
  "defaultStrategy": "Canary",
  "currentVersion": null,
  "versionCount": 0,
  "deploymentCount": 0,
  "createdAt": "2025-11-23T12:00:00Z",
  "updatedAt": "2025-11-23T12:00:00Z",
  "createdBy": "admin@example.com"
}
```

**Error Responses:**
- `400 Bad Request` - Invalid profile name or configuration
- `409 Conflict` - Profile already exists
- `404 Not Found` - Schema ID does not exist

---

### List Config Profiles

List all configuration profiles.

**Endpoint:** `GET /api/v1/configs`
**Authorization:** All roles
**Rate Limit:** 60 req/min per user

**Request:**
```http
GET /api/v1/configs?environment=Production&limit=20&offset=0
Authorization: Bearer {token}
```

**Query Parameters:**

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `environment` | string | No | - | Filter by environment |
| `serviceType` | string | No | - | Filter by service type |
| `limit` | int | No | 20 | Max profiles to return (1-100) |
| `offset` | int | No | 0 | Pagination offset |

**Response 200 OK:**
```json
{
  "configs": [
    {
      "name": "payment-service.production",
      "environment": "Production",
      "currentVersion": "1.2.0",
      "versionCount": 15,
      "deploymentCount": 42,
      "createdAt": "2025-11-01T10:00:00Z"
    }
  ],
  "total": 42,
  "limit": 20,
  "offset": 0
}
```

---

### Get Config Profile

Get details about a specific configuration profile.

**Endpoint:** `GET /api/v1/configs/{name}`
**Authorization:** All roles
**Rate Limit:** 60 req/min per user

**Request:**
```http
GET /api/v1/configs/payment-service.production
Authorization: Bearer {token}
```

**Response 200 OK:**
```json
{
  "name": "payment-service.production",
  "description": "Production configuration for payment service",
  "environment": "Production",
  "serviceType": "Microservice",
  "currentVersion": "1.2.0",
  "schemaId": "payment-config.v1",
  "defaultStrategy": "Canary",
  "settings": {
    "alertEmail": "ops@example.com"
  },
  "versionCount": 15,
  "deploymentCount": 42,
  "createdAt": "2025-11-01T10:00:00Z",
  "updatedAt": "2025-11-23T12:00:00Z",
  "createdBy": "admin@example.com"
}
```

---

### Update Config Profile

Update configuration profile settings.

**Endpoint:** `PUT /api/v1/configs/{name}`
**Authorization:** Developer, Admin
**Rate Limit:** 10 req/min per user

**Request:**
```http
PUT /api/v1/configs/payment-service.production
Authorization: Bearer {token}
Content-Type: application/json

{
  "description": "Updated description",
  "defaultStrategy": "BlueGreen",
  "settings": {
    "alertEmail": "ops-team@example.com"
  }
}
```

**Response 200 OK:**
```json
{
  "name": "payment-service.production",
  "description": "Updated description",
  "defaultStrategy": "BlueGreen",
  "updatedAt": "2025-11-23T13:00:00Z"
}
```

**Note:** Cannot update `name`, `environment`, or `schemaId` after creation.

---

### Delete Config Profile

Delete a configuration profile (admin only).

**Endpoint:** `DELETE /api/v1/configs/{name}`
**Authorization:** Admin only
**Rate Limit:** 10 req/min per user

**Request:**
```http
DELETE /api/v1/configs/payment-service.production
Authorization: Bearer {token}
```

**Response 204 No Content**

**Error Responses:**
- `409 Conflict` - Profile has active deployments

---

## Config Versions API

### Upload Config Version

Upload a new configuration version.

**Endpoint:** `POST /api/v1/configs/{name}/versions`
**Authorization:** Developer, Admin
**Rate Limit:** 20 req/min per user

**Request:**
```http
POST /api/v1/configs/payment-service.production/versions
Authorization: Bearer {token}
Content-Type: application/json

{
  "version": "1.3.0",
  "configData": "{\"maxRetries\":5,\"timeout\":\"45s\",\"apiEndpoint\":\"https://api.payment.com\"}",
  "description": "Increased retry count and timeout",
  "tags": ["stable"],
  "metadata": {
    "jiraTicket": "INFRA-123"
  }
}
```

**Request Fields:**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `version` | string | Yes | Semantic version (e.g., "1.3.0") |
| `configData` | string | Yes | JSON configuration data (max 1 MB) |
| `description` | string | No | Version description/changelog |
| `tags` | array | No | Version tags (e.g., ["stable", "beta"]) |
| `metadata` | object | No | Additional metadata |

**Response 201 Created:**
```json
{
  "configName": "payment-service.production",
  "version": "1.3.0",
  "schemaVersion": "1.0",
  "description": "Increased retry count and timeout",
  "tags": ["stable"],
  "configHash": "5d41402abc4b2a76b9719d911017c592",
  "sizeBytes": 95,
  "createdAt": "2025-11-23T12:00:00Z",
  "createdBy": "admin@example.com"
}
```

**Error Responses:**
- `400 Bad Request` - Invalid version format or config data
- `409 Conflict` - Version already exists
- `422 Unprocessable Entity` - Schema validation failed

---

### List Config Versions

List all versions of a configuration.

**Endpoint:** `GET /api/v1/configs/{name}/versions`
**Authorization:** All roles
**Rate Limit:** 60 req/min per user

**Request:**
```http
GET /api/v1/configs/payment-service.production/versions?limit=10
Authorization: Bearer {token}
```

**Query Parameters:**

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `limit` | int | No | 20 | Max versions to return |
| `offset` | int | No | 0 | Pagination offset |
| `tags` | string | No | - | Filter by tag |

**Response 200 OK:**
```json
{
  "versions": [
    {
      "version": "1.3.0",
      "description": "Increased retry count and timeout",
      "tags": ["stable"],
      "sizeBytes": 95,
      "deploymentCount": 5,
      "createdAt": "2025-11-23T12:00:00Z",
      "createdBy": "admin@example.com"
    },
    {
      "version": "1.2.0",
      "description": "Updated API endpoint",
      "tags": ["stable"],
      "sizeBytes": 87,
      "deploymentCount": 42,
      "createdAt": "2025-11-15T10:00:00Z",
      "createdBy": "developer@example.com"
    }
  ],
  "total": 15,
  "limit": 10,
  "offset": 0
}
```

---

### Get Config Version

Get a specific configuration version.

**Endpoint:** `GET /api/v1/configs/{name}/versions/{version}`
**Authorization:** All roles
**Rate Limit:** 60 req/min per user

**Request:**
```http
GET /api/v1/configs/payment-service.production/versions/1.3.0
Authorization: Bearer {token}
```

**Response 200 OK:**
```json
{
  "configName": "payment-service.production",
  "version": "1.3.0",
  "configData": "{\"maxRetries\":5,\"timeout\":\"45s\",\"apiEndpoint\":\"https://api.payment.com\"}",
  "schemaVersion": "1.0",
  "description": "Increased retry count and timeout",
  "tags": ["stable"],
  "metadata": {
    "jiraTicket": "INFRA-123"
  },
  "configHash": "5d41402abc4b2a76b9719d911017c592",
  "sizeBytes": 95,
  "deploymentCount": 5,
  "isDeprecated": false,
  "createdAt": "2025-11-23T12:00:00Z",
  "createdBy": "admin@example.com"
}
```

---

### Compare Config Versions

Get diff between two configuration versions.

**Endpoint:** `GET /api/v1/configs/{name}/versions/{v1}/diff/{v2}`
**Authorization:** All roles
**Rate Limit:** 30 req/min per user

**Request:**
```http
GET /api/v1/configs/payment-service.production/versions/1.2.0/diff/1.3.0
Authorization: Bearer {token}
```

**Response 200 OK:**
```json
{
  "configName": "payment-service.production",
  "fromVersion": "1.2.0",
  "toVersion": "1.3.0",
  "diff": {
    "added": [],
    "removed": [],
    "modified": [
      {
        "field": "maxRetries",
        "oldValue": 3,
        "newValue": 5
      },
      {
        "field": "timeout",
        "oldValue": "30s",
        "newValue": "45s"
      }
    ]
  },
  "isBreakingChange": false
}
```

---

### Delete Config Version

Delete a configuration version.

**Endpoint:** `DELETE /api/v1/configs/{name}/versions/{version}`
**Authorization:** Admin only
**Rate Limit:** 10 req/min per user

**Request:**
```http
DELETE /api/v1/configs/payment-service.production/versions/1.3.0
Authorization: Bearer {token}
```

**Response 204 No Content**

**Error Responses:**
- `409 Conflict` - Version is currently deployed or tagged as stable

---

## Deployments API

### Create Deployment

Deploy a configuration version to service instances.

**Endpoint:** `POST /api/v1/deployments`
**Authorization:** Operator, Admin
**Rate Limit:** 20 req/min per user

**Request:**
```http
POST /api/v1/deployments
Authorization: Bearer {token}
Content-Type: application/json

{
  "configName": "payment-service.production",
  "configVersion": "1.3.0",
  "strategy": "Canary",
  "targetInstances": [
    "payment-svc-1",
    "payment-svc-2",
    "payment-svc-3",
    "payment-svc-4",
    "payment-svc-5"
  ],
  "config": {
    "canaryPercentage": 10,
    "phaseInterval": "PT5M",
    "autoPromote": true,
    "stopOnFailure": true
  },
  "healthCheck": {
    "enabled": true,
    "checkInterval": "PT30S",
    "errorRateThreshold": 5.0,
    "latencyThreshold": 50.0,
    "autoRollback": true
  }
}
```

**Request Fields:**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `configName` | string | Yes | Configuration profile name |
| `configVersion` | string | Yes | Version to deploy |
| `strategy` | string | Yes | `Canary`, `BlueGreen`, `Rolling`, or `Direct` |
| `targetInstances` | array | Yes | List of instance IDs |
| `config` | object | No | Strategy-specific configuration |
| `healthCheck` | object | No | Health monitoring configuration |

**Response 201 Created:**
```json
{
  "deploymentId": "deploy-7f8a9b1c-2d3e-4f5g-6h7i-8j9k0l1m2n3o",
  "configName": "payment-service.production",
  "configVersion": "1.3.0",
  "strategy": "Canary",
  "status": "InProgress",
  "progressPercentage": 0,
  "targetInstances": 5,
  "startedAt": "2025-11-23T12:00:00Z",
  "initiatedBy": "operator@example.com"
}
```

---

### List Deployments

List all deployments.

**Endpoint:** `GET /api/v1/deployments`
**Authorization:** All roles
**Rate Limit:** 60 req/min per user

**Request:**
```http
GET /api/v1/deployments?configName=payment-service.production&status=InProgress&limit=20
Authorization: Bearer {token}
```

**Query Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `configName` | string | No | Filter by config name |
| `status` | string | No | Filter by status |
| `environment` | string | No | Filter by environment |
| `limit` | int | No | Max deployments to return (default: 20) |
| `offset` | int | No | Pagination offset (default: 0) |

**Response 200 OK:**
```json
{
  "deployments": [
    {
      "deploymentId": "deploy-123",
      "configName": "payment-service.production",
      "configVersion": "1.3.0",
      "strategy": "Canary",
      "status": "InProgress",
      "progressPercentage": 30,
      "startedAt": "2025-11-23T12:00:00Z",
      "initiatedBy": "operator@example.com"
    }
  ],
  "total": 150,
  "limit": 20,
  "offset": 0
}
```

---

### Get Deployment Details

Get detailed information about a deployment.

**Endpoint:** `GET /api/v1/deployments/{id}`
**Authorization:** All roles
**Rate Limit:** 60 req/min per user

**Request:**
```http
GET /api/v1/deployments/deploy-123
Authorization: Bearer {token}
```

**Response 200 OK:**
```json
{
  "deploymentId": "deploy-123",
  "configName": "payment-service.production",
  "configVersion": "1.3.0",
  "previousVersion": "1.2.0",
  "strategy": "Canary",
  "status": "InProgress",
  "progressPercentage": 30,
  "targetInstances": ["payment-svc-1", "payment-svc-2", "payment-svc-3"],
  "instanceStatus": {
    "payment-svc-1": {
      "status": "Completed",
      "deployedAt": "2025-11-23T12:05:00Z"
    },
    "payment-svc-2": {
      "status": "InProgress",
      "deployedAt": null
    },
    "payment-svc-3": {
      "status": "Pending",
      "deployedAt": null
    }
  },
  "config": {
    "canaryPercentage": 10,
    "phaseInterval": "PT5M",
    "autoPromote": true
  },
  "healthCheck": {
    "enabled": true,
    "errorRateThreshold": 5.0,
    "latencyThreshold": 50.0,
    "autoRollback": true
  },
  "startedAt": "2025-11-23T12:00:00Z",
  "completedAt": null,
  "initiatedBy": "operator@example.com",
  "wasRolledBack": false
}
```

---

### Pause Deployment

Pause an in-progress deployment.

**Endpoint:** `POST /api/v1/deployments/{id}/pause`
**Authorization:** Operator, Admin
**Rate Limit:** 30 req/min per user

**Request:**
```http
POST /api/v1/deployments/deploy-123/pause
Authorization: Bearer {token}
```

**Response 200 OK:**
```json
{
  "deploymentId": "deploy-123",
  "status": "Paused",
  "pausedAt": "2025-11-23T12:10:00Z"
}
```

---

### Resume Deployment

Resume a paused deployment.

**Endpoint:** `POST /api/v1/deployments/{id}/resume`
**Authorization:** Operator, Admin
**Rate Limit:** 30 req/min per user

**Request:**
```http
POST /api/v1/deployments/deploy-123/resume
Authorization: Bearer {token}
```

**Response 200 OK:**
```json
{
  "deploymentId": "deploy-123",
  "status": "InProgress",
  "resumedAt": "2025-11-23T12:15:00Z"
}
```

---

### Rollback Deployment

Rollback a deployment to previous version.

**Endpoint:** `POST /api/v1/deployments/{id}/rollback`
**Authorization:** Operator, Admin
**Rate Limit:** 30 req/min per user

**Request:**
```http
POST /api/v1/deployments/deploy-123/rollback
Authorization: Bearer {token}
Content-Type: application/json

{
  "reason": "High error rate detected in production"
}
```

**Response 200 OK:**
```json
{
  "deploymentId": "deploy-123",
  "status": "RolledBack",
  "previousVersion": "1.2.0",
  "rolledBackAt": "2025-11-23T12:20:00Z",
  "rollbackReason": "High error rate detected in production"
}
```

---

### Get Deployment Health

Get health metrics for a deployment.

**Endpoint:** `GET /api/v1/deployments/{id}/health`
**Authorization:** All roles
**Rate Limit:** 60 req/min per user

**Request:**
```http
GET /api/v1/deployments/deploy-123/health
Authorization: Bearer {token}
```

**Response 200 OK:**
```json
{
  "deploymentId": "deploy-123",
  "overallStatus": "Healthy",
  "baselineErrorRate": 1.5,
  "currentErrorRate": 2.1,
  "baselineP99Latency": 200,
  "currentP99Latency": 220,
  "instanceMetrics": {
    "payment-svc-1": {
      "status": "Healthy",
      "errorRate": 2.0,
      "p99Latency": 215
    }
  },
  "lastChecked": "2025-11-23T12:25:00Z"
}
```

---

## Service Instances API

### Register Instance

Register a service instance.

**Endpoint:** `POST /api/v1/instances`
**Authorization:** Developer, Admin
**Rate Limit:** 30 req/min per user

**Request:**
```http
POST /api/v1/instances
Authorization: Bearer {token}
Content-Type: application/json

{
  "serviceName": "payment-service",
  "hostname": "10.0.1.15",
  "port": 8080,
  "environment": "Production",
  "version": "1.5.2",
  "metadata": {
    "region": "us-east-1",
    "az": "us-east-1a"
  }
}
```

**Response 201 Created:**
```json
{
  "instanceId": "instance-abc123",
  "serviceName": "payment-service",
  "hostname": "10.0.1.15",
  "port": 8080,
  "environment": "Production",
  "currentConfigVersion": null,
  "registeredAt": "2025-11-23T12:00:00Z"
}
```

---

### List Instances

List all registered instances.

**Endpoint:** `GET /api/v1/instances`
**Authorization:** All roles
**Rate Limit:** 60 req/min per user

**Request:**
```http
GET /api/v1/instances?serviceName=payment-service&environment=Production
Authorization: Bearer {token}
```

**Response 200 OK:**
```json
{
  "instances": [
    {
      "instanceId": "instance-abc123",
      "serviceName": "payment-service",
      "hostname": "10.0.1.15",
      "port": 8080,
      "environment": "Production",
      "currentConfigVersion": "1.2.0",
      "health": {
        "status": "Healthy",
        "errorRate": 1.5,
        "p99Latency": 200
      },
      "lastHeartbeat": "2025-11-23T12:30:00Z"
    }
  ],
  "total": 5
}
```

---

### Send Heartbeat

Send heartbeat signal from instance.

**Endpoint:** `POST /api/v1/instances/{id}/heartbeat`
**Authorization:** Developer, Admin
**Rate Limit:** 120 req/min per user

**Request:**
```http
POST /api/v1/instances/instance-abc123/heartbeat
Authorization: Bearer {token}
Content-Type: application/json

{
  "health": {
    "status": "Healthy",
    "errorRate": 1.5,
    "p99Latency": 200,
    "cpuUsage": 45.2,
    "memoryUsage": 62.5
  }
}
```

**Response 200 OK:**
```json
{
  "instanceId": "instance-abc123",
  "lastHeartbeat": "2025-11-23T12:30:00Z"
}
```

---

### Deregister Instance

Deregister a service instance.

**Endpoint:** `DELETE /api/v1/instances/{id}`
**Authorization:** Developer, Admin
**Rate Limit:** 30 req/min per user

**Request:**
```http
DELETE /api/v1/instances/instance-abc123
Authorization: Bearer {token}
```

**Response 204 No Content**

---

## Schemas API

### Register Schema

Register a new configuration schema.

**Endpoint:** `POST /api/v1/schemas`
**Authorization:** Developer, Admin
**Rate Limit:** 10 req/min per user

**Request:**
```http
POST /api/v1/schemas
Authorization: Bearer {token}
Content-Type: application/json

{
  "schemaId": "payment-config.v2",
  "schemaDefinition": "{\"type\":\"object\",\"properties\":{\"maxRetries\":{\"type\":\"integer\"},\"timeout\":{\"type\":\"string\"}},\"required\":[\"maxRetries\",\"timeout\"]}",
  "version": "2.0",
  "compatibility": "Backward"
}
```

**Response 201 Created:**
```json
{
  "schemaId": "payment-config.v2",
  "version": "2.0",
  "compatibility": "Backward",
  "status": "Draft",
  "createdAt": "2025-11-23T12:00:00Z"
}
```

---

### Validate Config Against Schema

Validate configuration data against a schema.

**Endpoint:** `POST /api/v1/schemas/{id}/validate`
**Authorization:** Developer, Admin
**Rate Limit:** 100 req/min per user

**Request:**
```http
POST /api/v1/schemas/payment-config.v2/validate
Authorization: Bearer {token}
Content-Type: application/json

{
  "configData": "{\"maxRetries\":5,\"timeout\":\"45s\"}"
}
```

**Response 200 OK (Valid):**
```json
{
  "isValid": true,
  "schemaId": "payment-config.v2",
  "validatedAt": "2025-11-23T12:00:00Z"
}
```

**Response 200 OK (Invalid):**
```json
{
  "isValid": false,
  "errors": [
    {
      "path": "$.maxRetries",
      "message": "Required property missing"
    }
  ],
  "schemaId": "payment-config.v2",
  "validatedAt": "2025-11-23T12:00:00Z"
}
```

---

### Approve Schema

Approve a schema for production use (admin only).

**Endpoint:** `POST /api/v1/schemas/{id}/approve`
**Authorization:** Admin only
**Rate Limit:** 10 req/min per user

**Request:**
```http
POST /api/v1/schemas/payment-config.v2/approve
Authorization: Bearer {token}
Content-Type: application/json

{
  "notes": "Approved after validation in staging"
}
```

**Response 200 OK:**
```json
{
  "schemaId": "payment-config.v2",
  "status": "Approved",
  "approvedBy": "admin@example.com",
  "approvedAt": "2025-11-23T12:00:00Z"
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
    "message": "Configuration validation failed",
    "details": [
      {
        "field": "configData",
        "message": "Required property 'timeout' is missing"
      }
    ],
    "traceId": "trace-xyz789",
    "timestamp": "2025-11-23T12:00:00Z"
  }
}
```

### Common Error Codes

| HTTP Status | Error Code | Description |
|-------------|------------|-------------|
| 400 | `VALIDATION_ERROR` | Request validation failed |
| 400 | `SCHEMA_VALIDATION_ERROR` | Config failed schema validation |
| 401 | `UNAUTHORIZED` | Missing or invalid JWT token |
| 403 | `FORBIDDEN` | Insufficient permissions |
| 404 | `NOT_FOUND` | Resource not found |
| 409 | `CONFLICT` | Resource already exists |
| 422 | `UNPROCESSABLE_ENTITY` | Config validation failed |
| 429 | `RATE_LIMIT_EXCEEDED` | Too many requests |
| 500 | `INTERNAL_ERROR` | Internal server error |
| 503 | `SERVICE_UNAVAILABLE` | Service temporarily unavailable |

---

## Rate Limiting

All endpoints are rate-limited based on user identity (JWT token).

### Rate Limit Headers

Every response includes rate limit headers:

```http
HTTP/1.1 200 OK
X-RateLimit-Limit: 60
X-RateLimit-Remaining: 55
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
| `POST /api/v1/configs` | 10 req | 1 minute |
| `GET /api/v1/configs` | 60 req | 1 minute |
| `POST /api/v1/configs/{name}/versions` | 20 req | 1 minute |
| `POST /api/v1/deployments` | 20 req | 1 minute |
| `GET /api/v1/deployments` | 60 req | 1 minute |
| `POST /api/v1/instances` | 30 req | 1 minute |
| `POST /api/v1/instances/{id}/heartbeat` | 120 req | 1 minute |
| `POST /api/v1/schemas` | 10 req | 1 minute |
| `POST /api/v1/schemas/{id}/validate` | 100 req | 1 minute |

---

## Pagination

List endpoints support pagination via limit/offset parameters.

### Pagination Parameters

```http
GET /api/v1/configs?limit=20&offset=40
```

**Parameters:**
- `limit` - Max items to return (1-100, default: 20)
- `offset` - Number of items to skip (default: 0)

### Pagination Response

```json
{
  "configs": [...],
  "total": 150,
  "limit": 20,
  "offset": 40,
  "hasMore": true,
  "nextOffset": 60
}
```

---

**API Version:** 1.0.0
**Last Updated:** 2025-11-23
**Support:** api-support@example.com
