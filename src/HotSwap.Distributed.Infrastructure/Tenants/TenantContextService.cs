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

        // Extract tenant ID from various sources (async for subdomain resolution)
        var tenantId = await ExtractTenantIdAsync(httpContext, cancellationToken);
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

        // Synchronous extraction - only from headers/claims, not subdomain (which requires async DB lookup)
        return ExtractTenantIdSync(httpContext);
    }

    /// <summary>
    /// INTERNAL USE ONLY: Sets the current tenant in the HTTP context cache.
    ///
    /// SECURITY WARNING: This method bypasses all authentication and authorization checks.
    /// It should ONLY be used in:
    /// 1. Unit/integration tests to set up test context
    /// 2. System-level operations with proper authorization validation
    ///
    /// DO NOT expose this through any public API endpoint or middleware.
    /// Use GetCurrentTenantAsync() which enforces JWT-based security validation.
    /// </summary>
    /// <param name="tenant">Tenant to set in the current HttpContext</param>
    /// <exception cref="ArgumentNullException">When tenant is null</exception>
    /// <exception cref="InvalidOperationException">When HttpContext is not available</exception>
    internal void SetCurrentTenantForTesting(Tenant tenant)
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

    /// <summary>
    /// Extracts tenant ID synchronously from trusted sources only (JWT claims).
    /// SECURITY: This method does NOT trust client-provided headers or subdomains for authenticated users.
    /// </summary>
    private Guid? ExtractTenantIdSync(HttpContext context)
    {
        // SECURITY PRINCIPLE: For authenticated users, JWT claim is the ONLY source of truth
        // This prevents horizontal privilege escalation where a user from Tenant A
        // could access Tenant B's data by providing X-Tenant-ID: <tenant-b-guid>

        var user = context.User;
        if (user.Identity?.IsAuthenticated == true)
        {
            // For authenticated users, ONLY trust the JWT claim
            var tenantClaim = user.FindFirst("tenant_id");
            if (tenantClaim != null && Guid.TryParse(tenantClaim.Value, out var claimTenantId))
            {
                _logger.LogDebug("Tenant resolved from authenticated JWT claim: {TenantId}", claimTenantId);
                return claimTenantId;
            }

            _logger.LogWarning("Authenticated user has no tenant_id claim in JWT");
            return null;
        }

        // For unauthenticated requests, subdomain resolution requires async DB lookup
        _logger.LogDebug("Unauthenticated request - subdomain resolution requires async");
        return null;
    }

    /// <summary>
    /// Extracts tenant ID asynchronously with proper security validation.
    /// SECURITY CRITICAL: Implements defense-in-depth against tenant impersonation attacks.
    ///
    /// Attack Scenarios Prevented:
    /// 1. Horizontal Privilege Escalation: User from Tenant A cannot access Tenant B by sending X-Tenant-ID header
    /// 2. Host Header Injection: Subdomain is only trusted for unauthenticated requests
    /// 3. JWT Bypass: Headers/subdomains cannot override authenticated user's tenant
    ///
    /// Security Hierarchy (in order of trust):
    /// - Authenticated: JWT claim is the ONLY source of truth (most secure)
    /// - Unauthenticated: Subdomain lookup (for public pages, acceptable risk)
    /// - Never: Client-provided headers for authenticated users (INSECURE)
    /// </summary>
    private async Task<Guid?> ExtractTenantIdAsync(HttpContext context, CancellationToken cancellationToken = default)
    {
        var user = context.User;
        var isAuthenticated = user.Identity?.IsAuthenticated == true;

        // PRIORITY 1: For authenticated users, JWT claim is the ONLY source of truth
        if (isAuthenticated)
        {
            var tenantClaim = user.FindFirst("tenant_id");
            if (tenantClaim != null && Guid.TryParse(tenantClaim.Value, out var jwtTenantId))
            {
                _logger.LogDebug("Tenant resolved from authenticated JWT claim: {TenantId}", jwtTenantId);
                return jwtTenantId;
            }

            // SECURITY: Authenticated user MUST have tenant_id in JWT
            // If missing, this is a configuration error - deny access
            _logger.LogWarning("SECURITY: Authenticated user {UserId} has no tenant_id claim in JWT - denying access",
                user.FindFirst("sub")?.Value ?? "unknown");
            return null;
        }

        // PRIORITY 2: For unauthenticated requests, allow subdomain resolution
        // This is safe for public pages (e.g., tenant1.platform.com/login)
        var host = context.Request.Host.Host;
        var parts = host.Split('.');
        if (parts.Length >= 3)
        {
            // Normalize subdomain to lowercase (DNS is case-insensitive)
            var subdomain = parts[0].ToLowerInvariant();

            // Validate subdomain format to prevent injection
            if (IsValidSubdomain(subdomain))
            {
                var tenant = await _tenantRepository.GetBySubdomainAsync(subdomain, cancellationToken);
                if (tenant != null)
                {
                    _logger.LogDebug("Tenant resolved from subdomain (unauthenticated): {Subdomain} -> {TenantId}",
                        subdomain, tenant.TenantId);
                    return tenant.TenantId;
                }
            }
            else
            {
                _logger.LogWarning("SECURITY: Invalid subdomain format rejected: {Subdomain}", subdomain);
            }
        }

        _logger.LogDebug("Could not extract tenant ID from request (no JWT claim, no valid subdomain)");
        return null;
    }

    /// <summary>
    /// Validates subdomain format to prevent injection attacks and reserved name conflicts.
    /// Subdomains must be lowercase alphanumeric with hyphens only, and cannot be reserved names.
    /// </summary>
    private static bool IsValidSubdomain(string subdomain)
    {
        if (string.IsNullOrWhiteSpace(subdomain) || subdomain.Length > 63)
            return false;

        // Reserved subdomains that cannot be used for tenant names
        // These are typically infrastructure/platform services
        var reservedSubdomains = new[]
        {
            "www", "api", "admin", "app", "cdn", "ftp", "mail", "smtp",
            "pop", "imap", "webmail", "ns", "ns1", "ns2", "dns", "status",
            "help", "support", "docs", "blog", "forum", "shop", "store",
            "assets", "static", "images", "img", "files", "downloads",
            "dev", "staging", "test", "qa", "demo", "sandbox"
        };

        if (reservedSubdomains.Contains(subdomain))
            return false;

        // RFC 1123: subdomain must be lowercase alphanumeric with hyphens
        // Cannot start or end with hyphen
        return subdomain.All(c => char.IsLower(c) || char.IsDigit(c) || c == '-') &&
               !subdomain.StartsWith('-') &&
               !subdomain.EndsWith('-');
    }
}
