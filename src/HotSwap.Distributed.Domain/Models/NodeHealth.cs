using HotSwap.Distributed.Domain.Enums;

namespace HotSwap.Distributed.Domain.Models;

/// <summary>
/// Health status of a node.
/// </summary>
public class NodeHealth
{
    /// <summary>
    /// Node identifier.
    /// </summary>
    public Guid NodeId { get; set; }

    /// <summary>
    /// Current node status.
    /// </summary>
    public NodeStatus Status { get; set; }

    /// <summary>
    /// Whether the node is healthy.
    /// </summary>
    public bool IsHealthy { get; set; }

    /// <summary>
    /// Last heartbeat timestamp.
    /// </summary>
    public DateTime LastHeartbeat { get; set; }

    /// <summary>
    /// Current metrics snapshot.
    /// </summary>
    public NodeMetricsSnapshot? Metrics { get; set; }

    /// <summary>
    /// Health check messages or warnings.
    /// </summary>
    public List<string> Messages { get; set; } = new();

    /// <summary>
    /// Checks if node is considered healthy based on thresholds.
    /// </summary>
    public bool EvaluateHealth(
        double maxCpuPercent = 90,
        double maxMemoryPercent = 90,
        double maxErrorRate = 5,
        TimeSpan heartbeatTimeout = default)
    {
        if (heartbeatTimeout == default)
            heartbeatTimeout = TimeSpan.FromMinutes(2);

        var now = DateTime.UtcNow;
        var timeSinceHeartbeat = now - LastHeartbeat;

        if (timeSinceHeartbeat > heartbeatTimeout)
        {
            Messages.Add($"No heartbeat for {timeSinceHeartbeat.TotalSeconds:F0} seconds");
            IsHealthy = false;
            return false;
        }

        if (Metrics != null)
        {
            if (Metrics.CpuUsagePercent > maxCpuPercent)
            {
                Messages.Add($"CPU usage too high: {Metrics.CpuUsagePercent:F1}%");
                IsHealthy = false;
            }

            if (Metrics.MemoryUsagePercent > maxMemoryPercent)
            {
                Messages.Add($"Memory usage too high: {Metrics.MemoryUsagePercent:F1}%");
                IsHealthy = false;
            }

            if (Metrics.ErrorRate > maxErrorRate)
            {
                Messages.Add($"Error rate too high: {Metrics.ErrorRate:F1}%");
                IsHealthy = false;
            }
        }

        IsHealthy = Messages.Count == 0;
        return IsHealthy;
    }
}

/// <summary>
/// Health status of an entire cluster.
/// </summary>
public class ClusterHealth
{
    /// <summary>
    /// Environment identifier.
    /// </summary>
    public required string Environment { get; set; }

    /// <summary>
    /// Timestamp of health check.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Total number of nodes.
    /// </summary>
    public int TotalNodes { get; set; }

    /// <summary>
    /// Number of healthy nodes.
    /// </summary>
    public int HealthyNodes { get; set; }

    /// <summary>
    /// Number of unhealthy nodes.
    /// </summary>
    public int UnhealthyNodes { get; set; }

    /// <summary>
    /// Whether the cluster is considered healthy overall.
    /// </summary>
    public bool IsHealthy => UnhealthyNodes == 0 || (double)HealthyNodes / TotalNodes >= 0.8;

    /// <summary>
    /// Health status of individual nodes.
    /// </summary>
    public List<NodeHealth> NodeHealthStatuses { get; set; } = new();
}
