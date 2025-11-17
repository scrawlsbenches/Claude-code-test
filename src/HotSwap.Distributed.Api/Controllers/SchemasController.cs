using System.Diagnostics;
using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Orchestrator.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace HotSwap.Distributed.Api.Controllers;

/// <summary>
/// Controller for managing message schemas.
/// </summary>
[ApiController]
[Route("api/v1/schemas")]
public class SchemasController : ControllerBase
{
    private static readonly ActivitySource ActivitySource = new("HotSwap.Api.Schemas", "1.0.0");
    private readonly ISchemaRegistry _schemaRegistry;
    private readonly ISchemaValidator _schemaValidator;
    private readonly ISchemaApprovalService _schemaApprovalService;
    private readonly ILogger<SchemasController> _logger;

    public SchemasController(
        ISchemaRegistry schemaRegistry,
        ISchemaValidator schemaValidator,
        ISchemaApprovalService schemaApprovalService,
        ILogger<SchemasController> logger)
    {
        _schemaRegistry = schemaRegistry ?? throw new ArgumentNullException(nameof(schemaRegistry));
        _schemaValidator = schemaValidator ?? throw new ArgumentNullException(nameof(schemaValidator));
        _schemaApprovalService = schemaApprovalService ?? throw new ArgumentNullException(nameof(schemaApprovalService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Registers a new schema.
    /// </summary>
    /// <param name="schema">The schema to register.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The registered schema.</returns>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MessageSchema>> RegisterSchema(
        [FromBody] MessageSchema schema,
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("RegisterSchema");
        activity?.SetTag("schema_id", schema.SchemaId);
        activity?.SetTag("version", schema.Version);

        try
        {
            var registeredSchema = await _schemaRegistry.RegisterSchemaAsync(schema, cancellationToken);

            _logger.LogInformation(
                "Schema '{SchemaId}' version '{Version}' registered successfully",
                schema.SchemaId,
                schema.Version);

            activity?.SetStatus(ActivityStatusCode.Ok);
            return CreatedAtAction(
                nameof(GetSchema),
                new { id = registeredSchema.SchemaId },
                registeredSchema);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid schema registration attempt for '{SchemaId}'", schema.SchemaId);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Schema '{SchemaId}' already exists", schema.SchemaId);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return Conflict(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Lists all schemas, optionally filtered by status.
    /// </summary>
    /// <param name="status">Optional status filter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of schemas.</returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<MessageSchema>>> ListSchemas(
        [FromQuery] SchemaStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("ListSchemas");
        activity?.SetTag("status_filter", status?.ToString() ?? "none");

        var schemas = await _schemaRegistry.ListSchemasAsync(status, cancellationToken);

        _logger.LogInformation("Listed {Count} schemas with filter: {Filter}", schemas.Count, status?.ToString() ?? "none");

        activity?.SetTag("schema_count", schemas.Count);
        activity?.SetStatus(ActivityStatusCode.Ok);
        return Ok(schemas);
    }

    /// <summary>
    /// Gets a specific schema by ID.
    /// </summary>
    /// <param name="id">The schema ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The schema.</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MessageSchema>> GetSchema(
        string id,
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("GetSchema");
        activity?.SetTag("schema_id", id);

        var schema = await _schemaRegistry.GetSchemaAsync(id, cancellationToken);

        if (schema == null)
        {
            _logger.LogWarning("Schema '{SchemaId}' not found", id);
            activity?.SetStatus(ActivityStatusCode.Error, "Schema not found");
            return NotFound(new { error = $"Schema '{id}' not found" });
        }

        activity?.SetStatus(ActivityStatusCode.Ok);
        return Ok(schema);
    }

    /// <summary>
    /// Validates a payload against a schema.
    /// </summary>
    /// <param name="id">The schema ID.</param>
    /// <param name="payload">The JSON payload to validate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Validation result.</returns>
    [HttpPost("{id}/validate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SchemaValidationResult>> ValidatePayload(
        string id,
        [FromBody] string payload,
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("ValidatePayload");
        activity?.SetTag("schema_id", id);
        activity?.SetTag("payload_length", payload.Length);

        var schema = await _schemaRegistry.GetSchemaAsync(id, cancellationToken);

        if (schema == null)
        {
            _logger.LogWarning("Schema '{SchemaId}' not found for validation", id);
            activity?.SetStatus(ActivityStatusCode.Error, "Schema not found");
            return NotFound(new { error = $"Schema '{id}' not found" });
        }

        var result = await _schemaValidator.ValidateAsync(payload, schema.SchemaDefinition, cancellationToken);

        activity?.SetTag("is_valid", result.IsValid);
        activity?.SetTag("error_count", result.Errors.Count);
        activity?.SetStatus(ActivityStatusCode.Ok);

        if (result.IsValid)
        {
            _logger.LogInformation("Payload validated successfully against schema '{SchemaId}'", id);
            return Ok(result);
        }

        _logger.LogWarning(
            "Payload validation failed against schema '{SchemaId}' with {ErrorCount} errors",
            id,
            result.Errors.Count);

        return BadRequest(result);
    }

    /// <summary>
    /// Approves a pending schema.
    /// </summary>
    /// <param name="id">The schema ID.</param>
    /// <param name="approvedBy">Email of the approver.</param>
    /// <param name="reason">Optional approval reason.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Success indicator.</returns>
    [HttpPost("{id}/approve")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ApproveSchema(
        string id,
        [FromQuery] string approvedBy,
        [FromQuery] string? reason = null,
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("ApproveSchema");
        activity?.SetTag("schema_id", id);
        activity?.SetTag("approved_by", approvedBy);

        try
        {
            var success = await _schemaApprovalService.ApproveSchemaAsync(id, approvedBy, reason, cancellationToken);

            if (!success)
            {
                _logger.LogWarning("Schema '{SchemaId}' not found for approval", id);
                activity?.SetStatus(ActivityStatusCode.Error, "Schema not found");
                return NotFound(new { error = $"Schema '{id}' not found" });
            }

            _logger.LogInformation("Schema '{SchemaId}' approved by '{ApprovedBy}'", id, approvedBy);
            activity?.SetStatus(ActivityStatusCode.Ok);
            return Ok(new { message = $"Schema '{id}' approved successfully", approvedBy, reason });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to approve schema '{SchemaId}'", id);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Rejects a pending schema.
    /// </summary>
    /// <param name="id">The schema ID.</param>
    /// <param name="rejectedBy">Email of the rejector.</param>
    /// <param name="reason">Rejection reason.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Success indicator.</returns>
    [HttpPost("{id}/reject")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RejectSchema(
        string id,
        [FromQuery] string rejectedBy,
        [FromQuery] string? reason = null,
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("RejectSchema");
        activity?.SetTag("schema_id", id);
        activity?.SetTag("rejected_by", rejectedBy);

        try
        {
            var success = await _schemaApprovalService.RejectSchemaAsync(id, rejectedBy, reason, cancellationToken);

            if (!success)
            {
                _logger.LogWarning("Schema '{SchemaId}' not found for rejection", id);
                activity?.SetStatus(ActivityStatusCode.Error, "Schema not found");
                return NotFound(new { error = $"Schema '{id}' not found" });
            }

            _logger.LogWarning("Schema '{SchemaId}' rejected by '{RejectedBy}'. Reason: {Reason}", id, rejectedBy, reason ?? "Not specified");
            activity?.SetStatus(ActivityStatusCode.Ok);
            return Ok(new { message = $"Schema '{id}' rejected", rejectedBy, reason });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to reject schema '{SchemaId}'", id);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Deprecates an approved schema.
    /// </summary>
    /// <param name="id">The schema ID.</param>
    /// <param name="deprecatedBy">Email of the person deprecating the schema.</param>
    /// <param name="reason">Optional deprecation reason.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Success indicator.</returns>
    [HttpPost("{id}/deprecate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeprecateSchema(
        string id,
        [FromQuery] string deprecatedBy,
        [FromQuery] string? reason = null,
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("DeprecateSchema");
        activity?.SetTag("schema_id", id);
        activity?.SetTag("deprecated_by", deprecatedBy);

        try
        {
            var success = await _schemaApprovalService.DeprecateSchemaAsync(id, deprecatedBy, reason, cancellationToken);

            if (!success)
            {
                _logger.LogWarning("Schema '{SchemaId}' not found for deprecation", id);
                activity?.SetStatus(ActivityStatusCode.Error, "Schema not found");
                return NotFound(new { error = $"Schema '{id}' not found" });
            }

            _logger.LogInformation("Schema '{SchemaId}' deprecated by '{DeprecatedBy}'. Reason: {Reason}", id, deprecatedBy, reason ?? "Not specified");
            activity?.SetStatus(ActivityStatusCode.Ok);
            return Ok(new { message = $"Schema '{id}' deprecated successfully", deprecatedBy, reason });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to deprecate schema '{SchemaId}'", id);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return BadRequest(new { error = ex.Message });
        }
    }
}
