# Gaming & Real-Time Configuration System API Reference

**Version:** 1.0.0
**Base URL:** `https://api.example.com/api/v1`
**Authentication:** JWT Bearer Token
**Content-Type:** `application/json`

---

## Table of Contents

1. [Authentication](#authentication)
2. [Game Configurations API](#game-configurations-api)
3. [Deployments API](#deployments-api)
4. [Game Servers API](#game-servers-api)
5. [Live Events API](#live-events-api)
6. [Metrics API](#metrics-api)
7. [A/B Tests API](#ab-tests-api)
8. [Error Responses](#error-responses)

---

## Authentication

All API endpoints require JWT authentication.

### Get JWT Token

```http
POST /api/v1/authentication/login
Content-Type: application/json

{
  "username": "gamedev@example.com",
  "password": "SecurePass123!"
}

Response 200 OK:
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expiresAt": "2025-11-24T12:00:00Z",
  "user": {
    "username": "gamedev@example.com",
    "role": "GameDev"
  }
}
```

---

## Game Configurations API

### Create Configuration

**Endpoint:** `POST /api/v1/game-configs`
**Authorization:** GameDev, Admin
**Rate Limit:** 20 req/min

```http
POST /api/v1/game-configs
Authorization: Bearer {token}

{
  "name": "weapon-balance-patch-2.1",
  "gameId": "battle-royale",
  "configType": "GameBalance",
  "configuration": "{\"weapons\":{\"rifle\":{\"damage\":45,\"fireRate\":600}}}",
  "version": "2.1.0",
  "schemaId": "game-balance-v1",
  "changeDescription": "Increased rifle damage by 5 points",
  "tags": ["balance", "weapons"]
}

Response 201 Created:
{
  "configId": "cfg-7f8a9b1c-2d3e-4f5g-6h7i-8j9k0l1m2n3o",
  "name": "weapon-balance-patch-2.1",
  "gameId": "battle-royale",
  "configType": "GameBalance",
  "version": "2.1.0",
  "status": "Draft",
  "createdAt": "2025-11-23T12:00:00Z"
}
```

### List Configurations

**Endpoint:** `GET /api/v1/game-configs`

```http
GET /api/v1/game-configs?gameId=battle-royale&configType=GameBalance&status=Approved

Response 200 OK:
{
  "configurations": [
    {
      "configId": "cfg-123",
      "name": "weapon-balance-patch-2.1",
      "version": "2.1.0",
      "status": "Approved",
      "createdAt": "2025-11-23T12:00:00Z"
    }
  ],
  "total": 15
}
```

### Approve Configuration

**Endpoint:** `POST /api/v1/game-configs/{configId}/approve`
**Authorization:** Admin only

```http
POST /api/v1/game-configs/cfg-123/approve

{
  "notes": "Approved after QA testing"
}

Response 200 OK:
{
  "configId": "cfg-123",
  "status": "Approved",
  "approvedBy": "admin@example.com",
  "approvedAt": "2025-11-23T13:00:00Z"
}
```

---

## Deployments API

### Create Deployment

**Endpoint:** `POST /api/v1/deployments`
**Authorization:** GameDev, Admin

```http
POST /api/v1/deployments

{
  "configId": "cfg-123",
  "strategy": "Canary",
  "targetRegions": ["NA-WEST", "NA-EAST"],
  "targetServerTags": ["production"],
  "canaryPhases": [10, 30, 50, 100],
  "evaluationPeriod": "PT30M",
  "autoProgressEnabled": true,
  "thresholds": {
    "churnRateIncreaseMax": 5.0,
    "crashRateIncreaseMax": 10.0
  },
  "notes": "Deploying weapon balance patch"
}

Response 202 Accepted:
{
  "deploymentId": "dep-abc123",
  "configId": "cfg-123",
  "strategy": "Canary",
  "status": "Pending",
  "currentPhase": 0,
  "currentPercentage": 0,
  "startedAt": "2025-11-23T14:00:00Z"
}
```

### Get Deployment Status

**Endpoint:** `GET /api/v1/deployments/{deploymentId}`

```http
GET /api/v1/deployments/dep-abc123

Response 200 OK:
{
  "deploymentId": "dep-abc123",
  "configId": "cfg-123",
  "strategy": "Canary",
  "status": "InProgress",
  "currentPhase": 2,
  "currentPercentage": 30,
  "totalPhases": 4,
  "startedAt": "2025-11-23T14:00:00Z",
  "metrics": {
    "baseline": {
      "churnRate": 3.5,
      "avgSessionDurationMinutes": 45.0,
      "activePlayers": 50000
    },
    "current": {
      "churnRate": 3.8,
      "avgSessionDurationMinutes": 44.5,
      "activePlayers": 49800
    },
    "comparison": {
      "churnRateChange": 0.3,
      "healthScore": 95.0
    }
  }
}
```

### Rollback Deployment

**Endpoint:** `POST /api/v1/deployments/{deploymentId}/rollback`

```http
POST /api/v1/deployments/dep-abc123/rollback

{
  "reason": "Churn rate spike detected"
}

Response 200 OK:
{
  "deploymentId": "dep-abc123",
  "status": "RolledBack",
  "rolledBackAt": "2025-11-23T15:30:00Z",
  "rollbackReason": "Churn rate spike detected"
}
```

---

## Game Servers API

### Register Game Server

**Endpoint:** `POST /api/v1/game-servers`

```http
POST /api/v1/game-servers

{
  "serverId": "srv-west-001",
  "hostname": "game-server-west-001.example.com",
  "ipAddress": "192.168.1.100",
  "port": 7777,
  "region": "NA-WEST",
  "tags": ["production", "premium"],
  "maxPlayers": 100
}

Response 201 Created:
{
  "serverId": "srv-west-001",
  "region": "NA-WEST",
  "status": "Healthy",
  "registeredAt": "2025-11-23T12:00:00Z"
}
```

### List Game Servers

**Endpoint:** `GET /api/v1/game-servers`

```http
GET /api/v1/game-servers?region=NA-WEST&healthy=true

Response 200 OK:
{
  "servers": [
    {
      "serverId": "srv-west-001",
      "hostname": "game-server-west-001.example.com",
      "region": "NA-WEST",
      "playerCount": 85,
      "maxPlayers": 100,
      "currentConfigId": "cfg-123",
      "health": {
        "isHealthy": true,
        "cpuUsage": 45.2,
        "avgFps": 60.0
      },
      "lastHeartbeat": "2025-11-23T14:59:50Z"
    }
  ],
  "total": 1500
}
```

---

## Live Events API

### Create Live Event

**Endpoint:** `POST /api/v1/live-events`

```http
POST /api/v1/live-events

{
  "name": "Halloween 2025",
  "gameId": "battle-royale",
  "eventId": "halloween-2025",
  "configId": "cfg-halloween",
  "startTime": "2025-10-31T00:00:00Z",
  "endTime": "2025-11-07T23:59:59Z",
  "targetRegions": ["NA-WEST", "NA-EAST", "EU-WEST"],
  "description": "Spooky seasonal event",
  "rewardsConfig": "{\"participation\":{\"item\":\"pumpkin-helmet\"}}"
}

Response 201 Created:
{
  "eventId": "halloween-2025",
  "name": "Halloween 2025",
  "status": "Scheduled",
  "startTime": "2025-10-31T00:00:00Z",
  "endTime": "2025-11-07T23:59:59Z",
  "createdAt": "2025-11-23T12:00:00Z"
}
```

### Activate Event

**Endpoint:** `POST /api/v1/live-events/{eventId}/activate`

```http
POST /api/v1/live-events/halloween-2025/activate

Response 200 OK:
{
  "eventId": "halloween-2025",
  "status": "Active",
  "activatedAt": "2025-10-31T00:00:00Z"
}
```

---

## Metrics API

### Get Deployment Metrics

**Endpoint:** `GET /api/v1/deployments/{deploymentId}/metrics`

```http
GET /api/v1/deployments/dep-abc123/metrics?window=PT1H

Response 200 OK:
{
  "deploymentId": "dep-abc123",
  "window": "PT1H",
  "baseline": {
    "activePlayers": 50000,
    "churnRate": 3.5,
    "avgSessionDurationMinutes": 45.0,
    "engagementScore": 78.5,
    "crashRate": 0.1
  },
  "current": {
    "activePlayers": 49800,
    "churnRate": 3.8,
    "avgSessionDurationMinutes": 44.5,
    "engagementScore": 77.2,
    "crashRate": 0.15
  },
  "comparison": {
    "churnRateChange": 0.3,
    "sessionDurationChangePercent": -1.1,
    "healthScore": 95.0
  },
  "timestamp": "2025-11-23T15:00:00Z"
}
```

### Submit Player Feedback

**Endpoint:** `POST /api/v1/metrics/player-feedback`

```http
POST /api/v1/metrics/player-feedback

{
  "deploymentId": "dep-abc123",
  "playerId": "player-12345",
  "rating": 4,
  "feedback": "Game feels more balanced",
  "sentiment": "positive"
}

Response 200 OK:
{
  "feedbackId": "fb-xyz789",
  "recorded": true,
  "timestamp": "2025-11-23T15:00:00Z"
}
```

---

## A/B Tests API

### Create A/B Test

**Endpoint:** `POST /api/v1/ab-tests`

```http
POST /api/v1/ab-tests

{
  "name": "Weapon Balance Test",
  "gameId": "battle-royale",
  "variants": [
    {
      "variantId": "A",
      "name": "Control",
      "configId": "cfg-123",
      "weight": 50
    },
    {
      "variantId": "B",
      "name": "Experimental",
      "configId": "cfg-124",
      "weight": 50
    }
  ],
  "duration": "P7D",
  "successMetric": "engagement",
  "targetImprovement": 5.0
}

Response 201 Created:
{
  "testId": "test-abc123",
  "name": "Weapon Balance Test",
  "status": "Draft",
  "createdAt": "2025-11-23T12:00:00Z"
}
```

### Get Test Results

**Endpoint:** `GET /api/v1/ab-tests/{testId}/results`

```http
GET /api/v1/ab-tests/test-abc123/results

Response 200 OK:
{
  "testId": "test-abc123",
  "status": "Running",
  "variants": [
    {
      "variantId": "A",
      "name": "Control",
      "metrics": {
        "engagementScore": 78.5,
        "activePlayers": 25000,
        "avgSessionDurationMinutes": 45.0
      }
    },
    {
      "variantId": "B",
      "name": "Experimental",
      "metrics": {
        "engagementScore": 82.1,
        "activePlayers": 25100,
        "avgSessionDurationMinutes": 47.5
      }
    }
  ],
  "results": {
    "pValue": 0.023,
    "isSignificant": true,
    "winnerVariantId": "B",
    "improvementPercent": 4.6,
    "sampleSize": 50100
  }
}
```

### Declare Winner

**Endpoint:** `POST /api/v1/ab-tests/{testId}/declare-winner`

```http
POST /api/v1/ab-tests/test-abc123/declare-winner

{
  "winnerVariantId": "B"
}

Response 200 OK:
{
  "testId": "test-abc123",
  "status": "Completed",
  "winnerVariantId": "B",
  "completedAt": "2025-11-30T12:00:00Z"
}
```

---

## Error Responses

### Error Format

```json
{
  "error": {
    "code": "VALIDATION_ERROR",
    "message": "Configuration validation failed",
    "details": [
      {
        "field": "configuration",
        "message": "Invalid weapon damage value"
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
| 400 | `CONFIG_VALIDATION_ERROR` | Configuration validation failed |
| 401 | `UNAUTHORIZED` | Missing or invalid JWT token |
| 403 | `FORBIDDEN` | Insufficient permissions |
| 404 | `NOT_FOUND` | Resource not found |
| 409 | `CONFLICT` | Resource already exists |
| 429 | `RATE_LIMIT_EXCEEDED` | Too many requests |
| 500 | `INTERNAL_ERROR` | Internal server error |

---

**API Version:** 1.0.0
**Last Updated:** 2025-11-23
