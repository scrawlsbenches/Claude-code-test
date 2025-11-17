using Testcontainers.PostgreSql;

namespace HotSwap.Distributed.IntegrationTests.Fixtures;

/// <summary>
/// Manages PostgreSQL Docker container lifecycle for integration tests.
/// Implements IAsyncLifetime for xUnit test collection fixtures.
/// </summary>
public class PostgreSqlContainerFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _container;

    /// <summary>
    /// Gets the PostgreSQL container instance.
    /// </summary>
    public PostgreSqlContainer Container => _container
        ?? throw new InvalidOperationException("Container not initialized. Call InitializeAsync first.");

    /// <summary>
    /// Initializes and starts the PostgreSQL container.
    /// Called automatically by xUnit before tests run.
    /// </summary>
    public async Task InitializeAsync()
    {
        _container = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("distributed_kernel_test")
            .WithUsername("testuser")
            .WithPassword("testpass")
            .WithCleanUp(true)
            .Build();

        await _container.StartAsync();
    }

    /// <summary>
    /// Stops and disposes the PostgreSQL container.
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
    /// Gets the connection string for the running PostgreSQL container.
    /// </summary>
    public string GetConnectionString() => Container.GetConnectionString();
}
