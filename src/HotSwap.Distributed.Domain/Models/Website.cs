using HotSwap.Distributed.Domain.Enums;

namespace HotSwap.Distributed.Domain.Models;

/// <summary>
/// Represents a website within a tenant's account.
/// </summary>
public class Website
{
    /// <summary>
    /// Unique identifier for the website.
    /// </summary>
    public Guid WebsiteId { get; set; }

    /// <summary>
    /// Tenant that owns this website.
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// Website name (e.g., "My Blog").
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Subdomain for website access (e.g., "myblog" for myblog.tenant.platform.com).
    /// </summary>
    public required string Subdomain { get; set; }

    /// <summary>
    /// Custom domains mapped to this website.
    /// </summary>
    public List<string> CustomDomains { get; set; } = new();

    /// <summary>
    /// Current operational status.
    /// </summary>
    public WebsiteStatus Status { get; set; } = WebsiteStatus.Provisioning;

    /// <summary>
    /// Current active theme ID.
    /// </summary>
    public Guid CurrentThemeId { get; set; }

    /// <summary>
    /// Current theme version.
    /// </summary>
    public string? ThemeVersion { get; set; }

    /// <summary>
    /// List of installed plugin IDs.
    /// </summary>
    public List<Guid> InstalledPluginIds { get; set; } = new();

    /// <summary>
    /// Website configuration settings.
    /// </summary>
    public Dictionary<string, string> Configuration { get; set; } = new();

    /// <summary>
    /// Date and time when the website was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Date and time when the website was last updated.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Checks if the website is active.
    /// </summary>
    public bool IsActive() => Status == WebsiteStatus.Active;

    /// <summary>
    /// Checks if the website is suspended.
    /// </summary>
    public bool IsSuspended() => Status == WebsiteStatus.Suspended;
}
