using FluentAssertions;
using Xunit;

namespace HotSwap.Distributed.Tests.Infrastructure.Storage;

public class MockObjectStorageServiceTests
{
    private readonly MockObjectStorageService _service;

    public MockObjectStorageServiceTests()
    {
        _service = new MockObjectStorageService();
    }

    [Fact]
    public async Task CreateBucketAsync_ShouldCreateBucket()
    {
        // Act
        var created = await _service.CreateBucketAsync("test-bucket");

        // Assert
        created.Should().BeTrue();
        var exists = await _service.BucketExistsAsync("test-bucket");
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task CreateBucketAsync_WhenBucketExists_ShouldReturnFalse()
    {
        // Arrange
        await _service.CreateBucketAsync("test-bucket");

        // Act
        var created = await _service.CreateBucketAsync("test-bucket");

        // Assert
        created.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteBucketAsync_ShouldRemoveBucket()
    {
        // Arrange
        await _service.CreateBucketAsync("test-bucket");

        // Act
        await _service.DeleteBucketAsync("test-bucket");

        // Assert
        var exists = await _service.BucketExistsAsync("test-bucket");
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task UploadObjectAsync_ShouldStoreObject()
    {
        // Arrange
        await _service.CreateBucketAsync("test-bucket");
        var data = "Hello, World!"u8.ToArray();
        var stream = new MemoryStream(data);

        // Act
        var url = await _service.UploadObjectAsync("test-bucket", "hello.txt", stream, "text/plain");

        // Assert
        url.Should().Contain("test-bucket");
        url.Should().Contain("hello.txt");

        var count = await _service.GetObjectCountAsync("test-bucket");
        count.Should().Be(1);
    }

    [Fact]
    public async Task UploadObjectAsync_WhenBucketDoesNotExist_ShouldThrow()
    {
        // Arrange
        var stream = new MemoryStream("data"u8.ToArray());

        // Act & Assert
        await FluentActions.Awaiting(() =>
            _service.UploadObjectAsync("nonexistent", "file.txt", stream, "text/plain"))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task DownloadObjectAsync_ShouldRetrieveData()
    {
        // Arrange
        await _service.CreateBucketAsync("test-bucket");
        var originalData = "Test Data Content"u8.ToArray();
        await _service.UploadObjectAsync("test-bucket", "data.bin", new MemoryStream(originalData), "application/octet-stream");

        // Act
        var downloadedStream = await _service.DownloadObjectAsync("test-bucket", "data.bin");

        // Assert
        using var memStream = new MemoryStream();
        await downloadedStream.CopyToAsync(memStream);
        var downloadedData = memStream.ToArray();

        downloadedData.Should().Equal(originalData);
    }

    [Fact]
    public async Task DownloadObjectAsync_WhenObjectNotFound_ShouldThrow()
    {
        // Arrange
        await _service.CreateBucketAsync("test-bucket");

        // Act & Assert
        await FluentActions.Awaiting(() =>
            _service.DownloadObjectAsync("test-bucket", "nonexistent.txt"))
            .Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task DeleteObjectAsync_ShouldRemoveObject()
    {
        // Arrange
        await _service.CreateBucketAsync("test-bucket");
        var data = "data"u8.ToArray();
        await _service.UploadObjectAsync("test-bucket", "file.txt", new MemoryStream(data), "text/plain");

        // Act
        await _service.DeleteObjectAsync("test-bucket", "file.txt");

        // Assert
        var count = await _service.GetObjectCountAsync("test-bucket");
        count.Should().Be(0);
    }

    [Fact]
    public async Task GetBucketSizeAsync_ShouldCalculateTotalSize()
    {
        // Arrange
        await _service.CreateBucketAsync("test-bucket");

        var file1 = new byte[1000];
        var file2 = new byte[2000];
        var file3 = new byte[500];

        await _service.UploadObjectAsync("test-bucket", "file1.bin", new MemoryStream(file1), "application/octet-stream");
        await _service.UploadObjectAsync("test-bucket", "file2.bin", new MemoryStream(file2), "application/octet-stream");
        await _service.UploadObjectAsync("test-bucket", "file3.bin", new MemoryStream(file3), "application/octet-stream");

        // Act
        var size = await _service.GetBucketSizeAsync("test-bucket");

        // Assert
        size.Should().Be(3500);
    }

    [Fact]
    public async Task GetBucketSizeAsync_WhenBucketEmpty_ShouldReturnZero()
    {
        // Arrange
        await _service.CreateBucketAsync("test-bucket");

        // Act
        var size = await _service.GetBucketSizeAsync("test-bucket");

        // Assert
        size.Should().Be(0);
    }

    [Fact]
    public async Task GetObjectCountAsync_ShouldCountObjects()
    {
        // Arrange
        await _service.CreateBucketAsync("test-bucket");

        for (int i = 0; i < 5; i++)
        {
            var data = new byte[100];
            await _service.UploadObjectAsync("test-bucket", $"file{i}.dat", new MemoryStream(data), "application/octet-stream");
        }

        // Act
        var count = await _service.GetObjectCountAsync("test-bucket");

        // Assert
        count.Should().Be(5);
    }

    [Fact]
    public async Task ListObjectsAsync_ShouldReturnAllObjects()
    {
        // Arrange
        await _service.CreateBucketAsync("test-bucket");

        var fileNames = new[] { "file1.txt", "file2.txt", "file3.txt" };
        foreach (var fileName in fileNames)
        {
            var data = new byte[100];
            await _service.UploadObjectAsync("test-bucket", fileName, new MemoryStream(data), "text/plain");
        }

        // Act
        var objects = await _service.ListObjectsAsync("test-bucket");

        // Assert
        objects.Should().HaveCount(3);
        objects.Should().Contain(fileNames);
    }

    [Fact]
    public async Task ListObjectsAsync_WithPrefix_ShouldFilterObjects()
    {
        // Arrange
        await _service.CreateBucketAsync("test-bucket");

        await _service.UploadObjectAsync("test-bucket", "images/photo1.jpg", new MemoryStream(new byte[100]), "image/jpeg");
        await _service.UploadObjectAsync("test-bucket", "images/photo2.jpg", new MemoryStream(new byte[100]), "image/jpeg");
        await _service.UploadObjectAsync("test-bucket", "documents/doc1.pdf", new MemoryStream(new byte[100]), "application/pdf");

        // Act
        var images = await _service.ListObjectsAsync("test-bucket", "images/");

        // Assert
        images.Should().HaveCount(2);
        images.Should().AllSatisfy(name => name.Should().StartWith("images/"));
    }

    [Fact]
    public async Task HealthCheckAsync_ShouldReturnHealthStatus()
    {
        // Act
        var healthy = await _service.HealthCheckAsync();

        // Assert
        healthy.Should().BeTrue();
    }

    [Fact]
    public async Task HealthCheckAsync_WhenUnhealthy_ShouldReturnFalse()
    {
        // Arrange
        _service.SetHealthy(false);

        // Act
        var healthy = await _service.HealthCheckAsync();

        // Assert
        healthy.Should().BeFalse();
    }

    [Fact]
    public async Task GetBucketCount_ShouldReturnNumberOfBuckets()
    {
        // Arrange
        await _service.CreateBucketAsync("bucket1");
        await _service.CreateBucketAsync("bucket2");
        await _service.CreateBucketAsync("bucket3");

        // Act
        var count = _service.GetBucketCount();

        // Assert
        count.Should().Be(3);
    }

    [Fact]
    public async Task Clear_ShouldRemoveAllBuckets()
    {
        // Arrange
        await _service.CreateBucketAsync("bucket1");
        await _service.CreateBucketAsync("bucket2");

        // Act
        _service.Clear();

        // Assert
        _service.GetBucketCount().Should().Be(0);
        var exists1 = await _service.BucketExistsAsync("bucket1");
        var exists2 = await _service.BucketExistsAsync("bucket2");
        exists1.Should().BeFalse();
        exists2.Should().BeFalse();
    }
}
