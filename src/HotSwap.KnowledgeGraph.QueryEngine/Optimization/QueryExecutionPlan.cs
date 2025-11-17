namespace HotSwap.KnowledgeGraph.QueryEngine.Optimization;

/// <summary>
/// Represents an optimized query execution plan.
/// </summary>
public class QueryExecutionPlan
{
    /// <summary>
    /// Execution steps in order.
    /// </summary>
    public List<PlanStep> Steps { get; init; } = new();

    /// <summary>
    /// Estimated cost of executing this plan (arbitrary units).
    /// </summary>
    public double EstimatedCost { get; init; }

    /// <summary>
    /// Estimated selectivity (fraction of rows returned, 0.0 to 1.0).
    /// </summary>
    public double EstimatedSelectivity { get; init; }

    /// <summary>
    /// Estimated number of results.
    /// </summary>
    public int EstimatedCardinality { get; init; }

    /// <summary>
    /// Whether an index scan is recommended.
    /// </summary>
    public bool IndexScanRecommended { get; init; }

    /// <summary>
    /// Recommended indexes to create or use.
    /// </summary>
    public List<string> RecommendedIndexes { get; init; } = new();

    /// <summary>
    /// List of optimizations applied.
    /// </summary>
    public List<string> OptimizationsApplied { get; init; } = new();

    /// <summary>
    /// Converts the plan to a human-readable string.
    /// </summary>
    /// <returns>Human-readable execution plan.</returns>
    public string ToReadableString()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=== Query Execution Plan ===");
        sb.AppendLine($"Estimated Cost: {EstimatedCost:F2}");
        sb.AppendLine($"Estimated Cardinality: {EstimatedCardinality}");
        sb.AppendLine($"Estimated Selectivity: {EstimatedSelectivity:P2}");
        sb.AppendLine();

        if (RecommendedIndexes.Any())
        {
            sb.AppendLine($"Recommended Indexes: {string.Join(", ", RecommendedIndexes)}");
            sb.AppendLine();
        }

        sb.AppendLine("Steps:");
        for (int i = 0; i < Steps.Count; i++)
        {
            var step = Steps[i];
            sb.AppendLine($"{i + 1}. {step.Operation}");
            if (!string.IsNullOrEmpty(step.EntityType))
                sb.AppendLine($"   Entity Type: {step.EntityType}");
            if (step.PropertyFilters?.Any() == true)
                sb.AppendLine($"   Filters: {string.Join(", ", step.PropertyFilters.Keys)}");
            if (step.Limit.HasValue)
                sb.AppendLine($"   Limit: {step.Limit}");
            if (step.Offset.HasValue)
                sb.AppendLine($"   Offset: {step.Offset}");
            sb.AppendLine($"   Cost: {step.Cost:F2}");
        }

        if (OptimizationsApplied.Any())
        {
            sb.AppendLine();
            sb.AppendLine("Optimizations Applied:");
            foreach (var opt in OptimizationsApplied)
            {
                sb.AppendLine($"  - {opt}");
            }
        }

        return sb.ToString();
    }
}

/// <summary>
/// Represents a single step in a query execution plan.
/// </summary>
public class PlanStep
{
    /// <summary>
    /// Operation type (e.g., "ScanByEntityType", "FilterByProperty", "Limit").
    /// </summary>
    public string Operation { get; init; } = string.Empty;

    /// <summary>
    /// Entity type for this step (if applicable).
    /// </summary>
    public string? EntityType { get; init; }

    /// <summary>
    /// Property filters for this step (if applicable).
    /// </summary>
    public Dictionary<string, object>? PropertyFilters { get; init; }

    /// <summary>
    /// Limit for this step (if applicable).
    /// </summary>
    public int? Limit { get; init; }

    /// <summary>
    /// Offset for this step (if applicable).
    /// </summary>
    public int? Offset { get; init; }

    /// <summary>
    /// Estimated cost of this step.
    /// </summary>
    public double Cost { get; init; }

    /// <summary>
    /// Estimated selectivity of this step (0.0 to 1.0).
    /// </summary>
    public double Selectivity { get; init; }
}
