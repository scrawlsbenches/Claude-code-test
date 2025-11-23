# Live Event System API Reference

**Version:** 1.0.0
**Base URL:** `https://api.example.com/api/v1`
**Authentication:** JWT Bearer Token
**Content-Type:** `application/json`

---

## Table of Contents

1. [Authentication](#authentication)
2. [Events API](#events-api)
3. [Deployments API](#deployments-api)
4. [Metrics API](#metrics-api)
5. [Segments API](#segments-api)
6. [Regions API](#regions-api)
7. [Error Responses](#error-responses)

---

## Authentication

All API endpoints (except `/health`) require JWT authentication.

### Get JWT Token

```http
POST /api/v1/authentication/login
Content-Type: application/json

{
  "username": "gamedesigner@example.com",
  "password": "Designer123!"
}

Response 200 OK:
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expiresAt": "2025-11-24T12:00:00Z",
  "user": {
    "username": "gamedesigner@example.com",
    "role": "GameDesigner"
  }
}
```

### Use Token in Requests

```http
GET /api/v1/events
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

---

## Events API

### Create Event

Create a new live event.

**Endpoint:** `POST /api/v1/events`
**Authorization:** GameDesigner, Admin
**Rate Limit:** 10 req/min per user

**Request:**
```http
POST /api/v1/events
Authorization: Bearer {token}
Content-Type: application/json

{
  "eventId": "summer-fest-2025",
  "displayName": "Summer Festival 2025",
  "description": "Celebrate summer with exclusive rewards and challenges!",
  "type": "SeasonalPromotion",
  "category": "seasonal",
  "startTime": "2025-06-21T00:00:00Z",
  "endTime": "2025-07-21T23:59:59Z",
  "timezone": "UTC",
  "configuration": {
    "version": "1.0",
    "rewards": {
      "dailyLoginBonus": 100,
      "questMultiplier": 2.0,
      "items": {
        "summer-cosmetic-pack": 1
      },
      "currency": {
        "gold": 500,
        "gems": 50
      }
    },
    "multipliers": {
      "xp": 1.5,
      "currency": 2.0
    },
    "assets": {
      "bannerImageUrl": "https://cdn.example.com/events/summer-fest-2025-banner.png",
      "iconImageUrl": "https://cdn.example.com/events/summer-fest-2025-icon.png"
    }
  },
  "targetSegments": ["all-players"],
  "priority": 5,
  "tags": ["seasonal", "summer", "rewards"]
}
```

**Response 201 Created:**
```json
{
  "eventId": "summer-fest-2025",
  "displayName": "Summer Festival 2025",
  "type": "SeasonalPromotion",
  "state": "Draft",
  "startTime": "2025-06-21T00:00:00Z",
  "endTime": "2025-07-21T23:59:59Z",
  "approvalStatus": "Pending",
  "createdAt": "2025-11-23T12:00:00Z",
  "createdBy": "gamedesigner@example.com"
}
```

---

### List Events

List all events with optional filtering.

**Endpoint:** `GET /api/v1/events`
**Authorization:** All roles
**Rate Limit:** 60 req/min per user

**Request:**
```http
GET /api/v1/events?state=Active&type=SeasonalPromotion&limit=20
Authorization: Bearer {token}
```

**Query Parameters:**

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `state` | string | No | - | Filter by state (`Draft`, `Active`, etc.) |
| `type` | string | No | - | Filter by type |
| `limit` | int | No | 20 | Max events to return (1-100) |
| `offset` | int | No | 0 | Pagination offset |

**Response 200 OK:**
```json
{
  "events": [
    {
      "eventId": "summer-fest-2025",
      "displayName": "Summer Festival 2025",
      "type": "SeasonalPromotion",
      "state": "Active",
      "startTime": "2025-06-21T00:00:00Z",
      "endTime": "2025-07-21T23:59:59Z",
      "currentParticipants": 125000,
      "createdAt": "2025-11-23T12:00:00Z"
    }
  ],
  "total": 42,
  "limit": 20,
  "offset": 0
}
```

---

### Get Event Details

Get detailed information about a specific event.

**Endpoint:** `GET /api/v1/events/{eventId}`
**Authorization:** All roles
**Rate Limit:** 60 req/min per user

**Request:**
```http
GET /api/v1/events/summer-fest-2025
Authorization: Bearer {token}
```

**Response 200 OK:**
```json
{
  "eventId": "summer-fest-2025",
  "displayName": "Summer Festival 2025",
  "description": "Celebrate summer with exclusive rewards!",
  "type": "SeasonalPromotion",
  "state": "Active",
  "startTime": "2025-06-21T00:00:00Z",
  "endTime": "2025-07-21T23:59:59Z",
  "configuration": {
    "version": "1.0",
    "rewards": { ... }
  },
  "targetSegments": ["all-players"],
  "currentParticipants": 125000,
  "maxParticipants": null,
  "approvalStatus": "Approved",
  "approvedBy": "admin@example.com",
  "approvedAt": "2025-11-23T13:00:00Z",
  "createdAt": "2025-11-23T12:00:00Z"
}
```

---

### Update Event

Update an event (only allowed in Draft state).

**Endpoint:** `PUT /api/v1/events/{eventId}`
**Authorization:** GameDesigner, Admin
**Rate Limit:** 10 req/min per user

**Request:**
```http
PUT /api/v1/events/summer-fest-2025
Authorization: Bearer {token}
Content-Type: application/json

{
  "displayName": "Summer Festival 2025 - Extended!",
  "endTime": "2025-07-28T23:59:59Z"
}
```

**Response 200 OK:**
```json
{
  "eventId": "summer-fest-2025",
  "displayName": "Summer Festival 2025 - Extended!",
  "endTime": "2025-07-28T23:59:59Z",
  "updatedAt": "2025-11-23T14:00:00Z"
}
```

---

### Activate Event

Manually activate an event.

**Endpoint:** `POST /api/v1/events/{eventId}/activate`
**Authorization:** Admin only
**Rate Limit:** 10 req/min per user

**Request:**
```http
POST /api/v1/events/summer-fest-2025/activate
Authorization: Bearer {token}
```

**Response 200 OK:**
```json
{
  "eventId": "summer-fest-2025",
  "state": "Active",
  "activatedAt": "2025-11-23T15:00:00Z"
}
```

---

### Deactivate Event

Manually deactivate an event.

**Endpoint:** `POST /api/v1/events/{eventId}/deactivate`
**Authorization:** Admin only
**Rate Limit:** 10 req/min per user

**Request:**
```http
POST /api/v1/events/summer-fest-2025/deactivate
Authorization: Bearer {token}
Content-Type: application/json

{
  "reason": "Event ended early due to technical issues"
}
```

**Response 200 OK:**
```json
{
  "eventId": "summer-fest-2025",
  "state": "Cancelled",
  "deactivatedAt": "2025-11-23T16:00:00Z"
}
```

---

### Approve Event

Approve an event for production deployment (admin only).

**Endpoint:** `POST /api/v1/events/{eventId}/approve`
**Authorization:** Admin only
**Rate Limit:** 10 req/min per user

**Request:**
```http
POST /api/v1/events/summer-fest-2025/approve
Authorization: Bearer {token}
Content-Type: application/json

{
  "notes": "Configuration validated, assets verified"
}
```

**Response 200 OK:**
```json
{
  "eventId": "summer-fest-2025",
  "approvalStatus": "Approved",
  "approvedBy": "admin@example.com",
  "approvedAt": "2025-11-23T13:00:00Z"
}
```

---

### Query Player Events

Query active events for a specific player.

**Endpoint:** `GET /api/v1/players/{playerId}/events`
**Authorization:** Player, Developer, Admin
**Rate Limit:** 1000 req/min per player

**Request:**
```http
GET /api/v1/players/player123/events?region=us-east&platform=mobile&language=en
Authorization: Bearer {token}
```

**Query Parameters:**

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `region` | string | Yes | - | Player's region |
| `platform` | string | No | - | Player's platform (pc, mobile, console) |
| `language` | string | No | en | Player's language |
| `includeConfig` | boolean | No | true | Include full event configuration |

**Response 200 OK:**
```json
{
  "playerId": "player123",
  "region": "us-east",
  "events": [
    {
      "eventId": "summer-fest-2025",
      "displayName": "Summer Festival 2025",
      "type": "SeasonalPromotion",
      "startTime": "2025-06-21T00:00:00Z",
      "endTime": "2025-07-21T23:59:59Z",
      "configuration": { ... },
      "playerEligible": true,
      "participationStatus": "NotParticipated"
    }
  ],
  "cachedAt": "2025-11-23T17:00:00Z"
}
```

---

## Deployments API

### Create Deployment

Deploy an event to regions.

**Endpoint:** `POST /api/v1/deployments`
**Authorization:** Admin only
**Rate Limit:** 5 req/min per user

**Request:**
```http
POST /api/v1/deployments
Authorization: Bearer {token}
Content-Type: application/json

{
  "eventId": "summer-fest-2025",
  "regions": ["us-east", "us-west"],
  "strategy": "Canary",
  "configuration": {
    "canaryBatches": [10, 30, 50, 100],
    "batchDelay": "PT5M",
    "autoRollback": {
      "participationRateDrop": 0.2,
      "errorRateIncrease": 0.05
    }
  }
}
```

**Response 201 Created:**
```json
{
  "deploymentId": "deploy-abc123",
  "eventId": "summer-fest-2025",
  "regions": ["us-east", "us-west"],
  "strategy": "Canary",
  "status": "InProgress",
  "progressPercentage": 10,
  "currentBatch": 1,
  "totalBatches": 4,
  "startedAt": "2025-11-23T18:00:00Z",
  "deployedBy": "admin@example.com"
}
```

---

### Get Deployment Status

Get deployment status and progress.

**Endpoint:** `GET /api/v1/deployments/{deploymentId}`
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
  "eventId": "summer-fest-2025",
  "status": "InProgress",
  "progressPercentage": 30,
  "currentBatch": 2,
  "totalBatches": 4,
  "regionStatuses": {
    "us-east": {
      "region": "us-east",
      "status": "Completed",
      "playerPercentage": 30,
      "activatedAt": "2025-11-23T18:05:00Z"
    },
    "us-west": {
      "region": "us-west",
      "status": "InProgress",
      "playerPercentage": 30
    }
  },
  "startedAt": "2025-11-23T18:00:00Z"
}
```

---

### Rollback Deployment

Rollback an event deployment.

**Endpoint:** `POST /api/v1/deployments/{deploymentId}/rollback`
**Authorization:** Admin only
**Rate Limit:** 10 req/min per user

**Request:**
```http
POST /api/v1/deployments/deploy-abc123/rollback
Authorization: Bearer {token}
Content-Type: application/json

{
  "reason": "Participation rate dropped by 25%"
}
```

**Response 200 OK:**
```json
{
  "deploymentId": "deploy-abc123",
  "status": "RolledBack",
  "rollback": {
    "rolledBackAt": "2025-11-23T18:30:00Z",
    "rolledBackBy": "admin@example.com",
    "reason": "Participation rate dropped by 25%",
    "wasAutomatic": false
  }
}
```

---

## Metrics API

### Get Event Metrics

Get engagement metrics for an event.

**Endpoint:** `GET /api/v1/events/{eventId}/metrics`
**Authorization:** All roles
**Rate Limit:** 60 req/min per user

**Request:**
```http
GET /api/v1/events/summer-fest-2025/metrics?region=us-east&window=PT1H
Authorization: Bearer {token}
```

**Query Parameters:**

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `region` | string | No | - | Filter by region (null = global) |
| `window` | string | No | PT1H | Time window (ISO 8601 duration) |

**Response 200 OK:**
```json
{
  "eventId": "summer-fest-2025",
  "region": "us-east",
  "windowStart": "2025-11-23T17:00:00Z",
  "windowEnd": "2025-11-23T18:00:00Z",
  "engagement": {
    "activePlayers": 500000,
    "participants": 250000,
    "participationRate": 0.50,
    "completions": 125000,
    "completionRate": 0.50,
    "avgSessionDuration": 1800,
    "dau": 450000,
    "mau": 2000000
  },
  "revenue": {
    "totalRevenue": 150000,
    "baselineRevenue": 125000,
    "revenueUplift": 0.20,
    "arpu": 0.30,
    "arppu": 3.00,
    "conversionRate": 0.10,
    "purchaseCount": 50000
  },
  "sentiment": {
    "positiveFeedback": 15000,
    "negativeFeedback": 2000,
    "neutralFeedback": 3000,
    "nps": 65,
    "retentionRate": 0.85
  },
  "updatedAt": "2025-11-23T18:00:00Z"
}
```

---

## Segments API

### Create Segment

Create a player segment.

**Endpoint:** `POST /api/v1/segments`
**Authorization:** GameDesigner, Admin
**Rate Limit:** 10 req/min per user

**Request:**
```http
POST /api/v1/segments
Authorization: Bearer {token}
Content-Type: application/json

{
  "segmentId": "vip-tier-3",
  "displayName": "VIP Tier 3",
  "description": "High-value players with $100+ lifetime spend",
  "type": "VIP",
  "criteria": {
    "lifetimeSpend": [100, 500],
    "accountAgeDays": [30, 999999],
    "platforms": ["pc", "console"]
  }
}
```

**Response 201 Created:**
```json
{
  "segmentId": "vip-tier-3",
  "displayName": "VIP Tier 3",
  "type": "VIP",
  "estimatedPlayerCount": 45000,
  "createdAt": "2025-11-23T19:00:00Z",
  "createdBy": "gamedesigner@example.com"
}
```

---

### List Segments

List all player segments.

**Endpoint:** `GET /api/v1/segments`
**Authorization:** All roles
**Rate Limit:** 60 req/min per user

**Response 200 OK:**
```json
{
  "segments": [
    {
      "segmentId": "vip-tier-3",
      "displayName": "VIP Tier 3",
      "type": "VIP",
      "estimatedPlayerCount": 45000,
      "createdAt": "2025-11-23T19:00:00Z"
    }
  ],
  "total": 15
}
```

---

## Regions API

### List Regions

List all available regions.

**Endpoint:** `GET /api/v1/regions`
**Authorization:** All roles
**Rate Limit:** 60 req/min per user

**Response 200 OK:**
```json
{
  "regions": [
    {
      "regionId": "us-east",
      "displayName": "US East Coast",
      "timezone": "America/New_York",
      "countryCodes": ["US"],
      "playerPopulation": 2000000,
      "health": {
        "isHealthy": true,
        "serverLoad": 0.65,
        "activePlayers": 150000,
        "lastHealthCheck": "2025-11-23T19:59:00Z"
      },
      "isEnabled": true
    }
  ],
  "total": 10
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
    "message": "Event validation failed",
    "details": [
      {
        "field": "startTime",
        "message": "Start time must be in the future"
      }
    ],
    "traceId": "trace-xyz789",
    "timestamp": "2025-11-23T20:00:00Z"
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
| 409 | `CONFLICT` | Resource already exists or state conflict |
| 429 | `RATE_LIMIT_EXCEEDED` | Too many requests |
| 500 | `INTERNAL_ERROR` | Internal server error |

---

## Rate Limiting

All endpoints are rate-limited based on user identity (JWT token).

### Rate Limit Headers

```http
HTTP/1.1 200 OK
X-RateLimit-Limit: 60
X-RateLimit-Remaining: 45
X-RateLimit-Reset: 1700144400
```

### Rate Limits by Endpoint

| Endpoint | Limit | Window |
|----------|-------|--------|
| `GET /api/v1/players/{id}/events` | 1000 req | 1 minute |
| `POST /api/v1/events` | 10 req | 1 minute |
| `POST /api/v1/deployments` | 5 req | 1 minute |
| `GET /api/v1/events/{id}/metrics` | 60 req | 1 minute |
| `GET /api/v1/events` | 60 req | 1 minute |

---

**API Version:** 1.0.0
**Last Updated:** 2025-11-23
**Support:** api-support@example.com
