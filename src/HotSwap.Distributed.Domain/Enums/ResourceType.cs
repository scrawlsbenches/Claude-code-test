namespace HotSwap.Distributed.Domain.Enums;

/// <summary>
/// Types of resources that can be tracked and limited per tenant.
/// </summary>
public enum ResourceType
{
    /// <summary>
    /// Storage resources (in GB).
    /// </summary>
    Storage,

    /// <summary>
    /// Bandwidth resources (in GB).
    /// </summary>
    Bandwidth,

    /// <summary>
    /// Number of websites.
    /// </summary>
    Websites,

    /// <summary>
    /// Number of concurrent deployments.
    /// </summary>
    ConcurrentDeployments,

    /// <summary>
    /// Number of custom domains.
    /// </summary>
    CustomDomains
}
