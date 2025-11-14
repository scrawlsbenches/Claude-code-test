using HotSwap.Distributed.Domain.Enums;

namespace HotSwap.Distributed.Domain.Models;

/// <summary>
/// Configuration for a kernel node.
/// </summary>
public class NodeConfiguration
{
    /// <summary>
    /// Unique identifier for the node.
    /// </summary>
    public Guid NodeId { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Hostname or IP address of the node.
    /// </summary>
    public required string Hostname { get; set; }

    /// <summary>
    /// Port for API communication.
    /// </summary>
    public int Port { get; set; } = 8080;

    /// <summary>
    /// Environment where this node runs.
    /// </summary>
    public EnvironmentType Environment { get; set; }

    /// <summary>
    /// Heartbeat interval in seconds.
    /// </summary>
    public int HeartbeatIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// Heartbeat timeout in seconds (after which node is considered unhealthy).
    /// </summary>
    public int HeartbeatTimeoutSeconds { get; set; } = 120;

    /// <summary>
    /// Optional tags for node categorization.
    /// </summary>
    public Dictionary<string, string> Tags { get; set; } = new();

    /// <summary>
    /// Gets the full endpoint URL for the node.
    /// </summary>
    public string GetEndpoint() => $"http://{Hostname}:{Port}";
}
