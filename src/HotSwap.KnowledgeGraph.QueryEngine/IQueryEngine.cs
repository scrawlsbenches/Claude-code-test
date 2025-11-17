using HotSwap.KnowledgeGraph.Domain.Models;

namespace HotSwap.KnowledgeGraph.QueryEngine;

/// <summary>
/// Interface for executing graph queries.
/// Provides pattern matching, traversal, and path finding capabilities.
/// </summary>
public interface IQueryEngine
{
    /// <summary>
    /// Executes a graph query and returns matching entities and relationships.
    /// </summary>
    /// <param name="query">The query to execute.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>Query results including entities, relationships, and execution metadata.</returns>
    Task<GraphQueryResult> ExecuteQueryAsync(GraphQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the shortest path between two entities.
    /// </summary>
    /// <param name="sourceId">Source entity ID.</param>
    /// <param name="targetId">Target entity ID.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>The shortest path result, or null if no path exists.</returns>
    Task<PathResult?> FindShortestPathAsync(Guid sourceId, Guid targetId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds all paths between two entities up to a maximum depth.
    /// </summary>
    /// <param name="sourceId">Source entity ID.</param>
    /// <param name="targetId">Target entity ID.</param>
    /// <param name="maxDepth">Maximum path depth (number of hops).</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>List of all paths found within the depth limit.</returns>
    Task<List<PathResult>> FindAllPathsAsync(Guid sourceId, Guid targetId, int maxDepth = 5, CancellationToken cancellationToken = default);
}
