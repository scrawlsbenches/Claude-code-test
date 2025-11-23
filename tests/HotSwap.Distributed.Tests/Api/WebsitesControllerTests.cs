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

public class WebsitesControllerTests
{
    private readonly Mock<IWebsiteRepository> _mockWebsiteRepository;
    private readonly Mock<IWebsiteProvisioningService> _mockProvisioningService;
    private readonly Mock<ITenantContextService> _mockTenantContext;
    private readonly WebsitesController _controller;
    private readonly Guid _testTenantId = Guid.NewGuid();

    public WebsitesControllerTests()
    {
        _mockWebsiteRepository = new Mock<IWebsiteRepository>();
        _mockProvisioningService = new Mock<IWebsiteProvisioningService>();
        _mockTenantContext = new Mock<ITenantContextService>();

        _controller = new WebsitesController(
            _mockWebsiteRepository.Object,
            _mockProvisioningService.Object,
            _mockTenantContext.Object,
            NullLogger<WebsitesController>.Instance);
    }

    private Website CreateTestWebsite(Guid? websiteId = null, Guid? tenantId = null)
    {
        return new Website
        {
            WebsiteId = websiteId ?? Guid.NewGuid(),
            TenantId = tenantId ?? _testTenantId,
            Name = "Test Website",
            Subdomain = "test-website",
            CustomDomains = new List<string>(),
            Status = WebsiteStatus.Active,
            CurrentThemeId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow
        };
    }

    #region CreateWebsite Tests

    [Fact]
    public async Task CreateWebsite_WithValidRequest_ReturnsCreated()
    {
        // Arrange
        var request = new CreateWebsiteRequest
        {
            Name = "Test Website",
            Subdomain = "test-website"
        };

        var createdWebsite = CreateTestWebsite();

        _mockTenantContext.Setup(x => x.GetCurrentTenantId())
            .Returns(_testTenantId);

        _mockProvisioningService.Setup(x => x.ProvisionWebsiteAsync(
                It.IsAny<Website>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdWebsite);

        // Act
        var result = await _controller.CreateWebsite(request, CancellationToken.None);

        // Assert
        var createdResult = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.ActionName.Should().Be(nameof(WebsitesController.GetWebsite));
        var response = createdResult.Value.Should().BeOfType<WebsiteResponse>().Subject;
        response.WebsiteId.Should().Be(createdWebsite.WebsiteId);
        response.Name.Should().Be("Test Website");
    }

    [Fact]
    public async Task CreateWebsite_WithNoTenantContext_ReturnsBadRequest()
    {
        // Arrange
        var request = new CreateWebsiteRequest
        {
            Name = "Test Website",
            Subdomain = "test-website"
        };

        _mockTenantContext.Setup(x => x.GetCurrentTenantId())
            .Returns((Guid?)null);

        // Act
        var result = await _controller.CreateWebsite(request, CancellationToken.None);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var errorResponse = badRequestResult.Value.Should().BeOfType<ErrorResponse>().Subject;
        errorResponse.Error.Should().Be("Tenant context required");
    }

    [Fact]
    public async Task CreateWebsite_ConvertsSubdomainToLowercase()
    {
        // Arrange
        var request = new CreateWebsiteRequest
        {
            Name = "Test Website",
            Subdomain = "TEST-WEBSITE"
        };

        var createdWebsite = CreateTestWebsite();

        _mockTenantContext.Setup(x => x.GetCurrentTenantId())
            .Returns(_testTenantId);

        Website? capturedWebsite = null;
        _mockProvisioningService.Setup(x => x.ProvisionWebsiteAsync(
                It.IsAny<Website>(),
                It.IsAny<CancellationToken>()))
            .Callback<Website, CancellationToken>((w, ct) => capturedWebsite = w)
            .ReturnsAsync(createdWebsite);

        // Act
        await _controller.CreateWebsite(request, CancellationToken.None);

        // Assert
        capturedWebsite.Should().NotBeNull();
        capturedWebsite!.Subdomain.Should().Be("test-website");
    }

    [Fact]
    public async Task CreateWebsite_WithCustomDomains_IncludesDomains()
    {
        // Arrange
        var request = new CreateWebsiteRequest
        {
            Name = "Test Website",
            Subdomain = "test-website",
            CustomDomains = new List<string> { "example.com", "www.example.com" }
        };

        var createdWebsite = CreateTestWebsite();

        _mockTenantContext.Setup(x => x.GetCurrentTenantId())
            .Returns(_testTenantId);

        Website? capturedWebsite = null;
        _mockProvisioningService.Setup(x => x.ProvisionWebsiteAsync(
                It.IsAny<Website>(),
                It.IsAny<CancellationToken>()))
            .Callback<Website, CancellationToken>((w, ct) => capturedWebsite = w)
            .ReturnsAsync(createdWebsite);

        // Act
        await _controller.CreateWebsite(request, CancellationToken.None);

        // Assert
        capturedWebsite.Should().NotBeNull();
        capturedWebsite!.CustomDomains.Should().HaveCount(2);
        capturedWebsite.CustomDomains.Should().Contain("example.com");
        capturedWebsite.CustomDomains.Should().Contain("www.example.com");
    }

    [Fact]
    public async Task CreateWebsite_WithNoCustomDomains_UsesEmptyList()
    {
        // Arrange
        var request = new CreateWebsiteRequest
        {
            Name = "Test Website",
            Subdomain = "test-website",
            CustomDomains = null
        };

        var createdWebsite = CreateTestWebsite();

        _mockTenantContext.Setup(x => x.GetCurrentTenantId())
            .Returns(_testTenantId);

        Website? capturedWebsite = null;
        _mockProvisioningService.Setup(x => x.ProvisionWebsiteAsync(
                It.IsAny<Website>(),
                It.IsAny<CancellationToken>()))
            .Callback<Website, CancellationToken>((w, ct) => capturedWebsite = w)
            .ReturnsAsync(createdWebsite);

        // Act
        await _controller.CreateWebsite(request, CancellationToken.None);

        // Assert
        capturedWebsite.Should().NotBeNull();
        capturedWebsite!.CustomDomains.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateWebsite_WithThemeId_SetsTheme()
    {
        // Arrange
        var themeId = Guid.NewGuid();
        var request = new CreateWebsiteRequest
        {
            Name = "Test Website",
            Subdomain = "test-website",
            ThemeId = themeId
        };

        var createdWebsite = CreateTestWebsite();

        _mockTenantContext.Setup(x => x.GetCurrentTenantId())
            .Returns(_testTenantId);

        Website? capturedWebsite = null;
        _mockProvisioningService.Setup(x => x.ProvisionWebsiteAsync(
                It.IsAny<Website>(),
                It.IsAny<CancellationToken>()))
            .Callback<Website, CancellationToken>((w, ct) => capturedWebsite = w)
            .ReturnsAsync(createdWebsite);

        // Act
        await _controller.CreateWebsite(request, CancellationToken.None);

        // Assert
        capturedWebsite.Should().NotBeNull();
        capturedWebsite!.CurrentThemeId.Should().Be(themeId);
    }

    [Fact]
    public async Task CreateWebsite_WithNoThemeId_UsesEmptyGuid()
    {
        // Arrange
        var request = new CreateWebsiteRequest
        {
            Name = "Test Website",
            Subdomain = "test-website",
            ThemeId = null
        };

        var createdWebsite = CreateTestWebsite();

        _mockTenantContext.Setup(x => x.GetCurrentTenantId())
            .Returns(_testTenantId);

        Website? capturedWebsite = null;
        _mockProvisioningService.Setup(x => x.ProvisionWebsiteAsync(
                It.IsAny<Website>(),
                It.IsAny<CancellationToken>()))
            .Callback<Website, CancellationToken>((w, ct) => capturedWebsite = w)
            .ReturnsAsync(createdWebsite);

        // Act
        await _controller.CreateWebsite(request, CancellationToken.None);

        // Assert
        capturedWebsite.Should().NotBeNull();
        capturedWebsite!.CurrentThemeId.Should().Be(Guid.Empty);
    }

    #endregion

    #region GetWebsite Tests

    [Fact]
    public async Task GetWebsite_WithExistingWebsite_ReturnsOk()
    {
        // Arrange
        var websiteId = Guid.NewGuid();
        var website = CreateTestWebsite(websiteId);

        _mockWebsiteRepository.Setup(x => x.GetByIdAsync(websiteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(website);

        // Act
        var result = await _controller.GetWebsite(websiteId, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<WebsiteResponse>().Subject;
        response.WebsiteId.Should().Be(websiteId);
        response.Name.Should().Be("Test Website");
    }

    [Fact]
    public async Task GetWebsite_WithNonExistentWebsite_ReturnsNotFound()
    {
        // Arrange
        var websiteId = Guid.NewGuid();

        _mockWebsiteRepository.Setup(x => x.GetByIdAsync(websiteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Website?)null);

        // Act
        var result = await _controller.GetWebsite(websiteId, CancellationToken.None);

        // Assert
        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        var errorResponse = notFoundResult.Value.Should().BeOfType<ErrorResponse>().Subject;
        errorResponse.Error.Should().Be($"Website {websiteId} not found");
    }

    #endregion

    #region ListWebsites Tests

    [Fact]
    public async Task ListWebsites_WithValidTenantContext_ReturnsWebsites()
    {
        // Arrange
        var websites = new List<Website>
        {
            CreateTestWebsite(),
            CreateTestWebsite()
        };

        _mockTenantContext.Setup(x => x.GetCurrentTenantId())
            .Returns(_testTenantId);

        _mockWebsiteRepository.Setup(x => x.GetByTenantIdAsync(
                _testTenantId,
                It.IsAny<WebsiteStatus?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(websites);

        // Act
        var result = await _controller.ListWebsites(CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var responses = okResult.Value.Should().BeOfType<List<WebsiteResponse>>().Subject;
        responses.Should().HaveCount(2);
    }

    [Fact]
    public async Task ListWebsites_WithNoTenantContext_ReturnsBadRequest()
    {
        // Arrange
        _mockTenantContext.Setup(x => x.GetCurrentTenantId())
            .Returns((Guid?)null);

        // Act
        var result = await _controller.ListWebsites(CancellationToken.None);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var errorResponse = badRequestResult.Value.Should().BeOfType<ErrorResponse>().Subject;
        errorResponse.Error.Should().Be("Tenant context required");
    }

    [Fact]
    public async Task ListWebsites_WithNoWebsites_ReturnsEmptyList()
    {
        // Arrange
        _mockTenantContext.Setup(x => x.GetCurrentTenantId())
            .Returns(_testTenantId);

        _mockWebsiteRepository.Setup(x => x.GetByTenantIdAsync(
                _testTenantId,
                It.IsAny<WebsiteStatus?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Website>());

        // Act
        var result = await _controller.ListWebsites(CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var responses = okResult.Value.Should().BeOfType<List<WebsiteResponse>>().Subject;
        responses.Should().BeEmpty();
    }

    #endregion

    #region DeleteWebsite Tests

    [Fact]
    public async Task DeleteWebsite_WithExistingWebsite_ReturnsOk()
    {
        // Arrange
        var websiteId = Guid.NewGuid();

        _mockProvisioningService.Setup(x => x.DeprovisionWebsiteAsync(
                websiteId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.DeleteWebsite(websiteId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockProvisioningService.Verify(x => x.DeprovisionWebsiteAsync(
            websiteId,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteWebsite_WithNonExistentWebsite_ReturnsNotFound()
    {
        // Arrange
        var websiteId = Guid.NewGuid();

        _mockProvisioningService.Setup(x => x.DeprovisionWebsiteAsync(
                websiteId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.DeleteWebsite(websiteId, CancellationToken.None);

        // Assert
        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        var errorResponse = notFoundResult.Value.Should().BeOfType<ErrorResponse>().Subject;
        errorResponse.Error.Should().Be($"Website {websiteId} not found");
    }

    #endregion
}
