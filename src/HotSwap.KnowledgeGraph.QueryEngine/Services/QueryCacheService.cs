using System.Text;
using System.Text.Json;
using HotSwap.KnowledgeGraph.Domain.Models;
using Microsoft.Extensions.Caching.Memory;

namespace HotSwap.KnowledgeGraph.QueryEngine.Services;

/// <summary>
/// In-memory cache service for query results with TTL and statistics tracking.
/// </summary>
public class QueryCacheService
{
    private readonly IMemoryCache _cache;
    private readonly TimeSpan _cacheDuration;
    private long _hits;
    private long _misses;

    /// <summary>
    /// Initializes a new instance of the QueryCacheService.
    /// </summary>
    /// <param name="cacheDurationSeconds">Cache entry time-to-live in seconds.</param>
    /// <exception cref="ArgumentException">Thrown when duration is not positive.</exception>
    public QueryCacheService(int cacheDurationSeconds)
    {
        if (cacheDurationSeconds <= 0)
        {
            throw new ArgumentException("Cache duration must be greater than zero", nameof(cacheDurationSeconds));
        }

        _cache = new MemoryCache(new MemoryCacheOptions());
        _cacheDuration = TimeSpan.FromSeconds(cacheDurationSeconds);
        _hits = 0;
        _misses = 0;
    }

    /// <summary>
    /// Attempts to retrieve a cached query result.
    /// </summary>
    /// <param name="query">The query to look up.</param>
    /// <param name="result">The cached result, if found.</param>
    /// <returns>True if found in cache, false otherwise.</returns>
    public bool TryGet(GraphQuery query, out GraphQueryResult? result)
    {
        var cacheKey = GenerateCacheKey(query);

        if (_cache.TryGetValue(cacheKey, out GraphQueryResult? cachedResult))
        {
            Interlocked.Increment(ref _hits);

            // Mark result as from cache
            result = new GraphQueryResult
            {
                Entities = cachedResult!.Entities,
                Relationships = cachedResult.Relationships,
                TotalCount = cachedResult.TotalCount,
                ExecutionTime = cachedResult.ExecutionTime,
                QueryPlan = cachedResult.QueryPlan,
                TraceId = cachedResult.TraceId,
                FromCache = true, // Indicate this came from cache
                Warnings = cachedResult.Warnings
            };
            return true;
        }

        Interlocked.Increment(ref _misses);
        result = null;
        return false;
    }

    /// <summary>
    /// Stores a query result in the cache.
    /// </summary>
    /// <param name="query">The query that produced the result.</param>
    /// <param name="result">The result to cache.</param>
    public void Set(GraphQuery query, GraphQueryResult result)
    {
        var cacheKey = GenerateCacheKey(query);

        var cacheEntryOptions = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(_cacheDuration);

        _cache.Set(cacheKey, result, cacheEntryOptions);
    }

    /// <summary>
    /// Clears all cached entries.
    /// </summary>
    public void Clear()
    {
        if (_cache is MemoryCache memoryCache)
        {
            memoryCache.Compact(1.0); // Remove 100% of cache entries
        }
    }

    /// <summary>
    /// Gets cache performance statistics.
    /// </summary>
    /// <returns>Cache statistics including hits, misses, and hit rate.</returns>
    public CacheStatistics GetCacheStatistics()
    {
        var hits = Interlocked.Read(ref _hits);
        var misses = Interlocked.Read(ref _misses);
        var total = hits + misses;
        var hitRate = total > 0 ? (double)hits / total : 0.0;

        return new CacheStatistics
        {
            Hits = hits,
            Misses = misses,
            TotalRequests = total,
            HitRate = hitRate
        };
    }

    /// <summary>
    /// Generates a unique cache key for a query.
    /// Uses JSON serialization to ensure identical queries produce identical keys.
    /// </summary>
    private string GenerateCacheKey(GraphQuery query)
    {
        // Serialize query to JSON for consistent key generation
        // This ensures identical queries with different object instances produce the same key
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = false,
            WriteIndented = false
        };

        var queryJson = JsonSerializer.Serialize(query, jsonOptions);

        // Use hash for shorter keys
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(queryJson));
        return Convert.ToBase64String(hashBytes);
    }
}

/// <summary>
/// Cache performance statistics.
/// </summary>
public class CacheStatistics
{
    /// <summary>
    /// Number of cache hits.
    /// </summary>
    public long Hits { get; init; }

    /// <summary>
    /// Number of cache misses.
    /// </summary>
    public long Misses { get; init; }

    /// <summary>
    /// Total number of cache requests.
    /// </summary>
    public long TotalRequests { get; init; }

    /// <summary>
    /// Cache hit rate (0.0 to 1.0).
    /// </summary>
    public double HitRate { get; init; }
}
