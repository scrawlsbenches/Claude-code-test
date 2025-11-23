using Consul;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;

namespace HotSwap.Distributed.Infrastructure.ServiceDiscovery;

/// <summary>
/// Consul-based service discovery implementation.
/// Provides automatic node registration, health checking, and service lookup.
/// </summary>
public class ConsulServiceDiscovery : IServiceDiscovery, IAsyncDisposable
{
    private readonly IConsulClient _consulClient;
    private readonly ServiceDiscoveryConfiguration _config;
    private readonly ILogger<ConsulServiceDiscovery> _logger;
    private readonly IMemoryCache _cache;
    private readonly Dictionary<string, string> _registeredNodes = new();
    private readonly SemaphoreSlim _registrationLock = new(1, 1);
    private bool _disposed;

    public ConsulServiceDiscovery(
        ILogger<ConsulServiceDiscovery> logger,
        ServiceDiscoveryConfiguration config,
        IMemoryCache? cache = null)
    {
        _logger = logger;
        _config = config;
        _cache = cache ?? new MemoryCache(new MemoryCacheOptions());

        // Configure Consul client
        _consulClient = new ConsulClient(consulConfig =>
        {
            consulConfig.Address = new Uri(_config.Consul.Address);
            consulConfig.Datacenter = _config.Consul.Datacenter;

            if (!string.IsNullOrEmpty(_config.Consul.Token))
            {
                consulConfig.Token = _config.Consul.Token;
            }
        });

        _logger.LogInformation("Consul service discovery initialized. Address: {Address}, Datacenter: {Datacenter}",
            _config.Consul.Address, _config.Consul.Datacenter);
    }

    public async Task RegisterNodeAsync(NodeRegistration node, CancellationToken cancellationToken = default)
    {
        await _registrationLock.WaitAsync(cancellationToken);
        try
        {
            var serviceId = GenerateServiceId(node);
            var serviceName = $"{_config.ServiceName}-{node.Environment.ToLower()}";

            var registration = new AgentServiceRegistration
            {
                ID = serviceId,
                Name = serviceName,
                Address = node.Hostname,
                Port = node.Port,
                Tags = node.Tags.Concat(new[] { node.Environment, "hotswap", "kernel-node" }).ToArray(),
                Meta = new Dictionary<string, string>(node.Metadata)
                {
                    ["node-id"] = node.NodeId,
                    ["environment"] = node.Environment,
                    ["hostname"] = node.Hostname,
                    ["version"] = "1.0.0"
                }
            };

            // Add health check if enabled
            if (_config.EnableHealthChecks)
            {
                var healthCheckUrl = $"http://{node.Hostname}:{node.Port}{_config.HealthCheckPath}";

                registration.Check = new AgentServiceCheck
                {
                    HTTP = healthCheckUrl,
                    Interval = TimeSpan.FromSeconds(_config.HealthCheckIntervalSeconds),
                    Timeout = TimeSpan.FromSeconds(_config.HealthCheckTimeoutSeconds),
                    DeregisterCriticalServiceAfter = TimeSpan.Parse(_config.Consul.DeregisterCriticalServiceAfter)
                };

                _logger.LogDebug("Health check configured: {HealthCheckUrl}, Interval: {Interval}s",
                    healthCheckUrl, _config.HealthCheckIntervalSeconds);
            }

            // Register with Consul
            var result = await _consulClient.Agent.ServiceRegister(registration, cancellationToken);

            if (result.StatusCode == System.Net.HttpStatusCode.OK)
            {
                _registeredNodes[serviceId] = node.NodeId;
                _logger.LogInformation("Node registered with Consul: {ServiceId} ({Hostname}:{Port})",
                    serviceId, node.Hostname, node.Port);
            }
            else
            {
                _logger.LogError("Failed to register node with Consul. Status: {StatusCode}",
                    result.StatusCode);
                throw new InvalidOperationException($"Consul registration failed with status {result.StatusCode}");
            }
        }
        finally
        {
            _registrationLock.Release();
        }
    }

    public async Task DeregisterNodeAsync(string nodeId, CancellationToken cancellationToken = default)
    {
        await _registrationLock.WaitAsync(cancellationToken);
        try
        {
            var serviceId = _registeredNodes.FirstOrDefault(kvp => kvp.Value == nodeId).Key;

            if (string.IsNullOrEmpty(serviceId))
            {
                _logger.LogWarning("Node {NodeId} not found in registered nodes", nodeId);
                return;
            }

            var result = await _consulClient.Agent.ServiceDeregister(serviceId, cancellationToken);

            if (result.StatusCode == System.Net.HttpStatusCode.OK)
            {
                _registeredNodes.Remove(serviceId);
                _logger.LogInformation("Node deregistered from Consul: {ServiceId}", serviceId);
            }
            else
            {
                _logger.LogWarning("Failed to deregister node from Consul: {ServiceId}. Status: {StatusCode}",
                    serviceId, result.StatusCode);
            }
        }
        finally
        {
            _registrationLock.Release();
        }
    }

    public async Task<IReadOnlyList<ServiceNode>> DiscoverNodesAsync(
        string environment,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"nodes_{environment}";

        // Check cache first
        if (_cache.TryGetValue<List<ServiceNode>>(cacheKey, out var cachedNodes))
        {
            _logger.LogDebug("Returning {Count} cached nodes for environment {Environment}",
                cachedNodes!.Count, environment);
            return cachedNodes;
        }

        var serviceName = $"{_config.ServiceName}-{environment.ToLower()}";

        var services = await _consulClient.Health.Service(
            serviceName,
            tag: null,
            passingOnly: false,
            cancellationToken);

        var nodes = services.Response.Select(service => new ServiceNode
        {
            NodeId = GetMetaValue(service.Service.Meta, "node-id", service.Service.ID),
            Hostname = service.Service.Address,
            Port = service.Service.Port,
            Environment = GetMetaValue(service.Service.Meta, "environment", environment),
            IsHealthy = service.Checks.All(check => check.Status == HealthStatus.Passing),
            LastHealthCheck = DateTime.UtcNow,
            Metadata = ToDictionary(service.Service.Meta),
            Tags = service.Service.Tags.ToList()
        }).ToList();

        // Cache the results
        _cache.Set(cacheKey, nodes, TimeSpan.FromSeconds(_config.CacheExpirationSeconds));

        _logger.LogInformation("Discovered {Count} nodes for environment {Environment} from Consul",
            nodes.Count, environment);

        return nodes;
    }

    public async Task<ServiceNode?> GetNodeAsync(string nodeId, CancellationToken cancellationToken = default)
    {
        // Try to find in registered nodes
        var serviceId = _registeredNodes.FirstOrDefault(kvp => kvp.Value == nodeId).Key;

        if (string.IsNullOrEmpty(serviceId))
        {
            _logger.LogDebug("Node {NodeId} not found in registered nodes, searching Consul", nodeId);

            // Search across all services
            var allServices = await _consulClient.Agent.Services(cancellationToken);

            var service = allServices.Response.Values.FirstOrDefault(s =>
                GetMetaValue(s.Meta, "node-id") == nodeId);

            if (service == null)
            {
                _logger.LogWarning("Node {NodeId} not found in Consul", nodeId);
                return null;
            }

            // Get health status
            var healthCheck = await _consulClient.Health.Service(
                service.Service,
                tag: null,
                passingOnly: false,
                cancellationToken);

            var serviceEntry = healthCheck.Response.FirstOrDefault(s => s.Service.ID == service.ID);

            if (serviceEntry == null)
            {
                return null;
            }

            return new ServiceNode
            {
                NodeId = nodeId,
                Hostname = service.Address,
                Port = service.Port,
                Environment = GetMetaValue(service.Meta, "environment", "unknown"),
                IsHealthy = serviceEntry.Checks.All(check => check.Status == HealthStatus.Passing),
                LastHealthCheck = DateTime.UtcNow,
                Metadata = ToDictionary(service.Meta),
                Tags = service.Tags.ToList()
            };
        }

        // Get service details
        var services = await _consulClient.Agent.Services(cancellationToken);

        if (!services.Response.TryGetValue(serviceId, out var agentService))
        {
            _logger.LogWarning("Service {ServiceId} not found in Consul", serviceId);
            return null;
        }

        // Get health status for the service
        var health = await _consulClient.Health.Service(
            agentService.Service,
            tag: null,
            passingOnly: false,
            cancellationToken);

        var entry = health.Response.FirstOrDefault(s => s.Service.ID == serviceId);

        if (entry == null)
        {
            return null;
        }

        return new ServiceNode
        {
            NodeId = nodeId,
            Hostname = agentService.Address,
            Port = agentService.Port,
            Environment = GetMetaValue(agentService.Meta, "environment", "unknown"),
            IsHealthy = entry.Checks.All(check => check.Status == HealthStatus.Passing),
            LastHealthCheck = DateTime.UtcNow,
            Metadata = ToDictionary(agentService.Meta),
            Tags = agentService.Tags.ToList()
        };
    }

    public async Task UpdateHealthStatusAsync(
        string nodeId,
        bool isHealthy,
        CancellationToken cancellationToken = default)
    {
        var serviceId = _registeredNodes.FirstOrDefault(kvp => kvp.Value == nodeId).Key;

        if (string.IsNullOrEmpty(serviceId))
        {
            _logger.LogWarning("Cannot update health status: node {NodeId} not registered", nodeId);
            return;
        }

        // Note: When using HTTP health checks, Consul automatically updates the health status.
        // Manual updates via UpdateTTL are only for TTL-based health checks.
        // For HTTP checks, we just invalidate the cache to force a fresh lookup.

        _logger.LogInformation("Health status update requested for node {NodeId}: {Status}",
            nodeId, isHealthy ? "Healthy" : "Unhealthy");

        // Invalidate cache to force fresh lookup
        InvalidateCacheForNode(nodeId);

        await Task.CompletedTask;
    }

    public async Task RegisterHealthCheckAsync(
        string nodeId,
        string healthCheckUrl,
        int intervalSeconds = 10,
        int timeoutSeconds = 5,
        CancellationToken cancellationToken = default)
    {
        var serviceId = _registeredNodes.FirstOrDefault(kvp => kvp.Value == nodeId).Key;

        if (string.IsNullOrEmpty(serviceId))
        {
            _logger.LogWarning("Cannot register health check: node {NodeId} not registered", nodeId);
            return;
        }

        var checkRegistration = new AgentCheckRegistration
        {
            CheckID = $"check-{serviceId}",
            Name = $"Health check for {serviceId}",
            ServiceID = serviceId,
            HTTP = healthCheckUrl,
            Interval = TimeSpan.FromSeconds(intervalSeconds),
            Timeout = TimeSpan.FromSeconds(timeoutSeconds),
            DeregisterCriticalServiceAfter = TimeSpan.Parse(_config.Consul.DeregisterCriticalServiceAfter)
        };

        await _consulClient.Agent.CheckRegister(checkRegistration, cancellationToken);

        _logger.LogInformation("Registered health check for node {NodeId}: {Url}", nodeId, healthCheckUrl);
    }

    public async Task<IReadOnlyList<ServiceNode>> GetHealthyNodesAsync(
        string environment,
        CancellationToken cancellationToken = default)
    {
        var serviceName = $"{_config.ServiceName}-{environment.ToLower()}";

        // Query only passing services
        var services = await _consulClient.Health.Service(
            serviceName,
            tag: null,
            passingOnly: true,  // Only return healthy services
            cancellationToken);

        var nodes = services.Response.Select(service => new ServiceNode
        {
            NodeId = GetMetaValue(service.Service.Meta, "node-id", service.Service.ID),
            Hostname = service.Service.Address,
            Port = service.Service.Port,
            Environment = GetMetaValue(service.Service.Meta, "environment", environment),
            IsHealthy = true,  // All returned services are healthy due to passingOnly=true
            LastHealthCheck = DateTime.UtcNow,
            Metadata = ToDictionary(service.Service.Meta),
            Tags = service.Service.Tags.ToList()
        }).ToList();

        _logger.LogInformation("Found {Count} healthy nodes for environment {Environment}",
            nodes.Count, environment);

        return nodes;
    }

    private string GenerateServiceId(NodeRegistration node)
    {
        return $"{_config.ServiceName}-{node.Environment.ToLower()}-{node.NodeId}";
    }

    private static string GetMetaValue(IDictionary<string, string>? meta, string key, string defaultValue = "")
    {
        if (meta == null)
            return defaultValue;

        return meta.TryGetValue(key, out var value) ? value : defaultValue;
    }

    private static Dictionary<string, string> ToDictionary(IDictionary<string, string>? source)
    {
        if (source == null)
            return new Dictionary<string, string>();

        return new Dictionary<string, string>(source);
    }

    private void InvalidateCacheForNode(string nodeId)
    {
        // Invalidate all environment caches (since we don't know which environment the node belongs to)
        foreach (var env in new[] { "Development", "QA", "Staging", "Production" })
        {
            _cache.Remove($"nodes_{env}");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _logger.LogInformation("Disposing Consul service discovery, deregistering {Count} nodes",
            _registeredNodes.Count);

        // Deregister all registered nodes
        foreach (var serviceId in _registeredNodes.Keys.ToList())
        {
            try
            {
                await _consulClient.Agent.ServiceDeregister(serviceId);
                _logger.LogDebug("Deregistered node: {ServiceId}", serviceId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deregistering node {ServiceId}", serviceId);
            }
        }

        _consulClient?.Dispose();
        _registrationLock?.Dispose();
        _cache?.Dispose();

        _disposed = true;

        _logger.LogInformation("Consul service discovery disposed");
    }
}
