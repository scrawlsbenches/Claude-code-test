using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Infrastructure.Data;
using HotSwap.Distributed.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HotSwap.Distributed.Infrastructure.Repositories;

/// <summary>
/// PostgreSQL repository for approval request persistence.
/// Replaces static in-memory storage with durable database storage.
/// </summary>
public class ApprovalRepository : IApprovalRepository
{
    private readonly AuditLogDbContext _dbContext;
    private readonly ILogger<ApprovalRepository> _logger;

    public ApprovalRepository(
        AuditLogDbContext dbContext,
        ILogger<ApprovalRepository> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<ApprovalRequestEntity> CreateAsync(
        ApprovalRequestEntity request,
        CancellationToken cancellationToken = default)
    {
        request.CreatedAt = DateTime.UtcNow;
        request.UpdatedAt = DateTime.UtcNow;

        _dbContext.ApprovalRequests.Add(request);

        // Only auto-save if not within a transaction (Unit of Work pattern support)
        if (_dbContext.Database.CurrentTransaction == null)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        _logger.LogDebug("Created approval request {ApprovalId} for deployment {DeploymentId}",
            request.ApprovalId, request.DeploymentExecutionId);

        return request;
    }

    /// <inheritdoc />
    public async Task<ApprovalRequestEntity?> GetByIdAsync(
        Guid deploymentExecutionId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.ApprovalRequests
            .FirstOrDefaultAsync(a => a.DeploymentExecutionId == deploymentExecutionId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<List<ApprovalRequestEntity>> GetPendingAsync(
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        return await _dbContext.ApprovalRequests
            .Where(a => a.Status == ApprovalStatus.Pending && a.TimeoutAt > now)
            .OrderBy(a => a.RequestedAt)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<List<ApprovalRequestEntity>> GetExpiredPendingAsync(
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        return await _dbContext.ApprovalRequests
            .Where(a => a.Status == ApprovalStatus.Pending && a.TimeoutAt <= now)
            .OrderBy(a => a.TimeoutAt)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ApprovalRequestEntity> UpdateAsync(
        ApprovalRequestEntity request,
        CancellationToken cancellationToken = default)
    {
        request.UpdatedAt = DateTime.UtcNow;

        _dbContext.ApprovalRequests.Update(request);

        // Only auto-save if not within a transaction (Unit of Work pattern support)
        if (_dbContext.Database.CurrentTransaction == null)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        _logger.LogDebug("Updated approval request {ApprovalId} status to {Status}",
            request.ApprovalId, request.Status);

        return request;
    }

    /// <inheritdoc />
    public async Task<int> ExpirePendingRequestsAsync(
        DateTime now,
        CancellationToken cancellationToken = default)
    {
        // Use ExecuteUpdate for efficient bulk update (EF Core 7+)
        var expiredCount = await _dbContext.ApprovalRequests
            .Where(a => a.Status == ApprovalStatus.Pending && a.TimeoutAt <= now)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(a => a.Status, ApprovalStatus.Expired)
                .SetProperty(a => a.RespondedAt, now)
                .SetProperty(a => a.ResponseReason, "Automatically expired due to timeout")
                .SetProperty(a => a.UpdatedAt, now),
                cancellationToken);

        if (expiredCount > 0)
        {
            _logger.LogInformation("Expired {Count} approval requests", expiredCount);
        }

        return expiredCount;
    }

    /// <inheritdoc />
    public async Task<int> DeleteOlderThanAsync(
        DateTime cutoffDate,
        CancellationToken cancellationToken = default)
    {
        var deletedCount = await _dbContext.ApprovalRequests
            .Where(a => a.CreatedAt < cutoffDate)
            .ExecuteDeleteAsync(cancellationToken);

        if (deletedCount > 0)
        {
            _logger.LogInformation("Deleted {Count} old approval requests (older than {CutoffDate})",
                deletedCount, cutoffDate);
        }

        return deletedCount;
    }
}
