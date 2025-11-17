namespace HotSwap.Distributed.Domain.Models;

/// <summary>
/// Represents the health status of a message broker.
/// </summary>
public enum BrokerHealthStatus
{
    /// <summary>
    /// Health status is unknown (initial state before first check).
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Broker is operating normally with good performance.
    /// Queue depth is low and consumers are keeping up.
    /// </summary>
    Healthy = 1,

    /// <summary>
    /// Broker is experiencing some degradation but still functional.
    /// Queue depth is elevated but manageable.
    /// </summary>
    Degraded = 2,

    /// <summary>
    /// Broker is unhealthy and may require intervention.
    /// Queue depth is very high or consumers are significantly lagging.
    /// </summary>
    Unhealthy = 3
}
