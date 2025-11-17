using HotSwap.Distributed.Domain.Enums;

namespace HotSwap.Distributed.Domain.Models;

/// <summary>
/// Represents a page or post in a website.
/// </summary>
public class Page
{
    /// <summary>
    /// Unique identifier for the page.
    /// </summary>
    public Guid PageId { get; set; }

    /// <summary>
    /// Website that this page belongs to.
    /// </summary>
    public Guid WebsiteId { get; set; }

    /// <summary>
    /// Tenant that owns this page.
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// Page title.
    /// </summary>
    public required string Title { get; set; }

    /// <summary>
    /// URL slug (e.g., "about-us" for /about-us).
    /// </summary>
    public required string Slug { get; set; }

    /// <summary>
    /// Page content (HTML or Markdown).
    /// </summary>
    public required string Content { get; set; }

    /// <summary>
    /// Publication status.
    /// </summary>
    public PageStatus Status { get; set; } = PageStatus.Draft;

    /// <summary>
    /// Date and time when the page was published.
    /// </summary>
    public DateTime? PublishedAt { get; set; }

    /// <summary>
    /// Date and time when the page is scheduled to be published.
    /// </summary>
    public DateTime? ScheduledAt { get; set; }

    /// <summary>
    /// SEO metadata.
    /// </summary>
    public SeoMetadata Seo { get; set; } = new();

    /// <summary>
    /// Page version (for content versioning).
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// Featured image URL.
    /// </summary>
    public string? FeaturedImageUrl { get; set; }

    /// <summary>
    /// Page template name (e.g., "default", "full-width").
    /// </summary>
    public string Template { get; set; } = "default";

    /// <summary>
    /// Date and time when the page was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Date and time when the page was last updated.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Checks if the page is published.
    /// </summary>
    public bool IsPublished() => Status == PageStatus.Published && PublishedAt.HasValue;

    /// <summary>
    /// Checks if the page is scheduled for future publication.
    /// </summary>
    public bool IsScheduled() => Status == PageStatus.Scheduled && ScheduledAt.HasValue;
}
