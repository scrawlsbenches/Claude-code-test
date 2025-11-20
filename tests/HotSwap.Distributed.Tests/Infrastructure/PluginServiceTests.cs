using FluentAssertions;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Interfaces;
using HotSwap.Distributed.Infrastructure.Websites;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace HotSwap.Distributed.Tests.Infrastructure;

public class PluginServiceTests
{
    private readonly Mock<IWebsiteRepository> _mockWebsiteRepository;
    private readonly Mock<IPluginRepository> _mockPluginRepository;
    private readonly Mock<ILogger<PluginService>> _mockLogger;
    private readonly PluginService _service;

    public PluginServiceTests()
    {
        _mockWebsiteRepository = new Mock<IWebsiteRepository>();
        _mockPluginRepository = new Mock<IPluginRepository>();
        _mockLogger = new Mock<ILogger<PluginService>>();
        _service = new PluginService(_mockWebsiteRepository.Object, _mockPluginRepository.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task InstallPluginAsync_WithValidPlugin_InstallsSuccessfully()
    {
        // Arrange
        var websiteId = Guid.NewGuid();
        var pluginId = Guid.NewGuid();
        var plugin = new Plugin
        {
            PluginId = pluginId,
            Name = "Test Plugin",
            Version = "1.0.0",
            Description = "A test plugin",
            Author = "Test Author"
        };

        _mockPluginRepository.Setup(r => r.GetByIdAsync(pluginId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plugin);

        // Act
        var result = await _service.InstallPluginAsync(websiteId, pluginId);

        // Assert
        result.Should().BeTrue();
        _mockPluginRepository.Verify(r => r.GetByIdAsync(pluginId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InstallPluginAsync_WithNonExistentPlugin_ThrowsKeyNotFoundException()
    {
        // Arrange
        var websiteId = Guid.NewGuid();
        var pluginId = Guid.NewGuid();

        _mockPluginRepository.Setup(r => r.GetByIdAsync(pluginId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Plugin?)null);

        // Act
        var act = async () => await _service.InstallPluginAsync(websiteId, pluginId);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage($"Plugin {pluginId} not found");
    }

    [Fact]
    public async Task ActivatePluginAsync_WithExistingWebsite_ActivatesPlugin()
    {
        // Arrange
        var websiteId = Guid.NewGuid();
        var pluginId = Guid.NewGuid();
        var website = new Website
        {
            WebsiteId = websiteId,
            TenantId = Guid.NewGuid(),
            Name = "Test Website",
            Subdomain = "test",
            InstalledPluginIds = new List<Guid>()
        };

        _mockWebsiteRepository.Setup(r => r.GetByIdAsync(websiteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(website);
        _mockWebsiteRepository.Setup(r => r.UpdateAsync(It.IsAny<Website>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Website w, CancellationToken _) => w);

        // Act
        var result = await _service.ActivatePluginAsync(websiteId, pluginId);

        // Assert
        result.Should().BeTrue();
        _mockWebsiteRepository.Verify(r => r.UpdateAsync(It.Is<Website>(w =>
            w.InstalledPluginIds.Contains(pluginId)), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ActivatePluginAsync_WithAlreadyActivatedPlugin_DoesNotDuplicate()
    {
        // Arrange
        var websiteId = Guid.NewGuid();
        var pluginId = Guid.NewGuid();
        var website = new Website
        {
            WebsiteId = websiteId,
            TenantId = Guid.NewGuid(),
            Name = "Test Website",
            Subdomain = "test",
            InstalledPluginIds = new List<Guid> { pluginId } // Already activated
        };

        _mockWebsiteRepository.Setup(r => r.GetByIdAsync(websiteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(website);
        _mockWebsiteRepository.Setup(r => r.UpdateAsync(It.IsAny<Website>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Website w, CancellationToken _) => w);

        // Act
        var result = await _service.ActivatePluginAsync(websiteId, pluginId);

        // Assert
        result.Should().BeTrue();
        website.InstalledPluginIds.Should().HaveCount(1); // No duplicate
    }

    [Fact]
    public async Task ActivatePluginAsync_WithNonExistentWebsite_ThrowsKeyNotFoundException()
    {
        // Arrange
        var websiteId = Guid.NewGuid();
        var pluginId = Guid.NewGuid();

        _mockWebsiteRepository.Setup(r => r.GetByIdAsync(websiteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Website?)null);

        // Act
        var act = async () => await _service.ActivatePluginAsync(websiteId, pluginId);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage($"Website {websiteId} not found");
    }

    [Fact]
    public async Task DeactivatePluginAsync_WithActivatedPlugin_DeactivatesSuccessfully()
    {
        // Arrange
        var websiteId = Guid.NewGuid();
        var pluginId = Guid.NewGuid();
        var website = new Website
        {
            WebsiteId = websiteId,
            TenantId = Guid.NewGuid(),
            Name = "Test Website",
            Subdomain = "test",
            InstalledPluginIds = new List<Guid> { pluginId }
        };

        _mockWebsiteRepository.Setup(r => r.GetByIdAsync(websiteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(website);
        _mockWebsiteRepository.Setup(r => r.UpdateAsync(It.IsAny<Website>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Website w, CancellationToken _) => w);

        // Act
        var result = await _service.DeactivatePluginAsync(websiteId, pluginId);

        // Assert
        result.Should().BeTrue();
        _mockWebsiteRepository.Verify(r => r.UpdateAsync(It.Is<Website>(w =>
            !w.InstalledPluginIds.Contains(pluginId)), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UninstallPluginAsync_WithActivatedPlugin_UninstallsSuccessfully()
    {
        // Arrange
        var websiteId = Guid.NewGuid();
        var pluginId = Guid.NewGuid();
        var website = new Website
        {
            WebsiteId = websiteId,
            TenantId = Guid.NewGuid(),
            Name = "Test Website",
            Subdomain = "test",
            InstalledPluginIds = new List<Guid> { pluginId }
        };

        _mockWebsiteRepository.Setup(r => r.GetByIdAsync(websiteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(website);
        _mockWebsiteRepository.Setup(r => r.UpdateAsync(It.IsAny<Website>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Website w, CancellationToken _) => w);

        // Act
        var result = await _service.UninstallPluginAsync(websiteId, pluginId);

        // Assert
        result.Should().BeTrue();
        _mockWebsiteRepository.Verify(r => r.UpdateAsync(It.Is<Website>(w =>
            !w.InstalledPluginIds.Contains(pluginId)), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Constructor_WithNullWebsiteRepository_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new PluginService(null!, _mockPluginRepository.Object, _mockLogger.Object);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("websiteRepository");
    }

    [Fact]
    public void Constructor_WithNullPluginRepository_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new PluginService(_mockWebsiteRepository.Object, null!, _mockLogger.Object);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("pluginRepository");
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new PluginService(_mockWebsiteRepository.Object, _mockPluginRepository.Object, null!);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }
}
