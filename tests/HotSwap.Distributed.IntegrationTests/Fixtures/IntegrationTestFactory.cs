using HotSwap.Distributed.Api;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace HotSwap.Distributed.IntegrationTests.Fixtures;

/// <summary>
/// Custom WebApplicationFactory for integration testing.
/// Configures the API to use Testcontainers for dependencies.
/// </summary>
public class IntegrationTestFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainerFixture _postgreSqlFixture;
    private readonly RedisContainerFixture _redisFixture;

    public IntegrationTestFactory(
        PostgreSqlContainerFixture postgreSqlFixture,
        RedisContainerFixture redisFixture)
    {
        _postgreSqlFixture = postgreSqlFixture;
        _redisFixture = redisFixture;
    }

    /// <summary>
    /// Gets the PostgreSQL connection string from the test container.
    /// </summary>
    public string PostgreSqlConnectionString => _postgreSqlFixture.GetConnectionString();

    /// <summary>
    /// Gets the Redis connection string from the test container.
    /// </summary>
    public string RedisConnectionString => _redisFixture.GetConnectionString();

    /// <summary>
    /// Configures the test web host to use Testcontainers.
    /// </summary>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Override configuration to use test containers
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                // Redis configuration for distributed locking
                ["Redis:ConnectionString"] = RedisConnectionString,

                // PostgreSQL configuration for audit logs
                ["ConnectionStrings:PostgreSql"] = PostgreSqlConnectionString,

                // JWT configuration for authentication (test key)
                ["Jwt:SecretKey"] = "IntegrationTestSecretKey-MinimumLength32Characters",
                ["Jwt:Issuer"] = "IntegrationTestIssuer",
                ["Jwt:Audience"] = "IntegrationTestAudience",
                ["Jwt:ExpirationMinutes"] = "60",

                // Disable external telemetry services in tests
                ["Telemetry:JaegerEndpoint"] = "",

                // Security configuration for tests
                ["Security:StrictMode"] = "false", // Relax signature verification in tests

                // Rate limiting - higher limits for tests
                ["RateLimiting:GlobalRateLimit"] = "10000",
                ["RateLimiting:DeploymentRateLimit"] = "100",
                ["RateLimiting:ClusterRateLimit"] = "600",
                ["RateLimiting:ApprovalRateLimit"] = "300",
                ["RateLimiting:AuthenticationRateLimit"] = "50",

                // CORS - allow test origins
                ["Cors:AllowedOrigins:0"] = "http://localhost",

                // Pipeline configuration
                ["Pipeline:MaxConcurrentDeployments"] = "10",
                ["Pipeline:DefaultTimeoutMinutes"] = "5",

                // Reduce logging verbosity for integration tests
                // Only log warnings and errors to avoid 27k+ log lines
                ["Serilog:MinimumLevel:Default"] = "Warning",
                ["Serilog:MinimumLevel:Override:Microsoft"] = "Warning",
                ["Serilog:MinimumLevel:Override:Microsoft.AspNetCore"] = "Warning",
                ["Serilog:MinimumLevel:Override:System"] = "Warning",
                ["Serilog:MinimumLevel:Override:HotSwap.Distributed"] = "Warning",
                ["Logging:LogLevel:Default"] = "Warning",
                ["Logging:LogLevel:Microsoft.AspNetCore"] = "Warning",
                ["Logging:LogLevel:HotSwap.Distributed"] = "Warning",
            });
        });

        builder.ConfigureTestServices(services =>
        {
            // Additional service overrides for testing can go here
            // For example, replace background services that might interfere with tests
        });

        builder.UseEnvironment("Development");
    }

    /// <summary>
    /// Initializes the factory and ensures containers are ready.
    /// </summary>
    public async Task InitializeAsync()
    {
        // Containers are already initialized by their fixtures
        // This method is here for IAsyncLifetime compatibility
        await Task.CompletedTask;
    }

    /// <summary>
    /// Disposes the factory (containers are disposed by their fixtures).
    /// </summary>
    public new async Task DisposeAsync()
    {
        // Containers are disposed by their fixtures
        await base.DisposeAsync();
    }
}
