using System.Collections.Concurrent;
using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Deployments;
using HotSwap.Distributed.Infrastructure.Interfaces;
using HotSwap.Distributed.Infrastructure.Metrics;
using HotSwap.Distributed.Infrastructure.Security;
using HotSwap.Distributed.Infrastructure.Telemetry;
using HotSwap.Distributed.Orchestrator.Interfaces;
using HotSwap.Distributed.Orchestrator.Pipeline;
using HotSwap.Distributed.Orchestrator.Strategies;
using Microsoft.Extensions.Logging;

namespace HotSwap.Distributed.Orchestrator.Core;

/// <summary>
/// Central orchestrator for the distributed kernel system.
/// Manages clusters, coordinates deployments, and provides cluster registry.
/// </summary>
public class DistributedKernelOrchestrator : IClusterRegistry, IAsyncDisposable
{
    private readonly ILogger<DistributedKernelOrchestrator> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IMetricsProvider _metricsProvider;
    private readonly IModuleVerifier _moduleVerifier;
    private readonly TelemetryProvider _telemetry;
    private readonly PipelineConfiguration _pipelineConfig;
    private readonly IDeploymentTracker? _deploymentTracker;
    private readonly IApprovalService? _approvalService;
    private readonly IAuditLogService? _auditLogService;
    private readonly ConcurrentDictionary<EnvironmentType, EnvironmentCluster> _clusters;
    private readonly Dictionary<EnvironmentType, IDeploymentStrategy> _strategies;
    private DeploymentPipeline? _pipeline;
    private bool _initialized;
    private bool _disposed;

    public DistributedKernelOrchestrator(
        ILogger<DistributedKernelOrchestrator> logger,
        ILoggerFactory loggerFactory,
        IMetricsProvider? metricsProvider = null,
        IModuleVerifier? moduleVerifier = null,
        TelemetryProvider? telemetry = null,
        PipelineConfiguration? pipelineConfig = null,
        IDeploymentTracker? deploymentTracker = null,
        IApprovalService? approvalService = null,
        IAuditLogService? auditLogService = null)
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
        _metricsProvider = metricsProvider ?? new InMemoryMetricsProvider(
            _loggerFactory.CreateLogger<InMemoryMetricsProvider>());
        _moduleVerifier = moduleVerifier ?? new ModuleVerifier(
            _loggerFactory.CreateLogger<ModuleVerifier>());
        _telemetry = telemetry ?? new TelemetryProvider();
        _pipelineConfig = pipelineConfig ?? new PipelineConfiguration();
        _deploymentTracker = deploymentTracker;
        _approvalService = approvalService;
        _auditLogService = auditLogService;
        _clusters = new ConcurrentDictionary<EnvironmentType, EnvironmentCluster>();
        _strategies = new Dictionary<EnvironmentType, IDeploymentStrategy>();

        _logger.LogInformation("Distributed Kernel Orchestrator created");
    }

    /// <summary>
    /// Initializes all environment clusters with nodes.
    /// </summary>
    public async Task InitializeClustersAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
        {
            _logger.LogWarning("Orchestrator already initialized");
            return;
        }

        _logger.LogInformation("Initializing clusters for all environments");

        try
        {
            // Create clusters for each environment
            foreach (var environment in Enum.GetValues<EnvironmentType>())
            {
                var cluster = new EnvironmentCluster(
                    environment,
                    _loggerFactory.CreateLogger<EnvironmentCluster>());

                _clusters[environment] = cluster;

                // Add sample nodes to each cluster
                var nodeCount = environment switch
                {
                    EnvironmentType.Development => 3,
                    EnvironmentType.QA => 5,
                    EnvironmentType.Staging => 10,
                    EnvironmentType.Production => 20,
                    _ => 1
                };

                for (int i = 0; i < nodeCount; i++)
                {
                    var nodeConfig = new NodeConfiguration
                    {
                        Hostname = $"{environment.ToString().ToLower()}-node-{i + 1:D2}",
                        Port = 8080 + i,
                        Environment = environment
                    };

                    var node = await KernelNode.CreateAsync(
                        nodeConfig,
                        _loggerFactory.CreateLogger<KernelNode>(),
                        cancellationToken);

                    cluster.AddNode(node);
                }

                _logger.LogInformation("Initialized {Environment} cluster with {NodeCount} nodes",
                    environment, nodeCount);
            }

            // Initialize deployment strategies
            _strategies[EnvironmentType.Development] = new DirectDeploymentStrategy(
                _loggerFactory.CreateLogger<DirectDeploymentStrategy>());

            _strategies[EnvironmentType.QA] = new RollingDeploymentStrategy(
                _loggerFactory.CreateLogger<RollingDeploymentStrategy>(),
                maxConcurrent: _pipelineConfig.QaMaxConcurrentNodes);

            _strategies[EnvironmentType.Staging] = new BlueGreenDeploymentStrategy(
                _loggerFactory.CreateLogger<BlueGreenDeploymentStrategy>(),
                smokeTestTimeout: _pipelineConfig.StagingSmokeTestTimeout);

            _strategies[EnvironmentType.Production] = new CanaryDeploymentStrategy(
                _loggerFactory.CreateLogger<CanaryDeploymentStrategy>(),
                _metricsProvider,
                initialPercentage: _pipelineConfig.CanaryInitialPercentage,
                incrementPercentage: _pipelineConfig.CanaryIncrementPercentage,
                waitDuration: _pipelineConfig.CanaryWaitDuration);

            // Initialize deployment pipeline
            _pipeline = new DeploymentPipeline(
                _loggerFactory.CreateLogger<DeploymentPipeline>(),
                this,
                _moduleVerifier,
                _telemetry,
                _pipelineConfig,
                _strategies,
                approvalService: _approvalService,
                auditLogService: _auditLogService,
                deploymentTracker: _deploymentTracker);

            _initialized = true;

            _logger.LogInformation("Orchestrator initialization completed. Total nodes: {TotalNodes}",
                _clusters.Values.Sum(c => c.NodeCount));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize orchestrator");
            throw;
        }
    }

    /// <summary>
    /// Executes a full deployment pipeline for a module.
    /// </summary>
    public async Task<PipelineExecutionResult> ExecuteDeploymentPipelineAsync(
        DeploymentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("Orchestrator not initialized. Call InitializeClustersAsync first.");
        }

        if (_pipeline == null)
        {
            throw new InvalidOperationException("Pipeline not initialized");
        }

        _logger.LogInformation("Executing deployment pipeline for {ModuleName} v{Version} to {Environment}",
            request.Module.Name, request.Module.Version, request.TargetEnvironment);

        try
        {
            var result = await _pipeline.ExecutePipelineAsync(request, cancellationToken);

            _logger.LogInformation("Deployment pipeline completed for {ModuleName}: {Success}",
                request.Module.Name, result.Success ? "SUCCESS" : "FAILED");

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Deployment pipeline failed for {ModuleName}", request.Module.Name);
            throw;
        }
    }

    /// <summary>
    /// Gets the health status of a specific cluster.
    /// </summary>
    public async Task<ClusterHealth> GetClusterHealthAsync(
        EnvironmentType environment,
        CancellationToken cancellationToken = default)
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("Orchestrator not initialized");
        }

        var cluster = GetCluster(environment);
        return await cluster.GetHealthAsync(cancellationToken);
    }

    /// <summary>
    /// Rolls back a deployment to previous version.
    /// </summary>
    public async Task RollbackDeploymentAsync(
        DeploymentRequest originalRequest,
        CancellationToken cancellationToken = default)
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("Orchestrator not initialized");
        }

        using var activity = _telemetry.StartRollbackActivity(
            originalRequest.Module.Name,
            originalRequest.TargetEnvironment);

        var startTime = DateTime.UtcNow;
        var executionId = originalRequest.ExecutionId;

        try
        {
            _logger.LogInformation("Rolling back {ModuleName} in {Environment}",
                originalRequest.Module.Name, originalRequest.TargetEnvironment);

            var cluster = GetCluster(originalRequest.TargetEnvironment);
            var nodes = cluster.Nodes.ToList();

            var rollbackTasks = nodes.Select(node =>
                node.RollbackModuleAsync(originalRequest.Module.Name, cancellationToken));

            var results = await Task.WhenAll(rollbackTasks);

            var successCount = results.Count(r => r.Success);
            var failedCount = nodes.Count - successCount;
            var endTime = DateTime.UtcNow;
            var duration = endTime - startTime;

            _telemetry.RecordRollback(
                activity,
                originalRequest.Module.Name,
                originalRequest.TargetEnvironment,
                nodes.Count,
                successCount == nodes.Count);

            // Log rollback audit event if audit service is available
            if (_auditLogService != null)
            {
                var auditLog = new Infrastructure.Data.Entities.AuditLog
                {
                    EventType = "RollbackCompleted",
                    EventCategory = "Deployment",
                    Severity = successCount == nodes.Count ? "Info" : "Warning",
                    Action = "Rollback",
                    Result = successCount == nodes.Count ? "Success" : "PartialSuccess",
                    Message = $"Rollback completed for {originalRequest.Module.Name} v{originalRequest.Module.Version} in {originalRequest.TargetEnvironment}: {successCount}/{nodes.Count} nodes",
                    ResourceType = "Module",
                    ResourceId = $"{originalRequest.Module.Name}:{originalRequest.Module.Version}",
                    UserEmail = originalRequest.RequesterEmail,
                    TraceId = activity?.Id,
                    Metadata = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        moduleName = originalRequest.Module.Name,
                        moduleVersion = originalRequest.Module.Version,
                        environment = originalRequest.TargetEnvironment.ToString(),
                        nodesTargeted = nodes.Count,
                        nodesSuccessful = successCount,
                        nodesFailed = failedCount,
                        durationMs = (int)duration.TotalMilliseconds
                    })
                };

                var deploymentEvent = new Infrastructure.Data.Entities.DeploymentAuditEvent
                {
                    DeploymentExecutionId = executionId,
                    ModuleName = originalRequest.Module.Name,
                    ModuleVersion = originalRequest.Module.Version.ToString(),
                    TargetEnvironment = originalRequest.TargetEnvironment.ToString(),
                    DeploymentStrategy = "Rollback",
                    PipelineStage = "Rollback",
                    StageStatus = successCount == nodes.Count ? "Succeeded" : "PartiallySucceeded",
                    NodesTargeted = nodes.Count,
                    NodesDeployed = successCount,
                    NodesFailed = failedCount,
                    StartTime = startTime,
                    EndTime = endTime,
                    DurationMs = (int)duration.TotalMilliseconds,
                    RequesterEmail = originalRequest.RequesterEmail
                };

                try
                {
                    await _auditLogService.LogDeploymentEventAsync(auditLog, deploymentEvent, cancellationToken);
                }
                catch (Exception auditEx)
                {
                    _logger.LogError(auditEx, "Failed to log rollback audit event for {ModuleName}", originalRequest.Module.Name);
                    // Don't throw - audit logging failure shouldn't fail the rollback
                }
            }

            _logger.LogInformation("Rollback completed: {Success}/{Total} nodes",
                successCount, nodes.Count);
        }
        catch (Exception ex)
        {
            var endTime = DateTime.UtcNow;
            var duration = endTime - startTime;

            // Log rollback failure audit event if audit service is available
            if (_auditLogService != null)
            {
                var auditLog = new Infrastructure.Data.Entities.AuditLog
                {
                    EventType = "RollbackFailed",
                    EventCategory = "Deployment",
                    Severity = "Error",
                    Action = "Rollback",
                    Result = "Failure",
                    Message = $"Rollback failed for {originalRequest.Module.Name} v{originalRequest.Module.Version} in {originalRequest.TargetEnvironment}: {ex.Message}",
                    ResourceType = "Module",
                    ResourceId = $"{originalRequest.Module.Name}:{originalRequest.Module.Version}",
                    UserEmail = originalRequest.RequesterEmail,
                    TraceId = activity?.Id,
                    Metadata = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        moduleName = originalRequest.Module.Name,
                        moduleVersion = originalRequest.Module.Version,
                        environment = originalRequest.TargetEnvironment.ToString(),
                        errorMessage = ex.Message,
                        durationMs = (int)duration.TotalMilliseconds
                    })
                };

                var deploymentEvent = new Infrastructure.Data.Entities.DeploymentAuditEvent
                {
                    DeploymentExecutionId = executionId,
                    ModuleName = originalRequest.Module.Name,
                    ModuleVersion = originalRequest.Module.Version.ToString(),
                    TargetEnvironment = originalRequest.TargetEnvironment.ToString(),
                    DeploymentStrategy = "Rollback",
                    PipelineStage = "Rollback",
                    StageStatus = "Failed",
                    StartTime = startTime,
                    EndTime = endTime,
                    DurationMs = (int)duration.TotalMilliseconds,
                    ErrorMessage = ex.Message,
                    ExceptionDetails = ex.ToString(),
                    RequesterEmail = originalRequest.RequesterEmail
                };

                try
                {
                    await _auditLogService.LogDeploymentEventAsync(auditLog, deploymentEvent, cancellationToken);
                }
                catch (Exception auditEx)
                {
                    _logger.LogError(auditEx, "Failed to log rollback failure audit event for {ModuleName}", originalRequest.Module.Name);
                    // Don't throw - audit logging failure shouldn't fail the rollback
                }
            }

            _logger.LogError(ex, "Rollback failed for {ModuleName}", originalRequest.Module.Name);
            throw;
        }
    }

    #region IClusterRegistry Implementation

    public EnvironmentCluster GetCluster(EnvironmentType environment)
    {
        if (!_clusters.TryGetValue(environment, out var cluster))
        {
            throw new InvalidOperationException($"Cluster for environment {environment} not found");
        }

        return cluster;
    }

    public Task<EnvironmentCluster> GetClusterAsync(
        EnvironmentType environment,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(GetCluster(environment));
    }

    public IReadOnlyDictionary<EnvironmentType, EnvironmentCluster> GetAllClusters()
    {
        return _clusters;
    }

    #endregion

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _logger.LogInformation("Disposing Distributed Kernel Orchestrator");

        // Dispose pipeline
        _pipeline?.Dispose();

        // Dispose all clusters
        var disposeTasks = _clusters.Values.Select(c => c.DisposeAsync().AsTask());
        await Task.WhenAll(disposeTasks);

        // Don't clear clusters dictionary - allow in-flight deployments to complete gracefully
        // Clusters are already disposed individually above

        // Dispose telemetry
        _telemetry.Dispose();

        _disposed = true;

        _logger.LogInformation("Orchestrator disposed");
    }
}
