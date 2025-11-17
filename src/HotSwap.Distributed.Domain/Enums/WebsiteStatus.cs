namespace HotSwap.Distributed.Domain.Enums;

/// <summary>
/// Represents the operational status of a website.
/// </summary>
public enum WebsiteStatus
{
    /// <summary>
    /// Website is being provisioned.
    /// </summary>
    Provisioning,

    /// <summary>
    /// Website is active and accessible.
    /// </summary>
    Active,

    /// <summary>
    /// Website is suspended (not accessible).
    /// </summary>
    Suspended,

    /// <summary>
    /// Website is in maintenance mode.
    /// </summary>
    Maintenance,

    /// <summary>
    /// Website has been deleted.
    /// </summary>
    Deleted
}
