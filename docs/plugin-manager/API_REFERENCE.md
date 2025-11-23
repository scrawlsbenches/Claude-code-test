# Plugin Manager API Reference

**Version:** 1.0.0
**Base URL:** `https://api.example.com/api/v1`
**Authentication:** JWT Bearer Token
**Content-Type:** `application/json`

---

## Table of Contents

1. [Authentication](#authentication)
2. [Plugins API](#plugins-api)
3. [Plugin Deployments API](#plugin-deployments-api)
4. [Tenants API](#tenants-api)
5. [Plugin Health API](#plugin-health-api)
6. [Capabilities API](#capabilities-api)
7. [Marketplace API](#marketplace-api)
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
    "role": "PlatformAdmin"
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

Register a new plugin with metadata.

**Endpoint:** `POST /api/v1/plugins`
**Authorization:** PluginDeveloper, PlatformAdmin
**Rate Limit:** 10 req/min per user

**Request:**
```http
POST /api/v1/plugins
Authorization: Bearer {token}
Content-Type: application/json

{
  "pluginId": "payment-stripe",
  "name": "Stripe Payment Processor",
  "description": "Process payments via Stripe API",
  "category": "PaymentGateway",
  "publisher": "Acme Corp",
  "iconUrl": "https://example.com/icons/stripe.png",
  "documentationUrl": "https://docs.example.com/plugins/stripe",
  "repositoryUrl": "https://github.com/acme/stripe-plugin",
  "tags": ["payment", "stripe", "credit-card"],
  "version": {
    "semanticVersion": "2.0.0",
    "state": "Beta",
    "runtime": {
      "type": "DotNet8",
      "version": "8.0"
    },
    "resources": {
      "minCpu": 0.2,
      "maxCpu": 1.0,
      "minMemoryMB": 256,
      "maxMemoryMB": 1024
    },
    "binaryUrl": "s3://plugins/payment-stripe-2.0.0.dll",
    "checksum": "sha256:abc123...",
    "configurationSchema": "{\"type\":\"object\",\"properties\":{\"apiKey\":{\"type\":\"string\"}}}",
    "dependencies": [
      {
        "dependencyId": "core-payments",
        "type": "Plugin",
        "versionConstraint": ">=1.0.0,<2.0.0",
        "required": true
      }
    ],
    "releaseNotes": "Added support for recurring payments"
  },
  "capabilities": [
    {
      "capabilityId": "IPaymentProcessor",
      "name": "Payment Processor",
      "version": "1.0",
      "methods": [
        {
          "name": "ProcessPayment",
          "returnType": "PaymentResult",
          "parameters": [
            { "name": "amount", "type": "decimal", "required": true },
            { "name": "currency", "type": "string", "required": true }
          ]
        }
      ]
    }
  ]
}
```

**Response 201 Created:**
```json
{
  "pluginId": "payment-stripe",
  "name": "Stripe Payment Processor",
  "status": "Registered",
  "version": "2.0.0",
  "createdAt": "2025-11-23T12:00:00Z"
}
```

---

### List Plugins

List all registered plugins.

**Endpoint:** `GET /api/v1/plugins`
**Authorization:** All roles
**Rate Limit:** 60 req/min per user

**Request:**
```http
GET /api/v1/plugins?category=PaymentGateway&status=Active&limit=20&offset=0
Authorization: Bearer {token}
```

**Query Parameters:**

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `category` | string | No | - | Filter by category |
| `status` | string | No | - | Filter by status |
| `search` | string | No | - | Search by name/description |
| `tags` | string | No | - | Filter by tags (comma-separated) |
| `limit` | int | No | 20 | Max plugins to return |
| `offset` | int | No | 0 | Pagination offset |

**Response 200 OK:**
```json
{
  "plugins": [
    {
      "pluginId": "payment-stripe",
      "name": "Stripe Payment Processor",
      "category": "PaymentGateway",
      "publisher": "Acme Corp",
      "status": "Active",
      "latestVersion": "2.0.0",
      "installationCount": 245,
      "averageRating": 4.8,
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

Get detailed information about a specific plugin.

**Endpoint:** `GET /api/v1/plugins/{pluginId}`
**Authorization:** All roles
**Rate Limit:** 60 req/min per user

**Request:**
```http
GET /api/v1/plugins/payment-stripe
Authorization: Bearer {token}
```

**Response 200 OK:**
```json
{
  "pluginId": "payment-stripe",
  "name": "Stripe Payment Processor",
  "description": "Process payments via Stripe API",
  "category": "PaymentGateway",
  "publisher": "Acme Corp",
  "iconUrl": "https://example.com/icons/stripe.png",
  "documentationUrl": "https://docs.example.com/plugins/stripe",
  "tags": ["payment", "stripe", "credit-card"],
  "status": "Active",
  "versions": [
    {
      "semanticVersion": "2.0.0",
      "state": "Stable",
      "createdAt": "2025-11-23T12:00:00Z"
    },
    {
      "semanticVersion": "1.5.0",
      "state": "Deprecated",
      "createdAt": "2025-10-15T10:00:00Z"
    }
  ],
  "capabilities": [
    {
      "capabilityId": "IPaymentProcessor",
      "name": "Payment Processor"
    }
  ],
  "installationCount": 245,
  "averageRating": 4.8,
  "createdAt": "2025-11-23T12:00:00Z",
  "updatedAt": "2025-11-23T12:00:00Z"
}
```

---

### Get Plugin Version

Get details about a specific plugin version.

**Endpoint:** `GET /api/v1/plugins/{pluginId}/versions/{version}`
**Authorization:** All roles
**Rate Limit:** 60 req/min per user

**Request:**
```http
GET /api/v1/plugins/payment-stripe/versions/2.0.0
Authorization: Bearer {token}
```

**Response 200 OK:**
```json
{
  "versionId": "ver-abc123",
  "pluginId": "payment-stripe",
  "semanticVersion": "2.0.0",
  "state": "Stable",
  "runtime": {
    "type": "DotNet8",
    "version": "8.0"
  },
  "resources": {
    "minCpu": 0.2,
    "maxCpu": 1.0,
    "minMemoryMB": 256,
    "maxMemoryMB": 1024
  },
  "binaryUrl": "s3://plugins/payment-stripe-2.0.0.dll",
  "checksum": "sha256:abc123...",
  "dependencies": [
    {
      "dependencyId": "core-payments",
      "type": "Plugin",
      "versionConstraint": ">=1.0.0,<2.0.0"
    }
  ],
  "releaseNotes": "Added support for recurring payments",
  "createdAt": "2025-11-23T12:00:00Z"
}
```

---

### Delete Plugin

Delete a plugin (admin only).

**Endpoint:** `DELETE /api/v1/plugins/{pluginId}`
**Authorization:** PlatformAdmin only
**Rate Limit:** 10 req/min per user

**Request:**
```http
DELETE /api/v1/plugins/payment-stripe
Authorization: Bearer {token}
```

**Response 204 No Content**

**Error Responses:**
- `409 Conflict` - Plugin has active deployments (undeploy from all tenants first)

---

## Plugin Deployments API

### Deploy Plugin

Deploy a plugin to a tenant.

**Endpoint:** `POST /api/v1/plugin-deployments`
**Authorization:** TenantAdmin, PlatformAdmin
**Rate Limit:** 10 req/min per user

**Request:**
```http
POST /api/v1/plugin-deployments
Authorization: Bearer {token}
Content-Type: application/json

{
  "pluginId": "payment-stripe",
  "pluginVersion": "2.0.0",
  "tenantId": "tenant-acme-corp",
  "environment": "Production",
  "strategy": "Canary",
  "strategyConfig": {
    "canaryPercentage": "10",
    "stages": "10,30,50,100",
    "stageInterval": "300"
  },
  "configuration": {
    "apiKey": "${secret:stripe-api-key}",
    "webhookUrl": "https://acme.com/stripe/webhook"
  },
  "healthCheck": {
    "type": "HttpEndpoint",
    "httpEndpoint": "/health",
    "intervalSeconds": 30,
    "failureThreshold": 3
  },
  "totalInstances": 10
}
```

**Response 202 Accepted:**
```json
{
  "deploymentId": "dep-xyz789",
  "pluginId": "payment-stripe",
  "pluginVersion": "2.0.0",
  "tenantId": "tenant-acme-corp",
  "status": "Pending",
  "strategy": "Canary",
  "progress": 0,
  "startedAt": "2025-11-23T12:00:00Z"
}
```

---

### Get Deployment Status

Get current status of a plugin deployment.

**Endpoint:** `GET /api/v1/plugin-deployments/{deploymentId}`
**Authorization:** TenantAdmin, PlatformAdmin
**Rate Limit:** 60 req/min per user

**Request:**
```http
GET /api/v1/plugin-deployments/dep-xyz789
Authorization: Bearer {token}
```

**Response 200 OK:**
```json
{
  "deploymentId": "dep-xyz789",
  "pluginId": "payment-stripe",
  "pluginVersion": "2.0.0",
  "tenantId": "tenant-acme-corp",
  "environment": "Production",
  "strategy": "Canary",
  "status": "InProgress",
  "progress": 50,
  "totalInstances": 10,
  "healthyInstances": 5,
  "unhealthyInstances": 0,
  "deployedBy": "admin@acme.com",
  "startedAt": "2025-11-23T12:00:00Z",
  "estimatedCompletionAt": "2025-11-23T12:25:00Z"
}
```

---

### List Deployments

List all plugin deployments.

**Endpoint:** `GET /api/v1/plugin-deployments`
**Authorization:** TenantAdmin, PlatformAdmin
**Rate Limit:** 60 req/min per user

**Request:**
```http
GET /api/v1/plugin-deployments?tenantId=tenant-acme-corp&status=Completed
Authorization: Bearer {token}
```

**Query Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `tenantId` | string | No | Filter by tenant |
| `pluginId` | string | No | Filter by plugin |
| `environment` | string | No | Filter by environment |
| `status` | string | No | Filter by status |

**Response 200 OK:**
```json
{
  "deployments": [
    {
      "deploymentId": "dep-xyz789",
      "pluginId": "payment-stripe",
      "pluginVersion": "2.0.0",
      "tenantId": "tenant-acme-corp",
      "status": "Completed",
      "startedAt": "2025-11-23T12:00:00Z",
      "completedAt": "2025-11-23T12:25:00Z"
    }
  ],
  "total": 15
}
```

---

### Rollback Deployment

Rollback a plugin deployment to the previous version.

**Endpoint:** `POST /api/v1/plugin-deployments/{deploymentId}/rollback`
**Authorization:** TenantAdmin, PlatformAdmin
**Rate Limit:** 10 req/min per user

**Request:**
```http
POST /api/v1/plugin-deployments/dep-xyz789/rollback
Authorization: Bearer {token}
Content-Type: application/json

{
  "reason": "High error rate detected"
}
```

**Response 202 Accepted:**
```json
{
  "deploymentId": "dep-xyz789",
  "status": "RolledBack",
  "previousDeploymentId": "dep-abc123",
  "rolledBackAt": "2025-11-23T12:30:00Z"
}
```

---

## Tenants API

### Create Tenant

Create a new tenant.

**Endpoint:** `POST /api/v1/tenants`
**Authorization:** PlatformAdmin only
**Rate Limit:** 10 req/min per user

**Request:**
```http
POST /api/v1/tenants
Authorization: Bearer {token}
Content-Type: application/json

{
  "tenantId": "tenant-acme-corp",
  "name": "Acme Corporation",
  "description": "Enterprise customer",
  "namespace": "acme-prod",
  "quotas": {
    "maxPlugins": 20,
    "maxCpu": 8.0,
    "maxMemoryGB": 16,
    "maxStorageGB": 100
  },
  "rateLimits": {
    "maxRequestsPerMinute": 2000,
    "maxDeploymentsPerHour": 20
  },
  "allowedCategories": ["PaymentGateway", "Authentication", "Reporting"],
  "contactEmail": "admin@acme.com",
  "subscriptionTier": "Enterprise"
}
```

**Response 201 Created:**
```json
{
  "tenantId": "tenant-acme-corp",
  "name": "Acme Corporation",
  "namespace": "acme-prod",
  "status": "Active",
  "createdAt": "2025-11-23T12:00:00Z"
}
```

---

### List Tenants

List all tenants.

**Endpoint:** `GET /api/v1/tenants`
**Authorization:** PlatformAdmin
**Rate Limit:** 60 req/min per user

**Request:**
```http
GET /api/v1/tenants?status=Active&tier=Enterprise
Authorization: Bearer {token}
```

**Response 200 OK:**
```json
{
  "tenants": [
    {
      "tenantId": "tenant-acme-corp",
      "name": "Acme Corporation",
      "status": "Active",
      "subscriptionTier": "Enterprise",
      "pluginCount": 12,
      "createdAt": "2025-11-23T12:00:00Z"
    }
  ],
  "total": 50
}
```

---

### Get Tenant Details

Get detailed information about a tenant.

**Endpoint:** `GET /api/v1/tenants/{tenantId}`
**Authorization:** TenantAdmin (own tenant), PlatformAdmin
**Rate Limit:** 60 req/min per user

**Request:**
```http
GET /api/v1/tenants/tenant-acme-corp
Authorization: Bearer {token}
```

**Response 200 OK:**
```json
{
  "tenantId": "tenant-acme-corp",
  "name": "Acme Corporation",
  "description": "Enterprise customer",
  "namespace": "acme-prod",
  "status": "Active",
  "quotas": {
    "maxPlugins": 20,
    "maxCpu": 8.0,
    "maxMemoryGB": 16
  },
  "resourceUsage": {
    "currentCpu": 3.5,
    "currentMemoryGB": 6.2,
    "currentInstances": 25
  },
  "pluginCount": 12,
  "subscriptionTier": "Enterprise",
  "createdAt": "2025-11-23T12:00:00Z",
  "updatedAt": "2025-11-23T12:00:00Z"
}
```

---

### Get Tenant Plugins

List all plugins deployed to a tenant.

**Endpoint:** `GET /api/v1/tenants/{tenantId}/plugins`
**Authorization:** TenantAdmin (own tenant), PlatformAdmin
**Rate Limit:** 60 req/min per user

**Request:**
```http
GET /api/v1/tenants/tenant-acme-corp/plugins
Authorization: Bearer {token}
```

**Response 200 OK:**
```json
{
  "tenantId": "tenant-acme-corp",
  "plugins": [
    {
      "pluginId": "payment-stripe",
      "pluginVersion": "2.0.0",
      "environment": "Production",
      "status": "Active",
      "healthyInstances": 10,
      "totalInstances": 10,
      "deployedAt": "2025-11-23T12:00:00Z"
    }
  ],
  "total": 12
}
```

---

## Plugin Health API

### Get Plugin Health

Get current health status of a plugin deployment.

**Endpoint:** `GET /api/v1/plugin-deployments/{deploymentId}/health`
**Authorization:** TenantAdmin, PlatformAdmin
**Rate Limit:** 60 req/min per user

**Request:**
```http
GET /api/v1/plugin-deployments/dep-xyz789/health
Authorization: Bearer {token}
```

**Response 200 OK:**
```json
{
  "deploymentId": "dep-xyz789",
  "status": "Healthy",
  "totalInstances": 10,
  "healthyInstances": 10,
  "unhealthyInstances": 0,
  "metrics": {
    "avgResponseTime": 45.2,
    "errorRate": 0.001,
    "cpuUsage": 35.4,
    "memoryUsage": 52.1,
    "requestsPerSecond": 120.5
  },
  "lastHealthCheck": "2025-11-23T12:00:00Z"
}
```

---

### Get Health History

Get historical health data for a plugin deployment.

**Endpoint:** `GET /api/v1/plugin-deployments/{deploymentId}/health/history`
**Authorization:** TenantAdmin, PlatformAdmin
**Rate Limit:** 60 req/min per user

**Request:**
```http
GET /api/v1/plugin-deployments/dep-xyz789/health/history?window=PT24H
Authorization: Bearer {token}
```

**Response 200 OK:**
```json
{
  "deploymentId": "dep-xyz789",
  "window": "PT24H",
  "dataPoints": [
    {
      "timestamp": "2025-11-23T11:00:00Z",
      "healthyInstances": 10,
      "avgResponseTime": 42.1,
      "errorRate": 0.0005
    },
    {
      "timestamp": "2025-11-23T12:00:00Z",
      "healthyInstances": 10,
      "avgResponseTime": 45.2,
      "errorRate": 0.001
    }
  ]
}
```

---

## Capabilities API

### List Capabilities

List all registered capabilities.

**Endpoint:** `GET /api/v1/capabilities`
**Authorization:** All roles
**Rate Limit:** 60 req/min per user

**Request:**
```http
GET /api/v1/capabilities
Authorization: Bearer {token}
```

**Response 200 OK:**
```json
{
  "capabilities": [
    {
      "capabilityId": "IPaymentProcessor",
      "name": "Payment Processor",
      "version": "1.0",
      "pluginCount": 3
    },
    {
      "capabilityId": "IAuthenticationProvider",
      "name": "Authentication Provider",
      "version": "1.0",
      "pluginCount": 5
    }
  ],
  "total": 15
}
```

---

### Get Plugins by Capability

Get all plugins that implement a specific capability.

**Endpoint:** `GET /api/v1/capabilities/{capabilityId}/plugins`
**Authorization:** All roles
**Rate Limit:** 60 req/min per user

**Request:**
```http
GET /api/v1/capabilities/IPaymentProcessor/plugins
Authorization: Bearer {token}
```

**Response 200 OK:**
```json
{
  "capabilityId": "IPaymentProcessor",
  "plugins": [
    {
      "pluginId": "payment-stripe",
      "name": "Stripe Payment Processor",
      "latestVersion": "2.0.0"
    },
    {
      "pluginId": "payment-paypal",
      "name": "PayPal Payment Processor",
      "latestVersion": "1.5.0"
    }
  ],
  "total": 3
}
```

---

## Marketplace API

### Browse Marketplace

Browse available plugins in the marketplace.

**Endpoint:** `GET /api/v1/marketplace/plugins`
**Authorization:** All roles
**Rate Limit:** 60 req/min per user

**Request:**
```http
GET /api/v1/marketplace/plugins?category=PaymentGateway&featured=true
Authorization: Bearer {token}
```

**Response 200 OK:**
```json
{
  "plugins": [
    {
      "pluginId": "payment-stripe",
      "name": "Stripe Payment Processor",
      "category": "PaymentGateway",
      "publisher": "Acme Corp",
      "description": "Process payments via Stripe",
      "iconUrl": "https://example.com/icons/stripe.png",
      "latestVersion": "2.0.0",
      "installationCount": 245,
      "averageRating": 4.8,
      "featured": true
    }
  ],
  "total": 42
}
```

---

### Search Marketplace

Search for plugins in the marketplace.

**Endpoint:** `GET /api/v1/marketplace/search`
**Authorization:** All roles
**Rate Limit:** 60 req/min per user

**Request:**
```http
GET /api/v1/marketplace/search?q=payment&tags=stripe,credit-card
Authorization: Bearer {token}
```

**Response 200 OK:**
```json
{
  "query": "payment",
  "results": [
    {
      "pluginId": "payment-stripe",
      "name": "Stripe Payment Processor",
      "relevanceScore": 0.95,
      "installationCount": 245
    }
  ],
  "total": 5
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
        "field": "pluginId",
        "message": "PluginId must contain only lowercase letters, numbers, and dashes"
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
| 400 | `DEPENDENCY_CONFLICT` | Plugin dependency conflict |
| 401 | `UNAUTHORIZED` | Missing or invalid JWT token |
| 403 | `FORBIDDEN` | Insufficient permissions |
| 404 | `NOT_FOUND` | Resource not found |
| 409 | `CONFLICT` | Resource already exists |
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
| `POST /api/v1/plugin-deployments` | 10 req | 1 minute |
| `POST /api/v1/plugin-deployments/{id}/rollback` | 10 req | 1 minute |
| `GET /api/v1/plugins` | 60 req | 1 minute |
| `GET /api/v1/plugin-deployments` | 60 req | 1 minute |
| `GET /api/v1/tenants` | 60 req | 1 minute |
| `POST /api/v1/tenants` | 10 req | 1 minute |

---

**API Version:** 1.0.0
**Last Updated:** 2025-11-23
**Support:** api-support@example.com
