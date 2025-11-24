# CDN Configuration Manager API Reference

**Version:** 1.0.0
**Base URL:** `https://api.example.com/api/v1`
**Authentication:** JWT Bearer Token
**Content-Type:** `application/json`

---

## Table of Contents

1. [Authentication](#authentication)
2. [Configurations API](#configurations-api)
3. [Edge Locations API](#edge-locations-api)
4. [Deployments API](#deployments-api)
5. [Metrics API](#metrics-api)
6. [Versions API](#versions-api)
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
GET /api/v1/configurations
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

---

## Configurations API

### Create Configuration

Create a new CDN configuration.

**Endpoint:** `POST /api/v1/configurations`
**Authorization:** Developer, Operator, Admin
**Rate Limit:** 20 req/min per user

**Request:**
```http
POST /api/v1/configurations
Authorization: Bearer {token}
Content-Type: application/json

{
  "name": "static-assets-cache",
  "type": "CacheRule",
  "description": "Cache static assets with 1-hour TTL",
  "content": {
    "pathPattern": "/assets/*",
    "ttl": 3600,
    "cacheControl": "public, max-age=3600",
    "cacheKey": ["$uri", "$args"],
    "varyHeaders": ["Accept-Encoding"]
  },
  "schemaVersion": "1.0",
  "tags": ["production", "static-content"]
}
```

**Request Fields:**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `name` | string | Yes | Configuration name (unique) |
| `type` | string | Yes | `CacheRule`, `RoutingRule`, `SecurityRule`, `SSLCertificate`, `ResponseModification` |
| `description` | string | No | Human-readable description |
| `content` | object | Yes | Configuration content (type-specific) |
| `schemaVersion` | string | Yes | Schema version (e.g., "1.0") |
| `tags` | array | No | Tags for organization |

**Response 201 Created:**
```json
{
  "configurationId": "config-7f8a9b1c-2d3e-4f5g-6h7i-8j9k0l1m2n3o",
  "name": "static-assets-cache",
  "type": "CacheRule",
  "version": "1.0.0",
  "status": "Draft",
  "createdBy": "admin@example.com",
  "createdAt": "2025-11-23T12:00:00Z",
  "updatedAt": "2025-11-23T12:00:00Z"
}
```

**Error Responses:**
- `400 Bad Request` - Invalid configuration or validation failed
- `409 Conflict` - Configuration with this name already exists
- `429 Too Many Requests` - Rate limit exceeded

---

### List Configurations

List all configurations with optional filtering.

**Endpoint:** `GET /api/v1/configurations`
**Authorization:** All roles
**Rate Limit:** 60 req/min per user

**Request:**
```http
GET /api/v1/configurations?type=CacheRule&status=Approved&limit=20&offset=0
Authorization: Bearer {token}
```

**Query Parameters:**

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `type` | string | No | - | Filter by type |
| `status` | string | No | - | Filter by status (`Draft`, `Approved`, etc.) |
| `tags` | string | No | - | Filter by tags (comma-separated) |
| `limit` | int | No | 20 | Max configurations to return (1-100) |
| `offset` | int | No | 0 | Pagination offset |

**Response 200 OK:**
```json
{
  "configurations": [
    {
      "configurationId": "config-abc123",
      "name": "static-assets-cache",
      "type": "CacheRule",
      "version": "1.0.0",
      "status": "Approved",
      "isDeployed": true,
      "deployedLocations": ["us-east-1", "us-west-1"],
      "createdAt": "2025-11-23T12:00:00Z"
    }
  ],
  "total": 42,
  "limit": 20,
  "offset": 0
}
```

---

### Get Configuration Details

Get details about a specific configuration.

**Endpoint:** `GET /api/v1/configurations/{id}`
**Authorization:** All roles
**Rate Limit:** 60 req/min per user

**Request:**
```http
GET /api/v1/configurations/config-abc123
Authorization: Bearer {token}
```

**Response 200 OK:**
```json
{
  "configurationId": "config-abc123",
  "name": "static-assets-cache",
  "type": "CacheRule",
  "description": "Cache static assets with 1-hour TTL",
  "content": {
    "pathPattern": "/assets/*",
    "ttl": 3600,
    "cacheControl": "public, max-age=3600"
  },
  "schemaVersion": "1.0",
  "version": "1.0.0",
  "status": "Approved",
  "tags": ["production", "static-content"],
  "createdBy": "admin@example.com",
  "createdAt": "2025-11-23T12:00:00Z",
  "updatedAt": "2025-11-23T12:00:00Z",
  "approvedBy": "admin@example.com",
  "approvedAt": "2025-11-23T12:30:00Z",
  "isDeployed": true,
  "deployedLocations": ["us-east-1", "us-west-1"]
}
```

---

### Update Configuration

Update an existing configuration (creates new version).

**Endpoint:** `PUT /api/v1/configurations/{id}`
**Authorization:** Developer, Operator, Admin
**Rate Limit:** 20 req/min per user

**Request:**
```http
PUT /api/v1/configurations/config-abc123
Authorization: Bearer {token}
Content-Type: application/json

{
  "content": {
    "pathPattern": "/assets/*",
    "ttl": 7200,
    "cacheControl": "public, max-age=7200"
  },
  "changeDescription": "Increased TTL to 2 hours"
}
```

**Response 200 OK:**
```json
{
  "configurationId": "config-abc123",
  "version": "1.1.0",
  "status": "Draft",
  "updatedAt": "2025-11-23T13:00:00Z"
}
```

**Note:** Updating a configuration creates a new version. Previous versions remain immutable.

---

### Delete Configuration

Delete a configuration (admin only).

**Endpoint:** `DELETE /api/v1/configurations/{id}`
**Authorization:** Admin only
**Rate Limit:** 10 req/min per user

**Request:**
```http
DELETE /api/v1/configurations/config-abc123
Authorization: Bearer {token}
```

**Response 204 No Content**

**Error Responses:**
- `409 Conflict` - Configuration is currently deployed (undeploy first)

---

### Validate Configuration

Validate a configuration before deployment.

**Endpoint:** `POST /api/v1/configurations/{id}/validate`
**Authorization:** Developer, Operator, Admin
**Rate Limit:** 100 req/min per user

**Request:**
```http
POST /api/v1/configurations/config-abc123/validate
Authorization: Bearer {token}
```

**Response 200 OK (Valid):**
```json
{
  "isValid": true,
  "configurationId": "config-abc123",
  "validatedAt": "2025-11-23T14:00:00Z",
  "warnings": []
}
```

**Response 200 OK (Invalid):**
```json
{
  "isValid": false,
  "errors": [
    {
      "field": "content.ttl",
      "message": "TTL must be between 0 and 31536000 seconds"
    }
  ],
  "warnings": [
    {
      "field": "content.pathPattern",
      "message": "Path pattern overlaps with existing configuration 'config-xyz789'"
    }
  ],
  "configurationId": "config-abc123",
  "validatedAt": "2025-11-23T14:00:00Z"
}
```

---

### Approve Configuration

Approve a configuration for production deployment (admin only).

**Endpoint:** `POST /api/v1/configurations/{id}/approve`
**Authorization:** Admin only
**Rate Limit:** 20 req/min per user

**Request:**
```http
POST /api/v1/configurations/config-abc123/approve
Authorization: Bearer {token}
Content-Type: application/json

{
  "notes": "Validated and approved for production deployment"
}
```

**Response 200 OK:**
```json
{
  "configurationId": "config-abc123",
  "status": "Approved",
  "approvedBy": "admin@example.com",
  "approvedAt": "2025-11-23T14:30:00Z"
}
```

---

## Edge Locations API

### Create Edge Location

Register a new edge location.

**Endpoint:** `POST /api/v1/edge-locations`
**Authorization:** Admin only
**Rate Limit:** 10 req/min per user

**Request:**
```http
POST /api/v1/edge-locations
Authorization: Bearer {token}
Content-Type: application/json

{
  "locationId": "us-east-1",
  "name": "US East (Virginia)",
  "region": "North America",
  "countryCode": "US",
  "city": "Virginia",
  "type": "EdgePOP",
  "endpoint": "https://cdn-us-east-1.example.com",
  "capacity": {
    "maxRequestsPerSec": 100000,
    "maxBandwidthMbps": 10000,
    "cacheStorageGB": 1000
  }
}
```

**Request Fields:**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `locationId` | string | Yes | Unique location identifier |
| `name` | string | Yes | Human-readable name |
| `region` | string | Yes | Geographic region |
| `countryCode` | string | Yes | ISO 3166-1 alpha-2 code |
| `city` | string | No | City name |
| `type` | string | Yes | `EdgePOP`, `Shield`, `Origin` |
| `endpoint` | string | Yes | Edge location API endpoint |
| `capacity` | object | No | Capacity configuration |

**Response 201 Created:**
```json
{
  "locationId": "us-east-1",
  "name": "US East (Virginia)",
  "region": "North America",
  "type": "EdgePOP",
  "isActive": true,
  "createdAt": "2025-11-23T12:00:00Z"
}
```

---

### List Edge Locations

List all edge locations with optional filtering.

**Endpoint:** `GET /api/v1/edge-locations`
**Authorization:** All roles
**Rate Limit:** 60 req/min per user

**Request:**
```http
GET /api/v1/edge-locations?region=North%20America&isActive=true
Authorization: Bearer {token}
```

**Query Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `region` | string | No | Filter by region |
| `countryCode` | string | No | Filter by country |
| `type` | string | No | Filter by type |
| `isActive` | boolean | No | Filter by active status |

**Response 200 OK:**
```json
{
  "edgeLocations": [
    {
      "locationId": "us-east-1",
      "name": "US East (Virginia)",
      "region": "North America",
      "countryCode": "US",
      "type": "EdgePOP",
      "isActive": true,
      "health": {
        "isHealthy": true,
        "cpuUsage": 45.2,
        "memoryUsage": 62.8
      },
      "metrics": {
        "requestsTotal": 1500000,
        "cacheHitRate": 92.5,
        "avgLatencyMs": 12.3
      }
    }
  ],
  "total": 15
}
```

---

### Get Edge Location Details

Get details about a specific edge location.

**Endpoint:** `GET /api/v1/edge-locations/{id}`
**Authorization:** All roles
**Rate Limit:** 60 req/min per user

**Request:**
```http
GET /api/v1/edge-locations/us-east-1
Authorization: Bearer {token}
```

**Response 200 OK:**
```json
{
  "locationId": "us-east-1",
  "name": "US East (Virginia)",
  "region": "North America",
  "countryCode": "US",
  "city": "Virginia",
  "type": "EdgePOP",
  "endpoint": "https://cdn-us-east-1.example.com",
  "isActive": true,
  "health": {
    "isHealthy": true,
    "cpuUsage": 45.2,
    "memoryUsage": 62.8,
    "diskUsage": 30.5,
    "bandwidthUtilization": 55.0,
    "activeConnections": 12500
  },
  "metrics": {
    "requestsTotal": 1500000,
    "cacheHits": 1387500,
    "cacheMisses": 112500,
    "cacheHitRate": 92.5,
    "avgLatencyMs": 12.3,
    "p99LatencyMs": 45.7,
    "bytesSent": 52428800000,
    "errorsTotal": 1200,
    "errorRate": 0.08
  },
  "capacity": {
    "maxRequestsPerSec": 100000,
    "maxBandwidthMbps": 10000,
    "cacheStorageGB": 1000
  },
  "activeConfigurations": ["config-abc123", "config-xyz789"],
  "lastHeartbeat": "2025-11-23T14:59:50Z",
  "startedAt": "2025-11-20T10:00:00Z"
}
```

---

### Update Edge Location

Update edge location configuration.

**Endpoint:** `PUT /api/v1/edge-locations/{id}`
**Authorization:** Admin only
**Rate Limit:** 10 req/min per user

**Request:**
```http
PUT /api/v1/edge-locations/us-east-1
Authorization: Bearer {token}
Content-Type: application/json

{
  "isActive": true,
  "capacity": {
    "maxRequestsPerSec": 150000,
    "maxBandwidthMbps": 15000
  }
}
```

**Response 200 OK:**
```json
{
  "locationId": "us-east-1",
  "isActive": true,
  "updatedAt": "2025-11-23T15:00:00Z"
}
```

---

### Delete Edge Location

Delete an edge location (admin only).

**Endpoint:** `DELETE /api/v1/edge-locations/{id}`
**Authorization:** Admin only
**Rate Limit:** 5 req/min per user

**Request:**
```http
DELETE /api/v1/edge-locations/us-east-1
Authorization: Bearer {token}
```

**Response 204 No Content**

**Error Responses:**
- `409 Conflict` - Edge location has active deployments

---

## Deployments API

### Create Deployment

Deploy a configuration to edge locations.

**Endpoint:** `POST /api/v1/deployments`
**Authorization:** Operator, Admin
**Rate Limit:** 30 req/min per user

**Request:**
```http
POST /api/v1/deployments
Authorization: Bearer {token}
Content-Type: application/json

{
  "configurationId": "config-abc123",
  "configurationVersion": "1.0.0",
  "strategy": "RegionalCanary",
  "targetRegions": ["North America", "Europe"],
  "canaryConfig": {
    "initialPercentage": 10,
    "monitorDuration": "PT5M",
    "autoPromote": true,
    "promotionSteps": [10, 50, 100]
  },
  "rollbackConfig": {
    "autoRollback": true,
    "cacheHitRateThreshold": 80.0,
    "errorRateThreshold": 1.0,
    "p99LatencyThresholdMs": 200.0
  }
}
```

**Request Fields:**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `configurationId` | string | Yes | Configuration to deploy |
| `configurationVersion` | string | No | Specific version (defaults to latest) |
| `strategy` | string | Yes | Deployment strategy |
| `targetRegions` | array | Conditional | Target regions (required if not targetLocations) |
| `targetLocations` | array | Conditional | Specific edge locations |
| `canaryConfig` | object | Conditional | Canary configuration (if using RegionalCanary) |
| `rollbackConfig` | object | No | Rollback configuration |

**Response 202 Accepted:**
```json
{
  "deploymentId": "deploy-7f8a9b1c-2d3e-4f5g-6h7i-8j9k0l1m2n3o",
  "configurationId": "config-abc123",
  "configurationVersion": "1.0.0",
  "strategy": "RegionalCanary",
  "status": "Pending",
  "startedAt": "2025-11-23T15:00:00Z",
  "progressPercentage": 0
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
GET /api/v1/deployments?status=InProgress&limit=20
Authorization: Bearer {token}
```

**Query Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `configurationId` | string | No | Filter by configuration |
| `status` | string | No | Filter by status |
| `strategy` | string | No | Filter by strategy |
| `limit` | int | No | Max results (1-100) |
| `offset` | int | No | Pagination offset |

**Response 200 OK:**
```json
{
  "deployments": [
    {
      "deploymentId": "deploy-abc123",
      "configurationId": "config-abc123",
      "strategy": "RegionalCanary",
      "status": "InProgress",
      "progressPercentage": 50,
      "startedAt": "2025-11-23T15:00:00Z"
    }
  ],
  "total": 15,
  "limit": 20,
  "offset": 0
}
```

---

### Get Deployment Details

Get details about a specific deployment.

**Endpoint:** `GET /api/v1/deployments/{id}`
**Authorization:** All roles
**Rate Limit:** 60 req/min per user

**Request:**
```http
GET /api/v1/deployments/deploy-abc123
Authorization: Bearer {token}
```

**Response 200 OK:**
```json
{
  "deploymentId": "deploy-abc123",
  "configurationId": "config-abc123",
  "configurationVersion": "1.0.0",
  "strategy": "RegionalCanary",
  "status": "InProgress",
  "targetRegions": ["North America", "Europe"],
  "canaryConfig": {
    "initialPercentage": 10,
    "currentStep": 1,
    "promotionSteps": [10, 50, 100]
  },
  "progressPercentage": 50,
  "successfulLocations": ["us-east-1", "us-west-1"],
  "failedLocations": [],
  "errors": [],
  "preDeploymentMetrics": {
    "cacheHitRate": 91.2,
    "avgLatencyMs": 13.5,
    "errorRate": 0.05
  },
  "postDeploymentMetrics": {
    "cacheHitRate": 92.5,
    "avgLatencyMs": 12.3,
    "errorRate": 0.03
  },
  "deployedBy": "operator@example.com",
  "startedAt": "2025-11-23T15:00:00Z",
  "durationSeconds": 450
}
```

---

### Promote Canary Deployment

Promote a canary deployment to the next stage.

**Endpoint:** `POST /api/v1/deployments/{id}/promote`
**Authorization:** Operator, Admin
**Rate Limit:** 30 req/min per user

**Request:**
```http
POST /api/v1/deployments/deploy-abc123/promote
Authorization: Bearer {token}
```

**Response 200 OK:**
```json
{
  "deploymentId": "deploy-abc123",
  "status": "InProgress",
  "progressPercentage": 100,
  "currentStep": 2,
  "promotedAt": "2025-11-23T15:10:00Z"
}
```

---

### Rollback Deployment

Manually rollback a deployment.

**Endpoint:** `POST /api/v1/deployments/{id}/rollback`
**Authorization:** Operator, Admin
**Rate Limit:** 30 req/min per user

**Request:**
```http
POST /api/v1/deployments/deploy-abc123/rollback
Authorization: Bearer {token}
Content-Type: application/json

{
  "reason": "High error rate detected in canary"
}
```

**Response 200 OK:**
```json
{
  "deploymentId": "deploy-abc123",
  "status": "RolledBack",
  "rolledBackAt": "2025-11-23T15:15:00Z",
  "reason": "High error rate detected in canary"
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
POST /api/v1/deployments/deploy-abc123/pause
Authorization: Bearer {token}
```

**Response 200 OK:**
```json
{
  "deploymentId": "deploy-abc123",
  "status": "Paused",
  "pausedAt": "2025-11-23T15:20:00Z"
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
POST /api/v1/deployments/deploy-abc123/resume
Authorization: Bearer {token}
```

**Response 200 OK:**
```json
{
  "deploymentId": "deploy-abc123",
  "status": "InProgress",
  "resumedAt": "2025-11-23T15:25:00Z"
}
```

---

## Metrics API

### Get Edge Location Metrics

Get real-time metrics for an edge location.

**Endpoint:** `GET /api/v1/metrics/edge-locations/{id}`
**Authorization:** All roles
**Rate Limit:** 120 req/min per user

**Request:**
```http
GET /api/v1/metrics/edge-locations/us-east-1?window=PT1H
Authorization: Bearer {token}
```

**Query Parameters:**

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `window` | string | No | `PT1H` | Time window (ISO 8601 duration) |

**Response 200 OK:**
```json
{
  "locationId": "us-east-1",
  "window": "PT1H",
  "metrics": {
    "requestsTotal": 360000,
    "cacheHits": 334800,
    "cacheMisses": 25200,
    "cacheHitRate": 93.0,
    "avgLatencyMs": 11.8,
    "p50LatencyMs": 8.5,
    "p95LatencyMs": 28.3,
    "p99LatencyMs": 42.1,
    "bytesSent": 15728640000,
    "bytesReceived": 524288000,
    "errorsTotal": 180,
    "errorRate": 0.05
  },
  "timestamp": "2025-11-23T16:00:00Z"
}
```

---

### Get Configuration Metrics

Get metrics for a specific configuration across all deployed locations.

**Endpoint:** `GET /api/v1/metrics/configurations/{id}`
**Authorization:** All roles
**Rate Limit:** 120 req/min per user

**Request:**
```http
GET /api/v1/metrics/configurations/config-abc123?window=PT1H
Authorization: Bearer {token}
```

**Response 200 OK:**
```json
{
  "configurationId": "config-abc123",
  "window": "PT1H",
  "deployedLocations": ["us-east-1", "us-west-1", "eu-west-1"],
  "aggregateMetrics": {
    "requestsTotal": 1080000,
    "cacheHitRate": 92.5,
    "avgLatencyMs": 13.2,
    "p99LatencyMs": 45.0,
    "errorRate": 0.06
  },
  "locationMetrics": {
    "us-east-1": {
      "requestsTotal": 360000,
      "cacheHitRate": 93.0,
      "avgLatencyMs": 11.8
    },
    "us-west-1": {
      "requestsTotal": 360000,
      "cacheHitRate": 92.5,
      "avgLatencyMs": 13.5
    },
    "eu-west-1": {
      "requestsTotal": 360000,
      "cacheHitRate": 92.0,
      "avgLatencyMs": 14.3
    }
  },
  "timestamp": "2025-11-23T16:00:00Z"
}
```

---

### Get Regional Metrics

Get aggregated metrics for a region.

**Endpoint:** `GET /api/v1/metrics/regions/{region}`
**Authorization:** All roles
**Rate Limit:** 120 req/min per user

**Request:**
```http
GET /api/v1/metrics/regions/North%20America?window=PT1H
Authorization: Bearer {token}
```

**Response 200 OK:**
```json
{
  "region": "North America",
  "window": "PT1H",
  "edgeLocations": ["us-east-1", "us-west-1", "us-central-1"],
  "aggregateMetrics": {
    "requestsTotal": 1080000,
    "cacheHitRate": 92.8,
    "avgLatencyMs": 12.5,
    "p99LatencyMs": 43.2,
    "errorRate": 0.05,
    "bandwidthMbps": 8500
  },
  "timestamp": "2025-11-23T16:00:00Z"
}
```

---

## Versions API

### List Configuration Versions

List all versions of a configuration.

**Endpoint:** `GET /api/v1/configurations/{id}/versions`
**Authorization:** All roles
**Rate Limit:** 60 req/min per user

**Request:**
```http
GET /api/v1/configurations/config-abc123/versions
Authorization: Bearer {token}
```

**Response 200 OK:**
```json
{
  "configurationId": "config-abc123",
  "versions": [
    {
      "versionId": "ver-123",
      "version": "1.0.0",
      "changeDescription": "Initial version",
      "createdBy": "admin@example.com",
      "createdAt": "2025-11-23T12:00:00Z",
      "isDeployed": false,
      "tags": []
    },
    {
      "versionId": "ver-456",
      "version": "1.1.0",
      "changeDescription": "Increased TTL to 2 hours",
      "createdBy": "admin@example.com",
      "createdAt": "2025-11-23T13:00:00Z",
      "isDeployed": true,
      "tags": ["production"]
    }
  ],
  "total": 2
}
```

---

### Get Specific Version

Get details about a specific configuration version.

**Endpoint:** `GET /api/v1/configurations/{id}/versions/{version}`
**Authorization:** All roles
**Rate Limit:** 60 req/min per user

**Request:**
```http
GET /api/v1/configurations/config-abc123/versions/1.1.0
Authorization: Bearer {token}
```

**Response 200 OK:**
```json
{
  "versionId": "ver-456",
  "configurationId": "config-abc123",
  "version": "1.1.0",
  "content": {
    "pathPattern": "/assets/*",
    "ttl": 7200,
    "cacheControl": "public, max-age=7200"
  },
  "schemaVersion": "1.0",
  "changeDescription": "Increased TTL to 2 hours",
  "createdBy": "admin@example.com",
  "createdAt": "2025-11-23T13:00:00Z",
  "isDeployed": true,
  "tags": ["production"]
}
```

---

### Compare Versions

Compare two configuration versions.

**Endpoint:** `GET /api/v1/configurations/{id}/diff`
**Authorization:** All roles
**Rate Limit:** 60 req/min per user

**Request:**
```http
GET /api/v1/configurations/config-abc123/diff?from=1.0.0&to=1.1.0
Authorization: Bearer {token}
```

**Response 200 OK:**
```json
{
  "configurationId": "config-abc123",
  "fromVersion": "1.0.0",
  "toVersion": "1.1.0",
  "changes": [
    {
      "field": "content.ttl",
      "oldValue": 3600,
      "newValue": 7200,
      "changeType": "Modified"
    },
    {
      "field": "content.cacheControl",
      "oldValue": "public, max-age=3600",
      "newValue": "public, max-age=7200",
      "changeType": "Modified"
    }
  ],
  "summary": {
    "totalChanges": 2,
    "breakingChanges": 0
  }
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
        "field": "content.ttl",
        "message": "TTL must be between 0 and 31536000 seconds"
      }
    ],
    "traceId": "trace-xyz789",
    "timestamp": "2025-11-23T16:00:00Z"
  }
}
```

### Common Error Codes

| HTTP Status | Error Code | Description |
|-------------|------------|-------------|
| 400 | `VALIDATION_ERROR` | Request validation failed |
| 400 | `CONFIG_VALIDATION_ERROR` | Configuration validation failed |
| 401 | `UNAUTHORIZED` | Missing or invalid JWT token |
| 403 | `FORBIDDEN` | Insufficient permissions |
| 404 | `NOT_FOUND` | Resource not found |
| 409 | `CONFLICT` | Resource already exists or conflict |
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
X-RateLimit-Remaining: 45
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
| `POST /api/v1/configurations` | 20 req | 1 minute |
| `GET /api/v1/configurations` | 60 req | 1 minute |
| `POST /api/v1/deployments` | 30 req | 1 minute |
| `POST /api/v1/deployments/{id}/rollback` | 30 req | 1 minute |
| `GET /api/v1/metrics/*` | 120 req | 1 minute |
| `POST /api/v1/edge-locations` | 10 req | 1 minute |
| `POST /api/v1/configurations/{id}/approve` | 20 req | 1 minute |

---

## Pagination

List endpoints support pagination via limit/offset parameters.

### Pagination Parameters

```http
GET /api/v1/configurations?limit=20&offset=40
```

**Parameters:**
- `limit` - Max items to return (1-100, default: 20)
- `offset` - Number of items to skip (default: 0)

### Pagination Response

```json
{
  "configurations": [...],
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
