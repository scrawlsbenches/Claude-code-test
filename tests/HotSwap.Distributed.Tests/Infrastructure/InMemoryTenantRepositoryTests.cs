using FluentAssertions;
using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Tenants;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HotSwap.Distributed.Tests.Infrastructure;

public class InMemoryTenantRepositoryTests
{
    private readonly InMemoryTenantRepository _repository;

    public InMemoryTenantRepositoryTests()
    {
        _repository = new InMemoryTenantRepository(
            NullLogger<InMemoryTenantRepository>.Instance);
    }

    #region Create Tests

    [Fact]
    public async Task CreateAsync_WithValidTenant_ShouldCreateTenant()
    {
        // Arrange
        var tenant = CreateTestTenant();

        // Act
        var created = await _repository.CreateAsync(tenant);

        // Assert
        created.Should().NotBeNull();
        created.TenantId.Should().NotBe(Guid.Empty);
        created.Name.Should().Be("Test Tenant");
        created.Subdomain.Should().Be("test-tenant");
        created.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task CreateAsync_WithEmptyTenantId_ShouldGenerateId()
    {
        // Arrange
        var tenant = CreateTestTenant();
        tenant.TenantId = Guid.Empty;

        // Act
        var created = await _repository.CreateAsync(tenant);

        // Assert
        created.TenantId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task CreateAsync_WithDuplicateSubdomain_ShouldThrowException()
    {
        // Arrange
        var tenant1 = CreateTestTenant();
        var tenant2 = CreateTestTenant();
        tenant2.TenantId = Guid.NewGuid();

        await _repository.CreateAsync(tenant1);

        // Act
        Func<Task> act = async () => await _repository.CreateAsync(tenant2);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*subdomain*already exists*");
    }

    [Fact]
    public async Task CreateAsync_WithNullTenant_ShouldThrowException()
    {
        // Act
        Func<Task> act = async () => await _repository.CreateAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task CreateAsync_WithDifferentCaseSubdomain_ShouldThrowException()
    {
        // Arrange
        var tenant1 = CreateTestTenant();
        tenant1.Subdomain = "test-tenant";

        var tenant2 = CreateTestTenant();
        tenant2.TenantId = Guid.NewGuid();
        tenant2.Subdomain = "TEST-TENANT"; // Different case

        await _repository.CreateAsync(tenant1);

        // Act
        Func<Task> act = async () => await _repository.CreateAsync(tenant2);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    #endregion

    #region GetById Tests

    [Fact]
    public async Task GetByIdAsync_WithExistingTenant_ShouldReturnTenant()
    {
        // Arrange
        var tenant = CreateTestTenant();
        var created = await _repository.CreateAsync(tenant);

        // Act
        var retrieved = await _repository.GetByIdAsync(created.TenantId);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.TenantId.Should().Be(created.TenantId);
        retrieved.Name.Should().Be(created.Name);
    }

    [Fact]
    public async Task GetByIdAsync_WithNonExistentTenant_ShouldReturnNull()
    {
        // Arrange
        var tenantId = Guid.NewGuid();

        // Act
        var retrieved = await _repository.GetByIdAsync(tenantId);

        // Assert
        retrieved.Should().BeNull();
    }

    #endregion

    #region GetBySubdomain Tests

    [Fact]
    public async Task GetBySubdomainAsync_WithExistingSubdomain_ShouldReturnTenant()
    {
        // Arrange
        var tenant = CreateTestTenant();
        await _repository.CreateAsync(tenant);

        // Act
        var retrieved = await _repository.GetBySubdomainAsync("test-tenant");

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.Subdomain.Should().Be("test-tenant");
    }

    [Fact]
    public async Task GetBySubdomainAsync_WithDifferentCase_ShouldReturnTenant()
    {
        // Arrange
        var tenant = CreateTestTenant();
        tenant.Subdomain = "test-tenant";
        await _repository.CreateAsync(tenant);

        // Act
        var retrieved = await _repository.GetBySubdomainAsync("TEST-TENANT");

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.Subdomain.Should().Be("test-tenant");
    }

    [Fact]
    public async Task GetBySubdomainAsync_WithNonExistentSubdomain_ShouldReturnNull()
    {
        // Act
        var retrieved = await _repository.GetBySubdomainAsync("nonexistent");

        // Assert
        retrieved.Should().BeNull();
    }

    [Fact]
    public async Task GetBySubdomainAsync_WithNullSubdomain_ShouldReturnNull()
    {
        // Act
        var retrieved = await _repository.GetBySubdomainAsync(null!);

        // Assert
        retrieved.Should().BeNull();
    }

    [Fact]
    public async Task GetBySubdomainAsync_WithEmptySubdomain_ShouldReturnNull()
    {
        // Act
        var retrieved = await _repository.GetBySubdomainAsync(string.Empty);

        // Assert
        retrieved.Should().BeNull();
    }

    [Fact]
    public async Task GetBySubdomainAsync_WithWhitespaceSubdomain_ShouldReturnNull()
    {
        // Act
        var retrieved = await _repository.GetBySubdomainAsync("   ");

        // Assert
        retrieved.Should().BeNull();
    }

    #endregion

    #region GetAll Tests

    [Fact]
    public async Task GetAllAsync_WithNoFilter_ShouldReturnAllTenants()
    {
        // Arrange
        var tenant1 = CreateTestTenant("Tenant 1", "tenant-1");
        var tenant2 = CreateTestTenant("Tenant 2", "tenant-2");
        var tenant3 = CreateTestTenant("Tenant 3", "tenant-3");

        await _repository.CreateAsync(tenant1);
        await _repository.CreateAsync(tenant2);
        await _repository.CreateAsync(tenant3);

        // Act
        var tenants = await _repository.GetAllAsync();

        // Assert
        tenants.Should().HaveCount(3);
        tenants.Should().Contain(t => t.Name == "Tenant 1");
        tenants.Should().Contain(t => t.Name == "Tenant 2");
        tenants.Should().Contain(t => t.Name == "Tenant 3");
    }

    [Fact]
    public async Task GetAllAsync_WithStatusFilter_ShouldReturnFilteredTenants()
    {
        // Arrange
        var tenant1 = CreateTestTenant("Active 1", "active-1");
        tenant1.Status = TenantStatus.Active;

        var tenant2 = CreateTestTenant("Suspended", "suspended");
        tenant2.Status = TenantStatus.Suspended;

        var tenant3 = CreateTestTenant("Active 2", "active-2");
        tenant3.Status = TenantStatus.Active;

        await _repository.CreateAsync(tenant1);
        await _repository.CreateAsync(tenant2);
        await _repository.CreateAsync(tenant3);

        // Act
        var activeTenants = await _repository.GetAllAsync(TenantStatus.Active);

        // Assert
        activeTenants.Should().HaveCount(2);
        activeTenants.Should().AllSatisfy(t => t.Status.Should().Be(TenantStatus.Active));
    }

    [Fact]
    public async Task GetAllAsync_WhenNoTenants_ShouldReturnEmptyList()
    {
        // Act
        var tenants = await _repository.GetAllAsync();

        // Assert
        tenants.Should().BeEmpty();
    }

    #endregion

    #region Update Tests

    [Fact]
    public async Task UpdateAsync_WithExistingTenant_ShouldUpdateTenant()
    {
        // Arrange
        var tenant = CreateTestTenant();
        var created = await _repository.CreateAsync(tenant);

        created.Name = "Updated Name";
        created.Status = TenantStatus.Suspended;

        // Act
        var updated = await _repository.UpdateAsync(created);

        // Assert
        updated.Name.Should().Be("Updated Name");
        updated.Status.Should().Be(TenantStatus.Suspended);

        var retrieved = await _repository.GetByIdAsync(created.TenantId);
        retrieved!.Name.Should().Be("Updated Name");
        retrieved.Status.Should().Be(TenantStatus.Suspended);
    }

    [Fact]
    public async Task UpdateAsync_WithNonExistentTenant_ShouldThrowException()
    {
        // Arrange
        var tenant = CreateTestTenant();
        tenant.TenantId = Guid.NewGuid();

        // Act
        Func<Task> act = async () => await _repository.UpdateAsync(tenant);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task UpdateAsync_WithNullTenant_ShouldThrowException()
    {
        // Act
        Func<Task> act = async () => await _repository.UpdateAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion

    #region Delete Tests

    [Fact]
    public async Task DeleteAsync_WithExistingTenant_ShouldSoftDelete()
    {
        // Arrange
        var tenant = CreateTestTenant();
        var created = await _repository.CreateAsync(tenant);
        var beforeDelete = DateTime.UtcNow;

        // Act
        var result = await _repository.DeleteAsync(created.TenantId);
        var afterDelete = DateTime.UtcNow;

        // Assert
        result.Should().BeTrue();

        var retrieved = await _repository.GetByIdAsync(created.TenantId);
        retrieved.Should().NotBeNull();
        retrieved!.Status.Should().Be(TenantStatus.Deleted);
        retrieved.DeletedAt.Should().NotBeNull();
        retrieved.DeletedAt.Should().BeOnOrAfter(beforeDelete);
        retrieved.DeletedAt.Should().BeOnOrBefore(afterDelete);
    }

    [Fact]
    public async Task DeleteAsync_WithNonExistentTenant_ShouldReturnFalse()
    {
        // Arrange
        var tenantId = Guid.NewGuid();

        // Act
        var result = await _repository.DeleteAsync(tenantId);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Subdomain Availability Tests

    [Fact]
    public async Task IsSubdomainAvailableAsync_WithAvailableSubdomain_ShouldReturnTrue()
    {
        // Act
        var isAvailable = await _repository.IsSubdomainAvailableAsync("available-subdomain");

        // Assert
        isAvailable.Should().BeTrue();
    }

    [Fact]
    public async Task IsSubdomainAvailableAsync_WithTakenSubdomain_ShouldReturnFalse()
    {
        // Arrange
        var tenant = CreateTestTenant();
        await _repository.CreateAsync(tenant);

        // Act
        var isAvailable = await _repository.IsSubdomainAvailableAsync("test-tenant");

        // Assert
        isAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task IsSubdomainAvailableAsync_WithDifferentCase_ShouldReturnFalse()
    {
        // Arrange
        var tenant = CreateTestTenant();
        tenant.Subdomain = "test-tenant";
        await _repository.CreateAsync(tenant);

        // Act
        var isAvailable = await _repository.IsSubdomainAvailableAsync("TEST-TENANT");

        // Assert
        isAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task IsSubdomainAvailableAsync_WithNullSubdomain_ShouldReturnFalse()
    {
        // Act
        var isAvailable = await _repository.IsSubdomainAvailableAsync(null!);

        // Assert
        isAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task IsSubdomainAvailableAsync_WithEmptySubdomain_ShouldReturnFalse()
    {
        // Act
        var isAvailable = await _repository.IsSubdomainAvailableAsync(string.Empty);

        // Assert
        isAvailable.Should().BeFalse();
    }

    #endregion

    #region Transaction Tests

    [Fact]
    public async Task BeginTransactionAsync_ShouldReturnNoOpTransaction()
    {
        // Act
        var transaction = await _repository.BeginTransactionAsync();

        // Assert
        transaction.Should().NotBeNull();

        // Should not throw when disposed
        await transaction.DisposeAsync();
    }

    #endregion

    #region Helper Methods

    private Tenant CreateTestTenant(string name = "Test Tenant", string subdomain = "test-tenant")
    {
        return new Tenant
        {
            TenantId = Guid.NewGuid(),
            Name = name,
            Subdomain = subdomain,
            Status = TenantStatus.Active,
            Tier = SubscriptionTier.Professional,
            ResourceQuota = new ResourceQuota
            {
                MaxWebsites = 10,
                StorageQuotaGB = 100,
                BandwidthQuotaGB = 1000
            }
        };
    }

    #endregion
}
