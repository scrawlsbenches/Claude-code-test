using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace HotSwap.Distributed.Infrastructure.Storage;

/// <summary>
/// Health check for MinIO object storage connectivity.
/// </summary>
public class MinioHealthCheck : IHealthCheck
{
    private readonly IObjectStorageService _storageService;
    private readonly ILogger<MinioHealthCheck> _logger;

    public MinioHealthCheck(
        IObjectStorageService storageService,
        ILogger<MinioHealthCheck> logger)
    {
        _storageService = storageService;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var isHealthy = await _storageService.HealthCheckAsync(cancellationToken);

            if (isHealthy)
            {
                return HealthCheckResult.Healthy("MinIO storage is accessible");
            }

            _logger.LogWarning("MinIO storage health check failed");
            return HealthCheckResult.Unhealthy("MinIO storage is not accessible");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MinIO health check threw an exception");
            return HealthCheckResult.Unhealthy(
                "MinIO storage health check failed with exception",
                ex);
        }
    }
}
