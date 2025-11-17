using System.Diagnostics;
using HotSwap.KnowledgeGraph.Domain.Models;
using HotSwap.KnowledgeGraph.Infrastructure.Repositories;
using HotSwap.KnowledgeGraph.QueryEngine.Services;

namespace HotSwap.KnowledgeGraph.QueryEngine;

/// <summary>
/// PostgreSQL-based implementation of the query engine.
/// Executes graph queries using the repository pattern.
/// </summary>
public class GraphQueryEngine : IQueryEngine
{
    private readonly IGraphRepository _repository;
    private readonly GraphTraversalService _traversalService;

    /// <summary>
    /// Initializes a new instance of the GraphQueryEngine.
    /// </summary>
    /// <param name="repository">The graph repository for data access.</param>
    /// <exception cref="ArgumentNullException">Thrown when repository is null.</exception>
    public GraphQueryEngine(IGraphRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _traversalService = new GraphTraversalService(repository);
    }

    /// <inheritdoc/>
    public async Task<GraphQueryResult> ExecuteQueryAsync(GraphQuery query, CancellationToken cancellationToken = default)
    {
        if (query == null)
        {
            throw new ArgumentNullException(nameof(query));
        }

        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Delegate to repository for basic query execution
            // The repository handles entity type filtering, property filtering, and pagination
            var result = await _repository.ExecuteQueryAsync(query, cancellationToken);

            stopwatch.Stop();

            // Update execution time if not set by repository
            if (result.ExecutionTime == TimeSpan.Zero)
            {
                return new GraphQueryResult
                {
                    Entities = result.Entities,
                    Relationships = result.Relationships,
                    TotalCount = result.TotalCount,
                    ExecutionTime = stopwatch.Elapsed,
                    QueryPlan = result.QueryPlan,
                    TraceId = result.TraceId,
                    FromCache = result.FromCache,
                    Warnings = result.Warnings
                };
            }

            return result;
        }
        catch (Exception)
        {
            stopwatch.Stop();
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<PathResult?> FindShortestPathAsync(Guid sourceId, Guid targetId, CancellationToken cancellationToken = default)
    {
        // Use BFS for shortest path in unweighted graph
        // TODO: For weighted graphs, implement Dijkstra's algorithm
        return await _traversalService.BreadthFirstSearchAsync(sourceId, targetId, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<List<PathResult>> FindAllPathsAsync(Guid sourceId, Guid targetId, int maxDepth = 5, CancellationToken cancellationToken = default)
    {
        // Use DFS to find all paths up to maxDepth
        return await _traversalService.DepthFirstSearchAsync(sourceId, targetId, maxDepth, cancellationToken);
    }
}
