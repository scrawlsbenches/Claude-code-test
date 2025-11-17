namespace HotSwap.Distributed.Domain.Models;

/// <summary>
/// Value object representing resource quotas for a tenant.
/// </summary>
public class ResourceQuota
{
    /// <summary>
    /// Maximum number of websites allowed.
    /// </summary>
    public int MaxWebsites { get; set; }

    /// <summary>
    /// Storage quota in gigabytes.
    /// </summary>
    public long StorageQuotaGB { get; set; }

    /// <summary>
    /// Bandwidth quota in gigabytes per month.
    /// </summary>
    public long BandwidthQuotaGB { get; set; }

    /// <summary>
    /// Maximum number of concurrent deployments.
    /// </summary>
    public int MaxConcurrentDeployments { get; set; }

    /// <summary>
    /// Maximum number of custom domains allowed.
    /// </summary>
    public int MaxCustomDomains { get; set; }

    /// <summary>
    /// Creates a default quota for a given subscription tier.
    /// </summary>
    public static ResourceQuota CreateDefault(Enums.SubscriptionTier tier)
    {
        return tier switch
        {
            Enums.SubscriptionTier.Free => new ResourceQuota
            {
                MaxWebsites = 1,
                StorageQuotaGB = 1,
                BandwidthQuotaGB = 10,
                MaxConcurrentDeployments = 1,
                MaxCustomDomains = 0
            },
            Enums.SubscriptionTier.Starter => new ResourceQuota
            {
                MaxWebsites = 5,
                StorageQuotaGB = 10,
                BandwidthQuotaGB = 100,
                MaxConcurrentDeployments = 3,
                MaxCustomDomains = 5
            },
            Enums.SubscriptionTier.Professional => new ResourceQuota
            {
                MaxWebsites = 25,
                StorageQuotaGB = 50,
                BandwidthQuotaGB = 500,
                MaxConcurrentDeployments = 10,
                MaxCustomDomains = 25
            },
            Enums.SubscriptionTier.Enterprise => new ResourceQuota
            {
                MaxWebsites = 100,
                StorageQuotaGB = 500,
                BandwidthQuotaGB = 5000,
                MaxConcurrentDeployments = 50,
                MaxCustomDomains = 100
            },
            Enums.SubscriptionTier.Custom => new ResourceQuota
            {
                MaxWebsites = int.MaxValue,
                StorageQuotaGB = long.MaxValue,
                BandwidthQuotaGB = long.MaxValue,
                MaxConcurrentDeployments = int.MaxValue,
                MaxCustomDomains = int.MaxValue
            },
            _ => throw new ArgumentException($"Unknown subscription tier: {tier}")
        };
    }
}
