namespace HotSwap.Distributed.Domain.Enums;

/// <summary>
/// Status of a deployment approval request.
/// </summary>
public enum ApprovalStatus
{
    /// <summary>
    /// Approval request is pending decision.
    /// </summary>
    Pending,

    /// <summary>
    /// Automatically approved (no breaking changes detected).
    /// </summary>
    AutoApproved,

    /// <summary>
    /// Approval request has been approved.
    /// </summary>
    Approved,

    /// <summary>
    /// Approval request has been rejected.
    /// </summary>
    Rejected,

    /// <summary>
    /// Approval request has expired (timeout reached).
    /// </summary>
    Expired
}
