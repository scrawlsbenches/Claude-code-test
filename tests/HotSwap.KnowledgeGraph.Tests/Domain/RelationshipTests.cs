using FluentAssertions;
using HotSwap.KnowledgeGraph.Domain.Models;

namespace HotSwap.KnowledgeGraph.Tests.Domain;

public class RelationshipTests
{
    [Fact]
    public void Constructor_WithValidParameters_CreatesRelationship()
    {
        // Arrange
        var id = Guid.NewGuid();
        var type = "AUTHORED_BY";
        var sourceId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var properties = new Dictionary<string, object> { ["role"] = "primary" };
        var weight = 1.5;
        var createdAt = DateTimeOffset.UtcNow;
        var createdBy = "user@example.com";

        // Act
        var relationship = new Relationship
        {
            Id = id,
            Type = type,
            SourceEntityId = sourceId,
            TargetEntityId = targetId,
            Properties = properties,
            Weight = weight,
            IsDirected = true,
            CreatedAt = createdAt,
            CreatedBy = createdBy
        };

        // Assert
        relationship.Id.Should().Be(id);
        relationship.Type.Should().Be(type);
        relationship.SourceEntityId.Should().Be(sourceId);
        relationship.TargetEntityId.Should().Be(targetId);
        relationship.Properties.Should().BeEquivalentTo(properties);
        relationship.Weight.Should().Be(weight);
        relationship.IsDirected.Should().BeTrue();
        relationship.CreatedAt.Should().Be(createdAt);
        relationship.CreatedBy.Should().Be(createdBy);
    }

    [Fact]
    public void Type_WithEmptyString_ThrowsArgumentException()
    {
        // Arrange & Act
        Action act = () => new Relationship
        {
            Id = Guid.NewGuid(),
            Type = "",
            SourceEntityId = Guid.NewGuid(),
            TargetEntityId = Guid.NewGuid(),
            Properties = new Dictionary<string, object>(),
            CreatedAt = DateTimeOffset.UtcNow
        };

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Type*");
    }

    [Fact]
    public void Type_WithMoreThan100Characters_ThrowsArgumentException()
    {
        // Arrange
        var longType = new string('A', 101);

        // Act
        Action act = () => new Relationship
        {
            Id = Guid.NewGuid(),
            Type = longType,
            SourceEntityId = Guid.NewGuid(),
            TargetEntityId = Guid.NewGuid(),
            Properties = new Dictionary<string, object>(),
            CreatedAt = DateTimeOffset.UtcNow
        };

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Type*100*");
    }

    [Fact]
    public void Type_WithNonAlphanumeric_ThrowsArgumentException()
    {
        // Arrange & Act
        Action act = () => new Relationship
        {
            Id = Guid.NewGuid(),
            Type = "INVALID-TYPE!",
            SourceEntityId = Guid.NewGuid(),
            TargetEntityId = Guid.NewGuid(),
            Properties = new Dictionary<string, object>(),
            CreatedAt = DateTimeOffset.UtcNow
        };

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*alphanumeric*");
    }

    [Fact]
    public void Weight_WithNegativeValue_ThrowsArgumentException()
    {
        // Arrange & Act
        Action act = () => new Relationship
        {
            Id = Guid.NewGuid(),
            Type = "RELATED_TO",
            SourceEntityId = Guid.NewGuid(),
            TargetEntityId = Guid.NewGuid(),
            Properties = new Dictionary<string, object>(),
            Weight = -1.0,
            CreatedAt = DateTimeOffset.UtcNow
        };

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Weight*negative*");
    }

    [Fact]
    public void Weight_WithZero_IsValid()
    {
        // Arrange & Act
        var relationship = new Relationship
        {
            Id = Guid.NewGuid(),
            Type = "WEAK_LINK",
            SourceEntityId = Guid.NewGuid(),
            TargetEntityId = Guid.NewGuid(),
            Properties = new Dictionary<string, object>(),
            Weight = 0.0,
            CreatedAt = DateTimeOffset.UtcNow
        };

        // Assert
        relationship.Weight.Should().Be(0.0);
    }

    [Fact]
    public void IsDirected_DefaultsToTrue()
    {
        // Arrange & Act
        var relationship = new Relationship
        {
            Id = Guid.NewGuid(),
            Type = "KNOWS",
            SourceEntityId = Guid.NewGuid(),
            TargetEntityId = Guid.NewGuid(),
            Properties = new Dictionary<string, object>(),
            CreatedAt = DateTimeOffset.UtcNow
        };

        // Assert
        relationship.IsDirected.Should().BeTrue();
    }

    [Fact]
    public void Equals_WithSameId_ReturnsTrue()
    {
        // Arrange
        var id = Guid.NewGuid();
        var relationship1 = new Relationship
        {
            Id = id,
            Type = "AUTHORED_BY",
            SourceEntityId = Guid.NewGuid(),
            TargetEntityId = Guid.NewGuid(),
            Properties = new Dictionary<string, object>(),
            CreatedAt = DateTimeOffset.UtcNow
        };
        var relationship2 = new Relationship
        {
            Id = id,
            Type = "RELATED_TO", // Different type
            SourceEntityId = Guid.NewGuid(),
            TargetEntityId = Guid.NewGuid(),
            Properties = new Dictionary<string, object>(),
            CreatedAt = DateTimeOffset.UtcNow
        };

        // Act & Assert
        relationship1.Equals(relationship2).Should().BeTrue();
        (relationship1 == relationship2).Should().BeTrue();
    }

    [Fact]
    public void Equals_WithDifferentId_ReturnsFalse()
    {
        // Arrange
        var relationship1 = new Relationship
        {
            Id = Guid.NewGuid(),
            Type = "KNOWS",
            SourceEntityId = Guid.NewGuid(),
            TargetEntityId = Guid.NewGuid(),
            Properties = new Dictionary<string, object>(),
            CreatedAt = DateTimeOffset.UtcNow
        };
        var relationship2 = new Relationship
        {
            Id = Guid.NewGuid(),
            Type = "KNOWS",
            SourceEntityId = Guid.NewGuid(),
            TargetEntityId = Guid.NewGuid(),
            Properties = new Dictionary<string, object>(),
            CreatedAt = DateTimeOffset.UtcNow
        };

        // Act & Assert
        relationship1.Equals(relationship2).Should().BeFalse();
        (relationship1 != relationship2).Should().BeTrue();
    }

    [Fact]
    public void GetHashCode_WithSameId_ReturnsSameHashCode()
    {
        // Arrange
        var id = Guid.NewGuid();
        var relationship1 = new Relationship
        {
            Id = id,
            Type = "KNOWS",
            SourceEntityId = Guid.NewGuid(),
            TargetEntityId = Guid.NewGuid(),
            Properties = new Dictionary<string, object>(),
            CreatedAt = DateTimeOffset.UtcNow
        };
        var relationship2 = new Relationship
        {
            Id = id,
            Type = "KNOWS",
            SourceEntityId = Guid.NewGuid(),
            TargetEntityId = Guid.NewGuid(),
            Properties = new Dictionary<string, object>(),
            CreatedAt = DateTimeOffset.UtcNow
        };

        // Act & Assert
        relationship1.GetHashCode().Should().Be(relationship2.GetHashCode());
    }

    [Fact]
    public void ToString_WithDirectedRelationship_ReturnsFormattedString()
    {
        // Arrange
        var id = Guid.NewGuid();
        var sourceId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var relationship = new Relationship
        {
            Id = id,
            Type = "AUTHORED_BY",
            SourceEntityId = sourceId,
            TargetEntityId = targetId,
            Properties = new Dictionary<string, object>(),
            Weight = 2.5,
            IsDirected = true,
            CreatedAt = DateTimeOffset.UtcNow
        };

        // Act
        var result = relationship.ToString();

        // Assert
        result.Should().Contain("AUTHORED_BY");
        result.Should().Contain(sourceId.ToString());
        result.Should().Contain(targetId.ToString());
        result.Should().Contain("→");
        result.Should().Contain("Weight=2.5");
    }

    [Fact]
    public void ToString_WithUndirectedRelationship_ReturnsFormattedString()
    {
        // Arrange
        var sourceId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var relationship = new Relationship
        {
            Id = Guid.NewGuid(),
            Type = "FRIENDS_WITH",
            SourceEntityId = sourceId,
            TargetEntityId = targetId,
            Properties = new Dictionary<string, object>(),
            Weight = 1.0,
            IsDirected = false,
            CreatedAt = DateTimeOffset.UtcNow
        };

        // Act
        var result = relationship.ToString();

        // Assert
        result.Should().Contain("FRIENDS_WITH");
        result.Should().Contain(sourceId.ToString());
        result.Should().Contain(targetId.ToString());
        result.Should().Contain("↔");
    }

    [Fact]
    public void Equals_WithNull_ReturnsFalse()
    {
        // Arrange
        var relationship = new Relationship
        {
            Id = Guid.NewGuid(),
            Type = "KNOWS",
            SourceEntityId = Guid.NewGuid(),
            TargetEntityId = Guid.NewGuid(),
            Properties = new Dictionary<string, object>(),
            CreatedAt = DateTimeOffset.UtcNow
        };

        // Act & Assert
        relationship.Equals(null).Should().BeFalse();
        (relationship == null).Should().BeFalse();
    }

    [Fact]
    public void Equals_WithSameReference_ReturnsTrue()
    {
        // Arrange
        var relationship = new Relationship
        {
            Id = Guid.NewGuid(),
            Type = "KNOWS",
            SourceEntityId = Guid.NewGuid(),
            TargetEntityId = Guid.NewGuid(),
            Properties = new Dictionary<string, object>(),
            CreatedAt = DateTimeOffset.UtcNow
        };

        // Act & Assert
        relationship.Equals(relationship).Should().BeTrue();
        (relationship == relationship).Should().BeTrue();
    }

    [Fact]
    public void Equals_ObjectOverride_WithSameId_ReturnsTrue()
    {
        // Arrange
        var id = Guid.NewGuid();
        var relationship1 = new Relationship
        {
            Id = id,
            Type = "KNOWS",
            SourceEntityId = Guid.NewGuid(),
            TargetEntityId = Guid.NewGuid(),
            Properties = new Dictionary<string, object>(),
            CreatedAt = DateTimeOffset.UtcNow
        };
        object relationship2 = new Relationship
        {
            Id = id,
            Type = "KNOWS",
            SourceEntityId = Guid.NewGuid(),
            TargetEntityId = Guid.NewGuid(),
            Properties = new Dictionary<string, object>(),
            CreatedAt = DateTimeOffset.UtcNow
        };

        // Act & Assert
        relationship1.Equals(relationship2).Should().BeTrue();
    }

    [Fact]
    public void Equals_ObjectOverride_WithNonRelationship_ReturnsFalse()
    {
        // Arrange
        var relationship = new Relationship
        {
            Id = Guid.NewGuid(),
            Type = "KNOWS",
            SourceEntityId = Guid.NewGuid(),
            TargetEntityId = Guid.NewGuid(),
            Properties = new Dictionary<string, object>(),
            CreatedAt = DateTimeOffset.UtcNow
        };
        object notARelationship = "not a relationship";

        // Act & Assert
        relationship.Equals(notARelationship).Should().BeFalse();
    }

    [Fact]
    public void OperatorEquals_WithBothNull_ReturnsTrue()
    {
        // Arrange
        Relationship? relationship1 = null;
        Relationship? relationship2 = null;

        // Act & Assert
        (relationship1 == relationship2).Should().BeTrue();
    }

    [Fact]
    public void OperatorNotEquals_WithDifferentIds_ReturnsTrue()
    {
        // Arrange
        var relationship1 = new Relationship
        {
            Id = Guid.NewGuid(),
            Type = "KNOWS",
            SourceEntityId = Guid.NewGuid(),
            TargetEntityId = Guid.NewGuid(),
            Properties = new Dictionary<string, object>(),
            CreatedAt = DateTimeOffset.UtcNow
        };
        var relationship2 = new Relationship
        {
            Id = Guid.NewGuid(),
            Type = "KNOWS",
            SourceEntityId = Guid.NewGuid(),
            TargetEntityId = Guid.NewGuid(),
            Properties = new Dictionary<string, object>(),
            CreatedAt = DateTimeOffset.UtcNow
        };

        // Act & Assert
        (relationship1 != relationship2).Should().BeTrue();
    }

    [Fact]
    public void CreatedBy_CanBeNull()
    {
        // Arrange & Act
        var relationship = new Relationship
        {
            Id = Guid.NewGuid(),
            Type = "KNOWS",
            SourceEntityId = Guid.NewGuid(),
            TargetEntityId = Guid.NewGuid(),
            Properties = new Dictionary<string, object>(),
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = null
        };

        // Assert
        relationship.CreatedBy.Should().BeNull();
    }

    [Fact]
    public void Type_WithUnderscores_IsValid()
    {
        // Arrange & Act
        var relationship = new Relationship
        {
            Id = Guid.NewGuid(),
            Type = "RELATIONSHIP_TYPE_WITH_UNDERSCORES",
            SourceEntityId = Guid.NewGuid(),
            TargetEntityId = Guid.NewGuid(),
            Properties = new Dictionary<string, object>(),
            CreatedAt = DateTimeOffset.UtcNow
        };

        // Assert
        relationship.Type.Should().Be("RELATIONSHIP_TYPE_WITH_UNDERSCORES");
    }

    [Fact]
    public void IsDirected_CanBeFalse()
    {
        // Arrange & Act
        var relationship = new Relationship
        {
            Id = Guid.NewGuid(),
            Type = "FRIENDS_WITH",
            SourceEntityId = Guid.NewGuid(),
            TargetEntityId = Guid.NewGuid(),
            Properties = new Dictionary<string, object>(),
            IsDirected = false,
            CreatedAt = DateTimeOffset.UtcNow
        };

        // Assert
        relationship.IsDirected.Should().BeFalse();
    }

    [Fact]
    public void Properties_WithEmptyDictionary_IsValid()
    {
        // Arrange & Act
        var relationship = new Relationship
        {
            Id = Guid.NewGuid(),
            Type = "KNOWS",
            SourceEntityId = Guid.NewGuid(),
            TargetEntityId = Guid.NewGuid(),
            Properties = new Dictionary<string, object>(),
            CreatedAt = DateTimeOffset.UtcNow
        };

        // Assert
        relationship.Properties.Should().NotBeNull();
        relationship.Properties.Should().BeEmpty();
    }
}
