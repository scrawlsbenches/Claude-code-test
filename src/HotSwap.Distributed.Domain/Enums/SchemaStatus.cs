namespace HotSwap.Distributed.Domain.Enums;

/// <summary>
/// Represents the status of a schema in the approval workflow.
/// </summary>
public enum SchemaStatus
{
    /// <summary>
    /// Schema is in draft state (not yet submitted for approval).
    /// </summary>
    Draft,

    /// <summary>
    /// Schema is pending admin approval.
    /// </summary>
    PendingApproval,

    /// <summary>
    /// Schema is approved for production use.
    /// </summary>
    Approved,

    /// <summary>
    /// Schema is deprecated (marked for removal).
    /// </summary>
    Deprecated
}
