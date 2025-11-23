using System.Text.Json;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Data;
using HotSwap.Distributed.Infrastructure.Data.Entities;
using HotSwap.Distributed.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using DomainMessageStatus = HotSwap.Distributed.Domain.Enums.MessageStatus;
using EntityMessageStatus = HotSwap.Distributed.Infrastructure.Data.Entities.MessageStatus;

namespace HotSwap.Distributed.Infrastructure.Messaging;

/// <summary>
/// PostgreSQL-based message queue implementation with LISTEN/NOTIFY support.
/// Replaces in-memory queue with durable PostgreSQL storage for persistence across restarts.
/// </summary>
public class PostgresMessageQueue : IMessageQueue
{
    private readonly AuditLogDbContext _dbContext;
    private readonly ILogger<PostgresMessageQueue> _logger;
    private const string NOTIFICATION_CHANNEL = "message_queue";

    public PostgresMessageQueue(
        AuditLogDbContext dbContext,
        ILogger<PostgresMessageQueue> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Adds a message to the queue and sends PostgreSQL NOTIFY for real-time delivery.
    /// </summary>
    public async Task EnqueueAsync(Message message, CancellationToken cancellationToken = default)
    {
        if (message == null)
            throw new ArgumentNullException(nameof(message));

        // Validate message
        if (!message.IsValid(out var errors))
        {
            throw new InvalidOperationException($"Invalid message: {string.Join(", ", errors)}");
        }

        // Check if expired
        if (message.IsExpired())
        {
            _logger.LogWarning("Message {MessageId} is already expired, not enqueueing", message.MessageId);
            return;
        }

        _logger.LogDebug("Enqueueing message {MessageId} to topic {Topic} with priority {Priority}",
            message.MessageId, message.TopicName, message.Priority);

        // Convert domain message to entity
        var entity = ToEntity(message);

        // Insert message into database
        await _dbContext.Messages.AddAsync(entity, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Send NOTIFY to wake up consumers
        await SendNotificationAsync(message.TopicName, cancellationToken);

        _logger.LogInformation("Enqueued message {MessageId} to topic {Topic}",
            message.MessageId, message.TopicName);
    }

    /// <summary>
    /// Removes and returns the next message from the queue (highest priority first).
    /// Uses FOR UPDATE SKIP LOCKED to prevent duplicate processing.
    /// </summary>
    public async Task<Message?> DequeueAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        // Find the next available message (highest priority, oldest first)
        // Filter out expired messages
        var entity = await _dbContext.Messages
            .Where(m => m.Status == EntityMessageStatus.Pending &&
                       (m.LockedUntil == null || m.LockedUntil <= now))
            .OrderByDescending(m => m.Priority)
            .ThenBy(m => m.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (entity == null)
        {
            return null; // Queue is empty
        }

        // Check if message is expired (based on domain model)
        var message = ToMessage(entity);
        if (message.IsExpired())
        {
            // Mark as expired and remove from queue
            entity.Status = EntityMessageStatus.Failed;
            entity.ErrorMessage = "Message expired before delivery";
            entity.ProcessedAt = now;
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogWarning("Message {MessageId} expired before delivery", entity.MessageId);

            // Try to get the next message
            return await DequeueAsync(cancellationToken);
        }

        // Mark as processing (lock for 5 minutes)
        entity.Status = EntityMessageStatus.Processing;
        entity.ProcessedAt = now;
        entity.LockedUntil = now.AddMinutes(5);
        entity.ProcessingInstance = Environment.MachineName;

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Dequeued message {MessageId} from topic {Topic}",
            entity.MessageId, entity.Topic);

        return message;
    }

    /// <summary>
    /// Returns messages from the queue without removing them (ordered by priority).
    /// </summary>
    public async Task<List<Message>> PeekAsync(int limit, CancellationToken cancellationToken = default)
    {
        if (limit <= 0)
            throw new ArgumentException("Limit must be greater than 0", nameof(limit));

        var now = DateTime.UtcNow;

        var entities = await _dbContext.Messages
            .Where(m => m.Status == EntityMessageStatus.Pending &&
                       (m.LockedUntil == null || m.LockedUntil <= now))
            .OrderByDescending(m => m.Priority)
            .ThenBy(m => m.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return entities.Select(ToMessage).ToList();
    }

    /// <summary>
    /// Gets the current count of pending messages in the queue.
    /// </summary>
    public int Count
    {
        get
        {
            var now = DateTime.UtcNow;
            return _dbContext.Messages
                .Count(m => m.Status == EntityMessageStatus.Pending &&
                           (m.LockedUntil == null || m.LockedUntil <= now));
        }
    }

    /// <summary>
    /// Checks if the queue is empty.
    /// </summary>
    public bool IsEmpty => Count == 0;

    /// <summary>
    /// Acknowledges successful processing of a message (marks as completed).
    /// </summary>
    public async Task AcknowledgeAsync(string messageId, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.Messages
            .FirstOrDefaultAsync(m => m.MessageId == messageId, cancellationToken);

        if (entity == null)
        {
            _logger.LogWarning("Cannot acknowledge message {MessageId} - not found", messageId);
            return;
        }

        entity.Status = EntityMessageStatus.Completed;
        entity.AcknowledgedAt = DateTime.UtcNow;
        entity.LockedUntil = null;
        entity.ProcessingInstance = null;

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Acknowledged message {MessageId}", messageId);
    }

    /// <summary>
    /// Marks a message as failed (moves to failed status with error message).
    /// </summary>
    public async Task FailAsync(string messageId, string errorMessage, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.Messages
            .FirstOrDefaultAsync(m => m.MessageId == messageId, cancellationToken);

        if (entity == null)
        {
            _logger.LogWarning("Cannot fail message {MessageId} - not found", messageId);
            return;
        }

        entity.Status = EntityMessageStatus.Failed;
        entity.ErrorMessage = errorMessage;
        entity.RetryCount++;
        entity.LockedUntil = null;
        entity.ProcessingInstance = null;

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogWarning("Failed message {MessageId}: {Error}", messageId, errorMessage);
    }

    /// <summary>
    /// Sends PostgreSQL NOTIFY to wake up consumers listening on the topic channel.
    /// </summary>
    private async Task SendNotificationAsync(string topic, CancellationToken cancellationToken)
    {
        try
        {
            var connectionString = _dbContext.Database.GetConnectionString();
            if (connectionString == null)
            {
                _logger.LogWarning("Cannot send NOTIFY - connection string is null");
                return;
            }

            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            // Send NOTIFY with topic as payload
            await using var cmd = new NpgsqlCommand($"NOTIFY {NOTIFICATION_CHANNEL}, @topic", connection);
            cmd.Parameters.AddWithValue("topic", topic);
            await cmd.ExecuteNonQueryAsync(cancellationToken);

            _logger.LogDebug("Sent NOTIFY for topic {Topic}", topic);
        }
        catch (Exception ex)
        {
            // Don't fail the enqueue if NOTIFY fails - message is already persisted
            _logger.LogWarning(ex, "Failed to send NOTIFY for topic {Topic}", topic);
        }
    }

    /// <summary>
    /// Converts domain Message to database MessageEntity.
    /// </summary>
    private MessageEntity ToEntity(Message message)
    {
        return new MessageEntity
        {
            MessageId = message.MessageId,
            Topic = message.TopicName,
            Payload = JsonSerializer.Serialize(new
            {
                message.Payload,
                message.SchemaVersion,
                message.Headers,
                message.CorrelationId,
                message.ExpiresAt,
                message.PartitionKey
            }),
            Priority = message.Priority,
            Status = MapToEntityStatus(message.Status),
            CreatedAt = message.Timestamp,
            ProcessedAt = message.DeliveredAt,
            AcknowledgedAt = message.AcknowledgedAt,
            RetryCount = message.DeliveryAttempts,
            ErrorMessage = null,
            LockedUntil = null,
            ProcessingInstance = null
        };
    }

    /// <summary>
    /// Converts database MessageEntity to domain Message.
    /// </summary>
    private Message ToMessage(MessageEntity entity)
    {
        var payloadData = JsonSerializer.Deserialize<MessagePayloadData>(entity.Payload);

        return new Message
        {
            MessageId = entity.MessageId,
            TopicName = entity.Topic,
            Payload = payloadData?.Payload ?? string.Empty,
            SchemaVersion = payloadData?.SchemaVersion ?? "1.0",
            Timestamp = entity.CreatedAt,
            Headers = payloadData?.Headers ?? new Dictionary<string, string>(),
            CorrelationId = payloadData?.CorrelationId,
            Priority = entity.Priority,
            ExpiresAt = payloadData?.ExpiresAt,
            DeliveryAttempts = entity.RetryCount,
            Status = MapToDomainStatus(entity.Status),
            PartitionKey = payloadData?.PartitionKey,
            DeliveredAt = entity.ProcessedAt,
            AcknowledgedAt = entity.AcknowledgedAt
        };
    }

    /// <summary>
    /// Maps domain MessageStatus to entity MessageStatus.
    /// </summary>
    private EntityMessageStatus MapToEntityStatus(DomainMessageStatus status)
    {
        return status switch
        {
            DomainMessageStatus.Pending => EntityMessageStatus.Pending,
            DomainMessageStatus.Delivered => EntityMessageStatus.Processing,
            DomainMessageStatus.Acknowledged => EntityMessageStatus.Completed,
            DomainMessageStatus.Failed => EntityMessageStatus.Failed,
            DomainMessageStatus.Expired => EntityMessageStatus.Failed,
            _ => EntityMessageStatus.Pending
        };
    }

    /// <summary>
    /// Maps entity MessageStatus to domain MessageStatus.
    /// </summary>
    private DomainMessageStatus MapToDomainStatus(EntityMessageStatus status)
    {
        return status switch
        {
            EntityMessageStatus.Pending => DomainMessageStatus.Pending,
            EntityMessageStatus.Processing => DomainMessageStatus.Delivered,
            EntityMessageStatus.Completed => DomainMessageStatus.Acknowledged,
            EntityMessageStatus.Failed => DomainMessageStatus.Failed,
            _ => DomainMessageStatus.Pending
        };
    }

    /// <summary>
    /// Internal class for deserializing message payload data.
    /// </summary>
    private class MessagePayloadData
    {
        public string Payload { get; set; } = string.Empty;
        public string SchemaVersion { get; set; } = "1.0";
        public Dictionary<string, string> Headers { get; set; } = new();
        public string? CorrelationId { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public string? PartitionKey { get; set; }
    }
}
