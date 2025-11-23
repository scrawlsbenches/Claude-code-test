using HotSwap.Distributed.Domain.Enums;

namespace HotSwap.Distributed.Api.Models;

#region Request Models

/// <summary>
/// Request to create a new tenant.
/// </summary>
public class CreateTenantRequest
{
    /// <summary>
    /// Tenant name (e.g., "Acme Corporation").
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Subdomain for tenant access (e.g., "acme").
    /// </summary>
    public required string Subdomain { get; set; }

    /// <summary>
    /// Subscription tier.
    /// </summary>
    public SubscriptionTier Tier { get; set; } = SubscriptionTier.Free;

    /// <summary>
    /// Contact email for the tenant administrator.
    /// </summary>
    public required string ContactEmail { get; set; }

    /// <summary>
    /// Optional custom domain.
    /// </summary>
    public string? CustomDomain { get; set; }

    /// <summary>
    /// Additional metadata.
    /// </summary>
    public Dictionary<string, string>? Metadata { get; set; }
}

/// <summary>
/// Request to update a tenant.
/// </summary>
public class UpdateTenantRequest
{
    /// <summary>
    /// Tenant name.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Contact email.
    /// </summary>
    public string? ContactEmail { get; set; }

    /// <summary>
    /// Custom domain.
    /// </summary>
    public string? CustomDomain { get; set; }

    /// <summary>
    /// Metadata updates.
    /// </summary>
    public Dictionary<string, string>? Metadata { get; set; }
}

/// <summary>
/// Request to upgrade/downgrade subscription.
/// </summary>
public class UpdateSubscriptionRequest
{
    /// <summary>
    /// New subscription tier.
    /// </summary>
    public SubscriptionTier Tier { get; set; }
}

/// <summary>
/// Request to suspend a tenant.
/// </summary>
public class SuspendTenantRequest
{
    /// <summary>
    /// Reason for suspension.
    /// </summary>
    public required string Reason { get; set; }
}

#endregion

#region Response Models

/// <summary>
/// Tenant information response.
/// </summary>
public class TenantResponse
{
    public Guid TenantId { get; set; }
    public required string Name { get; set; }
    public required string Subdomain { get; set; }
    public string? CustomDomain { get; set; }
    public required string Status { get; set; }
    public required string Tier { get; set; }
    public required ResourceQuotaResponse ResourceQuota { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? SuspendedAt { get; set; }
    public required string ContactEmail { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
}

/// <summary>
/// Resource quota information.
/// </summary>
public class ResourceQuotaResponse
{
    public int MaxWebsites { get; set; }
    public long StorageQuotaGB { get; set; }
    public long BandwidthQuotaGB { get; set; }
    public int MaxConcurrentDeployments { get; set; }
    public int MaxCustomDomains { get; set; }
}

/// <summary>
/// Tenant summary for list view.
/// </summary>
public class TenantSummary
{
    public Guid TenantId { get; set; }
    public required string Name { get; set; }
    public required string Subdomain { get; set; }
    public required string Status { get; set; }
    public required string Tier { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Subscription information response.
/// </summary>
public class SubscriptionResponse
{
    public Guid SubscriptionId { get; set; }
    public Guid TenantId { get; set; }
    public required string Tier { get; set; }
    public required string Status { get; set; }
    public required string BillingCycle { get; set; }
    public decimal AmountUSD { get; set; }
    public DateTime CurrentPeriodStart { get; set; }
    public DateTime CurrentPeriodEnd { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Usage report response.
/// </summary>
public class UsageReportResponse
{
    public Guid TenantId { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public decimal StorageUsedGB { get; set; }
    public decimal BandwidthUsedGB { get; set; }
    public int DeploymentsCount { get; set; }
    public decimal TotalCost { get; set; }
    public Dictionary<string, decimal> LineItems { get; set; } = new();
}

#endregion
