# Kubernetes Operator Manager API Reference

**Version:** 1.0.0
**Base URL:** `https://api.example.com/api/v1`
**Authentication:** JWT Bearer Token
**Content-Type:** `application/json`

---

## Table of Contents

1. [Authentication](#authentication)
2. [Operators API](#operators-api)
3. [Deployments API](#deployments-api)
4. [Clusters API](#clusters-api)
5. [CRDs API](#crds-api)
6. [Health API](#health-api)
7. [Approvals API](#approvals-api)
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
GET /api/v1/operators
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

---

## Operators API

### Register Operator

Register a new Kubernetes operator with Helm chart details.

**Endpoint:** `POST /api/v1/operators`
**Authorization:** Operator, Admin
**Rate Limit:** 10 req/min per user

**Request:**
```http
POST /api/v1/operators
Authorization: Bearer {token}
Content-Type: application/json

{
  "name": "cert-manager",
  "description": "X.509 certificate management for Kubernetes",
  "namespace": "cert-manager",
  "chartRepository": "https://charts.jetstack.io",
  "chartName": "cert-manager",
  "currentVersion": "v1.13.0",
  "crdNames": [
    "certificates.cert-manager.io",
    "issuers.cert-manager.io",
    "clusterissuers.cert-manager.io"
  ],
  "labels": {
    "category": "security",
    "criticality": "high"
  },
  "defaultValues": {
    "installCRDs": true,
    "replicaCount": 3
  }
}
```

**Request Fields:**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `name` | string | Yes | Operator name (lowercase, hyphens) |
| `description` | string | No | Human-readable description |
| `namespace` | string | Yes | Kubernetes namespace |
| `chartRepository` | string | Yes | Helm chart repository URL |
| `chartName` | string | Yes | Chart name within repository |
| `currentVersion` | string | No | Current deployed version |
| `crdNames` | array | No | Associated CRD names |
| `labels` | object | No | Key-value labels |
| `defaultValues` | object | No | Default Helm values |

**Response 201 Created:**
```json
{
  "operatorId": "op-7f8a9b1c-2d3e-4f5g-6h7i-8j9k0l1m2n3o",
  "name": "cert-manager",
  "namespace": "cert-manager",
  "currentVersion": "v1.13.0",
  "latestVersion": "v1.14.0",
  "deployedClusterCount": 0,
  "createdAt": "2025-11-23T12:00:00Z"
}
```

**Error Responses:**
- `400 Bad Request` - Invalid operator name or chart repository
- `409 Conflict` - Operator already exists
- `404 Not Found` - Chart repository unreachable

---

### List Operators

List all registered operators.

**Endpoint:** `GET /api/v1/operators`
**Authorization:** All roles
**Rate Limit:** 60 req/min per user

**Request:**
```http
GET /api/v1/operators?namespace=cert-manager&limit=20&offset=0
Authorization: Bearer {token}
```

**Query Parameters:**

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `namespace` | string | No | - | Filter by namespace |
| `label` | string | No | - | Filter by label (key=value) |
| `hasUpdate` | boolean | No | - | Filter operators with updates available |
| `limit` | int | No | 20 | Max operators to return (1-100) |
| `offset` | int | No | 0 | Pagination offset |

**Response 200 OK:**
```json
{
  "operators": [
    {
      "operatorId": "op-123",
      "name": "cert-manager",
      "namespace": "cert-manager",
      "currentVersion": "v1.13.0",
      "latestVersion": "v1.14.0",
      "hasUpdate": true,
      "deployedClusterCount": 5,
      "createdAt": "2025-11-23T12:00:00Z"
    },
    {
      "operatorId": "op-456",
      "name": "istio-operator",
      "namespace": "istio-system",
      "currentVersion": "v1.20.0",
      "latestVersion": "v1.20.0",
      "hasUpdate": false,
      "deployedClusterCount": 3,
      "createdAt": "2025-11-22T10:00:00Z"
    }
  ],
  "total": 12,
  "limit": 20,
  "offset": 0
}
```

---

### Get Operator Details

Get detailed information about a specific operator.

**Endpoint:** `GET /api/v1/operators/{name}`
**Authorization:** All roles
**Rate Limit:** 60 req/min per user

**Request:**
```http
GET /api/v1/operators/cert-manager
Authorization: Bearer {token}
```

**Response 200 OK:**
```json
{
  "operatorId": "op-123",
  "name": "cert-manager",
  "description": "X.509 certificate management for Kubernetes",
  "namespace": "cert-manager",
  "chartRepository": "https://charts.jetstack.io",
  "chartName": "cert-manager",
  "currentVersion": "v1.13.0",
  "latestVersion": "v1.14.0",
  "hasUpdate": true,
  "crdNames": [
    "certificates.cert-manager.io",
    "issuers.cert-manager.io"
  ],
  "deployedClusterCount": 5,
  "deployedClusters": [
    {
      "clusterName": "prod-us-east",
      "version": "v1.13.0",
      "health": "Healthy",
      "deployedAt": "2025-11-20T10:00:00Z"
    }
  ],
  "createdAt": "2025-11-23T12:00:00Z",
  "updatedAt": "2025-11-23T12:00:00Z"
}
```

---

### Update Operator

Update operator metadata and configuration.

**Endpoint:** `PUT /api/v1/operators/{name}`
**Authorization:** Operator, Admin
**Rate Limit:** 10 req/min per user

**Request:**
```http
PUT /api/v1/operators/cert-manager
Authorization: Bearer {token}
Content-Type: application/json

{
  "description": "Updated description",
  "labels": {
    "category": "security",
    "criticality": "critical"
  },
  "defaultValues": {
    "installCRDs": true,
    "replicaCount": 5
  }
}
```

**Response 200 OK:**
```json
{
  "operatorId": "op-123",
  "name": "cert-manager",
  "description": "Updated description",
  "updatedAt": "2025-11-23T13:00:00Z"
}
```

**Note:** Cannot update `name`, `namespace`, or `chartRepository` after creation.

---

### Delete Operator

Delete an operator registration (admin only).

**Endpoint:** `DELETE /api/v1/operators/{name}`
**Authorization:** Admin only
**Rate Limit:** 10 req/min per user

**Request:**
```http
DELETE /api/v1/operators/cert-manager
Authorization: Bearer {token}
```

**Response 204 No Content**

**Error Responses:**
- `409 Conflict` - Operator is deployed to clusters (undeploy first)

---

### Get Operator Versions

List available versions for an operator from Helm repository.

**Endpoint:** `GET /api/v1/operators/{name}/versions`
**Authorization:** All roles
**Rate Limit:** 30 req/min per user

**Request:**
```http
GET /api/v1/operators/cert-manager/versions
Authorization: Bearer {token}
```

**Response 200 OK:**
```json
{
  "operatorName": "cert-manager",
  "currentVersion": "v1.13.0",
  "versions": [
    {
      "version": "v1.14.0",
      "releaseDate": "2025-11-15T00:00:00Z",
      "isLatest": true,
      "changelogUrl": "https://github.com/cert-manager/cert-manager/releases/tag/v1.14.0"
    },
    {
      "version": "v1.13.0",
      "releaseDate": "2025-10-01T00:00:00Z",
      "isLatest": false,
      "changelogUrl": "https://github.com/cert-manager/cert-manager/releases/tag/v1.13.0"
    }
  ]
}
```

---

## Deployments API

### Create Deployment

Deploy an operator to clusters with a specific strategy.

**Endpoint:** `POST /api/v1/deployments`
**Authorization:** Operator, Admin
**Rate Limit:** 10 req/min per user

**Request:**
```http
POST /api/v1/deployments
Authorization: Bearer {token}
Content-Type: application/json

{
  "operatorName": "cert-manager",
  "targetVersion": "v1.14.0",
  "strategy": "Canary",
  "targetClusters": [
    "dev-1",
    "dev-2",
    "staging",
    "prod-us-east",
    "prod-eu-west"
  ],
  "strategyConfig": {
    "canaryConfig": {
      "initialPercentage": 10,
      "incrementPercentage": 20,
      "evaluationPeriod": "PT5M",
      "successThreshold": 0.95,
      "autoRollbackEnabled": true
    }
  },
  "helmValues": {
    "replicaCount": 3,
    "resources": {
      "limits": {
        "cpu": "500m",
        "memory": "512Mi"
      }
    }
  },
  "autoRollbackEnabled": true
}
```

**Request Fields:**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `operatorName` | string | Yes | Operator to deploy |
| `targetVersion` | string | Yes | Target operator version |
| `strategy` | string | Yes | `Direct`, `Canary`, `BlueGreen`, `Rolling` |
| `targetClusters` | array | Yes | Cluster names to deploy to |
| `strategyConfig` | object | No | Strategy-specific configuration |
| `helmValues` | object | No | Helm values override |
| `autoRollbackEnabled` | boolean | No | Enable automatic rollback (default: true) |

**Response 202 Accepted:**
```json
{
  "deploymentId": "deploy-abc123",
  "operatorName": "cert-manager",
  "targetVersion": "v1.14.0",
  "previousVersion": "v1.13.0",
  "strategy": "Canary",
  "status": "Planning",
  "targetClusters": [
    "dev-1",
    "dev-2",
    "staging",
    "prod-us-east",
    "prod-eu-west"
  ],
  "initiatedBy": "admin@example.com",
  "approvalStatus": "Pending",
  "startedAt": "2025-11-23T14:00:00Z"
}
```

**Response Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `deploymentId` | string | Unique deployment ID (GUID) |
| `status` | string | `Planning`, `Deploying`, `Validating`, `Completed`, `Failed`, `RollingBack`, `RolledBack` |
| `approvalStatus` | string | `NotRequired`, `Pending`, `Approved`, `Rejected` |

**Error Responses:**
- `400 Bad Request` - Invalid configuration or version
- `404 Not Found` - Operator or cluster not found
- `409 Conflict` - Active deployment already in progress

---

### List Deployments

List all operator deployments.

**Endpoint:** `GET /api/v1/deployments`
**Authorization:** All roles
**Rate Limit:** 60 req/min per user

**Request:**
```http
GET /api/v1/deployments?operatorName=cert-manager&status=Deploying&limit=20
Authorization: Bearer {token}
```

**Query Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `operatorName` | string | No | Filter by operator |
| `status` | string | No | Filter by status |
| `initiatedBy` | string | No | Filter by user |
| `strategy` | string | No | Filter by strategy |
| `limit` | int | No | Max deployments (default: 20) |
| `offset` | int | No | Pagination offset |

**Response 200 OK:**
```json
{
  "deployments": [
    {
      "deploymentId": "deploy-abc123",
      "operatorName": "cert-manager",
      "targetVersion": "v1.14.0",
      "strategy": "Canary",
      "status": "Deploying",
      "targetClusters": ["dev-1", "dev-2", "staging", "prod-us-east"],
      "successfulClusters": ["dev-1", "dev-2"],
      "failedClusters": [],
      "successRate": 0.5,
      "startedAt": "2025-11-23T14:00:00Z",
      "estimatedCompletionAt": "2025-11-23T14:30:00Z"
    }
  ],
  "total": 45,
  "limit": 20,
  "offset": 0
}
```

---

### Get Deployment Details

Get detailed information about a specific deployment.

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
  "operatorName": "cert-manager",
  "targetVersion": "v1.14.0",
  "previousVersion": "v1.13.0",
  "strategy": "Canary",
  "status": "Deploying",
  "targetClusters": ["dev-1", "dev-2", "staging", "prod-us-east", "prod-eu-west"],
  "clusterStatuses": [
    {
      "clusterName": "dev-1",
      "status": "Completed",
      "startedAt": "2025-11-23T14:00:00Z",
      "completedAt": "2025-11-23T14:05:00Z",
      "healthCheck": {
        "overallHealth": "Healthy",
        "controllerPodHealth": {
          "readyPods": 3,
          "expectedPods": 3
        }
      }
    },
    {
      "clusterName": "dev-2",
      "status": "Deploying",
      "startedAt": "2025-11-23T14:07:00Z"
    },
    {
      "clusterName": "staging",
      "status": "Planning"
    }
  ],
  "initiatedBy": "admin@example.com",
  "approvalStatus": "Approved",
  "approvedBy": "manager@example.com",
  "approvedAt": "2025-11-23T13:55:00Z",
  "startedAt": "2025-11-23T14:00:00Z",
  "estimatedCompletionAt": "2025-11-23T14:30:00Z"
}
```

---

### Rollback Deployment

Manually trigger rollback to previous operator version.

**Endpoint:** `POST /api/v1/deployments/{id}/rollback`
**Authorization:** Admin only
**Rate Limit:** 10 req/min per user

**Request:**
```http
POST /api/v1/deployments/deploy-abc123/rollback
Authorization: Bearer {token}
Content-Type: application/json

{
  "reason": "High error rate detected in production clusters",
  "targetClusters": ["prod-us-east", "prod-eu-west"]
}
```

**Request Fields:**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `reason` | string | Yes | Rollback reason for audit trail |
| `targetClusters` | array | No | Specific clusters to rollback (default: all) |

**Response 202 Accepted:**
```json
{
  "deploymentId": "deploy-abc123",
  "status": "RollingBack",
  "rollbackReason": "High error rate detected in production clusters",
  "targetClusters": ["prod-us-east", "prod-eu-west"],
  "rolledBackAt": "2025-11-23T14:15:00Z"
}
```

**Error Responses:**
- `404 Not Found` - Deployment not found
- `409 Conflict` - Deployment not in rollback-eligible state

---

### Cancel Deployment

Cancel an in-progress deployment.

**Endpoint:** `DELETE /api/v1/deployments/{id}`
**Authorization:** Operator, Admin
**Rate Limit:** 10 req/min per user

**Request:**
```http
DELETE /api/v1/deployments/deploy-abc123
Authorization: Bearer {token}
```

**Response 202 Accepted:**
```json
{
  "deploymentId": "deploy-abc123",
  "status": "Cancelled",
  "message": "Deployment cancellation initiated. In-progress clusters will complete, pending clusters will be skipped."
}
```

---

## Clusters API

### Register Cluster

Register a new Kubernetes cluster.

**Endpoint:** `POST /api/v1/clusters`
**Authorization:** Admin only
**Rate Limit:** 5 req/min per user

**Request:**
```http
POST /api/v1/clusters
Authorization: Bearer {token}
Content-Type: application/json

{
  "name": "prod-us-east",
  "description": "Production cluster in US East region",
  "environment": "Production",
  "kubeconfig": "YXBpVmVyc2lvbjogdjEKY2x1c3RlcnM6Ci0gY2x1c3Rlcjo...",
  "labels": {
    "region": "us-east-1",
    "provider": "aws"
  }
}
```

**Request Fields:**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `name` | string | Yes | Cluster name (lowercase, hyphens) |
| `description` | string | No | Human-readable description |
| `environment` | string | Yes | `Development`, `Staging`, `Production` |
| `kubeconfig` | string | Yes | Base64-encoded kubeconfig |
| `labels` | object | No | Key-value labels |

**Response 201 Created:**
```json
{
  "clusterId": "cluster-xyz789",
  "name": "prod-us-east",
  "environment": "Production",
  "apiServerUrl": "https://prod-us-east.k8s.example.com",
  "kubernetesVersion": "v1.28.3",
  "nodeCount": 15,
  "healthStatus": "Healthy",
  "registeredAt": "2025-11-23T12:00:00Z"
}
```

**Error Responses:**
- `400 Bad Request` - Invalid kubeconfig or cluster unreachable
- `409 Conflict` - Cluster already registered

---

### List Clusters

List all registered clusters.

**Endpoint:** `GET /api/v1/clusters`
**Authorization:** All roles
**Rate Limit:** 60 req/min per user

**Request:**
```http
GET /api/v1/clusters?environment=Production&healthStatus=Healthy
Authorization: Bearer {token}
```

**Query Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `environment` | string | No | Filter by environment |
| `healthStatus` | string | No | Filter by health status |
| `label` | string | No | Filter by label (key=value) |

**Response 200 OK:**
```json
{
  "clusters": [
    {
      "clusterId": "cluster-xyz789",
      "name": "prod-us-east",
      "environment": "Production",
      "kubernetesVersion": "v1.28.3",
      "nodeCount": 15,
      "healthStatus": "Healthy",
      "deployedOperators": [
        {
          "operatorName": "cert-manager",
          "version": "v1.13.0",
          "health": "Healthy"
        }
      ],
      "lastHealthCheckAt": "2025-11-23T14:00:00Z"
    }
  ],
  "total": 8
}
```

---

### Get Cluster Details

Get detailed information about a specific cluster.

**Endpoint:** `GET /api/v1/clusters/{name}`
**Authorization:** All roles
**Rate Limit:** 60 req/min per user

**Request:**
```http
GET /api/v1/clusters/prod-us-east
Authorization: Bearer {token}
```

**Response 200 OK:**
```json
{
  "clusterId": "cluster-xyz789",
  "name": "prod-us-east",
  "description": "Production cluster in US East region",
  "environment": "Production",
  "apiServerUrl": "https://prod-us-east.k8s.example.com",
  "kubernetesVersion": "v1.28.3",
  "nodeCount": 15,
  "healthStatus": "Healthy",
  "deployedOperators": [
    {
      "operatorName": "cert-manager",
      "version": "v1.13.0",
      "namespace": "cert-manager",
      "deployedAt": "2025-11-20T10:00:00Z",
      "healthStatus": {
        "overallHealth": "Healthy",
        "controllerPodHealth": {
          "readyPods": 3,
          "expectedPods": 3
        }
      }
    }
  ],
  "labels": {
    "region": "us-east-1",
    "provider": "aws"
  },
  "registeredAt": "2025-11-23T12:00:00Z",
  "lastHealthCheckAt": "2025-11-23T14:00:00Z"
}
```

---

### Get Cluster Health

Get real-time health status of a cluster.

**Endpoint:** `GET /api/v1/clusters/{name}/health`
**Authorization:** All roles
**Rate Limit:** 60 req/min per user

**Request:**
```http
GET /api/v1/clusters/prod-us-east/health
Authorization: Bearer {token}
```

**Response 200 OK:**
```json
{
  "clusterName": "prod-us-east",
  "healthStatus": "Healthy",
  "kubernetesVersion": "v1.28.3",
  "nodeCount": 15,
  "nodeHealth": {
    "ready": 15,
    "notReady": 0
  },
  "apiServerReachable": true,
  "apiServerLatencyMs": 45,
  "deployedOperators": [
    {
      "operatorName": "cert-manager",
      "overallHealth": "Healthy",
      "controllerPodHealth": {
        "readyPods": 3,
        "expectedPods": 3,
        "crashLoopPods": 0
      },
      "webhookHealth": {
        "isReachable": true,
        "latencyMs": 150
      },
      "crdReconciliationHealth": {
        "activeReconciliations": 42,
        "staleReconciliations": 0,
        "errorRate": 0.01
      }
    }
  ],
  "lastCheckedAt": "2025-11-23T14:00:00Z"
}
```

---

### Delete Cluster

Delete a cluster registration (admin only).

**Endpoint:** `DELETE /api/v1/clusters/{name}`
**Authorization:** Admin only
**Rate Limit:** 5 req/min per user

**Request:**
```http
DELETE /api/v1/clusters/prod-us-east
Authorization: Bearer {token}
```

**Response 204 No Content**

**Error Responses:**
- `409 Conflict` - Cluster has deployed operators (undeploy first)

---

## CRDs API

### List CRDs

List all tracked Custom Resource Definitions.

**Endpoint:** `GET /api/v1/crds`
**Authorization:** All roles
**Rate Limit:** 60 req/min per user

**Request:**
```http
GET /api/v1/crds?operatorName=cert-manager
Authorization: Bearer {token}
```

**Query Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `operatorName` | string | No | Filter by operator |
| `status` | string | No | Filter by status (`Active`, `Deprecated`) |

**Response 200 OK:**
```json
{
  "crds": [
    {
      "crdId": "crd-123",
      "name": "certificates.cert-manager.io",
      "group": "cert-manager.io",
      "versions": ["v1", "v1alpha1"],
      "scope": "Namespaced",
      "operatorName": "cert-manager",
      "operatorVersion": "v1.13.0",
      "status": "Active",
      "approvalStatus": "Approved",
      "createdAt": "2025-11-20T10:00:00Z"
    }
  ],
  "total": 8
}
```

---

### Get CRD Details

Get detailed information about a CRD.

**Endpoint:** `GET /api/v1/crds/{name}`
**Authorization:** All roles
**Rate Limit:** 60 req/min per user

**Request:**
```http
GET /api/v1/crds/certificates.cert-manager.io
Authorization: Bearer {token}
```

**Response 200 OK:**
```json
{
  "crdId": "crd-123",
  "name": "certificates.cert-manager.io",
  "group": "cert-manager.io",
  "versions": ["v1", "v1alpha1"],
  "scope": "Namespaced",
  "operatorName": "cert-manager",
  "operatorVersion": "v1.13.0",
  "schemaDefinition": "{...}",
  "schemaVersion": "1.0",
  "status": "Active",
  "approvalStatus": "Approved",
  "approvedBy": "admin@example.com",
  "approvedAt": "2025-11-20T09:00:00Z",
  "schemaHistory": [
    {
      "changeType": "Modified",
      "fieldPath": "spec.dnsNames",
      "newType": "array",
      "isBreaking": false,
      "operatorVersion": "v1.13.0",
      "changedAt": "2025-11-20T10:00:00Z"
    }
  ],
  "createdAt": "2025-11-20T10:00:00Z"
}
```

---

### Validate CRD Compatibility

Validate CRD schema compatibility between versions.

**Endpoint:** `POST /api/v1/crds/{name}/validate`
**Authorization:** Operator, Admin
**Rate Limit:** 30 req/min per user

**Request:**
```http
POST /api/v1/crds/certificates.cert-manager.io/validate
Authorization: Bearer {token}
Content-Type: application/json

{
  "newSchemaDefinition": "{...}",
  "newOperatorVersion": "v1.14.0"
}
```

**Response 200 OK:**
```json
{
  "isCompatible": false,
  "breakingChanges": [
    {
      "changeType": "Removed",
      "fieldPath": "spec.secretName",
      "description": "Required field removed",
      "severity": "Critical"
    }
  ],
  "nonBreakingChanges": [
    {
      "changeType": "Added",
      "fieldPath": "spec.emailAddresses",
      "description": "Optional field added"
    }
  ],
  "requiresApproval": true,
  "validatedAt": "2025-11-23T14:00:00Z"
}
```

---

### Approve CRD

Approve a CRD for production deployment (admin only).

**Endpoint:** `POST /api/v1/crds/{name}/approve`
**Authorization:** Admin only
**Rate Limit:** 10 req/min per user

**Request:**
```http
POST /api/v1/crds/certificates.cert-manager.io/approve
Authorization: Bearer {token}
Content-Type: application/json

{
  "notes": "Breaking changes reviewed and approved for v1.14.0 upgrade"
}
```

**Response 200 OK:**
```json
{
  "crdId": "crd-123",
  "name": "certificates.cert-manager.io",
  "approvalStatus": "Approved",
  "approvedBy": "admin@example.com",
  "approvedAt": "2025-11-23T14:05:00Z"
}
```

---

## Health API

### Get Operator Health

Get health status for an operator across all clusters.

**Endpoint:** `GET /api/v1/operators/{name}/health`
**Authorization:** All roles
**Rate Limit:** 60 req/min per user

**Request:**
```http
GET /api/v1/operators/cert-manager/health
Authorization: Bearer {token}
```

**Response 200 OK:**
```json
{
  "operatorName": "cert-manager",
  "overallHealth": "Healthy",
  "healthyClusters": 4,
  "unhealthyClusters": 0,
  "degradedClusters": 1,
  "clusters": [
    {
      "clusterName": "prod-us-east",
      "overallHealth": "Healthy",
      "controllerPodHealth": {
        "readyPods": 3,
        "expectedPods": 3,
        "isHealthy": true
      },
      "webhookHealth": {
        "isReachable": true,
        "latencyMs": 150,
        "isHealthy": true
      },
      "crdReconciliationHealth": {
        "activeReconciliations": 42,
        "errorRate": 0.01,
        "isHealthy": true
      },
      "lastCheckedAt": "2025-11-23T14:00:00Z"
    }
  ]
}
```

---

### Get Cluster Operator Health

Get health status for a specific operator in a cluster.

**Endpoint:** `GET /api/v1/clusters/{clusterName}/operators/{operatorName}/health`
**Authorization:** All roles
**Rate Limit:** 60 req/min per user

**Request:**
```http
GET /api/v1/clusters/prod-us-east/operators/cert-manager/health
Authorization: Bearer {token}
```

**Response 200 OK:**
```json
{
  "operatorName": "cert-manager",
  "clusterName": "prod-us-east",
  "overallHealth": "Healthy",
  "controllerPodHealth": {
    "readyPods": 3,
    "expectedPods": 3,
    "crashLoopPods": 0,
    "averageRestarts": 0.3,
    "cpuUsagePercent": 15.2,
    "memoryUsagePercent": 42.8,
    "isHealthy": true
  },
  "webhookHealth": {
    "endpointUrl": "https://cert-manager-webhook.cert-manager.svc",
    "isReachable": true,
    "latencyMs": 150,
    "certificateExpiresAt": "2026-11-23T00:00:00Z",
    "isHealthy": true
  },
  "crdReconciliationHealth": {
    "activeReconciliations": 42,
    "staleReconciliations": 0,
    "errorRate": 0.01,
    "averageReconciliationMs": 250,
    "lastReconciliationAt": "2025-11-23T13:59:30Z",
    "isHealthy": true
  },
  "consecutiveFailures": 0,
  "lastCheckedAt": "2025-11-23T14:00:00Z"
}
```

---

## Approvals API

### List Pending Approvals

List all pending approval requests.

**Endpoint:** `GET /api/v1/approvals`
**Authorization:** Admin, Environment Owner
**Rate Limit:** 60 req/min per user

**Request:**
```http
GET /api/v1/approvals?status=Pending
Authorization: Bearer {token}
```

**Response 200 OK:**
```json
{
  "approvals": [
    {
      "approvalId": "approval-123",
      "type": "OperatorDeployment",
      "deploymentId": "deploy-abc123",
      "operatorName": "cert-manager",
      "targetVersion": "v1.14.0",
      "environment": "Production",
      "status": "Pending",
      "initiatedBy": "operator@example.com",
      "createdAt": "2025-11-23T13:50:00Z"
    }
  ],
  "total": 3
}
```

---

### Approve Deployment

Approve a deployment for execution (admin only).

**Endpoint:** `POST /api/v1/approvals/{id}/approve`
**Authorization:** Admin, Environment Owner
**Rate Limit:** 10 req/min per user

**Request:**
```http
POST /api/v1/approvals/approval-123/approve
Authorization: Bearer {token}
Content-Type: application/json

{
  "notes": "Reviewed and approved for production deployment"
}
```

**Response 200 OK:**
```json
{
  "approvalId": "approval-123",
  "status": "Approved",
  "approvedBy": "admin@example.com",
  "approvedAt": "2025-11-23T14:00:00Z"
}
```

---

### Reject Approval

Reject a deployment approval (admin only).

**Endpoint:** `POST /api/v1/approvals/{id}/reject`
**Authorization:** Admin, Environment Owner
**Rate Limit:** 10 req/min per user

**Request:**
```http
POST /api/v1/approvals/approval-123/reject
Authorization: Bearer {token}
Content-Type: application/json

{
  "reason": "CRD breaking changes require more testing"
}
```

**Response 200 OK:**
```json
{
  "approvalId": "approval-123",
  "status": "Rejected",
  "rejectedBy": "admin@example.com",
  "rejectedAt": "2025-11-23T14:00:00Z",
  "reason": "CRD breaking changes require more testing"
}
```

---

## Error Responses

All error responses follow a consistent format:

### Error Response Format

```json
{
  "error": {
    "code": "DEPLOYMENT_FAILED",
    "message": "Operator deployment failed",
    "details": [
      {
        "cluster": "prod-us-east",
        "message": "Controller pods in crash loop"
      }
    ],
    "traceId": "trace-xyz789",
    "timestamp": "2025-11-23T14:00:00Z"
  }
}
```

### Common Error Codes

| HTTP Status | Error Code | Description |
|-------------|------------|-------------|
| 400 | `VALIDATION_ERROR` | Request validation failed |
| 400 | `INVALID_KUBECONFIG` | Kubeconfig is invalid or malformed |
| 400 | `CRD_INCOMPATIBLE` | CRD schema incompatible |
| 401 | `UNAUTHORIZED` | Missing or invalid JWT token |
| 403 | `FORBIDDEN` | Insufficient permissions |
| 404 | `OPERATOR_NOT_FOUND` | Operator not found |
| 404 | `CLUSTER_NOT_FOUND` | Cluster not found |
| 404 | `DEPLOYMENT_NOT_FOUND` | Deployment not found |
| 409 | `DEPLOYMENT_IN_PROGRESS` | Active deployment already exists |
| 409 | `CLUSTER_ALREADY_EXISTS` | Cluster already registered |
| 429 | `RATE_LIMIT_EXCEEDED` | Too many requests |
| 500 | `INTERNAL_ERROR` | Internal server error |
| 503 | `CLUSTER_UNREACHABLE` | Kubernetes cluster unreachable |

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
| `POST /api/v1/operators` | 10 req | 1 minute |
| `GET /api/v1/operators` | 60 req | 1 minute |
| `POST /api/v1/deployments` | 10 req | 1 minute |
| `GET /api/v1/deployments` | 60 req | 1 minute |
| `POST /api/v1/clusters` | 5 req | 1 minute |
| `GET /api/v1/clusters` | 60 req | 1 minute |
| `GET /api/v1/*/health` | 60 req | 1 minute |
| `POST /api/v1/approvals/*/approve` | 10 req | 1 minute |

---

## Pagination

List endpoints support pagination via limit/offset parameters.

### Pagination Parameters

```http
GET /api/v1/operators?limit=20&offset=40
```

**Parameters:**
- `limit` - Max items to return (1-100, default: 20)
- `offset` - Number of items to skip (default: 0)

### Pagination Response

```json
{
  "operators": [...],
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
