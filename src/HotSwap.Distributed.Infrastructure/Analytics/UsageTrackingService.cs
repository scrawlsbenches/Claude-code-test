using HotSwap.Distributed.Infrastructure.Interfaces;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace HotSwap.Distributed.Infrastructure.Analytics;

/// <summary>
/// Service for tracking usage and generating analytics.
/// </summary>
public class UsageTrackingService : IUsageTrackingService
{
    private readonly ILogger<UsageTrackingService> _logger;
    private readonly ConcurrentDictionary<string, long> _pageViews = new();
    private readonly ConcurrentDictionary<Guid, long> _bandwidthUsage = new();
    private readonly ConcurrentDictionary<Guid, long> _storageUsage = new();

    public UsageTrackingService(ILogger<UsageTrackingService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task RecordPageViewAsync(
        Guid websiteId,
        string path,
        string userAgent,
        string ipAddress,
        CancellationToken cancellationToken = default)
    {
        var key = $"{websiteId}:{path}";
        _pageViews.AddOrUpdate(key, 1, (_, count) => count + 1);

        _logger.LogDebug("Recorded page view: {WebsiteId} - {Path}", websiteId, path);
        return Task.CompletedTask;
    }

    public Task RecordBandwidthUsageAsync(Guid tenantId, long bytes, CancellationToken cancellationToken = default)
    {
        _bandwidthUsage.AddOrUpdate(tenantId, bytes, (_, current) => current + bytes);

        _logger.LogDebug("Recorded bandwidth usage: {TenantId} - {Bytes} bytes", tenantId, bytes);
        return Task.CompletedTask;
    }

    public Task RecordStorageUsageAsync(Guid tenantId, long bytes, CancellationToken cancellationToken = default)
    {
        _storageUsage.AddOrUpdate(tenantId, bytes, (_, current) => current + bytes);

        _logger.LogDebug("Recorded storage usage: {TenantId} - {Bytes} bytes", tenantId, bytes);
        return Task.CompletedTask;
    }

    public Task<TrafficAnalytics> GetTrafficAnalyticsAsync(
        Guid websiteId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        var websitePrefix = $"{websiteId}:";
        var pageViews = _pageViews
            .Where(kvp => kvp.Key.StartsWith(websitePrefix))
            .ToDictionary(kvp => kvp.Key.Replace(websitePrefix, ""), kvp => kvp.Value);

        var analytics = new TrafficAnalytics
        {
            WebsiteId = websiteId,
            TotalPageViews = pageViews.Values.Sum(),
            UniqueVisitors = 0, // TODO: Track unique visitors
            TopPages = pageViews.OrderByDescending(kvp => kvp.Value).Take(10).ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
        };

        return Task.FromResult(analytics);
    }
}
