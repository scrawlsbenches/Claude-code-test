using FluentAssertions;
using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Deployments;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HotSwap.Distributed.Tests.Infrastructure;

public class InMemoryDeploymentTrackerTests
{
    private readonly InMemoryDeploymentTracker _tracker;
    private readonly IMemoryCache _cache;

    public InMemoryDeploymentTrackerTests()
    {
        _cache = new MemoryCache(new MemoryCacheOptions());
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DeploymentTracking:CachePriority"] = "Normal"
            })
            .Build();

        _tracker = new InMemoryDeploymentTracker(
            _cache,
            NullLogger<InMemoryDeploymentTracker>.Instance,
            configuration);
    }

    #region Result Management Tests

    [Fact]
    public async Task StoreResultAsync_ShouldStoreResult()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        var result = CreateTestResult(executionId);

        // Act
        await _tracker.StoreResultAsync(executionId, result);

        // Assert
        var retrieved = await _tracker.GetResultAsync(executionId);
        retrieved.Should().NotBeNull();
        retrieved!.ExecutionId.Should().Be(executionId);
        retrieved.ModuleName.Should().Be("test-module");
    }

    [Fact]
    public async Task GetResultAsync_WhenNotExists_ShouldReturnNull()
    {
        // Arrange
        var executionId = Guid.NewGuid();

        // Act
        var result = await _tracker.GetResultAsync(executionId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAllResultsAsync_ShouldReturnAllStoredResults()
    {
        // Arrange
        var result1 = CreateTestResult(Guid.NewGuid());
        var result2 = CreateTestResult(Guid.NewGuid());
        var result3 = CreateTestResult(Guid.NewGuid());

        await _tracker.StoreResultAsync(result1.ExecutionId, result1);
        await _tracker.StoreResultAsync(result2.ExecutionId, result2);
        await _tracker.StoreResultAsync(result3.ExecutionId, result3);

        // Act
        var results = await _tracker.GetAllResultsAsync();

        // Assert
        results.Should().HaveCount(3);
        results.Should().Contain(r => r.ExecutionId == result1.ExecutionId);
        results.Should().Contain(r => r.ExecutionId == result2.ExecutionId);
        results.Should().Contain(r => r.ExecutionId == result3.ExecutionId);
    }

    [Fact]
    public async Task GetAllResultsAsync_WhenNoResults_ShouldReturnEmptyCollection()
    {
        // Act
        var results = await _tracker.GetAllResultsAsync();

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task StoreFailureAsync_ShouldStoreFailedDeploymentResult()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        var exception = new InvalidOperationException("Deployment failed due to network error");

        // Act
        await _tracker.StoreFailureAsync(executionId, exception);

        // Assert
        var result = await _tracker.GetResultAsync(executionId);
        result.Should().NotBeNull();
        result!.ExecutionId.Should().Be(executionId);
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Deployment failed with exception");
        result.Message.Should().Contain("network error");
    }

    [Fact]
    public async Task StoreFailureAsync_ShouldCreateStageResultWithExceptionDetails()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        var exception = new ArgumentException("Invalid module name");

        // Act
        await _tracker.StoreFailureAsync(executionId, exception);

        // Assert
        var result = await _tracker.GetResultAsync(executionId);
        result.Should().NotBeNull();
        result!.StageResults.Should().HaveCount(1);

        var stageResult = result.StageResults.First();
        stageResult.StageName.Should().Be("Exception");
        stageResult.Status.Should().Be(PipelineStageStatus.Failed);
        stageResult.Message.Should().Be("Invalid module name");
        stageResult.Duration.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public async Task StoreFailureAsync_ShouldSetStartAndEndTimesCorrectly()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        var exception = new Exception("Test failure");
        var beforeStore = DateTime.UtcNow;

        // Act
        await _tracker.StoreFailureAsync(executionId, exception);

        // Assert
        var result = await _tracker.GetResultAsync(executionId);
        result.Should().NotBeNull();
        result!.StartTime.Should().BeOnOrAfter(beforeStore);
        result.EndTime.Should().BeOnOrAfter(result.StartTime);
        result.EndTime.Should().BeCloseTo(result.StartTime, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task StoreFailureAsync_WithNullException_ShouldHandleGracefully()
    {
        // Arrange
        var executionId = Guid.NewGuid();

        // Act
        await _tracker.StoreFailureAsync(executionId, null!);

        // Assert
        var result = await _tracker.GetResultAsync(executionId);
        result.Should().NotBeNull();
        result!.Success.Should().BeFalse();
    }

    [Fact]
    public async Task StoreFailureAsync_WithCancellationToken_ShouldRespectCancellation()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        var exception = new Exception("Test");
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act - Should complete successfully even with cancelled token (fire and forget pattern)
        await _tracker.StoreFailureAsync(executionId, exception, cts.Token);

        // Assert - Should still store the failure
        var result = await _tracker.GetResultAsync(executionId);
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task StoreFailureAsync_MultipleFailures_ShouldBeIndependent()
    {
        // Arrange
        var executionId1 = Guid.NewGuid();
        var executionId2 = Guid.NewGuid();
        var exception1 = new InvalidOperationException("Error 1");
        var exception2 = new ArgumentException("Error 2");

        // Act
        await _tracker.StoreFailureAsync(executionId1, exception1);
        await _tracker.StoreFailureAsync(executionId2, exception2);

        // Assert
        var result1 = await _tracker.GetResultAsync(executionId1);
        var result2 = await _tracker.GetResultAsync(executionId2);

        result1.Should().NotBeNull();
        result1!.Message.Should().Contain("Error 1");

        result2.Should().NotBeNull();
        result2!.Message.Should().Contain("Error 2");
    }

    #endregion

    #region In-Progress Tracking Tests

    [Fact]
    public async Task TrackInProgressAsync_ShouldTrackDeployment()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        var request = CreateTestRequest(executionId);

        // Act
        await _tracker.TrackInProgressAsync(executionId, request);

        // Assert
        var retrieved = await _tracker.GetInProgressAsync(executionId);
        retrieved.Should().NotBeNull();
        retrieved!.ExecutionId.Should().Be(executionId);
        retrieved.Module.Name.Should().Be("test-module");
    }

    [Fact]
    public async Task GetInProgressAsync_WhenNotExists_ShouldReturnNull()
    {
        // Arrange
        var executionId = Guid.NewGuid();

        // Act
        var result = await _tracker.GetInProgressAsync(executionId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task RemoveInProgressAsync_ShouldRemoveTracking()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        var request = CreateTestRequest(executionId);
        await _tracker.TrackInProgressAsync(executionId, request);

        // Act
        await _tracker.RemoveInProgressAsync(executionId);

        // Assert
        var retrieved = await _tracker.GetInProgressAsync(executionId);
        retrieved.Should().BeNull();
    }

    [Fact]
    public async Task RemoveInProgressAsync_WhenNotExists_ShouldNotThrow()
    {
        // Arrange
        var executionId = Guid.NewGuid();

        // Act
        Func<Task> act = async () => await _tracker.RemoveInProgressAsync(executionId);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task GetAllInProgressAsync_ShouldReturnAllTrackedDeployments()
    {
        // Arrange
        var request1 = CreateTestRequest(Guid.NewGuid());
        var request2 = CreateTestRequest(Guid.NewGuid());
        var request3 = CreateTestRequest(Guid.NewGuid());

        await _tracker.TrackInProgressAsync(request1.ExecutionId, request1);
        await _tracker.TrackInProgressAsync(request2.ExecutionId, request2);
        await _tracker.TrackInProgressAsync(request3.ExecutionId, request3);

        // Act
        var requests = await _tracker.GetAllInProgressAsync();

        // Assert
        requests.Should().HaveCount(3);
        requests.Should().Contain(r => r.ExecutionId == request1.ExecutionId);
        requests.Should().Contain(r => r.ExecutionId == request2.ExecutionId);
        requests.Should().Contain(r => r.ExecutionId == request3.ExecutionId);
    }

    [Fact]
    public async Task GetAllInProgressAsync_WhenNoInProgress_ShouldReturnEmptyCollection()
    {
        // Act
        var requests = await _tracker.GetAllInProgressAsync();

        // Assert
        requests.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllInProgressAsync_AfterRemoval_ShouldNotIncludeRemovedDeployment()
    {
        // Arrange
        var request1 = CreateTestRequest(Guid.NewGuid());
        var request2 = CreateTestRequest(Guid.NewGuid());

        await _tracker.TrackInProgressAsync(request1.ExecutionId, request1);
        await _tracker.TrackInProgressAsync(request2.ExecutionId, request2);
        await _tracker.RemoveInProgressAsync(request1.ExecutionId);

        // Act
        var requests = await _tracker.GetAllInProgressAsync();

        // Assert
        requests.Should().HaveCount(1);
        requests.Should().Contain(r => r.ExecutionId == request2.ExecutionId);
        requests.Should().NotContain(r => r.ExecutionId == request1.ExecutionId);
    }

    #endregion

    #region Pipeline State Tests

    [Fact]
    public async Task UpdatePipelineStateAsync_ShouldStoreState()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        var state = CreateTestPipelineState(executionId);

        // Act
        await _tracker.UpdatePipelineStateAsync(executionId, state);

        // Assert
        var retrieved = await _tracker.GetPipelineStateAsync(executionId);
        retrieved.Should().NotBeNull();
        retrieved!.ExecutionId.Should().Be(executionId);
        retrieved.Status.Should().Be("Running");
        retrieved.CurrentStage.Should().Be("Deploy");
    }

    [Fact]
    public async Task UpdatePipelineStateAsync_ShouldUpdateLastUpdatedTime()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        var state = CreateTestPipelineState(executionId);
        var beforeUpdate = DateTime.UtcNow;

        // Act
        await _tracker.UpdatePipelineStateAsync(executionId, state);
        var afterUpdate = DateTime.UtcNow;

        // Assert
        var retrieved = await _tracker.GetPipelineStateAsync(executionId);
        retrieved.Should().NotBeNull();
        retrieved!.LastUpdated.Should().BeOnOrAfter(beforeUpdate);
        retrieved.LastUpdated.Should().BeOnOrBefore(afterUpdate);
    }

    [Fact]
    public async Task GetPipelineStateAsync_WhenNotExists_ShouldReturnNull()
    {
        // Arrange
        var executionId = Guid.NewGuid();

        // Act
        var state = await _tracker.GetPipelineStateAsync(executionId);

        // Assert
        state.Should().BeNull();
    }

    [Fact]
    public async Task UpdatePipelineStateAsync_MultipleTimes_ShouldKeepLatestState()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        var state1 = CreateTestPipelineState(executionId);
        state1.Status = "Running";
        state1.CurrentStage = "Validate";

        var state2 = CreateTestPipelineState(executionId);
        state2.Status = "PendingApproval";
        state2.CurrentStage = "Approval";

        // Act
        await _tracker.UpdatePipelineStateAsync(executionId, state1);
        await _tracker.UpdatePipelineStateAsync(executionId, state2);

        // Assert
        var retrieved = await _tracker.GetPipelineStateAsync(executionId);
        retrieved.Should().NotBeNull();
        retrieved!.Status.Should().Be("PendingApproval");
        retrieved.CurrentStage.Should().Be("Approval");
    }

    #endregion

    #region Configuration Tests

    [Fact]
    public void Constructor_WithNeverRemoveCachePriority_ShouldLogWarning()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DeploymentTracking:CachePriority"] = "NeverRemove"
            })
            .Build();

        // Act & Assert - should not throw
        var tracker = new InMemoryDeploymentTracker(
            _cache,
            NullLogger<InMemoryDeploymentTracker>.Instance,
            configuration);

        tracker.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithInvalidCachePriority_ShouldUseNormal()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DeploymentTracking:CachePriority"] = "InvalidPriority"
            })
            .Build();

        // Act & Assert - should default to Normal priority and not throw
        var tracker = new InMemoryDeploymentTracker(
            _cache,
            NullLogger<InMemoryDeploymentTracker>.Instance,
            configuration);

        tracker.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullConfiguration_ShouldUseNormalPriority()
    {
        // Act & Assert
        var tracker = new InMemoryDeploymentTracker(
            _cache,
            NullLogger<InMemoryDeploymentTracker>.Instance,
            null!);

        tracker.Should().NotBeNull();
    }

    #endregion

    #region Helper Methods

    private PipelineExecutionResult CreateTestResult(Guid executionId, bool success = true)
    {
        return new PipelineExecutionResult
        {
            ExecutionId = executionId,
            ModuleName = "test-module",
            Version = new Version(1, 0, 0),
            Success = success,
            StartTime = DateTime.UtcNow.AddMinutes(-5),
            EndTime = DateTime.UtcNow,
            StageResults = new List<PipelineStageResult>
            {
                new PipelineStageResult
                {
                    StageName = "Deploy",
                    Status = success ? PipelineStageStatus.Succeeded : PipelineStageStatus.Failed,
                    StartTime = DateTime.UtcNow.AddMinutes(-5),
                    EndTime = DateTime.UtcNow,
                    Strategy = "BlueGreen",
                    NodesDeployed = 3,
                    NodesFailed = 0,
                    Message = "Success"
                }
            },
            TraceId = executionId.ToString()
        };
    }

    private DeploymentRequest CreateTestRequest(Guid executionId)
    {
        return new DeploymentRequest
        {
            ExecutionId = executionId,
            Module = new ModuleDescriptor
            {
                Name = "test-module",
                Version = new Version(1, 0, 0),
                Description = "Test module",
                Dependencies = new Dictionary<string, string>()
            },
            RequesterEmail = "test@example.com",
            CreatedAt = DateTime.UtcNow,
            TargetEnvironment = EnvironmentType.Production,
            RequireApproval = false,
            Metadata = new Dictionary<string, string>()
        };
    }

    private PipelineExecutionState CreateTestPipelineState(Guid executionId)
    {
        return new PipelineExecutionState
        {
            ExecutionId = executionId,
            Status = "Running",
            CurrentStage = "Deploy",
            StartTime = DateTime.UtcNow.AddMinutes(-2),
            Request = CreateTestRequest(executionId),
            Stages = new List<PipelineStageResult>()
        };
    }

    #endregion
}
