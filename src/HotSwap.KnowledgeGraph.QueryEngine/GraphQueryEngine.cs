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
    private readonly DijkstraPathFinder _dijkstraPathFinder;
    private readonly QueryCacheService _cacheService;

    /// <summary>
    /// Initializes a new instance of the GraphQueryEngine.
    /// </summary>
    /// <param name="repository">The graph repository for data access.</param>
    /// <param name="cacheDurationSeconds">Cache entry TTL in seconds. Default: 300 (5 minutes).</param>
    /// <exception cref="ArgumentNullException">Thrown when repository is null.</exception>
    public GraphQueryEngine(IGraphRepository repository, int cacheDurationSeconds = 300)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _traversalService = new GraphTraversalService(repository);
        _dijkstraPathFinder = new DijkstraPathFinder(repository);
        _cacheService = new QueryCacheService(cacheDurationSeconds);
    }

    /// <inheritdoc/>
    public async Task<GraphQueryResult> ExecuteQueryAsync(GraphQuery query, CancellationToken cancellationToken = default)
    {
        if (query == null)
        {
            throw new ArgumentNullException(nameof(query));
        }

        // Check cache first
        if (_cacheService.TryGet(query, out var cachedResult))
        {
            return cachedResult!;
        }

        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Cache miss - execute query via repository
            var result = await _repository.ExecuteQueryAsync(query, cancellationToken);

            stopwatch.Stop();

            // Update execution time if not set by repository
            if (result.ExecutionTime == TimeSpan.Zero)
            {
                result = new GraphQueryResult
                {
                    Entities = result.Entities,
                    Relationships = result.Relationships,
                    TotalCount = result.TotalCount,
                    ExecutionTime = stopwatch.Elapsed,
                    QueryPlan = result.QueryPlan,
                    TraceId = result.TraceId,
                    FromCache = false,
                    Warnings = result.Warnings
                };
            }

            // Store in cache for future requests
            _cacheService.Set(query, result);

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
        // Use Dijkstra's algorithm for weighted shortest path
        // Dijkstra handles both weighted and unweighted graphs optimally
        // (unweighted is just the special case where all weights = 1.0)
        return await _dijkstraPathFinder.FindShortestPathAsync(sourceId, targetId, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<List<PathResult>> FindAllPathsAsync(Guid sourceId, Guid targetId, int maxDepth = 5, CancellationToken cancellationToken = default)
    {
        // Use DFS to find all paths up to maxDepth
        return await _traversalService.DepthFirstSearchAsync(sourceId, targetId, maxDepth, cancellationToken);
    }
}
