using FluentAssertions;
using HotSwap.Distributed.Api.Controllers;
using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Orchestrator.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace HotSwap.Distributed.Tests.Api;

public class SchemasControllerTests
{
    private readonly Mock<ISchemaRegistry> _mockRegistry;
    private readonly Mock<ISchemaValidator> _mockValidator;
    private readonly Mock<ISchemaApprovalService> _mockApprovalService;
    private readonly SchemasController _controller;

    public SchemasControllerTests()
    {
        _mockRegistry = new Mock<ISchemaRegistry>();
        _mockValidator = new Mock<ISchemaValidator>();
        _mockApprovalService = new Mock<ISchemaApprovalService>();
        _controller = new SchemasController(
            _mockRegistry.Object,
            _mockValidator.Object,
            _mockApprovalService.Object,
            NullLogger<SchemasController>.Instance);
    }

    private MessageSchema CreateTestSchema(string schemaId, SchemaStatus status = SchemaStatus.Draft)
    {
        return new MessageSchema
        {
            SchemaId = schemaId,
            SchemaDefinition = @"{""type"": ""object"", ""properties"": {""name"": {""type"": ""string""}}}",
            Version = "1.0",
            Status = status,
            Compatibility = SchemaCompatibility.Backward,
            CreatedAt = DateTime.UtcNow
        };
    }

    #region RegisterSchema Tests

    [Fact]
    public async Task RegisterSchema_WithValidSchema_ReturnsCreated()
    {
        // Arrange
        var schema = CreateTestSchema("test.schema.v1");
        _mockRegistry.Setup(x => x.RegisterSchemaAsync(It.IsAny<MessageSchema>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(schema);

        // Act
        var result = await _controller.RegisterSchema(schema);

        // Assert
        var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.ActionName.Should().Be(nameof(SchemasController.GetSchema));
        createdResult.RouteValues!["id"].Should().Be(schema.SchemaId);
        var returnedSchema = createdResult.Value.Should().BeOfType<MessageSchema>().Subject;
        returnedSchema.SchemaId.Should().Be(schema.SchemaId);
    }

    [Fact]
    public async Task RegisterSchema_WithInvalidSchema_ReturnsBadRequest()
    {
        // Arrange - invalid schema will throw ArgumentException
        var schema = CreateTestSchema("test.schema.v1");
        _mockRegistry.Setup(x => x.RegisterSchemaAsync(It.IsAny<MessageSchema>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("Schema validation failed"));

        // Act
        var result = await _controller.RegisterSchema(schema);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task RegisterSchema_WithDuplicateSchemaId_ReturnsConflict()
    {
        // Arrange
        var schema = CreateTestSchema("test.schema.v1");
        _mockRegistry.Setup(x => x.RegisterSchemaAsync(It.IsAny<MessageSchema>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Schema already exists"));

        // Act
        var result = await _controller.RegisterSchema(schema);

        // Assert
        result.Result.Should().BeOfType<ConflictObjectResult>();
    }

    #endregion

    #region ListSchemas Tests

    [Fact]
    public async Task ListSchemas_WithNoFilter_ReturnsAllSchemas()
    {
        // Arrange
        var schemas = new List<MessageSchema>
        {
            CreateTestSchema("schema1", SchemaStatus.Approved),
            CreateTestSchema("schema2", SchemaStatus.Draft),
            CreateTestSchema("schema3", SchemaStatus.Deprecated)
        };
        _mockRegistry.Setup(x => x.ListSchemasAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(schemas);

        // Act
        var result = await _controller.ListSchemas(null);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedSchemas = okResult.Value.Should().BeAssignableTo<List<MessageSchema>>().Subject;
        returnedSchemas.Should().HaveCount(3);
    }

    [Fact]
    public async Task ListSchemas_WithStatusFilter_ReturnsFilteredSchemas()
    {
        // Arrange
        var schemas = new List<MessageSchema>
        {
            CreateTestSchema("schema1", SchemaStatus.Approved),
            CreateTestSchema("schema2", SchemaStatus.Approved)
        };
        _mockRegistry.Setup(x => x.ListSchemasAsync(SchemaStatus.Approved, It.IsAny<CancellationToken>()))
            .ReturnsAsync(schemas);

        // Act
        var result = await _controller.ListSchemas(SchemaStatus.Approved);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedSchemas = okResult.Value.Should().BeAssignableTo<List<MessageSchema>>().Subject;
        returnedSchemas.Should().HaveCount(2);
        returnedSchemas.Should().AllSatisfy(s => s.Status.Should().Be(SchemaStatus.Approved));
    }

    [Fact]
    public async Task ListSchemas_WhenEmpty_ReturnsEmptyList()
    {
        // Arrange
        _mockRegistry.Setup(x => x.ListSchemasAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MessageSchema>());

        // Act
        var result = await _controller.ListSchemas(null);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedSchemas = okResult.Value.Should().BeAssignableTo<List<MessageSchema>>().Subject;
        returnedSchemas.Should().BeEmpty();
    }

    #endregion

    #region GetSchema Tests

    [Fact]
    public async Task GetSchema_WithExistingId_ReturnsSchema()
    {
        // Arrange
        var schema = CreateTestSchema("test.schema.v1");
        _mockRegistry.Setup(x => x.GetSchemaAsync("test.schema.v1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(schema);

        // Act
        var result = await _controller.GetSchema("test.schema.v1");

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedSchema = okResult.Value.Should().BeOfType<MessageSchema>().Subject;
        returnedSchema.SchemaId.Should().Be("test.schema.v1");
    }

    [Fact]
    public async Task GetSchema_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        _mockRegistry.Setup(x => x.GetSchemaAsync("nonexistent", It.IsAny<CancellationToken>()))
            .ReturnsAsync((MessageSchema?)null);

        // Act
        var result = await _controller.GetSchema("nonexistent");

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    #endregion

    #region ValidatePayload Tests

    [Fact]
    public async Task ValidatePayload_WithValidPayload_ReturnsOk()
    {
        // Arrange
        var schemaId = "test.schema.v1";
        var schema = CreateTestSchema(schemaId);
        var payload = @"{""name"": ""test""}";

        _mockRegistry.Setup(x => x.GetSchemaAsync(schemaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(schema);

        var validationResult = SchemaValidationResult.Success(10);
        _mockValidator.Setup(x => x.ValidateAsync(payload, schema.SchemaDefinition, It.IsAny<CancellationToken>()))
            .ReturnsAsync(validationResult);

        // Act
        var result = await _controller.ValidatePayload(schemaId, payload);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedResult = okResult.Value.Should().BeOfType<SchemaValidationResult>().Subject;
        returnedResult.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidatePayload_WithInvalidPayload_ReturnsBadRequest()
    {
        // Arrange
        var schemaId = "test.schema.v1";
        var schema = CreateTestSchema(schemaId);
        var payload = @"{""invalid"": true}";

        _mockRegistry.Setup(x => x.GetSchemaAsync(schemaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(schema);

        var validationResult = SchemaValidationResult.Failure(
            new List<ValidationError>
            {
                new ValidationError
                {
                    Path = "$.name",
                    Message = "Required property 'name' not found",
                    Kind = "Required"
                }
            },
            10);

        _mockValidator.Setup(x => x.ValidateAsync(payload, schema.SchemaDefinition, It.IsAny<CancellationToken>()))
            .ReturnsAsync(validationResult);

        // Act
        var result = await _controller.ValidatePayload(schemaId, payload);

        // Assert
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var returnedResult = badRequestResult.Value.Should().BeOfType<SchemaValidationResult>().Subject;
        returnedResult.IsValid.Should().BeFalse();
        returnedResult.Errors.Should().HaveCount(1);
    }

    [Fact]
    public async Task ValidatePayload_WithNonExistentSchema_ReturnsNotFound()
    {
        // Arrange
        _mockRegistry.Setup(x => x.GetSchemaAsync("nonexistent", It.IsAny<CancellationToken>()))
            .ReturnsAsync((MessageSchema?)null);

        // Act
        var result = await _controller.ValidatePayload("nonexistent", @"{""test"": true}");

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    #endregion

    #region ApproveSchema Tests

    [Fact]
    public async Task ApproveSchema_WithPendingSchema_ReturnsOk()
    {
        // Arrange
        var schemaId = "test.schema.v1";
        var approvedBy = "admin@example.com";
        var reason = "Approved after review";

        _mockApprovalService.Setup(x => x.ApproveSchemaAsync(schemaId, approvedBy, reason, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.ApproveSchema(schemaId, approvedBy, reason);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task ApproveSchema_WithNonExistentSchema_ReturnsNotFound()
    {
        // Arrange
        var schemaId = "nonexistent";
        var approvedBy = "admin@example.com";

        _mockApprovalService.Setup(x => x.ApproveSchemaAsync(schemaId, approvedBy, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.ApproveSchema(schemaId, approvedBy);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task ApproveSchema_WithInvalidStatus_ReturnsBadRequest()
    {
        // Arrange - schema not in PendingApproval status
        var schemaId = "test.schema.v1";
        var approvedBy = "admin@example.com";

        _mockApprovalService.Setup(x => x.ApproveSchemaAsync(schemaId, approvedBy, null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Schema not in PendingApproval status"));

        // Act
        var result = await _controller.ApproveSchema(schemaId, approvedBy);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    #endregion

    #region RejectSchema Tests

    [Fact]
    public async Task RejectSchema_WithPendingSchema_ReturnsOk()
    {
        // Arrange
        var schemaId = "test.schema.v1";
        var rejectedBy = "admin@example.com";
        var reason = "Breaking changes not acceptable";

        _mockApprovalService.Setup(x => x.RejectSchemaAsync(schemaId, rejectedBy, reason, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.RejectSchema(schemaId, rejectedBy, reason);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task RejectSchema_WithNonExistentSchema_ReturnsNotFound()
    {
        // Arrange
        var schemaId = "nonexistent";
        var rejectedBy = "admin@example.com";
        var reason = "Not found";

        _mockApprovalService.Setup(x => x.RejectSchemaAsync(schemaId, rejectedBy, reason, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.RejectSchema(schemaId, rejectedBy, reason);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    #endregion

    #region DeprecateSchema Tests

    [Fact]
    public async Task DeprecateSchema_WithApprovedSchema_ReturnsOk()
    {
        // Arrange
        var schemaId = "test.schema.v1";
        var deprecatedBy = "admin@example.com";
        var reason = "Replaced by v2.0";

        _mockApprovalService.Setup(x => x.DeprecateSchemaAsync(schemaId, deprecatedBy, reason, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.DeprecateSchema(schemaId, deprecatedBy, reason);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task DeprecateSchema_WithNonExistentSchema_ReturnsNotFound()
    {
        // Arrange
        var schemaId = "nonexistent";
        var deprecatedBy = "admin@example.com";

        _mockApprovalService.Setup(x => x.DeprecateSchemaAsync(schemaId, deprecatedBy, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.DeprecateSchema(schemaId, deprecatedBy);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task DeprecateSchema_WithNonApprovedSchema_ReturnsBadRequest()
    {
        // Arrange
        var schemaId = "test.schema.v1";
        var deprecatedBy = "admin@example.com";

        _mockApprovalService.Setup(x => x.DeprecateSchemaAsync(schemaId, deprecatedBy, null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Only approved schemas can be deprecated"));

        // Act
        var result = await _controller.DeprecateSchema(schemaId, deprecatedBy);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    #endregion
}
