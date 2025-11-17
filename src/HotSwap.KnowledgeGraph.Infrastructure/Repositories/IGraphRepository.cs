using HotSwap.KnowledgeGraph.Domain.Models;

namespace HotSwap.KnowledgeGraph.Infrastructure.Repositories;

/// <summary>
/// Repository interface for knowledge graph operations.
/// Provides methods for CRUD operations on entities and relationships.
/// </summary>
public interface IGraphRepository
{
    // Entity operations
    Task<Entity?> GetEntityByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<Entity>> GetEntitiesByTypeAsync(string type, int skip = 0, int take = 100, CancellationToken cancellationToken = default);
    Task<Entity> CreateEntityAsync(Entity entity, CancellationToken cancellationToken = default);
    Task<Entity> UpdateEntityAsync(Entity entity, CancellationToken cancellationToken = default);
    Task<bool> DeleteEntityAsync(Guid id, CancellationToken cancellationToken = default);

    // Relationship operations
    Task<Relationship?> GetRelationshipByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<Relationship>> GetRelationshipsByEntityAsync(Guid entityId, bool includeOutgoing = true, bool includeIncoming = true, CancellationToken cancellationToken = default);
    Task<Relationship> CreateRelationshipAsync(Relationship relationship, CancellationToken cancellationToken = default);
    Task<bool> DeleteRelationshipAsync(Guid id, CancellationToken cancellationToken = default);

    // Schema operations
    Task<GraphSchema?> GetSchemaByVersionAsync(string version, CancellationToken cancellationToken = default);
    Task<GraphSchema?> GetLatestSchemaAsync(CancellationToken cancellationToken = default);
    Task<GraphSchema> CreateSchemaAsync(GraphSchema schema, CancellationToken cancellationToken = default);

    // Query operations
    Task<GraphQueryResult> ExecuteQueryAsync(GraphQuery query, CancellationToken cancellationToken = default);
}
