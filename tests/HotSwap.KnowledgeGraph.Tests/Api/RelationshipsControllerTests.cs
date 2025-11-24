using FluentAssertions;
using HotSwap.KnowledgeGraph.Api.Controllers;
using HotSwap.KnowledgeGraph.Api.Models;
using HotSwap.KnowledgeGraph.Domain.Models;
using HotSwap.KnowledgeGraph.Infrastructure.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace HotSwap.KnowledgeGraph.Tests.Api;

public class RelationshipsControllerTests
{
    private readonly Mock<IGraphRepository> _repositoryMock;
    private readonly Mock<ILogger<RelationshipsController>> _loggerMock;
    private readonly RelationshipsController _controller;

    public RelationshipsControllerTests()
    {
        _repositoryMock = new Mock<IGraphRepository>();
        _loggerMock = new Mock<ILogger<RelationshipsController>>();
        _controller = new RelationshipsController(_repositoryMock.Object, _loggerMock.Object);
    }

    #region CreateRelationship Tests

    [Fact]
    public async Task CreateRelationship_ValidRequest_ReturnsCreatedResult()
    {
        // Arrange
        var sourceId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var request = new CreateRelationshipRequest
        {
            Type = "TAGGED_WITH",
            SourceEntityId = sourceId,
            TargetEntityId = targetId
        };

        var sourceEntity = new Entity { Id = sourceId, Type = "Document", Properties = new(), CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        var targetEntity = new Entity { Id = targetId, Type = "Tag", Properties = new(), CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };

        _repositoryMock.Setup(r => r.GetEntityByIdAsync(sourceId, It.IsAny<CancellationToken>())).ReturnsAsync(sourceEntity);
        _repositoryMock.Setup(r => r.GetEntityByIdAsync(targetId, It.IsAny<CancellationToken>())).ReturnsAsync(targetEntity);
        _repositoryMock.Setup(r => r.CreateRelationshipAsync(It.IsAny<Relationship>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Relationship r, CancellationToken _) => r);

        // Act
        var result = await _controller.CreateRelationship(request, CancellationToken.None);

        // Assert
        var createdResult = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.StatusCode.Should().Be(201);
        var response = createdResult.Value.Should().BeOfType<RelationshipResponse>().Subject;
        response.Type.Should().Be("TAGGED_WITH");
    }

    [Fact]
    public async Task CreateRelationship_SourceNotFound_ReturnsNotFound()
    {
        // Arrange
        var sourceId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var request = new CreateRelationshipRequest
        {
            Type = "TAGGED_WITH",
            SourceEntityId = sourceId,
            TargetEntityId = targetId
        };

        _repositoryMock.Setup(r => r.GetEntityByIdAsync(sourceId, It.IsAny<CancellationToken>())).ReturnsAsync((Entity?)null);

        // Act
        var result = await _controller.CreateRelationship(request, CancellationToken.None);

        // Assert
        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        var error = notFoundResult.Value.Should().BeOfType<ErrorResponse>().Subject;
        error.Message.Should().Contain("Source entity");
    }

    [Fact]
    public async Task CreateRelationship_TargetNotFound_ReturnsNotFound()
    {
        // Arrange
        var sourceId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var request = new CreateRelationshipRequest
        {
            Type = "TAGGED_WITH",
            SourceEntityId = sourceId,
            TargetEntityId = targetId
        };

        var sourceEntity = new Entity { Id = sourceId, Type = "Document", Properties = new(), CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        _repositoryMock.Setup(r => r.GetEntityByIdAsync(sourceId, It.IsAny<CancellationToken>())).ReturnsAsync(sourceEntity);
        _repositoryMock.Setup(r => r.GetEntityByIdAsync(targetId, It.IsAny<CancellationToken>())).ReturnsAsync((Entity?)null);

        // Act
        var result = await _controller.CreateRelationship(request, CancellationToken.None);

        // Assert
        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        var error = notFoundResult.Value.Should().BeOfType<ErrorResponse>().Subject;
        error.Message.Should().Contain("Target entity");
    }

    [Fact]
    public async Task CreateRelationship_WithWeightAndDirection_SetsCorrectly()
    {
        // Arrange
        var sourceId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var request = new CreateRelationshipRequest
        {
            Type = "RELATED_TO",
            SourceEntityId = sourceId,
            TargetEntityId = targetId,
            Weight = 0.75,
            IsDirected = false
        };

        var sourceEntity = new Entity { Id = sourceId, Type = "Document", Properties = new(), CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        var targetEntity = new Entity { Id = targetId, Type = "Document", Properties = new(), CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };

        Relationship? capturedRelationship = null;
        _repositoryMock.Setup(r => r.GetEntityByIdAsync(sourceId, It.IsAny<CancellationToken>())).ReturnsAsync(sourceEntity);
        _repositoryMock.Setup(r => r.GetEntityByIdAsync(targetId, It.IsAny<CancellationToken>())).ReturnsAsync(targetEntity);
        _repositoryMock.Setup(r => r.CreateRelationshipAsync(It.IsAny<Relationship>(), It.IsAny<CancellationToken>()))
            .Callback<Relationship, CancellationToken>((r, _) => capturedRelationship = r)
            .ReturnsAsync((Relationship r, CancellationToken _) => r);

        // Act
        await _controller.CreateRelationship(request, CancellationToken.None);

        // Assert
        capturedRelationship.Should().NotBeNull();
        capturedRelationship!.Weight.Should().Be(0.75);
        capturedRelationship.IsDirected.Should().BeFalse();
    }

    #endregion

    #region GetRelationship Tests

    [Fact]
    public async Task GetRelationship_ExistingId_ReturnsOkResult()
    {
        // Arrange
        var relationshipId = Guid.NewGuid();
        var relationship = new Relationship
        {
            Id = relationshipId,
            Type = "TAGGED_WITH",
            SourceEntityId = Guid.NewGuid(),
            TargetEntityId = Guid.NewGuid(),
            Properties = new(),
            CreatedAt = DateTimeOffset.UtcNow
        };

        _repositoryMock.Setup(r => r.GetRelationshipByIdAsync(relationshipId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(relationship);

        // Act
        var result = await _controller.GetRelationship(relationshipId, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<RelationshipResponse>().Subject;
        response.Id.Should().Be(relationshipId);
    }

    [Fact]
    public async Task GetRelationship_NonExistingId_ReturnsNotFound()
    {
        // Arrange
        var relationshipId = Guid.NewGuid();
        _repositoryMock.Setup(r => r.GetRelationshipByIdAsync(relationshipId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Relationship?)null);

        // Act
        var result = await _controller.GetRelationship(relationshipId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    #endregion

    #region DeleteRelationship Tests

    [Fact]
    public async Task DeleteRelationship_ExistingRelationship_ReturnsNoContent()
    {
        // Arrange
        var relationshipId = Guid.NewGuid();
        _repositoryMock.Setup(r => r.DeleteRelationshipAsync(relationshipId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.DeleteRelationship(relationshipId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task DeleteRelationship_NonExistingRelationship_ReturnsNotFound()
    {
        // Arrange
        var relationshipId = Guid.NewGuid();
        _repositoryMock.Setup(r => r.DeleteRelationshipAsync(relationshipId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.DeleteRelationship(relationshipId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    #endregion

    #region BulkCreateRelationships Tests

    [Fact]
    public async Task CreateRelationshipsBulk_ValidRequest_ReturnsCreatedWithIds()
    {
        // Arrange
        var sourceId = Guid.NewGuid();
        var target1Id = Guid.NewGuid();
        var target2Id = Guid.NewGuid();

        var request = new BulkCreateRelationshipsRequest
        {
            Relationships = new List<CreateRelationshipRequest>
            {
                new() { Type = "TAGGED_WITH", SourceEntityId = sourceId, TargetEntityId = target1Id },
                new() { Type = "TAGGED_WITH", SourceEntityId = sourceId, TargetEntityId = target2Id }
            }
        };

        var sourceEntity = new Entity { Id = sourceId, Type = "Document", Properties = new(), CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        var target1Entity = new Entity { Id = target1Id, Type = "Tag", Properties = new(), CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        var target2Entity = new Entity { Id = target2Id, Type = "Tag", Properties = new(), CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };

        _repositoryMock.Setup(r => r.GetEntityByIdAsync(sourceId, It.IsAny<CancellationToken>())).ReturnsAsync(sourceEntity);
        _repositoryMock.Setup(r => r.GetEntityByIdAsync(target1Id, It.IsAny<CancellationToken>())).ReturnsAsync(target1Entity);
        _repositoryMock.Setup(r => r.GetEntityByIdAsync(target2Id, It.IsAny<CancellationToken>())).ReturnsAsync(target2Entity);
        _repositoryMock.Setup(r => r.CreateRelationshipAsync(It.IsAny<Relationship>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Relationship r, CancellationToken _) => r);

        // Act
        var result = await _controller.CreateRelationshipsBulk(request, CancellationToken.None);

        // Assert
        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(201);
        var response = objectResult.Value.Should().BeOfType<BulkOperationResponse>().Subject;
        response.SuccessCount.Should().Be(2);
        response.FailureCount.Should().Be(0);
    }

    [Fact]
    public async Task CreateRelationshipsBulk_SomeInvalidEntities_ReturnsPartialSuccess()
    {
        // Arrange
        var sourceId = Guid.NewGuid();
        var validTargetId = Guid.NewGuid();
        var invalidTargetId = Guid.NewGuid();

        var request = new BulkCreateRelationshipsRequest
        {
            Relationships = new List<CreateRelationshipRequest>
            {
                new() { Type = "TAGGED_WITH", SourceEntityId = sourceId, TargetEntityId = validTargetId },
                new() { Type = "TAGGED_WITH", SourceEntityId = sourceId, TargetEntityId = invalidTargetId }
            }
        };

        var sourceEntity = new Entity { Id = sourceId, Type = "Document", Properties = new(), CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        var validTargetEntity = new Entity { Id = validTargetId, Type = "Tag", Properties = new(), CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };

        _repositoryMock.Setup(r => r.GetEntityByIdAsync(sourceId, It.IsAny<CancellationToken>())).ReturnsAsync(sourceEntity);
        _repositoryMock.Setup(r => r.GetEntityByIdAsync(validTargetId, It.IsAny<CancellationToken>())).ReturnsAsync(validTargetEntity);
        _repositoryMock.Setup(r => r.GetEntityByIdAsync(invalidTargetId, It.IsAny<CancellationToken>())).ReturnsAsync((Entity?)null);
        _repositoryMock.Setup(r => r.CreateRelationshipAsync(It.IsAny<Relationship>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Relationship r, CancellationToken _) => r);

        // Act
        var result = await _controller.CreateRelationshipsBulk(request, CancellationToken.None);

        // Assert
        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        var response = objectResult.Value.Should().BeOfType<BulkOperationResponse>().Subject;
        response.SuccessCount.Should().Be(1);
        response.FailureCount.Should().Be(1);
        response.Errors.Should().HaveCount(1);
    }

    #endregion
}
