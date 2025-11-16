using System.Collections.Concurrent;
using System.Net;

namespace HotSwap.Distributed.Api.Middleware;

/// <summary>
/// Middleware for API rate limiting based on IP address
/// </summary>
public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitingMiddleware> _logger;
    private readonly RateLimitConfiguration _config;

    // In-memory storage for request counters
    private static readonly ConcurrentDictionary<string, ClientRateLimitInfo> _clients = new();

    public RateLimitingMiddleware(
        RequestDelegate next,
        ILogger<RateLimitingMiddleware> logger,
        RateLimitConfiguration config)
    {
        _next = next;
        _logger = logger;
        _config = config;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip rate limiting for health checks
        if (context.Request.Path.StartsWithSegments("/health"))
        {
            await _next(context);
            return;
        }

        // Skip rate limiting if disabled in configuration
        if (!_config.Enabled)
        {
            await _next(context);
            return;
        }

        // Get client identifier (token-based for authenticated users, IP-based for others)
        var clientId = GetClientIdentifier(context);

        // Get or create client info
        var clientInfo = _clients.GetOrAdd(clientId, _ => new ClientRateLimitInfo());

        // Determine rate limit for this endpoint
        var limit = GetRateLimitForEndpoint(context.Request.Path);

        // Check if rate limit exceeded
        if (!clientInfo.TryRecordRequest(limit))
        {
            _logger.LogWarning(
                "Rate limit exceeded for client {ClientId} on endpoint {Path}. Limit: {Limit} requests per {Window}",
                clientId,
                context.Request.Path,
                limit.MaxRequests,
                limit.TimeWindow);

            // Set rate limit headers
            context.Response.Headers["X-RateLimit-Limit"] = limit.MaxRequests.ToString();
            context.Response.Headers["X-RateLimit-Remaining"] = "0";
            context.Response.Headers["X-RateLimit-Reset"] = DateTimeOffset.UtcNow.Add(limit.TimeWindow).ToUnixTimeSeconds().ToString();
            context.Response.Headers["Retry-After"] = ((int)limit.TimeWindow.TotalSeconds).ToString();

            context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "Rate limit exceeded",
                message = $"Too many requests. Please retry after {limit.TimeWindow.TotalSeconds} seconds.",
                retryAfter = limit.TimeWindow.TotalSeconds
            });

            return;
        }

        // Set rate limit headers for successful requests
        var remaining = limit.MaxRequests - clientInfo.GetRequestCount(limit.TimeWindow);
        context.Response.Headers["X-RateLimit-Limit"] = limit.MaxRequests.ToString();
        context.Response.Headers["X-RateLimit-Remaining"] = Math.Max(0, remaining).ToString();
        context.Response.Headers["X-RateLimit-Reset"] = clientInfo.GetResetTime(limit.TimeWindow).ToUnixTimeSeconds().ToString();

        await _next(context);
    }

    private string GetClientIdentifier(HttpContext context)
    {
        // For authenticated users, use token-based limiting (by username/subject)
        // This ensures each user gets their own rate limit quota
        if (context.User?.Identity?.IsAuthenticated == true)
        {
            var username = context.User.Identity.Name;
            if (!string.IsNullOrEmpty(username))
            {
                return $"user:{username}";
            }

            // Fallback to subject claim if Name is not available
            var subjectClaim = context.User.Claims.FirstOrDefault(c => c.Type == "sub" || c.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier");
            if (subjectClaim != null)
            {
                return $"user:{subjectClaim.Value}";
            }
        }

        // For unauthenticated requests, use IP-based limiting
        // Check for X-Forwarded-For header (when behind proxy)
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            return $"ip:{forwardedFor.Split(',')[0].Trim()}";
        }

        // Fall back to direct connection IP
        var remoteIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return $"ip:{remoteIp}";
    }

    private RateLimit GetRateLimitForEndpoint(PathString path)
    {
        // Check for specific endpoint limits
        foreach (var endpointLimit in _config.EndpointLimits)
        {
            if (path.StartsWithSegments(endpointLimit.Key))
            {
                return endpointLimit.Value;
            }
        }

        // Return global limit
        return _config.GlobalLimit;
    }

    /// <summary>
    /// Cleans up expired rate limit entries from the cache.
    /// Called periodically by RateLimitCleanupService.
    /// </summary>
    public static void CleanupExpiredEntries()
    {
        var now = DateTimeOffset.UtcNow;
        var keysToRemove = new List<string>();

        foreach (var kvp in _clients)
        {
            if (kvp.Value.IsExpired(now))
            {
                keysToRemove.Add(kvp.Key);
            }
        }

        foreach (var key in keysToRemove)
        {
            _clients.TryRemove(key, out _);
        }
    }
}

/// <summary>
/// Configuration for rate limiting
/// </summary>
public class RateLimitConfiguration
{
    /// <summary>
    /// Enable or disable rate limiting globally
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Global rate limit applied to all endpoints
    /// </summary>
    public RateLimit GlobalLimit { get; set; } = new RateLimit
    {
        MaxRequests = 1000,
        TimeWindow = TimeSpan.FromMinutes(1)
    };

    /// <summary>
    /// Endpoint-specific rate limits
    /// Key is the endpoint path prefix (e.g., "/api/v1/deployments")
    /// </summary>
    public Dictionary<string, RateLimit> EndpointLimits { get; set; } = new()
    {
        ["/api/v1/deployments"] = new RateLimit
        {
            MaxRequests = 10,
            TimeWindow = TimeSpan.FromMinutes(1)
        },
        ["/api/v1/clusters"] = new RateLimit
        {
            MaxRequests = 60,
            TimeWindow = TimeSpan.FromMinutes(1)
        }
    };
}

/// <summary>
/// Rate limit configuration for a specific endpoint or global
/// </summary>
public class RateLimit
{
    /// <summary>
    /// Maximum number of requests allowed
    /// </summary>
    public int MaxRequests { get; set; }

    /// <summary>
    /// Time window for the rate limit
    /// </summary>
    public TimeSpan TimeWindow { get; set; }
}

/// <summary>
/// Tracks rate limit information for a specific client
/// </summary>
internal class ClientRateLimitInfo
{
    private readonly object _lock = new();
    private readonly Queue<DateTimeOffset> _requests = new();
    private DateTimeOffset _lastActivity;

    public ClientRateLimitInfo()
    {
        _lastActivity = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Attempts to record a new request. Returns true if allowed, false if rate limit exceeded.
    /// </summary>
    public bool TryRecordRequest(RateLimit limit)
    {
        lock (_lock)
        {
            var now = DateTimeOffset.UtcNow;
            _lastActivity = now;

            // Remove requests outside the time window
            var windowStart = now.Subtract(limit.TimeWindow);
            while (_requests.Count > 0 && _requests.Peek() < windowStart)
            {
                _requests.Dequeue();
            }

            // Check if limit exceeded
            if (_requests.Count >= limit.MaxRequests)
            {
                return false;
            }

            // Record new request
            _requests.Enqueue(now);
            return true;
        }
    }

    /// <summary>
    /// Gets the number of requests in the current window
    /// </summary>
    public int GetRequestCount(TimeSpan window)
    {
        lock (_lock)
        {
            var now = DateTimeOffset.UtcNow;
            var windowStart = now.Subtract(window);

            // Remove expired requests
            while (_requests.Count > 0 && _requests.Peek() < windowStart)
            {
                _requests.Dequeue();
            }

            return _requests.Count;
        }
    }

    /// <summary>
    /// Gets the time when the rate limit will reset
    /// </summary>
    public DateTimeOffset GetResetTime(TimeSpan window)
    {
        lock (_lock)
        {
            if (_requests.Count == 0)
            {
                return DateTimeOffset.UtcNow.Add(window);
            }

            return _requests.Peek().Add(window);
        }
    }

    /// <summary>
    /// Checks if this client info is expired and can be cleaned up
    /// </summary>
    public bool IsExpired(DateTimeOffset now)
    {
        lock (_lock)
        {
            // Consider expired if no activity for 1 hour
            return now.Subtract(_lastActivity) > TimeSpan.FromHours(1);
        }
    }
}
