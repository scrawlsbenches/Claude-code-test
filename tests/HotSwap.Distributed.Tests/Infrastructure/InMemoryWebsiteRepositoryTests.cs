using FluentAssertions;
using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Websites;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HotSwap.Distributed.Tests.Infrastructure;

public class InMemoryWebsiteRepositoryTests
{
    private readonly InMemoryWebsiteRepository _repository;
    private readonly Guid _tenantId = Guid.NewGuid();

    public InMemoryWebsiteRepositoryTests()
    {
        _repository = new InMemoryWebsiteRepository(
            NullLogger<InMemoryWebsiteRepository>.Instance);
    }

    #region Create Tests

    [Fact]
    public async Task CreateAsync_WithValidWebsite_ShouldCreateWebsite()
    {
        // Arrange
        var website = CreateTestWebsite();

        // Act
        var created = await _repository.CreateAsync(website);

        // Assert
        created.Should().NotBeNull();
        created.WebsiteId.Should().NotBe(Guid.Empty);
        created.Name.Should().Be("Test Website");
        created.Subdomain.Should().Be("test-site");
        created.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task CreateAsync_WithEmptyWebsiteId_ShouldGenerateId()
    {
        // Arrange
        var website = CreateTestWebsite();
        website.WebsiteId = Guid.Empty;

        // Act
        var created = await _repository.CreateAsync(website);

        // Assert
        created.WebsiteId.Should().NotBe(Guid.Empty);
    }

    #endregion

    #region GetById Tests

    [Fact]
    public async Task GetByIdAsync_WithExistingWebsite_ShouldReturnWebsite()
    {
        // Arrange
        var website = CreateTestWebsite();
        var created = await _repository.CreateAsync(website);

        // Act
        var retrieved = await _repository.GetByIdAsync(created.WebsiteId);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.WebsiteId.Should().Be(created.WebsiteId);
        retrieved.Name.Should().Be(created.Name);
    }

    [Fact]
    public async Task GetByIdAsync_WithNonExistentWebsite_ShouldReturnNull()
    {
        // Arrange
        var websiteId = Guid.NewGuid();

        // Act
        var retrieved = await _repository.GetByIdAsync(websiteId);

        // Assert
        retrieved.Should().BeNull();
    }

    #endregion

    #region GetBySubdomain Tests

    [Fact]
    public async Task GetBySubdomainAsync_WithExistingSubdomain_ShouldReturnWebsite()
    {
        // Arrange
        var website = CreateTestWebsite();
        await _repository.CreateAsync(website);

        // Act
        var retrieved = await _repository.GetBySubdomainAsync(_tenantId, "test-site");

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.Subdomain.Should().Be("test-site");
    }

    [Fact]
    public async Task GetBySubdomainAsync_WithDifferentCase_ShouldReturnWebsite()
    {
        // Arrange
        var website = CreateTestWebsite();
        website.Subdomain = "test-site";
        await _repository.CreateAsync(website);

        // Act
        var retrieved = await _repository.GetBySubdomainAsync(_tenantId, "TEST-SITE");

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.Subdomain.Should().Be("test-site");
    }

    [Fact]
    public async Task GetBySubdomainAsync_WithDifferentTenant_ShouldReturnNull()
    {
        // Arrange
        var website = CreateTestWebsite();
        await _repository.CreateAsync(website);

        // Act
        var retrieved = await _repository.GetBySubdomainAsync(Guid.NewGuid(), "test-site");

        // Assert
        retrieved.Should().BeNull();
    }

    [Fact]
    public async Task GetBySubdomainAsync_WithNonExistentSubdomain_ShouldReturnNull()
    {
        // Act
        var retrieved = await _repository.GetBySubdomainAsync(_tenantId, "nonexistent");

        // Assert
        retrieved.Should().BeNull();
    }

    #endregion

    #region GetByTenantId Tests

    [Fact]
    public async Task GetByTenantIdAsync_WithNoFilter_ShouldReturnAllWebsitesForTenant()
    {
        // Arrange
        var website1 = CreateTestWebsite("Site 1", "site-1");
        var website2 = CreateTestWebsite("Site 2", "site-2");
        var website3 = CreateTestWebsite("Site 3", "site-3");
        website3.TenantId = Guid.NewGuid(); // Different tenant

        await _repository.CreateAsync(website1);
        await _repository.CreateAsync(website2);
        await _repository.CreateAsync(website3);

        // Act
        var websites = await _repository.GetByTenantIdAsync(_tenantId);

        // Assert
        websites.Should().HaveCount(2);
        websites.Should().Contain(w => w.Name == "Site 1");
        websites.Should().Contain(w => w.Name == "Site 2");
        websites.Should().NotContain(w => w.Name == "Site 3");
    }

    [Fact]
    public async Task GetByTenantIdAsync_WithStatusFilter_ShouldReturnFilteredWebsites()
    {
        // Arrange
        var website1 = CreateTestWebsite("Active 1", "active-1");
        website1.Status = WebsiteStatus.Active;

        var website2 = CreateTestWebsite("Suspended", "suspended");
        website2.Status = WebsiteStatus.Suspended;

        var website3 = CreateTestWebsite("Active 2", "active-2");
        website3.Status = WebsiteStatus.Active;

        await _repository.CreateAsync(website1);
        await _repository.CreateAsync(website2);
        await _repository.CreateAsync(website3);

        // Act
        var activeWebsites = await _repository.GetByTenantIdAsync(_tenantId, WebsiteStatus.Active);

        // Assert
        activeWebsites.Should().HaveCount(2);
        activeWebsites.Should().AllSatisfy(w => w.Status.Should().Be(WebsiteStatus.Active));
    }

    [Fact]
    public async Task GetByTenantIdAsync_WhenNoWebsites_ShouldReturnEmptyList()
    {
        // Act
        var websites = await _repository.GetByTenantIdAsync(_tenantId);

        // Assert
        websites.Should().BeEmpty();
    }

    #endregion

    #region Update Tests

    [Fact]
    public async Task UpdateAsync_WithExistingWebsite_ShouldUpdateWebsite()
    {
        // Arrange
        var website = CreateTestWebsite();
        var created = await _repository.CreateAsync(website);

        created.Name = "Updated Name";
        created.Status = WebsiteStatus.Suspended;

        // Act
        var updated = await _repository.UpdateAsync(created);

        // Assert
        updated.Name.Should().Be("Updated Name");
        updated.Status.Should().Be(WebsiteStatus.Suspended);
        updated.UpdatedAt.Should().NotBeNull();
        updated.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));

        var retrieved = await _repository.GetByIdAsync(created.WebsiteId);
        retrieved!.Name.Should().Be("Updated Name");
        retrieved.Status.Should().Be(WebsiteStatus.Suspended);
    }

    [Fact]
    public async Task UpdateAsync_WithNonExistentWebsite_ShouldThrowException()
    {
        // Arrange
        var website = CreateTestWebsite();
        website.WebsiteId = Guid.NewGuid();

        // Act
        Func<Task> act = async () => await _repository.UpdateAsync(website);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*not found*");
    }

    #endregion

    #region Delete Tests

    [Fact]
    public async Task DeleteAsync_WithExistingWebsite_ShouldSoftDelete()
    {
        // Arrange
        var website = CreateTestWebsite();
        var created = await _repository.CreateAsync(website);

        // Act
        var result = await _repository.DeleteAsync(created.WebsiteId);

        // Assert
        result.Should().BeTrue();

        var retrieved = await _repository.GetByIdAsync(created.WebsiteId);
        retrieved.Should().NotBeNull();
        retrieved!.Status.Should().Be(WebsiteStatus.Deleted);
        retrieved.UpdatedAt.Should().NotBeNull();
        retrieved.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task DeleteAsync_WithNonExistentWebsite_ShouldReturnFalse()
    {
        // Arrange
        var websiteId = Guid.NewGuid();

        // Act
        var result = await _repository.DeleteAsync(websiteId);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Subdomain Availability Tests

    [Fact]
    public async Task IsSubdomainAvailableAsync_WithAvailableSubdomain_ShouldReturnTrue()
    {
        // Act
        var isAvailable = await _repository.IsSubdomainAvailableAsync(_tenantId, "available-subdomain");

        // Assert
        isAvailable.Should().BeTrue();
    }

    [Fact]
    public async Task IsSubdomainAvailableAsync_WithTakenSubdomain_ShouldReturnFalse()
    {
        // Arrange
        var website = CreateTestWebsite();
        await _repository.CreateAsync(website);

        // Act
        var isAvailable = await _repository.IsSubdomainAvailableAsync(_tenantId, "test-site");

        // Assert
        isAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task IsSubdomainAvailableAsync_WithDifferentCase_ShouldReturnFalse()
    {
        // Arrange
        var website = CreateTestWebsite();
        website.Subdomain = "test-site";
        await _repository.CreateAsync(website);

        // Act
        var isAvailable = await _repository.IsSubdomainAvailableAsync(_tenantId, "TEST-SITE");

        // Assert
        isAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task IsSubdomainAvailableAsync_WithDifferentTenant_ShouldReturnTrue()
    {
        // Arrange
        var website = CreateTestWebsite();
        await _repository.CreateAsync(website);

        // Act - Same subdomain but different tenant
        var isAvailable = await _repository.IsSubdomainAvailableAsync(Guid.NewGuid(), "test-site");

        // Assert
        isAvailable.Should().BeTrue();
    }

    #endregion

    #region Helper Methods

    private Website CreateTestWebsite(string name = "Test Website", string subdomain = "test-site")
    {
        return new Website
        {
            WebsiteId = Guid.NewGuid(),
            TenantId = _tenantId,
            Name = name,
            Subdomain = subdomain,
            Status = WebsiteStatus.Active,
            CurrentThemeId = Guid.NewGuid()
        };
    }

    #endregion
}
