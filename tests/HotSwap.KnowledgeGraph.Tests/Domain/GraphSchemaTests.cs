using FluentAssertions;
using HotSwap.KnowledgeGraph.Domain.Models;

namespace HotSwap.KnowledgeGraph.Tests.Domain;

public class GraphSchemaTests
{
    [Fact]
    public void Constructor_WithValidParameters_CreatesGraphSchema()
    {
        // Arrange
        var version = "1.0.0";
        var entityTypes = new Dictionary<string, EntityTypeDefinition>
        {
            ["Person"] = new EntityTypeDefinition
            {
                Name = "Person",
                Properties = new Dictionary<string, PropertyDefinition>
                {
                    ["name"] = new PropertyDefinition { Name = "name", Type = "String", IsRequired = true }
                }
            }
        };
        var relationshipTypes = new Dictionary<string, RelationshipTypeDefinition>
        {
            ["KNOWS"] = new RelationshipTypeDefinition
            {
                Name = "KNOWS",
                AllowedSourceTypes = new List<string> { "Person" },
                AllowedTargetTypes = new List<string> { "Person" }
            }
        };

        // Act
        var schema = new GraphSchema
        {
            Version = version,
            EntityTypes = entityTypes,
            RelationshipTypes = relationshipTypes,
            CreatedAt = DateTimeOffset.UtcNow
        };

        // Assert
        schema.Version.Should().Be(version);
        schema.EntityTypes.Should().HaveCount(1);
        schema.RelationshipTypes.Should().HaveCount(1);
    }

    [Fact]
    public void Version_WithInvalidSemanticVersion_ThrowsArgumentException()
    {
        // Arrange & Act
        Action act = () => new GraphSchema
        {
            Version = "invalid",
            EntityTypes = new Dictionary<string, EntityTypeDefinition>(),
            RelationshipTypes = new Dictionary<string, RelationshipTypeDefinition>(),
            CreatedAt = DateTimeOffset.UtcNow
        };

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*semantic version*");
    }

    [Fact]
    public void Version_WithValidSemanticVersion_IsAccepted()
    {
        // Arrange & Act
        var schema = new GraphSchema
        {
            Version = "2.1.3",
            EntityTypes = new Dictionary<string, EntityTypeDefinition>(),
            RelationshipTypes = new Dictionary<string, RelationshipTypeDefinition>(),
            CreatedAt = DateTimeOffset.UtcNow
        };

        // Assert
        schema.Version.Should().Be("2.1.3");
    }

    [Fact]
    public void IsCompatibleWith_WithSameMajorVersion_ReturnsTrue()
    {
        // Arrange
        var schema1 = new GraphSchema
        {
            Version = "1.0.0",
            EntityTypes = new Dictionary<string, EntityTypeDefinition>(),
            RelationshipTypes = new Dictionary<string, RelationshipTypeDefinition>(),
            CreatedAt = DateTimeOffset.UtcNow
        };
        var schema2 = new GraphSchema
        {
            Version = "1.2.5",
            EntityTypes = new Dictionary<string, EntityTypeDefinition>(),
            RelationshipTypes = new Dictionary<string, RelationshipTypeDefinition>(),
            CreatedAt = DateTimeOffset.UtcNow
        };

        // Act
        var isCompatible = schema1.IsCompatibleWith(schema2);

        // Assert
        isCompatible.Should().BeTrue();
    }

    [Fact]
    public void IsCompatibleWith_WithDifferentMajorVersion_ReturnsFalse()
    {
        // Arrange
        var schema1 = new GraphSchema
        {
            Version = "1.0.0",
            EntityTypes = new Dictionary<string, EntityTypeDefinition>(),
            RelationshipTypes = new Dictionary<string, RelationshipTypeDefinition>(),
            CreatedAt = DateTimeOffset.UtcNow
        };
        var schema2 = new GraphSchema
        {
            Version = "2.0.0",
            EntityTypes = new Dictionary<string, EntityTypeDefinition>(),
            RelationshipTypes = new Dictionary<string, RelationshipTypeDefinition>(),
            CreatedAt = DateTimeOffset.UtcNow
        };

        // Act
        var isCompatible = schema1.IsCompatibleWith(schema2);

        // Assert
        isCompatible.Should().BeFalse();
    }

    [Fact]
    public void EntityTypeDefinition_WithRequiredProperties_ValidatesCorrectly()
    {
        // Arrange & Act
        var entityType = new EntityTypeDefinition
        {
            Name = "Document",
            Properties = new Dictionary<string, PropertyDefinition>
            {
                ["title"] = new PropertyDefinition { Name = "title", Type = "String", IsRequired = true },
                ["content"] = new PropertyDefinition { Name = "content", Type = "String", IsRequired = false }
            },
            Indexes = new List<string> { "title" }
        };

        // Assert
        entityType.Name.Should().Be("Document");
        entityType.Properties.Should().HaveCount(2);
        entityType.Properties["title"].IsRequired.Should().BeTrue();
        entityType.Properties["content"].IsRequired.Should().BeFalse();
        entityType.Indexes.Should().Contain("title");
    }

    [Fact]
    public void PropertyDefinition_WithValidationType_StoresValidationRules()
    {
        // Arrange & Act
        var property = new PropertyDefinition
        {
            Name = "email",
            Type = "String",
            IsRequired = true,
            ValidationPattern = @"^[^@]+@[^@]+\.[^@]+$"
        };

        // Assert
        property.Name.Should().Be("email");
        property.Type.Should().Be("String");
        property.IsRequired.Should().BeTrue();
        property.ValidationPattern.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void RelationshipTypeDefinition_WithAllowedTypes_ValidatesCorrectly()
    {
        // Arrange & Act
        var relationshipType = new RelationshipTypeDefinition
        {
            Name = "AUTHORED_BY",
            AllowedSourceTypes = new List<string> { "Document", "Article" },
            AllowedTargetTypes = new List<string> { "Person", "Organization" },
            IsDirected = true
        };

        // Assert
        relationshipType.Name.Should().Be("AUTHORED_BY");
        relationshipType.AllowedSourceTypes.Should().HaveCount(2);
        relationshipType.AllowedTargetTypes.Should().HaveCount(2);
        relationshipType.IsDirected.Should().BeTrue();
    }

    [Fact]
    public void ValidateEntity_WithValidEntity_ReturnsTrue()
    {
        // Arrange
        var schema = new GraphSchema
        {
            Version = "1.0.0",
            EntityTypes = new Dictionary<string, EntityTypeDefinition>
            {
                ["Person"] = new EntityTypeDefinition
                {
                    Name = "Person",
                    Properties = new Dictionary<string, PropertyDefinition>
                    {
                        ["name"] = new PropertyDefinition { Name = "name", Type = "String", IsRequired = true }
                    }
                }
            },
            RelationshipTypes = new Dictionary<string, RelationshipTypeDefinition>(),
            CreatedAt = DateTimeOffset.UtcNow
        };

        var entity = new Entity
        {
            Id = Guid.NewGuid(),
            Type = "Person",
            Properties = new Dictionary<string, object> { ["name"] = "Alice" },
            CreatedAt = DateTimeOffset.UtcNow
        };

        // Act
        var isValid = schema.ValidateEntity(entity);

        // Assert
        isValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateEntity_WithMissingRequiredProperty_ReturnsFalse()
    {
        // Arrange
        var schema = new GraphSchema
        {
            Version = "1.0.0",
            EntityTypes = new Dictionary<string, EntityTypeDefinition>
            {
                ["Person"] = new EntityTypeDefinition
                {
                    Name = "Person",
                    Properties = new Dictionary<string, PropertyDefinition>
                    {
                        ["name"] = new PropertyDefinition { Name = "name", Type = "String", IsRequired = true }
                    }
                }
            },
            RelationshipTypes = new Dictionary<string, RelationshipTypeDefinition>(),
            CreatedAt = DateTimeOffset.UtcNow
        };

        var entity = new Entity
        {
            Id = Guid.NewGuid(),
            Type = "Person",
            Properties = new Dictionary<string, object>(), // Missing required "name"
            CreatedAt = DateTimeOffset.UtcNow
        };

        // Act
        var isValid = schema.ValidateEntity(entity);

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public void ValidateEntity_WithUndefinedEntityType_ReturnsFalse()
    {
        // Arrange
        var schema = new GraphSchema
        {
            Version = "1.0.0",
            EntityTypes = new Dictionary<string, EntityTypeDefinition>(),
            RelationshipTypes = new Dictionary<string, RelationshipTypeDefinition>(),
            CreatedAt = DateTimeOffset.UtcNow
        };

        var entity = new Entity
        {
            Id = Guid.NewGuid(),
            Type = "UnknownType",
            Properties = new Dictionary<string, object>(),
            CreatedAt = DateTimeOffset.UtcNow
        };

        // Act
        var isValid = schema.ValidateEntity(entity);

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public void ValidateRelationship_WithValidRelationship_ReturnsTrue()
    {
        // Arrange
        var schema = new GraphSchema
        {
            Version = "1.0.0",
            EntityTypes = new Dictionary<string, EntityTypeDefinition>
            {
                ["Person"] = new EntityTypeDefinition { Name = "Person", Properties = new Dictionary<string, PropertyDefinition>() }
            },
            RelationshipTypes = new Dictionary<string, RelationshipTypeDefinition>
            {
                ["KNOWS"] = new RelationshipTypeDefinition
                {
                    Name = "KNOWS",
                    AllowedSourceTypes = new List<string> { "Person" },
                    AllowedTargetTypes = new List<string> { "Person" },
                    IsDirected = false
                }
            },
            CreatedAt = DateTimeOffset.UtcNow
        };

        var relationship = new Relationship
        {
            Id = Guid.NewGuid(),
            Type = "KNOWS",
            SourceEntityId = Guid.NewGuid(),
            TargetEntityId = Guid.NewGuid(),
            Properties = new Dictionary<string, object>(),
            CreatedAt = DateTimeOffset.UtcNow
        };

        // Act
        var isValid = schema.ValidateRelationship(relationship, "Person", "Person");

        // Assert
        isValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateRelationship_WithInvalidSourceType_ReturnsFalse()
    {
        // Arrange
        var schema = new GraphSchema
        {
            Version = "1.0.0",
            EntityTypes = new Dictionary<string, EntityTypeDefinition>(),
            RelationshipTypes = new Dictionary<string, RelationshipTypeDefinition>
            {
                ["AUTHORED_BY"] = new RelationshipTypeDefinition
                {
                    Name = "AUTHORED_BY",
                    AllowedSourceTypes = new List<string> { "Document" },
                    AllowedTargetTypes = new List<string> { "Person" }
                }
            },
            CreatedAt = DateTimeOffset.UtcNow
        };

        var relationship = new Relationship
        {
            Id = Guid.NewGuid(),
            Type = "AUTHORED_BY",
            SourceEntityId = Guid.NewGuid(),
            TargetEntityId = Guid.NewGuid(),
            Properties = new Dictionary<string, object>(),
            CreatedAt = DateTimeOffset.UtcNow
        };

        // Act - Invalid source type
        var isValid = schema.ValidateRelationship(relationship, "Person", "Person");

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public void Version_WithEmptyString_ThrowsArgumentException()
    {
        // Arrange & Act
        Action act = () => new GraphSchema
        {
            Version = "",
            EntityTypes = new Dictionary<string, EntityTypeDefinition>(),
            RelationshipTypes = new Dictionary<string, RelationshipTypeDefinition>(),
            CreatedAt = DateTimeOffset.UtcNow
        };

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*version*empty*");
    }

    [Fact]
    public void IsCompatibleWith_WithNull_ReturnsFalse()
    {
        // Arrange
        var schema = new GraphSchema
        {
            Version = "1.0.0",
            EntityTypes = new Dictionary<string, EntityTypeDefinition>(),
            RelationshipTypes = new Dictionary<string, RelationshipTypeDefinition>(),
            CreatedAt = DateTimeOffset.UtcNow
        };

        // Act
        var isCompatible = schema.IsCompatibleWith(null!);

        // Assert
        isCompatible.Should().BeFalse();
    }

    [Fact]
    public void ValidateEntity_WithNull_ReturnsFalse()
    {
        // Arrange
        var schema = new GraphSchema
        {
            Version = "1.0.0",
            EntityTypes = new Dictionary<string, EntityTypeDefinition>(),
            RelationshipTypes = new Dictionary<string, RelationshipTypeDefinition>(),
            CreatedAt = DateTimeOffset.UtcNow
        };

        // Act
        var isValid = schema.ValidateEntity(null!);

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public void ValidateRelationship_WithNull_ReturnsFalse()
    {
        // Arrange
        var schema = new GraphSchema
        {
            Version = "1.0.0",
            EntityTypes = new Dictionary<string, EntityTypeDefinition>(),
            RelationshipTypes = new Dictionary<string, RelationshipTypeDefinition>(),
            CreatedAt = DateTimeOffset.UtcNow
        };

        // Act
        var isValid = schema.ValidateRelationship(null!, "Person", "Person");

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public void ValidateRelationship_WithUndefinedType_ReturnsFalse()
    {
        // Arrange
        var schema = new GraphSchema
        {
            Version = "1.0.0",
            EntityTypes = new Dictionary<string, EntityTypeDefinition>(),
            RelationshipTypes = new Dictionary<string, RelationshipTypeDefinition>(),
            CreatedAt = DateTimeOffset.UtcNow
        };

        var relationship = new Relationship
        {
            Id = Guid.NewGuid(),
            Type = "UNDEFINED_TYPE",
            SourceEntityId = Guid.NewGuid(),
            TargetEntityId = Guid.NewGuid(),
            Properties = new Dictionary<string, object>(),
            CreatedAt = DateTimeOffset.UtcNow
        };

        // Act
        var isValid = schema.ValidateRelationship(relationship, "Person", "Person");

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public void ValidateRelationship_WithInvalidTargetType_ReturnsFalse()
    {
        // Arrange
        var schema = new GraphSchema
        {
            Version = "1.0.0",
            EntityTypes = new Dictionary<string, EntityTypeDefinition>(),
            RelationshipTypes = new Dictionary<string, RelationshipTypeDefinition>
            {
                ["KNOWS"] = new RelationshipTypeDefinition
                {
                    Name = "KNOWS",
                    AllowedSourceTypes = new List<string> { "Person" },
                    AllowedTargetTypes = new List<string> { "Person" }
                }
            },
            CreatedAt = DateTimeOffset.UtcNow
        };

        var relationship = new Relationship
        {
            Id = Guid.NewGuid(),
            Type = "KNOWS",
            SourceEntityId = Guid.NewGuid(),
            TargetEntityId = Guid.NewGuid(),
            Properties = new Dictionary<string, object>(),
            CreatedAt = DateTimeOffset.UtcNow
        };

        // Act
        var isValid = schema.ValidateRelationship(relationship, "Person", "Document");

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public void CreatedBy_CanBeNull()
    {
        // Arrange & Act
        var schema = new GraphSchema
        {
            Version = "1.0.0",
            EntityTypes = new Dictionary<string, EntityTypeDefinition>(),
            RelationshipTypes = new Dictionary<string, RelationshipTypeDefinition>(),
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = null
        };

        // Assert
        schema.CreatedBy.Should().BeNull();
    }

    [Fact]
    public void PropertyDefinition_WithNumericValidation_StoresRules()
    {
        // Arrange & Act
        var property = new PropertyDefinition
        {
            Name = "age",
            Type = "Integer",
            IsRequired = true,
            MinValue = 0,
            MaxValue = 150
        };

        // Assert
        property.MinValue.Should().Be(0);
        property.MaxValue.Should().Be(150);
    }

    [Fact]
    public void PropertyDefinition_WithDescription_StoresDescription()
    {
        // Arrange & Act
        var property = new PropertyDefinition
        {
            Name = "email",
            Type = "String",
            Description = "User email address"
        };

        // Assert
        property.Description.Should().Be("User email address");
    }

    [Fact]
    public void EntityTypeDefinition_WithDescription_StoresDescription()
    {
        // Arrange & Act
        var entityType = new EntityTypeDefinition
        {
            Name = "Person",
            Properties = new Dictionary<string, PropertyDefinition>(),
            Description = "Represents a person entity"
        };

        // Assert
        entityType.Description.Should().Be("Represents a person entity");
    }

    [Fact]
    public void EntityTypeDefinition_Indexes_CanBeNull()
    {
        // Arrange & Act
        var entityType = new EntityTypeDefinition
        {
            Name = "Person",
            Properties = new Dictionary<string, PropertyDefinition>(),
            Indexes = null
        };

        // Assert
        entityType.Indexes.Should().BeNull();
    }

    [Fact]
    public void RelationshipTypeDefinition_WithDescription_StoresDescription()
    {
        // Arrange & Act
        var relType = new RelationshipTypeDefinition
        {
            Name = "KNOWS",
            AllowedSourceTypes = new List<string> { "Person" },
            AllowedTargetTypes = new List<string> { "Person" },
            Description = "Represents acquaintance"
        };

        // Assert
        relType.Description.Should().Be("Represents acquaintance");
    }

    [Fact]
    public void RelationshipTypeDefinition_Properties_CanBeNull()
    {
        // Arrange & Act
        var relType = new RelationshipTypeDefinition
        {
            Name = "KNOWS",
            AllowedSourceTypes = new List<string> { "Person" },
            AllowedTargetTypes = new List<string> { "Person" },
            Properties = null
        };

        // Assert
        relType.Properties.Should().BeNull();
    }

    [Fact]
    public void RelationshipTypeDefinition_IsDirected_DefaultsToTrue()
    {
        // Arrange & Act
        var relType = new RelationshipTypeDefinition
        {
            Name = "AUTHORED_BY",
            AllowedSourceTypes = new List<string> { "Document" },
            AllowedTargetTypes = new List<string> { "Person" }
        };

        // Assert
        relType.IsDirected.Should().BeTrue();
    }

    [Fact]
    public void RelationshipTypeDefinition_IsDirected_CanBeFalse()
    {
        // Arrange & Act
        var relType = new RelationshipTypeDefinition
        {
            Name = "FRIENDS_WITH",
            AllowedSourceTypes = new List<string> { "Person" },
            AllowedTargetTypes = new List<string> { "Person" },
            IsDirected = false
        };

        // Assert
        relType.IsDirected.Should().BeFalse();
    }
}
