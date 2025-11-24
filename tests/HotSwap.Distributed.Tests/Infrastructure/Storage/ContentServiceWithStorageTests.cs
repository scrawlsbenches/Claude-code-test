using System.Text;
using FluentAssertions;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Interfaces;
using HotSwap.Distributed.Infrastructure.Websites;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace HotSwap.Distributed.Tests.Infrastructure.Storage;

public class ContentServiceWithStorageTests
{
    private readonly Mock<IPageRepository> _mockPageRepository;
    private readonly Mock<IMediaRepository> _mockMediaRepository;
    private readonly MockObjectStorageService _mockStorageService;
    private readonly Mock<ILogger<ContentService>> _mockLogger;
    private readonly ContentService _service;

    public ContentServiceWithStorageTests()
    {
        _mockPageRepository = new Mock<IPageRepository>();
        _mockMediaRepository = new Mock<IMediaRepository>();
        _mockStorageService = new MockObjectStorageService();
        _mockLogger = new Mock<ILogger<ContentService>>();

        _service = new ContentService(
            _mockPageRepository.Object,
            _mockMediaRepository.Object,
            _mockLogger.Object,
            _mockStorageService);
    }

    [Fact]
    public async Task UploadMediaAsync_ShouldUploadToStorage()
    {
        // Arrange
        var websiteId = Guid.NewGuid();
        var fileName = "test-image.jpg";
        var contentType = "image/jpeg";
        var fileContent = "Test file content"u8.ToArray();
        var fileStream = new MemoryStream(fileContent);

        _mockMediaRepository
            .Setup(r => r.CreateAsync(It.IsAny<MediaAsset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MediaAsset m, CancellationToken ct) => m);

        // Act
        var result = await _service.UploadMediaAsync(websiteId, fileStream, fileName, contentType);

        // Assert
        result.Should().NotBeNull();
        result.FileName.Should().Be(fileName);
        result.ContentType.Should().Be(contentType);
        result.SizeBytes.Should().Be(fileContent.Length);
        result.StorageUrl.Should().Contain("tenant-media");

        // Verify storage
        var bucketExists = await _mockStorageService.BucketExistsAsync("tenant-media");
        bucketExists.Should().BeTrue("media bucket should be created");

        var objectCount = await _mockStorageService.GetObjectCountAsync("tenant-media");
        objectCount.Should().Be(1, "one object should be uploaded");

        var bucketSize = await _mockStorageService.GetBucketSizeAsync("tenant-media");
        bucketSize.Should().Be(fileContent.Length);
    }

    [Fact]
    public async Task UploadMediaAsync_MultipleFiles_ShouldStoreAll()
    {
        // Arrange
        var websiteId = Guid.NewGuid();
        var files = new[]
        {
            ("image1.jpg", "image/jpeg", "Content 1"u8.ToArray()),
            ("image2.png", "image/png", "Content 2 longer"u8.ToArray()),
            ("document.pdf", "application/pdf", "PDF Content Here"u8.ToArray())
        };

        _mockMediaRepository
            .Setup(r => r.CreateAsync(It.IsAny<MediaAsset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MediaAsset m, CancellationToken ct) => m);

        // Act
        var results = new List<MediaAsset>();
        foreach (var (fileName, contentType, content) in files)
        {
            var stream = new MemoryStream(content);
            var result = await _service.UploadMediaAsync(websiteId, stream, fileName, contentType);
            results.Add(result);
        }

        // Assert
        results.Should().HaveCount(3);
        var objectCount = await _mockStorageService.GetObjectCountAsync("tenant-media");
        objectCount.Should().Be(3);

        var totalSize = files.Sum(f => f.Item3.Length);
        var bucketSize = await _mockStorageService.GetBucketSizeAsync("tenant-media");
        bucketSize.Should().Be(totalSize);
    }

    [Fact]
    public async Task DeleteMediaAsync_ShouldRemoveFromStorage()
    {
        // Arrange
        var mediaId = Guid.NewGuid();
        var websiteId = Guid.NewGuid();
        var fileName = "test-image.jpg";
        var fileContent = "Test file content"u8.ToArray();

        // Upload first
        _mockMediaRepository
            .Setup(r => r.CreateAsync(It.IsAny<MediaAsset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MediaAsset m, CancellationToken ct) => m);

        var uploaded = await _service.UploadMediaAsync(
            websiteId,
            new MemoryStream(fileContent),
            fileName,
            "image/jpeg");

        // Setup for deletion
        _mockMediaRepository
            .Setup(r => r.GetByIdAsync(mediaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MediaAsset
            {
                MediaId = mediaId,
                WebsiteId = websiteId,
                FileName = fileName,
                StorageUrl = uploaded.StorageUrl,
                SizeBytes = fileContent.Length,
                ContentType = "image/jpeg"
            });

        _mockMediaRepository
            .Setup(r => r.DeleteAsync(mediaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _service.DeleteMediaAsync(mediaId);

        // Assert
        result.Should().BeTrue();

        var objectCount = await _mockStorageService.GetObjectCountAsync("tenant-media");
        objectCount.Should().Be(0, "object should be deleted from storage");
    }

    [Fact]
    public async Task DeleteMediaAsync_WhenMediaDoesNotExist_ShouldReturnFalse()
    {
        // Arrange
        var mediaId = Guid.NewGuid();

        _mockMediaRepository
            .Setup(r => r.GetByIdAsync(mediaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MediaAsset?)null);

        // Act
        var result = await _service.DeleteMediaAsync(mediaId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task UploadMediaAsync_ShouldGenerateUniqueObjectKeys()
    {
        // Arrange
        var websiteId = Guid.NewGuid();
        var fileName = "duplicate.jpg";
        var fileContent = "Content"u8.ToArray();

        _mockMediaRepository
            .Setup(r => r.CreateAsync(It.IsAny<MediaAsset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MediaAsset m, CancellationToken ct) => m);

        // Act - upload same file twice
        var result1 = await _service.UploadMediaAsync(
            websiteId,
            new MemoryStream(fileContent),
            fileName,
            "image/jpeg");

        var result2 = await _service.UploadMediaAsync(
            websiteId,
            new MemoryStream(fileContent),
            fileName,
            "image/jpeg");

        // Assert
        result1.StorageUrl.Should().NotBe(result2.StorageUrl, "URLs should be unique due to GUID in path");

        var objectCount = await _mockStorageService.GetObjectCountAsync("tenant-media");
        objectCount.Should().Be(2, "both files should be stored with unique keys");
    }
}
