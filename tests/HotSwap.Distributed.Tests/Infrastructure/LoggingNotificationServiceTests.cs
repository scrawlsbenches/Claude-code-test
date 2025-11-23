using FluentAssertions;
using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Notifications;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace HotSwap.Distributed.Tests.Infrastructure;

/// <summary>
/// Unit tests for LoggingNotificationService.
/// Tests that notifications are logged correctly.
/// </summary>
public class LoggingNotificationServiceTests
{
    private readonly Mock<ILogger<LoggingNotificationService>> _mockLogger;
    private readonly LoggingNotificationService _service;

    public LoggingNotificationServiceTests()
    {
        _mockLogger = new Mock<ILogger<LoggingNotificationService>>();
        _service = new LoggingNotificationService(_mockLogger.Object);
    }

    private static ApprovalRequest CreateTestApprovalRequest() => new()
    {
        DeploymentExecutionId = Guid.NewGuid(),
        ModuleName = "TestModule",
        Version = new Version(1, 2, 3),
        TargetEnvironment = EnvironmentType.Production,
        RequesterEmail = "requester@example.com",
        ApproverEmails = new List<string> { "approver1@example.com", "approver2@example.com" },
        TimeoutAt = DateTime.UtcNow.AddHours(24)
    };

    [Fact]
    public async Task SendApprovalRequestNotificationAsync_LogsInformation()
    {
        // Arrange
        var approval = CreateTestApprovalRequest();

        // Act
        await _service.SendApprovalRequestNotificationAsync(approval);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "Should log approval request notification");
    }

    [Fact]
    public async Task SendApprovalRequestNotificationAsync_WithEmptyApprovers_CompletesSuccessfully()
    {
        // Arrange
        var approval = CreateTestApprovalRequest();
        approval.ApproverEmails = new List<string>();

        // Act
        Func<Task> act = async () => await _service.SendApprovalRequestNotificationAsync(approval);

        // Assert
        await act.Should().NotThrowAsync("should handle empty approver list gracefully");
    }

    [Fact]
    public async Task SendApprovalRequestNotificationAsync_WithCancellationToken_CompletesSuccessfully()
    {
        // Arrange
        var approval = CreateTestApprovalRequest();
        var cts = new CancellationTokenSource();

        // Act
        await _service.SendApprovalRequestNotificationAsync(approval, cts.Token);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SendApprovalGrantedNotificationAsync_LogsInformation()
    {
        // Arrange
        var approval = CreateTestApprovalRequest();
        approval.RespondedByEmail = "approver@example.com";
        approval.ResponseReason = "Looks good to deploy";

        // Act
        await _service.SendApprovalGrantedNotificationAsync(approval);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "Should log approval granted notification");
    }

    [Fact]
    public async Task SendApprovalGrantedNotificationAsync_WithNullResponderAndReason_UsesDefaults()
    {
        // Arrange
        var approval = CreateTestApprovalRequest();
        approval.RespondedByEmail = null;
        approval.ResponseReason = null;

        // Act
        Func<Task> act = async () => await _service.SendApprovalGrantedNotificationAsync(approval);

        // Assert
        await act.Should().NotThrowAsync("should handle null responder email and reason gracefully");
    }

    [Fact]
    public async Task SendApprovalGrantedNotificationAsync_WithCancellationToken_CompletesSuccessfully()
    {
        // Arrange
        var approval = CreateTestApprovalRequest();
        var cts = new CancellationTokenSource();

        // Act
        await _service.SendApprovalGrantedNotificationAsync(approval, cts.Token);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SendApprovalRejectedNotificationAsync_LogsWarning()
    {
        // Arrange
        var approval = CreateTestApprovalRequest();
        approval.RespondedByEmail = "approver@example.com";
        approval.ResponseReason = "Security concerns";

        // Act
        await _service.SendApprovalRejectedNotificationAsync(approval);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "Should log approval rejected notification as warning");
    }

    [Fact]
    public async Task SendApprovalRejectedNotificationAsync_WithNullResponderAndReason_UsesDefaults()
    {
        // Arrange
        var approval = CreateTestApprovalRequest();
        approval.RespondedByEmail = null;
        approval.ResponseReason = null;

        // Act
        Func<Task> act = async () => await _service.SendApprovalRejectedNotificationAsync(approval);

        // Assert
        await act.Should().NotThrowAsync("should handle null responder email and reason gracefully");
    }

    [Fact]
    public async Task SendApprovalRejectedNotificationAsync_WithCancellationToken_CompletesSuccessfully()
    {
        // Arrange
        var approval = CreateTestApprovalRequest();
        var cts = new CancellationTokenSource();

        // Act
        await _service.SendApprovalRejectedNotificationAsync(approval, cts.Token);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SendApprovalExpiredNotificationAsync_LogsWarning()
    {
        // Arrange
        var approval = CreateTestApprovalRequest();
        approval.TimeoutAt = DateTime.UtcNow.AddHours(-1); // Expired

        // Act
        await _service.SendApprovalExpiredNotificationAsync(approval);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "Should log approval expired notification as warning");
    }

    [Fact]
    public async Task SendApprovalExpiredNotificationAsync_WithCancellationToken_CompletesSuccessfully()
    {
        // Arrange
        var approval = CreateTestApprovalRequest();
        var cts = new CancellationTokenSource();

        // Act
        await _service.SendApprovalExpiredNotificationAsync(approval, cts.Token);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SendSecretRotationNotificationAsync_LogsWarning()
    {
        // Arrange
        var recipients = new[] { "admin1@example.com", "admin2@example.com" };
        var secretId = "jwt-signing-key";
        var previousVersion = 1;
        var newVersion = 2;
        var rotationWindowEndsAt = DateTime.UtcNow.AddHours(24);

        // Act
        await _service.SendSecretRotationNotificationAsync(
            recipients,
            secretId,
            previousVersion,
            newVersion,
            rotationWindowEndsAt);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "Should log secret rotation notification as warning");
    }

    [Fact]
    public async Task SendSecretRotationNotificationAsync_WithSingleRecipient_CompletesSuccessfully()
    {
        // Arrange
        var recipients = new[] { "admin@example.com" };
        var secretId = "api-key";

        // Act
        Func<Task> act = async () => await _service.SendSecretRotationNotificationAsync(
            recipients,
            secretId,
            1,
            2,
            DateTime.UtcNow.AddDays(1));

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SendSecretRotationNotificationAsync_WithCancellationToken_CompletesSuccessfully()
    {
        // Arrange
        var recipients = new[] { "admin@example.com" };
        var cts = new CancellationTokenSource();

        // Act
        await _service.SendSecretRotationNotificationAsync(
            recipients,
            "test-secret",
            1,
            2,
            DateTime.UtcNow.AddDays(1),
            cts.Token);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SendSecretExpirationWarningAsync_LogsWarning()
    {
        // Arrange
        var recipients = new[] { "admin1@example.com", "admin2@example.com" };
        var secretId = "database-password";
        var daysRemaining = 7;
        var expiresAt = DateTime.UtcNow.AddDays(7);

        // Act
        await _service.SendSecretExpirationWarningAsync(
            recipients,
            secretId,
            daysRemaining,
            expiresAt);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "Should log secret expiration warning as warning");
    }

    [Fact]
    public async Task SendSecretExpirationWarningAsync_WithOneDayRemaining_CompletesSuccessfully()
    {
        // Arrange
        var recipients = new[] { "admin@example.com" };
        var secretId = "expiring-secret";

        // Act
        Func<Task> act = async () => await _service.SendSecretExpirationWarningAsync(
            recipients,
            secretId,
            1,
            DateTime.UtcNow.AddDays(1));

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SendSecretExpirationWarningAsync_WithCancellationToken_CompletesSuccessfully()
    {
        // Arrange
        var recipients = new[] { "admin@example.com" };
        var cts = new CancellationTokenSource();

        // Act
        await _service.SendSecretExpirationWarningAsync(
            recipients,
            "test-secret",
            7,
            DateTime.UtcNow.AddDays(7),
            cts.Token);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void Constructor_WithValidLogger_CreatesInstance()
    {
        // Act
        var service = new LoggingNotificationService(_mockLogger.Object);

        // Assert
        service.Should().NotBeNull();
    }

    [Fact]
    public async Task AllNotificationMethods_CompleteSuccessfully_ReturningCompletedTask()
    {
        // Arrange
        var approval = CreateTestApprovalRequest();
        var recipients = new[] { "admin@example.com" };

        // Act & Assert - All methods should complete synchronously
        var task1 = _service.SendApprovalRequestNotificationAsync(approval);
        task1.IsCompletedSuccessfully.Should().BeTrue();

        var task2 = _service.SendApprovalGrantedNotificationAsync(approval);
        task2.IsCompletedSuccessfully.Should().BeTrue();

        var task3 = _service.SendApprovalRejectedNotificationAsync(approval);
        task3.IsCompletedSuccessfully.Should().BeTrue();

        var task4 = _service.SendApprovalExpiredNotificationAsync(approval);
        task4.IsCompletedSuccessfully.Should().BeTrue();

        var task5 = _service.SendSecretRotationNotificationAsync(
            recipients, "secret", 1, 2, DateTime.UtcNow.AddDays(1));
        task5.IsCompletedSuccessfully.Should().BeTrue();

        var task6 = _service.SendSecretExpirationWarningAsync(
            recipients, "secret", 7, DateTime.UtcNow.AddDays(7));
        task6.IsCompletedSuccessfully.Should().BeTrue();

        await Task.WhenAll(task1, task2, task3, task4, task5, task6);
    }
}
