using System.Collections.Concurrent;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Interfaces;

namespace HotSwap.Distributed.Infrastructure.Messaging;

/// <summary>
/// In-memory implementation of message persistence for testing/fallback when Redis is unavailable.
/// Messages are stored in memory and will be lost on application restart.
/// </summary>
public class InMemoryMessagePersistence : IMessagePersistence
{
    private readonly ConcurrentDictionary<string, Message> _messages = new();
    private readonly ConcurrentDictionary<string, List<Message>> _messagesByTopic = new();
    private readonly object _lock = new();

    public Task StoreAsync(Message message, CancellationToken cancellationToken = default)
    {
        _messages[message.MessageId] = message;

        // Add to topic index
        lock (_lock)
        {
            if (!_messagesByTopic.ContainsKey(message.TopicName))
            {
                _messagesByTopic[message.TopicName] = new List<Message>();
            }
            _messagesByTopic[message.TopicName].Add(message);
        }

        return Task.CompletedTask;
    }

    public Task<Message?> RetrieveAsync(string messageId, CancellationToken cancellationToken = default)
    {
        _messages.TryGetValue(messageId, out var message);
        return Task.FromResult(message);
    }

    public Task<List<Message>> GetByTopicAsync(string topicName, int limit, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (_messagesByTopic.TryGetValue(topicName, out var messages))
            {
                var result = messages
                    .OrderByDescending(m => m.Timestamp)
                    .Take(limit)
                    .ToList();
                return Task.FromResult(result);
            }
        }

        return Task.FromResult(new List<Message>());
    }

    public Task<bool> DeleteAsync(string messageId, CancellationToken cancellationToken = default)
    {
        if (_messages.TryRemove(messageId, out var message))
        {
            // Remove from topic index
            lock (_lock)
            {
                if (_messagesByTopic.TryGetValue(message.TopicName, out var messages))
                {
                    messages.RemoveAll(m => m.MessageId == messageId);
                }
            }
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }
}
