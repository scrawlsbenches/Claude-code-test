using HotSwap.KnowledgeGraph.Domain.Models;

namespace HotSwap.KnowledgeGraph.QueryEngine.Optimization;

/// <summary>
/// Cost-based query optimizer that generates execution plans.
/// Uses heuristics to estimate query costs and selectivity.
/// </summary>
public class CostBasedOptimizer : IQueryOptimizer
{
    // Cost constants (arbitrary units)
    private const double ENTITY_SCAN_COST_PER_ROW = 0.1;
    private const double PROPERTY_FILTER_COST_PER_ROW = 0.05;
    private const double LIMIT_COST = 1.0;
    private const double INDEX_SCAN_COST_MULTIPLIER = 0.01;

    // Selectivity estimates (fraction of rows passing filter)
    private const double DEFAULT_SELECTIVITY = 0.5;
    private const double GUID_SELECTIVITY = 0.001; // Highly selective
    private const double EQUALITY_SELECTIVITY = 0.1;
    private const double STRING_CONTAINS_SELECTIVITY = 0.3;

    // Assumed table size for cost estimation (in absence of statistics)
    private const int DEFAULT_TABLE_SIZE = 1000;

    /// <inheritdoc/>
    public QueryExecutionPlan OptimizeQuery(GraphQuery query)
    {
        if (query == null)
        {
            throw new ArgumentNullException(nameof(query));
        }

        var steps = new List<PlanStep>();
        var optimizations = new List<string>();
        var recommendedIndexes = new List<string>();
        var currentCardinality = DEFAULT_TABLE_SIZE;
        var totalCost = 0.0;

        // Step 1: Entity type scan
        if (!string.IsNullOrEmpty(query.EntityType))
        {
            var scanCost = ENTITY_SCAN_COST_PER_ROW * currentCardinality;
            steps.Add(new PlanStep
            {
                Operation = "ScanByEntityType",
                EntityType = query.EntityType,
                Cost = scanCost,
                Selectivity = 0.1 // Assume entity type reduces to 10% of all entities
            });
            totalCost += scanCost;
            currentCardinality = (int)(currentCardinality * 0.1);
        }

        // Step 2: Property filters (ordered by selectivity for optimal filtering)
        if (query.PropertyFilters?.Any() == true)
        {
            // Order filters by estimated selectivity (most selective first)
            var orderedFilters = query.PropertyFilters
                .OrderBy(f => EstimateFilterSelectivity(f.Key, f.Value))
                .ToList();

            foreach (var filter in orderedFilters)
            {
                var selectivity = EstimateFilterSelectivity(filter.Key, filter.Value);
                var filterCost = PROPERTY_FILTER_COST_PER_ROW * currentCardinality;

                steps.Add(new PlanStep
                {
                    Operation = "FilterByProperty",
                    PropertyFilters = new Dictionary<string, object> { [filter.Key] = filter.Value },
                    Cost = filterCost,
                    Selectivity = selectivity
                });

                totalCost += filterCost;
                currentCardinality = (int)(currentCardinality * selectivity);

                // Recommend indexes for highly selective filters
                if (selectivity < 0.2)
                {
                    recommendedIndexes.Add(filter.Key);
                }
            }

            optimizations.Add($"Ordered {orderedFilters.Count} filters by selectivity");
        }

        // Step 3: Pagination (Limit and Offset)
        if (query.PageSize > 0)
        {
            steps.Add(new PlanStep
            {
                Operation = "Limit",
                Limit = query.PageSize,
                Offset = query.Skip,
                Cost = LIMIT_COST,
                Selectivity = 1.0
            });
            totalCost += LIMIT_COST;
            currentCardinality = Math.Min(currentCardinality, query.PageSize);
        }

        // Calculate overall selectivity
        var overallSelectivity = steps.Any()
            ? steps.Where(s => s.Selectivity > 0).Select(s => s.Selectivity).Aggregate((a, b) => a * b)
            : DEFAULT_SELECTIVITY;

        // Determine if index scan is recommended
        var indexScanRecommended = recommendedIndexes.Any() ||
            (query.PropertyFilters?.Count >= 2);

        if (indexScanRecommended)
        {
            optimizations.Add("Index scan recommended for improved performance");
            // Note: In real implementation, index scans would reduce cost of specific steps
            // For now, we just flag it as recommended without artificially reducing cost
        }

        return new QueryExecutionPlan
        {
            Steps = steps,
            EstimatedCost = totalCost,
            EstimatedSelectivity = overallSelectivity,
            EstimatedCardinality = Math.Max(1, currentCardinality),
            IndexScanRecommended = indexScanRecommended,
            RecommendedIndexes = recommendedIndexes.Distinct().ToList(),
            OptimizationsApplied = optimizations
        };
    }

    /// <summary>
    /// Estimates the selectivity of a property filter.
    /// </summary>
    /// <param name="propertyName">Property name.</param>
    /// <param name="propertyValue">Property value.</param>
    /// <returns>Estimated selectivity (0.0 to 1.0).</returns>
    private double EstimateFilterSelectivity(string propertyName, object propertyValue)
    {
        // GUID/ID filters are highly selective
        if (propertyName.Equals("id", StringComparison.OrdinalIgnoreCase) ||
            propertyValue is Guid)
        {
            return GUID_SELECTIVITY;
        }

        // Email filters are highly selective
        if (propertyName.Equals("email", StringComparison.OrdinalIgnoreCase))
        {
            return GUID_SELECTIVITY;
        }

        // String contains operations are less selective
        if (propertyValue is string strValue && strValue.Contains("*"))
        {
            return STRING_CONTAINS_SELECTIVITY;
        }

        // Equality filters on indexed fields
        if (propertyName.Equals("firstName", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Equals("lastName", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Equals("username", StringComparison.OrdinalIgnoreCase))
        {
            return EQUALITY_SELECTIVITY;
        }

        // Default selectivity for other filters
        return DEFAULT_SELECTIVITY;
    }
}
