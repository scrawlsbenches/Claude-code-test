using System.Net.Http.Json;
using System.Text.Json;
using HotSwap.Distributed.Api.Models;
using HotSwap.Distributed.Domain.Models;

namespace HotSwap.Distributed.IntegrationTests.Helpers;

/// <summary>
/// Helper class for making API calls in integration tests.
/// Provides convenience methods for common API operations.
/// </summary>
public class ApiClientHelper
{
    private readonly HttpClient _client;

    public ApiClientHelper(HttpClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    /// <summary>
    /// Creates a deployment and returns the deployment result.
    /// </summary>
    public async Task<DeploymentResult> CreateDeploymentAsync(DeploymentRequest request)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/deployments", request);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<DeploymentResult>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (result == null)
        {
            throw new InvalidOperationException("Failed to deserialize deployment result");
        }

        return result;
    }

    /// <summary>
    /// Gets the status of a deployment by execution ID.
    /// </summary>
    public async Task<DeploymentResult?> GetDeploymentStatusAsync(string executionId)
    {
        var response = await _client.GetAsync($"/api/v1/deployments/{executionId}");

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<DeploymentResult>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        return result;
    }

    /// <summary>
    /// Waits for a deployment to complete (succeed or fail).
    /// Polls the API until the deployment is no longer in progress.
    /// </summary>
    /// <param name="executionId">The deployment execution ID</param>
    /// <param name="timeout">Maximum time to wait (default: 2 minutes)</param>
    /// <param name="pollInterval">How often to check status (default: 1 second)</param>
    /// <returns>The final deployment result</returns>
    public async Task<DeploymentResult> WaitForDeploymentCompletionAsync(
        string executionId,
        TimeSpan? timeout = null,
        TimeSpan? pollInterval = null)
    {
        timeout ??= TimeSpan.FromMinutes(2);
        pollInterval ??= TimeSpan.FromSeconds(1);

        var startTime = DateTime.UtcNow;

        while (DateTime.UtcNow - startTime < timeout)
        {
            var result = await GetDeploymentStatusAsync(executionId);

            if (result == null)
            {
                throw new InvalidOperationException($"Deployment {executionId} not found");
            }

            // Check if deployment is complete (not in progress)
            if (result.IsComplete || result.Success || result.Failed)
            {
                return result;
            }

            await Task.Delay(pollInterval.Value);
        }

        throw new TimeoutException($"Deployment {executionId} did not complete within {timeout.Value.TotalSeconds} seconds");
    }

    /// <summary>
    /// Gets the list of all deployments.
    /// </summary>
    public async Task<List<DeploymentResult>> ListDeploymentsAsync()
    {
        var response = await _client.GetAsync("/api/v1/deployments");
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        var results = JsonSerializer.Deserialize<List<DeploymentResult>>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        return results ?? new List<DeploymentResult>();
    }

    /// <summary>
    /// Gets the list of all clusters.
    /// </summary>
    public async Task<List<ClusterInfoResponse>> ListClustersAsync()
    {
        var response = await _client.GetAsync("/api/v1/clusters");
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        var clusters = JsonSerializer.Deserialize<List<ClusterInfoResponse>>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        return clusters ?? new List<ClusterInfoResponse>();
    }

    /// <summary>
    /// Gets information about a specific cluster.
    /// </summary>
    public async Task<ClusterInfoResponse?> GetClusterInfoAsync(string environment)
    {
        var response = await _client.GetAsync($"/api/v1/clusters/{environment}");

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        var clusterInfo = JsonSerializer.Deserialize<ClusterInfoResponse>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        return clusterInfo;
    }

    /// <summary>
    /// Approves a pending deployment.
    /// </summary>
    public async Task<HttpResponseMessage> ApproveDeploymentAsync(string executionId, string reason = "Integration test approval")
    {
        var request = new { reason };
        var response = await _client.PostAsJsonAsync($"/api/v1/approvals/deployments/{executionId}/approve", request);
        return response;
    }

    /// <summary>
    /// Rejects a pending deployment.
    /// </summary>
    public async Task<HttpResponseMessage> RejectDeploymentAsync(string executionId, string reason = "Integration test rejection")
    {
        var request = new { reason };
        var response = await _client.PostAsJsonAsync($"/api/v1/approvals/deployments/{executionId}/reject", request);
        return response;
    }

    /// <summary>
    /// Initiates a rollback for a deployment.
    /// </summary>
    public async Task<HttpResponseMessage> RollbackDeploymentAsync(string executionId)
    {
        var response = await _client.PostAsync($"/api/v1/deployments/{executionId}/rollback", null);
        return response;
    }
}
