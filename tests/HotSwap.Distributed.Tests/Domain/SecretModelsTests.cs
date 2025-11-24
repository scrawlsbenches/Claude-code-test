using FluentAssertions;
using HotSwap.Distributed.Domain.Models;
using Xunit;

namespace HotSwap.Distributed.Tests.Domain;

public class SecretModelsTests
{
    #region SecretMetadata Tests

    [Fact]
    public void SecretMetadata_IsInRotationWindow_WhenHasPreviousVersion_ShouldReturnTrue()
    {
        var metadata = new SecretMetadata
        {
            SecretId = "test-secret",
            CurrentVersion = 2,
            PreviousVersion = 1,
            CreatedAt = DateTime.UtcNow
        };

        metadata.IsInRotationWindow.Should().BeTrue();
    }

    [Fact]
    public void SecretMetadata_IsInRotationWindow_WhenNoPreviousVersion_ShouldReturnFalse()
    {
        var metadata = new SecretMetadata
        {
            SecretId = "test-secret",
            CurrentVersion = 1,
            PreviousVersion = null,
            CreatedAt = DateTime.UtcNow
        };

        metadata.IsInRotationWindow.Should().BeFalse();
    }

    [Fact]
    public void SecretMetadata_DaysUntilExpiration_WhenHasExpiresAt_ShouldCalculateCorrectly()
    {
        var metadata = new SecretMetadata
        {
            SecretId = "test-secret",
            CurrentVersion = 1,
            ExpiresAt = DateTime.UtcNow.AddDays(10),
            CreatedAt = DateTime.UtcNow
        };

        var days = metadata.DaysUntilExpiration;

        days.Should().NotBeNull();
        days.Should().BeInRange(9, 10); // Allow for timing variance
    }

    [Fact]
    public void SecretMetadata_DaysUntilExpiration_WhenNoExpiresAt_ShouldReturnNull()
    {
        var metadata = new SecretMetadata
        {
            SecretId = "test-secret",
            CurrentVersion = 1,
            ExpiresAt = null,
            CreatedAt = DateTime.UtcNow
        };

        metadata.DaysUntilExpiration.Should().BeNull();
    }

    [Fact]
    public void SecretMetadata_DaysUntilExpiration_WhenExpired_ShouldReturnNegativeValue()
    {
        var metadata = new SecretMetadata
        {
            SecretId = "test-secret",
            CurrentVersion = 1,
            ExpiresAt = DateTime.UtcNow.AddDays(-5),
            CreatedAt = DateTime.UtcNow.AddDays(-10)
        };

        var days = metadata.DaysUntilExpiration;

        days.Should().NotBeNull();
        days.Should().BeLessThan(0);
    }

    [Fact]
    public void SecretMetadata_DefaultValues_ShouldBeInitialized()
    {
        var metadata = new SecretMetadata();

        metadata.SecretId.Should().BeEmpty();
        metadata.Tags.Should().NotBeNull().And.BeEmpty();
        metadata.PreviousVersion.Should().BeNull();
        metadata.ExpiresAt.Should().BeNull();
        metadata.LastRotatedAt.Should().BeNull();
        metadata.NextRotationAt.Should().BeNull();
        metadata.RotationPolicy.Should().BeNull();
    }

    #endregion

    #region SecretVersion Tests

    [Fact]
    public void SecretVersion_DefaultValues_ShouldBeInitialized()
    {
        var version = new SecretVersion();

        version.SecretId.Should().BeEmpty();
        version.Value.Should().BeEmpty();
        version.Version.Should().Be(0);
        version.IsActive.Should().BeFalse();
        version.IsDeleted.Should().BeFalse();
        version.ExpiresAt.Should().BeNull();
    }

    [Fact]
    public void SecretVersion_Properties_ShouldBeSettable()
    {
        var version = new SecretVersion
        {
            SecretId = "test-secret",
            Version = 5,
            Value = "secret-value",
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            IsActive = true,
            IsDeleted = false
        };

        version.SecretId.Should().Be("test-secret");
        version.Version.Should().Be(5);
        version.Value.Should().Be("secret-value");
        version.IsActive.Should().BeTrue();
        version.IsDeleted.Should().BeFalse();
        version.ExpiresAt.Should().NotBeNull();
    }

    #endregion

    #region RotationPolicy Tests

    [Fact]
    public void RotationPolicy_DefaultValues_ShouldBeInitialized()
    {
        var policy = new RotationPolicy();

        policy.RotationIntervalDays.Should().BeNull();
        policy.MaxAgeDays.Should().BeNull();
        policy.NotificationThresholdDays.Should().Be(7);
        policy.RotationWindowHours.Should().Be(24);
        policy.EnableAutomaticRotation.Should().BeFalse();
        policy.NotificationRecipients.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void RotationPolicy_Properties_ShouldBeSettable()
    {
        var policy = new RotationPolicy
        {
            RotationIntervalDays = 30,
            MaxAgeDays = 90,
            NotificationThresholdDays = 14,
            RotationWindowHours = 48,
            EnableAutomaticRotation = true,
            NotificationRecipients = new List<string> { "admin@example.com" }
        };

        policy.RotationIntervalDays.Should().Be(30);
        policy.MaxAgeDays.Should().Be(90);
        policy.NotificationThresholdDays.Should().Be(14);
        policy.RotationWindowHours.Should().Be(48);
        policy.EnableAutomaticRotation.Should().BeTrue();
        policy.NotificationRecipients.Should().ContainSingle("admin@example.com");
    }

    #endregion

    #region SecretRotationResult Tests

    [Fact]
    public void SecretRotationResult_CreateSuccess_ShouldCreateSuccessfulResult()
    {
        var secretId = "test-secret";
        var newVersion = 3;
        var previousVersion = 2;
        var rotationWindowEndsAt = DateTime.UtcNow.AddHours(24);

        var result = SecretRotationResult.CreateSuccess(secretId, newVersion, previousVersion, rotationWindowEndsAt);

        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.SecretId.Should().Be(secretId);
        result.NewVersion.Should().Be(newVersion);
        result.PreviousVersion.Should().Be(previousVersion);
        result.RotatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        result.RotationWindowEndsAt.Should().BeCloseTo(rotationWindowEndsAt, TimeSpan.FromSeconds(1));
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void SecretRotationResult_CreateSuccess_WithNoPreviousVersion_ShouldWork()
    {
        var secretId = "new-secret";
        var newVersion = 1;
        var rotationWindowEndsAt = DateTime.UtcNow.AddHours(24);

        var result = SecretRotationResult.CreateSuccess(secretId, newVersion, null, rotationWindowEndsAt);

        result.Success.Should().BeTrue();
        result.SecretId.Should().Be(secretId);
        result.NewVersion.Should().Be(newVersion);
        result.PreviousVersion.Should().BeNull();
    }

    [Fact]
    public void SecretRotationResult_CreateFailure_ShouldCreateFailedResult()
    {
        var secretId = "test-secret";
        var errorMessage = "Rotation failed due to network error";

        var result = SecretRotationResult.CreateFailure(secretId, errorMessage);

        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.SecretId.Should().Be(secretId);
        result.ErrorMessage.Should().Be(errorMessage);
        result.RotatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        result.NewVersion.Should().Be(0);
        result.PreviousVersion.Should().BeNull();
    }

    [Fact]
    public void SecretRotationResult_DefaultValues_ShouldBeInitialized()
    {
        var result = new SecretRotationResult();

        result.SecretId.Should().BeEmpty();
        result.Success.Should().BeFalse();
        result.NewVersion.Should().Be(0);
        result.PreviousVersion.Should().BeNull();
        result.ErrorMessage.Should().BeNull();
        result.Details.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void SecretRotationResult_Details_ShouldBeModifiable()
    {
        var result = SecretRotationResult.CreateSuccess("test", 2, 1, DateTime.UtcNow.AddHours(24));

        result.Details["rotation_reason"] = "scheduled";
        result.Details["triggered_by"] = "system";

        result.Details.Should().HaveCount(2);
        result.Details["rotation_reason"].Should().Be("scheduled");
        result.Details["triggered_by"].Should().Be("system");
    }

    #endregion
}
