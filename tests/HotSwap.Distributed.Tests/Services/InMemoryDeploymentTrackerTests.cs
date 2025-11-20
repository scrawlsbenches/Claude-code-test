using FluentAssertions;
using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Deployments;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace HotSwap.Distributed.Tests.Services;

public class InMemoryDeploymentTrackerTests
{
    private readonly IMemoryCache _cache;
    private readonly Mock<ILogger<InMemoryDeploymentTracker>> _mockLogger;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly InMemoryDeploymentTracker _tracker;

    public InMemoryDeploymentTrackerTests()
    {
        _cache = new MemoryCache(new MemoryCacheOptions());
        _mockLogger = new Mock<ILogger<InMemoryDeploymentTracker>>();
        _mockConfiguration = new Mock<IConfiguration>();

        // Default configuration: use Normal priority (production behavior)
        _mockConfiguration.Setup(c => c["DeploymentTracking:CachePriority"]).Returns("Normal");

        _tracker = new InMemoryDeploymentTracker(_cache, _mockLogger.Object, _mockConfiguration.Object);
    }

    [Fact]
    public async Task GetResultAsync_WithNonExistentId_ReturnsNull()
    {
        // Arrange
        var executionId = Guid.NewGuid();

        // Act
        var result = await _tracker.GetResultAsync(executionId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task StoreResultAsync_ThenGetResultAsync_ReturnsStoredResult()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        var pipelineResult = new PipelineExecutionResult
        {
            ExecutionId = executionId,
            ModuleName = "TestModule",
            Version = new Version(1, 0, 0),
            Success = true,
            StartTime = DateTime.UtcNow.AddMinutes(-5),
            EndTime = DateTime.UtcNow,
            StageResults = new List<PipelineStageResult>(),
            TraceId = executionId.ToString()
        };

        // Act
        await _tracker.StoreResultAsync(executionId, pipelineResult);
        var retrieved = await _tracker.GetResultAsync(executionId);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved.Should().Be(pipelineResult);
        retrieved!.ExecutionId.Should().Be(executionId);
        retrieved.ModuleName.Should().Be("TestModule");
        retrieved.Success.Should().BeTrue();
    }

    [Fact]
    public async Task GetInProgressAsync_WithNonExistentId_ReturnsNull()
    {
        // Arrange
        var executionId = Guid.NewGuid();

        // Act
        var result = await _tracker.GetInProgressAsync(executionId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task TrackInProgressAsync_ThenGetInProgressAsync_ReturnsTrackedRequest()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        var deploymentRequest = new DeploymentRequest
        {
            Module = new ModuleDescriptor
            {
                Name = "TestModule",
                Version = new Version(1, 0, 0),
                Description = "Test",
                Author = "test@example.com"
            },
            TargetEnvironment = EnvironmentType.Production,
            RequesterEmail = "test@example.com",
            ExecutionId = executionId,
            CreatedAt = DateTime.UtcNow
        };

        // Act
        await _tracker.TrackInProgressAsync(executionId, deploymentRequest);
        var retrieved = await _tracker.GetInProgressAsync(executionId);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved.Should().Be(deploymentRequest);
        retrieved!.ExecutionId.Should().Be(executionId);
        retrieved.Module.Name.Should().Be("TestModule");
    }

    [Fact]
    public async Task RemoveInProgressAsync_RemovesTrackedDeployment()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        var deploymentRequest = new DeploymentRequest
        {
            Module = new ModuleDescriptor
            {
                Name = "TestModule",
                Version = new Version(1, 0, 0),
                Description = "Test",
                Author = "test@example.com"
            },
            TargetEnvironment = EnvironmentType.Production,
            RequesterEmail = "test@example.com",
            ExecutionId = executionId,
            CreatedAt = DateTime.UtcNow
        };

        await _tracker.TrackInProgressAsync(executionId, deploymentRequest);

        // Act
        await _tracker.RemoveInProgressAsync(executionId);
        var retrieved = await _tracker.GetInProgressAsync(executionId);

        // Assert
        retrieved.Should().BeNull();
    }

    [Fact]
    public async Task GetAllResultsAsync_WithNoResults_ReturnsEmptyList()
    {
        // Act
        var results = await _tracker.GetAllResultsAsync();

        // Assert
        results.Should().NotBeNull();
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllResultsAsync_WithMultipleResults_ReturnsAllResults()
    {
        // Arrange
        var result1 = new PipelineExecutionResult
        {
            ExecutionId = Guid.NewGuid(),
            ModuleName = "Module1",
            Version = new Version(1, 0, 0),
            Success = true,
            StartTime = DateTime.UtcNow.AddMinutes(-10),
            EndTime = DateTime.UtcNow.AddMinutes(-5),
            StageResults = new List<PipelineStageResult>(),
            TraceId = Guid.NewGuid().ToString()
        };

        var result2 = new PipelineExecutionResult
        {
            ExecutionId = Guid.NewGuid(),
            ModuleName = "Module2",
            Version = new Version(2, 0, 0),
            Success = false,
            StartTime = DateTime.UtcNow.AddMinutes(-5),
            EndTime = DateTime.UtcNow,
            StageResults = new List<PipelineStageResult>(),
            TraceId = Guid.NewGuid().ToString()
        };

        await _tracker.StoreResultAsync(result1.ExecutionId, result1);
        await _tracker.StoreResultAsync(result2.ExecutionId, result2);

        // Act
        var results = await _tracker.GetAllResultsAsync();

        // Assert
        results.Should().NotBeNull();
        results.Should().HaveCount(2);
        results.Should().Contain(r => r.ModuleName == "Module1");
        results.Should().Contain(r => r.ModuleName == "Module2");
    }

    [Fact]
    public async Task GetAllInProgressAsync_WithNoInProgress_ReturnsEmptyList()
    {
        // Act
        var requests = await _tracker.GetAllInProgressAsync();

        // Assert
        requests.Should().NotBeNull();
        requests.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllInProgressAsync_WithMultipleInProgress_ReturnsAllRequests()
    {
        // Arrange
        var request1 = new DeploymentRequest
        {
            Module = new ModuleDescriptor
            {
                Name = "Module1",
                Version = new Version(1, 0, 0),
                Description = "Test1",
                Author = "test1@example.com"
            },
            TargetEnvironment = EnvironmentType.Production,
            RequesterEmail = "test1@example.com",
            ExecutionId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow
        };

        var request2 = new DeploymentRequest
        {
            Module = new ModuleDescriptor
            {
                Name = "Module2",
                Version = new Version(2, 0, 0),
                Description = "Test2",
                Author = "test2@example.com"
            },
            TargetEnvironment = EnvironmentType.Staging,
            RequesterEmail = "test2@example.com",
            ExecutionId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow
        };

        await _tracker.TrackInProgressAsync(request1.ExecutionId, request1);
        await _tracker.TrackInProgressAsync(request2.ExecutionId, request2);

        // Act
        var requests = await _tracker.GetAllInProgressAsync();

        // Assert
        requests.Should().NotBeNull();
        requests.Should().HaveCount(2);
        requests.Should().Contain(r => r.Module.Name == "Module1");
        requests.Should().Contain(r => r.Module.Name == "Module2");
    }

    [Fact]
    public async Task GetAllResultsAsync_AfterRemoval_DoesNotIncludeRemovedResult()
    {
        // Arrange
        var result1 = new PipelineExecutionResult
        {
            ExecutionId = Guid.NewGuid(),
            ModuleName = "Module1",
            Version = new Version(1, 0, 0),
            Success = true,
            StartTime = DateTime.UtcNow.AddMinutes(-10),
            EndTime = DateTime.UtcNow.AddMinutes(-5),
            StageResults = new List<PipelineStageResult>(),
            TraceId = Guid.NewGuid().ToString()
        };

        var result2 = new PipelineExecutionResult
        {
            ExecutionId = Guid.NewGuid(),
            ModuleName = "Module2",
            Version = new Version(2, 0, 0),
            Success = true,
            StartTime = DateTime.UtcNow.AddMinutes(-5),
            EndTime = DateTime.UtcNow,
            StageResults = new List<PipelineStageResult>(),
            TraceId = Guid.NewGuid().ToString()
        };

        await _tracker.StoreResultAsync(result1.ExecutionId, result1);
        await _tracker.StoreResultAsync(result2.ExecutionId, result2);

        // Act - manually remove one from cache (simulating expiration)
        _cache.Remove("deployment:result:" + result1.ExecutionId);
        var results = await _tracker.GetAllResultsAsync();

        // Assert - GetAllResultsAsync should clean up expired entries
        results.Should().NotBeNull();
        results.Should().HaveCount(1);
        results.Should().Contain(r => r.ModuleName == "Module2");
        results.Should().NotContain(r => r.ModuleName == "Module1");
    }

    [Fact]
    public async Task GetAllInProgressAsync_AfterRemoval_DoesNotIncludeRemovedRequest()
    {
        // Arrange
        var request1 = new DeploymentRequest
        {
            Module = new ModuleDescriptor
            {
                Name = "Module1",
                Version = new Version(1, 0, 0),
                Description = "Test1",
                Author = "test1@example.com"
            },
            TargetEnvironment = EnvironmentType.Production,
            RequesterEmail = "test1@example.com",
            ExecutionId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow
        };

        var request2 = new DeploymentRequest
        {
            Module = new ModuleDescriptor
            {
                Name = "Module2",
                Version = new Version(2, 0, 0),
                Description = "Test2",
                Author = "test2@example.com"
            },
            TargetEnvironment = EnvironmentType.Staging,
            RequesterEmail = "test2@example.com",
            ExecutionId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow
        };

        await _tracker.TrackInProgressAsync(request1.ExecutionId, request1);
        await _tracker.TrackInProgressAsync(request2.ExecutionId, request2);

        // Act
        await _tracker.RemoveInProgressAsync(request1.ExecutionId);
        var requests = await _tracker.GetAllInProgressAsync();

        // Assert
        requests.Should().NotBeNull();
        requests.Should().HaveCount(1);
        requests.Should().Contain(r => r.Module.Name == "Module2");
        requests.Should().NotContain(r => r.Module.Name == "Module1");
    }

    [Fact]
    public async Task StoreResultAsync_WithNullResult_ShouldStoreNull()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        PipelineExecutionResult result = null!;

        // Act
        await _tracker.StoreResultAsync(executionId, result);
        var retrieved = await _tracker.GetResultAsync(executionId);

        // Assert - null should be stored in cache
        retrieved.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithNullCache_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new InMemoryDeploymentTracker(null!, _mockLogger.Object, _mockConfiguration.Object);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("cache");
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new InMemoryDeploymentTracker(_cache, null!, _mockConfiguration.Object);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public async Task Workflow_CreateDeployment_TracksAndCompletes()
    {
        // Arrange - Simulate full deployment workflow
        var executionId = Guid.NewGuid();
        var deploymentRequest = new DeploymentRequest
        {
            Module = new ModuleDescriptor
            {
                Name = "WorkflowModule",
                Version = new Version(1, 0, 0),
                Description = "Workflow test",
                Author = "test@example.com"
            },
            TargetEnvironment = EnvironmentType.Production,
            RequesterEmail = "test@example.com",
            ExecutionId = executionId,
            CreatedAt = DateTime.UtcNow
        };

        // Act - Step 1: Track as in-progress
        await _tracker.TrackInProgressAsync(executionId, deploymentRequest);
        var inProgress = await _tracker.GetInProgressAsync(executionId);
        var allInProgress = await _tracker.GetAllInProgressAsync();

        // Assert - Should be in-progress
        inProgress.Should().NotBeNull();
        allInProgress.Should().ContainSingle();

        // Act - Step 2: Complete deployment
        var completedResult = new PipelineExecutionResult
        {
            ExecutionId = executionId,
            ModuleName = "WorkflowModule",
            Version = new Version(1, 0, 0),
            Success = true,
            StartTime = deploymentRequest.CreatedAt,
            EndTime = DateTime.UtcNow,
            StageResults = new List<PipelineStageResult>(),
            TraceId = executionId.ToString()
        };

        await _tracker.RemoveInProgressAsync(executionId);
        await _tracker.StoreResultAsync(executionId, completedResult);

        var result = await _tracker.GetResultAsync(executionId);
        var allResults = await _tracker.GetAllResultsAsync();
        var stillInProgress = await _tracker.GetInProgressAsync(executionId);
        var noLongerInProgress = await _tracker.GetAllInProgressAsync();

        // Assert - Should be completed, not in-progress
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        allResults.Should().ContainSingle();
        stillInProgress.Should().BeNull();
        noLongerInProgress.Should().BeEmpty();
    }
}
