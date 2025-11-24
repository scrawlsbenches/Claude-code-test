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

public class EntitiesControllerTests
{
    private readonly Mock<IGraphRepository> _repositoryMock;
    private readonly Mock<ILogger<EntitiesController>> _loggerMock;
    private readonly EntitiesController _controller;

    public EntitiesControllerTests()
    {
        _repositoryMock = new Mock<IGraphRepository>();
        _loggerMock = new Mock<ILogger<EntitiesController>>();
        _controller = new EntitiesController(_repositoryMock.Object, _loggerMock.Object);
    }

    #region CreateEntity Tests

    [Fact]
    public async Task CreateEntity_ValidRequest_ReturnsCreatedResult()
    {
        // Arrange
        var request = new CreateEntityRequest
        {
            Type = "Document",
            Properties = new Dictionary<string, object> { ["title"] = "Test" }
        };

        _repositoryMock
            .Setup(r => r.CreateEntityAsync(It.IsAny<Entity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Entity e, CancellationToken _) => e);

        // Act
        var result = await _controller.CreateEntity(request, CancellationToken.None);

        // Assert
        var createdResult = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.StatusCode.Should().Be(201);
        var response = createdResult.Value.Should().BeOfType<EntityResponse>().Subject;
        response.Type.Should().Be("Document");
    }

    [Fact]
    public async Task CreateEntity_WithProperties_PassesPropertiesToRepository()
    {
        // Arrange
        var properties = new Dictionary<string, object>
        {
            ["title"] = "Test Document",
            ["category"] = "Testing"
        };

        var request = new CreateEntityRequest
        {
            Type = "Document",
            Properties = properties
        };

        Entity? capturedEntity = null;
        _repositoryMock
            .Setup(r => r.CreateEntityAsync(It.IsAny<Entity>(), It.IsAny<CancellationToken>()))
            .Callback<Entity, CancellationToken>((e, _) => capturedEntity = e)
            .ReturnsAsync((Entity e, CancellationToken _) => e);

        // Act
        await _controller.CreateEntity(request, CancellationToken.None);

        // Assert
        capturedEntity.Should().NotBeNull();
        capturedEntity!.Properties.Should().ContainKey("title");
        capturedEntity.Properties.Should().ContainKey("category");
    }

    #endregion

    #region GetEntity Tests

    [Fact]
    public async Task GetEntity_ExistingId_ReturnsOkResult()
    {
        // Arrange
        var entityId = Guid.NewGuid();
        var entity = new Entity
        {
            Id = entityId,
            Type = "Document",
            Properties = new Dictionary<string, object>(),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _repositoryMock
            .Setup(r => r.GetEntityByIdAsync(entityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        // Act
        var result = await _controller.GetEntity(entityId, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<EntityResponse>().Subject;
        response.Id.Should().Be(entityId);
    }

    [Fact]
    public async Task GetEntity_NonExistingId_ReturnsNotFound()
    {
        // Arrange
        var entityId = Guid.NewGuid();
        _repositoryMock
            .Setup(r => r.GetEntityByIdAsync(entityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Entity?)null);

        // Act
        var result = await _controller.GetEntity(entityId, CancellationToken.None);

        // Assert
        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFoundResult.StatusCode.Should().Be(404);
    }

    #endregion

    #region ListEntities Tests

    [Fact]
    public async Task ListEntities_NoFilter_ReturnsAllEntities()
    {
        // Arrange
        var entities = new List<Entity>
        {
            new() { Id = Guid.NewGuid(), Type = "Document", Properties = new(), CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
            new() { Id = Guid.NewGuid(), Type = "Author", Properties = new(), CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow }
        };

        _repositoryMock
            .Setup(r => r.GetEntitiesByTypeAsync("", 0, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entities);

        // Act
        var result = await _controller.ListEntities(null, 0, 50, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<PaginatedResponse<EntityResponse>>().Subject;
        response.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task ListEntities_WithTypeFilter_FiltersEntities()
    {
        // Arrange
        var entities = new List<Entity>
        {
            new() { Id = Guid.NewGuid(), Type = "Document", Properties = new(), CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow }
        };

        _repositoryMock
            .Setup(r => r.GetEntitiesByTypeAsync("Document", 0, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entities);

        // Act
        var result = await _controller.ListEntities("Document", 0, 50, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<PaginatedResponse<EntityResponse>>().Subject;
        response.Items.Should().HaveCount(1);
        response.Items[0].Type.Should().Be("Document");
    }

    [Fact]
    public async Task ListEntities_PageSizeExceeds100_CapsAt100()
    {
        // Arrange
        _repositoryMock
            .Setup(r => r.GetEntitiesByTypeAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Entity>());

        // Act
        await _controller.ListEntities(null, 0, 200, CancellationToken.None);

        // Assert
        _repositoryMock.Verify(r => r.GetEntitiesByTypeAsync("", 0, 100, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region UpdateEntity Tests

    [Fact]
    public async Task UpdateEntity_ExistingEntity_ReturnsUpdatedEntity()
    {
        // Arrange
        var entityId = Guid.NewGuid();
        var existingEntity = new Entity
        {
            Id = entityId,
            Type = "Document",
            Properties = new Dictionary<string, object> { ["title"] = "Old Title" },
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var request = new UpdateEntityRequest
        {
            Properties = new Dictionary<string, object> { ["title"] = "New Title" }
        };

        _repositoryMock
            .Setup(r => r.GetEntityByIdAsync(entityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingEntity);

        _repositoryMock
            .Setup(r => r.UpdateEntityAsync(It.IsAny<Entity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Entity e, CancellationToken _) => e);

        // Act
        var result = await _controller.UpdateEntity(entityId, request, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<EntityResponse>().Subject;
        response.Properties["title"].Should().Be("New Title");
    }

    [Fact]
    public async Task UpdateEntity_NonExistingEntity_ReturnsNotFound()
    {
        // Arrange
        var entityId = Guid.NewGuid();
        _repositoryMock
            .Setup(r => r.GetEntityByIdAsync(entityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Entity?)null);

        var request = new UpdateEntityRequest
        {
            Properties = new Dictionary<string, object> { ["title"] = "New Title" }
        };

        // Act
        var result = await _controller.UpdateEntity(entityId, request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    #endregion

    #region DeleteEntity Tests

    [Fact]
    public async Task DeleteEntity_ExistingEntity_ReturnsNoContent()
    {
        // Arrange
        var entityId = Guid.NewGuid();
        _repositoryMock
            .Setup(r => r.DeleteEntityAsync(entityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.DeleteEntity(entityId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task DeleteEntity_NonExistingEntity_ReturnsNotFound()
    {
        // Arrange
        var entityId = Guid.NewGuid();
        _repositoryMock
            .Setup(r => r.DeleteEntityAsync(entityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.DeleteEntity(entityId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    #endregion

    #region BulkCreateEntities Tests

    [Fact]
    public async Task CreateEntitiesBulk_ValidRequest_ReturnsCreatedWithIds()
    {
        // Arrange
        var request = new BulkCreateEntitiesRequest
        {
            Entities = new List<CreateEntityRequest>
            {
                new() { Type = "Document", Properties = new() },
                new() { Type = "Tag", Properties = new() }
            }
        };

        _repositoryMock
            .Setup(r => r.CreateEntityAsync(It.IsAny<Entity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Entity e, CancellationToken _) => e);

        // Act
        var result = await _controller.CreateEntitiesBulk(request, CancellationToken.None);

        // Assert
        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(201);
        var response = objectResult.Value.Should().BeOfType<BulkOperationResponse>().Subject;
        response.SuccessCount.Should().Be(2);
        response.CreatedIds.Should().HaveCount(2);
    }

    #endregion

    #region GetEntityRelationships Tests

    [Fact]
    public async Task GetEntityRelationships_ExistingEntity_ReturnsRelationships()
    {
        // Arrange
        var entityId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var entity = new Entity
        {
            Id = entityId,
            Type = "Document",
            Properties = new(),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var relationships = new List<Relationship>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Type = "TAGGED_WITH",
                SourceEntityId = entityId,
                TargetEntityId = targetId,
                Properties = new(),
                CreatedAt = DateTimeOffset.UtcNow
            }
        };

        _repositoryMock
            .Setup(r => r.GetEntityByIdAsync(entityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        _repositoryMock
            .Setup(r => r.GetRelationshipsByEntityAsync(entityId, true, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(relationships);

        // Act
        var result = await _controller.GetEntityRelationships(entityId, "both", CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeAssignableTo<List<RelationshipResponse>>().Subject;
        response.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetEntityRelationships_NonExistingEntity_ReturnsNotFound()
    {
        // Arrange
        var entityId = Guid.NewGuid();
        _repositoryMock
            .Setup(r => r.GetEntityByIdAsync(entityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Entity?)null);

        // Act
        var result = await _controller.GetEntityRelationships(entityId, "both", CancellationToken.None);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    #endregion
}
