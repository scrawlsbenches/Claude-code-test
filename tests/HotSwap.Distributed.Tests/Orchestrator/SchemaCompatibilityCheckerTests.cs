using FluentAssertions;
using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Orchestrator.Interfaces;
using HotSwap.Distributed.Orchestrator.Schema;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HotSwap.Distributed.Tests.Orchestrator;

public class SchemaCompatibilityCheckerTests
{
    private readonly SchemaCompatibilityChecker _checker;

    public SchemaCompatibilityCheckerTests()
    {
        _checker = new SchemaCompatibilityChecker(NullLogger<SchemaCompatibilityChecker>.Instance);
    }

    private MessageSchema CreateTestSchema(string schemaDefinition)
    {
        return new MessageSchema
        {
            SchemaId = "test.schema.v1",
            SchemaDefinition = schemaDefinition,
            Version = "1.0",
            Status = SchemaStatus.Approved,
            Compatibility = SchemaCompatibility.Backward
        };
    }

    #region Backward Compatibility Tests

    [Fact]
    public async Task CheckBackwardCompatibility_WithAddedOptionalField_IsCompatible()
    {
        // Arrange - old schema has fewer fields
        var oldSchema = CreateTestSchema(@"{
            ""type"": ""object"",
            ""properties"": {
                ""name"": { ""type"": ""string"" }
            },
            ""required"": [""name""]
        }");

        var newSchema = CreateTestSchema(@"{
            ""type"": ""object"",
            ""properties"": {
                ""name"": { ""type"": ""string"" },
                ""email"": { ""type"": ""string"" }
            },
            ""required"": [""name""]
        }");

        // Act
        var result = await _checker.CheckBackwardCompatibilityAsync(oldSchema, newSchema);

        // Assert
        result.IsCompatible.Should().BeTrue();
        result.BreakingChanges.Should().BeEmpty();
    }

    [Fact]
    public async Task CheckBackwardCompatibility_WithAddedRequiredField_IsIncompatible()
    {
        // Arrange
        var oldSchema = CreateTestSchema(@"{
            ""type"": ""object"",
            ""properties"": {
                ""name"": { ""type"": ""string"" }
            },
            ""required"": [""name""]
        }");

        var newSchema = CreateTestSchema(@"{
            ""type"": ""object"",
            ""properties"": {
                ""name"": { ""type"": ""string"" },
                ""email"": { ""type"": ""string"" }
            },
            ""required"": [""name"", ""email""]
        }");

        // Act
        var result = await _checker.CheckBackwardCompatibilityAsync(oldSchema, newSchema);

        // Assert
        result.IsCompatible.Should().BeFalse();
        result.BreakingChanges.Should().Contain(c =>
            c.ChangeType == BreakingChangeType.AddedRequiredField &&
            c.Path.Contains("email"));
    }

    [Fact]
    public async Task CheckBackwardCompatibility_WithRemovedField_IsCompatible()
    {
        // Arrange - removing fields is backward compatible
        var oldSchema = CreateTestSchema(@"{
            ""type"": ""object"",
            ""properties"": {
                ""name"": { ""type"": ""string"" },
                ""age"": { ""type"": ""integer"" }
            }
        }");

        var newSchema = CreateTestSchema(@"{
            ""type"": ""object"",
            ""properties"": {
                ""name"": { ""type"": ""string"" }
            }
        }");

        // Act
        var result = await _checker.CheckBackwardCompatibilityAsync(oldSchema, newSchema);

        // Assert
        result.IsCompatible.Should().BeTrue();
    }

    [Fact]
    public async Task CheckBackwardCompatibility_WithTypeChange_IsIncompatible()
    {
        // Arrange
        var oldSchema = CreateTestSchema(@"{
            ""type"": ""object"",
            ""properties"": {
                ""age"": { ""type"": ""integer"" }
            }
        }");

        var newSchema = CreateTestSchema(@"{
            ""type"": ""object"",
            ""properties"": {
                ""age"": { ""type"": ""string"" }
            }
        }");

        // Act
        var result = await _checker.CheckBackwardCompatibilityAsync(oldSchema, newSchema);

        // Assert
        result.IsCompatible.Should().BeFalse();
        result.BreakingChanges.Should().Contain(c =>
            c.ChangeType == BreakingChangeType.TypeChanged &&
            c.Path.Contains("age"));
    }

    #endregion

    #region Forward Compatibility Tests

    [Fact]
    public async Task CheckForwardCompatibility_WithAddedOptionalField_IsCompatible()
    {
        // Arrange - old schema should handle new optional fields
        var oldSchema = CreateTestSchema(@"{
            ""type"": ""object"",
            ""properties"": {
                ""name"": { ""type"": ""string"" }
            },
            ""required"": [""name""]
        }");

        var newSchema = CreateTestSchema(@"{
            ""type"": ""object"",
            ""properties"": {
                ""name"": { ""type"": ""string"" },
                ""email"": { ""type"": ""string"" }
            },
            ""required"": [""name""]
        }");

        // Act
        var result = await _checker.CheckForwardCompatibilityAsync(oldSchema, newSchema);

        // Assert
        result.IsCompatible.Should().BeTrue();
    }

    [Fact]
    public async Task CheckForwardCompatibility_WithRemovedRequiredField_IsIncompatible()
    {
        // Arrange - old schema expects this field
        var oldSchema = CreateTestSchema(@"{
            ""type"": ""object"",
            ""properties"": {
                ""name"": { ""type"": ""string"" },
                ""email"": { ""type"": ""string"" }
            },
            ""required"": [""name"", ""email""]
        }");

        var newSchema = CreateTestSchema(@"{
            ""type"": ""object"",
            ""properties"": {
                ""name"": { ""type"": ""string"" }
            },
            ""required"": [""name""]
        }");

        // Act
        var result = await _checker.CheckForwardCompatibilityAsync(oldSchema, newSchema);

        // Assert
        result.IsCompatible.Should().BeFalse();
        result.BreakingChanges.Should().Contain(c =>
            c.ChangeType == BreakingChangeType.RemovedField &&
            c.Path.Contains("email"));
    }

    #endregion

    #region Full Compatibility Tests

    [Fact]
    public async Task CheckFullCompatibility_WithNoChanges_IsCompatible()
    {
        // Arrange
        var schema = CreateTestSchema(@"{
            ""type"": ""object"",
            ""properties"": {
                ""name"": { ""type"": ""string"" }
            }
        }");

        // Act
        var result = await _checker.CheckFullCompatibilityAsync(schema, schema);

        // Assert
        result.IsCompatible.Should().BeTrue();
    }

    [Fact]
    public async Task CheckFullCompatibility_WithAddedOptionalField_IsCompatible()
    {
        // Arrange
        var oldSchema = CreateTestSchema(@"{
            ""type"": ""object"",
            ""properties"": {
                ""name"": { ""type"": ""string"" }
            }
        }");

        var newSchema = CreateTestSchema(@"{
            ""type"": ""object"",
            ""properties"": {
                ""name"": { ""type"": ""string"" },
                ""email"": { ""type"": ""string"" }
            }
        }");

        // Act
        var result = await _checker.CheckFullCompatibilityAsync(oldSchema, newSchema);

        // Assert
        result.IsCompatible.Should().BeTrue();
    }

    [Fact]
    public async Task CheckFullCompatibility_WithAddedRequiredField_IsIncompatible()
    {
        // Arrange
        var oldSchema = CreateTestSchema(@"{
            ""type"": ""object"",
            ""properties"": {
                ""name"": { ""type"": ""string"" }
            }
        }");

        var newSchema = CreateTestSchema(@"{
            ""type"": ""object"",
            ""properties"": {
                ""name"": { ""type"": ""string"" },
                ""email"": { ""type"": ""string"" }
            },
            ""required"": [""email""]
        }");

        // Act
        var result = await _checker.CheckFullCompatibilityAsync(oldSchema, newSchema);

        // Assert
        result.IsCompatible.Should().BeFalse();
    }

    #endregion

    #region CheckCompatibilityAsync Tests

    [Fact]
    public async Task CheckCompatibility_WithBackwardMode_CallsBackwardCheck()
    {
        // Arrange
        var oldSchema = CreateTestSchema(@"{ ""type"": ""object"", ""properties"": { ""name"": { ""type"": ""string"" } } }");
        var newSchema = CreateTestSchema(@"{ ""type"": ""object"", ""properties"": { ""name"": { ""type"": ""string"" }, ""age"": { ""type"": ""integer"" } } }");

        // Act
        var result = await _checker.CheckCompatibilityAsync(oldSchema, newSchema, SchemaCompatibility.Backward);

        // Assert
        result.IsCompatible.Should().BeTrue();
        result.CompatibilityMode.Should().Be(SchemaCompatibility.Backward);
    }

    [Fact]
    public async Task CheckCompatibility_WithForwardMode_CallsForwardCheck()
    {
        // Arrange
        var oldSchema = CreateTestSchema(@"{ ""type"": ""object"", ""properties"": { ""name"": { ""type"": ""string"" } } }");
        var newSchema = CreateTestSchema(@"{ ""type"": ""object"", ""properties"": { ""name"": { ""type"": ""string"" } } }");

        // Act
        var result = await _checker.CheckCompatibilityAsync(oldSchema, newSchema, SchemaCompatibility.Forward);

        // Assert
        result.CompatibilityMode.Should().Be(SchemaCompatibility.Forward);
    }

    [Fact]
    public async Task CheckCompatibility_WithFullMode_CallsFullCheck()
    {
        // Arrange
        var oldSchema = CreateTestSchema(@"{ ""type"": ""object"", ""properties"": { ""name"": { ""type"": ""string"" } } }");
        var newSchema = CreateTestSchema(@"{ ""type"": ""object"", ""properties"": { ""name"": { ""type"": ""string"" } } }");

        // Act
        var result = await _checker.CheckCompatibilityAsync(oldSchema, newSchema, SchemaCompatibility.Full);

        // Assert
        result.CompatibilityMode.Should().Be(SchemaCompatibility.Full);
    }

    [Fact]
    public async Task CheckCompatibility_WithNoneMode_ReturnsCompatible()
    {
        // Arrange - None mode means no compatibility checking
        var oldSchema = CreateTestSchema(@"{ ""type"": ""object"", ""properties"": { ""name"": { ""type"": ""string"" } } }");
        var newSchema = CreateTestSchema(@"{ ""type"": ""object"", ""properties"": { ""age"": { ""type"": ""integer"" } }, ""required"": [""age""] }");

        // Act
        var result = await _checker.CheckCompatibilityAsync(oldSchema, newSchema, SchemaCompatibility.None);

        // Assert
        result.IsCompatible.Should().BeTrue();
        result.CompatibilityMode.Should().Be(SchemaCompatibility.None);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task CheckBackwardCompatibility_WithNestedObjectChanges_DetectsBreakingChanges()
    {
        // Arrange
        var oldSchema = CreateTestSchema(@"{
            ""type"": ""object"",
            ""properties"": {
                ""user"": {
                    ""type"": ""object"",
                    ""properties"": {
                        ""name"": { ""type"": ""string"" }
                    }
                }
            }
        }");

        var newSchema = CreateTestSchema(@"{
            ""type"": ""object"",
            ""properties"": {
                ""user"": {
                    ""type"": ""object"",
                    ""properties"": {
                        ""name"": { ""type"": ""string"" },
                        ""email"": { ""type"": ""string"" }
                    },
                    ""required"": [""email""]
                }
            }
        }");

        // Act
        var result = await _checker.CheckBackwardCompatibilityAsync(oldSchema, newSchema);

        // Assert
        result.IsCompatible.Should().BeFalse();
        result.BreakingChanges.Should().Contain(c => c.Path.Contains("email"));
    }

    [Fact]
    public async Task CheckBackwardCompatibility_WithArrayItemTypeChange_IsIncompatible()
    {
        // Arrange
        var oldSchema = CreateTestSchema(@"{
            ""type"": ""object"",
            ""properties"": {
                ""items"": {
                    ""type"": ""array"",
                    ""items"": { ""type"": ""string"" }
                }
            }
        }");

        var newSchema = CreateTestSchema(@"{
            ""type"": ""object"",
            ""properties"": {
                ""items"": {
                    ""type"": ""array"",
                    ""items"": { ""type"": ""integer"" }
                }
            }
        }");

        // Act
        var result = await _checker.CheckBackwardCompatibilityAsync(oldSchema, newSchema);

        // Assert
        result.IsCompatible.Should().BeFalse();
        result.BreakingChanges.Should().Contain(c => c.ChangeType == BreakingChangeType.TypeChanged);
    }

    [Fact]
    public async Task CheckBackwardCompatibility_WithEnumValueRemoved_IsIncompatible()
    {
        // Arrange
        var oldSchema = CreateTestSchema(@"{
            ""type"": ""object"",
            ""properties"": {
                ""status"": {
                    ""type"": ""string"",
                    ""enum"": [""draft"", ""published"", ""archived""]
                }
            }
        }");

        var newSchema = CreateTestSchema(@"{
            ""type"": ""object"",
            ""properties"": {
                ""status"": {
                    ""type"": ""string"",
                    ""enum"": [""draft"", ""published""]
                }
            }
        }");

        // Act
        var result = await _checker.CheckBackwardCompatibilityAsync(oldSchema, newSchema);

        // Assert
        result.IsCompatible.Should().BeFalse();
        result.BreakingChanges.Should().Contain(c => c.ChangeType == BreakingChangeType.RemovedEnumValue);
    }

    [Fact]
    public async Task CheckBackwardCompatibility_WithConstraintNarrowed_IsIncompatible()
    {
        // Arrange - minLength increased (more restrictive)
        var oldSchema = CreateTestSchema(@"{
            ""type"": ""object"",
            ""properties"": {
                ""code"": {
                    ""type"": ""string"",
                    ""minLength"": 2
                }
            }
        }");

        var newSchema = CreateTestSchema(@"{
            ""type"": ""object"",
            ""properties"": {
                ""code"": {
                    ""type"": ""string"",
                    ""minLength"": 5
                }
            }
        }");

        // Act
        var result = await _checker.CheckBackwardCompatibilityAsync(oldSchema, newSchema);

        // Assert
        result.IsCompatible.Should().BeFalse();
        result.BreakingChanges.Should().Contain(c => c.ChangeType == BreakingChangeType.ConstraintNarrowed);
    }

    #endregion
}
