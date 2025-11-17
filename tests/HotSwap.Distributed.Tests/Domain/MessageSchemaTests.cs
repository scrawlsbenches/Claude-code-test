using FluentAssertions;
using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;
using Xunit;

namespace HotSwap.Distributed.Tests.Domain;

public class MessageSchemaTests
{
    [Fact]
    public void IsValid_WithValidSchema_ReturnsTrue()
    {
        // Arrange
        var schema = new MessageSchema
        {
            SchemaId = "test.schema.v1",
            SchemaDefinition = "{\"type\":\"object\"}"
        };

        // Act
        var result = schema.IsValid(out var errors);

        // Assert
        result.Should().BeTrue();
        errors.Should().BeEmpty();
    }

    [Fact]
    public void IsValid_WithMissingSchemaId_ReturnsFalse()
    {
        // Arrange
        var schema = new MessageSchema
        {
            SchemaId = "",
            SchemaDefinition = "{\"type\":\"object\"}"
        };

        // Act
        var result = schema.IsValid(out var errors);

        // Assert
        result.Should().BeFalse();
        errors.Should().Contain("SchemaId is required");
    }

    [Fact]
    public void IsValid_WithMissingSchemaDefinition_ReturnsFalse()
    {
        // Arrange
        var schema = new MessageSchema
        {
            SchemaId = "test.schema.v1",
            SchemaDefinition = ""
        };

        // Act
        var result = schema.IsValid(out var errors);

        // Assert
        result.Should().BeFalse();
        errors.Should().Contain("SchemaDefinition is required");
    }

    [Fact]
    public void IsValid_WithInvalidJson_ReturnsFalse()
    {
        // Arrange
        var schema = new MessageSchema
        {
            SchemaId = "test.schema.v1",
            SchemaDefinition = "not-valid-json"
        };

        // Act
        var result = schema.IsValid(out var errors);

        // Assert
        result.Should().BeFalse();
        errors.Should().Contain("SchemaDefinition must be valid JSON");
    }

    [Fact]
    public void IsApproved_WithApprovedStatus_ReturnsTrue()
    {
        // Arrange
        var schema = new MessageSchema
        {
            SchemaId = "test.schema.v1",
            SchemaDefinition = "{\"type\":\"object\"}",
            Status = SchemaStatus.Approved
        };

        // Act
        var result = schema.IsApproved();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsApproved_WithDraftStatus_ReturnsFalse()
    {
        // Arrange
        var schema = new MessageSchema
        {
            SchemaId = "test.schema.v1",
            SchemaDefinition = "{\"type\":\"object\"}",
            Status = SchemaStatus.Draft
        };

        // Act
        var result = schema.IsApproved();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsDeprecated_WithDeprecatedStatus_ReturnsTrue()
    {
        // Arrange
        var schema = new MessageSchema
        {
            SchemaId = "test.schema.v1",
            SchemaDefinition = "{\"type\":\"object\"}",
            Status = SchemaStatus.Deprecated
        };

        // Act
        var result = schema.IsDeprecated();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsDeprecated_WithApprovedStatus_ReturnsFalse()
    {
        // Arrange
        var schema = new MessageSchema
        {
            SchemaId = "test.schema.v1",
            SchemaDefinition = "{\"type\":\"object\"}",
            Status = SchemaStatus.Approved
        };

        // Act
        var result = schema.IsDeprecated();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void MessageSchema_DefaultVersion_ShouldBe1Point0()
    {
        // Arrange & Act
        var schema = new MessageSchema
        {
            SchemaId = "test.schema.v1",
            SchemaDefinition = "{\"type\":\"object\"}"
        };

        // Assert
        schema.Version.Should().Be("1.0");
    }

    [Fact]
    public void MessageSchema_DefaultCompatibility_ShouldBeNone()
    {
        // Arrange & Act
        var schema = new MessageSchema
        {
            SchemaId = "test.schema.v1",
            SchemaDefinition = "{\"type\":\"object\"}"
        };

        // Assert
        schema.Compatibility.Should().Be(SchemaCompatibility.None);
    }

    [Fact]
    public void MessageSchema_DefaultStatus_ShouldBeDraft()
    {
        // Arrange & Act
        var schema = new MessageSchema
        {
            SchemaId = "test.schema.v1",
            SchemaDefinition = "{\"type\":\"object\"}"
        };

        // Assert
        schema.Status.Should().Be(SchemaStatus.Draft);
    }
}
