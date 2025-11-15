namespace HotSwap.Distributed.Domain.Models;

/// <summary>
/// Represents an approval decision (approve or reject) for a deployment.
/// </summary>
public class ApprovalDecision
{
    /// <summary>
    /// The deployment execution ID being approved or rejected.
    /// </summary>
    public required Guid DeploymentExecutionId { get; set; }

    /// <summary>
    /// Email of the approver making the decision.
    /// </summary>
    public required string ApproverEmail { get; set; }

    /// <summary>
    /// Whether the deployment is approved (true) or rejected (false).
    /// </summary>
    public required bool Approved { get; set; }

    /// <summary>
    /// Optional reason for the approval or rejection decision.
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// When the decision was made.
    /// </summary>
    public DateTime DecidedAt { get; set; } = DateTime.UtcNow;
}
