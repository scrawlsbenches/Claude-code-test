using HotSwap.Distributed.Domain.Models;
using Microsoft.Extensions.Caching.Memory;

namespace HotSwap.Distributed.Api.Services;

/// <summary>
/// In-memory implementation of deployment tracker using IMemoryCache
/// </summary>
public class InMemoryDeploymentTracker : IDeploymentTracker
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<InMemoryDeploymentTracker> _logger;

    // Cache key prefixes
    private const string ResultKeyPrefix = "deployment:result:";
    private const string InProgressKeyPrefix = "deployment:inprogress:";

    // Cache expiration settings
    private static readonly TimeSpan ResultExpiration = TimeSpan.FromHours(24);
    private static readonly TimeSpan InProgressExpiration = TimeSpan.FromHours(2);

    public InMemoryDeploymentTracker(
        IMemoryCache cache,
        ILogger<InMemoryDeploymentTracker> logger)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<PipelineExecutionResult?> GetResultAsync(Guid executionId)
    {
        var key = ResultKeyPrefix + executionId;
        _cache.TryGetValue(key, out PipelineExecutionResult? result);
        return Task.FromResult(result);
    }

    public Task StoreResultAsync(Guid executionId, PipelineExecutionResult result)
    {
        var key = ResultKeyPrefix + executionId;
        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = ResultExpiration,
            Priority = CacheItemPriority.Normal
        };

        _cache.Set(key, result, options);
        _logger.LogDebug("Stored deployment result for execution {ExecutionId}", executionId);

        return Task.CompletedTask;
    }

    public Task<DeploymentRequest?> GetInProgressAsync(Guid executionId)
    {
        var key = InProgressKeyPrefix + executionId;
        _cache.TryGetValue(key, out DeploymentRequest? request);
        return Task.FromResult(request);
    }

    public Task TrackInProgressAsync(Guid executionId, DeploymentRequest request)
    {
        var key = InProgressKeyPrefix + executionId;
        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = InProgressExpiration,
            Priority = CacheItemPriority.High // In-progress deployments should not be evicted
        };

        _cache.Set(key, request, options);
        _logger.LogDebug("Tracking deployment {ExecutionId} as in-progress", executionId);

        return Task.CompletedTask;
    }

    public Task RemoveInProgressAsync(Guid executionId)
    {
        var key = InProgressKeyPrefix + executionId;
        _cache.Remove(key);
        _logger.LogDebug("Removed in-progress tracking for deployment {ExecutionId}", executionId);

        return Task.CompletedTask;
    }
}
