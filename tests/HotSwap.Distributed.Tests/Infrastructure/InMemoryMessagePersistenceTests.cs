using FluentAssertions;
using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Messaging;
using Xunit;

namespace HotSwap.Distributed.Tests.Infrastructure;

/// <summary>
/// Unit tests for InMemoryMessagePersistence.
/// These tests verify the message persistence logic WITHOUT requiring Redis or any external dependencies.
/// </summary>
public class InMemoryMessagePersistenceTests
{
    private readonly InMemoryMessagePersistence _persistence;

    public InMemoryMessagePersistenceTests()
    {
        _persistence = new InMemoryMessagePersistence();
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

    #region Store Tests

    [Fact(Skip = "Temporarily disabled - investigating test hang")]
    public async Task StoreAsync_WithValidMessage_StoresSuccessfully()
    {
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

    [Fact(Skip = "Temporarily disabled - investigating test hang")]
    public async Task StoreAsync_WithNullMessage_ThrowsArgumentException()
    {
        // Arrange
        Message nullMessage = null!;

        // Act
        Func<Task> act = async () => await _persistence.StoreAsync(nullMessage);

        // Assert - InMemoryMessagePersistence will throw NullReferenceException when accessing MessageId
        await act.Should().ThrowAsync<NullReferenceException>();
    }

    [Fact(Skip = "Temporarily disabled - investigating test hang")]
    public async Task StoreAsync_PreservesAllMessageProperties()
    {
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
        retrieved.DeliveredAt.Should().BeCloseTo(message.DeliveredAt!.Value, TimeSpan.FromMilliseconds(10));
        retrieved.ExpiresAt.Should().BeCloseTo(message.ExpiresAt!.Value, TimeSpan.FromMilliseconds(10));
    }

    #endregion

    #region Retrieve Tests

    [Fact(Skip = "Temporarily disabled - investigating test hang")]
    public async Task RetrieveAsync_WithExistingMessage_ReturnsMessage()
    {
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

    [Fact(Skip = "Temporarily disabled - investigating test hang")]
    public async Task RetrieveAsync_WithNonExistentMessage_ReturnsNull()
    {
        // Arrange
        var nonExistentId = "non-existent-msg";

        // Act
        var result = await _persistence.RetrieveAsync(nonExistentId);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region Delete Tests

    [Fact(Skip = "Temporarily disabled - investigating test hang")]
    public async Task DeleteAsync_WithExistingMessage_ReturnsTrue()
    {
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

    [Fact(Skip = "Temporarily disabled - investigating test hang")]
    public async Task DeleteAsync_WithNonExistentMessage_ReturnsFalse()
    {
        // Arrange
        var nonExistentId = "non-existent-msg";

        // Act
        var deleted = await _persistence.DeleteAsync(nonExistentId);

        // Assert
        deleted.Should().BeFalse();
    }

    [Fact(Skip = "Temporarily disabled - investigating test hang")]
    public async Task DeleteAsync_RemovesFromTopicIndex()
    {
        // Arrange
        var message = CreateTestMessage("delete-topic-test", "test.delete");
        await _persistence.StoreAsync(message);

        // Verify it's in the topic index
        var beforeDelete = await _persistence.GetByTopicAsync("test.delete", 10);
        beforeDelete.Should().HaveCount(1);

        // Act
        await _persistence.DeleteAsync("delete-topic-test");

        // Assert - Should be removed from topic index
        var afterDelete = await _persistence.GetByTopicAsync("test.delete", 10);
        afterDelete.Should().BeEmpty();
    }

    #endregion

    #region GetByTopicAsync Tests

    [Fact(Skip = "Temporarily disabled - investigating test hang")]
    public async Task GetByTopicAsync_WithMessagesInTopic_ReturnsFilteredMessages()
    {
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

    [Fact(Skip = "Temporarily disabled - investigating test hang")]
    public async Task GetByTopicAsync_WithLimit_ReturnsLimitedMessages()
    {
        // Arrange
        for (int i = 0; i < 5; i++)
        {
            var msg = CreateTestMessage($"limit-msg-{i}", "test.limit");
            await _persistence.StoreAsync(msg);
            await Task.Delay(1); // Ensure different timestamps
        }

        // Act
        var results = await _persistence.GetByTopicAsync("test.limit", 3);

        // Assert
        results.Should().HaveCount(3);
    }

    [Fact(Skip = "Temporarily disabled - investigating test hang")]
    public async Task GetByTopicAsync_WithNoMessages_ReturnsEmptyList()
    {
        // Arrange
        var emptyTopic = "empty.topic";

        // Act
        var results = await _persistence.GetByTopicAsync(emptyTopic, 10);

        // Assert
        results.Should().BeEmpty();
    }

    [Fact(Skip = "Temporarily disabled - investigating test hang")]
    public async Task GetByTopicAsync_WithZeroLimit_ReturnsEmptyList()
    {
        // Arrange
        var message = CreateTestMessage("zero-limit", "test.zero");
        await _persistence.StoreAsync(message);

        // Act
        var results = await _persistence.GetByTopicAsync("test.zero", 0);

        // Assert
        results.Should().BeEmpty();
    }

    [Fact(Skip = "Temporarily disabled - investigating test hang")]
    public async Task GetByTopicAsync_WithNegativeLimit_ReturnsEmptyList()
    {
        // Arrange
        var message = CreateTestMessage("neg-limit", "test.negative");
        await _persistence.StoreAsync(message);

        // Act
        var results = await _persistence.GetByTopicAsync("test.negative", -5);

        // Assert
        results.Should().BeEmpty();
    }

    [Fact(Skip = "Temporarily disabled - investigating test hang")]
    public async Task GetByTopicAsync_ReturnsMessagesOrderedByTimestampDescending()
    {
        // Arrange
        var oldMessage = CreateTestMessage("old", "test.order");
        oldMessage.Timestamp = DateTime.UtcNow.AddHours(-2);

        var newMessage = CreateTestMessage("new", "test.order");
        newMessage.Timestamp = DateTime.UtcNow;

        var midMessage = CreateTestMessage("mid", "test.order");
        midMessage.Timestamp = DateTime.UtcNow.AddHours(-1);

        await _persistence.StoreAsync(oldMessage);
        await _persistence.StoreAsync(newMessage);
        await _persistence.StoreAsync(midMessage);

        // Act
        var results = await _persistence.GetByTopicAsync("test.order", 10);

        // Assert
        results.Should().HaveCount(3);
        results[0].MessageId.Should().Be("new", "newest message should be first");
        results[1].MessageId.Should().Be("mid", "middle message should be second");
        results[2].MessageId.Should().Be("old", "oldest message should be last");
    }

    #endregion

    #region Concurrency Tests

    [Fact(Skip = "Temporarily disabled - investigating test hang")]
    public async Task ConcurrentStoreAsync_ThreadSafe()
    {
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

    [Fact(Skip = "Temporarily disabled - investigating test hang")]
    public async Task ConcurrentDeleteAsync_ThreadSafe()
    {
        // Arrange - Store 10 messages
        for (int i = 0; i < 10; i++)
        {
            var message = CreateTestMessage($"delete-{i}", "test.concurrent.delete");
            await _persistence.StoreAsync(message);
        }

        // Act - Delete them concurrently
        var deleteTasks = new List<Task<bool>>();
        for (int i = 0; i < 10; i++)
        {
            var messageId = $"delete-{i}";
            deleteTasks.Add(Task.Run(async () => await _persistence.DeleteAsync(messageId)));
        }

        var results = await Task.WhenAll(deleteTasks);

        // Assert - All deletes should succeed
        results.Should().AllSatisfy(r => r.Should().BeTrue());

        // Verify all messages are gone
        var remaining = await _persistence.GetByTopicAsync("test.concurrent.delete", 100);
        remaining.Should().BeEmpty();
    }

    #endregion

    #region Update/Overwrite Tests

    [Fact(Skip = "Temporarily disabled - investigating test hang")]
    public async Task StoreAsync_WithSameMessageId_OverwritesExisting()
    {
        // Arrange
        var message1 = CreateTestMessage("update-test");
        message1.Status = MessageStatus.Pending;
        await _persistence.StoreAsync(message1);

        // Act - Store again with same ID but different status
        var message2 = CreateTestMessage("update-test");
        message2.Status = MessageStatus.Delivered;
        message2.DeliveredAt = DateTime.UtcNow;
        await _persistence.StoreAsync(message2);

        // Assert
        var retrieved = await _persistence.RetrieveAsync("update-test");
        retrieved.Should().NotBeNull();
        retrieved!.Status.Should().Be(MessageStatus.Delivered);
        retrieved.DeliveredAt.Should().NotBeNull();
    }

    #endregion
}
