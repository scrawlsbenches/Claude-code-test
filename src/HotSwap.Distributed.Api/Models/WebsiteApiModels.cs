namespace HotSwap.Distributed.Api.Models;

#region Request Models

/// <summary>
/// Request to create a new website.
/// </summary>
public class CreateWebsiteRequest
{
    public required string Name { get; set; }
    public required string Subdomain { get; set; }
    public List<string>? CustomDomains { get; set; }
    public Guid? ThemeId { get; set; }
}

/// <summary>
/// Request to create or update a page.
/// </summary>
public class CreatePageRequest
{
    public required string Title { get; set; }
    public required string Slug { get; set; }
    public required string Content { get; set; }
    public string? Template { get; set; }
    public SeoMetadataRequest? Seo { get; set; }
}

/// <summary>
/// SEO metadata request.
/// </summary>
public class SeoMetadataRequest
{
    public string? MetaTitle { get; set; }
    public string? MetaDescription { get; set; }
    public string? MetaKeywords { get; set; }
}

#endregion

#region Response Models

/// <summary>
/// Website information response.
/// </summary>
public class WebsiteResponse
{
    public Guid WebsiteId { get; set; }
    public required string Name { get; set; }
    public required string Subdomain { get; set; }
    public List<string> CustomDomains { get; set; } = new();
    public required string Status { get; set; }
    public Guid CurrentThemeId { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Page information response.
/// </summary>
public class PageResponse
{
    public Guid PageId { get; set; }
    public required string Title { get; set; }
    public required string Slug { get; set; }
    public required string Content { get; set; }
    public required string Status { get; set; }
    public DateTime? PublishedAt { get; set; }
    public int Version { get; set; }
}

/// <summary>
/// Media asset response.
/// </summary>
public class MediaAssetResponse
{
    public Guid MediaId { get; set; }
    public required string FileName { get; set; }
    public required string ContentType { get; set; }
    public long SizeBytes { get; set; }
    public required string StorageUrl { get; set; }
    public DateTime UploadedAt { get; set; }
}

/// <summary>
/// Theme information response.
/// </summary>
public class ThemeResponse
{
    public Guid ThemeId { get; set; }
    public required string Name { get; set; }
    public required string Version { get; set; }
    public required string Author { get; set; }
    public string? Description { get; set; }
    public string? PreviewImageUrl { get; set; }
}

/// <summary>
/// Plugin information response.
/// </summary>
public class PluginResponse
{
    public Guid PluginId { get; set; }
    public required string Name { get; set; }
    public required string Version { get; set; }
    public required string Category { get; set; }
    public string? Description { get; set; }
}

#endregion
