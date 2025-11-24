using HotSwap.Distributed.Infrastructure.Data.Entities;

namespace HotSwap.Distributed.Infrastructure.Repositories;

/// <summary>
/// Repository interface for audit log operations
/// </summary>
public interface IAuditLogRepository
{
    /// <summary>
    /// Creates a new audit log entry
    /// </summary>
    Task CreateAsync(AuditLog auditLog, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an audit log by ID
    /// </summary>
    Task<AuditLog?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all audit logs for a specific entity
    /// </summary>
    Task<List<AuditLog>> GetByEntityAsync(string entityType, Guid entityId, CancellationToken cancellationToken = default);
}
