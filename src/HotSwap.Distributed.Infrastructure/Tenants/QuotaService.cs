using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Infrastructure.Interfaces;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace HotSwap.Distributed.Infrastructure.Tenants;

/// <summary>
/// Service for enforcing resource quotas for tenants.
/// </summary>
public class QuotaService : IQuotaService
{
    private readonly ITenantRepository _tenantRepository;
    private readonly ILogger<QuotaService> _logger;
    private readonly ConcurrentDictionary<string, long> _usageCache = new();

    public QuotaService(
        ITenantRepository tenantRepository,
        ILogger<QuotaService> logger)
    {
        _tenantRepository = tenantRepository ?? throw new ArgumentNullException(nameof(tenantRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> CheckQuotaAsync(Guid tenantId, ResourceType resourceType, long amount, CancellationToken cancellationToken = default)
    {
        var currentUsage = await GetCurrentUsageAsync(tenantId, resourceType, cancellationToken);
        var quotaLimit = await GetQuotaLimitAsync(tenantId, resourceType, cancellationToken);

        var availableQuota = quotaLimit - currentUsage;
        var hasQuota = availableQuota >= amount;

        if (!hasQuota)
        {
            _logger.LogWarning("Quota exceeded for tenant {TenantId}: {ResourceType} - Current: {Current}, Limit: {Limit}, Requested: {Requested}",
                tenantId, resourceType, currentUsage, quotaLimit, amount);
        }

        return hasQuota;
    }

    public Task<long> GetCurrentUsageAsync(Guid tenantId, ResourceType resourceType, CancellationToken cancellationToken = default)
    {
        var key = GetUsageKey(tenantId, resourceType);
        var usage = _usageCache.GetOrAdd(key, _ => 0);
        return Task.FromResult(usage);
    }

    public Task<bool> RecordUsageAsync(Guid tenantId, ResourceType resourceType, long amount, CancellationToken cancellationToken = default)
    {
        var key = GetUsageKey(tenantId, resourceType);
        _usageCache.AddOrUpdate(key, amount, (_, current) => current + amount);

        _logger.LogDebug("Recorded usage for tenant {TenantId}: {ResourceType} += {Amount}",
            tenantId, resourceType, amount);

        return Task.FromResult(true);
    }

    public async Task<long> GetQuotaLimitAsync(Guid tenantId, ResourceType resourceType, CancellationToken cancellationToken = default)
    {
        var tenant = await _tenantRepository.GetByIdAsync(tenantId, cancellationToken);
        if (tenant == null)
        {
            _logger.LogWarning("Tenant not found: {TenantId}", tenantId);
            return 0;
        }

        return resourceType switch
        {
            ResourceType.Storage => tenant.ResourceQuota.StorageQuotaGB,
            ResourceType.Bandwidth => tenant.ResourceQuota.BandwidthQuotaGB,
            ResourceType.Websites => tenant.ResourceQuota.MaxWebsites,
            ResourceType.ConcurrentDeployments => tenant.ResourceQuota.MaxConcurrentDeployments,
            ResourceType.CustomDomains => tenant.ResourceQuota.MaxCustomDomains,
            _ => throw new ArgumentException($"Unknown resource type: {resourceType}")
        };
    }

    public async Task<bool> IsWithinQuotaAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        foreach (ResourceType resourceType in Enum.GetValues(typeof(ResourceType)))
        {
            var currentUsage = await GetCurrentUsageAsync(tenantId, resourceType, cancellationToken);
            var quotaLimit = await GetQuotaLimitAsync(tenantId, resourceType, cancellationToken);

            if (currentUsage > quotaLimit)
            {
                _logger.LogWarning("Tenant {TenantId} exceeded quota for {ResourceType}: {Usage} > {Limit}",
                    tenantId, resourceType, currentUsage, quotaLimit);
                return false;
            }
        }

        return true;
    }

    private static string GetUsageKey(Guid tenantId, ResourceType resourceType)
        => $"{tenantId}:{resourceType}";
}
