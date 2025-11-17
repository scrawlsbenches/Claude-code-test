using HotSwap.Distributed.Domain.Enums;

namespace HotSwap.Distributed.Domain.Models;

/// <summary>
/// Represents a message in the distributed messaging system.
/// </summary>
public class Message
{
    /// <summary>
    /// Unique message identifier (GUID format).
    /// </summary>
    public required string MessageId { get; set; }

    /// <summary>
    /// Target topic name.
    /// </summary>
    public required string TopicName { get; set; }

    /// <summary>
    /// Message payload (JSON format, max 1 MB).
    /// </summary>
    public required string Payload { get; set; }

    /// <summary>
    /// Schema version used for validation (e.g., "1.0", "2.0").
    /// </summary>
    public required string SchemaVersion { get; set; }

    /// <summary>
    /// Publish timestamp (UTC).
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Message metadata (key-value pairs).
    /// </summary>
    public Dictionary<string, string> Headers { get; set; } = new();

    /// <summary>
    /// Request/reply correlation identifier (optional).
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// Message priority (0-9, where 9 is highest priority).
    /// </summary>
    public int Priority { get; set; } = 0;

    /// <summary>
    /// Message expiration time (TTL). Null means no expiration.
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// Number of delivery attempts (incremented on retry).
    /// </summary>
    public int DeliveryAttempts { get; set; } = 0;

    /// <summary>
    /// Current message status.
    /// </summary>
    public MessageStatus Status { get; set; } = MessageStatus.Pending;

    /// <summary>
    /// Partition key for ordered delivery (optional).
    /// </summary>
    public string? PartitionKey { get; set; }

    /// <summary>
    /// Assigned partition number (set by broker).
    /// </summary>
    public int? Partition { get; set; }

    /// <summary>
    /// Timestamp when message was delivered to consumer (UTC).
    /// </summary>
    public DateTime? DeliveredAt { get; set; }

    /// <summary>
    /// Timestamp when message was acknowledged (UTC).
    /// </summary>
    public DateTime? AcknowledgedAt { get; set; }

    /// <summary>
    /// Acknowledgment deadline (UTC). Message requeued if not acked by this time.
    /// </summary>
    public DateTime? AckDeadline { get; set; }

    /// <summary>
    /// Validates the message for required fields and constraints.
    /// </summary>
    public bool IsValid(out List<string> errors)
    {
        errors = new List<string>();

        if (string.IsNullOrWhiteSpace(MessageId))
            errors.Add("MessageId is required");

        if (string.IsNullOrWhiteSpace(TopicName))
            errors.Add("TopicName is required");

        if (string.IsNullOrWhiteSpace(Payload))
            errors.Add("Payload is required");

        if (string.IsNullOrWhiteSpace(SchemaVersion))
            errors.Add("SchemaVersion is required");

        if (Priority < 0 || Priority > 9)
            errors.Add("Priority must be between 0 and 9");

        if (Payload != null && Payload.Length > 1048576) // 1 MB
            errors.Add("Payload exceeds maximum size of 1 MB");

        return errors.Count == 0;
    }

    /// <summary>
    /// Checks if the message has expired based on TTL.
    /// </summary>
    public bool IsExpired() => ExpiresAt.HasValue && DateTime.UtcNow > ExpiresAt.Value;
}
