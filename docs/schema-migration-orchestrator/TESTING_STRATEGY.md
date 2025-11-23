# Testing Strategy

**Version:** 1.0.0
**Target Coverage:** 85%+
**Test Count:** 350+ tests
**Framework:** xUnit, Moq, FluentAssertions

---

## Overview

The schema migration orchestrator follows **Test-Driven Development (TDD)** with comprehensive test coverage across all layers.

### Test Pyramid

```
                 ▲
                / \
               /E2E\           6% - End-to-End Tests (20 tests)
              /_____\
             /       \
            /Integration\      14% - Integration Tests (50 tests)
           /___________\
          /             \
         /  Unit Tests   \     80% - Unit Tests (280 tests)
        /_________________\
```

**Total Tests:** 350+ tests across all layers

---

## Unit Testing

**Target:** 280+ unit tests, 85%+ code coverage

### Domain Model Tests

**File:** `tests/HotSwap.SchemaMigration.Tests/Domain/MigrationTests.cs`

```csharp
public class MigrationTests
{
    [Fact]
    public void Migration_WithValidData_PassesValidation()
    {
        // Arrange
        var migration = new Migration
        {
            MigrationId = Guid.NewGuid().ToString(),
            Name = "add_users_email_index",
            TargetDatabaseId = "db-1",
            MigrationScript = "CREATE INDEX ...",
            RollbackScript = "DROP INDEX ...",
            CreatedBy = "admin@example.com"
        };

        // Act
        var isValid = migration.IsValid(out var errors);

        // Assert
        isValid.Should().BeTrue();
        errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData(RiskLevel.Low, false)]
    [InlineData(RiskLevel.Medium, true)]
    [InlineData(RiskLevel.High, true)]
    public void RequiresApproval_BasedOnRiskLevel_ReturnsCorrectValue(
        RiskLevel riskLevel, bool expectedRequiresApproval)
    {
        // Arrange
        var migration = CreateMigration();
        migration.RiskLevel = riskLevel;

        // Act
        var requiresApproval = migration.RequiresApproval();

        // Assert
        requiresApproval.Should().Be(expectedRequiresApproval);
    }
}
```

---

### Migration Strategy Tests

**File:** `tests/HotSwap.SchemaMigration.Tests/Strategies/PhasedMigrationStrategyTests.cs`

```csharp
public class PhasedMigrationStrategyTests
{
    [Fact]
    public async Task ExecuteAsync_WithMultipleReplicas_ExecutesReplicasBeforeMaster()
    {
        // Arrange
        var strategy = new PhasedMigrationStrategy(_mockDriver.Object, _mockMonitor.Object);
        var migration = CreateMigration();
        var database = CreateDatabaseWithReplicas(3);

        // Act
        var result = await strategy.ExecuteAsync(migration, database);

        // Assert
        result.Success.Should().BeTrue();
        VerifyReplicasExecutedFirst();
    }
}
```

---

## Integration Testing

**Target:** 50+ integration tests

**Test Scenarios:**
1. Create migration → Execute on PostgreSQL → Verify success
2. Execute migration → Performance degradation → Automatic rollback
3. Phased rollout → Replica failure → Rollback phase
4. Blue-Green deployment → Traffic switch → Validate

### End-to-End Migration Test

```csharp
[Collection("Integration")]
public class MigrationE2ETests : IClassFixture<TestServerFixture>
{
    [Fact]
    public async Task EndToEndMigration_CreateExecuteRollback_WorksCorrectly()
    {
        // Arrange - Create migration
        var createResponse = await _client.PostAsJsonAsync("/api/v1/migrations", new
        {
            name = "add_test_index",
            targetDatabaseId = "test-db",
            migrationScript = "CREATE INDEX ...",
            rollbackScript = "DROP INDEX ..."
        });
        var migration = await createResponse.Content.ReadFromJsonAsync<Migration>();

        // Act - Execute migration
        var executeResponse = await _client.PostAsJsonAsync(
            $"/api/v1/migrations/{migration.MigrationId}/execute",
            new { environment = "Development", strategy = "Direct" }
        );

        // Assert - Migration succeeded
        var execution = await executeResponse.Content.ReadFromJsonAsync<MigrationExecution>();
        execution.Status.Should().Be(ExecutionStatus.Succeeded);

        // Act - Rollback migration
        await _client.PostAsJsonAsync(
            $"/api/v1/migrations/{migration.MigrationId}/executions/{execution.ExecutionId}/rollback",
            new { reason = "Test rollback" }
        );

        // Assert - Rollback succeeded
        var rollbackExecution = await GetExecutionAsync(execution.ExecutionId);
        rollbackExecution.Status.Should().Be(ExecutionStatus.RolledBack);
    }
}
```

---

## Performance Testing

### Throughput Test

```csharp
[Fact]
public async Task Throughput_10ConcurrentMigrations_CompletesWithinTimeout()
{
    // Arrange
    var migrations = CreateMigrations(10);
    var timeout = TimeSpan.FromMinutes(15);

    // Act
    var stopwatch = Stopwatch.StartNew();
    var tasks = migrations.Select(m => ExecuteMigrationAsync(m));
    await Task.WhenAll(tasks);
    stopwatch.Stop();

    // Assert
    stopwatch.Elapsed.Should().BeLessThan(timeout);
}
```

---

## TDD Workflow

Follow Red-Green-Refactor cycle:

**Step 1: 🔴 RED - Write Failing Test**
```csharp
[Fact]
public async Task RollbackAsync_WhenPerformanceDegrades_ExecutesRollback()
{
    // This test will fail - RollbackAsync doesn't exist yet
}
```

**Step 2: 🟢 GREEN - Minimal Implementation**
```csharp
public async Task RollbackAsync(Migration migration)
{
    await ExecuteSqlAsync(migration.RollbackScript);
}
```

**Step 3: 🔵 REFACTOR - Improve Implementation**
```csharp
public async Task RollbackAsync(Migration migration)
{
    using var activity = _telemetry.StartActivity("migration.rollback");
    try
    {
        await ExecuteSqlAsync(migration.RollbackScript);
        _metrics.IncrementRollbackCount();
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Rollback failed");
        throw;
    }
}
```

---

## CI/CD Integration

```yaml
name: Schema Migration Tests

on:
  push:
    branches: [main, claude/*]

jobs:
  test:
    runs-on: ubuntu-latest
    
    services:
      postgres:
        image: postgres:15
        env:
          POSTGRES_PASSWORD: test_password
        ports:
          - 5432:5432

    steps:
      - uses: actions/checkout@v3
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '8.0.x'
      
      - name: Run tests
        run: |
          dotnet test tests/HotSwap.SchemaMigration.Tests/
          dotnet test tests/HotSwap.SchemaMigration.IntegrationTests/
          dotnet test tests/HotSwap.SchemaMigration.E2ETests/
```

---

**Last Updated:** 2025-11-23
**Test Count:** 350+ tests
**Coverage Target:** 85%+
