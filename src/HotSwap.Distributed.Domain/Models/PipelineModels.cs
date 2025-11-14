using HotSwap.Distributed.Domain.Enums;

namespace HotSwap.Distributed.Domain.Models;

/// <summary>
/// Result of executing a deployment pipeline.
/// </summary>
public class PipelineExecutionResult
{
    /// <summary>
    /// Unique execution identifier.
    /// </summary>
    public Guid ExecutionId { get; set; }

    /// <summary>
    /// Module that was deployed.
    /// </summary>
    public required string ModuleName { get; set; }

    /// <summary>
    /// Module version.
    /// </summary>
    public required Version Version { get; set; }

    /// <summary>
    /// Overall success status.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Pipeline start time.
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// Pipeline end time.
    /// </summary>
    public DateTime EndTime { get; set; }

    /// <summary>
    /// Total duration.
    /// </summary>
    public TimeSpan Duration => EndTime - StartTime;

    /// <summary>
    /// Results of each pipeline stage.
    /// </summary>
    public List<PipelineStageResult> StageResults { get; set; } = new();

    /// <summary>
    /// Distributed trace ID.
    /// </summary>
    public string? TraceId { get; set; }

    /// <summary>
    /// Summary message.
    /// </summary>
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Result of a single pipeline stage.
/// </summary>
public class PipelineStageResult
{
    /// <summary>
    /// Stage name (e.g., "Build", "Test", "Deploy to Production").
    /// </summary>
    public required string StageName { get; set; }

    /// <summary>
    /// Stage status.
    /// </summary>
    public PipelineStageStatus Status { get; set; }

    /// <summary>
    /// Deployment strategy used (if applicable).
    /// </summary>
    public string? Strategy { get; set; }

    /// <summary>
    /// Start time.
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// End time.
    /// </summary>
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// Duration of the stage.
    /// </summary>
    public TimeSpan Duration => EndTime.HasValue ? EndTime.Value - StartTime : TimeSpan.Zero;

    /// <summary>
    /// Number of nodes deployed (for deployment stages).
    /// </summary>
    public int? NodesDeployed { get; set; }

    /// <summary>
    /// Number of nodes that failed (for deployment stages).
    /// </summary>
    public int? NodesFailed { get; set; }

    /// <summary>
    /// Detailed result message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Exception if stage failed.
    /// </summary>
    public Exception? Exception { get; set; }
}

/// <summary>
/// Configuration for the deployment pipeline.
/// </summary>
public class PipelineConfiguration
{
    /// <summary>
    /// Maximum number of concurrent pipeline executions.
    /// </summary>
    public int MaxConcurrentPipelines { get; set; } = 5;

    /// <summary>
    /// Maximum concurrent nodes for QA rolling deployment.
    /// </summary>
    public int QaMaxConcurrentNodes { get; set; } = 2;

    /// <summary>
    /// Timeout for staging smoke tests.
    /// </summary>
    public TimeSpan StagingSmokeTestTimeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Initial percentage for canary deployment.
    /// </summary>
    public int CanaryInitialPercentage { get; set; } = 10;

    /// <summary>
    /// Increment percentage for canary deployment.
    /// </summary>
    public int CanaryIncrementPercentage { get; set; } = 20;

    /// <summary>
    /// Wait duration between canary increments.
    /// </summary>
    public TimeSpan CanaryWaitDuration { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Whether to automatically rollback on failure.
    /// </summary>
    public bool AutoRollbackOnFailure { get; set; } = true;

    /// <summary>
    /// Approval timeout in hours.
    /// </summary>
    public int ApprovalTimeoutHours { get; set; } = 4;
}
