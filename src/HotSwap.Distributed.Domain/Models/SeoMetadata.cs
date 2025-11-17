namespace HotSwap.Distributed.Domain.Models;

/// <summary>
/// SEO metadata for pages and posts.
/// </summary>
public class SeoMetadata
{
    /// <summary>
    /// Meta title (overrides page title if set).
    /// </summary>
    public string? MetaTitle { get; set; }

    /// <summary>
    /// Meta description.
    /// </summary>
    public string? MetaDescription { get; set; }

    /// <summary>
    /// Meta keywords (comma-separated).
    /// </summary>
    public string? MetaKeywords { get; set; }

    /// <summary>
    /// Canonical URL.
    /// </summary>
    public string? CanonicalUrl { get; set; }

    /// <summary>
    /// Open Graph title.
    /// </summary>
    public string? OgTitle { get; set; }

    /// <summary>
    /// Open Graph description.
    /// </summary>
    public string? OgDescription { get; set; }

    /// <summary>
    /// Open Graph image URL.
    /// </summary>
    public string? OgImageUrl { get; set; }

    /// <summary>
    /// Twitter card type (summary, summary_large_image, etc.).
    /// </summary>
    public string? TwitterCard { get; set; }

    /// <summary>
    /// Robots meta tag (index, noindex, follow, nofollow).
    /// </summary>
    public string Robots { get; set; } = "index, follow";
}
