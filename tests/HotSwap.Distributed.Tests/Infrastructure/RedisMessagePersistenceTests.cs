using FluentAssertions;
using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Messaging;
using StackExchange.Redis;
using Xunit;

namespace HotSwap.Distributed.Tests.Infrastructure;

public class RedisMessagePersistenceTests : IAsyncLifetime
{
    private IConnectionMultiplexer _redis = null!;
    private RedisMessagePersistence _persistence = null!;
    private readonly string _testKeyPrefix = "test:msg:";
    private bool _redisAvailable;

    public async Task InitializeAsync()
    {
        try
        {
            // Try to connect to Redis (assumes Redis is running on localhost:6379)
            // abortConnect=true (default) makes it fail fast if Redis is unavailable
            _redis = await ConnectionMultiplexer.ConnectAsync("localhost:6379,connectTimeout=1000");

            // Verify connection is actually working
            var db = _redis.GetDatabase();
            await db.PingAsync();

            _persistence = new RedisMessagePersistence(_redis, _testKeyPrefix);
            _redisAvailable = true;
        }
        catch (Exception)
        {
            // Redis is not available - tests will be skipped
            _redisAvailable = false;
        }
    }

    public async Task DisposeAsync()
    {
        if (!_redisAvailable || _redis == null)
        {
            return;
        }

        try
        {
            // Clean up test data
            var db = _redis.GetDatabase();
            var server = _redis.GetServer(_redis.GetEndPoints()[0]);

            await foreach (var key in server.KeysAsync(pattern: $"{_testKeyPrefix}*"))
            {
                await db.KeyDeleteAsync(key);
            }

            await _redis.DisposeAsync();
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    private Message CreateTestMessage(string id, string topicName = "test.topic")
    {
        return new Message
        {
            MessageId = id,
            TopicName = topicName,
            Payload = "{\"test\":\"data\"}",
            SchemaVersion = "1.0",
            Priority = 0,
            Status = MessageStatus.Pending,
            Timestamp = DateTime.UtcNow,
            Headers = new Dictionary<string, string> { { "key", "value" } }
        };
    }

    [SkippableFact]
    public async Task StoreAsync_WithValidMessage_StoresSuccessfully()
    {
        Skip.IfNot(_redisAvailable, "Redis server is not available");

        // Arrange
        var message = CreateTestMessage("msg-1");

        // Act
        await _persistence.StoreAsync(message);

        // Assert
        var retrieved = await _persistence.RetrieveAsync("msg-1");
        retrieved.Should().NotBeNull();
        retrieved!.MessageId.Should().Be("msg-1");
        retrieved.TopicName.Should().Be("test.topic");
        retrieved.Payload.Should().Be("{\"test\":\"data\"}");
    }

    [SkippableFact]
    public async Task StoreAsync_WithNullMessage_ThrowsArgumentNullException()
    {
        Skip.IfNot(_redisAvailable, "Redis server is not available");

        // Arrange
        Message nullMessage = null!;

        // Act
        Func<Task> act = async () => await _persistence.StoreAsync(nullMessage);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [SkippableFact]
    public async Task RetrieveAsync_WithExistingMessage_ReturnsMessage()
    {
        Skip.IfNot(_redisAvailable, "Redis server is not available");

        // Arrange
        var message = CreateTestMessage("msg-2", "test.topic.2");
        await _persistence.StoreAsync(message);

        // Act
        var result = await _persistence.RetrieveAsync("msg-2");

        // Assert
        result.Should().NotBeNull();
        result!.MessageId.Should().Be("msg-2");
        result.TopicName.Should().Be("test.topic.2");
    }

    [SkippableFact]
    public async Task RetrieveAsync_WithNonExistentMessage_ReturnsNull()
    {
        Skip.IfNot(_redisAvailable, "Redis server is not available");

        // Arrange
        var nonExistentId = "non-existent-msg";

        // Act
        var result = await _persistence.RetrieveAsync(nonExistentId);

        // Assert
        result.Should().BeNull();
    }

    [SkippableFact]
    public async Task DeleteAsync_WithExistingMessage_ReturnsTrue()
    {
        Skip.IfNot(_redisAvailable, "Redis server is not available");

        // Arrange
        var message = CreateTestMessage("msg-3");
        await _persistence.StoreAsync(message);

        // Act
        var deleted = await _persistence.DeleteAsync("msg-3");

        // Assert
        deleted.Should().BeTrue();

        // Verify message is gone
        var retrieved = await _persistence.RetrieveAsync("msg-3");
        retrieved.Should().BeNull();
    }

    [SkippableFact]
    public async Task DeleteAsync_WithNonExistentMessage_ReturnsFalse()
    {
        Skip.IfNot(_redisAvailable, "Redis server is not available");

        // Arrange
        var nonExistentId = "non-existent-msg";

        // Act
        var deleted = await _persistence.DeleteAsync(nonExistentId);

        // Assert
        deleted.Should().BeFalse();
    }

    [SkippableFact]
    public async Task GetByTopicAsync_WithMessagesInTopic_ReturnsFilteredMessages()
    {
        Skip.IfNot(_redisAvailable, "Redis server is not available");

        // Arrange
        var msg1 = CreateTestMessage("topic-msg-1", "orders.created");
        var msg2 = CreateTestMessage("topic-msg-2", "orders.created");
        var msg3 = CreateTestMessage("topic-msg-3", "users.created");

        await _persistence.StoreAsync(msg1);
        await _persistence.StoreAsync(msg2);
        await _persistence.StoreAsync(msg3);

        // Act
        var results = await _persistence.GetByTopicAsync("orders.created", 10);

        // Assert
        results.Should().HaveCount(2);
        results.Should().OnlyContain(m => m.TopicName == "orders.created");
    }

    [SkippableFact]
    public async Task GetByTopicAsync_WithLimit_ReturnsLimitedMessages()
    {
        Skip.IfNot(_redisAvailable, "Redis server is not available");

        // Arrange
        for (int i = 0; i < 5; i++)
        {
            var msg = CreateTestMessage($"limit-msg-{i}", "test.limit");
            await _persistence.StoreAsync(msg);
        }

        // Act
        var results = await _persistence.GetByTopicAsync("test.limit", 3);

        // Assert
        results.Should().HaveCount(3);
    }

    [SkippableFact]
    public async Task GetByTopicAsync_WithNoMessages_ReturnsEmptyList()
    {
        Skip.IfNot(_redisAvailable, "Redis server is not available");

        // Arrange
        var emptyTopic = "empty.topic";

        // Act
        var results = await _persistence.GetByTopicAsync(emptyTopic, 10);

        // Assert
        results.Should().BeEmpty();
    }

    [SkippableFact]
    public async Task StoreAsync_PreservesMessageProperties()
    {
        Skip.IfNot(_redisAvailable, "Redis server is not available");

        // Arrange
        var message = new Message
        {
            MessageId = "prop-test",
            TopicName = "test.properties",
            Payload = "{\"complex\":\"data\",\"nested\":{\"value\":123}}",
            SchemaVersion = "2.1",
            Priority = 5,
            Status = MessageStatus.Delivered,
            Timestamp = DateTime.UtcNow,
            DeliveredAt = DateTime.UtcNow.AddSeconds(10),
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            Headers = new Dictionary<string, string>
            {
                { "correlation-id", "abc123" },
                { "source", "api-gateway" }
            }
        };

        // Act
        await _persistence.StoreAsync(message);
        var retrieved = await _persistence.RetrieveAsync("prop-test");

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.MessageId.Should().Be(message.MessageId);
        retrieved.TopicName.Should().Be(message.TopicName);
        retrieved.Payload.Should().Be(message.Payload);
        retrieved.SchemaVersion.Should().Be(message.SchemaVersion);
        retrieved.Priority.Should().Be(message.Priority);
        retrieved.Status.Should().Be(message.Status);
        retrieved.Headers.Should().ContainKey("correlation-id");
        retrieved.Headers["correlation-id"].Should().Be("abc123");
    }

    [SkippableFact]
    public async Task ConcurrentStoreAsync_ThreadSafe()
    {
        Skip.IfNot(_redisAvailable, "Redis server is not available");

        // Arrange
        var tasks = new List<Task>();

        // Act - Store 50 messages concurrently
        for (int i = 0; i < 50; i++)
        {
            var messageId = $"concurrent-{i}";
            var message = CreateTestMessage(messageId, "test.concurrent");
            tasks.Add(Task.Run(async () => await _persistence.StoreAsync(message)));
        }

        await Task.WhenAll(tasks);

        // Assert - All messages should be stored
        var results = await _persistence.GetByTopicAsync("test.concurrent", 100);
        results.Should().HaveCount(50);
    }

    [SkippableFact]
    public async Task StoreAsync_UpdatesExistingMessage()
    {
        Skip.IfNot(_redisAvailable, "Redis server is not available");

        // Arrange
        var message = CreateTestMessage("update-test");
        await _persistence.StoreAsync(message);

        // Act - Update message status
        message.Status = MessageStatus.Acknowledged;
        message.DeliveredAt = DateTime.UtcNow;
        await _persistence.StoreAsync(message);

        // Assert
        var retrieved = await _persistence.RetrieveAsync("update-test");
        retrieved.Should().NotBeNull();
        retrieved!.Status.Should().Be(MessageStatus.Acknowledged);
        retrieved.DeliveredAt.Should().NotBeNull();
    }

    [SkippableFact]
    public async Task GetByTopicAsync_WithZeroLimit_ReturnsEmptyList()
    {
        Skip.IfNot(_redisAvailable, "Redis server is not available");

        // Arrange
        var message = CreateTestMessage("zero-limit", "test.zero");
        await _persistence.StoreAsync(message);

        // Act
        var results = await _persistence.GetByTopicAsync("test.zero", 0);

        // Assert
        results.Should().BeEmpty();
    }

    [SkippableFact]
    public async Task GetByTopicAsync_WithNegativeLimit_ReturnsEmptyList()
    {
        Skip.IfNot(_redisAvailable, "Redis server is not available");

        // Arrange
        var message = CreateTestMessage("neg-limit", "test.negative");
        await _persistence.StoreAsync(message);

        // Act
        var results = await _persistence.GetByTopicAsync("test.negative", -5);

        // Assert
        results.Should().BeEmpty();
    }
}
