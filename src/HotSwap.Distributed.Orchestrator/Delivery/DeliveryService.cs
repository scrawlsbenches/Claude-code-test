using System.Diagnostics;
using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Interfaces;
using Microsoft.Extensions.Logging;

namespace HotSwap.Distributed.Orchestrator.Delivery;

/// <summary>
/// Service for delivering messages with retry logic and dead letter queue support.
/// Implements at-least-once delivery semantics with exponential backoff.
/// </summary>
public class DeliveryService
{
    private static readonly ActivitySource ActivitySource = new("HotSwap.Delivery", "1.0.0");
    private readonly IMessageQueue _messageQueue;
    private readonly DeliveryOptions _options;
    private readonly ILogger<DeliveryService> _logger;

    public DeliveryService(
        IMessageQueue messageQueue,
        DeliveryOptions options,
        ILogger<DeliveryService> logger)
    {
        _messageQueue = messageQueue ?? throw new ArgumentNullException(nameof(messageQueue));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (!_options.IsValid(out var errors))
        {
            throw new ArgumentException($"Invalid delivery options: {string.Join(", ", errors)}");
        }
    }

    /// <summary>
    /// Delivers a message with retry logic and exponential backoff.
    /// </summary>
    /// <param name="message">The message to deliver.</param>
    /// <param name="deliveryFunc">Function that performs the actual delivery.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Delivery result including retry information.</returns>
    public async Task<DeliveryResult> DeliverWithRetryAsync(
        Message message,
        Func<Message, CancellationToken, Task<DeliveryResult>> deliveryFunc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message, nameof(message));
        ArgumentNullException.ThrowIfNull(deliveryFunc, nameof(deliveryFunc));

        using var activity = ActivitySource.StartActivity("DeliverWithRetry");
        activity?.SetTag("message_id", message.MessageId);
        activity?.SetTag("topic_name", message.TopicName);
        activity?.SetTag("max_retries", _options.MaxRetries);

        var initialAttempts = message.DeliveryAttempts;
        var totalDelayMs = 0L;
        var lastError = string.Empty;

        for (int attempt = 0; attempt <= _options.MaxRetries; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            message.DeliveryAttempts = initialAttempts + attempt + 1;

            try
            {
                _logger.LogDebug(
                    "Attempting delivery of message '{MessageId}' (attempt {Attempt}/{MaxAttempts})",
                    message.MessageId,
                    attempt + 1,
                    _options.MaxRetries + 1);

                var result = await deliveryFunc(message, cancellationToken);

                if (result.IsSuccess)
                {
                    _logger.LogInformation(
                        "Message '{MessageId}' delivered successfully after {Attempts} attempt(s)",
                        message.MessageId,
                        attempt + 1);

                    result.DeliveryAttempts = message.DeliveryAttempts;
                    result.TotalDelayMs = totalDelayMs;
                    activity?.SetTag("delivery_attempts", result.DeliveryAttempts);
                    activity?.SetTag("total_delay_ms", totalDelayMs);
                    activity?.SetStatus(ActivityStatusCode.Ok);
                    return result;
                }

                lastError = result.ErrorMessage ?? "Unknown error";
                _logger.LogWarning(
                    "Delivery attempt {Attempt} failed for message '{MessageId}': {Error}",
                    attempt + 1,
                    message.MessageId,
                    lastError);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                lastError = ex.Message;
                _logger.LogWarning(
                    ex,
                    "Delivery attempt {Attempt} threw exception for message '{MessageId}'",
                    attempt + 1,
                    message.MessageId);
            }

            // If not the last attempt, apply exponential backoff
            if (attempt < _options.MaxRetries)
            {
                var backoffMs = CalculateBackoff(attempt);
                totalDelayMs += backoffMs;

                _logger.LogDebug(
                    "Waiting {BackoffMs}ms before retry {NextAttempt}",
                    backoffMs,
                    attempt + 2);

                await Task.Delay(backoffMs, cancellationToken);
            }
        }

        // Max retries exceeded - move to DLQ
        _logger.LogError(
            "Message '{MessageId}' failed after {MaxRetries} retries. Moving to DLQ. Last error: {Error}",
            message.MessageId,
            _options.MaxRetries + 1,
            lastError);

        var movedToDLQ = await MoveToDeadLetterQueueAsync(message, lastError, cancellationToken);

        activity?.SetTag("moved_to_dlq", movedToDLQ);
        activity?.SetTag("delivery_attempts", message.DeliveryAttempts);
        activity?.SetStatus(ActivityStatusCode.Error, lastError);

        return new DeliveryResult
        {
            IsSuccess = false,
            MessageId = message.MessageId,
            ErrorMessage = lastError,
            DeliveryAttempts = message.DeliveryAttempts,
            TotalDelayMs = totalDelayMs,
            MovedToDLQ = movedToDLQ
        };
    }

    /// <summary>
    /// Calculates exponential backoff delay for a given attempt number.
    /// </summary>
    private int CalculateBackoff(int attemptNumber)
    {
        var backoff = _options.InitialBackoffMs * Math.Pow(_options.BackoffMultiplier, attemptNumber);
        var backoffMs = (int)Math.Min(backoff, _options.MaxBackoffMs);
        return backoffMs;
    }

    /// <summary>
    /// Moves a message to the dead letter queue.
    /// </summary>
    private async Task<bool> MoveToDeadLetterQueueAsync(
        Message message,
        string errorReason,
        CancellationToken cancellationToken)
    {
        try
        {
            var dlqTopicName = GetDLQTopicName(message.TopicName);

            // Create a copy of the message for DLQ
            var dlqMessage = new Message
            {
                MessageId = message.MessageId,
                TopicName = dlqTopicName,
                Payload = message.Payload,
                SchemaVersion = message.SchemaVersion,
                Headers = new Dictionary<string, string>(message.Headers)
                {
                    ["X-Original-Topic"] = message.TopicName,
                    ["X-DLQ-Reason"] = errorReason,
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
                "Message '{MessageId}' moved to DLQ topic '{DLQTopic}'",
                message.MessageId,
                dlqTopicName);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to move message '{MessageId}' to DLQ",
                message.MessageId);

            return false;
        }
    }

    /// <summary>
    /// Gets the dead letter queue topic name for a given topic.
    /// </summary>
    public static string GetDLQTopicName(string topicName)
    {
        ArgumentNullException.ThrowIfNull(topicName, nameof(topicName));
        return $"{topicName}.dlq";
    }
}
