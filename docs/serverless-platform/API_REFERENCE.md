# Serverless Function Platform API Reference

**Version:** 1.0.0
**Base URL:** `https://api.example.com/api/v1`
**Authentication:** JWT Bearer Token
**Content-Type:** `application/json`

---

## Table of Contents

1. [Authentication](#authentication)
2. [Functions API](#functions-api)
3. [Versions API](#versions-api)
4. [Aliases API](#aliases-api)
5. [Deployments API](#deployments-api)
6. [Triggers API](#triggers-api)
7. [Invocations API](#invocations-api)
8. [Runners API](#runners-api)
9. [Error Responses](#error-responses)
10. [Rate Limiting](#rate-limiting)

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
GET /api/v1/functions
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

---

## Functions API

### Create Function

Create a new serverless function.

**Endpoint:** `POST /api/v1/functions`
**Authorization:** Developer, Admin
**Rate Limit:** 20 req/min per user

**Request:**
```http
POST /api/v1/functions
Authorization: Bearer {token}
Content-Type: application/json

{
  "name": "image-processor",
  "description": "Process and resize images",
  "runtime": "Python311",
  "handler": "handler.process_image",
  "memorySize": 512,
  "timeout": 30,
  "environment": {
    "BUCKET_NAME": "processed-images",
    "MAX_SIZE": "1024"
  },
  "tags": {
    "team": "media",
    "cost-center": "engineering"
  }
}
```

**Response 201 Created:**
```json
{
  "name": "image-processor",
  "description": "Process and resize images",
  "runtime": "Python311",
  "handler": "handler.process_image",
  "memorySize": 512,
  "timeout": 30,
  "publishedVersion": null,
  "ownerId": "user-123",
  "createdAt": "2025-11-23T12:00:00Z",
  "updatedAt": "2025-11-23T12:00:00Z"
}
```

**Error Responses:**
- `400 Bad Request` - Invalid configuration
- `409 Conflict` - Function already exists
- `429 Too Many Requests` - Rate limit exceeded

---

### List Functions

List all functions (with pagination and filtering).

**Endpoint:** `GET /api/v1/functions`
**Authorization:** All roles
**Rate Limit:** 60 req/min per user

**Request:**
```http
GET /api/v1/functions?runtime=Python311&limit=20&offset=0
Authorization: Bearer {token}
```

**Query Parameters:**

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `runtime` | string | No | - | Filter by runtime |
| `tag` | string | No | - | Filter by tag (key:value) |
| `limit` | int | No | 20 | Max functions to return (1-100) |
| `offset` | int | No | 0 | Pagination offset |

**Response 200 OK:**
```json
{
  "functions": [
    {
      "name": "image-processor",
      "runtime": "Python311",
      "memorySize": 512,
      "totalInvocations": 15234,
      "lastInvokedAt": "2025-11-23T11:50:00Z",
      "createdAt": "2025-11-23T10:00:00Z"
    }
  ],
  "total": 42,
  "limit": 20,
  "offset": 0
}
```

---

### Get Function Details

Get detailed information about a function.

**Endpoint:** `GET /api/v1/functions/{name}`
**Authorization:** All roles
**Rate Limit:** 60 req/min per user

**Request:**
```http
GET /api/v1/functions/image-processor
Authorization: Bearer {token}
```

**Response 200 OK:**
```json
{
  "name": "image-processor",
  "description": "Process and resize images",
  "runtime": "Python311",
  "handler": "handler.process_image",
  "memorySize": 512,
  "timeout": 30,
  "environment": {
    "BUCKET_NAME": "processed-images",
    "MAX_SIZE": "1024"
  },
  "publishedVersion": "v5",
  "totalInvocations": 15234,
  "lastInvokedAt": "2025-11-23T11:50:00Z",
  "createdAt": "2025-11-23T10:00:00Z",
  "updatedAt": "2025-11-23T11:00:00Z"
}
```

---

### Update Function Configuration

Update function configuration (memory, timeout, environment variables).

**Endpoint:** `PUT /api/v1/functions/{name}`
**Authorization:** Developer, Admin
**Rate Limit:** 20 req/min per user

**Request:**
```http
PUT /api/v1/functions/image-processor
Authorization: Bearer {token}
Content-Type: application/json

{
  "memorySize": 1024,
  "timeout": 60,
  "environment": {
    "BUCKET_NAME": "processed-images-v2",
    "MAX_SIZE": "2048"
  }
}
```

**Response 200 OK:**
```json
{
  "name": "image-processor",
  "memorySize": 1024,
  "timeout": 60,
  "updatedAt": "2025-11-23T12:30:00Z"
}
```

---

### Delete Function

Delete a function (soft delete with retention).

**Endpoint:** `DELETE /api/v1/functions/{name}`
**Authorization:** Admin only
**Rate Limit:** 10 req/min per user

**Request:**
```http
DELETE /api/v1/functions/image-processor
Authorization: Bearer {token}
```

**Response 204 No Content**

**Error Responses:**
- `409 Conflict` - Function has active deployments

---

### Get Function Metrics

Get real-time metrics for a function.

**Endpoint:** `GET /api/v1/functions/{name}/metrics`
**Authorization:** All roles
**Rate Limit:** 60 req/min per user

**Request:**
```http
GET /api/v1/functions/image-processor/metrics?window=PT1H
Authorization: Bearer {token}
```

**Query Parameters:**

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `window` | string | No | `PT1H` | Time window (ISO 8601 duration) |

**Response 200 OK:**
```json
{
  "functionName": "image-processor",
  "window": "PT1H",
  "metrics": {
    "invocations": 5432,
    "errors": 12,
    "errorRate": 0.22,
    "coldStarts": 45,
    "avgDuration": 145.5,
    "p50Duration": 120.0,
    "p95Duration": 250.0,
    "p99Duration": 450.0,
    "avgMemoryUsed": 256,
    "maxMemoryUsed": 512,
    "throttles": 0
  },
  "timestamp": "2025-11-23T13:00:00Z"
}
```

---

## Versions API

### Create Function Version

Upload function code and create a new version.

**Endpoint:** `POST /api/v1/functions/{name}/versions`
**Authorization:** Developer, Admin
**Rate Limit:** 20 req/min per user

**Request:**
```http
POST /api/v1/functions/image-processor/versions
Authorization: Bearer {token}
Content-Type: application/json

{
  "code": "<base64-encoded-zip-file>",
  "description": "Fix image orientation bug",
  "environment": {
    "BUCKET_NAME": "processed-images"
  }
}
```

**Request Fields:**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `code` | string | Yes | Base64-encoded ZIP file (max 50 MB) |
| `description` | string | No | Version description/changelog |
| `environment` | object | No | Override environment variables |

**Response 201 Created:**
```json
{
  "functionName": "image-processor",
  "version": 6,
  "versionString": "v6",
  "codeSha256": "abc123def456...",
  "codeSize": 1048576,
  "codeLocation": "s3://functions/image-processor/versions/6/code.zip",
  "runtime": "Python311",
  "createdAt": "2025-11-23T12:00:00Z",
  "createdBy": "user-123"
}
```

---

### List Function Versions

List all versions of a function.

**Endpoint:** `GET /api/v1/functions/{name}/versions`
**Authorization:** All roles
**Rate Limit:** 60 req/min per user

**Request:**
```http
GET /api/v1/functions/image-processor/versions?limit=10
Authorization: Bearer {token}
```

**Response 200 OK:**
```json
{
  "functionName": "image-processor",
  "versions": [
    {
      "version": 6,
      "versionString": "v6",
      "codeSha256": "abc123...",
      "invocationCount": 1250,
      "createdAt": "2025-11-23T12:00:00Z"
    },
    {
      "version": 5,
      "versionString": "v5",
      "codeSha256": "def456...",
      "invocationCount": 15234,
      "createdAt": "2025-11-22T10:00:00Z"
    }
  ],
  "total": 6
}
```

---

### Get Version Details

Get detailed information about a specific version.

**Endpoint:** `GET /api/v1/functions/{name}/versions/{version}`
**Authorization:** All roles
**Rate Limit:** 60 req/min per user

**Request:**
```http
GET /api/v1/functions/image-processor/versions/6
Authorization: Bearer {token}
```

**Response 200 OK:**
```json
{
  "functionName": "image-processor",
  "version": 6,
  "versionString": "v6",
  "codeSha256": "abc123def456...",
  "codeSize": 1048576,
  "codeLocation": "s3://functions/image-processor/versions/6/code.zip",
  "runtime": "Python311",
  "handler": "handler.process_image",
  "memorySize": 512,
  "timeout": 30,
  "environment": {
    "BUCKET_NAME": "processed-images"
  },
  "invocationCount": 1250,
  "createdAt": "2025-11-23T12:00:00Z",
  "createdBy": "user-123",
  "status": "Active"
}
```

---

### Delete Version

Delete a function version.

**Endpoint:** `DELETE /api/v1/functions/{name}/versions/{version}`
**Authorization:** Developer, Admin
**Rate Limit:** 10 req/min per user

**Request:**
```http
DELETE /api/v1/functions/image-processor/versions/1
Authorization: Bearer {token}
```

**Response 204 No Content**

**Error Responses:**
- `409 Conflict` - Version is actively deployed or has aliases

---

## Aliases API

### Create Alias

Create a new function alias.

**Endpoint:** `POST /api/v1/functions/{name}/aliases`
**Authorization:** Developer, Admin
**Rate Limit:** 20 req/min per user

**Request:**
```http
POST /api/v1/functions/image-processor/aliases
Authorization: Bearer {token}
Content-Type: application/json

{
  "aliasName": "production",
  "version": 5,
  "description": "Production version"
}
```

**Response 201 Created:**
```json
{
  "functionName": "image-processor",
  "aliasName": "production",
  "version": 5,
  "description": "Production version",
  "createdAt": "2025-11-23T12:00:00Z"
}
```

---

### Update Alias

Update an alias to point to a new version.

**Endpoint:** `PUT /api/v1/functions/{name}/aliases/{alias}`
**Authorization:** Operator, Admin
**Rate Limit:** 20 req/min per user

**Request (Simple Update):**
```http
PUT /api/v1/functions/image-processor/aliases/production
Authorization: Bearer {token}
Content-Type: application/json

{
  "version": 6
}
```

**Request (Weighted Routing for Canary):**
```http
PUT /api/v1/functions/image-processor/aliases/production
Authorization: Bearer {token}
Content-Type: application/json

{
  "version": 5,
  "routingConfig": {
    "5": 90,
    "6": 10
  }
}
```

**Response 200 OK:**
```json
{
  "functionName": "image-processor",
  "aliasName": "production",
  "version": 5,
  "routingConfig": {
    "5": 90,
    "6": 10
  },
  "updatedAt": "2025-11-23T13:00:00Z"
}
```

---

### Get Alias Details

Get alias information including routing configuration.

**Endpoint:** `GET /api/v1/functions/{name}/aliases/{alias}`
**Authorization:** All roles
**Rate Limit:** 60 req/min per user

**Request:**
```http
GET /api/v1/functions/image-processor/aliases/production
Authorization: Bearer {token}
```

**Response 200 OK:**
```json
{
  "functionName": "image-processor",
  "aliasName": "production",
  "version": 5,
  "routingConfig": {
    "5": 90,
    "6": 10
  },
  "description": "Production version",
  "createdAt": "2025-11-23T12:00:00Z",
  "updatedAt": "2025-11-23T13:00:00Z"
}
```

---

### Delete Alias

Delete a function alias.

**Endpoint:** `DELETE /api/v1/functions/{name}/aliases/{alias}`
**Authorization:** Operator, Admin
**Rate Limit:** 10 req/min per user

**Request:**
```http
DELETE /api/v1/functions/image-processor/aliases/staging
Authorization: Bearer {token}
```

**Response 204 No Content**

---

## Deployments API

### Create Deployment

Deploy a function version using a deployment strategy.

**Endpoint:** `POST /api/v1/deployments`
**Authorization:** Operator, Admin
**Rate Limit:** 10 req/min per user

**Request (Canary Deployment):**
```http
POST /api/v1/deployments
Authorization: Bearer {token}
Content-Type: application/json

{
  "functionName": "image-processor",
  "version": 6,
  "strategy": "Canary",
  "config": {
    "canaryPercentage": 10,
    "canaryDuration": "PT10M",
    "canaryIncrements": [10, 50, 100],
    "rollbackOnErrorRate": 0.05,
    "rollbackOnLatencyP99": 1000
  }
}
```

**Request (Blue-Green Deployment):**
```http
POST /api/v1/deployments
Authorization: Bearer {token}
Content-Type: application/json

{
  "functionName": "image-processor",
  "version": 6,
  "strategy": "BlueGreen",
  "config": {
    "testDuration": "PT5M",
    "keepBlue": true
  }
}
```

**Response 202 Accepted:**
```json
{
  "deploymentId": "deploy-abc123",
  "functionName": "image-processor",
  "targetVersion": 6,
  "sourceVersion": 5,
  "strategy": "Canary",
  "status": "InProgress",
  "progress": 0,
  "startedAt": "2025-11-23T13:00:00Z",
  "deployedBy": "user-123"
}
```

---

### Get Deployment Status

Get current deployment status and progress.

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
  "functionName": "image-processor",
  "targetVersion": 6,
  "sourceVersion": 5,
  "strategy": "Canary",
  "status": "InProgress",
  "progress": 50,
  "currentPhase": "Canary 50%",
  "startedAt": "2025-11-23T13:00:00Z",
  "metrics": {
    "invocationCount": 1250,
    "errorCount": 5,
    "errorRate": 0.004,
    "avgDuration": 145.5,
    "p99Duration": 450.0
  }
}
```

---

### Rollback Deployment

Manually rollback a deployment to previous version.

**Endpoint:** `POST /api/v1/deployments/{id}/rollback`
**Authorization:** Operator, Admin
**Rate Limit:** 10 req/min per user

**Request:**
```http
POST /api/v1/deployments/deploy-abc123/rollback
Authorization: Bearer {token}
Content-Type: application/json

{
  "reason": "High error rate detected manually"
}
```

**Response 200 OK:**
```json
{
  "deploymentId": "deploy-abc123",
  "status": "RolledBack",
  "rollbackReason": "High error rate detected manually",
  "completedAt": "2025-11-23T13:15:00Z"
}
```

---

### List Deployments

List all deployments (with filtering).

**Endpoint:** `GET /api/v1/deployments`
**Authorization:** All roles
**Rate Limit:** 60 req/min per user

**Request:**
```http
GET /api/v1/deployments?functionName=image-processor&status=Completed&limit=10
Authorization: Bearer {token}
```

**Query Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `functionName` | string | No | Filter by function |
| `status` | string | No | Filter by status |
| `limit` | int | No | Max deployments to return |

**Response 200 OK:**
```json
{
  "deployments": [
    {
      "deploymentId": "deploy-abc123",
      "functionName": "image-processor",
      "targetVersion": 6,
      "strategy": "Canary",
      "status": "Completed",
      "startedAt": "2025-11-23T13:00:00Z",
      "completedAt": "2025-11-23T13:30:00Z"
    }
  ],
  "total": 25
}
```

---

## Triggers API

### Create Trigger

Create an event trigger for a function.

**Endpoint:** `POST /api/v1/functions/{name}/triggers`
**Authorization:** Developer, Admin
**Rate Limit:** 20 req/min per user

**Request (HTTP Trigger):**
```http
POST /api/v1/functions/image-processor/triggers
Authorization: Bearer {token}
Content-Type: application/json

{
  "type": "Http",
  "targetVersion": "production",
  "config": {
    "httpPath": "/api/process",
    "httpMethods": ["POST"],
    "corsEnabled": true
  }
}
```

**Request (Scheduled Trigger):**
```http
POST /api/v1/functions/cleanup-task/triggers
Authorization: Bearer {token}
Content-Type: application/json

{
  "type": "Scheduled",
  "config": {
    "scheduleExpression": "cron(0 2 * * ? *)",
    "timezone": "America/New_York"
  }
}
```

**Response 201 Created:**
```json
{
  "triggerId": "trigger-abc123",
  "functionName": "image-processor",
  "type": "Http",
  "isEnabled": true,
  "config": {
    "httpPath": "/api/process",
    "httpMethods": ["POST"],
    "corsEnabled": true
  },
  "createdAt": "2025-11-23T14:00:00Z"
}
```

---

### Enable/Disable Trigger

Enable or disable a trigger.

**Endpoint:** `PUT /api/v1/functions/{name}/triggers/{id}/enable`
**Endpoint:** `PUT /api/v1/functions/{name}/triggers/{id}/disable`
**Authorization:** Developer, Admin
**Rate Limit:** 30 req/min per user

**Request:**
```http
PUT /api/v1/functions/image-processor/triggers/trigger-abc123/disable
Authorization: Bearer {token}
```

**Response 200 OK:**
```json
{
  "triggerId": "trigger-abc123",
  "isEnabled": false,
  "updatedAt": "2025-11-23T14:30:00Z"
}
```

---

### Delete Trigger

Delete a trigger.

**Endpoint:** `DELETE /api/v1/functions/{name}/triggers/{id}`
**Authorization:** Developer, Admin
**Rate Limit:** 10 req/min per user

**Request:**
```http
DELETE /api/v1/functions/image-processor/triggers/trigger-abc123
Authorization: Bearer {token}
```

**Response 204 No Content**

---

## Invocations API

### Invoke Function (Synchronous)

Invoke a function and wait for response.

**Endpoint:** `POST /api/v1/functions/{name}/invoke`
**Authorization:** All roles
**Rate Limit:** 10,000 req/min per function

**Request:**
```http
POST /api/v1/functions/image-processor/invoke
Authorization: Bearer {token}
Content-Type: application/json

{
  "payload": "{\"imageUrl\":\"https://example.com/photo.jpg\",\"size\":\"thumbnail\"}",
  "alias": "production",
  "invocationType": "RequestResponse"
}
```

**Response 200 OK:**
```json
{
  "statusCode": 200,
  "body": "{\"processedUrl\":\"https://example.com/photo-thumb.jpg\"}",
  "executionTime": 145,
  "billedDuration": 200,
  "memoryUsed": 256,
  "wasColdStart": false,
  "requestId": "req-xyz789",
  "logStreamName": "2025-11-23/image-processor-abc123"
}
```

---

### Invoke Function (Asynchronous)

Invoke a function asynchronously (fire-and-forget).

**Endpoint:** `POST /api/v1/functions/{name}/invoke-async`
**Authorization:** All roles
**Rate Limit:** 10,000 req/min per function

**Request:**
```http
POST /api/v1/functions/image-processor/invoke-async
Authorization: Bearer {token}
Content-Type: application/json

{
  "payload": "{\"imageUrl\":\"https://example.com/photo.jpg\"}",
  "alias": "production"
}
```

**Response 202 Accepted:**
```json
{
  "requestId": "req-xyz789",
  "status": "Queued"
}
```

---

### Get Invocation Logs

Get logs for a function invocation.

**Endpoint:** `GET /api/v1/functions/{name}/invocations/{requestId}/logs`
**Authorization:** All roles
**Rate Limit:** 60 req/min per user

**Request:**
```http
GET /api/v1/functions/image-processor/invocations/req-xyz789/logs
Authorization: Bearer {token}
```

**Response 200 OK:**
```json
{
  "requestId": "req-xyz789",
  "logs": [
    {
      "timestamp": "2025-11-23T14:00:00.123Z",
      "level": "INFO",
      "message": "Processing image photo.jpg"
    },
    {
      "timestamp": "2025-11-23T14:00:00.456Z",
      "level": "INFO",
      "message": "Image resized successfully"
    }
  ],
  "functionName": "image-processor",
  "version": "v6"
}
```

---

## Runners API

### List Runner Nodes

List all runner nodes in the cluster.

**Endpoint:** `GET /api/v1/runners`
**Authorization:** Admin, Viewer
**Rate Limit:** 60 req/min per user

**Request:**
```http
GET /api/v1/runners
Authorization: Bearer {token}
```

**Response 200 OK:**
```json
{
  "runners": [
    {
      "nodeId": "runner-1",
      "hostname": "runner1.example.com",
      "port": 8080,
      "health": {
        "isHealthy": true,
        "cpuUsage": 45.2,
        "memoryUsage": 62.8,
        "activeContainers": 12
      },
      "startedAt": "2025-11-23T10:00:00Z",
      "lastHeartbeat": "2025-11-23T14:59:50Z"
    }
  ],
  "total": 3
}
```

---

### Get Runner Details

Get detailed information about a runner node.

**Endpoint:** `GET /api/v1/runners/{nodeId}`
**Authorization:** Admin, Viewer
**Rate Limit:** 60 req/min per user

**Request:**
```http
GET /api/v1/runners/runner-1
Authorization: Bearer {token}
```

**Response 200 OK:**
```json
{
  "nodeId": "runner-1",
  "hostname": "runner1.example.com",
  "port": 8080,
  "health": {
    "isHealthy": true,
    "cpuUsage": 45.2,
    "memoryUsage": 62.8,
    "activeContainers": 12,
    "queuedInvocations": 3
  },
  "resources": {
    "totalCpuCores": 8,
    "totalMemoryMB": 16384,
    "maxContainers": 100
  },
  "metrics": {
    "totalInvocations": 50000,
    "successfulInvocations": 49850,
    "failedInvocations": 150,
    "coldStarts": 320,
    "avgInvocationDuration": 145.5,
    "avgColdStartDuration": 180.2
  },
  "activeContainers": {
    "image-processor:v6": "container-abc123",
    "data-processor:v2": "container-def456"
  },
  "startedAt": "2025-11-23T10:00:00Z",
  "lastHeartbeat": "2025-11-23T14:59:50Z"
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
    "message": "Function validation failed",
    "details": [
      {
        "field": "memorySize",
        "message": "MemorySize must be 128-10240 MB in 64 MB increments"
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
| 400 | `INVALID_CODE_PACKAGE` | Function code ZIP is invalid |
| 401 | `UNAUTHORIZED` | Missing or invalid JWT token |
| 403 | `FORBIDDEN` | Insufficient permissions |
| 404 | `NOT_FOUND` | Resource not found |
| 409 | `CONFLICT` | Resource already exists |
| 413 | `PAYLOAD_TOO_LARGE` | Request payload exceeds 6 MB |
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
X-RateLimit-Limit: 10000
X-RateLimit-Remaining: 9543
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
| `POST /api/v1/functions/{name}/invoke` | 10,000 req | 1 minute |
| `POST /api/v1/deployments` | 10 req | 1 minute |
| `POST /api/v1/functions` | 20 req | 1 minute |
| `GET /api/v1/functions` | 60 req | 1 minute |
| `POST /api/v1/functions/{name}/versions` | 20 req | 1 minute |

---

**API Version:** 1.0.0
**Last Updated:** 2025-11-23
**Support:** api-support@example.com
