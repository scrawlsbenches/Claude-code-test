using HotSwap.Distributed.Api.Models;
using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Infrastructure.Interfaces;
using HotSwap.Distributed.Orchestrator.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotSwap.Distributed.Api.Controllers;

/// <summary>
/// API endpoints for cluster management and monitoring.
/// Available to all authenticated users (read-only operations).
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
[Authorize(Roles = "Viewer,Deployer,Admin")] // All endpoints are read-only, available to all authenticated users
public class ClustersController : ControllerBase
{
    private readonly DistributedKernelOrchestrator _orchestrator;
    private readonly IMetricsProvider _metricsProvider;
    private readonly ILogger<ClustersController> _logger;

    public ClustersController(
        DistributedKernelOrchestrator orchestrator,
        IMetricsProvider metricsProvider,
        ILogger<ClustersController> logger)
    {
        _orchestrator = orchestrator;
        _metricsProvider = metricsProvider;
        _logger = logger;
    }

    /// <summary>
    /// Gets cluster information and health status.
    /// </summary>
    /// <param name="environment">Environment name (Development, QA, Staging, Production)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Cluster health and node information</returns>
    /// <response code="200">Cluster information retrieved</response>
    /// <response code="404">Cluster not found</response>
    [HttpGet("{environment}")]
    [ProducesResponseType(typeof(ClusterInfoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCluster(
        string environment,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Getting cluster info for {Environment}", environment);

            if (!Enum.TryParse<EnvironmentType>(environment, true, out var envType))
            {
                return NotFound(new ErrorResponse
                {
                    Error = $"Environment '{environment}' not found"
                });
            }

            var cluster = _orchestrator.GetCluster(envType);
            var health = await _orchestrator.GetClusterHealthAsync(envType, cancellationToken);
            var metrics = await _metricsProvider.GetClusterMetricsAsync(envType, cancellationToken);

            var response = new ClusterInfoResponse
            {
                Environment = environment,
                TotalNodes = health.TotalNodes,
                HealthyNodes = health.HealthyNodes,
                UnhealthyNodes = health.UnhealthyNodes,
                Metrics = new ClusterMetrics
                {
                    AvgCpuUsage = metrics.AvgCpuUsage,
                    AvgMemoryUsage = metrics.AvgMemoryUsage,
                    AvgLatency = metrics.AvgLatency,
                    ErrorRate = metrics.AvgErrorRate,
                    RequestsPerSecond = metrics.TotalRequestsPerSecond
                },
                Nodes = cluster.Nodes.Select(n => new NodeSummary
                {
                    NodeId = n.NodeId,
                    Hostname = n.Hostname,
                    Status = n.Status.ToString(),
                    LastHeartbeat = n.LastHeartbeat
                }).ToList()
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get cluster info");
            return StatusCode(500, new ErrorResponse
            {
                Error = "Failed to get cluster info",
                Details = ex.Message
            });
        }
    }

    /// <summary>
    /// Gets detailed cluster metrics over time.
    /// </summary>
    /// <param name="environment">Environment name</param>
    /// <param name="from">Start timestamp (ISO 8601)</param>
    /// <param name="to">End timestamp (ISO 8601)</param>
    /// <param name="interval">Interval (1m, 5m, 15m, 1h)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Time-series metrics data</returns>
    /// <response code="200">Metrics retrieved</response>
    /// <response code="400">Invalid parameters</response>
    [HttpGet("{environment}/metrics")]
    [ProducesResponseType(typeof(ClusterMetricsTimeSeriesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetClusterMetrics(
        string environment,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string interval = "5m",
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!Enum.TryParse<EnvironmentType>(environment, true, out var envType))
            {
                return BadRequest(new ErrorResponse
                {
                    Error = $"Invalid environment: {environment}"
                });
            }

            var startTime = from ?? DateTime.UtcNow.AddHours(-1);
            var endTime = to ?? DateTime.UtcNow;

            _logger.LogInformation("Getting cluster metrics for {Environment} from {From} to {To}",
                environment, startTime, endTime);

            var currentMetrics = await _metricsProvider.GetClusterMetricsAsync(
                envType,
                cancellationToken);

            var response = new ClusterMetricsTimeSeriesResponse
            {
                Environment = environment,
                Interval = interval,
                DataPoints = new List<MetricsDataPoint>
                {
                    new MetricsDataPoint
                    {
                        Timestamp = currentMetrics.Timestamp,
                        CpuUsage = currentMetrics.AvgCpuUsage,
                        MemoryUsage = currentMetrics.AvgMemoryUsage,
                        Latency = currentMetrics.AvgLatency,
                        ErrorRate = currentMetrics.AvgErrorRate
                    }
                }
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get cluster metrics");
            return StatusCode(500, new ErrorResponse
            {
                Error = "Failed to get cluster metrics",
                Details = ex.Message
            });
        }
    }

    /// <summary>
    /// Lists all available clusters.
    /// </summary>
    /// <returns>List of all clusters</returns>
    /// <response code="200">Clusters retrieved</response>
    [HttpGet]
    [ProducesResponseType(typeof(List<ClusterSummary>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListClusters()
    {
        try
        {
            var clusters = _orchestrator.GetAllClusters();

            var summaries = new List<ClusterSummary>();

            foreach (var kvp in clusters)
            {
                var health = await _orchestrator.GetClusterHealthAsync(kvp.Key);

                summaries.Add(new ClusterSummary
                {
                    Environment = kvp.Key.ToString(),
                    TotalNodes = health.TotalNodes,
                    HealthyNodes = health.HealthyNodes,
                    IsHealthy = health.IsHealthy
                });
            }

            return Ok(summaries);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list clusters");
            return StatusCode(500, new ErrorResponse
            {
                Error = "Failed to list clusters",
                Details = ex.Message
            });
        }
    }
}
