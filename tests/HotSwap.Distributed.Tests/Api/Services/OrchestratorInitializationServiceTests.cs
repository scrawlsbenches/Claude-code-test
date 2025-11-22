using FluentAssertions;
using HotSwap.Distributed.Api.Services;
using HotSwap.Distributed.Orchestrator.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace HotSwap.Distributed.Tests.Api.Services;

public class OrchestratorInitializationServiceTests
{
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly Mock<IServiceScope> _mockServiceScope;
    private readonly Mock<IServiceScopeFactory> _mockServiceScopeFactory;
    private readonly Mock<DistributedKernelOrchestrator> _mockOrchestrator;
    private readonly OrchestratorInitializationService _service;

    public OrchestratorInitializationServiceTests()
    {
        // Create mock orchestrator with required constructor parameters
        var mockLogger = new Mock<ILogger<DistributedKernelOrchestrator>>();
        var mockLoggerFactory = new Mock<ILoggerFactory>();
        mockLoggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>()))
            .Returns(NullLogger.Instance);

        _mockOrchestrator = new Mock<DistributedKernelOrchestrator>(
            mockLogger.Object,
            mockLoggerFactory.Object,
            null!, null!, null!, null!, null!, null!, null!);

        _mockServiceScope = new Mock<IServiceScope>();
        _mockServiceScopeFactory = new Mock<IServiceScopeFactory>();
        _mockServiceProvider = new Mock<IServiceProvider>();

        // Setup service scope factory
        var scopeServiceProvider = new Mock<IServiceProvider>();
        scopeServiceProvider.Setup(x => x.GetService(typeof(DistributedKernelOrchestrator)))
            .Returns(_mockOrchestrator.Object);

        _mockServiceScope.Setup(x => x.ServiceProvider).Returns(scopeServiceProvider.Object);
        _mockServiceScopeFactory.Setup(x => x.CreateScope()).Returns(_mockServiceScope.Object);

        _mockServiceProvider.Setup(x => x.GetService(typeof(IServiceScopeFactory)))
            .Returns(_mockServiceScopeFactory.Object);

        _service = new OrchestratorInitializationService(
            _mockServiceProvider.Object,
            NullLogger<OrchestratorInitializationService>.Instance);
    }

    [Fact]
    public async Task StartAsync_InitializesClusters()
    {
        // Arrange
        _mockOrchestrator.Setup(x => x.InitializeClustersAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.StartAsync(CancellationToken.None);

        // Assert
        _mockOrchestrator.Verify(x => x.InitializeClustersAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StartAsync_CreatesServiceScope()
    {
        // Arrange
        _mockOrchestrator.Setup(x => x.InitializeClustersAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.StartAsync(CancellationToken.None);

        // Assert
        _mockServiceScopeFactory.Verify(x => x.CreateScope(), Times.Once);
    }

    [Fact]
    public async Task StartAsync_DisposesServiceScopeAfterInitialization()
    {
        // Arrange
        _mockOrchestrator.Setup(x => x.InitializeClustersAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.StartAsync(CancellationToken.None);

        // Assert
        _mockServiceScope.Verify(x => x.Dispose(), Times.Once);
    }

    [Fact]
    public async Task StartAsync_WithCancellationToken_PassesToInitialization()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        var capturedToken = CancellationToken.None;

        _mockOrchestrator.Setup(x => x.InitializeClustersAsync(It.IsAny<CancellationToken>()))
            .Callback<CancellationToken>(ct => capturedToken = ct)
            .Returns(Task.CompletedTask);

        // Act
        await _service.StartAsync(cts.Token);

        // Assert
        capturedToken.Should().Be(cts.Token);
    }

    [Fact]
    public async Task StartAsync_WithException_ThrowsAndDisposesScope()
    {
        // Arrange
        _mockOrchestrator.Setup(x => x.InitializeClustersAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Initialization failed"));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await _service.StartAsync(CancellationToken.None));

        _mockServiceScope.Verify(x => x.Dispose(), Times.Once);
    }

    [Fact]
    public async Task StartAsync_WithNullOrchestrator_ThrowsInvalidOperationException()
    {
        // Arrange
        var scopeServiceProvider = new Mock<IServiceProvider>();
        scopeServiceProvider.Setup(x => x.GetService(typeof(DistributedKernelOrchestrator)))
            .Returns(null!);

        var mockScope = new Mock<IServiceScope>();
        mockScope.Setup(x => x.ServiceProvider).Returns(scopeServiceProvider.Object);

        var mockScopeFactory = new Mock<IServiceScopeFactory>();
        mockScopeFactory.Setup(x => x.CreateScope()).Returns(mockScope.Object);

        var mockProvider = new Mock<IServiceProvider>();
        mockProvider.Setup(x => x.GetService(typeof(IServiceScopeFactory)))
            .Returns(mockScopeFactory.Object);

        var service = new OrchestratorInitializationService(
            mockProvider.Object,
            NullLogger<OrchestratorInitializationService>.Instance);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await service.StartAsync(CancellationToken.None));

        mockScope.Verify(x => x.Dispose(), Times.Once);
    }

    [Fact]
    public async Task StopAsync_CompletesSuccessfully()
    {
        // Arrange
        _mockOrchestrator.Setup(x => x.InitializeClustersAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _service.StartAsync(CancellationToken.None);

        // Act & Assert
        await _service.StopAsync(CancellationToken.None); // Should not throw
    }

    [Fact]
    public async Task StartAsync_ExecutesOnlyOnce()
    {
        // Arrange
        _mockOrchestrator.Setup(x => x.InitializeClustersAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.StartAsync(CancellationToken.None);
        await _service.StartAsync(CancellationToken.None);

        // Assert - should not reinitialize on subsequent starts
        _mockOrchestrator.Verify(x => x.InitializeClustersAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
