using HotSwap.Distributed.Domain.Enums;

namespace HotSwap.Distributed.Domain.Models;

/// <summary>
/// Represents a tenant in the multi-tenant system.
/// </summary>
public class Tenant
{
    /// <summary>
    /// Unique identifier for the tenant.
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// Tenant name (e.g., "Acme Corporation").
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Subdomain for tenant access (e.g., "acme" for acme.platform.com).
    /// </summary>
    public required string Subdomain { get; set; }

    /// <summary>
    /// Optional custom domain (e.g., "www.acme.com").
    /// </summary>
    public string? CustomDomain { get; set; }

    /// <summary>
    /// Current operational status of the tenant.
    /// </summary>
    public TenantStatus Status { get; set; } = TenantStatus.Provisioning;

    /// <summary>
    /// Subscription tier for billing and quotas.
    /// </summary>
    public SubscriptionTier Tier { get; set; } = SubscriptionTier.Free;

    /// <summary>
    /// Resource quotas based on subscription tier.
    /// </summary>
    public ResourceQuota ResourceQuota { get; set; } = new ResourceQuota();

    /// <summary>
    /// Date and time when the tenant was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Date and time when the tenant was suspended (if applicable).
    /// </summary>
    public DateTime? SuspendedAt { get; set; }

    /// <summary>
    /// Date and time when the tenant was deleted (if applicable).
    /// </summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>
    /// Additional metadata for the tenant (tags, configuration, etc.).
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();

    /// <summary>
    /// Database schema name for tenant isolation (e.g., "tenant_abc123").
    /// </summary>
    public string? DatabaseSchema { get; set; }

    /// <summary>
    /// Kubernetes namespace for tenant isolation (e.g., "tenant-abc123").
    /// </summary>
    public string? KubernetesNamespace { get; set; }

    /// <summary>
    /// Cache key prefix for tenant isolation using in-memory cache (e.g., "tenant:abc123:").
    /// </summary>
    public string? CacheKeyPrefix { get; set; }

    /// <summary>
    /// S3 bucket prefix for tenant isolation (e.g., "tenant-abc123/").
    /// </summary>
    public string? StorageBucketPrefix { get; set; }

    /// <summary>
    /// Contact email for the tenant administrator.
    /// </summary>
    public string? ContactEmail { get; set; }

    /// <summary>
    /// Checks if the tenant is active and operational.
    /// </summary>
    public bool IsActive() => Status == TenantStatus.Active;

    /// <summary>
    /// Checks if the tenant is suspended.
    /// </summary>
    public bool IsSuspended() => Status == TenantStatus.Suspended;

    /// <summary>
    /// Checks if the tenant is deleted.
    /// </summary>
    public bool IsDeleted() => Status == TenantStatus.Deleted;
}
