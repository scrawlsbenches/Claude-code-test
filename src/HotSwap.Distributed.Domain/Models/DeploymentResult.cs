using HotSwap.Distributed.Domain.Enums;

namespace HotSwap.Distributed.Domain.Models;

/// <summary>
/// Result of a deployment operation.
/// </summary>
public class DeploymentResult
{
    /// <summary>
    /// Strategy used for deployment.
    /// </summary>
    public string Strategy { get; set; } = string.Empty;

    /// <summary>
    /// Target environment.
    /// </summary>
    public EnvironmentType Environment { get; set; }

    /// <summary>
    /// Whether the deployment succeeded.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Summary message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Exception if deployment failed.
    /// </summary>
    public Exception? Exception { get; set; }

    /// <summary>
    /// Deployment start time.
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// Deployment end time.
    /// </summary>
    public DateTime EndTime { get; set; }

    /// <summary>
    /// Total duration.
    /// </summary>
    public TimeSpan Duration => EndTime - StartTime;

    /// <summary>
    /// Results per node.
    /// </summary>
    public List<NodeDeploymentResult> NodeResults { get; set; } = new();

    /// <summary>
    /// Whether rollback was performed.
    /// </summary>
    public bool RollbackPerformed { get; set; }

    /// <summary>
    /// Whether rollback succeeded.
    /// </summary>
    public bool RollbackSuccessful { get; set; }

    /// <summary>
    /// Rollback results per node.
    /// </summary>
    public List<NodeRollbackResult> RollbackResults { get; set; } = new();

    /// <summary>
    /// Distributed trace ID.
    /// </summary>
    public string? TraceId { get; set; }

    /// <summary>
    /// Span ID within the trace.
    /// </summary>
    public string? SpanId { get; set; }
}

/// <summary>
/// Result of deploying to a single node.
/// </summary>
public class NodeDeploymentResult
{
    public Guid NodeId { get; set; }
    public string Hostname { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public Exception? Exception { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public TimeSpan Duration { get; set; }
}

/// <summary>
/// Result of rolling back a node.
/// </summary>
public class NodeRollbackResult
{
    public Guid NodeId { get; set; }
    public string Hostname { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public Exception? Exception { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
