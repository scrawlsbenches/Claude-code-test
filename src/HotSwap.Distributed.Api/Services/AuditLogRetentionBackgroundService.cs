using HotSwap.Distributed.Infrastructure.Interfaces;

namespace HotSwap.Distributed.Api.Services;

/// <summary>
/// Background service that periodically cleans up old audit log entries based on retention policy.
/// Runs daily to enforce the configured audit log retention period.
/// </summary>
public class AuditLogRetentionBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AuditLogRetentionBackgroundService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromDays(1); // Check daily
    private readonly int _retentionDays = 90; // Default retention: 90 days

    public AuditLogRetentionBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<AuditLogRetentionBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Audit Log Retention Background Service starting. Will clean up logs older than {RetentionDays} days every {Interval}",
            _retentionDays,
            _checkInterval);

        // Wait before first execution to allow the application to fully start
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupOldAuditLogsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up old audit logs");
            }

            // Wait before next check
            try
            {
                await Task.Delay(_checkInterval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // Expected when the service is stopping
                break;
            }
        }

        _logger.LogInformation("Audit Log Retention Background Service stopping");
    }

    private async Task CleanupOldAuditLogsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var auditLogService = scope.ServiceProvider.GetService<IAuditLogService>();

        if (auditLogService == null)
        {
            _logger.LogWarning("AuditLogService not registered, skipping audit log cleanup");
            return;
        }

        try
        {
            var deletedCount = await auditLogService.DeleteOldAuditLogsAsync(_retentionDays, cancellationToken);

            if (deletedCount > 0)
            {
                _logger.LogInformation(
                    "Deleted {Count} audit log entries older than {RetentionDays} days",
                    deletedCount,
                    _retentionDays);
            }
            else
            {
                _logger.LogDebug(
                    "No audit logs older than {RetentionDays} days found for cleanup",
                    _retentionDays);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete old audit logs");
        }
    }
}
