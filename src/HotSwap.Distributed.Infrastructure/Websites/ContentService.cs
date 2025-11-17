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

        // TODO: Upload to S3 or object storage
        var storageUrl = $"https://storage.example.com/websites/{websiteId}/media/{fileName}";

        var media = new MediaAsset
        {
            MediaId = Guid.NewGuid(),
            WebsiteId = websiteId,
            TenantId = Guid.Empty, // TODO: Get from context
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

        // TODO: Delete from S3 or object storage

        return await _mediaRepository.DeleteAsync(mediaId, cancellationToken);
    }
}
