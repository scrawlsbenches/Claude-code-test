using HotSwap.Distributed.Api;
using HotSwap.Distributed.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

namespace HotSwap.Distributed.IntegrationTests.Fixtures;

/// <summary>
/// Custom WebApplicationFactory for integration testing.
/// Uses in-memory alternatives (SQLite in-memory, MemoryDistributedCache) instead of Docker containers.
/// This allows tests to run anywhere without Docker dependencies.
/// </summary>
public class IntegrationTestFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly SqliteConnection _sqliteConnection;

    public IntegrationTestFactory()
    {
        // Create and open SQLite in-memory connection in constructor
        // Must stay open for lifetime of tests or database will be destroyed
        _sqliteConnection = new SqliteConnection("Data Source=:memory:");
        _sqliteConnection.Open();
    }

    /// <summary>
    /// Gets the SQLite in-memory connection string.
    /// </summary>
    public string PostgreSqlConnectionString => "Data Source=:memory:";

    /// <summary>
    /// Configures the test web host to use in-memory alternatives.
    /// </summary>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Override configuration to use in-memory alternatives
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                // SQLite in-memory configuration for audit logs
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

                // Disable rate limiting for integration tests
                // Tests should not be blocked by rate limits - they test functionality, not rate limiting
                ["RateLimiting:Enabled"] = "false",

                // CORS - allow test origins
                ["Cors:AllowedOrigins:0"] = "http://localhost",

                // Pipeline configuration - use FAST timeouts for integration tests
                ["Pipeline:MaxConcurrentPipelines"] = "10",
                ["Pipeline:QaMaxConcurrentNodes"] = "4",
                ["Pipeline:StagingSmokeTestTimeout"] = "00:00:10", // 10 seconds (vs 5 minutes production)
                ["Pipeline:CanaryInitialPercentage"] = "10",
                ["Pipeline:CanaryIncrementPercentage"] = "50", // Faster rollout: 50% increments vs 20% production
                ["Pipeline:CanaryWaitDuration"] = "00:00:05", // 5 SECONDS (vs 15 MINUTES production) - CRITICAL
                ["Pipeline:AutoRollbackOnFailure"] = "true",
                ["Pipeline:ApprovalTimeoutHours"] = "1", // 1 hour for integration tests (vs 24 hours production)

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

                // Disable OpenTelemetry Activity console output (prevents massive trace spam)
                ["Logging:Console:FormatterName"] = "simple",
                ["Logging:Console:FormatterOptions:IncludeScopes"] = "false",
            });
        });

        builder.ConfigureTestServices(services =>
        {
            // Replace PostgreSQL DbContext with SQLite in-memory
            var dbContextDescriptor = services.FirstOrDefault(d =>
                d.ServiceType == typeof(Microsoft.EntityFrameworkCore.DbContextOptions<HotSwap.Distributed.Infrastructure.Data.AuditLogDbContext>));
            if (dbContextDescriptor != null)
            {
                services.Remove(dbContextDescriptor);
            }

            // Add SQLite in-memory DbContext
            services.AddDbContext<HotSwap.Distributed.Infrastructure.Data.AuditLogDbContext>(options =>
            {
                options.UseSqlite(_sqliteConnection);
            });

            // Replace Redis with MemoryDistributedCache for in-memory distributed locking
            // Remove any existing IConnectionMultiplexer registrations
            var redisDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IConnectionMultiplexer));
            if (redisDescriptor != null)
            {
                services.Remove(redisDescriptor);
            }

            // Remove any existing IDistributedCache registrations
            var cacheDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(Microsoft.Extensions.Caching.Distributed.IDistributedCache));
            if (cacheDescriptor != null)
            {
                services.Remove(cacheDescriptor);
            }

            // Add in-memory distributed cache (no Redis needed)
            services.AddDistributedMemoryCache();

            // Replace RedisDistributedLock with InMemoryDistributedLock
            var lockDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IDistributedLock));
            if (lockDescriptor != null)
            {
                services.Remove(lockDescriptor);
            }

            // Add in-memory distributed lock (no Redis needed)
            services.AddSingleton<IDistributedLock, InMemoryDistributedLock>();
        });

        builder.UseEnvironment("Development");
    }

    /// <summary>
    /// Initializes the factory and ensures database schema is created.
    /// </summary>
    public async Task InitializeAsync()
    {
        // Ensure database schema is created
        using (var scope = Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<HotSwap.Distributed.Infrastructure.Data.AuditLogDbContext>();
            await dbContext.Database.EnsureCreatedAsync();
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Disposes the factory and closes in-memory database connection.
    /// </summary>
    public new async Task DisposeAsync()
    {
        // Close and dispose SQLite connection
        await _sqliteConnection.CloseAsync();
        await _sqliteConnection.DisposeAsync();

        await base.DisposeAsync();
    }
}
