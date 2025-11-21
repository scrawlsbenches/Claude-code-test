using FluentAssertions;
using HotSwap.Distributed.Api.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace HotSwap.Distributed.Tests.Middleware;

public class SecurityHeadersMiddlewareTests
{
    private readonly Mock<ILogger<SecurityHeadersMiddleware>> _loggerMock;
    private readonly Mock<RequestDelegate> _nextMock;
    private DefaultHttpContext _httpContext;

    public SecurityHeadersMiddlewareTests()
    {
        _loggerMock = new Mock<ILogger<SecurityHeadersMiddleware>>();
        _nextMock = new Mock<RequestDelegate>();
        _httpContext = new DefaultHttpContext();
    }

    [Fact]
    public async Task InvokeAsync_WithDefaultConfiguration_ShouldAddAllSecurityHeaders()
    {
        // Arrange
        var config = new SecurityHeadersConfiguration(); // Use defaults
        var middleware = new SecurityHeadersMiddleware(_nextMock.Object, _loggerMock.Object, config);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _nextMock.Verify(next => next(_httpContext), Times.Once);

        var headers = _httpContext.Response.Headers;
        headers["X-Content-Type-Options"].ToString().Should().Be("nosniff");
        headers["X-Frame-Options"].ToString().Should().Be("DENY");
        headers["X-XSS-Protection"].ToString().Should().Be("1; mode=block");
        headers["Strict-Transport-Security"].ToString().Should().Be("max-age=31536000; includeSubDomains; preload");
        headers["Content-Security-Policy"].ToString().Should().Contain("default-src 'self'");
        headers["Referrer-Policy"].ToString().Should().Be("strict-origin-when-cross-origin");
        headers["Permissions-Policy"].ToString().Should().Contain("geolocation=()");
        headers["X-Permitted-Cross-Domain-Policies"].ToString().Should().Be("none");
        headers["X-API-Version"].ToString().Should().Be("v1.0.0");
        headers.Should().NotContainKey("Server");
        headers.Should().NotContainKey("X-Powered-By");
    }

    [Fact]
    public async Task InvokeAsync_WithXContentTypeOptionsDisabled_ShouldNotAddHeader()
    {
        // Arrange
        var config = new SecurityHeadersConfiguration
        {
            EnableXContentTypeOptions = false
        };
        var middleware = new SecurityHeadersMiddleware(_nextMock.Object, _loggerMock.Object, config);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _httpContext.Response.Headers.Should().NotContainKey("X-Content-Type-Options");
        _nextMock.Verify(next => next(_httpContext), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_WithXFrameOptionsDisabled_ShouldNotAddHeader()
    {
        // Arrange
        var config = new SecurityHeadersConfiguration
        {
            EnableXFrameOptions = false
        };
        var middleware = new SecurityHeadersMiddleware(_nextMock.Object, _loggerMock.Object, config);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _httpContext.Response.Headers.Should().NotContainKey("X-Frame-Options");
    }

    [Fact]
    public async Task InvokeAsync_WithCustomXFrameOptionsValue_ShouldUseCustomValue()
    {
        // Arrange
        var config = new SecurityHeadersConfiguration
        {
            EnableXFrameOptions = true,
            XFrameOptionsValue = "SAMEORIGIN"
        };
        var middleware = new SecurityHeadersMiddleware(_nextMock.Object, _loggerMock.Object, config);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _httpContext.Response.Headers["X-Frame-Options"].ToString().Should().Be("SAMEORIGIN");
    }

    [Fact]
    public async Task InvokeAsync_WithXXSSProtectionDisabled_ShouldNotAddHeader()
    {
        // Arrange
        var config = new SecurityHeadersConfiguration
        {
            EnableXXSSProtection = false
        };
        var middleware = new SecurityHeadersMiddleware(_nextMock.Object, _loggerMock.Object, config);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _httpContext.Response.Headers.Should().NotContainKey("X-XSS-Protection");
    }

    [Fact]
    public async Task InvokeAsync_WithHSTSDisabled_ShouldNotAddHeader()
    {
        // Arrange
        var config = new SecurityHeadersConfiguration
        {
            EnableHSTS = false
        };
        var middleware = new SecurityHeadersMiddleware(_nextMock.Object, _loggerMock.Object, config);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _httpContext.Response.Headers.Should().NotContainKey("Strict-Transport-Security");
    }

    [Fact]
    public async Task InvokeAsync_WithCustomHSTSMaxAge_ShouldUseCustomValue()
    {
        // Arrange
        var config = new SecurityHeadersConfiguration
        {
            EnableHSTS = true,
            HSTSMaxAge = 15552000 // 6 months
        };
        var middleware = new SecurityHeadersMiddleware(_nextMock.Object, _loggerMock.Object, config);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _httpContext.Response.Headers["Strict-Transport-Security"].ToString()
            .Should().Be("max-age=15552000; includeSubDomains; preload");
    }

    [Fact]
    public async Task InvokeAsync_WithExistingHSTSHeader_ShouldNotOverwrite()
    {
        // Arrange
        var config = new SecurityHeadersConfiguration
        {
            EnableHSTS = true,
            HSTSMaxAge = 31536000
        };
        var middleware = new SecurityHeadersMiddleware(_nextMock.Object, _loggerMock.Object, config);

        // Add existing HSTS header
        _httpContext.Response.Headers["Strict-Transport-Security"] = "max-age=63072000; includeSubDomains";

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _httpContext.Response.Headers["Strict-Transport-Security"].ToString()
            .Should().Be("max-age=63072000; includeSubDomains"); // Should not be overwritten
    }

    [Fact]
    public async Task InvokeAsync_WithCSPDisabled_ShouldNotAddHeader()
    {
        // Arrange
        var config = new SecurityHeadersConfiguration
        {
            EnableCSP = false
        };
        var middleware = new SecurityHeadersMiddleware(_nextMock.Object, _loggerMock.Object, config);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _httpContext.Response.Headers.Should().NotContainKey("Content-Security-Policy");
    }

    [Fact]
    public async Task InvokeAsync_WithCustomCSP_ShouldUseCustomValue()
    {
        // Arrange
        var config = new SecurityHeadersConfiguration
        {
            EnableCSP = true,
            ContentSecurityPolicy = "default-src 'none'; script-src 'self'; connect-src 'self'"
        };
        var middleware = new SecurityHeadersMiddleware(_nextMock.Object, _loggerMock.Object, config);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _httpContext.Response.Headers["Content-Security-Policy"].ToString()
            .Should().Be("default-src 'none'; script-src 'self'; connect-src 'self'");
    }

    [Fact]
    public async Task InvokeAsync_WithEmptyCSP_ShouldNotAddHeader()
    {
        // Arrange
        var config = new SecurityHeadersConfiguration
        {
            EnableCSP = true,
            ContentSecurityPolicy = ""
        };
        var middleware = new SecurityHeadersMiddleware(_nextMock.Object, _loggerMock.Object, config);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _httpContext.Response.Headers.Should().NotContainKey("Content-Security-Policy");
    }

    [Fact]
    public async Task InvokeAsync_WithReferrerPolicyDisabled_ShouldNotAddHeader()
    {
        // Arrange
        var config = new SecurityHeadersConfiguration
        {
            EnableReferrerPolicy = false
        };
        var middleware = new SecurityHeadersMiddleware(_nextMock.Object, _loggerMock.Object, config);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _httpContext.Response.Headers.Should().NotContainKey("Referrer-Policy");
    }

    [Fact]
    public async Task InvokeAsync_WithCustomReferrerPolicy_ShouldUseCustomValue()
    {
        // Arrange
        var config = new SecurityHeadersConfiguration
        {
            EnableReferrerPolicy = true,
            ReferrerPolicyValue = "no-referrer"
        };
        var middleware = new SecurityHeadersMiddleware(_nextMock.Object, _loggerMock.Object, config);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _httpContext.Response.Headers["Referrer-Policy"].ToString().Should().Be("no-referrer");
    }

    [Fact]
    public async Task InvokeAsync_WithPermissionsPolicyDisabled_ShouldNotAddHeader()
    {
        // Arrange
        var config = new SecurityHeadersConfiguration
        {
            EnablePermissionsPolicy = false
        };
        var middleware = new SecurityHeadersMiddleware(_nextMock.Object, _loggerMock.Object, config);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _httpContext.Response.Headers.Should().NotContainKey("Permissions-Policy");
    }

    [Fact]
    public async Task InvokeAsync_WithCustomPermissionsPolicy_ShouldUseCustomValue()
    {
        // Arrange
        var config = new SecurityHeadersConfiguration
        {
            EnablePermissionsPolicy = true,
            PermissionsPolicy = "geolocation=(), camera=(self)"
        };
        var middleware = new SecurityHeadersMiddleware(_nextMock.Object, _loggerMock.Object, config);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _httpContext.Response.Headers["Permissions-Policy"].ToString()
            .Should().Be("geolocation=(), camera=(self)");
    }

    [Fact]
    public async Task InvokeAsync_WithEmptyPermissionsPolicy_ShouldNotAddHeader()
    {
        // Arrange
        var config = new SecurityHeadersConfiguration
        {
            EnablePermissionsPolicy = true,
            PermissionsPolicy = ""
        };
        var middleware = new SecurityHeadersMiddleware(_nextMock.Object, _loggerMock.Object, config);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _httpContext.Response.Headers.Should().NotContainKey("Permissions-Policy");
    }

    [Fact]
    public async Task InvokeAsync_WithXPermittedCrossDomainPoliciesDisabled_ShouldNotAddHeader()
    {
        // Arrange
        var config = new SecurityHeadersConfiguration
        {
            EnableXPermittedCrossDomainPolicies = false
        };
        var middleware = new SecurityHeadersMiddleware(_nextMock.Object, _loggerMock.Object, config);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _httpContext.Response.Headers.Should().NotContainKey("X-Permitted-Cross-Domain-Policies");
    }

    [Fact]
    public async Task InvokeAsync_WithRemoveServerHeaderEnabled_ShouldRemoveServerHeaders()
    {
        // Arrange
        var config = new SecurityHeadersConfiguration
        {
            RemoveServerHeader = true
        };
        var middleware = new SecurityHeadersMiddleware(_nextMock.Object, _loggerMock.Object, config);

        // Add server headers before middleware
        _httpContext.Response.Headers["Server"] = "Kestrel";
        _httpContext.Response.Headers["X-Powered-By"] = "ASP.NET Core";

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _httpContext.Response.Headers.Should().NotContainKey("Server");
        _httpContext.Response.Headers.Should().NotContainKey("X-Powered-By");
    }

    [Fact]
    public async Task InvokeAsync_WithRemoveServerHeaderDisabled_ShouldNotRemoveServerHeaders()
    {
        // Arrange
        var config = new SecurityHeadersConfiguration
        {
            RemoveServerHeader = false
        };
        var middleware = new SecurityHeadersMiddleware(_nextMock.Object, _loggerMock.Object, config);

        // Add server headers before middleware
        _httpContext.Response.Headers["Server"] = "Kestrel";
        _httpContext.Response.Headers["X-Powered-By"] = "ASP.NET Core";

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _httpContext.Response.Headers["Server"].ToString().Should().Be("Kestrel");
        _httpContext.Response.Headers["X-Powered-By"].ToString().Should().Be("ASP.NET Core");
    }

    [Fact]
    public async Task InvokeAsync_WithAddApiVersionHeaderEnabled_ShouldAddVersionHeader()
    {
        // Arrange
        var config = new SecurityHeadersConfiguration
        {
            AddApiVersionHeader = true
        };
        var middleware = new SecurityHeadersMiddleware(_nextMock.Object, _loggerMock.Object, config);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _httpContext.Response.Headers["X-API-Version"].ToString().Should().Be("v1.0.0");
    }

    [Fact]
    public async Task InvokeAsync_WithAddApiVersionHeaderDisabled_ShouldNotAddVersionHeader()
    {
        // Arrange
        var config = new SecurityHeadersConfiguration
        {
            AddApiVersionHeader = false
        };
        var middleware = new SecurityHeadersMiddleware(_nextMock.Object, _loggerMock.Object, config);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _httpContext.Response.Headers.Should().NotContainKey("X-API-Version");
    }

    [Fact]
    public async Task InvokeAsync_WithAllHeadersDisabled_ShouldOnlyCallNextMiddleware()
    {
        // Arrange
        var config = new SecurityHeadersConfiguration
        {
            EnableXContentTypeOptions = false,
            EnableXFrameOptions = false,
            EnableXXSSProtection = false,
            EnableHSTS = false,
            EnableCSP = false,
            EnableReferrerPolicy = false,
            EnablePermissionsPolicy = false,
            EnableXPermittedCrossDomainPolicies = false,
            RemoveServerHeader = false,
            AddApiVersionHeader = false
        };
        var middleware = new SecurityHeadersMiddleware(_nextMock.Object, _loggerMock.Object, config);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _nextMock.Verify(next => next(_httpContext), Times.Once);
        // No security headers should be added
        var headers = _httpContext.Response.Headers;
        headers.Should().NotContainKey("X-Content-Type-Options");
        headers.Should().NotContainKey("X-Frame-Options");
        headers.Should().NotContainKey("X-XSS-Protection");
        headers.Should().NotContainKey("Strict-Transport-Security");
        headers.Should().NotContainKey("Content-Security-Policy");
        headers.Should().NotContainKey("Referrer-Policy");
        headers.Should().NotContainKey("Permissions-Policy");
        headers.Should().NotContainKey("X-Permitted-Cross-Domain-Policies");
        headers.Should().NotContainKey("X-API-Version");
    }

    [Fact]
    public async Task InvokeAsync_ShouldAlwaysCallNextMiddleware()
    {
        // Arrange
        var config = new SecurityHeadersConfiguration();
        var middleware = new SecurityHeadersMiddleware(_nextMock.Object, _loggerMock.Object, config);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _nextMock.Verify(next => next(_httpContext), Times.Once);
    }
}
