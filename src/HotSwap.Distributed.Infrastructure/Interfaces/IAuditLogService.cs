using HotSwap.Distributed.Infrastructure.Data.Entities;

namespace HotSwap.Distributed.Infrastructure.Interfaces;

/// <summary>
/// Service for persisting audit log entries to PostgreSQL.
/// Provides methods for logging deployment, approval, authentication, and configuration events.
/// </summary>
public interface IAuditLogService
{
    /// <summary>
    /// Logs a deployment pipeline event.
    /// </summary>
    /// <param name="auditLog">Main audit log entry</param>
    /// <param name="deploymentEvent">Deployment-specific details</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The created audit log ID</returns>
    Task<long> LogDeploymentEventAsync(
        AuditLog auditLog,
        DeploymentAuditEvent deploymentEvent,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Logs an approval workflow event.
    /// </summary>
    /// <param name="auditLog">Main audit log entry</param>
    /// <param name="approvalEvent">Approval-specific details</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The created audit log ID</returns>
    Task<long> LogApprovalEventAsync(
        AuditLog auditLog,
        ApprovalAuditEvent approvalEvent,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Logs an authentication event.
    /// </summary>
    /// <param name="auditLog">Main audit log entry</param>
    /// <param name="authenticationEvent">Authentication-specific details</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The created audit log ID</returns>
    Task<long> LogAuthenticationEventAsync(
        AuditLog auditLog,
        AuthenticationAuditEvent authenticationEvent,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Logs a configuration change event.
    /// </summary>
    /// <param name="auditLog">Main audit log entry</param>
    /// <param name="configurationEvent">Configuration-specific details</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The created audit log ID</returns>
    Task<long> LogConfigurationEventAsync(
        AuditLog auditLog,
        ConfigurationAuditEvent configurationEvent,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves audit logs by event category with pagination.
    /// </summary>
    /// <param name="category">Event category to filter by</param>
    /// <param name="pageNumber">Page number (1-based)</param>
    /// <param name="pageSize">Number of items per page</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of audit logs for the specified category</returns>
    Task<List<AuditLog>> GetAuditLogsByCategoryAsync(
        string category,
        int pageNumber = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves audit logs by trace ID for distributed tracing correlation.
    /// </summary>
    /// <param name="traceId">OpenTelemetry trace ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of audit logs with the specified trace ID</returns>
    Task<List<AuditLog>> GetAuditLogsByTraceIdAsync(
        string traceId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves deployment audit events for a specific deployment execution.
    /// </summary>
    /// <param name="deploymentExecutionId">Deployment execution ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of deployment audit events</returns>
    Task<List<DeploymentAuditEvent>> GetDeploymentEventsAsync(
        Guid deploymentExecutionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves approval audit events for a specific deployment execution.
    /// </summary>
    /// <param name="deploymentExecutionId">Deployment execution ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of approval audit events</returns>
    Task<List<ApprovalAuditEvent>> GetApprovalEventsAsync(
        Guid deploymentExecutionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves authentication events for a specific user.
    /// </summary>
    /// <param name="username">Username to query</param>
    /// <param name="startDate">Start date (UTC) for filtering</param>
    /// <param name="endDate">End date (UTC) for filtering</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of authentication audit events</returns>
    Task<List<AuthenticationAuditEvent>> GetAuthenticationEventsAsync(
        string username,
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes audit logs older than the specified retention period.
    /// Used by background cleanup service to enforce retention policy.
    /// </summary>
    /// <param name="retentionDays">Number of days to retain audit logs</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of audit log entries deleted</returns>
    Task<int> DeleteOldAuditLogsAsync(
        int retentionDays,
        CancellationToken cancellationToken = default);
}
