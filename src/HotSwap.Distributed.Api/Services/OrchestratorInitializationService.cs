using HotSwap.Distributed.Orchestrator.Core;

namespace HotSwap.Distributed.Api.Services;

/// <summary>
/// Background service for initializing the orchestrator on startup.
/// Replaces blocking InitializeClustersAsync().GetAwaiter().GetResult() call.
/// </summary>
public class OrchestratorInitializationService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OrchestratorInitializationService> _logger;

    public OrchestratorInitializationService(
        IServiceProvider serviceProvider,
        ILogger<OrchestratorInitializationService> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Orchestrator initialization service starting");

        try
        {
            // Get the orchestrator from the service provider
            var orchestrator = _serviceProvider.GetRequiredService<DistributedKernelOrchestrator>();

            // Initialize clusters asynchronously
            await orchestrator.InitializeClustersAsync();

            _logger.LogInformation("Orchestrator clusters initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize orchestrator clusters");
            // Note: In production, you might want to exit the application if initialization fails
            throw;
        }
    }
}
