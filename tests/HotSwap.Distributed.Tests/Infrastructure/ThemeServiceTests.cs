using FluentAssertions;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Interfaces;
using HotSwap.Distributed.Infrastructure.Websites;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace HotSwap.Distributed.Tests.Infrastructure;

public class ThemeServiceTests
{
    private readonly Mock<IWebsiteRepository> _mockWebsiteRepository;
    private readonly Mock<IThemeRepository> _mockThemeRepository;
    private readonly Mock<ILogger<ThemeService>> _mockLogger;
    private readonly ThemeService _service;

    public ThemeServiceTests()
    {
        _mockWebsiteRepository = new Mock<IWebsiteRepository>();
        _mockThemeRepository = new Mock<IThemeRepository>();
        _mockLogger = new Mock<ILogger<ThemeService>>();
        _service = new ThemeService(_mockWebsiteRepository.Object, _mockThemeRepository.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task InstallThemeAsync_WithValidTheme_InstallsSuccessfully()
    {
        // Arrange
        var websiteId = Guid.NewGuid();
        var themeId = Guid.NewGuid();
        var theme = new Theme
        {
            ThemeId = themeId,
            Name = "Modern Theme",
            Version = "1.5.0",
            Description = "A modern, responsive theme",
            Author = "Test Author"
        };

        _mockThemeRepository.Setup(r => r.GetByIdAsync(themeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(theme);

        // Act
        var result = await _service.InstallThemeAsync(websiteId, themeId);

        // Assert
        result.Should().BeTrue();
        _mockThemeRepository.Verify(r => r.GetByIdAsync(themeId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InstallThemeAsync_WithNonExistentTheme_ThrowsKeyNotFoundException()
    {
        // Arrange
        var websiteId = Guid.NewGuid();
        var themeId = Guid.NewGuid();

        _mockThemeRepository.Setup(r => r.GetByIdAsync(themeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Theme?)null);

        // Act
        var act = async () => await _service.InstallThemeAsync(websiteId, themeId);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage($"Theme {themeId} not found");
    }

    [Fact]
    public async Task ActivateThemeAsync_WithValidTheme_ActivatesSuccessfully()
    {
        // Arrange
        var websiteId = Guid.NewGuid();
        var themeId = Guid.NewGuid();
        var website = new Website
        {
            WebsiteId = websiteId,
            TenantId = Guid.NewGuid(),
            Name = "Test Website",
            Subdomain = "test",
            CurrentThemeId = Guid.NewGuid() // Old theme
        };
        var theme = new Theme
        {
            ThemeId = themeId,
            Name = "New Theme",
            Version = "2.0.0",
            Author = "Test Author"
        };

        _mockWebsiteRepository.Setup(r => r.GetByIdAsync(websiteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(website);
        _mockThemeRepository.Setup(r => r.GetByIdAsync(themeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(theme);
        _mockWebsiteRepository.Setup(r => r.UpdateAsync(It.IsAny<Website>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Website w, CancellationToken _) => w);

        // Act
        var result = await _service.ActivateThemeAsync(websiteId, themeId);

        // Assert
        result.Should().BeTrue();
        _mockWebsiteRepository.Verify(r => r.UpdateAsync(It.Is<Website>(w =>
            w.CurrentThemeId == themeId &&
            w.ThemeVersion == theme.Version), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ActivateThemeAsync_WithNonExistentWebsite_ThrowsKeyNotFoundException()
    {
        // Arrange
        var websiteId = Guid.NewGuid();
        var themeId = Guid.NewGuid();

        _mockWebsiteRepository.Setup(r => r.GetByIdAsync(websiteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Website?)null);

        // Act
        var act = async () => await _service.ActivateThemeAsync(websiteId, themeId);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage($"Website {websiteId} not found");
    }

    [Fact]
    public async Task ActivateThemeAsync_WithNonExistentTheme_ThrowsKeyNotFoundException()
    {
        // Arrange
        var websiteId = Guid.NewGuid();
        var themeId = Guid.NewGuid();
        var website = new Website
        {
            WebsiteId = websiteId,
            TenantId = Guid.NewGuid(),
            Name = "Test Website",
            Subdomain = "test"
        };

        _mockWebsiteRepository.Setup(r => r.GetByIdAsync(websiteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(website);
        _mockThemeRepository.Setup(r => r.GetByIdAsync(themeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Theme?)null);

        // Act
        var act = async () => await _service.ActivateThemeAsync(websiteId, themeId);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage($"Theme {themeId} not found");
    }

    [Fact]
    public async Task CustomizeThemeAsync_WithValidCustomizations_AppliesSuccessfully()
    {
        // Arrange
        var websiteId = Guid.NewGuid();
        var website = new Website
        {
            WebsiteId = websiteId,
            TenantId = Guid.NewGuid(),
            Name = "Test Website",
            Subdomain = "test",
            Configuration = new Dictionary<string, string>()
        };
        var customizations = new Dictionary<string, string>
        {
            { "primary_color", "#FF5733" },
            { "font_family", "Arial, sans-serif" },
            { "header_height", "80px" }
        };

        _mockWebsiteRepository.Setup(r => r.GetByIdAsync(websiteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(website);
        _mockWebsiteRepository.Setup(r => r.UpdateAsync(It.IsAny<Website>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Website w, CancellationToken _) => w);

        // Act
        var result = await _service.CustomizeThemeAsync(websiteId, customizations);

        // Assert
        result.Should().BeTrue();
        _mockWebsiteRepository.Verify(r => r.UpdateAsync(It.Is<Website>(w =>
            w.Configuration.ContainsKey("theme_primary_color") &&
            w.Configuration["theme_primary_color"] == "#FF5733" &&
            w.Configuration.ContainsKey("theme_font_family") &&
            w.Configuration["theme_font_family"] == "Arial, sans-serif" &&
            w.Configuration.ContainsKey("theme_header_height") &&
            w.Configuration["theme_header_height"] == "80px"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CustomizeThemeAsync_WithNonExistentWebsite_ThrowsKeyNotFoundException()
    {
        // Arrange
        var websiteId = Guid.NewGuid();
        var customizations = new Dictionary<string, string> { { "color", "blue" } };

        _mockWebsiteRepository.Setup(r => r.GetByIdAsync(websiteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Website?)null);

        // Act
        var act = async () => await _service.CustomizeThemeAsync(websiteId, customizations);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage($"Website {websiteId} not found");
    }

    [Fact]
    public void Constructor_WithNullWebsiteRepository_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new ThemeService(null!, _mockThemeRepository.Object, _mockLogger.Object);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("websiteRepository");
    }

    [Fact]
    public void Constructor_WithNullThemeRepository_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new ThemeService(_mockWebsiteRepository.Object, null!, _mockLogger.Object);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("themeRepository");
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new ThemeService(_mockWebsiteRepository.Object, _mockThemeRepository.Object, null!);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }
}
