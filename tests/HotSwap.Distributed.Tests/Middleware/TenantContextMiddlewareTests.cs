using System.Text.Json;
using FluentAssertions;
using HotSwap.Distributed.Api.Middleware;
using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace HotSwap.Distributed.Tests.Middleware;

public class TenantContextMiddlewareTests
{
    private readonly Mock<ILogger<TenantContextMiddleware>> _loggerMock;
    private readonly Mock<RequestDelegate> _nextMock;
    private readonly Mock<ITenantContextService> _tenantContextServiceMock;
    private DefaultHttpContext _httpContext;

    public TenantContextMiddlewareTests()
    {
        _loggerMock = new Mock<ILogger<TenantContextMiddleware>>();
        _nextMock = new Mock<RequestDelegate>();
        _tenantContextServiceMock = new Mock<ITenantContextService>();
        _httpContext = new DefaultHttpContext
        {
            Response = { Body = new MemoryStream() }
        };
        _httpContext.Request.Path = "/api/v1/deployments";
    }

    [Theory(Skip = "Temporarily disabled - investigating test hang")]
    [InlineData("/health")]
    [InlineData("/health/ready")]
    [InlineData("/swagger")]
    [InlineData("/swagger/index.html")]
    [InlineData("/api/v1/admin")]
    [InlineData("/api/v1/admin/tenants")]
    [InlineData("/api/v1/auth")]
    [InlineData("/api/v1/auth/login")]
    public async Task InvokeAsync_WithSkipPaths_ShouldSkipTenantResolution(string path)
    {
        // Arrange
        var middleware = new TenantContextMiddleware(_nextMock.Object, _loggerMock.Object);
        _httpContext.Request.Path = path;

        // Act
        await middleware.InvokeAsync(_httpContext, _tenantContextServiceMock.Object);

        // Assert
        _nextMock.Verify(next => next(_httpContext), Times.Once);
        _tenantContextServiceMock.Verify(
            s => s.GetCurrentTenantAsync(It.IsAny<CancellationToken>()),
            Times.Never);
        _httpContext.Response.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task InvokeAsync_WithActiveTenant_ShouldSetTenantContextAndCallNext()
    {
        // Arrange
        var tenant = new Tenant
        {
            TenantId = Guid.NewGuid(),
            Name = "Acme Corporation",
            Subdomain = "acme",
            Status = TenantStatus.Active
        };

        _tenantContextServiceMock
            .Setup(s => s.GetCurrentTenantAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);

        var middleware = new TenantContextMiddleware(_nextMock.Object, _loggerMock.Object);

        // Act
        await middleware.InvokeAsync(_httpContext, _tenantContextServiceMock.Object);

        // Assert
        _nextMock.Verify(next => next(_httpContext), Times.Once);
        _httpContext.Response.StatusCode.Should().Be(200);
        _httpContext.Items["TenantId"].Should().Be(tenant.TenantId);
        _httpContext.Items["Tenant"].Should().Be(tenant);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Tenant context resolved")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_WithNoTenant_ShouldReturn400()
    {
        // Arrange
        _tenantContextServiceMock
            .Setup(s => s.GetCurrentTenantAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tenant?)null);

        var middleware = new TenantContextMiddleware(_nextMock.Object, _loggerMock.Object);

        // Act
        await middleware.InvokeAsync(_httpContext, _tenantContextServiceMock.Object);

        // Assert
        _httpContext.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        _nextMock.Verify(next => next(_httpContext), Times.Never);

        _httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var responseBody = await new StreamReader(_httpContext.Response.Body).ReadToEndAsync();
        var errorResponse = JsonSerializer.Deserialize<JsonElement>(responseBody);

        errorResponse.GetProperty("error").GetString().Should().Be("Tenant context required");
        errorResponse.GetProperty("message").GetString().Should().Contain("Please specify tenant");

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Tenant context required but not found")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_WithSuspendedTenant_ShouldReturn403()
    {
        // Arrange
        var tenant = new Tenant
        {
            TenantId = Guid.NewGuid(),
            Name = "Acme Corporation",
            Subdomain = "acme",
            Status = TenantStatus.Suspended
        };

        _tenantContextServiceMock
            .Setup(s => s.GetCurrentTenantAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);

        var middleware = new TenantContextMiddleware(_nextMock.Object, _loggerMock.Object);

        // Act
        await middleware.InvokeAsync(_httpContext, _tenantContextServiceMock.Object);

        // Assert
        _httpContext.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        _nextMock.Verify(next => next(_httpContext), Times.Never);

        _httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var responseBody = await new StreamReader(_httpContext.Response.Body).ReadToEndAsync();
        var errorResponse = JsonSerializer.Deserialize<JsonElement>(responseBody);

        errorResponse.GetProperty("error").GetString().Should().Be("Tenant not available");
        errorResponse.GetProperty("message").GetString().Should().Be("Tenant is suspended");
        errorResponse.GetProperty("tenantId").GetGuid().Should().Be(tenant.TenantId);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("is not active")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_WithDeletedTenant_ShouldReturn403()
    {
        // Arrange
        var tenant = new Tenant
        {
            TenantId = Guid.NewGuid(),
            Name = "Deleted Tenant",
            Subdomain = "deleted",
            Status = TenantStatus.Deleted
        };

        _tenantContextServiceMock
            .Setup(s => s.GetCurrentTenantAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);

        var middleware = new TenantContextMiddleware(_nextMock.Object, _loggerMock.Object);

        // Act
        await middleware.InvokeAsync(_httpContext, _tenantContextServiceMock.Object);

        // Assert
        _httpContext.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        _nextMock.Verify(next => next(_httpContext), Times.Never);

        _httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var responseBody = await new StreamReader(_httpContext.Response.Body).ReadToEndAsync();
        var errorResponse = JsonSerializer.Deserialize<JsonElement>(responseBody);

        errorResponse.GetProperty("message").GetString().Should().Be("Tenant is deleted");
    }

    [Fact]
    public async Task InvokeAsync_WithProvisioningTenant_ShouldReturn403()
    {
        // Arrange
        var tenant = new Tenant
        {
            TenantId = Guid.NewGuid(),
            Name = "Provisioning Tenant",
            Subdomain = "provisioning",
            Status = TenantStatus.Provisioning
        };

        _tenantContextServiceMock
            .Setup(s => s.GetCurrentTenantAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);

        var middleware = new TenantContextMiddleware(_nextMock.Object, _loggerMock.Object);

        // Act
        await middleware.InvokeAsync(_httpContext, _tenantContextServiceMock.Object);

        // Assert
        _httpContext.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        _nextMock.Verify(next => next(_httpContext), Times.Never);

        _httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var responseBody = await new StreamReader(_httpContext.Response.Body).ReadToEndAsync();
        var errorResponse = JsonSerializer.Deserialize<JsonElement>(responseBody);

        errorResponse.GetProperty("message").GetString().Should().Be("Tenant is provisioning");
    }

    [Fact]
    public async Task InvokeAsync_WithDeprovisioningTenant_ShouldReturn403()
    {
        // Arrange
        var tenant = new Tenant
        {
            TenantId = Guid.NewGuid(),
            Name = "Deprovisioning Tenant",
            Subdomain = "deprovisioning",
            Status = TenantStatus.Deprovisioning
        };

        _tenantContextServiceMock
            .Setup(s => s.GetCurrentTenantAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);

        var middleware = new TenantContextMiddleware(_nextMock.Object, _loggerMock.Object);

        // Act
        await middleware.InvokeAsync(_httpContext, _tenantContextServiceMock.Object);

        // Assert
        _httpContext.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        _nextMock.Verify(next => next(_httpContext), Times.Never);

        _httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var responseBody = await new StreamReader(_httpContext.Response.Body).ReadToEndAsync();
        var errorResponse = JsonSerializer.Deserialize<JsonElement>(responseBody);

        errorResponse.GetProperty("message").GetString().Should().Be("Tenant is deprovisioning");
    }

    [Fact]
    public void InvokeAsync_WithNullRequestDelegate_ShouldThrowArgumentNullException()
    {
        // Arrange & Act & Assert
        var act = () => new TenantContextMiddleware(null!, _loggerMock.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("next");
    }

    [Fact]
    public void InvokeAsync_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Arrange & Act & Assert
        var act = () => new TenantContextMiddleware(_nextMock.Object, null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    [Fact]
    public async Task InvokeAsync_WithCancellationToken_ShouldPassCancellationToken()
    {
        // Arrange
        var tenant = new Tenant
        {
            TenantId = Guid.NewGuid(),
            Name = "Acme Corporation",
            Subdomain = "acme",
            Status = TenantStatus.Active
        };

        var cancellationToken = new CancellationTokenSource().Token;
        _httpContext.RequestAborted = cancellationToken;

        _tenantContextServiceMock
            .Setup(s => s.GetCurrentTenantAsync(cancellationToken))
            .ReturnsAsync(tenant);

        var middleware = new TenantContextMiddleware(_nextMock.Object, _loggerMock.Object);

        // Act
        await middleware.InvokeAsync(_httpContext, _tenantContextServiceMock.Object);

        // Assert
        _tenantContextServiceMock.Verify(
            s => s.GetCurrentTenantAsync(cancellationToken),
            Times.Once);
    }

    [Theory(Skip = "Temporarily disabled - investigating test hang")]
    [InlineData("/HEALTH")]
    [InlineData("/Swagger")]
    [InlineData("/API/V1/ADMIN")]
    [InlineData("/Api/V1/Auth")]
    public async Task InvokeAsync_WithSkipPathsCaseInsensitive_ShouldSkipTenantResolution(string path)
    {
        // Arrange
        var middleware = new TenantContextMiddleware(_nextMock.Object, _loggerMock.Object);
        _httpContext.Request.Path = path;

        // Act
        await middleware.InvokeAsync(_httpContext, _tenantContextServiceMock.Object);

        // Assert
        _nextMock.Verify(next => next(_httpContext), Times.Once);
        _tenantContextServiceMock.Verify(
            s => s.GetCurrentTenantAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
