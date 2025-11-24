using FluentAssertions;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Interfaces;
using HotSwap.Distributed.Infrastructure.Websites;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace HotSwap.Distributed.Tests.Websites;

public class ThemeServiceTests
{
    private readonly Mock<IWebsiteRepository> _mockWebsiteRepository;
    private readonly Mock<IThemeRepository> _mockThemeRepository;
    private readonly Mock<ILogger<ThemeService>> _mockLogger;
    private readonly ThemeService _service;
    private readonly Guid _websiteId;
    private readonly Guid _themeId;

    public ThemeServiceTests()
    {
        _mockWebsiteRepository = new Mock<IWebsiteRepository>();
        _mockThemeRepository = new Mock<IThemeRepository>();
        _mockLogger = new Mock<ILogger<ThemeService>>();
        _service = new ThemeService(
            _mockWebsiteRepository.Object,
            _mockThemeRepository.Object,
            _mockLogger.Object);
        _websiteId = Guid.NewGuid();
        _themeId = Guid.NewGuid();
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullWebsiteRepository_ShouldThrowArgumentNullException()
    {
        var action = () => new ThemeService(
            null!,
            _mockThemeRepository.Object,
            _mockLogger.Object);

        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("websiteRepository");
    }

    [Fact]
    public void Constructor_WithNullThemeRepository_ShouldThrowArgumentNullException()
    {
        var action = () => new ThemeService(
            _mockWebsiteRepository.Object,
            null!,
            _mockLogger.Object);

        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("themeRepository");
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        var action = () => new ThemeService(
            _mockWebsiteRepository.Object,
            _mockThemeRepository.Object,
            null!);

        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    #endregion

    #region InstallThemeAsync Tests

    [Fact]
    public async Task InstallThemeAsync_WithValidTheme_ShouldInstallSuccessfully()
    {
        var theme = CreateTestTheme();

        _mockThemeRepository
            .Setup(x => x.GetByIdAsync(_themeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(theme);

        var result = await _service.InstallThemeAsync(_websiteId, _themeId);

        result.Should().BeTrue();

        _mockThemeRepository.Verify(x => x.GetByIdAsync(
            _themeId,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InstallThemeAsync_WithNonExistentTheme_ShouldThrowKeyNotFoundException()
    {
        _mockThemeRepository
            .Setup(x => x.GetByIdAsync(_themeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Theme?)null);

        var action = async () => await _service.InstallThemeAsync(_websiteId, _themeId);

        await action.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage($"Theme {_themeId} not found");
    }

    [Fact]
    public async Task InstallThemeAsync_WithRepositoryException_ShouldThrowException()
    {
        _mockThemeRepository
            .Setup(x => x.GetByIdAsync(_themeId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database connection failed"));

        var action = async () => await _service.InstallThemeAsync(_websiteId, _themeId);

        await action.Should().ThrowAsync<Exception>()
            .WithMessage("Database connection failed");
    }

    #endregion

    #region ActivateThemeAsync Tests

    [Fact]
    public async Task ActivateThemeAsync_WithValidWebsiteAndTheme_ShouldActivateSuccessfully()
    {
        var website = CreateTestWebsite();
        var theme = CreateTestTheme();
        Website? updatedWebsite = null;

        _mockWebsiteRepository
            .Setup(x => x.GetByIdAsync(_websiteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(website);

        _mockThemeRepository
            .Setup(x => x.GetByIdAsync(_themeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(theme);

        _mockWebsiteRepository
            .Setup(x => x.UpdateAsync(It.IsAny<Website>(), It.IsAny<CancellationToken>()))
            .Callback<Website, CancellationToken>((w, _) => updatedWebsite = w)
            .ReturnsAsync((Website w, CancellationToken _) => w);

        var result = await _service.ActivateThemeAsync(_websiteId, _themeId);

        result.Should().BeTrue();
        updatedWebsite.Should().NotBeNull();
        updatedWebsite!.CurrentThemeId.Should().Be(_themeId);
        updatedWebsite.ThemeVersion.Should().Be(theme.Version);

        _mockWebsiteRepository.Verify(x => x.UpdateAsync(
            It.IsAny<Website>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ActivateThemeAsync_WithNonExistentWebsite_ShouldThrowKeyNotFoundException()
    {
        _mockWebsiteRepository
            .Setup(x => x.GetByIdAsync(_websiteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Website?)null);

        var action = async () => await _service.ActivateThemeAsync(_websiteId, _themeId);

        await action.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage($"Website {_websiteId} not found");

        _mockThemeRepository.Verify(x => x.GetByIdAsync(
            It.IsAny<Guid>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ActivateThemeAsync_WithNonExistentTheme_ShouldThrowKeyNotFoundException()
    {
        var website = CreateTestWebsite();

        _mockWebsiteRepository
            .Setup(x => x.GetByIdAsync(_websiteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(website);

        _mockThemeRepository
            .Setup(x => x.GetByIdAsync(_themeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Theme?)null);

        var action = async () => await _service.ActivateThemeAsync(_websiteId, _themeId);

        await action.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage($"Theme {_themeId} not found");

        _mockWebsiteRepository.Verify(x => x.UpdateAsync(
            It.IsAny<Website>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ActivateThemeAsync_ShouldUpdateWebsiteWithThemeVersionAtomically()
    {
        var website = CreateTestWebsite();
        website.CurrentThemeId = Guid.NewGuid(); // Different theme
        website.ThemeVersion = "0.0.1"; // Old version

        var theme = CreateTestTheme();
        Website? capturedWebsite = null;

        _mockWebsiteRepository
            .Setup(x => x.GetByIdAsync(_websiteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(website);

        _mockThemeRepository
            .Setup(x => x.GetByIdAsync(_themeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(theme);

        _mockWebsiteRepository
            .Setup(x => x.UpdateAsync(It.IsAny<Website>(), It.IsAny<CancellationToken>()))
            .Callback<Website, CancellationToken>((w, _) => capturedWebsite = w)
            .ReturnsAsync((Website w, CancellationToken _) => w);

        await _service.ActivateThemeAsync(_websiteId, _themeId);

        capturedWebsite.Should().NotBeNull();
        capturedWebsite!.CurrentThemeId.Should().Be(_themeId);
        capturedWebsite.ThemeVersion.Should().Be(theme.Version);
    }

    #endregion

    #region CustomizeThemeAsync Tests

    [Fact]
    public async Task CustomizeThemeAsync_WithValidCustomizations_ShouldStoreInConfiguration()
    {
        var website = CreateTestWebsite();
        var customizations = new Dictionary<string, string>
        {
            { "primary_color", "#ff0000" },
            { "font_family", "Arial" },
            { "logo_url", "https://example.com/logo.png" }
        };
        Website? updatedWebsite = null;

        _mockWebsiteRepository
            .Setup(x => x.GetByIdAsync(_websiteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(website);

        _mockWebsiteRepository
            .Setup(x => x.UpdateAsync(It.IsAny<Website>(), It.IsAny<CancellationToken>()))
            .Callback<Website, CancellationToken>((w, _) => updatedWebsite = w)
            .ReturnsAsync((Website w, CancellationToken _) => w);

        var result = await _service.CustomizeThemeAsync(_websiteId, customizations);

        result.Should().BeTrue();
        updatedWebsite.Should().NotBeNull();
        updatedWebsite!.Configuration.Should().ContainKey("theme_primary_color")
            .WhoseValue.Should().Be("#ff0000");
        updatedWebsite.Configuration.Should().ContainKey("theme_font_family")
            .WhoseValue.Should().Be("Arial");
        updatedWebsite.Configuration.Should().ContainKey("theme_logo_url")
            .WhoseValue.Should().Be("https://example.com/logo.png");

        _mockWebsiteRepository.Verify(x => x.UpdateAsync(
            It.IsAny<Website>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CustomizeThemeAsync_WithNonExistentWebsite_ShouldThrowKeyNotFoundException()
    {
        var customizations = new Dictionary<string, string>
        {
            { "primary_color", "#ff0000" }
        };

        _mockWebsiteRepository
            .Setup(x => x.GetByIdAsync(_websiteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Website?)null);

        var action = async () => await _service.CustomizeThemeAsync(_websiteId, customizations);

        await action.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage($"Website {_websiteId} not found");

        _mockWebsiteRepository.Verify(x => x.UpdateAsync(
            It.IsAny<Website>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CustomizeThemeAsync_WithEmptyCustomizations_ShouldNotAddAnyConfiguration()
    {
        var website = CreateTestWebsite();
        var customizations = new Dictionary<string, string>();
        Website? updatedWebsite = null;

        _mockWebsiteRepository
            .Setup(x => x.GetByIdAsync(_websiteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(website);

        _mockWebsiteRepository
            .Setup(x => x.UpdateAsync(It.IsAny<Website>(), It.IsAny<CancellationToken>()))
            .Callback<Website, CancellationToken>((w, _) => updatedWebsite = w)
            .ReturnsAsync((Website w, CancellationToken _) => w);

        var result = await _service.CustomizeThemeAsync(_websiteId, customizations);

        result.Should().BeTrue();
        updatedWebsite.Should().NotBeNull();
        updatedWebsite!.Configuration.Keys
            .Where(k => k.StartsWith("theme_"))
            .Should().BeEmpty();
    }

    [Fact]
    public async Task CustomizeThemeAsync_ShouldPrefixCustomizationKeysWithTheme()
    {
        var website = CreateTestWebsite();
        var customizations = new Dictionary<string, string>
        {
            { "color", "blue" }
        };
        Website? updatedWebsite = null;

        _mockWebsiteRepository
            .Setup(x => x.GetByIdAsync(_websiteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(website);

        _mockWebsiteRepository
            .Setup(x => x.UpdateAsync(It.IsAny<Website>(), It.IsAny<CancellationToken>()))
            .Callback<Website, CancellationToken>((w, _) => updatedWebsite = w)
            .ReturnsAsync((Website w, CancellationToken _) => w);

        await _service.CustomizeThemeAsync(_websiteId, customizations);

        updatedWebsite.Should().NotBeNull();
        updatedWebsite!.Configuration.Should().ContainKey("theme_color");
        updatedWebsite.Configuration.Should().NotContainKey("color");
    }

    [Fact]
    public async Task CustomizeThemeAsync_WithExistingCustomizations_ShouldOverwriteValues()
    {
        var website = CreateTestWebsite();
        website.Configuration["theme_color"] = "red";
        website.Configuration["theme_font"] = "Helvetica";

        var customizations = new Dictionary<string, string>
        {
            { "color", "blue" } // Should overwrite existing red
        };
        Website? updatedWebsite = null;

        _mockWebsiteRepository
            .Setup(x => x.GetByIdAsync(_websiteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(website);

        _mockWebsiteRepository
            .Setup(x => x.UpdateAsync(It.IsAny<Website>(), It.IsAny<CancellationToken>()))
            .Callback<Website, CancellationToken>((w, _) => updatedWebsite = w)
            .ReturnsAsync((Website w, CancellationToken _) => w);

        await _service.CustomizeThemeAsync(_websiteId, customizations);

        updatedWebsite.Should().NotBeNull();
        updatedWebsite!.Configuration["theme_color"].Should().Be("blue");
        updatedWebsite.Configuration["theme_font"].Should().Be("Helvetica"); // Unchanged
    }

    #endregion

    #region Helper Methods

    private Website CreateTestWebsite()
    {
        return new Website
        {
            WebsiteId = _websiteId,
            TenantId = Guid.NewGuid(),
            Name = "Test Website",
            Subdomain = "test-site",
            CurrentThemeId = Guid.Empty,
            ThemeVersion = null,
            Configuration = new Dictionary<string, string>(),
            CreatedAt = DateTime.UtcNow
        };
    }

    private Theme CreateTestTheme()
    {
        return new Theme
        {
            ThemeId = _themeId,
            Name = "Test Theme",
            Version = "1.2.3",
            Author = "Test Author",
            IsPublic = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    #endregion
}
