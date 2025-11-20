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
    private readonly ConcurrentDictionary<string, HashSet<string>> _uniqueVisitors = new(); // Key: "websiteId:date", Value: Set of visitor hashes

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

        // Track unique visitors using hashed IP + User Agent
        // In production, use more sophisticated fingerprinting:
        // - Cookie-based tracking
        // - Session-based tracking
        // - Canvas fingerprinting (for web apps)
        // - Device fingerprinting libraries
        var visitorHash = ComputeVisitorHash(ipAddress, userAgent);
        var dateKey = $"{websiteId}:{DateTime.UtcNow:yyyy-MM-dd}";

        _uniqueVisitors.AddOrUpdate(
            dateKey,
            _ => new HashSet<string> { visitorHash },
            (_, existingSet) =>
            {
                lock (existingSet)
                {
                    existingSet.Add(visitorHash);
                }
                return existingSet;
            });

        _logger.LogDebug("Recorded page view: {WebsiteId} - {Path} (Visitor: {VisitorHash})",
            websiteId, path, visitorHash.Substring(0, 8));
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

        // Calculate unique visitors across the date range
        var uniqueVisitorsCount = CalculateUniqueVisitors(websiteId, startDate, endDate);

        var analytics = new TrafficAnalytics
        {
            WebsiteId = websiteId,
            TotalPageViews = pageViews.Values.Sum(),
            UniqueVisitors = uniqueVisitorsCount,
            TopPages = pageViews.OrderByDescending(kvp => kvp.Value).Take(10).ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
        };

        return Task.FromResult(analytics);
    }

    private long CalculateUniqueVisitors(Guid websiteId, DateTime startDate, DateTime endDate)
    {
        // Aggregate unique visitors across all days in the range
        // In production, this would query a database or analytics service
        var allUniqueVisitors = new HashSet<string>();

        for (var date = startDate.Date; date <= endDate.Date; date = date.AddDays(1))
        {
            var dateKey = $"{websiteId}:{date:yyyy-MM-dd}";
            if (_uniqueVisitors.TryGetValue(dateKey, out var visitorsForDay))
            {
                lock (visitorsForDay)
                {
                    foreach (var visitor in visitorsForDay)
                    {
                        allUniqueVisitors.Add(visitor);
                    }
                }
            }
        }

        return allUniqueVisitors.Count;
    }

    private static string ComputeVisitorHash(string ipAddress, string userAgent)
    {
        // Create a hash of IP address + User Agent for visitor identification
        // In production, consider:
        // - Adding salt for privacy
        // - Using more sophisticated fingerprinting
        // - GDPR/privacy compliance (anonymization, consent)
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var inputBytes = System.Text.Encoding.UTF8.GetBytes($"{ipAddress}|{userAgent}");
        var hashBytes = sha256.ComputeHash(inputBytes);
        return Convert.ToHexString(hashBytes);
    }
}
