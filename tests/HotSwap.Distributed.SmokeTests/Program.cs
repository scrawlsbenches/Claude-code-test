using System.Net.Http.Json;
using System.Text.Json;

namespace HotSwap.Distributed.SmokeTests;

/// <summary>
/// Smoke tests for the Distributed Kernel Orchestration API.
/// These tests verify that the API is up and responding correctly.
///
/// Smoke tests are designed to:
/// - Run quickly (< 60 seconds total)
/// - Test critical paths only
/// - Fail fast on errors
/// - Be suitable for CI/CD pipelines
/// </summary>
class Program
{
    private static HttpClient? _httpClient;
    private static int _passedTests = 0;
    private static int _failedTests = 0;
    private static readonly List<string> _errors = new();

    static async Task<int> Main(string[] args)
    {
        var apiBaseUrl = args.Length > 0 ? args[0] : "http://localhost:5000";
        var startTime = DateTime.UtcNow;

        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║   Distributed Kernel Orchestration API - Smoke Tests        ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
        Console.WriteLine();
        Console.WriteLine($"API URL:    {apiBaseUrl}");
        Console.WriteLine($"Started:    {startTime:yyyy-MM-dd HH:mm:ss UTC}");
        Console.WriteLine();

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(apiBaseUrl),
            Timeout = TimeSpan.FromSeconds(30)
        };

        try
        {
            // Critical Path Tests
            await RunTest("Health Check", Test_HealthCheck);
            await RunTest("List Clusters", Test_ListClusters);
            await RunTest("Get Cluster Info", Test_GetClusterInfo);
            await RunTest("Create Deployment", Test_CreateDeployment);
            await RunTest("Get Deployment Status", Test_GetDeploymentStatus);
            await RunTest("List Deployments", Test_ListDeployments);

            // Print results
            Console.WriteLine();
            Console.WriteLine("═══════════════════════════════════════════════════════════════");
            Console.WriteLine("                       TEST RESULTS");
            Console.WriteLine("═══════════════════════════════════════════════════════════════");
            Console.WriteLine($"✅ Passed:  {_passedTests}");
            Console.WriteLine($"❌ Failed:  {_failedTests}");
            Console.WriteLine($"⏱️  Duration: {(DateTime.UtcNow - startTime).TotalSeconds:F2}s");
            Console.WriteLine();

            if (_errors.Any())
            {
                Console.WriteLine("Errors:");
                foreach (var error in _errors)
                {
                    Console.WriteLine($"  • {error}");
                }
                Console.WriteLine();
            }

            return _failedTests == 0 ? 0 : 1;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Fatal error: {ex.Message}");
            return 1;
        }
        finally
        {
            _httpClient?.Dispose();
        }
    }

    private static async Task RunTest(string name, Func<Task> test)
    {
        Console.Write($"Running: {name,-30} ... ");
        try
        {
            await test();
            _passedTests++;
            Console.WriteLine("✅ PASS");
        }
        catch (Exception ex)
        {
            _failedTests++;
            Console.WriteLine("❌ FAIL");
            _errors.Add($"{name}: {ex.Message}");
        }
    }

    #region Test Cases

    private static async Task Test_HealthCheck()
    {
        var response = await _httpClient!.GetAsync("/health");
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();

        if (string.IsNullOrWhiteSpace(content))
            throw new Exception("Health check returned empty response");
    }

    private static async Task Test_ListClusters()
    {
        var response = await _httpClient!.GetAsync("/api/v1/clusters");
        response.EnsureSuccessStatusCode();

        var clusters = await response.Content.ReadFromJsonAsync<List<ClusterSummary>>();

        if (clusters == null || clusters.Count == 0)
            throw new Exception("No clusters returned");

        // Verify expected environments
        var environments = clusters.Select(c => c.Environment).ToHashSet();
        var expected = new[] { "Development", "QA", "Staging", "Production" };

        foreach (var env in expected)
        {
            if (!environments.Contains(env))
                throw new Exception($"Missing expected environment: {env}");
        }
    }

    private static async Task Test_GetClusterInfo()
    {
        var response = await _httpClient!.GetAsync("/api/v1/clusters/Production");
        response.EnsureSuccessStatusCode();

        var clusterInfo = await response.Content.ReadFromJsonAsync<ClusterInfo>();

        if (clusterInfo == null)
            throw new Exception("Cluster info was null");

        if (clusterInfo.Environment != "Production")
            throw new Exception($"Expected Production, got {clusterInfo.Environment}");

        if (clusterInfo.TotalNodes == 0)
            throw new Exception("Production cluster has no nodes");
    }

    private static Guid? _testDeploymentId;

    private static async Task Test_CreateDeployment()
    {
        var request = new
        {
            moduleName = "smoke-test-module",
            version = "1.0.0",
            targetEnvironment = "Development",
            requesterEmail = "smoke-test@example.com",
            description = "Smoke test deployment",
            requireApproval = false
        };

        var response = await _httpClient!.PostAsJsonAsync("/api/v1/deployments", request);
        response.EnsureSuccessStatusCode();

        var deployment = await response.Content.ReadFromJsonAsync<DeploymentResponse>();

        if (deployment == null)
            throw new Exception("Deployment response was null");

        if (deployment.ExecutionId == Guid.Empty)
            throw new Exception("Deployment ID was empty");

        _testDeploymentId = deployment.ExecutionId;

        // Wait a moment for deployment to process
        await Task.Delay(2000);
    }

    private static async Task Test_GetDeploymentStatus()
    {
        if (_testDeploymentId == null)
        {
            // Try to get any deployment
            var deploymentsResponse = await _httpClient!.GetAsync("/api/v1/deployments");
            deploymentsResponse.EnsureSuccessStatusCode();
            var deployments = await deploymentsResponse.Content.ReadFromJsonAsync<List<DeploymentSummary>>();

            if (deployments == null || deployments.Count == 0)
                throw new Exception("No deployments available for status check");

            _testDeploymentId = deployments[0].ExecutionId;
        }

        var response = await _httpClient!.GetAsync($"/api/v1/deployments/{_testDeploymentId}");
        response.EnsureSuccessStatusCode();

        var status = await response.Content.ReadFromJsonAsync<DeploymentStatus>();

        if (status == null)
            throw new Exception("Deployment status was null");

        if (status.ExecutionId != _testDeploymentId)
            throw new Exception("Deployment ID mismatch");
    }

    private static async Task Test_ListDeployments()
    {
        var response = await _httpClient!.GetAsync("/api/v1/deployments");
        response.EnsureSuccessStatusCode();

        var deployments = await response.Content.ReadFromJsonAsync<List<DeploymentSummary>>();

        if (deployments == null)
            throw new Exception("Deployments list was null");

        // We just created one, so there should be at least one
        if (deployments.Count == 0)
            throw new Exception("No deployments found");
    }

    #endregion

    #region DTOs (Minimal definitions for deserialization)

    private record ClusterSummary(
        string Environment,
        int TotalNodes,
        int HealthyNodes,
        bool IsHealthy
    );

    private record ClusterInfo(
        string Environment,
        int TotalNodes,
        int HealthyNodes,
        int UnhealthyNodes,
        ClusterMetrics Metrics,
        List<NodeInfo> Nodes
    );

    private record ClusterMetrics(
        double AvgCpuUsage,
        double AvgMemoryUsage,
        double AvgLatency,
        double ErrorRate,
        double RequestsPerSecond
    );

    private record NodeInfo(
        Guid NodeId,
        string Hostname,
        string Status,
        DateTime LastHeartbeat
    );

    private record DeploymentResponse(
        Guid ExecutionId,
        string Status,
        DateTime StartTime,
        string EstimatedDuration,
        string TraceId,
        Dictionary<string, string> Links
    );

    private record DeploymentStatus(
        Guid ExecutionId,
        string ModuleName,
        string Version,
        string TargetEnvironment,
        string Status,
        string Duration,
        List<StageInfo> Stages
    );

    private record StageInfo(
        string Name,
        string Status,
        int? NodesDeployed,
        int? NodesFailed
    );

    private record DeploymentSummary(
        Guid ExecutionId,
        string ModuleName,
        string Version,
        string Status,
        DateTime StartTime,
        string Duration
    );

    #endregion
}
