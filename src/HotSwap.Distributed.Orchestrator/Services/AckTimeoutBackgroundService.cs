using System.Diagnostics;
using HotSwap.Distributed.Infrastructure.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HotSwap.Distributed.Orchestrator.Services;

/// <summary>
/// Background service that monitors message acknowledgment deadlines and requeues expired messages.
/// Runs periodically to ensure messages that weren't acknowledged in time are retried.
/// </summary>
public class AckTimeoutBackgroundService : BackgroundService
{
    private static readonly ActivitySource ActivitySource = new("HotSwap.AckTimeout", "1.0.0");
    private readonly IMessageQueue _messageQueue;
    private readonly ILogger<AckTimeoutBackgroundService> _logger;
    private readonly TimeSpan _checkInterval;
    private readonly TimeSpan _ackTimeout;

    public AckTimeoutBackgroundService(
        IMessageQueue messageQueue,
        ILogger<AckTimeoutBackgroundService> logger,
        TimeSpan? checkInterval = null,
        TimeSpan? ackTimeout = null)
    {
        _messageQueue = messageQueue ?? throw new ArgumentNullException(nameof(messageQueue));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _checkInterval = checkInterval ?? TimeSpan.FromSeconds(10);
        _ackTimeout = ackTimeout ?? TimeSpan.FromSeconds(30);
    }

    /// <summary>
    /// Executes the background service, periodically checking for and requeuing expired messages.
    /// </summary>
    /// <param name="stoppingToken">Cancellation token to stop the service.</param>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "AckTimeoutBackgroundService started. Check interval: {CheckInterval}, Ack timeout: {AckTimeout}",
            _checkInterval,
            _ackTimeout);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var activity = ActivitySource.StartActivity("CheckAckTimeouts");

                await CheckAndRequeueExpiredMessagesAsync(stoppingToken);

                activity?.SetStatus(ActivityStatusCode.Ok);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Expected when service is stopping
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error checking for expired ack deadlines. Will retry after {CheckInterval}",
                    _checkInterval);
            }

            try
            {
                await Task.Delay(_checkInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Expected when service is stopping
                break;
            }
        }

        _logger.LogInformation("AckTimeoutBackgroundService stopped");
    }

    /// <summary>
    /// Checks for messages with expired ack deadlines and requeues them.
    /// </summary>
    private async Task CheckAndRequeueExpiredMessagesAsync(CancellationToken cancellationToken)
    {
        List<Domain.Models.Message> messages;

        try
        {
            // Peek messages from the queue (limit to 1000 for performance)
            messages = await _messageQueue.PeekAsync(1000, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to peek messages from queue");
            return;
        }

        if (messages.Count == 0)
        {
            _logger.LogDebug("No messages in queue to check for ack timeouts");
            return;
        }

        // Filter messages with expired ack deadlines
        var now = DateTime.UtcNow;
        var expiredMessages = messages
            .Where(m => m.AckDeadline.HasValue && m.AckDeadline.Value < now)
            .ToList();

        if (expiredMessages.Count == 0)
        {
            _logger.LogDebug(
                "Checked {TotalMessages} messages, found 0 with expired ack deadlines",
                messages.Count);
            return;
        }

        _logger.LogInformation(
            "Found {ExpiredCount} messages with expired ack deadlines out of {TotalMessages} messages",
            expiredMessages.Count,
            messages.Count);

        // Requeue each expired message
        var requeuedCount = 0;
        foreach (var message in expiredMessages)
        {
            try
            {
                await RequeueExpiredMessageAsync(message, cancellationToken);
                requeuedCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to requeue message '{MessageId}' with expired ack deadline",
                    message.MessageId);
            }
        }

        _logger.LogInformation(
            "Successfully requeued {RequeuedCount} out of {ExpiredCount} expired messages",
            requeuedCount,
            expiredMessages.Count);
    }

    /// <summary>
    /// Requeues an expired message with updated delivery attempts and ack deadline.
    /// </summary>
    private async Task RequeueExpiredMessageAsync(
        Domain.Models.Message message,
        CancellationToken cancellationToken)
    {
        // Create requeued message with incremented delivery attempts and new ack deadline
        var requeuedMessage = new Domain.Models.Message
        {
            MessageId = message.MessageId,
            TopicName = message.TopicName,
            Payload = message.Payload,
            SchemaVersion = message.SchemaVersion,
            Headers = new Dictionary<string, string>(message.Headers),
            Priority = message.Priority,
            Timestamp = message.Timestamp,
            ExpiresAt = message.ExpiresAt,
            DeliveryAttempts = message.DeliveryAttempts + 1,
            Status = message.Status,
            AckDeadline = DateTime.UtcNow.Add(_ackTimeout)
        };

        await _messageQueue.EnqueueAsync(requeuedMessage, cancellationToken);

        _logger.LogDebug(
            "Requeued message '{MessageId}' due to ack timeout. Delivery attempts: {DeliveryAttempts}, New ack deadline: {AckDeadline}",
            message.MessageId,
            requeuedMessage.DeliveryAttempts,
            requeuedMessage.AckDeadline);
    }
}
