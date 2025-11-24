using FluentAssertions;
using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Websites;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HotSwap.Distributed.Tests.Infrastructure;

public class InMemoryPluginRepositoryTests
{
    private readonly InMemoryPluginRepository _repository;

    public InMemoryPluginRepositoryTests()
    {
        _repository = new InMemoryPluginRepository(
            NullLogger<InMemoryPluginRepository>.Instance);
    }

    #region Create Tests

    [Fact]
    public async Task CreateAsync_WithValidPlugin_ShouldCreatePlugin()
    {
        // Arrange
        var plugin = CreateTestPlugin();

        // Act
        var created = await _repository.CreateAsync(plugin);

        // Assert
        created.Should().NotBeNull();
        created.PluginId.Should().NotBe(Guid.Empty);
        created.Name.Should().Be("Test Plugin");
        created.Version.Should().Be("1.0.0");
        created.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task CreateAsync_WithEmptyPluginId_ShouldGenerateId()
    {
        // Arrange
        var plugin = CreateTestPlugin();
        plugin.PluginId = Guid.Empty;

        // Act
        var created = await _repository.CreateAsync(plugin);

        // Assert
        created.PluginId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task CreateAsync_ShouldSetCreatedAtTimestamp()
    {
        // Arrange
        var plugin = CreateTestPlugin();
        var beforeCreation = DateTime.UtcNow;

        // Act
        var created = await _repository.CreateAsync(plugin);
        var afterCreation = DateTime.UtcNow;

        // Assert
        created.CreatedAt.Should().BeOnOrAfter(beforeCreation);
        created.CreatedAt.Should().BeOnOrBefore(afterCreation);
    }

    #endregion

    #region GetById Tests

    [Fact]
    public async Task GetByIdAsync_WithExistingPlugin_ShouldReturnPlugin()
    {
        // Arrange
        var plugin = CreateTestPlugin();
        var created = await _repository.CreateAsync(plugin);

        // Act
        var retrieved = await _repository.GetByIdAsync(created.PluginId);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.PluginId.Should().Be(created.PluginId);
        retrieved.Name.Should().Be(created.Name);
        retrieved.Version.Should().Be(created.Version);
    }

    [Fact]
    public async Task GetByIdAsync_WithNonExistentPlugin_ShouldReturnNull()
    {
        // Arrange
        var pluginId = Guid.NewGuid();

        // Act
        var retrieved = await _repository.GetByIdAsync(pluginId);

        // Assert
        retrieved.Should().BeNull();
    }

    #endregion

    #region GetPublicPlugins Tests

    [Fact]
    public async Task GetPublicPluginsAsync_ShouldReturnOnlyPublicPlugins()
    {
        // Arrange
        var publicPlugin1 = CreateTestPlugin("Public Plugin 1");
        publicPlugin1.IsPublic = true;

        var privatePlugin = CreateTestPlugin("Private Plugin");
        privatePlugin.IsPublic = false;

        var publicPlugin2 = CreateTestPlugin("Public Plugin 2");
        publicPlugin2.IsPublic = true;

        await _repository.CreateAsync(publicPlugin1);
        await _repository.CreateAsync(privatePlugin);
        await _repository.CreateAsync(publicPlugin2);

        // Act
        var publicPlugins = await _repository.GetPublicPluginsAsync();

        // Assert
        publicPlugins.Should().HaveCount(2);
        publicPlugins.Should().Contain(p => p.Name == "Public Plugin 1");
        publicPlugins.Should().Contain(p => p.Name == "Public Plugin 2");
        publicPlugins.Should().NotContain(p => p.Name == "Private Plugin");
    }

    [Fact]
    public async Task GetPublicPluginsAsync_WithCategoryFilter_ShouldReturnFilteredPlugins()
    {
        // Arrange
        var ecommercePlugin1 = CreateTestPlugin("Ecommerce Plugin 1");
        ecommercePlugin1.IsPublic = true;
        ecommercePlugin1.Category = PluginCategory.Ecommerce;

        var formsPlugin = CreateTestPlugin("Forms Plugin");
        formsPlugin.IsPublic = true;
        formsPlugin.Category = PluginCategory.Forms;

        var ecommercePlugin2 = CreateTestPlugin("Ecommerce Plugin 2");
        ecommercePlugin2.IsPublic = true;
        ecommercePlugin2.Category = PluginCategory.Ecommerce;

        await _repository.CreateAsync(ecommercePlugin1);
        await _repository.CreateAsync(formsPlugin);
        await _repository.CreateAsync(ecommercePlugin2);

        // Act
        var ecommercePlugins = await _repository.GetPublicPluginsAsync(PluginCategory.Ecommerce);

        // Assert
        ecommercePlugins.Should().HaveCount(2);
        ecommercePlugins.Should().AllSatisfy(p => p.Category.Should().Be(PluginCategory.Ecommerce));
        ecommercePlugins.Should().NotContain(p => p.Name == "Forms Plugin");
    }

    [Fact]
    public async Task GetPublicPluginsAsync_WithCategoryFilter_ShouldNotReturnPrivatePlugins()
    {
        // Arrange
        var publicEcommerce = CreateTestPlugin("Public Ecommerce");
        publicEcommerce.IsPublic = true;
        publicEcommerce.Category = PluginCategory.Ecommerce;

        var privateEcommerce = CreateTestPlugin("Private Ecommerce");
        privateEcommerce.IsPublic = false;
        privateEcommerce.Category = PluginCategory.Ecommerce;

        await _repository.CreateAsync(publicEcommerce);
        await _repository.CreateAsync(privateEcommerce);

        // Act
        var plugins = await _repository.GetPublicPluginsAsync(PluginCategory.Ecommerce);

        // Assert
        plugins.Should().HaveCount(1);
        plugins.Should().Contain(p => p.Name == "Public Ecommerce");
        plugins.Should().NotContain(p => p.Name == "Private Ecommerce");
    }

    [Fact]
    public async Task GetPublicPluginsAsync_WhenNoPublicPlugins_ShouldReturnEmptyList()
    {
        // Arrange
        var privatePlugin1 = CreateTestPlugin("Private 1");
        privatePlugin1.IsPublic = false;

        var privatePlugin2 = CreateTestPlugin("Private 2");
        privatePlugin2.IsPublic = false;

        await _repository.CreateAsync(privatePlugin1);
        await _repository.CreateAsync(privatePlugin2);

        // Act
        var publicPlugins = await _repository.GetPublicPluginsAsync();

        // Assert
        publicPlugins.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPublicPluginsAsync_WithMultipleCategories_ShouldReturnAllPublicPlugins()
    {
        // Arrange
        var analyticsPlugin = CreateTestPlugin("Analytics Plugin");
        analyticsPlugin.IsPublic = true;
        analyticsPlugin.Category = PluginCategory.Analytics;

        var seoPlugin = CreateTestPlugin("SEO Plugin");
        seoPlugin.IsPublic = true;
        seoPlugin.Category = PluginCategory.SEO;

        var securityPlugin = CreateTestPlugin("Security Plugin");
        securityPlugin.IsPublic = true;
        securityPlugin.Category = PluginCategory.Security;

        await _repository.CreateAsync(analyticsPlugin);
        await _repository.CreateAsync(seoPlugin);
        await _repository.CreateAsync(securityPlugin);

        // Act
        var allPublicPlugins = await _repository.GetPublicPluginsAsync();

        // Assert
        allPublicPlugins.Should().HaveCount(3);
        allPublicPlugins.Should().Contain(p => p.Category == PluginCategory.Analytics);
        allPublicPlugins.Should().Contain(p => p.Category == PluginCategory.SEO);
        allPublicPlugins.Should().Contain(p => p.Category == PluginCategory.Security);
    }

    [Fact]
    public async Task GetPublicPluginsAsync_WithNonExistentCategory_ShouldReturnEmptyList()
    {
        // Arrange
        var formsPlugin = CreateTestPlugin("Forms Plugin");
        formsPlugin.IsPublic = true;
        formsPlugin.Category = PluginCategory.Forms;

        await _repository.CreateAsync(formsPlugin);

        // Act - Search for Analytics category when only Forms exist
        var analyticsPlugins = await _repository.GetPublicPluginsAsync(PluginCategory.Analytics);

        // Assert
        analyticsPlugins.Should().BeEmpty();
    }

    #endregion

    #region Update Tests

    [Fact]
    public async Task UpdateAsync_WithExistingPlugin_ShouldUpdatePlugin()
    {
        // Arrange
        var plugin = CreateTestPlugin();
        var created = await _repository.CreateAsync(plugin);

        created.Name = "Updated Plugin Name";
        created.Version = "2.0.0";
        created.IsPublic = false;

        // Act
        var updated = await _repository.UpdateAsync(created);

        // Assert
        updated.Name.Should().Be("Updated Plugin Name");
        updated.Version.Should().Be("2.0.0");
        updated.IsPublic.Should().BeFalse();
        updated.UpdatedAt.Should().NotBeNull();
        updated.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));

        var retrieved = await _repository.GetByIdAsync(created.PluginId);
        retrieved!.Name.Should().Be("Updated Plugin Name");
        retrieved.Version.Should().Be("2.0.0");
    }

    [Fact]
    public async Task UpdateAsync_WithNonExistentPlugin_ShouldThrowException()
    {
        // Arrange
        var plugin = CreateTestPlugin();
        plugin.PluginId = Guid.NewGuid();

        // Act
        Func<Task> act = async () => await _repository.UpdateAsync(plugin);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task UpdateAsync_ShouldSetUpdatedAtTimestamp()
    {
        // Arrange
        var plugin = CreateTestPlugin();
        var created = await _repository.CreateAsync(plugin);
        var beforeUpdate = DateTime.UtcNow;

        // Act
        created.Description = "Updated description";
        var updated = await _repository.UpdateAsync(created);
        var afterUpdate = DateTime.UtcNow;

        // Assert
        updated.UpdatedAt.Should().NotBeNull();
        updated.UpdatedAt.Should().BeOnOrAfter(beforeUpdate);
        updated.UpdatedAt.Should().BeOnOrBefore(afterUpdate);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task UpdateAsync_PublicToPrivate_ShouldRemoveFromPublicList()
    {
        // Arrange
        var plugin = CreateTestPlugin("Test Public Plugin");
        plugin.IsPublic = true;
        var created = await _repository.CreateAsync(plugin);

        // Act - Make it private
        created.IsPublic = false;
        await _repository.UpdateAsync(created);

        // Assert
        var publicPlugins = await _repository.GetPublicPluginsAsync();
        publicPlugins.Should().NotContain(p => p.PluginId == created.PluginId);
    }

    [Fact]
    public async Task UpdateAsync_PrivateToPublic_ShouldAddToPublicList()
    {
        // Arrange
        var plugin = CreateTestPlugin("Test Private Plugin");
        plugin.IsPublic = false;
        var created = await _repository.CreateAsync(plugin);

        // Act - Make it public
        created.IsPublic = true;
        await _repository.UpdateAsync(created);

        // Assert
        var publicPlugins = await _repository.GetPublicPluginsAsync();
        publicPlugins.Should().Contain(p => p.PluginId == created.PluginId);
    }

    [Fact]
    public async Task UpdateAsync_ChangingCategory_ShouldReflectInFilteredQueries()
    {
        // Arrange
        var plugin = CreateTestPlugin("Category Test Plugin");
        plugin.IsPublic = true;
        plugin.Category = PluginCategory.Forms;
        var created = await _repository.CreateAsync(plugin);

        // Act - Change category
        created.Category = PluginCategory.Analytics;
        await _repository.UpdateAsync(created);

        // Assert
        var formsPlugins = await _repository.GetPublicPluginsAsync(PluginCategory.Forms);
        formsPlugins.Should().NotContain(p => p.PluginId == created.PluginId);

        var analyticsPlugins = await _repository.GetPublicPluginsAsync(PluginCategory.Analytics);
        analyticsPlugins.Should().Contain(p => p.PluginId == created.PluginId);
    }

    #endregion

    #region Helper Methods

    private Plugin CreateTestPlugin(string name = "Test Plugin")
    {
        return new Plugin
        {
            PluginId = Guid.NewGuid(),
            Name = name,
            Version = "1.0.0",
            Author = "Test Author",
            IsPublic = true,
            Category = PluginCategory.Custom,
            Description = "A test plugin",
            Dependencies = new List<string>(),
            Manifest = new PluginManifest
            {
                Name = name,
                Version = "1.0.0",
                RequiredPermissions = new List<string> { "read", "write" },
                DefaultSettings = new Dictionary<string, object>(),
                Hooks = new List<HookRegistration>(),
                ApiEndpoints = new List<ApiEndpoint>()
            }
        };
    }

    #endregion
}
