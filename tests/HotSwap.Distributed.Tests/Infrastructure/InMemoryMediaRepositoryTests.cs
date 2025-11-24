using FluentAssertions;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Websites;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HotSwap.Distributed.Tests.Infrastructure;

public class InMemoryMediaRepositoryTests
{
    private readonly InMemoryMediaRepository _repository;
    private readonly Guid _websiteId = Guid.NewGuid();

    public InMemoryMediaRepositoryTests()
    {
        _repository = new InMemoryMediaRepository(
            NullLogger<InMemoryMediaRepository>.Instance);
    }

    #region Create Tests

    [Fact]
    public async Task CreateAsync_WithValidMedia_ShouldCreateMedia()
    {
        // Arrange
        var media = CreateTestMedia();

        // Act
        var created = await _repository.CreateAsync(media);

        // Assert
        created.Should().NotBeNull();
        created.MediaId.Should().NotBe(Guid.Empty);
        created.FileName.Should().Be("test-image.jpg");
        created.WebsiteId.Should().Be(_websiteId);
        created.UploadedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task CreateAsync_WithEmptyMediaId_ShouldGenerateId()
    {
        // Arrange
        var media = CreateTestMedia();
        media.MediaId = Guid.Empty;

        // Act
        var created = await _repository.CreateAsync(media);

        // Assert
        created.MediaId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task CreateAsync_ShouldSetUploadedAtTimestamp()
    {
        // Arrange
        var media = CreateTestMedia();
        var beforeCreation = DateTime.UtcNow;

        // Act
        var created = await _repository.CreateAsync(media);
        var afterCreation = DateTime.UtcNow;

        // Assert
        created.UploadedAt.Should().BeOnOrAfter(beforeCreation);
        created.UploadedAt.Should().BeOnOrBefore(afterCreation);
    }

    #endregion

    #region GetById Tests

    [Fact]
    public async Task GetByIdAsync_WithExistingMedia_ShouldReturnMedia()
    {
        // Arrange
        var media = CreateTestMedia();
        var created = await _repository.CreateAsync(media);

        // Act
        var retrieved = await _repository.GetByIdAsync(created.MediaId);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.MediaId.Should().Be(created.MediaId);
        retrieved.FileName.Should().Be(created.FileName);
    }

    [Fact]
    public async Task GetByIdAsync_WithNonExistentMedia_ShouldReturnNull()
    {
        // Arrange
        var mediaId = Guid.NewGuid();

        // Act
        var retrieved = await _repository.GetByIdAsync(mediaId);

        // Assert
        retrieved.Should().BeNull();
    }

    #endregion

    #region GetByWebsiteId Tests

    [Fact]
    public async Task GetByWebsiteIdAsync_ShouldReturnAllMediaForWebsite()
    {
        // Arrange
        var media1 = CreateTestMedia("image1.jpg");
        var media2 = CreateTestMedia("image2.jpg");
        var media3 = CreateTestMedia("image3.jpg");
        media3.WebsiteId = Guid.NewGuid(); // Different website

        await _repository.CreateAsync(media1);
        await _repository.CreateAsync(media2);
        await _repository.CreateAsync(media3);

        // Act
        var mediaList = await _repository.GetByWebsiteIdAsync(_websiteId);

        // Assert
        mediaList.Should().HaveCount(2);
        mediaList.Should().Contain(m => m.FileName == "image1.jpg");
        mediaList.Should().Contain(m => m.FileName == "image2.jpg");
        mediaList.Should().NotContain(m => m.FileName == "image3.jpg");
    }

    [Fact]
    public async Task GetByWebsiteIdAsync_WhenNoMedia_ShouldReturnEmptyList()
    {
        // Act
        var mediaList = await _repository.GetByWebsiteIdAsync(_websiteId);

        // Assert
        mediaList.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByWebsiteIdAsync_WithDifferentWebsite_ShouldNotIncludeOtherWebsiteMedia()
    {
        // Arrange
        var media1 = CreateTestMedia("website1-image.jpg");
        var media2 = CreateTestMedia("website2-image.jpg");
        media2.WebsiteId = Guid.NewGuid();

        await _repository.CreateAsync(media1);
        await _repository.CreateAsync(media2);

        // Act
        var mediaList = await _repository.GetByWebsiteIdAsync(_websiteId);

        // Assert
        mediaList.Should().HaveCount(1);
        mediaList.Should().Contain(m => m.FileName == "website1-image.jpg");
        mediaList.Should().NotContain(m => m.FileName == "website2-image.jpg");
    }

    #endregion

    #region Delete Tests

    [Fact]
    public async Task DeleteAsync_WithExistingMedia_ShouldHardDeleteMedia()
    {
        // Arrange
        var media = CreateTestMedia();
        var created = await _repository.CreateAsync(media);

        // Act
        var result = await _repository.DeleteAsync(created.MediaId);

        // Assert
        result.Should().BeTrue();

        var retrieved = await _repository.GetByIdAsync(created.MediaId);
        retrieved.Should().BeNull(); // Hard delete
    }

    [Fact]
    public async Task DeleteAsync_WithNonExistentMedia_ShouldReturnFalse()
    {
        // Arrange
        var mediaId = Guid.NewGuid();

        // Act
        var result = await _repository.DeleteAsync(mediaId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsync_ShouldRemoveMediaFromWebsiteList()
    {
        // Arrange
        var media1 = CreateTestMedia("image1.jpg");
        var media2 = CreateTestMedia("image2.jpg");
        await _repository.CreateAsync(media1);
        var created2 = await _repository.CreateAsync(media2);

        // Act
        await _repository.DeleteAsync(created2.MediaId);

        // Assert
        var mediaList = await _repository.GetByWebsiteIdAsync(_websiteId);
        mediaList.Should().HaveCount(1);
        mediaList.Should().Contain(m => m.FileName == "image1.jpg");
        mediaList.Should().NotContain(m => m.FileName == "image2.jpg");
    }

    #endregion

    #region Multiple Operations Tests

    [Fact]
    public async Task MultipleOperations_CreateAndDelete_ShouldMaintainConsistency()
    {
        // Arrange & Act
        var media1 = await _repository.CreateAsync(CreateTestMedia("image1.jpg"));
        var media2 = await _repository.CreateAsync(CreateTestMedia("image2.jpg"));
        var media3 = await _repository.CreateAsync(CreateTestMedia("image3.jpg"));

        await _repository.DeleteAsync(media2.MediaId);

        // Assert
        var allMedia = await _repository.GetByWebsiteIdAsync(_websiteId);
        allMedia.Should().HaveCount(2);
        allMedia.Should().Contain(m => m.MediaId == media1.MediaId);
        allMedia.Should().Contain(m => m.MediaId == media3.MediaId);
        allMedia.Should().NotContain(m => m.MediaId == media2.MediaId);
    }

    #endregion

    #region Helper Methods

    private MediaAsset CreateTestMedia(string fileName = "test-image.jpg")
    {
        return new MediaAsset
        {
            MediaId = Guid.NewGuid(),
            WebsiteId = _websiteId,
            TenantId = Guid.NewGuid(),
            FileName = fileName,
            ContentType = "image/jpeg",
            SizeBytes = 1024 * 100, // 100 KB
            StorageUrl = $"https://cdn.example.com/media/{fileName}"
        };
    }

    #endregion
}
