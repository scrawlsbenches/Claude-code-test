namespace HotSwap.Distributed.Domain.Enums;

/// <summary>
/// Represents the delivery guarantee mode for a topic.
/// </summary>
public enum DeliveryGuarantee
{
    /// <summary>
    /// Fire-and-forget (no retry, may lose messages).
    /// </summary>
    AtMostOnce,

    /// <summary>
    /// Retry until acknowledged (may deliver duplicates).
    /// </summary>
    AtLeastOnce,

    /// <summary>
    /// Exactly-once delivery (no duplicates, uses distributed locks).
    /// </summary>
    ExactlyOnce
}
