using HotSwap.Distributed.Orchestrator.Interfaces;

namespace HotSwap.Distributed.Api.Services;

/// <summary>
/// Background service that periodically checks for and processes expired approval requests.
/// </summary>
public class ApprovalTimeoutBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ApprovalTimeoutBackgroundService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(5); // Check every 5 minutes

    public ApprovalTimeoutBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<ApprovalTimeoutBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Approval Timeout Background Service starting. Will check for expired approvals every {Interval}",
            _checkInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessExpiredApprovalsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing expired approvals");
            }

            // Wait before next check
            await Task.Delay(_checkInterval, stoppingToken);
        }

        _logger.LogInformation("Approval Timeout Background Service stopping");
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
        }
    }
}
