namespace HotSwap.Distributed.Infrastructure.Interfaces;

/// <summary>
/// Provides distributed locking mechanism for coordination.
/// </summary>
public interface IDistributedLock
{
    /// <summary>
    /// Acquires a distributed lock.
    /// </summary>
    /// <param name="resource">Resource name to lock.</param>
    /// <param name="timeout">Maximum time to wait for lock.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Lock handle if acquired, null if timeout.</returns>
    Task<ILockHandle?> AcquireLockAsync(
        string resource,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Handle for a distributed lock that can be released.
/// </summary>
public interface ILockHandle : IAsyncDisposable
{
    /// <summary>
    /// Resource that is locked.
    /// </summary>
    string Resource { get; }

    /// <summary>
    /// When the lock was acquired.
    /// </summary>
    DateTime AcquiredAt { get; }

    /// <summary>
    /// Whether the lock is still held.
    /// </summary>
    bool IsHeld { get; }

    /// <summary>
    /// Explicitly releases the lock.
    /// </summary>
    Task ReleaseAsync();
}
