using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using FluentAssertions;
using HotSwap.Distributed.Api;
using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace HotSwap.Distributed.Tests.Api;

public class MessagesControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly Mock<IMessageQueue> _mockQueue;
    private readonly Mock<IMessagePersistence> _mockPersistence;

    public MessagesControllerTests(WebApplicationFactory<Program> factory)
    {
        _mockQueue = new Mock<IMessageQueue>();
        _mockPersistence = new Mock<IMessagePersistence>();

        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Replace real services with mocks
                var descriptors = services.Where(d =>
                    d.ServiceType == typeof(IMessageQueue) ||
                    d.ServiceType == typeof(IMessagePersistence)).ToList();

                foreach (var descriptor in descriptors)
                {
                    services.Remove(descriptor);
                }

                services.AddSingleton(_mockQueue.Object);
                services.AddSingleton(_mockPersistence.Object);

                // Add test authentication
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = "TestScheme";
                    options.DefaultChallengeScheme = "TestScheme";
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("TestScheme", options => { });
            });
        });
    }

    private HttpClient CreateAuthenticatedClient(Guid? userId = null, List<string>? roles = null)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("TestScheme");
        return client;
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

    [Fact]
    public async Task PublishMessage_WithValidMessage_ReturnsCreated()
    {
        // Arrange
        var client = CreateAuthenticatedClient();
        var message = CreateTestMessage();

        _mockQueue.Setup(x => x.EnqueueAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockPersistence.Setup(x => x.StoreAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var response = await client.PostAsJsonAsync("/api/messages/publish", message);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().Contain($"/api/messages/{message.MessageId}");

        _mockQueue.Verify(x => x.EnqueueAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockPersistence.Verify(x => x.StoreAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PublishMessage_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();
        var message = CreateTestMessage();

        // Act
        var response = await client.PostAsJsonAsync("/api/messages/publish", message);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PublishMessage_WithInvalidMessage_ReturnsBadRequest()
    {
        // Arrange
        var client = CreateAuthenticatedClient();
        var invalidMessage = new Message
        {
            MessageId = "msg-123",
            TopicName = "",  // Invalid: empty topic name
            Payload = "{\"test\":\"data\"}",
            SchemaVersion = "1.0"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/messages/publish", invalidMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetMessage_WithExistingId_ReturnsMessage()
    {
        // Arrange
        var client = CreateAuthenticatedClient();
        var message = CreateTestMessage("msg-123");

        _mockPersistence.Setup(x => x.RetrieveAsync("msg-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);

        // Act
        var response = await client.GetAsync("/api/messages/msg-123");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var retrievedMessage = await response.Content.ReadFromJsonAsync<Message>();
        retrievedMessage.Should().NotBeNull();
        retrievedMessage!.MessageId.Should().Be("msg-123");
    }

    [Fact]
    public async Task GetMessage_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        var client = CreateAuthenticatedClient();

        _mockPersistence.Setup(x => x.RetrieveAsync("non-existent", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Message?)null);

        // Act
        var response = await client.GetAsync("/api/messages/non-existent");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetMessage_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/messages/msg-123");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetMessagesByTopic_WithExistingTopic_ReturnsMessages()
    {
        // Arrange
        var client = CreateAuthenticatedClient();
        var messages = new List<Message>
        {
            CreateTestMessage("msg-1", "orders.created"),
            CreateTestMessage("msg-2", "orders.created")
        };

        _mockPersistence.Setup(x => x.GetByTopicAsync("orders.created", 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(messages);

        // Act
        var response = await client.GetAsync("/api/messages/topic/orders.created?limit=100");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var retrievedMessages = await response.Content.ReadFromJsonAsync<List<Message>>();
        retrievedMessages.Should().NotBeNull();
        retrievedMessages.Should().HaveCount(2);
        retrievedMessages.Should().OnlyContain(m => m.TopicName == "orders.created");
    }

    [Fact]
    public async Task GetMessagesByTopic_WithDefaultLimit_Uses100()
    {
        // Arrange
        var client = CreateAuthenticatedClient();
        var messages = new List<Message> { CreateTestMessage() };

        _mockPersistence.Setup(x => x.GetByTopicAsync("test.topic", 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(messages);

        // Act
        var response = await client.GetAsync("/api/messages/topic/test.topic");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _mockPersistence.Verify(x => x.GetByTopicAsync("test.topic", 100, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetMessagesByTopic_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/messages/topic/test.topic");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AcknowledgeMessage_WithExistingMessage_ReturnsOk()
    {
        // Arrange
        var client = CreateAuthenticatedClient();
        var message = CreateTestMessage("msg-123");

        _mockPersistence.Setup(x => x.RetrieveAsync("msg-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);

        _mockPersistence.Setup(x => x.StoreAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var response = await client.PostAsync("/api/messages/msg-123/acknowledge", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

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
        var client = CreateAuthenticatedClient();

        _mockPersistence.Setup(x => x.RetrieveAsync("non-existent", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Message?)null);

        // Act
        var response = await client.PostAsync("/api/messages/non-existent/acknowledge", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AcknowledgeMessage_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.PostAsync("/api/messages/msg-123/acknowledge", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteMessage_WithExistingMessage_ReturnsNoContent()
    {
        // Arrange
        var client = CreateAuthenticatedClient();

        _mockPersistence.Setup(x => x.DeleteAsync("msg-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var response = await client.DeleteAsync("/api/messages/msg-123");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        _mockPersistence.Verify(x => x.DeleteAsync("msg-123", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteMessage_WithNonExistentMessage_ReturnsNotFound()
    {
        // Arrange
        var client = CreateAuthenticatedClient();

        _mockPersistence.Setup(x => x.DeleteAsync("non-existent", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var response = await client.DeleteAsync("/api/messages/non-existent");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteMessage_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.DeleteAsync("/api/messages/msg-123");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PublishMessage_StoresMessageWithGeneratedId()
    {
        // Arrange
        var client = CreateAuthenticatedClient();
        var message = CreateTestMessage();
        message.MessageId = ""; // Let server generate ID

        Message? storedMessage = null;
        _mockPersistence.Setup(x => x.StoreAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()))
            .Callback<Message, CancellationToken>((m, ct) => storedMessage = m)
            .Returns(Task.CompletedTask);

        // Act
        var response = await client.PostAsJsonAsync("/api/messages/publish", message);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        storedMessage.Should().NotBeNull();
        storedMessage!.MessageId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task PublishMessage_SetsTimestamp()
    {
        // Arrange
        var client = CreateAuthenticatedClient();
        var message = CreateTestMessage();
        var beforePublish = DateTime.UtcNow;

        Message? storedMessage = null;
        _mockPersistence.Setup(x => x.StoreAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()))
            .Callback<Message, CancellationToken>((m, ct) => storedMessage = m)
            .Returns(Task.CompletedTask);

        // Act
        await client.PostAsJsonAsync("/api/messages/publish", message);

        var afterPublish = DateTime.UtcNow;

        // Assert
        storedMessage.Should().NotBeNull();
        storedMessage!.Timestamp.Should().BeOnOrAfter(beforePublish).And.BeOnOrBefore(afterPublish);
    }

    [Fact]
    public async Task GetMessagesByTopic_WithCustomLimit_UsesProvidedLimit()
    {
        // Arrange
        var client = CreateAuthenticatedClient();
        var messages = new List<Message> { CreateTestMessage() };

        _mockPersistence.Setup(x => x.GetByTopicAsync("test.topic", 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(messages);

        // Act
        var response = await client.GetAsync("/api/messages/topic/test.topic?limit=50");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _mockPersistence.Verify(x => x.GetByTopicAsync("test.topic", 50, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetMessagesByTopic_WithExcessiveLimit_CapsAt1000()
    {
        // Arrange
        var client = CreateAuthenticatedClient();
        var messages = new List<Message> { CreateTestMessage() };

        _mockPersistence.Setup(x => x.GetByTopicAsync("test.topic", 1000, It.IsAny<CancellationToken>()))
            .ReturnsAsync(messages);

        // Act
        var response = await client.GetAsync("/api/messages/topic/test.topic?limit=5000");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _mockPersistence.Verify(x => x.GetByTopicAsync("test.topic", 1000, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AcknowledgeMessage_UpdatesStatusAndTimestamp()
    {
        // Arrange
        var client = CreateAuthenticatedClient();
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
        await client.PostAsync("/api/messages/msg-123/acknowledge", null);

        var afterAck = DateTime.UtcNow;

        // Assert
        updatedMessage.Should().NotBeNull();
        updatedMessage!.Status.Should().Be(MessageStatus.Acknowledged);
        updatedMessage.AcknowledgedAt.Should().NotBeNull();
        updatedMessage.AcknowledgedAt!.Value.Should().BeOnOrAfter(beforeAck).And.BeOnOrBefore(afterAck);
    }
}
