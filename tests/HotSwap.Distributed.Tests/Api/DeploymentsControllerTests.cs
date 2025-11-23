using FluentAssertions;
using HotSwap.Distributed.Api.Controllers;
using HotSwap.Distributed.Api.Models;
using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Deployments;
using HotSwap.Distributed.Orchestrator.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace HotSwap.Distributed.Tests.Api;

public class DeploymentsControllerTests
{
    private readonly Mock<DistributedKernelOrchestrator> _mockOrchestrator;
    private readonly Mock<IDeploymentTracker> _mockTracker;
    private readonly DeploymentsController _controller;

    public DeploymentsControllerTests()
    {
        var mockLogger = new Mock<ILogger<DistributedKernelOrchestrator>>();
        var mockLoggerFactory = new Mock<ILoggerFactory>();
        mockLoggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>()))
            .Returns(NullLogger.Instance);

        _mockOrchestrator = new Mock<DistributedKernelOrchestrator>(
            mockLogger.Object,
            mockLoggerFactory.Object,
            null!, null!, null!, null!, null!, null!, null!);

        _mockTracker = new Mock<IDeploymentTracker>();
        _controller = new DeploymentsController(
            _mockOrchestrator.Object,
            NullLogger<DeploymentsController>.Instance,
            _mockTracker.Object);
    }

    private CreateDeploymentRequest CreateTestRequest(
        string moduleName = "test-module",
        string version = "1.0.0",
        string environment = "Production")
    {
        return new CreateDeploymentRequest
        {
            ModuleName = moduleName,
            Version = version,
            TargetEnvironment = environment,
            RequesterEmail = "test@example.com",
            RequireApproval = false,
            Description = "Test deployment",
            Metadata = new Dictionary<string, string>()
        };
    }

    private PipelineExecutionResult CreateTestResult(Guid executionId, bool success = true)
    {
        var startTime = DateTime.UtcNow.AddMinutes(-5);
        return new PipelineExecutionResult
        {
            ExecutionId = executionId,
            ModuleName = "test-module",
            Version = new Version(1, 0, 0),
            Success = success,
            StartTime = startTime,
            EndTime = DateTime.UtcNow,
            StageResults = new List<PipelineStageResult>
            {
                new PipelineStageResult
                {
                    StageName = "Deploy",
                    Status = success ? PipelineStageStatus.Succeeded : PipelineStageStatus.Failed,
                    StartTime = startTime,
                    EndTime = DateTime.UtcNow,
                    Strategy = "BlueGreen",
                    NodesDeployed = 3,
                    NodesFailed = success ? 0 : 1,
                    Message = success ? "Success" : "Failed"
                }
            },
            TraceId = executionId.ToString()
        };
    }

    #region CreateDeployment Tests

    [Fact]
    public async Task CreateDeployment_WithValidRequest_ReturnsAccepted()
    {
        // Arrange
        var request = CreateTestRequest();

        _mockTracker.Setup(x => x.TrackInProgressAsync(It.IsAny<Guid>(), It.IsAny<DeploymentRequest>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.CreateDeployment(request, CancellationToken.None);

        // Assert
        var acceptedResult = result.Should().BeOfType<AcceptedAtActionResult>().Subject;
        acceptedResult.ActionName.Should().Be(nameof(DeploymentsController.GetDeployment));

        var response = acceptedResult.Value.Should().BeOfType<DeploymentResponse>().Subject;
        response.Status.Should().Be("Running");
        response.EstimatedDuration.Should().Be("PT30M");
        response.Links.Should().ContainKey("self");
        response.Links.Should().ContainKey("trace");

        _mockTracker.Verify(x => x.TrackInProgressAsync(
            It.IsAny<Guid>(),
            It.Is<DeploymentRequest>(r =>
                r.Module.Name == "test-module" &&
                r.Module.Version == new Version(1, 0, 0) &&
                r.RequesterEmail == "test@example.com")),
            Times.Once);
    }

    [Fact]
    public async Task CreateDeployment_WithInvalidModuleName_ThrowsValidationException()
    {
        // Arrange
        var request = CreateTestRequest(moduleName: "InvalidModule");

        _mockTracker.Setup(x => x.TrackInProgressAsync(It.IsAny<Guid>(), It.IsAny<DeploymentRequest>()))
            .Returns(Task.CompletedTask);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<HotSwap.Distributed.Api.Validation.ValidationException>(() =>
            _controller.CreateDeployment(request, CancellationToken.None));
        ex.Message.Should().Contain("ModuleName");
    }

    [Fact]
    public async Task CreateDeployment_WithInvalidEnvironment_ThrowsValidationException()
    {
        // Arrange
        var request = CreateTestRequest(environment: "InvalidEnv");

        _mockTracker.Setup(x => x.TrackInProgressAsync(It.IsAny<Guid>(), It.IsAny<DeploymentRequest>()))
            .Returns(Task.CompletedTask);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<HotSwap.Distributed.Api.Validation.ValidationException>(() =>
            _controller.CreateDeployment(request, CancellationToken.None));
        ex.Message.Should().Contain("TargetEnvironment");
    }

    [Fact]
    public async Task CreateDeployment_SetsExecutionIdAndCreatedAt()
    {
        // Arrange
        var request = CreateTestRequest();
        var beforeCreation = DateTime.UtcNow;

        DeploymentRequest? capturedRequest = null;
        _mockTracker.Setup(x => x.TrackInProgressAsync(It.IsAny<Guid>(), It.IsAny<DeploymentRequest>()))
            .Callback<Guid, DeploymentRequest>((id, req) => capturedRequest = req)
            .Returns(Task.CompletedTask);

        // Act
        await _controller.CreateDeployment(request, CancellationToken.None);

        var afterCreation = DateTime.UtcNow;

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.ExecutionId.Should().NotBe(Guid.Empty);
        capturedRequest.CreatedAt.Should().BeOnOrAfter(beforeCreation).And.BeOnOrBefore(afterCreation);
    }

    [Fact]
    public async Task CreateDeployment_WithNullDescription_UsesEmptyString()
    {
        // Arrange
        var request = CreateTestRequest();
        request.Description = null;

        DeploymentRequest? capturedRequest = null;
        _mockTracker.Setup(x => x.TrackInProgressAsync(It.IsAny<Guid>(), It.IsAny<DeploymentRequest>()))
            .Callback<Guid, DeploymentRequest>((id, req) => capturedRequest = req)
            .Returns(Task.CompletedTask);

        // Act
        await _controller.CreateDeployment(request, CancellationToken.None);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Module.Description.Should().Be(string.Empty);
    }

    [Fact]
    public async Task CreateDeployment_WithNullMetadata_UsesEmptyDictionary()
    {
        // Arrange
        var request = CreateTestRequest();
        request.Metadata = null;

        DeploymentRequest? capturedRequest = null;
        _mockTracker.Setup(x => x.TrackInProgressAsync(It.IsAny<Guid>(), It.IsAny<DeploymentRequest>()))
            .Callback<Guid, DeploymentRequest>((id, req) => capturedRequest = req)
            .Returns(Task.CompletedTask);

        // Act
        await _controller.CreateDeployment(request, CancellationToken.None);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Metadata.Should().NotBeNull();
        capturedRequest.Metadata.Should().BeEmpty();
    }

    #endregion

    #region GetDeployment Tests

    [Fact]
    public async Task GetDeployment_WithCompletedDeployment_ReturnsOkWithResult()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        var result = CreateTestResult(executionId, success: true);

        _mockTracker.Setup(x => x.GetResultAsync(executionId))
            .ReturnsAsync(result);

        // Act
        var actionResult = await _controller.GetDeployment(executionId);

        // Assert
        var okResult = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<DeploymentStatusResponse>().Subject;

        response.ExecutionId.Should().Be(executionId);
        response.ModuleName.Should().Be("test-module");
        response.Version.Should().Be("1.0.0");
        response.Status.Should().Be("Succeeded");
        response.StartTime.Should().Be(result.StartTime);
        response.EndTime.Should().Be(result.EndTime);
        response.Stages.Should().HaveCount(1);
        response.Stages[0].Name.Should().Be("Deploy");
    }

    [Fact]
    public async Task GetDeployment_WithFailedDeployment_ReturnsFailedStatus()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        var result = CreateTestResult(executionId, success: false);

        _mockTracker.Setup(x => x.GetResultAsync(executionId))
            .ReturnsAsync(result);

        // Act
        var actionResult = await _controller.GetDeployment(executionId);

        // Assert
        var okResult = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<DeploymentStatusResponse>().Subject;

        response.Status.Should().Be("Failed");
    }

    [Fact]
    public async Task GetDeployment_WithPipelineStateAvailable_ReturnsPipelineState()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        var pipelineState = new PipelineExecutionState
        {
            ExecutionId = executionId,
            Status = "PendingApproval",
            StartTime = DateTime.UtcNow.AddMinutes(-2),
            Request = new DeploymentRequest
            {
                Module = new ModuleDescriptor { Name = "test-module", Version = new Version(1, 0, 0) },
                ExecutionId = executionId,
                RequesterEmail = "test@example.com"
            },
            Stages = new List<PipelineStageResult>()
        };

        _mockTracker.Setup(x => x.GetResultAsync(executionId))
            .ReturnsAsync((PipelineExecutionResult?)null);
        _mockTracker.Setup(x => x.GetPipelineStateAsync(executionId))
            .ReturnsAsync(pipelineState);

        // Act
        var actionResult = await _controller.GetDeployment(executionId);

        // Assert
        var okResult = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<DeploymentStatusResponse>().Subject;

        response.ExecutionId.Should().Be(executionId);
        response.Status.Should().Be("PendingApproval");
        response.EndTime.Should().BeNull();
    }

    [Fact]
    public async Task GetDeployment_WithInProgressDeployment_ReturnsRunningStatus()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        var inProgressRequest = new DeploymentRequest
        {
            ExecutionId = executionId,
            Module = new ModuleDescriptor { Name = "test-module", Version = new Version(1, 0, 0) },
            RequesterEmail = "test@example.com",
            CreatedAt = DateTime.UtcNow.AddMinutes(-1)
        };

        _mockTracker.Setup(x => x.GetResultAsync(executionId))
            .ReturnsAsync((PipelineExecutionResult?)null);
        _mockTracker.Setup(x => x.GetPipelineStateAsync(executionId))
            .ReturnsAsync((PipelineExecutionState?)null);
        _mockTracker.Setup(x => x.GetInProgressAsync(executionId))
            .ReturnsAsync(inProgressRequest);

        // Act
        var actionResult = await _controller.GetDeployment(executionId);

        // Assert
        var okResult = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<DeploymentStatusResponse>().Subject;

        response.ExecutionId.Should().Be(executionId);
        response.Status.Should().Be("Running");
        response.EndTime.Should().BeNull();
        response.Stages.Should().BeEmpty();
    }

    [Fact]
    public async Task GetDeployment_WithNonExistentDeployment_ThrowsKeyNotFoundException()
    {
        // Arrange
        var executionId = Guid.NewGuid();

        _mockTracker.Setup(x => x.GetResultAsync(executionId))
            .ReturnsAsync((PipelineExecutionResult?)null);
        _mockTracker.Setup(x => x.GetPipelineStateAsync(executionId))
            .ReturnsAsync((PipelineExecutionState?)null);
        _mockTracker.Setup(x => x.GetInProgressAsync(executionId))
            .ReturnsAsync((DeploymentRequest?)null);

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _controller.GetDeployment(executionId));
    }

    #endregion

    #region RollbackDeployment Tests

    [Fact]
    public async Task RollbackDeployment_WithInProgressDeployment_ReturnsBadRequest()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        var inProgressRequest = new DeploymentRequest
        {
            ExecutionId = executionId,
            Module = new ModuleDescriptor { Name = "test-module", Version = new Version(1, 0, 0) },
            RequesterEmail = "test@example.com"
        };

        _mockTracker.Setup(x => x.GetResultAsync(executionId))
            .ReturnsAsync((PipelineExecutionResult?)null);
        _mockTracker.Setup(x => x.GetInProgressAsync(executionId))
            .ReturnsAsync(inProgressRequest);

        // Act
        var actionResult = await _controller.RollbackDeployment(executionId, CancellationToken.None);

        // Assert
        var badRequestResult = actionResult.Should().BeOfType<BadRequestObjectResult>().Subject;
        var errorResponse = badRequestResult.Value.Should().BeOfType<ErrorResponse>().Subject;

        errorResponse.Error.Should().Contain("Cannot rollback a deployment that is still in progress");
    }

    [Fact]
    public async Task RollbackDeployment_WithNonExistentDeployment_ThrowsKeyNotFoundException()
    {
        // Arrange
        var executionId = Guid.NewGuid();

        _mockTracker.Setup(x => x.GetResultAsync(executionId))
            .ReturnsAsync((PipelineExecutionResult?)null);
        _mockTracker.Setup(x => x.GetInProgressAsync(executionId))
            .ReturnsAsync((DeploymentRequest?)null);

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _controller.RollbackDeployment(executionId, CancellationToken.None));
    }

    #endregion

    #region ListDeployments Tests

    [Fact]
    public async Task ListDeployments_WithCompletedAndInProgressDeployments_ReturnsAllSorted()
    {
        // Arrange
        var completedResults = new List<PipelineExecutionResult>
        {
            CreateTestResult(Guid.NewGuid(), success: true),
            CreateTestResult(Guid.NewGuid(), success: false)
        };

        var inProgressRequests = new List<DeploymentRequest>
        {
            new DeploymentRequest
            {
                ExecutionId = Guid.NewGuid(),
                Module = new ModuleDescriptor { Name = "Module1", Version = new Version(1, 0, 0) },
                RequesterEmail = "test@example.com",
                CreatedAt = DateTime.UtcNow.AddMinutes(-10)
            }
        };

        _mockTracker.Setup(x => x.GetAllResultsAsync())
            .ReturnsAsync(completedResults);
        _mockTracker.Setup(x => x.GetAllInProgressAsync())
            .ReturnsAsync(inProgressRequests);

        // Act
        var actionResult = await _controller.ListDeployments();

        // Assert
        var okResult = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        var summaries = okResult.Value.Should().BeAssignableTo<List<DeploymentSummary>>().Subject;

        summaries.Should().HaveCount(3);
        summaries.Should().Contain(s => s.Status == "Succeeded");
        summaries.Should().Contain(s => s.Status == "Failed");
        summaries.Should().Contain(s => s.Status == "Running");

        // Verify sorted by start time descending
        for (int i = 0; i < summaries.Count - 1; i++)
        {
            summaries[i].StartTime.Should().BeOnOrAfter(summaries[i + 1].StartTime);
        }
    }

    [Fact]
    public async Task ListDeployments_WithNoDeployments_ReturnsEmptyList()
    {
        // Arrange
        _mockTracker.Setup(x => x.GetAllResultsAsync())
            .ReturnsAsync(new List<PipelineExecutionResult>());
        _mockTracker.Setup(x => x.GetAllInProgressAsync())
            .ReturnsAsync(new List<DeploymentRequest>());

        // Act
        var actionResult = await _controller.ListDeployments();

        // Assert
        var okResult = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        var summaries = okResult.Value.Should().BeAssignableTo<List<DeploymentSummary>>().Subject;

        summaries.Should().BeEmpty();
    }

    [Fact]
    public async Task ListDeployments_WithOnlyCompletedDeployments_ReturnsCompletedOnly()
    {
        // Arrange
        var completedResults = new List<PipelineExecutionResult>
        {
            CreateTestResult(Guid.NewGuid(), success: true),
            CreateTestResult(Guid.NewGuid(), success: false)
        };

        _mockTracker.Setup(x => x.GetAllResultsAsync())
            .ReturnsAsync(completedResults);
        _mockTracker.Setup(x => x.GetAllInProgressAsync())
            .ReturnsAsync(new List<DeploymentRequest>());

        // Act
        var actionResult = await _controller.ListDeployments();

        // Assert
        var okResult = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        var summaries = okResult.Value.Should().BeAssignableTo<List<DeploymentSummary>>().Subject;

        summaries.Should().HaveCount(2);
        summaries.Should().AllSatisfy(s => s.Status.Should().BeOneOf("Succeeded", "Failed"));
    }

    [Fact]
    public async Task ListDeployments_WithOnlyInProgressDeployments_ReturnsInProgressOnly()
    {
        // Arrange
        var inProgressRequests = new List<DeploymentRequest>
        {
            new DeploymentRequest
            {
                ExecutionId = Guid.NewGuid(),
                Module = new ModuleDescriptor { Name = "module-1", Version = new Version(1, 0, 0) },
                RequesterEmail = "test@example.com",
                CreatedAt = DateTime.UtcNow.AddMinutes(-5)
            },
            new DeploymentRequest
            {
                ExecutionId = Guid.NewGuid(),
                Module = new ModuleDescriptor { Name = "module-2", Version = new Version(2, 0, 0) },
                RequesterEmail = "test@example.com",
                CreatedAt = DateTime.UtcNow.AddMinutes(-10)
            }
        };

        _mockTracker.Setup(x => x.GetAllResultsAsync())
            .ReturnsAsync(new List<PipelineExecutionResult>());
        _mockTracker.Setup(x => x.GetAllInProgressAsync())
            .ReturnsAsync(inProgressRequests);

        // Act
        var actionResult = await _controller.ListDeployments();

        // Assert
        var okResult = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        var summaries = okResult.Value.Should().BeAssignableTo<List<DeploymentSummary>>().Subject;

        summaries.Should().HaveCount(2);
        summaries.Should().AllSatisfy(s => s.Status.Should().Be("Running"));
    }

    #endregion
}
