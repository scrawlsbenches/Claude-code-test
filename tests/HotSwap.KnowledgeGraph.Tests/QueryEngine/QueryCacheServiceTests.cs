using FluentAssertions;
using HotSwap.KnowledgeGraph.Domain.Models;
using HotSwap.KnowledgeGraph.QueryEngine.Services;

namespace HotSwap.KnowledgeGraph.Tests.QueryEngine;

/// <summary>
/// Tests for query result caching service.
/// </summary>
public class QueryCacheServiceTests
{
    [Fact]
    public void TryGet_WithNonExistentKey_ReturnsFalse()
    {
        // Arrange
        var cache = new QueryCacheService(cacheDurationSeconds: 60);
        var query = new GraphQuery { EntityType = "Person" };

        // Act
        var found = cache.TryGet(query, out var result);

        // Assert
        found.Should().BeFalse();
        result.Should().BeNull();
    }

    [Fact]
    public void Set_ThenTryGet_ReturnsTrue()
    {
        // Arrange
        var cache = new QueryCacheService(cacheDurationSeconds: 60);
        var query = new GraphQuery { EntityType = "Person" };
        var expectedResult = new GraphQueryResult
        {
            Entities = new List<Entity>(),
            Relationships = new List<Relationship>(),
            TotalCount = 0,
            ExecutionTime = TimeSpan.FromMilliseconds(10)
        };

        // Act
        cache.Set(query, expectedResult);
        var found = cache.TryGet(query, out var result);

        // Assert
        found.Should().BeTrue();
        result.Should().NotBeNull();
        result!.TotalCount.Should().Be(expectedResult.TotalCount);
        result.FromCache.Should().BeTrue(); // Should be marked as from cache
    }

    [Fact]
    public void Set_WithSameQueryDifferentParams_StoresSeparately()
    {
        // Arrange
        var cache = new QueryCacheService(cacheDurationSeconds: 60);
        var query1 = new GraphQuery { EntityType = "Person", PageSize = 10 };
        var query2 = new GraphQuery { EntityType = "Person", PageSize = 20 };

        var result1 = new GraphQueryResult
        {
            Entities = new List<Entity>(),
            Relationships = new List<Relationship>(),
            TotalCount = 10,
            ExecutionTime = TimeSpan.FromMilliseconds(5)
        };

        var result2 = new GraphQueryResult
        {
            Entities = new List<Entity>(),
            Relationships = new List<Relationship>(),
            TotalCount = 20,
            ExecutionTime = TimeSpan.FromMilliseconds(8)
        };

        // Act
        cache.Set(query1, result1);
        cache.Set(query2, result2);

        cache.TryGet(query1, out var retrievedResult1);
        cache.TryGet(query2, out var retrievedResult2);

        // Assert
        retrievedResult1!.TotalCount.Should().Be(10);
        retrievedResult2!.TotalCount.Should().Be(20);
    }

    [Fact]
    public async Task TryGet_AfterExpiration_ReturnsFalse()
    {
        // Arrange
        var cache = new QueryCacheService(cacheDurationSeconds: 1); // 1 second TTL
        var query = new GraphQuery { EntityType = "Person" };
        var result = new GraphQueryResult
        {
            Entities = new List<Entity>(),
            Relationships = new List<Relationship>(),
            TotalCount = 0,
            ExecutionTime = TimeSpan.FromMilliseconds(5)
        };

        cache.Set(query, result);

        // Act - Wait for cache to expire
        await Task.Delay(TimeSpan.FromSeconds(1.5));
        var found = cache.TryGet(query, out var retrievedResult);

        // Assert
        found.Should().BeFalse();
        retrievedResult.Should().BeNull();
    }

    [Fact]
    public void Clear_RemovesAllCachedItems()
    {
        // Arrange
        var cache = new QueryCacheService(cacheDurationSeconds: 60);
        var query1 = new GraphQuery { EntityType = "Person" };
        var query2 = new GraphQuery { EntityType = "Company" };

        var result = new GraphQueryResult
        {
            Entities = new List<Entity>(),
            Relationships = new List<Relationship>(),
            TotalCount = 0,
            ExecutionTime = TimeSpan.FromMilliseconds(5)
        };

        cache.Set(query1, result);
        cache.Set(query2, result);

        // Act
        cache.Clear();

        // Assert
        cache.TryGet(query1, out _).Should().BeFalse();
        cache.TryGet(query2, out _).Should().BeFalse();
    }

    [Fact]
    public void GetCacheStatistics_TracksHitsAndMisses()
    {
        // Arrange
        var cache = new QueryCacheService(cacheDurationSeconds: 60);
        var query = new GraphQuery { EntityType = "Person" };
        var result = new GraphQueryResult
        {
            Entities = new List<Entity>(),
            Relationships = new List<Relationship>(),
            TotalCount = 0,
            ExecutionTime = TimeSpan.FromMilliseconds(5)
        };

        cache.Set(query, result);

        // Act
        cache.TryGet(query, out _); // Hit
        cache.TryGet(query, out _); // Hit
        cache.TryGet(new GraphQuery { EntityType = "Company" }, out _); // Miss
        cache.TryGet(new GraphQuery { EntityType = "Product" }, out _); // Miss

        var stats = cache.GetCacheStatistics();

        // Assert
        stats.Hits.Should().Be(2);
        stats.Misses.Should().Be(2);
        stats.HitRate.Should().BeApproximately(0.5, 0.01); // 50% hit rate
        stats.TotalRequests.Should().Be(4);
    }

    [Fact]
    public void Constructor_WithInvalidDuration_ThrowsArgumentException()
    {
        // Act
        Action act = () => new QueryCacheService(cacheDurationSeconds: 0);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*must be greater than zero*");
    }

    [Fact]
    public void GenerateCacheKey_WithIdenticalQueries_GeneratesSameKey()
    {
        // Arrange
        var cache = new QueryCacheService(cacheDurationSeconds: 60);
        var query1 = new GraphQuery
        {
            EntityType = "Person",
            PageSize = 100,
            Skip = 0,
            PropertyFilters = new Dictionary<string, object> { ["age"] = 30 }
        };

        var query2 = new GraphQuery
        {
            EntityType = "Person",
            PageSize = 100,
            Skip = 0,
            PropertyFilters = new Dictionary<string, object> { ["age"] = 30 }
        };

        var result = new GraphQueryResult
        {
            Entities = new List<Entity>(),
            Relationships = new List<Relationship>(),
            TotalCount = 0,
            ExecutionTime = TimeSpan.FromMilliseconds(5)
        };

        // Act
        cache.Set(query1, result);
        var found = cache.TryGet(query2, out var retrievedResult);

        // Assert
        found.Should().BeTrue("identical queries should generate the same cache key");
        retrievedResult.Should().NotBeNull();
    }
}
