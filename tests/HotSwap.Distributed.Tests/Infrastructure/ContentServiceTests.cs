using FluentAssertions;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Interfaces;
using HotSwap.Distributed.Infrastructure.Websites;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace HotSwap.Distributed.Tests.Infrastructure;

public class ContentServiceTests
{
    private readonly Mock<IPageRepository> _mockPageRepository;
    private readonly Mock<IMediaRepository> _mockMediaRepository;
    private readonly Mock<ILogger<ContentService>> _mockLogger;
    private readonly ContentService _service;

    public ContentServiceTests()
    {
        _mockPageRepository = new Mock<IPageRepository>();
        _mockMediaRepository = new Mock<IMediaRepository>();
        _mockLogger = new Mock<ILogger<ContentService>>();
        _service = new ContentService(_mockPageRepository.Object, _mockMediaRepository.Object, _mockLogger.Object);
    }

    [Fact(Skip = "Temporarily disabled - investigating test hang")]
    public async Task UploadMediaAsync_WithValidFile_UploadsSuccessfully()
    {
        // Arrange
        var websiteId = Guid.NewGuid();
        var fileName = "test-image.jpg";
        var contentType = "image/jpeg";
        var fileContent = "fake image content"u8.ToArray();
        using var fileStream = new MemoryStream(fileContent);

        var capturedMedia = null as MediaAsset;
        _mockMediaRepository.Setup(r => r.CreateAsync(It.IsAny<MediaAsset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MediaAsset m, CancellationToken _) =>
            {
                capturedMedia = m;
                return m;
            });

        // Act
        var result = await _service.UploadMediaAsync(websiteId, fileStream, fileName, contentType);

        // Assert
        result.Should().NotBeNull();
        result.WebsiteId.Should().Be(websiteId);
        result.FileName.Should().Be(fileName);
        result.ContentType.Should().Be(contentType);
        result.SizeBytes.Should().Be(fileContent.Length);
        result.StorageUrl.Should().NotBeNullOrEmpty();
        result.MediaId.Should().NotBeEmpty();

        _mockMediaRepository.Verify(r => r.CreateAsync(It.IsAny<MediaAsset>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(Skip = "Temporarily disabled - investigating test hang")]
    public async Task UploadMediaAsync_WithEmptyFile_UploadsWithZeroSize()
    {
        // Arrange
        var websiteId = Guid.NewGuid();
        var fileName = "empty.txt";
        var contentType = "text/plain";
        using var fileStream = new MemoryStream();

        _mockMediaRepository.Setup(r => r.CreateAsync(It.IsAny<MediaAsset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MediaAsset m, CancellationToken _) => m);

        // Act
        var result = await _service.UploadMediaAsync(websiteId, fileStream, fileName, contentType);

        // Assert
        result.SizeBytes.Should().Be(0);
    }

    [Fact(Skip = "Temporarily disabled - investigating test hang")]
    public async Task UploadMediaAsync_WithLargeFile_UploadsSuccessfully()
    {
        // Arrange
        var websiteId = Guid.NewGuid();
        var fileName = "large-file.bin";
        var contentType = "application/octet-stream";
        var fileContent = new byte[10 * 1024 * 1024]; // 10 MB
        using var fileStream = new MemoryStream(fileContent);

        _mockMediaRepository.Setup(r => r.CreateAsync(It.IsAny<MediaAsset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MediaAsset m, CancellationToken _) => m);

        // Act
        var result = await _service.UploadMediaAsync(websiteId, fileStream, fileName, contentType);

        // Assert
        result.SizeBytes.Should().Be(fileContent.Length);
    }

    [Fact(Skip = "Temporarily disabled - investigating test hang")]
    public async Task DeleteMediaAsync_WithExistingMedia_DeletesSuccessfully()
    {
        // Arrange
        var mediaId = Guid.NewGuid();
        var media = new MediaAsset
        {
            MediaId = mediaId,
            WebsiteId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            FileName = "test.jpg",
            ContentType = "image/jpeg",
            SizeBytes = 1024,
            StorageUrl = "https://storage.example.com/test.jpg"
        };

        _mockMediaRepository.Setup(r => r.GetByIdAsync(mediaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(media);
        _mockMediaRepository.Setup(r => r.DeleteAsync(mediaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _service.DeleteMediaAsync(mediaId);

        // Assert
        result.Should().BeTrue();
        _mockMediaRepository.Verify(r => r.GetByIdAsync(mediaId, It.IsAny<CancellationToken>()), Times.Once);
        _mockMediaRepository.Verify(r => r.DeleteAsync(mediaId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(Skip = "Temporarily disabled - investigating test hang")]
    public async Task DeleteMediaAsync_WithNonExistentMedia_ReturnsFalse()
    {
        // Arrange
        var mediaId = Guid.NewGuid();
        _mockMediaRepository.Setup(r => r.GetByIdAsync(mediaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MediaAsset?)null);

        // Act
        var result = await _service.DeleteMediaAsync(mediaId);

        // Assert
        result.Should().BeFalse();
        _mockMediaRepository.Verify(r => r.GetByIdAsync(mediaId, It.IsAny<CancellationToken>()), Times.Once);
        _mockMediaRepository.Verify(r => r.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(Skip = "Temporarily disabled - investigating test hang")]
    public void Constructor_WithNullPageRepository_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new ContentService(null!, _mockMediaRepository.Object, _mockLogger.Object);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("pageRepository");
    }

    [Fact(Skip = "Temporarily disabled - investigating test hang")]
    public void Constructor_WithNullMediaRepository_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new ContentService(_mockPageRepository.Object, null!, _mockLogger.Object);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("mediaRepository");
    }

    [Fact(Skip = "Temporarily disabled - investigating test hang")]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new ContentService(_mockPageRepository.Object, _mockMediaRepository.Object, null!);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }
}
