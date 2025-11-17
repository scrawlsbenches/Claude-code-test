using System.Diagnostics;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Interfaces;
using HotSwap.Distributed.Orchestrator.Interfaces;
using Microsoft.Extensions.Logging;

namespace HotSwap.Distributed.Orchestrator.Delivery;

/// <summary>
/// Service for exactly-once message delivery using distributed locks and idempotency keys.
/// </summary>
public class ExactlyOnceDeliveryService
{
    private static readonly ActivitySource ActivitySource = new("HotSwap.ExactlyOnceDelivery", "1.0.0");
    private readonly IDistributedLock _distributedLock;
    private readonly IIdempotencyStore _idempotencyStore;
    private readonly IDeliveryService _deliveryService;
    private readonly ILogger<ExactlyOnceDeliveryService> _logger;
    private readonly TimeSpan _lockTimeout;

    public ExactlyOnceDeliveryService(
        IDistributedLock distributedLock,
        IIdempotencyStore idempotencyStore,
        IDeliveryService deliveryService,
        ILogger<ExactlyOnceDeliveryService> logger,
        TimeSpan? lockTimeout = null)
    {
        _distributedLock = distributedLock ?? throw new ArgumentNullException(nameof(distributedLock));
        _idempotencyStore = idempotencyStore ?? throw new ArgumentNullException(nameof(idempotencyStore));
        _deliveryService = deliveryService ?? throw new ArgumentNullException(nameof(deliveryService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _lockTimeout = lockTimeout ?? TimeSpan.FromSeconds(30);
    }

    /// <summary>
    /// Delivers a message exactly once using idempotency keys and distributed locks.
    /// </summary>
    /// <param name="message">The message to deliver.</param>
    /// <param name="deliveryFunc">Function that performs the actual delivery.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Exactly-once delivery result.</returns>
    public async Task<ExactlyOnceDeliveryResult> DeliverExactlyOnceAsync(
        Message message,
        Func<Message, CancellationToken, Task<DeliveryResult>> deliveryFunc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message, nameof(message));
        ArgumentNullException.ThrowIfNull(deliveryFunc, nameof(deliveryFunc));

        using var activity = ActivitySource.StartActivity("DeliverExactlyOnce");
        activity?.SetTag("message_id", message.MessageId);

        var idempotencyKey = GetIdempotencyKey(message);
        activity?.SetTag("idempotency_key", idempotencyKey);

        var lockStartTime = DateTime.UtcNow;

        // Acquire distributed lock for this idempotency key
        ILockHandle? lockHandle = null;
        try
        {
            lockHandle = await _distributedLock.AcquireLockAsync(
                idempotencyKey,
                _lockTimeout,
                cancellationToken);

            if (lockHandle == null)
            {
                _logger.LogWarning(
                    "Could not acquire lock for idempotency key '{IdempotencyKey}' within {Timeout}",
                    idempotencyKey,
                    _lockTimeout);

                activity?.SetStatus(ActivityStatusCode.Error, "Lock timeout");
                return ExactlyOnceDeliveryResult.Failure(
                    message.MessageId,
                    $"Could not acquire lock for idempotency key '{idempotencyKey}' within {_lockTimeout}",
                    idempotencyKey);
            }

            var lockWaitTime = (long)(DateTime.UtcNow - lockStartTime).TotalMilliseconds;
            _logger.LogDebug(
                "Acquired lock for idempotency key '{IdempotencyKey}' in {LockWaitMs}ms",
                idempotencyKey,
                lockWaitTime);

            activity?.SetTag("lock_acquired", true);
            activity?.SetTag("lock_wait_ms", lockWaitTime);

            // Check if this message has already been processed
            var hasBeenProcessed = await _idempotencyStore.HasBeenProcessedAsync(
                idempotencyKey,
                cancellationToken);

            if (hasBeenProcessed)
            {
                _logger.LogInformation(
                    "Message '{MessageId}' with idempotency key '{IdempotencyKey}' has already been processed. Skipping delivery.",
                    message.MessageId,
                    idempotencyKey);

                activity?.SetTag("duplicate", true);
                activity?.SetStatus(ActivityStatusCode.Ok);

                return new ExactlyOnceDeliveryResult
                {
                    IsSuccess = false,
                    IsDuplicate = true,
                    MessageId = message.MessageId,
                    IdempotencyKey = idempotencyKey,
                    LockAcquiredAt = lockHandle.AcquiredAt,
                    LockWaitTimeMs = lockWaitTime
                };
            }

            // Attempt delivery
            DeliveryResult deliveryResult;
            try
            {
                deliveryResult = await deliveryFunc(message, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Exception during delivery of message '{MessageId}' with idempotency key '{IdempotencyKey}'",
                    message.MessageId,
                    idempotencyKey);

                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

                return new ExactlyOnceDeliveryResult
                {
                    IsSuccess = false,
                    IsDuplicate = false,
                    MessageId = message.MessageId,
                    IdempotencyKey = idempotencyKey,
                    ErrorMessage = ex.Message,
                    LockAcquiredAt = lockHandle.AcquiredAt,
                    LockWaitTimeMs = lockWaitTime
                };
            }

            // If delivery was successful, mark as processed
            if (deliveryResult.IsSuccess)
            {
                try
                {
                    await _idempotencyStore.MarkAsProcessedAsync(
                        idempotencyKey,
                        message.MessageId,
                        cancellationToken);

                    _logger.LogInformation(
                        "Message '{MessageId}' with idempotency key '{IdempotencyKey}' delivered successfully and marked as processed",
                        message.MessageId,
                        idempotencyKey);

                    activity?.SetTag("marked_processed", true);
                    activity?.SetStatus(ActivityStatusCode.Ok);

                    return new ExactlyOnceDeliveryResult
                    {
                        IsSuccess = true,
                        IsDuplicate = false,
                        MessageId = message.MessageId,
                        IdempotencyKey = idempotencyKey,
                        ConsumerId = deliveryResult.ConsumerId,
                        LockAcquiredAt = lockHandle.AcquiredAt,
                        LockWaitTimeMs = lockWaitTime
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Failed to mark message '{MessageId}' as processed in idempotency store",
                        message.MessageId);

                    activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

                    return ExactlyOnceDeliveryResult.Failure(
                        message.MessageId,
                        $"Delivery succeeded but failed to mark as processed: {ex.Message}",
                        idempotencyKey);
                }
            }

            // Delivery failed
            _logger.LogWarning(
                "Delivery failed for message '{MessageId}' with idempotency key '{IdempotencyKey}': {Error}",
                message.MessageId,
                idempotencyKey,
                deliveryResult.ErrorMessage);

            activity?.SetStatus(ActivityStatusCode.Error, deliveryResult.ErrorMessage ?? "Delivery failed");

            return new ExactlyOnceDeliveryResult
            {
                IsSuccess = false,
                IsDuplicate = false,
                MessageId = message.MessageId,
                IdempotencyKey = idempotencyKey,
                ErrorMessage = deliveryResult.ErrorMessage,
                LockAcquiredAt = lockHandle.AcquiredAt,
                LockWaitTimeMs = lockWaitTime
            };
        }
        finally
        {
            // Always release the lock
            if (lockHandle != null)
            {
                try
                {
                    await lockHandle.ReleaseAsync();
                    _logger.LogDebug(
                        "Released lock for idempotency key '{IdempotencyKey}'",
                        idempotencyKey);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Failed to release lock for idempotency key '{IdempotencyKey}'",
                        idempotencyKey);
                }
            }
        }
    }

    /// <summary>
    /// Gets the idempotency key from a message.
    /// Uses the "Idempotency-Key" header if present, otherwise falls back to MessageId.
    /// </summary>
    public static string GetIdempotencyKey(Message message)
    {
        ArgumentNullException.ThrowIfNull(message, nameof(message));

        if (message.Headers.TryGetValue("Idempotency-Key", out var key) && !string.IsNullOrWhiteSpace(key))
        {
            return key;
        }

        return message.MessageId;
    }
}
