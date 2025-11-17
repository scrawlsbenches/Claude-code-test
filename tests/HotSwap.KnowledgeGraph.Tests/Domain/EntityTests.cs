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
}
