using HotSwap.Distributed.Api.Models;
using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotSwap.Distributed.Api.Controllers;

/// <summary>
/// API for managing website content (pages, media).
/// </summary>
[ApiController]
[Route("api/v1/websites/{websiteId}/content")]
[Produces("application/json")]
[Authorize]
public class ContentController : ControllerBase
{
    private readonly IContentService _contentService;
    private readonly IPageRepository _pageRepository;
    private readonly IMediaRepository _mediaRepository;
    private readonly ITenantContextService _tenantContext;
    private readonly ILogger<ContentController> _logger;

    public ContentController(
        IContentService contentService,
        IPageRepository pageRepository,
        IMediaRepository mediaRepository,
        ITenantContextService tenantContext,
        ILogger<ContentController> logger)
    {
        _contentService = contentService;
        _pageRepository = pageRepository;
        _mediaRepository = mediaRepository;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new page.
    /// </summary>
    [HttpPost("pages")]
    [ProducesResponseType(typeof(PageResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreatePage(
        Guid websiteId,
        [FromBody] CreatePageRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.GetCurrentTenantId();
        if (tenantId == null)
            return BadRequest(new ErrorResponse { Error = "Tenant context required" });

        var page = new Page
        {
            WebsiteId = websiteId,
            TenantId = tenantId.Value,
            Title = request.Title,
            Slug = request.Slug.ToLowerInvariant(),
            Content = request.Content,
            Template = request.Template ?? "default",
            Status = PageStatus.Draft
        };

        if (request.Seo != null)
        {
            page.Seo = new SeoMetadata
            {
                MetaTitle = request.Seo.MetaTitle,
                MetaDescription = request.Seo.MetaDescription,
                MetaKeywords = request.Seo.MetaKeywords
            };
        }

        page = await _contentService.CreatePageAsync(page, cancellationToken);

        return CreatedAtAction(nameof(GetPage), new { websiteId, pageId = page.PageId }, MapToPageResponse(page));
    }

    /// <summary>
    /// Gets a page by ID.
    /// </summary>
    [HttpGet("pages/{pageId}")]
    [ProducesResponseType(typeof(PageResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPage(
        Guid websiteId,
        Guid pageId,
        CancellationToken cancellationToken)
    {
        var page = await _pageRepository.GetByIdAsync(pageId, cancellationToken);
        if (page == null || page.WebsiteId != websiteId)
            return NotFound(new ErrorResponse { Error = "Page not found" });

        return Ok(MapToPageResponse(page));
    }

    /// <summary>
    /// Lists all pages for a website.
    /// </summary>
    [HttpGet("pages")]
    [ProducesResponseType(typeof(List<PageResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListPages(
        Guid websiteId,
        CancellationToken cancellationToken)
    {
        var pages = await _pageRepository.GetByWebsiteIdAsync(websiteId, cancellationToken: cancellationToken);
        var responses = pages.Select(MapToPageResponse).ToList();

        return Ok(responses);
    }

    /// <summary>
    /// Publishes a page.
    /// </summary>
    [HttpPost("pages/{pageId}/publish")]
    [ProducesResponseType(typeof(PageResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> PublishPage(
        Guid websiteId,
        Guid pageId,
        CancellationToken cancellationToken)
    {
        var page = await _contentService.PublishPageAsync(pageId, cancellationToken);
        return Ok(MapToPageResponse(page));
    }

    /// <summary>
    /// Uploads media to a website.
    /// </summary>
    [HttpPost("media")]
    [ProducesResponseType(typeof(MediaAssetResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> UploadMedia(
        Guid websiteId,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new ErrorResponse { Error = "File is required" });

        using var stream = file.OpenReadStream();
        var media = await _contentService.UploadMediaAsync(
            websiteId,
            stream,
            file.FileName,
            file.ContentType,
            cancellationToken);

        return CreatedAtAction(nameof(GetMedia), new { websiteId, mediaId = media.MediaId }, MapToMediaResponse(media));
    }

    /// <summary>
    /// Gets a media asset by ID.
    /// </summary>
    [HttpGet("media/{mediaId}")]
    [ProducesResponseType(typeof(MediaAssetResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMedia(
        Guid websiteId,
        Guid mediaId,
        CancellationToken cancellationToken)
    {
        var media = await _mediaRepository.GetByIdAsync(mediaId, cancellationToken);
        if (media == null || media.WebsiteId != websiteId)
            return NotFound(new ErrorResponse { Error = "Media not found" });

        return Ok(MapToMediaResponse(media));
    }

    /// <summary>
    /// Lists all media for a website.
    /// </summary>
    [HttpGet("media")]
    [ProducesResponseType(typeof(List<MediaAssetResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListMedia(
        Guid websiteId,
        CancellationToken cancellationToken)
    {
        var media = await _mediaRepository.GetByWebsiteIdAsync(websiteId, cancellationToken);
        var responses = media.Select(MapToMediaResponse).ToList();

        return Ok(responses);
    }

    private static PageResponse MapToPageResponse(Page page)
    {
        return new PageResponse
        {
            PageId = page.PageId,
            Title = page.Title,
            Slug = page.Slug,
            Content = page.Content,
            Status = page.Status.ToString(),
            PublishedAt = page.PublishedAt,
            Version = page.Version
        };
    }

    private static MediaAssetResponse MapToMediaResponse(MediaAsset media)
    {
        return new MediaAssetResponse
        {
            MediaId = media.MediaId,
            FileName = media.FileName,
            ContentType = media.ContentType,
            SizeBytes = media.SizeBytes,
            StorageUrl = media.StorageUrl,
            UploadedAt = media.UploadedAt
        };
    }
}
