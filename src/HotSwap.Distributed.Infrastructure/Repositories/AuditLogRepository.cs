using HotSwap.Distributed.Infrastructure.Data;
using HotSwap.Distributed.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace HotSwap.Distributed.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for audit log operations
/// </summary>
public class AuditLogRepository : IAuditLogRepository
{
    private readonly AuditLogDbContext _context;

    public AuditLogRepository(AuditLogDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc />
    public async Task CreateAsync(AuditLog auditLog, CancellationToken cancellationToken = default)
    {
        _context.AuditLogs.Add(auditLog);
        // Only auto-save if not within a transaction (Unit of Work pattern support)
        if (_context.Database.CurrentTransaction == null)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task<AuditLog?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.AuditLogs
            .FirstOrDefaultAsync(a => a.EventId == id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<List<AuditLog>> GetByEntityAsync(
        string entityType,
        Guid entityId,
        CancellationToken cancellationToken = default)
    {
        return await _context.AuditLogs
            .Where(a => a.ResourceType == entityType && a.ResourceId == entityId.ToString())
            .OrderByDescending(a => a.Timestamp)
            .ToListAsync(cancellationToken);
    }
}
