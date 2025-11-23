using FluentAssertions;
using HotSwap.Distributed.Api.Controllers;
using HotSwap.Distributed.Api.Models;
using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace HotSwap.Distributed.Tests.Api;

public class TenantsControllerTests
{
    private readonly Mock<ITenantRepository> _mockRepository;
    private readonly Mock<ITenantProvisioningService> _mockProvisioning;
    private readonly TenantsController _controller;

    public TenantsControllerTests()
    {
        _mockRepository = new Mock<ITenantRepository>();
        _mockProvisioning = new Mock<ITenantProvisioningService>();
        _controller = new TenantsController(
            _mockRepository.Object,
            _mockProvisioning.Object,
            NullLogger<TenantsController>.Instance);
    }

    private CreateTenantRequest CreateTestRequest(
        string name = "Test Tenant",
        string subdomain = "test-tenant",
        string contactEmail = "admin@test.com")
    {
        return new CreateTenantRequest
        {
            Name = name,
            Subdomain = subdomain,
            ContactEmail = contactEmail,
            Tier = SubscriptionTier.Free,
            CustomDomain = null,
            Metadata = null
        };
    }

    private Tenant CreateTestTenant(Guid? tenantId = null, string subdomain = "test-tenant")
    {
        return new Tenant
        {
            TenantId = tenantId ?? Guid.NewGuid(),
            Name = "Test Tenant",
            Subdomain = subdomain,
            CustomDomain = null,
            Status = TenantStatus.Active,
            Tier = SubscriptionTier.Free,
            ContactEmail = "admin@test.com",
            ResourceQuota = ResourceQuota.CreateDefault(SubscriptionTier.Free),
            Metadata = new Dictionary<string, string>(),
            CreatedAt = DateTime.UtcNow
        };
    }

    #region CreateTenant Tests

    [Fact]
    public async Task CreateTenant_WithValidRequest_ReturnsCreated()
    {
        // Arrange
        var request = CreateTestRequest();

        _mockRepository.Setup(x => x.IsSubdomainAvailableAsync(request.Subdomain, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        Tenant? provisionedTenant = null;
        _mockProvisioning.Setup(x => x.ProvisionTenantAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>()))
            .Callback<Tenant, CancellationToken>((t, ct) => provisionedTenant = t)
            .ReturnsAsync((Tenant t, CancellationToken ct) => t);

        // Act
        var result = await _controller.CreateTenant(request, CancellationToken.None);

        // Assert
        var createdResult = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.ActionName.Should().Be(nameof(TenantsController.GetTenant));

        var response = createdResult.Value.Should().BeOfType<TenantResponse>().Subject;
        response.Name.Should().Be(request.Name);
        response.Subdomain.Should().Be(request.Subdomain.ToLowerInvariant());
        response.ContactEmail.Should().Be(request.ContactEmail);
        response.TenantId.Should().NotBeEmpty();

        provisionedTenant.Should().NotBeNull();
        provisionedTenant!.TenantId.Should().NotBe(Guid.Empty);
        provisionedTenant.Subdomain.Should().Be(request.Subdomain.ToLowerInvariant());
    }

    [Fact]
    public async Task CreateTenant_WithExistingSubdomain_ReturnsBadRequest()
    {
        // Arrange
        var request = CreateTestRequest();

        _mockRepository.Setup(x => x.IsSubdomainAvailableAsync(request.Subdomain, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.CreateTenant(request, CancellationToken.None);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var errorResponse = badRequestResult.Value.Should().BeOfType<ErrorResponse>().Subject;

        errorResponse.Error.Should().Contain("already taken");
        errorResponse.Error.Should().Contain(request.Subdomain);

        _mockProvisioning.Verify(x => x.ProvisionTenantAsync(
            It.IsAny<Tenant>(),
            It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateTenant_SubdomainConvertedToLowercase()
    {
        // Arrange
        var request = CreateTestRequest(subdomain: "Test-Tenant-UPPER");

        _mockRepository.Setup(x => x.IsSubdomainAvailableAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        Tenant? provisionedTenant = null;
        _mockProvisioning.Setup(x => x.ProvisionTenantAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>()))
            .Callback<Tenant, CancellationToken>((t, ct) => provisionedTenant = t)
            .ReturnsAsync((Tenant t, CancellationToken ct) => t);

        // Act
        await _controller.CreateTenant(request, CancellationToken.None);

        // Assert
        provisionedTenant.Should().NotBeNull();
        provisionedTenant!.Subdomain.Should().Be("test-tenant-upper");
    }

    [Fact]
    public async Task CreateTenant_SetsResourceQuotaBasedOnTier()
    {
        // Arrange
        var request = CreateTestRequest();
        request.Tier = SubscriptionTier.Professional;

        _mockRepository.Setup(x => x.IsSubdomainAvailableAsync(request.Subdomain, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        Tenant? provisionedTenant = null;
        _mockProvisioning.Setup(x => x.ProvisionTenantAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>()))
            .Callback<Tenant, CancellationToken>((t, ct) => provisionedTenant = t)
            .ReturnsAsync((Tenant t, CancellationToken ct) => t);

        // Act
        await _controller.CreateTenant(request, CancellationToken.None);

        // Assert
        provisionedTenant.Should().NotBeNull();
        provisionedTenant!.ResourceQuota.Should().NotBeNull();
        provisionedTenant.Tier.Should().Be(SubscriptionTier.Professional);
    }

    [Fact]
    public async Task CreateTenant_WithNullMetadata_UsesEmptyDictionary()
    {
        // Arrange
        var request = CreateTestRequest();
        request.Metadata = null;

        _mockRepository.Setup(x => x.IsSubdomainAvailableAsync(request.Subdomain, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        Tenant? provisionedTenant = null;
        _mockProvisioning.Setup(x => x.ProvisionTenantAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>()))
            .Callback<Tenant, CancellationToken>((t, ct) => provisionedTenant = t)
            .ReturnsAsync((Tenant t, CancellationToken ct) => t);

        // Act
        await _controller.CreateTenant(request, CancellationToken.None);

        // Assert
        provisionedTenant.Should().NotBeNull();
        provisionedTenant!.Metadata.Should().NotBeNull();
        provisionedTenant.Metadata.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateTenant_WhenProvisioningFails_ReturnsInternalServerError()
    {
        // Arrange
        var request = CreateTestRequest();

        _mockRepository.Setup(x => x.IsSubdomainAvailableAsync(request.Subdomain, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _mockProvisioning.Setup(x => x.ProvisionTenantAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Provisioning service unavailable"));

        // Act
        var result = await _controller.CreateTenant(request, CancellationToken.None);

        // Assert
        var statusCodeResult = result.Should().BeOfType<ObjectResult>().Subject;
        statusCodeResult.StatusCode.Should().Be(500);

        var errorResponse = statusCodeResult.Value.Should().BeOfType<ErrorResponse>().Subject;
        errorResponse.Error.Should().Contain("Tenant creation failed");
        errorResponse.Details.Should().Contain("Provisioning service unavailable");
    }

    #endregion

    #region GetTenant Tests

    [Fact]
    public async Task GetTenant_WithExistingId_ReturnsOk()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var tenant = CreateTestTenant(tenantId);

        _mockRepository.Setup(x => x.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);

        // Act
        var result = await _controller.GetTenant(tenantId, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<TenantResponse>().Subject;

        response.TenantId.Should().Be(tenantId);
        response.Name.Should().Be(tenant.Name);
        response.Subdomain.Should().Be(tenant.Subdomain);
    }

    [Fact]
    public async Task GetTenant_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        var tenantId = Guid.NewGuid();

        _mockRepository.Setup(x => x.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tenant?)null);

        // Act
        var result = await _controller.GetTenant(tenantId, CancellationToken.None);

        // Assert
        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        var errorResponse = notFoundResult.Value.Should().BeOfType<ErrorResponse>().Subject;

        errorResponse.Error.Should().Contain(tenantId.ToString());
    }

    #endregion

    #region ListTenants Tests

    [Fact]
    public async Task ListTenants_WithoutStatusFilter_ReturnsAllTenants()
    {
        // Arrange
        var tenants = new List<Tenant>
        {
            CreateTestTenant(Guid.NewGuid(), "tenant1"),
            CreateTestTenant(Guid.NewGuid(), "tenant2"),
            CreateTestTenant(Guid.NewGuid(), "tenant3")
        };

        _mockRepository.Setup(x => x.GetAllAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenants);

        // Act
        var result = await _controller.ListTenants(null, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var responses = okResult.Value.Should().BeOfType<List<TenantResponse>>().Subject;

        responses.Should().HaveCount(3);
        responses.Select(r => r.Subdomain).Should().Contain(new[] { "tenant1", "tenant2", "tenant3" });
    }

    [Fact]
    public async Task ListTenants_WithValidStatusFilter_ReturnsFilteredTenants()
    {
        // Arrange
        var activeTenants = new List<Tenant>
        {
            CreateTestTenant(Guid.NewGuid(), "active1"),
            CreateTestTenant(Guid.NewGuid(), "active2")
        };

        _mockRepository.Setup(x => x.GetAllAsync(TenantStatus.Active, It.IsAny<CancellationToken>()))
            .ReturnsAsync(activeTenants);

        // Act
        var result = await _controller.ListTenants("Active", CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var responses = okResult.Value.Should().BeOfType<List<TenantResponse>>().Subject;

        responses.Should().HaveCount(2);
    }

    [Fact]
    public async Task ListTenants_WithInvalidStatusFilter_IgnoresFilter()
    {
        // Arrange
        var tenants = new List<Tenant>
        {
            CreateTestTenant(Guid.NewGuid(), "tenant1")
        };

        _mockRepository.Setup(x => x.GetAllAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenants);

        // Act
        var result = await _controller.ListTenants("InvalidStatus", CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var responses = okResult.Value.Should().BeOfType<List<TenantResponse>>().Subject;

        responses.Should().HaveCount(1);
        _mockRepository.Verify(x => x.GetAllAsync(null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ListTenants_WithEmptyStatusFilter_ReturnsAllTenants()
    {
        // Arrange
        var tenants = new List<Tenant>
        {
            CreateTestTenant(Guid.NewGuid(), "tenant1")
        };

        _mockRepository.Setup(x => x.GetAllAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenants);

        // Act
        var result = await _controller.ListTenants("", CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        _mockRepository.Verify(x => x.GetAllAsync(null, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region UpdateTenant Tests

    [Fact]
    public async Task UpdateTenant_WithExistingTenant_ReturnsUpdatedTenant()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var tenant = CreateTestTenant(tenantId);
        var request = new UpdateTenantRequest
        {
            Name = "Updated Name",
            ContactEmail = "newemail@test.com",
            CustomDomain = "custom.example.com",
            Metadata = new Dictionary<string, string> { { "key1", "value1" } }
        };

        _mockRepository.Setup(x => x.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);

        _mockRepository.Setup(x => x.UpdateAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tenant t, CancellationToken ct) => t);

        // Act
        var result = await _controller.UpdateTenant(tenantId, request, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<TenantResponse>().Subject;

        response.Name.Should().Be("Updated Name");
        response.ContactEmail.Should().Be("newemail@test.com");
        response.CustomDomain.Should().Be("custom.example.com");
        response.Metadata.Should().ContainKey("key1");
    }

    [Fact]
    public async Task UpdateTenant_WithNonExistentTenant_ReturnsNotFound()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var request = new UpdateTenantRequest { Name = "Updated Name" };

        _mockRepository.Setup(x => x.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tenant?)null);

        // Act
        var result = await _controller.UpdateTenant(tenantId, request, CancellationToken.None);

        // Assert
        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        var errorResponse = notFoundResult.Value.Should().BeOfType<ErrorResponse>().Subject;

        errorResponse.Error.Should().Contain(tenantId.ToString());

        _mockRepository.Verify(x => x.UpdateAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateTenant_WithPartialUpdate_OnlyUpdatesProvidedFields()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var tenant = CreateTestTenant(tenantId);
        tenant.Name = "Original Name";
        tenant.ContactEmail = "original@test.com";

        var request = new UpdateTenantRequest
        {
            Name = "New Name"
            // ContactEmail not provided
        };

        _mockRepository.Setup(x => x.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);

        Tenant? updatedTenant = null;
        _mockRepository.Setup(x => x.UpdateAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>()))
            .Callback<Tenant, CancellationToken>((t, ct) => updatedTenant = t)
            .ReturnsAsync((Tenant t, CancellationToken ct) => t);

        // Act
        await _controller.UpdateTenant(tenantId, request, CancellationToken.None);

        // Assert
        updatedTenant.Should().NotBeNull();
        updatedTenant!.Name.Should().Be("New Name");
        updatedTenant.ContactEmail.Should().Be("original@test.com"); // Should not change
    }

    [Fact]
    public async Task UpdateTenant_WithMetadata_MergesMetadata()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var tenant = CreateTestTenant(tenantId);
        tenant.Metadata = new Dictionary<string, string>
        {
            { "existing1", "value1" },
            { "existing2", "value2" }
        };

        var request = new UpdateTenantRequest
        {
            Metadata = new Dictionary<string, string>
            {
                { "existing1", "updated_value1" },
                { "new1", "new_value1" }
            }
        };

        _mockRepository.Setup(x => x.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);

        Tenant? updatedTenant = null;
        _mockRepository.Setup(x => x.UpdateAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>()))
            .Callback<Tenant, CancellationToken>((t, ct) => updatedTenant = t)
            .ReturnsAsync((Tenant t, CancellationToken ct) => t);

        // Act
        await _controller.UpdateTenant(tenantId, request, CancellationToken.None);

        // Assert
        updatedTenant.Should().NotBeNull();
        updatedTenant!.Metadata.Should().ContainKey("existing1");
        updatedTenant.Metadata["existing1"].Should().Be("updated_value1");
        updatedTenant.Metadata.Should().ContainKey("existing2"); // Original metadata preserved
        updatedTenant.Metadata.Should().ContainKey("new1");
    }

    #endregion

    #region SuspendTenant Tests

    [Fact]
    public async Task SuspendTenant_WithExistingTenant_ReturnsOk()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var tenant = CreateTestTenant(tenantId);
        tenant.Status = TenantStatus.Suspended;

        _mockProvisioning.Setup(x => x.SuspendTenantAsync(tenantId, "Suspended by administrator", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _mockRepository.Setup(x => x.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);

        // Act
        var result = await _controller.SuspendTenant(tenantId, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<TenantResponse>().Subject;

        response.TenantId.Should().Be(tenantId);

        _mockProvisioning.Verify(x => x.SuspendTenantAsync(
            tenantId,
            "Suspended by administrator",
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SuspendTenant_WithNonExistentTenant_ReturnsNotFound()
    {
        // Arrange
        var tenantId = Guid.NewGuid();

        _mockProvisioning.Setup(x => x.SuspendTenantAsync(tenantId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.SuspendTenant(tenantId, CancellationToken.None);

        // Assert
        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        var errorResponse = notFoundResult.Value.Should().BeOfType<ErrorResponse>().Subject;

        errorResponse.Error.Should().Contain(tenantId.ToString());

        _mockRepository.Verify(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region ActivateTenant Tests

    [Fact]
    public async Task ActivateTenant_WithExistingTenant_ReturnsOk()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var tenant = CreateTestTenant(tenantId);
        tenant.Status = TenantStatus.Active;

        _mockProvisioning.Setup(x => x.ActivateTenantAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _mockRepository.Setup(x => x.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);

        // Act
        var result = await _controller.ActivateTenant(tenantId, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<TenantResponse>().Subject;

        response.TenantId.Should().Be(tenantId);

        _mockProvisioning.Verify(x => x.ActivateTenantAsync(tenantId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ActivateTenant_WithNonExistentTenant_ReturnsNotFound()
    {
        // Arrange
        var tenantId = Guid.NewGuid();

        _mockProvisioning.Setup(x => x.ActivateTenantAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.ActivateTenant(tenantId, CancellationToken.None);

        // Assert
        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        var errorResponse = notFoundResult.Value.Should().BeOfType<ErrorResponse>().Subject;

        errorResponse.Error.Should().Contain(tenantId.ToString());
    }

    #endregion

    #region UpdateSubscription Tests

    [Fact]
    public async Task UpdateSubscription_WithExistingTenant_UpdatesTierAndQuota()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var tenant = CreateTestTenant(tenantId);
        tenant.Tier = SubscriptionTier.Free;

        var request = new UpdateSubscriptionRequest
        {
            Tier = SubscriptionTier.Enterprise
        };

        _mockRepository.Setup(x => x.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);

        Tenant? updatedTenant = null;
        _mockRepository.Setup(x => x.UpdateAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>()))
            .Callback<Tenant, CancellationToken>((t, ct) => updatedTenant = t)
            .ReturnsAsync((Tenant t, CancellationToken ct) => t);

        // Act
        var result = await _controller.UpdateSubscription(tenantId, request, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<TenantResponse>().Subject;

        response.Tier.Should().Be("Enterprise");

        updatedTenant.Should().NotBeNull();
        updatedTenant!.Tier.Should().Be(SubscriptionTier.Enterprise);
        updatedTenant.ResourceQuota.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateSubscription_WithNonExistentTenant_ReturnsNotFound()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var request = new UpdateSubscriptionRequest { Tier = SubscriptionTier.Professional };

        _mockRepository.Setup(x => x.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tenant?)null);

        // Act
        var result = await _controller.UpdateSubscription(tenantId, request, CancellationToken.None);

        // Assert
        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        var errorResponse = notFoundResult.Value.Should().BeOfType<ErrorResponse>().Subject;

        errorResponse.Error.Should().Contain(tenantId.ToString());

        _mockRepository.Verify(x => x.UpdateAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region DeleteTenant Tests

    [Fact]
    public async Task DeleteTenant_WithExistingTenant_ReturnsOk()
    {
        // Arrange
        var tenantId = Guid.NewGuid();

        _mockRepository.Setup(x => x.DeleteAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.DeleteTenant(tenantId, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().NotBeNull();

        _mockRepository.Verify(x => x.DeleteAsync(tenantId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteTenant_WithNonExistentTenant_ReturnsNotFound()
    {
        // Arrange
        var tenantId = Guid.NewGuid();

        _mockRepository.Setup(x => x.DeleteAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.DeleteTenant(tenantId, CancellationToken.None);

        // Assert
        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        var errorResponse = notFoundResult.Value.Should().BeOfType<ErrorResponse>().Subject;

        errorResponse.Error.Should().Contain(tenantId.ToString());
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullRepository_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new TenantsController(null!, _mockProvisioning.Object, NullLogger<TenantsController>.Instance));
    }

    [Fact]
    public void Constructor_WithNullProvisioningService_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new TenantsController(_mockRepository.Object, null!, NullLogger<TenantsController>.Instance));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new TenantsController(_mockRepository.Object, _mockProvisioning.Object, null!));
    }

    #endregion
}
