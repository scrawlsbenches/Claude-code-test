# WASM Orchestrator API Reference

**Version:** 1.0.0
**Base URL:** `https://api.example.com/api/v1`
**Authentication:** JWT Bearer Token
**Content-Type:** `application/json`

---

## Table of Contents

1. [Authentication](#authentication)
2. [Modules API](#modules-api)
3. [Deployments API](#deployments-api)
4. [Edge Nodes API](#edge-nodes-api)
5. [Interfaces API](#interfaces-api)
6. [Execution API](#execution-api)
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
GET /api/v1/wasm/modules
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

---

## Modules API

### Register Module

Register a new WASM module with binary upload.

**Endpoint:** `POST /api/v1/wasm/modules`
**Authorization:** Developer, Admin
**Rate Limit:** 10 req/min per user

**Request:**
```http
POST /api/v1/wasm/modules
Authorization: Bearer {token}
Content-Type: application/json

{
  "name": "image-processor",
  "version": "1.2.0",
  "description": "WASM module for image processing at edge",
  "wasmBinary": "base64-encoded-wasm-binary...",
  "wasiVersion": "preview2",
  "requiredInterfaces": [
    "wasi:filesystem/types@0.2.0",
    "wasi:http/outgoing-handler@0.2.0"
  ],
  "limits": {
    "maxMemoryMB": 128,
    "maxCpuPercent": 50,
    "maxExecutionTimeSeconds": 30
  },
  "metadata": {
    "gitCommit": "abc123def456",
    "buildTimestamp": "2025-11-23T12:00:00Z"
  }
}
```

**Request Fields:**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `name` | string | Yes | Module name (alphanumeric, dashes, underscores) |
| `version` | string | Yes | Semantic version (e.g., "1.2.0") |
| `description` | string | No | Human-readable description |
| `wasmBinary` | string | Yes | Base64-encoded WASM binary (max 50 MB) |
| `wasiVersion` | string | Yes | "preview1" or "preview2" |
| `requiredInterfaces` | array | No | WASI interfaces required |
| `limits` | object | No | Resource limits (defaults applied) |
| `metadata` | object | No | Custom metadata |

**Response 201 Created:**
```json
{
  "moduleId": "image-processor-v1.2.0",
  "name": "image-processor",
  "version": "1.2.0",
  "binaryPath": "s3://wasm-modules/image-processor/1.2.0/module.wasm",
  "checksum": "a1b2c3d4e5f6789...",
  "sizeBytes": 5242880,
  "exportedFunctions": ["process_image", "resize", "convert_format", "health_check"],
  "registeredAt": "2025-11-23T12:00:00Z",
  "status": "Active"
}
```

**Error Responses:**
- `400 Bad Request` - Invalid WASM binary or validation failed
- `409 Conflict` - Module version already exists
- `413 Payload Too Large` - Binary exceeds 50 MB
- `429 Too Many Requests` - Rate limit exceeded

---

### List Modules

List all registered WASM modules with filtering.

**Endpoint:** `GET /api/v1/wasm/modules`
**Authorization:** All roles
**Rate Limit:** 100 req/min per user

**Request:**
```http
GET /api/v1/wasm/modules?name=image-processor&status=Active&limit=20&offset=0
Authorization: Bearer {token}
```

**Query Parameters:**

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `name` | string | No | - | Filter by module name |
| `status` | string | No | - | Filter by status (Active, Deprecated, Disabled) |
| `limit` | int | No | 20 | Max modules to return (1-100) |
| `offset` | int | No | 0 | Pagination offset |

**Response 200 OK:**
```json
{
  "modules": [
    {
      "moduleId": "image-processor-v1.2.0",
      "name": "image-processor",
      "version": "1.2.0",
      "sizeBytes": 5242880,
      "status": "Active",
      "deploymentCount": 5,
      "invocationCount": 125000,
      "registeredAt": "2025-11-23T12:00:00Z"
    }
  ],
  "total": 1,
  "limit": 20,
  "offset": 0
}
```

---

### Get Module Details

Get detailed information about a specific module.

**Endpoint:** `GET /api/v1/wasm/modules/{moduleId}`
**Authorization:** All roles
**Rate Limit:** 300 req/min per user

**Request:**
```http
GET /api/v1/wasm/modules/image-processor-v1.2.0
Authorization: Bearer {token}
```

**Response 200 OK:**
```json
{
  "moduleId": "image-processor-v1.2.0",
  "name": "image-processor",
  "version": "1.2.0",
  "description": "WASM module for image processing at edge",
  "binaryPath": "s3://wasm-modules/image-processor/1.2.0/module.wasm",
  "checksum": "a1b2c3d4e5f6789...",
  "sizeBytes": 5242880,
  "wasiVersion": "preview2",
  "requiredInterfaces": [
    "wasi:filesystem/types@0.2.0",
    "wasi:http/outgoing-handler@0.2.0"
  ],
  "exportedFunctions": ["process_image", "resize", "convert_format", "health_check"],
  "limits": {
    "maxMemoryMB": 128,
    "maxCpuPercent": 50,
    "maxExecutionTimeSeconds": 30
  },
  "status": "Active",
  "deploymentCount": 5,
  "invocationCount": 125000,
  "registeredAt": "2025-11-23T12:00:00Z",
  "registeredBy": "developer@example.com"
}
```

---

### Delete Module

Delete a module (admin only). Fails if module has active deployments.

**Endpoint:** `DELETE /api/v1/wasm/modules/{moduleId}`
**Authorization:** Admin only
**Rate Limit:** 10 req/min per user

**Request:**
```http
DELETE /api/v1/wasm/modules/image-processor-v1.2.0
Authorization: Bearer {token}
```

**Response 204 No Content**

**Error Responses:**
- `409 Conflict` - Module has active deployments
- `404 Not Found` - Module does not exist

---

## Deployments API

### Create Deployment

Create a new module deployment configuration.

**Endpoint:** `POST /api/v1/wasm/deployments`
**Authorization:** Developer, Admin
**Rate Limit:** 20 req/min per user

**Request:**
```http
POST /api/v1/wasm/deployments
Authorization: Bearer {token}
Content-Type: application/json

{
  "moduleId": "image-processor-v1.2.0",
  "targetRegions": ["us-east", "us-west"],
  "strategy": "Canary",
  "strategyConfig": {
    "stages": [10, 25, 50, 100],
    "evaluationPeriod": "PT5M",
    "healthCheckFunction": "health_check",
    "autoPromote": true
  },
  "healthCheck": {
    "enabled": true,
    "functionName": "health_check",
    "expectedResult": "OK",
    "timeoutSeconds": 5
  },
  "autoRollbackEnabled": true
}
```

**Request Fields:**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `moduleId` | string | Yes | Module to deploy |
| `targetRegions` | array | Yes* | Target regions (* or targetNodes) |
| `targetNodes` | array | No | Specific nodes (overrides regions) |
| `strategy` | string | Yes | Deployment strategy (Canary, BlueGreen, Rolling, Regional, ABTesting) |
| `strategyConfig` | object | Yes | Strategy-specific configuration |
| `healthCheck` | object | No | Health check configuration |
| `autoRollbackEnabled` | boolean | No | Enable automatic rollback (default: true) |

**Response 201 Created:**
```json
{
  "deploymentId": "deploy-7f8a9b1c-2d3e-4f5g",
  "moduleId": "image-processor-v1.2.0",
  "status": "Pending",
  "strategy": "Canary",
  "targetRegions": ["us-east", "us-west"],
  "progressPercent": 0,
  "createdAt": "2025-11-23T12:00:00Z",
  "createdBy": "developer@example.com"
}
```

---

### Execute Deployment

Execute a pending deployment. Requires admin approval in production.

**Endpoint:** `POST /api/v1/wasm/deployments/{deploymentId}/execute`
**Authorization:** Admin (production), Developer (dev/staging)
**Rate Limit:** 20 req/min per user

**Request:**
```http
POST /api/v1/wasm/deployments/deploy-7f8a9b1c-2d3e-4f5g/execute
Authorization: Bearer {token}
Content-Type: application/json

{
  "approvedBy": "admin@example.com"
}
```

**Response 202 Accepted:**
```json
{
  "deploymentId": "deploy-7f8a9b1c-2d3e-4f5g",
  "status": "InProgress",
  "startedAt": "2025-11-23T12:05:00Z",
  "estimatedCompletionTime": "2025-11-23T12:20:00Z"
}
```

---

### Get Deployment Status

Get real-time deployment status and progress.

**Endpoint:** `GET /api/v1/wasm/deployments/{deploymentId}`
**Authorization:** All roles
**Rate Limit:** 300 req/min per user

**Request:**
```http
GET /api/v1/wasm/deployments/deploy-7f8a9b1c-2d3e-4f5g
Authorization: Bearer {token}
```

**Response 200 OK:**
```json
{
  "deploymentId": "deploy-7f8a9b1c-2d3e-4f5g",
  "moduleId": "image-processor-v1.2.0",
  "status": "InProgress",
  "strategy": "Canary",
  "progressPercent": 25,
  "succeededNodes": ["edge-us-east-01", "edge-us-east-02"],
  "failedNodes": [],
  "currentStage": "25% rollout",
  "createdAt": "2025-11-23T12:00:00Z",
  "startedAt": "2025-11-23T12:05:00Z",
  "estimatedCompletionTime": "2025-11-23T12:20:00Z"
}
```

---

### Rollback Deployment

Rollback a deployment to previous module version.

**Endpoint:** `POST /api/v1/wasm/deployments/{deploymentId}/rollback`
**Authorization:** Operator, Admin
**Rate Limit:** 20 req/min per user

**Request:**
```http
POST /api/v1/wasm/deployments/deploy-7f8a9b1c-2d3e-4f5g/rollback
Authorization: Bearer {token}
Content-Type: application/json

{
  "reason": "High error rate detected"
}
```

**Response 200 OK:**
```json
{
  "deploymentId": "deploy-7f8a9b1c-2d3e-4f5g",
  "status": "RolledBack",
  "previousModuleId": "image-processor-v1.1.0",
  "rollbackCompletedAt": "2025-11-23T12:15:30Z"
}
```

---

## Edge Nodes API

### Register Edge Node

Register a new edge computing node.

**Endpoint:** `POST /api/v1/wasm/nodes`
**Authorization:** Admin
**Rate Limit:** 10 req/min per user

**Request:**
```http
POST /api/v1/wasm/nodes
Authorization: Bearer {token}
Content-Type: application/json

{
  "nodeId": "edge-us-east-01",
  "hostname": "edge01.us-east.example.com",
  "port": 8080,
  "region": "us-east",
  "zone": "us-east-1a",
  "runtime": "Wasmtime",
  "runtimeVersion": "15.0.0",
  "wasiVersion": "preview2",
  "hardware": {
    "cpuCores": 8,
    "totalMemoryMB": 16384,
    "totalDiskGB": 200,
    "architecture": "x86_64"
  },
  "maxModules": 1000
}
```

**Response 201 Created:**
```json
{
  "nodeId": "edge-us-east-01",
  "hostname": "edge01.us-east.example.com",
  "region": "us-east",
  "status": "Healthy",
  "registeredAt": "2025-11-23T12:00:00Z"
}
```

---

### Get Node Health

Get health status of an edge node.

**Endpoint:** `GET /api/v1/wasm/nodes/{nodeId}/health`
**Authorization:** All roles
**Rate Limit:** 300 req/min per user

**Request:**
```http
GET /api/v1/wasm/nodes/edge-us-east-01/health
Authorization: Bearer {token}
```

**Response 200 OK:**
```json
{
  "nodeId": "edge-us-east-01",
  "status": "Healthy",
  "modulesLoaded": 42,
  "cpuUsagePercent": 45.2,
  "memoryUsageMB": 8192,
  "diskUsageGB": 50,
  "lastHeartbeat": "2025-11-23T12:00:00Z",
  "uptime": "PT72H"
}
```

---

## Interfaces API

### Register Interface

Register a new WASI interface definition.

**Endpoint:** `POST /api/v1/wasm/interfaces`
**Authorization:** Developer, Admin
**Rate Limit:** 10 req/min per user

**Request:**
```http
POST /api/v1/wasm/interfaces
Authorization: Bearer {token}
Content-Type: application/json

{
  "namespace": "wasi:custom",
  "name": "image-processing",
  "version": "0.1.0",
  "witDefinition": "interface image-processing { ... }",
  "description": "Custom image processing interface",
  "compatibility": "Backward"
}
```

**Response 201 Created:**
```json
{
  "interfaceId": "wasi:custom/image-processing@0.1.0",
  "status": "Draft",
  "registeredAt": "2025-11-23T12:00:00Z"
}
```

---

## Execution API

### Execute Function

Execute a WASM module function on edge nodes.

**Endpoint:** `POST /api/v1/wasm/execute`
**Authorization:** All roles
**Rate Limit:** 1000 req/min per user

**Request:**
```http
POST /api/v1/wasm/execute
Authorization: Bearer {token}
Content-Type: application/json

{
  "moduleId": "image-processor-v1.2.0",
  "functionName": "process_image",
  "parameters": {
    "imageUrl": "https://example.com/image.jpg",
    "operation": "resize",
    "width": 800,
    "height": 600
  },
  "targetRegion": "us-east",
  "timeout": 10
}
```

**Response 200 OK:**
```json
{
  "success": true,
  "returnValue": "{\"processedImageUrl\":\"https://cdn.example.com/processed.jpg\"}",
  "durationMs": 234.5,
  "memoryUsedBytes": 52428800,
  "executedOn": "edge-us-east-01",
  "timestamp": "2025-11-23T12:00:00Z"
}
```

---

## Error Responses

### Standard Error Format

```json
{
  "error": {
    "code": "VALIDATION_ERROR",
    "message": "Module binary validation failed",
    "details": [
      "Invalid WASM magic number",
      "Binary size exceeds 50 MB limit"
    ],
    "timestamp": "2025-11-23T12:00:00Z",
    "requestId": "req-abc123"
  }
}
```

### HTTP Status Codes

| Code | Meaning | Description |
|------|---------|-------------|
| 200 | OK | Request successful |
| 201 | Created | Resource created |
| 202 | Accepted | Request accepted, processing async |
| 204 | No Content | Request successful, no response body |
| 400 | Bad Request | Invalid request parameters |
| 401 | Unauthorized | Missing or invalid authentication |
| 403 | Forbidden | Insufficient permissions |
| 404 | Not Found | Resource not found |
| 409 | Conflict | Resource conflict |
| 413 | Payload Too Large | Request body too large |
| 429 | Too Many Requests | Rate limit exceeded |
| 500 | Internal Server Error | Server error |
| 503 | Service Unavailable | Service temporarily unavailable |

---

## Rate Limiting

**Rate Limit Headers:**
```http
X-RateLimit-Limit: 100
X-RateLimit-Remaining: 75
X-RateLimit-Reset: 1700738400
```

**Rate Limits by Endpoint:**

| Endpoint | Limit | Window |
|----------|-------|--------|
| POST /wasm/modules | 10 req/min | per user |
| POST /wasm/deployments | 20 req/min | per user |
| POST /wasm/execute | 1000 req/min | per user |
| GET /wasm/* | 300 req/min | per user |

**Rate Limit Exceeded Response:**
```json
{
  "error": {
    "code": "RATE_LIMIT_EXCEEDED",
    "message": "Rate limit exceeded. Try again in 45 seconds.",
    "retryAfter": 45
  }
}
```

---

**Last Updated:** 2025-11-23
**API Version:** 1.0.0
