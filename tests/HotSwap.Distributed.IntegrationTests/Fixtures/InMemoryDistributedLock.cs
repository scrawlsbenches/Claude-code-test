using System.Collections.Concurrent;
using HotSwap.Distributed.Infrastructure.Interfaces;

namespace HotSwap.Distributed.IntegrationTests.Fixtures;

/// <summary>
/// In-memory implementation of distributed lock for testing.
/// Uses ConcurrentDictionary with SemaphoreSlim to simulate distributed locking behavior.
/// This allows tests to run without Redis.
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
    /// In-memory lock handle for testing.
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
