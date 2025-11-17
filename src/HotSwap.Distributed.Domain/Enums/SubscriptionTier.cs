namespace HotSwap.Distributed.Domain.Enums;

/// <summary>
/// Represents the subscription tier for a tenant.
/// </summary>
public enum SubscriptionTier
{
    /// <summary>
    /// Free tier with limited resources.
    /// </summary>
    Free,

    /// <summary>
    /// Starter tier for small projects.
    /// </summary>
    Starter,

    /// <summary>
    /// Professional tier for growing businesses.
    /// </summary>
    Professional,

    /// <summary>
    /// Enterprise tier with dedicated resources.
    /// </summary>
    Enterprise,

    /// <summary>
    /// Custom tier with negotiated pricing.
    /// </summary>
    Custom
}
