using FluentAssertions;
using HotSwap.Distributed.Infrastructure.Data;
using HotSwap.Distributed.Infrastructure.Data.Entities;
using HotSwap.Distributed.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HotSwap.Distributed.Tests.Infrastructure.Repositories;

/// <summary>
/// Unit tests for AuditLogRepository (DESIGN-04 support)
/// Tests repository operations for audit log entities
/// </summary>
public class AuditLogRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AuditLogDbContext _dbContext;
    private readonly AuditLogRepository _repository;

    public AuditLogRepositoryTests()
    {
        // Create in-memory SQLite database for testing
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AuditLogDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new AuditLogDbContext(options);
        _dbContext.Database.EnsureCreated();

        _repository = new AuditLogRepository(_dbContext);
    }

    [Fact]
    public async Task CreateAsync_ShouldAddAuditLog()
    {
        // Arrange
        var auditLog = new AuditLog
        {
            Id = Guid.NewGuid(),
            EntityType = "Deployment",
            EntityId = Guid.NewGuid(),
            Action = "Deploy",
            Timestamp = DateTime.UtcNow,
            UserId = "test-user",
            Changes = "{\"version\": \"1.0.0\"}"
        };

        // Act
        await _repository.CreateAsync(auditLog);
        await _dbContext.SaveChangesAsync();

        // Assert
        var retrieved = await _repository.GetByIdAsync(auditLog.Id);
        retrieved.Should().NotBeNull();
        retrieved!.EntityType.Should().Be("Deployment");
        retrieved.Action.Should().Be("Deploy");
        retrieved.UserId.Should().Be("test-user");
    }

    [Fact]
    public async Task GetByIdAsync_WhenExists_ShouldReturnAuditLog()
    {
        // Arrange
        var auditLog = new AuditLog
        {
            Id = Guid.NewGuid(),
            EntityType = "Approval",
            EntityId = Guid.NewGuid(),
            Action = "Create",
            Timestamp = DateTime.UtcNow,
            UserId = "user@example.com",
            Changes = "{}"
        };

        await _repository.CreateAsync(auditLog);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _repository.GetByIdAsync(auditLog.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(auditLog.Id);
        result.EntityType.Should().Be("Approval");
    }

    [Fact]
    public async Task GetByIdAsync_WhenNotExists_ShouldReturnNull()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await _repository.GetByIdAsync(nonExistentId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByEntityAsync_ShouldReturnAuditLogsForEntity()
    {
        // Arrange
        var entityId = Guid.NewGuid();
        var entityType = "Deployment";

        var auditLogs = new[]
        {
            new AuditLog
            {
                Id = Guid.NewGuid(),
                EntityType = entityType,
                EntityId = entityId,
                Action = "Create",
                Timestamp = DateTime.UtcNow.AddMinutes(-10),
                UserId = "user1",
                Changes = "{}"
            },
            new AuditLog
            {
                Id = Guid.NewGuid(),
                EntityType = entityType,
                EntityId = entityId,
                Action = "Update",
                Timestamp = DateTime.UtcNow.AddMinutes(-5),
                UserId = "user2",
                Changes = "{}"
            },
            new AuditLog
            {
                Id = Guid.NewGuid(),
                EntityType = entityType,
                EntityId = entityId,
                Action = "Delete",
                Timestamp = DateTime.UtcNow,
                UserId = "user3",
                Changes = "{}"
            },
            // Different entity - should not be returned
            new AuditLog
            {
                Id = Guid.NewGuid(),
                EntityType = entityType,
                EntityId = Guid.NewGuid(),
                Action = "Create",
                Timestamp = DateTime.UtcNow,
                UserId = "user4",
                Changes = "{}"
            }
        };

        foreach (var log in auditLogs)
        {
            await _repository.CreateAsync(log);
        }
        await _dbContext.SaveChangesAsync();

        // Act
        var results = await _repository.GetByEntityAsync(entityType, entityId);

        // Assert
        results.Should().HaveCount(3);
        results.Should().OnlyContain(l => l.EntityId == entityId);
        results.Should().OnlyContain(l => l.EntityType == entityType);

        // Should be ordered by timestamp descending (most recent first)
        results[0].Action.Should().Be("Delete");
        results[1].Action.Should().Be("Update");
        results[2].Action.Should().Be("Create");
    }

    [Fact]
    public async Task GetByEntityAsync_WhenNoMatchingLogs_ShouldReturnEmptyList()
    {
        // Arrange
        var entityId = Guid.NewGuid();
        var entityType = "NonExistent";

        // Act
        var results = await _repository.GetByEntityAsync(entityType, entityId);

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByEntityAsync_ShouldFilterByEntityType()
    {
        // Arrange
        var entityId = Guid.NewGuid();

        var auditLogs = new[]
        {
            new AuditLog
            {
                Id = Guid.NewGuid(),
                EntityType = "Deployment",
                EntityId = entityId,
                Action = "Create",
                Timestamp = DateTime.UtcNow,
                UserId = "user1",
                Changes = "{}"
            },
            new AuditLog
            {
                Id = Guid.NewGuid(),
                EntityType = "Approval",
                EntityId = entityId, // Same entity ID, different type
                Action = "Create",
                Timestamp = DateTime.UtcNow,
                UserId = "user2",
                Changes = "{}"
            }
        };

        foreach (var log in auditLogs)
        {
            await _repository.CreateAsync(log);
        }
        await _dbContext.SaveChangesAsync();

        // Act
        var deploymentResults = await _repository.GetByEntityAsync("Deployment", entityId);
        var approvalResults = await _repository.GetByEntityAsync("Approval", entityId);

        // Assert
        deploymentResults.Should().HaveCount(1);
        deploymentResults[0].EntityType.Should().Be("Deployment");

        approvalResults.Should().HaveCount(1);
        approvalResults[0].EntityType.Should().Be("Approval");
    }

    [Fact]
    public async Task CreateAsync_WithCancellation_ShouldRespectCancellationToken()
    {
        // Arrange
        var auditLog = new AuditLog
        {
            Id = Guid.NewGuid(),
            EntityType = "Test",
            EntityId = Guid.NewGuid(),
            Action = "Create",
            Timestamp = DateTime.UtcNow,
            UserId = "test-user",
            Changes = "{}"
        };

        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await _repository.CreateAsync(auditLog, cts.Token));
    }

    [Fact]
    public async Task GetByIdAsync_WithCancellation_ShouldRespectCancellationToken()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await _repository.GetByIdAsync(Guid.NewGuid(), cts.Token));
    }

    [Fact]
    public async Task GetByEntityAsync_WithCancellation_ShouldRespectCancellationToken()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await _repository.GetByEntityAsync("Test", Guid.NewGuid(), cts.Token));
    }

    [Fact]
    public async Task CreateAsync_WithComplexChanges_ShouldStoreJsonCorrectly()
    {
        // Arrange
        var complexChanges = @"{
            ""before"": {""status"": ""Pending"", ""version"": ""1.0.0""},
            ""after"": {""status"": ""Approved"", ""version"": ""1.0.0""},
            ""approver"": ""admin@example.com""
        }";

        var auditLog = new AuditLog
        {
            Id = Guid.NewGuid(),
            EntityType = "Approval",
            EntityId = Guid.NewGuid(),
            Action = "StatusChange",
            Timestamp = DateTime.UtcNow,
            UserId = "admin@example.com",
            Changes = complexChanges
        };

        // Act
        await _repository.CreateAsync(auditLog);
        await _dbContext.SaveChangesAsync();

        // Assert
        var retrieved = await _repository.GetByIdAsync(auditLog.Id);
        retrieved.Should().NotBeNull();
        retrieved!.Changes.Should().Contain("Pending");
        retrieved.Changes.Should().Contain("Approved");
        retrieved.Changes.Should().Contain("admin@example.com");
    }

    [Fact]
    public async Task MultipleAuditLogs_ShouldMaintainIndependence()
    {
        // Arrange
        var logs = Enumerable.Range(1, 10).Select(i => new AuditLog
        {
            Id = Guid.NewGuid(),
            EntityType = "TestEntity",
            EntityId = Guid.NewGuid(),
            Action = $"Action{i}",
            Timestamp = DateTime.UtcNow.AddMinutes(i),
            UserId = $"user{i}",
            Changes = $"{{\"change\": {i}}}"
        }).ToList();

        // Act
        foreach (var log in logs)
        {
            await _repository.CreateAsync(log);
        }
        await _dbContext.SaveChangesAsync();

        // Assert
        foreach (var log in logs)
        {
            var retrieved = await _repository.GetByIdAsync(log.Id);
            retrieved.Should().NotBeNull();
            retrieved!.Action.Should().Be(log.Action);
            retrieved.UserId.Should().Be(log.UserId);
        }
    }

    public void Dispose()
    {
        _dbContext?.Dispose();
        _connection?.Dispose();
    }
}
