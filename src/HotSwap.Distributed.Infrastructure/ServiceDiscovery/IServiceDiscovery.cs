namespace HotSwap.Distributed.Infrastructure.ServiceDiscovery;

/// <summary>
/// Service discovery interface for dynamic node registration and lookup.
/// Supports multiple backends (Consul, etcd, etc.)
/// </summary>
public interface IServiceDiscovery
{
    /// <summary>
    /// Registers a node with the service discovery system.
    /// </summary>
    /// <param name="registration">Node registration information</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the registration operation</returns>
    Task RegisterNodeAsync(NodeRegistration registration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deregisters a node from the service discovery system.
    /// </summary>
    /// <param name="nodeId">The node ID to deregister</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the deregistration operation</returns>
    Task DeregisterNodeAsync(string nodeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Discovers all nodes for a specific environment.
    /// </summary>
    /// <param name="environment">The environment to query</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of discovered nodes</returns>
    Task<IReadOnlyList<ServiceNode>> DiscoverNodesAsync(
        string environment,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific node by ID.
    /// </summary>
    /// <param name="nodeId">The node ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The node if found, null otherwise</returns>
    Task<ServiceNode?> GetNodeAsync(string nodeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the health status of a node.
    /// </summary>
    /// <param name="nodeId">The node ID</param>
    /// <param name="isHealthy">Health status</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the update operation</returns>
    Task UpdateHealthStatusAsync(
        string nodeId,
        bool isHealthy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Registers a health check for a node.
    /// </summary>
    /// <param name="nodeId">The node ID</param>
    /// <param name="healthCheckUrl">The health check URL</param>
    /// <param name="intervalSeconds">Check interval in seconds</param>
    /// <param name="timeoutSeconds">Check timeout in seconds</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the registration operation</returns>
    Task RegisterHealthCheckAsync(
        string nodeId,
        string healthCheckUrl,
        int intervalSeconds = 10,
        int timeoutSeconds = 5,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all healthy nodes for an environment.
    /// </summary>
    /// <param name="environment">The environment to query</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of healthy nodes</returns>
    Task<IReadOnlyList<ServiceNode>> GetHealthyNodesAsync(
        string environment,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Node registration information.
/// </summary>
public class NodeRegistration
{
    /// <summary>
    /// Unique node identifier.
    /// </summary>
    public required string NodeId { get; init; }

    /// <summary>
    /// Hostname or IP address.
    /// </summary>
    public required string Hostname { get; init; }

    /// <summary>
    /// Service port.
    /// </summary>
    public required int Port { get; init; }

    /// <summary>
    /// Environment name (e.g., "Development", "Production").
    /// </summary>
    public required string Environment { get; init; }

    /// <summary>
    /// Additional metadata.
    /// </summary>
    public Dictionary<string, string> Metadata { get; init; } = new();

    /// <summary>
    /// Service tags.
    /// </summary>
    public List<string> Tags { get; init; } = new();
}

/// <summary>
/// Represents a discovered service node.
/// </summary>
public class ServiceNode
{
    /// <summary>
    /// Unique node identifier.
    /// </summary>
    public required string NodeId { get; init; }

    /// <summary>
    /// Hostname or IP address.
    /// </summary>
    public required string Hostname { get; init; }

    /// <summary>
    /// Service port.
    /// </summary>
    public required int Port { get; init; }

    /// <summary>
    /// Environment name.
    /// </summary>
    public required string Environment { get; init; }

    /// <summary>
    /// Health status.
    /// </summary>
    public bool IsHealthy { get; init; } = true;

    /// <summary>
    /// Last health check timestamp.
    /// </summary>
    public DateTime LastHealthCheck { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Additional metadata.
    /// </summary>
    public Dictionary<string, string> Metadata { get; init; } = new();

    /// <summary>
    /// Service tags.
    /// </summary>
    public List<string> Tags { get; init; } = new();
}
