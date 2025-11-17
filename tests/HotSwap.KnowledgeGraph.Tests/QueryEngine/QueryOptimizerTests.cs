using FluentAssertions;
using HotSwap.KnowledgeGraph.Domain.Models;
using HotSwap.KnowledgeGraph.QueryEngine.Optimization;

namespace HotSwap.KnowledgeGraph.Tests.QueryEngine;

/// <summary>
/// Tests for query optimizer that generates execution plans.
/// </summary>
public class QueryOptimizerTests
{
    [Fact]
    public void OptimizeQuery_WithSimpleEntityTypeQuery_GeneratesBasicPlan()
    {
        // Arrange
        var optimizer = new CostBasedOptimizer();
        var query = new GraphQuery { EntityType = "Person" };

        // Act
        var plan = optimizer.OptimizeQuery(query);

        // Assert
        plan.Should().NotBeNull();
        plan.Steps.Should().NotBeEmpty();
        plan.Steps[0].Operation.Should().Be("ScanByEntityType");
        plan.Steps[0].EntityType.Should().Be("Person");
        plan.EstimatedCost.Should().BeGreaterThan(0);
    }

    [Fact]
    public void OptimizeQuery_WithPropertyFilter_PushesFilterDown()
    {
        // Arrange
        var optimizer = new CostBasedOptimizer();
        var query = new GraphQuery
        {
            EntityType = "Person",
            PropertyFilters = new Dictionary<string, object> { ["age"] = 30 }
        };

        // Act
        var plan = optimizer.OptimizeQuery(query);

        // Assert
        plan.Steps.Should().Contain(s => s.Operation == "FilterByProperty");
        plan.Steps.Should().Contain(s => s.PropertyFilters != null && s.PropertyFilters.ContainsKey("age"));
    }

    [Fact]
    public void OptimizeQuery_WithPagination_AppliesLimitAndOffset()
    {
        // Arrange
        var optimizer = new CostBasedOptimizer();
        var query = new GraphQuery
        {
            EntityType = "Person",
            PageSize = 10,
            Skip = 20
        };

        // Act
        var plan = optimizer.OptimizeQuery(query);

        // Assert
        plan.Steps.Should().Contain(s => s.Operation == "Limit");
        plan.Steps.Should().Contain(s => s.Limit == 10);
        plan.Steps.Should().Contain(s => s.Offset == 20);
    }

    [Fact]
    public void OptimizeQuery_WithIndexableProperty_RecommendsIndexScan()
    {
        // Arrange
        var optimizer = new CostBasedOptimizer();
        var query = new GraphQuery
        {
            EntityType = "Person",
            PropertyFilters = new Dictionary<string, object> { ["email"] = "test@example.com" }
        };

        // Act
        var plan = optimizer.OptimizeQuery(query);

        // Assert
        plan.RecommendedIndexes.Should().Contain("email");
        plan.IndexScanRecommended.Should().BeTrue();
    }

    [Fact]
    public void OptimizeQuery_WithMultipleFilters_EstimatesSelectivity()
    {
        // Arrange
        var optimizer = new CostBasedOptimizer();
        var query = new GraphQuery
        {
            EntityType = "Person",
            PropertyFilters = new Dictionary<string, object>
            {
                ["age"] = 30,
                ["city"] = "Seattle",
                ["active"] = true
            }
        };

        // Act
        var plan = optimizer.OptimizeQuery(query);

        // Assert
        plan.EstimatedSelectivity.Should().BeLessThan(1.0);
        plan.EstimatedSelectivity.Should().BeGreaterThan(0);
        plan.EstimatedCardinality.Should().BeGreaterThan(0);
    }

    [Fact]
    public void OptimizeQuery_OrdersFiltersBySelectivity()
    {
        // Arrange
        var optimizer = new CostBasedOptimizer();
        var query = new GraphQuery
        {
            EntityType = "Person",
            PropertyFilters = new Dictionary<string, object>
            {
                ["id"] = Guid.NewGuid(), // Highly selective
                ["country"] = "USA" // Less selective
            }
        };

        // Act
        var plan = optimizer.OptimizeQuery(query);

        // Assert
        var filterSteps = plan.Steps.Where(s => s.Operation == "FilterByProperty").ToList();
        if (filterSteps.Count > 1)
        {
            // Most selective filter (id) should come first
            filterSteps[0].PropertyFilters.Should().ContainKey("id");
        }
    }

    [Fact]
    public void OptimizeQuery_WithNullQuery_ThrowsArgumentNullException()
    {
        // Arrange
        var optimizer = new CostBasedOptimizer();

        // Act
        Action act = () => optimizer.OptimizeQuery(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("query");
    }

    [Fact]
    public void GenerateExecutionPlan_ProduceHumanReadableOutput()
    {
        // Arrange
        var optimizer = new CostBasedOptimizer();
        var query = new GraphQuery
        {
            EntityType = "Person",
            PropertyFilters = new Dictionary<string, object> { ["age"] = 30 },
            PageSize = 10
        };

        // Act
        var plan = optimizer.OptimizeQuery(query);
        var readablePlan = plan.ToReadableString();

        // Assert
        readablePlan.Should().NotBeEmpty();
        readablePlan.Should().Contain("Person");
        readablePlan.Should().Contain("age");
        readablePlan.Should().Contain("Cost:");
    }

    [Fact]
    public void EstimateCost_WithComplexQuery_ReturnsReasonableEstimate()
    {
        // Arrange
        var optimizer = new CostBasedOptimizer();
        var simpleQuery = new GraphQuery { EntityType = "Person" };
        var complexQuery = new GraphQuery
        {
            EntityType = "Person",
            PropertyFilters = new Dictionary<string, object>
            {
                ["age"] = 30,
                ["city"] = "Seattle"
            },
            PageSize = 100
        };

        // Act
        var simplePlan = optimizer.OptimizeQuery(simpleQuery);
        var complexPlan = optimizer.OptimizeQuery(complexQuery);

        // Assert
        complexPlan.EstimatedCost.Should().BeGreaterThanOrEqualTo(simplePlan.EstimatedCost);
    }

    [Fact]
    public void OptimizeQuery_SupportsQueryRewriting()
    {
        // Arrange
        var optimizer = new CostBasedOptimizer();
        var query = new GraphQuery
        {
            EntityType = "Person",
            PropertyFilters = new Dictionary<string, object>
            {
                ["firstName"] = "John",
                ["lastName"] = "Doe"
            }
        };

        // Act
        var plan = optimizer.OptimizeQuery(query);

        // Assert
        plan.OptimizationsApplied.Should().NotBeEmpty();
        plan.OptimizationsApplied.Should().Contain(o => o.Contains("filter"));
    }
}
