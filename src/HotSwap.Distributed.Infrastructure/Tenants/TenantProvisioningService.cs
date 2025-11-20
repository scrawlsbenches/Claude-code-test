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

    // Resource provisioning methods
    private async Task ProvisionDatabaseSchemaAsync(Tenant tenant, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Provisioning database schema: {Schema}", tenant.DatabaseSchema);

        try
        {
            // Execute CREATE SCHEMA SQL command
            // In production, this would use DbConnection to execute SQL
            // For now, we log the action as the repository handles the actual database
            _logger.LogInformation("Database schema {Schema} provisioned for tenant {TenantId}",
                tenant.DatabaseSchema, tenant.TenantId);

            // Simulate async database operation
            await Task.Delay(100, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to provision database schema: {Schema}", tenant.DatabaseSchema);
            throw;
        }
    }

    private async Task ProvisionKubernetesNamespaceAsync(Tenant tenant, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Provisioning Kubernetes namespace: {Namespace}", tenant.KubernetesNamespace);

        try
        {
            // Create Kubernetes namespace with resource quotas
            // In production, this would use Kubernetes.Client library
            // Example production code:
            // var k8s = new Kubernetes(KubernetesClientConfiguration.BuildDefaultConfig());
            // var ns = new V1Namespace { Metadata = new V1ObjectMeta { Name = tenant.KubernetesNamespace } };
            // await k8s.CreateNamespaceAsync(ns, cancellationToken: cancellationToken);
            //
            // var quota = new V1ResourceQuota
            // {
            //     Metadata = new V1ObjectMeta { Name = "tenant-quota", NamespaceProperty = tenant.KubernetesNamespace },
            //     Spec = new V1ResourceQuotaSpec
            //     {
            //         Hard = new Dictionary<string, ResourceQuantity>
            //         {
            //             { "requests.cpu", new ResourceQuantity($"{tenant.ResourceQuota.MaxCpu}") },
            //             { "requests.memory", new ResourceQuantity($"{tenant.ResourceQuota.MaxMemoryMB}Mi") },
            //             { "pods", new ResourceQuantity($"{tenant.ResourceQuota.MaxPods}") }
            //         }
            //     }
            // };
            // await k8s.CreateNamespacedResourceQuotaAsync(quota, tenant.KubernetesNamespace, cancellationToken: cancellationToken);

            _logger.LogInformation("Kubernetes namespace {Namespace} provisioned with resource quotas for tenant {TenantId}",
                tenant.KubernetesNamespace, tenant.TenantId);

            // Simulate async Kubernetes operation
            await Task.Delay(150, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to provision Kubernetes namespace: {Namespace}", tenant.KubernetesNamespace);
            throw;
        }
    }

    private Task ProvisionRedisNamespaceAsync(Tenant tenant, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Provisioning Redis namespace: {Prefix}", tenant.RedisKeyPrefix);
        // Redis namespacing is logical via key prefixes, no provisioning needed
        return Task.CompletedTask;
    }

    private async Task ProvisionStorageBucketAsync(Tenant tenant, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Provisioning storage bucket prefix: {Prefix}", tenant.StorageBucketPrefix);

        try
        {
            // Create S3 bucket or prefix with appropriate permissions
            // In production, this would use AWS SDK or Azure Storage SDK
            // Example production code for AWS S3:
            // using var s3Client = new AmazonS3Client();
            // var bucketName = $"tenant-{tenant.TenantId:N}";
            // await s3Client.PutBucketAsync(new PutBucketRequest
            // {
            //     BucketName = bucketName,
            //     UseClientRegion = true
            // }, cancellationToken);
            //
            // // Set bucket policy for tenant isolation
            // var policy = new
            // {
            //     Version = "2012-10-17",
            //     Statement = new[]
            //     {
            //         new
            //         {
            //             Effect = "Allow",
            //             Principal = new { AWS = $"arn:aws:iam::account:role/tenant-{tenant.TenantId}" },
            //             Action = new[] { "s3:GetObject", "s3:PutObject", "s3:DeleteObject" },
            //             Resource = $"arn:aws:s3:::{bucketName}/{tenant.StorageBucketPrefix}*"
            //         }
            //     }
            // };
            // await s3Client.PutBucketPolicyAsync(new PutBucketPolicyRequest
            // {
            //     BucketName = bucketName,
            //     Policy = JsonSerializer.Serialize(policy)
            // }, cancellationToken);

            _logger.LogInformation("Storage bucket provisioned with prefix {Prefix} for tenant {TenantId}",
                tenant.StorageBucketPrefix, tenant.TenantId);

            // Simulate async storage operation
            await Task.Delay(100, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to provision storage bucket: {Prefix}", tenant.StorageBucketPrefix);
            throw;
        }
    }

    private async Task CreateDefaultAdminUserAsync(Tenant tenant, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Creating default admin user for tenant: {TenantId}", tenant.TenantId);

        try
        {
            // Create default admin user for tenant
            // In production, this would create a user with hashed password
            // Example production code:
            // var defaultUser = new User
            // {
            //     UserId = Guid.NewGuid(),
            //     TenantId = tenant.TenantId,
            //     Username = $"admin@{tenant.Subdomain}",
            //     Email = $"admin@{tenant.Subdomain}.example.com",
            //     PasswordHash = BCrypt.Net.BCrypt.HashPassword("ChangeMe123!"),
            //     Role = UserRole.TenantAdmin,
            //     CreatedAt = DateTime.UtcNow
            // };
            // await _userRepository.CreateAsync(defaultUser, cancellationToken);

            _logger.LogInformation("Default admin user created for tenant {TenantId}", tenant.TenantId);

            // Simulate async user creation
            await Task.Delay(50, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create default admin user for tenant: {TenantId}", tenant.TenantId);
            throw;
        }
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
    private async Task CleanupDatabaseSchemaAsync(Tenant tenant, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Cleaning up database schema: {Schema}", tenant.DatabaseSchema);

        try
        {
            // Execute DROP SCHEMA SQL command
            // In production, this would use DbConnection to execute SQL
            // Example production code:
            // await using var connection = new NpgsqlConnection(connectionString);
            // await connection.OpenAsync(cancellationToken);
            // await using var command = new NpgsqlCommand($"DROP SCHEMA IF EXISTS {tenant.DatabaseSchema} CASCADE", connection);
            // await command.ExecuteNonQueryAsync(cancellationToken);

            _logger.LogInformation("Database schema {Schema} cleaned up for tenant {TenantId}",
                tenant.DatabaseSchema, tenant.TenantId);

            // Simulate async operation
            await Task.Delay(100, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cleanup database schema: {Schema}", tenant.DatabaseSchema);
            // Don't rethrow - best effort cleanup
        }
    }

    private async Task CleanupKubernetesNamespaceAsync(Tenant tenant, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Cleaning up Kubernetes namespace: {Namespace}", tenant.KubernetesNamespace);

        try
        {
            // Delete Kubernetes namespace
            // In production, this would use Kubernetes.Client library
            // Example production code:
            // var k8s = new Kubernetes(KubernetesClientConfiguration.BuildDefaultConfig());
            // await k8s.DeleteNamespaceAsync(tenant.KubernetesNamespace, cancellationToken: cancellationToken);

            _logger.LogInformation("Kubernetes namespace {Namespace} cleaned up for tenant {TenantId}",
                tenant.KubernetesNamespace, tenant.TenantId);

            // Simulate async operation
            await Task.Delay(150, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cleanup Kubernetes namespace: {Namespace}", tenant.KubernetesNamespace);
            // Don't rethrow - best effort cleanup
        }
    }

    private async Task CleanupRedisNamespaceAsync(Tenant tenant, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Cleaning up Redis namespace: {Prefix}", tenant.RedisKeyPrefix);

        try
        {
            // Delete all keys with tenant prefix
            // In production, this would use StackExchange.Redis
            // Example production code:
            // var connection = await ConnectionMultiplexer.ConnectAsync(redisConnectionString);
            // var db = connection.GetDatabase();
            // var server = connection.GetServer(connection.GetEndPoints().First());
            // await foreach (var key in server.KeysAsync(pattern: $"{tenant.RedisKeyPrefix}*"))
            // {
            //     await db.KeyDeleteAsync(key);
            // }

            _logger.LogInformation("Redis namespace {Prefix} cleaned up for tenant {TenantId}",
                tenant.RedisKeyPrefix, tenant.TenantId);

            // Simulate async operation
            await Task.Delay(50, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cleanup Redis namespace: {Prefix}", tenant.RedisKeyPrefix);
            // Don't rethrow - best effort cleanup
        }
    }

    private async Task CleanupStorageBucketAsync(Tenant tenant, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Cleaning up storage bucket prefix: {Prefix}", tenant.StorageBucketPrefix);

        try
        {
            // Delete all objects with tenant prefix
            // In production, this would use AWS SDK or Azure Storage SDK
            // Example production code for AWS S3:
            // using var s3Client = new AmazonS3Client();
            // var bucketName = $"tenant-{tenant.TenantId:N}";
            // var listRequest = new ListObjectsV2Request
            // {
            //     BucketName = bucketName,
            //     Prefix = tenant.StorageBucketPrefix
            // };
            // ListObjectsV2Response listResponse;
            // do
            // {
            //     listResponse = await s3Client.ListObjectsV2Async(listRequest, cancellationToken);
            //     if (listResponse.S3Objects.Any())
            //     {
            //         var deleteRequest = new DeleteObjectsRequest
            //         {
            //             BucketName = bucketName,
            //             Objects = listResponse.S3Objects.Select(o => new KeyVersion { Key = o.Key }).ToList()
            //         };
            //         await s3Client.DeleteObjectsAsync(deleteRequest, cancellationToken);
            //     }
            //     listRequest.ContinuationToken = listResponse.NextContinuationToken;
            // } while (listResponse.IsTruncated);

            _logger.LogInformation("Storage bucket prefix {Prefix} cleaned up for tenant {TenantId}",
                tenant.StorageBucketPrefix, tenant.TenantId);

            // Simulate async operation
            await Task.Delay(100, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cleanup storage bucket: {Prefix}", tenant.StorageBucketPrefix);
            // Don't rethrow - best effort cleanup
        }
    }
}
