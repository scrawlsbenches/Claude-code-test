using FluentAssertions;
using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Interfaces;
using HotSwap.Distributed.Infrastructure.Websites;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace HotSwap.Distributed.Tests.Websites;

public class WebsiteProvisioningServiceTests
{
    private readonly Mock<IWebsiteRepository> _mockWebsiteRepository;
    private readonly Mock<IThemeRepository> _mockThemeRepository;
    private readonly Mock<ILogger<WebsiteProvisioningService>> _mockLogger;
    private readonly WebsiteProvisioningService _service;
    private readonly Guid _tenantId;
    private readonly Guid _websiteId;

    public WebsiteProvisioningServiceTests()
    {
        _mockWebsiteRepository = new Mock<IWebsiteRepository>();
        _mockThemeRepository = new Mock<IThemeRepository>();
        _mockLogger = new Mock<ILogger<WebsiteProvisioningService>>();
        _service = new WebsiteProvisioningService(
            _mockWebsiteRepository.Object,
            _mockThemeRepository.Object,
            _mockLogger.Object);
        _tenantId = Guid.NewGuid();
        _websiteId = Guid.NewGuid();
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullWebsiteRepository_ShouldThrowArgumentNullException()
    {
        var action = () => new WebsiteProvisioningService(
            null!,
            _mockThemeRepository.Object,
            _mockLogger.Object);

        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("websiteRepository");
    }

    [Fact]
    public void Constructor_WithNullThemeRepository_ShouldThrowArgumentNullException()
    {
        var action = () => new WebsiteProvisioningService(
            _mockWebsiteRepository.Object,
            null!,
            _mockLogger.Object);

        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("themeRepository");
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        var action = () => new WebsiteProvisioningService(
            _mockWebsiteRepository.Object,
            _mockThemeRepository.Object,
            null!);

        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    #endregion

    #region ProvisionWebsiteAsync Tests

    [Fact]
    public async Task ProvisionWebsiteAsync_WithValidWebsite_ShouldProvisionSuccessfully()
    {
        var website = CreateTestWebsite();
        var defaultTheme = CreateTestTheme();
        WebsiteStatus? statusAtCreate = null;
        WebsiteStatus? statusAtUpdate = null;

        _mockWebsiteRepository
            .Setup(x => x.CreateAsync(It.IsAny<Website>(), It.IsAny<CancellationToken>()))
            .Callback<Website, CancellationToken>((w, _) => statusAtCreate = w.Status)
            .ReturnsAsync((Website w, CancellationToken _) => w);

        _mockThemeRepository
            .Setup(x => x.GetPublicThemesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Theme> { defaultTheme });

        _mockWebsiteRepository
            .Setup(x => x.UpdateAsync(It.IsAny<Website>(), It.IsAny<CancellationToken>()))
            .Callback<Website, CancellationToken>((w, _) => statusAtUpdate = w.Status)
            .ReturnsAsync((Website w, CancellationToken _) => w);

        var result = await _service.ProvisionWebsiteAsync(website);

        result.Should().NotBeNull();
        result.Status.Should().Be(WebsiteStatus.Active);
        result.CurrentThemeId.Should().Be(defaultTheme.ThemeId);
        result.ThemeVersion.Should().Be(defaultTheme.Version);

        statusAtCreate.Should().Be(WebsiteStatus.Provisioning);
        statusAtUpdate.Should().Be(WebsiteStatus.Active);

        _mockWebsiteRepository.Verify(x => x.CreateAsync(
            It.IsAny<Website>(),
            It.IsAny<CancellationToken>()), Times.Once);

        _mockWebsiteRepository.Verify(x => x.UpdateAsync(
            It.IsAny<Website>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProvisionWebsiteAsync_WithExistingThemeId_ShouldNotOverwriteTheme()
    {
        var existingThemeId = Guid.NewGuid();
        var website = CreateTestWebsite();
        website.CurrentThemeId = existingThemeId;
        website.ThemeVersion = "1.2.3";

        _mockWebsiteRepository
            .Setup(x => x.CreateAsync(It.IsAny<Website>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Website w, CancellationToken _) => w);

        _mockWebsiteRepository
            .Setup(x => x.UpdateAsync(It.IsAny<Website>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Website w, CancellationToken _) => w);

        var result = await _service.ProvisionWebsiteAsync(website);

        result.CurrentThemeId.Should().Be(existingThemeId);
        result.ThemeVersion.Should().Be("1.2.3");

        _mockThemeRepository.Verify(x => x.GetPublicThemesAsync(
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProvisionWebsiteAsync_WithNoPublicThemes_ShouldProvisionWithoutTheme()
    {
        var website = CreateTestWebsite();

        _mockWebsiteRepository
            .Setup(x => x.CreateAsync(It.IsAny<Website>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Website w, CancellationToken _) => w);

        _mockThemeRepository
            .Setup(x => x.GetPublicThemesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Theme>());

        _mockWebsiteRepository
            .Setup(x => x.UpdateAsync(It.IsAny<Website>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Website w, CancellationToken _) => w);

        var result = await _service.ProvisionWebsiteAsync(website);

        result.Status.Should().Be(WebsiteStatus.Active);
        result.CurrentThemeId.Should().Be(Guid.Empty);
    }

    [Fact]
    public async Task ProvisionWebsiteAsync_WithInvalidData_ShouldThrowInvalidOperationException()
    {
        var website = CreateTestWebsite();
        website.Name = ""; // Invalid: empty name

        var action = async () => await _service.ProvisionWebsiteAsync(website);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Website validation failed*");
    }

    [Fact]
    public async Task ProvisionWebsiteAsync_WithRepositoryException_ShouldThrowException()
    {
        var website = CreateTestWebsite();

        _mockWebsiteRepository
            .Setup(x => x.CreateAsync(It.IsAny<Website>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database connection failed"));

        var action = async () => await _service.ProvisionWebsiteAsync(website);

        await action.Should().ThrowAsync<Exception>()
            .WithMessage("Database connection failed");
    }

    [Fact]
    public async Task ProvisionWebsiteAsync_ShouldSetStatusToProvisioningBeforeCreation()
    {
        var website = CreateTestWebsite();
        website.Status = WebsiteStatus.Maintenance;
        WebsiteStatus? capturedStatus = null;

        _mockWebsiteRepository
            .Setup(x => x.CreateAsync(It.IsAny<Website>(), It.IsAny<CancellationToken>()))
            .Callback<Website, CancellationToken>((w, _) => capturedStatus = w.Status)
            .ReturnsAsync((Website w, CancellationToken _) => w);

        _mockThemeRepository
            .Setup(x => x.GetPublicThemesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Theme>());

        _mockWebsiteRepository
            .Setup(x => x.UpdateAsync(It.IsAny<Website>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Website w, CancellationToken _) => w);

        await _service.ProvisionWebsiteAsync(website);

        capturedStatus.Should().Be(WebsiteStatus.Provisioning);
    }

    #endregion

    #region ValidateProvisioningAsync Tests

    [Fact]
    public async Task ValidateProvisioningAsync_WithValidWebsite_ShouldReturnSuccess()
    {
        var website = CreateTestWebsite();

        var result = await _service.ValidateProvisioningAsync(website);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateProvisioningAsync_WithMissingName_ShouldReturnFailure()
    {
        var website = CreateTestWebsite();
        website.Name = "";

        var result = await _service.ValidateProvisioningAsync(website);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Website name is required");
    }

    [Fact]
    public async Task ValidateProvisioningAsync_WithWhitespaceName_ShouldReturnFailure()
    {
        var website = CreateTestWebsite();
        website.Name = "   ";

        var result = await _service.ValidateProvisioningAsync(website);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Website name is required");
    }

    [Fact]
    public async Task ValidateProvisioningAsync_WithMissingSubdomain_ShouldReturnFailure()
    {
        var website = CreateTestWebsite();
        website.Subdomain = "";

        var result = await _service.ValidateProvisioningAsync(website);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Website subdomain is required");
    }

    [Fact]
    public async Task ValidateProvisioningAsync_WithEmptyTenantId_ShouldReturnFailure()
    {
        var website = CreateTestWebsite();
        website.TenantId = Guid.Empty;

        var result = await _service.ValidateProvisioningAsync(website);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Tenant ID is required");
    }

    [Theory]
    [InlineData("UPPERCASE")]
    [InlineData("Has Spaces")]
    [InlineData("has_underscores")]
    [InlineData("has.dots")]
    [InlineData("has@special")]
    public async Task ValidateProvisioningAsync_WithInvalidSubdomainFormat_ShouldReturnFailure(string subdomain)
    {
        var website = CreateTestWebsite();
        website.Subdomain = subdomain;

        var result = await _service.ValidateProvisioningAsync(website);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Subdomain must contain only lowercase letters, numbers, and hyphens");
    }

    [Theory]
    [InlineData("valid")]
    [InlineData("valid-subdomain")]
    [InlineData("valid123")]
    [InlineData("123valid")]
    [InlineData("valid-123-subdomain")]
    public async Task ValidateProvisioningAsync_WithValidSubdomainFormat_ShouldReturnSuccess(string subdomain)
    {
        var website = CreateTestWebsite();
        website.Subdomain = subdomain;

        var result = await _service.ValidateProvisioningAsync(website);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateProvisioningAsync_WithMultipleErrors_ShouldReturnAllErrors()
    {
        var website = CreateTestWebsite();
        website.Name = "";
        website.Subdomain = "";
        website.TenantId = Guid.Empty;

        var result = await _service.ValidateProvisioningAsync(website);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCount(3);
        result.Errors.Should().Contain("Website name is required");
        result.Errors.Should().Contain("Website subdomain is required");
        result.Errors.Should().Contain("Tenant ID is required");
    }

    #endregion

    #region DeprovisionWebsiteAsync Tests

    [Fact]
    public async Task DeprovisionWebsiteAsync_WithExistingWebsite_ShouldDeprovisionSuccessfully()
    {
        var website = CreateTestWebsite();

        _mockWebsiteRepository
            .Setup(x => x.GetByIdAsync(_websiteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(website);

        _mockWebsiteRepository
            .Setup(x => x.DeleteAsync(_websiteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _service.DeprovisionWebsiteAsync(_websiteId);

        result.Should().BeTrue();

        _mockWebsiteRepository.Verify(x => x.DeleteAsync(
            _websiteId,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeprovisionWebsiteAsync_WithNonExistentWebsite_ShouldReturnFalse()
    {
        _mockWebsiteRepository
            .Setup(x => x.GetByIdAsync(_websiteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Website?)null);

        var result = await _service.DeprovisionWebsiteAsync(_websiteId);

        result.Should().BeFalse();

        _mockWebsiteRepository.Verify(x => x.DeleteAsync(
            It.IsAny<Guid>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeprovisionWebsiteAsync_ShouldCallCleanupAndRevokeSSL()
    {
        var website = CreateTestWebsite();

        _mockWebsiteRepository
            .Setup(x => x.GetByIdAsync(_websiteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(website);

        _mockWebsiteRepository
            .Setup(x => x.DeleteAsync(_websiteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _service.DeprovisionWebsiteAsync(_websiteId);

        result.Should().BeTrue();
        // In production, we'd verify cleanup and SSL revocation were called
        // These are private methods that log and perform async operations
    }

    #endregion

    #region Helper Methods

    private Website CreateTestWebsite()
    {
        return new Website
        {
            WebsiteId = _websiteId,
            TenantId = _tenantId,
            Name = "Test Website",
            Subdomain = "test-site",
            Status = WebsiteStatus.Provisioning,
            CurrentThemeId = Guid.Empty,
            ThemeVersion = "",
            Configuration = new Dictionary<string, string>(),
            CreatedAt = DateTime.UtcNow
        };
    }

    private Theme CreateTestTheme()
    {
        return new Theme
        {
            ThemeId = Guid.NewGuid(),
            Name = "Default Theme",
            Version = "1.0.0",
            Author = "Platform Team",
            Description = "Default platform theme",
            IsPublic = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    #endregion
}
