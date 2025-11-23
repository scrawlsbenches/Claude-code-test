namespace HotSwap.Distributed.Infrastructure.Data.Entities;

/// <summary>
/// Database entity for message queue using PostgreSQL LISTEN/NOTIFY.
/// Replaces in-memory message queue with durable PostgreSQL storage.
/// </summary>
public class MessageEntity
{
    /// <summary>
    /// Auto-increment primary key.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Unique message ID.
    /// </summary>
    public string MessageId { get; set; } = string.Empty;

    /// <summary>
    /// Message topic/channel.
    /// </summary>
    public string Topic { get; set; } = string.Empty;

    /// <summary>
    /// Serialized JSON payload.
    /// </summary>
    public string Payload { get; set; } = string.Empty;

    /// <summary>
    /// Message priority (higher = more urgent).
    /// </summary>
    public int Priority { get; set; }

    /// <summary>
    /// Message status.
    /// </summary>
    public MessageStatus Status { get; set; }

    /// <summary>
    /// When the message was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When processing started.
    /// </summary>
    public DateTime? ProcessedAt { get; set; }

    /// <summary>
    /// When the message was acknowledged (successfully processed).
    /// </summary>
    public DateTime? AcknowledgedAt { get; set; }

    /// <summary>
    /// Lock expiration time (prevents duplicate processing).
    /// </summary>
    public DateTime? LockedUntil { get; set; }

    /// <summary>
    /// Instance ID of the worker processing this message.
    /// </summary>
    public string? ProcessingInstance { get; set; }

    /// <summary>
    /// Number of retry attempts.
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    /// Error message if processing failed.
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Message status enum.
/// </summary>
public enum MessageStatus
{
    Pending = 0,
    Processing = 1,
    Completed = 2,
    Failed = 3
}
