using HotSwap.Distributed.Domain.Models;

namespace HotSwap.Distributed.Infrastructure.Interfaces;

/// <summary>
/// Service for tracking usage and generating analytics.
/// </summary>
public interface IUsageTrackingService
{
    /// <summary>
    /// Records a page view for a website.
    /// </summary>
    Task RecordPageViewAsync(Guid websiteId, string path, string userAgent, string ipAddress, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records bandwidth usage.
    /// </summary>
    Task RecordBandwidthUsageAsync(Guid tenantId, long bytes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records storage usage.
    /// </summary>
    Task RecordStorageUsageAsync(Guid tenantId, long bytes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets website traffic analytics.
    /// </summary>
    Task<TrafficAnalytics> GetTrafficAnalyticsAsync(Guid websiteId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
}

/// <summary>
/// Service for cost attribution and billing analytics.
/// </summary>
public interface ICostAttributionService
{
    /// <summary>
    /// Calculates costs for a tenant for a period.
    /// </summary>
    Task<TenantCostReport> CalculateCostsAsync(Guid tenantId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets cost breakdown by resource type.
    /// </summary>
    Task<Dictionary<string, decimal>> GetCostBreakdownAsync(Guid tenantId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
}

/// <summary>
/// Traffic analytics data.
/// </summary>
public class TrafficAnalytics
{
    public Guid WebsiteId { get; set; }
    public long TotalPageViews { get; set; }
    public long UniqueVisitors { get; set; }
    public Dictionary<string, long> TopPages { get; set; } = new();
    public Dictionary<string, long> TrafficByDay { get; set; } = new();
}

/// <summary>
/// Tenant cost report.
/// </summary>
public class TenantCostReport
{
    public Guid TenantId { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public decimal TotalCost { get; set; }
    public decimal ComputeCost { get; set; }
    public decimal StorageCost { get; set; }
    public decimal BandwidthCost { get; set; }
    public Dictionary<string, decimal> AdditionalCosts { get; set; } = new();
}
