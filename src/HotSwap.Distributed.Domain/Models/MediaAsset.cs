namespace HotSwap.Distributed.Domain.Models;

/// <summary>
/// Represents a media asset (image, video, document) in a website.
/// </summary>
public class MediaAsset
{
    /// <summary>
    /// Unique identifier for the media asset.
    /// </summary>
    public Guid MediaId { get; set; }

    /// <summary>
    /// Website that this media belongs to.
    /// </summary>
    public Guid WebsiteId { get; set; }

    /// <summary>
    /// Tenant that owns this media.
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// Original filename.
    /// </summary>
    public required string FileName { get; set; }

    /// <summary>
    /// MIME content type (e.g., "image/jpeg", "video/mp4").
    /// </summary>
    public required string ContentType { get; set; }

    /// <summary>
    /// File size in bytes.
    /// </summary>
    public long SizeBytes { get; set; }

    /// <summary>
    /// Storage URL (S3, CDN, etc.).
    /// </summary>
    public required string StorageUrl { get; set; }

    /// <summary>
    /// Alt text for accessibility.
    /// </summary>
    public string? AltText { get; set; }

    /// <summary>
    /// Additional metadata (dimensions, duration, etc.).
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();

    /// <summary>
    /// Date and time when the media was uploaded.
    /// </summary>
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Checks if the media is an image.
    /// </summary>
    public bool IsImage() => ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Checks if the media is a video.
    /// </summary>
    public bool IsVideo() => ContentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Checks if the media is a document.
    /// </summary>
    public bool IsDocument() => ContentType.StartsWith("application/", StringComparison.OrdinalIgnoreCase);
}
