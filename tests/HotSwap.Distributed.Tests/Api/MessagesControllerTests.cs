using FluentAssertions;
using HotSwap.Distributed.Api.Controllers;
using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace HotSwap.Distributed.Tests.Api;

public class MessagesControllerTests
{
    private readonly Mock<IMessageQueue> _mockQueue;
    private readonly Mock<IMessagePersistence> _mockPersistence;
    private readonly MessagesController _controller;

    public MessagesControllerTests()
    {
        _mockQueue = new Mock<IMessageQueue>();
        _mockPersistence = new Mock<IMessagePersistence>();
        _controller = new MessagesController(
            _mockQueue.Object,
            _mockPersistence.Object,
            NullLogger<MessagesController>.Instance);
    }

    private Message CreateTestMessage(string id = "msg-1", string topicName = "test.topic")
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

    #region PublishMessage Tests

    [Fact]
    public async Task PublishMessage_WithValidMessage_ReturnsCreated()
    {
        // Arrange
        var message = CreateTestMessage();

        _mockQueue.Setup(x => x.EnqueueAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockPersistence.Setup(x => x.StoreAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.PublishMessage(message);

        // Assert
        var createdResult = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.ActionName.Should().Be(nameof(MessagesController.GetMessage));
        createdResult.RouteValues!["id"].Should().Be(message.MessageId);
        var returnedMessage = createdResult.Value.Should().BeOfType<Message>().Subject;
        returnedMessage.MessageId.Should().Be(message.MessageId);

        _mockQueue.Verify(x => x.EnqueueAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockPersistence.Verify(x => x.StoreAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PublishMessage_WithInvalidMessage_ReturnsBadRequest()
    {
        // Arrange
        var invalidMessage = new Message
        {
            MessageId = "msg-123",
            TopicName = "",  // Invalid: empty topic name
            Payload = "{\"test\":\"data\"}",
            SchemaVersion = "1.0"
        };

        // Act
        var result = await _controller.PublishMessage(invalidMessage);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task PublishMessage_StoresMessageWithGeneratedId()
    {
        // Arrange
        var message = CreateTestMessage();
        message.MessageId = ""; // Let controller generate ID

        Message? storedMessage = null;
        _mockPersistence.Setup(x => x.StoreAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()))
            .Callback<Message, CancellationToken>((m, ct) => storedMessage = m)
            .Returns(Task.CompletedTask);

        _mockQueue.Setup(x => x.EnqueueAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.PublishMessage(message);

        // Assert
        result.Should().BeOfType<CreatedAtActionResult>();
        storedMessage.Should().NotBeNull();
        storedMessage!.MessageId.Should().NotBeNullOrEmpty();
        Guid.TryParse(storedMessage.MessageId, out _).Should().BeTrue("Generated ID should be a valid GUID");
    }

    [Fact]
    public async Task PublishMessage_SetsTimestamp()
    {
        // Arrange
        var message = CreateTestMessage();
        var beforePublish = DateTime.UtcNow;

        Message? storedMessage = null;
        _mockPersistence.Setup(x => x.StoreAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()))
            .Callback<Message, CancellationToken>((m, ct) => storedMessage = m)
            .Returns(Task.CompletedTask);

        _mockQueue.Setup(x => x.EnqueueAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _controller.PublishMessage(message);

        var afterPublish = DateTime.UtcNow;

        // Assert
        storedMessage.Should().NotBeNull();
        storedMessage!.Timestamp.Should().BeOnOrAfter(beforePublish).And.BeOnOrBefore(afterPublish);
    }

    #endregion

    #region GetMessage Tests

    [Fact]
    public async Task GetMessage_WithExistingId_ReturnsMessage()
    {
        // Arrange
        var message = CreateTestMessage("msg-123");

        _mockPersistence.Setup(x => x.RetrieveAsync("msg-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);

        // Act
        var result = await _controller.GetMessage("msg-123");

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var retrievedMessage = okResult.Value.Should().BeOfType<Message>().Subject;
        retrievedMessage.MessageId.Should().Be("msg-123");
    }

    [Fact]
    public async Task GetMessage_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        _mockPersistence.Setup(x => x.RetrieveAsync("non-existent", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Message?)null);

        // Act
        var result = await _controller.GetMessage("non-existent");

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    #endregion

    #region GetMessagesByTopic Tests

    [Fact]
    public async Task GetMessagesByTopic_WithExistingTopic_ReturnsMessages()
    {
        // Arrange
        var messages = new List<Message>
        {
            CreateTestMessage("msg-1", "orders.created"),
            CreateTestMessage("msg-2", "orders.created")
        };

        _mockPersistence.Setup(x => x.GetByTopicAsync("orders.created", 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(messages);

        // Act
        var result = await _controller.GetMessagesByTopic("orders.created", 100);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var retrievedMessages = okResult.Value.Should().BeOfType<List<Message>>().Subject;
        retrievedMessages.Should().HaveCount(2);
        retrievedMessages.Should().OnlyContain(m => m.TopicName == "orders.created");
    }

    [Fact]
    public async Task GetMessagesByTopic_WithDefaultLimit_Uses100()
    {
        // Arrange
        var messages = new List<Message> { CreateTestMessage() };

        _mockPersistence.Setup(x => x.GetByTopicAsync("test.topic", 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(messages);

        // Act
        var result = await _controller.GetMessagesByTopic("test.topic");

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockPersistence.Verify(x => x.GetByTopicAsync("test.topic", 100, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetMessagesByTopic_WithCustomLimit_UsesProvidedLimit()
    {
        // Arrange
        var messages = new List<Message> { CreateTestMessage() };

        _mockPersistence.Setup(x => x.GetByTopicAsync("test.topic", 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(messages);

        // Act
        var result = await _controller.GetMessagesByTopic("test.topic", 50);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockPersistence.Verify(x => x.GetByTopicAsync("test.topic", 50, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetMessagesByTopic_WithExcessiveLimit_CapsAt1000()
    {
        // Arrange
        var messages = new List<Message> { CreateTestMessage() };

        _mockPersistence.Setup(x => x.GetByTopicAsync("test.topic", 1000, It.IsAny<CancellationToken>()))
            .ReturnsAsync(messages);

        // Act
        var result = await _controller.GetMessagesByTopic("test.topic", 5000);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockPersistence.Verify(x => x.GetByTopicAsync("test.topic", 1000, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region AcknowledgeMessage Tests

    [Fact]
    public async Task AcknowledgeMessage_WithExistingMessage_ReturnsOk()
    {
        // Arrange
        var message = CreateTestMessage("msg-123");

        _mockPersistence.Setup(x => x.RetrieveAsync("msg-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);

        _mockPersistence.Setup(x => x.StoreAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.AcknowledgeMessage("msg-123");

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var acknowledgedMessage = okResult.Value.Should().BeOfType<Message>().Subject;
        acknowledgedMessage.MessageId.Should().Be("msg-123");
        acknowledgedMessage.Status.Should().Be(MessageStatus.Acknowledged);
        acknowledgedMessage.AcknowledgedAt.Should().NotBeNull();

        _mockPersistence.Verify(x => x.StoreAsync(
            It.Is<Message>(m =>
                m.MessageId == "msg-123" &&
                m.Status == MessageStatus.Acknowledged &&
                m.AcknowledgedAt != null),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AcknowledgeMessage_WithNonExistentMessage_ReturnsNotFound()
    {
        // Arrange
        _mockPersistence.Setup(x => x.RetrieveAsync("non-existent", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Message?)null);

        // Act
        var result = await _controller.AcknowledgeMessage("non-existent");

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task AcknowledgeMessage_UpdatesStatusAndTimestamp()
    {
        // Arrange
        var message = CreateTestMessage("msg-123");
        message.Status = MessageStatus.Delivered;
        var beforeAck = DateTime.UtcNow;

        _mockPersistence.Setup(x => x.RetrieveAsync("msg-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);

        Message? updatedMessage = null;
        _mockPersistence.Setup(x => x.StoreAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()))
            .Callback<Message, CancellationToken>((m, ct) => updatedMessage = m)
            .Returns(Task.CompletedTask);

        // Act
        await _controller.AcknowledgeMessage("msg-123");

        var afterAck = DateTime.UtcNow;

        // Assert
        updatedMessage.Should().NotBeNull();
        updatedMessage!.Status.Should().Be(MessageStatus.Acknowledged);
        updatedMessage.AcknowledgedAt.Should().NotBeNull();
        updatedMessage.AcknowledgedAt!.Value.Should().BeOnOrAfter(beforeAck).And.BeOnOrBefore(afterAck);
    }

    #endregion

    #region DeleteMessage Tests

    [Fact]
    public async Task DeleteMessage_WithExistingMessage_ReturnsNoContent()
    {
        // Arrange
        _mockPersistence.Setup(x => x.DeleteAsync("msg-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.DeleteMessage("msg-123");

        // Assert
        result.Should().BeOfType<NoContentResult>();
        _mockPersistence.Verify(x => x.DeleteAsync("msg-123", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteMessage_WithNonExistentMessage_ReturnsNotFound()
    {
        // Arrange
        _mockPersistence.Setup(x => x.DeleteAsync("non-existent", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.DeleteMessage("non-existent");

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    #endregion
}
