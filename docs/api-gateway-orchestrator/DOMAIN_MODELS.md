# Domain Models Reference

**Version:** 1.0.0
**Namespace:** `HotSwap.Gateway.Domain.Models`

---

## Table of Contents

1. [GatewayRoute](#gatewayroute)
2. [Backend](#backend)
3. [HealthCheck](#healthcheck)
4. [RouteDeployment](#routedeployment)
5. [TrafficSplit](#trafficsplit)
6. [Enumerations](#enumerations)
7. [Value Objects](#value-objects)

---

## GatewayRoute

Represents a gateway routing rule.

**File:** `src/HotSwap.Gateway.Domain/Models/GatewayRoute.cs`

```csharp
namespace HotSwap.Gateway.Domain.Models;

/// <summary>
/// Represents a gateway routing rule.
/// </summary>
public class GatewayRoute
{
    /// <summary>
    /// Unique route identifier.
    /// </summary>
    public required string RouteId { get; set; }

    /// <summary>
    /// Route name (human-readable).
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Path pattern (supports wildcards: /path/*, /path/**, /path/{id}).
    /// </summary>
    public required string PathPattern { get; set; }

    /// <summary>
    /// HTTP methods allowed (GET, POST, PUT, DELETE, PATCH, *).
    /// </summary>
    public List<string> Methods { get; set; } = new() { "*" };

    /// <summary>
    /// Backend services for this route.
    /// </summary>
    public List<Backend> Backends { get; set; } = new();

    /// <summary>
    /// Routing strategy.
    /// </summary>
    public RoutingStrategy Strategy { get; set} = RoutingStrategy.RoundRobin;

    /// <summary>
    /// Strategy-specific configuration (JSON).
    /// </summary>
    public Dictionary<string, string> StrategyConfig { get; set; } = new();

    /// <summary>
    /// Request timeout in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Whether route is enabled.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Route priority (higher = evaluated first).
    /// </summary>
    public int Priority { get; set; } = 0;

    /// <summary>
    /// Request/response transformations.
    /// </summary>
    public TransformationConfig? Transformations { get; set; }

    /// <summary>
    /// Rate limiting configuration.
    /// </summary>
    public RateLimitConfig? RateLimit { get; set; }

    /// <summary>
    /// Circuit breaker configuration.
    /// </summary>
    public CircuitBreakerConfig? CircuitBreaker { get; set; }

    /// <summary>
    /// Retry policy configuration.
    /// </summary>
    public RetryPolicyConfig? RetryPolicy { get; set; }

    /// <summary>
    /// Route creation timestamp (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last update timestamp (UTC).
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Configuration version.
    /// </summary>
    public string Version { get; set; } = "1.0";

    /// <summary>
    /// Validates the route configuration.
    /// </summary>
    public bool IsValid(out List<string> errors)
    {
        errors = new List<string>();

        if (string.IsNullOrWhiteSpace(RouteId))
            errors.Add("RouteId is required");

        if (string.IsNullOrWhiteSpace(Name))
            errors.Add("Name is required");

        if (string.IsNullOrWhiteSpace(PathPattern))
            errors.Add("PathPattern is required");

        if (!IsValidPathPattern(PathPattern))
            errors.Add($"Invalid path pattern: {PathPattern}");

        if (Backends.Count == 0)
            errors.Add("At least one backend is required");

        if (TimeoutSeconds < 1 || TimeoutSeconds > 300)
            errors.Add("TimeoutSeconds must be between 1 and 300");

        if (Priority < 0 || Priority > 100)
            errors.Add("Priority must be between 0 and 100");

        foreach (var backend in Backends)
        {
            if (!backend.IsValid(out var backendErrors))
                errors.AddRange(backendErrors);
        }

        return errors.Count == 0;
    }

    private bool IsValidPathPattern(string pattern)
    {
        // Basic validation: starts with /, contains valid characters
        if (!pattern.StartsWith("/"))
            return false;

        // Check for valid wildcards: * (single segment), ** (multiple segments), {param}
        return System.Text.RegularExpressions.Regex.IsMatch(
            pattern,
            @"^/([a-zA-Z0-9_\-\{\}\*]+/?)*$"
        );
    }

    /// <summary>
    /// Matches request path against this route's pattern.
    /// </summary>
    public bool MatchesPath(string requestPath)
    {
        // Convert pattern to regex
        var regex = PathPatternToRegex(PathPattern);
        return regex.IsMatch(requestPath);
    }

    private System.Text.RegularExpressions.Regex PathPatternToRegex(string pattern)
    {
        // /api/users/* → ^/api/users/[^/]+$
        // /api/users/** → ^/api/users/.*$
        // /api/users/{id} → ^/api/users/(?<id>[^/]+)$

        var regexPattern = pattern
            .Replace("**", "___DOUBLESTAR___")
            .Replace("*", "[^/]+")
            .Replace("___DOUBLESTAR___", ".*")
            .Replace("{", "(?<")
            .Replace("}", ">[^/]+)");

        return new System.Text.RegularExpressions.Regex($"^{regexPattern}$");
    }
}
```

---

## Backend

Represents a backend service.

**File:** `src/HotSwap.Gateway.Domain/Models/Backend.cs`

```csharp
namespace HotSwap.Gateway.Domain.Models;

/// <summary>
/// Represents a backend service.
/// </summary>
public class Backend
{
    /// <summary>
    /// Unique backend identifier.
    /// </summary>
    public required string BackendId { get; set; }

    /// <summary>
    /// Backend name.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Backend URL (http://host:port or https://host:port).
    /// </summary>
    public required string Url { get; set; }

    /// <summary>
    /// Backend weight (for weighted routing, 0-100).
    /// </summary>
    public int Weight { get; set; } = 100;

    /// <summary>
    /// Whether backend is enabled.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Current health status.
    /// </summary>
    public HealthStatus HealthStatus { get; set; } = HealthStatus.Unknown;

    /// <summary>
    /// Health check configuration.
    /// </summary>
    public HealthCheck? HealthCheck { get; set; }

    /// <summary>
    /// Connection pool settings.
    /// </summary>
    public ConnectionPoolConfig ConnectionPool { get; set; } = new();

    /// <summary>
    /// Timeout settings.
    /// </summary>
    public TimeoutConfig Timeouts { get; set; } = new();

    /// <summary>
    /// Last health check timestamp (UTC).
    /// </summary>
    public DateTime? LastHealthCheck { get; set; }

    /// <summary>
    /// Active connections count.
    /// </summary>
    public int ActiveConnections { get; set; } = 0;

    /// <summary>
    /// Total requests sent to this backend.
    /// </summary>
    public long TotalRequests { get; set; } = 0;

    /// <summary>
    /// Failed requests count.
    /// </summary>
    public long FailedRequests { get; set; } = 0;

    /// <summary>
    /// Backend creation timestamp (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Validates the backend configuration.
    /// </summary>
    public bool IsValid(out List<string> errors)
    {
        errors = new List<string>();

        if (string.IsNullOrWhiteSpace(BackendId))
            errors.Add("BackendId is required");

        if (string.IsNullOrWhiteSpace(Name))
            errors.Add("Name is required");

        if (string.IsNullOrWhiteSpace(Url))
            errors.Add("Url is required");
        else if (!Uri.IsWellFormedUriString(Url, UriKind.Absolute))
            errors.Add($"Invalid URL: {Url}");

        if (Weight < 0 || Weight > 100)
            errors.Add("Weight must be between 0 and 100");

        return errors.Count == 0;
    }

    /// <summary>
    /// Checks if backend is healthy and enabled.
    /// </summary>
    public bool IsAvailable() => IsEnabled && HealthStatus == HealthStatus.Healthy;
}

/// <summary>
/// Connection pool configuration.
/// </summary>
public class ConnectionPoolConfig
{
    public int MinSize { get; set; } = 10;
    public int MaxSize { get; set; } = 100;
    public TimeSpan MaxIdleTime { get; set; } = TimeSpan.FromMinutes(5);
    public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(5);
}

/// <summary>
/// Timeout configuration.
/// </summary>
public class TimeoutConfig
{
    public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(5);
    public TimeSpan ReadTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan WriteTimeout { get; set; } = TimeSpan.FromSeconds(30);
}
```

---

## HealthCheck

Represents a health check configuration.

**File:** `src/HotSwap.Gateway.Domain/Models/HealthCheck.cs`

```csharp
namespace HotSwap.Gateway.Domain.Models;

/// <summary>
/// Represents a health check configuration.
/// </summary>
public class HealthCheck
{
    /// <summary>
    /// Health check type.
    /// </summary>
    public HealthCheckType Type { get; set; } = HealthCheckType.Http;

    /// <summary>
    /// Health check endpoint (for HTTP checks).
    /// </summary>
    public string Endpoint { get; set; } = "/health";

    /// <summary>
    /// Health check interval.
    /// </summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Health check timeout.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Consecutive failures before marking unhealthy.
    /// </summary>
    public int UnhealthyThreshold { get; set; } = 3;

    /// <summary>
    /// Consecutive successes before marking healthy.
    /// </summary>
    public int HealthyThreshold { get; set; } = 2;

    /// <summary>
    /// Expected HTTP status code (for HTTP checks).
    /// </summary>
    public int ExpectedStatusCode { get; set; } = 200;

    /// <summary>
    /// Expected response body substring (optional).
    /// </summary>
    public string? ExpectedResponseBody { get; set; }

    /// <summary>
    /// Custom headers for health check requests.
    /// </summary>
    public Dictionary<string, string> Headers { get; set; } = new();
}
```

---

## RouteDeployment

Represents a route configuration deployment.

**File:** `src/HotSwap.Gateway.Domain/Models/RouteDeployment.cs`

```csharp
namespace HotSwap.Gateway.Domain.Models;

/// <summary>
/// Represents a route configuration deployment.
/// </summary>
public class RouteDeployment
{
    /// <summary>
    /// Unique deployment identifier.
    /// </summary>
    public required string DeploymentId { get; set; }

    /// <summary>
    /// Route being deployed.
    /// </summary>
    public required string RouteId { get; set; }

    /// <summary>
    /// Configuration version being deployed.
    /// </summary>
    public required string ConfigVersion { get; set; }

    /// <summary>
    /// Deployment strategy.
    /// </summary>
    public DeploymentStrategy Strategy { get; set; } = DeploymentStrategy.Rolling;

    /// <summary>
    /// Target environment.
    /// </summary>
    public string Environment { get; set; } = "Production";

    /// <summary>
    /// Current deployment status.
    /// </summary>
    public DeploymentStatus Status { get; set; } = DeploymentStatus.Pending;

    /// <summary>
    /// Traffic split configuration (for canary/blue-green).
    /// </summary>
    public TrafficSplit? TrafficSplit { get; set; }

    /// <summary>
    /// Deployment phases (for multi-phase deployments).
    /// </summary>
    public List<DeploymentPhase> Phases { get; set; } = new();

    /// <summary>
    /// Current phase index.
    /// </summary>
    public int CurrentPhaseIndex { get; set; } = 0;

    /// <summary>
    /// Deployment start timestamp (UTC).
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// Deployment completion timestamp (UTC).
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Deployment metrics.
    /// </summary>
    public DeploymentMetrics Metrics { get; set; } = new();

    /// <summary>
    /// Deployment created timestamp (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// User who initiated deployment.
    /// </summary>
    public string? CreatedBy { get; set; }

    /// <summary>
    /// Approval status (for production deployments).
    /// </summary>
    public ApprovalStatus ApprovalStatus { get; set; } = ApprovalStatus.Pending;

    /// <summary>
    /// Approver user ID.
    /// </summary>
    public string? ApprovedBy { get; set; }

    /// <summary>
    /// Approval timestamp (UTC).
    /// </summary>
    public DateTime? ApprovedAt { get; set; }

    /// <summary>
    /// Rollback timestamp (UTC), if rolled back.
    /// </summary>
    public DateTime? RolledBackAt { get; set; }

    /// <summary>
    /// Rollback reason.
    /// </summary>
    public string? RollbackReason { get; set; }
}

/// <summary>
/// Deployment phase.
/// </summary>
public class DeploymentPhase
{
    public string Name { get; set; } = "";
    public int TrafficPercentage { get; set; }
    public TimeSpan Duration { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public PhaseStatus Status { get; set; } = PhaseStatus.Pending;
}

/// <summary>
/// Deployment metrics.
/// </summary>
public class DeploymentMetrics
{
    public double BaselineErrorRate { get; set; }
    public double CurrentErrorRate { get; set; }
    public double BaselineP99Latency { get; set; }
    public double CurrentP99Latency { get; set; }
    public long TotalRequests { get; set; }
    public long FailedRequests { get; set; }
}
```

---

## TrafficSplit

Represents traffic split configuration.

**File:** `src/HotSwap.Gateway.Domain/Models/TrafficSplit.cs`

```csharp
namespace HotSwap.Gateway.Domain.Models;

/// <summary>
/// Represents traffic split configuration for canary/blue-green deployments.
/// </summary>
public class TrafficSplit
{
    /// <summary>
    /// Stable version traffic percentage (0-100).
    /// </summary>
    public int StablePercentage { get; set; } = 100;

    /// <summary>
    /// Canary version traffic percentage (0-100).
    /// </summary>
    public int CanaryPercentage { get; set; } = 0;

    /// <summary>
    /// Validates traffic split (must sum to 100).
    /// </summary>
    public bool IsValid(out List<string> errors)
    {
        errors = new List<string>();

        if (StablePercentage < 0 || StablePercentage > 100)
            errors.Add("StablePercentage must be between 0 and 100");

        if (CanaryPercentage < 0 || CanaryPercentage > 100)
            errors.Add("CanaryPercentage must be between 0 and 100");

        if (StablePercentage + CanaryPercentage != 100)
            errors.Add("StablePercentage + CanaryPercentage must equal 100");

        return errors.Count == 0;
    }
}
```

---

## Enumerations

### RoutingStrategy

```csharp
namespace HotSwap.Gateway.Domain.Enums;

/// <summary>
/// Backend selection strategy.
/// </summary>
public enum RoutingStrategy
{
    /// <summary>
    /// Round-robin distribution.
    /// </summary>
    RoundRobin,

    /// <summary>
    /// Weighted round-robin (based on backend weights).
    /// </summary>
    WeightedRoundRobin,

    /// <summary>
    /// Least connections (backend with fewest active connections).
    /// </summary>
    LeastConnections,

    /// <summary>
    /// IP hash (sticky sessions based on client IP).
    /// </summary>
    IPHash,

    /// <summary>
    /// Header-based routing (for A/B testing).
    /// </summary>
    HeaderBased,

    /// <summary>
    /// Direct (always first backend).
    /// </summary>
    Direct
}
```

### HealthStatus

```csharp
namespace HotSwap.Gateway.Domain.Enums;

/// <summary>
/// Backend health status.
/// </summary>
public enum HealthStatus
{
    /// <summary>
    /// Health unknown (not yet checked).
    /// </summary>
    Unknown,

    /// <summary>
    /// Backend is healthy.
    /// </summary>
    Healthy,

    /// <summary>
    /// Backend is unhealthy.
    /// </summary>
    Unhealthy,

    /// <summary>
    /// Backend is being drained (no new connections).
    /// </summary>
    Draining
}
```

### DeploymentStrategy

```csharp
namespace HotSwap.Gateway.Domain.Enums;

/// <summary>
/// Deployment strategy.
/// </summary>
public enum DeploymentStrategy
{
    /// <summary>
    /// Canary deployment (gradual traffic shift).
    /// </summary>
    Canary,

    /// <summary>
    /// Blue-green deployment (instant switch).
    /// </summary>
    BlueGreen,

    /// <summary>
    /// Rolling deployment (update nodes one by one).
    /// </summary>
    Rolling,

    /// <summary>
    /// Direct deployment (immediate update).
    /// </summary>
    Direct
}
```

### DeploymentStatus

```csharp
namespace HotSwap.Gateway.Domain.Enums;

/// <summary>
/// Deployment status.
/// </summary>
public enum DeploymentStatus
{
    /// <summary>
    /// Deployment created, awaiting approval.
    /// </summary>
    Pending,

    /// <summary>
    /// Deployment approved, ready to start.
    /// </summary>
    Approved,

    /// <summary>
    /// Deployment in progress.
    /// </summary>
    InProgress,

    /// <summary>
    /// Deployment completed successfully.
    /// </summary>
    Completed,

    /// <summary>
    /// Deployment failed.
    /// </summary>
    Failed,

    /// <summary>
    /// Deployment rolled back.
    /// </summary>
    RolledBack
}
```

### HealthCheckType

```csharp
namespace HotSwap.Gateway.Domain.Enums;

/// <summary>
/// Health check type.
/// </summary>
public enum HealthCheckType
{
    /// <summary>
    /// HTTP health check (GET request).
    /// </summary>
    Http,

    /// <summary>
    /// TCP connection test.
    /// </summary>
    Tcp,

    /// <summary>
    /// Custom health check script.
    /// </summary>
    Custom
}
```

---

## Configuration Objects

### TransformationConfig

```csharp
namespace HotSwap.Gateway.Domain.Models;

/// <summary>
/// Request/response transformation configuration.
/// </summary>
public class TransformationConfig
{
    public RequestTransformation? Request { get; set; }
    public ResponseTransformation? Response { get; set; }
}

public class RequestTransformation
{
    public Dictionary<string, string> AddHeaders { get; set; } = new();
    public List<string> RemoveHeaders { get; set; } = new();
    public string? RewritePath { get; set; }
}

public class ResponseTransformation
{
    public Dictionary<string, string> AddHeaders { get; set; } = new();
    public List<string> RemoveHeaders { get; set; } = new();
}
```

### RateLimitConfig

```csharp
namespace HotSwap.Gateway.Domain.Models;

/// <summary>
/// Rate limiting configuration.
/// </summary>
public class RateLimitConfig
{
    public RateLimitType Type { get; set; } = RateLimitType.SlidingWindow;
    public int Requests { get; set; } = 100;
    public TimeSpan Window { get; set; } = TimeSpan.FromMinutes(1);
    public RateLimitKeyType KeyBy { get; set; } = RateLimitKeyType.ClientIP;
}

public enum RateLimitType
{
    FixedWindow,
    SlidingWindow,
    TokenBucket
}

public enum RateLimitKeyType
{
    ClientIP,
    ApiKey,
    UserId
}
```

### CircuitBreakerConfig

```csharp
namespace HotSwap.Gateway.Domain.Models;

/// <summary>
/// Circuit breaker configuration.
/// </summary>
public class CircuitBreakerConfig
{
    public int FailureThreshold { get; set; } = 5;
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
    public int HalfOpenRequests { get; set; } = 3;
}
```

### RetryPolicyConfig

```csharp
namespace HotSwap.Gateway.Domain.Models;

/// <summary>
/// Retry policy configuration.
/// </summary>
public class RetryPolicyConfig
{
    public int MaxAttempts { get; set; } = 3;
    public TimeSpan InitialDelay { get; set; } = TimeSpan.FromMilliseconds(100);
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(2);
    public double BackoffMultiplier { get; set; } = 2.0;
    public List<int> RetryableStatusCodes { get; set; } = new() { 502, 503, 504 };
}
```

---

**Last Updated:** 2025-11-23
**Namespace:** `HotSwap.Gateway.Domain`
