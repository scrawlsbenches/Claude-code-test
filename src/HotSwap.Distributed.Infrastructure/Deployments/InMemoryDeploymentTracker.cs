using System.Collections.Concurrent;
using HotSwap.Distributed.Domain.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace HotSwap.Distributed.Infrastructure.Deployments;

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
    private const string PipelineStateKeyPrefix = "deployment:state:";

    // Cache expiration settings
    private static readonly TimeSpan ResultExpiration = TimeSpan.FromHours(24);
    private static readonly TimeSpan InProgressExpiration = TimeSpan.FromHours(2);

    // Track execution IDs for listing (ConcurrentDictionary used as concurrent set)
    private readonly ConcurrentDictionary<Guid, byte> _resultIds = new();
    private readonly ConcurrentDictionary<Guid, byte> _inProgressIds = new();

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
        try
        {
            var key = ResultKeyPrefix + executionId;
            var options = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = ResultExpiration,
                Priority = CacheItemPriority.Normal
            };

            _cache.Set(key, result, options);
            _resultIds.TryAdd(executionId, 0);
            _logger.LogDebug("Stored deployment result for execution {ExecutionId}", executionId);
        }
        catch (ObjectDisposedException)
        {
            // Cache already disposed (application shutting down) - ignore
            _logger.LogDebug("Cache disposed while storing result for deployment {ExecutionId}", executionId);
        }

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
        try
        {
            var key = InProgressKeyPrefix + executionId;
            var options = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = InProgressExpiration,
                Priority = CacheItemPriority.High // In-progress deployments should not be evicted
            };

            _cache.Set(key, request, options);
            _inProgressIds.TryAdd(executionId, 0);
            _logger.LogDebug("Tracking deployment {ExecutionId} as in-progress", executionId);
        }
        catch (ObjectDisposedException)
        {
            // Cache already disposed (application shutting down) - ignore
            _logger.LogDebug("Cache disposed while tracking deployment {ExecutionId}", executionId);
        }

        return Task.CompletedTask;
    }

    public Task RemoveInProgressAsync(Guid executionId)
    {
        try
        {
            var key = InProgressKeyPrefix + executionId;
            _cache.Remove(key);
            _inProgressIds.TryRemove(executionId, out _);
            _logger.LogDebug("Removed in-progress tracking for deployment {ExecutionId}", executionId);
        }
        catch (ObjectDisposedException)
        {
            // Cache already disposed (application shutting down) - ignore
            _logger.LogDebug("Cache disposed while removing in-progress deployment {ExecutionId}", executionId);
        }

        return Task.CompletedTask;
    }

    public Task<IEnumerable<PipelineExecutionResult>> GetAllResultsAsync()
    {
        var results = new List<PipelineExecutionResult>();

        foreach (var executionId in _resultIds.Keys)
        {
            var key = ResultKeyPrefix + executionId;
            if (_cache.TryGetValue(key, out PipelineExecutionResult? result) && result != null)
            {
                results.Add(result);
            }
            else
            {
                // Cache entry expired but ID still in set - clean up
                _resultIds.TryRemove(executionId, out _);
            }
        }

        _logger.LogDebug("Retrieved {Count} deployment results", results.Count);
        return Task.FromResult<IEnumerable<PipelineExecutionResult>>(results);
    }

    public Task<IEnumerable<DeploymentRequest>> GetAllInProgressAsync()
    {
        var requests = new List<DeploymentRequest>();

        foreach (var executionId in _inProgressIds.Keys)
        {
            var key = InProgressKeyPrefix + executionId;
            if (_cache.TryGetValue(key, out DeploymentRequest? request) && request != null)
            {
                requests.Add(request);
            }
            else
            {
                // Cache entry expired but ID still in set - clean up
                _inProgressIds.TryRemove(executionId, out _);
            }
        }

        _logger.LogDebug("Retrieved {Count} in-progress deployments", requests.Count);
        return Task.FromResult<IEnumerable<DeploymentRequest>>(requests);
    }

    public Task UpdatePipelineStateAsync(Guid executionId, PipelineExecutionState state)
    {
        try
        {
            var key = PipelineStateKeyPrefix + executionId;
            var options = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = InProgressExpiration,
                Priority = CacheItemPriority.High // Pipeline state should not be evicted
            };

            state.LastUpdated = DateTime.UtcNow;
            _cache.Set(key, state, options);
            _logger.LogDebug("Updated pipeline state for execution {ExecutionId}: Status={Status}, Stage={Stage}",
                executionId, state.Status, state.CurrentStage);
        }
        catch (ObjectDisposedException)
        {
            // Cache already disposed (application shutting down) - ignore
            _logger.LogDebug("Cache disposed while updating pipeline state for deployment {ExecutionId}", executionId);
        }

        return Task.CompletedTask;
    }

    public Task<PipelineExecutionState?> GetPipelineStateAsync(Guid executionId)
    {
        var key = PipelineStateKeyPrefix + executionId;
        _cache.TryGetValue(key, out PipelineExecutionState? state);
        return Task.FromResult(state);
    }
}
