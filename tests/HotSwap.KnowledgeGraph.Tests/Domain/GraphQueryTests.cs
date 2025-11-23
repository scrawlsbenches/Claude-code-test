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

    [Fact]
    public void EntityType_CanBeNull()
    {
        // Arrange & Act
        var query = new GraphQuery { EntityType = null };

        // Assert
        query.EntityType.Should().BeNull();
    }

    [Fact]
    public void PropertyFilters_CanBeNull()
    {
        // Arrange & Act
        var query = new GraphQuery { PropertyFilters = null };

        // Assert
        query.PropertyFilters.Should().BeNull();
    }

    [Fact]
    public void RelationshipPatterns_CanBeNull()
    {
        // Arrange & Act
        var query = new GraphQuery { RelationshipPatterns = null };

        // Assert
        query.RelationshipPatterns.Should().BeNull();
    }

    [Fact]
    public void TraceId_CanBeSet()
    {
        // Arrange & Act
        var query = new GraphQuery { TraceId = "trace-123" };

        // Assert
        query.TraceId.Should().Be("trace-123");
    }

    [Fact]
    public void TraceId_CanBeNull()
    {
        // Arrange & Act
        var query = new GraphQuery { TraceId = null };

        // Assert
        query.TraceId.Should().BeNull();
    }

    [Fact]
    public void Timeout_CanBeCustomized()
    {
        // Arrange & Act
        var query = new GraphQuery { Timeout = TimeSpan.FromMinutes(5) };

        // Assert
        query.Timeout.Should().Be(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public void PageSize_WithZero_IsValid()
    {
        // Arrange & Act
        var query = new GraphQuery { PageSize = 0 };

        // Assert
        query.PageSize.Should().Be(0);
    }

    [Fact]
    public void Skip_WithPositiveValue_IsValid()
    {
        // Arrange & Act
        var query = new GraphQuery { Skip = 50 };

        // Assert
        query.Skip.Should().Be(50);
    }

    [Fact]
    public void MaxDepth_WithZero_IsValid()
    {
        // Arrange & Act
        var query = new GraphQuery { MaxDepth = 0 };

        // Assert
        query.MaxDepth.Should().Be(0);
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

    [Fact]
    public void RelationshipType_CanBeNull()
    {
        // Arrange & Act
        var pattern = new RelationshipPattern { RelationshipType = null };

        // Assert
        pattern.RelationshipType.Should().BeNull();
    }

    [Fact]
    public void TargetEntityType_CanBeNull()
    {
        // Arrange & Act
        var pattern = new RelationshipPattern { TargetEntityType = null };

        // Assert
        pattern.TargetEntityType.Should().BeNull();
    }

    [Fact]
    public void PropertyFilters_CanBeNull()
    {
        // Arrange & Act
        var pattern = new RelationshipPattern { PropertyFilters = null };

        // Assert
        pattern.PropertyFilters.Should().BeNull();
    }

    [Fact]
    public void MinWeight_CanBeSet()
    {
        // Arrange & Act
        var pattern = new RelationshipPattern { MinWeight = 0.5 };

        // Assert
        pattern.MinWeight.Should().Be(0.5);
    }

    [Fact]
    public void MaxWeight_CanBeSet()
    {
        // Arrange & Act
        var pattern = new RelationshipPattern { MaxWeight = 10.0 };

        // Assert
        pattern.MaxWeight.Should().Be(10.0);
    }

    [Fact]
    public void MinWeight_CanBeNull()
    {
        // Arrange & Act
        var pattern = new RelationshipPattern { MinWeight = null };

        // Assert
        pattern.MinWeight.Should().BeNull();
    }

    [Fact]
    public void MaxWeight_CanBeNull()
    {
        // Arrange & Act
        var pattern = new RelationshipPattern { MaxWeight = null };

        // Assert
        pattern.MaxWeight.Should().BeNull();
    }

    [Fact]
    public void Direction_CanBeIncoming()
    {
        // Arrange & Act
        var pattern = new RelationshipPattern { Direction = Direction.Incoming };

        // Assert
        pattern.Direction.Should().Be(Direction.Incoming);
    }

    [Fact]
    public void Direction_CanBeBoth()
    {
        // Arrange & Act
        var pattern = new RelationshipPattern { Direction = Direction.Both };

        // Assert
        pattern.Direction.Should().Be(Direction.Both);
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

    [Fact]
    public void TraceId_CanBeSet()
    {
        // Arrange & Act
        var result = new GraphQueryResult { TraceId = "trace-456" };

        // Assert
        result.TraceId.Should().Be("trace-456");
    }

    [Fact]
    public void QueryPlan_CanBeSet()
    {
        // Arrange & Act
        var result = new GraphQueryResult { QueryPlan = "Index scan on entities" };

        // Assert
        result.QueryPlan.Should().Be("Index scan on entities");
    }

    [Fact]
    public void FromCache_DefaultsToFalse()
    {
        // Arrange & Act
        var result = new GraphQueryResult();

        // Assert
        result.FromCache.Should().BeFalse();
    }

    [Fact]
    public void FromCache_CanBeTrue()
    {
        // Arrange & Act
        var result = new GraphQueryResult { FromCache = true };

        // Assert
        result.FromCache.Should().BeTrue();
    }

    [Fact]
    public void Warnings_CanBeSet()
    {
        // Arrange
        var warnings = new List<string> { "Query timeout", "Partial results" };

        // Act
        var result = new GraphQueryResult { Warnings = warnings };

        // Assert
        result.Warnings.Should().HaveCount(2);
        result.Warnings.Should().Contain("Query timeout");
    }

    [Fact]
    public void Warnings_CanBeNull()
    {
        // Arrange & Act
        var result = new GraphQueryResult { Warnings = null };

        // Assert
        result.Warnings.Should().BeNull();
    }
}

public class PathResultTests
{
    [Fact]
    public void Constructor_WithValidData_CreatesPathResult()
    {
        // Arrange
        var entities = new List<Entity>
        {
            new Entity { Id = Guid.NewGuid(), Type = "Person", Properties = new Dictionary<string, object>(), CreatedAt = DateTimeOffset.UtcNow },
            new Entity { Id = Guid.NewGuid(), Type = "Person", Properties = new Dictionary<string, object>(), CreatedAt = DateTimeOffset.UtcNow }
        };
        var relationships = new List<Relationship>
        {
            new Relationship
            {
                Id = Guid.NewGuid(),
                Type = "KNOWS",
                SourceEntityId = entities[0].Id,
                TargetEntityId = entities[1].Id,
                Properties = new Dictionary<string, object>(),
                Weight = 1.5,
                CreatedAt = DateTimeOffset.UtcNow
            }
        };

        // Act
        var pathResult = new PathResult
        {
            Entities = entities,
            Relationships = relationships,
            TotalWeight = 1.5
        };

        // Assert
        pathResult.Entities.Should().HaveCount(2);
        pathResult.Relationships.Should().HaveCount(1);
        pathResult.TotalWeight.Should().Be(1.5);
    }

    [Fact]
    public void Hops_ReturnsRelationshipCount()
    {
        // Arrange
        var relationships = new List<Relationship>
        {
            new Relationship { Id = Guid.NewGuid(), Type = "KNOWS", SourceEntityId = Guid.NewGuid(), TargetEntityId = Guid.NewGuid(), Properties = new Dictionary<string, object>(), CreatedAt = DateTimeOffset.UtcNow },
            new Relationship { Id = Guid.NewGuid(), Type = "KNOWS", SourceEntityId = Guid.NewGuid(), TargetEntityId = Guid.NewGuid(), Properties = new Dictionary<string, object>(), CreatedAt = DateTimeOffset.UtcNow }
        };

        var pathResult = new PathResult { Relationships = relationships };

        // Act & Assert
        pathResult.Hops.Should().Be(2);
    }

    [Fact]
    public void Length_ReturnsEntityCount()
    {
        // Arrange
        var entities = new List<Entity>
        {
            new Entity { Id = Guid.NewGuid(), Type = "Person", Properties = new Dictionary<string, object>(), CreatedAt = DateTimeOffset.UtcNow },
            new Entity { Id = Guid.NewGuid(), Type = "Person", Properties = new Dictionary<string, object>(), CreatedAt = DateTimeOffset.UtcNow },
            new Entity { Id = Guid.NewGuid(), Type = "Person", Properties = new Dictionary<string, object>(), CreatedAt = DateTimeOffset.UtcNow }
        };

        var pathResult = new PathResult { Entities = entities };

        // Act & Assert
        pathResult.Length.Should().Be(3);
    }

    [Fact]
    public void DefaultValues_AreSetCorrectly()
    {
        // Arrange & Act
        var pathResult = new PathResult();

        // Assert
        pathResult.Entities.Should().BeEmpty();
        pathResult.Relationships.Should().BeEmpty();
        pathResult.TotalWeight.Should().Be(0);
        pathResult.Hops.Should().Be(0);
        pathResult.Length.Should().Be(0);
    }

    [Fact]
    public void TotalWeight_CanBeZero()
    {
        // Arrange & Act
        var pathResult = new PathResult { TotalWeight = 0 };

        // Assert
        pathResult.TotalWeight.Should().Be(0);
    }
}
