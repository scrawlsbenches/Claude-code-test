using HotSwap.Distributed.Infrastructure.Interfaces;
using Microsoft.Extensions.Logging;

namespace HotSwap.Distributed.Infrastructure.Analytics;

/// <summary>
/// Service for cost attribution and billing analytics.
/// </summary>
public class CostAttributionService : ICostAttributionService
{
    private readonly IQuotaService _quotaService;
    private readonly ILogger<CostAttributionService> _logger;

    // Pricing configuration (USD)
    private const decimal ComputeCostPerHour = 0.05m;
    private const decimal StorageCostPerGB = 0.10m;
    private const decimal BandwidthCostPerGB = 0.09m;

    public CostAttributionService(
        IQuotaService quotaService,
        ILogger<CostAttributionService> logger)
    {
        _quotaService = quotaService ?? throw new ArgumentNullException(nameof(quotaService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<TenantCostReport> CalculateCostsAsync(
        Guid tenantId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Calculating costs for tenant {TenantId} from {StartDate} to {EndDate}",
            tenantId, startDate, endDate);

        // Get usage metrics
        var storageUsageGB = await _quotaService.GetCurrentUsageAsync(
            tenantId,
            Domain.Enums.ResourceType.Storage,
            cancellationToken);

        var bandwidthUsageGB = await _quotaService.GetCurrentUsageAsync(
            tenantId,
            Domain.Enums.ResourceType.Bandwidth,
            cancellationToken);

        // Calculate costs
        var hours = (endDate - startDate).TotalHours;
        var computeCost = (decimal)hours * ComputeCostPerHour;
        var storageCost = storageUsageGB * StorageCostPerGB;
        var bandwidthCost = bandwidthUsageGB * BandwidthCostPerGB;

        var report = new TenantCostReport
        {
            TenantId = tenantId,
            PeriodStart = startDate,
            PeriodEnd = endDate,
            ComputeCost = computeCost,
            StorageCost = storageCost,
            BandwidthCost = bandwidthCost,
            TotalCost = computeCost + storageCost + bandwidthCost
        };

        _logger.LogInformation("Total cost for tenant {TenantId}: ${TotalCost:F2}",
            tenantId, report.TotalCost);

        return report;
    }

    public async Task<Dictionary<string, decimal>> GetCostBreakdownAsync(
        Guid tenantId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        var report = await CalculateCostsAsync(tenantId, startDate, endDate, cancellationToken);

        return new Dictionary<string, decimal>
        {
            { "Compute", report.ComputeCost },
            { "Storage", report.StorageCost },
            { "Bandwidth", report.BandwidthCost }
        };
    }
}
