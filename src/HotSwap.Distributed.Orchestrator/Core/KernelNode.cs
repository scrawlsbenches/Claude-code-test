using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;
using Microsoft.Extensions.Logging;

namespace HotSwap.Distributed.Orchestrator.Core;

/// <summary>
/// Represents a single kernel node in the distributed system.
/// </summary>
public class KernelNode : IAsyncDisposable
{
    private readonly NodeConfiguration _config;
    private readonly ILogger<KernelNode> _logger;
    private readonly SemaphoreSlim _disposeLock = new(1, 1);
    private readonly object _heartbeatLock = new();
    private DateTime _lastHeartbeat;
    private bool _disposed;

    public Guid NodeId => _config.NodeId;
    public string Hostname => _config.Hostname;
    public int Port => _config.Port;
    public EnvironmentType Environment => _config.Environment;

    private NodeStatus _status;
    public NodeStatus Status
    {
        get => _status;
        private set => _status = value;
    }

    public DateTime LastHeartbeat
    {
        get
        {
            lock (_heartbeatLock)
            {
                return _lastHeartbeat;
            }
        }
    }

    private KernelNode(
        NodeConfiguration config,
        ILogger<KernelNode> logger)
    {
        _config = config;
        _logger = logger;
        _status = NodeStatus.Initializing;
        _lastHeartbeat = DateTime.UtcNow;
    }

    /// <summary>
    /// Factory method to create and initialize a kernel node.
    /// </summary>
    public static async Task<KernelNode> CreateAsync(
        NodeConfiguration config,
        ILogger<KernelNode> logger,
        CancellationToken cancellationToken = default)
    {
        var node = new KernelNode(config, logger);

        try
        {
            logger.LogInformation("Initializing kernel node {NodeId} at {Hostname}:{Port}",
                node.NodeId, node.Hostname, node.Port);

            // Simulate initialization
            await Task.Delay(100, cancellationToken);

            node.Status = NodeStatus.Running;
            node.UpdateHeartbeat();

            logger.LogInformation("Kernel node {NodeId} initialized successfully", node.NodeId);

            return node;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initialize kernel node {NodeId}", node.NodeId);
            node.Status = NodeStatus.Failed;
            throw;
        }
    }

    /// <summary>
    /// Deploys a module to this node.
    /// </summary>
    public async Task<NodeDeploymentResult> DeployModuleAsync(
        ModuleDeploymentRequest request,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            _logger.LogInformation("Deploying module {ModuleName} v{Version} to node {NodeId}",
                request.ModuleName, request.Version, NodeId);

            // Simulate module deployment (hot swap)
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);

            // Update heartbeat after successful deployment
            UpdateHeartbeat();

            var result = new NodeDeploymentResult
            {
                NodeId = NodeId,
                Hostname = Hostname,
                Success = true,
                Message = $"Successfully deployed {request.ModuleName} v{request.Version}",
                Timestamp = DateTime.UtcNow,
                Duration = DateTime.UtcNow - startTime
            };

            _logger.LogInformation("Module {ModuleName} deployed successfully to node {NodeId} in {Duration}ms",
                request.ModuleName, NodeId, result.Duration.TotalMilliseconds);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deploy module {ModuleName} to node {NodeId}",
                request.ModuleName, NodeId);

            Status = NodeStatus.Degraded;

            return new NodeDeploymentResult
            {
                NodeId = NodeId,
                Hostname = Hostname,
                Success = false,
                Message = $"Deployment failed: {ex.Message}",
                Exception = ex,
                Timestamp = DateTime.UtcNow,
                Duration = DateTime.UtcNow - startTime
            };
        }
    }

    /// <summary>
    /// Rolls back to previous module version.
    /// </summary>
    public async Task<NodeRollbackResult> RollbackModuleAsync(
        string moduleName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Rolling back module {ModuleName} on node {NodeId}",
                moduleName, NodeId);

            // Simulate rollback
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);

            UpdateHeartbeat();

            _logger.LogInformation("Module {ModuleName} rolled back successfully on node {NodeId}",
                moduleName, NodeId);

            return new NodeRollbackResult
            {
                NodeId = NodeId,
                Hostname = Hostname,
                Success = true,
                Message = $"Successfully rolled back {moduleName}",
                Timestamp = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rollback module {ModuleName} on node {NodeId}",
                moduleName, NodeId);

            return new NodeRollbackResult
            {
                NodeId = NodeId,
                Hostname = Hostname,
                Success = false,
                Message = $"Rollback failed: {ex.Message}",
                Exception = ex,
                Timestamp = DateTime.UtcNow
            };
        }
    }

    /// <summary>
    /// Gets current health status of the node.
    /// </summary>
    public async Task<NodeHealth> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        // Simulate health check
        await Task.Delay(50, cancellationToken);

        var health = new NodeHealth
        {
            NodeId = NodeId,
            Status = Status,
            LastHeartbeat = LastHeartbeat
        };

        health.EvaluateHealth();

        return health;
    }

    /// <summary>
    /// Pings the node to check if it's responsive.
    /// </summary>
    public async Task<bool> PingAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeout);

            // Simulate ping
            await Task.Delay(10, cts.Token);

            UpdateHeartbeat();
            return true;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Ping timeout for node {NodeId}", NodeId);
            return false;
        }
    }

    /// <summary>
    /// Updates the last heartbeat timestamp (thread-safe).
    /// </summary>
    internal void UpdateHeartbeat()
    {
        lock (_heartbeatLock)
        {
            _lastHeartbeat = DateTime.UtcNow;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        await _disposeLock.WaitAsync();
        try
        {
            if (_disposed)
                return;

            _logger.LogInformation("Disposing kernel node {NodeId}", NodeId);

            Status = NodeStatus.Stopping;

            // Cleanup resources
            await Task.Delay(100);

            Status = NodeStatus.Stopped;
            _disposed = true;

            _logger.LogInformation("Kernel node {NodeId} disposed", NodeId);
        }
        finally
        {
            _disposeLock.Release();
            _disposeLock.Dispose();
        }
    }
}
