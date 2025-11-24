using FluentAssertions;
using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Interfaces;
using HotSwap.Distributed.Infrastructure.Tenants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace HotSwap.Distributed.Tests.Tenants;

public class QuotaServiceTests
{
    private readonly Mock<ITenantRepository> _mockTenantRepository;
    private readonly Mock<ILogger<QuotaService>> _mockLogger;
    private readonly QuotaService _service;
    private readonly Guid _tenantId;

    public QuotaServiceTests()
    {
        _mockTenantRepository = new Mock<ITenantRepository>();
        _mockLogger = new Mock<ILogger<QuotaService>>();
        _service = new QuotaService(_mockTenantRepository.Object, _mockLogger.Object);
        _tenantId = Guid.NewGuid();
    }

    #region CheckQuotaAsync Tests

    [Fact]
    public async Task CheckQuotaAsync_WhenWithinQuota_ShouldReturnTrue()
    {
        // Arrange
        var tenant = CreateTenantWithQuota(storageQuota: 100);
        _mockTenantRepository
            .Setup(x => x.GetByIdAsync(_tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);

        // Record some usage (50 GB)
        await _service.RecordUsageAsync(_tenantId, ResourceType.Storage, 50);

        // Act - Request 30 GB (total would be 80, under limit of 100)
        var result = await _service.CheckQuotaAsync(_tenantId, ResourceType.Storage, 30);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CheckQuotaAsync_WhenExceedsQuota_ShouldReturnFalse()
    {
        // Arrange
        var tenant = CreateTenantWithQuota(storageQuota: 100);
        _mockTenantRepository
            .Setup(x => x.GetByIdAsync(_tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);

        // Record some usage (80 GB)
        await _service.RecordUsageAsync(_tenantId, ResourceType.Storage, 80);

        // Act - Request 30 GB (total would be 110, exceeds limit of 100)
        var result = await _service.CheckQuotaAsync(_tenantId, ResourceType.Storage, 30);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task CheckQuotaAsync_WhenExactlyAtLimit_ShouldReturnTrue()
    {
        // Arrange
        var tenant = CreateTenantWithQuota(storageQuota: 100);
        _mockTenantRepository
            .Setup(x => x.GetByIdAsync(_tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);

        // Record some usage (70 GB)
        await _service.RecordUsageAsync(_tenantId, ResourceType.Storage, 70);

        // Act - Request exactly remaining quota (30 GB)
        var result = await _service.CheckQuotaAsync(_tenantId, ResourceType.Storage, 30);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CheckQuotaAsync_WithNoUsage_ShouldAllowUpToLimit()
    {
        // Arrange
        var tenant = CreateTenantWithQuota(storageQuota: 100);
        _mockTenantRepository
            .Setup(x => x.GetByIdAsync(_tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);

        // Act - Request full quota
        var result = await _service.CheckQuotaAsync(_tenantId, ResourceType.Storage, 100);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CheckQuotaAsync_ForDifferentResourceTypes_ShouldCheckCorrectQuota()
    {
        // Arrange
        var tenant = CreateTenantWithQuota(
            storageQuota: 100,
            bandwidthQuota: 500,
            maxWebsites: 10,
            maxDeployments: 3);

        _mockTenantRepository
            .Setup(x => x.GetByIdAsync(_tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);

        // Act & Assert - Storage
        var storageResult = await _service.CheckQuotaAsync(_tenantId, ResourceType.Storage, 50);
        storageResult.Should().BeTrue();

        // Act & Assert - Bandwidth
        var bandwidthResult = await _service.CheckQuotaAsync(_tenantId, ResourceType.Bandwidth, 600);
        bandwidthResult.Should().BeFalse(); // Exceeds 500

        // Act & Assert - Websites
        var websitesResult = await _service.CheckQuotaAsync(_tenantId, ResourceType.Websites, 11);
        websitesResult.Should().BeFalse(); // Exceeds 10

        // Act & Assert - Deployments
        var deploymentsResult = await _service.CheckQuotaAsync(_tenantId, ResourceType.ConcurrentDeployments, 2);
        deploymentsResult.Should().BeTrue();
    }

    #endregion

    #region GetCurrentUsageAsync Tests

    [Fact]
    public async Task GetCurrentUsageAsync_WithNoUsage_ShouldReturnZero()
    {
        // Act
        var usage = await _service.GetCurrentUsageAsync(_tenantId, ResourceType.Storage);

        // Assert
        usage.Should().Be(0);
    }

    [Fact]
    public async Task GetCurrentUsageAsync_AfterRecording_ShouldReturnRecordedAmount()
    {
        // Arrange
        await _service.RecordUsageAsync(_tenantId, ResourceType.Storage, 50);

        // Act
        var usage = await _service.GetCurrentUsageAsync(_tenantId, ResourceType.Storage);

        // Assert
        usage.Should().Be(50);
    }

    [Fact]
    public async Task GetCurrentUsageAsync_WithMultipleRecords_ShouldReturnTotal()
    {
        // Arrange
        await _service.RecordUsageAsync(_tenantId, ResourceType.Storage, 30);
        await _service.RecordUsageAsync(_tenantId, ResourceType.Storage, 20);
        await _service.RecordUsageAsync(_tenantId, ResourceType.Storage, 10);

        // Act
        var usage = await _service.GetCurrentUsageAsync(_tenantId, ResourceType.Storage);

        // Assert
        usage.Should().Be(60);
    }

    [Fact]
    public async Task GetCurrentUsageAsync_ForDifferentResourceTypes_ShouldTrackSeparately()
    {
        // Arrange
        await _service.RecordUsageAsync(_tenantId, ResourceType.Storage, 50);
        await _service.RecordUsageAsync(_tenantId, ResourceType.Bandwidth, 100);
        await _service.RecordUsageAsync(_tenantId, ResourceType.Websites, 3);

        // Act
        var storageUsage = await _service.GetCurrentUsageAsync(_tenantId, ResourceType.Storage);
        var bandwidthUsage = await _service.GetCurrentUsageAsync(_tenantId, ResourceType.Bandwidth);
        var websitesUsage = await _service.GetCurrentUsageAsync(_tenantId, ResourceType.Websites);

        // Assert
        storageUsage.Should().Be(50);
        bandwidthUsage.Should().Be(100);
        websitesUsage.Should().Be(3);
    }

    #endregion

    #region RecordUsageAsync Tests

    [Fact]
    public async Task RecordUsageAsync_FirstRecord_ShouldSetUsage()
    {
        // Act
        var result = await _service.RecordUsageAsync(_tenantId, ResourceType.Storage, 50);

        // Assert
        result.Should().BeTrue();
        var usage = await _service.GetCurrentUsageAsync(_tenantId, ResourceType.Storage);
        usage.Should().Be(50);
    }

    [Fact]
    public async Task RecordUsageAsync_MultipleRecords_ShouldAccumulate()
    {
        // Act
        await _service.RecordUsageAsync(_tenantId, ResourceType.Storage, 30);
        await _service.RecordUsageAsync(_tenantId, ResourceType.Storage, 20);
        await _service.RecordUsageAsync(_tenantId, ResourceType.Storage, 15);

        // Assert
        var usage = await _service.GetCurrentUsageAsync(_tenantId, ResourceType.Storage);
        usage.Should().Be(65);
    }

    [Fact]
    public async Task RecordUsageAsync_ForDifferentTenants_ShouldTrackSeparately()
    {
        // Arrange
        var tenant1 = Guid.NewGuid();
        var tenant2 = Guid.NewGuid();

        // Act
        await _service.RecordUsageAsync(tenant1, ResourceType.Storage, 50);
        await _service.RecordUsageAsync(tenant2, ResourceType.Storage, 30);

        // Assert
        var usage1 = await _service.GetCurrentUsageAsync(tenant1, ResourceType.Storage);
        var usage2 = await _service.GetCurrentUsageAsync(tenant2, ResourceType.Storage);

        usage1.Should().Be(50);
        usage2.Should().Be(30);
    }

    [Fact]
    public async Task RecordUsageAsync_AlwaysReturnsTrue()
    {
        // Act & Assert
        var result1 = await _service.RecordUsageAsync(_tenantId, ResourceType.Storage, 50);
        var result2 = await _service.RecordUsageAsync(_tenantId, ResourceType.Storage, 100);

        result1.Should().BeTrue();
        result2.Should().BeTrue();
    }

    #endregion

    #region GetQuotaLimitAsync Tests

    [Fact]
    public async Task GetQuotaLimitAsync_ForStorage_ShouldReturnStorageQuota()
    {
        // Arrange
        var tenant = CreateTenantWithQuota(storageQuota: 200);
        _mockTenantRepository
            .Setup(x => x.GetByIdAsync(_tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);

        // Act
        var limit = await _service.GetQuotaLimitAsync(_tenantId, ResourceType.Storage);

        // Assert
        limit.Should().Be(200);
    }

    [Fact]
    public async Task GetQuotaLimitAsync_ForBandwidth_ShouldReturnBandwidthQuota()
    {
        // Arrange
        var tenant = CreateTenantWithQuota(bandwidthQuota: 1000);
        _mockTenantRepository
            .Setup(x => x.GetByIdAsync(_tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);

        // Act
        var limit = await _service.GetQuotaLimitAsync(_tenantId, ResourceType.Bandwidth);

        // Assert
        limit.Should().Be(1000);
    }

    [Fact]
    public async Task GetQuotaLimitAsync_ForWebsites_ShouldReturnMaxWebsites()
    {
        // Arrange
        var tenant = CreateTenantWithQuota(maxWebsites: 25);
        _mockTenantRepository
            .Setup(x => x.GetByIdAsync(_tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);

        // Act
        var limit = await _service.GetQuotaLimitAsync(_tenantId, ResourceType.Websites);

        // Assert
        limit.Should().Be(25);
    }

    [Fact]
    public async Task GetQuotaLimitAsync_ForDeployments_ShouldReturnMaxDeployments()
    {
        // Arrange
        var tenant = CreateTenantWithQuota(maxDeployments: 5);
        _mockTenantRepository
            .Setup(x => x.GetByIdAsync(_tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);

        // Act
        var limit = await _service.GetQuotaLimitAsync(_tenantId, ResourceType.ConcurrentDeployments);

        // Assert
        limit.Should().Be(5);
    }

    [Fact]
    public async Task GetQuotaLimitAsync_ForCustomDomains_ShouldReturnMaxDomains()
    {
        // Arrange
        var tenant = CreateTenantWithQuota(maxCustomDomains: 10);
        _mockTenantRepository
            .Setup(x => x.GetByIdAsync(_tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);

        // Act
        var limit = await _service.GetQuotaLimitAsync(_tenantId, ResourceType.CustomDomains);

        // Assert
        limit.Should().Be(10);
    }

    [Fact]
    public async Task GetQuotaLimitAsync_WhenTenantNotFound_ShouldReturnZero()
    {
        // Arrange
        _mockTenantRepository
            .Setup(x => x.GetByIdAsync(_tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tenant?)null);

        // Act
        var limit = await _service.GetQuotaLimitAsync(_tenantId, ResourceType.Storage);

        // Assert
        limit.Should().Be(0);
    }

    #endregion

    #region IsWithinQuotaAsync Tests

    [Fact]
    public async Task IsWithinQuotaAsync_WhenAllResourcesWithinQuota_ShouldReturnTrue()
    {
        // Arrange
        var tenant = CreateTenantWithQuota(
            storageQuota: 100,
            bandwidthQuota: 500,
            maxWebsites: 10,
            maxDeployments: 5,
            maxCustomDomains: 15);

        _mockTenantRepository
            .Setup(x => x.GetByIdAsync(_tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);

        // Record usage under limits
        await _service.RecordUsageAsync(_tenantId, ResourceType.Storage, 50);
        await _service.RecordUsageAsync(_tenantId, ResourceType.Bandwidth, 300);
        await _service.RecordUsageAsync(_tenantId, ResourceType.Websites, 5);
        await _service.RecordUsageAsync(_tenantId, ResourceType.ConcurrentDeployments, 2);
        await _service.RecordUsageAsync(_tenantId, ResourceType.CustomDomains, 10);

        // Act
        var result = await _service.IsWithinQuotaAsync(_tenantId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsWithinQuotaAsync_WhenOneResourceExceedsQuota_ShouldReturnFalse()
    {
        // Arrange
        var tenant = CreateTenantWithQuota(
            storageQuota: 100,
            bandwidthQuota: 500,
            maxWebsites: 10);

        _mockTenantRepository
            .Setup(x => x.GetByIdAsync(_tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);

        // Record usage - storage exceeds
        await _service.RecordUsageAsync(_tenantId, ResourceType.Storage, 150);
        await _service.RecordUsageAsync(_tenantId, ResourceType.Bandwidth, 300);
        await _service.RecordUsageAsync(_tenantId, ResourceType.Websites, 5);

        // Act
        var result = await _service.IsWithinQuotaAsync(_tenantId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsWithinQuotaAsync_WhenMultipleResourcesExceed_ShouldReturnFalse()
    {
        // Arrange
        var tenant = CreateTenantWithQuota(
            storageQuota: 100,
            bandwidthQuota: 500,
            maxWebsites: 10);

        _mockTenantRepository
            .Setup(x => x.GetByIdAsync(_tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);

        // Record usage - multiple exceed
        await _service.RecordUsageAsync(_tenantId, ResourceType.Storage, 150);
        await _service.RecordUsageAsync(_tenantId, ResourceType.Bandwidth, 600);
        await _service.RecordUsageAsync(_tenantId, ResourceType.Websites, 15);

        // Act
        var result = await _service.IsWithinQuotaAsync(_tenantId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsWithinQuotaAsync_WhenExactlyAtLimits_ShouldReturnTrue()
    {
        // Arrange
        var tenant = CreateTenantWithQuota(
            storageQuota: 100,
            bandwidthQuota: 500,
            maxWebsites: 10);

        _mockTenantRepository
            .Setup(x => x.GetByIdAsync(_tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);

        // Record usage exactly at limits
        await _service.RecordUsageAsync(_tenantId, ResourceType.Storage, 100);
        await _service.RecordUsageAsync(_tenantId, ResourceType.Bandwidth, 500);
        await _service.RecordUsageAsync(_tenantId, ResourceType.Websites, 10);

        // Act
        var result = await _service.IsWithinQuotaAsync(_tenantId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsWithinQuotaAsync_WithNoUsage_ShouldReturnTrue()
    {
        // Arrange
        var tenant = CreateTenantWithQuota(storageQuota: 100);
        _mockTenantRepository
            .Setup(x => x.GetByIdAsync(_tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);

        // Act
        var result = await _service.IsWithinQuotaAsync(_tenantId);

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region Helper Methods

    private Tenant CreateTenantWithQuota(
        long storageQuota = 100,
        long bandwidthQuota = 1000,
        int maxWebsites = 10,
        int maxDeployments = 5,
        int maxCustomDomains = 10)
    {
        return new Tenant
        {
            TenantId = _tenantId,
            Name = "Test Tenant",
            Subdomain = "test",
            ResourceQuota = new ResourceQuota
            {
                StorageQuotaGB = storageQuota,
                BandwidthQuotaGB = bandwidthQuota,
                MaxWebsites = maxWebsites,
                MaxConcurrentDeployments = maxDeployments,
                MaxCustomDomains = maxCustomDomains
            }
        };
    }

    #endregion
}
