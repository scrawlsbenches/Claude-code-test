using System.Net.Http.Json;
using System.Text.Json;
using HotSwap.Examples.ApiUsage.Models;
using Microsoft.Extensions.Logging;

namespace HotSwap.Examples.ApiUsage.Client;

/// <summary>
/// Client for interacting with the Distributed Kernel Orchestration API.
/// Provides comprehensive access to all API endpoints with proper error handling and retry logic.
/// </summary>
public class DistributedKernelApiClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<DistributedKernelApiClient> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private const int MaxRetries = 3;
    private const int RetryDelayMs = 1000;

    /// <summary>
    /// Initializes a new instance of the API client.
    /// </summary>
    /// <param name="httpClient">HTTP client for making requests</param>
    /// <param name="logger">Logger for diagnostics</param>
    public DistributedKernelApiClient(HttpClient httpClient, ILogger<DistributedKernelApiClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };
    }

    #region Deployment Endpoints

    /// <summary>
    /// Creates and executes a new deployment.
    /// </summary>
    /// <param name="request">Deployment request details</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Deployment response with execution ID and status</returns>
    public async Task<DeploymentResponse> CreateDeploymentAsync(
        CreateDeploymentRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Creating deployment for {ModuleName} v{Version} to {Environment}",
            request.ModuleName,
            request.Version,
            request.TargetEnvironment);

        return await ExecuteWithRetryAsync(async () =>
        {
            var response = await _httpClient.PostAsJsonAsync(
                "api/v1/deployments",
                request,
                _jsonOptions,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var deployment = await response.Content.ReadFromJsonAsync<DeploymentResponse>(
                _jsonOptions,
                cancellationToken);

            if (deployment == null)
                throw new InvalidOperationException("Failed to deserialize deployment response");

            _logger.LogInformation(
                "Deployment created successfully. ExecutionId: {ExecutionId}",
                deployment.ExecutionId);

            return deployment;
        });
    }

    /// <summary>
    /// Gets the status of a specific deployment.
    /// </summary>
    /// <param name="executionId">Deployment execution ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Deployment status and results</returns>
    public async Task<DeploymentStatusResponse> GetDeploymentStatusAsync(
        Guid executionId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting deployment status for {ExecutionId}", executionId);

        return await ExecuteWithRetryAsync(async () =>
        {
            var response = await _httpClient.GetAsync(
                $"api/v1/deployments/{executionId}",
                cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                throw new InvalidOperationException($"Deployment {executionId} not found");
            }

            response.EnsureSuccessStatusCode();

            var status = await response.Content.ReadFromJsonAsync<DeploymentStatusResponse>(
                _jsonOptions,
                cancellationToken);

            if (status == null)
                throw new InvalidOperationException("Failed to deserialize deployment status");

            return status;
        });
    }

    /// <summary>
    /// Lists recent deployments.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of recent deployments</returns>
    public async Task<List<DeploymentSummary>> ListDeploymentsAsync(
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Listing recent deployments");

        return await ExecuteWithRetryAsync(async () =>
        {
            var response = await _httpClient.GetAsync(
                "api/v1/deployments",
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var deployments = await response.Content.ReadFromJsonAsync<List<DeploymentSummary>>(
                _jsonOptions,
                cancellationToken);

            return deployments ?? new List<DeploymentSummary>();
        });
    }

    /// <summary>
    /// Rolls back a deployment to the previous version.
    /// </summary>
    /// <param name="executionId">Deployment execution ID to rollback</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Rollback response with status</returns>
    public async Task<RollbackResponse> RollbackDeploymentAsync(
        Guid executionId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Rolling back deployment {ExecutionId}", executionId);

        return await ExecuteWithRetryAsync(async () =>
        {
            var response = await _httpClient.PostAsync(
                $"api/v1/deployments/{executionId}/rollback",
                null,
                cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                throw new InvalidOperationException($"Deployment {executionId} not found");
            }

            response.EnsureSuccessStatusCode();

            var rollback = await response.Content.ReadFromJsonAsync<RollbackResponse>(
                _jsonOptions,
                cancellationToken);

            if (rollback == null)
                throw new InvalidOperationException("Failed to deserialize rollback response");

            _logger.LogInformation(
                "Rollback initiated successfully. RollbackId: {RollbackId}",
                rollback.RollbackId);

            return rollback;
        });
    }

    #endregion

    #region Cluster Endpoints

    /// <summary>
    /// Lists all available clusters.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of cluster summaries</returns>
    public async Task<List<ClusterSummary>> ListClustersAsync(
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Listing all clusters");

        return await ExecuteWithRetryAsync(async () =>
        {
            var response = await _httpClient.GetAsync(
                "api/v1/clusters",
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var clusters = await response.Content.ReadFromJsonAsync<List<ClusterSummary>>(
                _jsonOptions,
                cancellationToken);

            return clusters ?? new List<ClusterSummary>();
        });
    }

    /// <summary>
    /// Gets detailed information about a specific cluster.
    /// </summary>
    /// <param name="environment">Environment name (Development, QA, Staging, Production)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Cluster information including health and nodes</returns>
    public async Task<ClusterInfoResponse> GetClusterInfoAsync(
        string environment,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting cluster info for {Environment}", environment);

        return await ExecuteWithRetryAsync(async () =>
        {
            var response = await _httpClient.GetAsync(
                $"api/v1/clusters/{environment}",
                cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                throw new InvalidOperationException($"Cluster '{environment}' not found");
            }

            response.EnsureSuccessStatusCode();

            var clusterInfo = await response.Content.ReadFromJsonAsync<ClusterInfoResponse>(
                _jsonOptions,
                cancellationToken);

            if (clusterInfo == null)
                throw new InvalidOperationException("Failed to deserialize cluster info");

            return clusterInfo;
        });
    }

    /// <summary>
    /// Gets time-series metrics for a specific cluster.
    /// </summary>
    /// <param name="environment">Environment name</param>
    /// <param name="from">Start timestamp (optional)</param>
    /// <param name="to">End timestamp (optional)</param>
    /// <param name="interval">Interval (1m, 5m, 15m, 1h)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Time-series metrics data</returns>
    public async Task<ClusterMetricsTimeSeriesResponse> GetClusterMetricsAsync(
        string environment,
        DateTime? from = null,
        DateTime? to = null,
        string interval = "5m",
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Getting cluster metrics for {Environment} with interval {Interval}",
            environment,
            interval);

        return await ExecuteWithRetryAsync(async () =>
        {
            var queryParams = new List<string>();

            if (from.HasValue)
                queryParams.Add($"from={from.Value:O}");

            if (to.HasValue)
                queryParams.Add($"to={to.Value:O}");

            queryParams.Add($"interval={interval}");

            var queryString = string.Join("&", queryParams);
            var url = $"api/v1/clusters/{environment}/metrics?{queryString}";

            var response = await _httpClient.GetAsync(url, cancellationToken);

            response.EnsureSuccessStatusCode();

            var metrics = await response.Content.ReadFromJsonAsync<ClusterMetricsTimeSeriesResponse>(
                _jsonOptions,
                cancellationToken);

            if (metrics == null)
                throw new InvalidOperationException("Failed to deserialize cluster metrics");

            return metrics;
        });
    }

    #endregion

    #region Health Check

    /// <summary>
    /// Checks if the API is healthy and responsive.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if healthy, false otherwise</returns>
    public async Task<bool> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Checking API health");

            var response = await _httpClient.GetAsync("health", cancellationToken);

            var isHealthy = response.IsSuccessStatusCode;

            _logger.LogInformation("API health check: {Status}",
                isHealthy ? "Healthy" : "Unhealthy");

            return isHealthy;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed");
            return false;
        }
    }

    #endregion

    #region Retry Logic

    /// <summary>
    /// Executes an async operation with retry logic for transient failures.
    /// </summary>
    private async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> operation)
    {
        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (HttpRequestException ex) when (attempt < MaxRetries)
            {
                _logger.LogWarning(
                    ex,
                    "Request failed (attempt {Attempt}/{MaxRetries}). Retrying in {Delay}ms...",
                    attempt,
                    MaxRetries,
                    RetryDelayMs);

                await Task.Delay(RetryDelayMs * attempt);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Request failed on attempt {Attempt}/{MaxRetries}",
                    attempt, MaxRetries);
                throw;
            }
        }

        throw new InvalidOperationException("Should not reach here");
    }

    #endregion

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}
