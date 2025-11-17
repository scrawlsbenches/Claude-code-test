using HotSwap.Distributed.Domain.Models;

namespace HotSwap.Distributed.Infrastructure.Interfaces;

/// <summary>
/// Service for provisioning and deprovisioning tenant resources.
/// </summary>
public interface ITenantProvisioningService
{
    /// <summary>
    /// Provisions a new tenant with all required resources.
    /// </summary>
    /// <param name="tenant">Tenant to provision</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Provisioned tenant with resource identifiers</returns>
    Task<Tenant> ProvisionTenantAsync(Tenant tenant, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deprovisions a tenant and cleans up all resources.
    /// </summary>
    /// <param name="tenantId">Tenant ID to deprovision</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if deprovision successful</returns>
    Task<bool> DeprovisionTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates tenant provisioning prerequisites.
    /// </summary>
    /// <param name="tenant">Tenant to validate</param>
    /// <returns>Validation result with any errors</returns>
    Task<ProvisioningValidationResult> ValidateProvisioningAsync(Tenant tenant);

    /// <summary>
    /// Suspends a tenant (preserves resources but blocks access).
    /// </summary>
    /// <param name="tenantId">Tenant ID to suspend</param>
    /// <param name="reason">Suspension reason</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if suspension successful</returns>
    Task<bool> SuspendTenantAsync(Guid tenantId, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reactivates a suspended tenant.
    /// </summary>
    /// <param name="tenantId">Tenant ID to activate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if activation successful</returns>
    Task<bool> ActivateTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of provisioning validation.
/// </summary>
public class ProvisioningValidationResult
{
    /// <summary>
    /// Indicates if validation passed.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Validation error messages.
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    public static ProvisioningValidationResult Success() => new() { IsValid = true };

    /// <summary>
    /// Creates a failed validation result with errors.
    /// </summary>
    public static ProvisioningValidationResult Failure(params string[] errors) =>
        new() { IsValid = false, Errors = errors.ToList() };
}
