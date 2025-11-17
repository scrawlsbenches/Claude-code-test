using FluentAssertions;
using HotSwap.KnowledgeGraph.Domain.Models;
using HotSwap.KnowledgeGraph.QueryEngine.Services;
using HotSwap.KnowledgeGraph.Infrastructure.Repositories;
using Moq;

namespace HotSwap.KnowledgeGraph.Tests.QueryEngine;

/// <summary>
/// Tests for Dijkstra's shortest path algorithm for weighted graphs.
/// </summary>
public class DijkstraAlgorithmTests
{
    private readonly Mock<IGraphRepository> _mockRepository;
    private readonly DijkstraPathFinder _pathFinder;

    public DijkstraAlgorithmTests()
    {
        _mockRepository = new Mock<IGraphRepository>();
        _pathFinder = new DijkstraPathFinder(_mockRepository.Object);
    }

    [Fact]
    public async Task FindShortestPathAsync_WithWeightedGraph_FindsOptimalPath()
    {
        // Arrange
        var sourceId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var pathAId = Guid.NewGuid();
        var pathBId = Guid.NewGuid();

        var source = CreateEntity(sourceId, "Person", "Alice");
        var target = CreateEntity(targetId, "Person", "Dave");
        var pathA = CreateEntity(pathAId, "Person", "Bob");
        var pathB = CreateEntity(pathBId, "Person", "Charlie");

        // Two paths:
        // Alice -> Bob -> Dave (weight: 1 + 1 = 2)
        // Alice -> Charlie -> Dave (weight: 10 + 1 = 11)
        var rel1 = CreateRelationship(sourceId, pathAId, "KNOWS", weight: 1.0);
        var rel2 = CreateRelationship(pathAId, targetId, "KNOWS", weight: 1.0);
        var rel3 = CreateRelationship(sourceId, pathBId, "KNOWS", weight: 10.0);
        var rel4 = CreateRelationship(pathBId, targetId, "KNOWS", weight: 1.0);

        SetupMockEntities(sourceId, source, targetId, target, pathAId, pathA, pathBId, pathB);
        SetupMockRelationships(sourceId, new[] { rel1, rel3 }, pathAId, new[] { rel2 }, pathBId, new[] { rel4 }, targetId, Array.Empty<Relationship>());

        // Act
        var result = await _pathFinder.FindShortestPathAsync(sourceId, targetId);

        // Assert
        result.Should().NotBeNull();
        result!.Entities.Should().HaveCount(3);
        result.Entities[0].Id.Should().Be(sourceId);
        result.Entities[1].Id.Should().Be(pathAId); // Should go through Bob, not Charlie
        result.Entities[2].Id.Should().Be(targetId);
        result.TotalWeight.Should().Be(2.0);
        result.Hops.Should().Be(2);
    }

    [Fact]
    public async Task FindShortestPathAsync_WithDirectPath_ChoosesShorterWeight()
    {
        // Arrange
        var sourceId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var intermediateId = Guid.NewGuid();

        var source = CreateEntity(sourceId, "Person", "Alice");
        var target = CreateEntity(targetId, "Person", "Bob");
        var intermediate = CreateEntity(intermediateId, "Person", "Charlie");

        // Two paths:
        // Alice -> Bob (direct, weight: 5)
        // Alice -> Charlie -> Bob (indirect, weight: 2 + 2 = 4)
        var relDirect = CreateRelationship(sourceId, targetId, "KNOWS", weight: 5.0);
        var rel1 = CreateRelationship(sourceId, intermediateId, "KNOWS", weight: 2.0);
        var rel2 = CreateRelationship(intermediateId, targetId, "KNOWS", weight: 2.0);

        SetupMockEntities(sourceId, source, targetId, target, intermediateId, intermediate);
        SetupMockRelationships(sourceId, new[] { relDirect, rel1 }, intermediateId, new[] { rel2 }, targetId, Array.Empty<Relationship>());

        // Act
        var result = await _pathFinder.FindShortestPathAsync(sourceId, targetId);

        // Assert
        result.Should().NotBeNull();
        result!.TotalWeight.Should().Be(4.0); // Should take indirect path with lower weight
        result.Entities.Should().HaveCount(3); // Alice -> Charlie -> Bob
        result.Entities[1].Id.Should().Be(intermediateId);
    }

    [Fact]
    public async Task FindShortestPathAsync_WithNoPath_ReturnsNull()
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
        var result = await _pathFinder.FindShortestPathAsync(sourceId, targetId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task FindShortestPathAsync_WithSameSourceAndTarget_ReturnsEmptyPath()
    {
        // Arrange
        var entityId = Guid.NewGuid();
        var entity = CreateEntity(entityId, "Person", "Alice");

        _mockRepository
            .Setup(r => r.GetEntityByIdAsync(entityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        // Act
        var result = await _pathFinder.FindShortestPathAsync(entityId, entityId);

        // Assert
        result.Should().NotBeNull();
        result!.Entities.Should().HaveCount(1);
        result.Entities[0].Id.Should().Be(entityId);
        result.Relationships.Should().BeEmpty();
        result.TotalWeight.Should().Be(0.0);
        result.Hops.Should().Be(0);
    }

    [Fact]
    public async Task FindShortestPathAsync_WithComplexGraph_FindsOptimalPath()
    {
        // Arrange - Diamond-shaped graph with different weights
        var sourceId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var node1Id = Guid.NewGuid();
        var node2Id = Guid.NewGuid();
        var node3Id = Guid.NewGuid();

        var source = CreateEntity(sourceId, "Person", "A");
        var target = CreateEntity(targetId, "Person", "E");
        var node1 = CreateEntity(node1Id, "Person", "B");
        var node2 = CreateEntity(node2Id, "Person", "C");
        var node3 = CreateEntity(node3Id, "Person", "D");

        // Paths:
        // A -> B -> E (1 + 10 = 11)
        // A -> C -> E (5 + 2 = 7)
        // A -> D -> E (3 + 3 = 6) ← Optimal
        var rel1 = CreateRelationship(sourceId, node1Id, "KNOWS", weight: 1.0);
        var rel2 = CreateRelationship(node1Id, targetId, "KNOWS", weight: 10.0);
        var rel3 = CreateRelationship(sourceId, node2Id, "KNOWS", weight: 5.0);
        var rel4 = CreateRelationship(node2Id, targetId, "KNOWS", weight: 2.0);
        var rel5 = CreateRelationship(sourceId, node3Id, "KNOWS", weight: 3.0);
        var rel6 = CreateRelationship(node3Id, targetId, "KNOWS", weight: 3.0);

        SetupMockEntities(sourceId, source, targetId, target, node1Id, node1, node2Id, node2, node3Id, node3);
        SetupMockRelationships(
            sourceId, new[] { rel1, rel3, rel5 },
            node1Id, new[] { rel2 },
            node2Id, new[] { rel4 },
            node3Id, new[] { rel6 },
            targetId, Array.Empty<Relationship>());

        // Act
        var result = await _pathFinder.FindShortestPathAsync(sourceId, targetId);

        // Assert
        result.Should().NotBeNull();
        result!.TotalWeight.Should().Be(6.0);
        result.Entities[1].Id.Should().Be(node3Id); // Should go through D
    }

    [Fact]
    public void Constructor_WithNullRepository_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => new DijkstraPathFinder(null!);

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

    private static Relationship CreateRelationship(Guid sourceId, Guid targetId, string type, double weight = 1.0)
    {
        return new Relationship
        {
            Id = Guid.NewGuid(),
            Type = type,
            SourceEntityId = sourceId,
            TargetEntityId = targetId,
            Properties = new Dictionary<string, object>(),
            Weight = weight,
            IsDirected = true,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    private void SetupMockEntities(params object[] args)
    {
        for (int i = 0; i < args.Length; i += 2)
        {
            var id = (Guid)args[i];
            var entity = (Entity)args[i + 1];
            _mockRepository
                .Setup(r => r.GetEntityByIdAsync(id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(entity);
        }
    }

    private void SetupMockRelationships(params object[] args)
    {
        for (int i = 0; i < args.Length; i += 2)
        {
            var id = (Guid)args[i];
            var relationships = (Relationship[])args[i + 1];
            _mockRepository
                .Setup(r => r.GetRelationshipsByEntityAsync(id, true, false, It.IsAny<CancellationToken>()))
                .ReturnsAsync(relationships.ToList());
        }
    }
}
