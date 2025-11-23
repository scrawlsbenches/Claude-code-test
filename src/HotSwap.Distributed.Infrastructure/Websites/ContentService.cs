using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Interfaces;
using HotSwap.Distributed.Infrastructure.Storage;
using Microsoft.Extensions.Logging;

namespace HotSwap.Distributed.Infrastructure.Websites;

/// <summary>
/// Service for content management operations.
/// </summary>
public class ContentService : IContentService
{
    private readonly IPageRepository _pageRepository;
    private readonly IMediaRepository _mediaRepository;
    private readonly IObjectStorageService? _storageService;
    private readonly ILogger<ContentService> _logger;
    private const string MediaBucketName = "tenant-media";

    public ContentService(
        IPageRepository pageRepository,
        IMediaRepository mediaRepository,
        ILogger<ContentService> logger,
        IObjectStorageService? storageService = null)
    {
        _pageRepository = pageRepository ?? throw new ArgumentNullException(nameof(pageRepository));
        _mediaRepository = mediaRepository ?? throw new ArgumentNullException(nameof(mediaRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _storageService = storageService; // Optional - falls back to simulated storage if not provided
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

        // Read file stream into memory
        using var memoryStream = new MemoryStream();
        await fileStream.CopyToAsync(memoryStream, cancellationToken);
        var fileBytes = memoryStream.ToArray();

        string storageUrl;

        if (_storageService != null)
        {
            // Upload to MinIO object storage (self-hosted, S3-compatible)
            var objectKey = $"websites/{websiteId}/media/{Guid.NewGuid():N}/{fileName}";

            // Ensure bucket exists
            var bucketExists = await _storageService.BucketExistsAsync(MediaBucketName, cancellationToken);
            if (!bucketExists)
            {
                await _storageService.CreateBucketAsync(MediaBucketName, cancellationToken);
                _logger.LogInformation("Created media bucket: {BucketName}", MediaBucketName);
            }

            // Upload file
            memoryStream.Position = 0; // Reset stream position
            storageUrl = await _storageService.UploadObjectAsync(
                MediaBucketName,
                objectKey,
                memoryStream,
                contentType,
                cancellationToken);

            _logger.LogInformation("Uploaded media to MinIO: {ObjectKey}", objectKey);
        }
        else
        {
            // Fallback to simulated storage (for testing without MinIO)
            storageUrl = $"https://storage.example.com/websites/{websiteId}/media/{Guid.NewGuid():N}/{fileName}";
            _logger.LogWarning("No storage service configured - using simulated storage URL");
        }

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

        if (_storageService != null)
        {
            // Extract object key from storage URL
            var objectKey = ExtractObjectKeyFromUrl(media.StorageUrl);

            if (!string.IsNullOrEmpty(objectKey))
            {
                try
                {
                    await _storageService.DeleteObjectAsync(MediaBucketName, objectKey, cancellationToken);
                    _logger.LogInformation("Deleted media from MinIO: {ObjectKey}", objectKey);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete media from storage: {ObjectKey} - continuing with database deletion", objectKey);
                    // Don't fail the entire operation if storage deletion fails
                }
            }
            else
            {
                _logger.LogWarning("Could not extract object key from storage URL: {StorageUrl}", media.StorageUrl);
            }
        }
        else
        {
            _logger.LogWarning("No storage service configured - simulated media deletion for {MediaId}", mediaId);
        }

        _logger.LogInformation("Deleted media from storage: {StorageUrl}", media.StorageUrl);

        return await _mediaRepository.DeleteAsync(mediaId, cancellationToken);
    }

    /// <summary>
    /// Extracts the object key from a MinIO storage URL.
    /// Expected URL format: http(s)://endpoint/bucket/object-key
    /// </summary>
    private static string ExtractObjectKeyFromUrl(string storageUrl)
    {
        try
        {
            var uri = new Uri(storageUrl);
            // Remove leading slash and bucket name to get object key
            var path = uri.AbsolutePath.TrimStart('/');
            var segments = path.Split('/', 2);
            return segments.Length >= 2 ? segments[1] : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
}
