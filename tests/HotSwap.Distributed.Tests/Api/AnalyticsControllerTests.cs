using FluentAssertions;
using HotSwap.Distributed.Api.Controllers;
using HotSwap.Distributed.Api.Models;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace HotSwap.Distributed.Tests.Api;

public class AnalyticsControllerTests
{
    private readonly Mock<IUsageTrackingService> _mockUsageTracking;
    private readonly Mock<ICostAttributionService> _mockCostAttribution;
    private readonly Mock<ISubscriptionService> _mockSubscriptionService;
    private readonly Mock<ITenantContextService> _mockTenantContext;
    private readonly AnalyticsController _controller;
    private readonly Guid _testTenantId = Guid.NewGuid();

    public AnalyticsControllerTests()
    {
        _mockUsageTracking = new Mock<IUsageTrackingService>();
        _mockCostAttribution = new Mock<ICostAttributionService>();
        _mockSubscriptionService = new Mock<ISubscriptionService>();
        _mockTenantContext = new Mock<ITenantContextService>();

        _controller = new AnalyticsController(
            _mockUsageTracking.Object,
            _mockCostAttribution.Object,
            _mockSubscriptionService.Object,
            _mockTenantContext.Object,
            NullLogger<AnalyticsController>.Instance);
    }

    private TrafficAnalytics CreateTestTrafficAnalytics(Guid websiteId)
    {
        return new TrafficAnalytics
        {
            WebsiteId = websiteId,
            TotalPageViews = 1000,
            UniqueVisitors = 250,
            TopPages = new Dictionary<string, long>
            {
                { "/home", 500 },
                { "/about", 300 },
                { "/contact", 200 }
            },
            TrafficByDay = new Dictionary<string, long>
            {
                { "2024-01-01", 100 },
                { "2024-01-02", 150 }
            }
        };
    }

    private UsageReport CreateTestUsageReport()
    {
        return new UsageReport
        {
            TenantId = _testTenantId,
            PeriodStart = DateTime.UtcNow.AddMonths(-1),
            PeriodEnd = DateTime.UtcNow,
            StorageUsedGB = 50,
            BandwidthUsedGB = 100,
            DeploymentsCount = 25,
            TotalCost = 125.50m,
            LineItems = new Dictionary<string, decimal>
            {
                { "Storage", 25.00m },
                { "Bandwidth", 50.00m },
                { "Deployments", 50.50m }
            }
        };
    }

    #region GetWebsiteTraffic Tests

    [Fact]
    public async Task GetWebsiteTraffic_WithValidRequest_ReturnsOk()
    {
        // Arrange
        var websiteId = Guid.NewGuid();
        var startDate = DateTime.UtcNow.AddDays(-7);
        var endDate = DateTime.UtcNow;
        var analytics = CreateTestTrafficAnalytics(websiteId);

        _mockUsageTracking.Setup(x => x.GetTrafficAnalyticsAsync(
                websiteId,
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(analytics);

        // Act
        var result = await _controller.GetWebsiteTraffic(
            websiteId, startDate, endDate, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<TrafficAnalytics>().Subject;
        response.WebsiteId.Should().Be(websiteId);
        response.TotalPageViews.Should().Be(1000);
        response.UniqueVisitors.Should().Be(250);
        response.TopPages.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetWebsiteTraffic_WithoutDates_UsesDefaults()
    {
        // Arrange
        var websiteId = Guid.NewGuid();
        var analytics = CreateTestTrafficAnalytics(websiteId);

        DateTime? capturedStartDate = null;
        DateTime? capturedEndDate = null;

        _mockUsageTracking.Setup(x => x.GetTrafficAnalyticsAsync(
                websiteId,
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .Callback<Guid, DateTime, DateTime, CancellationToken>(
                (id, start, end, ct) =>
                {
                    capturedStartDate = start;
                    capturedEndDate = end;
                })
            .ReturnsAsync(analytics);

        // Act
        var result = await _controller.GetWebsiteTraffic(
            websiteId, null, null, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        capturedStartDate.Should().NotBeNull();
        capturedEndDate.Should().NotBeNull();

        // Should default to approximately 30 days ago and now
        var expectedStart = DateTime.UtcNow.AddDays(-30);
        var expectedEnd = DateTime.UtcNow;

        capturedStartDate!.Value.Should().BeCloseTo(expectedStart, TimeSpan.FromSeconds(5));
        capturedEndDate!.Value.Should().BeCloseTo(expectedEnd, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task GetWebsiteTraffic_WithStartDateOnly_UsesDefaultEndDate()
    {
        // Arrange
        var websiteId = Guid.NewGuid();
        var startDate = DateTime.UtcNow.AddDays(-14);
        var analytics = CreateTestTrafficAnalytics(websiteId);

        DateTime? capturedEndDate = null;

        _mockUsageTracking.Setup(x => x.GetTrafficAnalyticsAsync(
                websiteId,
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .Callback<Guid, DateTime, DateTime, CancellationToken>(
                (id, start, end, ct) => capturedEndDate = end)
            .ReturnsAsync(analytics);

        // Act
        var result = await _controller.GetWebsiteTraffic(
            websiteId, startDate, null, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        capturedEndDate.Should().NotBeNull();
        capturedEndDate!.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    #endregion

    #region GetUsageReport Tests

    [Fact]
    public async Task GetUsageReport_WithValidTenantContext_ReturnsOk()
    {
        // Arrange
        var startDate = DateTime.UtcNow.AddMonths(-1);
        var endDate = DateTime.UtcNow;
        var usageReport = CreateTestUsageReport();

        _mockTenantContext.Setup(x => x.GetCurrentTenantId())
            .Returns(_testTenantId);

        _mockSubscriptionService.Setup(x => x.GetUsageReportAsync(
                _testTenantId,
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(usageReport);

        // Act
        var result = await _controller.GetUsageReport(
            startDate, endDate, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<UsageReportResponse>().Subject;
        response.TenantId.Should().Be(_testTenantId);
        response.StorageUsedGB.Should().Be(50);
        response.BandwidthUsedGB.Should().Be(100);
        response.DeploymentsCount.Should().Be(25);
        response.TotalCost.Should().Be(125.50m);
        response.LineItems.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetUsageReport_WithNoTenantContext_ReturnsBadRequest()
    {
        // Arrange
        _mockTenantContext.Setup(x => x.GetCurrentTenantId())
            .Returns((Guid?)null);

        // Act
        var result = await _controller.GetUsageReport(
            DateTime.UtcNow.AddMonths(-1), DateTime.UtcNow, CancellationToken.None);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var errorResponse = badRequestResult.Value.Should().BeOfType<ErrorResponse>().Subject;
        errorResponse.Error.Should().Be("Tenant context required");
    }

    [Fact]
    public async Task GetUsageReport_WithoutDates_UsesDefaults()
    {
        // Arrange
        var usageReport = CreateTestUsageReport();

        _mockTenantContext.Setup(x => x.GetCurrentTenantId())
            .Returns(_testTenantId);

        DateTime? capturedStartDate = null;
        DateTime? capturedEndDate = null;

        _mockSubscriptionService.Setup(x => x.GetUsageReportAsync(
                _testTenantId,
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .Callback<Guid, DateTime, DateTime, CancellationToken>(
                (id, start, end, ct) =>
                {
                    capturedStartDate = start;
                    capturedEndDate = end;
                })
            .ReturnsAsync(usageReport);

        // Act
        var result = await _controller.GetUsageReport(null, null, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        capturedStartDate.Should().NotBeNull();
        capturedEndDate.Should().NotBeNull();

        // Should default to approximately 1 month ago and now
        var expectedStart = DateTime.UtcNow.AddMonths(-1);
        var expectedEnd = DateTime.UtcNow;

        capturedStartDate!.Value.Should().BeCloseTo(expectedStart, TimeSpan.FromSeconds(5));
        capturedEndDate!.Value.Should().BeCloseTo(expectedEnd, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task GetUsageReport_MapsAllFieldsCorrectly()
    {
        // Arrange
        var usageReport = CreateTestUsageReport();
        usageReport.PeriodStart = new DateTime(2024, 1, 1);
        usageReport.PeriodEnd = new DateTime(2024, 1, 31);

        _mockTenantContext.Setup(x => x.GetCurrentTenantId())
            .Returns(_testTenantId);

        _mockSubscriptionService.Setup(x => x.GetUsageReportAsync(
                It.IsAny<Guid>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(usageReport);

        // Act
        var result = await _controller.GetUsageReport(null, null, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<UsageReportResponse>().Subject;

        response.TenantId.Should().Be(usageReport.TenantId);
        response.PeriodStart.Should().Be(usageReport.PeriodStart);
        response.PeriodEnd.Should().Be(usageReport.PeriodEnd);
        response.StorageUsedGB.Should().Be(usageReport.StorageUsedGB);
        response.BandwidthUsedGB.Should().Be(usageReport.BandwidthUsedGB);
        response.DeploymentsCount.Should().Be(usageReport.DeploymentsCount);
        response.TotalCost.Should().Be(usageReport.TotalCost);
        response.LineItems.Should().BeEquivalentTo(usageReport.LineItems);
    }

    #endregion

    #region GetCostBreakdown Tests

    [Fact]
    public async Task GetCostBreakdown_WithValidTenantContext_ReturnsOk()
    {
        // Arrange
        var startDate = DateTime.UtcNow.AddMonths(-1);
        var endDate = DateTime.UtcNow;
        var costBreakdown = new Dictionary<string, decimal>
        {
            { "Compute", 50.00m },
            { "Storage", 25.00m },
            { "Bandwidth", 30.00m }
        };

        _mockTenantContext.Setup(x => x.GetCurrentTenantId())
            .Returns(_testTenantId);

        _mockCostAttribution.Setup(x => x.GetCostBreakdownAsync(
                _testTenantId,
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(costBreakdown);

        // Act
        var result = await _controller.GetCostBreakdown(
            startDate, endDate, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<Dictionary<string, decimal>>().Subject;
        response.Should().HaveCount(3);
        response["Compute"].Should().Be(50.00m);
        response["Storage"].Should().Be(25.00m);
        response["Bandwidth"].Should().Be(30.00m);
    }

    [Fact]
    public async Task GetCostBreakdown_WithNoTenantContext_ReturnsBadRequest()
    {
        // Arrange
        _mockTenantContext.Setup(x => x.GetCurrentTenantId())
            .Returns((Guid?)null);

        // Act
        var result = await _controller.GetCostBreakdown(
            DateTime.UtcNow.AddMonths(-1), DateTime.UtcNow, CancellationToken.None);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var errorResponse = badRequestResult.Value.Should().BeOfType<ErrorResponse>().Subject;
        errorResponse.Error.Should().Be("Tenant context required");
    }

    [Fact]
    public async Task GetCostBreakdown_WithoutDates_UsesDefaults()
    {
        // Arrange
        var costBreakdown = new Dictionary<string, decimal>
        {
            { "Compute", 50.00m }
        };

        _mockTenantContext.Setup(x => x.GetCurrentTenantId())
            .Returns(_testTenantId);

        DateTime? capturedStartDate = null;
        DateTime? capturedEndDate = null;

        _mockCostAttribution.Setup(x => x.GetCostBreakdownAsync(
                _testTenantId,
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .Callback<Guid, DateTime, DateTime, CancellationToken>(
                (id, start, end, ct) =>
                {
                    capturedStartDate = start;
                    capturedEndDate = end;
                })
            .ReturnsAsync(costBreakdown);

        // Act
        var result = await _controller.GetCostBreakdown(null, null, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        capturedStartDate.Should().NotBeNull();
        capturedEndDate.Should().NotBeNull();

        // Should default to approximately 1 month ago and now
        var expectedStart = DateTime.UtcNow.AddMonths(-1);
        var expectedEnd = DateTime.UtcNow;

        capturedStartDate!.Value.Should().BeCloseTo(expectedStart, TimeSpan.FromSeconds(5));
        capturedEndDate!.Value.Should().BeCloseTo(expectedEnd, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task GetCostBreakdown_WithEmptyBreakdown_ReturnsEmptyDictionary()
    {
        // Arrange
        var costBreakdown = new Dictionary<string, decimal>();

        _mockTenantContext.Setup(x => x.GetCurrentTenantId())
            .Returns(_testTenantId);

        _mockCostAttribution.Setup(x => x.GetCostBreakdownAsync(
                It.IsAny<Guid>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(costBreakdown);

        // Act
        var result = await _controller.GetCostBreakdown(null, null, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<Dictionary<string, decimal>>().Subject;
        response.Should().BeEmpty();
    }

    #endregion
}
