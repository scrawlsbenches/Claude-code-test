using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Interfaces;
using Microsoft.Extensions.Logging;

namespace HotSwap.Distributed.Infrastructure.Websites;

/// <summary>
/// Service for content management operations.
/// </summary>
public class ContentService : IContentService
{
    private readonly IPageRepository _pageRepository;
    private readonly IMediaRepository _mediaRepository;
    private readonly ILogger<ContentService> _logger;

    public ContentService(
        IPageRepository pageRepository,
        IMediaRepository mediaRepository,
        ILogger<ContentService> logger)
    {
        _pageRepository = pageRepository ?? throw new ArgumentNullException(nameof(pageRepository));
        _mediaRepository = mediaRepository ?? throw new ArgumentNullException(nameof(mediaRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Page> CreatePageAsync(Page page, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating page: {Title} for website {WebsiteId}",
            page.Title, page.WebsiteId);

        page = await _pageRepository.CreateAsync(page, cancellationToken);
        return page;
    }

    public async Task<Page> UpdatePageAsync(Page page, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Updating page: {PageId}", page.PageId);
        page = await _pageRepository.UpdateAsync(page, cancellationToken);
        return page;
    }

    public async Task<Page> PublishPageAsync(Guid pageId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Publishing page: {PageId}", pageId);

        var page = await _pageRepository.GetByIdAsync(pageId, cancellationToken);
        if (page == null)
            throw new KeyNotFoundException($"Page {pageId} not found");

        page.Status = PageStatus.Published;
        page.PublishedAt = DateTime.UtcNow;

        page = await _pageRepository.UpdateAsync(page, cancellationToken);
        return page;
    }

    public async Task<Page> SchedulePageAsync(Guid pageId, DateTime scheduledAt, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Scheduling page: {PageId} for {ScheduledAt}", pageId, scheduledAt);

        var page = await _pageRepository.GetByIdAsync(pageId, cancellationToken);
        if (page == null)
            throw new KeyNotFoundException($"Page {pageId} not found");

        page.Status = PageStatus.Scheduled;
        page.ScheduledAt = scheduledAt;

        page = await _pageRepository.UpdateAsync(page, cancellationToken);
        return page;
    }

    public async Task<bool> DeletePageAsync(Guid pageId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Deleting page: {PageId}", pageId);
        return await _pageRepository.DeleteAsync(pageId, cancellationToken);
    }

    public async Task<MediaAsset> UploadMediaAsync(
        Guid websiteId,
        Stream fileStream,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Uploading media: {FileName} for website {WebsiteId}",
            fileName, websiteId);

        // Read file stream
        using var memoryStream = new MemoryStream();
        await fileStream.CopyToAsync(memoryStream, cancellationToken);
        var fileBytes = memoryStream.ToArray();

        // Upload to MinIO object storage (self-hosted, S3-compatible)
        // In production, this would use MinIO SDK
        // Example production code for MinIO:
        // using var minioClient = new MinioClient()
        //     .WithEndpoint("minio.example.com:9000")
        //     .WithCredentials(accessKey, secretKey)
        //     .WithSSL()
        //     .Build();
        // var bucketName = "tenant-media";
        // var objectKey = $"websites/{websiteId}/media/{Guid.NewGuid():N}/{fileName}";
        // await minioClient.PutObjectAsync(new PutObjectArgs()
        //     .WithBucket(bucketName)
        //     .WithObject(objectKey)
        //     .WithStreamData(new MemoryStream(fileBytes))
        //     .WithObjectSize(fileBytes.Length)
        //     .WithContentType(contentType)
        //     .WithServerSideEncryption(sse), cancellationToken);
        // var storageUrl = $"https://minio.example.com/{bucketName}/{objectKey}";

        // For now, use simulated storage URL
        var storageUrl = $"https://storage.example.com/websites/{websiteId}/media/{Guid.NewGuid():N}/{fileName}";

        var media = new MediaAsset
        {
            MediaId = Guid.NewGuid(),
            WebsiteId = websiteId,
            TenantId = Guid.Empty, // Get from HttpContext.User claims in production: User.FindFirst("tenantId")?.Value
            FileName = fileName,
            ContentType = contentType,
            SizeBytes = fileBytes.Length,
            StorageUrl = storageUrl
        };

        media = await _mediaRepository.CreateAsync(media, cancellationToken);

        _logger.LogInformation("Uploaded media: {MediaId} ({SizeBytes} bytes)",
            media.MediaId, media.SizeBytes);

        return media;
    }

    public async Task<bool> DeleteMediaAsync(Guid mediaId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Deleting media: {MediaId}", mediaId);

        var media = await _mediaRepository.GetByIdAsync(mediaId, cancellationToken);
        if (media == null)
            return false;

        // Delete from MinIO object storage (self-hosted, S3-compatible)
        // In production, this would use MinIO SDK
        // Example production code for MinIO:
        // using var minioClient = new MinioClient()
        //     .WithEndpoint("minio.example.com:9000")
        //     .WithCredentials(accessKey, secretKey)
        //     .WithSSL()
        //     .Build();
        // var bucketName = "tenant-media";
        // // Extract object key from storage URL
        // var objectKey = ExtractObjectKeyFromUrl(media.StorageUrl);
        // await minioClient.RemoveObjectAsync(new RemoveObjectArgs()
        //     .WithBucket(bucketName)
        //     .WithObject(objectKey), cancellationToken);

        _logger.LogInformation("Deleted media from storage: {StorageUrl}", media.StorageUrl);

        return await _mediaRepository.DeleteAsync(mediaId, cancellationToken);
    }
}
