using HotSwap.Distributed.Domain.Models;

namespace HotSwap.Distributed.Infrastructure.Interfaces;

/// <summary>
/// Service for resolving and managing tenant context from HTTP requests.
/// </summary>
public interface ITenantContextService
{
    /// <summary>
    /// Gets the current tenant from the HTTP context.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Current tenant if resolved, null otherwise</returns>
    Task<Tenant?> GetCurrentTenantAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current tenant ID from the HTTP context.
    /// </summary>
    /// <returns>Current tenant ID if resolved, null otherwise</returns>
    Guid? GetCurrentTenantId();

    /// <summary>
    /// Sets the current tenant in the HTTP context.
    /// </summary>
    /// <param name="tenant">Tenant to set</param>
    void SetCurrentTenant(Tenant tenant);

    /// <summary>
    /// Validates that the current tenant is active and operational.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if tenant is active, false otherwise</returns>
    Task<bool> ValidateCurrentTenantAsync(CancellationToken cancellationToken = default);
}
