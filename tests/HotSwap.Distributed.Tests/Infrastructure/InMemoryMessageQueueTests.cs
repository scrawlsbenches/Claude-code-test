using FluentAssertions;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Messaging;
using Xunit;

namespace HotSwap.Distributed.Tests.Infrastructure;

public class InMemoryMessageQueueTests
{
    private InMemoryMessageQueue CreateQueue() => new InMemoryMessageQueue();

    private Message CreateTestMessage(string id = null!)
    {
        return new Message
        {
            MessageId = id ?? Guid.NewGuid().ToString(),
            TopicName = "test.topic",
            Payload = "{\"test\":\"data\"}",
            SchemaVersion = "1.0"
        };
    }

    [Fact]
    public async Task EnqueueAsync_WithValidMessage_AddsToQueue()
    {
        // Arrange
        var queue = CreateQueue();
        var message = CreateTestMessage();

        // Act
        await queue.EnqueueAsync(message);

        // Assert
        queue.Count.Should().Be(1);
        queue.IsEmpty.Should().BeFalse();
    }

    [Fact]
    public async Task DequeueAsync_WithMessages_ReturnsMessage()
    {
        // Arrange
        var queue = CreateQueue();
        var message = CreateTestMessage();
        await queue.EnqueueAsync(message);

        // Act
        var result = await queue.DequeueAsync();

        // Assert
        result.Should().NotBeNull();
        result!.MessageId.Should().Be(message.MessageId);
    }

    [Fact]
    public async Task DequeueAsync_FromEmptyQueue_ReturnsNull()
    {
        // Arrange
        var queue = CreateQueue();

        // Act
        var result = await queue.DequeueAsync();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task DequeueAsync_RemovesMessageFromQueue()
    {
        // Arrange
        var queue = CreateQueue();
        var message = CreateTestMessage();
        await queue.EnqueueAsync(message);

        // Act
        await queue.DequeueAsync();

        // Assert
        queue.Count.Should().Be(0);
        queue.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public async Task PeekAsync_WithMessages_ReturnsMessagesWithoutRemoving()
    {
        // Arrange
        var queue = CreateQueue();
        var message1 = CreateTestMessage("msg-1");
        var message2 = CreateTestMessage("msg-2");
        await queue.EnqueueAsync(message1);
        await queue.EnqueueAsync(message2);

        // Act
        var result = await queue.PeekAsync(2);

        // Assert
        result.Should().HaveCount(2);
        queue.Count.Should().Be(2); // Messages still in queue
    }

    [Fact]
    public async Task PeekAsync_FromEmptyQueue_ReturnsEmptyList()
    {
        // Arrange
        var queue = CreateQueue();

        // Act
        var result = await queue.PeekAsync(10);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task PeekAsync_WithLimitLessThanCount_ReturnsLimitedMessages()
    {
        // Arrange
        var queue = CreateQueue();
        for (int i = 0; i < 5; i++)
        {
            await queue.EnqueueAsync(CreateTestMessage($"msg-{i}"));
        }

        // Act
        var result = await queue.PeekAsync(3);

        // Assert
        result.Should().HaveCount(3);
        queue.Count.Should().Be(5); // All messages still in queue
    }

    [Fact]
    public async Task PeekAsync_WithLimitGreaterThanCount_ReturnsAllMessages()
    {
        // Arrange
        var queue = CreateQueue();
        await queue.EnqueueAsync(CreateTestMessage("msg-1"));
        await queue.EnqueueAsync(CreateTestMessage("msg-2"));

        // Act
        var result = await queue.PeekAsync(10);

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task Queue_MaintainsFIFOOrder()
    {
        // Arrange
        var queue = CreateQueue();
        var message1 = CreateTestMessage("first");
        var message2 = CreateTestMessage("second");
        var message3 = CreateTestMessage("third");

        await queue.EnqueueAsync(message1);
        await queue.EnqueueAsync(message2);
        await queue.EnqueueAsync(message3);

        // Act
        var result1 = await queue.DequeueAsync();
        var result2 = await queue.DequeueAsync();
        var result3 = await queue.DequeueAsync();

        // Assert
        result1!.MessageId.Should().Be("first");
        result2!.MessageId.Should().Be("second");
        result3!.MessageId.Should().Be("third");
    }

    [Fact]
    public async Task Count_UpdatesCorrectlyOnEnqueueAndDequeue()
    {
        // Arrange
        var queue = CreateQueue();

        // Act & Assert
        queue.Count.Should().Be(0);

        await queue.EnqueueAsync(CreateTestMessage());
        queue.Count.Should().Be(1);

        await queue.EnqueueAsync(CreateTestMessage());
        queue.Count.Should().Be(2);

        await queue.DequeueAsync();
        queue.Count.Should().Be(1);

        await queue.DequeueAsync();
        queue.Count.Should().Be(0);
    }

    [Fact]
    public void IsEmpty_ReturnsTrueForEmptyQueue()
    {
        // Arrange
        var queue = CreateQueue();

        // Act & Assert
        queue.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public async Task IsEmpty_ReturnsFalseForNonEmptyQueue()
    {
        // Arrange
        var queue = CreateQueue();
        await queue.EnqueueAsync(CreateTestMessage());

        // Act & Assert
        queue.IsEmpty.Should().BeFalse();
    }

    [Fact]
    public async Task ConcurrentEnqueue_ThreadSafe()
    {
        // Arrange
        var queue = CreateQueue();
        var tasks = new List<Task>();

        // Act - Enqueue 100 messages concurrently
        for (int i = 0; i < 100; i++)
        {
            var messageId = i;
            tasks.Add(Task.Run(async () => await queue.EnqueueAsync(CreateTestMessage($"concurrent-{messageId}"))));
        }

        await Task.WhenAll(tasks);

        // Assert
        queue.Count.Should().Be(100);
    }

    [Fact]
    public async Task ConcurrentDequeue_ThreadSafe()
    {
        // Arrange
        var queue = CreateQueue();
        for (int i = 0; i < 100; i++)
        {
            await queue.EnqueueAsync(CreateTestMessage($"msg-{i}"));
        }

        var tasks = new List<Task<Message?>>();

        // Act - Dequeue 100 messages concurrently
        for (int i = 0; i < 100; i++)
        {
            tasks.Add(Task.Run(async () => await queue.DequeueAsync()));
        }

        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().HaveCount(100);
        results.Where(r => r != null).Should().HaveCount(100); // All should succeed
        queue.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public async Task ConcurrentEnqueueAndDequeue_ThreadSafe()
    {
        // Arrange
        var queue = CreateQueue();
        var enqueueCount = 0;
        var dequeueCount = 0;

        // Act - Concurrent enqueue and dequeue
        var enqueueTasks = Enumerable.Range(0, 50).Select(i => Task.Run(async () =>
        {
            await queue.EnqueueAsync(CreateTestMessage($"msg-{i}"));
            Interlocked.Increment(ref enqueueCount);
        }));

        var dequeueTasks = Enumerable.Range(0, 30).Select(i => Task.Run(async () =>
        {
            var msg = await queue.DequeueAsync();
            if (msg != null)
                Interlocked.Increment(ref dequeueCount);
        }));

        await Task.WhenAll(enqueueTasks.Concat(dequeueTasks));

        // Assert
        enqueueCount.Should().Be(50);
        dequeueCount.Should().BeLessOrEqualTo(50);
        queue.Count.Should().Be(enqueueCount - dequeueCount);
    }

    [Fact]
    public async Task PeekAsync_WithZeroLimit_ReturnsEmptyList()
    {
        // Arrange
        var queue = CreateQueue();
        await queue.EnqueueAsync(CreateTestMessage());

        // Act
        var result = await queue.PeekAsync(0);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task PeekAsync_WithNegativeLimit_ReturnsEmptyList()
    {
        // Arrange
        var queue = CreateQueue();
        await queue.EnqueueAsync(CreateTestMessage());

        // Act
        var result = await queue.PeekAsync(-5);

        // Assert
        result.Should().BeEmpty();
    }
}
