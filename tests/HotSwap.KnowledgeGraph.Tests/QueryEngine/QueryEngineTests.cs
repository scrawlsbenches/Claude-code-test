using FluentAssertions;
using HotSwap.KnowledgeGraph.Domain.Models;
using HotSwap.KnowledgeGraph.QueryEngine;
using HotSwap.KnowledgeGraph.Infrastructure.Repositories;
using Moq;

namespace HotSwap.KnowledgeGraph.Tests.QueryEngine;

/// <summary>
/// Tests for the query engine implementation.
/// </summary>
public class QueryEngineTests
{
    private readonly Mock<IGraphRepository> _mockRepository;

    public QueryEngineTests()
    {
        _mockRepository = new Mock<IGraphRepository>();
    }

    [Fact]
    public async Task ExecuteQueryAsync_WithEntityTypeFilter_ReturnsMatchingEntities()
    {
        // Arrange
        var entities = new List<Entity>
        {
            new Entity
            {
                Id = Guid.NewGuid(),
                Type = "Person",
                Properties = new Dictionary<string, object> { ["name"] = "Alice" },
                CreatedAt = DateTimeOffset.UtcNow
            },
            new Entity
            {
                Id = Guid.NewGuid(),
                Type = "Person",
                Properties = new Dictionary<string, object> { ["name"] = "Bob" },
                CreatedAt = DateTimeOffset.UtcNow
            }
        };

        var query = new GraphQuery
        {
            EntityType = "Person",
            PageSize = 100,
            Skip = 0
        };

        var expectedResult = new GraphQueryResult
        {
            Entities = entities,
            Relationships = new List<Relationship>(),
            TotalCount = 2,
            ExecutionTime = TimeSpan.FromMilliseconds(10)
        };

        _mockRepository
            .Setup(r => r.ExecuteQueryAsync(It.IsAny<GraphQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        var engine = new GraphQueryEngine(_mockRepository.Object);

        // Act
        var result = await engine.ExecuteQueryAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.Entities.Should().HaveCount(2);
        result.Entities.Should().AllSatisfy(e => e.Type.Should().Be("Person"));
        result.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task ExecuteQueryAsync_WithPropertyFilter_ReturnsMatchingEntities()
    {
        // Arrange
        var entity = new Entity
        {
            Id = Guid.NewGuid(),
            Type = "Person",
            Properties = new Dictionary<string, object>
            {
                ["name"] = "Alice",
                ["age"] = 30
            },
            CreatedAt = DateTimeOffset.UtcNow
        };

        var query = new GraphQuery
        {
            EntityType = "Person",
            PropertyFilters = new Dictionary<string, object> { ["name"] = "Alice" },
            PageSize = 100,
            Skip = 0
        };

        var expectedResult = new GraphQueryResult
        {
            Entities = new List<Entity> { entity },
            Relationships = new List<Relationship>(),
            TotalCount = 1,
            ExecutionTime = TimeSpan.FromMilliseconds(5)
        };

        _mockRepository
            .Setup(r => r.ExecuteQueryAsync(It.IsAny<GraphQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        var engine = new GraphQueryEngine(_mockRepository.Object);

        // Act
        var result = await engine.ExecuteQueryAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.Entities.Should().HaveCount(1);
        result.Entities[0].Properties["name"].Should().Be("Alice");
    }

    [Fact]
    public async Task ExecuteQueryAsync_WithPagination_ReturnsPagedResults()
    {
        // Arrange
        var query = new GraphQuery
        {
            EntityType = "Person",
            PageSize = 10,
            Skip = 20
        };

        var expectedResult = new GraphQueryResult
        {
            Entities = new List<Entity>(),
            Relationships = new List<Relationship>(),
            TotalCount = 100,
            ExecutionTime = TimeSpan.FromMilliseconds(15)
        };

        _mockRepository
            .Setup(r => r.ExecuteQueryAsync(It.IsAny<GraphQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        var engine = new GraphQueryEngine(_mockRepository.Object);

        // Act
        var result = await engine.ExecuteQueryAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.TotalCount.Should().Be(100);
        _mockRepository.Verify(r => r.ExecuteQueryAsync(
            It.Is<GraphQuery>(q => q.PageSize == 10 && q.Skip == 20),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteQueryAsync_WithTimeout_CompletesWithinTimeout()
    {
        // Arrange
        var query = new GraphQuery
        {
            EntityType = "Person",
            Timeout = TimeSpan.FromSeconds(5)
        };

        var expectedResult = new GraphQueryResult
        {
            Entities = new List<Entity>(),
            Relationships = new List<Relationship>(),
            TotalCount = 0,
            ExecutionTime = TimeSpan.FromMilliseconds(50)
        };

        _mockRepository
            .Setup(r => r.ExecuteQueryAsync(It.IsAny<GraphQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        var engine = new GraphQueryEngine(_mockRepository.Object);

        // Act
        var result = await engine.ExecuteQueryAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.ExecutionTime.Should().BeLessThan(query.Timeout);
    }

    [Fact]
    public async Task ExecuteQueryAsync_WithCancellationToken_PropagatesToken()
    {
        // Arrange
        var query = new GraphQuery { EntityType = "Person" };
        var cts = new CancellationTokenSource();

        var expectedResult = new GraphQueryResult
        {
            Entities = new List<Entity>(),
            Relationships = new List<Relationship>(),
            TotalCount = 0,
            ExecutionTime = TimeSpan.FromMilliseconds(10)
        };

        _mockRepository
            .Setup(r => r.ExecuteQueryAsync(It.IsAny<GraphQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        var engine = new GraphQueryEngine(_mockRepository.Object);

        // Act
        var result = await engine.ExecuteQueryAsync(query, cts.Token);

        // Assert
        result.Should().NotBeNull();
        _mockRepository.Verify(r => r.ExecuteQueryAsync(
            It.IsAny<GraphQuery>(),
            cts.Token), Times.Once);
    }

    [Fact]
    public async Task ExecuteQueryAsync_WithNullQuery_ThrowsArgumentNullException()
    {
        // Arrange
        var engine = new GraphQueryEngine(_mockRepository.Object);

        // Act
        Func<Task> act = async () => await engine.ExecuteQueryAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("query");
    }

    [Fact]
    public void Constructor_WithNullRepository_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => new GraphQueryEngine(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("repository");
    }

    [Fact]
    public async Task ExecuteQueryAsync_WithEmptyResult_ReturnsEmptyResult()
    {
        // Arrange
        var query = new GraphQuery { EntityType = "NonExistent" };

        var expectedResult = new GraphQueryResult
        {
            Entities = new List<Entity>(),
            Relationships = new List<Relationship>(),
            TotalCount = 0,
            ExecutionTime = TimeSpan.FromMilliseconds(5)
        };

        _mockRepository
            .Setup(r => r.ExecuteQueryAsync(It.IsAny<GraphQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        var engine = new GraphQueryEngine(_mockRepository.Object);

        // Act
        var result = await engine.ExecuteQueryAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.Entities.Should().BeEmpty();
        result.Relationships.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }
}
