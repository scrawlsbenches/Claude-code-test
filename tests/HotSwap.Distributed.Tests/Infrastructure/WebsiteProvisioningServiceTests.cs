using FluentAssertions;
using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Interfaces;
using HotSwap.Distributed.Infrastructure.Websites;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace HotSwap.Distributed.Tests.Infrastructure;

public class WebsiteProvisioningServiceTests
{
    private readonly Mock<IWebsiteRepository> _mockWebsiteRepository;
    private readonly Mock<IThemeRepository> _mockThemeRepository;
    private readonly Mock<ILogger<WebsiteProvisioningService>> _mockLogger;
    private readonly WebsiteProvisioningService _service;

    public WebsiteProvisioningServiceTests()
    {
        _mockWebsiteRepository = new Mock<IWebsiteRepository>();
        _mockThemeRepository = new Mock<IThemeRepository>();
        _mockLogger = new Mock<ILogger<WebsiteProvisioningService>>();
        _service = new WebsiteProvisioningService(_mockWebsiteRepository.Object, _mockThemeRepository.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task ProvisionWebsiteAsync_WithValidWebsite_SuccessfullyProvisions()
    {
        // Arrange
        var website = CreateValidWebsite();
        var defaultTheme = new Theme
        {
            ThemeId = Guid.NewGuid(),
            Name = "Default Theme",
            Version = "1.0.0",
            Author = "Test Author"
        };

        _mockWebsiteRepository.Setup(r => r.CreateAsync(It.IsAny<Website>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Website w, CancellationToken _) => w);
        _mockWebsiteRepository.Setup(r => r.UpdateAsync(It.IsAny<Website>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Website w, CancellationToken _) => w);
        _mockThemeRepository.Setup(r => r.GetPublicThemesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Theme> { defaultTheme });

        // Act
        var result = await _service.ProvisionWebsiteAsync(website);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(WebsiteStatus.Active);
        result.CurrentThemeId.Should().Be(defaultTheme.ThemeId);
        result.ThemeVersion.Should().Be(defaultTheme.Version);

        _mockWebsiteRepository.Verify(r => r.CreateAsync(It.IsAny<Website>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockWebsiteRepository.Verify(r => r.UpdateAsync(It.IsAny<Website>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProvisionWebsiteAsync_WithInvalidWebsite_ThrowsInvalidOperationException()
    {
        // Arrange
        var website = new Website
        {
            WebsiteId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "", // Invalid: empty name
            Subdomain = "test",
            Status = WebsiteStatus.Provisioning
        };

        // Act
        var act = async () => await _service.ProvisionWebsiteAsync(website);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Website validation failed:*");
    }

    [Fact]
    public async Task ProvisionWebsiteAsync_WithoutDefaultTheme_KeepsExistingTheme()
    {
        // Arrange
        var website = CreateValidWebsite();
        website.CurrentThemeId = Guid.NewGuid();
        website.ThemeVersion = "1.0.0";

        _mockWebsiteRepository.Setup(r => r.CreateAsync(It.IsAny<Website>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Website w, CancellationToken _) => w);
        _mockWebsiteRepository.Setup(r => r.UpdateAsync(It.IsAny<Website>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Website w, CancellationToken _) => w);
        _mockThemeRepository.Setup(r => r.GetPublicThemesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Theme>());

        var originalThemeId = website.CurrentThemeId;

        // Act
        var result = await _service.ProvisionWebsiteAsync(website);

        // Assert
        result.CurrentThemeId.Should().Be(originalThemeId);
    }

    [Fact]
    public async Task DeprovisionWebsiteAsync_WithExistingWebsite_SuccessfullyDeprovisions()
    {
        // Arrange
        var websiteId = Guid.NewGuid();
        var website = CreateValidWebsite();
        website.WebsiteId = websiteId;

        _mockWebsiteRepository.Setup(r => r.GetByIdAsync(websiteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(website);
        _mockWebsiteRepository.Setup(r => r.DeleteAsync(websiteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _service.DeprovisionWebsiteAsync(websiteId);

        // Assert
        result.Should().BeTrue();
        _mockWebsiteRepository.Verify(r => r.DeleteAsync(websiteId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeprovisionWebsiteAsync_WithNonExistentWebsite_ReturnsFalse()
    {
        // Arrange
        var websiteId = Guid.NewGuid();
        _mockWebsiteRepository.Setup(r => r.GetByIdAsync(websiteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Website?)null);

        // Act
        var result = await _service.DeprovisionWebsiteAsync(websiteId);

        // Assert
        result.Should().BeFalse();
        _mockWebsiteRepository.Verify(r => r.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ValidateProvisioningAsync_WithValidWebsite_ReturnsSuccess()
    {
        // Arrange
        var website = CreateValidWebsite();

        // Act
        var result = await _service.ValidateProvisioningAsync(website);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateProvisioningAsync_WithMissingName_ReturnsFailure()
    {
        // Arrange
        var website = CreateValidWebsite();
        website.Name = "";

        // Act
        var result = await _service.ValidateProvisioningAsync(website);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Website name is required");
    }

    [Fact]
    public async Task ValidateProvisioningAsync_WithMissingSubdomain_ReturnsFailure()
    {
        // Arrange
        var website = CreateValidWebsite();
        website.Subdomain = "";

        // Act
        var result = await _service.ValidateProvisioningAsync(website);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Website subdomain is required");
    }

    [Fact]
    public async Task ValidateProvisioningAsync_WithInvalidSubdomainFormat_ReturnsFailure()
    {
        // Arrange
        var website = CreateValidWebsite();
        website.Subdomain = "Invalid_Subdomain";

        // Act
        var result = await _service.ValidateProvisioningAsync(website);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Subdomain must contain only lowercase letters, numbers, and hyphens");
    }

    [Fact]
    public async Task ValidateProvisioningAsync_WithEmptyTenantId_ReturnsFailure()
    {
        // Arrange
        var website = CreateValidWebsite();
        website.TenantId = Guid.Empty;

        // Act
        var result = await _service.ValidateProvisioningAsync(website);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Tenant ID is required");
    }

    [Fact]
    public void Constructor_WithNullWebsiteRepository_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new WebsiteProvisioningService(null!, _mockThemeRepository.Object, _mockLogger.Object);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("websiteRepository");
    }

    [Fact]
    public void Constructor_WithNullThemeRepository_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new WebsiteProvisioningService(_mockWebsiteRepository.Object, null!, _mockLogger.Object);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("themeRepository");
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new WebsiteProvisioningService(_mockWebsiteRepository.Object, _mockThemeRepository.Object, null!);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    private static Website CreateValidWebsite()
    {
        return new Website
        {
            WebsiteId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "Test Website",
            Subdomain = "test-website",
            Status = WebsiteStatus.Provisioning,
            CurrentThemeId = Guid.Empty
        };
    }
}
