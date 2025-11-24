using System.Collections.Concurrent;
using HotSwap.Distributed.Infrastructure.Interfaces;
using Microsoft.Extensions.Logging;

namespace HotSwap.Distributed.Infrastructure.Analytics;

/// <summary>
/// Service for tracking usage and generating analytics.
/// MEMORY MANAGEMENT: Automatically cleans up data older than RETENTION_DAYS to prevent unbounded growth.
/// In production, this data should be persisted to a database and cleared from memory more frequently.
/// </summary>
public class UsageTrackingService : IUsageTrackingService
{
    private const int RETENTION_DAYS = 30; // Keep 30 days of data in memory
    private const int MAX_PAGE_VIEW_ENTRIES = 10000; // Max number of page view entries per website

    private readonly ILogger<UsageTrackingService> _logger;
    private readonly ConcurrentDictionary<string, long> _pageViews = new();
    private readonly ConcurrentDictionary<Guid, long> _bandwidthUsage = new();
    private readonly ConcurrentDictionary<Guid, long> _storageUsage = new();
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _uniqueVisitors = new(); // Key: "websiteId:date", Value: Concurrent set of visitor hashes
    private DateTime _lastCleanupTime = DateTime.UtcNow;

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
        // Periodic cleanup to prevent unbounded memory growth
        CleanupOldDataIfNeeded();

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

        // Use ConcurrentDictionary as a thread-safe set (value is unused)
        var visitors = _uniqueVisitors.GetOrAdd(dateKey, _ => new ConcurrentDictionary<string, byte>());
        visitors.TryAdd(visitorHash, 0);

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
        var allUniqueVisitors = new ConcurrentDictionary<string, byte>();

        for (var date = startDate.Date; date <= endDate.Date; date = date.AddDays(1))
        {
            var dateKey = $"{websiteId}:{date:yyyy-MM-dd}";
            if (_uniqueVisitors.TryGetValue(dateKey, out var visitorsForDay))
            {
                foreach (var visitor in visitorsForDay.Keys)
                {
                    allUniqueVisitors.TryAdd(visitor, 0);
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

    /// <summary>
    /// Cleans up old data if it's been more than 24 hours since last cleanup.
    /// This prevents unbounded memory growth by removing stale entries.
    /// </summary>
    private void CleanupOldDataIfNeeded()
    {
        // Only cleanup once per day to avoid performance overhead
        if ((DateTime.UtcNow - _lastCleanupTime).TotalHours < 24)
            return;

        _lastCleanupTime = DateTime.UtcNow;

        try
        {
            CleanupOldVisitorData();
            EnforcePageViewLimits();
            _logger.LogInformation("Memory cleanup completed. UniqueVisitors: {VisitorCount} entries, PageViews: {PageViewCount} entries",
                _uniqueVisitors.Count, _pageViews.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during memory cleanup");
        }
    }

    /// <summary>
    /// Removes visitor data older than RETENTION_DAYS.
    /// </summary>
    private void CleanupOldVisitorData()
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-RETENTION_DAYS).Date;
        var keysToRemove = new List<string>();

        foreach (var key in _uniqueVisitors.Keys)
        {
            // Key format: "websiteId:yyyy-MM-dd"
            var datePart = key.Split(':').LastOrDefault();
            if (datePart != null && DateTime.TryParseExact(datePart, "yyyy-MM-dd",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var entryDate))
            {
                if (entryDate < cutoffDate)
                {
                    keysToRemove.Add(key);
                }
            }
        }

        foreach (var key in keysToRemove)
        {
            if (_uniqueVisitors.TryRemove(key, out var removed))
            {
                _logger.LogDebug("Removed old visitor data: {Key} ({VisitorCount} visitors)",
                    key, removed.Count);
            }
        }

        if (keysToRemove.Count > 0)
        {
            _logger.LogInformation("Cleaned up {Count} old visitor data entries", keysToRemove.Count);
        }
    }

    /// <summary>
    /// Enforces maximum page view entries per website to prevent memory exhaustion.
    /// Removes least recently used entries when limit is exceeded.
    /// </summary>
    private void EnforcePageViewLimits()
    {
        // Group page views by website
        var websiteGroups = _pageViews.Keys
            .Select(key => new { Key = key, WebsiteId = key.Split(':').FirstOrDefault() })
            .Where(x => x.WebsiteId != null)
            .GroupBy(x => x.WebsiteId);

        foreach (var group in websiteGroups)
        {
            var websiteKeys = group.Select(x => x.Key).ToList();
            if (websiteKeys.Count > MAX_PAGE_VIEW_ENTRIES)
            {
                // Remove entries with lowest counts (LFU eviction)
                var entriesToRemove = websiteKeys
                    .Select(key => new { Key = key, Count = _pageViews.TryGetValue(key, out var count) ? count : 0 })
                    .OrderBy(x => x.Count)
                    .Take(websiteKeys.Count - MAX_PAGE_VIEW_ENTRIES)
                    .Select(x => x.Key)
                    .ToList();

                foreach (var key in entriesToRemove)
                {
                    _pageViews.TryRemove(key, out _);
                }

                _logger.LogInformation("Enforced page view limit for website {WebsiteId}: removed {Count} entries",
                    group.Key, entriesToRemove.Count);
            }
        }
    }
}
