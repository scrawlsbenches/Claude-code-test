using HotSwap.Distributed.Api.Models;
using HotSwap.Distributed.Api.Validation;
using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;
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
    private static readonly Dictionary<Guid, PipelineExecutionResult> _executionResults = new();
    private static readonly Dictionary<Guid, DeploymentRequest> _inProgressDeployments = new();

    public DeploymentsController(
        DistributedKernelOrchestrator orchestrator,
        ILogger<DeploymentsController> logger)
    {
        _orchestrator = orchestrator;
        _logger = logger;
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
        _inProgressDeployments[deploymentRequest.ExecutionId] = deploymentRequest;

        // Start deployment asynchronously
        _ = Task.Run(async () =>
        {
            try
            {
                var result = await _orchestrator.ExecuteDeploymentPipelineAsync(
                    deploymentRequest,
                    cancellationToken);

                // Move from in-progress to completed
                _inProgressDeployments.Remove(deploymentRequest.ExecutionId);
                _executionResults[deploymentRequest.ExecutionId] = result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Deployment {ExecutionId} failed", deploymentRequest.ExecutionId);
                _inProgressDeployments.Remove(deploymentRequest.ExecutionId);
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
    public IActionResult GetDeployment(Guid executionId)
    {
        _logger.LogInformation("Getting deployment status for execution {ExecutionId}", executionId);

        // Check if deployment has completed
        if (_executionResults.TryGetValue(executionId, out var result))
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

        // Check if deployment is in progress
        if (_inProgressDeployments.TryGetValue(executionId, out var inProgressRequest))
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

        // Not found in either dictionary
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
        if (!_executionResults.TryGetValue(executionId, out var result) &&
            !_inProgressDeployments.ContainsKey(executionId))
        {
            throw new KeyNotFoundException($"Deployment execution {executionId} not found");
        }

        // Can't rollback an in-progress deployment
        if (_inProgressDeployments.ContainsKey(executionId))
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
    public IActionResult ListDeployments()
    {
        // Get completed deployments
        var completedDeployments = _executionResults.Values
            .Select(r => new DeploymentSummary
            {
                ExecutionId = r.ExecutionId,
                ModuleName = r.ModuleName,
                Version = r.Version.ToString(),
                Status = r.Success ? "Succeeded" : "Failed",
                StartTime = r.StartTime,
                Duration = r.Duration.ToString()
            });

        // Get in-progress deployments
        var inProgressDeploymentsList = _inProgressDeployments.Values
            .Select(req => new DeploymentSummary
            {
                ExecutionId = req.ExecutionId,
                ModuleName = req.Module.Name,
                Version = req.Module.Version.ToString(),
                Status = "Running",
                StartTime = req.CreatedAt,
                Duration = (DateTime.UtcNow - req.CreatedAt).ToString()
            });

        // Combine and sort by start time
        var deployments = completedDeployments
            .Concat(inProgressDeploymentsList)
            .OrderByDescending(d => d.StartTime)
            .Take(50)
            .ToList();

        return Ok(deployments);
    }
}
