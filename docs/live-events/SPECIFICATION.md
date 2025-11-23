# HotSwap Live Event Configuration System - Technical Specification

**Version:** 1.0.0
**Date:** 2025-11-23
**Status:** Design Specification
**Authors:** Platform Architecture Team

---

## Table of Contents

1. [Executive Summary](#executive-summary)
2. [System Requirements](#system-requirements)
3. [Event Lifecycle](#event-lifecycle)
4. [Rollout Strategies](#rollout-strategies)
5. [Performance Requirements](#performance-requirements)
6. [Security Requirements](#security-requirements)
7. [Observability Requirements](#observability-requirements)
8. [Scalability Requirements](#scalability-requirements)

---

## Executive Summary

The HotSwap Live Event Configuration System provides enterprise-grade live event management capabilities for online games and interactive applications. The system treats event configurations as hot-swappable modules, enabling zero-downtime updates and progressive geographic rollouts.

### Key Innovations

1. **Hot-Swappable Events** - Event configs deployed via existing orchestration strategies
2. **Geographic Rollout** - Region-based progressive deployment with automated rollback
3. **Real-Time Engagement** - Player participation and revenue metrics tracked live
4. **Automated Lifecycle** - Events activate/deactivate based on schedule
5. **Player Segmentation** - Target specific player cohorts with precision

### Design Principles

1. **Leverage Existing Infrastructure** - Reuse authentication, tracing, metrics, approval workflows
2. **Clean Architecture** - Maintain 4-layer separation (API, Orchestrator, Infrastructure, Domain)
3. **Test-Driven Development** - 85%+ test coverage with comprehensive unit/integration tests
4. **Production-Ready** - Security, observability, and reliability from day one
5. **Performance First** - < 5s event activation, 50K+ player queries/sec

---

## System Requirements

### Functional Requirements

#### FR-EVT-001: Event Creation
**Priority:** Critical
**Description:** System MUST support creating live events with configuration

**Requirements:**
- Create event with metadata (name, display name, description)
- Define event type (SeasonalPromotion, LimitedTimeOffer, CompetitiveEvent, ContentRelease)
- Set event schedule (start time, end time, timezone)
- Configure event parameters (rewards, multipliers, unlocks)
- Validate configuration schema
- Generate unique event ID
- Return event ID and status (201 Created)

**API Endpoint:**
```
POST /api/v1/events
```

**Acceptance Criteria:**
- Event ID generated (slug format: "summer-fest-2025")
- Configuration validation performed before storage
- Invalid configurations rejected with 400 Bad Request
- Trace context propagated to event operations
- Event stored to PostgreSQL

---

#### FR-EVT-002: Event Deployment
**Priority:** Critical
**Description:** System MUST support deploying events to geographic regions

**Requirements:**
- Deploy event to specific regions (us-east, us-west, eu-central, ap-southeast)
- Select rollout strategy (Canary, BlueGreen, Rolling, Geographic)
- Configure rollout parameters (target percentage, batch size, delay)
- Track deployment progress in real-time
- Support automated rollback on metric thresholds
- Record deployment history

**API Endpoints:**
```
POST /api/v1/deployments
GET  /api/v1/deployments/{id}
POST /api/v1/deployments/{id}/rollback
```

**Acceptance Criteria:**
- Deployment executed progressively per strategy
- Region health monitored during rollout
- Automatic rollback on engagement drop > 20%
- Deployment state persisted (survives restarts)

---

#### FR-EVT-003: Event Lifecycle Management
**Priority:** Critical
**Description:** System MUST automatically manage event lifecycle

**Requirements:**
- Schedule event activation at start time
- Schedule event deactivation at end time
- Support manual activation/deactivation
- Handle timezone conversions correctly
- Send notifications on lifecycle changes
- Support event extensions (延长活动)

**Event States:**
- **Draft** - Event created but not deployed
- **Scheduled** - Event scheduled for future activation
- **Active** - Event currently running
- **Paused** - Event temporarily disabled
- **Completed** - Event ended naturally
- **Cancelled** - Event ended prematurely

**API Endpoints:**
```
POST /api/v1/events/{id}/activate
POST /api/v1/events/{id}/deactivate
POST /api/v1/events/{id}/pause
POST /api/v1/events/{id}/resume
PUT  /api/v1/events/{id}/extend
```

**Acceptance Criteria:**
- Events activate/deactivate within 5 seconds of scheduled time
- Timezone handling accurate (no off-by-one hour errors)
- State transitions logged for audit trail
- Notifications sent to webhooks on state changes

---

#### FR-EVT-004: Player Event Queries
**Priority:** Critical
**Description:** System MUST support high-throughput player event queries

**Requirements:**
- Query active events for a player (by player ID)
- Filter events by region, language, platform
- Support player segmentation (VIP, new, inactive)
- Cache results for performance (Redis)
- Return event configuration to game client
- Handle 50,000+ queries per second

**API Endpoints:**
```
GET /api/v1/players/{playerId}/events
GET /api/v1/regions/{region}/events
```

**Query Parameters:**
- `playerId` - Player identifier
- `region` - Geographic region
- `language` - Player language (en, es, fr, de, ja, zh)
- `platform` - Platform (pc, mobile, console)
- `includeConfig` - Include full event configuration (default: true)

**Acceptance Criteria:**
- Query latency p99 < 50ms (with Redis cache)
- Cache hit rate > 95%
- Handle 50K+ queries/sec per server
- Graceful degradation on cache miss

---

#### FR-EVT-005: Player Segmentation
**Priority:** High
**Description:** System MUST support targeting specific player cohorts

**Requirements:**
- Define player segments (VIP, whale, new, inactive, at-risk)
- Target events to specific segments
- Support multiple targeting criteria (level, spend, days since last login)
- Dynamic segment membership (recalculated in real-time)
- Segment exclusion rules (e.g., exclude VIP from beginner events)

**Segment Types:**
- **VIP Tier** - Players with high lifetime value (> $100)
- **New Players** - Account age < 7 days
- **Inactive Players** - Last login > 30 days ago
- **High Engagement** - Daily active for last 7 days
- **At-Risk** - Declining engagement (50% drop in sessions)

**API Endpoints:**
```
POST   /api/v1/segments
GET    /api/v1/segments
GET    /api/v1/segments/{id}/players
POST   /api/v1/events/{eventId}/segments/{segmentId}/assign
DELETE /api/v1/events/{eventId}/segments/{segmentId}/remove
```

**Acceptance Criteria:**
- Segment membership calculated within 1 second
- Support up to 10 million players per segment
- Segment targeting applied before event query response

---

#### FR-EVT-006: Engagement Metrics Tracking
**Priority:** High
**Description:** System MUST track real-time player engagement metrics

**Requirements:**
- Track participation rate (% of players who interacted with event)
- Track conversion rate (% of participants who completed event goal)
- Track revenue impact (purchases during event period)
- Track player sentiment (positive/negative feedback)
- Calculate KPIs in real-time (< 1 second lag)
- Aggregate metrics by region, segment, time period

**Metrics:**

**Engagement Metrics:**
- `event.participation.rate` - % of active players who engaged
- `event.completion.rate` - % of participants who finished
- `event.session.duration` - Average session length during event
- `event.dau` - Daily active users during event
- `event.mau` - Monthly active users during event

**Revenue Metrics:**
- `event.revenue.total` - Total revenue during event period
- `event.revenue.uplift` - % increase vs. baseline
- `event.arpu` - Average revenue per user
- `event.arppu` - Average revenue per paying user
- `event.conversion.rate` - % of players who made a purchase

**Sentiment Metrics:**
- `event.feedback.positive` - Count of positive feedback
- `event.feedback.negative` - Count of negative feedback
- `event.nps` - Net Promoter Score
- `event.retention.rate` - % of players who returned after event

**API Endpoints:**
```
GET /api/v1/events/{id}/metrics
GET /api/v1/events/{id}/metrics/engagement
GET /api/v1/events/{id}/metrics/revenue
GET /api/v1/events/{id}/metrics/sentiment
```

**Acceptance Criteria:**
- Metrics updated in real-time (< 1 second lag)
- Historical metrics queryable (30-day window)
- Metrics exported to Prometheus
- Dashboards available in Grafana

---

#### FR-EVT-007: A/B Testing
**Priority:** Medium
**Description:** System MUST support A/B testing multiple event variants

**Requirements:**
- Create multiple event variants (A, B, C)
- Split player traffic across variants (50/50, 33/33/33)
- Track variant performance independently
- Automatic winner selection based on KPI
- Support multi-armed bandit optimization
- Gradual traffic shift to winning variant

**API Endpoints:**
```
POST /api/v1/events/{id}/variants
GET  /api/v1/events/{id}/variants/{variantId}/metrics
POST /api/v1/events/{id}/variants/{variantId}/promote
```

**Acceptance Criteria:**
- Traffic split accurate (±2%)
- Variant assignment stable per player (no flip-flopping)
- Winner declared when statistical significance > 95%
- Automatic traffic shift to winner over 24 hours

---

#### FR-EVT-008: Configuration Validation
**Priority:** High
**Description:** System MUST validate event configurations before deployment

**Requirements:**
- Validate JSON schema for event configuration
- Check for conflicting events (overlapping schedules)
- Verify referenced assets exist (images, audio, localization files)
- Validate reward configurations (item IDs, currency amounts)
- Require approval for production deployments (admin only)

**Validation Rules:**
- Event name must be unique
- Start time must be in the future (or within 5 minutes for testing)
- End time must be after start time
- Reward item IDs must exist in game database
- Currency amounts must be positive
- No overlapping events in same category

**API Endpoints:**
```
POST /api/v1/events/{id}/validate
POST /api/v1/events/{id}/approve (admin only)
```

**Acceptance Criteria:**
- All validation rules enforced
- Invalid configurations rejected with detailed error messages
- Production deployments require admin approval
- Validation time < 500ms

---

#### FR-EVT-009: Event Rollback
**Priority:** Critical
**Description:** System MUST support instant event rollback

**Requirements:**
- Rollback event to previous configuration
- Rollback deployment to previous regions
- Automatic rollback on metric thresholds
- Manual rollback via API or admin UI
- Preserve rollback history (audit trail)

**Rollback Triggers:**
- Participation rate drop > 20%
- Negative feedback spike > 50 complaints/minute
- Server error rate > 5%
- Manual trigger by admin

**API Endpoints:**
```
POST /api/v1/events/{id}/rollback
GET  /api/v1/events/{id}/rollback-history
```

**Acceptance Criteria:**
- Rollback completes within 30 seconds
- Zero player data loss during rollback
- Rollback history preserved indefinitely

---

## Event Lifecycle

### Event State Machine

```
     ┌────────┐
     │ Draft  │
     └───┬────┘
         │ deploy
         ▼
   ┌──────────┐
   │Scheduled │
   └────┬─────┘
        │ activate (auto/manual)
        ▼
   ┌────────┐     pause      ┌────────┐
   │ Active │ ◄──────────────┤ Paused │
   └───┬────┘  ──────────────►└────────┘
       │           resume
       │ deactivate
       ▼
   ┌───────────┐              ┌───────────┐
   │ Completed │              │ Cancelled │
   └───────────┘              └───────────┘
```

### State Transition Rules

| From | To | Trigger | Validation |
|------|----|---------| ----------|
| Draft | Scheduled | deploy() | Configuration valid, start time > now |
| Scheduled | Active | auto/activate() | Current time >= start time |
| Active | Paused | pause() | Admin only |
| Paused | Active | resume() | Admin only |
| Active | Completed | auto/deactivate() | Current time >= end time |
| Active | Cancelled | cancel() | Admin only |
| Scheduled | Cancelled | cancel() | Admin only |

---

## Rollout Strategies

### 1. Canary Rollout

**Use Case:** Progressive rollout with safety validation

**Behavior:**
- Deploy to 10% of region → monitor → 30% → 50% → 100%
- Automatic rollback on metric threshold breach
- Configurable batch sizes and delays

**Configuration:**
```json
{
  "strategy": "Canary",
  "batches": [10, 30, 50, 100],
  "batchDelay": "PT5M",
  "rollbackThreshold": {
    "participationRateDrop": 0.2,
    "errorRateIncrease": 0.05
  }
}
```

---

### 2. Blue-Green Rollout

**Use Case:** Instant region switch with quick rollback

**Behavior:**
- Deploy to "green" environment (inactive)
- Switch traffic to green instantly
- Keep blue environment for quick rollback

**Configuration:**
```json
{
  "strategy": "BlueGreen",
  "targetEnvironment": "green",
  "switchDelay": "PT0S"
}
```

---

### 3. Rolling Rollout

**Use Case:** Region-by-region deployment

**Behavior:**
- Deploy to regions sequentially (us-east → us-west → eu-central → ap-southeast)
- Monitor each region before next
- Continue on success, halt on failure

**Configuration:**
```json
{
  "strategy": "Rolling",
  "regionOrder": ["us-east", "us-west", "eu-central", "ap-southeast"],
  "regionDelay": "PT10M"
}
```

---

### 4. Geographic Rollout

**Use Case:** Time-zone aware event activation

**Behavior:**
- Activate event at same local time across regions
- Us-east: 12:00 EST, us-west: 12:00 PST, etc.
- Automatic timezone conversion

**Configuration:**
```json
{
  "strategy": "Geographic",
  "localTime": "12:00:00",
  "regions": ["us-east", "us-west", "eu-central", "ap-southeast"]
}
```

---

## Performance Requirements

### Throughput

| Metric | Target | Notes |
|--------|--------|-------|
| Player Event Queries | 50,000/sec | Per server, with Redis cache |
| Event Activations | 100/sec | Concurrent event activations |
| Metric Updates | 10,000/sec | Real-time engagement tracking |

### Latency

| Operation | p50 | p95 | p99 |
|-----------|-----|-----|-----|
| Player Event Query | 10ms | 30ms | 50ms |
| Event Activation | 2s | 4s | 5s |
| Metric Calculation | 100ms | 300ms | 500ms |
| Deployment Rollback | 10s | 20s | 30s |

### Availability

| Metric | Target | Notes |
|--------|--------|-------|
| Event Query Uptime | 99.95% | 4.38 hours downtime/year |
| Event Activation Success | 99.9% | 1 failure per 1000 activations |
| Cache Hit Rate | 95%+ | Redis cache for player queries |

### Scalability

| Resource | Limit | Notes |
|----------|-------|-------|
| Max Active Events | 1,000 | Across all regions |
| Max Player Segments | 100 | Per event |
| Max Event Configurations | 50,000 | Historical events stored |
| Max Players per Segment | 10 million | Segment membership query |

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
| **Admin** | Full access (create events, deploy, approve, rollback) |
| **GameDesigner** | Create/edit events, view metrics (no deploy/approve) |
| **Developer** | View events, metrics (read-only) |
| **Player** | Query active events only |

**Endpoint Authorization:**
```
POST   /api/v1/events                    - GameDesigner, Admin
POST   /api/v1/deployments               - Admin only
POST   /api/v1/events/{id}/approve       - Admin only
POST   /api/v1/events/{id}/rollback      - Admin only
GET    /api/v1/players/{id}/events       - Player, Developer, Admin
```

### Transport Security

**Requirements:**
- HTTPS/TLS 1.2+ enforced (production)
- HSTS headers sent
- Certificate validation

### Configuration Encryption

**Requirements:**
- Optional sensitive field encryption (API keys, webhooks)
- Fields encrypted before storage
- Decrypted on deployment
- Keys managed via HashiCorp Vault (self-hosted) or Kubernetes Secrets

### Rate Limiting

**Requirements:**
- Prevent query spam
- Protect servers from overload
- Configurable per endpoint

**Limits (Production):**
```
Player Event Queries:  1000 req/min per player
Event Creation:        10 req/min per user
Deployments:           5 req/min per user
Metrics Queries:       60 req/min per user
```

---

## Observability Requirements

### Distributed Tracing

**Requirement:** ALL event operations MUST be traced end-to-end

**Spans:**
1. `event.create` - Event creation operation
2. `event.validate` - Configuration validation
3. `event.deploy` - Deployment operation
4. `event.activate` - Event activation
5. `event.query` - Player event query
6. `metrics.calculate` - Engagement metrics calculation

**Trace Context:**
- Propagate W3C trace context in HTTP headers
- Link event operations across services
- Include event metadata in span attributes

**Example Trace:**
```
Root Span: event.deploy
  ├─ Child: event.validate (configuration validation)
  ├─ Child: deployment.canary.batch1 (10% rollout)
  ├─ Child: metrics.monitor (engagement tracking)
  └─ Child: deployment.canary.batch2 (30% rollout)
```

### Metrics

**Required Metrics:**

**Counters:**
- `events.created.total` - Total events created
- `events.deployed.total` - Total events deployed
- `events.activated.total` - Total events activated
- `events.rollback.total` - Total rollbacks
- `player.queries.total` - Total player queries

**Histograms:**
- `event.activation.duration` - Event activation latency
- `event.query.duration` - Player query latency
- `metrics.calculation.duration` - Metrics calculation time
- `deployment.duration` - Full deployment duration

**Gauges:**
- `events.active.count` - Currently active events
- `players.active.count` - Active players per event
- `cache.hit.rate` - Redis cache hit rate
- `deployment.progress.percent` - Current deployment progress

### Logging

**Requirements:**
- Structured logging (JSON format)
- Trace ID correlation
- Log levels: Debug, Info, Warning, Error
- Contextual enrichment

**Example Log:**
```json
{
  "timestamp": "2025-11-23T12:00:00Z",
  "level": "INFO",
  "message": "Event activated",
  "traceId": "abc-123",
  "eventId": "summer-fest-2025",
  "region": "us-east",
  "playersAffected": 150000,
  "userId": "admin@example.com"
}
```

### Health Monitoring

**Requirements:**
- Event scheduler health check every 30 seconds
- Region health tracking
- Cache availability monitoring
- Database connection pool monitoring

**Health Check Endpoint:**
```
GET /api/v1/health

Response:
{
  "status": "Healthy",
  "activeEvents": 42,
  "scheduledEvents": 15,
  "cacheHitRate": 0.97,
  "dbConnectionPool": 45,
  "lastSchedulerRun": "2025-11-23T12:00:00Z"
}
```

---

## Scalability Requirements

### Horizontal Scaling

**Requirements:**
- Add application servers without downtime
- Load balancing across servers
- Shared state via Redis
- Linear query throughput increase

**Scaling Targets:**
```
1 Server  → 50K queries/sec
3 Servers → 150K queries/sec
10 Servers → 500K queries/sec
```

### Geographic Distribution

**Requirements:**
- Support 10+ geographic regions
- Region-specific deployments
- Cross-region event queries
- Region failover capability

**Supported Regions:**
- `us-east` - US East Coast
- `us-west` - US West Coast
- `us-central` - US Central
- `eu-west` - Europe West
- `eu-central` - Europe Central
- `ap-southeast` - Asia Pacific Southeast
- `ap-northeast` - Asia Pacific Northeast
- `sa-east` - South America East
- `me-central` - Middle East Central
- `af-south` - Africa South

### Caching Strategy

**Requirements:**
- Redis cache for active events
- Cache invalidation on event updates
- Fallback to database on cache miss
- Cache warming on event activation

**Cache TTL:**
- Active events: 60 seconds
- Player segments: 300 seconds
- Metrics: 10 seconds

### Resource Limits

**Per Server:**
- CPU: < 80% sustained
- Memory: < 75% of allocated
- Disk: < 70% of allocated
- Network: < 1 Gbps

**Auto-Scaling Triggers:**
- CPU > 70% for 5 minutes → Scale up
- Query latency p99 > 100ms → Scale up
- CPU < 30% for 15 minutes → Scale down

---

## Non-Functional Requirements

### Reliability

- Event activation success rate: 99.9%
- Query uptime: 99.95%
- Zero data loss during deployments/rollbacks
- Automatic failover < 30 seconds

### Maintainability

- Clean code (SOLID principles)
- 85%+ test coverage
- Comprehensive documentation
- Runbooks for common operations

### Testability

- Unit tests for all components
- Integration tests for end-to-end flows
- Load tests for performance validation
- Chaos tests for failure scenarios

### Compliance

- Audit logging for all operations
- Event approval workflow (production)
- Data retention policies (30 days for metrics)
- GDPR compliance (player data deletion)

---

## Dependencies

### Required Infrastructure

1. **Redis 7+** - Event cache, distributed locks
2. **PostgreSQL 15+** - Event storage, durable persistence
3. **.NET 8.0 Runtime** - Application runtime
4. **Jaeger** - Distributed tracing (optional)
5. **Prometheus** - Metrics collection (optional)
6. **Grafana** - Metrics visualization (optional)

### External Services

1. **HashiCorp Vault (self-hosted)** / **Kubernetes Secrets** - Secret management
2. **SMTP Server** - Email notifications (approval workflow)
3. **Webhook Endpoints** - Event lifecycle notifications
4. **CDN** - Event asset delivery (images, audio)

---

## Acceptance Criteria

**System is production-ready when:**

1. ✅ All functional requirements implemented
2. ✅ 85%+ test coverage achieved
3. ✅ Performance targets met (50K queries/sec, < 5s activation)
4. ✅ Security requirements satisfied (JWT, HTTPS, RBAC)
5. ✅ Observability complete (tracing, metrics, logging)
6. ✅ Documentation complete (API docs, deployment guide, runbooks)
7. ✅ Zero-downtime deployment verified
8. ✅ Rollback tested (< 30 seconds)
9. ✅ Load testing passed (100K queries/sec cluster)
10. ✅ Production deployment successful

---

**Document Version:** 1.0.0
**Last Updated:** 2025-11-23
**Next Review:** After Epic 1 Implementation
**Approval Status:** Pending Architecture Review
