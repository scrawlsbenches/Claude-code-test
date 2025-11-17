using HotSwap.KnowledgeGraph.Domain.Models;
using HotSwap.KnowledgeGraph.Infrastructure.Repositories;

namespace HotSwap.KnowledgeGraph.QueryEngine.Services;

/// <summary>
/// Dijkstra's algorithm implementation for finding shortest paths in weighted graphs.
/// Guarantees optimal path when all edge weights are non-negative.
/// </summary>
public class DijkstraPathFinder
{
    private readonly IGraphRepository _repository;

    /// <summary>
    /// Initializes a new instance of the DijkstraPathFinder.
    /// </summary>
    /// <param name="repository">The graph repository for data access.</param>
    /// <exception cref="ArgumentNullException">Thrown when repository is null.</exception>
    public DijkstraPathFinder(IGraphRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <summary>
    /// Finds the shortest weighted path from source to target using Dijkstra's algorithm.
    /// Time complexity: O((V + E) log V) where V = vertices, E = edges.
    /// </summary>
    /// <param name="sourceId">Source entity ID.</param>
    /// <param name="targetId">Target entity ID.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>The shortest path by weight, or null if no path exists.</returns>
    public async Task<PathResult?> FindShortestPathAsync(
        Guid sourceId,
        Guid targetId,
        CancellationToken cancellationToken = default)
    {
        // Special case: source equals target
        if (sourceId == targetId)
        {
            var entity = await _repository.GetEntityByIdAsync(sourceId, cancellationToken);
            if (entity == null) return null;

            return new PathResult
            {
                Entities = new List<Entity> { entity },
                Relationships = new List<Relationship>(),
                TotalWeight = 0.0
            };
        }

        // Dijkstra's algorithm data structures
        var distances = new Dictionary<Guid, double>();
        var parentMap = new Dictionary<Guid, (Guid entityId, Relationship relationship)>();
        var visited = new HashSet<Guid>();
        var priorityQueue = new PriorityQueue<Guid, double>();

        // Initialize
        distances[sourceId] = 0.0;
        priorityQueue.Enqueue(sourceId, 0.0);

        while (priorityQueue.Count > 0)
        {
            var currentId = priorityQueue.Dequeue();

            // Skip if already visited (due to priority queue duplicates)
            if (visited.Contains(currentId))
                continue;

            visited.Add(currentId);

            // Early termination: found target
            if (currentId == targetId)
            {
                return await ReconstructPathAsync(sourceId, targetId, parentMap, distances, cancellationToken);
            }

            var currentDistance = distances[currentId];

            // Explore neighbors
            var relationships = await _repository.GetRelationshipsByEntityAsync(
                currentId,
                includeOutgoing: true,
                includeIncoming: false,
                cancellationToken);

            foreach (var rel in relationships)
            {
                var neighborId = rel.TargetEntityId;

                // Skip if already visited
                if (visited.Contains(neighborId))
                    continue;

                var edgeWeight = rel.Weight;
                var newDistance = currentDistance + edgeWeight;

                // Relax edge if shorter path found
                if (!distances.ContainsKey(neighborId) || newDistance < distances[neighborId])
                {
                    distances[neighborId] = newDistance;
                    parentMap[neighborId] = (currentId, rel);
                    priorityQueue.Enqueue(neighborId, newDistance);
                }
            }
        }

        // No path found
        return null;
    }

    /// <summary>
    /// Reconstructs the path from source to target using the parent map.
    /// </summary>
    private async Task<PathResult> ReconstructPathAsync(
        Guid sourceId,
        Guid targetId,
        Dictionary<Guid, (Guid entityId, Relationship relationship)> parentMap,
        Dictionary<Guid, double> distances,
        CancellationToken cancellationToken)
    {
        var path = new List<Guid>();
        var relationships = new List<Relationship>();
        var currentId = targetId;

        // Trace back from target to source
        while (currentId != sourceId)
        {
            path.Add(currentId);
            var (parentId, rel) = parentMap[currentId];
            relationships.Add(rel);
            currentId = parentId;
        }
        path.Add(sourceId);

        // Reverse to get source -> target order
        path.Reverse();
        relationships.Reverse();

        // Fetch entity details
        var entities = new List<Entity>();
        foreach (var entityId in path)
        {
            var entity = await _repository.GetEntityByIdAsync(entityId, cancellationToken);
            if (entity != null)
            {
                entities.Add(entity);
            }
        }

        var totalWeight = distances[targetId];

        return new PathResult
        {
            Entities = entities,
            Relationships = relationships,
            TotalWeight = totalWeight
        };
    }
}
