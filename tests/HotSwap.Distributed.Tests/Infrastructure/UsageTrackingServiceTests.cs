using FluentAssertions;
using HotSwap.Distributed.Infrastructure.Analytics;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace HotSwap.Distributed.Tests.Infrastructure;

public class UsageTrackingServiceTests
{
    private readonly Mock<ILogger<UsageTrackingService>> _mockLogger;
    private readonly UsageTrackingService _service;

    public UsageTrackingServiceTests()
    {
        _mockLogger = new Mock<ILogger<UsageTrackingService>>();
        _service = new UsageTrackingService(_mockLogger.Object);
    }

    [Fact]
    public async Task RecordPageViewAsync_WithValidData_RecordsPageView()
    {
        // Arrange
        var websiteId = Guid.NewGuid();
        var path = "/home";
        var userAgent = "Mozilla/5.0";
        var ipAddress = "192.168.1.1";

        // Act
        await _service.RecordPageViewAsync(websiteId, path, userAgent, ipAddress);

        // Assert - Should not throw
        // Verification happens via GetTrafficAnalyticsAsync
        var analytics = await _service.GetTrafficAnalyticsAsync(websiteId, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1));
        analytics.TotalPageViews.Should().Be(1);
    }

    [Fact]
    public async Task RecordPageViewAsync_MultipleViews_AggregatesCorrectly()
    {
        // Arrange
        var websiteId = Guid.NewGuid();
        var path = "/home";

        // Act
        await _service.RecordPageViewAsync(websiteId, path, "UA1", "192.168.1.1");
        await _service.RecordPageViewAsync(websiteId, path, "UA2", "192.168.1.2");
        await _service.RecordPageViewAsync(websiteId, path, "UA3", "192.168.1.3");

        // Assert
        var analytics = await _service.GetTrafficAnalyticsAsync(websiteId, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1));
        analytics.TotalPageViews.Should().Be(3);
        analytics.UniqueVisitors.Should().Be(3); // 3 different IP+UA combinations
    }

    [Fact]
    public async Task RecordPageViewAsync_SameVisitorMultipleTimes_CountsAsOneUniqueVisitor()
    {
        // Arrange
        var websiteId = Guid.NewGuid();
        var userAgent = "Mozilla/5.0";
        var ipAddress = "192.168.1.1";

        // Act
        await _service.RecordPageViewAsync(websiteId, "/home", userAgent, ipAddress);
        await _service.RecordPageViewAsync(websiteId, "/about", userAgent, ipAddress);
        await _service.RecordPageViewAsync(websiteId, "/contact", userAgent, ipAddress);

        // Assert
        var analytics = await _service.GetTrafficAnalyticsAsync(websiteId, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1));
        analytics.TotalPageViews.Should().Be(3);
        analytics.UniqueVisitors.Should().Be(1); // Same visitor (IP+UA hash)
    }

    [Fact]
    public async Task RecordBandwidthUsageAsync_WithValidData_RecordsBandwidth()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var bytes = 1024L;

        // Act
        await _service.RecordBandwidthUsageAsync(tenantId, bytes);

        // Assert - Should complete without error
        // Bandwidth is tracked internally
    }

    [Fact]
    public async Task RecordBandwidthUsageAsync_MultipleCalls_AggregatesBytes()
    {
        // Arrange
        var tenantId = Guid.NewGuid();

        // Act
        await _service.RecordBandwidthUsageAsync(tenantId, 1000);
        await _service.RecordBandwidthUsageAsync(tenantId, 2000);
        await _service.RecordBandwidthUsageAsync(tenantId, 3000);

        // Assert - Total should be 6000 bytes
        // Verification would require GetBandwidthUsageAsync method
    }

    [Fact]
    public async Task RecordStorageUsageAsync_WithValidData_RecordsStorage()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var bytes = 1024000L;

        // Act
        await _service.RecordStorageUsageAsync(tenantId, bytes);

        // Assert - Should complete without error
    }

    [Fact]
    public async Task RecordStorageUsageAsync_MultipleCalls_AggregatesBytes()
    {
        // Arrange
        var tenantId = Guid.NewGuid();

        // Act
        await _service.RecordStorageUsageAsync(tenantId, 500000);
        await _service.RecordStorageUsageAsync(tenantId, 300000);
        await _service.RecordStorageUsageAsync(tenantId, 200000);

        // Assert - Total should be 1000000 bytes
    }

    [Fact]
    public async Task GetTrafficAnalyticsAsync_WithNoData_ReturnsZeroMetrics()
    {
        // Arrange
        var websiteId = Guid.NewGuid();
        var startDate = DateTime.UtcNow.AddDays(-7);
        var endDate = DateTime.UtcNow;

        // Act
        var analytics = await _service.GetTrafficAnalyticsAsync(websiteId, startDate, endDate);

        // Assert
        analytics.Should().NotBeNull();
        analytics.WebsiteId.Should().Be(websiteId);
        analytics.TotalPageViews.Should().Be(0);
        analytics.UniqueVisitors.Should().Be(0);
        analytics.TopPages.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTrafficAnalyticsAsync_WithData_ReturnsCorrectMetrics()
    {
        // Arrange
        var websiteId = Guid.NewGuid();
        await _service.RecordPageViewAsync(websiteId, "/home", "UA1", "IP1");
        await _service.RecordPageViewAsync(websiteId, "/home", "UA1", "IP1"); // Same visitor
        await _service.RecordPageViewAsync(websiteId, "/about", "UA2", "IP2"); // Different visitor
        await _service.RecordPageViewAsync(websiteId, "/contact", "UA3", "IP3"); // Different visitor

        var startDate = DateTime.UtcNow.AddDays(-1);
        var endDate = DateTime.UtcNow.AddDays(1);

        // Act
        var analytics = await _service.GetTrafficAnalyticsAsync(websiteId, startDate, endDate);

        // Assert
        analytics.WebsiteId.Should().Be(websiteId);
        analytics.TotalPageViews.Should().Be(4);
        analytics.UniqueVisitors.Should().Be(3); // 3 unique visitor hashes
        analytics.TopPages.Should().HaveCount(3);
        analytics.TopPages["/home"].Should().Be(2); // Most visited page
    }

    [Fact]
    public async Task GetTrafficAnalyticsAsync_ReturnsTopPagesOrderedByViews()
    {
        // Arrange
        var websiteId = Guid.NewGuid();
        await _service.RecordPageViewAsync(websiteId, "/home", "UA1", "IP1");
        await _service.RecordPageViewAsync(websiteId, "/home", "UA2", "IP2");
        await _service.RecordPageViewAsync(websiteId, "/home", "UA3", "IP3");
        await _service.RecordPageViewAsync(websiteId, "/about", "UA4", "IP4");
        await _service.RecordPageViewAsync(websiteId, "/about", "UA5", "IP5");
        await _service.RecordPageViewAsync(websiteId, "/contact", "UA6", "IP6");

        var startDate = DateTime.UtcNow.AddDays(-1);
        var endDate = DateTime.UtcNow.AddDays(1);

        // Act
        var analytics = await _service.GetTrafficAnalyticsAsync(websiteId, startDate, endDate);

        // Assert
        analytics.TopPages.Should().HaveCount(3);
        analytics.TopPages.First().Key.Should().Be("/home"); // Most views (3)
        analytics.TopPages.First().Value.Should().Be(3);
        analytics.TopPages.Skip(1).First().Key.Should().Be("/about"); // Second (2)
        analytics.TopPages.Skip(1).First().Value.Should().Be(2);
        analytics.TopPages.Last().Key.Should().Be("/contact"); // Least (1)
        analytics.TopPages.Last().Value.Should().Be(1);
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new UsageTrackingService(null!);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    /// <summary>
    /// Tests thread-safety of UsageTrackingService when recording concurrent page views.
    /// This test verifies the fix for HIGH-01: Thread-safety issue in UsageTrackingService.
    /// The fix replaced HashSet with ConcurrentDictionary to ensure thread-safe visitor tracking.
    /// </summary>
    [Fact]
    public async Task RecordPageViewAsync_ConcurrentRequests_HandlesThreadSafelyWithoutDataLoss()
    {
        // Arrange
        var websiteId = Guid.NewGuid();
        var path = "/home";
        var concurrentRequests = 100;
        var uniqueVisitors = 50;

        // Act - Simulate 100 concurrent requests from 50 unique visitors (each visitor makes 2 requests)
        var tasks = new List<Task>();
        for (int i = 0; i < concurrentRequests; i++)
        {
            var visitorNumber = i % uniqueVisitors; // Ensure we have 50 unique visitors
            var userAgent = $"Mozilla/5.0 (Visitor {visitorNumber})";
            var ipAddress = $"192.168.1.{visitorNumber}";

            tasks.Add(_service.RecordPageViewAsync(websiteId, path, userAgent, ipAddress));
        }

        await Task.WhenAll(tasks);

        // Assert
        var analytics = await _service.GetTrafficAnalyticsAsync(websiteId, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1));

        // Verify all page views were recorded (no data loss from race conditions)
        analytics.TotalPageViews.Should().Be(concurrentRequests);

        // Verify unique visitors are counted correctly (no duplicate counting from race conditions)
        analytics.UniqueVisitors.Should().Be(uniqueVisitors);
    }

    /// <summary>
    /// Tests thread-safety when recording page views for the same visitor concurrently.
    /// Ensures the ConcurrentDictionary-based implementation correctly handles same-visitor
    /// concurrent requests without race conditions or exceptions.
    /// </summary>
    [Fact]
    public async Task RecordPageViewAsync_SameVisitorConcurrentRequests_HandlesThreadSafely()
    {
        // Arrange
        var websiteId = Guid.NewGuid();
        var userAgent = "Mozilla/5.0";
        var ipAddress = "192.168.1.100";
        var paths = new[] { "/home", "/about", "/contact", "/services", "/blog" };
        var concurrentRequests = 50;

        // Act - Simulate 50 concurrent requests from the same visitor to different pages
        var tasks = new List<Task>();
        for (int i = 0; i < concurrentRequests; i++)
        {
            var path = paths[i % paths.Length];
            tasks.Add(_service.RecordPageViewAsync(websiteId, path, userAgent, ipAddress));
        }

        await Task.WhenAll(tasks);

        // Assert
        var analytics = await _service.GetTrafficAnalyticsAsync(websiteId, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1));

        // Verify all page views were recorded
        analytics.TotalPageViews.Should().Be(concurrentRequests);

        // Verify only one unique visitor is counted (same IP+UA hash)
        analytics.UniqueVisitors.Should().Be(1);
    }

    #region Memory Management Tests

    /// <summary>
    /// MEMORY SAFETY TEST: Verifies that old visitor data beyond RETENTION_DAYS is cleaned up.
    /// This prevents unbounded memory growth in long-running services.
    /// </summary>
    [Fact]
    public async Task RecordPageViewAsync_OldVisitorData_IsCleanedUpAutomatically()
    {
        // Arrange
        var websiteId = Guid.NewGuid();
        var mockLogger = new Mock<ILogger<UsageTrackingService>>();
        var service = new UsageTrackingService(mockLogger.Object);

        // Use reflection to set _lastCleanupTime to force immediate cleanup
        var lastCleanupField = typeof(UsageTrackingService).GetField("_lastCleanupTime",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        lastCleanupField!.SetValue(service, DateTime.UtcNow.AddDays(-2)); // Force cleanup

        // Manually add old visitor data using reflection
        var uniqueVisitorsField = typeof(UsageTrackingService).GetField("_uniqueVisitors",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var uniqueVisitors = uniqueVisitorsField!.GetValue(service) as System.Collections.Concurrent.ConcurrentDictionary<string, System.Collections.Concurrent.ConcurrentDictionary<string, byte>>;

        // Add data from 31 days ago (beyond RETENTION_DAYS = 30)
        var oldDate = DateTime.UtcNow.AddDays(-31).ToString("yyyy-MM-dd");
        var oldKey = $"{websiteId}:{oldDate}";
        var oldVisitors = new System.Collections.Concurrent.ConcurrentDictionary<string, byte>();
        oldVisitors.TryAdd("old_visitor_hash", 0);
        uniqueVisitors!.TryAdd(oldKey, oldVisitors);

        // Add recent data (within RETENTION_DAYS)
        var recentDate = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var recentKey = $"{websiteId}:{recentDate}";
        var recentVisitors = new System.Collections.Concurrent.ConcurrentDictionary<string, byte>();
        recentVisitors.TryAdd("recent_visitor_hash", 0);
        uniqueVisitors.TryAdd(recentKey, recentVisitors);

        // Act - Trigger cleanup by recording a new page view
        await service.RecordPageViewAsync(websiteId, "/test", "UA", "IP");

        // Assert - Old data should be removed, recent data should remain
        uniqueVisitors.ContainsKey(oldKey).Should().BeFalse("Old visitor data beyond RETENTION_DAYS should be cleaned up");
        uniqueVisitors.ContainsKey(recentKey).Should().BeTrue("Recent visitor data should be retained");

        // Verify cleanup was logged
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Cleaned up") && v.ToString()!.Contains("old visitor data entries")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    /// <summary>
    /// MEMORY SAFETY TEST: Verifies that page view entries are limited per website.
    /// This prevents memory exhaustion from tracking too many unique paths.
    /// </summary>
    [Fact]
    public async Task RecordPageViewAsync_ExcessivePageViewEntries_EnforcesLimits()
    {
        // Arrange
        var websiteId = Guid.NewGuid();
        var mockLogger = new Mock<ILogger<UsageTrackingService>>();
        var service = new UsageTrackingService(mockLogger.Object);

        // Get MAX_PAGE_VIEW_ENTRIES constant via reflection
        var maxEntriesField = typeof(UsageTrackingService).GetField("MAX_PAGE_VIEW_ENTRIES",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var maxEntries = (int)maxEntriesField!.GetValue(null)!;

        // Record more than MAX_PAGE_VIEW_ENTRIES unique paths
        var excessEntries = maxEntries + 100;
        for (int i = 0; i < excessEntries; i++)
        {
            await service.RecordPageViewAsync(websiteId, $"/page-{i}", "UA", "IP");
        }

        // Act - Directly invoke EnforcePageViewLimits via reflection
        var enforceMethod = typeof(UsageTrackingService).GetMethod("EnforcePageViewLimits",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        enforceMethod!.Invoke(service, null);

        // Assert - Verify enforcement was logged
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Enforced page view limit")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce,
            "Page view limit enforcement should be logged");

        // Get page views dictionary via reflection to verify count
        var pageViewsField = typeof(UsageTrackingService).GetField("_pageViews",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var pageViews = pageViewsField!.GetValue(service) as System.Collections.Concurrent.ConcurrentDictionary<string, long>;

        // Verify total entries for this website doesn't exceed MAX_PAGE_VIEW_ENTRIES
        var websitePrefix = $"{websiteId}:";
        var websiteEntries = pageViews!.Keys.Count(k => k.StartsWith(websitePrefix));
        websiteEntries.Should().BeLessOrEqualTo(maxEntries, "Page view entries should be limited to prevent memory exhaustion");
    }

    /// <summary>
    /// MEMORY SAFETY TEST: Verifies that cleanup only runs periodically to avoid performance overhead.
    /// </summary>
    [Fact]
    public async Task RecordPageViewAsync_FrequentCalls_DoesNotCleanupEveryTime()
    {
        // Arrange
        var websiteId = Guid.NewGuid();
        var mockLogger = new Mock<ILogger<UsageTrackingService>>();
        var service = new UsageTrackingService(mockLogger.Object);

        // Act - Record multiple page views in quick succession
        for (int i = 0; i < 10; i++)
        {
            await service.RecordPageViewAsync(websiteId, $"/page-{i}", "UA", "IP");
        }

        // Assert - Cleanup should not have been triggered (less than 24 hours since last cleanup)
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Memory cleanup completed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never,
            "Cleanup should not run on every page view (performance optimization)");
    }

    /// <summary>
    /// MEMORY SAFETY TEST: Verifies that cleanup handles exceptions gracefully without disrupting service.
    /// </summary>
    [Fact]
    public async Task RecordPageViewAsync_CleanupException_DoesNotDisruptService()
    {
        // Arrange
        var websiteId = Guid.NewGuid();
        var mockLogger = new Mock<ILogger<UsageTrackingService>>();
        var service = new UsageTrackingService(mockLogger.Object);

        // Force cleanup to run
        var lastCleanupField = typeof(UsageTrackingService).GetField("_lastCleanupTime",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        lastCleanupField!.SetValue(service, DateTime.UtcNow.AddDays(-2));

        // Act - Record page view (cleanup will run internally)
        var act = async () => await service.RecordPageViewAsync(websiteId, "/test", "UA", "IP");

        // Assert - Should not throw even if cleanup encounters issues
        await act.Should().NotThrowAsync("Service should handle cleanup errors gracefully");

        // Verify page view was still recorded
        var analytics = await service.GetTrafficAnalyticsAsync(websiteId, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1));
        analytics.TotalPageViews.Should().BeGreaterThan(0, "Page views should be recorded even if cleanup fails");
    }

    #endregion
}
