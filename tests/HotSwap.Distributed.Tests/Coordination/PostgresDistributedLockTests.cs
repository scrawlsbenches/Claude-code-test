using FluentAssertions;
using HotSwap.Distributed.Infrastructure.Coordination;
using HotSwap.Distributed.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Xunit;

namespace HotSwap.Distributed.Tests.Coordination;

/// <summary>
/// Unit tests for PostgresDistributedLock using in-memory SQLite.
/// Note: SQLite doesn't support PostgreSQL advisory locks, so these tests verify the general flow.
/// Integration tests with real PostgreSQL are recommended for full coverage.
/// </summary>
public class PostgresDistributedLockTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AuditLogDbContext _dbContext;
    private readonly PostgresDistributedLock _lock;

    public PostgresDistributedLockTests()
    {
        // Create in-memory SQLite database for testing
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AuditLogDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new AuditLogDbContext(options);
        _dbContext.Database.EnsureCreated();

        _lock = new PostgresDistributedLock(_dbContext, NullLogger<PostgresDistributedLock>.Instance);
    }

    [Fact(Skip = "Requires PostgreSQL - SQLite doesn't support advisory locks")]
    public async Task AcquireLockAsync_ValidResource_ReturnsLockHandle()
    {
        // Arrange
        const string resource = "test-resource";
        var timeout = TimeSpan.FromSeconds(5);

        // Act
        var handle = await _lock.AcquireLockAsync(resource, timeout);

        // Assert
        handle.Should().NotBeNull();
        handle!.Resource.Should().Be(resource);
        handle.IsHeld.Should().BeTrue();
        handle.AcquiredAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));

        // Cleanup
        await handle.DisposeAsync();
    }

    [Fact(Skip = "Requires PostgreSQL - SQLite doesn't support advisory locks")]
    public async Task AcquireLockAsync_SameResourceTwice_SecondRequestTimesOut()
    {
        // Arrange
        const string resource = "exclusive-resource";
        var timeout = TimeSpan.FromSeconds(2);

        // Act
        var handle1 = await _lock.AcquireLockAsync(resource, timeout);
        var handle2 = await _lock.AcquireLockAsync(resource, TimeSpan.FromMilliseconds(500));

        // Assert
        handle1.Should().NotBeNull();
        handle2.Should().BeNull(); // Second request should timeout

        // Cleanup
        await handle1!.DisposeAsync();
    }

    [Fact(Skip = "Requires PostgreSQL - SQLite doesn't support advisory locks")]
    public async Task AcquireLockAsync_AfterRelease_CanBeReacquired()
    {
        // Arrange
        const string resource = "reusable-resource";
        var timeout = TimeSpan.FromSeconds(5);

        // Act
        var handle1 = await _lock.AcquireLockAsync(resource, timeout);
        await handle1!.ReleaseAsync();

        var handle2 = await _lock.AcquireLockAsync(resource, timeout);

        // Assert
        handle2.Should().NotBeNull();
        handle1.IsHeld.Should().BeFalse();
        handle2!.IsHeld.Should().BeTrue();

        // Cleanup
        await handle2.DisposeAsync();
    }

    [Fact(Skip = "Requires PostgreSQL - SQLite doesn't support advisory locks")]
    public async Task AcquireLockAsync_Dispose_ReleasesLock()
    {
        // Arrange
        const string resource = "disposable-resource";
        var timeout = TimeSpan.FromSeconds(5);

        // Act
        var handle1 = await _lock.AcquireLockAsync(resource, timeout);
        await handle1!.DisposeAsync();

        var handle2 = await _lock.AcquireLockAsync(resource, timeout);

        // Assert
        handle2.Should().NotBeNull();
        handle1.IsHeld.Should().BeFalse();

        // Cleanup
        await handle2!.DisposeAsync();
    }

    [Fact(Skip = "Requires PostgreSQL - SQLite doesn't support advisory locks")]
    public async Task AcquireLockAsync_DifferentResources_BothSucceed()
    {
        // Arrange
        const string resource1 = "resource-1";
        const string resource2 = "resource-2";
        var timeout = TimeSpan.FromSeconds(5);

        // Act
        var handle1 = await _lock.AcquireLockAsync(resource1, timeout);
        var handle2 = await _lock.AcquireLockAsync(resource2, timeout);

        // Assert
        handle1.Should().NotBeNull();
        handle2.Should().NotBeNull();
        handle1!.IsHeld.Should().BeTrue();
        handle2!.IsHeld.Should().BeTrue();

        // Cleanup
        await handle1.DisposeAsync();
        await handle2.DisposeAsync();
    }

    [Fact]
    public async Task AcquireLockAsync_EmptyResource_ThrowsArgumentException()
    {
        // Arrange
        var timeout = TimeSpan.FromSeconds(5);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            async () => await _lock.AcquireLockAsync("", timeout));
    }

    [Fact(Skip = "Requires PostgreSQL - SQLite doesn't support advisory locks")]
    public async Task AcquireLockAsync_CancellationToken_CancelsOperation()
    {
        // Arrange
        const string resource = "cancellable-resource";
        var timeout = TimeSpan.FromSeconds(30);
        var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));

        // Acquire first lock to block second request
        var handle1 = await _lock.AcquireLockAsync(resource, timeout);

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await _lock.AcquireLockAsync(resource, timeout, cts.Token));

        // Cleanup
        await handle1!.DisposeAsync();
    }

    [Fact(Skip = "Requires PostgreSQL - SQLite doesn't support advisory locks")]
    public async Task ReleaseAsync_CalledTwice_IsIdempotent()
    {
        // Arrange
        const string resource = "idempotent-resource";
        var timeout = TimeSpan.FromSeconds(5);

        // Act
        var handle = await _lock.AcquireLockAsync(resource, timeout);
        await handle!.ReleaseAsync();
        await handle.ReleaseAsync(); // Second release should be safe

        // Assert
        handle.IsHeld.Should().BeFalse();
    }

    public void Dispose()
    {
        _dbContext?.Dispose();
        _connection?.Dispose();
    }
}

/// <summary>
/// Integration tests for PostgresDistributedLock using real PostgreSQL.
/// These tests require a PostgreSQL database to be available.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresPostgreSQL")]
public class PostgresDistributedLockIntegrationTests : IDisposable
{
    private readonly AuditLogDbContext? _dbContext;
    private readonly PostgresDistributedLock? _lock;

    public PostgresDistributedLockIntegrationTests()
    {
        // Check if PostgreSQL is available
        var connectionString = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING")
            ?? "Host=localhost;Database=hotswap_test;Username=postgres;Password=postgres";

        try
        {
            var options = new DbContextOptionsBuilder<AuditLogDbContext>()
                .UseNpgsql(connectionString)
                .Options;

            _dbContext = new AuditLogDbContext(options);
            _dbContext.Database.EnsureCreated();

            _lock = new PostgresDistributedLock(_dbContext, NullLogger<PostgresDistributedLock>.Instance);
        }
        catch (NpgsqlException)
        {
            // PostgreSQL not available - tests will be skipped
            _dbContext = null;
            _lock = null;
        }
    }

    [Fact]
    public async Task AcquireLockAsync_ValidResource_ReturnsLockHandle()
    {
        if (_lock == null)
        {
            // Skip test if PostgreSQL is not available
            return;
        }

        // Arrange
        const string resource = "integration-test-resource";
        var timeout = TimeSpan.FromSeconds(5);

        // Act
        var handle = await _lock.AcquireLockAsync(resource, timeout);

        // Assert
        handle.Should().NotBeNull();
        handle!.Resource.Should().Be(resource);
        handle.IsHeld.Should().BeTrue();
        handle.AcquiredAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));

        // Cleanup
        await handle.DisposeAsync();
    }

    [Fact]
    public async Task AcquireLockAsync_SameResourceTwice_SecondRequestTimesOut()
    {
        if (_lock == null)
        {
            return;
        }

        // Arrange
        const string resource = "integration-exclusive-resource";
        var timeout = TimeSpan.FromSeconds(2);

        // Act
        var handle1 = await _lock.AcquireLockAsync(resource, timeout);
        var handle2 = await _lock.AcquireLockAsync(resource, TimeSpan.FromMilliseconds(500));

        // Assert
        handle1.Should().NotBeNull();
        handle2.Should().BeNull(); // Second request should timeout

        // Cleanup
        await handle1!.DisposeAsync();
    }

    [Fact]
    public async Task AcquireLockAsync_ConcurrentRequests_OnlyOneSucceeds()
    {
        if (_lock == null)
        {
            return;
        }

        // Arrange
        const string resource = "integration-concurrent-resource";
        var timeout = TimeSpan.FromSeconds(5);

        // Act - Start 5 concurrent lock requests
        var tasks = Enumerable.Range(0, 5)
            .Select(i => _lock.AcquireLockAsync(resource, timeout))
            .ToArray();

        var handles = await Task.WhenAll(tasks);

        // Assert
        var acquiredHandles = handles.Where(h => h != null).ToList();
        acquiredHandles.Should().ContainSingle(); // Only one should succeed

        // Cleanup
        foreach (var handle in acquiredHandles)
        {
            await handle!.DisposeAsync();
        }
    }

    [Fact]
    public async Task AcquireLockAsync_AfterRelease_CanBeReacquired()
    {
        if (_lock == null)
        {
            return;
        }

        // Arrange
        const string resource = "integration-reusable-resource";
        var timeout = TimeSpan.FromSeconds(5);

        // Act
        var handle1 = await _lock.AcquireLockAsync(resource, timeout);
        await handle1!.ReleaseAsync();

        var handle2 = await _lock.AcquireLockAsync(resource, timeout);

        // Assert
        handle2.Should().NotBeNull();
        handle1.IsHeld.Should().BeFalse();
        handle2!.IsHeld.Should().BeTrue();

        // Cleanup
        await handle2.DisposeAsync();
    }

    public void Dispose()
    {
        _dbContext?.Dispose();
    }
}
