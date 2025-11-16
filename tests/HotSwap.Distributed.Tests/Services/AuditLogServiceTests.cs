using FluentAssertions;
using HotSwap.Distributed.Infrastructure.Data;
using HotSwap.Distributed.Infrastructure.Data.Entities;
using HotSwap.Distributed.Infrastructure.Interfaces;
using HotSwap.Distributed.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace HotSwap.Distributed.Tests.Services;

/// <summary>
/// Unit tests for AuditLogService following TDD principles.
/// Tests use in-memory database for isolation and speed.
/// </summary>
public class AuditLogServiceTests : IDisposable
{
    private readonly AuditLogDbContext _dbContext;
    private readonly IAuditLogService _auditLogService;
    private readonly Mock<ILogger<AuditLogService>> _mockLogger;

    public AuditLogServiceTests()
    {
        // Arrange: Set up in-memory database for each test
        var options = new DbContextOptionsBuilder<AuditLogDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new AuditLogDbContext(options);
        _mockLogger = new Mock<ILogger<AuditLogService>>();
        _auditLogService = new AuditLogService(_dbContext, _mockLogger.Object);
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    #region LogDeploymentEventAsync Tests

    [Fact]
    public async Task LogDeploymentEventAsync_WithValidData_CreatesAuditLogAndDeploymentEvent()
    {
        // Arrange
        var auditLog = new AuditLog
        {
            EventType = "DeploymentStarted",
            EventCategory = "Deployment",
            Severity = "Info",
            Action = "Deploy",
            Result = "Success",
            Message = "Deployment started",
            TraceId = "trace-123"
        };

        var deploymentEvent = new DeploymentAuditEvent
        {
            DeploymentExecutionId = Guid.NewGuid(),
            ModuleName = "test-module",
            ModuleVersion = "1.0.0",
            TargetEnvironment = "Development",
            DeploymentStrategy = "Direct",
            PipelineStage = "Deploy",
            StageStatus = "Running"
        };

        // Act
        var auditLogId = await _auditLogService.LogDeploymentEventAsync(auditLog, deploymentEvent);

        // Assert
        auditLogId.Should().BeGreaterThan(0);

        var savedAuditLog = await _dbContext.AuditLogs
            .Include(a => a.DeploymentEvent)
            .FirstOrDefaultAsync(a => a.Id == auditLogId);

        savedAuditLog.Should().NotBeNull();
        savedAuditLog!.EventType.Should().Be("DeploymentStarted");
        savedAuditLog.EventCategory.Should().Be("Deployment");
        savedAuditLog.TraceId.Should().Be("trace-123");

        savedAuditLog.DeploymentEvent.Should().NotBeNull();
        savedAuditLog.DeploymentEvent!.ModuleName.Should().Be("test-module");
        savedAuditLog.DeploymentEvent.ModuleVersion.Should().Be("1.0.0");
        savedAuditLog.DeploymentEvent.TargetEnvironment.Should().Be("Development");
    }

    [Fact]
    public async Task LogDeploymentEventAsync_WithNullAuditLog_ThrowsArgumentNullException()
    {
        // Arrange
        var deploymentEvent = new DeploymentAuditEvent
        {
            DeploymentExecutionId = Guid.NewGuid(),
            ModuleName = "test-module",
            ModuleVersion = "1.0.0",
            TargetEnvironment = "Development"
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _auditLogService.LogDeploymentEventAsync(null!, deploymentEvent));
    }

    [Fact]
    public async Task LogDeploymentEventAsync_WithNullDeploymentEvent_ThrowsArgumentNullException()
    {
        // Arrange
        var auditLog = new AuditLog
        {
            EventType = "DeploymentStarted",
            EventCategory = "Deployment",
            Action = "Deploy",
            Result = "Success"
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _auditLogService.LogDeploymentEventAsync(auditLog, null!));
    }

    #endregion

    #region LogApprovalEventAsync Tests

    [Fact]
    public async Task LogApprovalEventAsync_WithValidData_CreatesAuditLogAndApprovalEvent()
    {
        // Arrange
        var auditLog = new AuditLog
        {
            EventType = "ApprovalRequested",
            EventCategory = "Approval",
            Severity = "Info",
            Action = "Request",
            Result = "Pending",
            Message = "Approval requested"
        };

        var approvalEvent = new ApprovalAuditEvent
        {
            ApprovalId = Guid.NewGuid(),
            DeploymentExecutionId = Guid.NewGuid(),
            ModuleName = "test-module",
            ModuleVersion = "1.0.0",
            TargetEnvironment = "Production",
            RequesterEmail = "requester@example.com",
            ApproverEmails = new[] { "approver1@example.com", "approver2@example.com" },
            ApprovalStatus = "Pending",
            TimeoutAt = DateTime.UtcNow.AddHours(24)
        };

        // Act
        var auditLogId = await _auditLogService.LogApprovalEventAsync(auditLog, approvalEvent);

        // Assert
        auditLogId.Should().BeGreaterThan(0);

        var savedAuditLog = await _dbContext.AuditLogs
            .Include(a => a.ApprovalEvent)
            .FirstOrDefaultAsync(a => a.Id == auditLogId);

        savedAuditLog.Should().NotBeNull();
        savedAuditLog!.EventCategory.Should().Be("Approval");

        savedAuditLog.ApprovalEvent.Should().NotBeNull();
        savedAuditLog.ApprovalEvent!.ApprovalStatus.Should().Be("Pending");
        savedAuditLog.ApprovalEvent.RequesterEmail.Should().Be("requester@example.com");
        savedAuditLog.ApprovalEvent.ApproverEmails.Should().HaveCount(2);
    }

    #endregion

    #region LogAuthenticationEventAsync Tests

    [Fact]
    public async Task LogAuthenticationEventAsync_WithSuccessfulLogin_CreatesAuditLogAndAuthEvent()
    {
        // Arrange
        var auditLog = new AuditLog
        {
            EventType = "LoginSuccess",
            EventCategory = "Authentication",
            Severity = "Info",
            Action = "Login",
            Result = "Success",
            Username = "testuser",
            UserEmail = "testuser@example.com",
            SourceIp = "192.168.1.1"
        };

        var authEvent = new AuthenticationAuditEvent
        {
            UserId = Guid.NewGuid(),
            Username = "testuser",
            AuthenticationMethod = "JWT",
            AuthenticationResult = "Success",
            TokenIssued = true,
            TokenExpiresAt = DateTime.UtcNow.AddHours(1),
            SourceIp = "192.168.1.1",
            IsSuspicious = false
        };

        // Act
        var auditLogId = await _auditLogService.LogAuthenticationEventAsync(auditLog, authEvent);

        // Assert
        auditLogId.Should().BeGreaterThan(0);

        var savedAuditLog = await _dbContext.AuditLogs
            .Include(a => a.AuthenticationEvent)
            .FirstOrDefaultAsync(a => a.Id == auditLogId);

        savedAuditLog.Should().NotBeNull();
        savedAuditLog!.EventCategory.Should().Be("Authentication");
        savedAuditLog.AuthenticationEvent.Should().NotBeNull();
        savedAuditLog.AuthenticationEvent!.AuthenticationResult.Should().Be("Success");
        savedAuditLog.AuthenticationEvent.TokenIssued.Should().BeTrue();
    }

    [Fact]
    public async Task LogAuthenticationEventAsync_WithFailedLogin_MarksSuspiciousAfterMultipleFailures()
    {
        // Arrange
        var auditLog = new AuditLog
        {
            EventType = "LoginFailure",
            EventCategory = "Authentication",
            Severity = "Warning",
            Action = "Login",
            Result = "Failure",
            Username = "testuser",
            SourceIp = "192.168.1.100"
        };

        var authEvent = new AuthenticationAuditEvent
        {
            Username = "testuser",
            AuthenticationMethod = "JWT",
            AuthenticationResult = "Failure",
            FailureReason = "Invalid password",
            TokenIssued = false,
            SourceIp = "192.168.1.100",
            IsSuspicious = true // Marked suspicious by caller after detecting pattern
        };

        // Act
        var auditLogId = await _auditLogService.LogAuthenticationEventAsync(auditLog, authEvent);

        // Assert
        var savedAuthEvent = await _dbContext.AuthenticationAuditEvents
            .FirstOrDefaultAsync(a => a.AuditLogId == auditLogId);

        savedAuthEvent.Should().NotBeNull();
        savedAuthEvent!.IsSuspicious.Should().BeTrue();
        savedAuthEvent.FailureReason.Should().Be("Invalid password");
    }

    #endregion

    #region GetAuditLogsByCategoryAsync Tests

    [Fact]
    public async Task GetAuditLogsByCategoryAsync_WithDeploymentCategory_ReturnsOnlyDeploymentLogs()
    {
        // Arrange
        await SeedTestDataAsync();

        // Act
        var deploymentLogs = await _auditLogService.GetAuditLogsByCategoryAsync("Deployment");

        // Assert
        deploymentLogs.Should().HaveCount(2);
        deploymentLogs.Should().OnlyContain(log => log.EventCategory == "Deployment");
    }

    [Fact]
    public async Task GetAuditLogsByCategoryAsync_WithPagination_ReturnsCorrectPage()
    {
        // Arrange
        await SeedTestDataAsync();

        // Act - Get page 1 with page size 1
        var page1 = await _auditLogService.GetAuditLogsByCategoryAsync("Deployment", pageNumber: 1, pageSize: 1);
        var page2 = await _auditLogService.GetAuditLogsByCategoryAsync("Deployment", pageNumber: 2, pageSize: 1);

        // Assert
        page1.Should().HaveCount(1);
        page2.Should().HaveCount(1);
        page1[0].Id.Should().NotBe(page2[0].Id);
    }

    #endregion

    #region GetAuditLogsByTraceIdAsync Tests

    [Fact]
    public async Task GetAuditLogsByTraceIdAsync_WithValidTraceId_ReturnsAllRelatedLogs()
    {
        // Arrange
        var traceId = "trace-correlation-123";
        await SeedTestDataWithTraceId(traceId);

        // Act
        var logs = await _auditLogService.GetAuditLogsByTraceIdAsync(traceId);

        // Assert
        logs.Should().HaveCount(3);
        logs.Should().OnlyContain(log => log.TraceId == traceId);
    }

    [Fact]
    public async Task GetAuditLogsByTraceIdAsync_WithNonExistentTraceId_ReturnsEmptyList()
    {
        // Arrange
        await SeedTestDataAsync();

        // Act
        var logs = await _auditLogService.GetAuditLogsByTraceIdAsync("non-existent-trace");

        // Assert
        logs.Should().BeEmpty();
    }

    #endregion

    #region GetDeploymentEventsAsync Tests

    [Fact]
    public async Task GetDeploymentEventsAsync_WithValidExecutionId_ReturnsAllStages()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        await SeedDeploymentStagesAsync(executionId);

        // Act
        var events = await _auditLogService.GetDeploymentEventsAsync(executionId);

        // Assert
        events.Should().HaveCount(5); // Build, Test, Security, Deploy, Validation
        events.Should().Contain(e => e.PipelineStage == "Build");
        events.Should().Contain(e => e.PipelineStage == "Test");
        events.Should().Contain(e => e.PipelineStage == "Security");
    }

    #endregion

    #region GetAuthenticationEventsAsync Tests

    [Fact]
    public async Task GetAuthenticationEventsAsync_WithDateRange_ReturnsFilteredEvents()
    {
        // Arrange
        var username = "testuser";
        await SeedAuthenticationEventsAsync(username);

        var startDate = DateTime.UtcNow.AddDays(-7);
        var endDate = DateTime.UtcNow;

        // Act
        var events = await _auditLogService.GetAuthenticationEventsAsync(username, startDate, endDate);

        // Assert
        events.Should().NotBeEmpty();
        events.Should().OnlyContain(e => e.Username == username);
        events.Should().OnlyContain(e => e.CreatedAt >= startDate && e.CreatedAt <= endDate);
    }

    #endregion

    #region DeleteOldAuditLogsAsync Tests

    [Fact]
    public async Task DeleteOldAuditLogsAsync_WithRetentionPeriod_DeletesOldLogsOnly()
    {
        // Arrange
        await SeedOldAndNewLogsAsync();

        // Act - Delete logs older than 30 days
        var deletedCount = await _auditLogService.DeleteOldAuditLogsAsync(retentionDays: 30);

        // Assert
        deletedCount.Should().Be(2); // Only old logs should be deleted

        var remainingLogs = await _dbContext.AuditLogs.ToListAsync();
        remainingLogs.Should().HaveCount(3); // Recent logs should remain
        remainingLogs.Should().OnlyContain(log => log.CreatedAt >= DateTime.UtcNow.AddDays(-30));
    }

    #endregion

    #region Helper Methods

    private async Task SeedTestDataAsync()
    {
        var deploymentLog1 = new AuditLog
        {
            EventType = "DeploymentStarted",
            EventCategory = "Deployment",
            Action = "Deploy",
            Result = "Success",
            Timestamp = DateTime.UtcNow
        };

        var deploymentLog2 = new AuditLog
        {
            EventType = "DeploymentCompleted",
            EventCategory = "Deployment",
            Action = "Deploy",
            Result = "Success",
            Timestamp = DateTime.UtcNow.AddMinutes(5)
        };

        var approvalLog = new AuditLog
        {
            EventType = "ApprovalRequested",
            EventCategory = "Approval",
            Action = "Request",
            Result = "Pending",
            Timestamp = DateTime.UtcNow.AddMinutes(2)
        };

        _dbContext.AuditLogs.AddRange(deploymentLog1, deploymentLog2, approvalLog);
        await _dbContext.SaveChangesAsync();
    }

    private async Task SeedTestDataWithTraceId(string traceId)
    {
        var logs = new[]
        {
            new AuditLog
            {
                EventType = "DeploymentStarted",
                EventCategory = "Deployment",
                Action = "Deploy",
                Result = "Success",
                TraceId = traceId
            },
            new AuditLog
            {
                EventType = "ApprovalRequested",
                EventCategory = "Approval",
                Action = "Request",
                Result = "Pending",
                TraceId = traceId
            },
            new AuditLog
            {
                EventType = "DeploymentCompleted",
                EventCategory = "Deployment",
                Action = "Deploy",
                Result = "Success",
                TraceId = traceId
            }
        };

        _dbContext.AuditLogs.AddRange(logs);
        await _dbContext.SaveChangesAsync();
    }

    private async Task SeedDeploymentStagesAsync(Guid executionId)
    {
        var stages = new[] { "Build", "Test", "Security", "Deploy", "Validation" };

        foreach (var stage in stages)
        {
            var auditLog = new AuditLog
            {
                EventType = $"{stage}Stage",
                EventCategory = "Deployment",
                Action = "Execute",
                Result = "Success"
            };

            var deploymentEvent = new DeploymentAuditEvent
            {
                DeploymentExecutionId = executionId,
                ModuleName = "test-module",
                ModuleVersion = "1.0.0",
                TargetEnvironment = "Development",
                PipelineStage = stage,
                StageStatus = "Succeeded"
            };

            _dbContext.AuditLogs.Add(auditLog);
            await _dbContext.SaveChangesAsync();

            deploymentEvent.AuditLogId = auditLog.Id;
            _dbContext.DeploymentAuditEvents.Add(deploymentEvent);
        }

        await _dbContext.SaveChangesAsync();
    }

    private async Task SeedAuthenticationEventsAsync(string username)
    {
        for (int i = 0; i < 3; i++)
        {
            var auditLog = new AuditLog
            {
                EventType = "LoginAttempt",
                EventCategory = "Authentication",
                Action = "Login",
                Result = i < 2 ? "Success" : "Failure",
                Username = username,
                CreatedAt = DateTime.UtcNow.AddDays(-i)
            };

            var authEvent = new AuthenticationAuditEvent
            {
                Username = username,
                AuthenticationMethod = "JWT",
                AuthenticationResult = i < 2 ? "Success" : "Failure",
                TokenIssued = i < 2,
                CreatedAt = DateTime.UtcNow.AddDays(-i)
            };

            _dbContext.AuditLogs.Add(auditLog);
            await _dbContext.SaveChangesAsync();

            authEvent.AuditLogId = auditLog.Id;
            _dbContext.AuthenticationAuditEvents.Add(authEvent);
        }

        await _dbContext.SaveChangesAsync();
    }

    private async Task SeedOldAndNewLogsAsync()
    {
        // Create 2 old logs (>30 days old)
        var oldLogs = new[]
        {
            new AuditLog
            {
                EventType = "OldEvent1",
                EventCategory = "Deployment",
                Action = "Deploy",
                Result = "Success",
                CreatedAt = DateTime.UtcNow.AddDays(-60),
                Timestamp = DateTime.UtcNow.AddDays(-60)
            },
            new AuditLog
            {
                EventType = "OldEvent2",
                EventCategory = "Deployment",
                Action = "Deploy",
                Result = "Success",
                CreatedAt = DateTime.UtcNow.AddDays(-90),
                Timestamp = DateTime.UtcNow.AddDays(-90)
            }
        };

        // Create 3 recent logs (<30 days old)
        var newLogs = new[]
        {
            new AuditLog
            {
                EventType = "NewEvent1",
                EventCategory = "Deployment",
                Action = "Deploy",
                Result = "Success",
                CreatedAt = DateTime.UtcNow.AddDays(-10),
                Timestamp = DateTime.UtcNow.AddDays(-10)
            },
            new AuditLog
            {
                EventType = "NewEvent2",
                EventCategory = "Deployment",
                Action = "Deploy",
                Result = "Success",
                CreatedAt = DateTime.UtcNow.AddDays(-5),
                Timestamp = DateTime.UtcNow.AddDays(-5)
            },
            new AuditLog
            {
                EventType = "NewEvent3",
                EventCategory = "Deployment",
                Action = "Deploy",
                Result = "Success",
                CreatedAt = DateTime.UtcNow,
                Timestamp = DateTime.UtcNow
            }
        };

        _dbContext.AuditLogs.AddRange(oldLogs);
        _dbContext.AuditLogs.AddRange(newLogs);
        await _dbContext.SaveChangesAsync();
    }

    #endregion
}
