using FluentAssertions;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Websites;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HotSwap.Distributed.Tests.Infrastructure;

public class InMemoryThemeRepositoryTests
{
    private readonly InMemoryThemeRepository _repository;

    public InMemoryThemeRepositoryTests()
    {
        _repository = new InMemoryThemeRepository(
            NullLogger<InMemoryThemeRepository>.Instance);
    }

    #region Initialization Tests

    [Fact]
    public async Task Constructor_ShouldInitializeDefaultTheme()
    {
        // Act
        var defaultThemeId = Guid.Parse("10000000-0000-0000-0000-000000000001");
        var theme = await _repository.GetByIdAsync(defaultThemeId);

        // Assert
        theme.Should().NotBeNull();
        theme!.Name.Should().Be("Default Theme");
        theme.Version.Should().Be("1.0.0");
        theme.Author.Should().Be("Platform Team");
        theme.IsPublic.Should().BeTrue();
        theme.Manifest.Should().NotBeNull();
        theme.Manifest.Name.Should().Be("Default Theme");
    }

    [Fact]
    public async Task Constructor_DefaultTheme_ShouldHaveManifestWithTemplates()
    {
        // Act
        var defaultThemeId = Guid.Parse("10000000-0000-0000-0000-000000000001");
        var theme = await _repository.GetByIdAsync(defaultThemeId);

        // Assert
        theme!.Manifest.Templates.Should().NotBeEmpty();
        theme.Manifest.Templates.Should().Contain("index.html");
        theme.Manifest.Templates.Should().Contain("page.html");
        theme.Manifest.Templates.Should().Contain("post.html");
    }

    [Fact]
    public async Task Constructor_DefaultTheme_ShouldHaveStylesheetsAndScripts()
    {
        // Act
        var defaultThemeId = Guid.Parse("10000000-0000-0000-0000-000000000001");
        var theme = await _repository.GetByIdAsync(defaultThemeId);

        // Assert
        theme!.Manifest.Stylesheets.Should().Contain("style.css");
        theme.Manifest.Scripts.Should().Contain("main.js");
    }

    #endregion

    #region Create Tests

    [Fact]
    public async Task CreateAsync_WithValidTheme_ShouldCreateTheme()
    {
        // Arrange
        var theme = CreateTestTheme();

        // Act
        var created = await _repository.CreateAsync(theme);

        // Assert
        created.Should().NotBeNull();
        created.ThemeId.Should().NotBe(Guid.Empty);
        created.Name.Should().Be("Test Theme");
        created.Version.Should().Be("1.0.0");
        created.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task CreateAsync_WithEmptyThemeId_ShouldGenerateId()
    {
        // Arrange
        var theme = CreateTestTheme();
        theme.ThemeId = Guid.Empty;

        // Act
        var created = await _repository.CreateAsync(theme);

        // Assert
        created.ThemeId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task CreateAsync_ShouldSetCreatedAtTimestamp()
    {
        // Arrange
        var theme = CreateTestTheme();
        var beforeCreation = DateTime.UtcNow;

        // Act
        var created = await _repository.CreateAsync(theme);
        var afterCreation = DateTime.UtcNow;

        // Assert
        created.CreatedAt.Should().BeOnOrAfter(beforeCreation);
        created.CreatedAt.Should().BeOnOrBefore(afterCreation);
    }

    #endregion

    #region GetById Tests

    [Fact]
    public async Task GetByIdAsync_WithExistingTheme_ShouldReturnTheme()
    {
        // Arrange
        var theme = CreateTestTheme();
        var created = await _repository.CreateAsync(theme);

        // Act
        var retrieved = await _repository.GetByIdAsync(created.ThemeId);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.ThemeId.Should().Be(created.ThemeId);
        retrieved.Name.Should().Be(created.Name);
        retrieved.Version.Should().Be(created.Version);
    }

    [Fact]
    public async Task GetByIdAsync_WithNonExistentTheme_ShouldReturnNull()
    {
        // Arrange
        var themeId = Guid.NewGuid();

        // Act
        var retrieved = await _repository.GetByIdAsync(themeId);

        // Assert
        retrieved.Should().BeNull();
    }

    #endregion

    #region GetPublicThemes Tests

    [Fact]
    public async Task GetPublicThemesAsync_ShouldReturnOnlyPublicThemes()
    {
        // Arrange
        var publicTheme1 = CreateTestTheme("Public Theme 1");
        publicTheme1.IsPublic = true;

        var privateTheme = CreateTestTheme("Private Theme");
        privateTheme.IsPublic = false;

        var publicTheme2 = CreateTestTheme("Public Theme 2");
        publicTheme2.IsPublic = true;

        await _repository.CreateAsync(publicTheme1);
        await _repository.CreateAsync(privateTheme);
        await _repository.CreateAsync(publicTheme2);

        // Act
        var publicThemes = await _repository.GetPublicThemesAsync();

        // Assert
        publicThemes.Should().HaveCountGreaterThanOrEqualTo(3); // Includes default theme
        publicThemes.Should().Contain(t => t.Name == "Public Theme 1");
        publicThemes.Should().Contain(t => t.Name == "Public Theme 2");
        publicThemes.Should().Contain(t => t.Name == "Default Theme"); // From initialization
        publicThemes.Should().NotContain(t => t.Name == "Private Theme");
    }

    [Fact]
    public async Task GetPublicThemesAsync_ShouldIncludeDefaultTheme()
    {
        // Act
        var publicThemes = await _repository.GetPublicThemesAsync();

        // Assert
        publicThemes.Should().Contain(t => t.Name == "Default Theme");
    }

    [Fact]
    public async Task GetPublicThemesAsync_WhenOnlyPrivateThemesExist_ShouldReturnOnlyDefaultTheme()
    {
        // Arrange
        var privateTheme1 = CreateTestTheme("Private 1");
        privateTheme1.IsPublic = false;

        var privateTheme2 = CreateTestTheme("Private 2");
        privateTheme2.IsPublic = false;

        await _repository.CreateAsync(privateTheme1);
        await _repository.CreateAsync(privateTheme2);

        // Act
        var publicThemes = await _repository.GetPublicThemesAsync();

        // Assert
        publicThemes.Should().HaveCount(1); // Only default theme
        publicThemes.Should().Contain(t => t.Name == "Default Theme");
    }

    #endregion

    #region Update Tests

    [Fact]
    public async Task UpdateAsync_WithExistingTheme_ShouldUpdateTheme()
    {
        // Arrange
        var theme = CreateTestTheme();
        var created = await _repository.CreateAsync(theme);

        created.Name = "Updated Theme Name";
        created.Version = "2.0.0";
        created.IsPublic = false;

        // Act
        var updated = await _repository.UpdateAsync(created);

        // Assert
        updated.Name.Should().Be("Updated Theme Name");
        updated.Version.Should().Be("2.0.0");
        updated.IsPublic.Should().BeFalse();
        updated.UpdatedAt.Should().NotBeNull();
        updated.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));

        var retrieved = await _repository.GetByIdAsync(created.ThemeId);
        retrieved!.Name.Should().Be("Updated Theme Name");
        retrieved.Version.Should().Be("2.0.0");
    }

    [Fact]
    public async Task UpdateAsync_WithNonExistentTheme_ShouldThrowException()
    {
        // Arrange
        var theme = CreateTestTheme();
        theme.ThemeId = Guid.NewGuid();

        // Act
        Func<Task> act = async () => await _repository.UpdateAsync(theme);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task UpdateAsync_ShouldSetUpdatedAtTimestamp()
    {
        // Arrange
        var theme = CreateTestTheme();
        var created = await _repository.CreateAsync(theme);
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
        var theme = CreateTestTheme("Test Public Theme");
        theme.IsPublic = true;
        var created = await _repository.CreateAsync(theme);

        // Act - Make it private
        created.IsPublic = false;
        await _repository.UpdateAsync(created);

        // Assert
        var publicThemes = await _repository.GetPublicThemesAsync();
        publicThemes.Should().NotContain(t => t.ThemeId == created.ThemeId);
    }

    [Fact]
    public async Task UpdateAsync_PrivateToPublic_ShouldAddToPublicList()
    {
        // Arrange
        var theme = CreateTestTheme("Test Private Theme");
        theme.IsPublic = false;
        var created = await _repository.CreateAsync(theme);

        // Act - Make it public
        created.IsPublic = true;
        await _repository.UpdateAsync(created);

        // Assert
        var publicThemes = await _repository.GetPublicThemesAsync();
        publicThemes.Should().Contain(t => t.ThemeId == created.ThemeId);
    }

    #endregion

    #region Helper Methods

    private Theme CreateTestTheme(string name = "Test Theme")
    {
        return new Theme
        {
            ThemeId = Guid.NewGuid(),
            Name = name,
            Version = "1.0.0",
            Author = "Test Author",
            IsPublic = true,
            Description = "A test theme",
            Manifest = new ThemeManifest
            {
                Name = name,
                Version = "1.0.0",
                Templates = new List<string> { "index.html", "page.html" },
                Stylesheets = new List<string> { "style.css" },
                Scripts = new List<string> { "app.js" }
            }
        };
    }

    #endregion
}
