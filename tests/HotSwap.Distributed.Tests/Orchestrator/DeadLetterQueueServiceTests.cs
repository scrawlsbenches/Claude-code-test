using FluentAssertions;
using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Interfaces;
using HotSwap.Distributed.Orchestrator.Delivery;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace HotSwap.Distributed.Tests.Orchestrator;

public class DeadLetterQueueServiceTests
{
    private readonly Mock<IMessageQueue> _mockQueue;
    private readonly DeadLetterQueueService _service;

    public DeadLetterQueueServiceTests()
    {
        _mockQueue = new Mock<IMessageQueue>();
        _service = new DeadLetterQueueService(
            _mockQueue.Object,
            NullLogger<DeadLetterQueueService>.Instance);
    }

    private Message CreateTestMessage(string messageId, string topic = "test.topic")
    {
        return new Message
        {
            MessageId = messageId,
            TopicName = topic,
            Payload = "test payload",
            SchemaVersion = "1.0",
            Priority = 5,
            DeliveryAttempts = 3,
            Timestamp = DateTime.UtcNow,
            Status = MessageStatus.Pending,
            Headers = new Dictionary<string, string>
            {
                ["X-Custom-Header"] = "custom-value"
            }
        };
    }

    #region MoveToDeadLetterQueueAsync Tests

    [Fact]
    public async Task MoveToDeadLetterQueueAsync_WithValidMessage_MovesMessageToDLQ()
    {
        // Arrange
        var message = CreateTestMessage("msg-1");
        Message? dlqMessage = null;

        _mockQueue.Setup(x => x.EnqueueAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()))
            .Callback<Message, CancellationToken>((msg, ct) => dlqMessage = msg)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.MoveToDeadLetterQueueAsync(message, "Test error");

        // Assert
        result.Should().BeTrue();
        dlqMessage.Should().NotBeNull();
        dlqMessage!.TopicName.Should().Be("test.topic.dlq");
        dlqMessage.MessageId.Should().Be("msg-1");
        dlqMessage.Payload.Should().Be("test payload");
    }

    [Fact]
    public async Task MoveToDeadLetterQueueAsync_PreservesMetadata()
    {
        // Arrange
        var message = CreateTestMessage("msg-2");
        message.DeliveryAttempts = 5;
        Message? dlqMessage = null;

        _mockQueue.Setup(x => x.EnqueueAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()))
            .Callback<Message, CancellationToken>((msg, ct) => dlqMessage = msg)
            .Returns(Task.CompletedTask);

        // Act
        await _service.MoveToDeadLetterQueueAsync(message, "Permanent failure");

        // Assert
        dlqMessage.Should().NotBeNull();
        dlqMessage!.Headers["X-Original-Topic"].Should().Be("test.topic");
        dlqMessage.Headers["X-DLQ-Reason"].Should().Be("Permanent failure");
        dlqMessage.Headers["X-Delivery-Attempts"].Should().Be("5");
        dlqMessage.Headers["X-DLQ-Timestamp"].Should().NotBeNullOrWhiteSpace();
        dlqMessage.Headers["X-Custom-Header"].Should().Be("custom-value");
    }

    [Fact]
    public async Task MoveToDeadLetterQueueAsync_SetsStatusToFailed()
    {
        // Arrange
        var message = CreateTestMessage("msg-3");
        Message? dlqMessage = null;

        _mockQueue.Setup(x => x.EnqueueAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()))
            .Callback<Message, CancellationToken>((msg, ct) => dlqMessage = msg)
            .Returns(Task.CompletedTask);

        // Act
        await _service.MoveToDeadLetterQueueAsync(message, "Error");

        // Assert
        dlqMessage.Should().NotBeNull();
        dlqMessage!.Status.Should().Be(MessageStatus.Failed);
    }

    [Fact]
    public async Task MoveToDeadLetterQueueAsync_ClearsAckDeadline()
    {
        // Arrange
        var message = CreateTestMessage("msg-4");
        message.AckDeadline = DateTime.UtcNow.AddSeconds(30);
        Message? dlqMessage = null;

        _mockQueue.Setup(x => x.EnqueueAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()))
            .Callback<Message, CancellationToken>((msg, ct) => dlqMessage = msg)
            .Returns(Task.CompletedTask);

        // Act
        await _service.MoveToDeadLetterQueueAsync(message, "Error");

        // Assert
        dlqMessage.Should().NotBeNull();
        dlqMessage!.AckDeadline.Should().BeNull();
    }

    [Fact]
    public async Task MoveToDeadLetterQueueAsync_WithQueueFailure_ReturnsFalse()
    {
        // Arrange
        var message = CreateTestMessage("msg-5");

        _mockQueue.Setup(x => x.EnqueueAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Queue unavailable"));

        // Act
        var result = await _service.MoveToDeadLetterQueueAsync(message, "Error");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task MoveToDeadLetterQueueAsync_WithNullMessage_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await _service.MoveToDeadLetterQueueAsync(null!, "Error"));
    }

    [Fact]
    public async Task MoveToDeadLetterQueueAsync_WithEmptyErrorReason_UsesDefaultReason()
    {
        // Arrange
        var message = CreateTestMessage("msg-6");
        Message? dlqMessage = null;

        _mockQueue.Setup(x => x.EnqueueAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()))
            .Callback<Message, CancellationToken>((msg, ct) => dlqMessage = msg)
            .Returns(Task.CompletedTask);

        // Act
        await _service.MoveToDeadLetterQueueAsync(message, "");

        // Assert
        dlqMessage.Should().NotBeNull();
        dlqMessage!.Headers["X-DLQ-Reason"].Should().Be("Unknown error");
    }

    #endregion

    #region GetDeadLetterMessagesAsync Tests

    [Fact]
    public async Task GetDeadLetterMessagesAsync_ReturnsMessagesForTopic()
    {
        // Arrange
        var dlqMessages = new List<Message>
        {
            CreateTestMessage("msg-1", "orders.created.dlq"),
            CreateTestMessage("msg-2", "orders.created.dlq"),
            CreateTestMessage("msg-3", "users.registered.dlq")
        };

        _mockQueue.Setup(x => x.PeekAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dlqMessages);

        // Act
        var result = await _service.GetDeadLetterMessagesAsync("orders.created");

        // Assert
        result.Should().HaveCount(2);
        result.Should().AllSatisfy(m => m.TopicName.Should().Be("orders.created.dlq"));
    }

    [Fact]
    public async Task GetDeadLetterMessagesAsync_WithLimit_ReturnsLimitedResults()
    {
        // Arrange
        var dlqMessages = new List<Message>
        {
            CreateTestMessage("msg-1", "test.topic.dlq"),
            CreateTestMessage("msg-2", "test.topic.dlq"),
            CreateTestMessage("msg-3", "test.topic.dlq")
        };

        _mockQueue.Setup(x => x.PeekAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dlqMessages);

        // Act
        var result = await _service.GetDeadLetterMessagesAsync("test.topic", limit: 10);

        // Assert
        result.Should().HaveCount(3);
        _mockQueue.Verify(x => x.PeekAsync(10, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetDeadLetterMessagesAsync_WithNoMessages_ReturnsEmptyList()
    {
        // Arrange
        _mockQueue.Setup(x => x.PeekAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Message>());

        // Act
        var result = await _service.GetDeadLetterMessagesAsync("test.topic");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetDeadLetterMessagesAsync_WithNullTopic_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await _service.GetDeadLetterMessagesAsync(null!));
    }

    [Fact]
    public async Task GetDeadLetterMessagesAsync_WithEmptyTopic_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            async () => await _service.GetDeadLetterMessagesAsync(""));
    }

    #endregion

    #region ReplayFromDLQAsync Tests

    [Fact]
    public async Task ReplayFromDLQAsync_WithValidMessage_MovesBackToOriginalTopic()
    {
        // Arrange
        var dlqMessage = CreateTestMessage("msg-1", "orders.created.dlq");
        dlqMessage.Headers["X-Original-Topic"] = "orders.created";
        dlqMessage.Headers["X-DLQ-Reason"] = "Temporary network error";
        dlqMessage.Status = MessageStatus.Failed;
        dlqMessage.DeliveryAttempts = 5;

        var allMessages = new List<Message> { dlqMessage };
        Message? replayedMessage = null;

        _mockQueue.Setup(x => x.PeekAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(allMessages);

        _mockQueue.Setup(x => x.EnqueueAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()))
            .Callback<Message, CancellationToken>((msg, ct) => replayedMessage = msg)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.ReplayFromDLQAsync("msg-1");

        // Assert
        result.Should().BeTrue();
        replayedMessage.Should().NotBeNull();
        replayedMessage!.TopicName.Should().Be("orders.created");
        replayedMessage.Status.Should().Be(MessageStatus.Pending);
        replayedMessage.DeliveryAttempts.Should().Be(0);
        replayedMessage.Headers.Should().NotContainKey("X-Original-Topic");
        replayedMessage.Headers.Should().NotContainKey("X-DLQ-Reason");
    }

    [Fact]
    public async Task ReplayFromDLQAsync_WithMessageNotFound_ReturnsFalse()
    {
        // Arrange
        _mockQueue.Setup(x => x.PeekAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Message>());

        // Act
        var result = await _service.ReplayFromDLQAsync("nonexistent-msg");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ReplayFromDLQAsync_WithNullMessageId_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await _service.ReplayFromDLQAsync(null!));
    }

    [Fact]
    public async Task ReplayFromDLQAsync_WithQueueFailure_ReturnsFalse()
    {
        // Arrange
        var dlqMessage = CreateTestMessage("msg-1", "test.topic.dlq");
        dlqMessage.Headers["X-Original-Topic"] = "test.topic";

        _mockQueue.Setup(x => x.PeekAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Message> { dlqMessage });

        _mockQueue.Setup(x => x.EnqueueAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Queue unavailable"));

        // Act
        var result = await _service.ReplayFromDLQAsync("msg-1");

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region GetDLQTopicName Tests

    [Fact]
    public void GetDLQTopicName_ReturnsCorrectFormat()
    {
        // Arrange
        var topicName = "orders.created";

        // Act
        var dlqName = DeadLetterQueueService.GetDLQTopicName(topicName);

        // Assert
        dlqName.Should().Be("orders.created.dlq");
    }

    [Fact]
    public void GetDLQTopicName_WithNullTopic_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => DeadLetterQueueService.GetDLQTopicName(null!));
    }

    [Fact]
    public void GetDLQTopicName_WithEmptyTopic_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => DeadLetterQueueService.GetDLQTopicName(""));
    }

    #endregion
}
