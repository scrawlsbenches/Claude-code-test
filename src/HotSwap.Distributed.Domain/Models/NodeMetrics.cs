namespace HotSwap.Distributed.Domain.Models;

/// <summary>
/// Snapshot of node performance metrics at a point in time.
/// </summary>
public class NodeMetricsSnapshot
{
    /// <summary>
    /// Node identifier.
    /// </summary>
    public Guid NodeId { get; set; }

    /// <summary>
    /// Timestamp of the snapshot.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// CPU usage percentage (0-100).
    /// </summary>
    public double CpuUsagePercent { get; set; }

    /// <summary>
    /// Memory usage percentage (0-100).
    /// </summary>
    public double MemoryUsagePercent { get; set; }

    /// <summary>
    /// Average request latency in milliseconds.
    /// </summary>
    public double LatencyMs { get; set; }

    /// <summary>
    /// Error rate percentage (0-100).
    /// </summary>
    public double ErrorRate { get; set; }

    /// <summary>
    /// Requests per second throughput.
    /// </summary>
    public double RequestsPerSecond { get; set; }

    /// <summary>
    /// Number of active connections.
    /// </summary>
    public int ActiveConnections { get; set; }

    /// <summary>
    /// Number of loaded modules.
    /// </summary>
    public int LoadedModuleCount { get; set; }

    /// <summary>
    /// Custom application-specific metrics.
    /// </summary>
    public Dictionary<string, double> CustomMetrics { get; set; } = new();
}

/// <summary>
/// Aggregated metrics for a cluster.
/// </summary>
public class ClusterMetricsSnapshot
{
    /// <summary>
    /// Environment identifier.
    /// </summary>
    public required string Environment { get; set; }

    /// <summary>
    /// Timestamp of the snapshot.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Total number of nodes in cluster.
    /// </summary>
    public int TotalNodes { get; set; }

    /// <summary>
    /// Average CPU usage across all nodes.
    /// </summary>
    public double AvgCpuUsage { get; set; }

    /// <summary>
    /// Average memory usage across all nodes.
    /// </summary>
    public double AvgMemoryUsage { get; set; }

    /// <summary>
    /// Average latency across all nodes.
    /// </summary>
    public double AvgLatency { get; set; }

    /// <summary>
    /// Average error rate across all nodes.
    /// </summary>
    public double AvgErrorRate { get; set; }

    /// <summary>
    /// Total requests per second across all nodes.
    /// </summary>
    public double TotalRequestsPerSecond { get; set; }
}
