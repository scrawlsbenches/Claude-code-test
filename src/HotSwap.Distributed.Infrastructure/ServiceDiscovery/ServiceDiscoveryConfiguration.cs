namespace HotSwap.Distributed.Infrastructure.ServiceDiscovery;

/// <summary>
/// Configuration for service discovery.
/// </summary>
public class ServiceDiscoveryConfiguration
{
    /// <summary>
    /// Service discovery backend type (consul, etcd, inmemory).
    /// </summary>
    public string Backend { get; set; } = "consul";

    /// <summary>
    /// Service name for registration.
    /// </summary>
    public string ServiceName { get; set; } = "hotswap-node";

    /// <summary>
    /// Enable automatic node registration.
    /// </summary>
    public bool EnableAutoRegistration { get; set; } = true;

    /// <summary>
    /// Enable health check registration.
    /// </summary>
    public bool EnableHealthChecks { get; set; } = true;

    /// <summary>
    /// Health check interval in seconds.
    /// </summary>
    public int HealthCheckIntervalSeconds { get; set; } = 10;

    /// <summary>
    /// Health check timeout in seconds.
    /// </summary>
    public int HealthCheckTimeoutSeconds { get; set; } = 5;

    /// <summary>
    /// Health check endpoint path.
    /// </summary>
    public string HealthCheckPath { get; set; } = "/health";

    /// <summary>
    /// Cache expiration in seconds.
    /// </summary>
    public int CacheExpirationSeconds { get; set; } = 30;

    /// <summary>
    /// Consul-specific configuration.
    /// </summary>
    public ConsulConfiguration Consul { get; set; } = new();

    /// <summary>
    /// etcd-specific configuration.
    /// </summary>
    public EtcdConfiguration Etcd { get; set; } = new();
}

/// <summary>
/// Consul-specific configuration.
/// </summary>
public class ConsulConfiguration
{
    /// <summary>
    /// Consul server address.
    /// </summary>
    public string Address { get; set; } = "http://localhost:8500";

    /// <summary>
    /// Consul datacenter.
    /// </summary>
    public string Datacenter { get; set; } = "dc1";

    /// <summary>
    /// Consul token for authentication.
    /// </summary>
    public string? Token { get; set; }

    /// <summary>
    /// Enable TLS.
    /// </summary>
    public bool UseTls { get; set; } = false;

    /// <summary>
    /// Deregister critical services after (e.g., "30s", "1m").
    /// </summary>
    public string DeregisterCriticalServiceAfter { get; set; } = "30s";
}

/// <summary>
/// etcd-specific configuration.
/// </summary>
public class EtcdConfiguration
{
    /// <summary>
    /// etcd server endpoints.
    /// </summary>
    public List<string> Endpoints { get; set; } = new() { "http://localhost:2379" };

    /// <summary>
    /// Username for authentication.
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Password for authentication.
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// TTL for node registrations in seconds.
    /// </summary>
    public int TtlSeconds { get; set; } = 30;

    /// <summary>
    /// Key prefix for service discovery.
    /// </summary>
    public string KeyPrefix { get; set; } = "/services/hotswap";
}
