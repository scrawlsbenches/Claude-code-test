namespace HotSwap.Distributed.Orchestrator.Delivery;

/// <summary>
/// Represents the result of a message delivery attempt.
/// </summary>
public class DeliveryResult
{
    /// <summary>
    /// Whether the delivery was successful.
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// The message ID that was delivered.
    /// </summary>
    public required string MessageId { get; set; }

    /// <summary>
    /// The consumer ID that received the message (if successful).
    /// </summary>
    public string? ConsumerId { get; set; }

    /// <summary>
    /// Error message if delivery failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Number of delivery attempts made.
    /// </summary>
    public int DeliveryAttempts { get; set; }

    /// <summary>
    /// Total delay in milliseconds due to retries.
    /// </summary>
    public long TotalDelayMs { get; set; }

    /// <summary>
    /// Whether the message was moved to the dead letter queue.
    /// </summary>
    public bool MovedToDLQ { get; set; }

    /// <summary>
    /// Creates a successful delivery result.
    /// </summary>
    public static DeliveryResult Success(string messageId, string consumerId)
    {
        return new DeliveryResult
        {
            IsSuccess = true,
            MessageId = messageId,
            ConsumerId = consumerId,
            DeliveryAttempts = 1
        };
    }

    /// <summary>
    /// Creates a failed delivery result.
    /// </summary>
    public static DeliveryResult Failure(string messageId, string errorMessage)
    {
        return new DeliveryResult
        {
            IsSuccess = false,
            MessageId = messageId,
            ErrorMessage = errorMessage,
            DeliveryAttempts = 1
        };
    }
}
