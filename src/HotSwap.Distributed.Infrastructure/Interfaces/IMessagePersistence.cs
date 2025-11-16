using HotSwap.Distributed.Domain.Models;

namespace HotSwap.Distributed.Infrastructure.Interfaces;

/// <summary>
/// Interface for persistent message storage.
/// Provides durable message storage to survive broker restarts.
/// </summary>
public interface IMessagePersistence
{
    /// <summary>
    /// Stores a message persistently.
    /// </summary>
    /// <param name="message">The message to store.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task representing the asynchronous operation.</returns>
    Task StoreAsync(Message message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a message by its ID.
    /// </summary>
    /// <param name="messageId">The message ID to retrieve.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The message, or null if not found.</returns>
    Task<Message?> RetrieveAsync(string messageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a message by its ID.
    /// </summary>
    /// <param name="messageId">The message ID to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if deleted, false if not found.</returns>
    Task<bool> DeleteAsync(string messageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves messages for a specific topic.
    /// </summary>
    /// <param name="topicName">The topic name.</param>
    /// <param name="limit">Maximum number of messages to retrieve.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of messages for the topic.</returns>
    Task<List<Message>> GetByTopicAsync(string topicName, int limit, CancellationToken cancellationToken = default);
}
