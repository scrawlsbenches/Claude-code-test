using FluentAssertions;
using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Orchestrator.Schema;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HotSwap.Distributed.Tests.Orchestrator;

public class InMemorySchemaRegistryTests
{
    private readonly InMemorySchemaRegistry _registry;

    public InMemorySchemaRegistryTests()
    {
        _registry = new InMemorySchemaRegistry(NullLogger<InMemorySchemaRegistry>.Instance);
    }

    private MessageSchema CreateTestSchema(
        string schemaId = "test.schema.v1",
        string version = "1.0",
        SchemaStatus status = SchemaStatus.Draft)
    {
        return new MessageSchema
        {
            SchemaId = schemaId,
            SchemaDefinition = "{\"type\":\"object\",\"properties\":{\"name\":{\"type\":\"string\"}}}",
            Version = version,
            Status = status,
            Compatibility = SchemaCompatibility.None,
            CreatedAt = DateTime.UtcNow
        };
    }

    #region RegisterSchemaAsync Tests

    [Fact]
    public async Task RegisterSchemaAsync_WithValidSchema_RegistersSuccessfully()
    {
        // Arrange
        var schema = CreateTestSchema();

        // Act
        var result = await _registry.RegisterSchemaAsync(schema);

        // Assert
        result.Should().NotBeNull();
        result.SchemaId.Should().Be("test.schema.v1");
        result.Status.Should().Be(SchemaStatus.Draft);
        result.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task RegisterSchemaAsync_WithDuplicateSchemaId_ThrowsInvalidOperationException()
    {
        // Arrange
        var schema1 = CreateTestSchema("duplicate.schema");
        var schema2 = CreateTestSchema("duplicate.schema");

        await _registry.RegisterSchemaAsync(schema1);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await _registry.RegisterSchemaAsync(schema2));
    }

    [Fact]
    public async Task RegisterSchemaAsync_WithInvalidSchema_ThrowsArgumentException()
    {
        // Arrange - Schema with missing SchemaId
        var schema = new MessageSchema
        {
            SchemaId = "",
            SchemaDefinition = "{}"
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            async () => await _registry.RegisterSchemaAsync(schema));
    }

    #endregion

    #region GetSchemaAsync Tests

    [Fact]
    public async Task GetSchemaAsync_WithExistingSchema_ReturnsSchema()
    {
        // Arrange
        var schema = CreateTestSchema("existing.schema");
        await _registry.RegisterSchemaAsync(schema);

        // Act
        var result = await _registry.GetSchemaAsync("existing.schema");

        // Assert
        result.Should().NotBeNull();
        result!.SchemaId.Should().Be("existing.schema");
    }

    [Fact]
    public async Task GetSchemaAsync_WithNonExistentSchema_ReturnsNull()
    {
        // Act
        var result = await _registry.GetSchemaAsync("non.existent.schema");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetSchemaAsync_WithNullSchemaId_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            async () => await _registry.GetSchemaAsync(null!));
    }

    [Fact]
    public async Task GetSchemaAsync_WithEmptySchemaId_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            async () => await _registry.GetSchemaAsync(""));
    }

    #endregion

    #region ListSchemasAsync Tests

    [Fact]
    public async Task ListSchemasAsync_WithNoFilter_ReturnsAllSchemas()
    {
        // Arrange
        await _registry.RegisterSchemaAsync(CreateTestSchema("schema1", status: SchemaStatus.Draft));
        await _registry.RegisterSchemaAsync(CreateTestSchema("schema2", status: SchemaStatus.Approved));
        await _registry.RegisterSchemaAsync(CreateTestSchema("schema3", status: SchemaStatus.PendingApproval));

        // Act
        var result = await _registry.ListSchemasAsync();

        // Assert
        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task ListSchemasAsync_WithStatusFilter_ReturnsMatchingSchemas()
    {
        // Arrange
        await _registry.RegisterSchemaAsync(CreateTestSchema("draft1", status: SchemaStatus.Draft));
        await _registry.RegisterSchemaAsync(CreateTestSchema("draft2", status: SchemaStatus.Draft));
        await _registry.RegisterSchemaAsync(CreateTestSchema("approved1", status: SchemaStatus.Approved));

        // Act
        var draftSchemas = await _registry.ListSchemasAsync(SchemaStatus.Draft);

        // Assert
        draftSchemas.Should().HaveCount(2);
        draftSchemas.Should().AllSatisfy(s => s.Status.Should().Be(SchemaStatus.Draft));
    }

    [Fact]
    public async Task ListSchemasAsync_WithEmptyRegistry_ReturnsEmptyList()
    {
        // Act
        var result = await _registry.ListSchemasAsync();

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region UpdateSchemaStatusAsync Tests

    [Fact]
    public async Task UpdateSchemaStatusAsync_ToApproved_UpdatesStatusAndSetsApprover()
    {
        // Arrange
        var schema = CreateTestSchema("pending.schema", status: SchemaStatus.PendingApproval);
        await _registry.RegisterSchemaAsync(schema);

        // Act
        var result = await _registry.UpdateSchemaStatusAsync(
            "pending.schema",
            SchemaStatus.Approved,
            approvedBy: "admin@example.com");

        // Assert
        result.Should().BeTrue();

        var updated = await _registry.GetSchemaAsync("pending.schema");
        updated!.Status.Should().Be(SchemaStatus.Approved);
        updated.ApprovedBy.Should().Be("admin@example.com");
        updated.ApprovedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task UpdateSchemaStatusAsync_ToApprovedWithoutApprover_ThrowsArgumentException()
    {
        // Arrange
        var schema = CreateTestSchema("pending.schema", status: SchemaStatus.PendingApproval);
        await _registry.RegisterSchemaAsync(schema);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            async () => await _registry.UpdateSchemaStatusAsync(
                "pending.schema",
                SchemaStatus.Approved,
                approvedBy: null));
    }

    [Fact]
    public async Task UpdateSchemaStatusAsync_ToPendingApproval_UpdatesStatus()
    {
        // Arrange
        var schema = CreateTestSchema("draft.schema", status: SchemaStatus.Draft);
        await _registry.RegisterSchemaAsync(schema);

        // Act
        var result = await _registry.UpdateSchemaStatusAsync(
            "draft.schema",
            SchemaStatus.PendingApproval);

        // Assert
        result.Should().BeTrue();

        var updated = await _registry.GetSchemaAsync("draft.schema");
        updated!.Status.Should().Be(SchemaStatus.PendingApproval);
    }

    [Fact]
    public async Task UpdateSchemaStatusAsync_WithNonExistentSchema_ReturnsFalse()
    {
        // Act
        var result = await _registry.UpdateSchemaStatusAsync(
            "non.existent.schema",
            SchemaStatus.Approved,
            approvedBy: "admin@example.com");

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region DeleteSchemaAsync Tests

    [Fact]
    public async Task DeleteSchemaAsync_WithDraftSchema_DeletesSuccessfully()
    {
        // Arrange
        var schema = CreateTestSchema("draft.schema", status: SchemaStatus.Draft);
        await _registry.RegisterSchemaAsync(schema);

        // Act
        var result = await _registry.DeleteSchemaAsync("draft.schema");

        // Assert
        result.Should().BeTrue();

        var deleted = await _registry.GetSchemaAsync("draft.schema");
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task DeleteSchemaAsync_WithApprovedSchema_ThrowsInvalidOperationException()
    {
        // Arrange
        var schema = CreateTestSchema("approved.schema", status: SchemaStatus.Approved);
        await _registry.RegisterSchemaAsync(schema);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await _registry.DeleteSchemaAsync("approved.schema"));
    }

    [Fact]
    public async Task DeleteSchemaAsync_WithNonExistentSchema_ReturnsFalse()
    {
        // Act
        var result = await _registry.DeleteSchemaAsync("non.existent.schema");

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region SchemaExistsAsync Tests

    [Fact]
    public async Task SchemaExistsAsync_WithExistingSchema_ReturnsTrue()
    {
        // Arrange
        var schema = CreateTestSchema("exists.schema");
        await _registry.RegisterSchemaAsync(schema);

        // Act
        var result = await _registry.SchemaExistsAsync("exists.schema");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task SchemaExistsAsync_WithNonExistentSchema_ReturnsFalse()
    {
        // Act
        var result = await _registry.SchemaExistsAsync("non.existent.schema");

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Multiple Operations Tests

    [Fact]
    public async Task CompleteWorkflow_DraftToPendingToApproved_WorksCorrectly()
    {
        // Arrange
        var schema = CreateTestSchema("workflow.schema", status: SchemaStatus.Draft);

        // Act & Assert - Register
        var registered = await _registry.RegisterSchemaAsync(schema);
        registered.Status.Should().Be(SchemaStatus.Draft);

        // Act & Assert - Move to Pending
        await _registry.UpdateSchemaStatusAsync("workflow.schema", SchemaStatus.PendingApproval);
        var pending = await _registry.GetSchemaAsync("workflow.schema");
        pending!.Status.Should().Be(SchemaStatus.PendingApproval);

        // Act & Assert - Approve
        await _registry.UpdateSchemaStatusAsync(
            "workflow.schema",
            SchemaStatus.Approved,
            approvedBy: "admin@example.com");
        var approved = await _registry.GetSchemaAsync("workflow.schema");
        approved!.Status.Should().Be(SchemaStatus.Approved);
        approved.ApprovedBy.Should().Be("admin@example.com");
    }

    [Fact]
    public async Task MultipleSchemas_IndependentLifecycles_WorkCorrectly()
    {
        // Arrange & Act
        var schema1 = CreateTestSchema("schema1", status: SchemaStatus.Draft);
        var schema2 = CreateTestSchema("schema2", status: SchemaStatus.Draft);
        var schema3 = CreateTestSchema("schema3", status: SchemaStatus.Draft);

        await _registry.RegisterSchemaAsync(schema1);
        await _registry.RegisterSchemaAsync(schema2);
        await _registry.RegisterSchemaAsync(schema3);

        // Update different schemas to different states
        await _registry.UpdateSchemaStatusAsync("schema1", SchemaStatus.PendingApproval);
        await _registry.UpdateSchemaStatusAsync("schema2", SchemaStatus.Approved, approvedBy: "admin1");
        // schema3 remains Draft

        // Assert
        var all = await _registry.ListSchemasAsync();
        all.Should().HaveCount(3);

        var s1 = await _registry.GetSchemaAsync("schema1");
        s1!.Status.Should().Be(SchemaStatus.PendingApproval);

        var s2 = await _registry.GetSchemaAsync("schema2");
        s2!.Status.Should().Be(SchemaStatus.Approved);
        s2.ApprovedBy.Should().Be("admin1");

        var s3 = await _registry.GetSchemaAsync("schema3");
        s3!.Status.Should().Be(SchemaStatus.Draft);
    }

    #endregion
}
