using FluentAssertions;
using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Infrastructure.Data;
using HotSwap.Distributed.Infrastructure.Data.Entities;
using HotSwap.Distributed.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace HotSwap.Distributed.Tests.Infrastructure.Data;

/// <summary>
/// Unit tests for UnitOfWork pattern (DESIGN-04 fix)
/// Tests transactional consistency across multiple repositories
/// </summary>
public class UnitOfWorkTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AuditLogDbContext _dbContext;
    private readonly ILoggerFactory _loggerFactory;
    private readonly UnitOfWork _unitOfWork;

    public UnitOfWorkTests()
    {
        // Create in-memory SQLite database for testing
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AuditLogDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new AuditLogDbContext(options);
        _dbContext.Database.EnsureCreated();

        _loggerFactory = NullLoggerFactory.Instance;

        _unitOfWork = new UnitOfWork(_dbContext, NullLogger<UnitOfWork>.Instance, _loggerFactory);
    }

    [Fact]
    public void Constructor_ShouldInitializeRepositories()
    {
        // Assert
        _unitOfWork.Approvals.Should().NotBeNull();
        _unitOfWork.Approvals.Should().BeOfType<ApprovalRepository>();

        _unitOfWork.AuditLogs.Should().NotBeNull();
        _unitOfWork.AuditLogs.Should().BeOfType<AuditLogRepository>();
    }

    [Fact]
    public void CurrentTransaction_ShouldBeNullInitially()
    {
        // Assert
        _unitOfWork.CurrentTransaction.Should().BeNull();
    }

    [Fact]
    public async Task BeginTransactionAsync_ShouldStartTransaction()
    {
        // Act
        await _unitOfWork.BeginTransactionAsync();

        // Assert
        _unitOfWork.CurrentTransaction.Should().NotBeNull();
        _unitOfWork.CurrentTransaction!.TransactionId.Should().NotBeEmpty();
    }

    [Fact]
    public async Task BeginTransactionAsync_CalledTwice_ShouldThrowInvalidOperationException()
    {
        // Arrange
        await _unitOfWork.BeginTransactionAsync();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await _unitOfWork.BeginTransactionAsync());
    }

    [Fact]
    public async Task CommitTransactionAsync_WithoutBegin_ShouldThrowInvalidOperationException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await _unitOfWork.CommitTransactionAsync());
    }

    [Fact]
    public async Task CommitTransactionAsync_ShouldCommitChanges()
    {
        // Arrange
        await _unitOfWork.BeginTransactionAsync();

        var auditLog = new AuditLog
        {
            EventType = "TestEvent",
            EventCategory = "Test",
            Action = "Create",
            ResourceType = "Test",
            ResourceId = Guid.NewGuid().ToString()
        };

        await _unitOfWork.AuditLogs.CreateAsync(auditLog);

        // Act
        await _unitOfWork.CommitTransactionAsync();

        // Assert
        _unitOfWork.CurrentTransaction.Should().BeNull();

        var retrieved = await _unitOfWork.AuditLogs.GetByIdAsync(auditLog.EventId);
        retrieved.Should().NotBeNull();
        retrieved!.Action.Should().Be("Create");
    }

    [Fact]
    public async Task RollbackTransactionAsync_ShouldDiscardChanges()
    {
        // Arrange
        await _unitOfWork.BeginTransactionAsync();

        var auditLog = new AuditLog
        {
            EventType = "TestEvent",
            EventCategory = "Test",
            Action = "Create",
            ResourceType = "Test",
            ResourceId = Guid.NewGuid().ToString()
        };

        await _unitOfWork.AuditLogs.CreateAsync(auditLog);

        // Act
        await _unitOfWork.RollbackTransactionAsync();

        // Assert
        _unitOfWork.CurrentTransaction.Should().BeNull();

        var retrieved = await _unitOfWork.AuditLogs.GetByIdAsync(auditLog.EventId);
        retrieved.Should().BeNull(); // Should not exist after rollback
    }

    [Fact]
    public async Task RollbackTransactionAsync_WithoutBegin_ShouldThrowInvalidOperationException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await _unitOfWork.RollbackTransactionAsync());
    }

    [Fact]
    public async Task SaveChangesAsync_ShouldPersistChanges()
    {
        // Arrange
        var auditLog = new AuditLog
        {
            EventType = "TestEvent",
            EventCategory = "Test",
            Action = "Create",
            ResourceType = "Test",
            ResourceId = Guid.NewGuid().ToString()
        };

        await _unitOfWork.AuditLogs.CreateAsync(auditLog);

        // Act
        var changeCount = await _unitOfWork.SaveChangesAsync();

        // Assert
        changeCount.Should().Be(1);

        var retrieved = await _unitOfWork.AuditLogs.GetByIdAsync(auditLog.EventId);
        retrieved.Should().NotBeNull();
    }

    [Fact]
    public async Task Transaction_ShouldEnsureAtomicity()
    {
        // Arrange
        var approval = new ApprovalRequestEntity
        {
            DeploymentExecutionId = Guid.NewGuid(),
            ApprovalId = Guid.NewGuid(),
            ModuleName = "TestModule",
            ModuleVersion = "1.0.0",
            TargetEnvironment = "Production",
            Status = ApprovalStatus.Pending,
            RequesterEmail = "test@example.com",
            RequestedAt = DateTime.UtcNow,
            TimeoutAt = DateTime.UtcNow.AddHours(1)
        };

        var auditLog = new AuditLog
        {
            EventType = "Approval",
            EventCategory = "Test",
            ResourceType = "Approval",
            ResourceId = approval.ApprovalId.ToString(),
            Action = "Create",
            Timestamp = DateTime.UtcNow,
            Metadata = "{}"
        };

        // Act - Begin transaction, make changes, commit
        await _unitOfWork.BeginTransactionAsync();

        await _unitOfWork.Approvals.CreateAsync(approval);
        await _unitOfWork.AuditLogs.CreateAsync(auditLog);

        await _unitOfWork.CommitTransactionAsync();

        // Assert - Both should be saved atomically
        var retrievedApproval = await _unitOfWork.Approvals.GetByIdAsync(approval.DeploymentExecutionId);
        var retrievedAuditLog = await _unitOfWork.AuditLogs.GetByIdAsync(auditLog.EventId);

        retrievedApproval.Should().NotBeNull();
        retrievedAuditLog.Should().NotBeNull();
    }

    [Fact]
    public async Task Transaction_OnException_ShouldRollbackAllChanges()
    {
        // Arrange
        var approval = new ApprovalRequestEntity
        {
            DeploymentExecutionId = Guid.NewGuid(),
            ApprovalId = Guid.NewGuid(),
            ModuleName = "TestModule",
            ModuleVersion = "1.0.0",
            TargetEnvironment = "Production",
            Status = ApprovalStatus.Pending,
            RequesterEmail = "test@example.com",
            RequestedAt = DateTime.UtcNow,
            TimeoutAt = DateTime.UtcNow.AddHours(1)
        };

        var auditLog = new AuditLog
        {
            EventType = "Approval",
            EventCategory = "Test",
            ResourceType = "Approval",
            ResourceId = approval.ApprovalId.ToString(),
            Action = "Create",
            Timestamp = DateTime.UtcNow,
            Metadata = "{}"
        };

        // Act - Begin transaction, make changes, simulate failure, rollback
        await _unitOfWork.BeginTransactionAsync();

        await _unitOfWork.Approvals.CreateAsync(approval);
        await _unitOfWork.AuditLogs.CreateAsync(auditLog);

        // Simulate business logic failure
        await _unitOfWork.RollbackTransactionAsync();

        // Assert - Neither should be saved
        var retrievedApproval = await _unitOfWork.Approvals.GetByIdAsync(approval.DeploymentExecutionId);
        var retrievedAuditLog = await _unitOfWork.AuditLogs.GetByIdAsync(auditLog.EventId);

        retrievedApproval.Should().BeNull();
        retrievedAuditLog.Should().BeNull();
    }

    [Fact]
    public async Task CommitTransactionAsync_OnDatabaseError_ShouldThrowAndRollback()
    {
        // Arrange
        await _unitOfWork.BeginTransactionAsync();

        // Create an invalid entity that will fail on save
        var invalidAuditLog = new AuditLog
        {
            EventType = new string('X', 300), // Exceeds max length of 100
            EventCategory = "Test",
            Action = "Create",
            ResourceType = "Test",
            ResourceId = Guid.NewGuid().ToString()
        };

        await _unitOfWork.AuditLogs.CreateAsync(invalidAuditLog);

        // Act & Assert
        await Assert.ThrowsAnyAsync<Exception>(
            async () => await _unitOfWork.CommitTransactionAsync());

        _unitOfWork.CurrentTransaction.Should().BeNull();
    }

    [Fact]
    public async Task Dispose_ShouldRollbackActiveTransaction()
    {
        // Arrange
        await _unitOfWork.BeginTransactionAsync();

        var auditLog = new AuditLog
        {
            EventType = "TestEvent",
            EventCategory = "Test",
            Action = "Create",
            ResourceType = "Test",
            ResourceId = Guid.NewGuid().ToString()
        };

        await _unitOfWork.AuditLogs.CreateAsync(auditLog);

        // Act
        _unitOfWork.Dispose();

        // Create new context to verify rollback
        var newContext = new AuditLogDbContext(new DbContextOptionsBuilder<AuditLogDbContext>()
            .UseSqlite(_connection)
            .Options);

        var newUnitOfWork = new UnitOfWork(newContext, NullLogger<UnitOfWork>.Instance, NullLoggerFactory.Instance);

        // Assert
        var retrieved = await newUnitOfWork.AuditLogs.GetByIdAsync(auditLog.EventId);
        retrieved.Should().BeNull(); // Should not exist after dispose without commit

        newUnitOfWork.Dispose();
        newContext.Dispose();
    }

    [Fact]
    public async Task MultipleOperations_WithinTransaction_ShouldShareSameTransactionScope()
    {
        // Arrange
        await _unitOfWork.BeginTransactionAsync();
        var transactionId = _unitOfWork.CurrentTransaction!.TransactionId;

        // Act - Perform multiple operations
        var approval1 = new ApprovalRequestEntity
        {
            DeploymentExecutionId = Guid.NewGuid(),
            ApprovalId = Guid.NewGuid(),
            ModuleName = "Module1",
            ModuleVersion = "1.0.0",
            TargetEnvironment = "Production",
            Status = ApprovalStatus.Pending,
            RequesterEmail = "test@example.com",
            RequestedAt = DateTime.UtcNow,
            TimeoutAt = DateTime.UtcNow.AddHours(1)
        };

        var approval2 = new ApprovalRequestEntity
        {
            DeploymentExecutionId = Guid.NewGuid(),
            ApprovalId = Guid.NewGuid(),
            ModuleName = "Module2",
            ModuleVersion = "1.0.0",
            TargetEnvironment = "Production",
            Status = ApprovalStatus.Pending,
            RequesterEmail = "test@example.com",
            RequestedAt = DateTime.UtcNow,
            TimeoutAt = DateTime.UtcNow.AddHours(1)
        };

        await _unitOfWork.Approvals.CreateAsync(approval1);
        await _unitOfWork.Approvals.CreateAsync(approval2);

        // Assert - Transaction ID should remain the same
        _unitOfWork.CurrentTransaction!.TransactionId.Should().Be(transactionId);

        await _unitOfWork.CommitTransactionAsync();
    }

    public void Dispose()
    {
        _unitOfWork?.Dispose();
        _dbContext?.Dispose();
        _connection?.Dispose();
    }
}
