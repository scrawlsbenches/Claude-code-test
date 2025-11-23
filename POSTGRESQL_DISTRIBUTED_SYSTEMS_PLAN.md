# PostgreSQL-Based Distributed Systems Solution

**Date**: 2025-11-23
**Purpose**: Replace Redis with PostgreSQL for distributed locks, message queues, and state management
**Status**: Design Document

---

## Executive Summary

This document proposes **PostgreSQL-based solutions** to fix the 4 critical distributed systems issues identified in the code review (Task #26), **avoiding Redis** which can cause issues in this environment.

**Key Benefits**:
- ✅ Already integrated (EF Core, Npgsql)
- ✅ No new dependencies or infrastructure
- ✅ Works in all environments (SQLite for tests, PostgreSQL for production)
- ✅ ACID guarantees prevent data loss
- ✅ Proven patterns (advisory locks, LISTEN/NOTIFY, outbox pattern)

---

## Critical Issues from Code Review

### Issue #1: Split-Brain Vulnerability 🔴
**Problem**: `InMemoryDistributedLock` is process-local, multiple instances can acquire the same lock

**Current Implementation**:
```csharp
// Process-local lock - doesn't work across instances!
public class InMemoryDistributedLock : IDistributedLock
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();
}
```

**Impact**: Multiple API instances can deploy to the same cluster simultaneously

---

### Issue #2: Static State Memory Leak 🔴
**Problem**: `ApprovalService` uses static dictionaries, causes memory leak and doesn't scale

**Current Implementation**:
```csharp
public class ApprovalService
{
    private static readonly ConcurrentDictionary<string, ApprovalRequest> _pendingApprovals = new();
    private static readonly ConcurrentDictionary<string, List<ApprovalDecision>> _approvalHistory = new();
}
```

**Impact**: Static state grows indefinitely, doesn't work across instances

---

### Issue #3: Fire-and-Forget Deployments 🔴
**Problem**: Background deployment tasks are orphaned on server shutdown/restart

**Current Implementation**:
```csharp
// Fire-and-forget - no tracking!
_ = Task.Run(async () => {
    var result = await _orchestrator.ExecuteDeploymentPipelineAsync(...);
}, CancellationToken.None);
```

**Impact**: Deployments lost on restart, no recovery mechanism

---

### Issue #4: Message Queue Data Loss 🔴
**Problem**: In-memory message queue loses all data on restart

**Current Implementation**:
```csharp
public class InMemoryMessageQueue
{
    private static readonly ConcurrentDictionary<string, List<Message>> _messages = new();
}
```

**Impact**: Messages lost on restart, no durability

---

## PostgreSQL-Based Solutions

### Solution #1: PostgreSQL Advisory Locks (Replace InMemoryDistributedLock)

**Implementation**: Use PostgreSQL advisory locks for distributed coordination

**Advantages**:
- ✅ Built into PostgreSQL (no extensions needed)
- ✅ Automatic release on connection close
- ✅ Fast (in-memory on PostgreSQL server)
- ✅ Works across multiple API instances
- ✅ No polling needed

**API**:
```csharp
public class PostgresDistributedLock : IDistributedLock
{
    // PostgreSQL advisory lock functions:
    // - pg_advisory_lock(key) - Blocks until lock acquired
    // - pg_try_advisory_lock(key) - Returns immediately (true/false)
    // - pg_advisory_unlock(key) - Release lock
    // - pg_advisory_unlock_all() - Release all locks for connection
}
```

**Example**:
```csharp
public async Task<bool> AcquireLockAsync(string resourceId, TimeSpan timeout)
{
    var lockKey = GetLockKey(resourceId); // Hash to int64

    // Try to acquire lock with timeout
    var query = @"
        SELECT pg_try_advisory_lock(@lockKey) AS acquired
        FROM pg_sleep(@timeoutSeconds)
        WHERE NOT pg_try_advisory_lock(@lockKey)
        UNION ALL
        SELECT pg_try_advisory_lock(@lockKey)
        LIMIT 1";

    return await _dbContext.Database
        .ExecuteSqlRawAsync(query, lockKey, timeout.TotalSeconds);
}

public async Task ReleaseLockAsync(string resourceId)
{
    var lockKey = GetLockKey(resourceId);
    await _dbContext.Database.ExecuteSqlRawAsync(
        "SELECT pg_advisory_unlock(@lockKey)", lockKey);
}
```

**Benefits vs Redis**:
- No extra dependency
- Connection-scoped (auto-cleanup on disconnect)
- Lower latency (same database)
- Simpler deployment

---

### Solution #2: PostgreSQL Table for Approval State (Replace Static Dictionaries)

**Implementation**: Use database table with EF Core for approval requests

**Schema**:
```sql
CREATE TABLE approval_requests (
    deployment_id VARCHAR(100) PRIMARY KEY,
    requester_email VARCHAR(255) NOT NULL,
    target_environment VARCHAR(50) NOT NULL,
    module_name VARCHAR(100) NOT NULL,
    module_version VARCHAR(50) NOT NULL,
    request_date TIMESTAMP NOT NULL,
    expires_at TIMESTAMP NOT NULL,
    status VARCHAR(20) NOT NULL, -- Pending, Approved, Rejected, Expired
    approver_email VARCHAR(255),
    approval_date TIMESTAMP,
    rejection_reason TEXT,
    created_at TIMESTAMP DEFAULT NOW(),
    updated_at TIMESTAMP DEFAULT NOW()
);

CREATE INDEX idx_approval_status ON approval_requests(status, expires_at);
CREATE INDEX idx_approval_expiry ON approval_requests(expires_at)
    WHERE status = 'Pending';
```

**Entity**:
```csharp
public class ApprovalRequestEntity
{
    public string DeploymentId { get; set; }
    public string RequesterEmail { get; set; }
    public string TargetEnvironment { get; set; }
    public string ModuleName { get; set; }
    public string ModuleVersion { get; set; }
    public DateTime RequestDate { get; set; }
    public DateTime ExpiresAt { get; set; }
    public ApprovalStatus Status { get; set; }
    public string? ApproverEmail { get; set; }
    public DateTime? ApprovalDate { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
```

**Repository**:
```csharp
public class ApprovalRepository : IApprovalRepository
{
    private readonly AuditLogDbContext _dbContext;

    public async Task<ApprovalRequestEntity> CreateAsync(ApprovalRequest request)
    {
        var entity = new ApprovalRequestEntity
        {
            DeploymentId = request.DeploymentId,
            RequesterEmail = request.RequesterEmail,
            TargetEnvironment = request.TargetEnvironment,
            Status = ApprovalStatus.Pending,
            RequestDate = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(24)
        };

        _dbContext.ApprovalRequests.Add(entity);
        await _dbContext.SaveChangesAsync();
        return entity;
    }

    public async Task<List<ApprovalRequestEntity>> GetPendingAsync()
    {
        return await _dbContext.ApprovalRequests
            .Where(a => a.Status == ApprovalStatus.Pending && a.ExpiresAt > DateTime.UtcNow)
            .OrderBy(a => a.RequestDate)
            .ToListAsync();
    }

    public async Task ApproveAsync(string deploymentId, string approverEmail)
    {
        var request = await _dbContext.ApprovalRequests
            .FirstOrDefaultAsync(a => a.DeploymentId == deploymentId);

        if (request == null || request.Status != ApprovalStatus.Pending)
            throw new InvalidOperationException("Cannot approve this request");

        request.Status = ApprovalStatus.Approved;
        request.ApproverEmail = approverEmail;
        request.ApprovalDate = DateTime.UtcNow;
        request.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();
    }
}
```

**Benefits vs Static Dictionaries**:
- Persistent across restarts
- Works across multiple instances
- No memory leak
- Query capabilities (history, filtering)
- Automatic cleanup with background service

---

### Solution #3: PostgreSQL Outbox Pattern for Background Jobs (Replace Fire-and-Forget)

**Implementation**: Transactional outbox pattern for reliable background processing

**Schema**:
```sql
CREATE TABLE deployment_jobs (
    id SERIAL PRIMARY KEY,
    deployment_id VARCHAR(100) UNIQUE NOT NULL,
    status VARCHAR(20) NOT NULL, -- Pending, Running, Succeeded, Failed, Cancelled
    payload JSONB NOT NULL,
    created_at TIMESTAMP DEFAULT NOW(),
    started_at TIMESTAMP,
    completed_at TIMESTAMP,
    error_message TEXT,
    retry_count INT DEFAULT 0,
    max_retries INT DEFAULT 3,
    next_retry_at TIMESTAMP,
    locked_until TIMESTAMP,
    processing_instance VARCHAR(100)
);

CREATE INDEX idx_deployment_jobs_pending ON deployment_jobs(status, next_retry_at)
    WHERE status IN ('Pending', 'Failed');
CREATE INDEX idx_deployment_jobs_lock ON deployment_jobs(locked_until)
    WHERE status = 'Running';
```

**Background Service**:
```csharp
public class DeploymentJobProcessor : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DeploymentJobProcessor> _logger;
    private const int CHECK_INTERVAL_SECONDS = 5;
    private const int LOCK_DURATION_MINUTES = 10;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingJobsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing deployment jobs");
            }

            await Task.Delay(TimeSpan.FromSeconds(CHECK_INTERVAL_SECONDS), stoppingToken);
        }
    }

    private async Task ProcessPendingJobsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AuditLogDbContext>();

        // Claim pending jobs with pessimistic locking
        var lockUntil = DateTime.UtcNow.AddMinutes(LOCK_DURATION_MINUTES);
        var instanceId = Environment.MachineName;

        var sql = @"
            UPDATE deployment_jobs
            SET status = 'Running',
                started_at = NOW(),
                locked_until = @lockUntil,
                processing_instance = @instanceId
            WHERE id IN (
                SELECT id FROM deployment_jobs
                WHERE status IN ('Pending', 'Failed')
                  AND (next_retry_at IS NULL OR next_retry_at <= NOW())
                  AND retry_count < max_retries
                ORDER BY created_at
                LIMIT 10
                FOR UPDATE SKIP LOCKED
            )
            RETURNING id, deployment_id, payload";

        var jobs = await dbContext.Database.ExecuteSqlQueryAsync<DeploymentJob>(sql,
            new { lockUntil, instanceId });

        foreach (var job in jobs)
        {
            _ = Task.Run(() => ProcessJobAsync(job, cancellationToken), cancellationToken);
        }
    }

    private async Task ProcessJobAsync(DeploymentJob job, CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var orchestrator = scope.ServiceProvider.GetRequiredService<IDistributedKernelOrchestrator>();
        var dbContext = scope.ServiceProvider.GetRequiredService<AuditLogDbContext>();

        try
        {
            var payload = JsonSerializer.Deserialize<DeploymentRequest>(job.Payload);
            var result = await orchestrator.ExecuteDeploymentPipelineAsync(payload, cancellationToken);

            // Mark job as succeeded
            await dbContext.Database.ExecuteSqlRawAsync(@"
                UPDATE deployment_jobs
                SET status = 'Succeeded', completed_at = NOW()
                WHERE id = @jobId", job.Id);

            _logger.LogInformation("Deployment job {JobId} completed successfully", job.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Deployment job {JobId} failed", job.Id);

            // Increment retry count and schedule retry
            await dbContext.Database.ExecuteSqlRawAsync(@"
                UPDATE deployment_jobs
                SET status = 'Failed',
                    error_message = @errorMessage,
                    retry_count = retry_count + 1,
                    next_retry_at = NOW() + INTERVAL '5 minutes' * POWER(2, retry_count),
                    locked_until = NULL
                WHERE id = @jobId", job.Id, ex.Message);
        }
    }
}
```

**Benefits vs Fire-and-Forget**:
- Survives server restarts
- Automatic retry with exponential backoff
- Distributed processing (multiple workers)
- Observability (job status, errors)
- Prevents duplicate processing (SKIP LOCKED)

---

### Solution #4: PostgreSQL Message Queue (Replace InMemoryMessageQueue)

**Implementation**: Database-backed message queue with LISTEN/NOTIFY for real-time delivery

**Schema**:
```sql
CREATE TABLE message_queue (
    id SERIAL PRIMARY KEY,
    message_id VARCHAR(100) UNIQUE NOT NULL,
    topic VARCHAR(100) NOT NULL,
    payload JSONB NOT NULL,
    priority INT DEFAULT 0,
    status VARCHAR(20) NOT NULL, -- Pending, Processing, Completed, Failed
    created_at TIMESTAMP DEFAULT NOW(),
    processed_at TIMESTAMP,
    acknowledged_at TIMESTAMP,
    locked_until TIMESTAMP,
    processing_instance VARCHAR(100),
    retry_count INT DEFAULT 0,
    max_retries INT DEFAULT 3,
    error_message TEXT
);

CREATE INDEX idx_message_queue_pending ON message_queue(topic, priority DESC, created_at)
    WHERE status = 'Pending';
CREATE INDEX idx_message_queue_lock ON message_queue(locked_until)
    WHERE status = 'Processing';
```

**Publisher**:
```csharp
public class PostgresMessageQueue : IMessageQueue
{
    private readonly AuditLogDbContext _dbContext;

    public async Task PublishAsync<T>(string topic, T message, int priority = 0)
    {
        var entity = new MessageEntity
        {
            MessageId = Guid.NewGuid().ToString(),
            Topic = topic,
            Payload = JsonSerializer.Serialize(message),
            Priority = priority,
            Status = MessageStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Messages.Add(entity);
        await _dbContext.SaveChangesAsync();

        // Notify listeners via PostgreSQL LISTEN/NOTIFY
        await _dbContext.Database.ExecuteSqlRawAsync(
            "NOTIFY message_queue, @topic", topic);
    }
}
```

**Consumer**:
```csharp
public class MessageConsumerService : BackgroundService
{
    private NpgsqlConnection? _notificationConnection;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Set up LISTEN for real-time notifications
        _notificationConnection = new NpgsqlConnection(_connectionString);
        await _notificationConnection.OpenAsync(stoppingToken);
        _notificationConnection.Notification += OnNotification;

        await using var cmd = new NpgsqlCommand("LISTEN message_queue", _notificationConnection);
        await cmd.ExecuteNonQueryAsync(stoppingToken);

        // Process existing messages and wait for notifications
        while (!stoppingToken.IsCancellationRequested)
        {
            await ProcessMessagesAsync(stoppingToken);
            await _notificationConnection.WaitAsync(stoppingToken);
        }
    }

    private void OnNotification(object sender, NpgsqlNotificationEventArgs e)
    {
        _logger.LogInformation("Received notification for topic: {Topic}", e.Payload);
        // Trigger immediate processing
    }

    private async Task ProcessMessagesAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AuditLogDbContext>();

        // Claim messages with pessimistic locking
        var lockUntil = DateTime.UtcNow.AddMinutes(5);
        var instanceId = Environment.MachineName;

        var messages = await dbContext.Database.ExecuteSqlQueryAsync<MessageEntity>(@"
            UPDATE message_queue
            SET status = 'Processing',
                locked_until = @lockUntil,
                processing_instance = @instanceId
            WHERE id IN (
                SELECT id FROM message_queue
                WHERE status = 'Pending'
                  AND topic = @topic
                ORDER BY priority DESC, created_at
                LIMIT 10
                FOR UPDATE SKIP LOCKED
            )
            RETURNING *", new { lockUntil, instanceId, topic });

        foreach (var message in messages)
        {
            await ProcessMessageAsync(message, cancellationToken);
        }
    }
}
```

**Benefits vs In-Memory Queue**:
- Durable (survives restarts)
- Distributed (multiple consumers)
- Priority support
- Retry logic with dead-letter queue
- Real-time with LISTEN/NOTIFY
- No message loss

---

## Implementation Plan

### Phase 1: PostgreSQL Advisory Locks (2 days)

**Tasks**:
1. Create `PostgresDistributedLock : IDistributedLock`
2. Add lock key hashing (string → int64)
3. Implement timeout support
4. Add unit tests with in-memory PostgreSQL
5. Replace `InMemoryDistributedLock` in DI registration
6. Verify deployment locking works across instances

**Files**:
- `src/HotSwap.Distributed.Infrastructure/Locking/PostgresDistributedLock.cs`
- `tests/HotSwap.Distributed.Tests/Locking/PostgresDistributedLockTests.cs`

---

### Phase 2: Approval Request Persistence (1 day)

**Tasks**:
1. Add `ApprovalRequestEntity` to `AuditLogDbContext`
2. Create EF Core migration
3. Create `ApprovalRepository : IApprovalRepository`
4. Refactor `ApprovalService` to use repository
5. Update `ApprovalTimeoutBackgroundService` to use database queries
6. Add integration tests

**Files**:
- `src/HotSwap.Distributed.Infrastructure/Data/Entities/ApprovalRequestEntity.cs`
- `src/HotSwap.Distributed.Infrastructure/Repositories/ApprovalRepository.cs`
- `src/HotSwap.Distributed.Infrastructure/Services/ApprovalService.cs` (refactor)
- `src/HotSwap.Distributed.Infrastructure/Migrations/*.cs` (new migration)

---

### Phase 3: Deployment Job Queue (2-3 days)

**Tasks**:
1. Add `DeploymentJobEntity` to database
2. Create EF Core migration
3. Create `DeploymentJobProcessor` background service
4. Refactor `DeploymentsController` to enqueue jobs instead of fire-and-forget
5. Implement job retry logic with exponential backoff
6. Add job monitoring endpoints
7. Add integration tests

**Files**:
- `src/HotSwap.Distributed.Infrastructure/Data/Entities/DeploymentJobEntity.cs`
- `src/HotSwap.Distributed.Infrastructure/Services/DeploymentJobProcessor.cs`
- `src/HotSwap.Distributed.Api/Controllers/DeploymentsController.cs` (refactor)

---

### Phase 4: Message Queue Persistence (1-2 days)

**Tasks**:
1. Add `MessageEntity` to database
2. Create EF Core migration
3. Create `PostgresMessageQueue : IMessageQueue`
4. Implement LISTEN/NOTIFY for real-time delivery
5. Create `MessageConsumerService` background service
6. Replace `InMemoryMessageQueue` in DI
7. Add integration tests

**Files**:
- `src/HotSwap.Distributed.Infrastructure/Data/Entities/MessageEntity.cs`
- `src/HotSwap.Distributed.Infrastructure/Messaging/PostgresMessageQueue.cs`
- `src/HotSwap.Distributed.Infrastructure/Messaging/MessageConsumerService.cs`

---

## Testing Strategy

### Unit Tests
- Mock `DbContext` for repository tests
- In-memory database for lock tests
- Verify retry logic
- Test concurrent access

### Integration Tests
- SQLite in-memory for fast tests
- PostgreSQL for production-like tests
- Test multiple instances (distributed scenarios)
- Test failover and recovery

### Performance Tests
- Measure lock acquisition latency
- Test queue throughput (messages/second)
- Verify no memory leaks
- Load test with 100+ concurrent operations

---

## Migration Strategy

### Step 1: Parallel Implementation
- Keep existing in-memory implementations
- Add PostgreSQL implementations alongside
- Feature flag to switch between implementations

### Step 2: Gradual Rollout
- Development: Test with PostgreSQL
- Staging: Enable for approval persistence first
- Staging: Enable for distributed locks
- Production: Enable when validated

### Step 3: Deprecation
- Remove in-memory implementations after 2 sprints
- Update documentation
- Clean up unused code

---

## Performance Characteristics

### PostgreSQL Advisory Locks
- **Latency**: <1ms (in-memory on DB server)
- **Throughput**: 10,000+ locks/second
- **Scalability**: Limited by DB connections (100-1000)

### Database-Backed Queues
- **Latency**: 5-10ms (with LISTEN/NOTIFY)
- **Throughput**: 1,000+ messages/second
- **Scalability**: Millions of messages (with partitioning)

### Outbox Pattern Jobs
- **Latency**: 5-10 seconds (polling interval)
- **Throughput**: 100+ jobs/second
- **Scalability**: Horizontal (multiple workers)

---

## Comparison: PostgreSQL vs Redis

| Feature | PostgreSQL | Redis |
|---------|-----------|-------|
| **Already Integrated** | ✅ Yes (EF Core) | ❌ No (new dependency) |
| **Deployment Complexity** | ✅ Simple (reuse DB) | ❌ Additional service |
| **Durability** | ✅ ACID guarantees | ⚠️ Optional (RDB/AOF) |
| **Distributed Locks** | ✅ Advisory locks | ✅ Redlock algorithm |
| **Message Queue** | ✅ LISTEN/NOTIFY | ✅ Pub/Sub, Streams |
| **Performance** | ✅ Good (<10ms) | ✅ Excellent (<1ms) |
| **Scalability** | ⚠️ Vertical primarily | ✅ Horizontal (cluster) |
| **Learning Curve** | ✅ Already know SQL | ⚠️ New commands |
| **Operational Overhead** | ✅ One system | ❌ Two systems |

**Recommendation**: PostgreSQL is the **better choice** for this project because:
1. Already integrated (no new infrastructure)
2. Simpler deployment and operations
3. Strong consistency guarantees
4. Good enough performance for expected load
5. No Redis issues in this environment

---

## References

- **PostgreSQL Advisory Locks**: https://www.postgresql.org/docs/current/functions-admin.html#FUNCTIONS-ADVISORY-LOCKS
- **PostgreSQL LISTEN/NOTIFY**: https://www.postgresql.org/docs/current/sql-notify.html
- **Outbox Pattern**: https://microservices.io/patterns/data/transactional-outbox.html
- **SKIP LOCKED**: https://www.postgresql.org/docs/current/sql-select.html#SQL-FOR-UPDATE-SHARE

---

## Estimated Effort

- **Phase 1** (Distributed Locks): 2 days
- **Phase 2** (Approval Persistence): 1 day
- **Phase 3** (Job Queue): 2-3 days
- **Phase 4** (Message Queue): 1-2 days

**Total**: 6-8 days

**Benefits**:
- ✅ Fixes all 4 critical distributed systems issues
- ✅ No Redis required
- ✅ Production-ready horizontal scaling
- ✅ Better than in-memory alternatives
- ✅ Lower operational complexity than Redis
