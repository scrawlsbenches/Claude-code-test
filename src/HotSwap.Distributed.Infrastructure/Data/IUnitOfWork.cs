using HotSwap.Distributed.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore.Storage;

namespace HotSwap.Distributed.Infrastructure.Data;

/// <summary>
/// Unit of Work pattern for managing transactional consistency across repositories.
/// Ensures atomic operations spanning multiple repository calls.
/// </summary>
public interface IUnitOfWork : IDisposable
{
    /// <summary>
    /// Gets the approval repository
    /// </summary>
    IApprovalRepository Approvals { get; }

    /// <summary>
    /// Gets the audit log repository
    /// </summary>
    IAuditLogRepository AuditLogs { get; }

    /// <summary>
    /// Saves all pending changes to the database
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Begins a new database transaction
    /// </summary>
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Commits the current transaction
    /// </summary>
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Rolls back the current transaction
    /// </summary>
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current transaction (if any)
    /// </summary>
    IDbContextTransaction? CurrentTransaction { get; }
}
