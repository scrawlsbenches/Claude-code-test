using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;

namespace HotSwap.Distributed.Infrastructure.Interfaces;

/// <summary>
/// Service for managing tenant subscriptions and billing.
/// </summary>
public interface ISubscriptionService
{
    /// <summary>
    /// Creates a new subscription for a tenant.
    /// </summary>
    /// <param name="tenantId">Tenant ID</param>
    /// <param name="tier">Subscription tier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created subscription</returns>
    Task<TenantSubscription> CreateSubscriptionAsync(Guid tenantId, SubscriptionTier tier, CancellationToken cancellationToken = default);

    /// <summary>
    /// Upgrades a tenant's subscription to a higher tier.
    /// </summary>
    /// <param name="tenantId">Tenant ID</param>
    /// <param name="newTier">New subscription tier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated subscription</returns>
    Task<TenantSubscription> UpgradeSubscriptionAsync(Guid tenantId, SubscriptionTier newTier, CancellationToken cancellationToken = default);

    /// <summary>
    /// Downgrades a tenant's subscription to a lower tier.
    /// </summary>
    /// <param name="tenantId">Tenant ID</param>
    /// <param name="newTier">New subscription tier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated subscription</returns>
    Task<TenantSubscription> DowngradeSubscriptionAsync(Guid tenantId, SubscriptionTier newTier, CancellationToken cancellationToken = default);

    /// <summary>
    /// Suspends a subscription due to non-payment.
    /// </summary>
    /// <param name="tenantId">Tenant ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if suspension successful</returns>
    Task<bool> SuspendForNonPaymentAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets usage report for a tenant for a specific period.
    /// </summary>
    /// <param name="tenantId">Tenant ID</param>
    /// <param name="periodStart">Period start date</param>
    /// <param name="periodEnd">Period end date</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Usage report</returns>
    Task<UsageReport> GetUsageReportAsync(Guid tenantId, DateTime periodStart, DateTime periodEnd, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current subscription for a tenant.
    /// </summary>
    /// <param name="tenantId">Tenant ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Current subscription if exists</returns>
    Task<TenantSubscription?> GetCurrentSubscriptionAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
