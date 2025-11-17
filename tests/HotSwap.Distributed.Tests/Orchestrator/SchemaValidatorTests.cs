using FluentAssertions;
using HotSwap.Distributed.Orchestrator.Interfaces;
using HotSwap.Distributed.Orchestrator.Schema;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HotSwap.Distributed.Tests.Orchestrator;

public class SchemaValidatorTests
{
    private readonly SchemaValidator _validator;

    public SchemaValidatorTests()
    {
        _validator = new SchemaValidator(NullLogger<SchemaValidator>.Instance);
    }

    #region Valid Payload Tests

    [Fact]
    public async Task ValidateAsync_WithValidSimpleObject_ReturnsSuccess()
    {
        // Arrange
        var schema = @"{
            ""type"": ""object"",
            ""properties"": {
                ""name"": { ""type"": ""string"" },
                ""age"": { ""type"": ""integer"" }
            },
            ""required"": [""name""]
        }";

        var payload = @"{
            ""name"": ""John Doe"",
            ""age"": 30
        }";

        // Act
        var result = await _validator.ValidateAsync(payload, schema);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
        result.ValidationTimeMs.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task ValidateAsync_WithValidNestedObject_ReturnsSuccess()
    {
        // Arrange
        var schema = @"{
            ""type"": ""object"",
            ""properties"": {
                ""user"": {
                    ""type"": ""object"",
                    ""properties"": {
                        ""email"": { ""type"": ""string"", ""format"": ""email"" },
                        ""address"": {
                            ""type"": ""object"",
                            ""properties"": {
                                ""city"": { ""type"": ""string"" }
                            }
                        }
                    }
                }
            }
        }";

        var payload = @"{
            ""user"": {
                ""email"": ""john@example.com"",
                ""address"": {
                    ""city"": ""New York""
                }
            }
        }";

        // Act
        var result = await _validator.ValidateAsync(payload, schema);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_WithValidArray_ReturnsSuccess()
    {
        // Arrange
        var schema = @"{
            ""type"": ""object"",
            ""properties"": {
                ""items"": {
                    ""type"": ""array"",
                    ""items"": { ""type"": ""string"" }
                }
            }
        }";

        var payload = @"{
            ""items"": [""apple"", ""banana"", ""cherry""]
        }";

        // Act
        var result = await _validator.ValidateAsync(payload, schema);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    #endregion

    #region Invalid Type Tests

    [Fact]
    public async Task ValidateAsync_WithWrongType_ReturnsFailure()
    {
        // Arrange
        var schema = @"{
            ""type"": ""object"",
            ""properties"": {
                ""age"": { ""type"": ""integer"" }
            }
        }";

        var payload = @"{
            ""age"": ""not a number""
        }";

        // Act
        var result = await _validator.ValidateAsync(payload, schema);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
        result.Errors.Should().Contain(e => e.Path.Contains("age"));
    }

    [Fact]
    public async Task ValidateAsync_WithArrayInsteadOfObject_ReturnsFailure()
    {
        // Arrange
        var schema = @"{
            ""type"": ""object"",
            ""properties"": {
                ""user"": { ""type"": ""object"" }
            }
        }";

        var payload = @"{
            ""user"": []
        }";

        // Act
        var result = await _validator.ValidateAsync(payload, schema);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Path.Contains("user"));
    }

    #endregion

    #region Required Field Tests

    [Fact]
    public async Task ValidateAsync_WithMissingRequiredField_ReturnsFailure()
    {
        // Arrange
        var schema = @"{
            ""type"": ""object"",
            ""properties"": {
                ""name"": { ""type"": ""string"" }
            },
            ""required"": [""name""]
        }";

        var payload = @"{
            ""age"": 30
        }";

        // Act
        var result = await _validator.ValidateAsync(payload, schema);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Message.ToLower().Contains("required"));
    }

    [Fact]
    public async Task ValidateAsync_WithMultipleMissingRequiredFields_ReturnsAllErrors()
    {
        // Arrange
        var schema = @"{
            ""type"": ""object"",
            ""properties"": {
                ""name"": { ""type"": ""string"" },
                ""email"": { ""type"": ""string"" }
            },
            ""required"": [""name"", ""email""]
        }";

        var payload = @"{ ""age"": 30 }";

        // Act
        var result = await _validator.ValidateAsync(payload, schema);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    #endregion

    #region Pattern and Format Tests

    [Fact]
    public async Task ValidateAsync_WithInvalidEmailFormat_ReturnsFailure()
    {
        // Arrange
        var schema = @"{
            ""type"": ""object"",
            ""properties"": {
                ""email"": { ""type"": ""string"", ""format"": ""email"" }
            }
        }";

        var payload = @"{
            ""email"": ""not-an-email""
        }";

        // Act
        var result = await _validator.ValidateAsync(payload, schema);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Path.Contains("email"));
    }

    [Fact]
    public async Task ValidateAsync_WithPatternMismatch_ReturnsFailure()
    {
        // Arrange
        var schema = @"{
            ""type"": ""object"",
            ""properties"": {
                ""phone"": {
                    ""type"": ""string"",
                    ""pattern"": ""^\\d{3}-\\d{3}-\\d{4}$""
                }
            }
        }";

        var payload = @"{
            ""phone"": ""123456789""
        }";

        // Act
        var result = await _validator.ValidateAsync(payload, schema);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Path.Contains("phone"));
    }

    #endregion

    #region Numeric Constraint Tests

    [Fact]
    public async Task ValidateAsync_WithNumberBelowMinimum_ReturnsFailure()
    {
        // Arrange
        var schema = @"{
            ""type"": ""object"",
            ""properties"": {
                ""age"": {
                    ""type"": ""integer"",
                    ""minimum"": 0
                }
            }
        }";

        var payload = @"{
            ""age"": -5
        }";

        // Act
        var result = await _validator.ValidateAsync(payload, schema);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Path.Contains("age"));
    }

    [Fact]
    public async Task ValidateAsync_WithNumberAboveMaximum_ReturnsFailure()
    {
        // Arrange
        var schema = @"{
            ""type"": ""object"",
            ""properties"": {
                ""score"": {
                    ""type"": ""integer"",
                    ""maximum"": 100
                }
            }
        }";

        var payload = @"{
            ""score"": 150
        }";

        // Act
        var result = await _validator.ValidateAsync(payload, schema);

        // Assert
        result.IsValid.Should().BeFalse();
    }

    #endregion

    #region String Constraint Tests

    [Fact]
    public async Task ValidateAsync_WithStringTooShort_ReturnsFailure()
    {
        // Arrange
        var schema = @"{
            ""type"": ""object"",
            ""properties"": {
                ""username"": {
                    ""type"": ""string"",
                    ""minLength"": 3
                }
            }
        }";

        var payload = @"{
            ""username"": ""ab""
        }";

        // Act
        var result = await _validator.ValidateAsync(payload, schema);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Path.Contains("username"));
    }

    [Fact]
    public async Task ValidateAsync_WithStringTooLong_ReturnsFailure()
    {
        // Arrange
        var schema = @"{
            ""type"": ""object"",
            ""properties"": {
                ""code"": {
                    ""type"": ""string"",
                    ""maxLength"": 5
                }
            }
        }";

        var payload = @"{
            ""code"": ""ABCDEFG""
        }";

        // Act
        var result = await _validator.ValidateAsync(payload, schema);

        // Assert
        result.IsValid.Should().BeFalse();
    }

    #endregion

    #region Array Validation Tests

    [Fact]
    public async Task ValidateAsync_WithInvalidArrayItemType_ReturnsFailure()
    {
        // Arrange
        var schema = @"{
            ""type"": ""object"",
            ""properties"": {
                ""numbers"": {
                    ""type"": ""array"",
                    ""items"": { ""type"": ""integer"" }
                }
            }
        }";

        var payload = @"{
            ""numbers"": [1, 2, ""three"", 4]
        }";

        // Act
        var result = await _validator.ValidateAsync(payload, schema);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Path.Contains("numbers"));
    }

    [Fact]
    public async Task ValidateAsync_WithArrayTooFewItems_ReturnsFailure()
    {
        // Arrange
        var schema = @"{
            ""type"": ""object"",
            ""properties"": {
                ""items"": {
                    ""type"": ""array"",
                    ""minItems"": 2
                }
            }
        }";

        var payload = @"{
            ""items"": [""only-one""]
        }";

        // Act
        var result = await _validator.ValidateAsync(payload, schema);

        // Assert
        result.IsValid.Should().BeFalse();
    }

    #endregion

    #region Enum Validation Tests

    [Fact]
    public async Task ValidateAsync_WithInvalidEnumValue_ReturnsFailure()
    {
        // Arrange
        var schema = @"{
            ""type"": ""object"",
            ""properties"": {
                ""status"": {
                    ""type"": ""string"",
                    ""enum"": [""draft"", ""published"", ""archived""]
                }
            }
        }";

        var payload = @"{
            ""status"": ""invalid-status""
        }";

        // Act
        var result = await _validator.ValidateAsync(payload, schema);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Path.Contains("status"));
    }

    #endregion

    #region Invalid JSON Tests

    [Fact]
    public async Task ValidateAsync_WithInvalidJSON_ReturnsFailure()
    {
        // Arrange
        var schema = @"{
            ""type"": ""object"",
            ""properties"": {
                ""name"": { ""type"": ""string"" }
            }
        }";

        var payload = @"{ invalid json }";

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            async () => await _validator.ValidateAsync(payload, schema));
    }

    [Fact]
    public async Task ValidateAsync_WithEmptyPayload_ThrowsArgumentException()
    {
        // Arrange
        var schema = @"{ ""type"": ""object"" }";

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            async () => await _validator.ValidateAsync("", schema));
    }

    [Fact]
    public async Task ValidateAsync_WithNullPayload_ThrowsArgumentNullException()
    {
        // Arrange
        var schema = @"{ ""type"": ""object"" }";

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await _validator.ValidateAsync(null!, schema));
    }

    #endregion

    #region Invalid Schema Tests

    [Fact]
    public async Task ValidateAsync_WithInvalidSchema_ThrowsArgumentException()
    {
        // Arrange
        var payload = @"{ ""name"": ""test"" }";
        var schema = @"{ invalid schema json }";

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            async () => await _validator.ValidateAsync(payload, schema));
    }

    [Fact]
    public async Task ValidateAsync_WithEmptySchema_ThrowsArgumentException()
    {
        // Arrange
        var payload = @"{ ""name"": ""test"" }";

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            async () => await _validator.ValidateAsync(payload, ""));
    }

    #endregion

    #region Validation Result Tests

    [Fact]
    public async Task ValidateAsync_RecordsValidationTime()
    {
        // Arrange
        var schema = @"{
            ""type"": ""object"",
            ""properties"": {
                ""name"": { ""type"": ""string"" }
            }
        }";

        var payload = @"{ ""name"": ""test"" }";

        // Act
        var result = await _validator.ValidateAsync(payload, schema);

        // Assert
        result.ValidationTimeMs.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task ValidateAsync_WithMultipleErrors_ReturnsAllErrors()
    {
        // Arrange
        var schema = @"{
            ""type"": ""object"",
            ""properties"": {
                ""name"": { ""type"": ""string"", ""minLength"": 3 },
                ""age"": { ""type"": ""integer"", ""minimum"": 0 },
                ""email"": { ""type"": ""string"", ""format"": ""email"" }
            },
            ""required"": [""name"", ""age"", ""email""]
        }";

        var payload = @"{
            ""name"": ""AB"",
            ""age"": -5,
            ""email"": ""invalid""
        }";

        // Act
        var result = await _validator.ValidateAsync(payload, schema);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCountGreaterThanOrEqualTo(3);
    }

    #endregion

    #region Complex Schema Tests

    [Fact]
    public async Task ValidateAsync_WithComplexNestedSchema_ValidatesCorrectly()
    {
        // Arrange
        var schema = @"{
            ""type"": ""object"",
            ""properties"": {
                ""order"": {
                    ""type"": ""object"",
                    ""properties"": {
                        ""id"": { ""type"": ""string"" },
                        ""customer"": {
                            ""type"": ""object"",
                            ""properties"": {
                                ""name"": { ""type"": ""string"" },
                                ""email"": { ""type"": ""string"", ""format"": ""email"" }
                            },
                            ""required"": [""name"", ""email""]
                        },
                        ""items"": {
                            ""type"": ""array"",
                            ""items"": {
                                ""type"": ""object"",
                                ""properties"": {
                                    ""sku"": { ""type"": ""string"" },
                                    ""quantity"": { ""type"": ""integer"", ""minimum"": 1 }
                                },
                                ""required"": [""sku"", ""quantity""]
                            }
                        }
                    },
                    ""required"": [""id"", ""customer"", ""items""]
                }
            }
        }";

        var validPayload = @"{
            ""order"": {
                ""id"": ""ORD-001"",
                ""customer"": {
                    ""name"": ""John Doe"",
                    ""email"": ""john@example.com""
                },
                ""items"": [
                    { ""sku"": ""ITEM-1"", ""quantity"": 2 },
                    { ""sku"": ""ITEM-2"", ""quantity"": 1 }
                ]
            }
        }";

        var invalidPayload = @"{
            ""order"": {
                ""id"": ""ORD-001"",
                ""customer"": {
                    ""name"": ""John Doe""
                },
                ""items"": [
                    { ""sku"": ""ITEM-1"", ""quantity"": 0 }
                ]
            }
        }";

        // Act
        var validResult = await _validator.ValidateAsync(validPayload, schema);
        var invalidResult = await _validator.ValidateAsync(invalidPayload, schema);

        // Assert
        validResult.IsValid.Should().BeTrue();
        invalidResult.IsValid.Should().BeFalse();
        invalidResult.Errors.Should().HaveCountGreaterThanOrEqualTo(2); // Missing email + invalid quantity
    }

    #endregion
}
