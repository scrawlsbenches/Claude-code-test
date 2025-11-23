# ML Deployment System API Reference

**Version:** 1.0.0
**Base URL:** `https://api.example.com/api/v1`
**Authentication:** JWT Bearer Token
**Content-Type:** `application/json`

---

## Table of Contents

1. [Authentication](#authentication)
2. [Models API](#models-api)
3. [Deployments API](#deployments-api)
4. [Inference API](#inference-api)
5. [Metrics API](#metrics-api)
6. [Error Responses](#error-responses)

---

## Authentication

All API endpoints (except `/health`) require JWT authentication.

### Get JWT Token

```http
POST /api/v1/authentication/login
Content-Type: application/json

{
  "username": "mluser@example.com",
  "password": "SecurePass123!"
}

Response 200 OK:
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expiresAt": "2025-11-24T12:00:00Z",
  "user": {
    "username": "mluser@example.com",
    "role": "DataScientist"
  }
}
```

---

## Models API

### Register Model

Register a new ML model.

**Endpoint:** `POST /api/v1/models`
**Authorization:** DataScientist, MLEngineer, Admin
**Rate Limit:** 20 req/min

**Request:**
```http
POST /api/v1/models
Authorization: Bearer {token}
Content-Type: application/json

{
  "name": "fraud-detection",
  "description": "Credit card fraud detection model",
  "framework": "TensorFlow",
  "type": "Classification",
  "version": "2.0.0",
  "artifactPath": "s3://models/fraud-detection-v2.tar.gz",
  "checksum": "sha256:abc123...",
  "inputSchema": "{\"type\":\"object\",\"properties\":{\"amount\":{\"type\":\"number\"}}}",
  "outputSchema": "{\"type\":\"object\",\"properties\":{\"fraud_probability\":{\"type\":\"number\"}}}",
  "trainingMetadata": {
    "datasetName": "fraud_dataset_2025",
    "trainingSamples": 1000000,
    "accuracy": 0.95,
    "f1Score": 0.93
  }
}
```

**Response 201 Created:**
```json
{
  "modelId": "model-7f8a9b1c",
  "name": "fraud-detection",
  "version": "2.0.0",
  "status": "Registered",
  "createdAt": "2025-11-23T12:00:00Z"
}
```

---

### List Models

**Endpoint:** `GET /api/v1/models`
**Authorization:** All roles

```http
GET /api/v1/models?framework=TensorFlow&type=Classification&limit=20
Authorization: Bearer {token}
```

**Response 200 OK:**
```json
{
  "models": [
    {
      "modelId": "model-123",
      "name": "fraud-detection",
      "framework": "TensorFlow",
      "type": "Classification",
      "activeVersion": "2.0.0",
      "createdAt": "2025-11-23T12:00:00Z"
    }
  ],
  "total": 15,
  "limit": 20,
  "offset": 0
}
```

---

## Deployments API

### Create Deployment

Deploy a model version.

**Endpoint:** `POST /api/v1/deployments`
**Authorization:** MLEngineer, Admin
**Rate Limit:** 10 req/min

**Request (Canary Deployment):**
```http
POST /api/v1/deployments
Authorization: Bearer {token}

{
  "modelId": "model-123",
  "versionId": "version-456",
  "strategy": "Canary",
  "environment": "Production",
  "strategyConfig": {
    "canaryPercentage": 10,
    "incrementStep": 15,
    "monitoringDuration": "PT5M",
    "rollbackThresholds": {
      "accuracyDrop": 0.05,
      "latencyIncrease": 1.5
    }
  }
}
```

**Response 202 Accepted:**
```json
{
  "deploymentId": "deploy-789",
  "status": "Pending",
  "strategy": "Canary",
  "startedAt": "2025-11-23T12:05:00Z",
  "estimatedCompletionTime": "2025-11-23T12:35:00Z"
}
```

---

### Get Deployment Status

**Endpoint:** `GET /api/v1/deployments/{id}`

```http
GET /api/v1/deployments/deploy-789
```

**Response 200 OK:**
```json
{
  "deploymentId": "deploy-789",
  "status": "Deploying",
  "progressPercentage": 45,
  "trafficPercentage": 25,
  "validationResult": {
    "passed": true,
    "metrics": {
      "accuracy": 0.96,
      "avgLatencyMs": 45.2
    }
  },
  "assignedServers": ["server-1", "server-2"]
}
```

---

### Rollback Deployment

**Endpoint:** `POST /api/v1/deployments/{id}/rollback`
**Authorization:** MLEngineer, Admin

```http
POST /api/v1/deployments/deploy-789/rollback
Authorization: Bearer {token}

{
  "reason": "Performance degradation detected"
}
```

**Response 200 OK:**
```json
{
  "deploymentId": "deploy-789",
  "status": "RolledBack",
  "rolledBackAt": "2025-11-23T12:30:00Z",
  "previousVersion": "2.0.0",
  "currentVersion": "1.5.0"
}
```

---

## Inference API

### Run Inference

Execute model inference.

**Endpoint:** `POST /api/v1/inference/{modelName}`
**Authorization:** All roles (with API key)
**Rate Limit:** 1000 req/min

**Request:**
```http
POST /api/v1/inference/fraud-detection
Authorization: Bearer {token}

{
  "features": {
    "transaction_amount": 1500.00,
    "merchant_category": "retail",
    "user_age_days": 365,
    "transaction_hour": 14,
    "is_international": false
  },
  "version": "2.0.0"
}
```

**Response 200 OK:**
```json
{
  "requestId": "req-abc123",
  "modelName": "fraud-detection",
  "version": "2.0.0",
  "prediction": {
    "fraud_probability": 0.023,
    "is_fraud": false,
    "risk_level": "low"
  },
  "confidence": 0.977,
  "latencyMs": 42.5,
  "timestamp": "2025-11-23T12:15:00Z"
}
```

---

### Batch Inference

**Endpoint:** `POST /api/v1/inference/{modelName}/batch`

**Request:**
```http
POST /api/v1/inference/fraud-detection/batch

{
  "instances": [
    {"transaction_amount": 1500.00, "merchant_category": "retail"},
    {"transaction_amount": 5000.00, "merchant_category": "electronics"}
  ]
}
```

**Response 200 OK:**
```json
{
  "requestId": "batch-xyz789",
  "predictions": [
    {"fraud_probability": 0.023, "is_fraud": false},
    {"fraud_probability": 0.156, "is_fraud": false}
  ],
  "totalLatencyMs": 95.3
}
```

---

## Metrics API

### Get Model Metrics

**Endpoint:** `GET /api/v1/models/{id}/metrics`

```http
GET /api/v1/models/model-123/metrics?window=PT1H&version=2.0.0
```

**Response 200 OK:**
```json
{
  "modelId": "model-123",
  "version": "2.0.0",
  "window": "PT1H",
  "metrics": {
    "totalRequests": 50000,
    "successfulInferences": 49950,
    "failedInferences": 50,
    "avgLatencyMs": 45.2,
    "p50LatencyMs": 38.5,
    "p95LatencyMs": 75.0,
    "p99LatencyMs": 95.0,
    "throughputRps": 833.3,
    "accuracy": 0.96,
    "driftScore": 0.05
  },
  "timestamp": "2025-11-23T13:00:00Z"
}
```

---

## Error Responses

### Error Response Format

```json
{
  "error": {
    "code": "VALIDATION_ERROR",
    "message": "Model validation failed",
    "details": [
      {
        "field": "inputSchema",
        "message": "Invalid JSON Schema format"
      }
    ],
    "traceId": "trace-xyz789",
    "timestamp": "2025-11-23T13:00:00Z"
  }
}
```

### Common Error Codes

| HTTP Status | Error Code | Description |
|-------------|------------|-------------|
| 400 | `VALIDATION_ERROR` | Request validation failed |
| 400 | `SCHEMA_VALIDATION_ERROR` | Input schema validation failed |
| 401 | `UNAUTHORIZED` | Missing or invalid JWT token |
| 403 | `FORBIDDEN` | Insufficient permissions |
| 404 | `MODEL_NOT_FOUND` | Model does not exist |
| 409 | `CONFLICT` | Model version already exists |
| 429 | `RATE_LIMIT_EXCEEDED` | Too many requests |
| 500 | `INFERENCE_ERROR` | Model inference failed |
| 503 | `SERVICE_UNAVAILABLE` | Model server unavailable |

---

**API Version:** 1.0.0
**Last Updated:** 2025-11-23
**Support:** ml-platform@example.com
