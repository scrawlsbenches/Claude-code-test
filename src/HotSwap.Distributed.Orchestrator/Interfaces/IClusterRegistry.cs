using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Orchestrator.Core;

namespace HotSwap.Distributed.Orchestrator.Interfaces;

/// <summary>
/// Registry for managing environment clusters.
/// </summary>
public interface IClusterRegistry
{
    /// <summary>
    /// Gets a cluster for a specific environment.
    /// </summary>
    EnvironmentCluster GetCluster(EnvironmentType environment);

    /// <summary>
    /// Gets a cluster asynchronously.
    /// </summary>
    Task<EnvironmentCluster> GetClusterAsync(
        EnvironmentType environment,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all clusters.
    /// </summary>
    IReadOnlyDictionary<EnvironmentType, EnvironmentCluster> GetAllClusters();
}
