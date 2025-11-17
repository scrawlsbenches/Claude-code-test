using HotSwap.Distributed.Domain.Models;

namespace HotSwap.Distributed.Infrastructure.Interfaces;

/// <summary>
/// Repository for tenant management operations.
/// </summary>
public interface ITenantRepository
{
    /// <summary>
    /// Gets a tenant by ID.
    /// </summary>
    /// <param name="tenantId">Tenant ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Tenant if found, null otherwise</returns>
    Task<Tenant?> GetByIdAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a tenant by subdomain.
    /// </summary>
    /// <param name="subdomain">Tenant subdomain</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Tenant if found, null otherwise</returns>
    Task<Tenant?> GetBySubdomainAsync(string subdomain, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all tenants with optional filtering.
    /// </summary>
    /// <param name="status">Optional status filter</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of tenants</returns>
    Task<List<Tenant>> GetAllAsync(Domain.Enums.TenantStatus? status = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new tenant.
    /// </summary>
    /// <param name="tenant">Tenant to create</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created tenant</returns>
    Task<Tenant> CreateAsync(Tenant tenant, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing tenant.
    /// </summary>
    /// <param name="tenant">Tenant to update</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated tenant</returns>
    Task<Tenant> UpdateAsync(Tenant tenant, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a tenant (soft delete).
    /// </summary>
    /// <param name="tenantId">Tenant ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if deleted successfully</returns>
    Task<bool> DeleteAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a subdomain is available.
    /// </summary>
    /// <param name="subdomain">Subdomain to check</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if available, false if already taken</returns>
    Task<bool> IsSubdomainAvailableAsync(string subdomain, CancellationToken cancellationToken = default);

    /// <summary>
    /// Begins a database transaction for atomic operations.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Transaction object</returns>
    Task<IAsyncDisposable> BeginTransactionAsync(CancellationToken cancellationToken = default);
}
