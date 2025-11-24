using System.Security.Claims;
using FluentAssertions;
using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Interfaces;
using HotSwap.Distributed.Infrastructure.Tenants;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
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

        // User authenticated with JWT claim
        var claims = new[] { new Claim("tenant_id", tenantId.ToString()) };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        _httpContext.User = new ClaimsPrincipal(identity);

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

    #region Async Refactoring Tests (CRITICAL-03 Fix)

    /// <summary>
    /// Tests async subdomain resolution in GetCurrentTenantAsync.
    /// This test verifies the fix for CRITICAL-03: Remove synchronous blocking (.Result) in TenantContextService.
    /// The fix ensures subdomain resolution uses async/await throughout, eliminating deadlock risk.
    /// </summary>
    [Fact]
    public async Task GetCurrentTenantAsync_WithSubdomain_UsesAsyncRepositoryCall()
    {
        // Arrange
        var tenant = CreateTestTenant(subdomain: "testcorp");
        _httpContext.Request.Host = new HostString("testcorp.platform.com");

        _mockTenantRepository.Setup(r => r.GetBySubdomainAsync("testcorp", It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);
        _mockTenantRepository.Setup(r => r.GetByIdAsync(tenant.TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);

        // Act
        var result = await _service.GetCurrentTenantAsync();

        // Assert
        result.Should().NotBeNull();
        result!.TenantId.Should().Be(tenant.TenantId);

        // Verify async repository method was called (not blocking .Result)
        _mockTenantRepository.Verify(
            r => r.GetBySubdomainAsync("testcorp", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that GetCurrentTenantId() does NOT perform async subdomain resolution.
    /// The synchronous version only extracts from headers/JWT claims, not subdomain.
    /// This ensures no blocking .Result calls in synchronous contexts.
    /// </summary>
    [Fact]
    public void GetCurrentTenantId_WithSubdomainOnly_ReturnsNull()
    {
        // Arrange
        var tenant = CreateTestTenant(subdomain: "testcorp");
        _httpContext.Request.Host = new HostString("testcorp.platform.com");

        _mockTenantRepository.Setup(r => r.GetBySubdomainAsync("testcorp", It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);

        // Act
        var result = _service.GetCurrentTenantId();

        // Assert
        // Should return null because subdomain resolution requires async
        result.Should().BeNull();

        // Verify repository was NOT called (no blocking .Result call)
        _mockTenantRepository.Verify(
            r => r.GetBySubdomainAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Tests that async tenant resolution honors cancellation tokens.
    /// Ensures proper async/await pattern is followed throughout the chain.
    /// </summary>
    [Fact]
    public async Task GetCurrentTenantAsync_WithCancellationToken_PropagatesToken()
    {
        // Arrange
        var tenant = CreateTestTenant();
        var tenantId = tenant.TenantId;

        // User authenticated with JWT claim
        var claims = new[] { new Claim("tenant_id", tenantId.ToString()) };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        _httpContext.User = new ClaimsPrincipal(identity);

        var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;

        _mockTenantRepository.Setup(r => r.GetByIdAsync(tenantId, cancellationToken))
            .ReturnsAsync(tenant);

        // Act
        var result = await _service.GetCurrentTenantAsync(cancellationToken);

        // Assert
        result.Should().NotBeNull();

        // Verify cancellation token was propagated to repository
        _mockTenantRepository.Verify(
            r => r.GetByIdAsync(tenantId, cancellationToken),
            Times.Once);
    }

    #endregion

    #region Security Tests (Tenant Isolation)

    /// <summary>
    /// SECURITY TEST: Verifies that authenticated users cannot impersonate other tenants using headers.
    /// This prevents horizontal privilege escalation attacks where User A accesses Tenant B's data.
    /// </summary>
    [Fact]
    public async Task GetCurrentTenantAsync_AuthenticatedUser_IgnoresXTenantIDHeader()
    {
        // Arrange - User authenticated for Tenant A
        var tenantA = CreateTestTenant(subdomain: "tenantA");
        var tenantB = CreateTestTenant(subdomain: "tenantB");

        // User's JWT has tenant_id for Tenant A
        var claims = new[] { new Claim("tenant_id", tenantA.TenantId.ToString()) };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        _httpContext.User = new ClaimsPrincipal(identity);

        // ATTACK: User tries to access Tenant B by sending X-Tenant-ID header
        _httpContext.Request.Headers["X-Tenant-ID"] = tenantB.TenantId.ToString();

        _mockTenantRepository.Setup(r => r.GetByIdAsync(tenantA.TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenantA);

        // Act
        var result = await _service.GetCurrentTenantAsync();

        // Assert - Should return Tenant A (from JWT), NOT Tenant B (from header)
        result.Should().NotBeNull();
        result!.TenantId.Should().Be(tenantA.TenantId, "JWT claim must be respected, not header");

        // Verify Tenant B was never accessed
        _mockTenantRepository.Verify(
            r => r.GetByIdAsync(tenantB.TenantId, It.IsAny<CancellationToken>()),
            Times.Never,
            "Authenticated user should not be able to access other tenant via header");
    }

    /// <summary>
    /// SECURITY TEST: Verifies that authenticated users cannot impersonate tenants via subdomain.
    /// JWT claim takes precedence over subdomain for authenticated requests.
    /// </summary>
    [Fact]
    public async Task GetCurrentTenantAsync_AuthenticatedUser_IgnoresSubdomain()
    {
        // Arrange - User authenticated for Tenant A
        var tenantA = CreateTestTenant(subdomain: "tenanta");
        var tenantB = CreateTestTenant(subdomain: "tenantb");

        // User's JWT has tenant_id for Tenant A
        var claims = new[] { new Claim("tenant_id", tenantA.TenantId.ToString()) };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        _httpContext.User = new ClaimsPrincipal(identity);

        // ATTACK: User accesses tenantb.platform.com while authenticated for Tenant A
        _httpContext.Request.Host = new HostString("tenantb.platform.com");

        _mockTenantRepository.Setup(r => r.GetByIdAsync(tenantA.TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenantA);

        // Act
        var result = await _service.GetCurrentTenantAsync();

        // Assert - Should return Tenant A (from JWT), NOT Tenant B (from subdomain)
        result.Should().NotBeNull();
        result!.TenantId.Should().Be(tenantA.TenantId, "JWT claim must override subdomain");

        // Verify subdomain lookup was never performed for authenticated user
        _mockTenantRepository.Verify(
            r => r.GetBySubdomainAsync("tenantb", It.IsAny<CancellationToken>()),
            Times.Never,
            "Subdomain should not be used for authenticated users");
    }

    /// <summary>
    /// SECURITY TEST: Verifies that authenticated users without tenant_id in JWT are denied access.
    /// This prevents JWT manipulation attacks.
    /// </summary>
    [Fact]
    public async Task GetCurrentTenantAsync_AuthenticatedUserWithoutTenantClaim_ReturnsNull()
    {
        // Arrange - User authenticated but JWT has no tenant_id claim
        var claims = new[] { new Claim("sub", "user123"), new Claim("email", "user@example.com") };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        _httpContext.User = new ClaimsPrincipal(identity);

        // Even with valid subdomain and header, should be denied
        _httpContext.Request.Host = new HostString("tenant1.platform.com");
        _httpContext.Request.Headers["X-Tenant-ID"] = Guid.NewGuid().ToString();

        // Act
        var result = await _service.GetCurrentTenantAsync();

        // Assert - Must deny access
        result.Should().BeNull("authenticated user without tenant_id claim should be denied");
    }

    /// <summary>
    /// SECURITY TEST: Verifies that unauthenticated requests can still use subdomain resolution.
    /// This is needed for public pages like login.
    /// </summary>
    [Fact]
    public async Task GetCurrentTenantAsync_UnauthenticatedUser_CanUseSubdomain()
    {
        // Arrange - Unauthenticated request to subdomain
        var tenant = CreateTestTenant(subdomain: "publicsite");
        _httpContext.Request.Host = new HostString("publicsite.platform.com");

        // User is NOT authenticated
        _httpContext.User = new ClaimsPrincipal(new ClaimsIdentity());

        _mockTenantRepository.Setup(r => r.GetBySubdomainAsync("publicsite", It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);
        _mockTenantRepository.Setup(r => r.GetByIdAsync(tenant.TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);

        // Act
        var result = await _service.GetCurrentTenantAsync();

        // Assert - Should resolve via subdomain
        result.Should().NotBeNull();
        result!.TenantId.Should().Be(tenant.TenantId);

        _mockTenantRepository.Verify(
            r => r.GetBySubdomainAsync("publicsite", It.IsAny<CancellationToken>()),
            Times.Once,
            "Unauthenticated requests should use subdomain resolution");
    }

    /// <summary>
    /// SECURITY TEST: Verifies subdomain validation rejects malicious input.
    /// Prevents subdomain injection attacks.
    /// </summary>
    [Theory]
    [InlineData("UPPERCASE")]  // Must be lowercase
    [InlineData("tenant_name")] // No underscores
    [InlineData("-tenant")]     // Cannot start with hyphen
    [InlineData("tenant-")]     // Cannot end with hyphen
    [InlineData("")]            // Cannot be empty
    public async Task GetCurrentTenantAsync_WithInvalidSubdomain_ReturnsNull(string invalidSubdomain)
    {
        // Arrange - Unauthenticated request with invalid subdomain
        _httpContext.Request.Host = new HostString($"{invalidSubdomain}.platform.com");
        _httpContext.User = new ClaimsPrincipal(new ClaimsIdentity());

        // Act
        var result = await _service.GetCurrentTenantAsync();

        // Assert - Should reject invalid subdomain
        result.Should().BeNull("invalid subdomain should be rejected");

        // Verify repository was never called for invalid subdomain
        _mockTenantRepository.Verify(
            r => r.GetBySubdomainAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "Invalid subdomains should not reach the repository");
    }

    /// <summary>
    /// SECURITY TEST: Verifies valid subdomain formats are accepted.
    /// </summary>
    [Theory]
    [InlineData("tenant1")]
    [InlineData("test-tenant")]
    [InlineData("my-tenant-123")]
    public async Task GetCurrentTenantAsync_WithValidSubdomain_QueriesRepository(string validSubdomain)
    {
        // Arrange
        _httpContext.Request.Host = new HostString($"{validSubdomain}.platform.com");
        _httpContext.User = new ClaimsPrincipal(new ClaimsIdentity());

        _mockTenantRepository.Setup(r => r.GetBySubdomainAsync(validSubdomain, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tenant?)null);

        // Act
        var result = await _service.GetCurrentTenantAsync();

        // Assert - Valid subdomain should reach repository
        _mockTenantRepository.Verify(
            r => r.GetBySubdomainAsync(validSubdomain, It.IsAny<CancellationToken>()),
            Times.Once,
            "Valid subdomain should be queried");
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
