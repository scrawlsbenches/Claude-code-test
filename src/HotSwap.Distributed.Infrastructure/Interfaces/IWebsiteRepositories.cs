using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;

namespace HotSwap.Distributed.Infrastructure.Interfaces;

/// <summary>
/// Repository for website management operations.
/// </summary>
public interface IWebsiteRepository
{
    /// <summary>
    /// Gets a website by ID.
    /// </summary>
    Task<Website?> GetByIdAsync(Guid websiteId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a website by subdomain within a tenant.
    /// </summary>
    Task<Website?> GetBySubdomainAsync(Guid tenantId, string subdomain, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all websites for a tenant.
    /// </summary>
    Task<List<Website>> GetByTenantIdAsync(Guid tenantId, WebsiteStatus? status = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new website.
    /// </summary>
    Task<Website> CreateAsync(Website website, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing website.
    /// </summary>
    Task<Website> UpdateAsync(Website website, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a website (soft delete).
    /// </summary>
    Task<bool> DeleteAsync(Guid websiteId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a subdomain is available within a tenant.
    /// </summary>
    Task<bool> IsSubdomainAvailableAsync(Guid tenantId, string subdomain, CancellationToken cancellationToken = default);
}

/// <summary>
/// Repository for page/content management operations.
/// </summary>
public interface IPageRepository
{
    /// <summary>
    /// Gets a page by ID.
    /// </summary>
    Task<Page?> GetByIdAsync(Guid pageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a page by slug within a website.
    /// </summary>
    Task<Page?> GetBySlugAsync(Guid websiteId, string slug, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all pages for a website.
    /// </summary>
    Task<List<Page>> GetByWebsiteIdAsync(Guid websiteId, PageStatus? status = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new page.
    /// </summary>
    Task<Page> CreateAsync(Page page, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing page.
    /// </summary>
    Task<Page> UpdateAsync(Page page, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a page.
    /// </summary>
    Task<bool> DeleteAsync(Guid pageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets published pages for a website.
    /// </summary>
    Task<List<Page>> GetPublishedPagesAsync(Guid websiteId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Repository for media asset operations.
/// </summary>
public interface IMediaRepository
{
    /// <summary>
    /// Gets a media asset by ID.
    /// </summary>
    Task<MediaAsset?> GetByIdAsync(Guid mediaId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all media assets for a website.
    /// </summary>
    Task<List<MediaAsset>> GetByWebsiteIdAsync(Guid websiteId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new media asset.
    /// </summary>
    Task<MediaAsset> CreateAsync(MediaAsset media, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a media asset.
    /// </summary>
    Task<bool> DeleteAsync(Guid mediaId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Repository for theme operations.
/// </summary>
public interface IThemeRepository
{
    /// <summary>
    /// Gets a theme by ID.
    /// </summary>
    Task<Theme?> GetByIdAsync(Guid themeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all public themes.
    /// </summary>
    Task<List<Theme>> GetPublicThemesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new theme.
    /// </summary>
    Task<Theme> CreateAsync(Theme theme, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing theme.
    /// </summary>
    Task<Theme> UpdateAsync(Theme theme, CancellationToken cancellationToken = default);
}

/// <summary>
/// Repository for plugin operations.
/// </summary>
public interface IPluginRepository
{
    /// <summary>
    /// Gets a plugin by ID.
    /// </summary>
    Task<Plugin?> GetByIdAsync(Guid pluginId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all public plugins.
    /// </summary>
    Task<List<Plugin>> GetPublicPluginsAsync(PluginCategory? category = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new plugin.
    /// </summary>
    Task<Plugin> CreateAsync(Plugin plugin, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing plugin.
    /// </summary>
    Task<Plugin> UpdateAsync(Plugin plugin, CancellationToken cancellationToken = default);
}
