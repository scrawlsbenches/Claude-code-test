using FluentAssertions;
using HotSwap.KnowledgeGraph.Domain.Models;

namespace HotSwap.KnowledgeGraph.Tests.Domain;

public class EntityTests
{
    [Fact]
    public void Constructor_WithValidParameters_CreatesEntity()
    {
        // Arrange
        var id = Guid.NewGuid();
        var type = "Person";
        var properties = new Dictionary<string, object> { ["name"] = "Alice" };
        var createdAt = DateTimeOffset.UtcNow;
        var createdBy = "user@example.com";

        // Act
        var entity = new Entity
        {
            Id = id,
            Type = type,
            Properties = properties,
            CreatedAt = createdAt,
            CreatedBy = createdBy
        };

        // Assert
        entity.Id.Should().Be(id);
        entity.Type.Should().Be(type);
        entity.Properties.Should().BeEquivalentTo(properties);
        entity.CreatedAt.Should().Be(createdAt);
        entity.CreatedBy.Should().Be(createdBy);
        entity.Version.Should().Be(1); // Default version
    }

    [Fact]
    public void Type_WithEmptyString_ThrowsArgumentException()
    {
        // Arrange & Act
        Action act = () => new Entity
        {
            Id = Guid.NewGuid(),
            Type = "",
            Properties = new Dictionary<string, object>(),
            CreatedAt = DateTimeOffset.UtcNow
        };

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Type*");
    }

    [Fact]
    public void Type_WithWhitespace_ThrowsArgumentException()
    {
        // Arrange & Act
        Action act = () => new Entity
        {
            Id = Guid.NewGuid(),
            Type = "   ",
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
        Action act = () => new Entity
        {
            Id = Guid.NewGuid(),
            Type = longType,
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
        Action act = () => new Entity
        {
            Id = Guid.NewGuid(),
            Type = "Invalid-Type!",
            Properties = new Dictionary<string, object>(),
            CreatedAt = DateTimeOffset.UtcNow
        };

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*alphanumeric*");
    }

    [Fact]
    public void Equals_WithSameId_ReturnsTrue()
    {
        // Arrange
        var id = Guid.NewGuid();
        var entity1 = new Entity
        {
            Id = id,
            Type = "Person",
            Properties = new Dictionary<string, object> { ["name"] = "Alice" },
            CreatedAt = DateTimeOffset.UtcNow
        };
        var entity2 = new Entity
        {
            Id = id,
            Type = "Document", // Different type
            Properties = new Dictionary<string, object> { ["title"] = "Test" },
            CreatedAt = DateTimeOffset.UtcNow
        };

        // Act & Assert
        entity1.Equals(entity2).Should().BeTrue();
        (entity1 == entity2).Should().BeTrue();
    }

    [Fact]
    public void Equals_WithDifferentId_ReturnsFalse()
    {
        // Arrange
        var entity1 = new Entity
        {
            Id = Guid.NewGuid(),
            Type = "Person",
            Properties = new Dictionary<string, object>(),
            CreatedAt = DateTimeOffset.UtcNow
        };
        var entity2 = new Entity
        {
            Id = Guid.NewGuid(),
            Type = "Person",
            Properties = new Dictionary<string, object>(),
            CreatedAt = DateTimeOffset.UtcNow
        };

        // Act & Assert
        entity1.Equals(entity2).Should().BeFalse();
        (entity1 != entity2).Should().BeTrue();
    }

    [Fact]
    public void GetHashCode_WithSameId_ReturnsSameHashCode()
    {
        // Arrange
        var id = Guid.NewGuid();
        var entity1 = new Entity
        {
            Id = id,
            Type = "Person",
            Properties = new Dictionary<string, object>(),
            CreatedAt = DateTimeOffset.UtcNow
        };
        var entity2 = new Entity
        {
            Id = id,
            Type = "Person",
            Properties = new Dictionary<string, object>(),
            CreatedAt = DateTimeOffset.UtcNow
        };

        // Act & Assert
        entity1.GetHashCode().Should().Be(entity2.GetHashCode());
    }

    [Fact]
    public void Properties_WithEmptyDictionary_IsValid()
    {
        // Arrange & Act
        var entity = new Entity
        {
            Id = Guid.NewGuid(),
            Type = "Person",
            Properties = new Dictionary<string, object>(),
            CreatedAt = DateTimeOffset.UtcNow
        };

        // Assert
        entity.Properties.Should().NotBeNull();
        entity.Properties.Should().BeEmpty();
    }

    [Fact]
    public void UpdatedAt_CanBeModified()
    {
        // Arrange
        var entity = new Entity
        {
            Id = Guid.NewGuid(),
            Type = "Person",
            Properties = new Dictionary<string, object>(),
            CreatedAt = DateTimeOffset.UtcNow
        };
        var newUpdatedAt = DateTimeOffset.UtcNow.AddHours(1);

        // Act
        entity.UpdatedAt = newUpdatedAt;

        // Assert
        entity.UpdatedAt.Should().Be(newUpdatedAt);
    }

    [Fact]
    public void UpdatedBy_CanBeModified()
    {
        // Arrange
        var entity = new Entity
        {
            Id = Guid.NewGuid(),
            Type = "Person",
            Properties = new Dictionary<string, object>(),
            CreatedAt = DateTimeOffset.UtcNow
        };

        // Act
        entity.UpdatedBy = "updater@example.com";

        // Assert
        entity.UpdatedBy.Should().Be("updater@example.com");
    }

    [Fact]
    public void Version_CanBeModified()
    {
        // Arrange
        var entity = new Entity
        {
            Id = Guid.NewGuid(),
            Type = "Person",
            Properties = new Dictionary<string, object>(),
            CreatedAt = DateTimeOffset.UtcNow
        };

        // Act
        entity.Version = 5;

        // Assert
        entity.Version.Should().Be(5);
    }

    [Fact]
    public void Equals_WithNull_ReturnsFalse()
    {
        // Arrange
        var entity = new Entity
        {
            Id = Guid.NewGuid(),
            Type = "Person",
            Properties = new Dictionary<string, object>(),
            CreatedAt = DateTimeOffset.UtcNow
        };

        // Act & Assert
        entity.Equals(null).Should().BeFalse();
        (entity == null).Should().BeFalse();
    }

    [Fact]
    public void Equals_WithSameReference_ReturnsTrue()
    {
        // Arrange
        var entity = new Entity
        {
            Id = Guid.NewGuid(),
            Type = "Person",
            Properties = new Dictionary<string, object>(),
            CreatedAt = DateTimeOffset.UtcNow
        };

        // Act & Assert
        entity.Equals(entity).Should().BeTrue();
#pragma warning disable CS1718 // Comparison made to same variable - intentional for testing reference equality
        (entity == entity).Should().BeTrue();
#pragma warning restore CS1718
    }

    [Fact]
    public void Equals_ObjectOverride_WithSameId_ReturnsTrue()
    {
        // Arrange
        var id = Guid.NewGuid();
        var entity1 = new Entity
        {
            Id = id,
            Type = "Person",
            Properties = new Dictionary<string, object>(),
            CreatedAt = DateTimeOffset.UtcNow
        };
        object entity2 = new Entity
        {
            Id = id,
            Type = "Person",
            Properties = new Dictionary<string, object>(),
            CreatedAt = DateTimeOffset.UtcNow
        };

        // Act & Assert
        entity1.Equals(entity2).Should().BeTrue();
    }

    [Fact]
    public void Equals_ObjectOverride_WithNonEntity_ReturnsFalse()
    {
        // Arrange
        var entity = new Entity
        {
            Id = Guid.NewGuid(),
            Type = "Person",
            Properties = new Dictionary<string, object>(),
            CreatedAt = DateTimeOffset.UtcNow
        };
        object notAnEntity = "not an entity";

        // Act & Assert
        entity.Equals(notAnEntity).Should().BeFalse();
    }

    [Fact]
    public void OperatorEquals_WithBothNull_ReturnsTrue()
    {
        // Arrange
        Entity? entity1 = null;
        Entity? entity2 = null;

        // Act & Assert
        (entity1 == entity2).Should().BeTrue();
    }

    [Fact]
    public void OperatorNotEquals_WithDifferentIds_ReturnsTrue()
    {
        // Arrange
        var entity1 = new Entity
        {
            Id = Guid.NewGuid(),
            Type = "Person",
            Properties = new Dictionary<string, object>(),
            CreatedAt = DateTimeOffset.UtcNow
        };
        var entity2 = new Entity
        {
            Id = Guid.NewGuid(),
            Type = "Person",
            Properties = new Dictionary<string, object>(),
            CreatedAt = DateTimeOffset.UtcNow
        };

        // Act & Assert
        (entity1 != entity2).Should().BeTrue();
    }

    [Fact]
    public void ToString_ReturnsFormattedString()
    {
        // Arrange
        var id = Guid.NewGuid();
        var entity = new Entity
        {
            Id = id,
            Type = "Person",
            Properties = new Dictionary<string, object> { ["name"] = "Alice", ["age"] = 30 },
            CreatedAt = DateTimeOffset.UtcNow,
            Version = 3
        };

        // Act
        var result = entity.ToString();

        // Assert
        result.Should().Contain("Person");
        result.Should().Contain(id.ToString());
        result.Should().Contain("Properties=2");
        result.Should().Contain("Version=3");
    }

    [Fact]
    public void CreatedBy_CanBeNull()
    {
        // Arrange & Act
        var entity = new Entity
        {
            Id = Guid.NewGuid(),
            Type = "Person",
            Properties = new Dictionary<string, object>(),
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = null
        };

        // Assert
        entity.CreatedBy.Should().BeNull();
    }

    [Fact]
    public void Type_WithUnderscores_IsValid()
    {
        // Arrange & Act
        var entity = new Entity
        {
            Id = Guid.NewGuid(),
            Type = "Entity_Type_With_Underscores",
            Properties = new Dictionary<string, object>(),
            CreatedAt = DateTimeOffset.UtcNow
        };

        // Assert
        entity.Type.Should().Be("Entity_Type_With_Underscores");
    }
}
