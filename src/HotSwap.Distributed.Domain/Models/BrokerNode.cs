using HotSwap.Distributed.Domain.Enums;

namespace HotSwap.Distributed.Domain.Models;

/// <summary>
/// Represents a message broker node in the cluster.
/// </summary>
public class BrokerNode
{
    /// <summary>
    /// Unique node identifier (e.g., "broker-1").
    /// </summary>
    public required string NodeId { get; set; }

    /// <summary>
    /// Broker hostname or IP address.
    /// </summary>
    public required string Hostname { get; set; }

    /// <summary>
    /// Broker port number.
    /// </summary>
    public int Port { get; set; } = 5050;

    /// <summary>
    /// Broker role (Master or Replica).
    /// </summary>
    public BrokerRole Role { get; set; } = BrokerRole.Replica;

    /// <summary>
    /// Topics assigned to this broker node.
    /// </summary>
    public List<string> AssignedTopics { get; set; } = new();

    /// <summary>
    /// Current health status.
    /// </summary>
    public BrokerHealth Health { get; set; } = new();

    /// <summary>
    /// Performance metrics.
    /// </summary>
    public BrokerMetrics Metrics { get; set; } = new();

    /// <summary>
    /// Last heartbeat timestamp (UTC).
    /// </summary>
    public DateTime LastHeartbeat { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Broker startup timestamp (UTC).
    /// </summary>
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Checks if the broker is healthy based on heartbeat and health checks.
    /// </summary>
    public bool IsHealthy()
    {
        // Unhealthy if no heartbeat in last 2 minutes
        if (DateTime.UtcNow - LastHeartbeat > TimeSpan.FromMinutes(2))
            return false;

        return Health.IsHealthy;
    }

    /// <summary>
    /// Updates the heartbeat timestamp.
    /// </summary>
    public void RecordHeartbeat()
    {
        LastHeartbeat = DateTime.UtcNow;
    }
}

/// <summary>
/// Broker health information.
/// </summary>
public class BrokerHealth
{
    /// <summary>
    /// Overall health status.
    /// </summary>
    public bool IsHealthy { get; set; } = true;

    /// <summary>
    /// Total messages queued across all topics.
    /// </summary>
    public int QueueDepth { get; set; } = 0;

    /// <summary>
    /// Number of active consumers connected to this broker.
    /// </summary>
    public int ActiveConsumers { get; set; } = 0;

    /// <summary>
    /// CPU usage percentage (0-100).
    /// </summary>
    public double CpuUsage { get; set; } = 0;

    /// <summary>
    /// Memory usage percentage (0-100).
    /// </summary>
    public double MemoryUsage { get; set; } = 0;
}

/// <summary>
/// Broker performance metrics.
/// </summary>
public class BrokerMetrics
{
    /// <summary>
    /// Total messages published to this broker.
    /// </summary>
    public long MessagesPublished { get; set; } = 0;

    /// <summary>
    /// Total messages delivered by this broker.
    /// </summary>
    public long MessagesDelivered { get; set; } = 0;

    /// <summary>
    /// Total failed message deliveries.
    /// </summary>
    public long MessagesFailed { get; set; } = 0;

    /// <summary>
    /// Average publish latency in milliseconds.
    /// </summary>
    public double AvgPublishLatencyMs { get; set; } = 0;

    /// <summary>
    /// Average delivery latency in milliseconds.
    /// </summary>
    public double AvgDeliveryLatencyMs { get; set; } = 0;

    /// <summary>
    /// Current throughput in messages per second.
    /// </summary>
    public double ThroughputMsgPerSec { get; set; } = 0;
}
