using HotSwap.Distributed.Domain.Models;

namespace HotSwap.Distributed.Infrastructure.Interfaces;

/// <summary>
/// Service for managing tenant-scoped deployments.
/// </summary>
public interface ITenantDeploymentService
{
    /// <summary>
    /// Deploys a module to a tenant's website(s).
    /// </summary>
    Task<TenantDeploymentResult> DeployAsync(
        TenantDeploymentRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets deployment status for a tenant.
    /// </summary>
    Task<TenantDeploymentResult?> GetDeploymentStatusAsync(
        Guid deploymentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all deployments for a tenant.
    /// </summary>
    Task<List<TenantDeploymentResult>> GetDeploymentsForTenantAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Rolls back a deployment.
    /// </summary>
    Task<bool> RollbackDeploymentAsync(
        Guid deploymentId,
        CancellationToken cancellationToken = default);
}
