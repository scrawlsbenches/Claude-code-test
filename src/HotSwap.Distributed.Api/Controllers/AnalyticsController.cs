using HotSwap.Distributed.Api.Models;
using HotSwap.Distributed.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotSwap.Distributed.Api.Controllers;

/// <summary>
/// API for tenant analytics and usage reporting.
/// </summary>
[ApiController]
[Route("api/v1/analytics")]
[Produces("application/json")]
[Authorize]
public class AnalyticsController : ControllerBase
{
    private readonly IUsageTrackingService _usageTracking;
    private readonly ICostAttributionService _costAttribution;
    private readonly ISubscriptionService _subscriptionService;
    private readonly ITenantContextService _tenantContext;
    private readonly ILogger<AnalyticsController> _logger;

    public AnalyticsController(
        IUsageTrackingService usageTracking,
        ICostAttributionService costAttribution,
        ISubscriptionService subscriptionService,
        ITenantContextService tenantContext,
        ILogger<AnalyticsController> logger)
    {
        _usageTracking = usageTracking;
        _costAttribution = costAttribution;
        _subscriptionService = subscriptionService;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    /// <summary>
    /// Gets traffic analytics for a website.
    /// </summary>
    [HttpGet("websites/{websiteId}/traffic")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetWebsiteTraffic(
        Guid websiteId,
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        CancellationToken cancellationToken)
    {
        var start = startDate ?? DateTime.UtcNow.AddDays(-30);
        var end = endDate ?? DateTime.UtcNow;

        var analytics = await _usageTracking.GetTrafficAnalyticsAsync(
            websiteId, start, end, cancellationToken);

        return Ok(analytics);
    }

    /// <summary>
    /// Gets usage report for the current tenant.
    /// </summary>
    [HttpGet("usage")]
    [ProducesResponseType(typeof(UsageReportResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUsageReport(
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.GetCurrentTenantId();
        if (tenantId == null)
            return BadRequest(new ErrorResponse { Error = "Tenant context required" });

        var start = startDate ?? DateTime.UtcNow.AddMonths(-1);
        var end = endDate ?? DateTime.UtcNow;

        var report = await _subscriptionService.GetUsageReportAsync(
            tenantId.Value, start, end, cancellationToken);

        var response = new UsageReportResponse
        {
            TenantId = report.TenantId,
            PeriodStart = report.PeriodStart,
            PeriodEnd = report.PeriodEnd,
            StorageUsedGB = report.StorageUsedGB,
            BandwidthUsedGB = report.BandwidthUsedGB,
            DeploymentsCount = report.DeploymentsCount,
            TotalCost = report.TotalCost,
            LineItems = report.LineItems
        };

        return Ok(response);
    }

    /// <summary>
    /// Gets cost breakdown for the current tenant.
    /// </summary>
    [HttpGet("costs")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCostBreakdown(
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.GetCurrentTenantId();
        if (tenantId == null)
            return BadRequest(new ErrorResponse { Error = "Tenant context required" });

        var start = startDate ?? DateTime.UtcNow.AddMonths(-1);
        var end = endDate ?? DateTime.UtcNow;

        var costs = await _costAttribution.GetCostBreakdownAsync(
            tenantId.Value, start, end, cancellationToken);

        return Ok(costs);
    }
}
