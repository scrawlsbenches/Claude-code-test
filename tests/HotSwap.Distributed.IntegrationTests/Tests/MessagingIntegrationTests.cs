using FluentAssertions;
using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.IntegrationTests.Fixtures;
using HotSwap.Distributed.IntegrationTests.Helpers;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace HotSwap.Distributed.IntegrationTests.Tests;

/// <summary>
/// Integration tests for messaging system.
/// Tests message publishing, retrieval, acknowledgment, and deletion workflows.
/// </summary>
[Collection("IntegrationTests")]
public class MessagingIntegrationTests : IClassFixture<PostgreSqlContainerFixture>, IClassFixture<RedisContainerFixture>, IAsyncLifetime
{
    private readonly PostgreSqlContainerFixture _postgreSqlFixture;
    private readonly RedisContainerFixture _redisFixture;
    private IntegrationTestFactory? _factory;
    private HttpClient? _client;
    private AuthHelper? _authHelper;

    public MessagingIntegrationTests(
        PostgreSqlContainerFixture postgreSqlFixture,
        RedisContainerFixture redisFixture)
    {
        _postgreSqlFixture = postgreSqlFixture ?? throw new ArgumentNullException(nameof(postgreSqlFixture));
        _redisFixture = redisFixture ?? throw new ArgumentNullException(nameof(redisFixture));
    }

    public async Task InitializeAsync()
    {
        // Create factory and client for each test
        _factory = new IntegrationTestFactory(_postgreSqlFixture, _redisFixture);
        await _factory.InitializeAsync();

        _client = _factory.CreateClient();
        _authHelper = new AuthHelper(_client);

        // Authenticate with deployer role (has access to messaging)
        var token = await _authHelper.GetDeployerTokenAsync();
        _authHelper.AddAuthorizationHeader(_client, token);
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        if (_factory != null)
        {
            await _factory.DisposeAsync();
        }
    }

    #region Message Publishing Tests

    /// <summary>
    /// Tests publishing a valid message to a topic.
    /// </summary>
    [Fact]
    public async Task PublishMessage_WithValidPayload_ReturnsCreatedMessage()
    {
        // Arrange
        var message = new Message
        {
            MessageId = Guid.NewGuid().ToString(),
            TopicName = "test.integration.topic",
            Payload = JsonSerializer.Serialize(new { TestData = "Hello World", Timestamp = DateTime.UtcNow }),
            SchemaVersion = "1.0",
            Priority = 5
        };

        // Act
        var response = await _client!.PostAsJsonAsync("/api/messages/publish", message);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created, "Valid message should be published successfully");

        var createdMessage = await response.Content.ReadFromJsonAsync<Message>();
        createdMessage.Should().NotBeNull();
        createdMessage!.MessageId.Should().Be(message.MessageId);
        createdMessage.TopicName.Should().Be("test.integration.topic");
        createdMessage.Status.Should().Be(MessageStatus.Pending);
        createdMessage.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    /// <summary>
    /// Tests publishing a message without message ID generates one automatically.
    /// </summary>
    [Fact]
    public async Task PublishMessage_WithoutMessageId_GeneratesIdAutomatically()
    {
        // Arrange
        var message = new Message
        {
            MessageId = "", // Empty ID
            TopicName = "test.autoid.topic",
            Payload = JsonSerializer.Serialize(new { Data = "Auto ID test" }),
            SchemaVersion = "1.0"
        };

        // Act
        var response = await _client!.PostAsJsonAsync("/api/messages/publish", message);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var createdMessage = await response.Content.ReadFromJsonAsync<Message>();
        createdMessage.Should().NotBeNull();
        createdMessage!.MessageId.Should().NotBeNullOrWhiteSpace("System should generate message ID");
        Guid.TryParse(createdMessage.MessageId, out _).Should().BeTrue("Generated ID should be a valid GUID");
    }

    /// <summary>
    /// Tests publishing an invalid message (missing required fields) returns 400.
    /// </summary>
    [Fact]
    public async Task PublishMessage_WithMissingTopicName_ReturnsBadRequest()
    {
        // Arrange
        var message = new Message
        {
            MessageId = Guid.NewGuid().ToString(),
            TopicName = "", // Missing topic name
            Payload = JsonSerializer.Serialize(new { Data = "Invalid" }),
            SchemaVersion = "1.0"
        };

        // Act
        var response = await _client!.PostAsJsonAsync("/api/messages/publish", message);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "Message without topic name should be rejected");
    }

    /// <summary>
    /// Tests publishing messages with different priorities.
    /// </summary>
    [Fact]
    public async Task PublishMessages_WithDifferentPriorities_AllSucceed()
    {
        // Arrange & Act
        var priorities = new[] { 0, 5, 9 };
        var publishedMessages = new List<Message>();

        foreach (var priority in priorities)
        {
            var message = new Message
            {
                MessageId = Guid.NewGuid().ToString(),
                TopicName = "test.priority.topic",
                Payload = JsonSerializer.Serialize(new { Priority = priority }),
                SchemaVersion = "1.0",
                Priority = priority
            };

            var response = await _client!.PostAsJsonAsync("/api/messages/publish", message);
            response.StatusCode.Should().Be(HttpStatusCode.Created);

            var published = await response.Content.ReadFromJsonAsync<Message>();
            publishedMessages.Add(published!);
        }

        // Assert
        publishedMessages.Should().HaveCount(3);
        publishedMessages[0].Priority.Should().Be(0);
        publishedMessages[1].Priority.Should().Be(5);
        publishedMessages[2].Priority.Should().Be(9);
    }

    #endregion

    #region Message Retrieval Tests

    /// <summary>
    /// Tests retrieving a message by its ID.
    /// </summary>
    [Fact]
    public async Task GetMessage_ByValidId_ReturnsMessage()
    {
        // Arrange - Publish a message first
        var originalMessage = new Message
        {
            MessageId = Guid.NewGuid().ToString(),
            TopicName = "test.retrieval.topic",
            Payload = JsonSerializer.Serialize(new { Data = "Test retrieval" }),
            SchemaVersion = "1.0"
        };

        await _client!.PostAsJsonAsync("/api/messages/publish", originalMessage);

        // Act - Retrieve the message
        var response = await _client!.GetAsync($"/api/messages/{originalMessage.MessageId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var retrievedMessage = await response.Content.ReadFromJsonAsync<Message>();
        retrievedMessage.Should().NotBeNull();
        retrievedMessage!.MessageId.Should().Be(originalMessage.MessageId);
        retrievedMessage.TopicName.Should().Be("test.retrieval.topic");
        retrievedMessage.Payload.Should().Be(originalMessage.Payload);
    }

    /// <summary>
    /// Tests retrieving a non-existent message returns 404.
    /// </summary>
    [Fact]
    public async Task GetMessage_WithNonExistentId_Returns404NotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid().ToString();

        // Act
        var response = await _client!.GetAsync($"/api/messages/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Tests retrieving messages by topic.
    /// </summary>
    [Fact]
    public async Task GetMessagesByTopic_ReturnsAllMessagesForTopic()
    {
        // Arrange - Publish multiple messages to the same topic
        var topicName = "test.topic.retrieval." + Guid.NewGuid().ToString();
        var messageIds = new List<string>();

        for (int i = 0; i < 3; i++)
        {
            var message = new Message
            {
                MessageId = Guid.NewGuid().ToString(),
                TopicName = topicName,
                Payload = JsonSerializer.Serialize(new { MessageNumber = i }),
                SchemaVersion = "1.0"
            };

            await _client!.PostAsJsonAsync("/api/messages/publish", message);
            messageIds.Add(message.MessageId);
        }

        // Give messages time to be persisted
        await Task.Delay(TimeSpan.FromSeconds(1));

        // Act - Retrieve messages by topic
        var response = await _client!.GetAsync($"/api/messages/topic/{topicName}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var messages = await response.Content.ReadFromJsonAsync<List<Message>>();
        messages.Should().NotBeNull();
        messages!.Should().HaveCountGreaterOrEqualTo(3, "At least 3 messages should be retrieved");
        messages.Should().OnlyContain(m => m.TopicName == topicName);
    }

    #endregion

    #region Message Acknowledgment Tests

    /// <summary>
    /// Tests acknowledging a message marks it as acknowledged.
    /// </summary>
    [Fact]
    public async Task AcknowledgeMessage_MarksMessageAsAcknowledged()
    {
        // Arrange - Publish a message
        var message = new Message
        {
            MessageId = Guid.NewGuid().ToString(),
            TopicName = "test.ack.topic",
            Payload = JsonSerializer.Serialize(new { Data = "Test acknowledgment" }),
            SchemaVersion = "1.0"
        };

        await _client!.PostAsJsonAsync("/api/messages/publish", message);

        // Act - Acknowledge the message
        var response = await _client!.PostAsync($"/api/messages/{message.MessageId}/acknowledge", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var acknowledgedMessage = await response.Content.ReadFromJsonAsync<Message>();
        acknowledgedMessage.Should().NotBeNull();
        acknowledgedMessage!.Status.Should().Be(MessageStatus.Acknowledged);
        acknowledgedMessage.AcknowledgedAt.Should().NotBeNull();
        acknowledgedMessage.AcknowledgedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    /// <summary>
    /// Tests acknowledging a non-existent message returns 404.
    /// </summary>
    [Fact]
    public async Task AcknowledgeMessage_WithNonExistentId_Returns404NotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid().ToString();

        // Act
        var response = await _client!.PostAsync($"/api/messages/{nonExistentId}/acknowledge", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region Message Deletion Tests

    /// <summary>
    /// Tests deleting a message removes it from the system.
    /// </summary>
    [Fact]
    public async Task DeleteMessage_RemovesMessageFromSystem()
    {
        // Arrange - Publish a message
        var message = new Message
        {
            MessageId = Guid.NewGuid().ToString(),
            TopicName = "test.delete.topic",
            Payload = JsonSerializer.Serialize(new { Data = "Test deletion" }),
            SchemaVersion = "1.0"
        };

        await _client!.PostAsJsonAsync("/api/messages/publish", message);

        // Act - Delete the message
        var deleteResponse = await _client!.DeleteAsync($"/api/messages/{message.MessageId}");

        // Assert - Deletion succeeds
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify message is gone
        var getResponse = await _client.GetAsync($"/api/messages/{message.MessageId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "Message should not be retrievable after deletion");
    }

    /// <summary>
    /// Tests deleting a non-existent message returns 404.
    /// </summary>
    [Fact]
    public async Task DeleteMessage_WithNonExistentId_Returns404NotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid().ToString();

        // Act
        var response = await _client!.DeleteAsync($"/api/messages/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region Authorization Tests

    /// <summary>
    /// Tests that messaging endpoints require authentication.
    /// </summary>
    [Fact]
    public async Task PublishMessage_WithoutAuthentication_Returns401Unauthorized()
    {
        // Arrange - Create unauthenticated client
        var unauthClient = _factory!.CreateClient();

        var message = new Message
        {
            MessageId = Guid.NewGuid().ToString(),
            TopicName = "test.auth.topic",
            Payload = JsonSerializer.Serialize(new { Data = "Auth test" }),
            SchemaVersion = "1.0"
        };

        // Act
        var response = await unauthClient.PostAsJsonAsync("/api/messages/publish", message);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        unauthClient.Dispose();
    }

    /// <summary>
    /// Tests that message retrieval requires authentication.
    /// </summary>
    [Fact]
    public async Task GetMessage_WithoutAuthentication_Returns401Unauthorized()
    {
        // Arrange
        var unauthClient = _factory!.CreateClient();
        var messageId = Guid.NewGuid().ToString();

        // Act
        var response = await unauthClient.GetAsync($"/api/messages/{messageId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        unauthClient.Dispose();
    }

    /// <summary>
    /// Tests that message deletion requires authentication.
    /// </summary>
    [Fact]
    public async Task DeleteMessage_WithoutAuthentication_Returns401Unauthorized()
    {
        // Arrange
        var unauthClient = _factory!.CreateClient();
        var messageId = Guid.NewGuid().ToString();

        // Act
        var response = await unauthClient.DeleteAsync($"/api/messages/{messageId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        unauthClient.Dispose();
    }

    #endregion

    #region End-to-End Workflow Tests

    /// <summary>
    /// Tests complete message lifecycle: publish → retrieve → acknowledge → delete.
    /// </summary>
    [Fact]
    public async Task MessageLifecycle_PublishRetrieveAcknowledgeDelete_WorksEndToEnd()
    {
        // Arrange
        var message = new Message
        {
            MessageId = Guid.NewGuid().ToString(),
            TopicName = "test.lifecycle.topic",
            Payload = JsonSerializer.Serialize(new { TestData = "Lifecycle test", Timestamp = DateTime.UtcNow }),
            SchemaVersion = "1.0",
            Priority = 5
        };

        // Act & Assert - Publish
        var publishResponse = await _client!.PostAsJsonAsync("/api/messages/publish", message);
        publishResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // Act & Assert - Retrieve
        var getResponse = await _client!.GetAsync($"/api/messages/{message.MessageId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var retrievedMessage = await getResponse.Content.ReadFromJsonAsync<Message>();
        retrievedMessage!.Status.Should().Be(MessageStatus.Pending);

        // Act & Assert - Acknowledge
        var ackResponse = await _client.PostAsync($"/api/messages/{message.MessageId}/acknowledge", null);
        ackResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var acknowledgedMessage = await ackResponse.Content.ReadFromJsonAsync<Message>();
        acknowledgedMessage!.Status.Should().Be(MessageStatus.Acknowledged);

        // Act & Assert - Delete
        var deleteResponse = await _client.DeleteAsync($"/api/messages/{message.MessageId}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify deletion
        var finalGetResponse = await _client.GetAsync($"/api/messages/{message.MessageId}");
        finalGetResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion
}
