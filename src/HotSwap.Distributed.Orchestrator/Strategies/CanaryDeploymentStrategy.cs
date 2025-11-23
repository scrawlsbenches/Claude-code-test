using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Interfaces;
using HotSwap.Distributed.Orchestrator.Core;
using HotSwap.Distributed.Orchestrator.Interfaces;
using HotSwap.Distributed.Orchestrator.Services;
using Microsoft.Extensions.Logging;

namespace HotSwap.Distributed.Orchestrator.Strategies;

/// <summary>
/// Canary deployment strategy - gradual rollout with metrics analysis.
/// Safest strategy for Production environment.
/// Uses resource-based stabilization checks instead of fixed time delays.
/// </summary>
public class CanaryDeploymentStrategy : IDeploymentStrategy
{
    private readonly ILogger<CanaryDeploymentStrategy> _logger;
    private readonly IMetricsProvider _metricsProvider;
    private readonly ResourceStabilizationService? _stabilizationService;
    private readonly int _initialPercentage;
    private readonly int _incrementPercentage;
    private readonly TimeSpan _waitDuration; // Fallback for backward compatibility
    private readonly ResourceStabilizationConfig? _stabilizationConfig;

    public string StrategyName => "Canary";

    /// <summary>
    /// Initializes a new instance with resource-based stabilization (recommended).
    /// </summary>
    public CanaryDeploymentStrategy(
        ILogger<CanaryDeploymentStrategy> logger,
        IMetricsProvider metricsProvider,
        ResourceStabilizationService stabilizationService,
        ResourceStabilizationConfig stabilizationConfig,
        int initialPercentage = 10,
        int incrementPercentage = 20)
    {
        _logger = logger;
        _metricsProvider = metricsProvider;
        _stabilizationService = stabilizationService;
        _stabilizationConfig = stabilizationConfig;
        _initialPercentage = initialPercentage;
        _incrementPercentage = incrementPercentage;
        _waitDuration = TimeSpan.FromMinutes(15); // Not used with stabilization service
    }

    /// <summary>
    /// Initializes a new instance with fixed time delays (legacy, for backward compatibility).
    /// </summary>
    public CanaryDeploymentStrategy(
        ILogger<CanaryDeploymentStrategy> logger,
        IMetricsProvider metricsProvider,
        int initialPercentage = 10,
        int incrementPercentage = 20,
        TimeSpan? waitDuration = null)
    {
        _logger = logger;
        _metricsProvider = metricsProvider;
        _stabilizationService = null; // Legacy mode - use fixed wait duration
        _stabilizationConfig = null;
        _initialPercentage = initialPercentage;
        _incrementPercentage = incrementPercentage;
        _waitDuration = waitDuration ?? TimeSpan.FromMinutes(15);
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
            _logger.LogInformation("Starting canary deployment of {ModuleName} v{Version} to {Environment}",
                request.ModuleName, request.Version, cluster.Environment);

            var allNodes = cluster.Nodes.ToList();

            if (allNodes.Count == 0)
            {
                result.Success = false;
                result.Message = "No nodes available in cluster";
                result.EndTime = DateTime.UtcNow;
                return result;
            }

            // Capture baseline metrics
            _logger.LogInformation("Capturing baseline metrics from cluster");
            var baselineMetrics = await _metricsProvider.GetClusterMetricsAsync(
                cluster.Environment,
                cancellationToken);

            var deployedNodes = new List<KernelNode>();
            var currentPercentage = _initialPercentage;

            while (deployedNodes.Count < allNodes.Count)
            {
                // Calculate how many nodes to deploy in this wave
                var targetCount = Math.Min(
                    (int)Math.Ceiling(allNodes.Count * currentPercentage / 100.0),
                    allNodes.Count);

                var nodesToDeploy = allNodes
                    .Except(deployedNodes)
                    .Take(targetCount - deployedNodes.Count)
                    .ToList();

                if (!nodesToDeploy.Any())
                    break;

                _logger.LogInformation("Canary wave: deploying to {Count} nodes ({Percentage}% of cluster)",
                    nodesToDeploy.Count,
                    (double)(deployedNodes.Count + nodesToDeploy.Count) / allNodes.Count * 100);

                // Deploy to canary nodes
                var waveTasks = nodesToDeploy.Select(node =>
                    node.DeployModuleAsync(request, cancellationToken));

                var waveResults = await Task.WhenAll(waveTasks);
                result.NodeResults.AddRange(waveResults);

                var failures = waveResults.Where(r => !r.Success).ToList();

                if (failures.Any())
                {
                    _logger.LogWarning("Canary deployment failed on {FailureCount} nodes",
                        failures.Count);

                    // Rollback all successful deployments
                    result.RollbackPerformed = true;
                    await RollbackAllAsync(request.ModuleName, deployedNodes, cluster, result);

                    result.Success = false;
                    result.Message = $"Canary deployment failed. Rolled back all changes.";
                    result.EndTime = DateTime.UtcNow;
                    return result;
                }

                deployedNodes.AddRange(nodesToDeploy);

                // If not all nodes deployed, wait and analyze metrics
                if (deployedNodes.Count < allNodes.Count)
                {
                    bool canaryHealthy;

                    // Use resource-based stabilization if available, otherwise fall back to fixed wait
                    if (_stabilizationService != null && _stabilizationConfig != null)
                    {
                        _logger.LogInformation("Waiting for canary resources to stabilize (adaptive timing)...");

                        var nodeIds = deployedNodes.Select(n => n.NodeId);
                        var stabilizationResult = await _stabilizationService.WaitForStabilizationAsync(
                            nodeIds,
                            baselineMetrics,
                            _stabilizationConfig,
                            cancellationToken);

                        if (!stabilizationResult.IsStable)
                        {
                            _logger.LogWarning(
                                "Canary resources did not stabilize within timeout. Timeout: {Timeout}, Elapsed: {Elapsed:F1}s",
                                stabilizationResult.TimeoutReached ? "Yes" : "No",
                                stabilizationResult.ElapsedTime.TotalSeconds);

                            canaryHealthy = false;
                        }
                        else
                        {
                            _logger.LogInformation(
                                "Canary resources stabilized after {Elapsed:F1}s ({Checks} checks, {Consecutive} consecutive stable)",
                                stabilizationResult.ElapsedTime.TotalSeconds,
                                stabilizationResult.TotalChecks,
                                stabilizationResult.ConsecutiveStableChecks);

                            // Additional legacy metrics check for extra safety
                            canaryHealthy = await AnalyzeCanaryMetricsAsync(
                                deployedNodes,
                                baselineMetrics,
                                cancellationToken);
                        }
                    }
                    else
                    {
                        // Legacy mode: fixed time delay
                        _logger.LogInformation("Waiting {Duration}m before metrics analysis (legacy fixed-time mode)",
                            _waitDuration.TotalMinutes);

                        await Task.Delay(_waitDuration, cancellationToken);

                        // Analyze canary metrics
                        _logger.LogInformation("Analyzing canary metrics...");

                        canaryHealthy = await AnalyzeCanaryMetricsAsync(
                            deployedNodes,
                            baselineMetrics,
                            cancellationToken);
                    }

                    if (!canaryHealthy)
                    {
                        _logger.LogWarning("Canary metrics analysis failed. Rolling back deployment.");

                        result.RollbackPerformed = true;
                        await RollbackAllAsync(request.ModuleName, deployedNodes, cluster, result);

                        result.Success = false;
                        result.Message = "Canary metrics degraded. Rolled back deployment.";
                        result.EndTime = DateTime.UtcNow;
                        return result;
                    }

                    _logger.LogInformation("Canary metrics healthy. Proceeding to next wave.");

                    // Increase percentage for next wave
                    currentPercentage += _incrementPercentage;
                }
            }

            result.Success = true;
            result.Message = $"Successfully deployed to all {allNodes.Count} nodes using canary strategy";
            result.EndTime = DateTime.UtcNow;

            _logger.LogInformation("Canary deployment succeeded for {ModuleName} to {Environment}",
                request.ModuleName, cluster.Environment);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Canary deployment failed for {ModuleName} to {Environment}",
                request.ModuleName, cluster.Environment);

            result.Success = false;
            result.Message = $"Deployment failed: {ex.Message}";
            result.Exception = ex;
            result.EndTime = DateTime.UtcNow;
            return result;
        }
    }

    private async Task<bool> AnalyzeCanaryMetricsAsync(
        List<KernelNode> canaryNodes,
        ClusterMetricsSnapshot baseline,
        CancellationToken cancellationToken)
    {
        try
        {
            // Get current metrics from canary nodes
            var canaryNodeIds = canaryNodes.Select(n => n.NodeId);
            var canaryMetrics = await _metricsProvider.GetNodesMetricsAsync(
                canaryNodeIds,
                cancellationToken);

            var avgCanaryMetrics = new
            {
                CpuUsage = canaryMetrics.Average(m => m.CpuUsagePercent),
                MemoryUsage = canaryMetrics.Average(m => m.MemoryUsagePercent),
                Latency = canaryMetrics.Average(m => m.LatencyMs),
                ErrorRate = canaryMetrics.Average(m => m.ErrorRate)
            };

            _logger.LogInformation("Canary metrics - CPU: {Cpu}%, Memory: {Memory}%, Latency: {Latency}ms, ErrorRate: {ErrorRate}%",
                avgCanaryMetrics.CpuUsage,
                avgCanaryMetrics.MemoryUsage,
                avgCanaryMetrics.Latency,
                avgCanaryMetrics.ErrorRate);

            // Compare against baseline with thresholds
            var cpuIncrease = (avgCanaryMetrics.CpuUsage - baseline.AvgCpuUsage) / baseline.AvgCpuUsage * 100;
            var memoryIncrease = (avgCanaryMetrics.MemoryUsage - baseline.AvgMemoryUsage) / baseline.AvgMemoryUsage * 100;
            var latencyIncrease = (avgCanaryMetrics.Latency - baseline.AvgLatency) / baseline.AvgLatency * 100;
            var errorRateIncrease = (avgCanaryMetrics.ErrorRate - baseline.AvgErrorRate) / Math.Max(baseline.AvgErrorRate, 0.1) * 100;

            _logger.LogInformation("Metric changes - CPU: {Cpu:+0.0;-0.0}%, Memory: {Memory:+0.0;-0.0}%, Latency: {Latency:+0.0;-0.0}%, ErrorRate: {ErrorRate:+0.0;-0.0}%",
                cpuIncrease, memoryIncrease, latencyIncrease, errorRateIncrease);

            // Thresholds from specification
            if (errorRateIncrease > 50)
            {
                _logger.LogWarning("Error rate increased by {Increase}% (threshold: 50%)", errorRateIncrease);
                return false;
            }

            if (latencyIncrease > 100)
            {
                _logger.LogWarning("Latency increased by {Increase}% (threshold: 100%)", latencyIncrease);
                return false;
            }

            if (cpuIncrease > 30)
            {
                _logger.LogWarning("CPU usage increased by {Increase}% (threshold: 30%)", cpuIncrease);
                return false;
            }

            if (memoryIncrease > 30)
            {
                _logger.LogWarning("Memory usage increased by {Increase}% (threshold: 30%)", memoryIncrease);
                return false;
            }

            _logger.LogInformation("Canary metrics are healthy");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing canary metrics");
            return false;
        }
    }

    private async Task RollbackAllAsync(
        string moduleName,
        List<KernelNode> deployedNodes,
        EnvironmentCluster cluster,
        DeploymentResult result)
    {
        _logger.LogInformation("Rolling back {Count} canary deployments", deployedNodes.Count);

        var rollbackTasks = deployedNodes.Select(node =>
            node.RollbackModuleAsync(moduleName));

        var rollbackResults = await Task.WhenAll(rollbackTasks);
        result.RollbackResults.AddRange(rollbackResults);

        var rollbackSuccessCount = rollbackResults.Count(r => r.Success);
        result.RollbackSuccessful = rollbackSuccessCount == rollbackResults.Length;

        _logger.LogInformation("Rollback completed: {Success}/{Total} successful",
            rollbackSuccessCount, rollbackResults.Length);
    }
}
