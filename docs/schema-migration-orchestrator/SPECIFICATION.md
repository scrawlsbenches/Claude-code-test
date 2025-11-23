# HotSwap Database Schema Migration Orchestrator - Technical Specification

**Version:** 1.0.0
**Date:** 2025-11-23
**Status:** Design Specification
**Authors:** Platform Architecture Team

---

## Table of Contents

1. [Executive Summary](#executive-summary)
2. [System Requirements](#system-requirements)
3. [Migration Strategies](#migration-strategies)
4. [Safety Mechanisms](#safety-mechanisms)
5. [Performance Requirements](#performance-requirements)
6. [Security Requirements](#security-requirements)
7. [Observability Requirements](#observability-requirements)
8. [Scalability Requirements](#scalability-requirements)

---

## Executive Summary

The HotSwap Database Schema Migration Orchestrator provides enterprise-grade database schema migration capabilities built on the existing kernel orchestration platform. The system treats database migrations as deployable modules with progressive rollout strategies, automatic safety mechanisms, and comprehensive monitoring.

### Key Innovations

1. **Progressive Migration Rollout** - Apply deployment strategies to database schema changes
2. **Automatic Performance Monitoring** - Detect query performance degradation in real-time
3. **Intelligent Rollback** - Automatic rollback on performance issues or errors
4. **Multi-Database Support** - Unified interface for PostgreSQL, SQL Server, MySQL, Oracle
5. **Zero Downtime** - Online DDL operations with minimal locking

### Design Principles

1. **Safety First** - Extensive validation before any schema change
2. **Leverage Existing Infrastructure** - Reuse authentication, tracing, metrics, approval workflows
3. **Clean Architecture** - Maintain 4-layer separation (API, Orchestrator, Infrastructure, Domain)
4. **Test-Driven Development** - 85%+ test coverage with comprehensive unit/integration tests
5. **Production-Ready** - Security, observability, and reliability from day one

---

## System Requirements

### Functional Requirements

#### FR-MIG-001: Migration Creation
**Priority:** Critical
**Description:** System MUST support creating migration definitions with forward and rollback scripts

**Requirements:**
- Create migration with SQL script
- Provide rollback script (mandatory for production)
- Specify target database cluster
- Set migration strategy (Direct, Phased, Canary, Blue-Green, Shadow)
- Add metadata (description, tags, dependencies)
- Validate SQL syntax
- Return migration ID and status (202 Accepted)

**API Endpoint:**
```
POST /api/v1/migrations
```

**Acceptance Criteria:**
- Migration ID generated (GUID format)
- SQL syntax validation performed
- Rollback script required for production targets
- Invalid SQL rejected with 400 Bad Request
- Migration persisted to PostgreSQL
- Audit log entry created

---

#### FR-MIG-002: Migration Validation
**Priority:** Critical
**Description:** System MUST validate migrations before execution

**Requirements:**
- SQL syntax validation
- Dependency detection (foreign keys, views, indexes)
- Conflict detection (duplicate indexes, constraint names)
- Lock impact analysis
- Estimated execution time
- Breaking change detection
- Dry-run simulation

**Validation Checks:**
1. **Syntax Validation** - Parse SQL and detect errors
2. **Dependency Analysis** - Identify affected tables/columns
3. **Lock Analysis** - Estimate lock duration and impact
4. **Performance Impact** - Estimate query performance changes
5. **Replication Lag** - Check if replicas can handle migration
6. **Disk Space** - Verify sufficient space for table rewrites

**API Endpoint:**
```
POST /api/v1/migrations/{id}/validate
```

**Acceptance Criteria:**
- All validation checks pass
- Validation errors reported with details
- Dry-run executes without applying changes
- Validation time < 30 seconds

---

#### FR-MIG-003: Migration Execution
**Priority:** Critical
**Description:** System MUST execute migrations with selected strategy

**Requirements:**
- Execute migration on target database
- Apply progressive rollout strategy
- Monitor query performance during execution
- Track execution progress
- Log all SQL statements executed
- Capture execution metrics
- Handle execution timeouts
- Support concurrent execution prevention (distributed lock)

**Execution Modes:**
1. **Dry Run** - Validate without applying changes
2. **Single Database** - Execute on one database instance
3. **Cluster Rollout** - Progressive rollout across cluster

**API Endpoints:**
```
POST /api/v1/migrations/{id}/execute
GET  /api/v1/migrations/{id}/executions
GET  /api/v1/migrations/{id}/executions/{executionId}
```

**Acceptance Criteria:**
- Migration executes successfully
- Progress tracked in real-time
- Execution logs captured
- Metrics collected (duration, rows affected)
- Distributed lock prevents concurrent executions
- Timeout enforced (configurable, default 30 minutes)

---

#### FR-MIG-004: Performance Monitoring
**Priority:** Critical
**Description:** System MUST monitor database performance during migrations

**Requirements:**
- Track query latency (p50, p95, p99)
- Monitor active connections
- Detect lock waits and deadlocks
- Track replication lag
- Measure CPU and memory usage
- Alert on performance degradation
- Automatic rollback trigger

**Monitored Metrics:**
1. **Query Latency** - Average query time (ms)
2. **Lock Wait Time** - Time spent waiting for locks (ms)
3. **Active Connections** - Number of active database connections
4. **Replication Lag** - Time lag between master and replicas (seconds)
5. **Table Scan Rate** - Full table scans per second
6. **CPU Usage** - Database CPU utilization (%)
7. **Disk I/O** - Read/write operations per second

**Performance Degradation Thresholds:**
- Query latency increase > 50% baseline
- Lock wait time > 5 seconds
- Replication lag > 60 seconds
- CPU usage > 90%

**API Endpoint:**
```
GET /api/v1/migrations/{id}/executions/{executionId}/metrics
```

**Acceptance Criteria:**
- Metrics collected every 5 seconds during migration
- Performance degradation detected within 10 seconds
- Automatic rollback triggered on threshold breach
- Metrics stored for 30 days

---

#### FR-MIG-005: Automatic Rollback
**Priority:** Critical
**Description:** System MUST automatically rollback migrations on failure or performance degradation

**Requirements:**
- Execute rollback script on failure
- Rollback on performance degradation
- Rollback on timeout
- Rollback on replication lag
- Track rollback execution
- Preserve original data
- Rollback time < 30 seconds for most operations

**Rollback Triggers:**
1. **SQL Execution Error** - Syntax error, constraint violation, deadlock
2. **Performance Degradation** - Query latency > 50% baseline
3. **Timeout** - Migration exceeds max execution time
4. **Replication Lag** - Replicas fall behind > 60 seconds
5. **Manual Trigger** - Admin-initiated rollback

**Rollback Process:**
1. Stop forward migration immediately
2. Acquire exclusive lock (prevent new connections)
3. Execute rollback script
4. Verify rollback success
5. Release lock
6. Notify admins

**API Endpoint:**
```
POST /api/v1/migrations/{id}/executions/{executionId}/rollback
```

**Acceptance Criteria:**
- Rollback completes within 30 seconds
- Data integrity verified post-rollback
- Rollback reason logged
- Admin notification sent
- Audit trail updated

---

#### FR-MIG-006: Migration Strategies
**Priority:** High
**Description:** System MUST support multiple migration rollout strategies

**Strategies:**

1. **Direct Migration**
   - Execute on single database immediately
   - Fastest, highest risk
   - Use for dev/QA environments

2. **Phased Migration**
   - Execute replica-by-replica
   - Test on replica before master
   - Default for production

3. **Canary Migration**
   - Execute on 10% of replicas first
   - Monitor performance
   - Rollout to remaining replicas if successful
   - Recommended for high-risk changes

4. **Blue-Green Migration**
   - Execute on shadow replica
   - Validate performance
   - Switch traffic to new replica
   - Requires traffic routing capability

5. **Shadow Migration**
   - Execute on shadow replica
   - Replay production traffic
   - Validate performance under load
   - Most conservative, highest confidence

**Strategy Selection Criteria:**
- **Dev/QA** - Direct
- **Production (low risk)** - Phased
- **Production (high risk)** - Canary
- **Critical systems** - Shadow

**API Endpoint:**
```
POST /api/v1/migrations/{id}/execute
{
  "strategy": "Phased",
  "phases": ["replica-1", "replica-2", "master"]
}
```

**Acceptance Criteria:**
- All 5 strategies implemented
- Strategy switch without data loss
- Progress tracking per phase
- Pause/resume between phases

---

#### FR-MIG-007: Database Target Management
**Priority:** High
**Description:** System MUST manage database cluster targets

**Requirements:**
- Register database clusters
- Define master/replica topology
- Health check endpoints
- Connection pooling
- Credential management (encrypted)
- Multi-database type support (PostgreSQL, SQL Server, MySQL, Oracle)

**Database Types:**
- PostgreSQL 12+
- SQL Server 2019+
- MySQL 8.0+
- Oracle 19c+

**API Endpoints:**
```
POST   /api/v1/databases
GET    /api/v1/databases
GET    /api/v1/databases/{id}
PUT    /api/v1/databases/{id}
DELETE /api/v1/databases/{id}
GET    /api/v1/databases/{id}/health
```

**Acceptance Criteria:**
- Database registered with connection string
- Credentials encrypted at rest (AES-256)
- Health check every 30 seconds
- Connection pooling configured (min: 2, max: 20)
- Master/replica role detection

---

#### FR-MIG-008: Migration Approval Workflow
**Priority:** High
**Description:** System MUST require approval for production migrations

**Requirements:**
- Production migrations require admin approval
- Breaking changes flagged automatically
- Risk assessment displayed
- Approval history tracked
- Email notifications sent
- Integration with existing approval system

**Approval Criteria:**
- **Low Risk** - Auto-approved (add nullable column, create index concurrently)
- **Medium Risk** - Single approver required (add not-null column with default)
- **High Risk** - Two approvers required (drop column, rename table)

**API Endpoints:**
```
POST /api/v1/migrations/{id}/submit-approval
POST /api/v1/migrations/{id}/approve
POST /api/v1/migrations/{id}/reject
GET  /api/v1/migrations/{id}/approvals
```

**Acceptance Criteria:**
- High-risk migrations blocked without approval
- Approval request email sent
- Approval expiry after 7 days
- Rejection requires reason

---

#### FR-MIG-009: Audit Logging
**Priority:** High
**Description:** System MUST log all migration activities

**Requirements:**
- Log all API requests
- Log migration executions
- Log rollbacks
- Log performance metrics
- Log approval decisions
- Retention: 1 year minimum
- Compliance: SOX, HIPAA, GDPR

**Logged Events:**
- Migration created/updated/deleted
- Migration executed/rolled back
- Approval submitted/approved/rejected
- Performance degradation detected
- Database health issues

**API Endpoint:**
```
GET /api/v1/migrations/{id}/audit-log
```

**Acceptance Criteria:**
- All events logged with timestamp, user, details
- Logs immutable (append-only)
- Logs searchable by date, user, event type
- Logs exportable (JSON, CSV)

---

## Migration Strategies

### 1. Direct Migration

**Use Case:** Development/QA environments, low-risk changes

**Behavior:**
- Execute migration immediately on target database
- No phased rollout
- Fastest execution
- Highest risk

**Process:**
1. Validate migration
2. Execute on target database
3. Monitor performance
4. Rollback on error

**Rollout Time:** ~1-5 minutes

---

### 2. Phased Migration

**Use Case:** Production deployments, standard risk changes

**Behavior:**
- Execute replica-by-replica
- Test on replicas before master
- Pause between phases
- Automatic rollback per phase

**Process:**
1. Validate migration
2. Execute on replica-1 → monitor 5 minutes
3. Execute on replica-2 → monitor 5 minutes
4. Execute on replica-N → monitor 5 minutes
5. Execute on master → monitor 10 minutes
6. Rollback if any phase fails

**Rollout Time:** ~30-60 minutes (depends on replica count)

---

### 3. Canary Migration

**Use Case:** High-risk changes, large datasets

**Behavior:**
- Execute on 10% of replicas first
- Monitor performance
- Progressive rollout if successful (10% → 50% → 100%)
- Automatic rollback on degradation

**Process:**
1. Validate migration
2. Execute on 10% replicas → monitor 15 minutes
3. Execute on 50% replicas → monitor 15 minutes
4. Execute on remaining replicas → monitor 15 minutes
5. Execute on master → monitor 20 minutes
6. Rollback if any phase fails

**Rollout Time:** ~60-90 minutes

---

### 4. Blue-Green Migration

**Use Case:** Zero-downtime requirements, traffic routing available

**Behavior:**
- Execute on shadow replica (green)
- Validate performance
- Switch traffic from current (blue) to new (green)
- Keep blue as backup

**Process:**
1. Validate migration
2. Execute on green replica
3. Run performance tests
4. Switch traffic to green
5. Monitor production traffic
6. Rollback to blue if issues detected

**Rollout Time:** ~45-60 minutes

**Prerequisites:** Load balancer or connection pooler (PgBouncer, ProxySQL)

---

### 5. Shadow Migration

**Use Case:** Mission-critical systems, highest confidence required

**Behavior:**
- Execute on shadow replica
- Replay production traffic to shadow
- Validate performance under production load
- Rollout to production if successful

**Process:**
1. Validate migration
2. Set up shadow replica (copy from master)
3. Execute migration on shadow
4. Replay production traffic (1 hour window)
5. Compare performance metrics
6. Rollout to production if metrics acceptable

**Rollout Time:** ~2-4 hours

**Prerequisites:** Traffic replay tool (pgBadger, pt-query-digest)

---

## Safety Mechanisms

### Pre-Migration Validation

**Checks:**
1. **SQL Syntax** - Parse and validate SQL
2. **Dependencies** - Foreign keys, views, indexes, triggers
3. **Conflicts** - Duplicate names, constraint violations
4. **Lock Analysis** - Estimate lock duration (SHARE, EXCLUSIVE)
5. **Disk Space** - Verify space for table rewrites
6. **Replication Lag** - Check replica health

**Example Validations:**

```sql
-- PostgreSQL: Check index conflicts
SELECT indexname FROM pg_indexes
WHERE indexname = 'idx_users_email';

-- PostgreSQL: Check foreign key dependencies
SELECT conname FROM pg_constraint
WHERE conrelid = 'users'::regclass;

-- PostgreSQL: Estimate table size for rewrites
SELECT pg_size_pretty(pg_total_relation_size('users'));
```

---

### Performance Baselines

**Captured Before Migration:**
- Average query latency (last 1 hour)
- 95th percentile query latency
- 99th percentile query latency
- Active connections
- Lock wait time
- CPU usage
- Disk I/O

**Threshold Detection:**
- Query latency increase > 50% baseline → ROLLBACK
- Lock wait time > 5 seconds → ROLLBACK
- Replication lag > 60 seconds → PAUSE
- CPU > 90% for 2 minutes → ROLLBACK

---

### Lock Timeout Protection

**Lock Timeouts:**
- Statement timeout: 30 seconds (prevent long-running DDL)
- Lock timeout: 5 seconds (prevent blocking user queries)
- Idle transaction timeout: 60 seconds

**PostgreSQL Example:**
```sql
SET statement_timeout = '30s';
SET lock_timeout = '5s';
SET idle_in_transaction_session_timeout = '60s';

CREATE INDEX CONCURRENTLY idx_users_email ON users(email);
```

**SQL Server Example:**
```sql
ALTER INDEX idx_users_email ON users
REBUILD WITH (ONLINE = ON, MAXDOP = 4);
```

---

### Data Integrity Verification

**Post-Migration Checks:**
1. **Row Count Verification** - Compare row counts before/after
2. **Checksum Verification** - Verify data checksums
3. **Constraint Verification** - Check all constraints valid
4. **Index Verification** - Verify indexes created successfully
5. **Replication Verification** - Verify replicas synced

**PostgreSQL Example:**
```sql
-- Verify constraint
SELECT COUNT(*) FROM users WHERE email IS NULL; -- Should be 0

-- Verify index
SELECT schemaname, tablename, indexname
FROM pg_indexes
WHERE indexname = 'idx_users_email';

-- Verify replication
SELECT client_addr, state, sync_state, replay_lag
FROM pg_stat_replication;
```

---

## Performance Requirements

### Execution Time

| Migration Type | Target | Max |
|---------------|--------|-----|
| CREATE INDEX CONCURRENTLY | 2 min | 30 min |
| ADD COLUMN (nullable) | 1 min | 5 min |
| ADD CONSTRAINT | 5 min | 30 min |
| DROP INDEX | 1 sec | 10 sec |
| RENAME COLUMN | 1 sec | 10 sec |

### Performance Overhead

| Metric | Target | Max |
|--------|--------|-----|
| Query Latency Increase | < 2% | 5% |
| CPU Overhead | < 5% | 10% |
| Lock Wait Time | < 100ms | 1s |
| Replication Lag | < 5s | 30s |

### Monitoring Frequency

| Metric | Frequency | Retention |
|--------|-----------|-----------|
| Query Latency | 5 seconds | 30 days |
| Lock Wait Time | 5 seconds | 30 days |
| Replication Lag | 10 seconds | 30 days |
| CPU Usage | 10 seconds | 30 days |

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
| **Admin** | Full access (create, execute, approve, rollback) |
| **Developer** | Create migrations, execute in dev/QA |
| **DBA** | Execute migrations, monitor, rollback |
| **Viewer** | Read-only access (view migrations, logs) |

**Endpoint Authorization:**
```
POST   /api/v1/migrations                - Developer, Admin
POST   /api/v1/migrations/{id}/execute   - DBA, Admin (prod), Developer (dev/QA)
POST   /api/v1/migrations/{id}/approve   - Admin only
POST   /api/v1/migrations/{id}/rollback  - DBA, Admin
DELETE /api/v1/migrations/{id}           - Admin only
```

### Database Credentials

**Requirements:**
- Credentials encrypted at rest (AES-256)
- Credentials stored in HashiCorp Vault (self-hosted) or Kubernetes Secrets
- Connection strings never logged
- Credentials rotated every 90 days

### Transport Security

**Requirements:**
- HTTPS/TLS 1.2+ enforced (production)
- HSTS headers sent
- Certificate validation
- Database connections encrypted (SSL/TLS)

---

## Observability Requirements

### Distributed Tracing

**Requirement:** ALL migration operations MUST be traced end-to-end

**Spans:**
1. `migration.create` - Migration creation
2. `migration.validate` - Pre-flight validation
3. `migration.execute` - Execution operation
4. `migration.monitor` - Performance monitoring
5. `migration.rollback` - Rollback operation

**Trace Context:**
- Propagate W3C trace context
- Link validation and execution spans
- Include migration metadata in span attributes

---

### Metrics

**Required Metrics:**

**Counters:**
- `migrations.created.total` - Total migrations created
- `migrations.executed.total` - Total migrations executed
- `migrations.succeeded.total` - Total successful migrations
- `migrations.failed.total` - Total failed migrations
- `migrations.rolled_back.total` - Total rollbacks

**Histograms:**
- `migration.execution.duration` - Execution time (seconds)
- `migration.validation.duration` - Validation time (seconds)
- `migration.rollback.duration` - Rollback time (seconds)
- `query.latency.ms` - Query latency during migration

**Gauges:**
- `migrations.in_progress` - Active migrations
- `databases.registered` - Total registered databases
- `database.connections.active` - Active database connections
- `database.replication_lag.seconds` - Replication lag

---

### Logging

**Requirements:**
- Structured logging (JSON format)
- Trace ID correlation
- Log levels: Debug, Info, Warning, Error
- Sensitive data redacted (connection strings, passwords)

**Example Log:**
```json
{
  "timestamp": "2025-11-23T12:00:00Z",
  "level": "INFO",
  "message": "Migration executed successfully",
  "traceId": "abc-123",
  "migrationId": "mig-456",
  "database": "production-db-cluster",
  "duration_ms": 45000,
  "userId": "dba@example.com"
}
```

---

### Health Monitoring

**Requirements:**
- Database health checks every 30 seconds
- Migration execution status tracking
- Performance metric collection
- Alert on failures

**Health Check Endpoint:**
```
GET /api/v1/databases/{id}/health

Response:
{
  "status": "Healthy",
  "connections": 15,
  "replicationLag": 2.5,
  "cpuUsage": 45.2,
  "diskUsage": 68.5,
  "lastCheck": "2025-11-23T12:00:00Z"
}
```

---

## Scalability Requirements

### Concurrent Migrations

**Requirements:**
- Support up to 10 concurrent migrations across different databases
- Distributed lock prevents concurrent migrations on same database
- Queue migrations if target busy

### Database Limits

**Supported Scale:**
- Up to 100 registered database clusters
- Up to 1,000 database instances across all clusters
- Up to 10,000 migrations in history

### Migration Size Limits

| Limit | Value |
|-------|-------|
| Max script size | 10 MB |
| Max execution time | 2 hours |
| Max rows affected | 1 billion |
| Max table size | 1 TB |

---

## Non-Functional Requirements

### Reliability

- Migration success rate: 99%+
- Rollback success rate: 99.9%+
- Zero data loss during rollback
- Automatic recovery from network failures

### Maintainability

- Clean code (SOLID principles)
- 85%+ test coverage
- Comprehensive documentation
- Runbooks for common operations

### Testability

- Unit tests for all components
- Integration tests with real databases
- End-to-end tests for migration scenarios
- Performance tests for large datasets

### Compliance

- Audit logging for all operations
- Migration approval workflow (production)
- Data retention policies (1 year)
- GDPR compliance (migration history deletion)

---

## Dependencies

### Required Infrastructure

1. **PostgreSQL 15+** - Migration metadata storage, schema registry
2. **Redis 7+** - Distributed locks, execution state
3. **.NET 8.0 Runtime** - Application runtime
4. **Database Clusters** - PostgreSQL, SQL Server, MySQL, Oracle (targets)
5. **Jaeger** - Distributed tracing (optional)
6. **Prometheus** - Metrics collection (optional)

### External Services

1. **HashiCorp Vault (self-hosted)** / **Kubernetes Secrets** - Database credential management
2. **SMTP Server** - Email notifications (approval workflow)
3. **Slack/PagerDuty** - Alert notifications

---

## Acceptance Criteria

**System is production-ready when:**

1. ✅ All functional requirements implemented
2. ✅ 85%+ test coverage achieved
3. ✅ Performance targets met (< 5% overhead)
4. ✅ Security requirements satisfied (JWT, HTTPS, encryption)
5. ✅ Observability complete (tracing, metrics, logging)
6. ✅ Documentation complete (API docs, runbooks)
7. ✅ Zero-downtime migrations verified (PostgreSQL CREATE INDEX CONCURRENTLY)
8. ✅ Automatic rollback tested (performance degradation scenarios)
9. ✅ Load testing passed (10 concurrent migrations)
10. ✅ Production deployment successful

---

**Document Version:** 1.0.0
**Last Updated:** 2025-11-23
**Next Review:** After Epic 1 Implementation
**Approval Status:** Pending Architecture Review
