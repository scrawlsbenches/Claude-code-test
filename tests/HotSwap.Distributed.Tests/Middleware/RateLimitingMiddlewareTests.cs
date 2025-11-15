using System.Net;
using System.Security.Claims;
using FluentAssertions;
using HotSwap.Distributed.Api.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace HotSwap.Distributed.Tests.Middleware;

public class RateLimitingMiddlewareTests
{
    private readonly Mock<ILogger<RateLimitingMiddleware>> _loggerMock;
    private readonly Mock<RequestDelegate> _nextMock;
    private DefaultHttpContext _httpContext;
    private static int _testCounter = 0;

    public RateLimitingMiddlewareTests()
    {
        _loggerMock = new Mock<ILogger<RateLimitingMiddleware>>();
        _nextMock = new Mock<RequestDelegate>();
        _httpContext = new DefaultHttpContext();
        // Use unique IP for each test to avoid shared rate limit state
        var uniqueIp = Interlocked.Increment(ref _testCounter);
        _httpContext.Connection.RemoteIpAddress = IPAddress.Parse($"192.168.{uniqueIp / 256}.{uniqueIp % 256}");
    }

    private static IPAddress GetUniqueTestIp()
    {
        var uniqueIp = Interlocked.Increment(ref _testCounter);
        return IPAddress.Parse($"192.168.{uniqueIp / 256}.{uniqueIp % 256}");
    }

    [Fact]
    public async Task InvokeAsync_WhenHealthCheckEndpoint_ShouldSkipRateLimiting()
    {
        // Arrange
        var config = new RateLimitConfiguration { Enabled = true };
        var middleware = new RateLimitingMiddleware(_nextMock.Object, _loggerMock.Object, config);
        _httpContext.Request.Path = "/health";

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _nextMock.Verify(next => next(_httpContext), Times.Once);
        _httpContext.Response.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task InvokeAsync_WhenRateLimitingDisabled_ShouldSkipRateLimiting()
    {
        // Arrange
        var config = new RateLimitConfiguration { Enabled = false };
        var middleware = new RateLimitingMiddleware(_nextMock.Object, _loggerMock.Object, config);
        _httpContext.Request.Path = "/api/v1/deployments";

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _nextMock.Verify(next => next(_httpContext), Times.Once);
        _httpContext.Response.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task InvokeAsync_WhenWithinRateLimit_ShouldAllowRequest()
    {
        // Arrange
        var config = new RateLimitConfiguration
        {
            Enabled = true,
            GlobalLimit = new RateLimit
            {
                MaxRequests = 10,
                TimeWindow = TimeSpan.FromMinutes(1)
            }
        };
        var middleware = new RateLimitingMiddleware(_nextMock.Object, _loggerMock.Object, config);
        _httpContext.Request.Path = "/api/v1/test";

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _nextMock.Verify(next => next(_httpContext), Times.Once);
        _httpContext.Response.StatusCode.Should().Be(200);
        _httpContext.Response.Headers["X-RateLimit-Limit"].ToString().Should().Be("10");
        _httpContext.Response.Headers["X-RateLimit-Remaining"].ToString().Should().Be("9");
    }

    [Fact]
    public async Task InvokeAsync_WhenExceedingRateLimit_ShouldReturn429()
    {
        // Arrange
        var testIp = GetUniqueTestIp(); // Use unique IP for this test
        var config = new RateLimitConfiguration
        {
            Enabled = true,
            GlobalLimit = new RateLimit
            {
                MaxRequests = 2,
                TimeWindow = TimeSpan.FromSeconds(10)
            }
        };
        var middleware = new RateLimitingMiddleware(_nextMock.Object, _loggerMock.Object, config);
        _httpContext.Connection.RemoteIpAddress = testIp; // Override with test-specific IP
        _httpContext.Request.Path = "/api/v1/test";
        _httpContext.Response.Body = new MemoryStream();

        // Act - Make 3 requests (2 should succeed, 1 should fail)
        await middleware.InvokeAsync(_httpContext);
        _httpContext.Response.StatusCode.Should().Be(200);

        // Reset context for second request
        _httpContext = new DefaultHttpContext
        {
            Connection = { RemoteIpAddress = testIp },
            Request = { Path = "/api/v1/test" },
            Response = { Body = new MemoryStream() }
        };
        await middleware.InvokeAsync(_httpContext);
        _httpContext.Response.StatusCode.Should().Be(200);

        // Reset context for third request (should be rate limited)
        _httpContext = new DefaultHttpContext
        {
            Connection = { RemoteIpAddress = testIp },
            Request = { Path = "/api/v1/test" },
            Response = { Body = new MemoryStream() }
        };
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _httpContext.Response.StatusCode.Should().Be((int)HttpStatusCode.TooManyRequests);
        _httpContext.Response.Headers["X-RateLimit-Limit"].ToString().Should().Be("2");
        _httpContext.Response.Headers["X-RateLimit-Remaining"].ToString().Should().Be("0");
        _httpContext.Response.Headers.Should().ContainKey("Retry-After");
    }

    [Fact]
    public async Task InvokeAsync_WithEndpointSpecificLimit_ShouldUseEndpointLimit()
    {
        // Arrange
        var config = new RateLimitConfiguration
        {
            Enabled = true,
            GlobalLimit = new RateLimit
            {
                MaxRequests = 1000,
                TimeWindow = TimeSpan.FromMinutes(1)
            },
            EndpointLimits = new Dictionary<string, RateLimit>
            {
                ["/api/v1/deployments"] = new RateLimit
                {
                    MaxRequests = 10,
                    TimeWindow = TimeSpan.FromMinutes(1)
                }
            }
        };
        var middleware = new RateLimitingMiddleware(_nextMock.Object, _loggerMock.Object, config);
        _httpContext.Request.Path = "/api/v1/deployments";

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _nextMock.Verify(next => next(_httpContext), Times.Once);
        _httpContext.Response.StatusCode.Should().Be(200);
        _httpContext.Response.Headers["X-RateLimit-Limit"].ToString().Should().Be("10");
        _httpContext.Response.Headers["X-RateLimit-Remaining"].ToString().Should().Be("9");
    }

    [Fact]
    public async Task InvokeAsync_WithAuthenticatedUser_ShouldUseTokenBasedLimiting()
    {
        // Arrange
        var config = new RateLimitConfiguration
        {
            Enabled = true,
            GlobalLimit = new RateLimit
            {
                MaxRequests = 5,
                TimeWindow = TimeSpan.FromMinutes(1)
            }
        };
        var middleware = new RateLimitingMiddleware(_nextMock.Object, _loggerMock.Object, config);

        // Create authenticated user
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, "testuser"),
            new Claim("sub", "user-123")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        _httpContext.User = new ClaimsPrincipal(identity);
        _httpContext.Request.Path = "/api/v1/test";

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _nextMock.Verify(next => next(_httpContext), Times.Once);
        _httpContext.Response.StatusCode.Should().Be(200);
        _httpContext.Response.Headers["X-RateLimit-Remaining"].ToString().Should().Be("4");
    }

    [Fact]
    public async Task InvokeAsync_WithDifferentAuthenticatedUsers_ShouldHaveSeparateRateLimits()
    {
        // Arrange
        var testIp = GetUniqueTestIp(); // Use unique IP for this test
        var config = new RateLimitConfiguration
        {
            Enabled = true,
            GlobalLimit = new RateLimit
            {
                MaxRequests = 2,
                TimeWindow = TimeSpan.FromMinutes(1)
            }
        };
        var middleware = new RateLimitingMiddleware(_nextMock.Object, _loggerMock.Object, config);

        // First user makes 2 requests
        var claims1 = new[] { new Claim(ClaimTypes.Name, "user1") };
        var identity1 = new ClaimsIdentity(claims1, "TestAuth");
        var context1 = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity1),
            Request = { Path = "/api/v1/test" },
            Connection = { RemoteIpAddress = testIp }
        };

        await middleware.InvokeAsync(context1);
        context1.Response.StatusCode.Should().Be(200);

        context1 = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity1),
            Request = { Path = "/api/v1/test" },
            Connection = { RemoteIpAddress = testIp }
        };
        await middleware.InvokeAsync(context1);
        context1.Response.StatusCode.Should().Be(200);

        // Second user (different from first) should still be able to make requests
        var claims2 = new[] { new Claim(ClaimTypes.Name, "user2") };
        var identity2 = new ClaimsIdentity(claims2, "TestAuth");
        var context2 = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity2),
            Request = { Path = "/api/v1/test" },
            Connection = { RemoteIpAddress = testIp },
            Response = { Body = new MemoryStream() }
        };

        // Act
        await middleware.InvokeAsync(context2);

        // Assert
        context2.Response.StatusCode.Should().Be(200);
        context2.Response.Headers["X-RateLimit-Remaining"].ToString().Should().Be("1");
    }

    [Fact]
    public async Task InvokeAsync_WithXForwardedForHeader_ShouldUseForwardedIp()
    {
        // Arrange
        var config = new RateLimitConfiguration
        {
            Enabled = true,
            GlobalLimit = new RateLimit
            {
                MaxRequests = 5,
                TimeWindow = TimeSpan.FromMinutes(1)
            }
        };
        var middleware = new RateLimitingMiddleware(_nextMock.Object, _loggerMock.Object, config);
        _httpContext.Request.Path = "/api/v1/test";
        _httpContext.Request.Headers["X-Forwarded-For"] = "192.168.1.100, 10.0.0.1";

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _nextMock.Verify(next => next(_httpContext), Times.Once);
        _httpContext.Response.StatusCode.Should().Be(200);
        _httpContext.Response.Headers["X-RateLimit-Remaining"].ToString().Should().Be("4");
    }

    [Fact]
    public async Task InvokeAsync_WhenRateLimitExceeded_ShouldLogWarning()
    {
        // Arrange
        var testIp = GetUniqueTestIp(); // Use unique IP for this test
        var config = new RateLimitConfiguration
        {
            Enabled = true,
            GlobalLimit = new RateLimit
            {
                MaxRequests = 1,
                TimeWindow = TimeSpan.FromSeconds(10)
            }
        };
        var middleware = new RateLimitingMiddleware(_nextMock.Object, _loggerMock.Object, config);
        _httpContext.Connection.RemoteIpAddress = testIp; // Override with test-specific IP
        _httpContext.Request.Path = "/api/v1/test";
        _httpContext.Response.Body = new MemoryStream();

        // Make first request to consume the limit
        await middleware.InvokeAsync(_httpContext);

        // Reset context for second request
        _httpContext = new DefaultHttpContext
        {
            Connection = { RemoteIpAddress = testIp },
            Request = { Path = "/api/v1/test" },
            Response = { Body = new MemoryStream() }
        };

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _httpContext.Response.StatusCode.Should().Be((int)HttpStatusCode.TooManyRequests);
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Rate limit exceeded")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_WithMetricsEndpoint_ShouldUse60RequestsPerMinute()
    {
        // Arrange
        var config = new RateLimitConfiguration
        {
            Enabled = true,
            GlobalLimit = new RateLimit
            {
                MaxRequests = 1000,
                TimeWindow = TimeSpan.FromMinutes(1)
            },
            EndpointLimits = new Dictionary<string, RateLimit>
            {
                ["/api/v1/clusters"] = new RateLimit
                {
                    MaxRequests = 60,
                    TimeWindow = TimeSpan.FromMinutes(1)
                }
            }
        };
        var middleware = new RateLimitingMiddleware(_nextMock.Object, _loggerMock.Object, config);
        _httpContext.Request.Path = "/api/v1/clusters/production/metrics";

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _nextMock.Verify(next => next(_httpContext), Times.Once);
        _httpContext.Response.StatusCode.Should().Be(200);
        _httpContext.Response.Headers["X-RateLimit-Limit"].ToString().Should().Be("60");
        _httpContext.Response.Headers["X-RateLimit-Remaining"].ToString().Should().Be("59");
    }

    [Fact]
    public async Task InvokeAsync_WithDeploymentsEndpoint_ShouldUse10RequestsPerMinute()
    {
        // Arrange
        var config = new RateLimitConfiguration
        {
            Enabled = true,
            GlobalLimit = new RateLimit
            {
                MaxRequests = 1000,
                TimeWindow = TimeSpan.FromMinutes(1)
            },
            EndpointLimits = new Dictionary<string, RateLimit>
            {
                ["/api/v1/deployments"] = new RateLimit
                {
                    MaxRequests = 10,
                    TimeWindow = TimeSpan.FromMinutes(1)
                }
            }
        };
        var middleware = new RateLimitingMiddleware(_nextMock.Object, _loggerMock.Object, config);
        _httpContext.Request.Path = "/api/v1/deployments";

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _nextMock.Verify(next => next(_httpContext), Times.Once);
        _httpContext.Response.StatusCode.Should().Be(200);
        _httpContext.Response.Headers["X-RateLimit-Limit"].ToString().Should().Be("10");
        _httpContext.Response.Headers["X-RateLimit-Remaining"].ToString().Should().Be("9");
    }
}
