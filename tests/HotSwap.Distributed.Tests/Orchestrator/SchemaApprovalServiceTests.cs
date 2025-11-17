using FluentAssertions;
using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Orchestrator.Interfaces;
using HotSwap.Distributed.Orchestrator.Schema;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace HotSwap.Distributed.Tests.Orchestrator;

public class SchemaApprovalServiceTests
{
    private readonly Mock<ISchemaRegistry> _mockRegistry;
    private readonly Mock<ISchemaCompatibilityChecker> _mockCompatibilityChecker;
    private readonly SchemaApprovalService _service;

    public SchemaApprovalServiceTests()
    {
        _mockRegistry = new Mock<ISchemaRegistry>();
        _mockCompatibilityChecker = new Mock<ISchemaCompatibilityChecker>();
        _service = new SchemaApprovalService(
            _mockRegistry.Object,
            _mockCompatibilityChecker.Object,
            NullLogger<SchemaApprovalService>.Instance);
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

    #region RequestApprovalAsync Tests

    [Fact]
    public async Task RequestApprovalAsync_WithBreakingChanges_RequiresApproval()
    {
        // Arrange
        var schemaId = "test.schema.v1";
        var requestedBy = "developer@example.com";
        var approvers = new List<string> { "admin@example.com" };

        var existingSchema = CreateTestSchema(schemaId, SchemaStatus.Approved);
        var newSchema = CreateTestSchema(schemaId, SchemaStatus.Draft);
        newSchema.Version = "2.0";

        _mockRegistry.Setup(x => x.GetSchemaAsync(schemaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingSchema);

        var breakingChanges = new List<BreakingChange>
        {
            new BreakingChange
            {
                ChangeType = BreakingChangeType.AddedRequiredField,
                Path = "$.email",
                Description = "Added required field"
            }
        };

        _mockCompatibilityChecker.Setup(x => x.CheckCompatibilityAsync(
                existingSchema,
                newSchema,
                SchemaCompatibility.Backward,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CompatibilityCheckResult.Incompatible(SchemaCompatibility.Backward, breakingChanges));

        _mockRegistry.Setup(x => x.UpdateSchemaStatusAsync(schemaId, SchemaStatus.PendingApproval, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _service.RequestApprovalAsync(schemaId, newSchema, requestedBy, approvers);

        // Assert
        result.Should().NotBeNull();
        result.SchemaId.Should().Be(schemaId);
        result.RequiresApproval.Should().BeTrue();
        result.BreakingChanges.Should().HaveCount(1);
        result.RequestedBy.Should().Be(requestedBy);
        result.Approvers.Should().BeEquivalentTo(approvers);
        result.Status.Should().Be(ApprovalStatus.Pending);

        _mockRegistry.Verify(x => x.UpdateSchemaStatusAsync(schemaId, SchemaStatus.PendingApproval, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RequestApprovalAsync_WithNonBreakingChanges_DoesNotRequireApproval()
    {
        // Arrange
        var schemaId = "test.schema.v1";
        var requestedBy = "developer@example.com";
        var approvers = new List<string> { "admin@example.com" };

        var existingSchema = CreateTestSchema(schemaId, SchemaStatus.Approved);
        var newSchema = CreateTestSchema(schemaId, SchemaStatus.Draft);
        newSchema.Version = "1.1";

        _mockRegistry.Setup(x => x.GetSchemaAsync(schemaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingSchema);

        _mockCompatibilityChecker.Setup(x => x.CheckCompatibilityAsync(
                existingSchema,
                newSchema,
                SchemaCompatibility.Backward,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CompatibilityCheckResult.Compatible(SchemaCompatibility.Backward));

        // Act
        var result = await _service.RequestApprovalAsync(schemaId, newSchema, requestedBy, approvers);

        // Assert
        result.Should().NotBeNull();
        result.SchemaId.Should().Be(schemaId);
        result.RequiresApproval.Should().BeFalse();
        result.BreakingChanges.Should().BeEmpty();
        result.Status.Should().Be(ApprovalStatus.AutoApproved);

        _mockRegistry.Verify(x => x.UpdateSchemaStatusAsync(It.IsAny<string>(), SchemaStatus.PendingApproval, null, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RequestApprovalAsync_WithNoExistingSchema_DoesNotRequireApproval()
    {
        // Arrange - first version of schema doesn't need approval
        var schemaId = "test.schema.v1";
        var requestedBy = "developer@example.com";
        var approvers = new List<string> { "admin@example.com" };
        var newSchema = CreateTestSchema(schemaId, SchemaStatus.Draft);

        _mockRegistry.Setup(x => x.GetSchemaAsync(schemaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MessageSchema?)null);

        // Act
        var result = await _service.RequestApprovalAsync(schemaId, newSchema, requestedBy, approvers);

        // Assert
        result.Should().NotBeNull();
        result.SchemaId.Should().Be(schemaId);
        result.RequiresApproval.Should().BeFalse();
        result.BreakingChanges.Should().BeEmpty();
        result.Status.Should().Be(ApprovalStatus.AutoApproved);

        _mockCompatibilityChecker.Verify(x => x.CheckCompatibilityAsync(
            It.IsAny<MessageSchema>(),
            It.IsAny<MessageSchema>(),
            It.IsAny<SchemaCompatibility>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RequestApprovalAsync_WithNullRequestedBy_ThrowsArgumentNullException()
    {
        // Arrange
        var schemaId = "test.schema.v1";
        var newSchema = CreateTestSchema(schemaId);
        var approvers = new List<string> { "admin@example.com" };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await _service.RequestApprovalAsync(schemaId, newSchema, null!, approvers));
    }

    [Fact]
    public async Task RequestApprovalAsync_WithEmptyApprovers_ThrowsArgumentException()
    {
        // Arrange
        var schemaId = "test.schema.v1";
        var newSchema = CreateTestSchema(schemaId);
        var requestedBy = "developer@example.com";
        var approvers = new List<string>();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            async () => await _service.RequestApprovalAsync(schemaId, newSchema, requestedBy, approvers));
    }

    #endregion

    #region ApproveSchemaAsync Tests

    [Fact]
    public async Task ApproveSchemaAsync_WithValidApproval_UpdatesStatusToApproved()
    {
        // Arrange
        var schemaId = "test.schema.v1";
        var approvedBy = "admin@example.com";
        var reason = "Changes reviewed and approved";
        var schema = CreateTestSchema(schemaId, SchemaStatus.PendingApproval);

        _mockRegistry.Setup(x => x.GetSchemaAsync(schemaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(schema);

        _mockRegistry.Setup(x => x.UpdateSchemaStatusAsync(schemaId, SchemaStatus.Approved, approvedBy, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _service.ApproveSchemaAsync(schemaId, approvedBy, reason);

        // Assert
        result.Should().BeTrue();
        _mockRegistry.Verify(x => x.UpdateSchemaStatusAsync(schemaId, SchemaStatus.Approved, approvedBy, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ApproveSchemaAsync_WithSchemaNotFound_ReturnsFalse()
    {
        // Arrange
        var schemaId = "nonexistent.schema";
        var approvedBy = "admin@example.com";

        _mockRegistry.Setup(x => x.GetSchemaAsync(schemaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MessageSchema?)null);

        // Act
        var result = await _service.ApproveSchemaAsync(schemaId, approvedBy);

        // Assert
        result.Should().BeFalse();
        _mockRegistry.Verify(x => x.UpdateSchemaStatusAsync(It.IsAny<string>(), It.IsAny<SchemaStatus>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ApproveSchemaAsync_WithNonPendingSchema_ThrowsInvalidOperationException()
    {
        // Arrange - schema already approved
        var schemaId = "test.schema.v1";
        var approvedBy = "admin@example.com";
        var schema = CreateTestSchema(schemaId, SchemaStatus.Approved);

        _mockRegistry.Setup(x => x.GetSchemaAsync(schemaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(schema);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await _service.ApproveSchemaAsync(schemaId, approvedBy));
    }

    [Fact]
    public async Task ApproveSchemaAsync_WithNullApprovedBy_ThrowsArgumentNullException()
    {
        // Arrange
        var schemaId = "test.schema.v1";

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await _service.ApproveSchemaAsync(schemaId, null!));
    }

    #endregion

    #region RejectSchemaAsync Tests

    [Fact]
    public async Task RejectSchemaAsync_WithValidRejection_UpdatesStatusToRejected()
    {
        // Arrange
        var schemaId = "test.schema.v1";
        var rejectedBy = "admin@example.com";
        var reason = "Breaking changes not acceptable";
        var schema = CreateTestSchema(schemaId, SchemaStatus.PendingApproval);

        _mockRegistry.Setup(x => x.GetSchemaAsync(schemaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(schema);

        _mockRegistry.Setup(x => x.UpdateSchemaStatusAsync(schemaId, SchemaStatus.Rejected, rejectedBy, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _service.RejectSchemaAsync(schemaId, rejectedBy, reason);

        // Assert
        result.Should().BeTrue();
        _mockRegistry.Verify(x => x.UpdateSchemaStatusAsync(schemaId, SchemaStatus.Rejected, rejectedBy, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RejectSchemaAsync_WithSchemaNotFound_ReturnsFalse()
    {
        // Arrange
        var schemaId = "nonexistent.schema";
        var rejectedBy = "admin@example.com";
        var reason = "Schema not found";

        _mockRegistry.Setup(x => x.GetSchemaAsync(schemaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MessageSchema?)null);

        // Act
        var result = await _service.RejectSchemaAsync(schemaId, rejectedBy, reason);

        // Assert
        result.Should().BeFalse();
        _mockRegistry.Verify(x => x.UpdateSchemaStatusAsync(It.IsAny<string>(), It.IsAny<SchemaStatus>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RejectSchemaAsync_WithNonPendingSchema_ThrowsInvalidOperationException()
    {
        // Arrange - schema already rejected
        var schemaId = "test.schema.v1";
        var rejectedBy = "admin@example.com";
        var schema = CreateTestSchema(schemaId, SchemaStatus.Rejected);

        _mockRegistry.Setup(x => x.GetSchemaAsync(schemaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(schema);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await _service.RejectSchemaAsync(schemaId, rejectedBy, "reason"));
    }

    [Fact]
    public async Task RejectSchemaAsync_WithNullRejectedBy_ThrowsArgumentNullException()
    {
        // Arrange
        var schemaId = "test.schema.v1";

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await _service.RejectSchemaAsync(schemaId, null!, "reason"));
    }

    #endregion

    #region DeprecateSchemaAsync Tests

    [Fact]
    public async Task DeprecateSchemaAsync_WithApprovedSchema_UpdatesStatusToDeprecated()
    {
        // Arrange
        var schemaId = "test.schema.v1";
        var deprecatedBy = "admin@example.com";
        var reason = "Replaced by version 2.0";
        var schema = CreateTestSchema(schemaId, SchemaStatus.Approved);

        _mockRegistry.Setup(x => x.GetSchemaAsync(schemaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(schema);

        _mockRegistry.Setup(x => x.UpdateSchemaStatusAsync(schemaId, SchemaStatus.Deprecated, deprecatedBy, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _service.DeprecateSchemaAsync(schemaId, deprecatedBy, reason);

        // Assert
        result.Should().BeTrue();
        _mockRegistry.Verify(x => x.UpdateSchemaStatusAsync(schemaId, SchemaStatus.Deprecated, deprecatedBy, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeprecateSchemaAsync_WithSchemaNotFound_ReturnsFalse()
    {
        // Arrange
        var schemaId = "nonexistent.schema";
        var deprecatedBy = "admin@example.com";

        _mockRegistry.Setup(x => x.GetSchemaAsync(schemaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MessageSchema?)null);

        // Act
        var result = await _service.DeprecateSchemaAsync(schemaId, deprecatedBy);

        // Assert
        result.Should().BeFalse();
        _mockRegistry.Verify(x => x.UpdateSchemaStatusAsync(It.IsAny<string>(), It.IsAny<SchemaStatus>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeprecateSchemaAsync_WithDraftSchema_ThrowsInvalidOperationException()
    {
        // Arrange - cannot deprecate draft schema
        var schemaId = "test.schema.v1";
        var deprecatedBy = "admin@example.com";
        var schema = CreateTestSchema(schemaId, SchemaStatus.Draft);

        _mockRegistry.Setup(x => x.GetSchemaAsync(schemaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(schema);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await _service.DeprecateSchemaAsync(schemaId, deprecatedBy));
    }

    [Fact]
    public async Task DeprecateSchemaAsync_WithNullDeprecatedBy_ThrowsArgumentNullException()
    {
        // Arrange
        var schemaId = "test.schema.v1";

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await _service.DeprecateSchemaAsync(schemaId, null!));
    }

    #endregion

    #region GetApprovalRequestAsync Tests

    [Fact]
    public async Task GetApprovalRequestAsync_WithExistingRequest_ReturnsRequest()
    {
        // Arrange
        var schemaId = "test.schema.v1";
        var schema = CreateTestSchema(schemaId, SchemaStatus.PendingApproval);
        var requestedBy = "developer@example.com";
        var approvers = new List<string> { "admin@example.com" };

        _mockRegistry.Setup(x => x.GetSchemaAsync(schemaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(schema);

        var existingSchema = CreateTestSchema(schemaId, SchemaStatus.Approved);
        existingSchema.Version = "1.0";

        _mockRegistry.Setup(x => x.GetSchemaAsync(schemaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingSchema);

        var newSchema = CreateTestSchema(schemaId);
        newSchema.Version = "2.0";

        var breakingChanges = new List<BreakingChange>
        {
            new BreakingChange
            {
                ChangeType = BreakingChangeType.TypeChanged,
                Path = "$.age",
                Description = "Type changed"
            }
        };

        _mockCompatibilityChecker.Setup(x => x.CheckCompatibilityAsync(
                existingSchema,
                newSchema,
                SchemaCompatibility.Backward,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CompatibilityCheckResult.Incompatible(SchemaCompatibility.Backward, breakingChanges));

        _mockRegistry.Setup(x => x.UpdateSchemaStatusAsync(schemaId, SchemaStatus.PendingApproval, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // First create the approval request
        await _service.RequestApprovalAsync(schemaId, newSchema, requestedBy, approvers);

        // Act
        var result = await _service.GetApprovalRequestAsync(schemaId);

        // Assert
        result.Should().NotBeNull();
        result!.SchemaId.Should().Be(schemaId);
        result.Status.Should().Be(ApprovalStatus.Pending);
    }

    [Fact]
    public async Task GetApprovalRequestAsync_WithNonExistentRequest_ReturnsNull()
    {
        // Arrange
        var schemaId = "nonexistent.schema";

        // Act
        var result = await _service.GetApprovalRequestAsync(schemaId);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region Integration Tests

    [Fact]
    public async Task FullWorkflow_RequestApproveDeprecate_CompletesSuccessfully()
    {
        // Arrange
        var schemaId = "test.schema.v1";
        var developer = "developer@example.com";
        var admin = "admin@example.com";
        var approvers = new List<string> { admin };

        var existingSchema = CreateTestSchema(schemaId, SchemaStatus.Approved);
        var newSchema = CreateTestSchema(schemaId, SchemaStatus.Draft);
        newSchema.Version = "2.0";

        var pendingSchema = CreateTestSchema(schemaId, SchemaStatus.PendingApproval);

        // Setup sequence: first call returns existing, subsequent calls return pending
        _mockRegistry.SetupSequence(x => x.GetSchemaAsync(schemaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingSchema)  // First call during RequestApprovalAsync
            .ReturnsAsync(pendingSchema)   // Second call during ApproveSchemaAsync
            .ReturnsAsync(pendingSchema);  // Third call during DeprecateSchemaAsync (after approval callback)

        var breakingChanges = new List<BreakingChange>
        {
            new BreakingChange
            {
                ChangeType = BreakingChangeType.AddedRequiredField,
                Path = "$.newField",
                Description = "Added required field"
            }
        };

        _mockCompatibilityChecker.Setup(x => x.CheckCompatibilityAsync(
                existingSchema,
                newSchema,
                SchemaCompatibility.Backward,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CompatibilityCheckResult.Incompatible(SchemaCompatibility.Backward, breakingChanges));

        _mockRegistry.Setup(x => x.UpdateSchemaStatusAsync(schemaId, SchemaStatus.PendingApproval, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _mockRegistry.Setup(x => x.UpdateSchemaStatusAsync(schemaId, SchemaStatus.Approved, admin, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .Callback(() =>
            {
                pendingSchema.Status = SchemaStatus.Approved;
            });

        _mockRegistry.Setup(x => x.UpdateSchemaStatusAsync(schemaId, SchemaStatus.Deprecated, admin, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act & Assert - Request Approval
        var approvalRequest = await _service.RequestApprovalAsync(schemaId, newSchema, developer, approvers);
        approvalRequest.RequiresApproval.Should().BeTrue();
        approvalRequest.BreakingChanges.Should().HaveCount(1);

        // Act & Assert - Approve
        var approveResult = await _service.ApproveSchemaAsync(schemaId, admin, "Approved after review");
        approveResult.Should().BeTrue();

        // Act & Assert - Deprecate
        var deprecateResult = await _service.DeprecateSchemaAsync(schemaId, admin, "Replaced by v3.0");
        deprecateResult.Should().BeTrue();
    }

    #endregion
}
