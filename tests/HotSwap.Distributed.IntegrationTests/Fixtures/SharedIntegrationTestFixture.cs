namespace HotSwap.Distributed.IntegrationTests.Fixtures;

/// <summary>
/// Shared fixture that manages all integration test resources.
/// Uses in-memory alternatives (SQLite, MemoryDistributedCache) instead of Docker containers.
/// This ensures the factory is created once per test collection, not per test class,
/// and tests can run anywhere without Docker dependencies.
/// </summary>
public class SharedIntegrationTestFixture : IAsyncLifetime
{
    private IntegrationTestFactory? _factory;

    /// <summary>
    /// Gets the integration test factory with in-memory dependencies.
    /// </summary>
    public IntegrationTestFactory Factory =>
        _factory ?? throw new InvalidOperationException("Fixture not initialized");

    /// <summary>
    /// Initializes the shared factory with in-memory dependencies.
    /// No Docker containers needed - uses SQLite in-memory and MemoryDistributedCache.
    /// </summary>
    public async Task InitializeAsync()
    {
        // Create factory with in-memory dependencies (no containers needed)
        _factory = new IntegrationTestFactory();
        await _factory.InitializeAsync();
    }

    /// <summary>
    /// Disposes the shared factory and cleans up in-memory resources.
    /// </summary>
    public async Task DisposeAsync()
    {
        if (_factory != null)
        {
            await _factory.DisposeAsync();
        }
    }
}
