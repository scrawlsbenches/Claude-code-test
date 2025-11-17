namespace HotSwap.Distributed.Domain.Enums;

/// <summary>
/// Represents the publication status of a page or post.
/// </summary>
public enum PageStatus
{
    /// <summary>
    /// Content is in draft state (not published).
    /// </summary>
    Draft,

    /// <summary>
    /// Content is published and visible.
    /// </summary>
    Published,

    /// <summary>
    /// Content is scheduled for future publication.
    /// </summary>
    Scheduled,

    /// <summary>
    /// Content is archived (no longer published).
    /// </summary>
    Archived
}
