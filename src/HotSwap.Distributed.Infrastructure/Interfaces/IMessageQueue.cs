using HotSwap.Distributed.Domain.Models;

namespace HotSwap.Distributed.Infrastructure.Interfaces;

/// <summary>
/// Interface for message queue operations.
/// Provides thread-safe methods for enqueueing, dequeueing, and peeking messages.
/// </summary>
public interface IMessageQueue
{
    /// <summary>
    /// Adds a message to the queue.
    /// </summary>
    /// <param name="message">The message to enqueue.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task representing the asynchronous operation.</returns>
    Task EnqueueAsync(Message message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes and returns the next message from the queue.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The next message, or null if queue is empty.</returns>
    Task<Message?> DequeueAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns messages from the queue without removing them.
    /// </summary>
    /// <param name="limit">Maximum number of messages to peek.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of messages (up to limit).</returns>
    Task<List<Message>> PeekAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current count of messages in the queue.
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Checks if the queue is empty.
    /// </summary>
    bool IsEmpty { get; }
}
