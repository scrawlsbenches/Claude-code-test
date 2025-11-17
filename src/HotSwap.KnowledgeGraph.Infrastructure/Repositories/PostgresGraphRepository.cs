using Microsoft.EntityFrameworkCore;
using HotSwap.KnowledgeGraph.Domain.Models;
using HotSwap.KnowledgeGraph.Infrastructure.Data;
using System.Diagnostics;

namespace HotSwap.KnowledgeGraph.Infrastructure.Repositories;

/// <summary>
/// PostgreSQL implementation of the graph repository.
/// Uses Entity Framework Core with Npgsql for database access.
/// </summary>
public class PostgresGraphRepository : IGraphRepository
{
    private readonly GraphDbContext _context;

    public PostgresGraphRepository(GraphDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    // Entity operations
    public async Task<Entity?> GetEntityByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Entities
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
    }

    public async Task<List<Entity>> GetEntitiesByTypeAsync(string type, int skip = 0, int take = 100, CancellationToken cancellationToken = default)
    {
        return await _context.Entities
            .AsNoTracking()
            .Where(e => e.Type == type)
            .OrderByDescending(e => e.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<Entity> CreateEntityAsync(Entity entity, CancellationToken cancellationToken = default)
    {
        _context.Entities.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task<Entity> UpdateEntityAsync(Entity entity, CancellationToken cancellationToken = default)
    {
        _context.Entities.Update(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task<bool> DeleteEntityAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Entities.FindAsync(new object[] { id }, cancellationToken);
        if (entity == null) return false;

        _context.Entities.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    // Relationship operations
    public async Task<Relationship?> GetRelationshipByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Relationships
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<List<Relationship>> GetRelationshipsByEntityAsync(
        Guid entityId,
        bool includeOutgoing = true,
        bool includeIncoming = true,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Relationships.AsNoTracking();

        if (includeOutgoing && includeIncoming)
        {
            query = query.Where(r => r.SourceEntityId == entityId || r.TargetEntityId == entityId);
        }
        else if (includeOutgoing)
        {
            query = query.Where(r => r.SourceEntityId == entityId);
        }
        else if (includeIncoming)
        {
            query = query.Where(r => r.TargetEntityId == entityId);
        }
        else
        {
            return new List<Relationship>();
        }

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<Relationship> CreateRelationshipAsync(Relationship relationship, CancellationToken cancellationToken = default)
    {
        _context.Relationships.Add(relationship);
        await _context.SaveChangesAsync(cancellationToken);
        return relationship;
    }

    public async Task<bool> DeleteRelationshipAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var relationship = await _context.Relationships.FindAsync(new object[] { id }, cancellationToken);
        if (relationship == null) return false;

        _context.Relationships.Remove(relationship);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    // Schema operations
    public async Task<GraphSchema?> GetSchemaByVersionAsync(string version, CancellationToken cancellationToken = default)
    {
        return await _context.Schemas
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Version == version, cancellationToken);
    }

    public async Task<GraphSchema?> GetLatestSchemaAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Schemas
            .AsNoTracking()
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<GraphSchema> CreateSchemaAsync(GraphSchema schema, CancellationToken cancellationToken = default)
    {
        _context.Schemas.Add(schema);
        await _context.SaveChangesAsync(cancellationToken);
        return schema;
    }

    // Query operations (simplified implementation)
    public async Task<GraphQueryResult> ExecuteQueryAsync(GraphQuery query, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        var entitiesQuery = _context.Entities.AsNoTracking();

        // Apply entity type filter
        if (!string.IsNullOrEmpty(query.EntityType))
        {
            entitiesQuery = entitiesQuery.Where(e => e.Type == query.EntityType);
        }

        // Get total count
        var totalCount = await entitiesQuery.CountAsync(cancellationToken);

        // Apply pagination
        var entities = await entitiesQuery
            .OrderByDescending(e => e.CreatedAt)
            .Skip(query.Skip)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        stopwatch.Stop();

        return new GraphQueryResult
        {
            Entities = entities,
            Relationships = new List<Relationship>(),
            TotalCount = totalCount,
            ExecutionTime = stopwatch.Elapsed,
            TraceId = query.TraceId
        };
    }
}
