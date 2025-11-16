using HotSwap.Distributed.Api.Middleware;

namespace HotSwap.Distributed.Api.Services;

/// <summary>
/// Background service for cleaning up expired rate limit entries.
/// Replaces the static Timer in RateLimitingMiddleware for proper disposal.
/// </summary>
public class RateLimitCleanupService : BackgroundService
{
    private readonly ILogger<RateLimitCleanupService> _logger;
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromMinutes(1);

    public RateLimitCleanupService(ILogger<RateLimitCleanupService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Rate limit cleanup service starting");

        using var timer = new PeriodicTimer(_cleanupInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                RateLimitingMiddleware.CleanupExpiredEntries();
                _logger.LogDebug("Rate limit cleanup completed");
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Rate limit cleanup service stopping");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in rate limit cleanup service");
        }
    }
}
