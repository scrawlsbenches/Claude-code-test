using FluentAssertions;
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

    [Fact]
    public async Task DeployAsync_WithThemeDeployment_DeploysSuccessfully()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var websiteId = Guid.NewGuid();
        var themeId = Guid.NewGuid();

        var request = new TenantDeploymentRequest
        {
            TenantId = tenantId,
            WebsiteId = websiteId,
            ModuleType = WebsiteModuleType.Theme,
            Scope = DeploymentScope.SingleWebsite,
            Metadata = new Dictionary<string, string> { { "themeId", themeId.ToString() } }
        };

        var website = new Website
        {
            WebsiteId = websiteId,
            TenantId = tenantId,
            Name = "Test Website",
            Subdomain = "test"
        };

        _mockQuotaService.Setup(q => q.CheckQuotaAsync(
            It.IsAny<Guid>(), It.IsAny<Domain.Enums.ResourceType>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockQuotaService.Setup(q => q.RecordUsageAsync(
            It.IsAny<Guid>(), It.IsAny<Domain.Enums.ResourceType>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockWebsiteRepository.Setup(r => r.GetByIdAsync(websiteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(website);
        _mockThemeService.Setup(s => s.ActivateThemeAsync(websiteId, themeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _service.DeployAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.AffectedWebsites.Should().HaveCount(1);
        result.AffectedWebsites.Should().Contain(websiteId);
        _mockThemeService.Verify(s => s.ActivateThemeAsync(websiteId, themeId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeployAsync_WithPluginDeployment_DeploysSuccessfully()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var websiteId = Guid.NewGuid();
        var pluginId = Guid.NewGuid();

        var request = new TenantDeploymentRequest
        {
            TenantId = tenantId,
            WebsiteId = websiteId,
            ModuleType = WebsiteModuleType.Plugin,
            Scope = DeploymentScope.SingleWebsite,
            Metadata = new Dictionary<string, string> { { "pluginId", pluginId.ToString() } }
        };

        var website = new Website
        {
            WebsiteId = websiteId,
            TenantId = tenantId,
            Name = "Test Website",
            Subdomain = "test"
        };

        _mockQuotaService.Setup(q => q.CheckQuotaAsync(
            It.IsAny<Guid>(), It.IsAny<Domain.Enums.ResourceType>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockQuotaService.Setup(q => q.RecordUsageAsync(
            It.IsAny<Guid>(), It.IsAny<Domain.Enums.ResourceType>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockWebsiteRepository.Setup(r => r.GetByIdAsync(websiteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(website);
        _mockPluginService.Setup(s => s.ActivatePluginAsync(websiteId, pluginId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _service.DeployAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        _mockPluginService.Verify(s => s.ActivatePluginAsync(websiteId, pluginId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeployAsync_WithContentDeployment_DeploysSuccessfully()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var websiteId = Guid.NewGuid();

        var request = new TenantDeploymentRequest
        {
            TenantId = tenantId,
            WebsiteId = websiteId,
            ModuleType = WebsiteModuleType.Content,
            Scope = DeploymentScope.SingleWebsite,
            Metadata = new Dictionary<string, string>()
        };

        var website = new Website
        {
            WebsiteId = websiteId,
            TenantId = tenantId,
            Name = "Test Website",
            Subdomain = "test"
        };

        _mockQuotaService.Setup(q => q.CheckQuotaAsync(
            It.IsAny<Guid>(), It.IsAny<Domain.Enums.ResourceType>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockQuotaService.Setup(q => q.RecordUsageAsync(
            It.IsAny<Guid>(), It.IsAny<Domain.Enums.ResourceType>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockWebsiteRepository.Setup(r => r.GetByIdAsync(websiteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(website);

        // Act
        var result = await _service.DeployAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Message.Should().Contain("Content deployment completed");
    }

    [Fact]
    public async Task DeployAsync_WithQuotaExceeded_ReturnsFailure()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var request = new TenantDeploymentRequest
        {
            TenantId = tenantId,
            ModuleType = WebsiteModuleType.Theme,
            Scope = DeploymentScope.AllTenantWebsites,
            Metadata = new Dictionary<string, string>()
        };

        _mockQuotaService.Setup(q => q.CheckQuotaAsync(
            tenantId, Domain.Enums.ResourceType.ConcurrentDeployments, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _service.DeployAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Message.Should().Be("Concurrent deployment quota exceeded");
        result.Errors.Should().Contain("Maximum concurrent deployments reached for this tenant");
    }

    [Fact]
    public async Task RollbackDeploymentAsync_WithValidDeployment_RollsBackSuccessfully()
    {
        // Arrange
        var deploymentId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var websiteId = Guid.NewGuid();

        // First create a deployment
        var request = new TenantDeploymentRequest
        {
            TenantId = tenantId,
            WebsiteId = websiteId,
            ModuleType = WebsiteModuleType.Theme,
            Scope = DeploymentScope.SingleWebsite,
            Metadata = new Dictionary<string, string> { { "themeId", Guid.NewGuid().ToString() } }
        };

        var website = new Website
        {
            WebsiteId = websiteId,
            TenantId = tenantId,
            Name = "Test Website",
            Subdomain = "test"
        };

        _mockQuotaService.Setup(q => q.CheckQuotaAsync(
            It.IsAny<Guid>(), It.IsAny<Domain.Enums.ResourceType>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockQuotaService.Setup(q => q.RecordUsageAsync(
            It.IsAny<Guid>(), It.IsAny<Domain.Enums.ResourceType>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockWebsiteRepository.Setup(r => r.GetByIdAsync(websiteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(website);
        _mockThemeService.Setup(s => s.ActivateThemeAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var deployment = await _service.DeployAsync(request);

        // Act
        var rollbackResult = await _service.RollbackDeploymentAsync(deployment.DeploymentId);

        // Assert
        rollbackResult.Should().BeTrue();
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

    [Fact]
    public async Task GetDeploymentStatusAsync_WithExistingDeployment_ReturnsStatus()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var websiteId = Guid.NewGuid();

        var request = new TenantDeploymentRequest
        {
            TenantId = tenantId,
            WebsiteId = websiteId,
            ModuleType = WebsiteModuleType.Theme,
            Scope = DeploymentScope.SingleWebsite,
            Metadata = new Dictionary<string, string> { { "themeId", Guid.NewGuid().ToString() } }
        };

        var website = new Website
        {
            WebsiteId = websiteId,
            TenantId = tenantId,
            Name = "Test Website",
            Subdomain = "test"
        };

        _mockQuotaService.Setup(q => q.CheckQuotaAsync(
            It.IsAny<Guid>(), It.IsAny<Domain.Enums.ResourceType>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockQuotaService.Setup(q => q.RecordUsageAsync(
            It.IsAny<Guid>(), It.IsAny<Domain.Enums.ResourceType>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockWebsiteRepository.Setup(r => r.GetByIdAsync(websiteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(website);
        _mockThemeService.Setup(s => s.ActivateThemeAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var deployment = await _service.DeployAsync(request);

        // Act
        var status = await _service.GetDeploymentStatusAsync(deployment.DeploymentId);

        // Assert
        status.Should().NotBeNull();
        status!.DeploymentId.Should().Be(deployment.DeploymentId);
        status.Success.Should().BeTrue();
    }

    [Fact]
    public async Task GetDeploymentsForTenantAsync_WithMultipleDeployments_ReturnsAll()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var websiteId1 = Guid.NewGuid();
        var websiteId2 = Guid.NewGuid();

        SetupDeployment(tenantId, websiteId1);
        SetupDeployment(tenantId, websiteId2);

        // Create two deployments
        await _service.DeployAsync(new TenantDeploymentRequest
        {
            TenantId = tenantId,
            WebsiteId = websiteId1,
            ModuleType = WebsiteModuleType.Theme,
            Scope = DeploymentScope.SingleWebsite,
            Metadata = new Dictionary<string, string> { { "themeId", Guid.NewGuid().ToString() } }
        });

        await _service.DeployAsync(new TenantDeploymentRequest
        {
            TenantId = tenantId,
            WebsiteId = websiteId2,
            ModuleType = WebsiteModuleType.Plugin,
            Scope = DeploymentScope.SingleWebsite,
            Metadata = new Dictionary<string, string> { { "pluginId", Guid.NewGuid().ToString() } }
        });

        // Act
        var deployments = await _service.GetDeploymentsForTenantAsync(tenantId);

        // Assert
        deployments.Should().HaveCount(2);
        deployments.Should().OnlyContain(d => d.TenantId == tenantId);
    }

    [Fact]
    public void Constructor_WithNullWebsiteRepository_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new TenantDeploymentService(null!, _mockThemeService.Object, _mockPluginService.Object, _mockQuotaService.Object, _mockLogger.Object);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("websiteRepository");
    }

    [Fact]
    public void Constructor_WithNullThemeService_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new TenantDeploymentService(_mockWebsiteRepository.Object, null!, _mockPluginService.Object, _mockQuotaService.Object, _mockLogger.Object);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("themeService");
    }

    [Fact]
    public void Constructor_WithNullPluginService_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new TenantDeploymentService(_mockWebsiteRepository.Object, _mockThemeService.Object, null!, _mockQuotaService.Object, _mockLogger.Object);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("pluginService");
    }

    [Fact]
    public void Constructor_WithNullQuotaService_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new TenantDeploymentService(_mockWebsiteRepository.Object, _mockThemeService.Object, _mockPluginService.Object, null!, _mockLogger.Object);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("quotaService");
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new TenantDeploymentService(_mockWebsiteRepository.Object, _mockThemeService.Object, _mockPluginService.Object, _mockQuotaService.Object, null!);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    private void SetupDeployment(Guid tenantId, Guid websiteId)
    {
        var website = new Website
        {
            WebsiteId = websiteId,
            TenantId = tenantId,
            Name = "Test Website",
            Subdomain = "test"
        };

        _mockQuotaService.Setup(q => q.CheckQuotaAsync(
            It.IsAny<Guid>(), It.IsAny<Domain.Enums.ResourceType>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockQuotaService.Setup(q => q.RecordUsageAsync(
            It.IsAny<Guid>(), It.IsAny<Domain.Enums.ResourceType>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockWebsiteRepository.Setup(r => r.GetByIdAsync(websiteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(website);
        _mockThemeService.Setup(s => s.ActivateThemeAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockPluginService.Setup(s => s.ActivatePluginAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }
}
