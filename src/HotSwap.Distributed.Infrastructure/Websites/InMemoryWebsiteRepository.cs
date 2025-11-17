using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Interfaces;
using Microsoft.Extensions.Logging;

namespace HotSwap.Distributed.Infrastructure.Websites;

/// <summary>
/// In-memory implementation of website repository.
/// </summary>
public class InMemoryWebsiteRepository : IWebsiteRepository
{
    private readonly Dictionary<Guid, Website> _websites = new();
    private readonly ILogger<InMemoryWebsiteRepository> _logger;
    private readonly object _lock = new();

    public InMemoryWebsiteRepository(ILogger<InMemoryWebsiteRepository> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<Website?> GetByIdAsync(Guid websiteId, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _websites.TryGetValue(websiteId, out var website);
            return Task.FromResult(website);
        }
    }

    public Task<Website?> GetBySubdomainAsync(Guid tenantId, string subdomain, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var website = _websites.Values.FirstOrDefault(w =>
                w.TenantId == tenantId &&
                w.Subdomain.Equals(subdomain, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(website);
        }
    }

    public Task<List<Website>> GetByTenantIdAsync(Guid tenantId, WebsiteStatus? status = null, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var websites = _websites.Values
                .Where(w => w.TenantId == tenantId)
                .Where(w => !status.HasValue || w.Status == status.Value)
                .ToList();
            return Task.FromResult(websites);
        }
    }

    public Task<Website> CreateAsync(Website website, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (website.WebsiteId == Guid.Empty)
                website.WebsiteId = Guid.NewGuid();

            website.CreatedAt = DateTime.UtcNow;
            _websites[website.WebsiteId] = website;

            _logger.LogInformation("Created website: {Name} (ID: {WebsiteId})",
                website.Name, website.WebsiteId);

            return Task.FromResult(website);
        }
    }

    public Task<Website> UpdateAsync(Website website, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (!_websites.ContainsKey(website.WebsiteId))
                throw new KeyNotFoundException($"Website {website.WebsiteId} not found");

            website.UpdatedAt = DateTime.UtcNow;
            _websites[website.WebsiteId] = website;

            _logger.LogInformation("Updated website: {Name} (ID: {WebsiteId})",
                website.Name, website.WebsiteId);

            return Task.FromResult(website);
        }
    }

    public Task<bool> DeleteAsync(Guid websiteId, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (!_websites.TryGetValue(websiteId, out var website))
                return Task.FromResult(false);

            website.Status = WebsiteStatus.Deleted;
            website.UpdatedAt = DateTime.UtcNow;

            _logger.LogInformation("Soft deleted website: {Name} (ID: {WebsiteId})",
                website.Name, website.WebsiteId);

            return Task.FromResult(true);
        }
    }

    public Task<bool> IsSubdomainAvailableAsync(Guid tenantId, string subdomain, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var exists = _websites.Values.Any(w =>
                w.TenantId == tenantId &&
                w.Subdomain.Equals(subdomain, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(!exists);
        }
    }
}

/// <summary>
/// In-memory implementation of page repository.
/// </summary>
public class InMemoryPageRepository : IPageRepository
{
    private readonly Dictionary<Guid, Page> _pages = new();
    private readonly ILogger<InMemoryPageRepository> _logger;
    private readonly object _lock = new();

    public InMemoryPageRepository(ILogger<InMemoryPageRepository> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<Page?> GetByIdAsync(Guid pageId, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _pages.TryGetValue(pageId, out var page);
            return Task.FromResult(page);
        }
    }

    public Task<Page?> GetBySlugAsync(Guid websiteId, string slug, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var page = _pages.Values.FirstOrDefault(p =>
                p.WebsiteId == websiteId &&
                p.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(page);
        }
    }

    public Task<List<Page>> GetByWebsiteIdAsync(Guid websiteId, PageStatus? status = null, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var pages = _pages.Values
                .Where(p => p.WebsiteId == websiteId)
                .Where(p => !status.HasValue || p.Status == status.Value)
                .ToList();
            return Task.FromResult(pages);
        }
    }

    public Task<Page> CreateAsync(Page page, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (page.PageId == Guid.Empty)
                page.PageId = Guid.NewGuid();

            page.CreatedAt = DateTime.UtcNow;
            _pages[page.PageId] = page;

            _logger.LogInformation("Created page: {Title} (ID: {PageId})",
                page.Title, page.PageId);

            return Task.FromResult(page);
        }
    }

    public Task<Page> UpdateAsync(Page page, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (!_pages.ContainsKey(page.PageId))
                throw new KeyNotFoundException($"Page {page.PageId} not found");

            page.UpdatedAt = DateTime.UtcNow;
            page.Version++;
            _pages[page.PageId] = page;

            _logger.LogInformation("Updated page: {Title} (ID: {PageId}, Version: {Version})",
                page.Title, page.PageId, page.Version);

            return Task.FromResult(page);
        }
    }

    public Task<bool> DeleteAsync(Guid pageId, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (_pages.Remove(pageId))
            {
                _logger.LogInformation("Deleted page: {PageId}", pageId);
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }
    }

    public Task<List<Page>> GetPublishedPagesAsync(Guid websiteId, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var pages = _pages.Values
                .Where(p => p.WebsiteId == websiteId && p.Status == PageStatus.Published)
                .ToList();
            return Task.FromResult(pages);
        }
    }
}

/// <summary>
/// In-memory implementation of media repository.
/// </summary>
public class InMemoryMediaRepository : IMediaRepository
{
    private readonly Dictionary<Guid, MediaAsset> _media = new();
    private readonly ILogger<InMemoryMediaRepository> _logger;
    private readonly object _lock = new();

    public InMemoryMediaRepository(ILogger<InMemoryMediaRepository> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<MediaAsset?> GetByIdAsync(Guid mediaId, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _media.TryGetValue(mediaId, out var media);
            return Task.FromResult(media);
        }
    }

    public Task<List<MediaAsset>> GetByWebsiteIdAsync(Guid websiteId, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var media = _media.Values.Where(m => m.WebsiteId == websiteId).ToList();
            return Task.FromResult(media);
        }
    }

    public Task<MediaAsset> CreateAsync(MediaAsset media, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (media.MediaId == Guid.Empty)
                media.MediaId = Guid.NewGuid();

            media.UploadedAt = DateTime.UtcNow;
            _media[media.MediaId] = media;

            _logger.LogInformation("Created media: {FileName} (ID: {MediaId})",
                media.FileName, media.MediaId);

            return Task.FromResult(media);
        }
    }

    public Task<bool> DeleteAsync(Guid mediaId, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (_media.Remove(mediaId))
            {
                _logger.LogInformation("Deleted media: {MediaId}", mediaId);
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }
    }
}

/// <summary>
/// In-memory implementation of theme repository.
/// </summary>
public class InMemoryThemeRepository : IThemeRepository
{
    private readonly Dictionary<Guid, Theme> _themes = new();
    private readonly ILogger<InMemoryThemeRepository> _logger;
    private readonly object _lock = new();

    public InMemoryThemeRepository(ILogger<InMemoryThemeRepository> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        InitializeDefaultThemes();
    }

    private void InitializeDefaultThemes()
    {
        var defaultTheme = new Theme
        {
            ThemeId = Guid.Parse("10000000-0000-0000-0000-000000000001"),
            Name = "Default Theme",
            Version = "1.0.0",
            Author = "Platform Team",
            IsPublic = true,
            Description = "Clean and simple default theme",
            Manifest = new ThemeManifest
            {
                Name = "Default Theme",
                Version = "1.0.0",
                Templates = new List<string> { "index.html", "page.html", "post.html" },
                Stylesheets = new List<string> { "style.css" },
                Scripts = new List<string> { "main.js" }
            }
        };

        _themes[defaultTheme.ThemeId] = defaultTheme;
        _logger.LogInformation("Initialized default theme");
    }

    public Task<Theme?> GetByIdAsync(Guid themeId, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _themes.TryGetValue(themeId, out var theme);
            return Task.FromResult(theme);
        }
    }

    public Task<List<Theme>> GetPublicThemesAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var themes = _themes.Values.Where(t => t.IsPublic).ToList();
            return Task.FromResult(themes);
        }
    }

    public Task<Theme> CreateAsync(Theme theme, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (theme.ThemeId == Guid.Empty)
                theme.ThemeId = Guid.NewGuid();

            theme.CreatedAt = DateTime.UtcNow;
            _themes[theme.ThemeId] = theme;

            _logger.LogInformation("Created theme: {Name} v{Version}",
                theme.Name, theme.Version);

            return Task.FromResult(theme);
        }
    }

    public Task<Theme> UpdateAsync(Theme theme, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (!_themes.ContainsKey(theme.ThemeId))
                throw new KeyNotFoundException($"Theme {theme.ThemeId} not found");

            theme.UpdatedAt = DateTime.UtcNow;
            _themes[theme.ThemeId] = theme;

            _logger.LogInformation("Updated theme: {Name} v{Version}",
                theme.Name, theme.Version);

            return Task.FromResult(theme);
        }
    }
}

/// <summary>
/// In-memory implementation of plugin repository.
/// </summary>
public class InMemoryPluginRepository : IPluginRepository
{
    private readonly Dictionary<Guid, Plugin> _plugins = new();
    private readonly ILogger<InMemoryPluginRepository> _logger;
    private readonly object _lock = new();

    public InMemoryPluginRepository(ILogger<InMemoryPluginRepository> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<Plugin?> GetByIdAsync(Guid pluginId, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _plugins.TryGetValue(pluginId, out var plugin);
            return Task.FromResult(plugin);
        }
    }

    public Task<List<Plugin>> GetPublicPluginsAsync(PluginCategory? category = null, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var plugins = _plugins.Values
                .Where(p => p.IsPublic)
                .Where(p => !category.HasValue || p.Category == category.Value)
                .ToList();
            return Task.FromResult(plugins);
        }
    }

    public Task<Plugin> CreateAsync(Plugin plugin, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (plugin.PluginId == Guid.Empty)
                plugin.PluginId = Guid.NewGuid();

            plugin.CreatedAt = DateTime.UtcNow;
            _plugins[plugin.PluginId] = plugin;

            _logger.LogInformation("Created plugin: {Name} v{Version}",
                plugin.Name, plugin.Version);

            return Task.FromResult(plugin);
        }
    }

    public Task<Plugin> UpdateAsync(Plugin plugin, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (!_plugins.ContainsKey(plugin.PluginId))
                throw new KeyNotFoundException($"Plugin {plugin.PluginId} not found");

            plugin.UpdatedAt = DateTime.UtcNow;
            _plugins[plugin.PluginId] = plugin;

            _logger.LogInformation("Updated plugin: {Name} v{Version}",
                plugin.Name, plugin.Version);

            return Task.FromResult(plugin);
        }
    }
}
