using Testcontainers.Redis;

namespace HotSwap.Distributed.IntegrationTests.Fixtures;

/// <summary>
/// Manages Redis Docker container lifecycle for integration tests.
/// Implements IAsyncLifetime for xUnit test collection fixtures.
/// </summary>
public class RedisContainerFixture : IAsyncLifetime
{
    private RedisContainer? _container;

    /// <summary>
    /// Gets the Redis container instance.
    /// </summary>
    public RedisContainer Container => _container
        ?? throw new InvalidOperationException("Container not initialized. Call InitializeAsync first.");

    /// <summary>
    /// Initializes and starts the Redis container.
    /// Called automatically by xUnit before tests run.
    /// </summary>
    public async Task InitializeAsync()
    {
        _container = new RedisBuilder()
            .WithImage("redis:7-alpine")
            .WithCleanUp(true)
            .Build();

        await _container.StartAsync();
    }

    /// <summary>
    /// Stops and disposes the Redis container.
    /// Called automatically by xUnit after tests complete.
    /// </summary>
    public async Task DisposeAsync()
    {
        if (_container != null)
        {
            await _container.DisposeAsync();
        }
    }

    /// <summary>
    /// Gets the connection string for the running Redis container.
    /// </summary>
    public string GetConnectionString() => Container.GetConnectionString();
}
