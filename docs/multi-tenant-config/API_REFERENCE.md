# Multi-Tenant Configuration Service API Reference

**Version:** 1.0.0
**Base URL:** `https://api.example.com/api/v1`
**Authentication:** JWT Bearer Token
**Content-Type:** `application/json`

---

## Table of Contents

1. [Authentication](#authentication)
2. [Tenants API](#tenants-api)
3. [Configurations API](#configurations-api)
4. [Environments API](#environments-api)
5. [Approvals API](#approvals-api)
6. [Deployments API](#deployments-api)
7. [Audit Logs API](#audit-logs-api)
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
GET /api/v1/tenants
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

---

## Tenants API

### Create Tenant

Create a new tenant in the system.

**Endpoint:** `POST /api/v1/tenants`
**Authorization:** Admin
**Rate Limit:** 10 req/min per user

**Request:**
```http
POST /api/v1/tenants
Authorization: Bearer {token}
Content-Type: application/json

{
  "tenantId": "acme-corp",
  "name": "ACME Corporation",
  "tier": "Enterprise",
  "maxConfigurations": 1000,
  "maxEnvironments": 4,
  "contactEmail": "admin@acme.com",
  "metadata": {
    "industry": "Technology",
    "region": "US"
  }
}
```

**Request Fields:**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `tenantId` | string | Yes | Unique tenant ID (3-50 alphanumeric/hyphens) |
| `name` | string | Yes | Human-readable tenant name |
| `tier` | string | No | Tier: `Free`, `Pro`, `Enterprise` (default: Free) |
| `maxConfigurations` | int | No | Max configs allowed (default: 100) |
| `maxEnvironments` | int | No | Max environments (default: 4) |
| `contactEmail` | string | No | Contact email for notifications |
| `metadata` | object | No | Custom key-value metadata |

**Response 201 Created:**
```json
{
  "tenantId": "acme-corp",
  "name": "ACME Corporation",
  "tier": "Enterprise",
  "status": "Active",
  "maxConfigurations": 1000,
  "maxEnvironments": 4,
  "contactEmail": "admin@acme.com",
  "metadata": {
    "industry": "Technology",
    "region": "US"
  },
  "createdAt": "2025-11-23T12:00:00Z",
  "updatedAt": "2025-11-23T12:00:00Z"
}
```

**Error Responses:**
- `400 Bad Request` - Invalid tenant ID or validation failure
- `409 Conflict` - Tenant ID already exists
- `429 Too Many Requests` - Rate limit exceeded

---

### List Tenants

Get a list of all tenants with pagination.

**Endpoint:** `GET /api/v1/tenants`
**Authorization:** Admin, Viewer
**Rate Limit:** 60 req/min per user

**Request:**
```http
GET /api/v1/tenants?tier=Enterprise&status=Active&limit=20&offset=0
Authorization: Bearer {token}
```

**Query Parameters:**

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `tier` | string | No | - | Filter by tier |
| `status` | string | No | - | Filter by status (`Active`, `Suspended`, `Deleted`) |
| `limit` | int | No | 20 | Max tenants to return (1-100) |
| `offset` | int | No | 0 | Pagination offset |

**Response 200 OK:**
```json
{
  "tenants": [
    {
      "tenantId": "acme-corp",
      "name": "ACME Corporation",
      "tier": "Enterprise",
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

### Get Tenant Details

Get detailed information about a specific tenant.

**Endpoint:** `GET /api/v1/tenants/{tenantId}`
**Authorization:** Admin, TenantAdmin (own tenant only), Viewer
**Rate Limit:** 60 req/min per user

**Request:**
```http
GET /api/v1/tenants/acme-corp
Authorization: Bearer {token}
```

**Response 200 OK:**
```json
{
  "tenantId": "acme-corp",
  "name": "ACME Corporation",
  "tier": "Enterprise",
  "status": "Active",
  "maxConfigurations": 1000,
  "maxEnvironments": 4,
  "contactEmail": "admin@acme.com",
  "metadata": {
    "industry": "Technology",
    "region": "US"
  },
  "createdAt": "2025-11-23T12:00:00Z",
  "updatedAt": "2025-11-23T12:00:00Z",
  "statistics": {
    "configurationCount": 245,
    "activeDeployments": 3
  }
}
```

---

### Update Tenant

Update tenant information.

**Endpoint:** `PUT /api/v1/tenants/{tenantId}`
**Authorization:** Admin, TenantAdmin (own tenant only)
**Rate Limit:** 10 req/min per user

**Request:**
```http
PUT /api/v1/tenants/acme-corp
Authorization: Bearer {token}
Content-Type: application/json

{
  "name": "ACME Corporation Ltd",
  "contactEmail": "support@acme.com",
  "maxConfigurations": 2000
}
```

**Response 200 OK:**
```json
{
  "tenantId": "acme-corp",
  "name": "ACME Corporation Ltd",
  "contactEmail": "support@acme.com",
  "maxConfigurations": 2000,
  "updatedAt": "2025-11-23T13:00:00Z"
}
```

---

### Delete Tenant

Soft delete a tenant (retains data for 30 days).

**Endpoint:** `DELETE /api/v1/tenants/{tenantId}`
**Authorization:** Admin only
**Rate Limit:** 10 req/min per user

**Request:**
```http
DELETE /api/v1/tenants/acme-corp
Authorization: Bearer {token}
```

**Response 204 No Content**

**Note:** Soft delete marks tenant as deleted but retains data for 30 days for recovery.

---

## Configurations API

### Create Configuration

Create a new configuration for a tenant.

**Endpoint:** `POST /api/v1/configs`
**Authorization:** ConfigManager, Admin
**Rate Limit:** 100 req/min per user

**Request:**
```http
POST /api/v1/configs
Authorization: Bearer {token}
Content-Type: application/json

{
  "tenantId": "acme-corp",
  "key": "feature.new_dashboard",
  "value": "true",
  "type": "Boolean",
  "environment": "Development",
  "description": "Enable new dashboard UI",
  "tags": ["feature-flag", "ui"],
  "isSensitive": false
}
```

**Request Fields:**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `tenantId` | string | Yes | Tenant identifier |
| `key` | string | Yes | Configuration key (dot-notation) |
| `value` | string | Yes | Configuration value |
| `type` | string | No | Type: `String`, `Number`, `Boolean`, `JSON` (default: String) |
| `environment` | string | No | Environment (default: Development) |
| `description` | string | No | Configuration description |
| `tags` | array | No | Tags for categorization |
| `isSensitive` | boolean | No | Mark as sensitive (default: false) |
| `defaultValue` | string | No | Default value for other environments |

**Response 201 Created:**
```json
{
  "configId": "cfg-7f8a9b1c-2d3e-4f5g-6h7i",
  "tenantId": "acme-corp",
  "key": "feature.new_dashboard",
  "value": "true",
  "type": "Boolean",
  "environment": "Development",
  "version": 1,
  "description": "Enable new dashboard UI",
  "tags": ["feature-flag", "ui"],
  "createdBy": "admin@acme.com",
  "createdAt": "2025-11-23T12:00:00Z",
  "updatedAt": "2025-11-23T12:00:00Z"
}
```

**Error Responses:**
- `400 Bad Request` - Invalid configuration or validation failure
- `404 Not Found` - Tenant does not exist
- `409 Conflict` - Configuration key already exists for this tenant/environment
- `429 Too Many Requests` - Rate limit exceeded

---

### Get Tenant Configurations

Get all configurations for a tenant.

**Endpoint:** `GET /api/v1/configs/tenant/{tenantId}`
**Authorization:** All roles (filtered by tenant access)
**Rate Limit:** 1000 req/min per tenant

**Request:**
```http
GET /api/v1/configs/tenant/acme-corp?environment=Production&tags=feature-flag&limit=50
Authorization: Bearer {token}
```

**Query Parameters:**

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `environment` | string | No | - | Filter by environment |
| `tags` | string | No | - | Filter by tags (comma-separated) |
| `prefix` | string | No | - | Filter keys by prefix (e.g., "feature.") |
| `limit` | int | No | 50 | Max configs to return (1-100) |
| `offset` | int | No | 0 | Pagination offset |

**Response 200 OK:**
```json
{
  "configs": [
    {
      "configId": "cfg-123",
      "key": "feature.new_dashboard",
      "value": "true",
      "type": "Boolean",
      "environment": "Production",
      "version": 3,
      "updatedAt": "2025-11-23T12:00:00Z"
    }
  ],
  "total": 245,
  "limit": 50,
  "offset": 0
}
```

---

### Get Configuration by ID

Get details of a specific configuration.

**Endpoint:** `GET /api/v1/configs/{configId}`
**Authorization:** All roles (filtered by tenant access)
**Rate Limit:** 300 req/min per user

**Request:**
```http
GET /api/v1/configs/cfg-123
Authorization: Bearer {token}
```

**Response 200 OK:**
```json
{
  "configId": "cfg-123",
  "tenantId": "acme-corp",
  "key": "feature.new_dashboard",
  "value": "true",
  "type": "Boolean",
  "environment": "Production",
  "version": 3,
  "description": "Enable new dashboard UI",
  "tags": ["feature-flag", "ui"],
  "createdBy": "admin@acme.com",
  "updatedBy": "manager@acme.com",
  "createdAt": "2025-11-20T10:00:00Z",
  "updatedAt": "2025-11-23T12:00:00Z"
}
```

---

### Update Configuration

Update a configuration value (creates new version).

**Endpoint:** `PUT /api/v1/configs/{configId}`
**Authorization:** ConfigManager, Admin
**Rate Limit:** 100 req/min per user

**Request:**
```http
PUT /api/v1/configs/cfg-123
Authorization: Bearer {token}
Content-Type: application/json

{
  "value": "false",
  "description": "Disable new dashboard temporarily"
}
```

**Response 200 OK:**
```json
{
  "configId": "cfg-123",
  "key": "feature.new_dashboard",
  "value": "false",
  "version": 4,
  "updatedBy": "admin@acme.com",
  "updatedAt": "2025-11-23T14:00:00Z"
}
```

**Note:** Updates in Production environment require approval workflow.

---

### Delete Configuration

Soft delete a configuration.

**Endpoint:** `DELETE /api/v1/configs/{configId}`
**Authorization:** ConfigManager, Admin
**Rate Limit:** 10 req/min per user

**Request:**
```http
DELETE /api/v1/configs/cfg-123
Authorization: Bearer {token}
```

**Response 204 No Content**

---

### Get Configuration Versions

Get version history for a configuration.

**Endpoint:** `GET /api/v1/configs/{configId}/versions`
**Authorization:** All roles (filtered by tenant access)
**Rate Limit:** 60 req/min per user

**Request:**
```http
GET /api/v1/configs/cfg-123/versions?limit=10
Authorization: Bearer {token}
```

**Response 200 OK:**
```json
{
  "versions": [
    {
      "versionId": "ver-456",
      "version": 4,
      "value": "false",
      "previousValue": "true",
      "changeType": "Updated",
      "changeDescription": "Disable new dashboard temporarily",
      "changedBy": "admin@acme.com",
      "createdAt": "2025-11-23T14:00:00Z"
    },
    {
      "versionId": "ver-455",
      "version": 3,
      "value": "true",
      "previousValue": "false",
      "changeType": "Updated",
      "changedBy": "manager@acme.com",
      "createdAt": "2025-11-23T12:00:00Z"
    }
  ],
  "total": 4
}
```

---

### Rollback Configuration

Rollback to a previous version.

**Endpoint:** `POST /api/v1/configs/{configId}/rollback`
**Authorization:** ConfigManager, Admin
**Rate Limit:** 10 req/min per user

**Request:**
```http
POST /api/v1/configs/cfg-123/rollback
Authorization: Bearer {token}
Content-Type: application/json

{
  "targetVersion": 3,
  "reason": "New dashboard causing performance issues"
}
```

**Response 200 OK:**
```json
{
  "configId": "cfg-123",
  "currentVersion": 5,
  "rolledBackToVersion": 3,
  "value": "true",
  "rolledBackBy": "admin@acme.com",
  "rolledBackAt": "2025-11-23T15:00:00Z"
}
```

---

### Compare Configuration Versions

Get diff between two versions.

**Endpoint:** `GET /api/v1/configs/{configId}/diff`
**Authorization:** All roles (filtered by tenant access)
**Rate Limit:** 60 req/min per user

**Request:**
```http
GET /api/v1/configs/cfg-123/diff?from=3&to=4
Authorization: Bearer {token}
```

**Response 200 OK:**
```json
{
  "configId": "cfg-123",
  "key": "feature.new_dashboard",
  "fromVersion": 3,
  "toVersion": 4,
  "changes": {
    "value": {
      "old": "true",
      "new": "false"
    },
    "changeType": "Updated",
    "changedBy": "admin@acme.com",
    "changedAt": "2025-11-23T14:00:00Z"
  }
}
```

---

### Promote Configuration

Promote configuration to another environment.

**Endpoint:** `POST /api/v1/configs/{configId}/promote`
**Authorization:** ConfigManager, Admin
**Rate Limit:** 10 req/min per user

**Request:**
```http
POST /api/v1/configs/cfg-123/promote
Authorization: Bearer {token}
Content-Type: application/json

{
  "targetEnvironment": "Production",
  "description": "Promote new dashboard feature to production",
  "requiresApproval": true
}
```

**Response 202 Accepted:**
```json
{
  "approvalId": "apr-789",
  "configId": "cfg-123",
  "targetEnvironment": "Production",
  "status": "Pending",
  "requiredApprovalLevel": 2,
  "expiresAt": "2025-11-26T12:00:00Z",
  "createdAt": "2025-11-23T12:00:00Z"
}
```

**Note:** Production promotions create approval requests automatically.

---

## Environments API

### List Environments

Get available environments and their hierarchy.

**Endpoint:** `GET /api/v1/environments`
**Authorization:** All roles
**Rate Limit:** 60 req/min per user

**Request:**
```http
GET /api/v1/environments
Authorization: Bearer {token}
```

**Response 200 OK:**
```json
{
  "environments": [
    {
      "name": "Development",
      "order": 1,
      "requiresApproval": false,
      "defaultStrategy": "Direct"
    },
    {
      "name": "QA",
      "order": 2,
      "requiresApproval": false,
      "defaultStrategy": "Direct"
    },
    {
      "name": "Staging",
      "order": 3,
      "requiresApproval": true,
      "approvalLevel": 1,
      "defaultStrategy": "Canary"
    },
    {
      "name": "Production",
      "order": 4,
      "requiresApproval": true,
      "approvalLevel": 2,
      "defaultStrategy": "Canary"
    }
  ]
}
```

---

## Approvals API

### Create Approval Request

Create an approval request for a configuration change.

**Endpoint:** `POST /api/v1/approvals`
**Authorization:** ConfigManager, Admin
**Rate Limit:** 10 req/min per user

**Request:**
```http
POST /api/v1/approvals
Authorization: Bearer {token}
Content-Type: application/json

{
  "configId": "cfg-123",
  "tenantId": "acme-corp",
  "targetEnvironment": "Production",
  "proposedValue": "true",
  "changeDescription": "Enable new dashboard in production"
}
```

**Response 201 Created:**
```json
{
  "approvalId": "apr-789",
  "configId": "cfg-123",
  "tenantId": "acme-corp",
  "targetEnvironment": "Production",
  "proposedValue": "true",
  "currentValue": "false",
  "status": "Pending",
  "requiredApprovalLevel": 2,
  "currentApprovalLevel": 0,
  "requestedBy": "admin@acme.com",
  "createdAt": "2025-11-23T12:00:00Z",
  "expiresAt": "2025-11-26T12:00:00Z"
}
```

---

### Get Approval Details

Get details about a specific approval request.

**Endpoint:** `GET /api/v1/approvals/{approvalId}`
**Authorization:** All roles (filtered by tenant access)
**Rate Limit:** 60 req/min per user

**Request:**
```http
GET /api/v1/approvals/apr-789
Authorization: Bearer {token}
```

**Response 200 OK:**
```json
{
  "approvalId": "apr-789",
  "configId": "cfg-123",
  "tenantId": "acme-corp",
  "targetEnvironment": "Production",
  "proposedValue": "true",
  "currentValue": "false",
  "changeDescription": "Enable new dashboard in production",
  "status": "Pending",
  "requiredApprovalLevel": 2,
  "currentApprovalLevel": 1,
  "approvers": [
    {
      "userId": "manager@acme.com",
      "level": 1,
      "status": "Approved",
      "comments": "Looks good, approved for staging first",
      "approvedAt": "2025-11-23T13:00:00Z"
    }
  ],
  "requestedBy": "admin@acme.com",
  "createdAt": "2025-11-23T12:00:00Z",
  "expiresAt": "2025-11-26T12:00:00Z"
}
```

---

### Approve Request

Approve an approval request.

**Endpoint:** `POST /api/v1/approvals/{approvalId}/approve`
**Authorization:** Approver, Admin
**Rate Limit:** 30 req/min per user

**Request:**
```http
POST /api/v1/approvals/apr-789/approve
Authorization: Bearer {token}
Content-Type: application/json

{
  "comments": "Approved after reviewing test results"
}
```

**Response 200 OK:**
```json
{
  "approvalId": "apr-789",
  "status": "Approved",
  "currentApprovalLevel": 2,
  "approvedBy": "compliance@acme.com",
  "approvedAt": "2025-11-23T14:00:00Z",
  "deploymentId": "dep-abc123"
}
```

**Note:** If all required approval levels are met, deployment starts automatically.

---

### Reject Request

Reject an approval request.

**Endpoint:** `POST /api/v1/approvals/{approvalId}/reject`
**Authorization:** Approver, Admin
**Rate Limit:** 30 req/min per user

**Request:**
```http
POST /api/v1/approvals/apr-789/reject
Authorization: Bearer {token}
Content-Type: application/json

{
  "comments": "Need more testing before production deployment"
}
```

**Response 200 OK:**
```json
{
  "approvalId": "apr-789",
  "status": "Rejected",
  "rejectedBy": "compliance@acme.com",
  "rejectedAt": "2025-11-23T14:00:00Z",
  "comments": "Need more testing before production deployment"
}
```

---

### List Pending Approvals

Get all pending approval requests.

**Endpoint:** `GET /api/v1/approvals/pending`
**Authorization:** Approver, Admin
**Rate Limit:** 60 req/min per user

**Request:**
```http
GET /api/v1/approvals/pending?tenantId=acme-corp&limit=20
Authorization: Bearer {token}
```

**Response 200 OK:**
```json
{
  "approvals": [
    {
      "approvalId": "apr-789",
      "configId": "cfg-123",
      "tenantId": "acme-corp",
      "targetEnvironment": "Production",
      "status": "Pending",
      "requiredApprovalLevel": 2,
      "currentApprovalLevel": 1,
      "requestedBy": "admin@acme.com",
      "createdAt": "2025-11-23T12:00:00Z",
      "expiresAt": "2025-11-26T12:00:00Z"
    }
  ],
  "total": 3
}
```

---

## Deployments API

### Create Deployment

Initiate a configuration deployment.

**Endpoint:** `POST /api/v1/deployments`
**Authorization:** ConfigManager, Admin
**Rate Limit:** 10 deployments/hour per tenant

**Request:**
```http
POST /api/v1/deployments
Authorization: Bearer {token}
Content-Type: application/json

{
  "configId": "cfg-123",
  "tenantId": "acme-corp",
  "environment": "Production",
  "strategy": "Canary",
  "approvalId": "apr-789"
}
```

**Response 201 Created:**
```json
{
  "deploymentId": "dep-abc123",
  "configId": "cfg-123",
  "tenantId": "acme-corp",
  "environment": "Production",
  "strategy": "Canary",
  "status": "InProgress",
  "progress": 0,
  "canaryPercentage": 10,
  "deployedBy": "admin@acme.com",
  "startedAt": "2025-11-23T12:00:00Z"
}
```

---

### Get Deployment Status

Get the current status of a deployment.

**Endpoint:** `GET /api/v1/deployments/{deploymentId}`
**Authorization:** All roles (filtered by tenant access)
**Rate Limit:** 300 req/min per user

**Request:**
```http
GET /api/v1/deployments/dep-abc123
Authorization: Bearer {token}
```

**Response 200 OK:**
```json
{
  "deploymentId": "dep-abc123",
  "configId": "cfg-123",
  "tenantId": "acme-corp",
  "environment": "Production",
  "strategy": "Canary",
  "status": "InProgress",
  "progress": 50,
  "canaryPercentage": 50,
  "metrics": {
    "baselineErrorRate": 0.5,
    "currentErrorRate": 0.6,
    "baselineLatencyP99": 120,
    "currentLatencyP99": 125,
    "requestCount": 15000
  },
  "deployedBy": "admin@acme.com",
  "startedAt": "2025-11-23T12:00:00Z"
}
```

---

### Rollback Deployment

Manually rollback a deployment.

**Endpoint:** `POST /api/v1/deployments/{deploymentId}/rollback`
**Authorization:** ConfigManager, Admin
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
  "rollbackReason": "High error rate detected",
  "rolledBackBy": "admin@acme.com",
  "rolledBackAt": "2025-11-23T12:30:00Z"
}
```

---

### List Deployments

Get deployment history.

**Endpoint:** `GET /api/v1/deployments`
**Authorization:** All roles (filtered by tenant access)
**Rate Limit:** 60 req/min per user

**Request:**
```http
GET /api/v1/deployments?tenantId=acme-corp&environment=Production&status=Completed&limit=20
Authorization: Bearer {token}
```

**Response 200 OK:**
```json
{
  "deployments": [
    {
      "deploymentId": "dep-abc123",
      "configId": "cfg-123",
      "environment": "Production",
      "strategy": "Canary",
      "status": "Completed",
      "progress": 100,
      "deployedBy": "admin@acme.com",
      "startedAt": "2025-11-23T12:00:00Z",
      "completedAt": "2025-11-23T12:25:00Z",
      "duration": "PT25M"
    }
  ],
  "total": 42
}
```

---

## Audit Logs API

### Query Audit Logs

Query audit logs with filters.

**Endpoint:** `GET /api/v1/audit-logs`
**Authorization:** Admin, Viewer
**Rate Limit:** 60 req/min per user

**Request:**
```http
GET /api/v1/audit-logs?tenantId=acme-corp&eventType=Updated&startDate=2025-11-20&limit=50
Authorization: Bearer {token}
```

**Query Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `tenantId` | string | No | Filter by tenant |
| `eventType` | string | No | Filter by event type |
| `userId` | string | No | Filter by user |
| `configKey` | string | No | Filter by config key |
| `startDate` | date | No | Filter from date (ISO 8601) |
| `endDate` | date | No | Filter to date (ISO 8601) |
| `limit` | int | No | Max logs to return (1-100) |
| `offset` | int | No | Pagination offset |

**Response 200 OK:**
```json
{
  "logs": [
    {
      "logId": "log-xyz789",
      "eventType": "Updated",
      "tenantId": "acme-corp",
      "configId": "cfg-123",
      "configKey": "feature.new_dashboard",
      "oldValue": "false",
      "newValue": "true",
      "userId": "admin@acme.com",
      "ipAddress": "192.168.1.100",
      "traceId": "trace-abc123",
      "environment": "Production",
      "timestamp": "2025-11-23T12:00:00Z"
    }
  ],
  "total": 1234,
  "limit": 50,
  "offset": 0
}
```

---

### Export Audit Logs

Export audit logs in CSV or JSON format.

**Endpoint:** `GET /api/v1/audit-logs/export`
**Authorization:** Admin
**Rate Limit:** 10 req/min per user

**Request:**
```http
GET /api/v1/audit-logs/export?tenantId=acme-corp&format=csv&startDate=2025-11-01&endDate=2025-11-30
Authorization: Bearer {token}
```

**Response 200 OK:**
```csv
LogId,EventType,TenantId,ConfigKey,OldValue,NewValue,UserId,Timestamp
log-xyz789,Updated,acme-corp,feature.new_dashboard,false,true,admin@acme.com,2025-11-23T12:00:00Z
...
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
        "field": "value",
        "message": "Value must be a valid boolean (true/false)"
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
| 400 | `INVALID_VALUE_TYPE` | Configuration value type mismatch |
| 401 | `UNAUTHORIZED` | Missing or invalid JWT token |
| 403 | `FORBIDDEN` | Insufficient permissions |
| 404 | `NOT_FOUND` | Resource not found |
| 409 | `CONFLICT` | Resource already exists |
| 409 | `QUOTA_EXCEEDED` | Tenant quota exceeded |
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
X-RateLimit-Limit: 1000
X-RateLimit-Remaining: 850
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
| `GET /api/v1/configs/tenant/{tenantId}` | 1000 req | 1 minute |
| `POST /api/v1/configs` | 100 req | 1 minute |
| `PUT /api/v1/configs/{configId}` | 100 req | 1 minute |
| `POST /api/v1/deployments` | 10 deployments | 1 hour |
| `POST /api/v1/approvals/{id}/approve` | 30 req | 1 minute |
| `GET /api/v1/audit-logs` | 60 req | 1 minute |
| `GET /api/v1/tenants` | 60 req | 1 minute |

---

## Pagination

List endpoints support pagination via limit/offset parameters.

### Pagination Parameters

```http
GET /api/v1/configs/tenant/acme-corp?limit=20&offset=40
```

**Parameters:**
- `limit` - Max items to return (1-100, default: 20)
- `offset` - Number of items to skip (default: 0)

### Pagination Response

```json
{
  "configs": [...],
  "total": 245,
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
