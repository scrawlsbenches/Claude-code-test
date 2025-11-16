using Newtonsoft.Json;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Interfaces;
using StackExchange.Redis;

namespace HotSwap.Distributed.Infrastructure.Messaging;

/// <summary>
/// Redis-based message persistence implementation.
/// Provides durable message storage with topic-based indexing.
/// </summary>
public class RedisMessagePersistence : IMessagePersistence
{
    private readonly IConnectionMultiplexer _redis;
    private readonly string _keyPrefix;
    private static readonly JsonSerializerSettings JsonSettings = new()
    {
        NullValueHandling = NullValueHandling.Ignore,
        Formatting = Formatting.None
    };

    /// <summary>
    /// Initializes a new instance of the RedisMessagePersistence class.
    /// </summary>
    /// <param name="redis">Redis connection multiplexer.</param>
    /// <param name="keyPrefix">Prefix for Redis keys (default: "msg:").</param>
    public RedisMessagePersistence(
        IConnectionMultiplexer redis,
        string keyPrefix = "msg:")
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _keyPrefix = keyPrefix ?? throw new ArgumentNullException(nameof(keyPrefix));
    }

    /// <summary>
    /// Stores a message persistently in Redis.
    /// </summary>
    public async Task StoreAsync(Message message, CancellationToken cancellationToken = default)
    {
        if (message == null)
            throw new ArgumentNullException(nameof(message));

        var db = _redis.GetDatabase();
        var messageKey = GetMessageKey(message.MessageId);
        var topicKey = GetTopicKey(message.TopicName);

        // Serialize message to JSON
        var json = JsonConvert.SerializeObject(message, JsonSettings);

        // Store message with 7-day expiration
        var storeTask = db.StringSetAsync(
            messageKey,
            json,
            TimeSpan.FromDays(7));

        // Add message ID to topic index (sorted set by timestamp)
        var score = new DateTimeOffset(message.Timestamp).ToUnixTimeSeconds();
        var indexTask = db.SortedSetAddAsync(
            topicKey,
            message.MessageId,
            score);

        await Task.WhenAll(storeTask, indexTask);
    }

    /// <summary>
    /// Retrieves a message by its ID from Redis.
    /// </summary>
    public async Task<Message?> RetrieveAsync(string messageId, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var messageKey = GetMessageKey(messageId);

        var json = await db.StringGetAsync(messageKey);

        if (json.IsNullOrEmpty)
            return null;

        return JsonConvert.DeserializeObject<Message>(json!);
    }

    /// <summary>
    /// Deletes a message by its ID from Redis.
    /// </summary>
    public async Task<bool> DeleteAsync(string messageId, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var messageKey = GetMessageKey(messageId);

        // First, retrieve the message to get topic name for index cleanup
        var message = await RetrieveAsync(messageId, cancellationToken);

        if (message == null)
            return false;

        var topicKey = GetTopicKey(message.TopicName);

        // Delete message and remove from topic index
        var deleteTask = db.KeyDeleteAsync(messageKey);
        var removeFromIndexTask = db.SortedSetRemoveAsync(topicKey, messageId);

        var results = await Task.WhenAll(deleteTask, removeFromIndexTask);

        return results[0]; // Return result of key deletion
    }

    /// <summary>
    /// Retrieves messages for a specific topic, ordered by publish time.
    /// </summary>
    public async Task<List<Message>> GetByTopicAsync(
        string topicName,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (limit <= 0)
            return new List<Message>();

        var db = _redis.GetDatabase();
        var topicKey = GetTopicKey(topicName);

        // Get message IDs from sorted set (most recent first)
        var messageIds = await db.SortedSetRangeByScoreAsync(
            topicKey,
            order: Order.Descending,
            take: limit);

        if (messageIds.Length == 0)
            return new List<Message>();

        // Retrieve all messages in parallel
        var tasks = messageIds
            .Select(id => RetrieveAsync(id.ToString(), cancellationToken))
            .ToArray();

        var messages = await Task.WhenAll(tasks);

        // Filter out nulls (messages that may have been deleted)
        return messages
            .Where(m => m != null)
            .Cast<Message>()
            .ToList();
    }

    private string GetMessageKey(string messageId) => $"{_keyPrefix}{messageId}";
    private string GetTopicKey(string topicName) => $"{_keyPrefix}topic:{topicName}";
}
