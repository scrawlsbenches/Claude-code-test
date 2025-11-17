using HotSwap.Distributed.Domain.Models;

namespace HotSwap.Distributed.Infrastructure.Interfaces;

/// <summary>
/// Service for provisioning and managing websites.
/// </summary>
public interface IWebsiteProvisioningService
{
    /// <summary>
    /// Provisions a new website with all required resources.
    /// </summary>
    Task<Website> ProvisionWebsiteAsync(Website website, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deprovisions a website and cleans up resources.
    /// </summary>
    Task<bool> DeprovisionWebsiteAsync(Guid websiteId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates website provisioning prerequisites.
    /// </summary>
    Task<WebsiteValidationResult> ValidateProvisioningAsync(Website website);
}

/// <summary>
/// Service for content management operations.
/// </summary>
public interface IContentService
{
    /// <summary>
    /// Creates a new page.
    /// </summary>
    Task<Page> CreatePageAsync(Page page, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a page.
    /// </summary>
    Task<Page> UpdatePageAsync(Page page, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes a page.
    /// </summary>
    Task<Page> PublishPageAsync(Guid pageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Schedules a page for future publication.
    /// </summary>
    Task<Page> SchedulePageAsync(Guid pageId, DateTime scheduledAt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a page.
    /// </summary>
    Task<bool> DeletePageAsync(Guid pageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Uploads media to a website.
    /// </summary>
    Task<MediaAsset> UploadMediaAsync(Guid websiteId, Stream fileStream, string fileName, string contentType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes media.
    /// </summary>
    Task<bool> DeleteMediaAsync(Guid mediaId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Service for theme management.
/// </summary>
public interface IThemeService
{
    /// <summary>
    /// Installs a theme for a website.
    /// </summary>
    Task<bool> InstallThemeAsync(Guid websiteId, Guid themeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Activates a theme for a website (hot-swap).
    /// </summary>
    Task<bool> ActivateThemeAsync(Guid websiteId, Guid themeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Customizes theme options.
    /// </summary>
    Task<bool> CustomizeThemeAsync(Guid websiteId, Dictionary<string, string> customizations, CancellationToken cancellationToken = default);
}

/// <summary>
/// Service for plugin management.
/// </summary>
public interface IPluginService
{
    /// <summary>
    /// Installs a plugin for a website.
    /// </summary>
    Task<bool> InstallPluginAsync(Guid websiteId, Guid pluginId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Activates a plugin for a website.
    /// </summary>
    Task<bool> ActivatePluginAsync(Guid websiteId, Guid pluginId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deactivates a plugin for a website.
    /// </summary>
    Task<bool> DeactivatePluginAsync(Guid websiteId, Guid pluginId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Uninstalls a plugin from a website.
    /// </summary>
    Task<bool> UninstallPluginAsync(Guid websiteId, Guid pluginId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Website validation result.
/// </summary>
public class WebsiteValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();

    public static WebsiteValidationResult Success() => new() { IsValid = true };
    public static WebsiteValidationResult Failure(params string[] errors) =>
        new() { IsValid = false, Errors = errors.ToList() };
}
