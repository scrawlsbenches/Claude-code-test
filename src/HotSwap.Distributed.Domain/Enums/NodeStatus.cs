namespace HotSwap.Distributed.Domain.Enums;

/// <summary>
/// Represents the current status of a kernel node.
/// </summary>
public enum NodeStatus
{
    /// <summary>
    /// Node is starting up and initializing.
    /// </summary>
    Initializing,

    /// <summary>
    /// Node is running and healthy.
    /// </summary>
    Running,

    /// <summary>
    /// Node is running but experiencing degraded performance.
    /// </summary>
    Degraded,

    /// <summary>
    /// Node is shutting down gracefully.
    /// </summary>
    Stopping,

    /// <summary>
    /// Node has stopped.
    /// </summary>
    Stopped,

    /// <summary>
    /// Node has failed and is not operational.
    /// </summary>
    Failed
}
