using System.Collections.Concurrent;
using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Interfaces;
using HotSwap.Distributed.Infrastructure.Storage;
using Microsoft.Extensions.Logging;

namespace HotSwap.Distributed.Infrastructure.Tenants;

/// <summary>
/// Service for managing tenant subscriptions and billing.
/// In production, integrate with Stripe or another payment provider.
/// </summary>
public class SubscriptionService : ISubscriptionService
{
    private readonly ITenantRepository _tenantRepository;
    private readonly IObjectStorageService? _storageService;
    private readonly ILogger<SubscriptionService> _logger;
    private readonly ConcurrentDictionary<Guid, TenantSubscription> _subscriptions = new();
    private readonly ConcurrentDictionary<Guid, List<UsageReport>> _usageReports = new();
    private readonly ConcurrentDictionary<Guid, long> _storageUsage = new();
    private readonly ConcurrentDictionary<Guid, long> _bandwidthUsage = new();

    public SubscriptionService(
        ITenantRepository tenantRepository,
        ILogger<SubscriptionService> logger,
        IObjectStorageService? storageService = null)
    {
        _tenantRepository = tenantRepository ?? throw new ArgumentNullException(nameof(tenantRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _storageService = storageService; // Optional - falls back to simulated metrics if not provided
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

    public async Task<UsageReport> GetUsageReportAsync(Guid tenantId, DateTime periodStart, DateTime periodEnd, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting usage report for tenant {TenantId} from {Start} to {End}",
            tenantId, periodStart, periodEnd);

        // Check if report already exists
        if (_usageReports.TryGetValue(tenantId, out var reports))
        {
            var existingReport = reports.FirstOrDefault(r =>
                r.PeriodStart == periodStart && r.PeriodEnd == periodEnd);
            if (existingReport != null)
                return existingReport;
        }

        // Calculate actual usage
        // In production, these would query actual metrics from:
        // - Storage: MinIO metrics API or filesystem usage monitoring
        // - Bandwidth: Nginx access logs or Varnish cache statistics
        // - Deployments: Deployment history from database

        var storageUsedGB = await CalculateStorageUsageAsync(tenantId, periodStart, periodEnd, cancellationToken);
        var bandwidthUsedGB = CalculateBandwidthUsage(tenantId, periodStart, periodEnd);
        var deploymentsCount = CountDeployments(tenantId, periodStart, periodEnd);

        // Get subscription for pricing
        var subscription = await GetCurrentSubscriptionAsync(tenantId, cancellationToken);
        var baseCost = subscription != null ? subscription.AmountCents / 100m : 0m;

        // Calculate overage costs
        var storageCostPerGB = 0.10m; // $0.10 per GB
        var bandwidthCostPerGB = 0.05m; // $0.05 per GB
        var storageCost = (decimal)Math.Max(0, storageUsedGB - 10) * storageCostPerGB; // First 10 GB free
        var bandwidthCost = (decimal)Math.Max(0, bandwidthUsedGB - 100) * bandwidthCostPerGB; // First 100 GB free

        var report = new UsageReport
        {
            TenantId = tenantId,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            StorageUsedGB = (long)storageUsedGB,
            BandwidthUsedGB = (long)bandwidthUsedGB,
            DeploymentsCount = deploymentsCount,
            TotalCost = baseCost + storageCost + bandwidthCost,
            LineItems = new Dictionary<string, decimal>
            {
                { "Base Subscription", baseCost },
                { "Additional Storage", storageCost },
                { "Additional Bandwidth", bandwidthCost }
            },
            GeneratedAt = DateTime.UtcNow
        };

        // Cache the report
        _usageReports.AddOrUpdate(tenantId,
            _ => new List<UsageReport> { report },
            (_, existing) => { existing.Add(report); return existing; });

        return report;
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

    private async Task<double> CalculateStorageUsageAsync(
        Guid tenantId,
        DateTime periodStart,
        DateTime periodEnd,
        CancellationToken cancellationToken = default)
    {
        if (_storageService != null)
        {
            try
            {
                // Get actual storage usage from MinIO
                var bucketName = $"tenant-{tenantId:N}";

                var bucketExists = await _storageService.BucketExistsAsync(bucketName, cancellationToken);
                if (bucketExists)
                {
                    var totalBytes = await _storageService.GetBucketSizeAsync(bucketName, cancellationToken);
                    var totalGB = totalBytes / (1024.0 * 1024.0 * 1024.0);

                    _logger.LogDebug("Calculated storage usage for tenant {TenantId}: {TotalBytes} bytes ({TotalGB} GB)",
                        tenantId, totalBytes, totalGB);

                    return totalGB;
                }
                else
                {
                    _logger.LogDebug("Bucket {BucketName} does not exist for tenant {TenantId} - storage usage is 0",
                        bucketName, tenantId);
                    return 0;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to calculate storage usage for tenant {TenantId} - falling back to simulated value",
                    tenantId);
                // Fall through to simulated value
            }
        }

        // Fallback to simulated value from in-memory tracking or 0
        _storageUsage.TryGetValue(tenantId, out var bytes);
        var gb = bytes / (1024.0 * 1024.0 * 1024.0);

        if (_storageService == null)
        {
            _logger.LogWarning("No storage service configured - using simulated storage usage for tenant {TenantId}: {GB} GB",
                tenantId, gb);
        }

        return gb;
    }

    private double CalculateBandwidthUsage(Guid tenantId, DateTime periodStart, DateTime periodEnd)
    {
        // Calculate actual bandwidth usage
        // In production, this would query:
        // - Nginx access logs (parse and aggregate bytes sent/received)
        // - Varnish cache statistics (varnishstat or varnishlog)
        // - Database query logs
        // Example production code for Nginx log analysis:
        // // Parse Nginx access logs: grep tenant-{tenantId} /var/log/nginx/access.log
        // // Sum the bytes_sent field (typically field $10 in default log format)
        // var logFile = $"/var/log/nginx/access.log";
        // var tenantPattern = $"tenant-{tenantId}";
        // var totalBytes = 0L;
        // await foreach (var line in File.ReadLinesAsync(logFile))
        // {
        //     if (line.Contains(tenantPattern))
        //     {
        //         var fields = line.Split(' ');
        //         if (fields.Length > 9 && long.TryParse(fields[9], out var bytes))
        //             totalBytes += bytes;
        //     }
        // }
        // return totalBytes / (1024.0 * 1024.0 * 1024.0); // Convert to GB

        // Return simulated value from in-memory tracking or 0
        _bandwidthUsage.TryGetValue(tenantId, out var bytes);
        return bytes / (1024.0 * 1024.0 * 1024.0); // Convert to GB
    }

    private int CountDeployments(Guid tenantId, DateTime periodStart, DateTime periodEnd)
    {
        // Count deployments in period
        // In production, this would query deployment history from database
        // Example production code:
        // await using var connection = new NpgsqlConnection(connectionString);
        // await connection.OpenAsync();
        // await using var command = new NpgsqlCommand(
        //     "SELECT COUNT(*) FROM deployments WHERE tenant_id = @tenantId " +
        //     "AND created_at >= @periodStart AND created_at < @periodEnd",
        //     connection);
        // command.Parameters.AddWithValue("tenantId", tenantId);
        // command.Parameters.AddWithValue("periodStart", periodStart);
        // command.Parameters.AddWithValue("periodEnd", periodEnd);
        // return Convert.ToInt32(await command.ExecuteScalarAsync());

        // Return simulated value
        return 0; // Would be populated from actual deployment tracking
    }
}
