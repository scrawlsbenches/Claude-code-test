namespace HotSwap.Distributed.Infrastructure.Interfaces;

/// <summary>
/// Storage for tracking processed idempotency keys to prevent duplicate message delivery.
/// </summary>
public interface IIdempotencyStore
{
    /// <summary>
    /// Checks if an idempotency key has been processed.
    /// </summary>
    /// <param name="idempotencyKey">The idempotency key to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if already processed, false otherwise.</returns>
    Task<bool> HasBeenProcessedAsync(string idempotencyKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks an idempotency key as processed.
    /// </summary>
    /// <param name="idempotencyKey">The idempotency key to mark.</param>
    /// <param name="messageId">The associated message ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task MarkAsProcessedAsync(string idempotencyKey, string messageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the message ID associated with an idempotency key.
    /// </summary>
    /// <param name="idempotencyKey">The idempotency key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The message ID if found, null otherwise.</returns>
    Task<string?> GetMessageIdAsync(string idempotencyKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes an idempotency key from the store (for testing/cleanup).
    /// </summary>
    /// <param name="idempotencyKey">The idempotency key to remove.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RemoveAsync(string idempotencyKey, CancellationToken cancellationToken = default);
}
