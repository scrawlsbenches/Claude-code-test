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

public class SubscriptionServiceTests
{
    private readonly Mock<ITenantRepository> _mockTenantRepository;
    private readonly Mock<IObjectStorageService> _mockStorageService;
    private readonly Mock<ILogger<SubscriptionService>> _mockLogger;
    private readonly SubscriptionService _service;
    private readonly Guid _tenantId;

    public SubscriptionServiceTests()
    {
        _mockTenantRepository = new Mock<ITenantRepository>();
        _mockStorageService = new Mock<IObjectStorageService>();
        _mockLogger = new Mock<ILogger<SubscriptionService>>();

        _service = new SubscriptionService(
            _mockTenantRepository.Object,
            _mockLogger.Object,
            _mockStorageService.Object);

        _tenantId = Guid.NewGuid();
    }

    #region CreateSubscriptionAsync Tests

    [Theory]
    [InlineData(SubscriptionTier.Free, 0)]
    [InlineData(SubscriptionTier.Starter, 2900)]
    [InlineData(SubscriptionTier.Professional, 9900)]
    [InlineData(SubscriptionTier.Enterprise, 49900)]
    public async Task CreateSubscriptionAsync_WithValidTier_ShouldCreateSubscription(SubscriptionTier tier, int expectedPrice)
    {
        // Arrange
        var tenant = CreateTestTenant();
        _mockTenantRepository
            .Setup(x => x.GetByIdAsync(_tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);

        _mockTenantRepository
            .Setup(x => x.UpdateAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tenant t, CancellationToken _) => t);

        // Act
        var subscription = await _service.CreateSubscriptionAsync(_tenantId, tier);

        // Assert
        subscription.Should().NotBeNull();
        subscription.TenantId.Should().Be(_tenantId);
        subscription.Tier.Should().Be(tier);
        subscription.Status.Should().Be("Active");
        subscription.AmountCents.Should().Be(expectedPrice);
        subscription.BillingCycle.Should().Be("Monthly");
    }

    [Fact]
    public async Task CreateSubscriptionAsync_ShouldUpdateTenantTier()
    {
        // Arrange
        var tenant = CreateTestTenant();
        _mockTenantRepository
            .Setup(x => x.GetByIdAsync(_tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);

        _mockTenantRepository
            .Setup(x => x.UpdateAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tenant t, CancellationToken _) => t);

        // Act
        await _service.CreateSubscriptionAsync(_tenantId, SubscriptionTier.Professional);

        // Assert
        _mockTenantRepository.Verify(
            x => x.UpdateAsync(
                It.Is<Tenant>(t => t.Tier == SubscriptionTier.Professional),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateSubscriptionAsync_ShouldUpdateResourceQuota()
    {
        // Arrange
        var tenant = CreateTestTenant();
        _mockTenantRepository
            .Setup(x => x.GetByIdAsync(_tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);

        _mockTenantRepository
            .Setup(x => x.UpdateAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tenant t, CancellationToken _) => t);

        // Act
        await _service.CreateSubscriptionAsync(_tenantId, SubscriptionTier.Professional);

        // Assert
        _mockTenantRepository.Verify(
            x => x.UpdateAsync(
                It.Is<Tenant>(t => t.ResourceQuota != null),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateSubscriptionAsync_ShouldSetBillingPeriod()
    {
        // Arrange
        var tenant = CreateTestTenant();
        _mockTenantRepository
            .Setup(x => x.GetByIdAsync(_tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);

        _mockTenantRepository
            .Setup(x => x.UpdateAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tenant t, CancellationToken _) => t);

        // Act
        var subscription = await _service.CreateSubscriptionAsync(_tenantId, SubscriptionTier.Starter);

        // Assert
        subscription.CurrentPeriodStart.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        subscription.CurrentPeriodEnd.Should().BeCloseTo(DateTime.UtcNow.AddMonths(1), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task CreateSubscriptionAsync_WhenTenantNotFound_ShouldThrowException()
    {
        // Arrange
        _mockTenantRepository
            .Setup(x => x.GetByIdAsync(_tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tenant?)null);

        // Act
        Func<Task> act = async () => await _service.CreateSubscriptionAsync(_tenantId, SubscriptionTier.Free);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    #endregion

    #region UpgradeSubscriptionAsync Tests

    [Theory]
    [InlineData(SubscriptionTier.Free, SubscriptionTier.Starter)]
    [InlineData(SubscriptionTier.Starter, SubscriptionTier.Professional)]
    [InlineData(SubscriptionTier.Professional, SubscriptionTier.Enterprise)]
    public async Task UpgradeSubscriptionAsync_WithValidUpgrade_ShouldSucceed(SubscriptionTier fromTier, SubscriptionTier toTier)
    {
        // Arrange
        var tenant = CreateTestTenant();
        _mockTenantRepository
            .Setup(x => x.GetByIdAsync(_tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);

        _mockTenantRepository
            .Setup(x => x.UpdateAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tenant t, CancellationToken _) => t);

        await _service.CreateSubscriptionAsync(_tenantId, fromTier);

        // Act
        var upgraded = await _service.UpgradeSubscriptionAsync(_tenantId, toTier);

        // Assert
        upgraded.Tier.Should().Be(toTier);
    }

    [Fact]
    public async Task UpgradeSubscriptionAsync_ShouldUpdatePrice()
    {
        // Arrange
        var tenant = CreateTestTenant();
        _mockTenantRepository
            .Setup(x => x.GetByIdAsync(_tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);

        _mockTenantRepository
            .Setup(x => x.UpdateAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tenant t, CancellationToken _) => t);

        await _service.CreateSubscriptionAsync(_tenantId, SubscriptionTier.Free);

        // Act
        var upgraded = await _service.UpgradeSubscriptionAsync(_tenantId, SubscriptionTier.Professional);

        // Assert
        upgraded.AmountCents.Should().Be(9900); // Professional tier price
    }

    [Fact]
    public async Task UpgradeSubscriptionAsync_WhenNoSubscription_ShouldThrowException()
    {
        // Act
        Func<Task> act = async () => await _service.UpgradeSubscriptionAsync(_tenantId, SubscriptionTier.Professional);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*No active subscription*");
    }

    [Fact]
    public async Task UpgradeSubscriptionAsync_ToSameTier_ShouldThrowException()
    {
        // Arrange
        var tenant = CreateTestTenant();
        _mockTenantRepository
            .Setup(x => x.GetByIdAsync(_tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);

        _mockTenantRepository
            .Setup(x => x.UpdateAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tenant t, CancellationToken _) => t);

        await _service.CreateSubscriptionAsync(_tenantId, SubscriptionTier.Professional);

        // Act
        Func<Task> act = async () => await _service.UpgradeSubscriptionAsync(_tenantId, SubscriptionTier.Professional);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Cannot upgrade*");
    }

    [Fact]
    public async Task UpgradeSubscriptionAsync_ToLowerTier_ShouldThrowException()
    {
        // Arrange
        var tenant = CreateTestTenant();
        _mockTenantRepository
            .Setup(x => x.GetByIdAsync(_tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);

        _mockTenantRepository
            .Setup(x => x.UpdateAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tenant t, CancellationToken _) => t);

        await _service.CreateSubscriptionAsync(_tenantId, SubscriptionTier.Professional);

        // Act
        Func<Task> act = async () => await _service.UpgradeSubscriptionAsync(_tenantId, SubscriptionTier.Starter);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Cannot upgrade*");
    }

    #endregion

    #region DowngradeSubscriptionAsync Tests

    [Theory]
    [InlineData(SubscriptionTier.Enterprise, SubscriptionTier.Professional)]
    [InlineData(SubscriptionTier.Professional, SubscriptionTier.Starter)]
    [InlineData(SubscriptionTier.Starter, SubscriptionTier.Free)]
    public async Task DowngradeSubscriptionAsync_WithValidDowngrade_ShouldSucceed(SubscriptionTier fromTier, SubscriptionTier toTier)
    {
        // Arrange
        var tenant = CreateTestTenant();
        _mockTenantRepository
            .Setup(x => x.GetByIdAsync(_tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);

        _mockTenantRepository
            .Setup(x => x.UpdateAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tenant t, CancellationToken _) => t);

        await _service.CreateSubscriptionAsync(_tenantId, fromTier);

        // Act
        var downgraded = await _service.DowngradeSubscriptionAsync(_tenantId, toTier);

        // Assert
        downgraded.Tier.Should().Be(toTier);
    }

    [Fact]
    public async Task DowngradeSubscriptionAsync_ShouldUpdatePrice()
    {
        // Arrange
        var tenant = CreateTestTenant();
        _mockTenantRepository
            .Setup(x => x.GetByIdAsync(_tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);

        _mockTenantRepository
            .Setup(x => x.UpdateAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tenant t, CancellationToken _) => t);

        await _service.CreateSubscriptionAsync(_tenantId, SubscriptionTier.Enterprise);

        // Act
        var downgraded = await _service.DowngradeSubscriptionAsync(_tenantId, SubscriptionTier.Starter);

        // Assert
        downgraded.AmountCents.Should().Be(2900); // Starter tier price
    }

    [Fact]
    public async Task DowngradeSubscriptionAsync_WhenNoSubscription_ShouldThrowException()
    {
        // Act
        Func<Task> act = async () => await _service.DowngradeSubscriptionAsync(_tenantId, SubscriptionTier.Free);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*No active subscription*");
    }

    [Fact]
    public async Task DowngradeSubscriptionAsync_ToSameTier_ShouldThrowException()
    {
        // Arrange
        var tenant = CreateTestTenant();
        _mockTenantRepository
            .Setup(x => x.GetByIdAsync(_tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);

        _mockTenantRepository
            .Setup(x => x.UpdateAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tenant t, CancellationToken _) => t);

        await _service.CreateSubscriptionAsync(_tenantId, SubscriptionTier.Starter);

        // Act
        Func<Task> act = async () => await _service.DowngradeSubscriptionAsync(_tenantId, SubscriptionTier.Starter);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Cannot downgrade*");
    }

    [Fact]
    public async Task DowngradeSubscriptionAsync_ToHigherTier_ShouldThrowException()
    {
        // Arrange
        var tenant = CreateTestTenant();
        _mockTenantRepository
            .Setup(x => x.GetByIdAsync(_tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);

        _mockTenantRepository
            .Setup(x => x.UpdateAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tenant t, CancellationToken _) => t);

        await _service.CreateSubscriptionAsync(_tenantId, SubscriptionTier.Starter);

        // Act
        Func<Task> act = async () => await _service.DowngradeSubscriptionAsync(_tenantId, SubscriptionTier.Professional);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Cannot downgrade*");
    }

    #endregion

    #region SuspendForNonPaymentAsync Tests

    [Fact]
    public async Task SuspendForNonPaymentAsync_WithSubscription_ShouldSuspend()
    {
        // Arrange
        var tenant = CreateTestTenant();
        _mockTenantRepository
            .Setup(x => x.GetByIdAsync(_tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);

        _mockTenantRepository
            .Setup(x => x.UpdateAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tenant t, CancellationToken _) => t);

        await _service.CreateSubscriptionAsync(_tenantId, SubscriptionTier.Professional);

        // Act
        var result = await _service.SuspendForNonPaymentAsync(_tenantId);

        // Assert
        result.Should().BeTrue();

        var subscription = await _service.GetCurrentSubscriptionAsync(_tenantId);
        subscription!.Status.Should().Be("Suspended");
    }

    [Fact]
    public async Task SuspendForNonPaymentAsync_ShouldSuspendTenant()
    {
        // Arrange
        var tenant = CreateTestTenant();
        _mockTenantRepository
            .Setup(x => x.GetByIdAsync(_tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);

        _mockTenantRepository
            .Setup(x => x.UpdateAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tenant t, CancellationToken _) => t);

        await _service.CreateSubscriptionAsync(_tenantId, SubscriptionTier.Professional);

        // Act
        await _service.SuspendForNonPaymentAsync(_tenantId);

        // Assert
        _mockTenantRepository.Verify(
            x => x.UpdateAsync(
                It.Is<Tenant>(t =>
                    t.Status == TenantStatus.Suspended &&
                    t.SuspendedAt != null &&
                    t.Metadata.ContainsKey("suspension_reason")),
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task SuspendForNonPaymentAsync_WithoutSubscription_ShouldStillSuspendTenant()
    {
        // Arrange
        var tenant = CreateTestTenant();
        _mockTenantRepository
            .Setup(x => x.GetByIdAsync(_tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);

        _mockTenantRepository
            .Setup(x => x.UpdateAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tenant t, CancellationToken _) => t);

        // Act
        var result = await _service.SuspendForNonPaymentAsync(_tenantId);

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region GetUsageReportAsync Tests

    [Fact]
    public async Task GetUsageReportAsync_WithStorageService_ShouldCalculateActualUsage()
    {
        // Arrange
        var tenant = CreateTestTenant();
        _mockTenantRepository
            .Setup(x => x.GetByIdAsync(_tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);

        _mockTenantRepository
            .Setup(x => x.UpdateAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tenant t, CancellationToken _) => t);

        await _service.CreateSubscriptionAsync(_tenantId, SubscriptionTier.Professional);

        var bucketName = $"tenant-{_tenantId:N}";
        _mockStorageService
            .Setup(x => x.BucketExistsAsync(bucketName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _mockStorageService
            .Setup(x => x.GetBucketSizeAsync(bucketName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(5L * 1024 * 1024 * 1024); // 5 GB

        var periodStart = DateTime.UtcNow.AddDays(-30);
        var periodEnd = DateTime.UtcNow;

        // Act
        var report = await _service.GetUsageReportAsync(_tenantId, periodStart, periodEnd);

        // Assert
        report.Should().NotBeNull();
        report.TenantId.Should().Be(_tenantId);
        report.StorageUsedGB.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetUsageReportAsync_ShouldCalculateTotalCost()
    {
        // Arrange
        var tenant = CreateTestTenant();
        _mockTenantRepository
            .Setup(x => x.GetByIdAsync(_tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);

        _mockTenantRepository
            .Setup(x => x.UpdateAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tenant t, CancellationToken _) => t);

        await _service.CreateSubscriptionAsync(_tenantId, SubscriptionTier.Professional);

        var periodStart = DateTime.UtcNow.AddDays(-30);
        var periodEnd = DateTime.UtcNow;

        // Act
        var report = await _service.GetUsageReportAsync(_tenantId, periodStart, periodEnd);

        // Assert
        report.TotalCost.Should().BeGreaterThanOrEqualTo(99.00m); // Professional base cost
        report.LineItems.Should().ContainKey("Base Subscription");
        report.LineItems["Base Subscription"].Should().Be(99.00m);
    }

    [Fact]
    public async Task GetUsageReportAsync_ShouldCacheReports()
    {
        // Arrange
        var tenant = CreateTestTenant();
        _mockTenantRepository
            .Setup(x => x.GetByIdAsync(_tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);

        _mockTenantRepository
            .Setup(x => x.UpdateAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tenant t, CancellationToken _) => t);

        await _service.CreateSubscriptionAsync(_tenantId, SubscriptionTier.Starter);

        var periodStart = DateTime.UtcNow.AddDays(-30);
        var periodEnd = DateTime.UtcNow;

        // Act - Generate report twice
        var report1 = await _service.GetUsageReportAsync(_tenantId, periodStart, periodEnd);
        var report2 = await _service.GetUsageReportAsync(_tenantId, periodStart, periodEnd);

        // Assert - Should return same cached report
        report1.GeneratedAt.Should().Be(report2.GeneratedAt);
    }

    [Fact]
    public async Task GetUsageReportAsync_WhenStorageBucketDoesNotExist_ShouldReturnZeroUsage()
    {
        // Arrange
        var tenant = CreateTestTenant();
        _mockTenantRepository
            .Setup(x => x.GetByIdAsync(_tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);

        _mockTenantRepository
            .Setup(x => x.UpdateAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tenant t, CancellationToken _) => t);

        await _service.CreateSubscriptionAsync(_tenantId, SubscriptionTier.Free);

        var bucketName = $"tenant-{_tenantId:N}";
        _mockStorageService
            .Setup(x => x.BucketExistsAsync(bucketName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var periodStart = DateTime.UtcNow.AddDays(-30);
        var periodEnd = DateTime.UtcNow;

        // Act
        var report = await _service.GetUsageReportAsync(_tenantId, periodStart, periodEnd);

        // Assert
        report.StorageUsedGB.Should().Be(0);
    }

    #endregion

    #region GetCurrentSubscriptionAsync Tests

    [Fact]
    public async Task GetCurrentSubscriptionAsync_WhenExists_ShouldReturnSubscription()
    {
        // Arrange
        var tenant = CreateTestTenant();
        _mockTenantRepository
            .Setup(x => x.GetByIdAsync(_tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);

        _mockTenantRepository
            .Setup(x => x.UpdateAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tenant t, CancellationToken _) => t);

        await _service.CreateSubscriptionAsync(_tenantId, SubscriptionTier.Professional);

        // Act
        var subscription = await _service.GetCurrentSubscriptionAsync(_tenantId);

        // Assert
        subscription.Should().NotBeNull();
        subscription!.TenantId.Should().Be(_tenantId);
        subscription.Tier.Should().Be(SubscriptionTier.Professional);
    }

    [Fact]
    public async Task GetCurrentSubscriptionAsync_WhenNotExists_ShouldReturnNull()
    {
        // Act
        var subscription = await _service.GetCurrentSubscriptionAsync(_tenantId);

        // Assert
        subscription.Should().BeNull();
    }

    #endregion

    #region Helper Methods

    private Tenant CreateTestTenant()
    {
        return new Tenant
        {
            TenantId = _tenantId,
            Name = "Test Tenant",
            Subdomain = "test",
            Status = TenantStatus.Active,
            Tier = SubscriptionTier.Free,
            ResourceQuota = new ResourceQuota
            {
                MaxWebsites = 5,
                StorageQuotaGB = 10,
                BandwidthQuotaGB = 100
            },
            Metadata = new Dictionary<string, string>()
        };
    }

    #endregion
}
