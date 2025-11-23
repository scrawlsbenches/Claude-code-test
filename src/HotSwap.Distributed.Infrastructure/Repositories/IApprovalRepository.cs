using HotSwap.Distributed.Infrastructure.Data.Entities;

namespace HotSwap.Distributed.Infrastructure.Repositories;

/// <summary>
/// Repository interface for approval request persistence.
/// </summary>
public interface IApprovalRepository
{
    /// <summary>
    /// Creates a new approval request.
    /// </summary>
    Task<ApprovalRequestEntity> CreateAsync(ApprovalRequestEntity request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an approval request by deployment execution ID.
    /// </summary>
    Task<ApprovalRequestEntity?> GetByIdAsync(Guid deploymentExecutionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all pending approval requests that haven't expired.
    /// </summary>
    Task<List<ApprovalRequestEntity>> GetPendingAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all expired pending approval requests.
    /// </summary>
    Task<List<ApprovalRequestEntity>> GetExpiredPendingAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing approval request.
    /// </summary>
    Task<ApprovalRequestEntity> UpdateAsync(ApprovalRequestEntity request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Expires all pending approval requests that have timed out.
    /// </summary>
    /// <returns>Number of requests expired</returns>
    Task<int> ExpirePendingRequestsAsync(DateTime now, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes approval requests older than the specified date (cleanup).
    /// </summary>
    Task<int> DeleteOlderThanAsync(DateTime cutoffDate, CancellationToken cancellationToken = default);
}
