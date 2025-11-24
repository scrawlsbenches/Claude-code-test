using HotSwap.KnowledgeGraph.Api.Models;
using HotSwap.KnowledgeGraph.Domain.Models;
using HotSwap.KnowledgeGraph.Infrastructure.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace HotSwap.KnowledgeGraph.Api.Controllers;

/// <summary>
/// API endpoints for managing relationships in the knowledge graph.
/// </summary>
[ApiController]
[Route("api/v1/graph/[controller]")]
[Produces("application/json")]
public class RelationshipsController : ControllerBase
{
    private readonly IGraphRepository _repository;
    private readonly ILogger<RelationshipsController> _logger;

    public RelationshipsController(
        IGraphRepository repository,
        ILogger<RelationshipsController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new relationship between two entities.
    /// </summary>
    /// <param name="request">Relationship creation request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Created relationship.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(RelationshipResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateRelationship(
        [FromBody] CreateRelationshipRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Creating {Type} relationship from {Source} to {Target}",
            request.Type, request.SourceEntityId, request.TargetEntityId);

        // Verify source entity exists
        var sourceEntity = await _repository.GetEntityByIdAsync(request.SourceEntityId, cancellationToken);
        if (sourceEntity == null)
        {
            return NotFound(new ErrorResponse
            {
                Message = $"Source entity with ID {request.SourceEntityId} not found"
            });
        }

        // Verify target entity exists
        var targetEntity = await _repository.GetEntityByIdAsync(request.TargetEntityId, cancellationToken);
        if (targetEntity == null)
        {
            return NotFound(new ErrorResponse
            {
                Message = $"Target entity with ID {request.TargetEntityId} not found"
            });
        }

        var now = DateTimeOffset.UtcNow;
        var relationship = new Relationship
        {
            Id = Guid.NewGuid(),
            Type = request.Type,
            SourceEntityId = request.SourceEntityId,
            TargetEntityId = request.TargetEntityId,
            Properties = request.Properties,
            Weight = request.Weight,
            IsDirected = request.IsDirected,
            CreatedAt = now
        };

        var created = await _repository.CreateRelationshipAsync(relationship, cancellationToken);

        var response = MapToResponse(created);
        return CreatedAtAction(nameof(GetRelationship), new { id = created.Id }, response);
    }

    /// <summary>
    /// Creates multiple relationships in bulk.
    /// </summary>
    /// <param name="request">Bulk creation request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Bulk operation result.</returns>
    [HttpPost("bulk")]
    [ProducesResponseType(typeof(BulkOperationResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateRelationshipsBulk(
        [FromBody] BulkCreateRelationshipsRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Bulk creating {Count} relationships", request.Relationships.Count);

        var createdIds = new List<Guid>();
        var errors = new List<string>();
        var now = DateTimeOffset.UtcNow;

        foreach (var relRequest in request.Relationships)
        {
            try
            {
                // Verify entities exist
                var sourceEntity = await _repository.GetEntityByIdAsync(relRequest.SourceEntityId, cancellationToken);
                if (sourceEntity == null)
                {
                    errors.Add($"Source entity {relRequest.SourceEntityId} not found for {relRequest.Type} relationship");
                    continue;
                }

                var targetEntity = await _repository.GetEntityByIdAsync(relRequest.TargetEntityId, cancellationToken);
                if (targetEntity == null)
                {
                    errors.Add($"Target entity {relRequest.TargetEntityId} not found for {relRequest.Type} relationship");
                    continue;
                }

                var relationship = new Relationship
                {
                    Id = Guid.NewGuid(),
                    Type = relRequest.Type,
                    SourceEntityId = relRequest.SourceEntityId,
                    TargetEntityId = relRequest.TargetEntityId,
                    Properties = relRequest.Properties,
                    Weight = relRequest.Weight,
                    IsDirected = relRequest.IsDirected,
                    CreatedAt = now
                };

                await _repository.CreateRelationshipAsync(relationship, cancellationToken);
                createdIds.Add(relationship.Id);
            }
            catch (Exception ex)
            {
                errors.Add($"Failed to create {relRequest.Type} relationship: {ex.Message}");
                _logger.LogWarning(ex, "Failed to create {Type} relationship", relRequest.Type);
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
    /// Gets a relationship by ID.
    /// </summary>
    /// <param name="id">Relationship ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Relationship if found.</returns>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(RelationshipResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRelationship(Guid id, CancellationToken cancellationToken)
    {
        var relationship = await _repository.GetRelationshipByIdAsync(id, cancellationToken);

        if (relationship == null)
        {
            return NotFound(new ErrorResponse { Message = $"Relationship with ID {id} not found" });
        }

        return Ok(MapToResponse(relationship));
    }

    /// <summary>
    /// Deletes a relationship.
    /// </summary>
    /// <param name="id">Relationship ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>No content on success.</returns>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteRelationship(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await _repository.DeleteRelationshipAsync(id, cancellationToken);

        if (!deleted)
        {
            return NotFound(new ErrorResponse { Message = $"Relationship with ID {id} not found" });
        }

        return NoContent();
    }

    private static RelationshipResponse MapToResponse(Relationship relationship)
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
