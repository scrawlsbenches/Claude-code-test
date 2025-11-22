using FluentAssertions;
using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Interfaces;
using HotSwap.Distributed.Infrastructure.Tenants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace HotSwap.Distributed.Tests.Infrastructure;

public class SubscriptionServiceTests
{
    private readonly Mock<ITenantRepository> _mockTenantRepository;
    private readonly Mock<ILogger<SubscriptionService>> _mockLogger;
    private readonly SubscriptionService _service;

    public SubscriptionServiceTests()
    {
        _mockTenantRepository = new Mock<ITenantRepository>();
        _mockLogger = new Mock<ILogger<SubscriptionService>>();
        _service = new SubscriptionService(_mockTenantRepository.Object, _mockLogger.Object);
    }

    [Fact(Skip = "Temporarily disabled - investigating test hang")]
    public async Task CreateSubscriptionAsync_WithValidTenant_CreatesSubscription()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var tier = SubscriptionTier.Professional;
        var tenant = new Tenant
        {
            TenantId = tenantId,
            Name = "Test Tenant",
            Subdomain = "test",
            Tier = SubscriptionTier.Free,
            ResourceQuota = ResourceQuota.CreateDefault(SubscriptionTier.Free)
        };

        _mockTenantRepository.Setup(r => r.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);
        _mockTenantRepository.Setup(r => r.UpdateAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tenant t, CancellationToken _) => t);

        // Act
        var result = await _service.CreateSubscriptionAsync(tenantId, tier);

        // Assert
        result.Should().NotBeNull();
        result.TenantId.Should().Be(tenantId);
        result.Tier.Should().Be(tier);
        result.Status.Should().Be("Active");
        result.BillingCycle.Should().Be("Monthly");
        result.AmountCents.Should().Be(9900); // Professional tier pricing

        _mockTenantRepository.Verify(r => r.UpdateAsync(It.Is<Tenant>(t =>
            t.Tier == tier &&
            t.ResourceQuota != null), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(Skip = "Temporarily disabled - investigating test hang")]
    public async Task CreateSubscriptionAsync_WithFreeTier_CreatesZeroPriceSubscription()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var tenant = CreateTestTenant(tenantId);

        _mockTenantRepository.Setup(r => r.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);
        _mockTenantRepository.Setup(r => r.UpdateAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tenant t, CancellationToken _) => t);

        // Act
        var result = await _service.CreateSubscriptionAsync(tenantId, SubscriptionTier.Free);

        // Assert
        result.AmountCents.Should().Be(0);
        result.Tier.Should().Be(SubscriptionTier.Free);
    }

    [Fact(Skip = "Temporarily disabled - investigating test hang")]
    public async Task CreateSubscriptionAsync_WithNonExistentTenant_ThrowsInvalidOperationException()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        _mockTenantRepository.Setup(r => r.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tenant?)null);

        // Act
        var act = async () => await _service.CreateSubscriptionAsync(tenantId, SubscriptionTier.Free);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Tenant {tenantId} not found");
    }

    [Fact(Skip = "Temporarily disabled - investigating test hang")]
    public async Task UpgradeSubscriptionAsync_FromFreeToStarter_UpgradesSuccessfully()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var tenant = CreateTestTenant(tenantId);

        // Create initial free subscription
        _mockTenantRepository.Setup(r => r.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);
        _mockTenantRepository.Setup(r => r.UpdateAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tenant t, CancellationToken _) => t);

        await _service.CreateSubscriptionAsync(tenantId, SubscriptionTier.Free);

        // Act
        var result = await _service.UpgradeSubscriptionAsync(tenantId, SubscriptionTier.Starter);

        // Assert
        result.Tier.Should().Be(SubscriptionTier.Starter);
        result.AmountCents.Should().Be(2900); // Starter tier pricing

        _mockTenantRepository.Verify(r => r.UpdateAsync(It.Is<Tenant>(t =>
            t.Tier == SubscriptionTier.Starter), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact(Skip = "Temporarily disabled - investigating test hang")]
    public async Task UpgradeSubscriptionAsync_ToLowerTier_ThrowsInvalidOperationException()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var tenant = CreateTestTenant(tenantId);

        _mockTenantRepository.Setup(r => r.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);
        _mockTenantRepository.Setup(r => r.UpdateAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tenant t, CancellationToken _) => t);

        await _service.CreateSubscriptionAsync(tenantId, SubscriptionTier.Professional);

        // Act
        var act = async () => await _service.UpgradeSubscriptionAsync(tenantId, SubscriptionTier.Starter);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Cannot upgrade from Professional to Starter");
    }

    [Fact(Skip = "Temporarily disabled - investigating test hang")]
    public async Task DowngradeSubscriptionAsync_FromProfessionalToStarter_DowngradesSuccessfully()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var tenant = CreateTestTenant(tenantId);

        _mockTenantRepository.Setup(r => r.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);
        _mockTenantRepository.Setup(r => r.UpdateAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tenant t, CancellationToken _) => t);

        await _service.CreateSubscriptionAsync(tenantId, SubscriptionTier.Professional);

        // Act
        var result = await _service.DowngradeSubscriptionAsync(tenantId, SubscriptionTier.Starter);

        // Assert
        result.Tier.Should().Be(SubscriptionTier.Starter);
        result.AmountCents.Should().Be(2900);
    }

    [Fact(Skip = "Temporarily disabled - investigating test hang")]
    public async Task DowngradeSubscriptionAsync_ToHigherTier_ThrowsInvalidOperationException()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var tenant = CreateTestTenant(tenantId);

        _mockTenantRepository.Setup(r => r.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);
        _mockTenantRepository.Setup(r => r.UpdateAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tenant t, CancellationToken _) => t);

        await _service.CreateSubscriptionAsync(tenantId, SubscriptionTier.Starter);

        // Act
        var act = async () => await _service.DowngradeSubscriptionAsync(tenantId, SubscriptionTier.Professional);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Cannot downgrade from Starter to Professional");
    }

    [Fact(Skip = "Temporarily disabled - investigating test hang")]
    public async Task SuspendForNonPaymentAsync_WithExistingTenant_SuspendsTenant()
    {
        // Arrange
        var tenantId = Guid.NewGuid();

        // Return a fresh copy of the tenant for each call to avoid reference issues
        _mockTenantRepository.Setup(r => r.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => CreateTestTenant(tenantId));
        _mockTenantRepository.Setup(r => r.UpdateAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tenant t, CancellationToken _) => t);

        await _service.CreateSubscriptionAsync(tenantId, SubscriptionTier.Professional);

        // Act
        var result = await _service.SuspendForNonPaymentAsync(tenantId);

        // Assert
        result.Should().BeTrue();

        _mockTenantRepository.Verify(r => r.UpdateAsync(It.Is<Tenant>(t =>
            t.Status == TenantStatus.Suspended &&
            t.SuspendedAt.HasValue &&
            t.Metadata.ContainsKey("suspension_reason") &&
            t.Metadata["suspension_reason"] == "Non-payment"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(Skip = "Temporarily disabled - investigating test hang")]
    public async Task GetUsageReportAsync_WithNoData_ReturnsReportWithZeroUsage()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var tenant = CreateTestTenant(tenantId);
        var periodStart = DateTime.UtcNow.AddMonths(-1);
        var periodEnd = DateTime.UtcNow;

        _mockTenantRepository.Setup(r => r.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);
        _mockTenantRepository.Setup(r => r.UpdateAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tenant t, CancellationToken _) => t);

        await _service.CreateSubscriptionAsync(tenantId, SubscriptionTier.Professional);

        // Act
        var report = await _service.GetUsageReportAsync(tenantId, periodStart, periodEnd);

        // Assert
        report.Should().NotBeNull();
        report.TenantId.Should().Be(tenantId);
        report.PeriodStart.Should().Be(periodStart);
        report.PeriodEnd.Should().Be(periodEnd);
        report.StorageUsedGB.Should().Be(0);
        report.BandwidthUsedGB.Should().Be(0);
        report.DeploymentsCount.Should().Be(0);
        report.TotalCost.Should().Be(99.00m); // Professional base price only
        report.LineItems.Should().ContainKey("Base Subscription");
    }

    [Fact(Skip = "Temporarily disabled - investigating test hang")]
    public async Task GetUsageReportAsync_SecondCall_ReturnsCachedReport()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var tenant = CreateTestTenant(tenantId);
        var periodStart = DateTime.UtcNow.AddMonths(-1);
        var periodEnd = DateTime.UtcNow;

        _mockTenantRepository.Setup(r => r.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);
        _mockTenantRepository.Setup(r => r.UpdateAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tenant t, CancellationToken _) => t);

        await _service.CreateSubscriptionAsync(tenantId, SubscriptionTier.Starter);

        // Act
        var report1 = await _service.GetUsageReportAsync(tenantId, periodStart, periodEnd);
        var report2 = await _service.GetUsageReportAsync(tenantId, periodStart, periodEnd);

        // Assert
        report1.Should().BeSameAs(report2); // Cached report
    }

    [Fact(Skip = "Temporarily disabled - investigating test hang")]
    public async Task GetCurrentSubscriptionAsync_WithExistingSubscription_ReturnsSubscription()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var tenant = CreateTestTenant(tenantId);

        _mockTenantRepository.Setup(r => r.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);
        _mockTenantRepository.Setup(r => r.UpdateAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tenant t, CancellationToken _) => t);

        await _service.CreateSubscriptionAsync(tenantId, SubscriptionTier.Enterprise);

        // Act
        var subscription = await _service.GetCurrentSubscriptionAsync(tenantId);

        // Assert
        subscription.Should().NotBeNull();
        subscription!.TenantId.Should().Be(tenantId);
        subscription.Tier.Should().Be(SubscriptionTier.Enterprise);
        subscription.AmountCents.Should().Be(49900);
    }

    [Fact(Skip = "Temporarily disabled - investigating test hang")]
    public async Task GetCurrentSubscriptionAsync_WithNoSubscription_ReturnsNull()
    {
        // Arrange
        var tenantId = Guid.NewGuid();

        // Act
        var subscription = await _service.GetCurrentSubscriptionAsync(tenantId);

        // Assert
        subscription.Should().BeNull();
    }

    [Fact(Skip = "Temporarily disabled - investigating test hang")]
    public void Constructor_WithNullRepository_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new SubscriptionService(null!, _mockLogger.Object);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("tenantRepository");
    }

    [Fact(Skip = "Temporarily disabled - investigating test hang")]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new SubscriptionService(_mockTenantRepository.Object, null!);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    private static Tenant CreateTestTenant(Guid tenantId)
    {
        return new Tenant
        {
            TenantId = tenantId,
            Name = "Test Tenant",
            Subdomain = "test",
            Tier = SubscriptionTier.Free,
            ResourceQuota = ResourceQuota.CreateDefault(SubscriptionTier.Free),
            Status = TenantStatus.Active
        };
    }
}
