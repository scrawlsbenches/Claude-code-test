# Edge AI Distribution API Reference

**Version:** 1.0.0
**Base URL:** `https://api.example.com/api/v1`
**Authentication:** JWT Bearer Token
**Content-Type:** `application/json`

---

## Table of Contents

1. [Authentication](#authentication)
2. [Models API](#models-api)
3. [Distributions API](#distributions-api)
4. [Devices API](#devices-api)
5. [Validation API](#validation-api)
6. [Error Responses](#error-responses)

---

## Authentication

All API endpoints (except `/health`) require JWT authentication.

```http
POST /api/v1/authentication/login
Authorization: Bearer {token}
```

---

## Models API

### Upload Model

**Endpoint:** `POST /api/v1/models`
**Authorization:** ModelDeveloper, Admin

**Request:**
```http
POST /api/v1/models
Authorization: Bearer {token}
Content-Type: multipart/form-data

{
  "modelId": "object-detection-v2",
  "name": "object-detection",
  "version": "2.0.0",
  "framework": "TensorFlow",
  "artifact": <binary-file>,
  "targetDeviceType": "edge-camera",
  "minMemoryMB": 2048,
  "inputSchema": "{\"type\":\"image\",\"shape\":[1,224,224,3]}",
  "outputSchema": "{\"type\":\"detections\",\"classes\":80}"
}
```

**Response 202 Accepted:**
```json
{
  "modelId": "object-detection-v2",
  "artifactUrl": "s3://models/object-detection-v2.zip",
  "checksum": "sha256:abc123...",
  "artifactSize": 104857600,
  "uploadedAt": "2025-11-23T12:00:00Z"
}
```

---

### List Models

**Endpoint:** `GET /api/v1/models`

**Request:**
```http
GET /api/v1/models?framework=TensorFlow&validationStatus=Passed
```

**Response 200 OK:**
```json
{
  "models": [
    {
      "modelId": "object-detection-v2",
      "name": "object-detection",
      "version": "2.0.0",
      "framework": "TensorFlow",
      "validationStatus": "Passed",
      "uploadedAt": "2025-11-23T12:00:00Z"
    }
  ],
  "total": 42
}
```

---

### Get Model Details

**Endpoint:** `GET /api/v1/models/{modelId}`

**Response 200 OK:**
```json
{
  "modelId": "object-detection-v2",
  "name": "object-detection",
  "version": "2.0.0",
  "framework": "TensorFlow",
  "artifactUrl": "s3://models/object-detection-v2.zip",
  "artifactSize": 104857600,
  "targetDeviceType": "edge-camera",
  "minMemoryMB": 2048,
  "validationStatus": "Passed",
  "uploadedAt": "2025-11-23T12:00:00Z"
}
```

---

## Distributions API

### Create Distribution

**Endpoint:** `POST /api/v1/distributions`
**Authorization:** Operator, Admin

**Request:**
```http
POST /api/v1/distributions
Authorization: Bearer {token}

{
  "modelId": "object-detection-v2",
  "modelVersion": "2.0.0",
  "strategy": "Canary",
  "filter": {
    "region": "us-west-1",
    "deviceType": "edge-camera",
    "minMemoryMB": 2048
  },
  "canaryPercentage": 10,
  "canaryDuration": "PT30M",
  "successCriteria": {
    "maxLatencyMs": 100,
    "maxErrorRate": 0.01,
    "maxLatencyIncrease": 0.20
  }
}
```

**Response 201 Created:**
```json
{
  "distributionId": "dist-abc123",
  "modelId": "object-detection-v2",
  "strategy": "Canary",
  "status": "Pending",
  "devicesTargeted": 1000,
  "createdAt": "2025-11-23T12:00:00Z"
}
```

---

### Get Distribution Status

**Endpoint:** `GET /api/v1/distributions/{id}/status`

**Response 200 OK:**
```json
{
  "distributionId": "dist-abc123",
  "status": "InProgress",
  "devicesTargeted": 1000,
  "devicesUpdated": 250,
  "devicesFailed": 5,
  "successRate": 98.0,
  "currentStage": "Canary",
  "metrics": {
    "avgLatencyMs": 85.3,
    "errorRate": 0.002,
    "latencyIncrease": 0.08
  },
  "startedAt": "2025-11-23T12:00:00Z"
}
```

---

### Rollback Distribution

**Endpoint:** `POST /api/v1/distributions/{id}/rollback`
**Authorization:** Operator, Admin

**Request:**
```http
POST /api/v1/distributions/dist-abc123/rollback
Authorization: Bearer {token}

{
  "reason": "Performance degradation detected"
}
```

**Response 200 OK:**
```json
{
  "distributionId": "dist-abc123",
  "status": "RolledBack",
  "devicesRolledBack": 250,
  "rolledBackAt": "2025-11-23T13:00:00Z"
}
```

---

## Devices API

### Register Device

**Endpoint:** `POST /api/v1/devices`

**Request:**
```http
POST /api/v1/devices
Authorization: Bearer {token}

{
  "deviceId": "device-123",
  "deviceName": "Camera-Main-Entrance",
  "deviceType": "edge-camera",
  "region": "us-west-1",
  "capabilities": {
    "cpuCores": 4,
    "memoryMB": 4096,
    "storageGB": 64,
    "hasGpu": false
  }
}
```

**Response 201 Created:**
```json
{
  "deviceId": "device-123",
  "deviceType": "edge-camera",
  "region": "us-west-1",
  "registeredAt": "2025-11-23T12:00:00Z"
}
```

---

### Get Device Metrics

**Endpoint:** `GET /api/v1/devices/{id}/metrics`

**Response 200 OK:**
```json
{
  "deviceId": "device-123",
  "currentModelVersion": "object-detection-v2",
  "health": {
    "isOnline": true,
    "cpuUsage": 45.2,
    "memoryUsage": 62.8,
    "diskUsage": 35.1
  },
  "inferenceMetrics": {
    "latencyP50Ms": 75.5,
    "latencyP99Ms": 95.2,
    "throughput": 15.3,
    "errorRate": 0.001
  },
  "lastHeartbeat": "2025-11-23T13:59:50Z"
}
```

---

## Validation API

### Validate Model

**Endpoint:** `POST /api/v1/models/{id}/validate`
**Authorization:** ModelDeveloper, Admin

**Request:**
```http
POST /api/v1/models/object-detection-v2/validate
Authorization: Bearer {token}

{
  "testDatasetUrl": "s3://datasets/coco-test-100.zip",
  "validationDevice": "validation-device-1"
}
```

**Response 202 Accepted:**
```json
{
  "validationId": "val-xyz789",
  "modelId": "object-detection-v2",
  "status": "InProgress",
  "startedAt": "2025-11-23T12:00:00Z"
}
```

---

### Get Validation Report

**Endpoint:** `GET /api/v1/models/{id}/validation-report`

**Response 200 OK:**
```json
{
  "modelId": "object-detection-v2",
  "status": "Passed",
  "metrics": {
    "latencyP50Ms": 75.5,
    "latencyP95Ms": 88.2,
    "latencyP99Ms": 95.7,
    "throughput": 15.3,
    "memoryUsageMB": 1024,
    "accuracy": 0.945
  },
  "validatedAt": "2025-11-23T12:30:00Z"
}
```

---

## Error Responses

All errors follow a consistent format:

```json
{
  "error": {
    "code": "VALIDATION_ERROR",
    "message": "Model validation failed",
    "details": [
      {
        "field": "latencyP99Ms",
        "message": "Latency 120ms exceeds threshold of 100ms"
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
| 400 | `MODEL_VALIDATION_ERROR` | Model failed validation |
| 404 | `MODEL_NOT_FOUND` | Model does not exist |
| 409 | `DISTRIBUTION_IN_PROGRESS` | Distribution already running |
| 500 | `INTERNAL_ERROR` | Internal server error |

---

**API Version:** 1.0.0
**Last Updated:** 2025-11-23
