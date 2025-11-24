using HotSwap.Distributed.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace HotSwap.Distributed.Infrastructure.Data;

/// <summary>
/// Concrete implementation of Unit of Work pattern using EF Core.
/// Coordinates multiple repositories within a single transaction.
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly AuditLogDbContext _context;
    private readonly ILogger<UnitOfWork> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private IDbContextTransaction? _transaction;
    private bool _disposed;

    // Lazy-initialized repositories
    private IApprovalRepository? _approvals;
    private IAuditLogRepository? _auditLogs;

    public UnitOfWork(
        AuditLogDbContext context,
        ILogger<UnitOfWork> logger,
        ILoggerFactory loggerFactory)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
    }

    /// <inheritdoc />
    public IApprovalRepository Approvals
    {
        get
        {
            _approvals ??= new ApprovalRepository(_context, _loggerFactory.CreateLogger<ApprovalRepository>());
            return _approvals;
        }
    }

    /// <inheritdoc />
    public IAuditLogRepository AuditLogs
    {
        get
        {
            _auditLogs ??= new AuditLogRepository(_context);
            return _auditLogs;
        }
    }

    /// <inheritdoc />
    public IDbContextTransaction? CurrentTransaction => _transaction;

    /// <inheritdoc />
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var changes = await _context.SaveChangesAsync(cancellationToken);
            _logger.LogDebug("Saved {ChangeCount} changes to database", changes);
            return changes;
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database update failed");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction != null)
        {
            throw new InvalidOperationException("Transaction already in progress");
        }

        _transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        _logger.LogDebug("Transaction started: {TransactionId}", _transaction.TransactionId);
    }

    /// <inheritdoc />
    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction == null)
        {
            throw new InvalidOperationException("No transaction in progress");
        }

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            await _transaction.CommitAsync(cancellationToken);

            _logger.LogInformation("Transaction committed successfully: {TransactionId}",
                _transaction.TransactionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Transaction commit failed: {TransactionId}",
                _transaction.TransactionId);
            throw;
        }
        finally
        {
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    /// <inheritdoc />
    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction == null)
        {
            _logger.LogWarning("Attempted to rollback but no transaction in progress");
            return;
        }

        try
        {
            await _transaction.RollbackAsync(cancellationToken);
            _logger.LogInformation("Transaction rolled back: {TransactionId}",
                _transaction.TransactionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Transaction rollback failed: {TransactionId}",
                _transaction.TransactionId);
            throw;
        }
        finally
        {
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            // Rollback any uncommitted transaction
            if (_transaction != null)
            {
                _logger.LogWarning("UnitOfWork disposed with uncommitted transaction, rolling back");
                _transaction.Rollback();
                _transaction.Dispose();
                _transaction = null;
            }

            // Note: We don't dispose _context here as it's injected and managed by DI
        }

        _disposed = true;
    }
}
