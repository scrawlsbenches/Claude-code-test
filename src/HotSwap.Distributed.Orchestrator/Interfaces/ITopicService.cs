using HotSwap.Distributed.Domain.Models;

namespace HotSwap.Distributed.Orchestrator.Interfaces;

/// <summary>
/// Service interface for managing messaging topics.
/// </summary>
public interface ITopicService
{
    /// <summary>
    /// Creates a new topic.
    /// </summary>
    /// <param name="topic">The topic to create.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created topic.</returns>
    /// <exception cref="InvalidOperationException">Thrown when topic already exists.</exception>
    Task<Topic> CreateTopicAsync(Topic topic, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all topics.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of all topics.</returns>
    Task<List<Topic>> ListTopicsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific topic by name.
    /// </summary>
    /// <param name="name">The topic name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The topic if found; otherwise, null.</returns>
    Task<Topic?> GetTopicAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing topic.
    /// </summary>
    /// <param name="name">The topic name.</param>
    /// <param name="topic">The updated topic data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated topic if found; otherwise, null.</returns>
    Task<Topic?> UpdateTopicAsync(string name, Topic topic, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a topic.
    /// </summary>
    /// <param name="name">The topic name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if deleted; false if not found.</returns>
    /// <exception cref="InvalidOperationException">Thrown when topic has active subscriptions.</exception>
    Task<bool> DeleteTopicAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets metrics for a specific topic.
    /// </summary>
    /// <param name="name">The topic name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dictionary of metrics if topic found; otherwise, null.</returns>
    Task<Dictionary<string, object>?> GetTopicMetricsAsync(string name, CancellationToken cancellationToken = default);
}
