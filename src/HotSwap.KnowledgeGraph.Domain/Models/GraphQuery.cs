using HotSwap.KnowledgeGraph.Domain.Enums;

namespace HotSwap.KnowledgeGraph.Domain.Models;

/// <summary>
/// Represents a query request for searching and traversing the knowledge graph.
/// Supports pattern matching, filtering, and graph traversal with pagination.
/// </summary>
public class GraphQuery
{
    private int _maxDepth = 3;
    private int _pageSize = 100;
    private int _skip = 0;

    /// <summary>
    /// Optional filter by entity type (e.g., "Person", "Document").
    /// If null, matches all entity types.
    /// </summary>
    public string? EntityType { get; init; }

    /// <summary>
    /// Optional filters on entity properties.
    /// Key: property name, Value: expected value or operator expression.
    /// </summary>
    public Dictionary<string, object>? PropertyFilters { get; init; }

    /// <summary>
    /// Optional relationship patterns to match.
    /// Supports multi-hop graph traversal.
    /// </summary>
    public List<RelationshipPattern>? RelationshipPatterns { get; init; }

    /// <summary>
    /// Maximum depth for graph traversal.
    /// Default: 3. Must be non-negative.
    /// </summary>
    public int MaxDepth
    {
        get => _maxDepth;
        init
        {
            if (value < 0)
            {
                throw new ArgumentException("MaxDepth cannot be negative.", nameof(MaxDepth));
            }
            _maxDepth = value;
        }
    }

    /// <summary>
    /// Number of results to return per page.
    /// Default: 100. Must be positive.
    /// </summary>
    public int PageSize
    {
        get => _pageSize;
        init
        {
            if (value < 0)
            {
                throw new ArgumentException("PageSize cannot be negative.", nameof(PageSize));
            }
            _pageSize = value;
        }
    }

    /// <summary>
    /// Number of results to skip (for pagination).
    /// Default: 0. Must be non-negative.
    /// </summary>
    public int Skip
    {
        get => _skip;
        init
        {
            if (value < 0)
            {
                throw new ArgumentException("Skip cannot be negative.", nameof(Skip));
            }
            _skip = value;
        }
    }

    /// <summary>
    /// Maximum query execution time.
    /// Default: 30 seconds.
    /// </summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Optional trace ID for distributed tracing.
    /// </summary>
    public string? TraceId { get; init; }
}

/// <summary>
/// Represents a pattern for matching relationships in a graph query.
/// Used for graph traversal and multi-hop queries.
/// </summary>
public class RelationshipPattern
{
    /// <summary>
    /// Optional relationship type filter (e.g., "KNOWS", "AUTHORED_BY").
    /// If null, matches all relationship types.
    /// </summary>
    public string? RelationshipType { get; init; }

    /// <summary>
    /// Direction of relationship traversal.
    /// Default: Outgoing.
    /// </summary>
    public Direction Direction { get; init; } = Direction.Outgoing;

    /// <summary>
    /// Optional target entity type filter.
    /// If null, matches all entity types.
    /// </summary>
    public string? TargetEntityType { get; init; }

    /// <summary>
    /// Optional filters on relationship properties.
    /// </summary>
    public Dictionary<string, object>? PropertyFilters { get; init; }

    /// <summary>
    /// Optional minimum weight for weighted graphs.
    /// </summary>
    public double? MinWeight { get; init; }

    /// <summary>
    /// Optional maximum weight for weighted graphs.
    /// </summary>
    public double? MaxWeight { get; init; }
}
