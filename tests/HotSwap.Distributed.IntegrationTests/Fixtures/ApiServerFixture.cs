using Xunit;

namespace HotSwap.Distributed.IntegrationTests.Fixtures;

/// <summary>
/// Class fixture for integration tests that provides a shared API server instance.
/// This ensures all test methods in a class use the same server and in-memory cache.
/// </summary>
public class ApiServerFixture : IAsyncLifetime
{
    private IntegrationTestFactory? _factory;

    public ApiServerFixture(
        PostgreSqlContainerFixture postgreSqlFixture,
        RedisContainerFixture redisFixture)
    {
        PostgreSqlFixture = postgreSqlFixture ?? throw new ArgumentNullException(nameof(postgreSqlFixture));
        RedisFixture = redisFixture ?? throw new ArgumentNullException(nameof(redisFixture));
    }

    public PostgreSqlContainerFixture PostgreSqlFixture { get; }
    public RedisContainerFixture RedisFixture { get; }

    public IntegrationTestFactory Factory
    {
        get => _factory ?? throw new InvalidOperationException("Factory not initialized. Call InitializeAsync first.");
    }

    public async Task InitializeAsync()
    {
        // Create factory once for the entire test class
        _factory = new IntegrationTestFactory(PostgreSqlFixture, RedisFixture);
        await _factory.InitializeAsync();
    }

    public async Task DisposeAsync()
    {
        if (_factory != null)
        {
            await _factory.DisposeAsync();
        }
    }

    /// <summary>
    /// Creates a new HttpClient for the shared API server.
    /// Each test method should create its own client but they all talk to the same server.
    /// </summary>
    public HttpClient CreateClient()
    {
        return Factory.CreateClient();
    }
}
