using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Interfaces;

namespace HotSwap.Distributed.IntegrationTests.Helpers;

/// <summary>
/// Deterministic metrics provider for integration tests.
/// Returns consistent, predictable metrics to ensure tests behave the same
/// in local development and CI/CD environments.
/// </summary>
public class DeterministicMetricsProvider : IMetricsProvider
{
    private readonly Dictionary<Guid, NodeMetricsSnapshot> _nodeMetrics = new();
    private readonly Dictionary<EnvironmentType, ClusterMetricsSnapshot> _clusterMetrics = new();

    public Task<NodeMetricsSnapshot> GetNodeMetricsAsync(
        Guid nodeId,
        CancellationToken cancellationToken = default)
    {
        if (!_nodeMetrics.ContainsKey(nodeId))
        {
            // Return healthy baseline metrics
            _nodeMetrics[nodeId] = new NodeMetricsSnapshot
            {
                NodeId = nodeId,
                Timestamp = DateTime.UtcNow,
                CpuUsagePercent = 45.0,      // Healthy CPU
                MemoryUsagePercent = 60.0,   // Healthy memory
                LatencyMs = 120.0,           // Acceptable latency
                ErrorRate = 0.5,             // Low error rate (0.5%)
                RequestsPerSecond = 1000.0,
                ActiveConnections = 100,
                LoadedModuleCount = 10
            };
        }

        return Task.FromResult(_nodeMetrics[nodeId]);
    }

    public async Task<IEnumerable<NodeMetricsSnapshot>> GetNodesMetricsAsync(
        IEnumerable<Guid> nodeIds,
        CancellationToken cancellationToken = default)
    {
        var tasks = nodeIds.Select(id => GetNodeMetricsAsync(id, cancellationToken));
        return await Task.WhenAll(tasks);
    }

    public Task<ClusterMetricsSnapshot> GetClusterMetricsAsync(
        EnvironmentType environment,
        CancellationToken cancellationToken = default)
    {
        if (!_clusterMetrics.ContainsKey(environment))
        {
            // Return healthy baseline cluster metrics
            _clusterMetrics[environment] = new ClusterMetricsSnapshot
            {
                Environment = environment.ToString(),
                Timestamp = DateTime.UtcNow,
                TotalNodes = 10,
                AvgCpuUsage = 45.0,          // Healthy CPU
                AvgMemoryUsage = 60.0,       // Healthy memory
                AvgLatency = 120.0,          // Acceptable latency
                AvgErrorRate = 0.5,          // Low error rate (0.5%)
                TotalRequestsPerSecond = 1000.0
            };
        }

        return Task.FromResult(_clusterMetrics[environment]);
    }

    public Task<IEnumerable<NodeMetricsSnapshot>> GetHistoricalMetricsAsync(
        Guid nodeId,
        DateTime startTime,
        DateTime endTime,
        CancellationToken cancellationToken = default)
    {
        var metrics = new List<NodeMetricsSnapshot>();
        var current = startTime;

        while (current <= endTime)
        {
            metrics.Add(new NodeMetricsSnapshot
            {
                NodeId = nodeId,
                Timestamp = current,
                CpuUsagePercent = 45.0,
                MemoryUsagePercent = 60.0,
                LatencyMs = 120.0,
                ErrorRate = 0.5,
                RequestsPerSecond = 1000.0,
                ActiveConnections = 100,
                LoadedModuleCount = 10
            });
            current = current.AddMinutes(5);
        }

        return Task.FromResult<IEnumerable<NodeMetricsSnapshot>>(metrics);
    }

    /// <summary>
    /// Sets custom metrics for specific nodes (for testing failure scenarios).
    /// </summary>
    public void SetNodeMetrics(Guid nodeId, NodeMetricsSnapshot metrics)
    {
        _nodeMetrics[nodeId] = metrics;
    }

    /// <summary>
    /// Sets custom cluster metrics (for testing baseline comparisons).
    /// </summary>
    public void SetClusterMetrics(EnvironmentType environment, ClusterMetricsSnapshot metrics)
    {
        _clusterMetrics[environment] = metrics;
    }

    /// <summary>
    /// Resets all metrics to default healthy state.
    /// </summary>
    public void Reset()
    {
        _nodeMetrics.Clear();
        _clusterMetrics.Clear();
    }
}
