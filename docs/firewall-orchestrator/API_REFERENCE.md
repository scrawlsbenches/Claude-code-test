# Firewall Orchestrator API Reference

**Version:** 1.0.0
**Base URL:** `https://api.example.com/api/v1`
**Authentication:** JWT Bearer Token
**Content-Type:** `application/json`

---

## Table of Contents

1. [Authentication](#authentication)
2. [RuleSets API](#rulesets-api)
3. [Rules API](#rules-api)
4. [Deployments API](#deployments-api)
5. [Targets API](#targets-api)
6. [Validation API](#validation-api)
7. [Error Responses](#error-responses)
8. [Rate Limiting](#rate-limiting)

---

## Authentication

All API endpoints require JWT authentication.

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
    "role": "FirewallAdmin"
  }
}
```

### Use Token in Requests

```http
GET /api/v1/firewall/rulesets
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

---

## RuleSets API

### Create RuleSet

Create a new firewall rule set.

**Endpoint:** `POST /api/v1/firewall/rulesets`
**Authorization:** FirewallDeveloper, FirewallAdmin
**Rate Limit:** 10 req/min per user

**Request:**
```http
POST /api/v1/firewall/rulesets
Authorization: Bearer {token}
Content-Type: application/json

{
  "name": "web-server-rules",
  "description": "Rules for web server tier",
  "environment": "Development",
  "targetType": "CloudFirewall",
  "metadata": {
    "team": "platform",
    "criticality": "high"
  }
}
```

**Request Fields:**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `name` | string | Yes | Unique rule set name (alphanumeric, dots, dashes) |
| `description` | string | No | Human-readable description |
| `environment` | string | Yes | Development, QA, Staging, Production |
| `targetType` | string | Yes | CloudFirewall, OnPremiseFirewall |
| `metadata` | object | No | Key-value metadata |

**Response 201 Created:**
```json
{
  "name": "web-server-rules",
  "description": "Rules for web server tier",
  "version": "1.0.0",
  "environment": "Development",
  "targetType": "CloudFirewall",
  "status": "Draft",
  "createdAt": "2025-11-23T12:00:00Z",
  "updatedAt": "2025-11-23T12:00:00Z",
  "createdBy": "admin@example.com"
}
```

**Error Responses:**
- `400 Bad Request` - Invalid rule set name or configuration
- `409 Conflict` - Rule set already exists

---

### List RuleSets

List all firewall rule sets.

**Endpoint:** `GET /api/v1/firewall/rulesets`
**Authorization:** All roles
**Rate Limit:** 60 req/min per user

**Request:**
```http
GET /api/v1/firewall/rulesets?environment=Production&limit=20&offset=0
Authorization: Bearer {token}
```

**Query Parameters:**

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `environment` | string | No | - | Filter by environment |
| `targetType` | string | No | - | Filter by target type |
| `status` | string | No | - | Filter by status |
| `limit` | int | No | 20 | Max rule sets to return (1-100) |
| `offset` | int | No | 0 | Pagination offset |

**Response 200 OK:**
```json
{
  "ruleSets": [
    {
      "name": "web-server-rules",
      "version": "1.0.0",
      "environment": "Production",
      "status": "Deployed",
      "ruleCount": 12,
      "createdAt": "2025-11-23T12:00:00Z"
    }
  ],
  "total": 42,
  "limit": 20,
  "offset": 0
}
```

---

### Get RuleSet Details

Get details about a specific rule set.

**Endpoint:** `GET /api/v1/firewall/rulesets/{name}`
**Authorization:** All roles
**Rate Limit:** 60 req/min per user

**Request:**
```http
GET /api/v1/firewall/rulesets/web-server-rules
Authorization: Bearer {token}
```

**Response 200 OK:**
```json
{
  "name": "web-server-rules",
  "description": "Rules for web server tier",
  "version": "1.0.0",
  "environment": "Production",
  "targetType": "CloudFirewall",
  "status": "Deployed",
  "rules": [
    {
      "ruleId": "rule-001",
      "name": "allow-https",
      "action": "Allow",
      "protocol": "TCP",
      "sourceAddress": "0.0.0.0/0",
      "destinationAddress": "10.0.1.0/24",
      "destinationPort": "443",
      "priority": 100,
      "enabled": true
    }
  ],
  "metadata": {
    "team": "platform",
    "criticality": "high"
  },
  "createdAt": "2025-11-23T12:00:00Z",
  "updatedAt": "2025-11-23T12:00:00Z",
  "createdBy": "admin@example.com"
}
```

---

### Update RuleSet

Update a rule set.

**Endpoint:** `PUT /api/v1/firewall/rulesets/{name}`
**Authorization:** FirewallDeveloper, FirewallAdmin
**Rate Limit:** 10 req/min per user

**Request:**
```http
PUT /api/v1/firewall/rulesets/web-server-rules
Authorization: Bearer {token}
Content-Type: application/json

{
  "description": "Updated description",
  "metadata": {
    "team": "security",
    "criticality": "critical"
  }
}
```

**Response 200 OK:**
```json
{
  "name": "web-server-rules",
  "description": "Updated description",
  "version": "1.0.1",
  "updatedAt": "2025-11-23T13:00:00Z"
}
```

**Note:** Updating rule set increments version

---

### Delete RuleSet

Delete a rule set (admin only).

**Endpoint:** `DELETE /api/v1/firewall/rulesets/{name}`
**Authorization:** FirewallAdmin only
**Rate Limit:** 10 req/min per user

**Request:**
```http
DELETE /api/v1/firewall/rulesets/web-server-rules
Authorization: Bearer {token}
```

**Response 204 No Content**

**Error Responses:**
- `409 Conflict` - Rule set is currently deployed (undeploy first)

---

## Rules API

### Add Rule to RuleSet

Add a new firewall rule to a rule set.

**Endpoint:** `POST /api/v1/firewall/rulesets/{name}/rules`
**Authorization:** FirewallDeveloper, FirewallAdmin
**Rate Limit:** 50 req/min per user

**Request:**
```http
POST /api/v1/firewall/rulesets/web-server-rules/rules
Authorization: Bearer {token}
Content-Type: application/json

{
  "name": "allow-https",
  "description": "Allow HTTPS traffic",
  "action": "Allow",
  "protocol": "TCP",
  "sourceAddress": "0.0.0.0/0",
  "destinationAddress": "10.0.1.0/24",
  "sourcePort": "any",
  "destinationPort": "443",
  "priority": 100,
  "enabled": true,
  "logEnabled": false,
  "tags": ["web", "https"]
}
```

**Request Fields:**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `name` | string | Yes | Rule name |
| `description` | string | No | Rule description |
| `action` | string | Yes | Allow, Deny, Reject |
| `protocol` | string | Yes | TCP, UDP, ICMP, ESP, AH, GRE, ALL |
| `sourceAddress` | string | Yes | Source IP or CIDR (e.g., "0.0.0.0/0") |
| `destinationAddress` | string | Yes | Destination IP or CIDR |
| `sourcePort` | string | No | Port or range (e.g., "80", "1024-65535", "any") |
| `destinationPort` | string | No | Port or range |
| `priority` | int | Yes | Priority 0-10000 (lower = higher priority) |
| `enabled` | bool | No | Default: true |
| `logEnabled` | bool | No | Default: false |
| `tags` | array | No | Rule tags |

**Response 201 Created:**
```json
{
  "ruleId": "rule-abc123",
  "name": "allow-https",
  "action": "Allow",
  "protocol": "TCP",
  "sourceAddress": "0.0.0.0/0",
  "destinationAddress": "10.0.1.0/24",
  "destinationPort": "443",
  "priority": 100,
  "enabled": true,
  "createdAt": "2025-11-23T12:00:00Z"
}
```

**Error Responses:**
- `400 Bad Request` - Invalid rule (validation errors)
- `409 Conflict` - Priority conflict with existing rule

---

### List Rules in RuleSet

List all rules in a rule set.

**Endpoint:** `GET /api/v1/firewall/rulesets/{name}/rules`
**Authorization:** All roles
**Rate Limit:** 60 req/min per user

**Request:**
```http
GET /api/v1/firewall/rulesets/web-server-rules/rules
Authorization: Bearer {token}
```

**Response 200 OK:**
```json
{
  "ruleSetName": "web-server-rules",
  "rules": [
    {
      "ruleId": "rule-001",
      "name": "allow-https",
      "action": "Allow",
      "priority": 100
    }
  ],
  "total": 12
}
```

---

### Update Rule

Update an existing rule.

**Endpoint:** `PUT /api/v1/firewall/rulesets/{name}/rules/{ruleId}`
**Authorization:** FirewallDeveloper, FirewallAdmin
**Rate Limit:** 50 req/min per user

**Request:**
```http
PUT /api/v1/firewall/rulesets/web-server-rules/rules/rule-abc123
Authorization: Bearer {token}
Content-Type: application/json

{
  "priority": 150,
  "logEnabled": true
}
```

**Response 200 OK:**
```json
{
  "ruleId": "rule-abc123",
  "name": "allow-https",
  "priority": 150,
  "logEnabled": true,
  "updatedAt": "2025-11-23T13:00:00Z"
}
```

---

### Delete Rule

Delete a rule from a rule set.

**Endpoint:** `DELETE /api/v1/firewall/rulesets/{name}/rules/{ruleId}`
**Authorization:** FirewallDeveloper, FirewallAdmin
**Rate Limit:** 50 req/min per user

**Request:**
```http
DELETE /api/v1/firewall/rulesets/web-server-rules/rules/rule-abc123
Authorization: Bearer {token}
```

**Response 204 No Content**

---

## Deployments API

### Create Deployment

Initiate a firewall rule deployment.

**Endpoint:** `POST /api/v1/firewall/deployments`
**Authorization:** FirewallDeveloper, FirewallAdmin
**Rate Limit:** 10 req/min per user

**Request:**
```http
POST /api/v1/firewall/deployments
Authorization: Bearer {token}
Content-Type: application/json

{
  "ruleSetName": "web-server-rules",
  "targetEnvironment": "Production",
  "strategy": "Canary",
  "targetIds": ["target-aws-prod-1", "target-aws-prod-2"],
  "validationChecks": ["ConnectivityTest", "PerformanceTest", "SecurityTest"],
  "config": {
    "canaryPercentage": 10,
    "stageWaitSeconds": 60,
    "autoRollback": true
  }
}
```

**Request Fields:**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `ruleSetName` | string | Yes | Rule set to deploy |
| `targetEnvironment` | string | Yes | Development, QA, Staging, Production |
| `strategy` | string | No | Direct, Canary, BlueGreen, Rolling, AB (default: Direct) |
| `targetIds` | array | Yes | Target firewall IDs |
| `validationChecks` | array | No | Validation checks to perform |
| `config` | object | No | Strategy-specific configuration |

**Response 202 Accepted:**
```json
{
  "deploymentId": "deploy-xyz789",
  "ruleSetName": "web-server-rules",
  "version": "1.0.0",
  "targetEnvironment": "Production",
  "strategy": "Canary",
  "status": "AwaitingApproval",
  "approvalRequired": true,
  "createdAt": "2025-11-23T12:00:00Z",
  "initiatedBy": "admin@example.com"
}
```

**Note:** Production deployments require approval before execution

---

### Get Deployment Status

Get deployment details and status.

**Endpoint:** `GET /api/v1/firewall/deployments/{id}`
**Authorization:** All roles
**Rate Limit:** 60 req/min per user

**Request:**
```http
GET /api/v1/firewall/deployments/deploy-xyz789
Authorization: Bearer {token}
```

**Response 200 OK:**
```json
{
  "deploymentId": "deploy-xyz789",
  "ruleSetName": "web-server-rules",
  "version": "1.0.0",
  "targetEnvironment": "Production",
  "strategy": "Canary",
  "status": "InProgress",
  "progress": {
    "currentStage": "Stage 1 (10%)",
    "targetsDeployed": 2,
    "totalTargets": 20,
    "percentComplete": 10
  },
  "metrics": {
    "targetsSucceeded": 2,
    "targetsFailed": 0,
    "avgDeploymentTimeMs": 28500
  },
  "validationResults": [
    {
      "type": "ConnectivityTest",
      "status": "Passed",
      "executionTimeMs": 4200
    }
  ],
  "startedAt": "2025-11-23T12:05:00Z",
  "createdAt": "2025-11-23T12:00:00Z"
}
```

---

### List Deployments

List all deployments (with filtering).

**Endpoint:** `GET /api/v1/firewall/deployments`
**Authorization:** All roles
**Rate Limit:** 60 req/min per user

**Request:**
```http
GET /api/v1/firewall/deployments?environment=Production&status=InProgress&limit=20
Authorization: Bearer {token}
```

**Query Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `environment` | string | No | Filter by environment |
| `status` | string | No | Filter by status |
| `ruleSetName` | string | No | Filter by rule set |
| `limit` | int | No | Max deployments to return (1-100) |
| `offset` | int | No | Pagination offset |

**Response 200 OK:**
```json
{
  "deployments": [
    {
      "deploymentId": "deploy-xyz789",
      "ruleSetName": "web-server-rules",
      "environment": "Production",
      "status": "InProgress",
      "strategy": "Canary",
      "createdAt": "2025-11-23T12:00:00Z"
    }
  ],
  "total": 156,
  "limit": 20,
  "offset": 0
}
```

---

### Rollback Deployment

Rollback a deployment (admin only).

**Endpoint:** `POST /api/v1/firewall/deployments/{id}/rollback`
**Authorization:** FirewallAdmin only
**Rate Limit:** 10 req/min per user

**Request:**
```http
POST /api/v1/firewall/deployments/deploy-xyz789/rollback
Authorization: Bearer {token}
Content-Type: application/json

{
  "reason": "Connectivity issues detected in production"
}
```

**Response 200 OK:**
```json
{
  "deploymentId": "deploy-xyz789",
  "status": "RolledBack",
  "rollback": {
    "reason": "Connectivity issues detected in production",
    "triggeredAt": "2025-11-23T12:10:00Z",
    "previousVersion": "0.9.0",
    "success": true,
    "durationSeconds": 8.5
  }
}
```

---

### Approve Deployment

Approve a pending deployment.

**Endpoint:** `POST /api/v1/firewall/deployments/{id}/approve`
**Authorization:** FirewallReviewer, FirewallAdmin
**Rate Limit:** 30 req/min per user

**Request:**
```http
POST /api/v1/firewall/deployments/deploy-xyz789/approve
Authorization: Bearer {token}
Content-Type: application/json

{
  "notes": "Reviewed and approved. Change looks good."
}
```

**Response 200 OK:**
```json
{
  "deploymentId": "deploy-xyz789",
  "approvalStatus": "Approved",
  "approvedBy": "security-reviewer@example.com",
  "approvedAt": "2025-11-23T12:02:00Z",
  "status": "Approved"
}
```

---

## Targets API

### Register Deployment Target

Register a firewall target for deployments.

**Endpoint:** `POST /api/v1/firewall/targets`
**Authorization:** FirewallAdmin
**Rate Limit:** 10 req/min per user

**Request:**
```http
POST /api/v1/firewall/targets
Authorization: Bearer {token}
Content-Type: application/json

{
  "name": "aws-prod-firewall-1",
  "providerType": "AWS",
  "environment": "Production",
  "region": "us-east-1",
  "providerConfig": {
    "accountId": "123456789012",
    "securityGroupId": "sg-abc123",
    "vpcId": "vpc-xyz789"
  },
  "tags": ["production", "web-tier"]
}
```

**Response 201 Created:**
```json
{
  "targetId": "target-aws-prod-1",
  "name": "aws-prod-firewall-1",
  "providerType": "AWS",
  "environment": "Production",
  "region": "us-east-1",
  "health": {
    "isHealthy": true,
    "lastCheckAt": "2025-11-23T12:00:00Z"
  },
  "enabled": true,
  "createdAt": "2025-11-23T12:00:00Z"
}
```

---

### List Targets

List all deployment targets.

**Endpoint:** `GET /api/v1/firewall/targets`
**Authorization:** All roles
**Rate Limit:** 60 req/min per user

**Request:**
```http
GET /api/v1/firewall/targets?environment=Production&enabled=true
Authorization: Bearer {token}
```

**Response 200 OK:**
```json
{
  "targets": [
    {
      "targetId": "target-aws-prod-1",
      "name": "aws-prod-firewall-1",
      "providerType": "AWS",
      "environment": "Production",
      "health": {
        "isHealthy": true
      },
      "currentVersion": "1.0.0",
      "lastDeployedAt": "2025-11-23T11:00:00Z"
    }
  ],
  "total": 45
}
```

---

### Delete Target

Delete a deployment target (admin only).

**Endpoint:** `DELETE /api/v1/firewall/targets/{id}`
**Authorization:** FirewallAdmin only
**Rate Limit:** 10 req/min per user

**Request:**
```http
DELETE /api/v1/firewall/targets/target-aws-prod-1
Authorization: Bearer {token}
```

**Response 204 No Content**

**Error Responses:**
- `409 Conflict` - Target has active deployments

---

## Validation API

### Validate RuleSet

Validate a rule set without deploying.

**Endpoint:** `POST /api/v1/firewall/rulesets/{name}/validate`
**Authorization:** All roles
**Rate Limit:** 100 req/min per user

**Request:**
```http
POST /api/v1/firewall/rulesets/web-server-rules/validate
Authorization: Bearer {token}
```

**Response 200 OK (Valid):**
```json
{
  "isValid": true,
  "ruleSetName": "web-server-rules",
  "validatedAt": "2025-11-23T12:00:00Z",
  "warnings": [
    {
      "code": "OVERLY_PERMISSIVE",
      "message": "Rule 'allow-all' allows traffic from 0.0.0.0/0 to 0.0.0.0/0",
      "recommendation": "Consider restricting source or destination addresses"
    }
  ]
}
```

**Response 200 OK (Invalid):**
```json
{
  "isValid": false,
  "errors": [
    {
      "code": "INVALID_CIDR",
      "message": "SourceAddress '192.168.1.0/33' is not a valid CIDR",
      "field": "rules[0].sourceAddress",
      "severity": "Error"
    },
    {
      "code": "PRIORITY_CONFLICT",
      "message": "Multiple rules have priority 100",
      "severity": "Error"
    }
  ],
  "validatedAt": "2025-11-23T12:00:00Z"
}
```

---

### Dry-Run Deployment

Test a deployment without actually deploying.

**Endpoint:** `POST /api/v1/firewall/deployments/dry-run`
**Authorization:** FirewallDeveloper, FirewallAdmin
**Rate Limit:** 50 req/min per user

**Request:**
```http
POST /api/v1/firewall/deployments/dry-run
Authorization: Bearer {token}
Content-Type: application/json

{
  "ruleSetName": "web-server-rules",
  "targetEnvironment": "Production",
  "strategy": "Canary",
  "targetIds": ["target-aws-prod-1"]
}
```

**Response 200 OK:**
```json
{
  "success": true,
  "ruleSetName": "web-server-rules",
  "version": "1.0.0",
  "targetEnvironment": "Production",
  "strategy": "Canary",
  "validationResults": {
    "ruleValidation": "Passed",
    "conflictCheck": "Passed",
    "securityCheck": "Passed with warnings",
    "targetHealthCheck": "Passed"
  },
  "estimatedDuration": "8-10 minutes",
  "warnings": [
    "1 overly permissive rule detected"
  ],
  "testedAt": "2025-11-23T12:00:00Z"
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
    "message": "Rule validation failed",
    "details": [
      {
        "field": "rules[0].sourceAddress",
        "message": "Invalid CIDR notation"
      }
    ],
    "traceId": "trace-abc123",
    "timestamp": "2025-11-23T12:00:00Z"
  }
}
```

### Common Error Codes

| HTTP Status | Error Code | Description |
|-------------|------------|-------------|
| 400 | `VALIDATION_ERROR` | Request validation failed |
| 400 | `INVALID_RULE` | Firewall rule validation failed |
| 401 | `UNAUTHORIZED` | Missing or invalid JWT token |
| 403 | `FORBIDDEN` | Insufficient permissions |
| 404 | `NOT_FOUND` | Resource not found |
| 409 | `CONFLICT` | Resource conflict (duplicate, in use) |
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
X-RateLimit-Limit: 100
X-RateLimit-Remaining: 85
X-RateLimit-Reset: 1700144400
Retry-After: 60
```

### Rate Limits by Endpoint

| Endpoint | Limit | Window |
|----------|-------|--------|
| `POST /api/v1/firewall/rulesets` | 10 req | 1 minute |
| `GET /api/v1/firewall/rulesets` | 60 req | 1 minute |
| `POST /api/v1/firewall/rulesets/{name}/rules` | 50 req | 1 minute |
| `POST /api/v1/firewall/deployments` | 10 req | 1 minute |
| `GET /api/v1/firewall/deployments/{id}` | 60 req | 1 minute |
| `POST /api/v1/firewall/deployments/{id}/rollback` | 10 req | 1 minute |
| `POST /api/v1/firewall/rulesets/{name}/validate` | 100 req | 1 minute |

---

**API Version:** 1.0.0
**Last Updated:** 2025-11-23
**Support:** api-support@example.com
