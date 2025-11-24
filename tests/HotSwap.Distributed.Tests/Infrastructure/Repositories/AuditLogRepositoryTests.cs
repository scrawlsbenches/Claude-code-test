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
            EventType = "DeploymentStarted",
            EventCategory = "Deployment",
            Action = "Deploy",
            ResourceType = "Module",
            ResourceId = Guid.NewGuid().ToString(),
            Metadata = "{\"version\": \"1.0.0\"}"
        };

        // Act
        await _repository.CreateAsync(auditLog);

        // Assert
        var retrieved = await _repository.GetByIdAsync(auditLog.EventId);
        retrieved.Should().NotBeNull();
        retrieved!.EventType.Should().Be("DeploymentStarted");
        retrieved.Action.Should().Be("Deploy");
        retrieved.ResourceType.Should().Be("Module");
    }

    [Fact]
    public async Task GetByIdAsync_WhenExists_ShouldReturnAuditLog()
    {
        // Arrange
        var auditLog = new AuditLog
        {
            EventType = "ApprovalRequested",
            EventCategory = "Approval",
            Action = "Create",
            ResourceType = "Approval",
            ResourceId = Guid.NewGuid().ToString()
        };

        await _repository.CreateAsync(auditLog);

        // Act
        var result = await _repository.GetByIdAsync(auditLog.EventId);

        // Assert
        result.Should().NotBeNull();
        result!.EventId.Should().Be(auditLog.EventId);
        result.EventType.Should().Be("ApprovalRequested");
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
    public async Task GetByEntityAsync_ShouldReturnAuditLogsForResource()
    {
        // Arrange
        var resourceId = Guid.NewGuid();
        var resourceType = "Deployment";

        var auditLogs = new[]
        {
            new AuditLog
            {
                EventType = "DeploymentCreated",
                EventCategory = "Deployment",
                Action = "Create",
                ResourceType = resourceType,
                ResourceId = resourceId.ToString(),
                Timestamp = DateTime.UtcNow.AddMinutes(-10)
            },
            new AuditLog
            {
                EventType = "DeploymentUpdated",
                EventCategory = "Deployment",
                Action = "Update",
                ResourceType = resourceType,
                ResourceId = resourceId.ToString(),
                Timestamp = DateTime.UtcNow.AddMinutes(-5)
            },
            new AuditLog
            {
                EventType = "DeploymentCompleted",
                EventCategory = "Deployment",
                Action = "Complete",
                ResourceType = resourceType,
                ResourceId = resourceId.ToString(),
                Timestamp = DateTime.UtcNow
            },
            // Different resource - should not be returned
            new AuditLog
            {
                EventType = "DeploymentCreated",
                EventCategory = "Deployment",
                Action = "Create",
                ResourceType = resourceType,
                ResourceId = Guid.NewGuid().ToString(),
                Timestamp = DateTime.UtcNow
            }
        };

        foreach (var log in auditLogs)
        {
            await _repository.CreateAsync(log);
        }

        // Act
        var results = await _repository.GetByEntityAsync(resourceType, resourceId);

        // Assert
        results.Should().HaveCount(3);
        results.Should().OnlyContain(l => l.ResourceId == resourceId.ToString());
        results.Should().OnlyContain(l => l.ResourceType == resourceType);

        // Should be ordered by timestamp descending (most recent first)
        results[0].EventType.Should().Be("DeploymentCompleted");
        results[1].EventType.Should().Be("DeploymentUpdated");
        results[2].EventType.Should().Be("DeploymentCreated");
    }

    [Fact]
    public async Task GetByEntityAsync_WhenNoMatchingLogs_ShouldReturnEmptyList()
    {
        // Arrange
        var resourceId = Guid.NewGuid();
        var resourceType = "NonExistent";

        // Act
        var results = await _repository.GetByEntityAsync(resourceType, resourceId);

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByEntityAsync_ShouldFilterByResourceType()
    {
        // Arrange
        var resourceId = Guid.NewGuid();

        var auditLogs = new[]
        {
            new AuditLog
            {
                EventType = "DeploymentCreated",
                EventCategory = "Deployment",
                Action = "Create",
                ResourceType = "Deployment",
                ResourceId = resourceId.ToString()
            },
            new AuditLog
            {
                EventType = "ApprovalRequested",
                EventCategory = "Approval",
                Action = "Create",
                ResourceType = "Approval",
                ResourceId = resourceId.ToString() // Same resource ID, different type
            }
        };

        foreach (var log in auditLogs)
        {
            await _repository.CreateAsync(log);
        }

        // Act
        var deploymentResults = await _repository.GetByEntityAsync("Deployment", resourceId);
        var approvalResults = await _repository.GetByEntityAsync("Approval", resourceId);

        // Assert
        deploymentResults.Should().HaveCount(1);
        deploymentResults[0].ResourceType.Should().Be("Deployment");

        approvalResults.Should().HaveCount(1);
        approvalResults[0].ResourceType.Should().Be("Approval");
    }

    [Fact]
    public async Task CreateAsync_WithCancellation_ShouldRespectCancellationToken()
    {
        // Arrange
        var auditLog = new AuditLog
        {
            EventType = "Test",
            EventCategory = "Test",
            Action = "Create",
            ResourceType = "Test",
            ResourceId = Guid.NewGuid().ToString()
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
    public async Task CreateAsync_WithMetadata_ShouldStoreJsonCorrectly()
    {
        // Arrange
        var complexMetadata = @"{
            ""before"": {""status"": ""Pending"", ""version"": ""1.0.0""},
            ""after"": {""status"": ""Approved"", ""version"": ""1.0.0""},
            ""approver"": ""admin@example.com""
        }";

        var auditLog = new AuditLog
        {
            EventType = "StatusChanged",
            EventCategory = "Approval",
            Action = "Update",
            ResourceType = "Approval",
            ResourceId = Guid.NewGuid().ToString(),
            Metadata = complexMetadata
        };

        // Act
        await _repository.CreateAsync(auditLog);

        // Assert
        var retrieved = await _repository.GetByIdAsync(auditLog.EventId);
        retrieved.Should().NotBeNull();
        retrieved!.Metadata.Should().Contain("Pending");
        retrieved.Metadata.Should().Contain("Approved");
        retrieved.Metadata.Should().Contain("admin@example.com");
    }

    [Fact]
    public async Task MultipleAuditLogs_ShouldMaintainIndependence()
    {
        // Arrange
        var logs = Enumerable.Range(1, 10).Select(i => new AuditLog
        {
            EventType = $"Event{i}",
            EventCategory = "Test",
            Action = $"Action{i}",
            ResourceType = "TestEntity",
            ResourceId = Guid.NewGuid().ToString(),
            Metadata = $"{{\"change\": {i}}}"
        }).ToList();

        // Act
        foreach (var log in logs)
        {
            await _repository.CreateAsync(log);
        }

        // Assert
        foreach (var log in logs)
        {
            var retrieved = await _repository.GetByIdAsync(log.EventId);
            retrieved.Should().NotBeNull();
            retrieved!.EventType.Should().Be(log.EventType);
            retrieved.Action.Should().Be(log.Action);
        }
    }

    public void Dispose()
    {
        _dbContext?.Dispose();
        _connection?.Dispose();
    }
}
