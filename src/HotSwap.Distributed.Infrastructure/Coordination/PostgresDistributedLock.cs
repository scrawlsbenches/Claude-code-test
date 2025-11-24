using System.Security.Cryptography;
using System.Text;
using HotSwap.Distributed.Infrastructure.Data;
using HotSwap.Distributed.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace HotSwap.Distributed.Infrastructure.Coordination;

/// <summary>
/// PostgreSQL-based distributed lock implementation using advisory locks.
/// Uses pg_advisory_lock functions for true distributed coordination across multiple instances.
/// Locks are automatically released when the database connection closes.
/// </summary>
public class PostgresDistributedLock : IDistributedLock
{
    private readonly AuditLogDbContext _dbContext;
    private readonly ILogger<PostgresDistributedLock> _logger;

    public PostgresDistributedLock(
        AuditLogDbContext dbContext,
        ILogger<PostgresDistributedLock> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Acquires a distributed lock using PostgreSQL advisory locks.
    /// </summary>
    /// <param name="resource">Resource name to lock</param>
    /// <param name="timeout">Maximum time to wait for lock</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Lock handle if acquired, null if timeout</returns>
    public async Task<ILockHandle?> AcquireLockAsync(
        string resource,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(resource))
            throw new ArgumentException("Resource name cannot be empty", nameof(resource));

        var lockKey = GetLockKey(resource);
        var startTime = DateTime.UtcNow;

        _logger.LogDebug("Attempting to acquire lock for resource '{Resource}' (key: {LockKey}, timeout: {Timeout})",
            resource, lockKey, timeout);

        // Create a dedicated connection for this lock
        var connectionString = _dbContext.Database.GetConnectionString();
        if (connectionString == null)
            throw new InvalidOperationException("Database connection string is null");

        var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        try
        {
            // Try to acquire the lock with timeout
            var acquired = await TryAcquireLockWithTimeoutAsync(
                connection,
                lockKey,
                timeout,
                cancellationToken);

            if (acquired)
            {
                var elapsed = DateTime.UtcNow - startTime;
                _logger.LogInformation("Acquired lock for resource '{Resource}' (key: {LockKey}, elapsed: {Elapsed}ms)",
                    resource, lockKey, elapsed.TotalMilliseconds);

                return new PostgresLockHandle(resource, lockKey, connection, _logger);
            }
            else
            {
                _logger.LogWarning("Failed to acquire lock for resource '{Resource}' (key: {LockKey}, timeout: {Timeout})",
                    resource, lockKey, timeout);

                await connection.CloseAsync();
                await connection.DisposeAsync();
                return null;
            }
        }
        catch
        {
            // Clean up connection on error
            await connection.CloseAsync();
            await connection.DisposeAsync();
            throw;
        }
    }

    /// <summary>
    /// Tries to acquire a PostgreSQL advisory lock with timeout using efficient blocking strategy.
    /// Uses lock_timeout to avoid polling overhead.
    /// </summary>
    private async Task<bool> TryAcquireLockWithTimeoutAsync(
        NpgsqlConnection connection,
        long lockKey,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        try
        {
            // Set lock_timeout for this connection (PostgreSQL blocks until timeout)
            // This is much more efficient than polling
            var timeoutMs = (int)timeout.TotalMilliseconds;

            await using var setTimeoutCmd = new NpgsqlCommand(
                "SET lock_timeout = @timeoutMs", connection);
            setTimeoutCmd.Parameters.AddWithValue("timeoutMs", timeoutMs);
            await setTimeoutCmd.ExecuteNonQueryAsync(cancellationToken);

            // Use blocking pg_advisory_lock - will wait up to lock_timeout
            await using var lockCmd = new NpgsqlCommand(
                "SELECT pg_advisory_lock(@lockKey)", connection);
            lockCmd.Parameters.AddWithValue("lockKey", lockKey);

            await lockCmd.ExecuteNonQueryAsync(cancellationToken);

            // If we get here, lock was acquired
            return true;
        }
        catch (PostgresException ex) when (ex.SqlState == "55P03") // lock_not_available
        {
            _logger.LogDebug("Lock acquisition timed out for key {LockKey} after {Timeout}ms",
                lockKey, timeout.TotalMilliseconds);
            return false;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogDebug("Lock acquisition cancelled for key {LockKey}", lockKey);
            return false;
        }
    }

    /// <summary>
    /// Generates a 64-bit lock key from a resource name using SHA-256 hash.
    /// This ensures consistent lock keys across different instances.
    /// </summary>
    private static long GetLockKey(string resource)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(resource));
        return BitConverter.ToInt64(hash, 0);
    }

    /// <summary>
    /// PostgreSQL advisory lock handle that manages lock lifecycle.
    /// </summary>
    private class PostgresLockHandle : ILockHandle
    {
        private readonly long _lockKey;
        private readonly NpgsqlConnection _connection;
        private readonly ILogger _logger;
        private bool _isHeld = true;

        public PostgresLockHandle(
            string resource,
            long lockKey,
            NpgsqlConnection connection,
            ILogger logger)
        {
            Resource = resource;
            _lockKey = lockKey;
            _connection = connection;
            _logger = logger;
            AcquiredAt = DateTime.UtcNow;
        }

        public string Resource { get; }

        public DateTime AcquiredAt { get; }

        public bool IsHeld => _isHeld && _connection.State == System.Data.ConnectionState.Open;

        public async Task ReleaseAsync()
        {
            if (!_isHeld)
                return;

            try
            {
                if (_connection.State == System.Data.ConnectionState.Open)
                {
                    // Release the advisory lock
                    await using var cmd = new NpgsqlCommand("SELECT pg_advisory_unlock(@lockKey)", _connection);
                    cmd.Parameters.AddWithValue("lockKey", _lockKey);

                    var result = await cmd.ExecuteScalarAsync();
                    if (result is bool released && released)
                    {
                        _logger.LogInformation("Released lock for resource '{Resource}' (key: {LockKey})",
                            Resource, _lockKey);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to release lock for resource '{Resource}' (key: {LockKey}) - lock was not held",
                            Resource, _lockKey);
                    }
                }

                _isHeld = false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error releasing lock for resource '{Resource}' (key: {LockKey})",
                    Resource, _lockKey);
                throw;
            }
            finally
            {
                // Always close the connection
                await _connection.CloseAsync();
                await _connection.DisposeAsync();
            }
        }

        public async ValueTask DisposeAsync()
        {
            await ReleaseAsync();
        }
    }
}
