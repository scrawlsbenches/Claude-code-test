using System.Collections.Concurrent;
using System.Diagnostics;
using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Data.Entities;
using HotSwap.Distributed.Infrastructure.Interfaces;
using HotSwap.Distributed.Infrastructure.Repositories;
using HotSwap.Distributed.Orchestrator.Interfaces;
using Microsoft.Extensions.Logging;

namespace HotSwap.Distributed.Orchestrator.Services;

/// <summary>
/// Service for managing deployment approval workflows.
/// Refactored to use PostgreSQL persistence instead of static in-memory storage.
/// </summary>
public class ApprovalServiceRefactored : IApprovalService
{
    private readonly ILogger<ApprovalServiceRefactored> _logger;
    private readonly PipelineConfiguration _config;
    private readonly INotificationService? _notificationService;
    private readonly IAuditLogService? _auditLogService;
    private readonly IApprovalRepository _approvalRepository;

    // Keep in-memory waiters for process-local signaling (TaskCompletionSource can't be serialized)
    // This is fine - it's just for signaling within a single process instance
    private static readonly ConcurrentDictionary<Guid, TaskCompletionSource<ApprovalRequest>> _approvalWaiters = new();

    public ApprovalServiceRefactored(
        ILogger<ApprovalServiceRefactored> logger,
        PipelineConfiguration config,
        IApprovalRepository approvalRepository,
        INotificationService? notificationService = null,
        IAuditLogService? auditLogService = null)
    {
        _logger = logger;
        _config = config;
        _approvalRepository = approvalRepository;
        _notificationService = notificationService;
        _auditLogService = auditLogService;
    }

    /// <inheritdoc />
    public async Task<ApprovalRequest> CreateApprovalRequestAsync(
        ApprovalRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Creating approval request for deployment {DeploymentId} to {Environment}",
            request.DeploymentExecutionId, request.TargetEnvironment);

        // Set timeout based on configuration
        if (request.TimeoutAt == default)
        {
            request.TimeoutAt = DateTime.UtcNow.Add(_config.ApprovalTimeout);
        }

        // Convert domain model to entity
        var entity = ToEntity(request);

        // Store in database
        await _approvalRepository.CreateAsync(entity, cancellationToken);

        // Create a task completion source for waiting
        _approvalWaiters.TryAdd(request.DeploymentExecutionId, new TaskCompletionSource<ApprovalRequest>());

        _logger.LogInformation(
            "Approval request {ApprovalId} created for deployment {DeploymentId}, timeout at {TimeoutAt}",
            request.ApprovalId, request.DeploymentExecutionId, request.TimeoutAt);

        // Send notification to approvers
        if (_notificationService != null)
        {
            await _notificationService.SendApprovalRequestNotificationAsync(request, cancellationToken);
        }

        // Audit log: Approval request created
        await LogApprovalEventAsync(
            "ApprovalRequested",
            "Pending",
            request,
            "Approval request created and awaiting decision",
            cancellationToken);

        return request;
    }

    /// <inheritdoc />
    public async Task<ApprovalRequest> ApproveDeploymentAsync(
        ApprovalDecision decision,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Processing approval for deployment {DeploymentId} by {Approver}",
            decision.DeploymentExecutionId, decision.ApproverEmail);

        var entity = await _approvalRepository.GetByIdAsync(decision.DeploymentExecutionId, cancellationToken);
        if (entity == null)
        {
            throw new KeyNotFoundException(
                $"Approval request not found for deployment {decision.DeploymentExecutionId}");
        }

        var request = ToModel(entity);

        // Check if approval is still pending
        if (request.Status != ApprovalStatus.Pending)
        {
            throw new InvalidOperationException(
                $"Approval request is in {request.Status} status and cannot be approved");
        }

        // Check if expired
        if (request.IsExpired)
        {
            request.Status = ApprovalStatus.Expired;
            entity.Status = ApprovalStatus.Expired;
            await _approvalRepository.UpdateAsync(entity, cancellationToken);

            throw new InvalidOperationException(
                $"Approval request has expired at {request.TimeoutAt}");
        }

        // Validate approver is authorized
        if (request.ApproverEmails.Any() && !request.ApproverEmails.Contains(decision.ApproverEmail))
        {
            throw new UnauthorizedAccessException(
                $"User {decision.ApproverEmail} is not authorized to approve this deployment");
        }

        // Update request status
        request.Status = ApprovalStatus.Approved;
        request.RespondedAt = decision.DecidedAt;
        request.RespondedByEmail = decision.ApproverEmail;
        request.ResponseReason = decision.Reason;

        // Update entity
        entity.Status = ApprovalStatus.Approved;
        entity.RespondedAt = decision.DecidedAt;
        entity.RespondedByEmail = decision.ApproverEmail;
        entity.ResponseReason = decision.Reason;

        await _approvalRepository.UpdateAsync(entity, cancellationToken);

        _logger.LogInformation(
            "Deployment {DeploymentId} approved by {Approver}. Reason: {Reason}",
            decision.DeploymentExecutionId, decision.ApproverEmail, decision.Reason ?? "None");

        // Send notification
        if (_notificationService != null)
        {
            await _notificationService.SendApprovalGrantedNotificationAsync(request, cancellationToken);
        }

        // Signal any waiters
        if (_approvalWaiters.TryGetValue(decision.DeploymentExecutionId, out var tcs))
        {
            tcs.TrySetResult(request);
        }

        // Audit log: Approval granted
        await LogApprovalEventAsync(
            "ApprovalGranted",
            "Approved",
            request,
            $"Approved by {decision.ApproverEmail}. Reason: {decision.Reason ?? "None"}",
            cancellationToken);

        return request;
    }

    /// <inheritdoc />
    public async Task<ApprovalRequest> RejectDeploymentAsync(
        ApprovalDecision decision,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Processing rejection for deployment {DeploymentId} by {Approver}",
            decision.DeploymentExecutionId, decision.ApproverEmail);

        var entity = await _approvalRepository.GetByIdAsync(decision.DeploymentExecutionId, cancellationToken);
        if (entity == null)
        {
            throw new KeyNotFoundException(
                $"Approval request not found for deployment {decision.DeploymentExecutionId}");
        }

        var request = ToModel(entity);

        // Check if approval is still pending
        if (request.Status != ApprovalStatus.Pending)
        {
            throw new InvalidOperationException(
                $"Approval request is in {request.Status} status and cannot be rejected");
        }

        // Validate approver is authorized
        if (request.ApproverEmails.Any() && !request.ApproverEmails.Contains(decision.ApproverEmail))
        {
            throw new UnauthorizedAccessException(
                $"User {decision.ApproverEmail} is not authorized to reject this deployment");
        }

        // Update request status
        request.Status = ApprovalStatus.Rejected;
        request.RespondedAt = decision.DecidedAt;
        request.RespondedByEmail = decision.ApproverEmail;
        request.ResponseReason = decision.Reason;

        // Update entity
        entity.Status = ApprovalStatus.Rejected;
        entity.RespondedAt = decision.DecidedAt;
        entity.RespondedByEmail = decision.ApproverEmail;
        entity.ResponseReason = decision.Reason;

        await _approvalRepository.UpdateAsync(entity, cancellationToken);

        _logger.LogWarning(
            "Deployment {DeploymentId} rejected by {Approver}. Reason: {Reason}",
            decision.DeploymentExecutionId, decision.ApproverEmail, decision.Reason ?? "None");

        // Send notification
        if (_notificationService != null)
        {
            await _notificationService.SendApprovalRejectedNotificationAsync(request, cancellationToken);
        }

        // Signal any waiters
        if (_approvalWaiters.TryGetValue(decision.DeploymentExecutionId, out var tcs))
        {
            tcs.TrySetResult(request);
        }

        // Audit log: Approval rejected
        await LogApprovalEventAsync(
            "ApprovalRejected",
            "Rejected",
            request,
            $"Rejected by {decision.ApproverEmail}. Reason: {decision.Reason ?? "None"}",
            cancellationToken);

        return request;
    }

    /// <inheritdoc />
    public async Task<ApprovalRequest?> GetApprovalRequestAsync(
        Guid deploymentExecutionId,
        CancellationToken cancellationToken = default)
    {
        var entity = await _approvalRepository.GetByIdAsync(deploymentExecutionId, cancellationToken);
        return entity == null ? null : ToModel(entity);
    }

    /// <inheritdoc />
    public async Task<List<ApprovalRequest>> GetPendingApprovalsAsync(
        CancellationToken cancellationToken = default)
    {
        var entities = await _approvalRepository.GetPendingAsync(cancellationToken);
        var pending = entities.Select(ToModel).ToList();

        _logger.LogInformation("Found {Count} pending approval requests", pending.Count);

        return pending;
    }

    /// <inheritdoc />
    public async Task<ApprovalRequest> WaitForApprovalAsync(
        Guid deploymentExecutionId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Waiting for approval decision for deployment {DeploymentId}",
            deploymentExecutionId);

        var entity = await _approvalRepository.GetByIdAsync(deploymentExecutionId, cancellationToken);
        if (entity == null)
        {
            throw new KeyNotFoundException(
                $"Approval request not found for deployment {deploymentExecutionId}");
        }

        var request = ToModel(entity);

        // If already resolved, return immediately
        if (request.IsResolved)
        {
            return request;
        }

        // Get or create task completion source
        var tcs = _approvalWaiters.GetOrAdd(
            deploymentExecutionId,
            _ => new TaskCompletionSource<ApprovalRequest>());

        // Set up timeout handling
        var timeoutDelay = request.TimeoutAt - DateTime.UtcNow;
        if (timeoutDelay > TimeSpan.Zero)
        {
            var timeoutTask = Task.Delay(timeoutDelay, cancellationToken);
            var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

            if (completedTask == timeoutTask)
            {
                // Timeout occurred - expire in database
                _logger.LogWarning(
                    "Approval request {ApprovalId} for deployment {DeploymentId} has timed out",
                    request.ApprovalId, deploymentExecutionId);

                entity.Status = ApprovalStatus.Expired;
                entity.RespondedAt = DateTime.UtcNow;
                entity.ResponseReason = "Approval request timed out after 24 hours";

                await _approvalRepository.UpdateAsync(entity, cancellationToken);

                request.Status = ApprovalStatus.Expired;
                request.RespondedAt = DateTime.UtcNow;
                request.ResponseReason = "Approval request timed out after 24 hours";

                tcs.TrySetResult(request);
            }
        }
        else
        {
            // Already expired
            entity.Status = ApprovalStatus.Expired;
            entity.RespondedAt = DateTime.UtcNow;
            entity.ResponseReason = "Approval request timed out";

            await _approvalRepository.UpdateAsync(entity, cancellationToken);

            request.Status = ApprovalStatus.Expired;
            request.RespondedAt = DateTime.UtcNow;
            request.ResponseReason = "Approval request timed out";

            tcs.TrySetResult(request);
        }

        return await tcs.Task;
    }

    /// <inheritdoc />
    public async Task<int> ProcessExpiredApprovalsAsync(
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        // Use repository's efficient bulk update
        var expiredCount = await _approvalRepository.ExpirePendingRequestsAsync(now, cancellationToken);

        if (expiredCount > 0)
        {
            _logger.LogInformation("Auto-rejected {Count} expired approval requests", expiredCount);

            // Get the expired requests to send notifications
            var expiredRequests = await _approvalRepository.GetExpiredPendingAsync(cancellationToken);

            foreach (var entity in expiredRequests)
            {
                var request = ToModel(entity);

                // Send expiry notification
                if (_notificationService != null)
                {
                    await _notificationService.SendApprovalExpiredNotificationAsync(request, cancellationToken);
                }

                // Signal any waiters
                if (_approvalWaiters.TryGetValue(request.DeploymentExecutionId, out var tcs))
                {
                    tcs.TrySetResult(request);
                }
            }
        }

        return expiredCount;
    }

    #region Helper Methods

    /// <summary>
    /// Converts domain model to database entity.
    /// </summary>
    private static ApprovalRequestEntity ToEntity(ApprovalRequest model)
    {
        return new ApprovalRequestEntity
        {
            DeploymentExecutionId = model.DeploymentExecutionId,
            ApprovalId = model.ApprovalId,
            RequesterEmail = model.RequesterEmail,
            TargetEnvironment = model.TargetEnvironment.ToString(),
            ModuleName = model.ModuleName,
            ModuleVersion = model.Version.ToString(),
            Status = model.Status,
            ApproverEmails = model.ApproverEmails.ToList(),
            RequestedAt = model.RequestedAt,
            TimeoutAt = model.TimeoutAt,
            RespondedAt = model.RespondedAt,
            RespondedByEmail = model.RespondedByEmail,
            ResponseReason = model.ResponseReason
        };
    }

    /// <summary>
    /// Converts database entity to domain model.
    /// </summary>
    private static ApprovalRequest ToModel(ApprovalRequestEntity entity)
    {
        return new ApprovalRequest
        {
            DeploymentExecutionId = entity.DeploymentExecutionId,
            ApprovalId = entity.ApprovalId,
            RequesterEmail = entity.RequesterEmail,
            TargetEnvironment = Enum.Parse<EnvironmentType>(entity.TargetEnvironment),
            ModuleName = entity.ModuleName,
            Version = Version.Parse(entity.ModuleVersion),
            Status = entity.Status,
            ApproverEmails = entity.ApproverEmails.ToList(),
            RequestedAt = entity.RequestedAt,
            TimeoutAt = entity.TimeoutAt,
            RespondedAt = entity.RespondedAt,
            RespondedByEmail = entity.RespondedByEmail,
            ResponseReason = entity.ResponseReason
        };
    }

    /// <summary>
    /// Helper method to log approval events to the audit log.
    /// </summary>
    private async Task LogApprovalEventAsync(
        string eventType,
        string approvalStatus,
        ApprovalRequest request,
        string message,
        CancellationToken cancellationToken)
    {
        if (_auditLogService == null)
        {
            return; // Audit logging is optional
        }

        try
        {
            var auditLog = new AuditLog
            {
                EventId = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                EventType = eventType,
                EventCategory = "Approval",
                Severity = approvalStatus == "Approved" ? "Information" : (approvalStatus == "Rejected" ? "Warning" : "Information"),
                UserId = null,
                Username = request.RespondedByEmail ?? "System",
                UserEmail = request.RespondedByEmail ?? request.RequesterEmail,
                ResourceType = "ApprovalRequest",
                ResourceId = request.ApprovalId.ToString(),
                Action = approvalStatus,
                Result = approvalStatus,
                Message = message,
                TraceId = Activity.Current?.TraceId.ToString(),
                SpanId = Activity.Current?.SpanId.ToString(),
                SourceIp = null,
                UserAgent = null,
                CreatedAt = DateTime.UtcNow
            };

            var approvalEvent = new ApprovalAuditEvent
            {
                ApprovalId = request.ApprovalId,
                DeploymentExecutionId = request.DeploymentExecutionId,
                ModuleName = request.ModuleName,
                ModuleVersion = request.Version.ToString(),
                TargetEnvironment = request.TargetEnvironment.ToString(),
                RequesterEmail = request.RequesterEmail,
                ApproverEmails = request.ApproverEmails.ToArray(),
                ApprovalStatus = approvalStatus,
                DecisionByEmail = request.RespondedByEmail,
                DecisionAt = request.RespondedAt,
                DecisionReason = request.ResponseReason,
                TimeoutAt = request.TimeoutAt,
                Metadata = null,
                CreatedAt = DateTime.UtcNow
            };

            await _auditLogService.LogApprovalEventAsync(auditLog, approvalEvent, cancellationToken);
        }
        catch (Exception ex)
        {
            // Log the error but don't fail the approval due to audit logging issues
            _logger.LogError(ex, "Failed to write audit log for approval event {EventType}", eventType);
        }
    }

    #endregion
}
