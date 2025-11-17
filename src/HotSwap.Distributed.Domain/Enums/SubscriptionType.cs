namespace HotSwap.Distributed.Domain.Enums;

/// <summary>
/// Represents the type of a subscription.
/// </summary>
public enum SubscriptionType
{
    /// <summary>
    /// Broker pushes messages to consumer (webhook).
    /// </summary>
    Push,

    /// <summary>
    /// Consumer polls for messages (HTTP GET).
    /// </summary>
    Pull
}
