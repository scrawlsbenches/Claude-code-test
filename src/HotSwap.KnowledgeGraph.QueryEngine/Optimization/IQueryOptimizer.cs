using HotSwap.KnowledgeGraph.Domain.Models;

namespace HotSwap.KnowledgeGraph.QueryEngine.Optimization;

/// <summary>
/// Interface for query optimization that generates execution plans.
/// </summary>
public interface IQueryOptimizer
{
    /// <summary>
    /// Optimizes a graph query and generates an execution plan.
    /// </summary>
    /// <param name="query">The query to optimize.</param>
    /// <returns>An optimized execution plan.</returns>
    /// <exception cref="ArgumentNullException">Thrown when query is null.</exception>
    QueryExecutionPlan OptimizeQuery(GraphQuery query);
}
