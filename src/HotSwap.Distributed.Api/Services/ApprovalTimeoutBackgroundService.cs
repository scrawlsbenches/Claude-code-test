using HotSwap.Distributed.Orchestrator.Interfaces;
using Microsoft.Extensions.Configuration;

namespace HotSwap.Distributed.Api.Services;

/// <summary>
/// Background service that periodically checks for and processes expired approval requests.
/// </summary>
public class ApprovalTimeoutBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ApprovalTimeoutBackgroundService> _logger;
    private readonly TimeSpan _checkInterval;

    public ApprovalTimeoutBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<ApprovalTimeoutBackgroundService> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;

        // Read check interval from configuration, default to 5 minutes
        var config = new ApprovalTimeoutConfiguration();
        configuration.GetSection("ApprovalTimeout").Bind(config);
        _checkInterval = TimeSpan.FromMinutes(config.CheckIntervalMinutes);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Approval Timeout Background Service starting. Will check for expired approvals every {Interval}",
            _checkInterval);

        using var timer = new PeriodicTimer(_checkInterval);

        try
        {
            // Wait for first interval before checking (no immediate check on startup)
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await ProcessExpiredApprovalsAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Approval Timeout Background Service stopping");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in Approval Timeout Background Service");
            throw;
        }
    }

    private async Task ProcessExpiredApprovalsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var approvalService = scope.ServiceProvider.GetService<IApprovalService>();

        if (approvalService == null)
        {
            _logger.LogWarning("ApprovalService not registered, skipping expired approval check");
            return;
        }

        try
        {
            var expiredCount = await approvalService.ProcessExpiredApprovalsAsync(cancellationToken);

            if (expiredCount > 0)
            {
                _logger.LogInformation(
                    "Processed {Count} expired approval requests",
                    expiredCount);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process expired approvals");
            // Don't throw - allow service to continue on next interval
        }
    }
}

/// <summary>
/// Configuration for approval timeout background service.
/// </summary>
public class ApprovalTimeoutConfiguration
{
    /// <summary>
    /// Interval in minutes between approval timeout checks.
    /// Default: 5 minutes.
    /// Supports fractional minutes for testing (e.g., 0.00017 = ~10ms).
    /// </summary>
    public double CheckIntervalMinutes { get; set; } = 5;
}
