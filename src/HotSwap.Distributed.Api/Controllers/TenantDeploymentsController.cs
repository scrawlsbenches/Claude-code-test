using HotSwap.Distributed.Api.Models;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotSwap.Distributed.Api.Controllers;

/// <summary>
/// API for tenant-scoped deployments (themes, plugins, content).
/// </summary>
[ApiController]
[Route("api/v1/tenant/deployments")]
[Produces("application/json")]
[Authorize]
public class TenantDeploymentsController : ControllerBase
{
    private readonly ITenantDeploymentService _deploymentService;
    private readonly ITenantContextService _tenantContext;
    private readonly ILogger<TenantDeploymentsController> _logger;

    public TenantDeploymentsController(
        ITenantDeploymentService deploymentService,
        ITenantContextService tenantContext,
        ILogger<TenantDeploymentsController> logger)
    {
        _deploymentService = deploymentService;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    /// <summary>
    /// Deploys a theme to website(s).
    /// </summary>
    [HttpPost("theme")]
    [ProducesResponseType(typeof(TenantDeploymentResultResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeployTheme(
        [FromBody] DeployThemeRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.GetCurrentTenantId();
        if (tenantId == null)
            return BadRequest(new ErrorResponse { Error = "Tenant context required" });

        var deploymentRequest = new TenantDeploymentRequest
        {
            TenantId = tenantId.Value,
            WebsiteId = request.WebsiteId,
            ModuleType = WebsiteModuleType.Theme,
            Scope = request.WebsiteId.HasValue ? DeploymentScope.SingleWebsite : DeploymentScope.AllTenantWebsites,
            Module = new ModuleDescriptor
            {
                Name = "Theme",
                Version = new Version("1.0.0")
            },
            TargetEnvironment = Domain.Enums.EnvironmentType.Production,
            RequesterEmail = User.Identity?.Name ?? "unknown",
            Metadata = new Dictionary<string, string>
            {
                { "themeId", request.ThemeId.ToString() }
            }
        };

        var result = await _deploymentService.DeployAsync(deploymentRequest, cancellationToken);

        return Ok(MapToResponse(result));
    }

    /// <summary>
    /// Deploys a plugin to website(s).
    /// </summary>
    [HttpPost("plugin")]
    [ProducesResponseType(typeof(TenantDeploymentResultResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeployPlugin(
        [FromBody] DeployPluginRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.GetCurrentTenantId();
        if (tenantId == null)
            return BadRequest(new ErrorResponse { Error = "Tenant context required" });

        var deploymentRequest = new TenantDeploymentRequest
        {
            TenantId = tenantId.Value,
            WebsiteId = request.WebsiteId,
            ModuleType = WebsiteModuleType.Plugin,
            Scope = request.WebsiteId.HasValue ? DeploymentScope.SingleWebsite : DeploymentScope.AllTenantWebsites,
            Module = new ModuleDescriptor
            {
                Name = "Plugin",
                Version = new Version("1.0.0")
            },
            TargetEnvironment = Domain.Enums.EnvironmentType.Production,
            RequesterEmail = User.Identity?.Name ?? "unknown",
            Metadata = new Dictionary<string, string>
            {
                { "pluginId", request.PluginId.ToString() }
            }
        };

        var result = await _deploymentService.DeployAsync(deploymentRequest, cancellationToken);

        return Ok(MapToResponse(result));
    }

    /// <summary>
    /// Gets deployment status by ID.
    /// </summary>
    [HttpGet("{deploymentId}")]
    [ProducesResponseType(typeof(TenantDeploymentResultResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDeploymentStatus(
        Guid deploymentId,
        CancellationToken cancellationToken)
    {
        var result = await _deploymentService.GetDeploymentStatusAsync(deploymentId, cancellationToken);

        if (result == null)
            return NotFound(new ErrorResponse { Error = "Deployment not found" });

        return Ok(MapToResponse(result));
    }

    /// <summary>
    /// Lists all deployments for the current tenant.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<TenantDeploymentResultResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListDeployments(CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.GetCurrentTenantId();
        if (tenantId == null)
            return BadRequest(new ErrorResponse { Error = "Tenant context required" });

        var deployments = await _deploymentService.GetDeploymentsForTenantAsync(tenantId.Value, cancellationToken);
        var responses = deployments.Select(MapToResponse).ToList();

        return Ok(responses);
    }

    /// <summary>
    /// Rolls back a deployment.
    /// </summary>
    [HttpPost("{deploymentId}/rollback")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RollbackDeployment(
        Guid deploymentId,
        CancellationToken cancellationToken)
    {
        var success = await _deploymentService.RollbackDeploymentAsync(deploymentId, cancellationToken);

        if (!success)
            return NotFound(new ErrorResponse { Error = "Deployment not found" });

        return Ok(new { message = "Deployment rollback initiated" });
    }

    private static TenantDeploymentResultResponse MapToResponse(TenantDeploymentResult result)
    {
        return new TenantDeploymentResultResponse
        {
            DeploymentId = result.DeploymentId,
            TenantId = result.TenantId,
            Success = result.Success,
            Message = result.Message,
            AffectedWebsites = result.AffectedWebsites,
            StartTime = result.StartTime,
            EndTime = result.EndTime,
            Errors = result.Errors
        };
    }
}

#region Request Models

public class DeployThemeRequest
{
    public Guid ThemeId { get; set; }
    public Guid? WebsiteId { get; set; }
}

public class DeployPluginRequest
{
    public Guid PluginId { get; set; }
    public Guid? WebsiteId { get; set; }
}

#endregion

#region Response Models

public class TenantDeploymentResultResponse
{
    public Guid DeploymentId { get; set; }
    public Guid TenantId { get; set; }
    public bool Success { get; set; }
    public required string Message { get; set; }
    public List<Guid> AffectedWebsites { get; set; } = new();
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public List<string> Errors { get; set; } = new();
}

#endregion
