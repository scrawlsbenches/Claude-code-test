using HotSwap.Distributed.Infrastructure.Data;
using HotSwap.Distributed.Infrastructure.Data.Entities;
using HotSwap.Distributed.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HotSwap.Distributed.Infrastructure.Services;

/// <summary>
/// Service for persisting audit log entries to PostgreSQL.
/// Implements comprehensive audit logging for deployment, approval, authentication, and configuration events.
/// </summary>
public class AuditLogService : IAuditLogService
{
    private readonly AuditLogDbContext _dbContext;
    private readonly ILogger<AuditLogService> _logger;

    public AuditLogService(
        AuditLogDbContext dbContext,
        ILogger<AuditLogService> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<long> LogDeploymentEventAsync(
        AuditLog auditLog,
        DeploymentAuditEvent deploymentEvent,
        CancellationToken cancellationToken = default)
    {
        if (auditLog == null)
            throw new ArgumentNullException(nameof(auditLog));

        if (deploymentEvent == null)
            throw new ArgumentNullException(nameof(deploymentEvent));

        try
        {
            // Add audit log first to get the ID
            _dbContext.AuditLogs.Add(auditLog);
            await _dbContext.SaveChangesAsync(cancellationToken);

            // Link deployment event to audit log
            deploymentEvent.AuditLogId = auditLog.Id;
            _dbContext.DeploymentAuditEvents.Add(deploymentEvent);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Logged deployment event: {EventType} for {ModuleName} v{ModuleVersion} (AuditLogId: {AuditLogId})",
                auditLog.EventType,
                deploymentEvent.ModuleName,
                deploymentEvent.ModuleVersion,
                auditLog.Id);

            return auditLog.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to log deployment event: {EventType} for {ModuleName}",
                auditLog.EventType,
                deploymentEvent.ModuleName);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<long> LogApprovalEventAsync(
        AuditLog auditLog,
        ApprovalAuditEvent approvalEvent,
        CancellationToken cancellationToken = default)
    {
        if (auditLog == null)
            throw new ArgumentNullException(nameof(auditLog));

        if (approvalEvent == null)
            throw new ArgumentNullException(nameof(approvalEvent));

        try
        {
            // Add audit log first to get the ID
            _dbContext.AuditLogs.Add(auditLog);
            await _dbContext.SaveChangesAsync(cancellationToken);

            // Link approval event to audit log
            approvalEvent.AuditLogId = auditLog.Id;
            _dbContext.ApprovalAuditEvents.Add(approvalEvent);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Logged approval event: {EventType} for deployment {DeploymentId} (AuditLogId: {AuditLogId})",
                auditLog.EventType,
                approvalEvent.DeploymentExecutionId,
                auditLog.Id);

            return auditLog.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to log approval event: {EventType} for deployment {DeploymentId}",
                auditLog.EventType,
                approvalEvent.DeploymentExecutionId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<long> LogAuthenticationEventAsync(
        AuditLog auditLog,
        AuthenticationAuditEvent authenticationEvent,
        CancellationToken cancellationToken = default)
    {
        if (auditLog == null)
            throw new ArgumentNullException(nameof(auditLog));

        if (authenticationEvent == null)
            throw new ArgumentNullException(nameof(authenticationEvent));

        try
        {
            // Add audit log first to get the ID
            _dbContext.AuditLogs.Add(auditLog);
            await _dbContext.SaveChangesAsync(cancellationToken);

            // Link authentication event to audit log
            authenticationEvent.AuditLogId = auditLog.Id;
            _dbContext.AuthenticationAuditEvents.Add(authenticationEvent);
            await _dbContext.SaveChangesAsync(cancellationToken);

            if (authenticationEvent.IsSuspicious)
            {
                _logger.LogWarning(
                    "SUSPICIOUS: Authentication {Result} for user {Username} from IP {SourceIp} (AuditLogId: {AuditLogId})",
                    authenticationEvent.AuthenticationResult,
                    authenticationEvent.Username,
                    authenticationEvent.SourceIp,
                    auditLog.Id);
            }
            else
            {
                _logger.LogInformation(
                    "Logged authentication event: {EventType} for user {Username} (AuditLogId: {AuditLogId})",
                    auditLog.EventType,
                    authenticationEvent.Username,
                    auditLog.Id);
            }

            return auditLog.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to log authentication event: {EventType} for user {Username}",
                auditLog.EventType,
                authenticationEvent.Username);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<long> LogConfigurationEventAsync(
        AuditLog auditLog,
        ConfigurationAuditEvent configurationEvent,
        CancellationToken cancellationToken = default)
    {
        if (auditLog == null)
            throw new ArgumentNullException(nameof(auditLog));

        if (configurationEvent == null)
            throw new ArgumentNullException(nameof(configurationEvent));

        try
        {
            // Add audit log first to get the ID
            _dbContext.AuditLogs.Add(auditLog);
            await _dbContext.SaveChangesAsync(cancellationToken);

            // Link configuration event to audit log
            configurationEvent.AuditLogId = auditLog.Id;
            _dbContext.ConfigurationAuditEvents.Add(configurationEvent);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Logged configuration event: {EventType} for key {ConfigKey} (AuditLogId: {AuditLogId})",
                auditLog.EventType,
                configurationEvent.ConfigurationKey,
                auditLog.Id);

            return auditLog.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to log configuration event: {EventType} for key {ConfigKey}",
                auditLog.EventType,
                configurationEvent.ConfigurationKey);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<AuditLog>> GetAuditLogsByCategoryAsync(
        string category,
        int pageNumber = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(category))
            throw new ArgumentException("Category cannot be null or empty", nameof(category));

        if (pageNumber < 1)
            throw new ArgumentOutOfRangeException(nameof(pageNumber), "Page number must be >= 1");

        if (pageSize < 1 || pageSize > 1000)
            throw new ArgumentOutOfRangeException(nameof(pageSize), "Page size must be between 1 and 1000");

        try
        {
            var skip = (pageNumber - 1) * pageSize;

            var logs = await _dbContext.AuditLogs
                .Where(a => a.EventCategory == category)
                .OrderByDescending(a => a.Timestamp)
                .Skip(skip)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return logs;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to retrieve audit logs by category: {Category}",
                category);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<AuditLog>> GetAuditLogsByTraceIdAsync(
        string traceId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(traceId))
            throw new ArgumentException("Trace ID cannot be null or empty", nameof(traceId));

        try
        {
            var logs = await _dbContext.AuditLogs
                .Where(a => a.TraceId == traceId)
                .OrderBy(a => a.Timestamp)
                .ToListAsync(cancellationToken);

            return logs;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to retrieve audit logs by trace ID: {TraceId}",
                traceId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<DeploymentAuditEvent>> GetDeploymentEventsAsync(
        Guid deploymentExecutionId,
        CancellationToken cancellationToken = default)
    {
        if (deploymentExecutionId == Guid.Empty)
            throw new ArgumentException("Deployment execution ID cannot be empty", nameof(deploymentExecutionId));

        try
        {
            var events = await _dbContext.DeploymentAuditEvents
                .Where(d => d.DeploymentExecutionId == deploymentExecutionId)
                .OrderBy(d => d.StartTime)
                .ToListAsync(cancellationToken);

            return events;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to retrieve deployment events for execution ID: {DeploymentExecutionId}",
                deploymentExecutionId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<ApprovalAuditEvent>> GetApprovalEventsAsync(
        Guid deploymentExecutionId,
        CancellationToken cancellationToken = default)
    {
        if (deploymentExecutionId == Guid.Empty)
            throw new ArgumentException("Deployment execution ID cannot be empty", nameof(deploymentExecutionId));

        try
        {
            var events = await _dbContext.ApprovalAuditEvents
                .Where(a => a.DeploymentExecutionId == deploymentExecutionId)
                .OrderBy(a => a.CreatedAt)
                .ToListAsync(cancellationToken);

            return events;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to retrieve approval events for deployment execution ID: {DeploymentExecutionId}",
                deploymentExecutionId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<AuthenticationAuditEvent>> GetAuthenticationEventsAsync(
        string username,
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username))
            throw new ArgumentException("Username cannot be null or empty", nameof(username));

        try
        {
            var query = _dbContext.AuthenticationAuditEvents
                .Where(a => a.Username == username);

            if (startDate.HasValue)
            {
                query = query.Where(a => a.CreatedAt >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                query = query.Where(a => a.CreatedAt <= endDate.Value);
            }

            var events = await query
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync(cancellationToken);

            return events;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to retrieve authentication events for user: {Username}",
                username);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<int> DeleteOldAuditLogsAsync(
        int retentionDays,
        CancellationToken cancellationToken = default)
    {
        if (retentionDays < 1)
            throw new ArgumentOutOfRangeException(nameof(retentionDays), "Retention days must be >= 1");

        try
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);

            var oldLogs = await _dbContext.AuditLogs
                .Where(a => a.CreatedAt < cutoffDate)
                .ToListAsync(cancellationToken);

            if (oldLogs.Any())
            {
                _dbContext.AuditLogs.RemoveRange(oldLogs);
                await _dbContext.SaveChangesAsync(cancellationToken);

                _logger.LogInformation(
                    "Deleted {Count} audit log entries older than {CutoffDate}",
                    oldLogs.Count,
                    cutoffDate);
            }

            return oldLogs.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to delete old audit logs with retention period: {RetentionDays} days",
                retentionDays);
            throw;
        }
    }
}
