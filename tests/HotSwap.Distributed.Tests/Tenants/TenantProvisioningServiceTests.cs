using FluentAssertions;
using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Interfaces;
using HotSwap.Distributed.Infrastructure.Storage;
using HotSwap.Distributed.Infrastructure.Tenants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace HotSwap.Distributed.Tests.Tenants;

public class TenantProvisioningServiceTests
{
    private readonly Mock<ITenantRepository> _mockTenantRepository;
    private readonly Mock<IObjectStorageService> _mockStorageService;
    private readonly Mock<ILogger<TenantProvisioningService>> _mockLogger;
    private readonly TenantProvisioningService _service;

    public TenantProvisioningServiceTests()
    {
        _mockTenantRepository = new Mock<ITenantRepository>();
        _mockStorageService = new Mock<IObjectStorageService>();
        _mockLogger = new Mock<ILogger<TenantProvisioningService>>();

        // Setup default behaviors for repository
        _mockTenantRepository
            .Setup(x => x.CreateAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tenant t, CancellationToken _) => t);

        _mockTenantRepository
            .Setup(x => x.UpdateAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tenant t, CancellationToken _) => t);

        _mockTenantRepository
            .Setup(x => x.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Setup default behaviors for storage service
        _mockStorageService
            .Setup(x => x.BucketExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _mockStorageService
            .Setup(x => x.CreateBucketAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _mockStorageService
            .Setup(x => x.DeleteBucketAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _service = new TenantProvisioningService(
            _mockTenantRepository.Object,
            _mockLogger.Object,
            _mockStorageService.Object);
    }

    #region ProvisionTenantAsync Tests

    [Fact]
    public async Task ProvisionTenantAsync_WithValidTenant_ShouldProvisionSuccessfully()
    {
        // Arrange
        var tenant = CreateValidTenant();

        // Act
        var result = await _service.ProvisionTenantAsync(tenant);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(TenantStatus.Active);
        result.DatabaseSchema.Should().NotBeNullOrEmpty();
        result.KubernetesNamespace.Should().NotBeNullOrEmpty();
        result.CacheKeyPrefix.Should().NotBeNullOrEmpty();
        result.StorageBucketPrefix.Should().NotBeNullOrEmpty();

        // Verify resource identifiers are generated correctly
        result.DatabaseSchema.Should().StartWith("tenant_");
        result.KubernetesNamespace.Should().StartWith("tenant-");
        result.CacheKeyPrefix.Should().StartWith("tenant:");
        result.CacheKeyPrefix.Should().EndWith(":");
        result.StorageBucketPrefix.Should().StartWith("tenant-");
        result.StorageBucketPrefix.Should().EndWith("/");
    }

    [Fact]
    public async Task ProvisionTenantAsync_ShouldCreateTenantInRepository()
    {
        // Arrange
        var tenant = CreateValidTenant();

        // Act
        await _service.ProvisionTenantAsync(tenant);

        // Assert
        _mockTenantRepository.Verify(
            x => x.CreateAsync(
                It.Is<Tenant>(t => t.TenantId == tenant.TenantId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProvisionTenantAsync_ShouldUpdateTenantStatusToActive()
    {
        // Arrange
        var tenant = CreateValidTenant();

        // Act
        await _service.ProvisionTenantAsync(tenant);

        // Assert
        _mockTenantRepository.Verify(
            x => x.UpdateAsync(
                It.Is<Tenant>(t => t.Status == TenantStatus.Active),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProvisionTenantAsync_ShouldCreateStorageBucket()
    {
        // Arrange
        var tenant = CreateValidTenant();

        // Act
        await _service.ProvisionTenantAsync(tenant);

        // Assert
        _mockStorageService.Verify(
            x => x.CreateBucketAsync(
                It.Is<string>(name => name.StartsWith("tenant-")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProvisionTenantAsync_WhenStorageBucketExists_ShouldNotCreateAgain()
    {
        // Arrange
        var tenant = CreateValidTenant();
        _mockStorageService
            .Setup(x => x.BucketExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _service.ProvisionTenantAsync(tenant);

        // Assert
        _mockStorageService.Verify(
            x => x.CreateBucketAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProvisionTenantAsync_WithMissingName_ShouldThrowException()
    {
        // Arrange
        var tenant = CreateValidTenant();
        tenant.Name = "";

        // Act
        Func<Task> act = async () => await _service.ProvisionTenantAsync(tenant);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Tenant name is required*");
    }

    [Fact]
    public async Task ProvisionTenantAsync_WithMissingSubdomain_ShouldThrowException()
    {
        // Arrange
        var tenant = CreateValidTenant();
        tenant.Subdomain = "";

        // Act
        Func<Task> act = async () => await _service.ProvisionTenantAsync(tenant);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Tenant subdomain is required*");
    }

    [Fact]
    public async Task ProvisionTenantAsync_WithInvalidSubdomainFormat_ShouldThrowException()
    {
        // Arrange
        var tenant = CreateValidTenant();
        tenant.Subdomain = "Invalid_Subdomain"; // Uppercase and underscore not allowed

        // Act
        Func<Task> act = async () => await _service.ProvisionTenantAsync(tenant);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Subdomain must contain only lowercase letters, numbers, and hyphens*");
    }

    [Fact]
    public async Task ProvisionTenantAsync_WithNullResourceQuota_ShouldThrowException()
    {
        // Arrange
        var tenant = CreateValidTenant();
        tenant.ResourceQuota = null!;

        // Act
        Func<Task> act = async () => await _service.ProvisionTenantAsync(tenant);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Resource quota is required*");
    }

    [Fact]
    public async Task ProvisionTenantAsync_OnFailure_ShouldRollback()
    {
        // Arrange
        var tenant = CreateValidTenant();
        _mockStorageService
            .Setup(x => x.CreateBucketAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Storage service failure"));

        // Act
        Func<Task> act = async () => await _service.ProvisionTenantAsync(tenant);

        // Assert
        await act.Should().ThrowAsync<Exception>()
            .WithMessage("Storage service failure");

        // Verify rollback was attempted - tenant should be deleted
        _mockTenantRepository.Verify(
            x => x.DeleteAsync(tenant.TenantId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProvisionTenantAsync_WithoutStorageService_ShouldUseSimulatedStorage()
    {
        // Arrange
        var serviceWithoutStorage = new TenantProvisioningService(
            _mockTenantRepository.Object,
            _mockLogger.Object,
            storageService: null);

        var tenant = CreateValidTenant();

        // Act
        var result = await serviceWithoutStorage.ProvisionTenantAsync(tenant);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(TenantStatus.Active);

        // Storage service should not be called
        _mockStorageService.Verify(
            x => x.CreateBucketAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region DeprovisionTenantAsync Tests

    [Fact]
    public async Task DeprovisionTenantAsync_WithValidTenant_ShouldDeprovisionSuccessfully()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var tenant = CreateValidTenant(tenantId);
        tenant.Status = TenantStatus.Active;

        _mockTenantRepository
            .Setup(x => x.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);

        // Act
        var result = await _service.DeprovisionTenantAsync(tenantId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task DeprovisionTenantAsync_ShouldUpdateStatusToDeprovisioning()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var tenant = CreateValidTenant(tenantId);
        tenant.Status = TenantStatus.Active;

        _mockTenantRepository
            .Setup(x => x.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);

        // Act
        await _service.DeprovisionTenantAsync(tenantId);

        // Assert
        _mockTenantRepository.Verify(
            x => x.UpdateAsync(
                It.Is<Tenant>(t => t.Status == TenantStatus.Deprovisioning),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DeprovisionTenantAsync_ShouldDeleteStorageBucket()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var tenant = CreateValidTenant(tenantId);
        tenant.Status = TenantStatus.Active;

        _mockTenantRepository
            .Setup(x => x.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);

        _mockStorageService
            .Setup(x => x.BucketExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _service.DeprovisionTenantAsync(tenantId);

        // Assert
        _mockStorageService.Verify(
            x => x.DeleteBucketAsync(
                It.Is<string>(name => name.StartsWith("tenant-")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DeprovisionTenantAsync_WhenBucketDoesNotExist_ShouldNotAttemptDelete()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var tenant = CreateValidTenant(tenantId);
        tenant.Status = TenantStatus.Active;

        _mockTenantRepository
            .Setup(x => x.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);

        _mockStorageService
            .Setup(x => x.BucketExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        await _service.DeprovisionTenantAsync(tenantId);

        // Assert
        _mockStorageService.Verify(
            x => x.DeleteBucketAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DeprovisionTenantAsync_ShouldSoftDeleteTenant()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var tenant = CreateValidTenant(tenantId);
        tenant.Status = TenantStatus.Active;

        _mockTenantRepository
            .Setup(x => x.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);

        // Act
        await _service.DeprovisionTenantAsync(tenantId);

        // Assert
        _mockTenantRepository.Verify(
            x => x.DeleteAsync(tenantId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DeprovisionTenantAsync_WithNonExistentTenant_ShouldReturnFalse()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        _mockTenantRepository
            .Setup(x => x.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tenant?)null);

        // Act
        var result = await _service.DeprovisionTenantAsync(tenantId);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region SuspendTenantAsync Tests

    [Fact]
    public async Task SuspendTenantAsync_WithValidTenant_ShouldSuspendSuccessfully()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var tenant = CreateValidTenant(tenantId);
        tenant.Status = TenantStatus.Active;

        _mockTenantRepository
            .Setup(x => x.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);

        var suspensionReason = "Payment overdue";

        // Act
        var result = await _service.SuspendTenantAsync(tenantId, suspensionReason);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task SuspendTenantAsync_ShouldSetStatusToSuspended()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var tenant = CreateValidTenant(tenantId);
        tenant.Status = TenantStatus.Active;

        _mockTenantRepository
            .Setup(x => x.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);

        var suspensionReason = "Violation of terms";

        // Act
        await _service.SuspendTenantAsync(tenantId, suspensionReason);

        // Assert
        _mockTenantRepository.Verify(
            x => x.UpdateAsync(
                It.Is<Tenant>(t =>
                    t.Status == TenantStatus.Suspended &&
                    t.SuspendedAt != null &&
                    t.Metadata.ContainsKey("suspension_reason") &&
                    t.Metadata["suspension_reason"] == suspensionReason),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SuspendTenantAsync_WithNonExistentTenant_ShouldReturnFalse()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        _mockTenantRepository
            .Setup(x => x.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tenant?)null);

        // Act
        var result = await _service.SuspendTenantAsync(tenantId, "Some reason");

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region ActivateTenantAsync Tests

    [Fact]
    public async Task ActivateTenantAsync_WithSuspendedTenant_ShouldActivateSuccessfully()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var tenant = CreateValidTenant(tenantId);
        tenant.Status = TenantStatus.Suspended;
        tenant.SuspendedAt = DateTime.UtcNow.AddDays(-7);
        tenant.Metadata["suspension_reason"] = "Payment overdue";

        _mockTenantRepository
            .Setup(x => x.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);

        // Act
        var result = await _service.ActivateTenantAsync(tenantId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ActivateTenantAsync_ShouldSetStatusToActive()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var tenant = CreateValidTenant(tenantId);
        tenant.Status = TenantStatus.Suspended;
        tenant.SuspendedAt = DateTime.UtcNow.AddDays(-7);
        tenant.Metadata["suspension_reason"] = "Payment overdue";

        _mockTenantRepository
            .Setup(x => x.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);

        // Act
        await _service.ActivateTenantAsync(tenantId);

        // Assert
        _mockTenantRepository.Verify(
            x => x.UpdateAsync(
                It.Is<Tenant>(t =>
                    t.Status == TenantStatus.Active &&
                    t.SuspendedAt == null &&
                    !t.Metadata.ContainsKey("suspension_reason")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ActivateTenantAsync_WithNonExistentTenant_ShouldReturnFalse()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        _mockTenantRepository
            .Setup(x => x.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tenant?)null);

        // Act
        var result = await _service.ActivateTenantAsync(tenantId);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region ValidateProvisioningAsync Tests

    [Fact]
    public async Task ValidateProvisioningAsync_WithValidTenant_ShouldReturnSuccess()
    {
        // Arrange
        var tenant = CreateValidTenant();

        // Act
        var result = await _service.ValidateProvisioningAsync(tenant);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateProvisioningAsync_WithMissingName_ShouldReturnError()
    {
        // Arrange
        var tenant = CreateValidTenant();
        tenant.Name = "";

        // Act
        var result = await _service.ValidateProvisioningAsync(tenant);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Tenant name is required");
    }

    [Fact]
    public async Task ValidateProvisioningAsync_WithMissingSubdomain_ShouldReturnError()
    {
        // Arrange
        var tenant = CreateValidTenant();
        tenant.Subdomain = "";

        // Act
        var result = await _service.ValidateProvisioningAsync(tenant);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Tenant subdomain is required");
    }

    [Fact]
    public async Task ValidateProvisioningAsync_WithInvalidSubdomainUppercase_ShouldReturnError()
    {
        // Arrange
        var tenant = CreateValidTenant();
        tenant.Subdomain = "AcmeCorp";

        // Act
        var result = await _service.ValidateProvisioningAsync(tenant);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Subdomain must contain only lowercase letters, numbers, and hyphens");
    }

    [Fact]
    public async Task ValidateProvisioningAsync_WithInvalidSubdomainSpecialChars_ShouldReturnError()
    {
        // Arrange
        var tenant = CreateValidTenant();
        tenant.Subdomain = "acme_corp@123";

        // Act
        var result = await _service.ValidateProvisioningAsync(tenant);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Subdomain must contain only lowercase letters, numbers, and hyphens");
    }

    [Fact]
    public async Task ValidateProvisioningAsync_WithValidSubdomainHyphens_ShouldReturnSuccess()
    {
        // Arrange
        var tenant = CreateValidTenant();
        tenant.Subdomain = "acme-corp-123";

        // Act
        var result = await _service.ValidateProvisioningAsync(tenant);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateProvisioningAsync_WithNullResourceQuota_ShouldReturnError()
    {
        // Arrange
        var tenant = CreateValidTenant();
        tenant.ResourceQuota = null!;

        // Act
        var result = await _service.ValidateProvisioningAsync(tenant);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Resource quota is required");
    }

    [Fact]
    public async Task ValidateProvisioningAsync_WithMultipleErrors_ShouldReturnAllErrors()
    {
        // Arrange
        var tenant = CreateValidTenant();
        tenant.Name = "";
        tenant.Subdomain = "INVALID";
        tenant.ResourceQuota = null!;

        // Act
        var result = await _service.ValidateProvisioningAsync(tenant);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCount(3);
        result.Errors.Should().Contain("Tenant name is required");
        result.Errors.Should().Contain("Subdomain must contain only lowercase letters, numbers, and hyphens");
        result.Errors.Should().Contain("Resource quota is required");
    }

    #endregion

    #region Helper Methods

    private Tenant CreateValidTenant(Guid? tenantId = null)
    {
        return new Tenant
        {
            TenantId = tenantId ?? Guid.NewGuid(),
            Name = "Test Tenant",
            Subdomain = "test-tenant",
            Status = TenantStatus.Provisioning,
            Tier = SubscriptionTier.Professional,
            ResourceQuota = new ResourceQuota
            {
                MaxWebsites = 10,
                StorageQuotaGB = 100,
                BandwidthQuotaGB = 1000
            },
            ContactEmail = "admin@test-tenant.com",
            Metadata = new Dictionary<string, string>()
        };
    }

    #endregion
}
