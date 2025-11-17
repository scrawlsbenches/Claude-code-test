using HotSwap.KnowledgeGraph.Domain.Models;
using HotSwap.KnowledgeGraph.Infrastructure.Repositories;

namespace HotSwap.KnowledgeGraph.QueryEngine.Services;

/// <summary>
/// Service for graph traversal algorithms including BFS and DFS.
/// </summary>
public class GraphTraversalService
{
    private readonly IGraphRepository _repository;

    /// <summary>
    /// Initializes a new instance of the GraphTraversalService.
    /// </summary>
    /// <param name="repository">The graph repository for data access.</param>
    /// <exception cref="ArgumentNullException">Thrown when repository is null.</exception>
    public GraphTraversalService(IGraphRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <summary>
    /// Performs breadth-first search to find the shortest path between two entities.
    /// BFS guarantees the shortest path in an unweighted graph.
    /// </summary>
    /// <param name="sourceId">Source entity ID.</param>
    /// <param name="targetId">Target entity ID.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>The shortest path, or null if no path exists.</returns>
    public async Task<PathResult?> BreadthFirstSearchAsync(
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
                TotalWeight = 0
            };
        }

        // BFS data structures
        var queue = new Queue<Guid>();
        var visited = new HashSet<Guid>();
        var parentMap = new Dictionary<Guid, (Guid entityId, Relationship relationship)>();

        queue.Enqueue(sourceId);
        visited.Add(sourceId);

        // BFS traversal
        while (queue.Count > 0)
        {
            var currentId = queue.Dequeue();

            // Get outgoing relationships for current entity
            var relationships = await _repository.GetRelationshipsByEntityAsync(
                currentId,
                includeOutgoing: true,
                includeIncoming: false,
                cancellationToken);

            foreach (var rel in relationships)
            {
                var neighborId = rel.TargetEntityId;

                if (!visited.Contains(neighborId))
                {
                    visited.Add(neighborId);
                    parentMap[neighborId] = (currentId, rel);
                    queue.Enqueue(neighborId);

                    // Check if we found the target
                    if (neighborId == targetId)
                    {
                        return await ReconstructPathAsync(sourceId, targetId, parentMap, cancellationToken);
                    }
                }
            }
        }

        // No path found
        return null;
    }

    /// <summary>
    /// Performs depth-first search to find all paths between two entities up to a maximum depth.
    /// </summary>
    /// <param name="sourceId">Source entity ID.</param>
    /// <param name="targetId">Target entity ID.</param>
    /// <param name="maxDepth">Maximum depth (number of hops) to search.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>List of all paths found within the depth limit.</returns>
    public async Task<List<PathResult>> DepthFirstSearchAsync(
        Guid sourceId,
        Guid targetId,
        int maxDepth = 5,
        CancellationToken cancellationToken = default)
    {
        var allPaths = new List<PathResult>();
        var currentPath = new List<Guid>();
        var currentRelationships = new List<Relationship>();
        var visited = new HashSet<Guid>();

        await DfsRecursiveAsync(
            sourceId,
            targetId,
            maxDepth,
            currentPath,
            currentRelationships,
            visited,
            allPaths,
            cancellationToken);

        return allPaths;
    }

    /// <summary>
    /// Recursive DFS helper method.
    /// </summary>
    private async Task DfsRecursiveAsync(
        Guid currentId,
        Guid targetId,
        int remainingDepth,
        List<Guid> currentPath,
        List<Relationship> currentRelationships,
        HashSet<Guid> visited,
        List<PathResult> allPaths,
        CancellationToken cancellationToken)
    {
        // Mark current node as visited and add to path
        visited.Add(currentId);
        currentPath.Add(currentId);

        // Check if we reached the target
        if (currentId == targetId)
        {
            // Found a path - convert to PathResult
            var pathEntities = new List<Entity>();
            foreach (var entityId in currentPath)
            {
                var entity = await _repository.GetEntityByIdAsync(entityId, cancellationToken);
                if (entity != null)
                {
                    pathEntities.Add(entity);
                }
            }

            var totalWeight = currentRelationships.Sum(r => r.Weight);

            allPaths.Add(new PathResult
            {
                Entities = pathEntities,
                Relationships = new List<Relationship>(currentRelationships),
                TotalWeight = totalWeight
            });
        }
        else if (remainingDepth > 0)
        {
            // Continue DFS if we haven't reached max depth
            var relationships = await _repository.GetRelationshipsByEntityAsync(
                currentId,
                includeOutgoing: true,
                includeIncoming: false,
                cancellationToken);

            foreach (var rel in relationships)
            {
                var neighborId = rel.TargetEntityId;

                // Avoid cycles
                if (!visited.Contains(neighborId))
                {
                    currentRelationships.Add(rel);

                    await DfsRecursiveAsync(
                        neighborId,
                        targetId,
                        remainingDepth - 1,
                        currentPath,
                        currentRelationships,
                        visited,
                        allPaths,
                        cancellationToken);

                    currentRelationships.RemoveAt(currentRelationships.Count - 1);
                }
            }
        }

        // Backtrack: remove current node from path and visited set
        currentPath.RemoveAt(currentPath.Count - 1);
        visited.Remove(currentId);
    }

    /// <summary>
    /// Reconstructs the path from source to target using the parent map from BFS.
    /// </summary>
    private async Task<PathResult> ReconstructPathAsync(
        Guid sourceId,
        Guid targetId,
        Dictionary<Guid, (Guid entityId, Relationship relationship)> parentMap,
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

        var totalWeight = relationships.Sum(r => r.Weight);

        return new PathResult
        {
            Entities = entities,
            Relationships = relationships,
            TotalWeight = totalWeight
        };
    }
}
