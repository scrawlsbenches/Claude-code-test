namespace HotSwap.KnowledgeGraph.Domain.Models;

/// <summary>
/// Represents the result of a graph query execution.
/// Contains matched entities, relationships, and query execution metadata.
/// </summary>
public class GraphQueryResult
{
    /// <summary>
    /// List of entities matched by the query.
    /// </summary>
    public List<Entity> Entities { get; init; } = new();

    /// <summary>
    /// List of relationships matched or traversed by the query.
    /// </summary>
    public List<Relationship> Relationships { get; init; } = new();

    /// <summary>
    /// Total count of matching entities (before pagination).
    /// Used for calculating total pages.
    /// </summary>
    public int TotalCount { get; init; }

    /// <summary>
    /// Query execution time.
    /// </summary>
    public TimeSpan ExecutionTime { get; init; }

    /// <summary>
    /// Optional query execution plan (for debugging and optimization).
    /// Shows the steps taken by the query optimizer.
    /// </summary>
    public string? QueryPlan { get; init; }

    /// <summary>
    /// Optional trace ID for distributed tracing.
    /// </summary>
    public string? TraceId { get; init; }

    /// <summary>
    /// Indicates whether the query was served from cache.
    /// </summary>
    public bool FromCache { get; init; }

    /// <summary>
    /// Optional warning messages (e.g., query timeout, partial results).
    /// </summary>
    public List<string>? Warnings { get; init; }
}

/// <summary>
/// Represents the result of a graph traversal query (e.g., shortest path, all paths).
/// Contains path information in addition to entities and relationships.
/// </summary>
public class PathResult
{
    /// <summary>
    /// List of entities in the path, ordered from source to target.
    /// </summary>
    public List<Entity> Entities { get; init; } = new();

    /// <summary>
    /// List of relationships in the path, ordered from source to target.
    /// </summary>
    public List<Relationship> Relationships { get; init; } = new();

    /// <summary>
    /// Total weight of the path (sum of relationship weights).
    /// </summary>
    public double TotalWeight { get; init; }

    /// <summary>
    /// Number of hops (relationships) in the path.
    /// </summary>
    public int Hops => Relationships.Count;

    /// <summary>
    /// Number of nodes (entities) in the path.
    /// </summary>
    public int Length => Entities.Count;
}
