using FluentAssertions;
using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Interfaces;
using HotSwap.Distributed.Infrastructure.Websites;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace HotSwap.Distributed.Tests.Websites;

public class PluginServiceTests
{
    private readonly Mock<IWebsiteRepository> _mockWebsiteRepository;
    private readonly Mock<IPluginRepository> _mockPluginRepository;
    private readonly Mock<ILogger<PluginService>> _mockLogger;
    private readonly PluginService _service;
    private readonly Guid _websiteId;
    private readonly Guid _pluginId;

    public PluginServiceTests()
    {
        _mockWebsiteRepository = new Mock<IWebsiteRepository>();
        _mockPluginRepository = new Mock<IPluginRepository>();
        _mockLogger = new Mock<ILogger<PluginService>>();
        _service = new PluginService(
            _mockWebsiteRepository.Object,
            _mockPluginRepository.Object,
            _mockLogger.Object);
        _websiteId = Guid.NewGuid();
        _pluginId = Guid.NewGuid();
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullWebsiteRepository_ShouldThrowArgumentNullException()
    {
        var action = () => new PluginService(
            null!,
            _mockPluginRepository.Object,
            _mockLogger.Object);

        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("websiteRepository");
    }

    [Fact]
    public void Constructor_WithNullPluginRepository_ShouldThrowArgumentNullException()
    {
        var action = () => new PluginService(
            _mockWebsiteRepository.Object,
            null!,
            _mockLogger.Object);

        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("pluginRepository");
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        var action = () => new PluginService(
            _mockWebsiteRepository.Object,
            _mockPluginRepository.Object,
            null!);

        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    #endregion

    #region InstallPluginAsync Tests

    [Fact]
    public async Task InstallPluginAsync_WithValidPlugin_ShouldInstallSuccessfully()
    {
        var plugin = CreateTestPlugin();

        _mockPluginRepository
            .Setup(x => x.GetByIdAsync(_pluginId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plugin);

        var result = await _service.InstallPluginAsync(_websiteId, _pluginId);

        result.Should().BeTrue();

        _mockPluginRepository.Verify(x => x.GetByIdAsync(
            _pluginId,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InstallPluginAsync_WithNonExistentPlugin_ShouldThrowKeyNotFoundException()
    {
        _mockPluginRepository
            .Setup(x => x.GetByIdAsync(_pluginId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Plugin?)null);

        var action = async () => await _service.InstallPluginAsync(_websiteId, _pluginId);

        await action.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage($"Plugin {_pluginId} not found");
    }

    [Fact]
    public async Task InstallPluginAsync_WithRepositoryException_ShouldThrowException()
    {
        _mockPluginRepository
            .Setup(x => x.GetByIdAsync(_pluginId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database connection failed"));

        var action = async () => await _service.InstallPluginAsync(_websiteId, _pluginId);

        await action.Should().ThrowAsync<Exception>()
            .WithMessage("Database connection failed");
    }

    #endregion

    #region ActivatePluginAsync Tests

    [Fact]
    public async Task ActivatePluginAsync_WithValidWebsite_ShouldAddPluginToInstalledList()
    {
        var website = CreateTestWebsite();
        Website? updatedWebsite = null;

        _mockWebsiteRepository
            .Setup(x => x.GetByIdAsync(_websiteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(website);

        _mockWebsiteRepository
            .Setup(x => x.UpdateAsync(It.IsAny<Website>(), It.IsAny<CancellationToken>()))
            .Callback<Website, CancellationToken>((w, _) => updatedWebsite = w)
            .ReturnsAsync((Website w, CancellationToken _) => w);

        var result = await _service.ActivatePluginAsync(_websiteId, _pluginId);

        result.Should().BeTrue();
        updatedWebsite.Should().NotBeNull();
        updatedWebsite!.InstalledPluginIds.Should().Contain(_pluginId);

        _mockWebsiteRepository.Verify(x => x.UpdateAsync(
            It.IsAny<Website>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ActivatePluginAsync_WithNonExistentWebsite_ShouldThrowKeyNotFoundException()
    {
        _mockWebsiteRepository
            .Setup(x => x.GetByIdAsync(_websiteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Website?)null);

        var action = async () => await _service.ActivatePluginAsync(_websiteId, _pluginId);

        await action.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage($"Website {_websiteId} not found");

        _mockWebsiteRepository.Verify(x => x.UpdateAsync(
            It.IsAny<Website>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ActivatePluginAsync_WhenPluginAlreadyActivated_ShouldNotAddDuplicate()
    {
        var website = CreateTestWebsite();
        website.InstalledPluginIds.Add(_pluginId); // Already activated
        var initialCount = website.InstalledPluginIds.Count;

        _mockWebsiteRepository
            .Setup(x => x.GetByIdAsync(_websiteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(website);

        var result = await _service.ActivatePluginAsync(_websiteId, _pluginId);

        result.Should().BeTrue();
        website.InstalledPluginIds.Should().HaveCount(initialCount);
        website.InstalledPluginIds.Count(p => p == _pluginId).Should().Be(1);

        _mockWebsiteRepository.Verify(x => x.UpdateAsync(
            It.IsAny<Website>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ActivatePluginAsync_WithMultiplePlugins_ShouldMaintainExistingPlugins()
    {
        var otherPluginId = Guid.NewGuid();
        var website = CreateTestWebsite();
        website.InstalledPluginIds.Add(otherPluginId);
        Website? updatedWebsite = null;

        _mockWebsiteRepository
            .Setup(x => x.GetByIdAsync(_websiteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(website);

        _mockWebsiteRepository
            .Setup(x => x.UpdateAsync(It.IsAny<Website>(), It.IsAny<CancellationToken>()))
            .Callback<Website, CancellationToken>((w, _) => updatedWebsite = w)
            .ReturnsAsync((Website w, CancellationToken _) => w);

        await _service.ActivatePluginAsync(_websiteId, _pluginId);

        updatedWebsite.Should().NotBeNull();
        updatedWebsite!.InstalledPluginIds.Should().HaveCount(2);
        updatedWebsite.InstalledPluginIds.Should().Contain(otherPluginId);
        updatedWebsite.InstalledPluginIds.Should().Contain(_pluginId);
    }

    #endregion

    #region DeactivatePluginAsync Tests

    [Fact]
    public async Task DeactivatePluginAsync_WithActivatedPlugin_ShouldRemoveFromInstalledList()
    {
        var website = CreateTestWebsite();
        website.InstalledPluginIds.Add(_pluginId);
        Website? updatedWebsite = null;

        _mockWebsiteRepository
            .Setup(x => x.GetByIdAsync(_websiteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(website);

        _mockWebsiteRepository
            .Setup(x => x.UpdateAsync(It.IsAny<Website>(), It.IsAny<CancellationToken>()))
            .Callback<Website, CancellationToken>((w, _) => updatedWebsite = w)
            .ReturnsAsync((Website w, CancellationToken _) => w);

        var result = await _service.DeactivatePluginAsync(_websiteId, _pluginId);

        result.Should().BeTrue();
        updatedWebsite.Should().NotBeNull();
        updatedWebsite!.InstalledPluginIds.Should().NotContain(_pluginId);

        _mockWebsiteRepository.Verify(x => x.UpdateAsync(
            It.IsAny<Website>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeactivatePluginAsync_WithNonExistentWebsite_ShouldThrowKeyNotFoundException()
    {
        _mockWebsiteRepository
            .Setup(x => x.GetByIdAsync(_websiteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Website?)null);

        var action = async () => await _service.DeactivatePluginAsync(_websiteId, _pluginId);

        await action.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage($"Website {_websiteId} not found");

        _mockWebsiteRepository.Verify(x => x.UpdateAsync(
            It.IsAny<Website>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeactivatePluginAsync_WithNonActivatedPlugin_ShouldHandleGracefully()
    {
        var website = CreateTestWebsite();
        // Plugin is not in the installed list
        Website? updatedWebsite = null;

        _mockWebsiteRepository
            .Setup(x => x.GetByIdAsync(_websiteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(website);

        _mockWebsiteRepository
            .Setup(x => x.UpdateAsync(It.IsAny<Website>(), It.IsAny<CancellationToken>()))
            .Callback<Website, CancellationToken>((w, _) => updatedWebsite = w)
            .ReturnsAsync((Website w, CancellationToken _) => w);

        var result = await _service.DeactivatePluginAsync(_websiteId, _pluginId);

        result.Should().BeTrue();
        updatedWebsite.Should().NotBeNull();

        _mockWebsiteRepository.Verify(x => x.UpdateAsync(
            It.IsAny<Website>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeactivatePluginAsync_WithMultiplePlugins_ShouldOnlyRemoveSpecifiedPlugin()
    {
        var otherPluginId = Guid.NewGuid();
        var website = CreateTestWebsite();
        website.InstalledPluginIds.Add(_pluginId);
        website.InstalledPluginIds.Add(otherPluginId);
        Website? updatedWebsite = null;

        _mockWebsiteRepository
            .Setup(x => x.GetByIdAsync(_websiteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(website);

        _mockWebsiteRepository
            .Setup(x => x.UpdateAsync(It.IsAny<Website>(), It.IsAny<CancellationToken>()))
            .Callback<Website, CancellationToken>((w, _) => updatedWebsite = w)
            .ReturnsAsync((Website w, CancellationToken _) => w);

        await _service.DeactivatePluginAsync(_websiteId, _pluginId);

        updatedWebsite.Should().NotBeNull();
        updatedWebsite!.InstalledPluginIds.Should().HaveCount(1);
        updatedWebsite.InstalledPluginIds.Should().NotContain(_pluginId);
        updatedWebsite.InstalledPluginIds.Should().Contain(otherPluginId);
    }

    #endregion

    #region UninstallPluginAsync Tests

    [Fact]
    public async Task UninstallPluginAsync_WithActivatedPlugin_ShouldDeactivateAndCleanup()
    {
        var website = CreateTestWebsite();
        website.InstalledPluginIds.Add(_pluginId);
        Website? updatedWebsite = null;

        _mockWebsiteRepository
            .Setup(x => x.GetByIdAsync(_websiteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(website);

        _mockWebsiteRepository
            .Setup(x => x.UpdateAsync(It.IsAny<Website>(), It.IsAny<CancellationToken>()))
            .Callback<Website, CancellationToken>((w, _) => updatedWebsite = w)
            .ReturnsAsync((Website w, CancellationToken _) => w);

        var result = await _service.UninstallPluginAsync(_websiteId, _pluginId);

        result.Should().BeTrue();
        updatedWebsite.Should().NotBeNull();
        updatedWebsite!.InstalledPluginIds.Should().NotContain(_pluginId);

        // Should call deactivate which calls GetByIdAsync and UpdateAsync
        _mockWebsiteRepository.Verify(x => x.GetByIdAsync(
            _websiteId,
            It.IsAny<CancellationToken>()), Times.Once);

        _mockWebsiteRepository.Verify(x => x.UpdateAsync(
            It.IsAny<Website>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UninstallPluginAsync_WithNonExistentWebsite_ShouldThrowKeyNotFoundException()
    {
        _mockWebsiteRepository
            .Setup(x => x.GetByIdAsync(_websiteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Website?)null);

        var action = async () => await _service.UninstallPluginAsync(_websiteId, _pluginId);

        await action.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage($"Website {_websiteId} not found");
    }

    [Fact]
    public async Task UninstallPluginAsync_WithDeactivateException_ShouldPropagateException()
    {
        _mockWebsiteRepository
            .Setup(x => x.GetByIdAsync(_websiteId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database connection failed"));

        var action = async () => await _service.UninstallPluginAsync(_websiteId, _pluginId);

        await action.Should().ThrowAsync<Exception>()
            .WithMessage("Database connection failed");
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
            InstalledPluginIds = new List<Guid>(),
            Configuration = new Dictionary<string, string>(),
            CreatedAt = DateTime.UtcNow
        };
    }

    private Plugin CreateTestPlugin()
    {
        return new Plugin
        {
            PluginId = _pluginId,
            Name = "Test Plugin",
            Version = "1.0.0",
            Author = "Test Author",
            Category = PluginCategory.Custom,
            Description = "Test plugin description",
            IsPublic = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    #endregion
}
