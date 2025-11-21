using FluentAssertions;
using HotSwap.Distributed.Api.Services;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace HotSwap.Distributed.Tests.Api.Services;

public class SecretRotationBackgroundServiceTests
{
    private readonly Mock<ISecretService> _mockSecretService;
    private readonly Mock<INotificationService> _mockNotificationService;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly SecretRotationBackgroundService _service;

    public SecretRotationBackgroundServiceTests()
    {
        _mockSecretService = new Mock<ISecretService>();
        _mockNotificationService = new Mock<INotificationService>();
        _mockConfiguration = new Mock<IConfiguration>();

        _service = new SecretRotationBackgroundService(
            _mockSecretService.Object,
            _mockNotificationService.Object,
            NullLogger<SecretRotationBackgroundService>.Instance,
            _mockConfiguration.Object);
    }

    private SecretMetadata CreateSecretMetadata(
        string secretId,
        DateTime? nextRotationAt = null,
        bool enableAutomaticRotation = true,
        int rotationIntervalDays = 30,
        int daysUntilExpiration = 10)
    {
        return new SecretMetadata
        {
            SecretId = secretId,
            CurrentVersion = 1,
            CreatedAt = DateTime.UtcNow.AddDays(-20),
            NextRotationAt = nextRotationAt ?? DateTime.UtcNow.AddDays(-1),
            RotationPolicy = new RotationPolicy
            {
                EnableAutomaticRotation = enableAutomaticRotation,
                RotationIntervalDays = rotationIntervalDays,
                NotificationThresholdDays = 7,
                RotationWindowHours = 24,
                NotificationRecipients = new List<string> { "admin@example.com" }
            },
            Tags = new Dictionary<string, string> { { "Environment", "Production" } }
        };
    }

    [Fact]
    public async Task StartAsync_StartsService()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        _mockSecretService.Setup(x => x.ListSecretsAsync(It.IsAny<Dictionary<string, string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SecretMetadata>());

        // Act
        await _service.StartAsync(cts.Token);
        await Task.Delay(100);

        // Assert
        cts.Cancel();
        await _service.StopAsync(CancellationToken.None);

        _mockSecretService.Verify(x => x.ListSecretsAsync(It.IsAny<Dictionary<string, string>?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task StopAsync_StopsService()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        _mockSecretService.Setup(x => x.ListSecretsAsync(It.IsAny<Dictionary<string, string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SecretMetadata>());

        var callCountBeforeStop = 0;

        // Act
        await _service.StartAsync(cts.Token);
        await Task.Delay(50);
        await _service.StopAsync(CancellationToken.None);

        callCountBeforeStop = _mockSecretService.Invocations.Count;
        await Task.Delay(TimeSpan.FromHours(2)); // Wait longer than check interval (1 hour)

        // Assert
        var callCountAfterStop = _mockSecretService.Invocations.Count;
        callCountAfterStop.Should().Be(callCountBeforeStop);
    }

    [Fact]
    public async Task ExecuteAsync_RotatesExpiredSecrets()
    {
        // Arrange
        var expiredSecret = CreateSecretMetadata("jwt-signing-key", DateTime.UtcNow.AddDays(-1));
        var rotationResult = SecretRotationResult.CreateSuccess("jwt-signing-key", 2, 1, DateTime.UtcNow.AddHours(24));

        _mockSecretService.Setup(x => x.ListSecretsAsync(It.IsAny<Dictionary<string, string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SecretMetadata> { expiredSecret });

        _mockSecretService.Setup(x => x.RotateSecretAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(rotationResult);

        _mockNotificationService.Setup(x => x.SendSecretRotationNotificationAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var cts = new CancellationTokenSource();

        // Act
        await _service.StartAsync(cts.Token);
        await Task.Delay(TimeSpan.FromHours(1).Add(TimeSpan.FromSeconds(1)));

        // Assert
        cts.Cancel();
        await _service.StopAsync(CancellationToken.None);

        _mockSecretService.Verify(x => x.RotateSecretAsync("jwt-signing-key", null, It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_SendsNotificationOnRotation()
    {
        // Arrange
        var expiredSecret = CreateSecretMetadata("database-password", DateTime.UtcNow.AddDays(-1));
        var rotationResult = SecretRotationResult.CreateSuccess("database-password", 2, 1, DateTime.UtcNow.AddHours(24));

        _mockSecretService.Setup(x => x.ListSecretsAsync(It.IsAny<Dictionary<string, string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SecretMetadata> { expiredSecret });

        _mockSecretService.Setup(x => x.RotateSecretAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(rotationResult);

        _mockNotificationService.Setup(x => x.SendSecretRotationNotificationAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var cts = new CancellationTokenSource();

        // Act
        await _service.StartAsync(cts.Token);
        await Task.Delay(TimeSpan.FromHours(1).Add(TimeSpan.FromSeconds(1)));

        // Assert
        cts.Cancel();
        await _service.StopAsync(CancellationToken.None);

        _mockNotificationService.Verify(x => x.SendSecretRotationNotificationAsync(
            It.Is<IEnumerable<string>>(recipients => recipients.Contains("admin@example.com")),
            "database-password",
            It.IsAny<int>(),
            It.IsAny<int>(),
            It.IsAny<DateTime>(),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_SkipsSecretsWithoutAutomaticRotation()
    {
        // Arrange
        var manualSecret = CreateSecretMetadata("manual-secret", DateTime.UtcNow.AddDays(-1), enableAutomaticRotation: false);

        _mockSecretService.Setup(x => x.ListSecretsAsync(It.IsAny<Dictionary<string, string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SecretMetadata> { manualSecret });

        var cts = new CancellationTokenSource();

        // Act
        await _service.StartAsync(cts.Token);
        await Task.Delay(TimeSpan.FromHours(1).Add(TimeSpan.FromSeconds(1)));

        // Assert
        cts.Cancel();
        await _service.StopAsync(CancellationToken.None);

        _mockSecretService.Verify(x => x.RotateSecretAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_SkipsSecretsNotDueForRotation()
    {
        // Arrange
        var futureSecret = CreateSecretMetadata("future-secret", DateTime.UtcNow.AddDays(7));

        _mockSecretService.Setup(x => x.ListSecretsAsync(It.IsAny<Dictionary<string, string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SecretMetadata> { futureSecret });

        var cts = new CancellationTokenSource();

        // Act
        await _service.StartAsync(cts.Token);
        await Task.Delay(TimeSpan.FromHours(1).Add(TimeSpan.FromSeconds(1)));

        // Assert
        cts.Cancel();
        await _service.StopAsync(CancellationToken.None);

        _mockSecretService.Verify(x => x.RotateSecretAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_SendsExpirationWarningNotifications()
    {
        // Arrange
        var soonToExpireSecret = CreateSecretMetadata(
            "expiring-secret",
            DateTime.UtcNow.AddDays(5),
            daysUntilExpiration: 5);

        _mockSecretService.Setup(x => x.ListSecretsAsync(It.IsAny<Dictionary<string, string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SecretMetadata> { soonToExpireSecret });

        _mockSecretService.Setup(x => x.IsSecretExpiringAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _mockNotificationService.Setup(x => x.SendSecretExpirationWarningAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var cts = new CancellationTokenSource();

        // Act
        await _service.StartAsync(cts.Token);
        await Task.Delay(TimeSpan.FromHours(1).Add(TimeSpan.FromSeconds(1)));

        // Assert
        cts.Cancel();
        await _service.StopAsync(CancellationToken.None);

        _mockNotificationService.Verify(x => x.SendSecretExpirationWarningAsync(
            It.IsAny<IEnumerable<string>>(),
            "expiring-secret",
            It.IsAny<int>(),
            It.IsAny<DateTime>(),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_ProcessesMultipleSecrets()
    {
        // Arrange
        var secrets = new List<SecretMetadata>
        {
            CreateSecretMetadata("secret-1", DateTime.UtcNow.AddDays(-1)),
            CreateSecretMetadata("secret-2", DateTime.UtcNow.AddDays(-2)),
            CreateSecretMetadata("secret-3", DateTime.UtcNow.AddDays(10)) // Not due
        };

        _mockSecretService.Setup(x => x.ListSecretsAsync(It.IsAny<Dictionary<string, string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(secrets);

        _mockSecretService.Setup(x => x.RotateSecretAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string id, string? val, CancellationToken ct) =>
                SecretRotationResult.CreateSuccess(id, 2, 1, DateTime.UtcNow.AddHours(24)));

        _mockNotificationService.Setup(x => x.SendSecretRotationNotificationAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var cts = new CancellationTokenSource();

        // Act
        await _service.StartAsync(cts.Token);
        await Task.Delay(TimeSpan.FromHours(1).Add(TimeSpan.FromSeconds(1)));

        // Assert
        cts.Cancel();
        await _service.StopAsync(CancellationToken.None);

        _mockSecretService.Verify(x => x.RotateSecretAsync("secret-1", null, It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        _mockSecretService.Verify(x => x.RotateSecretAsync("secret-2", null, It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        _mockSecretService.Verify(x => x.RotateSecretAsync("secret-3", null, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WithRotationFailure_ContinuesProcessingOtherSecrets()
    {
        // Arrange
        var secrets = new List<SecretMetadata>
        {
            CreateSecretMetadata("failing-secret", DateTime.UtcNow.AddDays(-1)),
            CreateSecretMetadata("working-secret", DateTime.UtcNow.AddDays(-1))
        };

        _mockSecretService.Setup(x => x.ListSecretsAsync(It.IsAny<Dictionary<string, string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(secrets);

        _mockSecretService.Setup(x => x.RotateSecretAsync("failing-secret", null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Rotation failed"));

        _mockSecretService.Setup(x => x.RotateSecretAsync("working-secret", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SecretRotationResult.CreateSuccess("working-secret", 2, 1, DateTime.UtcNow.AddHours(24)));

        _mockNotificationService.Setup(x => x.SendSecretRotationNotificationAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var cts = new CancellationTokenSource();

        // Act
        await _service.StartAsync(cts.Token);
        await Task.Delay(TimeSpan.FromHours(1).Add(TimeSpan.FromSeconds(1)));

        // Assert
        cts.Cancel();
        await _service.StopAsync(CancellationToken.None);

        _mockSecretService.Verify(x => x.RotateSecretAsync("working-secret", null, It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_WithException_ContinuesRunning()
    {
        // Arrange
        var callCount = 0;
        _mockSecretService.Setup(x => x.ListSecretsAsync(It.IsAny<Dictionary<string, string>?>(), It.IsAny<CancellationToken>()))
            .Callback(() => callCount++)
            .ThrowsAsync(new InvalidOperationException("Service unavailable"));

        var cts = new CancellationTokenSource();

        // Act
        await _service.StartAsync(cts.Token);
        await Task.Delay(TimeSpan.FromHours(2).Add(TimeSpan.FromSeconds(1)));
        cts.Cancel();
        await _service.StopAsync(CancellationToken.None);

        // Assert - service should continue trying despite failures
        callCount.Should().BeGreaterThan(1);
    }

    [Fact]
    public async Task ExecuteAsync_UsesCorrectCheckInterval()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        var callTimes = new List<DateTime>();

        _mockSecretService.Setup(x => x.ListSecretsAsync(It.IsAny<Dictionary<string, string>?>(), It.IsAny<CancellationToken>()))
            .Callback(() => callTimes.Add(DateTime.UtcNow))
            .ReturnsAsync(new List<SecretMetadata>());

        // Act
        await _service.StartAsync(cts.Token);
        await Task.Delay(TimeSpan.FromHours(2).Add(TimeSpan.FromSeconds(1))); // Wait for 2+ check intervals
        cts.Cancel();
        await _service.StopAsync(CancellationToken.None);

        // Assert - should execute approximately every 1 hour
        callTimes.Should().HaveCountGreaterOrEqualTo(2);

        if (callTimes.Count >= 2)
        {
            var intervalBetweenCalls = callTimes[1] - callTimes[0];
            intervalBetweenCalls.Should().BeCloseTo(TimeSpan.FromHours(1), TimeSpan.FromMinutes(2));
        }
    }
}
