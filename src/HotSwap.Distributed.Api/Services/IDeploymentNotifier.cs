using HotSwap.Distributed.Domain.Models;

namespace HotSwap.Distributed.Api.Services;

/// <summary>
/// Service for notifying clients about deployment updates via real-time channels.
/// </summary>
public interface IDeploymentNotifier
{
    /// <summary>
    /// Notifies clients that a deployment's status has changed.
    /// </summary>
    /// <param name="executionId">The unique identifier of the deployment execution.</param>
    /// <param name="state">The updated deployment execution state.</param>
    /// <exception cref="ArgumentNullException">Thrown when executionId or state is null.</exception>
    /// <exception cref="ArgumentException">Thrown when executionId is empty or whitespace.</exception>
    Task NotifyDeploymentStatusChanged(string executionId, PipelineExecutionState state);

    /// <summary>
    /// Notifies clients about deployment progress updates.
    /// </summary>
    /// <param name="executionId">The unique identifier of the deployment execution.</param>
    /// <param name="stage">The current deployment stage.</param>
    /// <param name="progress">The progress percentage (0-100).</param>
    /// <exception cref="ArgumentNullException">Thrown when executionId or stage is null.</exception>
    /// <exception cref="ArgumentException">Thrown when executionId is empty or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when progress is not between 0 and 100.</exception>
    Task NotifyDeploymentProgress(string executionId, string stage, int progress);
}
