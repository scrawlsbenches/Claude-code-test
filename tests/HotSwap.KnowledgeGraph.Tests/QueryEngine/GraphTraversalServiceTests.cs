using FluentAssertions;
using HotSwap.KnowledgeGraph.Domain.Models;
using HotSwap.KnowledgeGraph.Infrastructure.Repositories;
using HotSwap.KnowledgeGraph.QueryEngine.Services;
using Moq;

namespace HotSwap.KnowledgeGraph.Tests.QueryEngine;

/// <summary>
/// Tests for graph traversal algorithms (BFS, DFS).
/// </summary>
public class GraphTraversalServiceTests
{
    private readonly Mock<IGraphRepository> _mockRepository;
    private readonly GraphTraversalService _service;

    public GraphTraversalServiceTests()
    {
        _mockRepository = new Mock<IGraphRepository>();
        _service = new GraphTraversalService(_mockRepository.Object);
    }

    [Fact]
    public async Task BreadthFirstSearchAsync_WithDirectPath_FindsShortestPath()
    {
        // Arrange
        var sourceId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var intermediateId = Guid.NewGuid();

        var source = CreateEntity(sourceId, "Person", "Alice");
        var intermediate = CreateEntity(intermediateId, "Person", "Bob");
        var target = CreateEntity(targetId, "Person", "Charlie");

        var rel1 = CreateRelationship(sourceId, intermediateId, "KNOWS");
        var rel2 = CreateRelationship(intermediateId, targetId, "KNOWS");

        _mockRepository
            .Setup(r => r.GetEntityByIdAsync(sourceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(source);

        _mockRepository
            .Setup(r => r.GetEntityByIdAsync(targetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(target);

        _mockRepository
            .Setup(r => r.GetEntityByIdAsync(intermediateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(intermediate);

        _mockRepository
            .Setup(r => r.GetRelationshipsByEntityAsync(sourceId, true, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Relationship> { rel1 });

        _mockRepository
            .Setup(r => r.GetRelationshipsByEntityAsync(intermediateId, true, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Relationship> { rel2 });

        _mockRepository
            .Setup(r => r.GetRelationshipsByEntityAsync(targetId, true, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Relationship>());

        // Act
        var result = await _service.BreadthFirstSearchAsync(sourceId, targetId);

        // Assert
        result.Should().NotBeNull();
        result!.Entities.Should().HaveCount(3);
        result.Entities[0].Id.Should().Be(sourceId);
        result.Entities[1].Id.Should().Be(intermediateId);
        result.Entities[2].Id.Should().Be(targetId);
        result.Relationships.Should().HaveCount(2);
        result.Hops.Should().Be(2);
    }

    [Fact]
    public async Task BreadthFirstSearchAsync_WithNoPath_ReturnsNull()
    {
        // Arrange
        var sourceId = Guid.NewGuid();
        var targetId = Guid.NewGuid();

        var source = CreateEntity(sourceId, "Person", "Alice");
        var target = CreateEntity(targetId, "Person", "Bob");

        _mockRepository
            .Setup(r => r.GetEntityByIdAsync(sourceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(source);

        _mockRepository
            .Setup(r => r.GetEntityByIdAsync(targetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(target);

        _mockRepository
            .Setup(r => r.GetRelationshipsByEntityAsync(sourceId, true, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Relationship>());

        // Act
        var result = await _service.BreadthFirstSearchAsync(sourceId, targetId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task BreadthFirstSearchAsync_WithSameSourceAndTarget_ReturnsEmptyPath()
    {
        // Arrange
        var entityId = Guid.NewGuid();
        var entity = CreateEntity(entityId, "Person", "Alice");

        _mockRepository
            .Setup(r => r.GetEntityByIdAsync(entityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        // Act
        var result = await _service.BreadthFirstSearchAsync(entityId, entityId);

        // Assert
        result.Should().NotBeNull();
        result!.Entities.Should().HaveCount(1);
        result.Entities[0].Id.Should().Be(entityId);
        result.Relationships.Should().BeEmpty();
        result.Hops.Should().Be(0);
    }

    [Fact]
    public async Task DepthFirstSearchAsync_FindsAllPaths()
    {
        // Arrange
        var sourceId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var path1Id = Guid.NewGuid();
        var path2Id = Guid.NewGuid();

        var source = CreateEntity(sourceId, "Person", "Alice");
        var target = CreateEntity(targetId, "Person", "Dave");
        var path1 = CreateEntity(path1Id, "Person", "Bob");
        var path2 = CreateEntity(path2Id, "Person", "Charlie");

        // Two paths: Alice -> Bob -> Dave and Alice -> Charlie -> Dave
        var rel1 = CreateRelationship(sourceId, path1Id, "KNOWS");
        var rel2 = CreateRelationship(sourceId, path2Id, "KNOWS");
        var rel3 = CreateRelationship(path1Id, targetId, "KNOWS");
        var rel4 = CreateRelationship(path2Id, targetId, "KNOWS");

        _mockRepository
            .Setup(r => r.GetEntityByIdAsync(sourceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(source);

        _mockRepository
            .Setup(r => r.GetEntityByIdAsync(targetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(target);

        _mockRepository
            .Setup(r => r.GetEntityByIdAsync(path1Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(path1);

        _mockRepository
            .Setup(r => r.GetEntityByIdAsync(path2Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(path2);

        _mockRepository
            .Setup(r => r.GetRelationshipsByEntityAsync(sourceId, true, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Relationship> { rel1, rel2 });

        _mockRepository
            .Setup(r => r.GetRelationshipsByEntityAsync(path1Id, true, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Relationship> { rel3 });

        _mockRepository
            .Setup(r => r.GetRelationshipsByEntityAsync(path2Id, true, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Relationship> { rel4 });

        _mockRepository
            .Setup(r => r.GetRelationshipsByEntityAsync(targetId, true, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Relationship>());

        // Act
        var results = await _service.DepthFirstSearchAsync(sourceId, targetId, maxDepth: 5);

        // Assert
        results.Should().NotBeNull();
        results.Should().HaveCount(2);
        results.Should().AllSatisfy(path =>
        {
            path.Entities.Should().HaveCount(3);
            path.Entities[0].Id.Should().Be(sourceId);
            path.Entities[2].Id.Should().Be(targetId);
            path.Relationships.Should().HaveCount(2);
            path.Hops.Should().Be(2);
        });
    }

    [Fact]
    public async Task DepthFirstSearchAsync_WithMaxDepth_LimitsSearchDepth()
    {
        // Arrange
        var sourceId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var intermediate1 = Guid.NewGuid();
        var intermediate2 = Guid.NewGuid();

        var source = CreateEntity(sourceId, "Person", "Alice");
        var target = CreateEntity(targetId, "Person", "Eve");
        var int1 = CreateEntity(intermediate1, "Person", "Bob");
        var int2 = CreateEntity(intermediate2, "Person", "Charlie");

        // Path: Alice -> Bob -> Charlie -> Eve (depth 3)
        var rel1 = CreateRelationship(sourceId, intermediate1, "KNOWS");
        var rel2 = CreateRelationship(intermediate1, intermediate2, "KNOWS");
        var rel3 = CreateRelationship(intermediate2, targetId, "KNOWS");

        _mockRepository
            .Setup(r => r.GetEntityByIdAsync(sourceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(source);

        _mockRepository
            .Setup(r => r.GetEntityByIdAsync(targetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(target);

        _mockRepository
            .Setup(r => r.GetEntityByIdAsync(intermediate1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(int1);

        _mockRepository
            .Setup(r => r.GetEntityByIdAsync(intermediate2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(int2);

        _mockRepository
            .Setup(r => r.GetRelationshipsByEntityAsync(sourceId, true, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Relationship> { rel1 });

        _mockRepository
            .Setup(r => r.GetRelationshipsByEntityAsync(intermediate1, true, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Relationship> { rel2 });

        _mockRepository
            .Setup(r => r.GetRelationshipsByEntityAsync(intermediate2, true, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Relationship> { rel3 });

        _mockRepository
            .Setup(r => r.GetRelationshipsByEntityAsync(targetId, true, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Relationship>());

        // Act - maxDepth 2 should not find the path (requires 3 hops)
        var results = await _service.DepthFirstSearchAsync(sourceId, targetId, maxDepth: 2);

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task DepthFirstSearchAsync_WithCycles_AvoidsCycles()
    {
        // Arrange
        var sourceId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var cycleId = Guid.NewGuid();

        var source = CreateEntity(sourceId, "Person", "Alice");
        var target = CreateEntity(targetId, "Person", "Charlie");
        var cycle = CreateEntity(cycleId, "Person", "Bob");

        // Cycle: Alice -> Bob -> Alice (should be detected and avoided)
        // Path: Alice -> Charlie
        var rel1 = CreateRelationship(sourceId, cycleId, "KNOWS");
        var rel2 = CreateRelationship(cycleId, sourceId, "KNOWS"); // Cycle back
        var rel3 = CreateRelationship(sourceId, targetId, "KNOWS");

        _mockRepository
            .Setup(r => r.GetEntityByIdAsync(sourceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(source);

        _mockRepository
            .Setup(r => r.GetEntityByIdAsync(targetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(target);

        _mockRepository
            .Setup(r => r.GetEntityByIdAsync(cycleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cycle);

        _mockRepository
            .Setup(r => r.GetRelationshipsByEntityAsync(sourceId, true, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Relationship> { rel1, rel3 });

        _mockRepository
            .Setup(r => r.GetRelationshipsByEntityAsync(cycleId, true, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Relationship> { rel2 });

        _mockRepository
            .Setup(r => r.GetRelationshipsByEntityAsync(targetId, true, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Relationship>());

        // Act
        var results = await _service.DepthFirstSearchAsync(sourceId, targetId, maxDepth: 5);

        // Assert
        results.Should().HaveCount(1);
        results[0].Entities.Should().HaveCount(2);
        results[0].Entities[0].Id.Should().Be(sourceId);
        results[0].Entities[1].Id.Should().Be(targetId);
        results[0].Hops.Should().Be(1);
    }

    [Fact]
    public void Constructor_WithNullRepository_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => new GraphTraversalService(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("repository");
    }

    // Helper methods
    private static Entity CreateEntity(Guid id, string type, string name)
    {
        return new Entity
        {
            Id = id,
            Type = type,
            Properties = new Dictionary<string, object> { ["name"] = name },
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    private static Relationship CreateRelationship(Guid sourceId, Guid targetId, string type)
    {
        return new Relationship
        {
            Id = Guid.NewGuid(),
            Type = type,
            SourceEntityId = sourceId,
            TargetEntityId = targetId,
            Properties = new Dictionary<string, object>(),
            Weight = 1.0,
            IsDirected = true,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }
}
