using HotSwap.Distributed.Orchestrator.Core;

namespace HotSwap.Distributed.Api.Services;

/// <summary>
/// Background service for initializing the orchestrator on startup.
/// Replaces blocking InitializeClustersAsync().GetAwaiter().GetResult() call.
/// </summary>
public class OrchestratorInitializationService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OrchestratorInitializationService> _logger;
    private bool _initialized = false;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public OrchestratorInitializationService(
        IServiceProvider serviceProvider,
        ILogger<OrchestratorInitializationService> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _initLock.WaitAsync(cancellationToken);
        try
        {
            // Only initialize once
            if (_initialized)
            {
                _logger.LogDebug("Orchestrator already initialized, skipping");
                return;
            }

            _logger.LogInformation("Orchestrator initialization service starting");

            // Create a scope to resolve scoped services
            var scopeFactory = _serviceProvider.GetService(typeof(IServiceScopeFactory)) as IServiceScopeFactory;
            if (scopeFactory == null)
            {
                throw new InvalidOperationException("IServiceScopeFactory not found");
            }

            using var scope = scopeFactory.CreateScope();
            try
            {
                // Get the orchestrator from the scoped service provider
                var orchestrator = scope.ServiceProvider.GetService(typeof(DistributedKernelOrchestrator)) as DistributedKernelOrchestrator;
                if (orchestrator == null)
                {
                    throw new InvalidOperationException("DistributedKernelOrchestrator not found in service provider");
                }

                // Initialize clusters asynchronously
                await orchestrator.InitializeClustersAsync(cancellationToken);

                _logger.LogInformation("Orchestrator clusters initialized successfully");
                _initialized = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize orchestrator clusters");
                throw;
            }
        }
        finally
        {
            _initLock.Release();
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Orchestrator initialization service stopping");
        return Task.CompletedTask;
    }
}
