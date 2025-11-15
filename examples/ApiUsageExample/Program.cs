using HotSwap.Examples.ApiUsage.Client;
using HotSwap.Examples.ApiUsage.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HotSwap.Examples.ApiUsage;

/// <summary>
/// Comprehensive example demonstrating full utilization of the Distributed Kernel Orchestration API.
///
/// This example showcases:
/// - All deployment strategies (Direct, Rolling, Blue-Green, Canary)
/// - Cluster monitoring and health checks
/// - Metrics retrieval and analysis
/// - Deployment status tracking
/// - Rollback scenarios
/// - Error handling and retry logic
/// - Best practices for API integration
/// </summary>
class Program
{
    private static DistributedKernelApiClient? _apiClient;
    private static ILogger<Program>? _logger;

    static async Task<int> Main(string[] args)
    {
        // Parse command line arguments
        var apiBaseUrl = args.Length > 0 ? args[0] : "http://localhost:5000";

        // Setup dependency injection and logging
        var services = new ServiceCollection();
        ConfigureServices(services, apiBaseUrl);
        var serviceProvider = services.BuildServiceProvider();

        _logger = serviceProvider.GetRequiredService<ILogger<Program>>();
        _apiClient = serviceProvider.GetRequiredService<DistributedKernelApiClient>();

        try
        {
            DisplayWelcomeBanner(apiBaseUrl);

            // Run all examples
            await RunAllExamplesAsync();

            _logger.LogInformation("✅ All examples completed successfully!");
            return 0;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "❌ Application failed with error");
            return 1;
        }
        finally
        {
            await serviceProvider.DisposeAsync();
        }
    }

    private static void ConfigureServices(IServiceCollection services, string apiBaseUrl)
    {
        // Configure logging
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        // Configure HTTP client
        services.AddHttpClient<DistributedKernelApiClient>(client =>
        {
            client.BaseAddress = new Uri(apiBaseUrl);
            client.Timeout = TimeSpan.FromMinutes(5);
        });
    }

    private static void DisplayWelcomeBanner(string apiBaseUrl)
    {
        Console.WriteLine("╔═══════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║   Distributed Kernel Orchestration API - Comprehensive Examples  ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════════════════════╝");
        Console.WriteLine();
        Console.WriteLine($"API Base URL: {apiBaseUrl}");
        Console.WriteLine($"Started at:   {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        Console.WriteLine();
    }

    private static async Task RunAllExamplesAsync()
    {
        if (_apiClient == null || _logger == null)
            throw new InvalidOperationException("Services not initialized");

        // Example 1: Health Check
        await Example01_HealthCheckAsync();
        await PauseBetweenExamples();

        // Example 2: List All Clusters
        await Example02_ListAllClustersAsync();
        await PauseBetweenExamples();

        // Example 3: Get Detailed Cluster Information
        await Example03_GetClusterDetailsAsync();
        await PauseBetweenExamples();

        // Example 4: Monitor Cluster Metrics
        await Example04_MonitorClusterMetricsAsync();
        await PauseBetweenExamples();

        // Example 5: Development Environment - Direct Deployment
        var devDeploymentId = await Example05_DirectDeploymentAsync();
        await PauseBetweenExamples();

        // Example 6: Track Deployment Status
        if (devDeploymentId.HasValue)
        {
            await Example06_TrackDeploymentStatusAsync(devDeploymentId.Value);
            await PauseBetweenExamples();
        }

        // Example 7: QA Environment - Rolling Deployment
        await Example07_RollingDeploymentAsync();
        await PauseBetweenExamples();

        // Example 8: Staging Environment - Blue-Green Deployment
        await Example08_BlueGreenDeploymentAsync();
        await PauseBetweenExamples();

        // Example 9: Production Environment - Canary Deployment
        var prodDeploymentId = await Example09_CanaryDeploymentAsync();
        await PauseBetweenExamples();

        // Example 10: List All Deployments
        await Example10_ListAllDeploymentsAsync();
        await PauseBetweenExamples();

        // Example 11: Deployment with Metadata and Approval
        await Example11_DeploymentWithMetadataAsync();
        await PauseBetweenExamples();

        // Example 12: Rollback Scenario
        if (prodDeploymentId.HasValue)
        {
            await Example12_RollbackDeploymentAsync(prodDeploymentId.Value);
            await PauseBetweenExamples();
        }

        // Example 13: Multi-Environment Health Dashboard
        await Example13_MultiEnvironmentDashboardAsync();
        await PauseBetweenExamples();

        // Example 14: Error Handling Example
        await Example14_ErrorHandlingAsync();
    }

    #region Example 1: Health Check

    private static async Task Example01_HealthCheckAsync()
    {
        PrintExampleHeader("Example 1", "API Health Check");

        var isHealthy = await _apiClient!.CheckHealthAsync();

        Console.WriteLine($"API Status: {(isHealthy ? "✅ Healthy" : "❌ Unhealthy")}");

        if (!isHealthy)
        {
            _logger!.LogWarning("API is not healthy. Some examples may fail.");
        }
    }

    #endregion

    #region Example 2: List All Clusters

    private static async Task Example02_ListAllClustersAsync()
    {
        PrintExampleHeader("Example 2", "List All Clusters");

        var clusters = await _apiClient!.ListClustersAsync();

        Console.WriteLine($"Found {clusters.Count} cluster(s):\n");

        foreach (var cluster in clusters)
        {
            var healthIcon = cluster.IsHealthy ? "✅" : "⚠️";
            Console.WriteLine($"{healthIcon} {cluster.Environment}:");
            Console.WriteLine($"   Total Nodes:   {cluster.TotalNodes}");
            Console.WriteLine($"   Healthy Nodes: {cluster.HealthyNodes}");
            Console.WriteLine($"   Is Healthy:    {cluster.IsHealthy}");
            Console.WriteLine();
        }
    }

    #endregion

    #region Example 3: Get Detailed Cluster Information

    private static async Task Example03_GetClusterDetailsAsync()
    {
        PrintExampleHeader("Example 3", "Get Detailed Cluster Information - Production");

        var clusterInfo = await _apiClient!.GetClusterInfoAsync("Production");

        Console.WriteLine($"Environment:     {clusterInfo.Environment}");
        Console.WriteLine($"Total Nodes:     {clusterInfo.TotalNodes}");
        Console.WriteLine($"Healthy Nodes:   {clusterInfo.HealthyNodes}");
        Console.WriteLine($"Unhealthy Nodes: {clusterInfo.UnhealthyNodes}");
        Console.WriteLine();

        Console.WriteLine("Current Metrics:");
        Console.WriteLine($"  CPU Usage:    {clusterInfo.Metrics.AvgCpuUsage:F2}%");
        Console.WriteLine($"  Memory Usage: {clusterInfo.Metrics.AvgMemoryUsage:F2}%");
        Console.WriteLine($"  Avg Latency:  {clusterInfo.Metrics.AvgLatency:F2}ms");
        Console.WriteLine($"  Error Rate:   {clusterInfo.Metrics.ErrorRate:F4}%");
        Console.WriteLine($"  RPS:          {clusterInfo.Metrics.RequestsPerSecond:F2}");
        Console.WriteLine();

        Console.WriteLine($"Nodes ({clusterInfo.Nodes.Count}):");
        foreach (var node in clusterInfo.Nodes.Take(5))
        {
            var statusIcon = node.Status == "Healthy" ? "✅" : "❌";
            Console.WriteLine($"  {statusIcon} {node.Hostname}");
            Console.WriteLine($"     NodeId:        {node.NodeId}");
            Console.WriteLine($"     Status:        {node.Status}");
            Console.WriteLine($"     Last Heartbeat: {node.LastHeartbeat:yyyy-MM-dd HH:mm:ss}");
        }

        if (clusterInfo.Nodes.Count > 5)
        {
            Console.WriteLine($"  ... and {clusterInfo.Nodes.Count - 5} more nodes");
        }
    }

    #endregion

    #region Example 4: Monitor Cluster Metrics

    private static async Task Example04_MonitorClusterMetricsAsync()
    {
        PrintExampleHeader("Example 4", "Monitor Cluster Metrics Over Time");

        var environments = new[] { "Development", "QA", "Staging", "Production" };

        foreach (var env in environments)
        {
            try
            {
                var metrics = await _apiClient!.GetClusterMetricsAsync(
                    env,
                    from: DateTime.UtcNow.AddHours(-1),
                    to: DateTime.UtcNow,
                    interval: "5m"
                );

                Console.WriteLine($"📊 {metrics.Environment} - Interval: {metrics.Interval}");
                Console.WriteLine($"   Data Points: {metrics.DataPoints.Count}");

                if (metrics.DataPoints.Any())
                {
                    var latest = metrics.DataPoints.OrderByDescending(dp => dp.Timestamp).First();
                    Console.WriteLine($"   Latest Metrics ({latest.Timestamp:HH:mm:ss}):");
                    Console.WriteLine($"     CPU:    {latest.CpuUsage:F2}%");
                    Console.WriteLine($"     Memory: {latest.MemoryUsage:F2}%");
                    Console.WriteLine($"     Latency: {latest.Latency:F2}ms");
                }
                Console.WriteLine();
            }
            catch (Exception ex)
            {
                _logger!.LogWarning(ex, "Failed to get metrics for {Environment}", env);
            }
        }
    }

    #endregion

    #region Example 5: Direct Deployment (Development)

    private static async Task<Guid?> Example05_DirectDeploymentAsync()
    {
        PrintExampleHeader("Example 5", "Direct Deployment Strategy - Development Environment");

        var request = new CreateDeploymentRequest
        {
            ModuleName = "authentication-service",
            Version = "1.0.0",
            TargetEnvironment = "Development",
            RequesterEmail = "dev@example.com",
            Description = "Initial deployment of authentication service",
            RequireApproval = false,
            Metadata = new Dictionary<string, string>
            {
                ["jira-ticket"] = "AUTH-101",
                ["release-notes"] = "https://docs.example.com/releases/auth-1.0.0"
            }
        };

        Console.WriteLine("Deployment Configuration:");
        Console.WriteLine($"  Module:      {request.ModuleName} v{request.Version}");
        Console.WriteLine($"  Environment: {request.TargetEnvironment}");
        Console.WriteLine($"  Strategy:    Direct (all nodes simultaneously)");
        Console.WriteLine($"  Expected:    ~10 seconds");
        Console.WriteLine();

        var response = await _apiClient!.CreateDeploymentAsync(request);

        Console.WriteLine("✅ Deployment Accepted!");
        Console.WriteLine($"  Execution ID:        {response.ExecutionId}");
        Console.WriteLine($"  Status:              {response.Status}");
        Console.WriteLine($"  Started:             {response.StartTime:yyyy-MM-dd HH:mm:ss}");
        Console.WriteLine($"  Estimated Duration:  {response.EstimatedDuration}");
        Console.WriteLine($"  Trace ID:            {response.TraceId}");
        Console.WriteLine();
        Console.WriteLine("Links:");
        foreach (var link in response.Links)
        {
            Console.WriteLine($"  {link.Key}: {link.Value}");
        }

        return response.ExecutionId;
    }

    #endregion

    #region Example 6: Track Deployment Status

    private static async Task Example06_TrackDeploymentStatusAsync(Guid executionId)
    {
        PrintExampleHeader("Example 6", "Track Deployment Status with Polling");

        Console.WriteLine($"Tracking deployment: {executionId}\n");

        int maxPolls = 10;
        int pollIntervalSeconds = 3;

        for (int i = 0; i < maxPolls; i++)
        {
            try
            {
                var status = await _apiClient!.GetDeploymentStatusAsync(executionId);

                Console.WriteLine($"Poll #{i + 1} - {DateTime.Now:HH:mm:ss}");
                Console.WriteLine($"  Module:   {status.ModuleName} v{status.Version}");
                Console.WriteLine($"  Status:   {status.Status}");
                Console.WriteLine($"  Duration: {status.Duration}");

                if (status.Stages.Any())
                {
                    Console.WriteLine("  Stages:");
                    foreach (var stage in status.Stages)
                    {
                        var stageIcon = stage.Status == "Succeeded" ? "✅" :
                                       stage.Status == "Failed" ? "❌" : "⏳";
                        Console.WriteLine($"    {stageIcon} {stage.Name}: {stage.Status}");
                        if (stage.NodesDeployed.HasValue)
                        {
                            Console.WriteLine($"       Nodes Deployed: {stage.NodesDeployed}");
                        }
                        if (stage.NodesFailed.HasValue && stage.NodesFailed.Value > 0)
                        {
                            Console.WriteLine($"       Nodes Failed:   {stage.NodesFailed}");
                        }
                    }
                }

                if (status.Status == "Succeeded" || status.Status == "Failed")
                {
                    Console.WriteLine($"\n🏁 Deployment {status.Status}!");
                    Console.WriteLine($"   Total Duration: {status.Duration}");
                    break;
                }

                Console.WriteLine();
                await Task.Delay(TimeSpan.FromSeconds(pollIntervalSeconds));
            }
            catch (InvalidOperationException ex)
            {
                _logger!.LogWarning("Deployment not found yet: {Message}", ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(pollIntervalSeconds));
            }
        }
    }

    #endregion

    #region Example 7: Rolling Deployment (QA)

    private static async Task Example07_RollingDeploymentAsync()
    {
        PrintExampleHeader("Example 7", "Rolling Deployment Strategy - QA Environment");

        var request = new CreateDeploymentRequest
        {
            ModuleName = "payment-processor",
            Version = "2.1.0",
            TargetEnvironment = "QA",
            RequesterEmail = "qa@example.com",
            Description = "Payment processor with new fraud detection",
            RequireApproval = false,
            Metadata = new Dictionary<string, string>
            {
                ["jira-ticket"] = "PAY-245",
                ["change-type"] = "feature"
            }
        };

        Console.WriteLine("Deployment Configuration:");
        Console.WriteLine($"  Module:      {request.ModuleName} v{request.Version}");
        Console.WriteLine($"  Environment: {request.TargetEnvironment}");
        Console.WriteLine($"  Strategy:    Rolling (sequential batches with health checks)");
        Console.WriteLine($"  Expected:    2-5 minutes");
        Console.WriteLine();

        var response = await _apiClient!.CreateDeploymentAsync(request);

        Console.WriteLine("✅ Rolling Deployment Accepted!");
        Console.WriteLine($"  Execution ID: {response.ExecutionId}");
        Console.WriteLine($"  Status:       {response.Status}");
        Console.WriteLine();
    }

    #endregion

    #region Example 8: Blue-Green Deployment (Staging)

    private static async Task Example08_BlueGreenDeploymentAsync()
    {
        PrintExampleHeader("Example 8", "Blue-Green Deployment Strategy - Staging Environment");

        var request = new CreateDeploymentRequest
        {
            ModuleName = "order-management",
            Version = "3.0.0",
            TargetEnvironment = "Staging",
            RequesterEmail = "staging@example.com",
            Description = "Major refactoring with microservices architecture",
            RequireApproval = false,
            Metadata = new Dictionary<string, string>
            {
                ["jira-epic"] = "ORD-500",
                ["migration"] = "monolith-to-microservices",
                ["smoke-tests"] = "enabled"
            }
        };

        Console.WriteLine("Deployment Configuration:");
        Console.WriteLine($"  Module:      {request.ModuleName} v{request.Version}");
        Console.WriteLine($"  Environment: {request.TargetEnvironment}");
        Console.WriteLine($"  Strategy:    Blue-Green (parallel environment with smoke tests)");
        Console.WriteLine($"  Expected:    5-10 minutes");
        Console.WriteLine();

        var response = await _apiClient!.CreateDeploymentAsync(request);

        Console.WriteLine("✅ Blue-Green Deployment Accepted!");
        Console.WriteLine($"  Execution ID: {response.ExecutionId}");
        Console.WriteLine($"  Status:       {response.Status}");
        Console.WriteLine();
        Console.WriteLine("Blue-Green Process:");
        Console.WriteLine("  1. Deploy to Green environment");
        Console.WriteLine("  2. Run smoke tests on Green");
        Console.WriteLine("  3. Switch traffic from Blue to Green");
        Console.WriteLine("  4. Monitor Green environment");
        Console.WriteLine("  5. Decommission Blue environment");
    }

    #endregion

    #region Example 9: Canary Deployment (Production)

    private static async Task<Guid?> Example09_CanaryDeploymentAsync()
    {
        PrintExampleHeader("Example 9", "Canary Deployment Strategy - Production Environment");

        var request = new CreateDeploymentRequest
        {
            ModuleName = "recommendation-engine",
            Version = "4.5.2",
            TargetEnvironment = "Production",
            RequesterEmail = "prod-ops@example.com",
            Description = "ML model update with improved accuracy",
            RequireApproval = false,
            Metadata = new Dictionary<string, string>
            {
                ["jira-ticket"] = "REC-789",
                ["model-version"] = "v4.5.2",
                ["accuracy-improvement"] = "12%",
                ["rollback-threshold"] = "5%"
            }
        };

        Console.WriteLine("Deployment Configuration:");
        Console.WriteLine($"  Module:      {request.ModuleName} v{request.Version}");
        Console.WriteLine($"  Environment: {request.TargetEnvironment}");
        Console.WriteLine($"  Strategy:    Canary (gradual rollout with metrics monitoring)");
        Console.WriteLine($"  Expected:    15-30 minutes");
        Console.WriteLine();

        var response = await _apiClient!.CreateDeploymentAsync(request);

        Console.WriteLine("✅ Canary Deployment Accepted!");
        Console.WriteLine($"  Execution ID: {response.ExecutionId}");
        Console.WriteLine($"  Status:       {response.Status}");
        Console.WriteLine();
        Console.WriteLine("Canary Rollout Plan:");
        Console.WriteLine("  Phase 1: 10% of nodes  (monitor for 5 min)");
        Console.WriteLine("  Phase 2: 30% of nodes  (monitor for 5 min)");
        Console.WriteLine("  Phase 3: 50% of nodes  (monitor for 5 min)");
        Console.WriteLine("  Phase 4: 100% of nodes (full deployment)");
        Console.WriteLine();
        Console.WriteLine("Automatic rollback if error rate > 5%");

        return response.ExecutionId;
    }

    #endregion

    #region Example 10: List All Deployments

    private static async Task Example10_ListAllDeploymentsAsync()
    {
        PrintExampleHeader("Example 10", "List All Recent Deployments");

        var deployments = await _apiClient!.ListDeploymentsAsync();

        Console.WriteLine($"Found {deployments.Count} recent deployment(s):\n");

        foreach (var deployment in deployments.Take(10))
        {
            var statusIcon = deployment.Status == "Succeeded" ? "✅" :
                            deployment.Status == "Failed" ? "❌" : "⏳";

            Console.WriteLine($"{statusIcon} {deployment.ModuleName} v{deployment.Version}");
            Console.WriteLine($"   Execution ID: {deployment.ExecutionId}");
            Console.WriteLine($"   Status:       {deployment.Status}");
            Console.WriteLine($"   Started:      {deployment.StartTime:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine($"   Duration:     {deployment.Duration}");
            Console.WriteLine();
        }

        if (deployments.Count > 10)
        {
            Console.WriteLine($"... and {deployments.Count - 10} more deployment(s)");
        }
    }

    #endregion

    #region Example 11: Deployment with Metadata and Approval

    private static async Task Example11_DeploymentWithMetadataAsync()
    {
        PrintExampleHeader("Example 11", "Deployment with Comprehensive Metadata");

        var request = new CreateDeploymentRequest
        {
            ModuleName = "user-notification-service",
            Version = "2.3.1",
            TargetEnvironment = "Production",
            RequesterEmail = "platform-team@example.com",
            Description = "Bug fix for notification delivery failures",
            RequireApproval = true,  // Requires manual approval
            Metadata = new Dictionary<string, string>
            {
                ["jira-ticket"] = "NOTIF-123",
                ["severity"] = "high",
                ["bug-fix"] = "notification-delivery",
                ["affected-users"] = "15000",
                ["approved-by"] = "tech-lead@example.com",
                ["approval-date"] = DateTime.UtcNow.ToString("yyyy-MM-dd"),
                ["rollback-plan"] = "revert-to-v2.3.0",
                ["monitoring-dashboard"] = "https://grafana.example.com/notif",
                ["runbook"] = "https://wiki.example.com/runbooks/notif-deployment"
            }
        };

        Console.WriteLine("Deployment Configuration:");
        Console.WriteLine($"  Module:         {request.ModuleName} v{request.Version}");
        Console.WriteLine($"  Environment:    {request.TargetEnvironment}");
        Console.WriteLine($"  Requires Approval: {request.RequireApproval}");
        Console.WriteLine($"  Description:    {request.Description}");
        Console.WriteLine();
        Console.WriteLine("Metadata:");
        foreach (var meta in request.Metadata!)
        {
            Console.WriteLine($"  {meta.Key}: {meta.Value}");
        }
        Console.WriteLine();

        var response = await _apiClient!.CreateDeploymentAsync(request);

        Console.WriteLine("✅ Deployment Created (Pending Approval)");
        Console.WriteLine($"  Execution ID: {response.ExecutionId}");
        Console.WriteLine($"  Status:       {response.Status}");
    }

    #endregion

    #region Example 12: Rollback Deployment

    private static async Task Example12_RollbackDeploymentAsync(Guid executionId)
    {
        PrintExampleHeader("Example 12", "Rollback Deployment to Previous Version");

        Console.WriteLine($"Rolling back deployment: {executionId}\n");

        // First, check the deployment status
        try
        {
            var status = await _apiClient!.GetDeploymentStatusAsync(executionId);

            Console.WriteLine("Current Deployment Status:");
            Console.WriteLine($"  Module:  {status.ModuleName} v{status.Version}");
            Console.WriteLine($"  Status:  {status.Status}");
            Console.WriteLine();

            // Initiate rollback
            Console.WriteLine("Initiating rollback...");
            var rollback = await _apiClient.RollbackDeploymentAsync(executionId);

            Console.WriteLine("✅ Rollback Initiated!");
            Console.WriteLine($"  Rollback ID:     {rollback.RollbackId}");
            Console.WriteLine($"  Status:          {rollback.Status}");
            Console.WriteLine($"  Nodes Affected:  {rollback.NodesAffected}");
            Console.WriteLine();
            Console.WriteLine("Rollback will restore the previous stable version");
        }
        catch (InvalidOperationException ex)
        {
            _logger!.LogWarning("Cannot rollback: {Message}", ex.Message);
        }
    }

    #endregion

    #region Example 13: Multi-Environment Health Dashboard

    private static async Task Example13_MultiEnvironmentDashboardAsync()
    {
        PrintExampleHeader("Example 13", "Multi-Environment Health Dashboard");

        var environments = new[] { "Development", "QA", "Staging", "Production" };

        Console.WriteLine("╔═══════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║                     CLUSTER HEALTH DASHBOARD                      ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        foreach (var env in environments)
        {
            try
            {
                var clusterInfo = await _apiClient!.GetClusterInfoAsync(env);

                var healthIcon = clusterInfo.HealthyNodes == clusterInfo.TotalNodes ? "✅" : "⚠️";
                var healthPercent = (double)clusterInfo.HealthyNodes / clusterInfo.TotalNodes * 100;

                Console.WriteLine($"{healthIcon} {env,-15} Health: {healthPercent:F1}% " +
                                $"({clusterInfo.HealthyNodes}/{clusterInfo.TotalNodes} nodes)");

                Console.WriteLine($"   CPU: {GenerateBar(clusterInfo.Metrics.AvgCpuUsage, 100)} " +
                                $"{clusterInfo.Metrics.AvgCpuUsage:F1}%");

                Console.WriteLine($"   MEM: {GenerateBar(clusterInfo.Metrics.AvgMemoryUsage, 100)} " +
                                $"{clusterInfo.Metrics.AvgMemoryUsage:F1}%");

                Console.WriteLine($"   Latency: {clusterInfo.Metrics.AvgLatency:F2}ms | " +
                                $"Error Rate: {clusterInfo.Metrics.ErrorRate:F4}% | " +
                                $"RPS: {clusterInfo.Metrics.RequestsPerSecond:F2}");

                Console.WriteLine();
            }
            catch (Exception ex)
            {
                _logger!.LogWarning(ex, "Failed to get info for {Environment}", env);
                Console.WriteLine($"❌ {env,-15} Unable to retrieve cluster info");
                Console.WriteLine();
            }
        }
    }

    #endregion

    #region Example 14: Error Handling

    private static async Task Example14_ErrorHandlingAsync()
    {
        PrintExampleHeader("Example 14", "Error Handling and Edge Cases");

        // Test 1: Invalid deployment ID
        Console.WriteLine("Test 1: Query non-existent deployment");
        try
        {
            await _apiClient!.GetDeploymentStatusAsync(Guid.NewGuid());
            Console.WriteLine("❌ Expected error was not thrown");
        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine($"✅ Correctly handled: {ex.Message}");
        }
        Console.WriteLine();

        // Test 2: Invalid environment
        Console.WriteLine("Test 2: Query non-existent cluster");
        try
        {
            await _apiClient!.GetClusterInfoAsync("InvalidEnvironment");
            Console.WriteLine("❌ Expected error was not thrown");
        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine($"✅ Correctly handled: {ex.Message}");
        }
        Console.WriteLine();

        // Test 3: Invalid deployment request
        Console.WriteLine("Test 3: Create deployment with invalid data");
        try
        {
            var request = new CreateDeploymentRequest
            {
                ModuleName = "",  // Empty module name
                Version = "1.0.0",
                TargetEnvironment = "Production",
                RequesterEmail = "test@example.com"
            };

            await _apiClient!.CreateDeploymentAsync(request);
            Console.WriteLine("❌ Expected validation error was not thrown");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✅ Validation caught: {ex.Message}");
        }
        Console.WriteLine();

        Console.WriteLine("All error handling tests completed!");
    }

    #endregion

    #region Helper Methods

    private static void PrintExampleHeader(string exampleNumber, string title)
    {
        Console.WriteLine();
        Console.WriteLine("═══════════════════════════════════════════════════════════════════");
        Console.WriteLine($"  {exampleNumber}: {title}");
        Console.WriteLine("═══════════════════════════════════════════════════════════════════");
        Console.WriteLine();
    }

    private static async Task PauseBetweenExamples()
    {
        await Task.Delay(TimeSpan.FromSeconds(2));
    }

    private static string GenerateBar(double value, double max)
    {
        const int barLength = 20;
        var filled = (int)(value / max * barLength);
        var empty = barLength - filled;

        return $"[{new string('█', filled)}{new string('░', empty)}]";
    }

    #endregion
}
