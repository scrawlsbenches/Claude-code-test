using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;

namespace HotSwap.Distributed.Orchestrator.Interfaces;

/// <summary>
/// Service for managing schema approval workflows.
/// </summary>
public interface ISchemaApprovalService
{
    /// <summary>
    /// Requests approval for a schema change.
    /// Automatically detects breaking changes and determines if approval is required.
    /// </summary>
    /// <param name="schemaId">The schema identifier.</param>
    /// <param name="newSchema">The new schema version.</param>
    /// <param name="requestedBy">Email of the person requesting approval.</param>
    /// <param name="approvers">List of approver email addresses.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Schema approval request details.</returns>
    Task<SchemaApprovalRequest> RequestApprovalAsync(
        string schemaId,
        MessageSchema newSchema,
        string requestedBy,
        List<string> approvers,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Approves a schema change.
    /// </summary>
    /// <param name="schemaId">The schema identifier.</param>
    /// <param name="approvedBy">Email of the approver.</param>
    /// <param name="reason">Optional approval reason.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if approval succeeded, false otherwise.</returns>
    Task<bool> ApproveSchemaAsync(
        string schemaId,
        string approvedBy,
        string? reason = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Rejects a schema change.
    /// </summary>
    /// <param name="schemaId">The schema identifier.</param>
    /// <param name="rejectedBy">Email of the rejector.</param>
    /// <param name="reason">Rejection reason.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if rejection succeeded, false otherwise.</returns>
    Task<bool> RejectSchemaAsync(
        string schemaId,
        string rejectedBy,
        string? reason = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deprecates an approved schema.
    /// </summary>
    /// <param name="schemaId">The schema identifier.</param>
    /// <param name="deprecatedBy">Email of the person deprecating the schema.</param>
    /// <param name="reason">Optional deprecation reason.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if deprecation succeeded, false otherwise.</returns>
    Task<bool> DeprecateSchemaAsync(
        string schemaId,
        string deprecatedBy,
        string? reason = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an approval request for a schema.
    /// </summary>
    /// <param name="schemaId">The schema identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The approval request, or null if not found.</returns>
    Task<SchemaApprovalRequest?> GetApprovalRequestAsync(
        string schemaId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a schema approval request.
/// </summary>
public class SchemaApprovalRequest
{
    /// <summary>
    /// Unique identifier for the approval request.
    /// </summary>
    public Guid ApprovalId { get; set; } = Guid.NewGuid();

    /// <summary>
    /// The schema identifier.
    /// </summary>
    public required string SchemaId { get; set; }

    /// <summary>
    /// The new schema version.
    /// </summary>
    public required string Version { get; set; }

    /// <summary>
    /// Email of the person who requested approval.
    /// </summary>
    public required string RequestedBy { get; set; }

    /// <summary>
    /// List of approver email addresses.
    /// </summary>
    public List<string> Approvers { get; set; } = new();

    /// <summary>
    /// Whether this change requires approval.
    /// Non-breaking changes are auto-approved.
    /// </summary>
    public bool RequiresApproval { get; set; }

    /// <summary>
    /// List of breaking changes detected.
    /// </summary>
    public List<BreakingChange> BreakingChanges { get; set; } = new();

    /// <summary>
    /// Current status of the approval request.
    /// </summary>
    public ApprovalStatus Status { get; set; } = ApprovalStatus.Pending;

    /// <summary>
    /// When the approval request was created.
    /// </summary>
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the approval request was responded to.
    /// </summary>
    public DateTime? RespondedAt { get; set; }

    /// <summary>
    /// Email of the person who responded.
    /// </summary>
    public string? RespondedBy { get; set; }

    /// <summary>
    /// Response reason (approval/rejection).
    /// </summary>
    public string? ResponseReason { get; set; }
}
