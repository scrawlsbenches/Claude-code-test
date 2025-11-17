using System.Collections.Concurrent;
using System.Diagnostics;
using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Orchestrator.Interfaces;
using Microsoft.Extensions.Logging;

namespace HotSwap.Distributed.Orchestrator.Schema;

/// <summary>
/// Service for managing schema approval workflows.
/// </summary>
public class SchemaApprovalService : ISchemaApprovalService
{
    private static readonly ActivitySource ActivitySource = new("HotSwap.SchemaApproval", "1.0.0");
    private readonly ISchemaRegistry _schemaRegistry;
    private readonly ISchemaCompatibilityChecker _compatibilityChecker;
    private readonly ILogger<SchemaApprovalService> _logger;
    private readonly ConcurrentDictionary<string, SchemaApprovalRequest> _approvalRequests;

    public SchemaApprovalService(
        ISchemaRegistry schemaRegistry,
        ISchemaCompatibilityChecker compatibilityChecker,
        ILogger<SchemaApprovalService> logger)
    {
        _schemaRegistry = schemaRegistry ?? throw new ArgumentNullException(nameof(schemaRegistry));
        _compatibilityChecker = compatibilityChecker ?? throw new ArgumentNullException(nameof(compatibilityChecker));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _approvalRequests = new ConcurrentDictionary<string, SchemaApprovalRequest>();
    }

    /// <inheritdoc/>
    public async Task<SchemaApprovalRequest> RequestApprovalAsync(
        string schemaId,
        MessageSchema newSchema,
        string requestedBy,
        List<string> approvers,
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("SchemaApproval.RequestApproval");
        activity?.SetTag("schema_id", schemaId);
        activity?.SetTag("requested_by", requestedBy);

        ArgumentNullException.ThrowIfNull(requestedBy, nameof(requestedBy));
        if (approvers == null || approvers.Count == 0)
        {
            throw new ArgumentException("At least one approver must be specified", nameof(approvers));
        }

        // Check if existing schema version exists
        var existingSchema = await _schemaRegistry.GetSchemaAsync(schemaId, cancellationToken);

        // First version doesn't require approval
        if (existingSchema == null)
        {
            _logger.LogInformation("Schema '{SchemaId}' is the first version, auto-approved", schemaId);

            var autoApprovedRequest = new SchemaApprovalRequest
            {
                SchemaId = schemaId,
                Version = newSchema.Version,
                RequestedBy = requestedBy,
                Approvers = approvers,
                RequiresApproval = false,
                Status = ApprovalStatus.AutoApproved,
                RespondedAt = DateTime.UtcNow
            };

            _approvalRequests.TryAdd(schemaId, autoApprovedRequest);
            return autoApprovedRequest;
        }

        // Check compatibility with existing schema
        var compatibilityResult = await _compatibilityChecker.CheckCompatibilityAsync(
            existingSchema,
            newSchema,
            newSchema.Compatibility,
            cancellationToken);

        activity?.SetTag("is_compatible", compatibilityResult.IsCompatible);
        activity?.SetTag("breaking_changes_count", compatibilityResult.BreakingChanges.Count);

        // Non-breaking changes are auto-approved
        if (compatibilityResult.IsCompatible)
        {
            _logger.LogInformation(
                "Schema '{SchemaId}' version '{Version}' has no breaking changes, auto-approved",
                schemaId,
                newSchema.Version);

            var autoApprovedRequest = new SchemaApprovalRequest
            {
                SchemaId = schemaId,
                Version = newSchema.Version,
                RequestedBy = requestedBy,
                Approvers = approvers,
                RequiresApproval = false,
                Status = ApprovalStatus.AutoApproved,
                RespondedAt = DateTime.UtcNow
            };

            _approvalRequests.TryAdd(schemaId, autoApprovedRequest);
            return autoApprovedRequest;
        }

        // Breaking changes require approval
        _logger.LogWarning(
            "Schema '{SchemaId}' version '{Version}' has {Count} breaking changes, approval required",
            schemaId,
            newSchema.Version,
            compatibilityResult.BreakingChanges.Count);

        var approvalRequest = new SchemaApprovalRequest
        {
            SchemaId = schemaId,
            Version = newSchema.Version,
            RequestedBy = requestedBy,
            Approvers = approvers,
            RequiresApproval = true,
            BreakingChanges = compatibilityResult.BreakingChanges,
            Status = ApprovalStatus.Pending
        };

        _approvalRequests.TryAdd(schemaId, approvalRequest);

        // Update schema status to PendingApproval
        await _schemaRegistry.UpdateSchemaStatusAsync(
            schemaId,
            SchemaStatus.PendingApproval,
            approvedBy: null,
            cancellationToken);

        activity?.SetStatus(ActivityStatusCode.Ok);
        return approvalRequest;
    }

    /// <inheritdoc/>
    public async Task<bool> ApproveSchemaAsync(
        string schemaId,
        string approvedBy,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("SchemaApproval.Approve");
        activity?.SetTag("schema_id", schemaId);
        activity?.SetTag("approved_by", approvedBy);

        ArgumentNullException.ThrowIfNull(approvedBy, nameof(approvedBy));

        var schema = await _schemaRegistry.GetSchemaAsync(schemaId, cancellationToken);
        if (schema == null)
        {
            _logger.LogWarning("Schema '{SchemaId}' not found for approval", schemaId);
            return false;
        }

        if (schema.Status != SchemaStatus.PendingApproval)
        {
            throw new InvalidOperationException(
                $"Cannot approve schema '{schemaId}' with status '{schema.Status}'. Only schemas with status 'PendingApproval' can be approved.");
        }

        var success = await _schemaRegistry.UpdateSchemaStatusAsync(
            schemaId,
            SchemaStatus.Approved,
            approvedBy,
            cancellationToken);

        if (success && _approvalRequests.TryGetValue(schemaId, out var approvalRequest))
        {
            approvalRequest.Status = ApprovalStatus.Approved;
            approvalRequest.RespondedAt = DateTime.UtcNow;
            approvalRequest.RespondedBy = approvedBy;
            approvalRequest.ResponseReason = reason;
        }

        _logger.LogInformation(
            "Schema '{SchemaId}' approved by '{ApprovedBy}'. Reason: {Reason}",
            schemaId,
            approvedBy,
            reason ?? "No reason provided");

        activity?.SetStatus(ActivityStatusCode.Ok);
        return success;
    }

    /// <inheritdoc/>
    public async Task<bool> RejectSchemaAsync(
        string schemaId,
        string rejectedBy,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("SchemaApproval.Reject");
        activity?.SetTag("schema_id", schemaId);
        activity?.SetTag("rejected_by", rejectedBy);

        ArgumentNullException.ThrowIfNull(rejectedBy, nameof(rejectedBy));

        var schema = await _schemaRegistry.GetSchemaAsync(schemaId, cancellationToken);
        if (schema == null)
        {
            _logger.LogWarning("Schema '{SchemaId}' not found for rejection", schemaId);
            return false;
        }

        if (schema.Status != SchemaStatus.PendingApproval)
        {
            throw new InvalidOperationException(
                $"Cannot reject schema '{schemaId}' with status '{schema.Status}'. Only schemas with status 'PendingApproval' can be rejected.");
        }

        var success = await _schemaRegistry.UpdateSchemaStatusAsync(
            schemaId,
            SchemaStatus.Rejected,
            rejectedBy,
            cancellationToken);

        if (success && _approvalRequests.TryGetValue(schemaId, out var approvalRequest))
        {
            approvalRequest.Status = ApprovalStatus.Rejected;
            approvalRequest.RespondedAt = DateTime.UtcNow;
            approvalRequest.RespondedBy = rejectedBy;
            approvalRequest.ResponseReason = reason;
        }

        _logger.LogWarning(
            "Schema '{SchemaId}' rejected by '{RejectedBy}'. Reason: {Reason}",
            schemaId,
            rejectedBy,
            reason ?? "No reason provided");

        activity?.SetStatus(ActivityStatusCode.Ok);
        return success;
    }

    /// <inheritdoc/>
    public async Task<bool> DeprecateSchemaAsync(
        string schemaId,
        string deprecatedBy,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("SchemaApproval.Deprecate");
        activity?.SetTag("schema_id", schemaId);
        activity?.SetTag("deprecated_by", deprecatedBy);

        ArgumentNullException.ThrowIfNull(deprecatedBy, nameof(deprecatedBy));

        var schema = await _schemaRegistry.GetSchemaAsync(schemaId, cancellationToken);
        if (schema == null)
        {
            _logger.LogWarning("Schema '{SchemaId}' not found for deprecation", schemaId);
            return false;
        }

        if (schema.Status != SchemaStatus.Approved)
        {
            throw new InvalidOperationException(
                $"Cannot deprecate schema '{schemaId}' with status '{schema.Status}'. Only approved schemas can be deprecated.");
        }

        var success = await _schemaRegistry.UpdateSchemaStatusAsync(
            schemaId,
            SchemaStatus.Deprecated,
            deprecatedBy,
            cancellationToken);

        _logger.LogInformation(
            "Schema '{SchemaId}' deprecated by '{DeprecatedBy}'. Reason: {Reason}",
            schemaId,
            deprecatedBy,
            reason ?? "No reason provided");

        activity?.SetStatus(ActivityStatusCode.Ok);
        return success;
    }

    /// <inheritdoc/>
    public Task<SchemaApprovalRequest?> GetApprovalRequestAsync(
        string schemaId,
        CancellationToken cancellationToken = default)
    {
        _approvalRequests.TryGetValue(schemaId, out var approvalRequest);
        return Task.FromResult(approvalRequest);
    }
}
