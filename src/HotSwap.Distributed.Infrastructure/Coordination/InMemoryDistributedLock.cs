using HotSwap.Distributed.Infrastructure.Interfaces;
using System.Collections.Concurrent;

namespace HotSwap.Distributed.Infrastructure.Coordination;

/// <summary>
/// In-memory implementation of distributed lock.
/// Uses ConcurrentDictionary with SemaphoreSlim to provide thread-safe locking behavior.
/// Note: Locks are process-local only - not truly distributed across multiple instances.
/// For production use across multiple instances, consider Redis-based locking.
/// </summary>
public class InMemoryDistributedLock : IDistributedLock
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    public async Task<ILockHandle?> AcquireLockAsync(string resource, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        var semaphore = _locks.GetOrAdd(resource, _ => new SemaphoreSlim(1, 1));
        var acquired = await semaphore.WaitAsync(timeout, cancellationToken);

        if (acquired)
        {
            return new InMemoryLockHandle(resource, semaphore, this);
        }

        return null;
    }

    internal void ReleaseSemaphore(string resource, SemaphoreSlim semaphore)
    {
        if (_locks.TryGetValue(resource, out var existingSemaphore) && existingSemaphore == semaphore)
        {
            semaphore.Release();
        }
    }

    /// <summary>
    /// In-memory lock handle.
    /// </summary>
    private class InMemoryLockHandle : ILockHandle
    {
        private readonly SemaphoreSlim _semaphore;
        private readonly InMemoryDistributedLock _parent;
        private bool _isHeld = true;

        public InMemoryLockHandle(string resource, SemaphoreSlim semaphore, InMemoryDistributedLock parent)
        {
            Resource = resource;
            _semaphore = semaphore;
            _parent = parent;
            AcquiredAt = DateTime.UtcNow;
        }

        public string Resource { get; }

        public DateTime AcquiredAt { get; }

        public bool IsHeld => _isHeld;

        public Task ReleaseAsync()
        {
            if (_isHeld)
            {
                _parent.ReleaseSemaphore(Resource, _semaphore);
                _isHeld = false;
            }
            return Task.CompletedTask;
        }

        public async ValueTask DisposeAsync()
        {
            await ReleaseAsync();
        }
    }
}
