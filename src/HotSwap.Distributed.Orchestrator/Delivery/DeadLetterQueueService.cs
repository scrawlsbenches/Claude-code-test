using System.Diagnostics;
using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Interfaces;
using Microsoft.Extensions.Logging;

namespace HotSwap.Distributed.Orchestrator.Delivery;

/// <summary>
/// Service for managing dead letter queue (DLQ) operations.
/// Handles moving failed messages to DLQ, retrieving DLQ messages, and replaying messages.
/// </summary>
public class DeadLetterQueueService
{
    private static readonly ActivitySource ActivitySource = new("HotSwap.DeadLetterQueue", "1.0.0");
    private readonly IMessageQueue _messageQueue;
    private readonly ILogger<DeadLetterQueueService> _logger;

    public DeadLetterQueueService(
        IMessageQueue messageQueue,
        ILogger<DeadLetterQueueService> logger)
    {
        _messageQueue = messageQueue ?? throw new ArgumentNullException(nameof(messageQueue));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Moves a failed message to the dead letter queue.
    /// </summary>
    /// <param name="message">The message that failed delivery.</param>
    /// <param name="errorReason">The reason for the failure.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the message was successfully moved to DLQ; otherwise, false.</returns>
    public async Task<bool> MoveToDeadLetterQueueAsync(
        Message message,
        string errorReason,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message, nameof(message));

        using var activity = ActivitySource.StartActivity("MoveToDeadLetterQueue");
        activity?.SetTag("message_id", message.MessageId);
        activity?.SetTag("original_topic", message.TopicName);

        try
        {
            var dlqTopicName = GetDLQTopicName(message.TopicName);

            // Create a copy of the message for DLQ with metadata
            var dlqMessage = new Message
            {
                MessageId = message.MessageId,
                TopicName = dlqTopicName,
                Payload = message.Payload,
                SchemaVersion = message.SchemaVersion,
                Headers = new Dictionary<string, string>(message.Headers)
                {
                    ["X-Original-Topic"] = message.TopicName,
                    ["X-DLQ-Reason"] = string.IsNullOrWhiteSpace(errorReason) ? "Unknown error" : errorReason,
                    ["X-DLQ-Timestamp"] = DateTime.UtcNow.ToString("O"),
                    ["X-Delivery-Attempts"] = message.DeliveryAttempts.ToString()
                },
                Priority = message.Priority,
                Timestamp = message.Timestamp,
                ExpiresAt = message.ExpiresAt,
                DeliveryAttempts = message.DeliveryAttempts,
                Status = MessageStatus.Failed,
                AckDeadline = null // No ack deadline for DLQ messages
            };

            await _messageQueue.EnqueueAsync(dlqMessage, cancellationToken);

            _logger.LogInformation(
                "Message '{MessageId}' moved to DLQ topic '{DLQTopic}'. Reason: {Reason}",
                message.MessageId,
                dlqTopicName,
                errorReason);

            activity?.SetTag("dlq_topic", dlqTopicName);
            activity?.SetStatus(ActivityStatusCode.Ok);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to move message '{MessageId}' to DLQ",
                message.MessageId);

            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

            return false;
        }
    }

    /// <summary>
    /// Retrieves messages from the dead letter queue for a specific topic.
    /// </summary>
    /// <param name="topicName">The original topic name (not the DLQ topic).</param>
    /// <param name="limit">Maximum number of messages to retrieve.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of DLQ messages for the specified topic.</returns>
    public async Task<List<Message>> GetDeadLetterMessagesAsync(
        string topicName,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(topicName, nameof(topicName));

        if (string.IsNullOrWhiteSpace(topicName))
        {
            throw new ArgumentException("Topic name cannot be empty", nameof(topicName));
        }

        using var activity = ActivitySource.StartActivity("GetDeadLetterMessages");
        activity?.SetTag("topic_name", topicName);
        activity?.SetTag("limit", limit);

        try
        {
            var dlqTopicName = GetDLQTopicName(topicName);

            // Peek messages from the queue
            var allMessages = await _messageQueue.PeekAsync(limit, cancellationToken);

            // Filter to only DLQ messages for this topic
            var dlqMessages = allMessages
                .Where(m => m.TopicName == dlqTopicName)
                .ToList();

            _logger.LogDebug(
                "Retrieved {Count} DLQ messages for topic '{TopicName}'",
                dlqMessages.Count,
                topicName);

            activity?.SetTag("dlq_message_count", dlqMessages.Count);
            activity?.SetStatus(ActivityStatusCode.Ok);

            return dlqMessages;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to retrieve DLQ messages for topic '{TopicName}'",
                topicName);

            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

            throw;
        }
    }

    /// <summary>
    /// Replays a message from the DLQ back to its original topic.
    /// </summary>
    /// <param name="messageId">The ID of the message to replay.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the message was successfully replayed; otherwise, false.</returns>
    public async Task<bool> ReplayFromDLQAsync(
        string messageId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messageId, nameof(messageId));

        using var activity = ActivitySource.StartActivity("ReplayFromDLQ");
        activity?.SetTag("message_id", messageId);

        try
        {
            // Find the message in the DLQ
            var allMessages = await _messageQueue.PeekAsync(1000, cancellationToken);
            var dlqMessage = allMessages.FirstOrDefault(m => m.MessageId == messageId);

            if (dlqMessage == null)
            {
                _logger.LogWarning(
                    "Message '{MessageId}' not found in DLQ for replay",
                    messageId);

                activity?.SetStatus(ActivityStatusCode.Error, "Message not found");
                return false;
            }

            // Check if message has original topic metadata
            if (!dlqMessage.Headers.TryGetValue("X-Original-Topic", out var originalTopic))
            {
                _logger.LogWarning(
                    "Message '{MessageId}' does not have X-Original-Topic header",
                    messageId);

                activity?.SetStatus(ActivityStatusCode.Error, "Missing original topic");
                return false;
            }

            // Create replay message with original topic and clean headers
            var replayMessage = new Message
            {
                MessageId = dlqMessage.MessageId,
                TopicName = originalTopic,
                Payload = dlqMessage.Payload,
                SchemaVersion = dlqMessage.SchemaVersion,
                Headers = new Dictionary<string, string>(
                    dlqMessage.Headers.Where(kvp =>
                        !kvp.Key.StartsWith("X-DLQ-") &&
                        kvp.Key != "X-Original-Topic" &&
                        kvp.Key != "X-Delivery-Attempts")),
                Priority = dlqMessage.Priority,
                Timestamp = dlqMessage.Timestamp,
                ExpiresAt = dlqMessage.ExpiresAt,
                DeliveryAttempts = 0, // Reset delivery attempts
                Status = MessageStatus.Pending, // Reset status to pending
                AckDeadline = null
            };

            // Enqueue the message back to the original topic
            await _messageQueue.EnqueueAsync(replayMessage, cancellationToken);

            _logger.LogInformation(
                "Message '{MessageId}' replayed from DLQ to topic '{OriginalTopic}'",
                messageId,
                originalTopic);

            activity?.SetTag("original_topic", originalTopic);
            activity?.SetStatus(ActivityStatusCode.Ok);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to replay message '{MessageId}' from DLQ",
                messageId);

            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

            return false;
        }
    }

    /// <summary>
    /// Gets the dead letter queue topic name for a given topic.
    /// </summary>
    /// <param name="topicName">The original topic name.</param>
    /// <returns>The DLQ topic name in format "{topicName}.dlq".</returns>
    public static string GetDLQTopicName(string topicName)
    {
        ArgumentNullException.ThrowIfNull(topicName, nameof(topicName));

        if (string.IsNullOrWhiteSpace(topicName))
        {
            throw new ArgumentException("Topic name cannot be empty", nameof(topicName));
        }

        return $"{topicName}.dlq";
    }
}
