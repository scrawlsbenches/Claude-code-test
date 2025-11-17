namespace HotSwap.Distributed.Domain.Enums;

/// <summary>
/// Represents the role of a broker node.
/// </summary>
public enum BrokerRole
{
    /// <summary>
    /// Master node (handles writes and coordination).
    /// </summary>
    Master,

    /// <summary>
    /// Replica node (handles reads and provides redundancy).
    /// </summary>
    Replica
}
