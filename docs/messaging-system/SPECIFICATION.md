# HotSwap Distributed Messaging System - Technical Specification

**Version:** 1.0.0
**Date:** 2025-11-16
**Status:** Design Specification
**Authors:** Platform Architecture Team

---

## Table of Contents

1. [Executive Summary](#executive-summary)
2. [System Requirements](#system-requirements)
3. [Message Delivery Patterns](#message-delivery-patterns)
4. [Delivery Guarantees](#delivery-guarantees)
5. [Performance Requirements](#performance-requirements)
6. [Security Requirements](#security-requirements)
7. [Observability Requirements](#observability-requirements)
8. [Scalability Requirements](#scalability-requirements)

---

## Executive Summary

The HotSwap Distributed Messaging System provides enterprise-grade asynchronous messaging capabilities built on the existing kernel orchestration platform. The system treats message brokers as hot-swappable kernel modules, enabling zero-downtime upgrades and intelligent message routing.

### Key Innovations

1. **Hot-Swappable Brokers** - Broker modules deployed via existing orchestration strategies
2. **Routing Strategies** - Deployment strategies adapted for message routing
3. **Full Traceability** - OpenTelemetry integration for end-to-end message tracking
4. **Schema Evolution** - Approval workflow for production schema changes
5. **Zero Downtime** - Broker upgrades without message loss

### Design Principles

1. **Leverage Existing Infrastructure** - Reuse authentication, tracing, metrics, approval workflows
2. **Clean Architecture** - Maintain 4-layer separation (API, Orchestrator, Infrastructure, Domain)
3. **Test-Driven Development** - 85%+ test coverage with comprehensive unit/integration tests
4. **Production-Ready** - Security, observability, and reliability from day one
5. **Performance First** - 10,000+ msg/sec throughput, < 100ms p99 latency

---

## System Requirements

### Functional Requirements

#### FR-MSG-001: Message Publishing
**Priority:** Critical
**Description:** System MUST support publishing messages to topics with metadata

**Requirements:**
- Publish message with payload (JSON format)
- Attach headers (key-value pairs)
- Set message priority (0-9, 9 highest)
- Set message expiration (TTL)
- Generate unique message ID
- Validate message against schema
- Return message ID and status (202 Accepted)

**API Endpoint:**
```
POST /api/v1/messages/publish
```

**Acceptance Criteria:**
- Message ID generated (GUID format)
- Schema validation performed before queueing
- Invalid messages rejected with 400 Bad Request
- Trace context propagated to message headers
- Message persisted to Redis/PostgreSQL

---

#### FR-MSG-002: Message Consumption
**Priority:** Critical
**Description:** System MUST support consuming messages from topics

**Requirements:**
- Support pull-based consumption (HTTP polling)
- Support push-based consumption (webhooks)
- Return batch of messages (configurable limit)
- Set acknowledgment deadline per message
- Support consumer group isolation
- Track consumer lag

**API Endpoints:**
```
GET  /api/v1/messages/consume/{topic}?limit=10&ackTimeout=30
POST /api/v1/subscriptions (for push-based)
```

**Acceptance Criteria:**
- Messages delivered in FIFO order (per partition)
- Consumer groups isolated (no message duplication across groups)
- Ack deadline enforced (requeue on timeout)
- Consumer lag tracked and exposed via metrics

---

#### FR-MSG-003: Topic Management
**Priority:** Critical
**Description:** System MUST support creating and managing topics

**Requirements:**
- Create topic with configuration
- Delete topic (admin only)
- List all topics
- Get topic details (partitions, replication factor)
- Update topic configuration
- Get topic metrics (message count, throughput)

**Topic Types:**
- **Queue** - Point-to-point (single consumer per message)
- **PubSub** - Publish-subscribe (all consumers receive message)

**API Endpoints:**
```
POST   /api/v1/topics
GET    /api/v1/topics
GET    /api/v1/topics/{name}
PUT    /api/v1/topics/{name}
DELETE /api/v1/topics/{name}
GET    /api/v1/topics/{name}/metrics
```

**Acceptance Criteria:**
- Topic names validated (alphanumeric + dots/dashes)
- Partition count configurable (1-16)
- Replication factor configurable (1-3)
- Default retention period: 7 days
- Topic deletion requires admin role

---

#### FR-MSG-004: Subscription Management
**Priority:** Critical
**Description:** System MUST support creating and managing subscriptions

**Requirements:**
- Create subscription with consumer group
- Delete subscription
- Pause/resume subscription
- Configure push endpoint (webhook URL)
- Configure pull settings (batch size, timeout)
- Set message filters (header-based, content-based)
- Set retry policy (max retries, backoff)

**API Endpoints:**
```
POST   /api/v1/subscriptions
GET    /api/v1/subscriptions
GET    /api/v1/subscriptions/{id}
PUT    /api/v1/subscriptions/{id}
DELETE /api/v1/subscriptions/{id}
POST   /api/v1/subscriptions/{id}/pause
POST   /api/v1/subscriptions/{id}/resume
```

**Acceptance Criteria:**
- Consumer group names unique per topic
- Push endpoints validated (HTTPS required in production)
- Filters applied before message delivery
- Subscription state persisted (survives broker restart)

---

#### FR-MSG-005: Schema Registry
**Priority:** High
**Description:** System MUST support schema registration and validation

**Requirements:**
- Register schema with JSON Schema format
- Validate messages against schema before publish
- Detect breaking schema changes
- Require approval for breaking changes (production)
- Support schema versioning
- Deprecate old schemas

**Schema Compatibility Modes:**
- **None** - No compatibility checks
- **Backward** - New schema can read old data
- **Forward** - Old schema can read new data
- **Full** - Bidirectional compatibility

**API Endpoints:**
```
POST   /api/v1/schemas
GET    /api/v1/schemas
GET    /api/v1/schemas/{id}
POST   /api/v1/schemas/{id}/approve (admin only)
POST   /api/v1/schemas/{id}/deprecate (admin only)
POST   /api/v1/schemas/{id}/validate
```

**Acceptance Criteria:**
- JSON Schema validation enforced
- Breaking changes detected automatically
- Production schemas require admin approval
- Schema validation time < 10ms (p99)

---

#### FR-MSG-006: Delivery Guarantees
**Priority:** Critical
**Description:** System MUST support configurable delivery guarantees

**Delivery Modes:**

1. **At-Most-Once** (Fire-and-Forget)
   - No retry on failure
   - Lowest latency
   - May lose messages

2. **At-Least-Once** (Default)
   - Retry until acknowledged
   - May deliver duplicates
   - Recommended for most use cases

3. **Exactly-Once**
   - Deduplication via distributed locks
   - Idempotency keys enforced
   - Highest latency
   - No duplicate deliveries

**Requirements:**
- Configurable per topic
- Retry with exponential backoff
- Dead letter queue for failed messages
- Idempotency key support
- Message deduplication (exactly-once)

**Acceptance Criteria:**
- At-least-once: Messages retried until ack (max 3 retries)
- Exactly-once: Zero duplicate deliveries (verified via tests)
- DLQ: Failed messages moved after max retries
- Backoff: 2s, 4s, 8s, 16s, 32s

---

#### FR-MSG-007: Routing Strategies
**Priority:** High
**Description:** System MUST support multiple message routing strategies

**Strategies:**

1. **Direct Routing**
   - Route to single consumer
   - Lowest latency
   - No load balancing

2. **Fan-Out Routing**
   - Route to ALL consumers
   - Broadcast pattern
   - Used for events/notifications

3. **Load-Balanced Routing**
   - Distribute across consumers
   - Round-robin or least-loaded
   - Maximizes throughput

4. **Priority Routing**
   - High-priority messages first
   - Priority queue implementation
   - SLA-sensitive messages

5. **Content-Based Routing**
   - Route based on message content
   - Header/payload filters
   - Complex routing rules

**Requirements:**
- Strategy configurable per topic
- Strategy selection automatic based on topic type
- Custom routing rules supported

**Acceptance Criteria:**
- All 5 strategies implemented
- Strategy switch without message loss
- Routing latency < 5ms (p99)

---

#### FR-MSG-008: Broker Node Management
**Priority:** High
**Description:** System MUST support deploying and managing broker nodes

**Requirements:**
- Deploy brokers as kernel modules
- Hot-swap brokers without message loss
- Automatic consumer rebalancing
- Master/replica topology
- Partition assignment
- Health monitoring

**Broker Roles:**
- **Master** - Handles writes (publish)
- **Replica** - Handles reads (consume)

**API Endpoints:**
```
GET  /api/v1/brokers
GET  /api/v1/brokers/{nodeId}
GET  /api/v1/brokers/{nodeId}/metrics
POST /api/v1/brokers/{nodeId}/rebalance
```

**Acceptance Criteria:**
- Broker upgrade < 30 seconds
- Zero message loss during upgrade
- Consumer rebalancing < 5 seconds
- Master election within 10 seconds

---

#### FR-MSG-009: Dead Letter Queue
**Priority:** High
**Description:** System MUST support dead letter queues for failed messages

**Requirements:**
- Automatic DLQ creation per topic
- Move failed messages after max retries
- Preserve original message metadata
- Support message replay from DLQ
- DLQ metrics (count, age)

**DLQ Naming:**
```
{topic-name}.dlq
```

**API Endpoints:**
```
GET  /api/v1/topics/{name}/dlq
POST /api/v1/topics/{name}/dlq/{messageId}/replay
```

**Acceptance Criteria:**
- Failed messages moved to DLQ
- Original message preserved
- Replay functionality working
- DLQ monitored via alerts

---

## Message Delivery Patterns

### 1. Point-to-Point (Queue)

**Use Case:** Command messages where exactly one consumer processes each message

**Behavior:**
- Message delivered to only one consumer in the consumer group
- Load balancing across consumers
- Acknowledgment required for message removal

**Configuration:**
```json
{
  "name": "deployment.commands",
  "type": "Queue",
  "deliveryGuarantee": "AtLeastOnce"
}
```

**Message Flow:**
```
Producer → [Queue] → Consumer1 (msg1)
                  → Consumer2 (msg2)
                  → Consumer3 (msg3)
```

---

### 2. Publish-Subscribe (Fan-Out)

**Use Case:** Event notifications where all subscribers receive the message

**Behavior:**
- Message delivered to ALL active subscribers
- Each consumer group receives independent copy
- No acknowledgment required (fire-and-forget or at-least-once)

**Configuration:**
```json
{
  "name": "deployment.events",
  "type": "PubSub",
  "deliveryGuarantee": "AtLeastOnce"
}
```

**Message Flow:**
```
Producer → [Topic] → ConsumerGroup1 (all receive)
                  → ConsumerGroup2 (all receive)
                  → ConsumerGroup3 (all receive)
```

---

### 3. Request-Reply (RPC-Style)

**Use Case:** Synchronous-like operations over async messaging

**Behavior:**
- Producer sends message with `replyTo` header
- Consumer processes and publishes reply to `replyTo` topic
- Producer waits for reply with matching `correlationId`

**Configuration:**
```json
{
  "topicName": "health.check.requests",
  "headers": {
    "replyTo": "health.check.replies",
    "correlationId": "abc-123"
  }
}
```

**Message Flow:**
```
Producer → [Request Queue] → Consumer
         ← [Reply Queue]   ← Consumer
```

---

### 4. Streaming (Real-Time Data)

**Use Case:** Continuous data streams (metrics, logs)

**Behavior:**
- High-throughput, low-latency message delivery
- Consumer reads messages in batches
- Retention-based expiration

**Configuration:**
```json
{
  "name": "cluster.metrics",
  "type": "PubSub",
  "retentionPeriod": "PT1H",
  "partitionCount": 4
}
```

**Message Flow:**
```
Metrics Producer → [Stream] → Batch Consumer (100 msgs/batch)
```

---

## Delivery Guarantees

### At-Most-Once

**Characteristics:**
- Message sent once, no retry
- Lowest latency
- May lose messages on failure

**Use Cases:**
- Non-critical metrics
- Telemetry data
- Best-effort notifications

**Implementation:**
```csharp
public async Task PublishAsync(Message message)
{
    try
    {
        await _broker.SendAsync(message);
        // No retry, no ack required
    }
    catch
    {
        // Log and continue (message lost)
    }
}
```

---

### At-Least-Once (Default)

**Characteristics:**
- Message retried until acknowledged
- May deliver duplicates
- Recommended for most use cases

**Use Cases:**
- Event notifications
- Command processing (idempotent)
- Audit logs

**Implementation:**
```csharp
public async Task PublishAsync(Message message)
{
    int attempts = 0;
    while (attempts < 3)
    {
        try
        {
            await _broker.SendAsync(message);
            await WaitForAckAsync(message.MessageId);
            return; // Success
        }
        catch
        {
            attempts++;
            await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempts)));
        }
    }
    await MoveToDLQAsync(message);
}
```

---

### Exactly-Once

**Characteristics:**
- No duplicate deliveries
- Uses distributed locks
- Highest latency
- Strongest guarantee

**Use Cases:**
- Financial transactions
- Inventory updates
- Critical state changes

**Implementation:**
```csharp
public async Task PublishAsync(Message message)
{
    var idempotencyKey = message.Headers["IdempotencyKey"];

    using (var lock = await _distributedLock.AcquireAsync(idempotencyKey))
    {
        // Check if already processed
        if (await _deduplication.ExistsAsync(idempotencyKey))
            return; // Already processed

        await _broker.SendAsync(message);
        await _deduplication.StoreAsync(idempotencyKey);
    }
}
```

---

## Performance Requirements

### Throughput

| Metric | Target | Notes |
|--------|--------|-------|
| Single Broker | 10,000 msg/sec | In-memory queues |
| 3-Node Cluster | 30,000 msg/sec | Partitioned topics |
| 10-Node Cluster | 100,000 msg/sec | Full horizontal scale |

### Latency

| Operation | p50 | p95 | p99 |
|-----------|-----|-----|-----|
| Publish | 5ms | 10ms | 20ms |
| Consume | 10ms | 30ms | 50ms |
| End-to-End | 20ms | 50ms | 100ms |
| Schema Validation | 2ms | 5ms | 10ms |

### Availability

| Metric | Target | Notes |
|--------|--------|-------|
| Message Durability | 99.99% | No loss on failures |
| Broker Uptime | 99.9% | 3-node cluster |
| Consumer Availability | 99.5% | Auto-rebalancing |

### Scalability

| Resource | Limit | Notes |
|----------|-------|-------|
| Max Topics | 1,000 | Per cluster |
| Max Partitions | 10,000 | Across all topics |
| Max Consumers | 10,000 | Per broker |
| Max Message Size | 1 MB | Configurable |
| Max Batch Size | 100 messages | Per consume request |

---

## Security Requirements

### Authentication

**Requirement:** All API endpoints MUST require JWT authentication (except /health)

**Implementation:**
- Reuse existing JWT authentication middleware
- Validate token on every request
- Extract user identity and roles

### Authorization

**Role-Based Access Control:**

| Role | Permissions |
|------|-------------|
| **Admin** | Full access (create topics, approve schemas, delete messages) |
| **Producer** | Publish messages, create topics |
| **Consumer** | Consume messages, create subscriptions |
| **Viewer** | Read-only access (list topics, view metrics) |

**Endpoint Authorization:**
```
POST   /api/v1/messages/publish       - Producer, Admin
GET    /api/v1/messages/consume/{topic} - Consumer, Admin
POST   /api/v1/topics                 - Producer, Admin
DELETE /api/v1/topics/{name}          - Admin only
POST   /api/v1/schemas/{id}/approve   - Admin only
```

### Transport Security

**Requirements:**
- HTTPS/TLS 1.2+ enforced (production)
- HSTS headers sent
- Certificate validation

### Message Encryption

**Requirements:**
- Optional end-to-end encryption
- Payload encrypted before publish
- Consumer decrypts after consume
- Keys managed via Azure Key Vault / HashiCorp Vault

### Rate Limiting

**Requirements:**
- Prevent message spam
- Protect broker from overload
- Configurable per endpoint

**Limits (Production):**
```
Publish:     100 req/min per user
Consume:     600 req/min per user
Topics:      10 req/min per user
Schemas:     10 req/min per user
```

---

## Observability Requirements

### Distributed Tracing

**Requirement:** ALL message operations MUST be traced end-to-end

**Spans:**
1. `message.publish` - Publish operation
2. `message.queue` - Queue storage
3. `message.route` - Routing decision
4. `message.deliver` - Delivery to consumer
5. `message.ack` - Acknowledgment

**Trace Context:**
- Propagate W3C trace context in message headers
- Link producer and consumer spans
- Include message metadata in span attributes

**Example Trace:**
```
Root Span: message.publish
  ├─ Child: message.validate (schema validation)
  ├─ Child: message.queue (Redis persistence)
  └─ Child: message.route
      └─ Child: message.deliver
          └─ Child: message.ack
```

### Metrics

**Required Metrics:**

**Counters:**
- `messages.published.total` - Total messages published
- `messages.delivered.total` - Total messages delivered
- `messages.failed.total` - Total failed deliveries
- `messages.dlq.total` - Total messages in DLQ
- `messages.acknowledged.total` - Total acknowledged messages

**Histograms:**
- `message.publish.duration` - Publish latency
- `message.delivery.duration` - Delivery latency
- `message.e2e.duration` - End-to-end latency
- `message.size.bytes` - Message payload size

**Gauges:**
- `topics.count` - Total topics
- `subscriptions.count` - Total subscriptions
- `consumers.active` - Active consumers
- `queue.depth` - Messages queued per topic
- `consumer.lag` - Messages behind per consumer group

### Logging

**Requirements:**
- Structured logging (JSON format)
- Trace ID correlation
- Log levels: Debug, Info, Warning, Error
- Contextual enrichment

**Example Log:**
```json
{
  "timestamp": "2025-11-16T12:00:00Z",
  "level": "INFO",
  "message": "Message published",
  "traceId": "abc-123",
  "messageId": "msg-456",
  "topicName": "deployment.events",
  "size": 1024,
  "userId": "admin@example.com"
}
```

### Health Monitoring

**Requirements:**
- Broker health checks every 30 seconds
- Consumer heartbeat tracking
- Queue depth monitoring
- Consumer lag alerting

**Health Check Endpoint:**
```
GET /api/v1/brokers/{nodeId}/health

Response:
{
  "status": "Healthy",
  "queueDepth": 150,
  "activeConsumers": 12,
  "cpuUsage": 45.2,
  "memoryUsage": 62.8,
  "lastHeartbeat": "2025-11-16T12:00:00Z"
}
```

---

## Scalability Requirements

### Horizontal Scaling

**Requirements:**
- Add broker nodes without downtime
- Automatic partition rebalancing
- Consumer group scaling
- Linear throughput increase

**Scaling Targets:**
```
1 Broker  → 10K msg/sec
3 Brokers → 30K msg/sec
10 Brokers → 100K msg/sec
```

### Partitioning

**Requirements:**
- Topics partitioned across brokers
- Partition count configurable (1-16)
- Partition key for ordered delivery
- Automatic partition assignment

**Partition Strategy:**
```csharp
int partition = Hash(message.PartitionKey) % partitionCount;
```

### Replication

**Requirements:**
- Topic replication for high availability
- Replication factor configurable (1-3)
- Synchronous replication (strong consistency)
- Automatic failover to replicas

**Replication Flow:**
```
Producer → Master Broker → Replica 1
                        → Replica 2

(Ack after all replicas confirm)
```

### Resource Limits

**Per Broker:**
- CPU: < 80% sustained
- Memory: < 75% of allocated
- Disk: < 70% of allocated
- Network: < 1 Gbps

**Auto-Scaling Triggers:**
- CPU > 70% for 5 minutes → Scale up
- Queue depth > 10,000 → Scale up
- CPU < 30% for 15 minutes → Scale down

---

## Non-Functional Requirements

### Reliability

- Message durability: 99.99%
- Broker uptime: 99.9% (3-node cluster)
- Zero message loss during broker upgrades
- Automatic failover < 10 seconds

### Maintainability

- Clean code (SOLID principles)
- 85%+ test coverage
- Comprehensive documentation
- Runbooks for common operations

### Testability

- Unit tests for all components
- Integration tests for end-to-end flows
- Performance tests for load scenarios
- Chaos testing for failure scenarios

### Compliance

- Audit logging for all operations
- Schema approval workflow (production)
- Data retention policies
- GDPR compliance (message deletion)

---

## Dependencies

### Required Infrastructure

1. **Redis 7+** - Message persistence, distributed locks
2. **PostgreSQL 15+** - Schema registry, durable storage
3. **.NET 8.0 Runtime** - Application runtime
4. **Jaeger** - Distributed tracing (optional)
5. **Prometheus** - Metrics collection (optional)

### External Services

1. **Azure Key Vault** / **HashiCorp Vault** - Secret management
2. **SMTP Server** - Email notifications (approval workflow)
3. **Webhook Endpoints** - Push-based message delivery

---

## Acceptance Criteria

**System is production-ready when:**

1. ✅ All functional requirements implemented
2. ✅ 85%+ test coverage achieved
3. ✅ Performance targets met (10K msg/sec, < 100ms p99)
4. ✅ Security requirements satisfied (JWT, HTTPS, RBAC)
5. ✅ Observability complete (tracing, metrics, logging)
6. ✅ Documentation complete (API docs, deployment guide, runbooks)
7. ✅ Zero-downtime broker upgrade verified
8. ✅ Disaster recovery tested
9. ✅ Load testing passed (100K msg/sec cluster)
10. ✅ Production deployment successful

---

**Document Version:** 1.0.0
**Last Updated:** 2025-11-16
**Next Review:** After Epic 1 Implementation
**Approval Status:** Pending Architecture Review
