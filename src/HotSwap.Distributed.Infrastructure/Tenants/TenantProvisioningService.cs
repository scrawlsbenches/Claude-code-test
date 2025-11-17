using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Interfaces;
using Microsoft.Extensions.Logging;

namespace HotSwap.Distributed.Infrastructure.Tenants;

/// <summary>
/// Service for provisioning and deprovisioning tenant resources.
/// </summary>
public class TenantProvisioningService : ITenantProvisioningService
{
    private readonly ITenantRepository _tenantRepository;
    private readonly ILogger<TenantProvisioningService> _logger;

    public TenantProvisioningService(
        ITenantRepository tenantRepository,
        ILogger<TenantProvisioningService> logger)
    {
        _tenantRepository = tenantRepository ?? throw new ArgumentNullException(nameof(tenantRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Tenant> ProvisionTenantAsync(Tenant tenant, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting tenant provisioning for: {TenantName} (Subdomain: {Subdomain})",
            tenant.Name, tenant.Subdomain);

        try
        {
            // Validate provisioning prerequisites
            var validationResult = await ValidateProvisioningAsync(tenant);
            if (!validationResult.IsValid)
            {
                var errors = string.Join(", ", validationResult.Errors);
                throw new InvalidOperationException($"Provisioning validation failed: {errors}");
            }

            // Set tenant status to provisioning
            tenant.Status = TenantStatus.Provisioning;

            // Generate resource identifiers
            var tenantIdStr = tenant.TenantId.ToString("N").Substring(0, 8);
            tenant.DatabaseSchema = $"tenant_{tenantIdStr}";
            tenant.KubernetesNamespace = $"tenant-{tenantIdStr}";
            tenant.RedisKeyPrefix = $"tenant:{tenantIdStr}:";
            tenant.StorageBucketPrefix = $"tenant-{tenantIdStr}/";

            // Create tenant record
            tenant = await _tenantRepository.CreateAsync(tenant, cancellationToken);

            // Provision resources
            await ProvisionDatabaseSchemaAsync(tenant, cancellationToken);
            await ProvisionKubernetesNamespaceAsync(tenant, cancellationToken);
            await ProvisionRedisNamespaceAsync(tenant, cancellationToken);
            await ProvisionStorageBucketAsync(tenant, cancellationToken);
            await CreateDefaultAdminUserAsync(tenant, cancellationToken);

            // Update tenant status to active
            tenant.Status = TenantStatus.Active;
            await _tenantRepository.UpdateAsync(tenant, cancellationToken);

            _logger.LogInformation("Successfully provisioned tenant: {TenantName} (ID: {TenantId})",
                tenant.Name, tenant.TenantId);

            return tenant;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to provision tenant: {TenantName}", tenant.Name);

            // Attempt rollback
            await RollbackProvisioningAsync(tenant, cancellationToken);

            throw;
        }
    }

    public async Task<bool> DeprovisionTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting tenant deprovision for ID: {TenantId}", tenantId);

        var tenant = await _tenantRepository.GetByIdAsync(tenantId, cancellationToken);
        if (tenant == null)
        {
            _logger.LogWarning("Tenant not found for deprovision: {TenantId}", tenantId);
            return false;
        }

        try
        {
            // Set status to deprovisioning
            tenant.Status = TenantStatus.Deprovisioning;
            await _tenantRepository.UpdateAsync(tenant, cancellationToken);

            // Clean up resources
            await CleanupDatabaseSchemaAsync(tenant, cancellationToken);
            await CleanupKubernetesNamespaceAsync(tenant, cancellationToken);
            await CleanupRedisNamespaceAsync(tenant, cancellationToken);
            await CleanupStorageBucketAsync(tenant, cancellationToken);

            // Soft delete tenant
            await _tenantRepository.DeleteAsync(tenantId, cancellationToken);

            _logger.LogInformation("Successfully deprovisioned tenant: {TenantName} (ID: {TenantId})",
                tenant.Name, tenant.TenantId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deprovision tenant: {TenantId}", tenantId);
            throw;
        }
    }

    public Task<ProvisioningValidationResult> ValidateProvisioningAsync(Tenant tenant)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(tenant.Name))
            errors.Add("Tenant name is required");

        if (string.IsNullOrWhiteSpace(tenant.Subdomain))
            errors.Add("Tenant subdomain is required");

        // Validate subdomain format (lowercase alphanumeric and hyphens only)
        if (!string.IsNullOrWhiteSpace(tenant.Subdomain))
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(tenant.Subdomain, "^[a-z0-9-]+$"))
                errors.Add("Subdomain must contain only lowercase letters, numbers, and hyphens");
        }

        if (tenant.ResourceQuota == null)
            errors.Add("Resource quota is required");

        var result = errors.Count == 0
            ? ProvisioningValidationResult.Success()
            : ProvisioningValidationResult.Failure(errors.ToArray());

        return Task.FromResult(result);
    }

    public async Task<bool> SuspendTenantAsync(Guid tenantId, string reason, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Suspending tenant: {TenantId}, Reason: {Reason}", tenantId, reason);

        var tenant = await _tenantRepository.GetByIdAsync(tenantId, cancellationToken);
        if (tenant == null)
            return false;

        tenant.Status = TenantStatus.Suspended;
        tenant.SuspendedAt = DateTime.UtcNow;
        tenant.Metadata["suspension_reason"] = reason;

        await _tenantRepository.UpdateAsync(tenant, cancellationToken);

        _logger.LogInformation("Suspended tenant: {TenantName} (ID: {TenantId})", tenant.Name, tenant.TenantId);
        return true;
    }

    public async Task<bool> ActivateTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Activating tenant: {TenantId}", tenantId);

        var tenant = await _tenantRepository.GetByIdAsync(tenantId, cancellationToken);
        if (tenant == null)
            return false;

        tenant.Status = TenantStatus.Active;
        tenant.SuspendedAt = null;
        tenant.Metadata.Remove("suspension_reason");

        await _tenantRepository.UpdateAsync(tenant, cancellationToken);

        _logger.LogInformation("Activated tenant: {TenantName} (ID: {TenantId})", tenant.Name, tenant.TenantId);
        return true;
    }

    // Resource provisioning methods (simulated for in-memory implementation)
    private Task ProvisionDatabaseSchemaAsync(Tenant tenant, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Provisioning database schema: {Schema}", tenant.DatabaseSchema);
        // TODO: Execute CREATE SCHEMA SQL command
        return Task.CompletedTask;
    }

    private Task ProvisionKubernetesNamespaceAsync(Tenant tenant, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Provisioning Kubernetes namespace: {Namespace}", tenant.KubernetesNamespace);
        // TODO: Create Kubernetes namespace with resource quotas
        return Task.CompletedTask;
    }

    private Task ProvisionRedisNamespaceAsync(Tenant tenant, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Provisioning Redis namespace: {Prefix}", tenant.RedisKeyPrefix);
        // Redis namespacing is logical via key prefixes, no provisioning needed
        return Task.CompletedTask;
    }

    private Task ProvisionStorageBucketAsync(Tenant tenant, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Provisioning storage bucket prefix: {Prefix}", tenant.StorageBucketPrefix);
        // TODO: Create S3 bucket or prefix with appropriate permissions
        return Task.CompletedTask;
    }

    private Task CreateDefaultAdminUserAsync(Tenant tenant, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Creating default admin user for tenant: {TenantId}", tenant.TenantId);
        // TODO: Create default admin user for tenant
        return Task.CompletedTask;
    }

    private async Task RollbackProvisioningAsync(Tenant tenant, CancellationToken cancellationToken)
    {
        _logger.LogWarning("Rolling back provisioning for tenant: {TenantId}", tenant.TenantId);

        try
        {
            await CleanupDatabaseSchemaAsync(tenant, cancellationToken);
            await CleanupKubernetesNamespaceAsync(tenant, cancellationToken);
            await CleanupStorageBucketAsync(tenant, cancellationToken);

            // Mark tenant as deleted
            await _tenantRepository.DeleteAsync(tenant.TenantId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Rollback failed for tenant: {TenantId}", tenant.TenantId);
        }
    }

    // Cleanup methods
    private Task CleanupDatabaseSchemaAsync(Tenant tenant, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Cleaning up database schema: {Schema}", tenant.DatabaseSchema);
        // TODO: Execute DROP SCHEMA SQL command
        return Task.CompletedTask;
    }

    private Task CleanupKubernetesNamespaceAsync(Tenant tenant, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Cleaning up Kubernetes namespace: {Namespace}", tenant.KubernetesNamespace);
        // TODO: Delete Kubernetes namespace
        return Task.CompletedTask;
    }

    private Task CleanupRedisNamespaceAsync(Tenant tenant, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Cleaning up Redis namespace: {Prefix}", tenant.RedisKeyPrefix);
        // TODO: Delete all keys with tenant prefix
        return Task.CompletedTask;
    }

    private Task CleanupStorageBucketAsync(Tenant tenant, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Cleaning up storage bucket prefix: {Prefix}", tenant.StorageBucketPrefix);
        // TODO: Delete all objects with tenant prefix
        return Task.CompletedTask;
    }
}
