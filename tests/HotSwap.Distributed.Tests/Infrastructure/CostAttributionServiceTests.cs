using FluentAssertions;
using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Infrastructure.Analytics;
using HotSwap.Distributed.Infrastructure.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace HotSwap.Distributed.Tests.Infrastructure;

public class CostAttributionServiceTests
{
    private readonly Mock<IQuotaService> _mockQuotaService;
    private readonly Mock<ILogger<CostAttributionService>> _mockLogger;
    private readonly CostAttributionService _service;

    public CostAttributionServiceTests()
    {
        _mockQuotaService = new Mock<IQuotaService>();
        _mockLogger = new Mock<ILogger<CostAttributionService>>();
        _service = new CostAttributionService(_mockQuotaService.Object, _mockLogger.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullQuotaService_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new CostAttributionService(null!, _mockLogger.Object);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("quotaService");
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new CostAttributionService(_mockQuotaService.Object, null!);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    #endregion

    #region CalculateCostsAsync Tests

    [Fact]
    public async Task CalculateCostsAsync_WithUsageData_CalculatesCorrectCosts()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var startDate = DateTime.UtcNow.AddDays(-30);
        var endDate = DateTime.UtcNow;
        var hours = (endDate - startDate).TotalHours;

        // Setup: 100 GB storage, 50 GB bandwidth
        _mockQuotaService.Setup(q => q.GetCurrentUsageAsync(
            tenantId,
            ResourceType.Storage,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(100);

        _mockQuotaService.Setup(q => q.GetCurrentUsageAsync(
            tenantId,
            ResourceType.Bandwidth,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(50);

        // Act
        var result = await _service.CalculateCostsAsync(tenantId, startDate, endDate);

        // Assert
        result.Should().NotBeNull();
        result.TenantId.Should().Be(tenantId);
        result.PeriodStart.Should().Be(startDate);
        result.PeriodEnd.Should().Be(endDate);

        // Cost calculations: Compute: $0.05/hour, Storage: $0.10/GB, Bandwidth: $0.09/GB
        var expectedComputeCost = (decimal)hours * 0.05m;
        var expectedStorageCost = 100 * 0.10m; // $10
        var expectedBandwidthCost = 50 * 0.09m; // $4.50
        var expectedTotalCost = expectedComputeCost + expectedStorageCost + expectedBandwidthCost;

        result.ComputeCost.Should().Be(expectedComputeCost);
        result.StorageCost.Should().Be(expectedStorageCost);
        result.BandwidthCost.Should().Be(expectedBandwidthCost);
        result.TotalCost.Should().Be(expectedTotalCost);
    }

    [Fact]
    public async Task CalculateCostsAsync_WithNoUsage_ReturnsOnlyComputeCost()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var startDate = DateTime.UtcNow.AddHours(-24);
        var endDate = DateTime.UtcNow;
        var hours = (endDate - startDate).TotalHours;

        // Setup: No storage or bandwidth usage
        _mockQuotaService.Setup(q => q.GetCurrentUsageAsync(
            tenantId,
            ResourceType.Storage,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        _mockQuotaService.Setup(q => q.GetCurrentUsageAsync(
            tenantId,
            ResourceType.Bandwidth,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        // Act
        var result = await _service.CalculateCostsAsync(tenantId, startDate, endDate);

        // Assert
        result.Should().NotBeNull();
        result.StorageCost.Should().Be(0);
        result.BandwidthCost.Should().Be(0);
        result.ComputeCost.Should().BeGreaterThan(0);
        result.TotalCost.Should().Be(result.ComputeCost);
    }

    [Fact]
    public async Task CalculateCostsAsync_WithOneHourPeriod_CalculatesCorrectComputeCost()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var startDate = DateTime.UtcNow;
        var endDate = startDate.AddHours(1);

        _mockQuotaService.Setup(q => q.GetCurrentUsageAsync(
            tenantId,
            It.IsAny<ResourceType>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        // Act
        var result = await _service.CalculateCostsAsync(tenantId, startDate, endDate);

        // Assert
        result.ComputeCost.Should().Be(0.05m); // $0.05 per hour
        result.TotalCost.Should().Be(0.05m);
    }

    [Fact]
    public async Task CalculateCostsAsync_WithLargeUsage_HandlesCorrectly()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var startDate = DateTime.UtcNow.AddDays(-365);
        var endDate = DateTime.UtcNow;
        var hours = (endDate - startDate).TotalHours;

        // Setup: 1000 GB storage, 5000 GB bandwidth (large usage)
        _mockQuotaService.Setup(q => q.GetCurrentUsageAsync(
            tenantId,
            ResourceType.Storage,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(1000);

        _mockQuotaService.Setup(q => q.GetCurrentUsageAsync(
            tenantId,
            ResourceType.Bandwidth,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(5000);

        // Act
        var result = await _service.CalculateCostsAsync(tenantId, startDate, endDate);

        // Assert
        result.Should().NotBeNull();
        result.StorageCost.Should().Be(100m); // 1000 * $0.10
        result.BandwidthCost.Should().Be(450m); // 5000 * $0.09
        result.TotalCost.Should().BeGreaterThan(550m); // At least storage + bandwidth
    }

    #endregion

    #region GetCostBreakdownAsync Tests

    [Fact]
    public async Task GetCostBreakdownAsync_ReturnsCorrectBreakdown()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var startDate = DateTime.UtcNow.AddDays(-7);
        var endDate = DateTime.UtcNow;

        // Setup: 50 GB storage, 100 GB bandwidth
        _mockQuotaService.Setup(q => q.GetCurrentUsageAsync(
            tenantId,
            ResourceType.Storage,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(50);

        _mockQuotaService.Setup(q => q.GetCurrentUsageAsync(
            tenantId,
            ResourceType.Bandwidth,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(100);

        // Act
        var breakdown = await _service.GetCostBreakdownAsync(tenantId, startDate, endDate);

        // Assert
        breakdown.Should().NotBeNull();
        breakdown.Should().ContainKey("Compute");
        breakdown.Should().ContainKey("Storage");
        breakdown.Should().ContainKey("Bandwidth");

        breakdown["Storage"].Should().Be(5.0m); // 50 * $0.10
        breakdown["Bandwidth"].Should().Be(9.0m); // 100 * $0.09
        breakdown["Compute"].Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetCostBreakdownAsync_WithZeroUsage_ReturnsComputeOnly()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var startDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = startDate.AddHours(1);

        _mockQuotaService.Setup(q => q.GetCurrentUsageAsync(
            tenantId,
            It.IsAny<ResourceType>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        // Act
        var breakdown = await _service.GetCostBreakdownAsync(tenantId, startDate, endDate);

        // Assert
        breakdown.Should().NotBeNull();
        breakdown["Compute"].Should().Be(0.05m);
        breakdown["Storage"].Should().Be(0);
        breakdown["Bandwidth"].Should().Be(0);
    }

    [Fact]
    public async Task GetCostBreakdownAsync_ConsistentWithCalculateCosts()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var startDate = DateTime.UtcNow.AddDays(-1);
        var endDate = DateTime.UtcNow;

        _mockQuotaService.Setup(q => q.GetCurrentUsageAsync(
            tenantId,
            ResourceType.Storage,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(75);

        _mockQuotaService.Setup(q => q.GetCurrentUsageAsync(
            tenantId,
            ResourceType.Bandwidth,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(150);

        // Act
        var report = await _service.CalculateCostsAsync(tenantId, startDate, endDate);
        var breakdown = await _service.GetCostBreakdownAsync(tenantId, startDate, endDate);

        // Assert - Breakdown values should match report values
        breakdown["Compute"].Should().Be(report.ComputeCost);
        breakdown["Storage"].Should().Be(report.StorageCost);
        breakdown["Bandwidth"].Should().Be(report.BandwidthCost);
    }

    #endregion
}
