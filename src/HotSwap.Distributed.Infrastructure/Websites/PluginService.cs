using HotSwap.Distributed.Infrastructure.Interfaces;
using Microsoft.Extensions.Logging;

namespace HotSwap.Distributed.Infrastructure.Websites;

/// <summary>
/// Service for plugin management.
/// </summary>
public class PluginService : IPluginService
{
    private readonly IWebsiteRepository _websiteRepository;
    private readonly IPluginRepository _pluginRepository;
    private readonly ILogger<PluginService> _logger;

    public PluginService(
        IWebsiteRepository websiteRepository,
        IPluginRepository pluginRepository,
        ILogger<PluginService> logger)
    {
        _websiteRepository = websiteRepository ?? throw new ArgumentNullException(nameof(websiteRepository));
        _pluginRepository = pluginRepository ?? throw new ArgumentNullException(nameof(pluginRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> InstallPluginAsync(Guid websiteId, Guid pluginId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Installing plugin {PluginId} for website {WebsiteId}",
            pluginId, websiteId);

        var plugin = await _pluginRepository.GetByIdAsync(pluginId, cancellationToken);
        if (plugin == null)
            throw new KeyNotFoundException($"Plugin {pluginId} not found");

        // TODO: Extract plugin package and deploy
        _logger.LogInformation("Plugin installed successfully");
        return true;
    }

    public async Task<bool> ActivatePluginAsync(Guid websiteId, Guid pluginId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Activating plugin {PluginId} for website {WebsiteId}",
            pluginId, websiteId);

        var website = await _websiteRepository.GetByIdAsync(websiteId, cancellationToken);
        if (website == null)
            throw new KeyNotFoundException($"Website {websiteId} not found");

        if (!website.InstalledPluginIds.Contains(pluginId))
        {
            website.InstalledPluginIds.Add(pluginId);
            await _websiteRepository.UpdateAsync(website, cancellationToken);
        }

        _logger.LogInformation("Plugin activated successfully");
        return true;
    }

    public async Task<bool> DeactivatePluginAsync(Guid websiteId, Guid pluginId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Deactivating plugin {PluginId} for website {WebsiteId}",
            pluginId, websiteId);

        var website = await _websiteRepository.GetByIdAsync(websiteId, cancellationToken);
        if (website == null)
            throw new KeyNotFoundException($"Website {websiteId} not found");

        website.InstalledPluginIds.Remove(pluginId);
        await _websiteRepository.UpdateAsync(website, cancellationToken);

        _logger.LogInformation("Plugin deactivated successfully");
        return true;
    }

    public async Task<bool> UninstallPluginAsync(Guid websiteId, Guid pluginId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Uninstalling plugin {PluginId} from website {WebsiteId}",
            pluginId, websiteId);

        // First deactivate
        await DeactivatePluginAsync(websiteId, pluginId, cancellationToken);

        // TODO: Remove plugin files and cleanup
        _logger.LogInformation("Plugin uninstalled successfully");
        return true;
    }
}
