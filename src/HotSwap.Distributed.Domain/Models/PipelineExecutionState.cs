namespace HotSwap.Distributed.Domain.Models;

/// <summary>
/// Represents the current state of an in-progress deployment pipeline execution.
/// Used for tracking and reporting progress in real-time.
/// </summary>
public class PipelineExecutionState
{
    /// <summary>
    /// The unique execution ID of the deployment
    /// </summary>
    public Guid ExecutionId { get; set; }

    /// <summary>
    /// The deployment request being executed
    /// </summary>
    public required DeploymentRequest Request { get; set; }

    /// <summary>
    /// Current overall status of the deployment
    /// </summary>
    public string Status { get; set; } = "Running";

    /// <summary>
    /// Name of the current stage being executed
    /// </summary>
    public string? CurrentStage { get; set; }

    /// <summary>
    /// Completed and in-progress stages
    /// </summary>
    public List<PipelineStageResult> Stages { get; set; } = new();

    /// <summary>
    /// When the pipeline execution started
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// Last time this state was updated
    /// </summary>
    public DateTime LastUpdated { get; set; }
}
