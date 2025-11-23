using FluentAssertions;
using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Interfaces;
using HotSwap.Distributed.Infrastructure.Tenants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace HotSwap.Distributed.Tests.Infrastructure.Storage;

public class TenantProvisioningServiceWithStorageTests
{
    private readonly Mock<ITenantRepository> _mockTenantRepository;
    private readonly MockObjectStorageService _mockStorageService;
    private readonly Mock<ILogger<TenantProvisioningService>> _mockLogger;
    private readonly TenantProvisioningService _service;

    public TenantProvisioningServiceWithStorageTests()
    {
        _mockTenantRepository = new Mock<ITenantRepository>();
        _mockStorageService = new MockObjectStorageService();
        _mockLogger = new Mock<ILogger<TenantProvisioningService>>();

        _service = new TenantProvisioningService(
            _mockTenantRepository.Object,
            _mockLogger.Object,
            _mockStorageService);
    }

    [Fact]
    public async Task ProvisionTenantAsync_ShouldCreateStorageBucket()
    {
        // Arrange
        var tenant = new Tenant
        {
            TenantId = Guid.NewGuid(),
            Name = "Test Tenant",
            Subdomain = "test",
            Status = TenantStatus.Active,
            Tier = SubscriptionTier.Professional,
            CreatedAt = DateTime.UtcNow
        };

        _mockTenantRepository
            .Setup(r => r.CreateAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tenant t, CancellationToken ct) => t);

        _mockTenantRepository
            .Setup(r => r.UpdateAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tenant t, CancellationToken ct) => t);

        // Act
        var result = await _service.ProvisionTenantAsync(tenant);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(TenantStatus.Active);

        var bucketName = $"tenant-{tenant.TenantId:N}";
        var bucketExists = await _mockStorageService.BucketExistsAsync(bucketName);
        bucketExists.Should().BeTrue("storage bucket should be created for tenant");
    }

    [Fact]
    public async Task ProvisionTenantAsync_WhenBucketAlreadyExists_ShouldNotFail()
    {
        // Arrange
        var tenant = new Tenant
        {
            TenantId = Guid.NewGuid(),
            Name = "Test Tenant",
            Subdomain = "test",
            Status = TenantStatus.Active,
            Tier = SubscriptionTier.Professional,
            CreatedAt = DateTime.UtcNow
        };

        var bucketName = $"tenant-{tenant.TenantId:N}";
        await _mockStorageService.CreateBucketAsync(bucketName); // Pre-create bucket

        _mockTenantRepository
            .Setup(r => r.CreateAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tenant t, CancellationToken ct) => t);

        _mockTenantRepository
            .Setup(r => r.UpdateAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tenant t, CancellationToken ct) => t);

        // Act
        var result = await _service.ProvisionTenantAsync(tenant);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(TenantStatus.Active);
        _mockStorageService.GetBucketCount().Should().Be(1);
    }

    [Fact]
    public async Task DeprovisionTenantAsync_ShouldDeleteStorageBucket()
    {
        // Arrange
        var tenant = new Tenant
        {
            TenantId = Guid.NewGuid(),
            Name = "Test Tenant",
            Subdomain = "test",
            Status = TenantStatus.Active,
            Tier = SubscriptionTier.Professional,
            CreatedAt = DateTime.UtcNow
        };

        var bucketName = $"tenant-{tenant.TenantId:N}";
        await _mockStorageService.CreateBucketAsync(bucketName);

        _mockTenantRepository
            .Setup(r => r.GetByIdAsync(tenant.TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);

        _mockTenantRepository
            .Setup(r => r.DeleteAsync(tenant.TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _service.DeprovisionTenantAsync(tenant.TenantId);

        // Assert
        result.Should().BeTrue();

        var bucketExists = await _mockStorageService.BucketExistsAsync(bucketName);
        bucketExists.Should().BeFalse("storage bucket should be deleted after deprovisioning");
    }

    [Fact]
    public async Task DeprovisionTenantAsync_WhenBucketDoesNotExist_ShouldNotFail()
    {
        // Arrange
        var tenant = new Tenant
        {
            TenantId = Guid.NewGuid(),
            Name = "Test Tenant",
            Subdomain = "test",
            Status = TenantStatus.Active,
            Tier = SubscriptionTier.Professional,
            CreatedAt = DateTime.UtcNow
        };

        _mockTenantRepository
            .Setup(r => r.GetByIdAsync(tenant.TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);

        _mockTenantRepository
            .Setup(r => r.DeleteAsync(tenant.TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _service.DeprovisionTenantAsync(tenant.TenantId);

        // Assert
        result.Should().BeTrue();
    }
}
