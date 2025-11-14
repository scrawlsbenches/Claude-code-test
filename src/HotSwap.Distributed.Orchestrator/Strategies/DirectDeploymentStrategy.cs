using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Orchestrator.Core;
using HotSwap.Distributed.Orchestrator.Interfaces;
using Microsoft.Extensions.Logging;

namespace HotSwap.Distributed.Orchestrator.Strategies;

/// <summary>
/// Direct deployment strategy - deploys to all nodes simultaneously.
/// Fastest but highest risk. Suitable for Development environment.
/// </summary>
public class DirectDeploymentStrategy : IDeploymentStrategy
{
    private readonly ILogger<DirectDeploymentStrategy> _logger;

    public string StrategyName => "Direct";

    public DirectDeploymentStrategy(ILogger<DirectDeploymentStrategy> logger)
    {
        _logger = logger;
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
            _logger.LogInformation("Starting direct deployment of {ModuleName} v{Version} to {Environment} ({NodeCount} nodes)",
                request.ModuleName, request.Version, cluster.Environment, cluster.NodeCount);

            var nodes = cluster.Nodes.ToList();

            if (nodes.Count == 0)
            {
                result.Success = false;
                result.Message = "No nodes available in cluster";
                result.EndTime = DateTime.UtcNow;
                return result;
            }

            // Deploy to all nodes in parallel
            var deployTasks = nodes.Select(node =>
                node.DeployModuleAsync(request, cancellationToken));

            var nodeResults = await Task.WhenAll(deployTasks);
            result.NodeResults.AddRange(nodeResults);

            var successCount = nodeResults.Count(r => r.Success);
            var failureCount = nodeResults.Count(r => !r.Success);

            if (failureCount > 0)
            {
                _logger.LogWarning("Direct deployment partially failed: {Success}/{Total} nodes succeeded",
                    successCount, nodes.Count);

                // Rollback all successful deployments
                result.RollbackPerformed = true;
                await RollbackAsync(request.ModuleName, nodeResults.Where(r => r.Success), cluster, result);

                result.Success = false;
                result.Message = $"Deployment failed on {failureCount} nodes. Rolled back all changes.";
            }
            else
            {
                result.Success = true;
                result.Message = $"Successfully deployed to all {successCount} nodes";

                _logger.LogInformation("Direct deployment succeeded for {ModuleName} to {Environment}",
                    request.ModuleName, cluster.Environment);
            }

            result.EndTime = DateTime.UtcNow;
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Direct deployment failed for {ModuleName} to {Environment}",
                request.ModuleName, cluster.Environment);

            result.Success = false;
            result.Message = $"Deployment failed: {ex.Message}";
            result.Exception = ex;
            result.EndTime = DateTime.UtcNow;
            return result;
        }
    }

    private async Task RollbackAsync(
        string moduleName,
        IEnumerable<NodeDeploymentResult> successfulDeployments,
        EnvironmentCluster cluster,
        DeploymentResult result)
    {
        _logger.LogInformation("Rolling back {Count} successful deployments",
            successfulDeployments.Count());

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
