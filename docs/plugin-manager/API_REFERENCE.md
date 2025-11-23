# Plugin Manager API Reference

**Version:** 1.0.0
**Base URL:** `https://api.example.com/api/v1`
**Authentication:** JWT Bearer Token
**Content-Type:** `application/json`

---

## Table of Contents

1. [Authentication](#authentication)
2. [Plugins API](#plugins-api)
3. [Plugin Versions API](#plugin-versions-api)
4. [Deployments API](#deployments-api)
5. [Tenant Plugins API](#tenant-plugins-api)
6. [Sandbox API](#sandbox-api)
7. [Health & Monitoring API](#health--monitoring-api)
8. [Error Responses](#error-responses)
9. [Rate Limiting](#rate-limiting)

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
GET /api/v1/plugins
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

---

## Plugins API

### Register Plugin

Register a new plugin in the system.

**Endpoint:** `POST /api/v1/plugins`
**Authorization:** Admin, Developer
**Rate Limit:** 10 req/min per user

**Request:**
```http
POST /api/v1/plugins
Authorization: Bearer {token}
Content-Type: application/json

{
  "name": "payment-processor-stripe",
  "displayName": "Stripe Payment Processor",
  "description": "Integrate Stripe payment gateway",
  "type": "PaymentGateway",
  "author": "Platform Team",
  "documentationUrl": "https://docs.example.com/plugins/stripe",
  "tags": ["payment", "stripe", "gateway"]
}
```

**Request Fields:**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `name` | string | Yes | Unique plugin name (lowercase, alphanumeric with dashes) |
| `displayName` | string | Yes | Human-readable display name |
| `description` | string | No | Plugin description |
| `type` | string | Yes | Plugin type (see PluginType enum) |
| `author` | string | No | Plugin author/developer |
| `documentationUrl` | string | No | Documentation URL |
| `tags` | array | No | Tags for categorization |

**Response 201 Created:**
```json
{
  "pluginId": "plg-7f8a9b1c-2d3e-4f5g-6h7i-8j9k0l1m2n3o",
  "name": "payment-processor-stripe",
  "displayName": "Stripe Payment Processor",
  "type": "PaymentGateway",
  "status": "Draft",
  "createdAt": "2025-11-23T12:00:00Z"
}
```

**Error Responses:**
- `400 Bad Request` - Invalid plugin name or configuration
- `409 Conflict` - Plugin name already exists
- `429 Too Many Requests` - Rate limit exceeded

---

### List Plugins

List all registered plugins.

**Endpoint:** `GET /api/v1/plugins`
**Authorization:** All roles
**Rate Limit:** 60 req/min per user

**Request:**
```http
GET /api/v1/plugins?type=PaymentGateway&status=Active&limit=20&offset=0
Authorization: Bearer {token}
```

**Query Parameters:**

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `type` | string | No | - | Filter by plugin type |
| `status` | string | No | - | Filter by status (Draft, Active, Deprecated) |
| `tags` | string | No | - | Filter by tags (comma-separated) |
| `limit` | int | No | 20 | Max plugins to return (1-100) |
| `offset` | int | No | 0 | Pagination offset |

**Response 200 OK:**
```json
{
  "plugins": [
    {
      "pluginId": "plg-123",
      "name": "payment-processor-stripe",
      "displayName": "Stripe Payment Processor",
      "type": "PaymentGateway",
      "currentVersion": "1.5.0",
      "status": "Active",
      "createdAt": "2025-11-23T12:00:00Z"
    }
  ],
  "total": 42,
  "limit": 20,
  "offset": 0
}
```

---

### Get Plugin Details

Get details about a specific plugin.

**Endpoint:** `GET /api/v1/plugins/{id}`
**Authorization:** All roles
**Rate Limit:** 60 req/min per user

**Request:**
```http
GET /api/v1/plugins/payment-processor-stripe
Authorization: Bearer {token}
```

**Response 200 OK:**
```json
{
  "pluginId": "plg-123",
  "name": "payment-processor-stripe",
  "displayName": "Stripe Payment Processor",
  "description": "Integrate Stripe payment gateway",
  "type": "PaymentGateway",
  "currentVersion": "1.5.0",
  "status": "Active",
  "author": "Platform Team",
  "documentationUrl": "https://docs.example.com/plugins/stripe",
  "tags": ["payment", "stripe", "gateway"],
  "createdAt": "2025-11-23T12:00:00Z",
  "updatedAt": "2025-11-23T15:00:00Z"
}
```

---

### Update Plugin

Update plugin metadata.

**Endpoint:** `PUT /api/v1/plugins/{id}`
**Authorization:** Admin, Developer (own plugins only)
**Rate Limit:** 10 req/min per user

**Request:**
```http
PUT /api/v1/plugins/payment-processor-stripe
Authorization: Bearer {token}
Content-Type: application/json

{
  "displayName": "Stripe Payment Gateway (Updated)",
  "description": "Updated description",
  "documentationUrl": "https://docs.example.com/plugins/stripe-v2",
  "tags": ["payment", "stripe", "gateway", "pci-compliant"]
}
```

**Response 200 OK:**
```json
{
  "pluginId": "plg-123",
  "name": "payment-processor-stripe",
  "displayName": "Stripe Payment Gateway (Updated)",
  "updatedAt": "2025-11-23T16:00:00Z"
}
```

---

### Delete Plugin

Delete a plugin (admin only).

**Endpoint:** `DELETE /api/v1/plugins/{id}`
**Authorization:** Admin only
**Rate Limit:** 10 req/min per user

**Request:**
```http
DELETE /api/v1/plugins/payment-processor-stripe
Authorization: Bearer {token}
```

**Response 204 No Content**

**Error Responses:**
- `409 Conflict` - Plugin has active deployments (undeploy first)

---

### Deprecate Plugin

Mark a plugin as deprecated.

**Endpoint:** `POST /api/v1/plugins/{id}/deprecate`
**Authorization:** Admin only
**Rate Limit:** 10 req/min per user

**Request:**
```http
POST /api/v1/plugins/payment-processor-stripe/deprecate
Authorization: Bearer {token}
Content-Type: application/json

{
  "reason": "Superseded by payment-processor-stripe-v2",
  "migrationGuide": "https://docs.example.com/migration/stripe-v1-to-v2"
}
```

**Response 200 OK:**
```json
{
  "pluginId": "plg-123",
  "status": "Deprecated",
  "deprecatedAt": "2025-11-23T17:00:00Z"
}
```

---

## Plugin Versions API

### Upload Plugin Version

Upload a new plugin version with binary.

**Endpoint:** `POST /api/v1/plugins/{id}/versions`
**Authorization:** Admin, Developer
**Rate Limit:** 5 req/min per user

**Request (multipart/form-data):**
```http
POST /api/v1/plugins/payment-processor-stripe/versions
Authorization: Bearer {token}
Content-Type: multipart/form-data

--boundary
Content-Disposition: form-data; name="version"

1.6.0
--boundary
Content-Disposition: form-data; name="releaseNotes"

- Added support for Apple Pay
- Fixed currency conversion bug
- Improved error handling
--boundary
Content-Disposition: form-data; name="manifest"
Content-Type: application/json

{
  "name": "payment-processor-stripe",
  "version": "1.6.0",
  "entryPoint": "StripePaymentProcessor.dll",
  "targetFramework": "net8.0",
  "dependencies": [
    {
      "name": "Stripe.net",
      "versionConstraint": ">= 43.0.0",
      "type": "NuGetPackage"
    }
  ],
  "requiredPermissions": ["network.http", "secrets.read"],
  "capabilities": ["payment", "refund", "subscription"]
}
--boundary
Content-Disposition: form-data; name="binary"; filename="plugin.zip"
Content-Type: application/zip

[binary data]
--boundary--
```

**Response 201 Created:**
```json
{
  "versionId": "ver-abc123",
  "pluginId": "plg-123",
  "version": "1.6.0",
  "status": "Draft",
  "binaryPath": "plugins/payment-processor-stripe/1.6.0/plugin.zip",
  "checksum": "sha256:7f8a9b1c2d3e4f5g6h7i8j9k0l1m2n3o",
  "binarySize": 5242880,
  "createdAt": "2025-11-23T18:00:00Z"
}
```

**Note:** Large binaries (> 100MB) require presigned URL upload.

---

### List Plugin Versions

List all versions of a plugin.

**Endpoint:** `GET /api/v1/plugins/{id}/versions`
**Authorization:** All roles
**Rate Limit:** 60 req/min per user

**Request:**
```http
GET /api/v1/plugins/payment-processor-stripe/versions?status=Approved
Authorization: Bearer {token}
```

**Response 200 OK:**
```json
{
  "versions": [
    {
      "versionId": "ver-abc123",
      "version": "1.6.0",
      "status": "Approved",
      "isBreakingChange": false,
      "createdAt": "2025-11-23T18:00:00Z",
      "approvedAt": "2025-11-23T19:00:00Z"
    },
    {
      "versionId": "ver-def456",
      "version": "1.5.0",
      "status": "Deprecated",
      "isBreakingChange": false,
      "createdAt": "2025-11-15T10:00:00Z"
    }
  ],
  "total": 12
}
```

---

### Get Version Details

Get detailed information about a specific version.

**Endpoint:** `GET /api/v1/plugins/{id}/versions/{version}`
**Authorization:** All roles
**Rate Limit:** 60 req/min per user

**Request:**
```http
GET /api/v1/plugins/payment-processor-stripe/versions/1.6.0
Authorization: Bearer {token}
```

**Response 200 OK:**
```json
{
  "versionId": "ver-abc123",
  "pluginId": "plg-123",
  "version": "1.6.0",
  "releaseNotes": "- Added support for Apple Pay\n- Fixed currency conversion bug",
  "manifest": {
    "name": "payment-processor-stripe",
    "version": "1.6.0",
    "entryPoint": "StripePaymentProcessor.dll",
    "targetFramework": "net8.0",
    "dependencies": [...]
  },
  "binaryPath": "plugins/payment-processor-stripe/1.6.0/plugin.zip",
  "checksum": "sha256:7f8a9b1c2d3e4f5g6h7i8j9k0l1m2n3o",
  "binarySize": 5242880,
  "status": "Approved",
  "isBreakingChange": false,
  "approvedBy": "admin@example.com",
  "approvedAt": "2025-11-23T19:00:00Z",
  "createdAt": "2025-11-23T18:00:00Z"
}
```

---

### Approve Version

Approve a plugin version for production (admin only).

**Endpoint:** `POST /api/v1/plugins/{id}/versions/{version}/approve`
**Authorization:** Admin only
**Rate Limit:** 10 req/min per user

**Request:**
```http
POST /api/v1/plugins/payment-processor-stripe/versions/1.6.0/approve
Authorization: Bearer {token}
Content-Type: application/json

{
  "notes": "Tested in QA, all checks passed"
}
```

**Response 200 OK:**
```json
{
  "versionId": "ver-abc123",
  "version": "1.6.0",
  "status": "Approved",
  "approvedBy": "admin@example.com",
  "approvedAt": "2025-11-23T19:00:00Z"
}
```

---

### Download Plugin Binary

Download plugin binary file.

**Endpoint:** `GET /api/v1/plugins/{id}/versions/{version}/download`
**Authorization:** Admin, Developer
**Rate Limit:** 20 req/min per user

**Request:**
```http
GET /api/v1/plugins/payment-processor-stripe/versions/1.6.0/download
Authorization: Bearer {token}
```

**Response 200 OK:**
```http
Content-Type: application/zip
Content-Disposition: attachment; filename="payment-processor-stripe-1.6.0.zip"
Content-Length: 5242880

[binary data]
```

---

## Deployments API

### Create Deployment

Deploy a plugin to an environment.

**Endpoint:** `POST /api/v1/deployments`
**Authorization:** Admin, Developer (dev/qa only)
**Rate Limit:** 10 req/min per user

**Request:**
```http
POST /api/v1/deployments
Authorization: Bearer {token}
Content-Type: application/json

{
  "pluginId": "payment-processor-stripe",
  "version": "1.6.0",
  "environment": "Production",
  "strategy": "Canary",
  "config": {
    "initialPercentage": "10",
    "incrementPercentage": "20",
    "evaluationPeriod": "PT10M",
    "autoRollback": "true"
  }
}
```

**Request Fields:**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `pluginId` | string | Yes | Plugin identifier |
| `version` | string | Yes | Version to deploy |
| `environment` | string | Yes | Target environment (Development, QA, Staging, Production) |
| `strategy` | string | Yes | Deployment strategy (Direct, Canary, BlueGreen, Rolling, ABTesting) |
| `config` | object | No | Strategy-specific configuration |

**Response 202 Accepted:**
```json
{
  "deploymentId": "dep-xyz789",
  "pluginId": "payment-processor-stripe",
  "version": "1.6.0",
  "environment": "Production",
  "strategy": "Canary",
  "status": "Pending",
  "progressPercentage": 0,
  "initiatedBy": "developer@example.com",
  "createdAt": "2025-11-23T20:00:00Z"
}
```

**Note:** Production deployments require approval before execution.

---

### Get Deployment Status

Get deployment status and progress.

**Endpoint:** `GET /api/v1/deployments/{id}`
**Authorization:** All roles
**Rate Limit:** 60 req/min per user

**Request:**
```http
GET /api/v1/deployments/dep-xyz789
Authorization: Bearer {token}
```

**Response 200 OK:**
```json
{
  "deploymentId": "dep-xyz789",
  "pluginId": "payment-processor-stripe",
  "version": "1.6.0",
  "environment": "Production",
  "strategy": "Canary",
  "status": "InProgress",
  "progressPercentage": 30,
  "initiatedBy": "developer@example.com",
  "startedAt": "2025-11-23T20:05:00Z",
  "affectedTenants": 150,
  "healthChecks": [
    {
      "type": "Startup",
      "status": "Healthy",
      "checkedAt": "2025-11-23T20:06:00Z"
    },
    {
      "type": "Liveness",
      "status": "Healthy",
      "checkedAt": "2025-11-23T20:10:00Z"
    }
  ],
  "createdAt": "2025-11-23T20:00:00Z"
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
GET /api/v1/deployments?environment=Production&status=InProgress&limit=20
Authorization: Bearer {token}
```

**Query Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `pluginId` | string | No | Filter by plugin |
| `environment` | string | No | Filter by environment |
| `status` | string | No | Filter by status |
| `limit` | int | No | Max results (default: 20) |
| `offset` | int | No | Pagination offset |

**Response 200 OK:**
```json
{
  "deployments": [
    {
      "deploymentId": "dep-xyz789",
      "pluginId": "payment-processor-stripe",
      "version": "1.6.0",
      "environment": "Production",
      "strategy": "Canary",
      "status": "InProgress",
      "progressPercentage": 30,
      "createdAt": "2025-11-23T20:00:00Z"
    }
  ],
  "total": 5,
  "limit": 20,
  "offset": 0
}
```

---

### Rollback Deployment

Rollback a deployment to previous version.

**Endpoint:** `POST /api/v1/deployments/{id}/rollback`
**Authorization:** Admin, Developer
**Rate Limit:** 10 req/min per user

**Request:**
```http
POST /api/v1/deployments/dep-xyz789/rollback
Authorization: Bearer {token}
Content-Type: application/json

{
  "reason": "High error rate detected"
}
```

**Response 200 OK:**
```json
{
  "deploymentId": "dep-xyz789",
  "status": "RolledBack",
  "previousVersion": "1.5.0",
  "rolledBackAt": "2025-11-23T20:30:00Z",
  "rollbackReason": "High error rate detected"
}
```

---

### Pause Deployment

Pause an in-progress deployment.

**Endpoint:** `POST /api/v1/deployments/{id}/pause`
**Authorization:** Admin, Developer
**Rate Limit:** 10 req/min per user

**Request:**
```http
POST /api/v1/deployments/dep-xyz789/pause
Authorization: Bearer {token}
```

**Response 200 OK:**
```json
{
  "deploymentId": "dep-xyz789",
  "status": "Paused",
  "progressPercentage": 30,
  "pausedAt": "2025-11-23T20:15:00Z"
}
```

---

### Resume Deployment

Resume a paused deployment.

**Endpoint:** `POST /api/v1/deployments/{id}/resume`
**Authorization:** Admin, Developer
**Rate Limit:** 10 req/min per user

**Request:**
```http
POST /api/v1/deployments/dep-xyz789/resume
Authorization: Bearer {token}
```

**Response 200 OK:**
```json
{
  "deploymentId": "dep-xyz789",
  "status": "InProgress",
  "progressPercentage": 30,
  "resumedAt": "2025-11-23T20:20:00Z"
}
```

---

## Tenant Plugins API

### List Tenant Plugins

List all plugins enabled for a tenant.

**Endpoint:** `GET /api/v1/tenants/{tenantId}/plugins`
**Authorization:** Admin, Tenant Owner
**Rate Limit:** 60 req/min per user

**Request:**
```http
GET /api/v1/tenants/tenant-123/plugins
Authorization: Bearer {token}
```

**Response 200 OK:**
```json
{
  "tenantId": "tenant-123",
  "plugins": [
    {
      "pluginId": "payment-processor-stripe",
      "enabled": true,
      "version": "1.6.0",
      "configuredAt": "2025-11-23T12:00:00Z"
    }
  ],
  "total": 5
}
```

---

### Enable Plugin for Tenant

Enable a plugin for a specific tenant.

**Endpoint:** `POST /api/v1/tenants/{tenantId}/plugins/{pluginId}/enable`
**Authorization:** Admin, Tenant Owner
**Rate Limit:** 10 req/min per user

**Request:**
```http
POST /api/v1/tenants/tenant-123/plugins/payment-processor-stripe/enable
Authorization: Bearer {token}
Content-Type: application/json

{
  "pinnedVersion": "1.6.0",
  "configuration": {
    "apiKey": "sk_test_...",
    "webhookSecret": "whsec_...",
    "accountId": "acct_..."
  },
  "quotas": {
    "maxRequestsPerMinute": 100,
    "maxConcurrentExecutions": 10,
    "maxExecutionTimeSeconds": 30,
    "maxMemoryMB": 512
  }
}
```

**Response 201 Created:**
```json
{
  "configId": "cfg-abc123",
  "tenantId": "tenant-123",
  "pluginId": "payment-processor-stripe",
  "enabled": true,
  "pinnedVersion": "1.6.0",
  "enabledBy": "admin@example.com",
  "enabledAt": "2025-11-23T21:00:00Z"
}
```

---

### Disable Plugin for Tenant

Disable a plugin for a tenant.

**Endpoint:** `POST /api/v1/tenants/{tenantId}/plugins/{pluginId}/disable`
**Authorization:** Admin, Tenant Owner
**Rate Limit:** 10 req/min per user

**Request:**
```http
POST /api/v1/tenants/tenant-123/plugins/payment-processor-stripe/disable
Authorization: Bearer {token}
```

**Response 200 OK:**
```json
{
  "tenantId": "tenant-123",
  "pluginId": "payment-processor-stripe",
  "enabled": false,
  "disabledAt": "2025-11-23T21:30:00Z"
}
```

---

### Update Tenant Plugin Configuration

Update plugin configuration for a tenant.

**Endpoint:** `PUT /api/v1/tenants/{tenantId}/plugins/{pluginId}/config`
**Authorization:** Admin, Tenant Owner
**Rate Limit:** 10 req/min per user

**Request:**
```http
PUT /api/v1/tenants/tenant-123/plugins/payment-processor-stripe/config
Authorization: Bearer {token}
Content-Type: application/json

{
  "configuration": {
    "apiKey": "sk_live_...",
    "webhookSecret": "whsec_new...",
    "accountId": "acct_..."
  },
  "quotas": {
    "maxRequestsPerMinute": 200
  }
}
```

**Response 200 OK:**
```json
{
  "configId": "cfg-abc123",
  "tenantId": "tenant-123",
  "pluginId": "payment-processor-stripe",
  "updatedAt": "2025-11-23T22:00:00Z"
}
```

---

## Sandbox API

### Create Sandbox

Create an isolated sandbox environment for testing.

**Endpoint:** `POST /api/v1/sandbox/create`
**Authorization:** Admin, Developer
**Rate Limit:** 5 req/min per user

**Request:**
```http
POST /api/v1/sandbox/create
Authorization: Bearer {token}
Content-Type: application/json

{
  "pluginId": "payment-processor-stripe",
  "version": "1.6.0",
  "timeout": "PT5M"
}
```

**Response 201 Created:**
```json
{
  "sandboxId": "sbx-xyz123",
  "pluginId": "payment-processor-stripe",
  "version": "1.6.0",
  "status": "Created",
  "expiresAt": "2025-11-23T22:35:00Z",
  "createdAt": "2025-11-23T22:30:00Z"
}
```

---

### Execute Plugin in Sandbox

Execute plugin method in sandbox.

**Endpoint:** `POST /api/v1/sandbox/{sandboxId}/execute`
**Authorization:** Admin, Developer
**Rate Limit:** 20 req/min per user

**Request:**
```http
POST /api/v1/sandbox/sbx-xyz123/execute
Authorization: Bearer {token}
Content-Type: application/json

{
  "method": "ProcessPayment",
  "parameters": {
    "amount": 1000,
    "currency": "USD",
    "cardToken": "tok_test_..."
  }
}
```

**Response 200 OK:**
```json
{
  "sandboxId": "sbx-xyz123",
  "executionId": "exec-abc456",
  "result": {
    "success": true,
    "transactionId": "txn_123",
    "status": "succeeded"
  },
  "logs": [
    "Processing payment for $10.00",
    "Stripe API called successfully",
    "Transaction completed"
  ],
  "metrics": {
    "executionTimeMs": 250,
    "memoryUsedMB": 12
  },
  "executedAt": "2025-11-23T22:31:00Z"
}
```

---

### Cleanup Sandbox

Clean up and delete a sandbox.

**Endpoint:** `DELETE /api/v1/sandbox/{sandboxId}`
**Authorization:** Admin, Developer
**Rate Limit:** 10 req/min per user

**Request:**
```http
DELETE /api/v1/sandbox/sbx-xyz123
Authorization: Bearer {token}
```

**Response 204 No Content**

---

## Health & Monitoring API

### Get Plugin Health

Get health status of a deployed plugin.

**Endpoint:** `GET /api/v1/plugins/{id}/health`
**Authorization:** All roles
**Rate Limit:** 60 req/min per user

**Request:**
```http
GET /api/v1/plugins/payment-processor-stripe/health?environment=Production
Authorization: Bearer {token}
```

**Response 200 OK:**
```json
{
  "pluginId": "payment-processor-stripe",
  "version": "1.6.0",
  "environment": "Production",
  "overallStatus": "Healthy",
  "checks": [
    {
      "type": "Liveness",
      "status": "Healthy",
      "checkedAt": "2025-11-23T23:00:00Z",
      "durationMs": 15
    },
    {
      "type": "Readiness",
      "status": "Healthy",
      "checkedAt": "2025-11-23T23:00:00Z",
      "durationMs": 25
    }
  ],
  "metrics": {
    "avgLatencyMs": 45.2,
    "errorRate": 0.01,
    "requestsPerSecond": 150
  }
}
```

---

### Get Plugin Metrics

Get performance metrics for a plugin.

**Endpoint:** `GET /api/v1/plugins/{id}/metrics`
**Authorization:** All roles
**Rate Limit:** 60 req/min per user

**Request:**
```http
GET /api/v1/plugins/payment-processor-stripe/metrics?environment=Production&window=PT1H
Authorization: Bearer {token}
```

**Response 200 OK:**
```json
{
  "pluginId": "payment-processor-stripe",
  "environment": "Production",
  "window": "PT1H",
  "metrics": {
    "invocations": 54000,
    "avgLatencyMs": 45.2,
    "p50LatencyMs": 38,
    "p95LatencyMs": 85,
    "p99LatencyMs": 150,
    "errorRate": 0.01,
    "requestsPerSecond": 150,
    "memoryUsageMB": 128,
    "cpuUsagePercent": 12.5
  },
  "timestamp": "2025-11-23T23:00:00Z"
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
    "message": "Plugin validation failed",
    "details": [
      {
        "field": "name",
        "message": "Name must contain only lowercase alphanumeric characters and dashes"
      }
    ],
    "traceId": "trace-xyz789",
    "timestamp": "2025-11-23T23:00:00Z"
  }
}
```

### Common Error Codes

| HTTP Status | Error Code | Description |
|-------------|------------|-------------|
| 400 | `VALIDATION_ERROR` | Request validation failed |
| 401 | `UNAUTHORIZED` | Missing or invalid JWT token |
| 403 | `FORBIDDEN` | Insufficient permissions |
| 404 | `NOT_FOUND` | Resource not found |
| 409 | `CONFLICT` | Resource already exists |
| 413 | `PAYLOAD_TOO_LARGE` | Binary exceeds size limit |
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
| `POST /api/v1/plugins` | 10 req | 1 minute |
| `GET /api/v1/plugins` | 60 req | 1 minute |
| `POST /api/v1/deployments` | 10 req | 1 minute |
| `GET /api/v1/deployments` | 60 req | 1 minute |
| `POST /api/v1/plugins/{id}/versions` | 5 req | 1 minute |
| `POST /api/v1/sandbox/create` | 5 req | 1 minute |
| `POST /api/v1/sandbox/{id}/execute` | 20 req | 1 minute |

---

## Pagination

List endpoints support pagination via limit/offset parameters.

### Pagination Parameters

```http
GET /api/v1/plugins?limit=20&offset=40
```

**Parameters:**
- `limit` - Max items to return (1-100, default: 20)
- `offset` - Number of items to skip (default: 0)

### Pagination Response

```json
{
  "plugins": [...],
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
