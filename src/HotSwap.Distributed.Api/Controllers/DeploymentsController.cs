using HotSwap.Distributed.Api.Models;
using HotSwap.Distributed.Api.Validation;
using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Deployments;
using HotSwap.Distributed.Orchestrator.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotSwap.Distributed.Api.Controllers;

/// <summary>
/// API endpoints for managing module deployments.
/// Requires authentication with Deployer or Admin role.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
[Authorize] // All endpoints require authentication
public class DeploymentsController : ControllerBase
{
    private readonly DistributedKernelOrchestrator _orchestrator;
    private readonly ILogger<DeploymentsController> _logger;
    private readonly IDeploymentTracker _deploymentTracker;

    public DeploymentsController(
        DistributedKernelOrchestrator orchestrator,
        ILogger<DeploymentsController> logger,
        IDeploymentTracker deploymentTracker)
    {
        _orchestrator = orchestrator;
        _logger = logger;
        _deploymentTracker = deploymentTracker;
    }

    /// <summary>
    /// Creates and executes a deployment pipeline.
    /// Requires Deployer or Admin role.
    /// </summary>
    /// <param name="request">Deployment request with module information</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Deployment execution details with 202 Accepted status</returns>
    /// <response code="202">Deployment accepted and started</response>
    /// <response code="400">Invalid request</response>
    /// <response code="401">Unauthorized - authentication required</response>
    /// <response code="403">Forbidden - requires Deployer or Admin role</response>
    /// <response code="500">Server error</response>
    [HttpPost]
    [Authorize(Roles = "Deployer,Admin")]
    [ProducesResponseType(typeof(DeploymentResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateDeployment(
        [FromBody] CreateDeploymentRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Received deployment request for {ModuleName} v{Version} to {Environment}",
            request.ModuleName, request.Version, request.TargetEnvironment);

        // Validate request - exception will be caught by ExceptionHandlingMiddleware
        DeploymentRequestValidator.ValidateAndThrow(request);

        // Create deployment request
        var deploymentRequest = new DeploymentRequest
        {
            Module = new ModuleDescriptor
            {
                Name = request.ModuleName,
                Version = Version.Parse(request.Version),
                Description = request.Description ?? string.Empty,
                Author = request.RequesterEmail
            },
            TargetEnvironment = Enum.Parse<EnvironmentType>(request.TargetEnvironment, true),
            RequesterEmail = request.RequesterEmail,
            RequireApproval = request.RequireApproval,
            Metadata = request.Metadata ?? new Dictionary<string, string>(),
            ExecutionId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow
        };

        // Track deployment immediately (before it completes)
        await _deploymentTracker.TrackInProgressAsync(deploymentRequest.ExecutionId, deploymentRequest);

        // Start deployment asynchronously
        _ = Task.Run(async () =>
        {
            try
            {
                var result = await _orchestrator.ExecuteDeploymentPipelineAsync(
                    deploymentRequest,
                    cancellationToken);

                // Store result BEFORE removing in-progress to avoid race condition with rollback
                await _deploymentTracker.StoreResultAsync(deploymentRequest.ExecutionId, result);
                await _deploymentTracker.RemoveInProgressAsync(deploymentRequest.ExecutionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Deployment {ExecutionId} failed", deploymentRequest.ExecutionId);
                await _deploymentTracker.RemoveInProgressAsync(deploymentRequest.ExecutionId);
            }
        }, cancellationToken);

        // Return 202 Accepted with execution details
        var response = new DeploymentResponse
        {
            ExecutionId = deploymentRequest.ExecutionId,
            Status = "Running",
            StartTime = DateTime.UtcNow,
            EstimatedDuration = "PT30M",
            TraceId = deploymentRequest.ExecutionId.ToString(),
            Links = new Dictionary<string, string>
            {
                ["self"] = $"/api/v1/deployments/{deploymentRequest.ExecutionId}",
                ["trace"] = $"https://jaeger.example.com/trace/{deploymentRequest.ExecutionId}"
            }
        };

        return AcceptedAtAction(
            nameof(GetDeployment),
            new { executionId = deploymentRequest.ExecutionId },
            response);
    }

    /// <summary>
    /// Gets the status and result of a deployment execution.
    /// Available to all authenticated users (Viewer, Deployer, Admin).
    /// </summary>
    /// <param name="executionId">Execution ID from deployment creation</param>
    /// <returns>Deployment execution status and results</returns>
    /// <response code="200">Deployment status retrieved</response>
    /// <response code="401">Unauthorized - authentication required</response>
    /// <response code="404">Deployment not found</response>
    [HttpGet("{executionId}")]
    [Authorize(Roles = "Viewer,Deployer,Admin")]
    [ProducesResponseType(typeof(DeploymentStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDeployment(Guid executionId)
    {
        _logger.LogInformation("Getting deployment status for execution {ExecutionId}", executionId);

        // Check if deployment has completed
        var result = await _deploymentTracker.GetResultAsync(executionId);
        if (result != null)
        {
            var response = new DeploymentStatusResponse
            {
                ExecutionId = result.ExecutionId,
                ModuleName = result.ModuleName,
                Version = result.Version.ToString(),
                Status = result.Success ? "Succeeded" : "Failed",
                StartTime = result.StartTime,
                EndTime = result.EndTime,
                Duration = result.Duration.ToString(),
                Stages = result.StageResults.Select(s => new StageResult
                {
                    Name = s.StageName,
                    Status = s.Status.ToString(),
                    StartTime = s.StartTime,
                    Duration = s.Duration.ToString(),
                    Strategy = s.Strategy,
                    NodesDeployed = s.NodesDeployed,
                    NodesFailed = s.NodesFailed,
                    Message = s.Message
                }).ToList(),
                TraceId = result.TraceId
            };

            return Ok(response);
        }

        // Check if deployment is in progress - prefer pipeline state if available
        var pipelineState = await _deploymentTracker.GetPipelineStateAsync(executionId);
        if (pipelineState != null)
        {
            var inProgressResponse = new DeploymentStatusResponse
            {
                ExecutionId = pipelineState.ExecutionId,
                ModuleName = pipelineState.Request.Module.Name,
                Version = pipelineState.Request.Module.Version.ToString(),
                Status = pipelineState.Status, // Use actual status from pipeline (Running, PendingApproval, etc.)
                StartTime = pipelineState.StartTime,
                EndTime = null,
                Duration = (DateTime.UtcNow - pipelineState.StartTime).ToString(),
                Stages = pipelineState.Stages.Select(s => new StageResult
                {
                    Name = s.StageName,
                    Status = s.Status.ToString(),
                    StartTime = s.StartTime,
                    Duration = s.Duration.ToString(),
                    Strategy = s.Strategy,
                    NodesDeployed = s.NodesDeployed,
                    NodesFailed = s.NodesFailed,
                    Message = s.Message
                }).ToList(),
                TraceId = pipelineState.Request.ExecutionId.ToString()
            };

            return Ok(inProgressResponse);
        }

        // Fallback to basic in-progress tracking if pipeline state not available yet
        var inProgressRequest = await _deploymentTracker.GetInProgressAsync(executionId);
        if (inProgressRequest != null)
        {
            var inProgressResponse = new DeploymentStatusResponse
            {
                ExecutionId = inProgressRequest.ExecutionId,
                ModuleName = inProgressRequest.Module.Name,
                Version = inProgressRequest.Module.Version.ToString(),
                Status = "Running",
                StartTime = inProgressRequest.CreatedAt,
                EndTime = null,
                Duration = (DateTime.UtcNow - inProgressRequest.CreatedAt).ToString(),
                Stages = new List<StageResult>(),
                TraceId = inProgressRequest.ExecutionId.ToString()
            };

            return Ok(inProgressResponse);
        }

        // Not found in either cache
        throw new KeyNotFoundException($"Deployment execution {executionId} not found");
    }

    /// <summary>
    /// Rolls back a deployment to the previous version.
    /// Requires Deployer or Admin role.
    /// </summary>
    /// <param name="executionId">Execution ID of the deployment to rollback</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Rollback status</returns>
    /// <response code="202">Rollback accepted and started</response>
    /// <response code="401">Unauthorized - authentication required</response>
    /// <response code="403">Forbidden - requires Deployer or Admin role</response>
    /// <response code="404">Deployment not found</response>
    [HttpPost("{executionId}/rollback")]
    [Authorize(Roles = "Deployer,Admin")]
    [ProducesResponseType(typeof(RollbackResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RollbackDeployment(
        Guid executionId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Rolling back deployment {ExecutionId}", executionId);

        // Check if deployment exists (completed or in-progress)
        // Retry logic to handle race condition where deployment just completed
        PipelineExecutionResult? result = null;
        for (int attempt = 1; attempt <= 3; attempt++)
        {
            result = await _deploymentTracker.GetResultAsync(executionId);
            if (result != null)
            {
                break;
            }

            if (attempt < 3)
            {
                _logger.LogDebug("Deployment result not found on attempt {Attempt}, retrying in 100ms", attempt);
                await Task.Delay(100, cancellationToken);
            }
        }

        var inProgress = await _deploymentTracker.GetInProgressAsync(executionId);

        if (result == null && inProgress == null)
        {
            throw new KeyNotFoundException($"Deployment execution {executionId} not found");
        }

        // Can't rollback an in-progress deployment
        if (inProgress != null)
        {
            return BadRequest(new ErrorResponse
            {
                Error = "Cannot rollback a deployment that is still in progress"
            });
        }

        // Create rollback request (simplified - would need original request in production)
        var rollbackRequest = new DeploymentRequest
        {
            Module = new ModuleDescriptor
            {
                Name = result!.ModuleName,
                Version = result.Version
            },
            TargetEnvironment = EnvironmentType.Production, // Would be stored from original request
            RequesterEmail = "system@rollback",
            ExecutionId = executionId
        };

        // Execute rollback
        await _orchestrator.RollbackDeploymentAsync(rollbackRequest, cancellationToken);

        var response = new RollbackResponse
        {
            RollbackId = Guid.NewGuid(),
            Status = "InProgress",
            NodesAffected = result.StageResults.Sum(s => s.NodesDeployed ?? 0)
        };

        return Accepted(response);
    }

    /// <summary>
    /// Lists recent deployments.
    /// Available to all authenticated users (Viewer, Deployer, Admin).
    /// </summary>
    /// <returns>List of recent deployments</returns>
    /// <response code="200">Deployments retrieved</response>
    /// <response code="401">Unauthorized - authentication required</response>
    [HttpGet]
    [Authorize(Roles = "Viewer,Deployer,Admin")]
    [ProducesResponseType(typeof(List<DeploymentSummary>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ListDeployments()
    {
        _logger.LogInformation("Listing all deployments");

        var summaries = new List<DeploymentSummary>();

        // Get completed deployments
        var results = await _deploymentTracker.GetAllResultsAsync();
        foreach (var result in results)
        {
            summaries.Add(new DeploymentSummary
            {
                ExecutionId = result.ExecutionId,
                ModuleName = result.ModuleName,
                Version = result.Version.ToString(),
                Status = result.Success ? "Succeeded" : "Failed",
                StartTime = result.StartTime,
                Duration = result.Duration.ToString()
            });
        }

        // Get in-progress deployments
        var inProgress = await _deploymentTracker.GetAllInProgressAsync();
        foreach (var request in inProgress)
        {
            summaries.Add(new DeploymentSummary
            {
                ExecutionId = request.ExecutionId,
                ModuleName = request.Module.Name,
                Version = request.Module.Version.ToString(),
                Status = "Running",
                StartTime = request.CreatedAt,
                Duration = (DateTime.UtcNow - request.CreatedAt).ToString()
            });
        }

        // Sort by start time descending (most recent first)
        summaries = summaries.OrderByDescending(s => s.StartTime).ToList();

        _logger.LogInformation("Retrieved {Count} deployments", summaries.Count);
        return Ok(summaries);
    }
}
