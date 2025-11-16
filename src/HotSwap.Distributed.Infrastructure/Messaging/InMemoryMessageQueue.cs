using System.Collections.Concurrent;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Interfaces;

namespace HotSwap.Distributed.Infrastructure.Messaging;

/// <summary>
/// In-memory implementation of IMessageQueue using ConcurrentQueue for thread-safety.
/// Provides FIFO message queue operations suitable for single-broker scenarios.
/// </summary>
public class InMemoryMessageQueue : IMessageQueue
{
    private readonly ConcurrentQueue<Message> _queue;

    /// <summary>
    /// Initializes a new instance of the InMemoryMessageQueue.
    /// </summary>
    public InMemoryMessageQueue()
    {
        _queue = new ConcurrentQueue<Message>();
    }

    /// <inheritdoc />
    public Task EnqueueAsync(Message message, CancellationToken cancellationToken = default)
    {
        if (message == null)
            throw new ArgumentNullException(nameof(message));

        _queue.Enqueue(message);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<Message?> DequeueAsync(CancellationToken cancellationToken = default)
    {
        _queue.TryDequeue(out var message);
        return Task.FromResult(message);
    }

    /// <inheritdoc />
    public Task<List<Message>> PeekAsync(int limit, CancellationToken cancellationToken = default)
    {
        if (limit <= 0)
            return Task.FromResult(new List<Message>());

        var messages = _queue.Take(limit).ToList();
        return Task.FromResult(messages);
    }

    /// <inheritdoc />
    public int Count => _queue.Count;

    /// <inheritdoc />
    public bool IsEmpty => _queue.IsEmpty;
}
