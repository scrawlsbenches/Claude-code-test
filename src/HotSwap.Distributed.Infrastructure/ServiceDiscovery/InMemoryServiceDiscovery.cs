using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace HotSwap.Distributed.Infrastructure.ServiceDiscovery;

/// <summary>
/// In-memory service discovery implementation for testing and development.
/// Not suitable for production use across multiple instances.
/// </summary>
public class InMemoryServiceDiscovery : IServiceDiscovery
{
    private readonly ILogger<InMemoryServiceDiscovery> _logger;
    private readonly ConcurrentDictionary<string, ServiceNode> _nodes = new();
    private readonly ConcurrentDictionary<string, bool> _healthStatus = new();

    public InMemoryServiceDiscovery(ILogger<InMemoryServiceDiscovery> logger)
    {
        _logger = logger;
        _logger.LogInformation("In-memory service discovery initialized");
    }

    public Task RegisterNodeAsync(NodeRegistration registration, CancellationToken cancellationToken = default)
    {
        var serviceNode = new ServiceNode
        {
            NodeId = registration.NodeId,
            Hostname = registration.Hostname,
            Port = registration.Port,
            Environment = registration.Environment,
            IsHealthy = true,
            LastHealthCheck = DateTime.UtcNow,
            Metadata = new Dictionary<string, string>(registration.Metadata),
            Tags = new List<string>(registration.Tags)
        };

        _nodes[registration.NodeId] = serviceNode;
        _healthStatus[registration.NodeId] = true;

        _logger.LogInformation("Node registered: {NodeId} ({Hostname}:{Port}) in {Environment}",
            registration.NodeId, registration.Hostname, registration.Port, registration.Environment);

        return Task.CompletedTask;
    }

    public Task DeregisterNodeAsync(string nodeId, CancellationToken cancellationToken = default)
    {
        if (_nodes.TryRemove(nodeId, out var node))
        {
            _healthStatus.TryRemove(nodeId, out _);
            _logger.LogInformation("Node deregistered: {NodeId}", nodeId);
        }
        else
        {
            _logger.LogWarning("Attempted to deregister non-existent node: {NodeId}", nodeId);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ServiceNode>> DiscoverNodesAsync(
        string environment,
        CancellationToken cancellationToken = default)
    {
        var nodes = _nodes.Values
            .Where(n => n.Environment.Equals(environment, StringComparison.OrdinalIgnoreCase))
            .Select(n => new ServiceNode
            {
                NodeId = n.NodeId,
                Hostname = n.Hostname,
                Port = n.Port,
                Environment = n.Environment,
                IsHealthy = _healthStatus.GetValueOrDefault(n.NodeId, true),
                LastHealthCheck = DateTime.UtcNow,
                Metadata = n.Metadata,
                Tags = n.Tags
            })
            .ToList();

        _logger.LogDebug("Discovered {Count} nodes for environment {Environment}", nodes.Count, environment);

        return Task.FromResult<IReadOnlyList<ServiceNode>>(nodes);
    }

    public Task<ServiceNode?> GetNodeAsync(string nodeId, CancellationToken cancellationToken = default)
    {
        if (_nodes.TryGetValue(nodeId, out var node))
        {
            var updatedNode = new ServiceNode
            {
                NodeId = node.NodeId,
                Hostname = node.Hostname,
                Port = node.Port,
                Environment = node.Environment,
                IsHealthy = _healthStatus.GetValueOrDefault(nodeId, true),
                LastHealthCheck = DateTime.UtcNow,
                Metadata = node.Metadata,
                Tags = node.Tags
            };

            return Task.FromResult<ServiceNode?>(updatedNode);
        }

        _logger.LogDebug("Node {NodeId} not found", nodeId);
        return Task.FromResult<ServiceNode?>(null);
    }

    public Task UpdateHealthStatusAsync(
        string nodeId,
        bool isHealthy,
        CancellationToken cancellationToken = default)
    {
        if (_nodes.ContainsKey(nodeId))
        {
            _healthStatus[nodeId] = isHealthy;
            _logger.LogInformation("Health status updated for node {NodeId}: {Status}",
                nodeId, isHealthy ? "Healthy" : "Unhealthy");
        }
        else
        {
            _logger.LogWarning("Cannot update health status: node {NodeId} not registered", nodeId);
        }

        return Task.CompletedTask;
    }

    public Task RegisterHealthCheckAsync(
        string nodeId,
        string healthCheckUrl,
        int intervalSeconds = 10,
        int timeoutSeconds = 5,
        CancellationToken cancellationToken = default)
    {
        // In-memory implementation doesn't perform actual health checks
        // This is a no-op for compatibility
        _logger.LogDebug("Health check registration requested for node {NodeId}: {Url} (no-op in memory)",
            nodeId, healthCheckUrl);

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ServiceNode>> GetHealthyNodesAsync(
        string environment,
        CancellationToken cancellationToken = default)
    {
        var nodes = _nodes.Values
            .Where(n => n.Environment.Equals(environment, StringComparison.OrdinalIgnoreCase))
            .Where(n => _healthStatus.GetValueOrDefault(n.NodeId, true))
            .Select(n => new ServiceNode
            {
                NodeId = n.NodeId,
                Hostname = n.Hostname,
                Port = n.Port,
                Environment = n.Environment,
                IsHealthy = true,
                LastHealthCheck = DateTime.UtcNow,
                Metadata = n.Metadata,
                Tags = n.Tags
            })
            .ToList();

        _logger.LogDebug("Found {Count} healthy nodes for environment {Environment}",
            nodes.Count, environment);

        return Task.FromResult<IReadOnlyList<ServiceNode>>(nodes);
    }

    /// <summary>
    /// Gets all registered nodes (for testing purposes).
    /// </summary>
    public IReadOnlyCollection<ServiceNode> GetAllNodes()
    {
        return _nodes.Values.ToList();
    }

    /// <summary>
    /// Clears all registered nodes (for testing purposes).
    /// </summary>
    public void Clear()
    {
        _nodes.Clear();
        _healthStatus.Clear();
        _logger.LogInformation("All nodes cleared from in-memory service discovery");
    }
}
