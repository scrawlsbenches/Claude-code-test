using System.Collections.Concurrent;
using System.Diagnostics;
using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Data.Entities;
using HotSwap.Distributed.Infrastructure.Interfaces;
using HotSwap.Distributed.Orchestrator.Interfaces;
using Microsoft.Extensions.Logging;

namespace HotSwap.Distributed.Orchestrator.Services;

/// <summary>
/// Service for managing deployment approval workflows.
/// </summary>
public class ApprovalService : IApprovalService
{
    private readonly ILogger<ApprovalService> _logger;
    private readonly PipelineConfiguration _config;
    private readonly INotificationService? _notificationService;
    private readonly IAuditLogService? _auditLogService;

    // In-memory storage for approval requests
    // In production, this would be backed by a database
    private static readonly ConcurrentDictionary<Guid, ApprovalRequest> _approvalRequests = new();

    // Semaphore for waiting on approval decisions
    private static readonly ConcurrentDictionary<Guid, TaskCompletionSource<ApprovalRequest>> _approvalWaiters = new();

    public ApprovalService(
        ILogger<ApprovalService> logger,
        PipelineConfiguration config,
        INotificationService? notificationService = null,
        IAuditLogService? auditLogService = null)
    {
        _logger = logger;
        _config = config;
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

        // Store the approval request
        if (!_approvalRequests.TryAdd(request.DeploymentExecutionId, request))
        {
            throw new InvalidOperationException(
                $"Approval request already exists for deployment {request.DeploymentExecutionId}");
        }

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

        if (!_approvalRequests.TryGetValue(decision.DeploymentExecutionId, out var request))
        {
            throw new KeyNotFoundException(
                $"Approval request not found for deployment {decision.DeploymentExecutionId}");
        }

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

        if (!_approvalRequests.TryGetValue(decision.DeploymentExecutionId, out var request))
        {
            throw new KeyNotFoundException(
                $"Approval request not found for deployment {decision.DeploymentExecutionId}");
        }

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
    public Task<ApprovalRequest?> GetApprovalRequestAsync(
        Guid deploymentExecutionId,
        CancellationToken cancellationToken = default)
    {
        _approvalRequests.TryGetValue(deploymentExecutionId, out var request);
        return Task.FromResult(request);
    }

    /// <inheritdoc />
    public Task<List<ApprovalRequest>> GetPendingApprovalsAsync(
        CancellationToken cancellationToken = default)
    {
        var pending = _approvalRequests.Values
            .Where(r => r.IsPending)
            .OrderBy(r => r.RequestedAt)
            .ToList();

        _logger.LogInformation("Found {Count} pending approval requests", pending.Count);

        return Task.FromResult(pending);
    }

    /// <inheritdoc />
    public async Task<ApprovalRequest> WaitForApprovalAsync(
        Guid deploymentExecutionId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Waiting for approval decision for deployment {DeploymentId}",
            deploymentExecutionId);

        if (!_approvalRequests.TryGetValue(deploymentExecutionId, out var request))
        {
            throw new KeyNotFoundException(
                $"Approval request not found for deployment {deploymentExecutionId}");
        }

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
                // Timeout occurred
                _logger.LogWarning(
                    "Approval request {ApprovalId} for deployment {DeploymentId} has timed out",
                    request.ApprovalId, deploymentExecutionId);

                request.Status = ApprovalStatus.Expired;
                request.RespondedAt = DateTime.UtcNow;
                request.ResponseReason = "Approval request timed out after 24 hours";

                tcs.TrySetResult(request);
            }
        }
        else
        {
            // Already expired
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
        var expiredCount = 0;
        var now = DateTime.UtcNow;

        foreach (var request in _approvalRequests.Values)
        {
            if (request.Status == ApprovalStatus.Pending && now >= request.TimeoutAt)
            {
                _logger.LogWarning(
                    "Auto-rejecting expired approval request {ApprovalId} for deployment {DeploymentId}",
                    request.ApprovalId, request.DeploymentExecutionId);

                request.Status = ApprovalStatus.Expired;
                request.RespondedAt = now;
                request.ResponseReason = "Approval request automatically rejected due to timeout";

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

                expiredCount++;
            }
        }

        if (expiredCount > 0)
        {
            _logger.LogInformation("Auto-rejected {Count} expired approval requests", expiredCount);
        }

        return expiredCount;
    }

    /// <summary>
    /// Clears all approval requests and waiters from the in-memory storage.
    /// WARNING: This method is for testing purposes only and should not be used in production.
    /// </summary>
    public void ClearAllApprovalsForTesting()
    {
        _approvalRequests.Clear();
        _approvalWaiters.Clear();
        _logger.LogDebug("Cleared all approval requests and waiters (testing only)");
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
                // IsExpired is a computed column, cannot be set
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
}
