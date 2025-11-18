using FluentAssertions;
using HotSwap.Distributed.Api.Models;
using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.IntegrationTests.Fixtures;
using HotSwap.Distributed.IntegrationTests.Helpers;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace HotSwap.Distributed.IntegrationTests.Tests;

/// <summary>
/// Integration tests for multi-tenant system.
/// Tests tenant creation, management, subscription updates, and tenant isolation.
/// </summary>
[Collection("IntegrationTests")]
public class MultiTenantIntegrationTests : IAsyncLifetime
{
    private readonly SharedIntegrationTestFixture _fixture;
    private HttpClient? _client;
    private AuthHelper? _authHelper;

    public MultiTenantIntegrationTests(SharedIntegrationTestFixture fixture)
    {
        _fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
    }

    public async Task InitializeAsync()
    {
        // Factory is already initialized by collection fixture
        // Just create client for each test
        _client = _fixture.Factory.CreateClient();
        _authHelper = new AuthHelper(_client);

        // Authenticate with admin role (required for tenant management)
        var token = await _authHelper.GetAdminTokenAsync();
        _authHelper.AddAuthorizationHeader(_client, token);
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        // Factory is disposed by collection fixture, not here
        await Task.CompletedTask;
    }

    #region Tenant Creation Tests

    /// <summary>
    /// Tests creating a new tenant with valid data.
    /// </summary>
    [Fact(Skip = "Multi-tenant API endpoints not yet implemented - return 404")]
    public async Task CreateTenant_WithValidData_ReturnsCreatedTenant()
    {
        // Arrange
        var request = new CreateTenantRequest
        {
            Name = "Test Corporation",
            Subdomain = "testcorp-" + Guid.NewGuid().ToString()[..8],
            Tier = SubscriptionTier.Starter,
            ContactEmail = "admin@testcorp.com",
            Metadata = new Dictionary<string, string>
            {
                ["Industry"] = "Technology",
                ["Size"] = "Medium"
            }
        };

        // Act
        var response = await _client!.PostAsJsonAsync("/api/tenants", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created, "Valid tenant should be created");

        var tenant = await response.Content.ReadFromJsonAsync<TenantResponse>();
        tenant.Should().NotBeNull();
        tenant!.TenantId.Should().NotBeEmpty();
        tenant.Name.Should().Be("Test Corporation");
        tenant.Subdomain.Should().Be(request.Subdomain);
        tenant.Tier.Should().Be(SubscriptionTier.Starter.ToString());
        tenant.Status.Should().Be("Active");
        tenant.ContactEmail.Should().Be("admin@testcorp.com");
        tenant.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    /// <summary>
    /// Tests creating a tenant without subdomain fails validation.
    /// </summary>
    [Fact(Skip = "Multi-tenant API endpoints not yet implemented - return 404")]
    public async Task CreateTenant_WithoutSubdomain_ReturnsBadRequest()
    {
        // Arrange
        var request = new CreateTenantRequest
        {
            Name = "Invalid Tenant",
            Subdomain = "", // Missing subdomain
            ContactEmail = "invalid@test.com"
        };

        // Act
        var response = await _client!.PostAsJsonAsync("/api/tenants", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "Tenant without subdomain should be rejected");
    }

    /// <summary>
    /// Tests creating multiple tenants with unique subdomains.
    /// </summary>
    [Fact(Skip = "Multi-tenant API endpoints not yet implemented - return 404")]
    public async Task CreateMultipleTenants_WithUniqueSubdomains_AllSucceed()
    {
        // Arrange & Act
        var tenants = new List<TenantResponse>();

        for (int i = 0; i < 3; i++)
        {
            var request = new CreateTenantRequest
            {
                Name = $"Tenant {i}",
                Subdomain = $"tenant{i}-{Guid.NewGuid().ToString()[..8]}",
                ContactEmail = $"admin{i}@tenant{i}.com"
            };

            var response = await _client!.PostAsJsonAsync("/api/tenants", request);
            response.StatusCode.Should().Be(HttpStatusCode.Created);

            var tenant = await response.Content.ReadFromJsonAsync<TenantResponse>();
            tenants.Add(tenant!);
        }

        // Assert
        tenants.Should().HaveCount(3);
        tenants.Select(t => t.TenantId).Distinct().Should().HaveCount(3, "Each tenant should have unique ID");
        tenants.Select(t => t.Subdomain).Distinct().Should().HaveCount(3, "Each tenant should have unique subdomain");
    }

    #endregion

    #region Tenant Retrieval Tests

    /// <summary>
    /// Tests retrieving a tenant by ID.
    /// </summary>
    [Fact(Skip = "Multi-tenant API endpoints not yet implemented - return 404")]
    public async Task GetTenant_ByValidId_ReturnsTenant()
    {
        // Arrange - Create a tenant
        var createRequest = new CreateTenantRequest
        {
            Name = "Retrieve Test Corp",
            Subdomain = "retrievetest-" + Guid.NewGuid().ToString()[..8],
            ContactEmail = "retrieve@test.com"
        };

        var createResponse = await _client!.PostAsJsonAsync("/api/tenants", createRequest);
        var createdTenant = await createResponse.Content.ReadFromJsonAsync<TenantResponse>();

        // Act - Retrieve the tenant
        var getResponse = await _client!.GetAsync($"/api/tenants/{createdTenant!.TenantId}");

        // Assert
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var retrievedTenant = await getResponse.Content.ReadFromJsonAsync<TenantResponse>();
        retrievedTenant.Should().NotBeNull();
        retrievedTenant!.TenantId.Should().Be(createdTenant.TenantId);
        retrievedTenant.Name.Should().Be("Retrieve Test Corp");
        retrievedTenant.Subdomain.Should().Be(createRequest.Subdomain);
    }

    /// <summary>
    /// Tests retrieving a non-existent tenant returns 404.
    /// </summary>
    [Fact(Skip = "Multi-tenant API endpoints not yet implemented - return 404")]
    public async Task GetTenant_WithNonExistentId_Returns404NotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await _client!.GetAsync($"/api/tenants/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Tests listing all tenants.
    /// </summary>
    [Fact(Skip = "Multi-tenant API endpoints not yet implemented - return 404")]
    public async Task ListTenants_ReturnsAllTenants()
    {
        // Arrange - Create a few tenants
        var tenantNames = new[] { "ListTest1", "ListTest2", "ListTest3" };

        foreach (var name in tenantNames)
        {
            var request = new CreateTenantRequest
            {
                Name = name,
                Subdomain = $"listtest-{name.ToLower()}-{Guid.NewGuid().ToString()[..8]}",
                ContactEmail = $"{name.ToLower()}@test.com"
            };

            await _client!.PostAsJsonAsync("/api/tenants", request);
        }

        // Give tenants time to be created
        await Task.Delay(TimeSpan.FromSeconds(1));

        // Act
        var response = await _client!.GetAsync("/api/tenants");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var tenants = await response.Content.ReadFromJsonAsync<List<TenantResponse>>();
        tenants.Should().NotBeNull();
        tenants!.Should().HaveCountGreaterOrEqualTo(3, "At least the 3 created tenants should be listed");
    }

    #endregion

    #region Tenant Update Tests

    /// <summary>
    /// Tests updating tenant information.
    /// </summary>
    [Fact(Skip = "Multi-tenant API endpoints not yet implemented - return 404")]
    public async Task UpdateTenant_WithValidData_UpdatesSuccessfully()
    {
        // Arrange - Create a tenant
        var createRequest = new CreateTenantRequest
        {
            Name = "Original Name",
            Subdomain = "updatetest-" + Guid.NewGuid().ToString()[..8],
            ContactEmail = "original@test.com"
        };

        var createResponse = await _client!.PostAsJsonAsync("/api/tenants", createRequest);
        var tenant = await createResponse.Content.ReadFromJsonAsync<TenantResponse>();

        // Prepare update
        var updateRequest = new UpdateTenantRequest
        {
            Name = "Updated Name",
            ContactEmail = "updated@test.com",
            Metadata = new Dictionary<string, string> { ["UpdatedBy"] = "IntegrationTest" }
        };

        // Act - Update the tenant
        var updateResponse = await _client!.PutAsJsonAsync($"/api/tenants/{tenant!.TenantId}", updateRequest);

        // Assert
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var updatedTenant = await updateResponse.Content.ReadFromJsonAsync<TenantResponse>();
        updatedTenant.Should().NotBeNull();
        updatedTenant!.Name.Should().Be("Updated Name");
        updatedTenant.ContactEmail.Should().Be("updated@test.com");
        updatedTenant.TenantId.Should().Be(tenant.TenantId, "Tenant ID should not change");
    }

    #endregion

    #region Subscription Management Tests

    /// <summary>
    /// Tests upgrading tenant subscription tier.
    /// </summary>
    [Fact(Skip = "Multi-tenant API endpoints not yet implemented - return 404")]
    public async Task UpdateSubscription_Upgrade_UpdatesTierSuccessfully()
    {
        // Arrange - Create tenant with Free tier
        var createRequest = new CreateTenantRequest
        {
            Name = "Subscription Test Corp",
            Subdomain = "subtest-" + Guid.NewGuid().ToString()[..8],
            ContactEmail = "sub@test.com",
            Tier = SubscriptionTier.Free
        };

        var createResponse = await _client!.PostAsJsonAsync("/api/tenants", createRequest);
        var tenant = await createResponse.Content.ReadFromJsonAsync<TenantResponse>();

        tenant!.Tier.Should().Be(SubscriptionTier.Free.ToString());

        // Prepare upgrade
        var upgradeRequest = new UpdateSubscriptionRequest
        {
            Tier = SubscriptionTier.Professional
        };

        // Act - Upgrade subscription
        var upgradeResponse = await _client!.PutAsJsonAsync(
            $"/api/tenants/{tenant.TenantId}/subscription",
            upgradeRequest);

        // Assert
        upgradeResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var updatedTenant = await upgradeResponse.Content.ReadFromJsonAsync<TenantResponse>();
        updatedTenant.Should().NotBeNull();
        updatedTenant!.Tier.Should().Be(SubscriptionTier.Professional.ToString(),
            "Tenant should be upgraded to Professional tier");
    }

    /// <summary>
    /// Tests downgrading tenant subscription tier.
    /// </summary>
    [Fact(Skip = "Multi-tenant API endpoints not yet implemented - return 404")]
    public async Task UpdateSubscription_Downgrade_UpdatesTierSuccessfully()
    {
        // Arrange - Create tenant with Premium tier
        var createRequest = new CreateTenantRequest
        {
            Name = "Downgrade Test Corp",
            Subdomain = "downgrade-" + Guid.NewGuid().ToString()[..8],
            ContactEmail = "downgrade@test.com",
            Tier = SubscriptionTier.Professional
        };

        var createResponse = await _client!.PostAsJsonAsync("/api/tenants", createRequest);
        var tenant = await createResponse.Content.ReadFromJsonAsync<TenantResponse>();

        // Prepare downgrade
        var downgradeRequest = new UpdateSubscriptionRequest
        {
            Tier = SubscriptionTier.Starter
        };

        // Act - Downgrade subscription
        var downgradeResponse = await _client!.PutAsJsonAsync(
            $"/api/tenants/{tenant!.TenantId}/subscription",
            downgradeRequest);

        // Assert
        downgradeResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var downgradedTenant = await downgradeResponse.Content.ReadFromJsonAsync<TenantResponse>();
        downgradedTenant!.Tier.Should().Be(SubscriptionTier.Starter.ToString());
    }

    #endregion

    #region Tenant Suspension Tests

    /// <summary>
    /// Tests suspending a tenant.
    /// </summary>
    [Fact(Skip = "Multi-tenant API endpoints not yet implemented - return 404")]
    public async Task SuspendTenant_ChangesStatusToSuspended()
    {
        // Arrange - Create tenant
        var createRequest = new CreateTenantRequest
        {
            Name = "Suspend Test Corp",
            Subdomain = "suspend-" + Guid.NewGuid().ToString()[..8],
            ContactEmail = "suspend@test.com"
        };

        var createResponse = await _client!.PostAsJsonAsync("/api/tenants", createRequest);
        var tenant = await createResponse.Content.ReadFromJsonAsync<TenantResponse>();

        tenant!.Status.Should().Be("Active");

        // Act - Suspend tenant
        var suspendResponse = await _client!.PostAsync($"/api/tenants/{tenant.TenantId}/suspend", null);

        // Assert
        suspendResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var suspendedTenant = await suspendResponse.Content.ReadFromJsonAsync<TenantResponse>();
        suspendedTenant.Should().NotBeNull();
        suspendedTenant!.Status.Should().Be("Suspended");
        suspendedTenant.SuspendedAt.Should().NotBeNull();
        suspendedTenant.SuspendedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    /// <summary>
    /// Tests reactivating a suspended tenant.
    /// </summary>
    [Fact(Skip = "Multi-tenant API endpoints not yet implemented - return 404")]
    public async Task ReactivateTenant_RestoresActiveStatus()
    {
        // Arrange - Create and suspend tenant
        var createRequest = new CreateTenantRequest
        {
            Name = "Reactivate Test Corp",
            Subdomain = "reactivate-" + Guid.NewGuid().ToString()[..8],
            ContactEmail = "reactivate@test.com"
        };

        var createResponse = await _client!.PostAsJsonAsync("/api/tenants", createRequest);
        var tenant = await createResponse.Content.ReadFromJsonAsync<TenantResponse>();

        await _client!.PostAsync($"/api/tenants/{tenant!.TenantId}/suspend", null);

        // Act - Reactivate tenant
        var reactivateResponse = await _client!.PostAsync($"/api/tenants/{tenant.TenantId}/activate", null);

        // Assert
        reactivateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var reactivatedTenant = await reactivateResponse.Content.ReadFromJsonAsync<TenantResponse>();
        reactivatedTenant!.Status.Should().Be("Active");
        reactivatedTenant.SuspendedAt.Should().BeNull("Reactivated tenant should not have suspended timestamp");
    }

    #endregion

    #region Authorization Tests

    /// <summary>
    /// Tests that tenant creation requires admin role.
    /// </summary>
    [Fact(Skip = "Multi-tenant API endpoints not yet implemented - return 404")]
    public async Task CreateTenant_WithDeployerRole_Returns403Forbidden()
    {
        // Arrange - Create deployer client
        var deployerClient = _fixture.Factory.CreateClient();
        var deployerAuthHelper = new AuthHelper(deployerClient);
        var deployerToken = await deployerAuthHelper.GetDeployerTokenAsync();
        deployerAuthHelper.AddAuthorizationHeader(deployerClient, deployerToken);

        var request = new CreateTenantRequest
        {
            Name = "Unauthorized Tenant",
            Subdomain = "unauthorized-" + Guid.NewGuid().ToString()[..8],
            ContactEmail = "unauth@test.com"
        };

        // Act
        var response = await deployerClient.PostAsJsonAsync("/api/tenants", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "Only Admin role should be able to create tenants");

        deployerClient.Dispose();
    }

    /// <summary>
    /// Tests that tenant management endpoints require authentication.
    /// </summary>
    [Fact(Skip = "Multi-tenant API endpoints not yet implemented - return 404")]
    public async Task ListTenants_WithoutAuthentication_Returns401Unauthorized()
    {
        // Arrange
        var unauthClient = _fixture.Factory.CreateClient();

        // Act
        var response = await unauthClient.GetAsync("/api/tenants");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        unauthClient.Dispose();
    }

    #endregion

    #region Tenant Isolation Tests

    /// <summary>
    /// Tests that tenants are isolated from each other (different IDs, subdomains).
    /// </summary>
    [Fact(Skip = "Multi-tenant API endpoints not yet implemented - return 404")]
    public async Task TenantIsolation_DifferentTenantsHaveUniqueDomains()
    {
        // Arrange & Act - Create two tenants
        var tenant1Request = new CreateTenantRequest
        {
            Name = "Tenant A",
            Subdomain = "tenant-a-" + Guid.NewGuid().ToString()[..8],
            ContactEmail = "tenanta@test.com"
        };

        var tenant2Request = new CreateTenantRequest
        {
            Name = "Tenant B",
            Subdomain = "tenant-b-" + Guid.NewGuid().ToString()[..8],
            ContactEmail = "tenantb@test.com"
        };

        var response1 = await _client!.PostAsJsonAsync("/api/tenants", tenant1Request);
        var response2 = await _client!.PostAsJsonAsync("/api/tenants", tenant2Request);

        var tenant1 = await response1.Content.ReadFromJsonAsync<TenantResponse>();
        var tenant2 = await response2.Content.ReadFromJsonAsync<TenantResponse>();

        // Assert - Tenants are isolated
        tenant1!.TenantId.Should().NotBe(tenant2!.TenantId);
        tenant1.Subdomain.Should().NotBe(tenant2.Subdomain);
        tenant1.Name.Should().Be("Tenant A");
        tenant2.Name.Should().Be("Tenant B");
    }

    #endregion
}
