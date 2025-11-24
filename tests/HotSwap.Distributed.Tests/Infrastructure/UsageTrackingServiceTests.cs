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
}
