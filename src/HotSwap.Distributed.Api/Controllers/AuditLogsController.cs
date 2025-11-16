using HotSwap.Distributed.Api.Models;
using HotSwap.Distributed.Infrastructure.Data.Entities;
using HotSwap.Distributed.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotSwap.Distributed.Api.Controllers;

/// <summary>
/// API endpoints for querying audit logs.
/// Requires Admin role due to sensitive nature of audit data.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
[Authorize(Roles = "Admin")] // Audit logs contain sensitive data - Admin only
public class AuditLogsController : ControllerBase
{
    private readonly IAuditLogService? _auditLogService;
    private readonly ILogger<AuditLogsController> _logger;

    public AuditLogsController(
        ILogger<AuditLogsController> logger,
        IAuditLogService? auditLogService = null)
    {
        _logger = logger;
        _auditLogService = auditLogService;
    }

    /// <summary>
    /// Gets audit logs by event category with pagination.
    /// </summary>
    /// <param name="category">Event category (e.g., "Deployment", "Approval", "Authentication")</param>
    /// <param name="pageNumber">Page number (1-based, default: 1)</param>
    /// <param name="pageSize">Number of items per page (default: 50, max: 100)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Paginated list of audit logs for the specified category</returns>
    /// <response code="200">Audit logs retrieved successfully</response>
    /// <response code="400">Invalid request parameters</response>
    /// <response code="401">Unauthorized - Admin role required</response>
    /// <response code="503">Audit log service not available</response>
    [HttpGet("category/{category}")]
    [ProducesResponseType(typeof(List<AuditLog>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetAuditLogsByCategory(
        string category,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        if (_auditLogService == null)
        {
            _logger.LogWarning("Audit log service not available");
            return StatusCode(503, new ErrorResponse
            {
                Error = "Audit log service is not configured"
            });
        }

        if (string.IsNullOrWhiteSpace(category))
        {
            return BadRequest(new ErrorResponse
            {
                Error = "Category parameter is required"
            });
        }

        if (pageNumber < 1)
        {
            return BadRequest(new ErrorResponse
            {
                Error = "Page number must be greater than 0"
            });
        }

        if (pageSize < 1 || pageSize > 100)
        {
            return BadRequest(new ErrorResponse
            {
                Error = "Page size must be between 1 and 100"
            });
        }

        _logger.LogInformation(
            "Retrieving audit logs for category {Category}, page {PageNumber}, size {PageSize}",
            category, pageNumber, pageSize);

        var auditLogs = await _auditLogService.GetAuditLogsByCategoryAsync(
            category,
            pageNumber,
            pageSize,
            cancellationToken);

        return Ok(auditLogs);
    }

    /// <summary>
    /// Gets audit logs by trace ID for distributed tracing correlation.
    /// </summary>
    /// <param name="traceId">OpenTelemetry trace ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of audit logs with the specified trace ID</returns>
    /// <response code="200">Audit logs retrieved successfully</response>
    /// <response code="400">Invalid trace ID</response>
    /// <response code="401">Unauthorized - Admin role required</response>
    /// <response code="503">Audit log service not available</response>
    [HttpGet("trace/{traceId}")]
    [ProducesResponseType(typeof(List<AuditLog>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetAuditLogsByTraceId(
        string traceId,
        CancellationToken cancellationToken = default)
    {
        if (_auditLogService == null)
        {
            _logger.LogWarning("Audit log service not available");
            return StatusCode(503, new ErrorResponse
            {
                Error = "Audit log service is not configured"
            });
        }

        if (string.IsNullOrWhiteSpace(traceId))
        {
            return BadRequest(new ErrorResponse
            {
                Error = "Trace ID parameter is required"
            });
        }

        _logger.LogInformation("Retrieving audit logs for trace ID {TraceId}", traceId);

        var auditLogs = await _auditLogService.GetAuditLogsByTraceIdAsync(traceId, cancellationToken);

        return Ok(auditLogs);
    }

    /// <summary>
    /// Gets deployment audit events for a specific deployment execution.
    /// </summary>
    /// <param name="executionId">Deployment execution ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of deployment audit events</returns>
    /// <response code="200">Deployment events retrieved successfully</response>
    /// <response code="400">Invalid execution ID</response>
    /// <response code="401">Unauthorized - Admin role required</response>
    /// <response code="503">Audit log service not available</response>
    [HttpGet("deployments/{executionId}")]
    [ProducesResponseType(typeof(List<DeploymentAuditEvent>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetDeploymentEvents(
        Guid executionId,
        CancellationToken cancellationToken = default)
    {
        if (_auditLogService == null)
        {
            _logger.LogWarning("Audit log service not available");
            return StatusCode(503, new ErrorResponse
            {
                Error = "Audit log service is not configured"
            });
        }

        if (executionId == Guid.Empty)
        {
            return BadRequest(new ErrorResponse
            {
                Error = "Valid execution ID is required"
            });
        }

        _logger.LogInformation("Retrieving deployment events for execution {ExecutionId}", executionId);

        var deploymentEvents = await _auditLogService.GetDeploymentEventsAsync(executionId, cancellationToken);

        return Ok(deploymentEvents);
    }

    /// <summary>
    /// Gets approval audit events for a specific deployment execution.
    /// </summary>
    /// <param name="executionId">Deployment execution ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of approval audit events</returns>
    /// <response code="200">Approval events retrieved successfully</response>
    /// <response code="400">Invalid execution ID</response>
    /// <response code="401">Unauthorized - Admin role required</response>
    /// <response code="503">Audit log service not available</response>
    [HttpGet("approvals/{executionId}")]
    [ProducesResponseType(typeof(List<ApprovalAuditEvent>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetApprovalEvents(
        Guid executionId,
        CancellationToken cancellationToken = default)
    {
        if (_auditLogService == null)
        {
            _logger.LogWarning("Audit log service not available");
            return StatusCode(503, new ErrorResponse
            {
                Error = "Audit log service is not configured"
            });
        }

        if (executionId == Guid.Empty)
        {
            return BadRequest(new ErrorResponse
            {
                Error = "Valid execution ID is required"
            });
        }

        _logger.LogInformation("Retrieving approval events for execution {ExecutionId}", executionId);

        var approvalEvents = await _auditLogService.GetApprovalEventsAsync(executionId, cancellationToken);

        return Ok(approvalEvents);
    }

    /// <summary>
    /// Gets authentication events for a specific user.
    /// </summary>
    /// <param name="username">Username to query</param>
    /// <param name="startDate">Start date (UTC) for filtering (optional)</param>
    /// <param name="endDate">End date (UTC) for filtering (optional)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of authentication audit events</returns>
    /// <response code="200">Authentication events retrieved successfully</response>
    /// <response code="400">Invalid request parameters</response>
    /// <response code="401">Unauthorized - Admin role required</response>
    /// <response code="503">Audit log service not available</response>
    [HttpGet("authentication/{username}")]
    [ProducesResponseType(typeof(List<AuthenticationAuditEvent>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetAuthenticationEvents(
        string username,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        CancellationToken cancellationToken = default)
    {
        if (_auditLogService == null)
        {
            _logger.LogWarning("Audit log service not available");
            return StatusCode(503, new ErrorResponse
            {
                Error = "Audit log service is not configured"
            });
        }

        if (string.IsNullOrWhiteSpace(username))
        {
            return BadRequest(new ErrorResponse
            {
                Error = "Username parameter is required"
            });
        }

        if (startDate.HasValue && endDate.HasValue && startDate > endDate)
        {
            return BadRequest(new ErrorResponse
            {
                Error = "Start date must be before end date"
            });
        }

        _logger.LogInformation(
            "Retrieving authentication events for user {Username}, date range: {StartDate} to {EndDate}",
            username, startDate, endDate);

        var authenticationEvents = await _auditLogService.GetAuthenticationEventsAsync(
            username,
            startDate,
            endDate,
            cancellationToken);

        return Ok(authenticationEvents);
    }
}
