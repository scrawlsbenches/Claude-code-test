namespace HotSwap.Distributed.Domain.Models;

/// <summary>
/// Represents a usage report for a tenant for a specific billing period.
/// </summary>
public class UsageReport
{
    /// <summary>
    /// Tenant identifier.
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// Start of the reporting period.
    /// </summary>
    public DateTime PeriodStart { get; set; }

    /// <summary>
    /// End of the reporting period.
    /// </summary>
    public DateTime PeriodEnd { get; set; }

    /// <summary>
    /// Storage used in gigabytes.
    /// </summary>
    public decimal StorageUsedGB { get; set; }

    /// <summary>
    /// Bandwidth used in gigabytes.
    /// </summary>
    public decimal BandwidthUsedGB { get; set; }

    /// <summary>
    /// Number of deployments executed during the period.
    /// </summary>
    public int DeploymentsCount { get; set; }

    /// <summary>
    /// Total cost for the period in USD.
    /// </summary>
    public decimal TotalCost { get; set; }

    /// <summary>
    /// Itemized costs breakdown.
    /// </summary>
    public Dictionary<string, decimal> LineItems { get; set; } = new();

    /// <summary>
    /// Date and time when the report was generated.
    /// </summary>
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}
