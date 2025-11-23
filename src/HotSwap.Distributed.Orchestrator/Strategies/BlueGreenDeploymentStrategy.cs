using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Interfaces;
using HotSwap.Distributed.Orchestrator.Core;
using HotSwap.Distributed.Orchestrator.Interfaces;
using HotSwap.Distributed.Orchestrator.Services;
using Microsoft.Extensions.Logging;

namespace HotSwap.Distributed.Orchestrator.Strategies;

/// <summary>
/// Blue-Green deployment strategy - deploys to standby environment and switches traffic.
/// Suitable for Staging environment.
/// Uses resource-based stabilization checks to verify green environment stability before switching traffic.
/// </summary>
public class BlueGreenDeploymentStrategy : IDeploymentStrategy
{
    private readonly ILogger<BlueGreenDeploymentStrategy> _logger;
    private readonly IMetricsProvider? _metricsProvider;
    private readonly ResourceStabilizationService? _stabilizationService;
    private readonly TimeSpan _smokeTestTimeout;
    private readonly ResourceStabilizationConfig? _stabilizationConfig;

    public string StrategyName => "BlueGreen";

    /// <summary>
    /// Initializes a new instance with resource-based stabilization (recommended).
    /// </summary>
    public BlueGreenDeploymentStrategy(
        ILogger<BlueGreenDeploymentStrategy> logger,
        IMetricsProvider metricsProvider,
        ResourceStabilizationService stabilizationService,
        ResourceStabilizationConfig stabilizationConfig,
        TimeSpan? smokeTestTimeout = null)
    {
        _logger = logger;
        _metricsProvider = metricsProvider;
        _stabilizationService = stabilizationService;
        _stabilizationConfig = stabilizationConfig;
        _smokeTestTimeout = smokeTestTimeout ?? TimeSpan.FromMinutes(5);
    }

    /// <summary>
    /// Initializes a new instance with fixed time delays (legacy, for backward compatibility).
    /// </summary>
    public BlueGreenDeploymentStrategy(
        ILogger<BlueGreenDeploymentStrategy> logger,
        TimeSpan? smokeTestTimeout = null)
    {
        _logger = logger;
        _metricsProvider = null;
        _stabilizationService = null;
        _stabilizationConfig = null;
        _smokeTestTimeout = smokeTestTimeout ?? TimeSpan.FromMinutes(5);
    }

    public async Task<DeploymentResult> DeployAsync(
        ModuleDeploymentRequest request,
        EnvironmentCluster cluster,
        CancellationToken cancellationToken = default)
    {
        var result = new DeploymentResult
        {
            Strategy = StrategyName,
            Environment = cluster.Environment,
            StartTime = DateTime.UtcNow
        };

        try
        {
            _logger.LogInformation("Starting blue-green deployment of {ModuleName} v{Version} to {Environment}",
                request.ModuleName, request.Version, cluster.Environment);

            var nodes = cluster.Nodes.ToList();

            if (nodes.Count == 0)
            {
                result.Success = false;
                result.Message = "No nodes available in cluster";
                result.EndTime = DateTime.UtcNow;
                return result;
            }

            // Capture baseline metrics if using resource stabilization
            ClusterMetricsSnapshot? baselineMetrics = null;
            if (_stabilizationService != null && _metricsProvider != null)
            {
                _logger.LogInformation("Capturing baseline metrics for green environment");
                baselineMetrics = await _metricsProvider.GetClusterMetricsAsync(
                    cluster.Environment,
                    cancellationToken);
            }

            // In a real implementation, we would:
            // 1. Identify the "green" (standby) environment
            // 2. Deploy to green environment
            // 3. Wait for green environment to stabilize
            // 4. Run smoke tests
            // 5. Switch load balancer to green
            // 6. Monitor for issues
            // 7. Decommission blue (old) environment

            // For simulation, we'll deploy to all nodes and run smoke tests
            _logger.LogInformation("Deploying to green environment ({NodeCount} nodes)", nodes.Count);

            var deployTasks = nodes.Select(node =>
                node.DeployModuleAsync(request, cancellationToken));

            var nodeResults = await Task.WhenAll(deployTasks);
            result.NodeResults.AddRange(nodeResults);

            var failures = nodeResults.Where(r => !r.Success).ToList();

            if (failures.Any())
            {
                result.Success = false;
                result.Message = $"Deployment to green environment failed on {failures.Count} nodes";
                result.EndTime = DateTime.UtcNow;
                return result;
            }

            _logger.LogInformation("Green environment deployed successfully.");

            // Wait for green environment resources to stabilize before smoke tests
            if (_stabilizationService != null && _stabilizationConfig != null && baselineMetrics != null)
            {
                _logger.LogInformation("Waiting for green environment resources to stabilize (adaptive timing)...");

                var greenNodeIds = nodes.Select(n => n.NodeId);
                var stabilizationResult = await _stabilizationService.WaitForStabilizationAsync(
                    greenNodeIds,
                    baselineMetrics,
                    _stabilizationConfig,
                    cancellationToken);

                if (!stabilizationResult.IsStable)
                {
                    _logger.LogWarning(
                        "Green environment resources did not stabilize within timeout. Elapsed: {Elapsed:F1}s",
                        stabilizationResult.ElapsedTime.TotalSeconds);

                    result.Success = false;
                    result.Message = "Green environment resources did not stabilize. Not switching traffic.";
                    result.EndTime = DateTime.UtcNow;
                    return result;
                }

                _logger.LogInformation(
                    "Green environment stabilized after {Elapsed:F1}s ({Checks} checks, {Consecutive} consecutive stable)",
                    stabilizationResult.ElapsedTime.TotalSeconds,
                    stabilizationResult.TotalChecks,
                    stabilizationResult.ConsecutiveStableChecks);
            }

            _logger.LogInformation("Running smoke tests...");

            // Run smoke tests
            var smokeTestsPassed = await RunSmokeTestsAsync(cluster, cancellationToken);

            if (!smokeTestsPassed)
            {
                _logger.LogWarning("Smoke tests failed. Not switching traffic to green environment.");

                result.Success = false;
                result.Message = "Smoke tests failed. Traffic remains on blue environment.";
                result.EndTime = DateTime.UtcNow;
                return result;
            }

            _logger.LogInformation("Smoke tests passed. Switching traffic to green environment...");

            // Simulate load balancer switch
            await Task.Delay(1000, cancellationToken);

            _logger.LogInformation("Traffic switched to green environment successfully");

            result.Success = true;
            result.Message = $"Successfully deployed to {nodes.Count} nodes using blue-green strategy";
            result.EndTime = DateTime.UtcNow;

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Blue-green deployment failed for {ModuleName} to {Environment}",
                request.ModuleName, cluster.Environment);

            result.Success = false;
            result.Message = $"Deployment failed: {ex.Message}";
            result.Exception = ex;
            result.EndTime = DateTime.UtcNow;
            return result;
        }
    }

    private async Task<bool> RunSmokeTestsAsync(
        EnvironmentCluster cluster,
        CancellationToken cancellationToken)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(_smokeTestTimeout);

            _logger.LogInformation("Running smoke tests (timeout: {Timeout}s)",
                _smokeTestTimeout.TotalSeconds);

            // Simulate smoke tests
            await Task.Delay(2000, cts.Token);

            // Check health of all nodes
            var health = await cluster.GetHealthAsync(cts.Token);

            var allHealthy = health.HealthyNodes == health.TotalNodes;

            _logger.LogInformation("Smoke tests {Result}: {Healthy}/{Total} nodes healthy",
                allHealthy ? "PASSED" : "FAILED",
                health.HealthyNodes,
                health.TotalNodes);

            return allHealthy;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Smoke tests timed out after {Timeout}",
                _smokeTestTimeout);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running smoke tests");
            return false;
        }
    }
}
