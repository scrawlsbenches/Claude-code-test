using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;

namespace HotSwap.Distributed.Infrastructure.Interfaces;

/// <summary>
/// Provides real-time metrics collection from nodes and clusters.
/// </summary>
public interface IMetricsProvider
{
    /// <summary>
    /// Gets current metrics for a single node.
    /// </summary>
    Task<NodeMetricsSnapshot> GetNodeMetricsAsync(
        Guid nodeId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets current metrics for multiple nodes in parallel.
    /// </summary>
    Task<IEnumerable<NodeMetricsSnapshot>> GetNodesMetricsAsync(
        IEnumerable<Guid> nodeIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets aggregated metrics for an entire cluster.
    /// </summary>
    Task<ClusterMetricsSnapshot> GetClusterMetricsAsync(
        EnvironmentType environment,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets historical metrics for a node over a time range.
    /// </summary>
    Task<IEnumerable<NodeMetricsSnapshot>> GetHistoricalMetricsAsync(
        Guid nodeId,
        DateTime startTime,
        DateTime endTime,
        CancellationToken cancellationToken = default);
}
