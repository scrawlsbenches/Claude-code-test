# Database Migration Helper Skill

**Version:** 1.0.0
**Last Updated:** 2025-11-20
**Skill Type:** Infrastructure & Data
**Estimated Time:** 1-3 hours per migration
**Complexity:** Medium

---

## Purpose

This skill guides you through Entity Framework Core database migrations for PostgreSQL, from initial setup through testing and deployment. Focused on completing Task #3 (PostgreSQL audit log persistence).

**Use this skill when:**
- Setting up Entity Framework Core with PostgreSQL
- Creating database migrations for new entities
- Testing migrations locally before deployment
- Rolling back problematic migrations
- Troubleshooting migration errors

**This skill addresses:**
- Task #3: PostgreSQL Audit Log Persistence (85% complete ’ 100%)
- Any future database schema changes
- Entity model updates requiring migrations

---

## Prerequisites

**Before using this skill:**
- [ ] PostgreSQL server available (local or remote)
- [ ] Connection string configured
- [ ] Entity models defined (Domain layer)
- [ ] DbContext class created

**Required NuGet packages:**
```xml
<PackageReference Include="Microsoft.EntityFrameworkCore" Version="8.0.0" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="8.0.0" />
<PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="8.0.0" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Tools" Version="8.0.0" />
```

**Required tools:**
```bash
# Install EF Core CLI tools
dotnet tool install --global dotnet-ef
# Or update if already installed
dotnet tool update --global dotnet-ef

# Verify installation
dotnet ef --version
# Expected: 8.0.x or later
```

---

## Phase 1: Setup Entity Framework Core

### Step 1.1: Install NuGet Packages

```bash
cd src/HotSwap.Distributed.Infrastructure

# Install EF Core packages
dotnet add package Microsoft.EntityFrameworkCore --version 8.0.0
dotnet add package Microsoft.EntityFrameworkCore.Design --version 8.0.0
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL --version 8.0.0
dotnet add package Microsoft.EntityFrameworkCore.Tools --version 8.0.0
```

### Step 1.2: Create Entity Models

**Example: Audit Log Entity** (Task #3 specific)

```csharp
// src/HotSwap.Distributed.Infrastructure/Data/Entities/AuditLog.cs
namespace HotSwap.Distributed.Infrastructure.Data.Entities;

public class AuditLog
{
    public long Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string EventType { get; set; } = null!;
    public string? UserId { get; set; }
    public string? TraceId { get; set; }
    public string? Details { get; set; } // JSON
    public string? SourceIp { get; set; }
    public string? UserAgent { get; set; }
}

public class DeploymentAuditEvent : AuditLog
{
    public string DeploymentId { get; set; } = null!;
    public string? Stage { get; set; }
    public string? Status { get; set; }
    public TimeSpan? Duration { get; set; }
    public string? ErrorMessage { get; set; }
}

public class ApprovalAuditEvent : AuditLog
{
    public string ApprovalId { get; set; } = null!;
    public string DeploymentId { get; set; } = null!;
    public string? Decision { get; set; } // Approved, Rejected
    public string? Reason { get; set; }
    public string? ApproverEmail { get; set; }
}

public class AuthenticationAuditEvent : AuditLog
{
    public string? Username { get; set; }
    public string? EventSubtype { get; set; } // Login, Logout, TokenValidation, etc.
    public bool Success { get; set; }
    public string? FailureReason { get; set; }
}
```

### Step 1.3: Create DbContext

```csharp
// src/HotSwap.Distributed.Infrastructure/Data/AuditLogDbContext.cs
using Microsoft.EntityFrameworkCore;
using HotSwap.Distributed.Infrastructure.Data.Entities;

namespace HotSwap.Distributed.Infrastructure.Data;

public class AuditLogDbContext : DbContext
{
    public AuditLogDbContext(DbContextOptions<AuditLogDbContext> options)
        : base(options)
    {
    }

    public DbSet<AuditLog> AuditLogs { get; set; } = null!;
    public DbSet<DeploymentAuditEvent> DeploymentEvents { get; set; } = null!;
    public DbSet<ApprovalAuditEvent> ApprovalEvents { get; set; } = null!;
    public DbSet<AuthenticationAuditEvent> AuthenticationEvents { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure table names
        modelBuilder.Entity<AuditLog>().ToTable("audit_logs");
        modelBuilder.Entity<DeploymentAuditEvent>().ToTable("deployment_audit_events");
        modelBuilder.Entity<ApprovalAuditEvent>().ToTable("approval_audit_events");
        modelBuilder.Entity<AuthenticationAuditEvent>().ToTable("authentication_audit_events");

        // Configure indexes for performance
        modelBuilder.Entity<AuditLog>()
            .HasIndex(a => a.Timestamp)
            .HasDatabaseName("idx_audit_logs_timestamp");

        modelBuilder.Entity<AuditLog>()
            .HasIndex(a => a.EventType)
            .HasDatabaseName("idx_audit_logs_event_type");

        modelBuilder.Entity<AuditLog>()
            .HasIndex(a => a.TraceId)
            .HasDatabaseName("idx_audit_logs_trace_id");

        modelBuilder.Entity<DeploymentAuditEvent>()
            .HasIndex(d => d.DeploymentId)
            .HasDatabaseName("idx_deployment_events_deployment_id");

        modelBuilder.Entity<ApprovalAuditEvent>()
            .HasIndex(a => a.ApprovalId)
            .HasDatabaseName("idx_approval_events_approval_id");

        modelBuilder.Entity<AuthenticationAuditEvent>()
            .HasIndex(a => a.Username)
            .HasDatabaseName("idx_authentication_events_username");

        // Configure string lengths (PostgreSQL best practice)
        modelBuilder.Entity<AuditLog>()
            .Property(a => a.EventType)
            .HasMaxLength(100);

        modelBuilder.Entity<AuditLog>()
            .Property(a => a.UserId)
            .HasMaxLength(256);

        modelBuilder.Entity<AuditLog>()
            .Property(a => a.TraceId)
            .HasMaxLength(100);

        modelBuilder.Entity<AuditLog>()
            .Property(a => a.SourceIp)
            .HasMaxLength(45); // IPv6 max length

        // Configure JSON column for PostgreSQL
        modelBuilder.Entity<AuditLog>()
            .Property(a => a.Details)
            .HasColumnType("jsonb"); // PostgreSQL-specific
    }
}
```

### Step 1.4: Create DbContext Factory (for migrations)

```csharp
// src/HotSwap.Distributed.Infrastructure/Data/AuditLogDbContextFactory.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace HotSwap.Distributed.Infrastructure.Data;

/// <summary>
/// Design-time factory for EF Core migrations.
/// Used by 'dotnet ef migrations' commands.
/// </summary>
public class AuditLogDbContextFactory : IDesignTimeDbContextFactory<AuditLogDbContext>
{
    public AuditLogDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AuditLogDbContext>();

        // Use connection string from environment variable or default
        var connectionString = Environment.GetEnvironmentVariable("AUDIT_LOG_CONNECTION_STRING")
            ?? "Host=localhost;Port=5432;Database=hotswap_audit;Username=postgres;Password=postgres";

        optionsBuilder.UseNpgsql(connectionString);

        return new AuditLogDbContext(optionsBuilder.Options);
    }
}
```

### Step 1.5: Configure Connection String

**In appsettings.json:**
```json
{
  "ConnectionStrings": {
    "AuditLog": "Host=localhost;Port=5432;Database=hotswap_audit;Username=postgres;Password=postgres"
  }
}
```

**In Program.cs:**
```csharp
// Add DbContext to dependency injection
builder.Services.AddDbContext<AuditLogDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("AuditLog");
    options.UseNpgsql(connectionString);
});
```

---

## Phase 2: Create and Apply Migrations

### Step 2.1: Create Initial Migration

```bash
cd src/HotSwap.Distributed.Infrastructure

# Create initial migration
dotnet ef migrations add InitialAuditLogSchema \
  --context AuditLogDbContext \
  --output-dir Data/Migrations

# Expected output:
# Build started...
# Build succeeded.
# Done. To undo this action, use 'dotnet ef migrations remove'
```

**Verify migration created:**
```bash
ls Data/Migrations/
# Expected files:
# 20251120120000_InitialAuditLogSchema.cs
# 20251120120000_InitialAuditLogSchema.Designer.cs
# AuditLogDbContextModelSnapshot.cs
```

### Step 2.2: Review Generated Migration

```csharp
// Data/Migrations/20251120120000_InitialAuditLogSchema.cs
public partial class InitialAuditLogSchema : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "audit_logs",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                EventType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                UserId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                TraceId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                Details = table.Column<string>(type: "jsonb", nullable: true),
                SourceIp = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                UserAgent = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_audit_logs", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "idx_audit_logs_timestamp",
            table: "audit_logs",
            column: "Timestamp");

        migrationBuilder.CreateIndex(
            name: "idx_audit_logs_event_type",
            table: "audit_logs",
            column: "EventType");

        migrationBuilder.CreateIndex(
            name: "idx_audit_logs_trace_id",
            table: "audit_logs",
            column: "TraceId");

        // ... (more indexes for other tables)
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "audit_logs");
        migrationBuilder.DropTable(name: "deployment_audit_events");
        migrationBuilder.DropTable(name: "approval_audit_events");
        migrationBuilder.DropTable(name: "authentication_audit_events");
    }
}
```

**Checklist:**
- [ ] Migration creates all tables (audit_logs, deployment_audit_events, etc.)
- [ ] All indexes are created (idx_audit_logs_timestamp, etc.)
- [ ] Down() method drops tables correctly (for rollback)
- [ ] Column types are correct (bigint, timestamp, varchar, jsonb)

### Step 2.3: Test Migration Locally (SQLite)

**Quick test with SQLite before PostgreSQL:**

```bash
# Create test-specific DbContext factory
cat > Data/TestAuditLogDbContextFactory.cs << 'EOF'
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace HotSwap.Distributed.Infrastructure.Data;

public class TestAuditLogDbContextFactory : IDesignTimeDbContextFactory<AuditLogDbContext>
{
    public AuditLogDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AuditLogDbContext>();
        optionsBuilder.UseSqlite("Data Source=test_audit.db");
        return new AuditLogDbContext(optionsBuilder.Options);
    }
}
EOF

# Apply migration to SQLite
dotnet ef database update \
  --context AuditLogDbContext \
  -- --provider Sqlite

# Verify tables created
sqlite3 test_audit.db ".tables"
# Expected: audit_logs, deployment_audit_events, approval_audit_events, etc.

# Check schema
sqlite3 test_audit.db ".schema audit_logs"

# Clean up
rm test_audit.db
```

### Step 2.4: Apply Migration to PostgreSQL

**Start PostgreSQL (Docker):**
```bash
docker run -d \
  --name postgres-audit \
  -e POSTGRES_PASSWORD=postgres \
  -e POSTGRES_DB=hotswap_audit \
  -p 5432:5432 \
  postgres:15
```

**Set connection string:**
```bash
export AUDIT_LOG_CONNECTION_STRING="Host=localhost;Port=5432;Database=hotswap_audit;Username=postgres;Password=postgres"
```

**Apply migration:**
```bash
cd src/HotSwap.Distributed.Infrastructure

dotnet ef database update \
  --context AuditLogDbContext

# Expected output:
# Build started...
# Build succeeded.
# Applying migration '20251120120000_InitialAuditLogSchema'.
# Done.
```

**Verify tables created:**
```bash
# Connect to PostgreSQL
docker exec -it postgres-audit psql -U postgres -d hotswap_audit

# List tables
\dt

# Expected output:
# public | audit_logs                  | table | postgres
# public | deployment_audit_events     | table | postgres
# public | approval_audit_events       | table | postgres
# public | authentication_audit_events | table | postgres
# public | __EFMigrationsHistory       | table | postgres

# Check indexes
\di

# Quit
\q
```

---

## Phase 3: Test the Migration

### Step 3.1: Write Integration Test

```csharp
// tests/HotSwap.Distributed.Tests/Infrastructure/AuditLogDbContextTests.cs
using Xunit;
using Microsoft.EntityFrameworkCore;
using HotSwap.Distributed.Infrastructure.Data;
using HotSwap.Distributed.Infrastructure.Data.Entities;

public class AuditLogDbContextTests : IDisposable
{
    private readonly AuditLogDbContext _context;

    public AuditLogDbContextTests()
    {
        // Use in-memory database for fast tests
        var options = new DbContextOptionsBuilder<AuditLogDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new AuditLogDbContext(options);
        _context.Database.EnsureCreated();
    }

    [Fact]
    public async Task CanInsertAndRetrieveAuditLog()
    {
        // Arrange
        var auditLog = new AuditLog
        {
            Timestamp = DateTime.UtcNow,
            EventType = "Deployment.Started",
            UserId = "test-user",
            TraceId = "trace-123",
            Details = "{\"moduleName\": \"test-module\"}",
            SourceIp = "192.168.1.1",
            UserAgent = "Mozilla/5.0"
        };

        // Act
        _context.AuditLogs.Add(auditLog);
        await _context.SaveChangesAsync();

        // Assert
        var retrieved = await _context.AuditLogs.FirstOrDefaultAsync();
        Assert.NotNull(retrieved);
        Assert.Equal("Deployment.Started", retrieved.EventType);
        Assert.Equal("test-user", retrieved.UserId);
    }

    [Fact]
    public async Task CanInsertDeploymentAuditEvent()
    {
        // Arrange
        var deploymentEvent = new DeploymentAuditEvent
        {
            Timestamp = DateTime.UtcNow,
            EventType = "Deployment.Completed",
            DeploymentId = "deploy-123",
            Stage = "Production",
            Status = "Completed",
            Duration = TimeSpan.FromMinutes(15)
        };

        // Act
        _context.DeploymentEvents.Add(deploymentEvent);
        await _context.SaveChangesAsync();

        // Assert
        var retrieved = await _context.DeploymentEvents
            .FirstOrDefaultAsync(d => d.DeploymentId == "deploy-123");

        Assert.NotNull(retrieved);
        Assert.Equal("Production", retrieved.Stage);
        Assert.Equal(TimeSpan.FromMinutes(15), retrieved.Duration);
    }

    [Fact]
    public async Task CanQueryByTimestampIndex()
    {
        // Arrange
        var yesterday = DateTime.UtcNow.AddDays(-1);
        var today = DateTime.UtcNow;

        _context.AuditLogs.Add(new AuditLog
        {
            Timestamp = yesterday,
            EventType = "Test.Old"
        });

        _context.AuditLogs.Add(new AuditLog
        {
            Timestamp = today,
            EventType = "Test.New"
        });

        await _context.SaveChangesAsync();

        // Act - Query using indexed timestamp
        var recentLogs = await _context.AuditLogs
            .Where(a => a.Timestamp >= yesterday)
            .OrderByDescending(a => a.Timestamp)
            .ToListAsync();

        // Assert
        Assert.Equal(2, recentLogs.Count);
        Assert.Equal("Test.New", recentLogs[0].EventType);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
```

**Run tests:**
```bash
dotnet test --filter "FullyQualifiedName~AuditLogDbContextTests"

# Expected: All 3 tests pass
```

### Step 3.2: Test Against Real PostgreSQL

```csharp
// tests/HotSwap.Distributed.IntegrationTests/AuditLogPostgreSqlTests.cs
[Collection("PostgreSQL")]
public class AuditLogPostgreSqlTests : IClassFixture<PostgreSqlFixture>, IDisposable
{
    private readonly AuditLogDbContext _context;

    public AuditLogPostgreSqlTests(PostgreSqlFixture fixture)
    {
        var options = new DbContextOptionsBuilder<AuditLogDbContext>()
            .UseNpgsql(fixture.ConnectionString)
            .Options;

        _context = new AuditLogDbContext(options);
    }

    [Fact]
    public async Task CanInsertAndRetrieveFromPostgreSQL()
    {
        // Similar to above but uses real PostgreSQL
        var auditLog = new AuditLog
        {
            Timestamp = DateTime.UtcNow,
            EventType = "PostgreSQL.Test",
            Details = "{\"test\": true}"
        };

        _context.AuditLogs.Add(auditLog);
        await _context.SaveChangesAsync();

        var retrieved = await _context.AuditLogs
            .Where(a => a.EventType == "PostgreSQL.Test")
            .FirstOrDefaultAsync();

        Assert.NotNull(retrieved);
        Assert.Contains("\"test\": true", retrieved.Details);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
```

---

## Phase 4: Rollback (If Needed)

### Step 4.1: List Migrations

```bash
dotnet ef migrations list --context AuditLogDbContext

# Expected output:
# 20251120120000_InitialAuditLogSchema (Applied)
```

### Step 4.2: Rollback Migration

```bash
# Rollback to previous migration (or 0 for complete rollback)
dotnet ef database update 0 --context AuditLogDbContext

# Expected output:
# Reverting migration '20251120120000_InitialAuditLogSchema'.
# Done.
```

### Step 4.3: Remove Migration File

```bash
dotnet ef migrations remove --context AuditLogDbContext

# Expected output:
# Removing migration '20251120120000_InitialAuditLogSchema'.
# Done.
```

---

## Phase 5: Production Deployment

### Step 5.1: Generate SQL Script

**Generate SQL for review:**
```bash
dotnet ef migrations script \
  --context AuditLogDbContext \
  --output audit_log_migration.sql

# Review the SQL script before applying to production
cat audit_log_migration.sql
```

### Step 5.2: Apply to Production

**Option 1: Use EF Core (automated):**
```csharp
// In Program.cs startup
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AuditLogDbContext>();
    dbContext.Database.Migrate(); // Apply pending migrations
}
```

**Option 2: Use SQL script (manual/DBA-reviewed):**
```bash
# Apply SQL script to production database
psql -h production-db.example.com -U postgres -d hotswap_audit -f audit_log_migration.sql
```

### Step 5.3: Verify Production Deployment

```bash
# Connect to production database
psql -h production-db.example.com -U postgres -d hotswap_audit

# Verify tables exist
\dt

# Verify migration applied
SELECT * FROM "__EFMigrationsHistory";

# Expected output shows InitialAuditLogSchema migration
```

---

## Success Criteria

Migration is complete when:

- [ ] All NuGet packages installed
- [ ] Entity models created and configured
- [ ] DbContext created with proper OnModelCreating
- [ ] Migration generated (dotnet ef migrations add)
- [ ] Migration reviewed for correctness
- [ ] Migration tested locally (SQLite or PostgreSQL)
- [ ] Unit tests pass for DbContext
- [ ] Integration tests pass against PostgreSQL
- [ ] Migration applied to production
- [ ] Tables and indexes verified in production database
- [ ] TASK_LIST.md updated (Task #3: 85% ’ 100%)

---

## Quick Reference Commands

```bash
# Install EF Core CLI tools
dotnet tool install --global dotnet-ef

# Add migration
dotnet ef migrations add MigrationName --context DbContextName

# Apply migration
dotnet ef database update --context DbContextName

# Rollback migration
dotnet ef database update PreviousMigrationName --context DbContextName

# Remove last migration
dotnet ef migrations remove --context DbContextName

# Generate SQL script
dotnet ef migrations script --context DbContextName --output script.sql

# List migrations
dotnet ef migrations list --context DbContextName

# Drop database (use carefully!)
dotnet ef database drop --context DbContextName --force
```

---

## Troubleshooting

**Q: "No DbContext was found"**
A: Ensure DbContextFactory is in the same project and implements IDesignTimeDbContextFactory.

**Q: "Build failed"**
A: Run `dotnet build` first to check for compilation errors. Fix them before running migrations.

**Q: "Password authentication failed for user"**
A: Check connection string in AUDIT_LOG_CONNECTION_STRING environment variable or appsettings.json.

**Q: "Migration already applied"**
A: Check `__EFMigrationsHistory` table. Use `dotnet ef database update 0` to rollback, then reapply.

**Q: "Column type mismatch"**
A: PostgreSQL is case-sensitive and type-strict. Ensure `.HasColumnType("jsonb")` for JSON columns.

---

## Related Skills

- **tdd-helper**: Use TDD when creating repository implementations
- **precommit-check**: Run before committing migration files
- **integration-test-debugger**: Debug integration tests using the database

---

**Last Updated:** 2025-11-20
**Version:** 1.0.0
**Completes:** Task #3 (PostgreSQL Audit Log Persistence)
**Estimated Time Saved:** 1-2 days of trial-and-error with EF Core
