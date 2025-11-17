namespace HotSwap.Distributed.Domain.Enums;

/// <summary>
/// Represents the current status of a message.
/// </summary>
public enum MessageStatus
{
    /// <summary>
    /// Message is queued, awaiting delivery.
    /// </summary>
    Pending,

    /// <summary>
    /// Message has been delivered to consumer.
    /// </summary>
    Delivered,

    /// <summary>
    /// Consumer has acknowledged successful processing.
    /// </summary>
    Acknowledged,

    /// <summary>
    /// Delivery failed after max retries (moved to DLQ).
    /// </summary>
    Failed,

    /// <summary>
    /// Message expired before delivery (TTL exceeded).
    /// </summary>
    Expired
}
