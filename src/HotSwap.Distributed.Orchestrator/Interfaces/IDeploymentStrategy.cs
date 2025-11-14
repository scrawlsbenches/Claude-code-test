using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Orchestrator.Core;

namespace HotSwap.Distributed.Orchestrator.Interfaces;

/// <summary>
/// Strategy for deploying modules to a cluster.
/// </summary>
public interface IDeploymentStrategy
{
    /// <summary>
    /// Name of the strategy.
    /// </summary>
    string StrategyName { get; }

    /// <summary>
    /// Deploys a module to the cluster using this strategy.
    /// </summary>
    Task<DeploymentResult> DeployAsync(
        ModuleDeploymentRequest request,
        EnvironmentCluster cluster,
        CancellationToken cancellationToken = default);
}
