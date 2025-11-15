using HotSwap.Distributed.Api.Models;
using HotSwap.Distributed.Api.Validation;
using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Orchestrator.Core;
using Microsoft.AspNetCore.Mvc;

namespace HotSwap.Distributed.Api.Controllers;

/// <summary>
/// API endpoints for managing module deployments.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public class DeploymentsController : ControllerBase
{
    private readonly DistributedKernelOrchestrator _orchestrator;
    private readonly ILogger<DeploymentsController> _logger;
    private static readonly Dictionary<Guid, PipelineExecutionResult> _executionResults = new();

    public DeploymentsController(
        DistributedKernelOrchestrator orchestrator,
        ILogger<DeploymentsController> logger)
    {
        _orchestrator = orchestrator;
        _logger = logger;
    }

    /// <summary>
    /// Creates and executes a deployment pipeline.
    /// </summary>
    /// <param name="request">Deployment request with module information</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Deployment execution details with 202 Accepted status</returns>
    /// <response code="202">Deployment accepted and started</response>
    /// <response code="400">Invalid request</response>
    /// <response code="500">Server error</response>
    [HttpPost]
    [ProducesResponseType(typeof(DeploymentResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
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

        // Start deployment asynchronously
        _ = Task.Run(async () =>
        {
            var result = await _orchestrator.ExecuteDeploymentPipelineAsync(
                deploymentRequest,
                cancellationToken);

            _executionResults[deploymentRequest.ExecutionId] = result;
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
    /// </summary>
    /// <param name="executionId">Execution ID from deployment creation</param>
    /// <returns>Deployment execution status and results</returns>
    /// <response code="200">Deployment status retrieved</response>
    /// <response code="404">Deployment not found</response>
    [HttpGet("{executionId}")]
    [ProducesResponseType(typeof(DeploymentStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public IActionResult GetDeployment(Guid executionId)
    {
        _logger.LogInformation("Getting deployment status for execution {ExecutionId}", executionId);

        if (!_executionResults.TryGetValue(executionId, out var result))
        {
            throw new KeyNotFoundException($"Deployment execution {executionId} not found");
        }

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

    /// <summary>
    /// Rolls back a deployment to the previous version.
    /// </summary>
    /// <param name="executionId">Execution ID of the deployment to rollback</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Rollback status</returns>
    /// <response code="202">Rollback accepted and started</response>
    /// <response code="404">Deployment not found</response>
    [HttpPost("{executionId}/rollback")]
    [ProducesResponseType(typeof(RollbackResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RollbackDeployment(
        Guid executionId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Rolling back deployment {ExecutionId}", executionId);

        if (!_executionResults.TryGetValue(executionId, out var result))
        {
            throw new KeyNotFoundException($"Deployment execution {executionId} not found");
        }

        // Create rollback request (simplified - would need original request in production)
        var rollbackRequest = new DeploymentRequest
        {
            Module = new ModuleDescriptor
            {
                Name = result.ModuleName,
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
    /// </summary>
    /// <returns>List of recent deployments</returns>
    /// <response code="200">Deployments retrieved</response>
    [HttpGet]
    [ProducesResponseType(typeof(List<DeploymentSummary>), StatusCodes.Status200OK)]
    public IActionResult ListDeployments()
    {
        var deployments = _executionResults.Values
            .OrderByDescending(r => r.StartTime)
            .Take(50)
            .Select(r => new DeploymentSummary
            {
                ExecutionId = r.ExecutionId,
                ModuleName = r.ModuleName,
                Version = r.Version.ToString(),
                Status = r.Success ? "Succeeded" : "Failed",
                StartTime = r.StartTime,
                Duration = r.Duration.ToString()
            })
            .ToList();

        return Ok(deployments);
    }
}
