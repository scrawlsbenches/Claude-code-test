using System.Collections.Concurrent;
using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;
using Microsoft.Extensions.Logging;

namespace HotSwap.Distributed.Orchestrator.Core;

/// <summary>
/// Represents a cluster of kernel nodes in a specific environment.
/// </summary>
public class EnvironmentCluster : IAsyncDisposable
{
    private readonly EnvironmentType _environment;
    private readonly ILogger<EnvironmentCluster> _logger;
    private readonly ConcurrentDictionary<Guid, KernelNode> _nodes;
    private bool _disposed;

    public EnvironmentType Environment => _environment;

    public IReadOnlyCollection<KernelNode> Nodes => _nodes.Values.ToList();

    public int NodeCount => _nodes.Count;

    public EnvironmentCluster(
        EnvironmentType environment,
        ILogger<EnvironmentCluster> logger)
    {
        _environment = environment;
        _logger = logger;
        _nodes = new ConcurrentDictionary<Guid, KernelNode>();

        _logger.LogInformation("Created cluster for environment: {Environment}", environment);
    }

    /// <summary>
    /// Adds a node to the cluster.
    /// </summary>
    public bool AddNode(KernelNode node)
    {
        if (node.Environment != _environment)
        {
            _logger.LogWarning("Cannot add node {NodeId} to {Environment} cluster - node is for {NodeEnvironment}",
                node.NodeId, _environment, node.Environment);
            return false;
        }

        var added = _nodes.TryAdd(node.NodeId, node);

        if (added)
        {
            _logger.LogInformation("Added node {NodeId} to {Environment} cluster (total: {Count})",
                node.NodeId, _environment, _nodes.Count);
        }

        return added;
    }

    /// <summary>
    /// Removes a node from the cluster.
    /// </summary>
    public bool RemoveNode(Guid nodeId)
    {
        var removed = _nodes.TryRemove(nodeId, out var node);

        if (removed)
        {
            _logger.LogInformation("Removed node {NodeId} from {Environment} cluster (remaining: {Count})",
                nodeId, _environment, _nodes.Count);
        }

        return removed;
    }

    /// <summary>
    /// Gets a node by ID.
    /// </summary>
    public KernelNode? GetNode(Guid nodeId)
    {
        _nodes.TryGetValue(nodeId, out var node);
        return node;
    }

    /// <summary>
    /// Gets all healthy nodes.
    /// </summary>
    public async Task<List<KernelNode>> GetHealthyNodesAsync(CancellationToken cancellationToken = default)
    {
        var healthyNodes = new List<KernelNode>();

        foreach (var node in _nodes.Values)
        {
            var health = await node.GetHealthAsync(cancellationToken);

            if (health.IsHealthy)
            {
                healthyNodes.Add(node);
            }
        }

        return healthyNodes;
    }

    /// <summary>
    /// Gets cluster health status.
    /// </summary>
    public async Task<ClusterHealth> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        var healthTasks = _nodes.Values.Select(n => n.GetHealthAsync(cancellationToken));
        var nodeHealths = await Task.WhenAll(healthTasks);

        var clusterHealth = new ClusterHealth
        {
            Environment = _environment.ToString(),
            Timestamp = DateTime.UtcNow,
            TotalNodes = _nodes.Count,
            HealthyNodes = nodeHealths.Count(h => h.IsHealthy),
            UnhealthyNodes = nodeHealths.Count(h => !h.IsHealthy),
            NodeHealthStatuses = nodeHealths.ToList()
        };

        _logger.LogDebug("Cluster {Environment} health: {Healthy}/{Total} healthy nodes",
            _environment, clusterHealth.HealthyNodes, clusterHealth.TotalNodes);

        return clusterHealth;
    }

    /// <summary>
    /// Selects a subset of nodes based on percentage (for canary deployments).
    /// </summary>
    public List<KernelNode> SelectNodesByPercentage(int percentage)
    {
        if (percentage <= 0 || percentage > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(percentage), "Percentage must be between 1 and 100");
        }

        var count = Math.Max(1, (int)Math.Ceiling(_nodes.Count * percentage / 100.0));
        return _nodes.Values.Take(count).ToList();
    }

    /// <summary>
    /// Selects a specific number of nodes.
    /// </summary>
    public List<KernelNode> SelectNodes(int count)
    {
        if (count <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Count must be positive");
        }

        return _nodes.Values.Take(count).ToList();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _logger.LogInformation("Disposing {Environment} cluster with {Count} nodes",
            _environment, _nodes.Count);

        // Dispose all nodes
        var disposeTasks = _nodes.Values.Select(n => n.DisposeAsync().AsTask());
        await Task.WhenAll(disposeTasks);

        _nodes.Clear();
        _disposed = true;

        _logger.LogInformation("{Environment} cluster disposed", _environment);
    }
}
