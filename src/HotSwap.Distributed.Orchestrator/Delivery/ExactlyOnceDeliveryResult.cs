namespace HotSwap.Distributed.Orchestrator.Delivery;

/// <summary>
/// Result of an exactly-once delivery attempt.
/// </summary>
public class ExactlyOnceDeliveryResult
{
    /// <summary>
    /// Whether the delivery was successful.
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// Whether this message was detected as a duplicate.
    /// </summary>
    public bool IsDuplicate { get; set; }

    /// <summary>
    /// The message ID.
    /// </summary>
    public required string MessageId { get; set; }

    /// <summary>
    /// The idempotency key used for deduplication.
    /// </summary>
    public string? IdempotencyKey { get; set; }

    /// <summary>
    /// The consumer ID that received the message (if successful).
    /// </summary>
    public string? ConsumerId { get; set; }

    /// <summary>
    /// Error message if delivery failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// When the lock was acquired.
    /// </summary>
    public DateTime? LockAcquiredAt { get; set; }

    /// <summary>
    /// How long it took to acquire the lock (milliseconds).
    /// </summary>
    public long LockWaitTimeMs { get; set; }

    /// <summary>
    /// Creates a successful delivery result.
    /// </summary>
    public static ExactlyOnceDeliveryResult Success(string messageId, string consumerId, string idempotencyKey)
    {
        return new ExactlyOnceDeliveryResult
        {
            IsSuccess = true,
            IsDuplicate = false,
            MessageId = messageId,
            IdempotencyKey = idempotencyKey,
            ConsumerId = consumerId
        };
    }

    /// <summary>
    /// Creates a duplicate detection result.
    /// </summary>
    public static ExactlyOnceDeliveryResult Duplicate(string messageId, string idempotencyKey)
    {
        return new ExactlyOnceDeliveryResult
        {
            IsSuccess = false,
            IsDuplicate = true,
            MessageId = messageId,
            IdempotencyKey = idempotencyKey
        };
    }

    /// <summary>
    /// Creates a failed delivery result.
    /// </summary>
    public static ExactlyOnceDeliveryResult Failure(string messageId, string errorMessage, string? idempotencyKey = null)
    {
        return new ExactlyOnceDeliveryResult
        {
            IsSuccess = false,
            IsDuplicate = false,
            MessageId = messageId,
            ErrorMessage = errorMessage,
            IdempotencyKey = idempotencyKey
        };
    }
}
