namespace HotSwap.Distributed.Domain.Enums;

/// <summary>
/// Status of a pipeline stage execution.
/// </summary>
public enum PipelineStageStatus
{
    /// <summary>
    /// Stage has not started yet.
    /// </summary>
    Pending,

    /// <summary>
    /// Stage is currently executing.
    /// </summary>
    Running,

    /// <summary>
    /// Stage completed successfully.
    /// </summary>
    Succeeded,

    /// <summary>
    /// Stage failed to complete.
    /// </summary>
    Failed,

    /// <summary>
    /// Stage was skipped.
    /// </summary>
    Skipped,

    /// <summary>
    /// Stage is waiting for approval.
    /// </summary>
    WaitingForApproval
}
