# Schema Migration Orchestrator API Reference

**Version:** 1.0.0
**Base URL:** `https://api.example.com/api/v1`
**Authentication:** JWT Bearer Token
**Content-Type:** `application/json`

---

## Table of Contents

1. [Authentication](#authentication)
2. [Migrations API](#migrations-api)
3. [Databases API](#databases-api)
4. [Executions API](#executions-api)
5. [Approvals API](#approvals-api)
6. [Validation API](#validation-api)
7. [Error Responses](#error-responses)

---

## Authentication

All API endpoints require JWT authentication (except `/health`).

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

---

## Migrations API

### Create Migration

Create a new migration definition.

**Endpoint:** `POST /api/v1/migrations`
**Authorization:** Developer, Admin
**Rate Limit:** 10 req/min per user

**Request:**
```http
POST /api/v1/migrations
Authorization: Bearer {token}
Content-Type: application/json

{
  "name": "add_users_email_index",
  "description": "Add index on users.email for faster lookups",
  "targetDatabaseId": "prod-db-cluster-1",
  "migrationScript": "CREATE INDEX CONCURRENTLY idx_users_email ON users(email);",
  "rollbackScript": "DROP INDEX CONCURRENTLY IF EXISTS idx_users_email;",
  "strategy": "Phased",
  "riskLevel": "Low",
  "tags": ["index", "performance"]
}
```

**Response 201 Created:**
```json
{
  "migrationId": "mig-7f8a9b1c-2d3e-4f5g-6h7i-8j9k0l1m2n3o",
  "name": "add_users_email_index",
  "status": "Draft",
  "riskLevel": "Low",
  "requiresApproval": false,
  "createdAt": "2025-11-23T12:00:00Z"
}
```

---

### List Migrations

**Endpoint:** `GET /api/v1/migrations`

**Query Parameters:**
- `status` - Filter by status (Draft, PendingApproval, Approved, etc.)
- `database` - Filter by database ID
- `limit` - Max migrations to return (default: 20)
- `offset` - Pagination offset

**Response 200 OK:**
```json
{
  "migrations": [
    {
      "migrationId": "mig-123",
      "name": "add_users_email_index",
      "status": "Draft",
      "targetDatabase": "prod-db-cluster-1",
      "createdAt": "2025-11-23T12:00:00Z"
    }
  ],
  "total": 42,
  "limit": 20,
  "offset": 0
}
```

---

### Execute Migration

Execute a migration with selected strategy.

**Endpoint:** `POST /api/v1/migrations/{id}/execute`
**Authorization:** DBA, Admin (production); Developer (dev/QA)

**Request:**
```http
POST /api/v1/migrations/mig-123/execute
Authorization: Bearer {token}
Content-Type: application/json

{
  "environment": "Production",
  "strategy": "Phased",
  "dryRun": false,
  "phases": ["replica-1", "replica-2", "replica-3", "master"]
}
```

**Response 202 Accepted:**
```json
{
  "executionId": "exec-abc123",
  "migrationId": "mig-123",
  "status": "Running",
  "startedAt": "2025-11-23T12:00:00Z",
  "estimatedDuration": 120
}
```

---

## Databases API

### Register Database

**Endpoint:** `POST /api/v1/databases`

**Request:**
```json
{
  "name": "prod-db-cluster-1",
  "type": "PostgreSQL",
  "environment": "Production",
  "masterConnectionString": "Host=master.db.example.com;Database=app;Username=migrator;Password=***",
  "replicas": [
    {
      "replicaId": "replica-1",
      "connectionString": "Host=replica1.db.example.com;Database=app;Username=migrator;Password=***",
      "role": "AsyncReplica"
    }
  ]
}
```

**Response 201 Created:**
```json
{
  "databaseId": "db-456",
  "name": "prod-db-cluster-1",
  "type": "PostgreSQL",
  "environment": "Production",
  "health": {
    "isHealthy": true,
    "activeConnections": 15,
    "replicationLag": 2.5
  },
  "createdAt": "2025-11-23T12:00:00Z"
}
```

---

## Executions API

### Get Execution Status

**Endpoint:** `GET /api/v1/migrations/{migrationId}/executions/{executionId}`

**Response 200 OK:**
```json
{
  "executionId": "exec-abc123",
  "migrationId": "mig-123",
  "status": "Running",
  "startedAt": "2025-11-23T12:00:00Z",
  "currentPhase": "replica-2",
  "progress": 50,
  "performanceMetrics": {
    "avgQueryLatencyMs": 12.5,
    "p99QueryLatencyMs": 45.2,
    "replicationLagSeconds": 3.2
  }
}
```

---

### Rollback Migration

**Endpoint:** `POST /api/v1/migrations/{id}/executions/{executionId}/rollback`

**Request:**
```json
{
  "reason": "Performance degradation detected: query latency increased by 60%"
}
```

**Response 200 OK:**
```json
{
  "executionId": "exec-abc123",
  "status": "RolledBack",
  "rollbackCompletedAt": "2025-11-23T12:05:30Z",
  "rollbackDuration": 28.5
}
```

---

## Approvals API

### Submit for Approval

**Endpoint:** `POST /api/v1/migrations/{id}/submit-approval`

**Response 201 Created:**
```json
{
  "approvalId": "appr-789",
  "migrationId": "mig-123",
  "status": "Pending",
  "submittedAt": "2025-11-23T12:00:00Z",
  "expiresAt": "2025-11-30T12:00:00Z"
}
```

---

### Approve Migration

**Endpoint:** `POST /api/v1/migrations/{id}/approve`
**Authorization:** Admin only

**Request:**
```json
{
  "notes": "Reviewed and approved. Performance impact minimal."
}
```

**Response 200 OK:**
```json
{
  "approvalId": "appr-789",
  "status": "Approved",
  "reviewedBy": "admin@example.com",
  "reviewedAt": "2025-11-23T13:00:00Z"
}
```

---

## Validation API

### Validate Migration

**Endpoint:** `POST /api/v1/migrations/{id}/validate`

**Request:**
```json
{
  "dryRun": true
}
```

**Response 200 OK:**
```json
{
  "isValid": true,
  "errors": [],
  "warnings": [
    {
      "message": "Index creation may take 2-3 minutes on large table",
      "recommendation": "Consider running during low-traffic hours"
    }
  ],
  "estimatedDuration": 180,
  "dependencyCheck": {
    "hasConflicts": false,
    "affectedObjects": ["users table", "user_email_idx (will replace)"]
  }
}
```

---

## Error Responses

**Error Format:**
```json
{
  "error": {
    "code": "VALIDATION_ERROR",
    "message": "Migration validation failed",
    "details": [
      {
        "field": "migrationScript",
        "message": "SQL syntax error at line 1"
      }
    ],
    "traceId": "trace-xyz789",
    "timestamp": "2025-11-23T15:00:00Z"
  }
}
```

**Common Error Codes:**
- `400 VALIDATION_ERROR` - Request validation failed
- `401 UNAUTHORIZED` - Missing or invalid JWT token
- `403 FORBIDDEN` - Insufficient permissions
- `404 NOT_FOUND` - Resource not found
- `409 CONFLICT` - Migration already in progress
- `429 RATE_LIMIT_EXCEEDED` - Too many requests
- `500 INTERNAL_ERROR` - Internal server error

---

**API Version:** 1.0.0
**Last Updated:** 2025-11-23
