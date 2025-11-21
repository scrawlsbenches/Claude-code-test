using FluentAssertions;
using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Interfaces;
using HotSwap.Distributed.Infrastructure.Tenants;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;
using Xunit;

namespace HotSwap.Distributed.Tests.Infrastructure;

public class TenantContextServiceTests
{
    private readonly Mock<IHttpContextAccessor> _mockHttpContextAccessor;
    private readonly Mock<ITenantRepository> _mockTenantRepository;
    private readonly Mock<ILogger<TenantContextService>> _mockLogger;
    private readonly TenantContextService _service;
    private readonly DefaultHttpContext _httpContext;

    public TenantContextServiceTests()
    {
        _mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        _mockTenantRepository = new Mock<ITenantRepository>();
        _mockLogger = new Mock<ILogger<TenantContextService>>();

        _httpContext = new DefaultHttpContext();
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(_httpContext);

        _service = new TenantContextService(
            _mockHttpContextAccessor.Object,
            _mockTenantRepository.Object,
            _mockLogger.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullHttpContextAccessor_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new TenantContextService(
            null!,
            _mockTenantRepository.Object,
            _mockLogger.Object);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("httpContextAccessor");
    }

    [Fact]
    public void Constructor_WithNullTenantRepository_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new TenantContextService(
            _mockHttpContextAccessor.Object,
            null!,
            _mockLogger.Object);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("tenantRepository");
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new TenantContextService(
            _mockHttpContextAccessor.Object,
            _mockTenantRepository.Object,
            null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    #endregion

    #region GetCurrentTenantAsync Tests

    [Fact]
    public async Task GetCurrentTenantAsync_WithNoHttpContext_ReturnsNull()
    {
        // Arrange
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns((HttpContext?)null);

        // Act
        var result = await _service.GetCurrentTenantAsync();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetCurrentTenantAsync_WithCachedTenant_ReturnsCachedValue()
    {
        // Arrange
        var tenant = CreateTestTenant();
        _httpContext.Items["Tenant"] = tenant;

        // Act
        var result = await _service.GetCurrentTenantAsync();

        // Assert
        result.Should().BeSameAs(tenant);
        _mockTenantRepository.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetCurrentTenantAsync_WithXTenantIDHeader_LoadsTenantFromRepository()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var tenant = CreateTestTenant(tenantId);

        _httpContext.Request.Headers["X-Tenant-ID"] = tenantId.ToString();
        _mockTenantRepository.Setup(r => r.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);

        // Act
        var result = await _service.GetCurrentTenantAsync();

        // Assert
        result.Should().NotBeNull();
        result!.TenantId.Should().Be(tenantId);
        _httpContext.Items["Tenant"].Should().BeSameAs(tenant);
        _httpContext.Items["TenantId"].Should().Be(tenantId);
    }

    [Fact]
    public async Task GetCurrentTenantAsync_WithJWTClaim_LoadsTenantFromRepository()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var tenant = CreateTestTenant(tenantId);

        var claims = new[] { new Claim("tenant_id", tenantId.ToString()) };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        _httpContext.User = new ClaimsPrincipal(identity);

        _mockTenantRepository.Setup(r => r.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);

        // Act
        var result = await _service.GetCurrentTenantAsync();

        // Assert
        result.Should().NotBeNull();
        result!.TenantId.Should().Be(tenantId);
    }

    [Fact]
    public async Task GetCurrentTenantAsync_WithSubdomain_LoadsTenantFromSubdomain()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var tenant = CreateTestTenant(tenantId, "acme");

        _httpContext.Request.Host = new HostString("acme.platform.com");
        _mockTenantRepository.Setup(r => r.GetBySubdomainAsync("acme", It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);
        _mockTenantRepository.Setup(r => r.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);

        // Act
        var result = await _service.GetCurrentTenantAsync();

        // Assert
        result.Should().NotBeNull();
        result!.TenantId.Should().Be(tenantId);
        result.Subdomain.Should().Be("acme");
    }

    [Fact]
    public async Task GetCurrentTenantAsync_WithNoTenantIdentifier_ReturnsNull()
    {
        // Arrange
        _httpContext.Request.Host = new HostString("platform.com"); // No subdomain
        // No headers or claims

        // Act
        var result = await _service.GetCurrentTenantAsync();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetCurrentTenantAsync_WithNonExistentTenant_ReturnsNull()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        _httpContext.Request.Headers["X-Tenant-ID"] = tenantId.ToString();
        _mockTenantRepository.Setup(r => r.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tenant?)null);

        // Act
        var result = await _service.GetCurrentTenantAsync();

        // Assert
        result.Should().BeNull();
        _httpContext.Items.Should().NotContainKey("Tenant");
    }

    #endregion

    #region GetCurrentTenantId Tests

    [Fact]
    public void GetCurrentTenantId_WithNoHttpContext_ReturnsNull()
    {
        // Arrange
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns((HttpContext?)null);

        // Act
        var result = _service.GetCurrentTenantId();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetCurrentTenantId_WithCachedTenantId_ReturnsCachedValue()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        _httpContext.Items["TenantId"] = tenantId;

        // Act
        var result = _service.GetCurrentTenantId();

        // Assert
        result.Should().Be(tenantId);
        _mockTenantRepository.Verify(r => r.GetBySubdomainAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void GetCurrentTenantId_WithXTenantIDHeader_ReturnsExtractedId()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        _httpContext.Request.Headers["X-Tenant-ID"] = tenantId.ToString();

        // Act
        var result = _service.GetCurrentTenantId();

        // Assert
        result.Should().Be(tenantId);
    }

    [Fact]
    public void GetCurrentTenantId_WithInvalidHeader_ReturnsNull()
    {
        // Arrange
        _httpContext.Request.Headers["X-Tenant-ID"] = "invalid-guid";

        // Act
        var result = _service.GetCurrentTenantId();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetCurrentTenantId_WithJWTClaim_ReturnsExtractedId()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var claims = new[] { new Claim("tenant_id", tenantId.ToString()) };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        _httpContext.User = new ClaimsPrincipal(identity);

        // Act
        var result = _service.GetCurrentTenantId();

        // Assert
        result.Should().Be(tenantId);
    }

    #endregion

    #region SetCurrentTenant Tests

    [Fact]
    public void SetCurrentTenant_WithNullTenant_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => _service.SetCurrentTenant(null!);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("tenant");
    }

    [Fact]
    public void SetCurrentTenant_WithNoHttpContext_ThrowsInvalidOperationException()
    {
        // Arrange
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns((HttpContext?)null);
        var tenant = CreateTestTenant();

        // Act & Assert
        var act = () => _service.SetCurrentTenant(tenant);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("HttpContext is not available");
    }

    [Fact]
    public void SetCurrentTenant_WithValidTenant_SetsCacheInHttpContext()
    {
        // Arrange
        var tenant = CreateTestTenant();

        // Act
        _service.SetCurrentTenant(tenant);

        // Assert
        _httpContext.Items["TenantId"].Should().Be(tenant.TenantId);
        _httpContext.Items["Tenant"].Should().BeSameAs(tenant);
    }

    #endregion

    #region ValidateCurrentTenantAsync Tests

    [Fact]
    public async Task ValidateCurrentTenantAsync_WithNoTenant_ReturnsFalse()
    {
        // Arrange
        _httpContext.Request.Host = new HostString("platform.com");
        // No tenant identifiers

        // Act
        var result = await _service.ValidateCurrentTenantAsync();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateCurrentTenantAsync_WithActiveTenant_ReturnsTrue()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var tenant = CreateTestTenant(tenantId);
        tenant.Status = TenantStatus.Active;

        _httpContext.Request.Headers["X-Tenant-ID"] = tenantId.ToString();
        _mockTenantRepository.Setup(r => r.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);

        // Act
        var result = await _service.ValidateCurrentTenantAsync();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateCurrentTenantAsync_WithSuspendedTenant_ReturnsFalse()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var tenant = CreateTestTenant(tenantId);
        tenant.Status = TenantStatus.Suspended;

        _httpContext.Request.Headers["X-Tenant-ID"] = tenantId.ToString();
        _mockTenantRepository.Setup(r => r.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);

        // Act
        var result = await _service.ValidateCurrentTenantAsync();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateCurrentTenantAsync_WithDeletedTenant_ReturnsFalse()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var tenant = CreateTestTenant(tenantId);
        tenant.Status = TenantStatus.Deleted;
        tenant.DeletedAt = DateTime.UtcNow;

        _httpContext.Request.Headers["X-Tenant-ID"] = tenantId.ToString();
        _mockTenantRepository.Setup(r => r.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);

        // Act
        var result = await _service.ValidateCurrentTenantAsync();

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Helper Methods

    private static Tenant CreateTestTenant(Guid? tenantId = null, string subdomain = "test")
    {
        return new Tenant
        {
            TenantId = tenantId ?? Guid.NewGuid(),
            Name = "Test Tenant",
            Subdomain = subdomain,
            Tier = SubscriptionTier.Professional,
            Status = TenantStatus.Active,
            ResourceQuota = ResourceQuota.CreateDefault(SubscriptionTier.Professional)
        };
    }

    #endregion
}
