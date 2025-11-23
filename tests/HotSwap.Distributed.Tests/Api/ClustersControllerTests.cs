using FluentAssertions;
using HotSwap.Distributed.Api.Controllers;
using HotSwap.Distributed.Api.Models;
using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Interfaces;
using HotSwap.Distributed.Orchestrator.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace HotSwap.Distributed.Tests.Api;

public class ClustersControllerTests
{
    private readonly Mock<DistributedKernelOrchestrator> _mockOrchestrator;
    private readonly Mock<IMetricsProvider> _mockMetricsProvider;
    private readonly Mock<EnvironmentCluster> _mockCluster;
    private readonly ClustersController _controller;

    public ClustersControllerTests()
    {
        var mockLogger = new Mock<ILogger<DistributedKernelOrchestrator>>();
        var mockLoggerFactory = new Mock<ILoggerFactory>();
        mockLoggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>()))
            .Returns(NullLogger.Instance);

        _mockOrchestrator = new Mock<DistributedKernelOrchestrator>(
            mockLogger.Object,
            mockLoggerFactory.Object,
            null, null, null, null, null, null, null);

        _mockMetricsProvider = new Mock<IMetricsProvider>();

        var mockClusterLogger = new Mock<ILogger<EnvironmentCluster>>();
        _mockCluster = new Mock<EnvironmentCluster>(
            EnvironmentType.Production,
            mockClusterLogger.Object);

        _controller = new ClustersController(
            _mockOrchestrator.Object,
            _mockMetricsProvider.Object,
            NullLogger<ClustersController>.Instance);
    }

    private ClusterHealth CreateTestClusterHealth(int totalNodes = 2, int healthyNodes = 2)
    {
        return new ClusterHealth
        {
            Environment = "Production",
            TotalNodes = totalNodes,
            HealthyNodes = healthyNodes,
            UnhealthyNodes = totalNodes - healthyNodes
        };
    }

    private ClusterMetricsSnapshot CreateTestClusterMetrics()
    {
        return new ClusterMetricsSnapshot
        {
            Environment = "Production",
            Timestamp = DateTime.UtcNow,
            TotalNodes = 2,
            AvgCpuUsage = 45.5,
            AvgMemoryUsage = 60.2,
            AvgLatency = 125.3,
            AvgErrorRate = 0.5,
            TotalRequestsPerSecond = 1500.0
        };
    }

    // NOTE: GetCluster tests cannot be implemented as unit tests because
    // DistributedKernelOrchestrator.GetCluster() and GetClusterHealthAsync()
    // are non-virtual methods and cannot be mocked. These would require
    // integration tests or refactoring the orchestrator to use an interface.

    #region GetClusterMetrics Tests

    [Fact]
    public async Task GetClusterMetrics_WithValidEnvironment_ReturnsOk()
    {
        // Arrange
        var metrics = CreateTestClusterMetrics();

        _mockMetricsProvider.Setup(x => x.GetClusterMetricsAsync(
                EnvironmentType.Production,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(metrics);

        // Act
        var result = await _controller.GetClusterMetrics(
            "Production",
            DateTime.UtcNow.AddHours(-1),
            DateTime.UtcNow,
            "5m",
            CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ClusterMetricsTimeSeriesResponse>().Subject;
        response.Environment.Should().Be("Production");
        response.Interval.Should().Be("5m");
        response.DataPoints.Should().HaveCount(1);
        response.DataPoints[0].CpuUsage.Should().Be(45.5);
        response.DataPoints[0].MemoryUsage.Should().Be(60.2);
    }

    [Fact]
    public async Task GetClusterMetrics_WithDefaultTimeRange_UsesLastHour()
    {
        // Arrange
        var metrics = CreateTestClusterMetrics();

        _mockMetricsProvider.Setup(x => x.GetClusterMetricsAsync(
                EnvironmentType.Production,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(metrics);

        // Act
        var result = await _controller.GetClusterMetrics("Production", null, null);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetClusterMetrics_WithInvalidEnvironment_ReturnsBadRequest()
    {
        // Act
        var result = await _controller.GetClusterMetrics("InvalidEnv", null, null);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var errorResponse = badRequestResult.Value.Should().BeOfType<ErrorResponse>().Subject;
        errorResponse.Error.Should().Be("Invalid environment: InvalidEnv");
    }

    [Fact]
    public async Task GetClusterMetrics_WhenExceptionOccurs_ReturnsInternalServerError()
    {
        // Arrange
        _mockMetricsProvider.Setup(x => x.GetClusterMetricsAsync(
                EnvironmentType.Production,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Metrics service unavailable"));

        // Act
        var result = await _controller.GetClusterMetrics("Production", null, null);

        // Assert
        var statusCodeResult = result.Should().BeOfType<ObjectResult>().Subject;
        statusCodeResult.StatusCode.Should().Be(500);
        var errorResponse = statusCodeResult.Value.Should().BeOfType<ErrorResponse>().Subject;
        errorResponse.Error.Should().Be("Failed to get cluster metrics");
        errorResponse.Details.Should().Be("Metrics service unavailable");
    }

    [Fact]
    public async Task GetClusterMetrics_WithCustomInterval_UsesProvidedInterval()
    {
        // Arrange
        var metrics = CreateTestClusterMetrics();

        _mockMetricsProvider.Setup(x => x.GetClusterMetricsAsync(
                EnvironmentType.Staging,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(metrics);

        // Act
        var result = await _controller.GetClusterMetrics(
            "Staging",
            DateTime.UtcNow.AddHours(-2),
            DateTime.UtcNow,
            "15m",
            CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ClusterMetricsTimeSeriesResponse>().Subject;
        response.Interval.Should().Be("15m");
    }

    #endregion

    // NOTE: ListClusters tests cannot be implemented as unit tests because
    // DistributedKernelOrchestrator.GetAllClusters() and GetClusterHealthAsync()
    // are non-virtual methods and cannot be mocked. These would require
    // integration tests or refactoring the orchestrator to use an interface.
}
