using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Interfaces;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace HotSwap.Distributed.Infrastructure.Tenants;

/// <summary>
/// Service for managing tenant subscriptions and billing.
/// In production, integrate with Stripe or another payment provider.
/// </summary>
public class SubscriptionService : ISubscriptionService
{
    private readonly ITenantRepository _tenantRepository;
    private readonly ILogger<SubscriptionService> _logger;
    private readonly ConcurrentDictionary<Guid, TenantSubscription> _subscriptions = new();
    private readonly ConcurrentDictionary<Guid, List<UsageReport>> _usageReports = new();

    public SubscriptionService(
        ITenantRepository tenantRepository,
        ILogger<SubscriptionService> logger)
    {
        _tenantRepository = tenantRepository ?? throw new ArgumentNullException(nameof(tenantRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<TenantSubscription> CreateSubscriptionAsync(Guid tenantId, SubscriptionTier tier, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating subscription for tenant {TenantId} with tier {Tier}", tenantId, tier);

        var tenant = await _tenantRepository.GetByIdAsync(tenantId, cancellationToken);
        if (tenant == null)
            throw new InvalidOperationException($"Tenant {tenantId} not found");

        var subscription = new TenantSubscription
        {
            SubscriptionId = Guid.NewGuid(),
            TenantId = tenantId,
            Tier = tier,
            Status = "Active",
            BillingCycle = "Monthly",
            AmountCents = GetPriceForTier(tier),
            CurrentPeriodStart = DateTime.UtcNow,
            CurrentPeriodEnd = DateTime.UtcNow.AddMonths(1),
            CreatedAt = DateTime.UtcNow
        };

        _subscriptions[tenantId] = subscription;

        // Update tenant tier
        tenant.Tier = tier;
        tenant.ResourceQuota = ResourceQuota.CreateDefault(tier);
        await _tenantRepository.UpdateAsync(tenant, cancellationToken);

        _logger.LogInformation("Created subscription {SubscriptionId} for tenant {TenantId}",
            subscription.SubscriptionId, tenantId);

        return subscription;
    }

    public async Task<TenantSubscription> UpgradeSubscriptionAsync(Guid tenantId, SubscriptionTier newTier, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Upgrading subscription for tenant {TenantId} to tier {NewTier}",
            tenantId, newTier);

        var subscription = await GetCurrentSubscriptionAsync(tenantId, cancellationToken);
        if (subscription == null)
            throw new InvalidOperationException($"No active subscription found for tenant {tenantId}");

        var oldTier = subscription.Tier;
        if (newTier <= oldTier)
            throw new InvalidOperationException($"Cannot upgrade from {oldTier} to {newTier}");

        subscription.Tier = newTier;
        subscription.AmountCents = GetPriceForTier(newTier);

        var tenant = await _tenantRepository.GetByIdAsync(tenantId, cancellationToken);
        if (tenant != null)
        {
            tenant.Tier = newTier;
            tenant.ResourceQuota = ResourceQuota.CreateDefault(newTier);
            await _tenantRepository.UpdateAsync(tenant, cancellationToken);
        }

        _logger.LogInformation("Upgraded subscription for tenant {TenantId} from {OldTier} to {NewTier}",
            tenantId, oldTier, newTier);

        return subscription;
    }

    public async Task<TenantSubscription> DowngradeSubscriptionAsync(Guid tenantId, SubscriptionTier newTier, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Downgrading subscription for tenant {TenantId} to tier {NewTier}",
            tenantId, newTier);

        var subscription = await GetCurrentSubscriptionAsync(tenantId, cancellationToken);
        if (subscription == null)
            throw new InvalidOperationException($"No active subscription found for tenant {tenantId}");

        var oldTier = subscription.Tier;
        if (newTier >= oldTier)
            throw new InvalidOperationException($"Cannot downgrade from {oldTier} to {newTier}");

        subscription.Tier = newTier;
        subscription.AmountCents = GetPriceForTier(newTier);

        var tenant = await _tenantRepository.GetByIdAsync(tenantId, cancellationToken);
        if (tenant != null)
        {
            tenant.Tier = newTier;
            tenant.ResourceQuota = ResourceQuota.CreateDefault(newTier);
            await _tenantRepository.UpdateAsync(tenant, cancellationToken);
        }

        _logger.LogInformation("Downgraded subscription for tenant {TenantId} from {OldTier} to {NewTier}",
            tenantId, oldTier, newTier);

        return subscription;
    }

    public async Task<bool> SuspendForNonPaymentAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("Suspending tenant {TenantId} for non-payment", tenantId);

        var subscription = await GetCurrentSubscriptionAsync(tenantId, cancellationToken);
        if (subscription != null)
        {
            subscription.Status = "Suspended";
        }

        var tenant = await _tenantRepository.GetByIdAsync(tenantId, cancellationToken);
        if (tenant != null)
        {
            tenant.Status = TenantStatus.Suspended;
            tenant.SuspendedAt = DateTime.UtcNow;
            tenant.Metadata["suspension_reason"] = "Non-payment";
            await _tenantRepository.UpdateAsync(tenant, cancellationToken);
        }

        return true;
    }

    public Task<UsageReport> GetUsageReportAsync(Guid tenantId, DateTime periodStart, DateTime periodEnd, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting usage report for tenant {TenantId} from {Start} to {End}",
            tenantId, periodStart, periodEnd);

        // Check if report already exists
        if (_usageReports.TryGetValue(tenantId, out var reports))
        {
            var existingReport = reports.FirstOrDefault(r =>
                r.PeriodStart == periodStart && r.PeriodEnd == periodEnd);
            if (existingReport != null)
                return Task.FromResult(existingReport);
        }

        // Generate new report (placeholder values)
        var report = new UsageReport
        {
            TenantId = tenantId,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            StorageUsedGB = 0, // TODO: Calculate actual usage
            BandwidthUsedGB = 0, // TODO: Calculate actual usage
            DeploymentsCount = 0, // TODO: Count deployments
            TotalCost = 0m,
            LineItems = new Dictionary<string, decimal>
            {
                { "Base Subscription", 0m },
                { "Additional Storage", 0m },
                { "Additional Bandwidth", 0m }
            },
            GeneratedAt = DateTime.UtcNow
        };

        // Cache the report
        _usageReports.AddOrUpdate(tenantId,
            _ => new List<UsageReport> { report },
            (_, existing) => { existing.Add(report); return existing; });

        return Task.FromResult(report);
    }

    public Task<TenantSubscription?> GetCurrentSubscriptionAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        _subscriptions.TryGetValue(tenantId, out var subscription);
        return Task.FromResult(subscription);
    }

    private static int GetPriceForTier(SubscriptionTier tier)
    {
        return tier switch
        {
            SubscriptionTier.Free => 0,
            SubscriptionTier.Starter => 2900, // $29.00
            SubscriptionTier.Professional => 9900, // $99.00
            SubscriptionTier.Enterprise => 49900, // $499.00
            SubscriptionTier.Custom => 0, // Custom pricing
            _ => 0
        };
    }
}
