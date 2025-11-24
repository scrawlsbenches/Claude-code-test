using HotSwap.KnowledgeGraph.Api.Models;
using HotSwap.KnowledgeGraph.Domain.Models;
using HotSwap.KnowledgeGraph.Infrastructure.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace HotSwap.KnowledgeGraph.Api.Controllers;

/// <summary>
/// API endpoints for managing entities in the knowledge graph.
/// </summary>
[ApiController]
[Route("api/v1/graph/[controller]")]
[Produces("application/json")]
public class EntitiesController : ControllerBase
{
    private readonly IGraphRepository _repository;
    private readonly ILogger<EntitiesController> _logger;

    public EntitiesController(
        IGraphRepository repository,
        ILogger<EntitiesController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new entity in the knowledge graph.
    /// </summary>
    /// <param name="request">Entity creation request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Created entity.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(EntityResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateEntity(
        [FromBody] CreateEntityRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Creating entity of type '{Type}'", request.Type);

        var now = DateTimeOffset.UtcNow;
        var entity = new Entity
        {
            Id = Guid.NewGuid(),
            Type = request.Type,
            Properties = request.Properties,
            CreatedAt = now,
            UpdatedAt = now
        };

        var created = await _repository.CreateEntityAsync(entity, cancellationToken);

        var response = MapToResponse(created);
        return CreatedAtAction(nameof(GetEntity), new { id = created.Id }, response);
    }

    /// <summary>
    /// Creates multiple entities in bulk.
    /// </summary>
    /// <param name="request">Bulk creation request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Bulk operation result.</returns>
    [HttpPost("bulk")]
    [ProducesResponseType(typeof(BulkOperationResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateEntitiesBulk(
        [FromBody] BulkCreateEntitiesRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Bulk creating {Count} entities", request.Entities.Count);

        var createdIds = new List<Guid>();
        var errors = new List<string>();
        var now = DateTimeOffset.UtcNow;

        foreach (var entityRequest in request.Entities)
        {
            try
            {
                var entity = new Entity
                {
                    Id = Guid.NewGuid(),
                    Type = entityRequest.Type,
                    Properties = entityRequest.Properties,
                    CreatedAt = now,
                    UpdatedAt = now
                };

                await _repository.CreateEntityAsync(entity, cancellationToken);
                createdIds.Add(entity.Id);
            }
            catch (Exception ex)
            {
                errors.Add($"Failed to create entity of type '{entityRequest.Type}': {ex.Message}");
                _logger.LogWarning(ex, "Failed to create entity of type '{Type}'", entityRequest.Type);
            }
        }

        var response = new BulkOperationResponse
        {
            SuccessCount = createdIds.Count,
            FailureCount = errors.Count,
            CreatedIds = createdIds,
            Errors = errors
        };

        return StatusCode(StatusCodes.Status201Created, response);
    }

    /// <summary>
    /// Gets an entity by ID.
    /// </summary>
    /// <param name="id">Entity ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Entity if found.</returns>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(EntityResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEntity(Guid id, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetEntityByIdAsync(id, cancellationToken);

        if (entity == null)
        {
            return NotFound(new ErrorResponse { Message = $"Entity with ID {id} not found" });
        }

        return Ok(MapToResponse(entity));
    }

    /// <summary>
    /// Lists entities with optional filtering by type.
    /// </summary>
    /// <param name="type">Filter by entity type.</param>
    /// <param name="page">Page number (0-based).</param>
    /// <param name="pageSize">Number of items per page.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Paginated list of entities.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedResponse<EntityResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListEntities(
        [FromQuery] string? type = null,
        [FromQuery] int page = 0,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        pageSize = Math.Min(pageSize, 100); // Cap at 100 items per page
        var skip = page * pageSize;

        List<Entity> entities;
        int totalCount;

        if (!string.IsNullOrEmpty(type))
        {
            entities = await _repository.GetEntitiesByTypeAsync(type, skip, pageSize, cancellationToken);
            // For proper pagination, we'd need a count method in the repository
            totalCount = entities.Count < pageSize ? skip + entities.Count : skip + pageSize + 1;
        }
        else
        {
            // Get all entity types and aggregate (simplified)
            entities = await _repository.GetEntitiesByTypeAsync("", skip, pageSize, cancellationToken);
            totalCount = entities.Count < pageSize ? skip + entities.Count : skip + pageSize + 1;
        }

        var response = new PaginatedResponse<EntityResponse>
        {
            Items = entities.Select(MapToResponse).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };

        return Ok(response);
    }

    /// <summary>
    /// Updates an entity's properties.
    /// </summary>
    /// <param name="id">Entity ID.</param>
    /// <param name="request">Update request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Updated entity.</returns>
    [HttpPatch("{id:guid}")]
    [ProducesResponseType(typeof(EntityResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateEntity(
        Guid id,
        [FromBody] UpdateEntityRequest request,
        CancellationToken cancellationToken)
    {
        var entity = await _repository.GetEntityByIdAsync(id, cancellationToken);

        if (entity == null)
        {
            return NotFound(new ErrorResponse { Message = $"Entity with ID {id} not found" });
        }

        // Merge properties
        var updatedProperties = new Dictionary<string, object>(entity.Properties);
        foreach (var kvp in request.Properties)
        {
            updatedProperties[kvp.Key] = kvp.Value;
        }

        // Create updated entity (since Entity uses init properties)
        var updatedEntity = new Entity
        {
            Id = entity.Id,
            Type = entity.Type,
            Properties = updatedProperties,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = DateTimeOffset.UtcNow,
            CreatedBy = entity.CreatedBy,
            Version = entity.Version + 1
        };

        var result = await _repository.UpdateEntityAsync(updatedEntity, cancellationToken);

        return Ok(MapToResponse(result));
    }

    /// <summary>
    /// Deletes an entity.
    /// </summary>
    /// <param name="id">Entity ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>No content on success.</returns>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteEntity(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await _repository.DeleteEntityAsync(id, cancellationToken);

        if (!deleted)
        {
            return NotFound(new ErrorResponse { Message = $"Entity with ID {id} not found" });
        }

        return NoContent();
    }

    /// <summary>
    /// Gets relationships for an entity.
    /// </summary>
    /// <param name="id">Entity ID.</param>
    /// <param name="direction">Direction of relationships (outgoing, incoming, or both).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of relationships.</returns>
    [HttpGet("{id:guid}/relationships")]
    [ProducesResponseType(typeof(List<RelationshipResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEntityRelationships(
        Guid id,
        [FromQuery] string direction = "both",
        CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetEntityByIdAsync(id, cancellationToken);
        if (entity == null)
        {
            return NotFound(new ErrorResponse { Message = $"Entity with ID {id} not found" });
        }

        var includeOutgoing = direction.Equals("both", StringComparison.OrdinalIgnoreCase) ||
                             direction.Equals("outgoing", StringComparison.OrdinalIgnoreCase);
        var includeIncoming = direction.Equals("both", StringComparison.OrdinalIgnoreCase) ||
                             direction.Equals("incoming", StringComparison.OrdinalIgnoreCase);

        var relationships = await _repository.GetRelationshipsByEntityAsync(
            id, includeOutgoing, includeIncoming, cancellationToken);

        var response = relationships.Select(MapRelationshipToResponse).ToList();
        return Ok(response);
    }

    private static EntityResponse MapToResponse(Entity entity)
    {
        return new EntityResponse
        {
            Id = entity.Id,
            Type = entity.Type,
            Properties = entity.Properties,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
            CreatedBy = entity.CreatedBy,
            Version = entity.Version
        };
    }

    private static RelationshipResponse MapRelationshipToResponse(Relationship relationship)
    {
        return new RelationshipResponse
        {
            Id = relationship.Id,
            Type = relationship.Type,
            SourceEntityId = relationship.SourceEntityId,
            TargetEntityId = relationship.TargetEntityId,
            Properties = relationship.Properties,
            Weight = relationship.Weight,
            IsDirected = relationship.IsDirected,
            CreatedAt = relationship.CreatedAt
        };
    }
}
