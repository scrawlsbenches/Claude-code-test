using HotSwap.Distributed.Api.Models;
using HotSwap.Distributed.Api.Validation;
using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotSwap.Distributed.Api.Controllers;

/// <summary>
/// API endpoints for {ResourceDescription}.
/// {AuthorizationDescription}
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
[Authorize] // All endpoints require authentication by default
public class {ControllerName}Controller : ControllerBase
{
    // ============================================
    // Dependencies
    // ============================================
    private readonly ILogger<{ControllerName}Controller> _logger;
    private readonly I{ServiceName} _{serviceName};

    /// <summary>
    /// Initializes a new instance of the <see cref="{ControllerName}Controller"/> class.
    /// </summary>
    /// <param name="logger">Logger instance</param>
    /// <param name="{serviceName}">Service for {resource} operations</param>
    public {ControllerName}Controller(
        ILogger<{ControllerName}Controller> logger,
        I{ServiceName} {serviceName})
    {
        _logger = logger;
        _{serviceName} = {serviceName};
    }

    #region POST - Create Resource

    /// <summary>
    /// Creates a new {resource}.
    /// Requires {RequiredRole} role.
    /// </summary>
    /// <param name="request">Request containing {resource} data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created {resource} details</returns>
    /// <response code="201">Resource created successfully</response>
    /// <response code="400">Invalid request data</response>
    /// <response code="401">Unauthorized - authentication required</response>
    /// <response code="403">Forbidden - requires {RequiredRole} role</response>
    /// <response code="409">Conflict - resource already exists</response>
    /// <response code="500">Server error</response>
    [HttpPost]
    [Authorize(Roles = "{RequiredRole},Admin")]
    [ProducesResponseType(typeof({ResponseType}), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Create{ResourceName}(
        [FromBody] Create{ResourceName}Request request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Creating {ResourceName} with name: {Name}",
            nameof({ResourceName}),
            request.Name);

        // Validate request - validator throws ValidationException if invalid
        {ResourceName}Validator.ValidateAndThrow(request);

        // Map API request to domain model
        var domainModel = new {DomainModel}
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description ?? string.Empty,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = User.Identity?.Name ?? "system",
            // Map other properties
        };

        // Call service to create resource
        var result = await _{serviceName}.Create{ResourceName}Async(
            domainModel,
            cancellationToken);

        _logger.LogInformation(
            "{ResourceName} created successfully with ID: {Id}",
            nameof({ResourceName}),
            result.Id);

        // Map domain result to API response
        var response = new {ResponseType}
        {
            Id = result.Id,
            Name = result.Name,
            Description = result.Description,
            CreatedAt = result.CreatedAt,
            Links = new Dictionary<string, string>
            {
                ["self"] = $"/api/v1/{controller}/{result.Id}",
                ["update"] = $"/api/v1/{controller}/{result.Id}",
                ["delete"] = $"/api/v1/{controller}/{result.Id}"
            }
        };

        // Return 201 Created with location header
        return CreatedAtAction(
            nameof(Get{ResourceName}ById),
            new { id = result.Id },
            response);
    }

    #endregion

    #region GET - Read Resources

    /// <summary>
    /// Gets all {resources}.
    /// Available to all authenticated users.
    /// </summary>
    /// <param name="skip">Number of items to skip (pagination)</param>
    /// <param name="take">Number of items to take (default: 20, max: 100)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of {resources}</returns>
    /// <response code="200">Resources retrieved successfully</response>
    /// <response code="400">Invalid pagination parameters</response>
    /// <response code="401">Unauthorized - authentication required</response>
    [HttpGet]
    [Authorize(Roles = "Viewer,Deployer,Admin")]
    [ProducesResponseType(typeof(PaginatedResponse<{ResponseType}>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAll{ResourceName}s(
        [FromQuery] int skip = 0,
        [FromQuery] int take = 20,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Getting {ResourceName}s with pagination: skip={Skip}, take={Take}",
            nameof({ResourceName}),
            skip,
            take);

        // Validate pagination parameters
        if (skip < 0)
        {
            return BadRequest(new ErrorResponse
            {
                Error = "Invalid skip parameter",
                Details = "Skip must be non-negative"
            });
        }

        if (take <= 0 || take > 100)
        {
            return BadRequest(new ErrorResponse
            {
                Error = "Invalid take parameter",
                Details = "Take must be between 1 and 100"
            });
        }

        // Get resources from service
        var (items, totalCount) = await _{serviceName}.GetAll{ResourceName}sAsync(
            skip,
            take,
            cancellationToken);

        _logger.LogInformation(
            "Retrieved {Count} {ResourceName}s out of {Total} total",
            items.Count,
            nameof({ResourceName}),
            totalCount);

        // Map to response
        var response = new PaginatedResponse<{ResponseType}>
        {
            Items = items.Select(MapToResponse).ToList(),
            TotalCount = totalCount,
            Skip = skip,
            Take = take,
            Links = new Dictionary<string, string>
            {
                ["self"] = $"/api/v1/{controller}?skip={skip}&take={take}",
                ["first"] = $"/api/v1/{controller}?skip=0&take={take}",
                ["next"] = skip + take < totalCount
                    ? $"/api/v1/{controller}?skip={skip + take}&take={take}"
                    : null!,
                ["previous"] = skip > 0
                    ? $"/api/v1/{controller}?skip={Math.Max(0, skip - take)}&take={take}"
                    : null!
            }
        };

        return Ok(response);
    }

    /// <summary>
    /// Gets a specific {resource} by ID.
    /// Available to all authenticated users.
    /// </summary>
    /// <param name="id">Resource ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Resource details</returns>
    /// <response code="200">Resource found</response>
    /// <response code="401">Unauthorized - authentication required</response>
    /// <response code="404">Resource not found</response>
    [HttpGet("{id}")]
    [Authorize(Roles = "Viewer,Deployer,Admin")]
    [ProducesResponseType(typeof({ResponseType}), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get{ResourceName}ById(
        Guid id,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting {ResourceName} with ID: {Id}", nameof({ResourceName}), id);

        var result = await _{serviceName}.Get{ResourceName}ByIdAsync(id, cancellationToken);

        if (result == null)
        {
            _logger.LogWarning("{ResourceName} not found with ID: {Id}", nameof({ResourceName}), id);
            return NotFound(new ErrorResponse
            {
                Error = "{ResourceName} not found",
                Details = $"No {resource} found with ID: {id}"
            });
        }

        _logger.LogInformation("{ResourceName} retrieved successfully: {Id}", nameof({ResourceName}), id);

        var response = MapToResponse(result);
        return Ok(response);
    }

    #endregion

    #region PUT - Update Resource

    /// <summary>
    /// Updates an existing {resource}.
    /// Requires {RequiredRole} role.
    /// </summary>
    /// <param name="id">Resource ID to update</param>
    /// <param name="request">Updated resource data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated resource details</returns>
    /// <response code="200">Resource updated successfully</response>
    /// <response code="400">Invalid request data</response>
    /// <response code="401">Unauthorized - authentication required</response>
    /// <response code="403">Forbidden - requires {RequiredRole} role</response>
    /// <response code="404">Resource not found</response>
    /// <response code="500">Server error</response>
    [HttpPut("{id}")]
    [Authorize(Roles = "{RequiredRole},Admin")]
    [ProducesResponseType(typeof({ResponseType}), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Update{ResourceName}(
        Guid id,
        [FromBody] Update{ResourceName}Request request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Updating {ResourceName} with ID: {Id}", nameof({ResourceName}), id);

        // Validate request
        {ResourceName}Validator.ValidateUpdateAndThrow(request);

        // Map request to domain model
        var domainModel = new {DomainModel}
        {
            Id = id,
            Name = request.Name,
            Description = request.Description,
            UpdatedAt = DateTime.UtcNow,
            UpdatedBy = User.Identity?.Name ?? "system",
            // Map other properties
        };

        // Call service to update
        var result = await _{serviceName}.Update{ResourceName}Async(
            domainModel,
            cancellationToken);

        if (result == null)
        {
            _logger.LogWarning("{ResourceName} not found for update: {Id}", nameof({ResourceName}), id);
            return NotFound(new ErrorResponse
            {
                Error = "{ResourceName} not found",
                Details = $"No {resource} found with ID: {id}"
            });
        }

        _logger.LogInformation("{ResourceName} updated successfully: {Id}", nameof({ResourceName}), id);

        var response = MapToResponse(result);
        return Ok(response);
    }

    #endregion

    #region DELETE - Delete Resource

    /// <summary>
    /// Deletes a {resource}.
    /// Requires Admin role.
    /// </summary>
    /// <param name="id">Resource ID to delete</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>No content on successful deletion</returns>
    /// <response code="204">Resource deleted successfully</response>
    /// <response code="401">Unauthorized - authentication required</response>
    /// <response code="403">Forbidden - requires Admin role</response>
    /// <response code="404">Resource not found</response>
    /// <response code="500">Server error</response>
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Delete{ResourceName}(
        Guid id,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Deleting {ResourceName} with ID: {Id}", nameof({ResourceName}), id);

        var deleted = await _{serviceName}.Delete{ResourceName}Async(id, cancellationToken);

        if (!deleted)
        {
            _logger.LogWarning("{ResourceName} not found for deletion: {Id}", nameof({ResourceName}), id);
            return NotFound(new ErrorResponse
            {
                Error = "{ResourceName} not found",
                Details = $"No {resource} found with ID: {id}"
            });
        }

        _logger.LogInformation("{ResourceName} deleted successfully: {Id}", nameof({ResourceName}), id);

        return NoContent();
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Maps domain model to API response.
    /// </summary>
    /// <param name="domain">Domain model</param>
    /// <returns>API response model</returns>
    private {ResponseType} MapToResponse({DomainModel} domain)
    {
        return new {ResponseType}
        {
            Id = domain.Id,
            Name = domain.Name,
            Description = domain.Description,
            CreatedAt = domain.CreatedAt,
            UpdatedAt = domain.UpdatedAt,
            Links = new Dictionary<string, string>
            {
                ["self"] = $"/api/v1/{controller}/{domain.Id}",
                ["update"] = $"/api/v1/{controller}/{domain.Id}",
                ["delete"] = $"/api/v1/{controller}/{domain.Id}"
            }
        };
    }

    #endregion
}

/*
 * USAGE INSTRUCTIONS:
 *
 * 1. Copy this template to your controller file
 * 2. Replace placeholders:
 *    - {ControllerName} → Controller name (e.g., Users, Deployments, Approvals)
 *    - {ResourceName} → Singular resource name (e.g., User, Deployment, Approval)
 *    - {ResourceDescription} → What the resource represents
 *    - {AuthorizationDescription} → Authorization requirements
 *    - {RequiredRole} → Role needed for write operations (e.g., Deployer, Admin)
 *    - {ServiceName} → Service interface name (e.g., UserService, DeploymentService)
 *    - {serviceName} → camelCase service variable
 *    - {ResponseType} → API response model (e.g., UserResponse, DeploymentResponse)
 *    - {DomainModel} → Domain model (e.g., User, Deployment)
 *    - {resource} → lowercase resource name (e.g., user, deployment)
 *    - {resources} → lowercase plural (e.g., users, deployments)
 *    - {controller} → lowercase controller path (e.g., users, deployments)
 *
 * 3. Create required models:
 *    - Create{ResourceName}Request.cs in Api/Models/
 *    - Update{ResourceName}Request.cs in Api/Models/
 *    - {ResponseType}.cs in Api/Models/
 *    - ErrorResponse.cs (should already exist)
 *    - PaginatedResponse<T>.cs (should already exist)
 *
 * 4. Create validator:
 *    - {ResourceName}Validator.cs in Api/Validation/
 *    - Implement ValidateAndThrow() and ValidateUpdateAndThrow()
 *
 * 5. Create/update service interface:
 *    - I{ServiceName}.cs in appropriate namespace
 *    - Define all async methods used by controller
 *
 * 6. Register controller in Program.cs:
 *    builder.Services.AddControllers();
 *    // Controllers auto-registered
 *
 * 7. Remove unused endpoints:
 *    - If resource is read-only, remove POST/PUT/DELETE
 *    - If resource doesn't support listing, remove GetAll
 *    - etc.
 *
 * 8. Customize authorization:
 *    - Adjust [Authorize(Roles = "...")] per endpoint
 *    - Add [AllowAnonymous] if endpoint is public
 *    - Add custom authorization policies if needed
 *
 * BEST PRACTICES:
 *
 * - ✅ Return correct HTTP status codes (200, 201, 204, 400, 401, 403, 404, 500)
 * - ✅ Use ProducesResponseType for Swagger documentation
 * - ✅ Log all operations (Info for success, Warning for not found, Error for exceptions)
 * - ✅ Validate input using validators (throws ValidationException)
 * - ✅ Map between API models and domain models (separation of concerns)
 * - ✅ Include HATEOAS links in responses (self, related resources)
 * - ✅ Support pagination for collection endpoints
 * - ✅ Use CancellationToken for all async operations
 * - ✅ Return location header on POST (CreatedAtAction)
 * - ✅ Return 204 No Content on successful DELETE
 * - ✅ Include detailed error messages in ErrorResponse
 * - ✅ Follow RESTful conventions (GET, POST, PUT, DELETE)
 *
 * EXAMPLE:
 *
 * Example 1: Users Controller
 * - {ControllerName} → Users
 * - {ResourceName} → User
 * - {ServiceName} → UserService
 * - {ResponseType} → UserResponse
 * - {DomainModel} → User
 * - {RequiredRole} → Admin
 *
 * Example 2: Deployments Controller
 * - {ControllerName} → Deployments
 * - {ResourceName} → Deployment
 * - {ServiceName} → DeploymentOrchestrator
 * - {ResponseType} → DeploymentResponse
 * - {DomainModel} → DeploymentRequest
 * - {RequiredRole} → Deployer
 *
 * TESTING:
 *
 * Controller tests should verify:
 * 1. Correct HTTP status codes
 * 2. Request validation
 * 3. Service method calls
 * 4. Response mapping
 * 5. Error handling
 * 6. Authorization (use [Fact] with Mock<IAuthorizationService>)
 *
 * Run Swagger to test endpoints:
 * - Start app: dotnet run --project src/HotSwap.Distributed.Api/
 * - Navigate to: http://localhost:5000/swagger
 * - Test all endpoints with sample data
 *
 * COMMON HTTP STATUS CODES:
 *
 * - 200 OK: Successful GET, PUT
 * - 201 Created: Successful POST
 * - 202 Accepted: Async operation started
 * - 204 No Content: Successful DELETE
 * - 400 Bad Request: Invalid input
 * - 401 Unauthorized: Not authenticated
 * - 403 Forbidden: Not authorized (wrong role)
 * - 404 Not Found: Resource doesn't exist
 * - 409 Conflict: Resource already exists
 * - 500 Internal Server Error: Server exception
 */
