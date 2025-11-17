using System.Collections.Concurrent;
using HotSwap.Distributed.Infrastructure.Interfaces;

namespace HotSwap.Distributed.Infrastructure.Coordination;

/// <summary>
/// In-memory implementation of idempotency store.
/// Suitable for single-instance deployments or testing.
/// </summary>
public class InMemoryIdempotencyStore : IIdempotencyStore
{
    private readonly ConcurrentDictionary<string, IdempotencyRecord> _store;
    private readonly TimeSpan _expirationTime;

    public InMemoryIdempotencyStore(TimeSpan? expirationTime = null)
    {
        _store = new ConcurrentDictionary<string, IdempotencyRecord>();
        _expirationTime = expirationTime ?? TimeSpan.FromHours(24);
    }

    public Task<bool> HasBeenProcessedAsync(string idempotencyKey, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(idempotencyKey, nameof(idempotencyKey));

        if (_store.TryGetValue(idempotencyKey, out var record))
        {
            // Check if expired
            if (DateTime.UtcNow - record.ProcessedAt > _expirationTime)
            {
                _store.TryRemove(idempotencyKey, out _);
                return Task.FromResult(false);
            }

            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }

    public Task MarkAsProcessedAsync(string idempotencyKey, string messageId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(idempotencyKey, nameof(idempotencyKey));
        ArgumentNullException.ThrowIfNull(messageId, nameof(messageId));

        var record = new IdempotencyRecord
        {
            IdempotencyKey = idempotencyKey,
            MessageId = messageId,
            ProcessedAt = DateTime.UtcNow
        };

        _store.TryAdd(idempotencyKey, record);
        return Task.CompletedTask;
    }

    public Task<string?> GetMessageIdAsync(string idempotencyKey, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(idempotencyKey, nameof(idempotencyKey));

        if (_store.TryGetValue(idempotencyKey, out var record))
        {
            // Check if expired
            if (DateTime.UtcNow - record.ProcessedAt > _expirationTime)
            {
                _store.TryRemove(idempotencyKey, out _);
                return Task.FromResult<string?>(null);
            }

            return Task.FromResult<string?>(record.MessageId);
        }

        return Task.FromResult<string?>(null);
    }

    public Task RemoveAsync(string idempotencyKey, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(idempotencyKey, nameof(idempotencyKey));

        _store.TryRemove(idempotencyKey, out _);
        return Task.CompletedTask;
    }

    private class IdempotencyRecord
    {
        public required string IdempotencyKey { get; set; }
        public required string MessageId { get; set; }
        public DateTime ProcessedAt { get; set; }
    }
}
