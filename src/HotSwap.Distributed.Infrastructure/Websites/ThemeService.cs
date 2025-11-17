using HotSwap.Distributed.Infrastructure.Interfaces;
using Microsoft.Extensions.Logging;

namespace HotSwap.Distributed.Infrastructure.Websites;

/// <summary>
/// Service for theme management.
/// </summary>
public class ThemeService : IThemeService
{
    private readonly IWebsiteRepository _websiteRepository;
    private readonly IThemeRepository _themeRepository;
    private readonly ILogger<ThemeService> _logger;

    public ThemeService(
        IWebsiteRepository websiteRepository,
        IThemeRepository themeRepository,
        ILogger<ThemeService> logger)
    {
        _websiteRepository = websiteRepository ?? throw new ArgumentNullException(nameof(websiteRepository));
        _themeRepository = themeRepository ?? throw new ArgumentNullException(nameof(themeRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> InstallThemeAsync(Guid websiteId, Guid themeId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Installing theme {ThemeId} for website {WebsiteId}",
            themeId, websiteId);

        var theme = await _themeRepository.GetByIdAsync(themeId, cancellationToken);
        if (theme == null)
            throw new KeyNotFoundException($"Theme {themeId} not found");

        // TODO: Extract theme package and deploy assets
        _logger.LogInformation("Theme installed successfully");
        return true;
    }

    public async Task<bool> ActivateThemeAsync(Guid websiteId, Guid themeId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Activating theme {ThemeId} for website {WebsiteId}",
            themeId, websiteId);

        var website = await _websiteRepository.GetByIdAsync(websiteId, cancellationToken);
        if (website == null)
            throw new KeyNotFoundException($"Website {websiteId} not found");

        var theme = await _themeRepository.GetByIdAsync(themeId, cancellationToken);
        if (theme == null)
            throw new KeyNotFoundException($"Theme {themeId} not found");

        // Hot-swap theme (atomic update)
        website.CurrentThemeId = themeId;
        website.ThemeVersion = theme.Version;
        await _websiteRepository.UpdateAsync(website, cancellationToken);

        _logger.LogInformation("Theme activated successfully");
        return true;
    }

    public async Task<bool> CustomizeThemeAsync(Guid websiteId, Dictionary<string, string> customizations, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Customizing theme for website {WebsiteId}", websiteId);

        var website = await _websiteRepository.GetByIdAsync(websiteId, cancellationToken);
        if (website == null)
            throw new KeyNotFoundException($"Website {websiteId} not found");

        // Store customizations in website configuration
        foreach (var customization in customizations)
        {
            website.Configuration[$"theme_{customization.Key}"] = customization.Value;
        }

        await _websiteRepository.UpdateAsync(website, cancellationToken);

        _logger.LogInformation("Theme customizations saved");
        return true;
    }
}
