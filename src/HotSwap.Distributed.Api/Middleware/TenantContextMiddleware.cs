using HotSwap.Distributed.Infrastructure.Interfaces;

namespace HotSwap.Distributed.Api.Middleware;

/// <summary>
/// Middleware to extract and validate tenant context for multi-tenant requests.
/// </summary>
public class TenantContextMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantContextMiddleware> _logger;

    public TenantContextMiddleware(
        RequestDelegate next,
        ILogger<TenantContextMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InvokeAsync(HttpContext context, ITenantContextService tenantContextService)
    {
        // Skip tenant resolution for health checks and admin endpoints
        if (ShouldSkipTenantResolution(context.Request.Path))
        {
            await _next(context);
            return;
        }

        // Attempt to resolve tenant from request
        var tenant = await tenantContextService.GetCurrentTenantAsync(context.RequestAborted);

        if (tenant == null)
        {
            _logger.LogWarning("Tenant context required but not found for request: {Path}", context.Request.Path);
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "Tenant context required",
                message = "Please specify tenant via subdomain, X-Tenant-ID header, or JWT claim"
            });
            return;
        }

        // Validate tenant is active
        if (!tenant.IsActive())
        {
            _logger.LogWarning("Tenant {TenantId} is not active (Status: {Status})",
                tenant.TenantId, tenant.Status);
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "Tenant not available",
                message = $"Tenant is {tenant.Status.ToString().ToLower()}",
                tenantId = tenant.TenantId
            });
            return;
        }

        // Set tenant context for downstream services
        context.Items["TenantId"] = tenant.TenantId;
        context.Items["Tenant"] = tenant;

        _logger.LogDebug("Tenant context resolved: {TenantId} ({TenantName})",
            tenant.TenantId, tenant.Name);

        await _next(context);
    }

    private static bool ShouldSkipTenantResolution(PathString path)
    {
        // Skip tenant resolution for these paths
        var skipPaths = new[]
        {
            "/health",
            "/swagger",
            "/api/v1/admin",
            "/api/v1/auth"
        };

        return skipPaths.Any(p => path.StartsWithSegments(p, StringComparison.OrdinalIgnoreCase));
    }
}
