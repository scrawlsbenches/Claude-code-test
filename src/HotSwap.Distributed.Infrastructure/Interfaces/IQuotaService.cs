using HotSwap.Distributed.Domain.Enums;

namespace HotSwap.Distributed.Infrastructure.Interfaces;

/// <summary>
/// Service for enforcing resource quotas for tenants.
/// </summary>
public interface IQuotaService
{
    /// <summary>
    /// Checks if a tenant has available quota for a resource.
    /// </summary>
    /// <param name="tenantId">Tenant ID</param>
    /// <param name="resourceType">Resource type to check</param>
    /// <param name="amount">Amount needed</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if quota available, false otherwise</returns>
    Task<bool> CheckQuotaAsync(Guid tenantId, ResourceType resourceType, long amount, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets current usage for a resource type.
    /// </summary>
    /// <param name="tenantId">Tenant ID</param>
    /// <param name="resourceType">Resource type</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Current usage amount</returns>
    Task<long> GetCurrentUsageAsync(Guid tenantId, ResourceType resourceType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records resource usage for a tenant.
    /// </summary>
    /// <param name="tenantId">Tenant ID</param>
    /// <param name="resourceType">Resource type</param>
    /// <param name="amount">Amount used</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if usage recorded successfully</returns>
    Task<bool> RecordUsageAsync(Guid tenantId, ResourceType resourceType, long amount, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the quota limit for a resource type for a tenant.
    /// </summary>
    /// <param name="tenantId">Tenant ID</param>
    /// <param name="resourceType">Resource type</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Quota limit</returns>
    Task<long> GetQuotaLimitAsync(Guid tenantId, ResourceType resourceType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a tenant is within quota for all resource types.
    /// </summary>
    /// <param name="tenantId">Tenant ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if within quota for all resources</returns>
    Task<bool> IsWithinQuotaAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
