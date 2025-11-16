using HotSwap.Distributed.Domain.Models;

namespace HotSwap.Distributed.Api.Services;

/// <summary>
/// Service for tracking deployment execution state
/// </summary>
public interface IDeploymentTracker
{
    /// <summary>
    /// Gets a completed deployment result by execution ID
    /// </summary>
    Task<PipelineExecutionResult?> GetResultAsync(Guid executionId);

    /// <summary>
    /// Stores a completed deployment result
    /// </summary>
    Task StoreResultAsync(Guid executionId, PipelineExecutionResult result);

    /// <summary>
    /// Gets an in-progress deployment request by execution ID
    /// </summary>
    Task<DeploymentRequest?> GetInProgressAsync(Guid executionId);

    /// <summary>
    /// Tracks a deployment as in-progress
    /// </summary>
    Task TrackInProgressAsync(Guid executionId, DeploymentRequest request);

    /// <summary>
    /// Removes a deployment from in-progress tracking
    /// </summary>
    Task RemoveInProgressAsync(Guid executionId);
}
