using HotSwap.Distributed.Infrastructure.Interfaces;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace HotSwap.Distributed.Infrastructure.Coordination;

/// <summary>
/// Redis-based distributed lock implementation (Redlock algorithm).
/// </summary>
public class RedisDistributedLock : IDistributedLock
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisDistributedLock> _logger;

    public RedisDistributedLock(
        IConnectionMultiplexer redis,
        ILogger<RedisDistributedLock> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task<ILockHandle?> AcquireLockAsync(
        string resource,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        var lockKey = $"lock:{resource}";
        var lockValue = Guid.NewGuid().ToString();
        var expiry = TimeSpan.FromMinutes(5); // Lock auto-expires after 5 minutes
        var startTime = DateTime.UtcNow;

        var db = _redis.GetDatabase();

        while (DateTime.UtcNow - startTime < timeout)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Try to acquire lock
            var acquired = await db.StringSetAsync(
                lockKey,
                lockValue,
                expiry,
                When.NotExists);

            if (acquired)
            {
                _logger.LogDebug("Acquired lock for resource: {Resource}", resource);

                return new RedisLockHandle(
                    _redis,
                    lockKey,
                    lockValue,
                    resource,
                    _logger);
            }

            // Wait before retrying
            await Task.Delay(100, cancellationToken);
        }

        _logger.LogWarning("Failed to acquire lock for resource: {Resource} after {Timeout}",
            resource, timeout);

        return null;
    }
}

/// <summary>
/// Handle for a Redis-based distributed lock.
/// </summary>
internal class RedisLockHandle : ILockHandle
{
    private readonly IConnectionMultiplexer _redis;
    private readonly string _lockKey;
    private readonly string _lockValue;
    private readonly ILogger _logger;
    private bool _isReleased;

    public string Resource { get; }
    public DateTime AcquiredAt { get; }
    public bool IsHeld => !_isReleased;

    public RedisLockHandle(
        IConnectionMultiplexer redis,
        string lockKey,
        string lockValue,
        string resource,
        ILogger logger)
    {
        _redis = redis;
        _lockKey = lockKey;
        _lockValue = lockValue;
        Resource = resource;
        _logger = logger;
        AcquiredAt = DateTime.UtcNow;
        _isReleased = false;
    }

    public async Task ReleaseAsync()
    {
        if (_isReleased)
            return;

        try
        {
            var db = _redis.GetDatabase();

            // Lua script to ensure we only delete our own lock
            var script = @"
                if redis.call('get', KEYS[1]) == ARGV[1] then
                    return redis.call('del', KEYS[1])
                else
                    return 0
                end";

            var result = await db.ScriptEvaluateAsync(
                script,
                new RedisKey[] { _lockKey },
                new RedisValue[] { _lockValue });

            if ((int)result == 1)
            {
                _logger.LogDebug("Released lock for resource: {Resource}", Resource);
                _isReleased = true;
            }
            else
            {
                _logger.LogWarning("Lock for resource {Resource} was already released or expired", Resource);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error releasing lock for resource: {Resource}", Resource);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await ReleaseAsync();
    }
}
