using FluentAssertions;
using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Interfaces;
using HotSwap.Distributed.Infrastructure.Tenants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace HotSwap.Distributed.Tests.Infrastructure.Storage;

public class SubscriptionServiceWithStorageTests
{
    private readonly Mock<ITenantRepository> _mockTenantRepository;
    private readonly MockObjectStorageService _mockStorageService;
    private readonly Mock<ILogger<SubscriptionService>> _mockLogger;
    private readonly SubscriptionService _service;

    public SubscriptionServiceWithStorageTests()
    {
        _mockTenantRepository = new Mock<ITenantRepository>();
        _mockStorageService = new MockObjectStorageService();
        _mockLogger = new Mock<ILogger<SubscriptionService>>();

        _service = new SubscriptionService(
            _mockTenantRepository.Object,
            _mockLogger.Object,
            _mockStorageService);
    }

    [Fact]
    public async Task GetUsageReportAsync_ShouldCalculateActualStorageUsage()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var bucketName = $"tenant-{tenantId:N}";

        var tenant = new Tenant
        {
            TenantId = tenantId,
            Name = "Test Tenant",
            Subdomain = "test",
            Tier = SubscriptionTier.Professional
        };

        _mockTenantRepository
            .Setup(r => r.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);
        _mockTenantRepository
            .Setup(r => r.UpdateAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tenant t, CancellationToken _) => t);

        // Create bucket and upload some test data
        await _mockStorageService.CreateBucketAsync(bucketName);

        // Upload 3 files with known sizes
        var file1 = new byte[1024 * 1024]; // 1 MB
        var file2 = new byte[2 * 1024 * 1024]; // 2 MB
        var file3 = new byte[512 * 1024]; // 512 KB

        await _mockStorageService.UploadObjectAsync(bucketName, "file1.dat", new MemoryStream(file1), "application/octet-stream");
        await _mockStorageService.UploadObjectAsync(bucketName, "file2.dat", new MemoryStream(file2), "application/octet-stream");
        await _mockStorageService.UploadObjectAsync(bucketName, "file3.dat", new MemoryStream(file3), "application/octet-stream");

        var expectedSizeBytes = file1.Length + file2.Length + file3.Length; // ~3.5 MB
        var expectedSizeGB = expectedSizeBytes / (1024.0 * 1024.0 * 1024.0);

        // Create subscription
        await _service.CreateSubscriptionAsync(tenantId, SubscriptionTier.Professional);

        var periodStart = DateTime.UtcNow.AddMonths(-1);
        var periodEnd = DateTime.UtcNow;

        // Act
        var report = await _service.GetUsageReportAsync(tenantId, periodStart, periodEnd);

        // Assert
        report.Should().NotBeNull();
        report.TenantId.Should().Be(tenantId);
        report.StorageUsedGB.Should().BeGreaterThan(0);

        // Verify storage calculation
        var actualSizeGB = (double)report.StorageUsedGB;
        actualSizeGB.Should().BeApproximately(expectedSizeGB, 0.01, "storage should be calculated from actual bucket size");
    }

    [Fact]
    public async Task GetUsageReportAsync_WhenNoBucket_ShouldReturnZeroStorageUsage()
    {
        // Arrange
        var tenantId = Guid.NewGuid();

        var tenant = new Tenant
        {
            TenantId = tenantId,
            Name = "Test Tenant",
            Subdomain = "test",
            Tier = SubscriptionTier.Starter
        };

        _mockTenantRepository
            .Setup(r => r.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);
        _mockTenantRepository
            .Setup(r => r.UpdateAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tenant t, CancellationToken _) => t);

        await _service.CreateSubscriptionAsync(tenantId, SubscriptionTier.Starter);

        var periodStart = DateTime.UtcNow.AddMonths(-1);
        var periodEnd = DateTime.UtcNow;

        // Act
        var report = await _service.GetUsageReportAsync(tenantId, periodStart, periodEnd);

        // Assert
        report.Should().NotBeNull();
        report.StorageUsedGB.Should().Be(0, "storage usage should be 0 when bucket doesn't exist");
    }

    [Fact]
    public async Task GetUsageReportAsync_WhenEmptyBucket_ShouldReturnZeroStorageUsage()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var bucketName = $"tenant-{tenantId:N}";

        var tenant = new Tenant
        {
            TenantId = tenantId,
            Name = "Test Tenant",
            Subdomain = "test",
            Tier = SubscriptionTier.Professional
        };

        _mockTenantRepository
            .Setup(r => r.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);
        _mockTenantRepository
            .Setup(r => r.UpdateAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tenant t, CancellationToken _) => t);

        await _mockStorageService.CreateBucketAsync(bucketName);

        await _service.CreateSubscriptionAsync(tenantId, SubscriptionTier.Professional);

        var periodStart = DateTime.UtcNow.AddMonths(-1);
        var periodEnd = DateTime.UtcNow;

        // Act
        var report = await _service.GetUsageReportAsync(tenantId, periodStart, periodEnd);

        // Assert
        report.Should().NotBeNull();
        report.StorageUsedGB.Should().Be(0, "storage usage should be 0 for empty bucket");
    }

    [Fact]
    public async Task GetUsageReportAsync_MultipleReports_ShouldReflectStorageChanges()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var bucketName = $"tenant-{tenantId:N}";

        var tenant = new Tenant
        {
            TenantId = tenantId,
            Name = "Test Tenant",
            Subdomain = "test",
            Tier = SubscriptionTier.Enterprise
        };

        _mockTenantRepository
            .Setup(r => r.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);
        _mockTenantRepository
            .Setup(r => r.UpdateAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tenant t, CancellationToken _) => t);

        await _mockStorageService.CreateBucketAsync(bucketName);
        await _service.CreateSubscriptionAsync(tenantId, SubscriptionTier.Enterprise);

        var period1Start = DateTime.UtcNow.AddMonths(-2);
        var period1End = DateTime.UtcNow.AddMonths(-1);

        // Generate first report (no data)
        var report1 = await _service.GetUsageReportAsync(tenantId, period1Start, period1End);
        report1.StorageUsedGB.Should().Be(0);

        // Add some data
        var file = new byte[5 * 1024 * 1024]; // 5 MB
        await _mockStorageService.UploadObjectAsync(bucketName, "data.bin", new MemoryStream(file), "application/octet-stream");

        var period2Start = DateTime.UtcNow.AddMonths(-1);
        var period2End = DateTime.UtcNow;

        // Act - Generate second report (with data)
        var report2 = await _service.GetUsageReportAsync(tenantId, period2Start, period2End);

        // Assert
        report2.StorageUsedGB.Should().BeGreaterThan(0, "second report should show storage usage");
        report2.StorageUsedGB.Should().BeGreaterThan(report1.StorageUsedGB);
    }

    [Fact]
    public async Task GetUsageReportAsync_LargeStorage_ShouldCalculateCorrectly()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var bucketName = $"tenant-{tenantId:N}";

        var tenant = new Tenant
        {
            TenantId = tenantId,
            Name = "Test Tenant",
            Subdomain = "test",
            Tier = SubscriptionTier.Enterprise
        };

        _mockTenantRepository
            .Setup(r => r.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);
        _mockTenantRepository
            .Setup(r => r.UpdateAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tenant t, CancellationToken _) => t);

        await _mockStorageService.CreateBucketAsync(bucketName);

        // Upload 100 MB of data (split into multiple files to simulate realistic usage)
        var totalBytes = 0L;
        for (int i = 0; i < 10; i++)
        {
            var file = new byte[10 * 1024 * 1024]; // 10 MB each
            totalBytes += file.Length;
            await _mockStorageService.UploadObjectAsync(bucketName, $"file{i}.dat", new MemoryStream(file), "application/octet-stream");
        }

        var expectedGB = totalBytes / (1024.0 * 1024.0 * 1024.0);

        await _service.CreateSubscriptionAsync(tenantId, SubscriptionTier.Enterprise);

        var periodStart = DateTime.UtcNow.AddMonths(-1);
        var periodEnd = DateTime.UtcNow;

        // Act
        var report = await _service.GetUsageReportAsync(tenantId, periodStart, periodEnd);

        // Assert
        var actualGB = (double)report.StorageUsedGB;
        actualGB.Should().BeApproximately(expectedGB, 0.01, "should accurately calculate large storage usage");
        actualGB.Should().BeGreaterThan(0.09, "100 MB should be approximately 0.0931 GB");
    }
}
