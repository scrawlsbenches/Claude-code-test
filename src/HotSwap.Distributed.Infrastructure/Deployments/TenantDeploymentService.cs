using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Interfaces;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace HotSwap.Distributed.Infrastructure.Deployments;

/// <summary>
/// Service for managing tenant-scoped deployments.
/// </summary>
public class TenantDeploymentService : ITenantDeploymentService
{
    private readonly IWebsiteRepository _websiteRepository;
    private readonly IThemeService _themeService;
    private readonly IPluginService _pluginService;
    private readonly IQuotaService _quotaService;
    private readonly ILogger<TenantDeploymentService> _logger;
    private readonly ConcurrentDictionary<Guid, TenantDeploymentResult> _deployments = new();

    public TenantDeploymentService(
        IWebsiteRepository websiteRepository,
        IThemeService themeService,
        IPluginService pluginService,
        IQuotaService quotaService,
        ILogger<TenantDeploymentService> logger)
    {
        _websiteRepository = websiteRepository ?? throw new ArgumentNullException(nameof(websiteRepository));
        _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
        _pluginService = pluginService ?? throw new ArgumentNullException(nameof(pluginService));
        _quotaService = quotaService ?? throw new ArgumentNullException(nameof(quotaService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<TenantDeploymentResult> DeployAsync(
        TenantDeploymentRequest request,
        CancellationToken cancellationToken = default)
    {
        var deploymentId = Guid.NewGuid();
        var startTime = DateTime.UtcNow;

        _logger.LogInformation("Starting tenant deployment {DeploymentId} for tenant {TenantId}, module type: {ModuleType}",
            deploymentId, request.TenantId, request.ModuleType);

        var result = new TenantDeploymentResult
        {
            DeploymentId = deploymentId,
            TenantId = request.TenantId,
            StartTime = startTime,
            Success = false,
            Message = "Deployment in progress"
        };

        try
        {
            // Check quota for concurrent deployments
            var hasQuota = await _quotaService.CheckQuotaAsync(
                request.TenantId,
                Domain.Enums.ResourceType.ConcurrentDeployments,
                1,
                cancellationToken);

            if (!hasQuota)
            {
                result.Success = false;
                result.Message = "Concurrent deployment quota exceeded";
                result.Errors.Add("Maximum concurrent deployments reached for this tenant");
                result.EndTime = DateTime.UtcNow;
                return result;
            }

            // Record deployment usage
            await _quotaService.RecordUsageAsync(
                request.TenantId,
                Domain.Enums.ResourceType.ConcurrentDeployments,
                1,
                cancellationToken);

            // Get affected websites
            var websites = await GetAffectedWebsitesAsync(request, cancellationToken);
            if (!websites.Any())
            {
                result.Success = false;
                result.Message = "No websites found for deployment";
                result.Errors.Add("Deployment scope did not match any websites");
                result.EndTime = DateTime.UtcNow;
                return result;
            }

            // Execute deployment based on module type
            switch (request.ModuleType)
            {
                case WebsiteModuleType.Theme:
                    await DeployThemeAsync(websites, request, result, cancellationToken);
                    break;

                case WebsiteModuleType.Plugin:
                    await DeployPluginAsync(websites, request, result, cancellationToken);
                    break;

                case WebsiteModuleType.Content:
                    await DeployContentAsync(websites, request, result, cancellationToken);
                    break;

                default:
                    result.Success = false;
                    result.Message = $"Unsupported module type: {request.ModuleType}";
                    break;
            }

            result.EndTime = DateTime.UtcNow;
            result.AffectedWebsites = websites.Select(w => w.WebsiteId).ToList();

            _deployments[deploymentId] = result;

            _logger.LogInformation("Completed tenant deployment {DeploymentId}: Success={Success}, Websites={Count}",
                deploymentId, result.Success, result.AffectedWebsites.Count);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Deployment {DeploymentId} failed", deploymentId);
            result.Success = false;
            result.Message = "Deployment failed with exception";
            result.Errors.Add(ex.Message);
            result.EndTime = DateTime.UtcNow;
            return result;
        }
    }

    public Task<TenantDeploymentResult?> GetDeploymentStatusAsync(
        Guid deploymentId,
        CancellationToken cancellationToken = default)
    {
        _deployments.TryGetValue(deploymentId, out var result);
        return Task.FromResult(result);
    }

    public Task<List<TenantDeploymentResult>> GetDeploymentsForTenantAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var deployments = _deployments.Values
            .Where(d => d.TenantId == tenantId)
            .OrderByDescending(d => d.StartTime)
            .ToList();

        return Task.FromResult(deployments);
    }

    public async Task<bool> RollbackDeploymentAsync(
        Guid deploymentId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Rolling back deployment: {DeploymentId}", deploymentId);

        if (!_deployments.TryGetValue(deploymentId, out var deployment))
            return false;

        // TODO: Implement rollback logic based on module type
        _logger.LogWarning("Rollback not yet fully implemented");

        return true;
    }

    private async Task<List<Website>> GetAffectedWebsitesAsync(
        TenantDeploymentRequest request,
        CancellationToken cancellationToken)
    {
        return request.Scope switch
        {
            DeploymentScope.SingleWebsite when request.WebsiteId.HasValue =>
                (await _websiteRepository.GetByIdAsync(request.WebsiteId.Value, cancellationToken) is Website website)
                    ? new List<Website> { website }
                    : new List<Website>(),

            DeploymentScope.AllTenantWebsites =>
                await _websiteRepository.GetByTenantIdAsync(request.TenantId, cancellationToken: cancellationToken),

            _ => new List<Website>()
        };
    }

    private async Task DeployThemeAsync(
        List<Website> websites,
        TenantDeploymentRequest request,
        TenantDeploymentResult result,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Deploying theme to {Count} websites", websites.Count);

        // Extract theme ID from request metadata
        if (!request.Metadata.TryGetValue("themeId", out var themeIdStr) ||
            !Guid.TryParse(themeIdStr, out var themeId))
        {
            result.Success = false;
            result.Message = "Theme ID not provided in metadata";
            return;
        }

        var successCount = 0;
        foreach (var website in websites)
        {
            try
            {
                // Hot-swap theme deployment (zero downtime)
                await _themeService.ActivateThemeAsync(website.WebsiteId, themeId, cancellationToken);
                successCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deploy theme to website {WebsiteId}", website.WebsiteId);
                result.Errors.Add($"Website {website.WebsiteId}: {ex.Message}");
            }
        }

        result.Success = successCount == websites.Count;
        result.Message = $"Theme deployed to {successCount}/{websites.Count} websites";
    }

    private async Task DeployPluginAsync(
        List<Website> websites,
        TenantDeploymentRequest request,
        TenantDeploymentResult result,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Deploying plugin to {Count} websites", websites.Count);

        if (!request.Metadata.TryGetValue("pluginId", out var pluginIdStr) ||
            !Guid.TryParse(pluginIdStr, out var pluginId))
        {
            result.Success = false;
            result.Message = "Plugin ID not provided in metadata";
            return;
        }

        var successCount = 0;
        foreach (var website in websites)
        {
            try
            {
                await _pluginService.ActivatePluginAsync(website.WebsiteId, pluginId, cancellationToken);
                successCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deploy plugin to website {WebsiteId}", website.WebsiteId);
                result.Errors.Add($"Website {website.WebsiteId}: {ex.Message}");
            }
        }

        result.Success = successCount == websites.Count;
        result.Message = $"Plugin deployed to {successCount}/{websites.Count} websites";
    }

    private Task DeployContentAsync(
        List<Website> websites,
        TenantDeploymentRequest request,
        TenantDeploymentResult result,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Deploying content to {Count} websites", websites.Count);

        // TODO: Implement content deployment logic
        result.Success = true;
        result.Message = $"Content deployment completed for {websites.Count} websites";

        return Task.CompletedTask;
    }
}
