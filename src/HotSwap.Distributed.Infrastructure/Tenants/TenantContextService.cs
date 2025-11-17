using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace HotSwap.Distributed.Infrastructure.Tenants;

/// <summary>
/// Service for resolving and managing tenant context from HTTP requests.
/// </summary>
public class TenantContextService : ITenantContextService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ITenantRepository _tenantRepository;
    private readonly ILogger<TenantContextService> _logger;

    public TenantContextService(
        IHttpContextAccessor httpContextAccessor,
        ITenantRepository tenantRepository,
        ILogger<TenantContextService> logger)
    {
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        _tenantRepository = tenantRepository ?? throw new ArgumentNullException(nameof(tenantRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Tenant?> GetCurrentTenantAsync(CancellationToken cancellationToken = default)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
            return null;

        // Check if already resolved and cached
        if (httpContext.Items.TryGetValue("Tenant", out var cachedTenant))
            return cachedTenant as Tenant;

        // Extract tenant ID from various sources
        var tenantId = ExtractTenantId(httpContext);
        if (tenantId == null)
            return null;

        // Load tenant from repository
        var tenant = await _tenantRepository.GetByIdAsync(tenantId.Value, cancellationToken);

        // Cache in HttpContext for this request
        if (tenant != null)
        {
            httpContext.Items["TenantId"] = tenantId;
            httpContext.Items["Tenant"] = tenant;
        }

        return tenant;
    }

    public Guid? GetCurrentTenantId()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
            return null;

        // Check if already resolved
        if (httpContext.Items.TryGetValue("TenantId", out var cachedTenantId))
            return cachedTenantId as Guid?;

        return ExtractTenantId(httpContext);
    }

    public void SetCurrentTenant(Tenant tenant)
    {
        if (tenant == null)
            throw new ArgumentNullException(nameof(tenant));

        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
            throw new InvalidOperationException("HttpContext is not available");

        httpContext.Items["TenantId"] = tenant.TenantId;
        httpContext.Items["Tenant"] = tenant;
    }

    public async Task<bool> ValidateCurrentTenantAsync(CancellationToken cancellationToken = default)
    {
        var tenant = await GetCurrentTenantAsync(cancellationToken);

        if (tenant == null)
        {
            _logger.LogWarning("Tenant validation failed: no tenant context");
            return false;
        }

        if (!tenant.IsActive())
        {
            _logger.LogWarning("Tenant validation failed: tenant {TenantId} is not active (Status: {Status})",
                tenant.TenantId, tenant.Status);
            return false;
        }

        return true;
    }

    private Guid? ExtractTenantId(HttpContext context)
    {
        // Option 1: From subdomain (e.g., tenant1.platform.com)
        var host = context.Request.Host.Host;
        var parts = host.Split('.');
        if (parts.Length >= 3)
        {
            var subdomain = parts[0];
            // Async call in sync method - consider refactoring if needed
            var tenant = _tenantRepository.GetBySubdomainAsync(subdomain).Result;
            if (tenant != null)
            {
                _logger.LogDebug("Tenant resolved from subdomain: {Subdomain} -> {TenantId}",
                    subdomain, tenant.TenantId);
                return tenant.TenantId;
            }
        }

        // Option 2: From X-Tenant-ID header (for API access)
        if (context.Request.Headers.TryGetValue("X-Tenant-ID", out var headerValue))
        {
            if (Guid.TryParse(headerValue, out var tenantId))
            {
                _logger.LogDebug("Tenant resolved from header: {TenantId}", tenantId);
                return tenantId;
            }
        }

        // Option 3: From JWT claim
        var user = context.User;
        var tenantClaim = user.FindFirst("tenant_id");
        if (tenantClaim != null && Guid.TryParse(tenantClaim.Value, out var claimTenantId))
        {
            _logger.LogDebug("Tenant resolved from JWT claim: {TenantId}", claimTenantId);
            return claimTenantId;
        }

        _logger.LogWarning("Could not extract tenant ID from request");
        return null;
    }
}
