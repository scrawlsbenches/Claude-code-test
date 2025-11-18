namespace HotSwap.Distributed.IntegrationTests.Fixtures;

/// <summary>
/// Shared fixture that manages all integration test resources (containers and factory).
/// This ensures containers and factory are created once per test collection, not per test class.
/// </summary>
public class SharedIntegrationTestFixture : IAsyncLifetime
{
    private PostgreSqlContainerFixture? _postgreSqlFixture;
    private RedisContainerFixture? _redisFixture;
    private IntegrationTestFactory? _factory;

    /// <summary>
    /// Gets the PostgreSQL container fixture.
    /// </summary>
    public PostgreSqlContainerFixture PostgreSqlFixture =>
        _postgreSqlFixture ?? throw new InvalidOperationException("Fixture not initialized");

    /// <summary>
    /// Gets the Redis container fixture.
    /// </summary>
    public RedisContainerFixture RedisFixture =>
        _redisFixture ?? throw new InvalidOperationException("Fixture not initialized");

    /// <summary>
    /// Gets the integration test factory.
    /// </summary>
    public IntegrationTestFactory Factory =>
        _factory ?? throw new InvalidOperationException("Fixture not initialized");

    /// <summary>
    /// Initializes all shared resources (containers and factory).
    /// </summary>
    public async Task InitializeAsync()
    {
        // Initialize container fixtures first
        _postgreSqlFixture = new PostgreSqlContainerFixture();
        await _postgreSqlFixture.InitializeAsync();

        _redisFixture = new RedisContainerFixture();
        await _redisFixture.InitializeAsync();

        // Initialize factory after containers are ready
        _factory = new IntegrationTestFactory(_postgreSqlFixture, _redisFixture);
        await _factory.InitializeAsync();
    }

    /// <summary>
    /// Disposes all shared resources.
    /// </summary>
    public async Task DisposeAsync()
    {
        // Dispose in reverse order
        if (_factory != null)
        {
            await _factory.DisposeAsync();
        }

        if (_redisFixture != null)
        {
            await _redisFixture.DisposeAsync();
        }

        if (_postgreSqlFixture != null)
        {
            await _postgreSqlFixture.DisposeAsync();
        }
    }
}
