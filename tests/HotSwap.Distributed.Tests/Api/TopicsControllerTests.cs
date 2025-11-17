using FluentAssertions;
using HotSwap.Distributed.Api.Controllers;
using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Orchestrator.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace HotSwap.Distributed.Tests.Api;

public class TopicsControllerTests
{
    private readonly Mock<ITopicService> _mockTopicService;
    private readonly TopicsController _controller;

    public TopicsControllerTests()
    {
        _mockTopicService = new Mock<ITopicService>();
        _controller = new TopicsController(
            _mockTopicService.Object,
            NullLogger<TopicsController>.Instance);
    }

    private Topic CreateTestTopic(string name = "test.topic")
    {
        return new Topic
        {
            Name = name,
            Description = "Test topic",
            SchemaId = "test-schema-v1",
            Type = TopicType.PubSub,
            DeliveryGuarantee = DeliveryGuarantee.AtLeastOnce,
            RetentionPeriod = TimeSpan.FromDays(7),
            PartitionCount = 3,
            ReplicationFactor = 2,
            Config = new Dictionary<string, string>(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            MessageCount = 0,
            SubscriptionCount = 0
        };
    }

    #region CreateTopic Tests

    [Fact]
    public async Task CreateTopic_WithValidTopic_ReturnsCreatedAtAction()
    {
        // Arrange
        var topic = CreateTestTopic("orders.created");
        _mockTopicService.Setup(x => x.CreateTopicAsync(It.IsAny<Topic>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(topic);

        // Act
        var result = await _controller.CreateTopic(topic);

        // Assert
        var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.ActionName.Should().Be(nameof(TopicsController.GetTopic));
        createdResult.RouteValues!["name"].Should().Be("orders.created");
        var returnedTopic = createdResult.Value.Should().BeOfType<Topic>().Subject;
        returnedTopic.Name.Should().Be("orders.created");
    }

    [Fact]
    public async Task CreateTopic_WithInvalidTopic_ReturnsBadRequest()
    {
        // Arrange
        var topic = CreateTestTopic("");
        topic.Name = ""; // Invalid name

        // Act
        var result = await _controller.CreateTopic(topic);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task CreateTopic_WithDuplicateName_ReturnsConflict()
    {
        // Arrange
        var topic = CreateTestTopic("orders.created");
        _mockTopicService.Setup(x => x.CreateTopicAsync(It.IsAny<Topic>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Topic already exists"));

        // Act
        var result = await _controller.CreateTopic(topic);

        // Assert
        var conflictResult = result.Result.Should().BeOfType<ConflictObjectResult>().Subject;
        conflictResult.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateTopic_WithNullTopic_ReturnsBadRequest()
    {
        // Act
        var result = await _controller.CreateTopic(null!);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    #endregion

    #region ListTopics Tests

    [Fact]
    public async Task ListTopics_WithTopics_ReturnsOkWithTopicList()
    {
        // Arrange
        var topics = new List<Topic>
        {
            CreateTestTopic("orders.created"),
            CreateTestTopic("users.registered"),
            CreateTestTopic("payments.processed")
        };

        _mockTopicService.Setup(x => x.ListTopicsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(topics);

        // Act
        var result = await _controller.ListTopics();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedTopics = okResult.Value.Should().BeAssignableTo<IEnumerable<Topic>>().Subject;
        returnedTopics.Should().HaveCount(3);
    }

    [Fact]
    public async Task ListTopics_WithNoTopics_ReturnsEmptyList()
    {
        // Arrange
        _mockTopicService.Setup(x => x.ListTopicsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Topic>());

        // Act
        var result = await _controller.ListTopics();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedTopics = okResult.Value.Should().BeAssignableTo<IEnumerable<Topic>>().Subject;
        returnedTopics.Should().BeEmpty();
    }

    [Fact]
    public async Task ListTopics_WithServiceFailure_ReturnsInternalServerError()
    {
        // Arrange
        _mockTopicService.Setup(x => x.ListTopicsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database unavailable"));

        // Act
        var result = await _controller.ListTopics();

        // Assert
        var statusCodeResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        statusCodeResult.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
    }

    #endregion

    #region GetTopic Tests

    [Fact]
    public async Task GetTopic_WithExistingTopic_ReturnsOkWithTopic()
    {
        // Arrange
        var topic = CreateTestTopic("orders.created");
        _mockTopicService.Setup(x => x.GetTopicAsync("orders.created", It.IsAny<CancellationToken>()))
            .ReturnsAsync(topic);

        // Act
        var result = await _controller.GetTopic("orders.created");

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedTopic = okResult.Value.Should().BeOfType<Topic>().Subject;
        returnedTopic.Name.Should().Be("orders.created");
    }

    [Fact]
    public async Task GetTopic_WithNonExistentTopic_ReturnsNotFound()
    {
        // Arrange
        _mockTopicService.Setup(x => x.GetTopicAsync("nonexistent", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Topic?)null);

        // Act
        var result = await _controller.GetTopic("nonexistent");

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetTopic_WithNullName_ReturnsBadRequest()
    {
        // Act
        var result = await _controller.GetTopic(null!);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetTopic_WithEmptyName_ReturnsBadRequest()
    {
        // Act
        var result = await _controller.GetTopic("");

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    #endregion

    #region UpdateTopic Tests

    [Fact]
    public async Task UpdateTopic_WithValidUpdate_ReturnsOkWithUpdatedTopic()
    {
        // Arrange
        var existingTopic = CreateTestTopic("orders.created");
        var updatedTopic = CreateTestTopic("orders.created");
        updatedTopic.Description = "Updated description";
        updatedTopic.PartitionCount = 5;

        _mockTopicService.Setup(x => x.UpdateTopicAsync("orders.created", It.IsAny<Topic>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(updatedTopic);

        // Act
        var result = await _controller.UpdateTopic("orders.created", updatedTopic);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedTopic = okResult.Value.Should().BeOfType<Topic>().Subject;
        returnedTopic.Description.Should().Be("Updated description");
        returnedTopic.PartitionCount.Should().Be(5);
    }

    [Fact]
    public async Task UpdateTopic_WithNonExistentTopic_ReturnsNotFound()
    {
        // Arrange
        var topic = CreateTestTopic("nonexistent");
        _mockTopicService.Setup(x => x.UpdateTopicAsync("nonexistent", It.IsAny<Topic>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Topic?)null);

        // Act
        var result = await _controller.UpdateTopic("nonexistent", topic);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task UpdateTopic_WithInvalidTopic_ReturnsBadRequest()
    {
        // Arrange
        var topic = CreateTestTopic("orders.created");
        topic.PartitionCount = 100; // Invalid (max is 16)

        // Act
        var result = await _controller.UpdateTopic("orders.created", topic);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task UpdateTopic_WithNameMismatch_ReturnsBadRequest()
    {
        // Arrange
        var topic = CreateTestTopic("different.topic");

        // Act
        var result = await _controller.UpdateTopic("orders.created", topic);

        // Assert
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateTopic_WithNullTopic_ReturnsBadRequest()
    {
        // Act
        var result = await _controller.UpdateTopic("orders.created", null!);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    #endregion

    #region DeleteTopic Tests

    [Fact]
    public async Task DeleteTopic_WithExistingTopic_ReturnsNoContent()
    {
        // Arrange
        _mockTopicService.Setup(x => x.DeleteTopicAsync("orders.created", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.DeleteTopic("orders.created");

        // Assert
        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task DeleteTopic_WithNonExistentTopic_ReturnsNotFound()
    {
        // Arrange
        _mockTopicService.Setup(x => x.DeleteTopicAsync("nonexistent", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.DeleteTopic("nonexistent");

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task DeleteTopic_WithNullName_ReturnsBadRequest()
    {
        // Act
        var result = await _controller.DeleteTopic(null!);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task DeleteTopic_WithActiveSubscriptions_ReturnsConflict()
    {
        // Arrange
        _mockTopicService.Setup(x => x.DeleteTopicAsync("orders.created", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Cannot delete topic with active subscriptions"));

        // Act
        var result = await _controller.DeleteTopic("orders.created");

        // Assert
        var conflictResult = result.Should().BeOfType<ConflictObjectResult>().Subject;
        conflictResult.Value.Should().NotBeNull();
    }

    #endregion

    #region GetTopicMetrics Tests

    [Fact]
    public async Task GetTopicMetrics_WithExistingTopic_ReturnsOkWithMetrics()
    {
        // Arrange
        var metrics = new Dictionary<string, object>
        {
            ["messageCount"] = 1000L,
            ["subscriptionCount"] = 5,
            ["messagesPerSecond"] = 50.5,
            ["averageMessageSize"] = 2048L,
            ["partitions"] = new[]
            {
                new { partition = 0, messageCount = 300 },
                new { partition = 1, messageCount = 350 },
                new { partition = 2, messageCount = 350 }
            }
        };

        _mockTopicService.Setup(x => x.GetTopicMetricsAsync("orders.created", It.IsAny<CancellationToken>()))
            .ReturnsAsync(metrics);

        // Act
        var result = await _controller.GetTopicMetrics("orders.created");

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedMetrics = okResult.Value.Should().BeAssignableTo<IDictionary<string, object>>().Subject;
        returnedMetrics["messageCount"].Should().Be(1000L);
        returnedMetrics["subscriptionCount"].Should().Be(5);
    }

    [Fact]
    public async Task GetTopicMetrics_WithNonExistentTopic_ReturnsNotFound()
    {
        // Arrange
        _mockTopicService.Setup(x => x.GetTopicMetricsAsync("nonexistent", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Dictionary<string, object>?)null);

        // Act
        var result = await _controller.GetTopicMetrics("nonexistent");

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetTopicMetrics_WithNullName_ReturnsBadRequest()
    {
        // Act
        var result = await _controller.GetTopicMetrics(null!);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    #endregion
}
