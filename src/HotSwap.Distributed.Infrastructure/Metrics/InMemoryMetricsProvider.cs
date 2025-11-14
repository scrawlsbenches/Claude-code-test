using System.Collections.Concurrent;
using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Interfaces;
using Microsoft.Extensions.Logging;

namespace HotSwap.Distributed.Infrastructure.Metrics;

/// <summary>
/// In-memory implementation of metrics provider with caching.
/// In production, this would query Prometheus or similar.
/// </summary>
public class InMemoryMetricsProvider : IMetricsProvider
{
    private readonly ILogger<InMemoryMetricsProvider> _logger;
    private readonly ConcurrentDictionary<Guid, NodeMetricsSnapshot> _metricsCache;
    private readonly TimeSpan _cacheTtl = TimeSpan.FromSeconds(10);
    private readonly Random _random = new();

    public InMemoryMetricsProvider(ILogger<InMemoryMetricsProvider> logger)
    {
        _logger = logger;
        _metricsCache = new ConcurrentDictionary<Guid, NodeMetricsSnapshot>();
    }

    public async Task<NodeMetricsSnapshot> GetNodeMetricsAsync(
        Guid nodeId,
        CancellationToken cancellationToken = default)
    {
        // Check cache
        if (_metricsCache.TryGetValue(nodeId, out var cached))
        {
            var age = DateTime.UtcNow - cached.Timestamp;
            if (age < _cacheTtl)
            {
                return cached;
            }
        }

        // Simulate fetching metrics (in production, query from Prometheus)
        await Task.Delay(50, cancellationToken);

        var metrics = GenerateRandomMetrics(nodeId);
        _metricsCache[nodeId] = metrics;

        _logger.LogDebug("Fetched metrics for node {NodeId}: CPU={Cpu}%, Memory={Memory}%",
            nodeId, metrics.CpuUsagePercent, metrics.MemoryUsagePercent);

        return metrics;
    }

    public async Task<IEnumerable<NodeMetricsSnapshot>> GetNodesMetricsAsync(
        IEnumerable<Guid> nodeIds,
        CancellationToken cancellationToken = default)
    {
        // Query in parallel
        var tasks = nodeIds.Select(id => GetNodeMetricsAsync(id, cancellationToken));
        var results = await Task.WhenAll(tasks);

        return results;
    }

    public async Task<ClusterMetricsSnapshot> GetClusterMetricsAsync(
        EnvironmentType environment,
        CancellationToken cancellationToken = default)
    {
        // In production, this would aggregate from all nodes in the environment
        // For now, generate sample data
        await Task.Delay(100, cancellationToken);

        return new ClusterMetricsSnapshot
        {
            Environment = environment.ToString(),
            Timestamp = DateTime.UtcNow,
            TotalNodes = 10,
            AvgCpuUsage = 45.0 + _random.NextDouble() * 20,
            AvgMemoryUsage = 60.0 + _random.NextDouble() * 15,
            AvgLatency = 120.0 + _random.NextDouble() * 80,
            AvgErrorRate = 0.5 + _random.NextDouble() * 2,
            TotalRequestsPerSecond = 1000.0 + _random.NextDouble() * 5000
        };
    }

    public async Task<IEnumerable<NodeMetricsSnapshot>> GetHistoricalMetricsAsync(
        Guid nodeId,
        DateTime startTime,
        DateTime endTime,
        CancellationToken cancellationToken = default)
    {
        // In production, query time-series database
        await Task.Delay(200, cancellationToken);

        var metrics = new List<NodeMetricsSnapshot>();
        var current = startTime;

        while (current <= endTime)
        {
            metrics.Add(GenerateRandomMetrics(nodeId, current));
            current = current.AddMinutes(5);
        }

        return metrics;
    }

    private NodeMetricsSnapshot GenerateRandomMetrics(Guid nodeId, DateTime? timestamp = null)
    {
        return new NodeMetricsSnapshot
        {
            NodeId = nodeId,
            Timestamp = timestamp ?? DateTime.UtcNow,
            CpuUsagePercent = 30.0 + _random.NextDouble() * 40,
            MemoryUsagePercent = 50.0 + _random.NextDouble() * 30,
            LatencyMs = 100.0 + _random.NextDouble() * 200,
            ErrorRate = _random.NextDouble() * 3,
            RequestsPerSecond = 100.0 + _random.NextDouble() * 400,
            ActiveConnections = _random.Next(10, 200),
            LoadedModuleCount = _random.Next(5, 15)
        };
    }
}
