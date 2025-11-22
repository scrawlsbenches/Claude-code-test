using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Orchestrator.Core;
using HotSwap.Distributed.Orchestrator.Interfaces;
using Microsoft.Extensions.Logging;

namespace HotSwap.Distributed.Orchestrator.Strategies;

/// <summary>
/// Rolling deployment strategy - deploys sequentially with health checks.
/// Suitable for QA environment.
/// </summary>
public class RollingDeploymentStrategy : IDeploymentStrategy
{
    private readonly ILogger<RollingDeploymentStrategy> _logger;
    private readonly int _maxConcurrent;
    private readonly TimeSpan _healthCheckDelay;

    public string StrategyName => "Rolling";

    public RollingDeploymentStrategy(
        ILogger<RollingDeploymentStrategy> logger,
        int maxConcurrent = 2,
        TimeSpan? healthCheckDelay = null)
    {
        _logger = logger;
        _maxConcurrent = maxConcurrent;
        _healthCheckDelay = healthCheckDelay ?? TimeSpan.FromSeconds(30);
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
            _logger.LogInformation("Starting rolling deployment of {ModuleName} v{Version} to {Environment} (batch size: {BatchSize})",
                request.ModuleName, request.Version, cluster.Environment, _maxConcurrent);

            // Sort nodes by hostname to ensure deterministic batch ordering across deployments
            var nodes = cluster.Nodes.OrderBy(n => n.Hostname).ToList();

            if (nodes.Count == 0)
            {
                result.Success = false;
                result.Message = "No nodes available in cluster";
                result.EndTime = DateTime.UtcNow;
                return result;
            }

            var successfulDeployments = new List<NodeDeploymentResult>();

            // Process nodes in batches
            for (int i = 0; i < nodes.Count; i += _maxConcurrent)
            {
                var batch = nodes.Skip(i).Take(_maxConcurrent).ToList();

                _logger.LogInformation("Deploying to batch {BatchNum}/{TotalBatches} ({NodeCount} nodes)",
                    (i / _maxConcurrent) + 1,
                    (nodes.Count + _maxConcurrent - 1) / _maxConcurrent,
                    batch.Count);

                // Deploy to batch in parallel
                var batchTasks = batch.Select(node =>
                    node.DeployModuleAsync(request, cancellationToken));

                var batchResults = await Task.WhenAll(batchTasks);
                result.NodeResults.AddRange(batchResults);

                // Check for failures in batch
                var failures = batchResults.Where(r => !r.Success).ToList();

                if (failures.Any())
                {
                    _logger.LogWarning("Rolling deployment failed on {FailureCount} nodes in current batch",
                        failures.Count);

                    // Rollback all successful deployments
                    result.RollbackPerformed = true;
                    await RollbackAllAsync(request.ModuleName, successfulDeployments, cluster, result);

                    result.Success = false;
                    result.Message = $"Deployment failed on batch {(i / _maxConcurrent) + 1}. Rolled back all changes.";
                    result.EndTime = DateTime.UtcNow;
                    return result;
                }

                successfulDeployments.AddRange(batchResults);

                // Wait and perform health checks before next batch
                if (i + _maxConcurrent < nodes.Count)
                {
                    _logger.LogDebug("Waiting {Delay}s before health check",
                        _healthCheckDelay.TotalSeconds);

                    await Task.Delay(_healthCheckDelay, cancellationToken);

                    // Check health of deployed nodes
                    var healthCheckTasks = batch.Select(n => n.GetHealthAsync(cancellationToken));
                    var healthStatuses = await Task.WhenAll(healthCheckTasks);

                    var unhealthyNodes = healthStatuses.Where(h => !h.IsHealthy).ToList();

                    if (unhealthyNodes.Any())
                    {
                        _logger.LogWarning("Health check failed for {Count} nodes", unhealthyNodes.Count);

                        result.RollbackPerformed = true;
                        await RollbackAllAsync(request.ModuleName, successfulDeployments, cluster, result);

                        result.Success = false;
                        result.Message = "Health check failed after deployment. Rolled back all changes.";
                        result.EndTime = DateTime.UtcNow;
                        return result;
                    }
                }
            }

            result.Success = true;
            result.Message = $"Successfully deployed to all {nodes.Count} nodes using rolling strategy";
            result.EndTime = DateTime.UtcNow;

            _logger.LogInformation("Rolling deployment succeeded for {ModuleName} to {Environment}",
                request.ModuleName, cluster.Environment);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Rolling deployment failed for {ModuleName} to {Environment}",
                request.ModuleName, cluster.Environment);

            result.Success = false;
            result.Message = $"Deployment failed: {ex.Message}";
            result.Exception = ex;
            result.EndTime = DateTime.UtcNow;
            return result;
        }
    }

    private async Task RollbackAllAsync(
        string moduleName,
        List<NodeDeploymentResult> successfulDeployments,
        EnvironmentCluster cluster,
        DeploymentResult result)
    {
        _logger.LogInformation("Rolling back {Count} successful deployments",
            successfulDeployments.Count);

        var rollbackTasks = successfulDeployments.Select(async nodeResult =>
        {
            var node = cluster.GetNode(nodeResult.NodeId);
            if (node != null)
            {
                return await node.RollbackModuleAsync(moduleName);
            }
            return new NodeRollbackResult
            {
                NodeId = nodeResult.NodeId,
                Success = false,
                Message = "Node not found in cluster"
            };
        });

        var rollbackResults = await Task.WhenAll(rollbackTasks);
        result.RollbackResults.AddRange(rollbackResults);

        var rollbackSuccessCount = rollbackResults.Count(r => r.Success);
        result.RollbackSuccessful = rollbackSuccessCount == rollbackResults.Length;

        _logger.LogInformation("Rollback completed: {Success}/{Total} successful",
            rollbackSuccessCount, rollbackResults.Length);
    }
}
