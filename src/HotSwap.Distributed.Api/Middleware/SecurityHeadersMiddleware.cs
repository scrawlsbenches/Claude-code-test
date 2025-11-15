namespace HotSwap.Distributed.Api.Middleware;

/// <summary>
/// Middleware to add security headers to HTTP responses
/// </summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SecurityHeadersMiddleware> _logger;
    private readonly SecurityHeadersConfiguration _config;

    public SecurityHeadersMiddleware(
        RequestDelegate next,
        ILogger<SecurityHeadersMiddleware> logger,
        SecurityHeadersConfiguration config)
    {
        _next = next;
        _logger = logger;
        _config = config;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Add security headers before processing the request
        AddSecurityHeaders(context.Response);

        await _next(context);
    }

    private void AddSecurityHeaders(HttpResponse response)
    {
        // X-Content-Type-Options: Prevents MIME type sniffing
        if (_config.EnableXContentTypeOptions)
        {
            response.Headers["X-Content-Type-Options"] = "nosniff";
        }

        // X-Frame-Options: Prevents clickjacking
        if (_config.EnableXFrameOptions)
        {
            response.Headers["X-Frame-Options"] = _config.XFrameOptionsValue;
        }

        // X-XSS-Protection: Enables XSS filtering (legacy browsers)
        if (_config.EnableXXSSProtection)
        {
            response.Headers["X-XSS-Protection"] = "1; mode=block";
        }

        // Strict-Transport-Security (HSTS): Enforces HTTPS
        if (_config.EnableHSTS && !response.Headers.ContainsKey("Strict-Transport-Security"))
        {
            response.Headers["Strict-Transport-Security"] =
                $"max-age={_config.HSTSMaxAge}; includeSubDomains; preload";
        }

        // Content-Security-Policy: Defines valid sources of content
        if (_config.EnableCSP && !string.IsNullOrEmpty(_config.ContentSecurityPolicy))
        {
            response.Headers["Content-Security-Policy"] = _config.ContentSecurityPolicy;
        }

        // Referrer-Policy: Controls referrer information
        if (_config.EnableReferrerPolicy)
        {
            response.Headers["Referrer-Policy"] = _config.ReferrerPolicyValue;
        }

        // Permissions-Policy: Controls browser features
        if (_config.EnablePermissionsPolicy && !string.IsNullOrEmpty(_config.PermissionsPolicy))
        {
            response.Headers["Permissions-Policy"] = _config.PermissionsPolicy;
        }

        // X-Permitted-Cross-Domain-Policies: Controls Adobe cross-domain policies
        if (_config.EnableXPermittedCrossDomainPolicies)
        {
            response.Headers["X-Permitted-Cross-Domain-Policies"] = "none";
        }

        // Remove server header to avoid information disclosure
        if (_config.RemoveServerHeader)
        {
            response.Headers.Remove("Server");
            response.Headers.Remove("X-Powered-By");
        }

        // Add custom API version header
        if (_config.AddApiVersionHeader)
        {
            response.Headers["X-API-Version"] = "v1.0.0";
        }
    }
}

/// <summary>
/// Configuration for security headers
/// </summary>
public class SecurityHeadersConfiguration
{
    /// <summary>
    /// Enable X-Content-Type-Options header
    /// </summary>
    public bool EnableXContentTypeOptions { get; set; } = true;

    /// <summary>
    /// Enable X-Frame-Options header
    /// </summary>
    public bool EnableXFrameOptions { get; set; } = true;

    /// <summary>
    /// Value for X-Frame-Options header
    /// </summary>
    public string XFrameOptionsValue { get; set; } = "DENY";

    /// <summary>
    /// Enable X-XSS-Protection header
    /// </summary>
    public bool EnableXXSSProtection { get; set; } = true;

    /// <summary>
    /// Enable Strict-Transport-Security (HSTS) header
    /// </summary>
    public bool EnableHSTS { get; set; } = true;

    /// <summary>
    /// HSTS max-age in seconds (default: 1 year)
    /// </summary>
    public int HSTSMaxAge { get; set; } = 31536000;

    /// <summary>
    /// Enable Content-Security-Policy header
    /// </summary>
    public bool EnableCSP { get; set; } = true;

    /// <summary>
    /// Content-Security-Policy value
    /// </summary>
    public string ContentSecurityPolicy { get; set; } =
        "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; font-src 'self'; connect-src 'self'; frame-ancestors 'none'";

    /// <summary>
    /// Enable Referrer-Policy header
    /// </summary>
    public bool EnableReferrerPolicy { get; set; } = true;

    /// <summary>
    /// Referrer-Policy value
    /// </summary>
    public string ReferrerPolicyValue { get; set; } = "strict-origin-when-cross-origin";

    /// <summary>
    /// Enable Permissions-Policy header
    /// </summary>
    public bool EnablePermissionsPolicy { get; set; } = true;

    /// <summary>
    /// Permissions-Policy value
    /// </summary>
    public string PermissionsPolicy { get; set; } =
        "geolocation=(), microphone=(), camera=(), payment=(), usb=()";

    /// <summary>
    /// Enable X-Permitted-Cross-Domain-Policies header
    /// </summary>
    public bool EnableXPermittedCrossDomainPolicies { get; set; } = true;

    /// <summary>
    /// Remove Server and X-Powered-By headers
    /// </summary>
    public bool RemoveServerHeader { get; set; } = true;

    /// <summary>
    /// Add custom API version header
    /// </summary>
    public bool AddApiVersionHeader { get; set; } = true;
}
