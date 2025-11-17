using HotSwap.Distributed.Domain.Enums;

namespace HotSwap.Distributed.Domain.Models;

/// <summary>
/// Represents a subscription for a tenant.
/// </summary>
public class Subscription
{
    /// <summary>
    /// Unique identifier for the subscription.
    /// </summary>
    public Guid SubscriptionId { get; set; }

    /// <summary>
    /// Tenant identifier.
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// Subscription tier.
    /// </summary>
    public SubscriptionTier Tier { get; set; }

    /// <summary>
    /// Subscription status (Active, Suspended, Cancelled).
    /// </summary>
    public string Status { get; set; } = "Active";

    /// <summary>
    /// Billing cycle (Monthly, Yearly).
    /// </summary>
    public string BillingCycle { get; set; } = "Monthly";

    /// <summary>
    /// Subscription amount in cents (USD).
    /// </summary>
    public int AmountCents { get; set; }

    /// <summary>
    /// Start of the current billing period.
    /// </summary>
    public DateTime CurrentPeriodStart { get; set; }

    /// <summary>
    /// End of the current billing period.
    /// </summary>
    public DateTime CurrentPeriodEnd { get; set; }

    /// <summary>
    /// Stripe subscription ID (if using Stripe).
    /// </summary>
    public string? StripeSubscriptionId { get; set; }

    /// <summary>
    /// Stripe customer ID (if using Stripe).
    /// </summary>
    public string? StripeCustomerId { get; set; }

    /// <summary>
    /// Date and time when the subscription was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Date and time when the subscription was cancelled (if applicable).
    /// </summary>
    public DateTime? CancelledAt { get; set; }

    /// <summary>
    /// Checks if the subscription is active.
    /// </summary>
    public bool IsActive() => Status == "Active";

    /// <summary>
    /// Checks if the subscription is cancelled.
    /// </summary>
    public bool IsCancelled() => Status == "Cancelled";
}
