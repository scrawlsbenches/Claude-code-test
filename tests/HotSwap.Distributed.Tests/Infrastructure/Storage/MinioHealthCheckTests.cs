using FluentAssertions;
using HotSwap.Distributed.Infrastructure.Storage;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace HotSwap.Distributed.Tests.Infrastructure.Storage;

public class MinioHealthCheckTests
{
    private readonly MockObjectStorageService _mockStorageService;
    private readonly Mock<ILogger<MinioHealthCheck>> _mockLogger;
    private readonly MinioHealthCheck _healthCheck;

    public MinioHealthCheckTests()
    {
        _mockStorageService = new MockObjectStorageService();
        _mockLogger = new Mock<ILogger<MinioHealthCheck>>();
        _healthCheck = new MinioHealthCheck(_mockStorageService, _mockLogger.Object);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenStorageIsHealthy_ShouldReturnHealthy()
    {
        // Arrange
        _mockStorageService.SetHealthy(true);
        var context = new HealthCheckContext();

        // Act
        var result = await _healthCheck.CheckHealthAsync(context);

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Contain("accessible");
    }

    [Fact]
    public async Task CheckHealthAsync_WhenStorageIsUnhealthy_ShouldReturnUnhealthy()
    {
        // Arrange
        _mockStorageService.SetHealthy(false);
        var context = new HealthCheckContext();

        // Act
        var result = await _healthCheck.CheckHealthAsync(context);

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("not accessible");
    }
}
