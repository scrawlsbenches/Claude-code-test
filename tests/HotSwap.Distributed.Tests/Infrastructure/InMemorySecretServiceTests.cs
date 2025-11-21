using FluentAssertions;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.SecretManagement;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace HotSwap.Distributed.Tests.Infrastructure;

public class InMemorySecretServiceTests
{
    private readonly Mock<ILogger<InMemorySecretService>> _loggerMock;
    private readonly InMemorySecretService _secretService;

    public InMemorySecretServiceTests()
    {
        _loggerMock = new Mock<ILogger<InMemorySecretService>>();
        _secretService = new InMemorySecretService(_loggerMock.Object);
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Arrange & Act & Assert
        var act = () => new InMemorySecretService(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    [Fact]
    public void Constructor_ShouldLogWarningAboutProductionUse()
    {
        // Arrange - Use fresh logger mock to avoid counting constructor initialization
        var freshLoggerMock = new Mock<ILogger<InMemorySecretService>>();

        // Act
        var service = new InMemorySecretService(freshLoggerMock.Object);

        // Assert
        freshLoggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("NOT suitable for production")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task GetSecretAsync_WhenSecretDoesNotExist_ShouldReturnNull()
    {
        // Act
        var result = await _secretService.GetSecretAsync("non-existent-secret");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetSecretAsync_WhenSecretExists_ShouldReturnCurrentVersion()
    {
        // Arrange
        var secretId = "test-secret";
        var secretValue = "my-secret-value";
        await _secretService.SetSecretAsync(secretId, secretValue);

        // Act
        var result = await _secretService.GetSecretAsync(secretId);

        // Assert
        result.Should().NotBeNull();
        result!.SecretId.Should().Be(secretId);
        result.Value.Should().Be(secretValue);
        result.Version.Should().Be(1);
        result.IsActive.Should().BeTrue();
        result.IsDeleted.Should().BeFalse();
        result.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task GetSecretAsync_WithMultipleVersions_ShouldReturnLatestVersion()
    {
        // Arrange
        var secretId = "versioned-secret";
        await _secretService.SetSecretAsync(secretId, "version-1");
        await _secretService.SetSecretAsync(secretId, "version-2");
        await _secretService.SetSecretAsync(secretId, "version-3");

        // Act
        var result = await _secretService.GetSecretAsync(secretId);

        // Assert
        result.Should().NotBeNull();
        result!.Version.Should().Be(3);
        result.Value.Should().Be("version-3");
    }

    [Fact]
    public async Task GetSecretVersionAsync_WhenSecretDoesNotExist_ShouldReturnNull()
    {
        // Act
        var result = await _secretService.GetSecretVersionAsync("non-existent", 1);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetSecretVersionAsync_WhenVersionDoesNotExist_ShouldReturnNull()
    {
        // Arrange
        var secretId = "test-secret";
        await _secretService.SetSecretAsync(secretId, "value");

        // Act
        var result = await _secretService.GetSecretVersionAsync(secretId, 99);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetSecretVersionAsync_WhenVersionExists_ShouldReturnSpecificVersion()
    {
        // Arrange
        var secretId = "versioned-secret";
        await _secretService.SetSecretAsync(secretId, "version-1");
        await _secretService.SetSecretAsync(secretId, "version-2");
        await _secretService.SetSecretAsync(secretId, "version-3");

        // Act
        var result = await _secretService.GetSecretVersionAsync(secretId, 2);

        // Assert
        result.Should().NotBeNull();
        result!.Version.Should().Be(2);
        result.Value.Should().Be("version-2");
    }

    [Fact]
    public async Task GetSecretMetadataAsync_WhenSecretDoesNotExist_ShouldReturnNull()
    {
        // Act
        var result = await _secretService.GetSecretMetadataAsync("non-existent");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetSecretMetadataAsync_WhenSecretExists_ShouldReturnMetadata()
    {
        // Arrange
        var secretId = "test-secret";
        await _secretService.SetSecretAsync(secretId, "value");

        // Act
        var result = await _secretService.GetSecretMetadataAsync(secretId);

        // Assert
        result.Should().NotBeNull();
        result!.SecretId.Should().Be(secretId);
        result.CurrentVersion.Should().Be(1);
        result.PreviousVersion.Should().BeNull();
        result.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task SetSecretAsync_WithBasicValues_ShouldCreateSecret()
    {
        // Arrange
        var secretId = "jwt-signing-key";
        var secretValue = "my-super-secret-key-12345";

        // Act
        var result = await _secretService.SetSecretAsync(secretId, secretValue);

        // Assert
        result.Should().NotBeNull();
        result.SecretId.Should().Be(secretId);
        result.Value.Should().Be(secretValue);
        result.Version.Should().Be(1);
        result.IsActive.Should().BeTrue();
        result.IsDeleted.Should().BeFalse();
        result.ExpiresAt.Should().BeNull();

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Set secret {secretId}")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SetSecretAsync_WithRotationPolicy_ShouldSetExpirationAndNextRotation()
    {
        // Arrange
        var secretId = "rotatable-secret";
        var secretValue = "secret-value";
        var rotationPolicy = new RotationPolicy
        {
            MaxAgeDays = 30,
            RotationIntervalDays = 7,
            EnableAutomaticRotation = true
        };

        // Act
        var result = await _secretService.SetSecretAsync(secretId, secretValue, rotationPolicy);

        // Assert
        result.ExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddDays(30), TimeSpan.FromSeconds(5));

        var metadata = await _secretService.GetSecretMetadataAsync(secretId);
        metadata.Should().NotBeNull();
        metadata!.NextRotationAt.Should().BeCloseTo(DateTime.UtcNow.AddDays(7), TimeSpan.FromSeconds(5));
        metadata.RotationPolicy.Should().Be(rotationPolicy);
    }

    [Fact]
    public async Task SetSecretAsync_WithTags_ShouldStoreTagsInMetadata()
    {
        // Arrange
        var secretId = "tagged-secret";
        var tags = new Dictionary<string, string>
        {
            ["environment"] = "production",
            ["team"] = "platform",
            ["service"] = "api"
        };

        // Act
        await _secretService.SetSecretAsync(secretId, "value", tags: tags);

        // Assert
        var metadata = await _secretService.GetSecretMetadataAsync(secretId);
        metadata.Should().NotBeNull();
        metadata!.Tags.Should().BeEquivalentTo(tags);
    }

    [Fact]
    public async Task SetSecretAsync_MultipleVersions_ShouldIncrementVersionNumber()
    {
        // Arrange
        var secretId = "multi-version-secret";

        // Act
        var v1 = await _secretService.SetSecretAsync(secretId, "value-1");
        var v2 = await _secretService.SetSecretAsync(secretId, "value-2");
        var v3 = await _secretService.SetSecretAsync(secretId, "value-3");

        // Assert
        v1.Version.Should().Be(1);
        v2.Version.Should().Be(2);
        v3.Version.Should().Be(3);

        var metadata = await _secretService.GetSecretMetadataAsync(secretId);
        metadata!.CurrentVersion.Should().Be(3);
        metadata.PreviousVersion.Should().Be(2);
    }

    [Fact]
    public async Task RotateSecretAsync_WhenSecretDoesNotExist_ShouldReturnFailure()
    {
        // Act
        var result = await _secretService.RotateSecretAsync("non-existent");

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Secret not found");
    }

    [Fact]
    public async Task RotateSecretAsync_WithCustomValue_ShouldCreateNewVersionWithProvidedValue()
    {
        // Arrange
        var secretId = "rotatable-secret";
        await _secretService.SetSecretAsync(secretId, "original-value");
        var newValue = "new-rotated-value";

        // Act
        var result = await _secretService.RotateSecretAsync(secretId, newValue);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.SecretId.Should().Be(secretId);
        result.NewVersion.Should().Be(2);
        result.PreviousVersion.Should().Be(1);
        result.RotatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        result.RotationWindowEndsAt.Should().BeCloseTo(DateTime.UtcNow.AddHours(24), TimeSpan.FromSeconds(5));

        var newSecret = await _secretService.GetSecretAsync(secretId);
        newSecret!.Value.Should().Be(newValue);
        newSecret.Version.Should().Be(2);

        var oldVersion = await _secretService.GetSecretVersionAsync(secretId, 1);
        oldVersion!.Value.Should().Be("original-value");
    }

    [Fact]
    public async Task RotateSecretAsync_WithoutCustomValue_ShouldGenerateRandomSecret()
    {
        // Arrange
        var secretId = "auto-rotate-secret";
        await _secretService.SetSecretAsync(secretId, "original-value");

        // Act
        var result = await _secretService.RotateSecretAsync(secretId);

        // Assert
        result.Success.Should().BeTrue();
        result.NewVersion.Should().Be(2);

        var newSecret = await _secretService.GetSecretAsync(secretId);
        newSecret!.Value.Should().NotBe("original-value");
        newSecret.Value.Length.Should().Be(64); // Generated secrets are 64 chars
        newSecret.Value.Should().MatchRegex(@"^[A-Za-z0-9!@#$%^&*()\-_=+\[\]{}|;:,.<>?]+$");
    }

    [Fact]
    public async Task RotateSecretAsync_ShouldUpdateMetadataWithRotationInfo()
    {
        // Arrange
        var secretId = "metadata-update-secret";
        var rotationPolicy = new RotationPolicy
        {
            RotationWindowHours = 48,
            MaxAgeDays = 30
        };
        await _secretService.SetSecretAsync(secretId, "original", rotationPolicy);

        // Act
        var result = await _secretService.RotateSecretAsync(secretId, "rotated");

        // Assert
        var metadata = await _secretService.GetSecretMetadataAsync(secretId);
        metadata!.CurrentVersion.Should().Be(2);
        metadata.PreviousVersion.Should().Be(1);
        metadata.LastRotatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        metadata.IsInRotationWindow.Should().BeTrue();

        result.RotationWindowEndsAt.Should().BeCloseTo(DateTime.UtcNow.AddHours(48), TimeSpan.FromSeconds(5));

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Rotated secret")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task DeleteSecretAsync_WhenSecretDoesNotExist_ShouldReturnFalse()
    {
        // Act
        var result = await _secretService.DeleteSecretAsync("non-existent");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteSecretAsync_WhenSecretExists_ShouldDeleteAndReturnTrue()
    {
        // Arrange
        var secretId = "delete-me-secret";
        await _secretService.SetSecretAsync(secretId, "value");

        // Act
        var result = await _secretService.DeleteSecretAsync(secretId);

        // Assert
        result.Should().BeTrue();

        var secret = await _secretService.GetSecretAsync(secretId);
        secret.Should().BeNull();

        var metadata = await _secretService.GetSecretMetadataAsync(secretId);
        metadata.Should().BeNull();

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Deleted secret {secretId}")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ListSecretsAsync_WhenNoSecrets_ShouldReturnEmptyList()
    {
        // Act
        var result = await _secretService.ListSecretsAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ListSecretsAsync_WithMultipleSecrets_ShouldReturnAllSecrets()
    {
        // Arrange
        await _secretService.SetSecretAsync("secret-1", "value-1");
        await _secretService.SetSecretAsync("secret-2", "value-2");
        await _secretService.SetSecretAsync("secret-3", "value-3");

        // Act
        var result = await _secretService.ListSecretsAsync();

        // Assert
        result.Should().HaveCount(3);
        result.Select(m => m.SecretId).Should().Contain(new[] { "secret-1", "secret-2", "secret-3" });
    }

    [Fact]
    public async Task ListSecretsAsync_WithTagFilter_ShouldReturnOnlyMatchingSecrets()
    {
        // Arrange
        await _secretService.SetSecretAsync("prod-secret-1", "value", tags: new Dictionary<string, string>
        {
            ["environment"] = "production",
            ["team"] = "platform"
        });
        await _secretService.SetSecretAsync("prod-secret-2", "value", tags: new Dictionary<string, string>
        {
            ["environment"] = "production",
            ["team"] = "backend"
        });
        await _secretService.SetSecretAsync("dev-secret", "value", tags: new Dictionary<string, string>
        {
            ["environment"] = "development",
            ["team"] = "platform"
        });

        // Act - Filter by environment=production
        var productionSecrets = await _secretService.ListSecretsAsync(new Dictionary<string, string>
        {
            ["environment"] = "production"
        });

        // Act - Filter by environment=production AND team=platform
        var platformProductionSecrets = await _secretService.ListSecretsAsync(new Dictionary<string, string>
        {
            ["environment"] = "production",
            ["team"] = "platform"
        });

        // Assert
        productionSecrets.Should().HaveCount(2);
        productionSecrets.Select(m => m.SecretId).Should().Contain(new[] { "prod-secret-1", "prod-secret-2" });

        platformProductionSecrets.Should().HaveCount(1);
        platformProductionSecrets.First().SecretId.Should().Be("prod-secret-1");
    }

    [Fact]
    public async Task IsSecretExpiringAsync_WhenSecretDoesNotExist_ShouldReturnFalse()
    {
        // Act
        var result = await _secretService.IsSecretExpiringAsync("non-existent");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsSecretExpiringAsync_WhenSecretHasNoExpiration_ShouldReturnFalse()
    {
        // Arrange
        var secretId = "no-expiration-secret";
        await _secretService.SetSecretAsync(secretId, "value"); // No rotation policy = no expiration

        // Act
        var result = await _secretService.IsSecretExpiringAsync(secretId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsSecretExpiringAsync_WhenSecretExpiresWithinThreshold_ShouldReturnTrue()
    {
        // Arrange
        var secretId = "expiring-secret";
        var rotationPolicy = new RotationPolicy
        {
            MaxAgeDays = 5, // Expires in 5 days
            NotificationThresholdDays = 7 // Notify 7 days before expiration
        };
        await _secretService.SetSecretAsync(secretId, "value", rotationPolicy);

        // Act
        var result = await _secretService.IsSecretExpiringAsync(secretId);

        // Assert
        result.Should().BeTrue(); // 5 days until expiration <= 7 day threshold
    }

    [Fact]
    public async Task IsSecretExpiringAsync_WhenSecretExpiresBeyondThreshold_ShouldReturnFalse()
    {
        // Arrange
        var secretId = "not-expiring-soon-secret";
        var rotationPolicy = new RotationPolicy
        {
            MaxAgeDays = 30, // Expires in 30 days
            NotificationThresholdDays = 7 // Notify 7 days before expiration
        };
        await _secretService.SetSecretAsync(secretId, "value", rotationPolicy);

        // Act
        var result = await _secretService.IsSecretExpiringAsync(secretId);

        // Assert
        result.Should().BeFalse(); // 30 days until expiration > 7 day threshold
    }

    [Fact]
    public async Task ExtendRotationWindowAsync_WhenSecretDoesNotExist_ShouldReturnFalse()
    {
        // Act
        var result = await _secretService.ExtendRotationWindowAsync("non-existent", 12);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ExtendRotationWindowAsync_WhenNotInRotationWindow_ShouldReturnFalse()
    {
        // Arrange
        var secretId = "no-rotation-window-secret";
        await _secretService.SetSecretAsync(secretId, "value"); // No previous version = not in rotation window

        // Act
        var result = await _secretService.ExtendRotationWindowAsync(secretId, 12);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ExtendRotationWindowAsync_WhenInRotationWindow_ShouldExtendWindowAndReturnTrue()
    {
        // Arrange
        var secretId = "extend-window-secret";
        var rotationPolicy = new RotationPolicy
        {
            RotationWindowHours = 24
        };
        await _secretService.SetSecretAsync(secretId, "original", rotationPolicy);
        await _secretService.RotateSecretAsync(secretId, "rotated"); // Now in rotation window

        // Act
        var result = await _secretService.ExtendRotationWindowAsync(secretId, 12);

        // Assert
        result.Should().BeTrue();

        var metadata = await _secretService.GetSecretMetadataAsync(secretId);
        metadata!.RotationPolicy!.RotationWindowHours.Should().Be(36); // 24 + 12

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Extended rotation window")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SecretVersioning_CompleteWorkflow_ShouldMaintainVersionHistory()
    {
        // Arrange
        var secretId = "workflow-secret";

        // Act - Create initial secret
        var v1 = await _secretService.SetSecretAsync(secretId, "value-1");

        // Act - Update secret (creates v2)
        var v2 = await _secretService.SetSecretAsync(secretId, "value-2");

        // Act - Rotate secret (creates v3)
        var rotateResult = await _secretService.RotateSecretAsync(secretId, "value-3");

        // Assert - All versions should be accessible
        var currentVersion = await _secretService.GetSecretAsync(secretId);
        currentVersion!.Version.Should().Be(3);
        currentVersion.Value.Should().Be("value-3");

        var version1 = await _secretService.GetSecretVersionAsync(secretId, 1);
        version1!.Value.Should().Be("value-1");

        var version2 = await _secretService.GetSecretVersionAsync(secretId, 2);
        version2!.Value.Should().Be("value-2");

        var metadata = await _secretService.GetSecretMetadataAsync(secretId);
        metadata!.CurrentVersion.Should().Be(3);
        metadata.PreviousVersion.Should().Be(2);
        metadata.IsInRotationWindow.Should().BeTrue();
    }
}
