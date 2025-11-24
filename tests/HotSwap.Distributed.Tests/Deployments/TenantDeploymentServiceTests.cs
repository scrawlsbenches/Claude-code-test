using FluentAssertions;
using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Deployments;
using HotSwap.Distributed.Infrastructure.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace HotSwap.Distributed.Tests.Deployments;

public class TenantDeploymentServiceTests
{
    private readonly Mock<IWebsiteRepository> _mockWebsiteRepository;
    private readonly Mock<IThemeService> _mockThemeService;
    private readonly Mock<IPluginService> _mockPluginService;
    private readonly Mock<IQuotaService> _mockQuotaService;
    private readonly TenantDeploymentService _service;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _websiteId = Guid.NewGuid();

    public TenantDeploymentServiceTests()
    {
        _mockWebsiteRepository = new Mock<IWebsiteRepository>();
        _mockThemeService = new Mock<IThemeService>();
        _mockPluginService = new Mock<IPluginService>();
        _mockQuotaService = new Mock<IQuotaService>();

        _service = new TenantDeploymentService(
            _mockWebsiteRepository.Object,
            _mockThemeService.Object,
            _mockPluginService.Object,
            _mockQuotaService.Object,
            NullLogger<TenantDeploymentService>.Instance);

        SetupDefaultMocks();
    }

    private void SetupDefaultMocks()
    {
        // Default: quota is available
        _mockQuotaService
            .Setup(x => x.CheckQuotaAsync(
                It.IsAny<Guid>(),
                It.IsAny<ResourceType>(),
                It.IsAny<long>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _mockQuotaService
            .Setup(x => x.RecordUsageAsync(
                It.IsAny<Guid>(),
                It.IsAny<ResourceType>(),
                It.IsAny<long>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    #region Theme Deployment Tests

    [Fact]
    public async Task DeployAsync_WithThemeToSingleWebsite_ShouldSucceed()
    {
        // Arrange
        var themeId = Guid.NewGuid();
        var website = CreateTestWebsite();

        _mockWebsiteRepository
            .Setup(x => x.GetByIdAsync(_websiteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(website);

        _mockThemeService
            .Setup(x => x.ActivateThemeAsync(_websiteId, themeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var request = CreateTestRequest(
            WebsiteModuleType.Theme,
            DeploymentScope.SingleWebsite,
            _websiteId,
            new Dictionary<string, string> { ["themeId"] = themeId.ToString() });

        // Act
        var result = await _service.DeployAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Message.Should().Contain("1/1 websites");
        result.AffectedWebsites.Should().HaveCount(1);
        result.AffectedWebsites.Should().Contain(_websiteId);
        result.Errors.Should().BeEmpty();

        _mockThemeService.Verify(
            x => x.ActivateThemeAsync(_websiteId, themeId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DeployAsync_WithThemeToAllTenantWebsites_ShouldDeployToAll()
    {
        // Arrange
        var themeId = Guid.NewGuid();
        var websites = new List<Website>
        {
            CreateTestWebsite(),
            CreateTestWebsite(),
            CreateTestWebsite()
        };

        _mockWebsiteRepository
            .Setup(x => x.GetByTenantIdAsync(_tenantId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(websites);

        _mockThemeService
            .Setup(x => x.ActivateThemeAsync(It.IsAny<Guid>(), themeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var request = CreateTestRequest(
            WebsiteModuleType.Theme,
            DeploymentScope.AllTenantWebsites,
            null,
            new Dictionary<string, string> { ["themeId"] = themeId.ToString() });

        // Act
        var result = await _service.DeployAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        result.Message.Should().Contain("3/3 websites");
        result.AffectedWebsites.Should().HaveCount(3);

        _mockThemeService.Verify(
            x => x.ActivateThemeAsync(It.IsAny<Guid>(), themeId, It.IsAny<CancellationToken>()),
            Times.Exactly(3));
    }

    [Fact]
    public async Task DeployAsync_WithThemeButMissingThemeId_ShouldFail()
    {
        // Arrange
        var website = CreateTestWebsite();

        _mockWebsiteRepository
            .Setup(x => x.GetByIdAsync(_websiteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(website);

        var request = CreateTestRequest(
            WebsiteModuleType.Theme,
            DeploymentScope.SingleWebsite,
            _websiteId,
            new Dictionary<string, string>()); // Missing themeId

        // Act
        var result = await _service.DeployAsync(request);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Theme ID not provided");

        _mockThemeService.Verify(
            x => x.ActivateThemeAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DeployAsync_WithThemePartialFailure_ShouldRecordErrors()
    {
        // Arrange
        var themeId = Guid.NewGuid();
        var websites = new List<Website>
        {
            CreateTestWebsite(Guid.NewGuid()),
            CreateTestWebsite(Guid.NewGuid()),
            CreateTestWebsite(Guid.NewGuid())
        };

        _mockWebsiteRepository
            .Setup(x => x.GetByTenantIdAsync(_tenantId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(websites);

        // First website succeeds, second fails, third succeeds
        _mockThemeService
            .SetupSequence(x => x.ActivateThemeAsync(It.IsAny<Guid>(), themeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .ThrowsAsync(new InvalidOperationException("Theme activation failed"))
            .ReturnsAsync(true);

        var request = CreateTestRequest(
            WebsiteModuleType.Theme,
            DeploymentScope.AllTenantWebsites,
            null,
            new Dictionary<string, string> { ["themeId"] = themeId.ToString() });

        // Act
        var result = await _service.DeployAsync(request);

        // Assert
        result.Success.Should().BeFalse(); // Not all succeeded
        result.Message.Should().Contain("2/3 websites");
        result.Errors.Should().HaveCount(1);
        result.Errors[0].Should().Contain("Theme activation failed");
    }

    #endregion

    #region Plugin Deployment Tests

    [Fact]
    public async Task DeployAsync_WithPluginToSingleWebsite_ShouldSucceed()
    {
        // Arrange
        var pluginId = Guid.NewGuid();
        var website = CreateTestWebsite();

        _mockWebsiteRepository
            .Setup(x => x.GetByIdAsync(_websiteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(website);

        _mockPluginService
            .Setup(x => x.ActivatePluginAsync(_websiteId, pluginId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var request = CreateTestRequest(
            WebsiteModuleType.Plugin,
            DeploymentScope.SingleWebsite,
            _websiteId,
            new Dictionary<string, string> { ["pluginId"] = pluginId.ToString() });

        // Act
        var result = await _service.DeployAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        result.Message.Should().Contain("1/1 websites");

        _mockPluginService.Verify(
            x => x.ActivatePluginAsync(_websiteId, pluginId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DeployAsync_WithPluginButMissingPluginId_ShouldFail()
    {
        // Arrange
        var website = CreateTestWebsite();

        _mockWebsiteRepository
            .Setup(x => x.GetByIdAsync(_websiteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(website);

        var request = CreateTestRequest(
            WebsiteModuleType.Plugin,
            DeploymentScope.SingleWebsite,
            _websiteId,
            new Dictionary<string, string>()); // Missing pluginId

        // Act
        var result = await _service.DeployAsync(request);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Plugin ID not provided");
    }

    #endregion

    #region Content Deployment Tests

    [Fact]
    public async Task DeployAsync_WithContentDeployment_ShouldSucceed()
    {
        // Arrange
        var websites = new List<Website> { CreateTestWebsite() };

        _mockWebsiteRepository
            .Setup(x => x.GetByIdAsync(_websiteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(websites[0]);

        var request = CreateTestRequest(
            WebsiteModuleType.Content,
            DeploymentScope.SingleWebsite,
            _websiteId,
            new Dictionary<string, string>());

        // Act
        var result = await _service.DeployAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        result.Message.Should().Contain("Content deployment completed");
    }

    #endregion

    #region Quota Tests

    [Fact]
    public async Task DeployAsync_WhenQuotaExceeded_ShouldFailWithQuotaMessage()
    {
        // Arrange
        _mockQuotaService
            .Setup(x => x.CheckQuotaAsync(
                _tenantId,
                ResourceType.ConcurrentDeployments,
                1,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var request = CreateTestRequest(
            WebsiteModuleType.Theme,
            DeploymentScope.AllTenantWebsites,
            null,
            new Dictionary<string, string>());

        // Act
        var result = await _service.DeployAsync(request);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("quota exceeded");
        result.Errors.Should().Contain(e => e.Contains("Maximum concurrent deployments"));

        // Should not record usage if quota check failed
        _mockQuotaService.Verify(
            x => x.RecordUsageAsync(It.IsAny<Guid>(), It.IsAny<ResourceType>(), It.IsAny<long>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DeployAsync_WhenQuotaAvailable_ShouldRecordUsage()
    {
        // Arrange
        var website = CreateTestWebsite();

        _mockWebsiteRepository
            .Setup(x => x.GetByIdAsync(_websiteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(website);

        _mockThemeService
            .Setup(x => x.ActivateThemeAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var themeId = Guid.NewGuid();
        var request = CreateTestRequest(
            WebsiteModuleType.Theme,
            DeploymentScope.SingleWebsite,
            _websiteId,
            new Dictionary<string, string> { ["themeId"] = themeId.ToString() });

        // Act
        await _service.DeployAsync(request);

        // Assert
        _mockQuotaService.Verify(
            x => x.RecordUsageAsync(
                _tenantId,
                ResourceType.ConcurrentDeployments,
                1,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region Website Scoping Tests

    [Fact]
    public async Task DeployAsync_WithNoWebsitesFound_ShouldFail()
    {
        // Arrange
        _mockWebsiteRepository
            .Setup(x => x.GetByIdAsync(_websiteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Website?)null);

        var themeId = Guid.NewGuid();
        var request = CreateTestRequest(
            WebsiteModuleType.Theme,
            DeploymentScope.SingleWebsite,
            _websiteId,
            new Dictionary<string, string> { ["themeId"] = themeId.ToString() });

        // Act
        var result = await _service.DeployAsync(request);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("No websites found");
        result.Errors.Should().Contain(e => e.Contains("Deployment scope did not match any websites"));
    }

    #endregion

    #region Deployment Status and History Tests

    [Fact]
    public async Task GetDeploymentStatusAsync_ForExistingDeployment_ShouldReturnStatus()
    {
        // Arrange - Execute a deployment first
        var website = CreateTestWebsite();

        _mockWebsiteRepository
            .Setup(x => x.GetByIdAsync(_websiteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(website);

        _mockThemeService
            .Setup(x => x.ActivateThemeAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var themeId = Guid.NewGuid();
        var request = CreateTestRequest(
            WebsiteModuleType.Theme,
            DeploymentScope.SingleWebsite,
            _websiteId,
            new Dictionary<string, string> { ["themeId"] = themeId.ToString() });

        var deploymentResult = await _service.DeployAsync(request);

        // Act
        var status = await _service.GetDeploymentStatusAsync(deploymentResult.DeploymentId);

        // Assert
        status.Should().NotBeNull();
        status!.DeploymentId.Should().Be(deploymentResult.DeploymentId);
        status.TenantId.Should().Be(_tenantId);
        status.Success.Should().BeTrue();
    }

    [Fact]
    public async Task GetDeploymentStatusAsync_ForNonExistentDeployment_ShouldReturnNull()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var status = await _service.GetDeploymentStatusAsync(nonExistentId);

        // Assert
        status.Should().BeNull();
    }

    [Fact]
    public async Task GetDeploymentsForTenantAsync_ShouldReturnAllTenantDeployments()
    {
        // Arrange - Execute multiple deployments
        var website = CreateTestWebsite();

        _mockWebsiteRepository
            .Setup(x => x.GetByIdAsync(_websiteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(website);

        _mockThemeService
            .Setup(x => x.ActivateThemeAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var themeId = Guid.NewGuid();
        var request = CreateTestRequest(
            WebsiteModuleType.Theme,
            DeploymentScope.SingleWebsite,
            _websiteId,
            new Dictionary<string, string> { ["themeId"] = themeId.ToString() });

        await _service.DeployAsync(request);
        await _service.DeployAsync(request);
        await _service.DeployAsync(request);

        // Act
        var deployments = await _service.GetDeploymentsForTenantAsync(_tenantId);

        // Assert
        deployments.Should().HaveCount(3);
        deployments.Should().AllSatisfy(d => d.TenantId.Should().Be(_tenantId));
        // Should be ordered by start time descending
        deployments.Should().BeInDescendingOrder(d => d.StartTime);
    }

    #endregion

    #region Rollback Tests

    [Fact]
    public async Task RollbackDeploymentAsync_ForExistingDeployment_ShouldSucceed()
    {
        // Arrange - Execute a deployment first
        var website = CreateTestWebsite();

        _mockWebsiteRepository
            .Setup(x => x.GetByIdAsync(_websiteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(website);

        _mockThemeService
            .Setup(x => x.ActivateThemeAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var themeId = Guid.NewGuid();
        var request = CreateTestRequest(
            WebsiteModuleType.Theme,
            DeploymentScope.SingleWebsite,
            _websiteId,
            new Dictionary<string, string> { ["themeId"] = themeId.ToString() });

        var deploymentResult = await _service.DeployAsync(request);

        // Act
        var rollbackSuccess = await _service.RollbackDeploymentAsync(deploymentResult.DeploymentId);

        // Assert
        rollbackSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task RollbackDeploymentAsync_ForNonExistentDeployment_ShouldReturnFalse()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var rollbackSuccess = await _service.RollbackDeploymentAsync(nonExistentId);

        // Assert
        rollbackSuccess.Should().BeFalse();
    }

    #endregion

    #region Exception Handling Tests

    [Fact]
    public async Task DeployAsync_WhenExceptionThrown_ShouldReturnFailureResult()
    {
        // Arrange
        _mockWebsiteRepository
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database connection failed"));

        var themeId = Guid.NewGuid();
        var request = CreateTestRequest(
            WebsiteModuleType.Theme,
            DeploymentScope.SingleWebsite,
            _websiteId,
            new Dictionary<string, string> { ["themeId"] = themeId.ToString() });

        // Act
        var result = await _service.DeployAsync(request);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Deployment failed with exception");
        result.Errors.Should().Contain("Database connection failed");
    }

    #endregion

    #region Helper Methods

    private Website CreateTestWebsite(Guid? websiteId = null)
    {
        return new Website
        {
            WebsiteId = websiteId ?? _websiteId,
            TenantId = _tenantId,
            Name = "Test Website",
            Subdomain = "test-site",
            CustomDomains = new List<string> { "test.example.com" },
            CurrentThemeId = Guid.NewGuid(),
            Configuration = new Dictionary<string, string>()
        };
    }

    private TenantDeploymentRequest CreateTestRequest(
        WebsiteModuleType moduleType,
        DeploymentScope scope,
        Guid? websiteId = null,
        Dictionary<string, string>? metadata = null)
    {
        return new TenantDeploymentRequest
        {
            TenantId = _tenantId,
            WebsiteId = websiteId,
            ModuleType = moduleType,
            Scope = scope,
            Metadata = metadata ?? new Dictionary<string, string>(),
            // Required base class properties
            Module = new ModuleDescriptor
            {
                Name = "test-module",
                Version = new Version(1, 0, 0),
                Description = "Test module",
                Dependencies = new Dictionary<string, string>()
            },
            RequesterEmail = "test@example.com"
        };
    }

    #endregion
}
