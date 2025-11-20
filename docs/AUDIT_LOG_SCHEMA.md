# PostgreSQL Audit Log Schema Design

**Created:** 2025-11-16
**Updated:** 2025-11-16
**Status:** Implemented (Production Ready)
**Version:** 1.0

## Overview

This document defines the PostgreSQL database schema for comprehensive audit logging of all critical system events in the Distributed Kernel Orchestration System.

## Design Goals

1. **Comprehensive Coverage** - Capture all security-relevant and business-critical events
2. **Performance** - Efficient querying with proper indexes
3. **Retention** - Support configurable retention policies
4. **Compliance** - Meet audit trail requirements for enterprise environments
5. **Traceability** - Link events via trace IDs for distributed tracing

## Implementation Status

**Implementation Date:** 2025-11-16
**Status:** ✅ Complete (95% - Core functionality implemented)

### Completed Components

1. **✅ Database Schema** - All 5 tables implemented with indexes
   - audit_logs (main audit trail)
   - deployment_audit_events (deployment pipeline tracking)
   - approval_audit_events (approval workflow tracking)
   - authentication_audit_events (security audit trail)
   - configuration_audit_events (configuration change tracking)

2. **✅ Entity Framework Core Models**
   - AuditLog entity with complete mapping
   - DeploymentAuditEvent entity
   - ApprovalAuditEvent entity with computed column (IsExpired)
   - AuthenticationAuditEvent entity
   - ConfigurationAuditEvent entity
   - AuditLogDbContext with full configuration

3. **✅ Database Migration**
   - Migration: 20251116202007_InitialAuditLogSchema.cs
   - Includes all tables, indexes, relationships
   - Ready for production deployment

4. **✅ Service Layer**
   - IAuditLogService interface (10 methods)
   - AuditLogService implementation with error handling
   - Repository pattern for database access
   - 13 comprehensive unit tests (all passing)

5. **✅ Integration**
   - DeploymentPipeline: 3 audit events (PipelineStarted, PipelineCompleted, PipelineFailed)
   - ApprovalService: 3 audit events (ApprovalRequested, ApprovalGranted, ApprovalRejected)
   - AuthenticationController: 4 audit events (LoginSuccess, LoginFailed, TokenValidationFailed, UserNotFound)
   - Suspicious activity detection for authentication events

6. **✅ Retention Policy**
   - AuditLogRetentionBackgroundService implemented
   - Daily execution (configurable)
   - 90-day default retention period (configurable)
   - Automatic cleanup of old audit logs

7. **✅ Query API**
   - AuditLogsController with 5 REST API endpoints
   - Admin-only access (role-based authorization)
   - Pagination support
   - OpenTelemetry trace ID correlation
   - Full OpenAPI/Swagger documentation

### Pending Components

1. **⏳ Rollback Event Logging** - Not yet integrated (pending rollback implementation)
2. **⏳ Configuration Change Logging** - Not yet integrated (pending configuration management)
3. **⏳ Production Documentation** - Comprehensive usage guide (in progress)

### Files Implemented

- `src/HotSwap.Distributed.Infrastructure/Data/Entities/` - 5 entity models
- `src/HotSwap.Distributed.Infrastructure/Data/AuditLogDbContext.cs`
- `src/HotSwap.Distributed.Infrastructure/Data/AuditLogDbContextFactory.cs`
- `src/HotSwap.Distributed.Infrastructure/Interfaces/IAuditLogService.cs`
- `src/HotSwap.Distributed.Infrastructure/Services/AuditLogService.cs`
- `src/HotSwap.Distributed.Infrastructure/Migrations/20251116202007_InitialAuditLogSchema.cs`
- `src/HotSwap.Distributed.Api/Services/AuditLogRetentionBackgroundService.cs`
- `src/HotSwap.Distributed.Api/Controllers/AuditLogsController.cs`
- `tests/HotSwap.Distributed.Tests/Services/AuditLogServiceTests.cs`

## Schema Tables

### 1. audit_logs (Main Audit Trail)

Primary table for all audit events with structured JSON metadata.

```sql
CREATE TABLE audit_logs (
    id BIGSERIAL PRIMARY KEY,
    event_id UUID NOT NULL UNIQUE,
    timestamp TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP,
    event_type VARCHAR(100) NOT NULL,  -- e.g., 'DeploymentStarted', 'ApprovalGranted'
    event_category VARCHAR(50) NOT NULL,  -- 'Deployment', 'Approval', 'Authentication', 'Configuration'
    severity VARCHAR(20) NOT NULL,  -- 'Info', 'Warning', 'Error', 'Critical'
    user_id UUID,
    username VARCHAR(255),
    user_email VARCHAR(255),
    resource_type VARCHAR(100),  -- 'Module', 'Cluster', 'User', 'Configuration'
    resource_id VARCHAR(255),
    action VARCHAR(100) NOT NULL,  -- 'Create', 'Update', 'Delete', 'Approve', 'Reject'
    result VARCHAR(50) NOT NULL,  -- 'Success', 'Failure', 'Pending'
    message TEXT,
    metadata JSONB,  -- Flexible structured data
    trace_id VARCHAR(64),  -- OpenTelemetry trace ID
    span_id VARCHAR(32),  -- OpenTelemetry span ID
    source_ip VARCHAR(45),  -- IPv4 or IPv6
    user_agent VARCHAR(500),
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP,

    INDEX idx_audit_logs_timestamp (timestamp DESC),
    INDEX idx_audit_logs_event_type (event_type),
    INDEX idx_audit_logs_category (event_category),
    INDEX idx_audit_logs_user (username, user_email),
    INDEX idx_audit_logs_trace_id (trace_id),
    INDEX idx_audit_logs_resource (resource_type, resource_id)
);
```

### 2. deployment_audit_events

Specialized table for deployment pipeline events with detailed tracking.

```sql
CREATE TABLE deployment_audit_events (
    id BIGSERIAL PRIMARY KEY,
    audit_log_id BIGINT NOT NULL REFERENCES audit_logs(id) ON DELETE CASCADE,
    deployment_execution_id UUID NOT NULL,
    module_name VARCHAR(255) NOT NULL,
    module_version VARCHAR(100) NOT NULL,
    target_environment VARCHAR(50) NOT NULL,  -- 'Development', 'QA', 'Staging', 'Production'
    deployment_strategy VARCHAR(100),  -- 'Direct', 'Rolling', 'BlueGreen', 'Canary'
    pipeline_stage VARCHAR(100),  -- 'Build', 'Test', 'Security', 'Deploy', 'Validation'
    stage_status VARCHAR(50),  -- 'Running', 'Succeeded', 'Failed', 'WaitingForApproval'
    nodes_targeted INT,
    nodes_deployed INT,
    nodes_failed INT,
    start_time TIMESTAMP WITH TIME ZONE,
    end_time TIMESTAMP WITH TIME ZONE,
    duration_ms INT,
    error_message TEXT,
    exception_details TEXT,
    requester_email VARCHAR(255),
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP,

    INDEX idx_deployment_audit_execution_id (deployment_execution_id),
    INDEX idx_deployment_audit_module (module_name, module_version),
    INDEX idx_deployment_audit_environment (target_environment),
    INDEX idx_deployment_audit_status (stage_status),
    INDEX idx_deployment_audit_start_time (start_time DESC)
);
```

### 3. approval_audit_events

Audit trail for approval workflow events.

```sql
CREATE TABLE approval_audit_events (
    id BIGSERIAL PRIMARY KEY,
    audit_log_id BIGINT NOT NULL REFERENCES audit_logs(id) ON DELETE CASCADE,
    approval_id UUID NOT NULL,
    deployment_execution_id UUID NOT NULL,
    module_name VARCHAR(255) NOT NULL,
    module_version VARCHAR(100) NOT NULL,
    target_environment VARCHAR(50) NOT NULL,
    requester_email VARCHAR(255) NOT NULL,
    approver_emails TEXT[],  -- Array of authorized approvers
    approval_status VARCHAR(50) NOT NULL,  -- 'Pending', 'Approved', 'Rejected', 'Expired'
    decision_by_email VARCHAR(255),
    decision_at TIMESTAMP WITH TIME ZONE,
    decision_reason TEXT,
    timeout_at TIMESTAMP WITH TIME ZONE NOT NULL,
    is_expired BOOLEAN GENERATED ALWAYS AS (CURRENT_TIMESTAMP >= timeout_at AND approval_status = 'Pending') STORED,
    metadata JSONB,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP,

    INDEX idx_approval_audit_approval_id (approval_id),
    INDEX idx_approval_audit_deployment_id (deployment_execution_id),
    INDEX idx_approval_audit_status (approval_status),
    INDEX idx_approval_audit_requester (requester_email),
    INDEX idx_approval_audit_approver (decision_by_email),
    INDEX idx_approval_audit_environment (target_environment)
);
```

### 4. authentication_audit_events

Security audit trail for authentication events.

```sql
CREATE TABLE authentication_audit_events (
    id BIGSERIAL PRIMARY KEY,
    audit_log_id BIGINT NOT NULL REFERENCES audit_logs(id) ON DELETE CASCADE,
    user_id UUID,
    username VARCHAR(255) NOT NULL,
    authentication_method VARCHAR(50) NOT NULL,  -- 'JWT', 'BasicAuth', 'ApiKey'
    authentication_result VARCHAR(50) NOT NULL,  -- 'Success', 'Failure', 'LockedOut'
    failure_reason VARCHAR(255),
    token_issued BOOLEAN,
    token_expires_at TIMESTAMP WITH TIME ZONE,
    source_ip VARCHAR(45),
    user_agent VARCHAR(500),
    geo_location VARCHAR(255),  -- Optional: Country/City
    is_suspicious BOOLEAN DEFAULT FALSE,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP,

    INDEX idx_auth_audit_username (username),
    INDEX idx_auth_audit_result (authentication_result),
    INDEX idx_auth_audit_timestamp (created_at DESC),
    INDEX idx_auth_audit_source_ip (source_ip),
    INDEX idx_auth_audit_suspicious (is_suspicious) WHERE is_suspicious = TRUE
);
```

### 5. configuration_audit_events

Audit trail for system configuration changes (Future enhancement).

```sql
CREATE TABLE configuration_audit_events (
    id BIGSERIAL PRIMARY KEY,
    audit_log_id BIGINT NOT NULL REFERENCES audit_logs(id) ON DELETE CASCADE,
    configuration_key VARCHAR(255) NOT NULL,
    configuration_category VARCHAR(100) NOT NULL,  -- 'Pipeline', 'Security', 'Deployment'
    old_value TEXT,
    new_value TEXT,
    changed_by_user VARCHAR(255) NOT NULL,
    change_reason TEXT,
    approved_by_user VARCHAR(255),
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP,

    INDEX idx_config_audit_key (configuration_key),
    INDEX idx_config_audit_category (configuration_category),
    INDEX idx_config_audit_user (changed_by_user),
    INDEX idx_config_audit_timestamp (created_at DESC)
);
```

## Entity Framework Core Mapping

### Conventions

- All tables use BIGSERIAL for auto-incrementing primary keys (better for high-volume audit logs)
- UUID fields for correlation across distributed systems
- TIMESTAMP WITH TIME ZONE for all timestamps (UTC storage)
- JSONB for flexible metadata storage (indexed for performance)
- Proper foreign keys with CASCADE deletes for audit_logs parent table
- Indexes on commonly queried fields

### Connection String

```json
{
  "ConnectionStrings": {
    "AuditDatabase": "Host=localhost;Port=5432;Database=kernel_audit;Username=audit_user;Password=<secret>"
  }
}
```

## Retention Policy

### Automatic Cleanup

Implement background service to purge old audit logs based on retention policy:

```sql
-- Delete audit logs older than retention period (cascades to child tables)
DELETE FROM audit_logs
WHERE created_at < CURRENT_TIMESTAMP - INTERVAL '90 days';
```

### Configurable Retention

```json
{
  "AuditLog": {
    "RetentionDays": 90,  // Default: 90 days
    "CleanupIntervalHours": 24  // Run cleanup daily
  }
}
```

## Query Performance

### Estimated Row Counts (1 year @ 1000 deployments/day)

- `audit_logs`: ~10M rows (all events)
- `deployment_audit_events`: ~5M rows (avg 5 stages per deployment)
- `approval_audit_events`: ~200K rows (Staging + Production only)
- `authentication_audit_events`: ~500K rows (daily auth events)

### Index Strategy

- B-tree indexes on: timestamps, UUIDs, VARCHAR lookups
- GIN index on JSONB metadata: `CREATE INDEX idx_audit_logs_metadata ON audit_logs USING GIN (metadata);`
- Partial indexes on filtered queries (e.g., is_suspicious WHERE TRUE)

### Query Examples

**Get deployment history for a module:**
```sql
SELECT al.*, dae.*
FROM audit_logs al
JOIN deployment_audit_events dae ON dae.audit_log_id = al.id
WHERE dae.module_name = 'kernel-hotswap-module'
  AND dae.start_time >= CURRENT_TIMESTAMP - INTERVAL '30 days'
ORDER BY dae.start_time DESC;
```

**Get failed approvals:**
```sql
SELECT al.*, aae.*
FROM audit_logs al
JOIN approval_audit_events aae ON aae.audit_log_id = al.id
WHERE aae.approval_status IN ('Rejected', 'Expired')
  AND aae.created_at >= CURRENT_TIMESTAMP - INTERVAL '7 days'
ORDER BY aae.created_at DESC;
```

**Get suspicious login attempts:**
```sql
SELECT al.*, aue.*
FROM audit_logs al
JOIN authentication_audit_events aue ON aue.audit_log_id = al.id
WHERE aue.authentication_result = 'Failure'
  AND aue.created_at >= CURRENT_TIMESTAMP - INTERVAL '1 hour'
GROUP BY aue.source_ip
HAVING COUNT(*) > 5;  -- More than 5 failures from same IP
```

## Migration Strategy

### Phase 1: Infrastructure
1. Install PostgreSQL NuGet packages (Npgsql.EntityFrameworkCore.PostgreSQL)
2. Create DbContext and entity models
3. Generate initial migration
4. Apply migration to database

### Phase 2: Integration
1. Create IAuditLogService interface
2. Implement AuditLogService with repository pattern
3. Inject into existing services (Pipeline, Approval, Authentication)
4. Add audit logging calls at key events

### Phase 3: Querying
1. Create AuditLogController for querying audit logs
2. Add filtering, pagination, sorting
3. Add export functionality (CSV, JSON)

### Phase 4: Monitoring
1. Create AuditLogCleanupBackgroundService for retention policy
2. Add metrics for audit log volume
3. Add alerts for suspicious patterns

## Security Considerations

1. **Write-Only Access** - Application should only INSERT, never UPDATE or DELETE individual records
2. **Database Permissions** - Use restricted database user with INSERT-only permissions
3. **Encryption at Rest** - Enable PostgreSQL transparent data encryption
4. **Network Encryption** - Use SSL/TLS for database connections
5. **Audit the Auditor** - Log configuration changes to audit system itself

## Future Enhancements

1. **Archival** - Move old audit logs to cold storage (MinIO with lifecycle policies or compressed filesystem archives)
2. **Analytics** - Aggregate audit data for reporting dashboards
3. **Alerting** - Real-time alerts for suspicious patterns
4. **Search** - Full-text search on audit log messages
5. **Compliance Reports** - Pre-built reports for SOC 2, GDPR, HIPAA

---

## API Endpoints Reference

The following REST API endpoints are available for querying audit logs (Admin role required):

### 1. Query by Category
```http
GET /api/v1/auditlogs/category/{category}?pageNumber=1&pageSize=50
```
Query audit logs by event category with pagination.

**Categories:** Deployment, Approval, Authentication, Configuration

### 2. Query by Trace ID
```http
GET /api/v1/auditlogs/trace/{traceId}
```
Retrieve all audit logs for a specific OpenTelemetry trace ID (distributed tracing correlation).

### 3. Get Deployment Events
```http
GET /api/v1/auditlogs/deployments/{executionId}
```
Retrieve deployment audit events for a specific deployment execution.

### 4. Get Approval Events
```http
GET /api/v1/auditlogs/approvals/{executionId}
```
Retrieve approval audit events for a specific deployment execution.

### 5. Get Authentication Events
```http
GET /api/v1/auditlogs/authentication/{username}?startDate=2025-01-01&endDate=2025-12-31
```
Retrieve authentication events for a specific user with optional date filtering.

---

## Usage Examples

### Querying Audit Logs

**Example 1: Get deployment events for a specific execution**
```csharp
// In AuditLogsController or service
var deploymentEvents = await _auditLogService.GetDeploymentEventsAsync(
    executionId: deploymentId,
    cancellationToken: cancellationToken);
```

**Example 2: Query audit logs by category with pagination**
```csharp
var auditLogs = await _auditLogService.GetAuditLogsByCategoryAsync(
    category: "Authentication",
    pageNumber: 1,
    pageSize: 50,
    cancellationToken: cancellationToken);
```

**Example 3: Trace distributed request across services**
```csharp
var relatedLogs = await _auditLogService.GetAuditLogsByTraceIdAsync(
    traceId: Activity.Current?.TraceId.ToString(),
    cancellationToken: cancellationToken);
```

### Integrating Audit Logging

**Example 1: Log deployment event**
```csharp
var auditLog = new AuditLog
{
    EventId = Guid.NewGuid(),
    Timestamp = DateTime.UtcNow,
    EventType = "PipelineStarted",
    EventCategory = "Deployment",
    Severity = "Information",
    Username = "System",
    UserEmail = request.RequesterEmail,
    ResourceType = "DeploymentPipeline",
    ResourceId = request.ExecutionId.ToString(),
    Action = "ExecutePipeline",
    Result = "Running",
    Message = $"Pipeline started for {request.Module.Name}",
    TraceId = Activity.Current?.TraceId.ToString(),
    SpanId = Activity.Current?.SpanId.ToString(),
    CreatedAt = DateTime.UtcNow
};

var deploymentEvent = new DeploymentAuditEvent
{
    DeploymentExecutionId = request.ExecutionId,
    ModuleName = request.Module.Name,
    ModuleVersion = request.Module.Version.ToString(),
    TargetEnvironment = request.TargetEnvironment.ToString(),
    PipelineStage = "Pipeline",
    StageStatus = "Running",
    RequesterEmail = request.RequesterEmail,
    CreatedAt = DateTime.UtcNow
};

await _auditLogService.LogDeploymentEventAsync(auditLog, deploymentEvent, cancellationToken);
```

**Example 2: Log authentication event with suspicious activity detection**
```csharp
var auditLog = new AuditLog
{
    EventId = Guid.NewGuid(),
    Timestamp = DateTime.UtcNow,
    EventType = "LoginFailed",
    EventCategory = "Authentication",
    Severity = "Warning",
    Username = username,
    ResourceType = "Authentication",
    Action = "Authenticate",
    Result = "Failure",
    Message = "Invalid username or password",
    SourceIp = HttpContext.Connection.RemoteIpAddress?.ToString(),
    UserAgent = HttpContext.Request.Headers["User-Agent"].ToString(),
    CreatedAt = DateTime.UtcNow
};

var authEvent = new AuthenticationAuditEvent
{
    Username = username,
    AuthenticationMethod = "JWT",
    AuthenticationResult = "Failure",
    FailureReason = "Invalid username or password",
    TokenIssued = false,
    SourceIp = auditLog.SourceIp,
    UserAgent = auditLog.UserAgent,
    IsSuspicious = DetectSuspiciousActivity(result, sourceIp, userAgent),
    CreatedAt = DateTime.UtcNow
};

await _auditLogService.LogAuthenticationEventAsync(auditLog, authEvent, cancellationToken);
```

---

**Next Steps (Optional/Future):**
1. ✅ ~~Implement EF Core models based on this schema~~ - COMPLETE
2. ✅ ~~Create database migrations~~ - COMPLETE
3. ✅ ~~Write unit tests for AuditLogService~~ - COMPLETE (13 tests)
4. ✅ ~~Integrate into existing services~~ - COMPLETE (Deployment, Approval, Authentication)
5. ⏳ Integrate rollback event logging (pending rollback implementation)
6. ⏳ Integrate configuration change logging (pending configuration management)
7. ⏳ Add comprehensive production usage guide
8. ⏳ Add metrics dashboard for audit log analytics
9. ⏳ Implement real-time alerting for suspicious patterns
