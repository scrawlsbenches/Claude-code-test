# Messaging System API Reference

**Version:** 1.0.0
**Base URL:** `https://api.example.com/api/v1`
**Authentication:** JWT Bearer Token
**Content-Type:** `application/json`

---

## Table of Contents

1. [Authentication](#authentication)
2. [Messages API](#messages-api)
3. [Topics API](#topics-api)
4. [Subscriptions API](#subscriptions-api)
5. [Schemas API](#schemas-api)
6. [Brokers API](#brokers-api)
7. [Error Responses](#error-responses)
8. [Rate Limiting](#rate-limiting)

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
  "expiresAt": "2025-11-17T12:00:00Z",
  "user": {
    "username": "admin@example.com",
    "role": "Admin"
  }
}
```

### Use Token in Requests

```http
GET /api/v1/topics
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

---

## Messages API

### Publish Message

Publish a message to a topic.

**Endpoint:** `POST /api/v1/messages/publish`
**Authorization:** Producer, Admin
**Rate Limit:** 100 req/min per user

**Request:**
```http
POST /api/v1/messages/publish
Authorization: Bearer {token}
Content-Type: application/json

{
  "topicName": "deployment.events",
  "payload": "{\"event\":\"deployment.completed\",\"executionId\":\"abc123\"}",
  "schemaVersion": "1.0",
  "headers": {
    "source": "orchestrator-api",
    "environment": "Production"
  },
  "priority": 5,
  "expiresAt": "2025-11-17T10:00:00Z",
  "correlationId": "corr-123",
  "partitionKey": "deployment-abc123"
}
```

**Request Fields:**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `topicName` | string | Yes | Target topic name |
| `payload` | string | Yes | JSON payload (max 1 MB) |
| `schemaVersion` | string | Yes | Schema version (e.g., "1.0") |
| `headers` | object | No | Key-value metadata |
| `priority` | int | No | Priority 0-9 (default: 0) |
| `expiresAt` | datetime | No | Message expiration (ISO 8601) |
| `correlationId` | string | No | Request/reply correlation |
| `partitionKey` | string | No | Partition routing key |

**Response 202 Accepted:**
```json
{
  "messageId": "msg-7f8a9b1c-2d3e-4f5g-6h7i-8j9k0l1m2n3o",
  "topicName": "deployment.events",
  "status": "Pending",
  "publishedAt": "2025-11-16T12:00:00Z",
  "partition": 2
}
```

**Response Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `messageId` | string | Unique message ID (GUID) |
| `topicName` | string | Topic name |
| `status` | string | `Pending`, `Delivered`, `Failed` |
| `publishedAt` | datetime | Publish timestamp (UTC) |
| `partition` | int | Assigned partition number |

**Error Responses:**
- `400 Bad Request` - Invalid payload or schema validation failed
- `404 Not Found` - Topic does not exist
- `429 Too Many Requests` - Rate limit exceeded
- `500 Internal Server Error` - Server error

---

### Consume Messages

Consume messages from a topic (pull-based).

**Endpoint:** `GET /api/v1/messages/consume/{topicName}`
**Authorization:** Consumer, Admin
**Rate Limit:** 600 req/min per user

**Request:**
```http
GET /api/v1/messages/consume/deployment.events?limit=10&ackTimeout=30&consumerGroup=notification-service
Authorization: Bearer {token}
```

**Query Parameters:**

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `limit` | int | No | 10 | Max messages to return (1-100) |
| `ackTimeout` | int | No | 30 | Ack deadline in seconds |
| `consumerGroup` | string | Yes | - | Consumer group name |
| `filter` | string | No | - | JSONPath filter expression |
| `nextToken` | string | No | - | Pagination cursor |

**Response 200 OK:**
```json
{
  "messages": [
    {
      "messageId": "msg-123",
      "topicName": "deployment.events",
      "payload": "{\"event\":\"deployment.completed\",\"executionId\":\"abc123\"}",
      "schemaVersion": "1.0",
      "headers": {
        "source": "orchestrator-api",
        "traceId": "trace-xyz"
      },
      "priority": 5,
      "timestamp": "2025-11-16T12:00:00Z",
      "ackDeadline": "2025-11-16T12:00:30Z",
      "deliveryAttempt": 1
    }
  ],
  "nextToken": "cursor-abc123",
  "hasMore": true
}
```

**Response Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `messages` | array | Array of messages |
| `nextToken` | string | Pagination cursor (if `hasMore` is true) |
| `hasMore` | boolean | More messages available |

**Error Responses:**
- `404 Not Found` - Topic or consumer group does not exist
- `429 Too Many Requests` - Rate limit exceeded

---

### Acknowledge Message

Acknowledge successful message processing.

**Endpoint:** `POST /api/v1/messages/{messageId}/ack`
**Authorization:** Consumer, Admin
**Rate Limit:** 1000 req/min per user

**Request:**
```http
POST /api/v1/messages/msg-123/ack
Authorization: Bearer {token}
Content-Type: application/json

{
  "consumerGroup": "notification-service"
}
```

**Response 200 OK:**
```json
{
  "messageId": "msg-123",
  "status": "Acknowledged",
  "acknowledgedAt": "2025-11-16T12:00:35Z"
}
```

**Error Responses:**
- `404 Not Found` - Message not found or already acknowledged
- `410 Gone` - Ack deadline expired

---

### Negative Acknowledge (Requeue)

Negative acknowledge to requeue message for retry.

**Endpoint:** `POST /api/v1/messages/{messageId}/nack`
**Authorization:** Consumer, Admin
**Rate Limit:** 1000 req/min per user

**Request:**
```http
POST /api/v1/messages/msg-123/nack
Authorization: Bearer {token}
Content-Type: application/json

{
  "consumerGroup": "notification-service",
  "reason": "Temporary failure - database unavailable"
}
```

**Response 200 OK:**
```json
{
  "messageId": "msg-123",
  "status": "Pending",
  "requeuedAt": "2025-11-16T12:00:40Z",
  "deliveryAttempt": 2,
  "nextDeliveryAt": "2025-11-16T12:00:42Z"
}
```

**Error Responses:**
- `404 Not Found` - Message not found
- `429 Too Many Requests` - Max retries exceeded (moved to DLQ)

---

### Get Message Details

Get details about a specific message.

**Endpoint:** `GET /api/v1/messages/{messageId}`
**Authorization:** Consumer, Producer, Admin
**Rate Limit:** 300 req/min per user

**Request:**
```http
GET /api/v1/messages/msg-123
Authorization: Bearer {token}
```

**Response 200 OK:**
```json
{
  "messageId": "msg-123",
  "topicName": "deployment.events",
  "payload": "{...}",
  "schemaVersion": "1.0",
  "status": "Delivered",
  "priority": 5,
  "publishedAt": "2025-11-16T12:00:00Z",
  "deliveredAt": "2025-11-16T12:00:05Z",
  "acknowledgedAt": null,
  "deliveryAttempts": 1,
  "expiresAt": "2025-11-17T10:00:00Z"
}
```

---

### Delete Message

Delete a message (admin only).

**Endpoint:** `DELETE /api/v1/messages/{messageId}`
**Authorization:** Admin only
**Rate Limit:** 10 req/min per user

**Request:**
```http
DELETE /api/v1/messages/msg-123
Authorization: Bearer {token}
```

**Response 204 No Content**

---

## Topics API

### Create Topic

Create a new topic.

**Endpoint:** `POST /api/v1/topics`
**Authorization:** Producer, Admin
**Rate Limit:** 10 req/min per user

**Request:**
```http
POST /api/v1/topics
Authorization: Bearer {token}
Content-Type: application/json

{
  "name": "deployment.events",
  "description": "Deployment lifecycle events",
  "type": "PubSub",
  "schemaId": "deployment.event.v1",
  "deliveryGuarantee": "AtLeastOnce",
  "retentionPeriod": "P7D",
  "partitionCount": 4,
  "replicationFactor": 2,
  "config": {
    "maxMessageSize": "1048576",
    "allowAnonymousPublish": "false"
  }
}
```

**Request Fields:**

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| `name` | string | Yes | - | Topic name (alphanumeric, dots, dashes) |
| `description` | string | No | "" | Human-readable description |
| `type` | string | Yes | - | `Queue` or `PubSub` |
| `schemaId` | string | Yes | - | Schema ID for validation |
| `deliveryGuarantee` | string | No | `AtLeastOnce` | `AtMostOnce`, `AtLeastOnce`, `ExactlyOnce` |
| `retentionPeriod` | string | No | `P7D` | ISO 8601 duration (e.g., `P7D` = 7 days) |
| `partitionCount` | int | No | 1 | Number of partitions (1-16) |
| `replicationFactor` | int | No | 2 | Replication count (1-3) |
| `config` | object | No | {} | Topic-specific configuration |

**Response 201 Created:**
```json
{
  "name": "deployment.events",
  "description": "Deployment lifecycle events",
  "type": "PubSub",
  "schemaId": "deployment.event.v1",
  "deliveryGuarantee": "AtLeastOnce",
  "retentionPeriod": "P7D",
  "partitionCount": 4,
  "replicationFactor": 2,
  "createdAt": "2025-11-16T12:00:00Z",
  "updatedAt": "2025-11-16T12:00:00Z"
}
```

**Error Responses:**
- `400 Bad Request` - Invalid topic name or configuration
- `409 Conflict` - Topic already exists
- `404 Not Found` - Schema ID does not exist

---

### List Topics

List all topics.

**Endpoint:** `GET /api/v1/topics`
**Authorization:** All roles
**Rate Limit:** 60 req/min per user

**Request:**
```http
GET /api/v1/topics?type=PubSub&limit=20&offset=0
Authorization: Bearer {token}
```

**Query Parameters:**

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `type` | string | No | - | Filter by type (`Queue`, `PubSub`) |
| `limit` | int | No | 20 | Max topics to return (1-100) |
| `offset` | int | No | 0 | Pagination offset |

**Response 200 OK:**
```json
{
  "topics": [
    {
      "name": "deployment.events",
      "type": "PubSub",
      "partitionCount": 4,
      "messageCount": 15234,
      "createdAt": "2025-11-16T12:00:00Z"
    }
  ],
  "total": 42,
  "limit": 20,
  "offset": 0
}
```

---

### Get Topic Details

Get details about a specific topic.

**Endpoint:** `GET /api/v1/topics/{name}`
**Authorization:** All roles
**Rate Limit:** 60 req/min per user

**Request:**
```http
GET /api/v1/topics/deployment.events
Authorization: Bearer {token}
```

**Response 200 OK:**
```json
{
  "name": "deployment.events",
  "description": "Deployment lifecycle events",
  "type": "PubSub",
  "schemaId": "deployment.event.v1",
  "deliveryGuarantee": "AtLeastOnce",
  "retentionPeriod": "P7D",
  "partitionCount": 4,
  "replicationFactor": 2,
  "messageCount": 15234,
  "subscriptionCount": 8,
  "createdAt": "2025-11-16T12:00:00Z",
  "updatedAt": "2025-11-16T12:00:00Z"
}
```

---

### Update Topic Configuration

Update topic configuration.

**Endpoint:** `PUT /api/v1/topics/{name}`
**Authorization:** Admin only
**Rate Limit:** 10 req/min per user

**Request:**
```http
PUT /api/v1/topics/deployment.events
Authorization: Bearer {token}
Content-Type: application/json

{
  "description": "Updated description",
  "retentionPeriod": "P14D",
  "config": {
    "maxMessageSize": "2097152"
  }
}
```

**Response 200 OK:**
```json
{
  "name": "deployment.events",
  "description": "Updated description",
  "retentionPeriod": "P14D",
  "updatedAt": "2025-11-16T13:00:00Z"
}
```

**Note:** Cannot update `name`, `type`, `partitionCount`, or `replicationFactor` after creation.

---

### Delete Topic

Delete a topic (admin only).

**Endpoint:** `DELETE /api/v1/topics/{name}`
**Authorization:** Admin only
**Rate Limit:** 10 req/min per user

**Request:**
```http
DELETE /api/v1/topics/deployment.events
Authorization: Bearer {token}
```

**Response 204 No Content**

**Error Responses:**
- `409 Conflict` - Topic has active subscriptions (delete subscriptions first)

---

### Get Topic Metrics

Get real-time metrics for a topic.

**Endpoint:** `GET /api/v1/topics/{name}/metrics`
**Authorization:** All roles
**Rate Limit:** 60 req/min per user

**Request:**
```http
GET /api/v1/topics/deployment.events/metrics?window=PT1H
Authorization: Bearer {token}
```

**Query Parameters:**

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `window` | string | No | `PT1H` | Time window (ISO 8601 duration) |

**Response 200 OK:**
```json
{
  "topicName": "deployment.events",
  "window": "PT1H",
  "metrics": {
    "messagesPublished": 5432,
    "messagesDelivered": 5420,
    "messagesFailed": 12,
    "queueDepth": 150,
    "avgPublishLatencyMs": 8.5,
    "avgDeliveryLatencyMs": 45.2,
    "throughputMsgPerSec": 90.5
  },
  "timestamp": "2025-11-16T13:00:00Z"
}
```

---

## Subscriptions API

### Create Subscription

Create a new subscription to a topic.

**Endpoint:** `POST /api/v1/subscriptions`
**Authorization:** Consumer, Admin
**Rate Limit:** 10 req/min per user

**Request:**
```http
POST /api/v1/subscriptions
Authorization: Bearer {token}
Content-Type: application/json

{
  "topicName": "deployment.events",
  "consumerGroup": "notification-service",
  "consumerEndpoint": "https://notifications.example.com/webhook",
  "type": "Push",
  "filter": {
    "headerMatches": {
      "environment": "Production"
    },
    "payloadQuery": "$.event == 'deployment.completed'"
  },
  "maxRetries": 3,
  "ackTimeout": "PT30S"
}
```

**Request Fields:**

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| `topicName` | string | Yes | - | Topic to subscribe to |
| `consumerGroup` | string | Yes | - | Consumer group name |
| `consumerEndpoint` | string | Conditional | - | Webhook URL (required for Push) |
| `type` | string | Yes | - | `Push` or `Pull` |
| `filter` | object | No | null | Message filter rules |
| `maxRetries` | int | No | 3 | Max delivery retries |
| `ackTimeout` | string | No | `PT30S` | Ack deadline (ISO 8601 duration) |

**Response 201 Created:**
```json
{
  "subscriptionId": "sub-abc123",
  "topicName": "deployment.events",
  "consumerGroup": "notification-service",
  "type": "Push",
  "isActive": true,
  "createdAt": "2025-11-16T12:00:00Z"
}
```

---

### List Subscriptions

List all subscriptions (optionally filtered by topic).

**Endpoint:** `GET /api/v1/subscriptions`
**Authorization:** All roles
**Rate Limit:** 60 req/min per user

**Request:**
```http
GET /api/v1/subscriptions?topicName=deployment.events&consumerGroup=notification-service
Authorization: Bearer {token}
```

**Query Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `topicName` | string | No | Filter by topic |
| `consumerGroup` | string | No | Filter by consumer group |
| `isActive` | boolean | No | Filter by active status |

**Response 200 OK:**
```json
{
  "subscriptions": [
    {
      "subscriptionId": "sub-abc123",
      "topicName": "deployment.events",
      "consumerGroup": "notification-service",
      "type": "Push",
      "isActive": true,
      "createdAt": "2025-11-16T12:00:00Z",
      "lastConsumedAt": "2025-11-16T13:00:00Z"
    }
  ],
  "total": 8
}
```

---

### Get Subscription Details

Get details about a specific subscription.

**Endpoint:** `GET /api/v1/subscriptions/{id}`
**Authorization:** All roles
**Rate Limit:** 60 req/min per user

**Request:**
```http
GET /api/v1/subscriptions/sub-abc123
Authorization: Bearer {token}
```

**Response 200 OK:**
```json
{
  "subscriptionId": "sub-abc123",
  "topicName": "deployment.events",
  "consumerGroup": "notification-service",
  "consumerEndpoint": "https://notifications.example.com/webhook",
  "type": "Push",
  "filter": {
    "headerMatches": {
      "environment": "Production"
    }
  },
  "maxRetries": 3,
  "ackTimeout": "PT30S",
  "isActive": true,
  "createdAt": "2025-11-16T12:00:00Z",
  "lastConsumedAt": "2025-11-16T13:00:00Z",
  "messagesConsumed": 5420,
  "messagesFailed": 12
}
```

---

### Update Subscription

Update subscription configuration.

**Endpoint:** `PUT /api/v1/subscriptions/{id}`
**Authorization:** Consumer, Admin
**Rate Limit:** 10 req/min per user

**Request:**
```http
PUT /api/v1/subscriptions/sub-abc123
Authorization: Bearer {token}
Content-Type: application/json

{
  "maxRetries": 5,
  "ackTimeout": "PT60S"
}
```

**Response 200 OK:**
```json
{
  "subscriptionId": "sub-abc123",
  "maxRetries": 5,
  "ackTimeout": "PT60S",
  "updatedAt": "2025-11-16T13:30:00Z"
}
```

---

### Delete Subscription

Delete a subscription.

**Endpoint:** `DELETE /api/v1/subscriptions/{id}`
**Authorization:** Consumer, Admin
**Rate Limit:** 10 req/min per user

**Request:**
```http
DELETE /api/v1/subscriptions/sub-abc123
Authorization: Bearer {token}
```

**Response 204 No Content**

---

### Pause Subscription

Pause message delivery to a subscription.

**Endpoint:** `POST /api/v1/subscriptions/{id}/pause`
**Authorization:** Consumer, Admin
**Rate Limit:** 30 req/min per user

**Request:**
```http
POST /api/v1/subscriptions/sub-abc123/pause
Authorization: Bearer {token}
```

**Response 200 OK:**
```json
{
  "subscriptionId": "sub-abc123",
  "isActive": false,
  "pausedAt": "2025-11-16T14:00:00Z"
}
```

---

### Resume Subscription

Resume message delivery to a subscription.

**Endpoint:** `POST /api/v1/subscriptions/{id}/resume`
**Authorization:** Consumer, Admin
**Rate Limit:** 30 req/min per user

**Request:**
```http
POST /api/v1/subscriptions/sub-abc123/resume
Authorization: Bearer {token}
```

**Response 200 OK:**
```json
{
  "subscriptionId": "sub-abc123",
  "isActive": true,
  "resumedAt": "2025-11-16T14:05:00Z"
}
```

---

## Schemas API

### Register Schema

Register a new message schema.

**Endpoint:** `POST /api/v1/schemas`
**Authorization:** Producer, Admin
**Rate Limit:** 10 req/min per user

**Request:**
```http
POST /api/v1/schemas
Authorization: Bearer {token}
Content-Type: application/json

{
  "schemaId": "deployment.event.v2",
  "schemaDefinition": "{\"type\":\"object\",\"properties\":{\"event\":{\"type\":\"string\"},\"executionId\":{\"type\":\"string\"}},\"required\":[\"event\",\"executionId\"]}",
  "version": "2.0",
  "compatibility": "Backward"
}
```

**Request Fields:**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `schemaId` | string | Yes | Unique schema identifier |
| `schemaDefinition` | string | Yes | JSON Schema definition |
| `version` | string | Yes | Schema version (e.g., "2.0") |
| `compatibility` | string | No | `None`, `Backward`, `Forward`, `Full` |

**Response 201 Created:**
```json
{
  "schemaId": "deployment.event.v2",
  "version": "2.0",
  "compatibility": "Backward",
  "status": "Draft",
  "createdAt": "2025-11-16T12:00:00Z"
}
```

**Note:** Schemas in production require approval (status: `PendingApproval`).

---

### List Schemas

List all registered schemas.

**Endpoint:** `GET /api/v1/schemas`
**Authorization:** All roles
**Rate Limit:** 60 req/min per user

**Request:**
```http
GET /api/v1/schemas?status=Approved
Authorization: Bearer {token}
```

**Query Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `status` | string | No | Filter by status (`Draft`, `PendingApproval`, `Approved`, `Deprecated`) |

**Response 200 OK:**
```json
{
  "schemas": [
    {
      "schemaId": "deployment.event.v1",
      "version": "1.0",
      "status": "Approved",
      "createdAt": "2025-11-01T10:00:00Z"
    },
    {
      "schemaId": "deployment.event.v2",
      "version": "2.0",
      "status": "PendingApproval",
      "createdAt": "2025-11-16T12:00:00Z"
    }
  ],
  "total": 12
}
```

---

### Get Schema Details

Get details about a specific schema.

**Endpoint:** `GET /api/v1/schemas/{id}`
**Authorization:** All roles
**Rate Limit:** 60 req/min per user

**Request:**
```http
GET /api/v1/schemas/deployment.event.v2
Authorization: Bearer {token}
```

**Response 200 OK:**
```json
{
  "schemaId": "deployment.event.v2",
  "schemaDefinition": "{...}",
  "version": "2.0",
  "compatibility": "Backward",
  "status": "PendingApproval",
  "createdAt": "2025-11-16T12:00:00Z"
}
```

---

### Approve Schema

Approve a schema for production use (admin only).

**Endpoint:** `POST /api/v1/schemas/{id}/approve`
**Authorization:** Admin only
**Rate Limit:** 10 req/min per user

**Request:**
```http
POST /api/v1/schemas/deployment.event.v2/approve
Authorization: Bearer {token}
Content-Type: application/json

{
  "notes": "Breaking change approved - consumers notified"
}
```

**Response 200 OK:**
```json
{
  "schemaId": "deployment.event.v2",
  "status": "Approved",
  "approvedBy": "admin@example.com",
  "approvedAt": "2025-11-16T13:00:00Z"
}
```

---

### Deprecate Schema

Mark a schema as deprecated (admin only).

**Endpoint:** `POST /api/v1/schemas/{id}/deprecate`
**Authorization:** Admin only
**Rate Limit:** 10 req/min per user

**Request:**
```http
POST /api/v1/schemas/deployment.event.v1/deprecate
Authorization: Bearer {token}
Content-Type: application/json

{
  "reason": "Superseded by v2.0",
  "migrationGuide": "https://docs.example.com/migration/v1-to-v2"
}
```

**Response 200 OK:**
```json
{
  "schemaId": "deployment.event.v1",
  "status": "Deprecated",
  "deprecatedAt": "2025-11-16T14:00:00Z"
}
```

---

### Validate Message Against Schema

Validate a message payload against a schema.

**Endpoint:** `POST /api/v1/schemas/{id}/validate`
**Authorization:** Producer, Admin
**Rate Limit:** 100 req/min per user

**Request:**
```http
POST /api/v1/schemas/deployment.event.v2/validate
Authorization: Bearer {token}
Content-Type: application/json

{
  "payload": "{\"event\":\"deployment.completed\",\"executionId\":\"abc123\"}"
}
```

**Response 200 OK (Valid):**
```json
{
  "isValid": true,
  "schemaId": "deployment.event.v2",
  "validatedAt": "2025-11-16T14:30:00Z"
}
```

**Response 200 OK (Invalid):**
```json
{
  "isValid": false,
  "errors": [
    {
      "path": "$.executionId",
      "message": "Required property missing"
    }
  ],
  "schemaId": "deployment.event.v2",
  "validatedAt": "2025-11-16T14:30:00Z"
}
```

---

## Brokers API

### List Broker Nodes

List all broker nodes in the cluster.

**Endpoint:** `GET /api/v1/brokers`
**Authorization:** Admin, Viewer
**Rate Limit:** 60 req/min per user

**Request:**
```http
GET /api/v1/brokers
Authorization: Bearer {token}
```

**Response 200 OK:**
```json
{
  "brokers": [
    {
      "nodeId": "broker-1",
      "hostname": "broker1.example.com",
      "port": 5050,
      "role": "Master",
      "health": {
        "isHealthy": true,
        "queueDepth": 150,
        "activeConsumers": 12
      },
      "assignedTopics": ["deployment.events", "cluster.metrics"],
      "startedAt": "2025-11-16T10:00:00Z"
    }
  ],
  "total": 3
}
```

---

### Get Broker Details

Get detailed information about a specific broker node.

**Endpoint:** `GET /api/v1/brokers/{nodeId}`
**Authorization:** Admin, Viewer
**Rate Limit:** 60 req/min per user

**Request:**
```http
GET /api/v1/brokers/broker-1
Authorization: Bearer {token}
```

**Response 200 OK:**
```json
{
  "nodeId": "broker-1",
  "hostname": "broker1.example.com",
  "port": 5050,
  "role": "Master",
  "assignedTopics": ["deployment.events", "cluster.metrics"],
  "health": {
    "isHealthy": true,
    "queueDepth": 150,
    "activeConsumers": 12,
    "cpuUsage": 45.2,
    "memoryUsage": 62.8
  },
  "metrics": {
    "messagesPublished": 50000,
    "messagesDelivered": 49850,
    "messagesFailed": 150,
    "avgPublishLatencyMs": 8.5,
    "avgDeliveryLatencyMs": 45.2
  },
  "startedAt": "2025-11-16T10:00:00Z",
  "lastHeartbeat": "2025-11-16T14:59:50Z"
}
```

---

### Get Broker Metrics

Get real-time metrics for a broker node.

**Endpoint:** `GET /api/v1/brokers/{nodeId}/metrics`
**Authorization:** Admin, Viewer
**Rate Limit:** 60 req/min per user

**Request:**
```http
GET /api/v1/brokers/broker-1/metrics?window=PT1H
Authorization: Bearer {token}
```

**Response 200 OK:**
```json
{
  "nodeId": "broker-1",
  "window": "PT1H",
  "metrics": {
    "messagesPublished": 5432,
    "messagesDelivered": 5420,
    "messagesFailed": 12,
    "avgPublishLatencyMs": 8.5,
    "avgDeliveryLatencyMs": 45.2,
    "throughputMsgPerSec": 90.5,
    "queueDepth": 150,
    "activeConsumers": 12,
    "cpuUsage": 45.2,
    "memoryUsage": 62.8
  },
  "timestamp": "2025-11-16T15:00:00Z"
}
```

---

### Trigger Consumer Rebalancing

Manually trigger consumer rebalancing (admin only).

**Endpoint:** `POST /api/v1/brokers/{nodeId}/rebalance`
**Authorization:** Admin only
**Rate Limit:** 10 req/min per user

**Request:**
```http
POST /api/v1/brokers/broker-1/rebalance
Authorization: Bearer {token}
```

**Response 200 OK:**
```json
{
  "nodeId": "broker-1",
  "rebalanceTriggeredAt": "2025-11-16T15:00:00Z",
  "status": "InProgress"
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
    "message": "Message validation failed",
    "details": [
      {
        "field": "payload",
        "message": "Required property 'executionId' is missing"
      }
    ],
    "traceId": "trace-xyz789",
    "timestamp": "2025-11-16T15:00:00Z"
  }
}
```

### Common Error Codes

| HTTP Status | Error Code | Description |
|-------------|------------|-------------|
| 400 | `VALIDATION_ERROR` | Request validation failed |
| 400 | `SCHEMA_VALIDATION_ERROR` | Message failed schema validation |
| 401 | `UNAUTHORIZED` | Missing or invalid JWT token |
| 403 | `FORBIDDEN` | Insufficient permissions |
| 404 | `NOT_FOUND` | Resource not found |
| 409 | `CONFLICT` | Resource already exists |
| 410 | `GONE` | Resource expired or deleted |
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

**Headers:**
- `X-RateLimit-Limit` - Max requests per window
- `X-RateLimit-Remaining` - Remaining requests
- `X-RateLimit-Reset` - Unix timestamp when limit resets
- `Retry-After` - Seconds to wait before retrying (429 only)

### Rate Limits by Endpoint

| Endpoint | Limit | Window |
|----------|-------|--------|
| `POST /api/v1/messages/publish` | 100 req | 1 minute |
| `GET /api/v1/messages/consume/{topicName}` | 600 req | 1 minute |
| `POST /api/v1/messages/{messageId}/ack` | 1000 req | 1 minute |
| `POST /api/v1/messages/{messageId}/nack` | 1000 req | 1 minute |
| `POST /api/v1/topics` | 10 req | 1 minute |
| `GET /api/v1/topics` | 60 req | 1 minute |
| `POST /api/v1/subscriptions` | 10 req | 1 minute |
| `POST /api/v1/schemas` | 10 req | 1 minute |
| `POST /api/v1/schemas/{id}/validate` | 100 req | 1 minute |

---

## Pagination

List endpoints support pagination via limit/offset parameters.

### Pagination Parameters

```http
GET /api/v1/topics?limit=20&offset=40
```

**Parameters:**
- `limit` - Max items to return (1-100, default: 20)
- `offset` - Number of items to skip (default: 0)

### Pagination Response

```json
{
  "topics": [...],
  "total": 150,
  "limit": 20,
  "offset": 40,
  "hasMore": true,
  "nextOffset": 60
}
```

---

## Webhooks (Push-Based Delivery)

For push-based subscriptions, the messaging system delivers messages via HTTP POST to configured webhook endpoints.

### Webhook Request Format

```http
POST https://notifications.example.com/webhook
Content-Type: application/json
X-Message-Id: msg-123
X-Topic-Name: deployment.events
X-Delivery-Attempt: 1
X-Trace-Id: trace-xyz

{
  "messageId": "msg-123",
  "topicName": "deployment.events",
  "payload": "{\"event\":\"deployment.completed\",\"executionId\":\"abc123\"}",
  "schemaVersion": "1.0",
  "headers": {
    "source": "orchestrator-api"
  },
  "timestamp": "2025-11-16T12:00:00Z"
}
```

### Webhook Response

Consumer must respond within ack timeout (default: 30 seconds).

**Success (200-299):**
```http
HTTP/1.1 200 OK
```

**Retry (400-499, 500-599):**
```http
HTTP/1.1 500 Internal Server Error
```

System will retry with exponential backoff (2s, 4s, 8s, 16s, 32s).

---

**API Version:** 1.0.0
**Last Updated:** 2025-11-16
**Support:** api-support@example.com
