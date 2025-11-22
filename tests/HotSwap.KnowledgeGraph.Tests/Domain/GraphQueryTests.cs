using FluentAssertions;
using HotSwap.KnowledgeGraph.Domain.Enums;
using HotSwap.KnowledgeGraph.Domain.Models;

namespace HotSwap.KnowledgeGraph.Tests.Domain;

public class GraphQueryTests
{
    [Fact]
    public void Constructor_WithValidParameters_CreatesGraphQuery()
    {
        // Arrange & Act
        var query = new GraphQuery
        {
            EntityType = "Person",
            PropertyFilters = new Dictionary<string, object> { ["name"] = "Alice" },
            MaxDepth = 3,
            PageSize = 50,
            Timeout = TimeSpan.FromSeconds(30)
        };

        // Assert
        query.EntityType.Should().Be("Person");
        query.PropertyFilters.Should().HaveCount(1);
        query.MaxDepth.Should().Be(3);
        query.PageSize.Should().Be(50);
        query.Timeout.Should().Be(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void DefaultValues_AreSetCorrectly()
    {
        // Arrange & Act
        var query = new GraphQuery();

        // Assert
        query.MaxDepth.Should().Be(3);
        query.PageSize.Should().Be(100);
        query.Timeout.Should().Be(TimeSpan.FromSeconds(30));
        query.Skip.Should().Be(0);
    }

    [Fact]
    public void RelationshipPatterns_CanBeAdded()
    {
        // Arrange
        var pattern = new RelationshipPattern
        {
            RelationshipType = "KNOWS",
            Direction = Direction.Outgoing,
            TargetEntityType = "Person"
        };

        // Act
        var query = new GraphQuery
        {
            RelationshipPatterns = new List<RelationshipPattern> { pattern }
        };

        // Assert
        query.RelationshipPatterns.Should().HaveCount(1);
        query.RelationshipPatterns![0].RelationshipType.Should().Be("KNOWS");
        query.RelationshipPatterns[0].Direction.Should().Be(Direction.Outgoing);
    }

    [Fact]
    public void PageSize_WithNegativeValue_ThrowsArgumentException()
    {
        // Arrange & Act
        Action act = () => new GraphQuery { PageSize = -1 };

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*PageSize*negative*");
    }

    [Fact]
    public void MaxDepth_WithNegativeValue_ThrowsArgumentException()
    {
        // Arrange & Act
        Action act = () => new GraphQuery { MaxDepth = -1 };

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*MaxDepth*negative*");
    }

    [Fact]
    public void Skip_WithNegativeValue_ThrowsArgumentException()
    {
        // Arrange & Act
        Action act = () => new GraphQuery { Skip = -1 };

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Skip*negative*");
    }
}

public class RelationshipPatternTests
{
    [Fact]
    public void Constructor_WithValidParameters_CreatesPattern()
    {
        // Arrange & Act
        var pattern = new RelationshipPattern
        {
            RelationshipType = "AUTHORED_BY",
            Direction = Direction.Incoming,
            TargetEntityType = "Person",
            PropertyFilters = new Dictionary<string, object> { ["role"] = "primary" }
        };

        // Assert
        pattern.RelationshipType.Should().Be("AUTHORED_BY");
        pattern.Direction.Should().Be(Direction.Incoming);
        pattern.TargetEntityType.Should().Be("Person");
        pattern.PropertyFilters.Should().HaveCount(1);
    }

    [Fact]
    public void Direction_DefaultsToOutgoing()
    {
        // Arrange & Act
        var pattern = new RelationshipPattern();

        // Assert
        pattern.Direction.Should().Be(Direction.Outgoing);
    }
}

public class GraphQueryResultTests
{
    [Fact]
    public void Constructor_WithValidData_CreatesResult()
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
            }
        };
        var relationships = new List<Relationship>
        {
            new Relationship
            {
                Id = Guid.NewGuid(),
                Type = "KNOWS",
                SourceEntityId = Guid.NewGuid(),
                TargetEntityId = Guid.NewGuid(),
                Properties = new Dictionary<string, object>(),
                CreatedAt = DateTimeOffset.UtcNow
            }
        };

        // Act
        var result = new GraphQueryResult
        {
            Entities = entities,
            Relationships = relationships,
            TotalCount = 1,
            ExecutionTime = TimeSpan.FromMilliseconds(150)
        };

        // Assert
        result.Entities.Should().HaveCount(1);
        result.Relationships.Should().HaveCount(1);
        result.TotalCount.Should().Be(1);
        result.ExecutionTime.Should().Be(TimeSpan.FromMilliseconds(150));
    }

    [Fact]
    public void DefaultValues_AreSetCorrectly()
    {
        // Arrange & Act
        var result = new GraphQueryResult();

        // Assert
        result.Entities.Should().BeEmpty();
        result.Relationships.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
        result.QueryPlan.Should().BeNull();
    }
}
