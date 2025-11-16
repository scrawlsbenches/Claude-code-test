# PostgreSQL Audit Log Schema Design

**Created:** 2025-11-16
**Status:** Design Phase
**Version:** 1.0

## Overview

This document defines the PostgreSQL database schema for comprehensive audit logging of all critical system events in the Distributed Kernel Orchestration System.

## Design Goals

1. **Comprehensive Coverage** - Capture all security-relevant and business-critical events
2. **Performance** - Efficient querying with proper indexes
3. **Retention** - Support configurable retention policies
4. **Compliance** - Meet audit trail requirements for enterprise environments
5. **Traceability** - Link events via trace IDs for distributed tracing

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

1. **Archival** - Move old audit logs to cold storage (S3, Azure Blob)
2. **Analytics** - Aggregate audit data for reporting dashboards
3. **Alerting** - Real-time alerts for suspicious patterns
4. **Search** - Full-text search on audit log messages
5. **Compliance Reports** - Pre-built reports for SOC 2, GDPR, HIPAA

---

**Next Steps:**
1. Implement EF Core models based on this schema
2. Create database migrations
3. Write unit tests for AuditLogService
4. Integrate into existing services
