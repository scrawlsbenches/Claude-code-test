using FluentAssertions;
using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Deployments;
using HotSwap.Distributed.Infrastructure.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace HotSwap.Distributed.Tests.Infrastructure;

public class TenantDeploymentServiceTests
{
    private readonly Mock<IWebsiteRepository> _mockWebsiteRepository;
    private readonly Mock<IThemeService> _mockThemeService;
    private readonly Mock<IPluginService> _mockPluginService;
    private readonly Mock<IQuotaService> _mockQuotaService;
    private readonly Mock<ILogger<TenantDeploymentService>> _mockLogger;
    private readonly TenantDeploymentService _service;

    public TenantDeploymentServiceTests()
    {
        _mockWebsiteRepository = new Mock<IWebsiteRepository>();
        _mockThemeService = new Mock<IThemeService>();
        _mockPluginService = new Mock<IPluginService>();
        _mockQuotaService = new Mock<IQuotaService>();
        _mockLogger = new Mock<ILogger<TenantDeploymentService>>();

        _service = new TenantDeploymentService(
            _mockWebsiteRepository.Object,
            _mockThemeService.Object,
            _mockPluginService.Object,
            _mockQuotaService.Object,
            _mockLogger.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullWebsiteRepository_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new TenantDeploymentService(
            null!,
            _mockThemeService.Object,
            _mockPluginService.Object,
            _mockQuotaService.Object,
            _mockLogger.Object);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("websiteRepository");
    }

    [Fact]
    public void Constructor_WithNullThemeService_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new TenantDeploymentService(
            _mockWebsiteRepository.Object,
            null!,
            _mockPluginService.Object,
            _mockQuotaService.Object,
            _mockLogger.Object);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("themeService");
    }

    [Fact]
    public void Constructor_WithNullPluginService_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new TenantDeploymentService(
            _mockWebsiteRepository.Object,
            _mockThemeService.Object,
            null!,
            _mockQuotaService.Object,
            _mockLogger.Object);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("pluginService");
    }

    [Fact]
    public void Constructor_WithNullQuotaService_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new TenantDeploymentService(
            _mockWebsiteRepository.Object,
            _mockThemeService.Object,
            _mockPluginService.Object,
            null!,
            _mockLogger.Object);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("quotaService");
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new TenantDeploymentService(
            _mockWebsiteRepository.Object,
            _mockThemeService.Object,
            _mockPluginService.Object,
            _mockQuotaService.Object,
            null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    #endregion

    #region DeployAsync Tests

    [Fact]
    public async Task DeployAsync_WhenQuotaExceeded_ReturnsFailureResult()
    {
        // Arrange
        var request = CreateDeploymentRequest(WebsiteModuleType.Theme, DeploymentScope.AllTenantWebsites);
        _mockQuotaService.Setup(q => q.CheckQuotaAsync(
            request.TenantId,
            ResourceType.ConcurrentDeployments,
            1L,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _service.DeployAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Message.Should().Be("Concurrent deployment quota exceeded");
        result.Errors.Should().Contain("Maximum concurrent deployments reached for this tenant");

        _mockQuotaService.Verify(q => q.RecordUsageAsync(
            It.IsAny<Guid>(),
            It.IsAny<ResourceType>(),
            It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeployAsync_WhenNoWebsitesFound_ReturnsFailureResult()
    {
        // Arrange
        var request = CreateDeploymentRequest(WebsiteModuleType.Theme, DeploymentScope.AllTenantWebsites);
        _mockQuotaService.Setup(q => q.CheckQuotaAsync(
            It.IsAny<Guid>(),
            It.IsAny<ResourceType>(),
            It.IsAny<long>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _mockWebsiteRepository.Setup(r => r.GetByTenantIdAsync(
            request.TenantId,
            null,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Website>());

        // Act
        var result = await _service.DeployAsync(request);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Be("No websites found for deployment");
        result.Errors.Should().Contain("Deployment scope did not match any websites");
    }

    [Fact]
    public async Task DeployAsync_ThemeToSingleWebsite_SuccessfullyDeploys()
    {
        // Arrange
        var themeId = Guid.NewGuid();
        var websiteId = Guid.NewGuid();
        var website = CreateWebsite(websiteId);

        var request = CreateDeploymentRequest(WebsiteModuleType.Theme, DeploymentScope.SingleWebsite);
        request.WebsiteId = websiteId;
        request.Metadata["themeId"] = themeId.ToString();

        // Setup quota and recording
        _mockQuotaService.Setup(q => q.CheckQuotaAsync(
            request.TenantId,
            ResourceType.ConcurrentDeployments,
            1L,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockQuotaService.Setup(q => q.RecordUsageAsync(
            request.TenantId,
            ResourceType.ConcurrentDeployments,
            1L,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _mockWebsiteRepository.Setup(r => r.GetByIdAsync(websiteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(website);
        _mockThemeService.Setup(t => t.ActivateThemeAsync(websiteId, themeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _service.DeployAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Message.Should().Be("Theme deployed to 1/1 websites");
        result.AffectedWebsites.Should().ContainSingle()
            .Which.Should().Be(websiteId);
        result.Errors.Should().BeEmpty();

        _mockThemeService.Verify(t => t.ActivateThemeAsync(websiteId, themeId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeployAsync_ThemeToAllTenantWebsites_SuccessfullyDeploys()
    {
        // Arrange
        var themeId = Guid.NewGuid();
        var websites = new List<Website>
        {
            CreateWebsite(Guid.NewGuid()),
            CreateWebsite(Guid.NewGuid()),
            CreateWebsite(Guid.NewGuid())
        };

        var request = CreateDeploymentRequest(WebsiteModuleType.Theme, DeploymentScope.AllTenantWebsites);
        request.Metadata["themeId"] = themeId.ToString();

        SetupSuccessfulDeployment(websites);
        _mockWebsiteRepository.Setup(r => r.GetByTenantIdAsync(
            request.TenantId,
            null,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(websites);

        foreach (var website in websites)
        {
            _mockThemeService.Setup(t => t.ActivateThemeAsync(
                website.WebsiteId,
                themeId,
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
        }

        // Act
        var result = await _service.DeployAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        result.Message.Should().Be("Theme deployed to 3/3 websites");
        result.AffectedWebsites.Should().HaveCount(3);
        result.Errors.Should().BeEmpty();

        _mockThemeService.Verify(t => t.ActivateThemeAsync(
            It.IsAny<Guid>(),
            themeId,
            It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Fact]
    public async Task DeployAsync_ThemeWithoutThemeId_ReturnsFailure()
    {
        // Arrange
        var websites = new List<Website> { CreateWebsite(Guid.NewGuid()) };
        var request = CreateDeploymentRequest(WebsiteModuleType.Theme, DeploymentScope.AllTenantWebsites);
        // Not setting themeId in metadata

        SetupSuccessfulDeployment(websites);
        _mockWebsiteRepository.Setup(r => r.GetByTenantIdAsync(
            request.TenantId,
            null,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(websites);

        // Act
        var result = await _service.DeployAsync(request);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Be("Theme ID not provided in metadata");
    }

    [Fact]
    public async Task DeployAsync_PluginToAllWebsites_SuccessfullyDeploys()
    {
        // Arrange
        var pluginId = Guid.NewGuid();
        var websites = new List<Website>
        {
            CreateWebsite(Guid.NewGuid()),
            CreateWebsite(Guid.NewGuid())
        };

        var request = CreateDeploymentRequest(WebsiteModuleType.Plugin, DeploymentScope.AllTenantWebsites);
        request.Metadata["pluginId"] = pluginId.ToString();

        SetupSuccessfulDeployment(websites);
        _mockWebsiteRepository.Setup(r => r.GetByTenantIdAsync(
            request.TenantId,
            null,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(websites);

        foreach (var website in websites)
        {
            _mockPluginService.Setup(p => p.ActivatePluginAsync(
                website.WebsiteId,
                pluginId,
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
        }

        // Act
        var result = await _service.DeployAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        result.Message.Should().Be("Plugin deployed to 2/2 websites");
        result.AffectedWebsites.Should().HaveCount(2);

        _mockPluginService.Verify(p => p.ActivatePluginAsync(
            It.IsAny<Guid>(),
            pluginId,
            It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task DeployAsync_PluginWithoutPluginId_ReturnsFailure()
    {
        // Arrange
        var websites = new List<Website> { CreateWebsite(Guid.NewGuid()) };
        var request = CreateDeploymentRequest(WebsiteModuleType.Plugin, DeploymentScope.AllTenantWebsites);
        // Not setting pluginId in metadata

        SetupSuccessfulDeployment(websites);
        _mockWebsiteRepository.Setup(r => r.GetByTenantIdAsync(
            request.TenantId,
            null,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(websites);

        // Act
        var result = await _service.DeployAsync(request);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Be("Plugin ID not provided in metadata");
    }

    [Fact]
    public async Task DeployAsync_ContentToWebsites_SuccessfullyDeploys()
    {
        // Arrange
        var websites = new List<Website>
        {
            CreateWebsite(Guid.NewGuid()),
            CreateWebsite(Guid.NewGuid())
        };

        var request = CreateDeploymentRequest(WebsiteModuleType.Content, DeploymentScope.AllTenantWebsites);

        SetupSuccessfulDeployment(websites);
        _mockWebsiteRepository.Setup(r => r.GetByTenantIdAsync(
            request.TenantId,
            null,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(websites);

        // Act
        var result = await _service.DeployAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        result.Message.Should().Contain("Content deployment completed");
        result.AffectedWebsites.Should().HaveCount(2);
    }

    [Fact]
    public async Task DeployAsync_UnsupportedModuleType_ReturnsFailure()
    {
        // Arrange
        var websites = new List<Website> { CreateWebsite(Guid.NewGuid()) };
        var request = CreateDeploymentRequest((WebsiteModuleType)999, DeploymentScope.AllTenantWebsites);

        SetupSuccessfulDeployment(websites);
        _mockWebsiteRepository.Setup(r => r.GetByTenantIdAsync(
            request.TenantId,
            null,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(websites);

        // Act
        var result = await _service.DeployAsync(request);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Unsupported module type");
    }

    [Fact]
    public async Task DeployAsync_WhenThemeServiceThrows_RecordsError()
    {
        // Arrange
        var themeId = Guid.NewGuid();
        var websites = new List<Website>
        {
            CreateWebsite(Guid.NewGuid()),
            CreateWebsite(Guid.NewGuid())
        };

        var request = CreateDeploymentRequest(WebsiteModuleType.Theme, DeploymentScope.AllTenantWebsites);
        request.Metadata["themeId"] = themeId.ToString();

        SetupSuccessfulDeployment(websites);
        _mockWebsiteRepository.Setup(r => r.GetByTenantIdAsync(
            request.TenantId,
            null,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(websites);

        _mockThemeService.Setup(t => t.ActivateThemeAsync(
            websites[0].WebsiteId,
            themeId,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _mockThemeService.Setup(t => t.ActivateThemeAsync(
            websites[1].WebsiteId,
            themeId,
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Theme activation failed"));

        // Act
        var result = await _service.DeployAsync(request);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Be("Theme deployed to 1/2 websites");
        result.Errors.Should().HaveCount(1);
        result.Errors[0].Should().Contain("Theme activation failed");
    }

    [Fact]
    public async Task DeployAsync_WhenExceptionThrown_ReturnsFailureResult()
    {
        // Arrange
        var request = CreateDeploymentRequest(WebsiteModuleType.Theme, DeploymentScope.AllTenantWebsites);

        _mockQuotaService.Setup(q => q.CheckQuotaAsync(
            It.IsAny<Guid>(),
            It.IsAny<ResourceType>(),
            It.IsAny<long>(),
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Quota service unavailable"));

        // Act
        var result = await _service.DeployAsync(request);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Be("Deployment failed with exception");
        result.Errors.Should().Contain("Quota service unavailable");
        result.EndTime.Should().NotBeNull();
    }

    #endregion

    #region GetDeploymentStatusAsync Tests

    [Fact]
    public async Task GetDeploymentStatusAsync_WithExistingDeployment_ReturnsResult()
    {
        // Arrange
        var request = CreateDeploymentRequest(WebsiteModuleType.Theme, DeploymentScope.AllTenantWebsites);
        var websites = new List<Website> { CreateWebsite(Guid.NewGuid()) };
        request.Metadata["themeId"] = Guid.NewGuid().ToString();

        SetupSuccessfulDeployment(websites);
        _mockWebsiteRepository.Setup(r => r.GetByTenantIdAsync(
            request.TenantId,
            null,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(websites);
        _mockThemeService.Setup(t => t.ActivateThemeAsync(
            It.IsAny<Guid>(),
            It.IsAny<Guid>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var deployment = await _service.DeployAsync(request);

        // Act
        var result = await _service.GetDeploymentStatusAsync(deployment.DeploymentId);

        // Assert
        result.Should().NotBeNull();
        result!.DeploymentId.Should().Be(deployment.DeploymentId);
        result.TenantId.Should().Be(request.TenantId);
    }

    [Fact]
    public async Task GetDeploymentStatusAsync_WithNonExistentDeployment_ReturnsNull()
    {
        // Arrange
        var deploymentId = Guid.NewGuid();

        // Act
        var result = await _service.GetDeploymentStatusAsync(deploymentId);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region GetDeploymentsForTenantAsync Tests

    [Fact]
    public async Task GetDeploymentsForTenantAsync_ReturnsDeploymentsInDescendingOrder()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var websites = new List<Website> { CreateWebsite(Guid.NewGuid()) };

        SetupSuccessfulDeployment(websites);
        _mockWebsiteRepository.Setup(r => r.GetByTenantIdAsync(
            tenantId,
            null,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(websites);
        _mockThemeService.Setup(t => t.ActivateThemeAsync(
            It.IsAny<Guid>(),
            It.IsAny<Guid>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Create multiple deployments
        var deployment1 = await _service.DeployAsync(CreateDeploymentRequest(WebsiteModuleType.Theme, DeploymentScope.AllTenantWebsites, tenantId, new Dictionary<string, string> { ["themeId"] = Guid.NewGuid().ToString() }));
        await Task.Delay(100);
        var deployment2 = await _service.DeployAsync(CreateDeploymentRequest(WebsiteModuleType.Theme, DeploymentScope.AllTenantWebsites, tenantId, new Dictionary<string, string> { ["themeId"] = Guid.NewGuid().ToString() }));

        // Act
        var results = await _service.GetDeploymentsForTenantAsync(tenantId);

        // Assert
        results.Should().HaveCount(2);
        results[0].DeploymentId.Should().Be(deployment2.DeploymentId); // Most recent first
        results[1].DeploymentId.Should().Be(deployment1.DeploymentId);
    }

    [Fact]
    public async Task GetDeploymentsForTenantAsync_WithNoDeployments_ReturnsEmptyList()
    {
        // Arrange
        var tenantId = Guid.NewGuid();

        // Act
        var results = await _service.GetDeploymentsForTenantAsync(tenantId);

        // Assert
        results.Should().BeEmpty();
    }

    #endregion

    #region RollbackDeploymentAsync Tests

    [Fact]
    public async Task RollbackDeploymentAsync_WithExistingDeployment_ReturnsTrue()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var websites = new List<Website> { CreateWebsite(Guid.NewGuid()) };
        var request = CreateDeploymentRequest(WebsiteModuleType.Theme, DeploymentScope.AllTenantWebsites, tenantId, new Dictionary<string, string> { ["themeId"] = Guid.NewGuid().ToString() });

        SetupSuccessfulDeployment(websites);
        _mockWebsiteRepository.Setup(r => r.GetByTenantIdAsync(
            tenantId,
            null,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(websites);
        _mockThemeService.Setup(t => t.ActivateThemeAsync(
            It.IsAny<Guid>(),
            It.IsAny<Guid>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var deployment = await _service.DeployAsync(request);

        // Act
        var result = await _service.RollbackDeploymentAsync(deployment.DeploymentId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task RollbackDeploymentAsync_WithNonExistentDeployment_ReturnsFalse()
    {
        // Arrange
        var deploymentId = Guid.NewGuid();

        // Act
        var result = await _service.RollbackDeploymentAsync(deploymentId);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Helper Methods

    private TenantDeploymentRequest CreateDeploymentRequest(
        WebsiteModuleType moduleType,
        DeploymentScope scope,
        Guid? tenantId = null,
        Dictionary<string, string>? metadata = null)
    {
        return new TenantDeploymentRequest
        {
            TenantId = tenantId ?? Guid.NewGuid(),
            ModuleType = moduleType,
            Scope = scope,
            Metadata = metadata ?? new Dictionary<string, string>(),
            Module = new ModuleDescriptor
            {
                Name = "TestModule",
                Version = new Version(1, 0, 0)
            },
            RequesterEmail = "test@example.com"
        };
    }

    private Website CreateWebsite(Guid websiteId)
    {
        return new Website
        {
            WebsiteId = websiteId,
            TenantId = Guid.NewGuid(),
            Subdomain = $"site-{websiteId.ToString("N")[..8]}",
            Name = "Test Website",
            Status = WebsiteStatus.Active
        };
    }

    private void SetupSuccessfulDeployment(List<Website> websites)
    {
        _mockQuotaService.Setup(q => q.CheckQuotaAsync(
            It.IsAny<Guid>(),
            It.IsAny<ResourceType>(),
            It.IsAny<long>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _mockQuotaService.Setup(q => q.RecordUsageAsync(
            It.IsAny<Guid>(),
            It.IsAny<ResourceType>(),
            It.IsAny<long>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    #endregion
}
