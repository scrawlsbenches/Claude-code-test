using FluentAssertions;
using HotSwap.Distributed.IntegrationTests.Fixtures;
using HotSwap.Distributed.IntegrationTests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace HotSwap.Distributed.IntegrationTests.Tests;

/// <summary>
/// Diagnostic tests to identify why DeploymentStrategyIntegrationTests are timing out.
/// These tests probe the deployment pipeline to find blocking points.
/// </summary>
[Collection("IntegrationTests")]
public class DeploymentDiagnosticTests : IAsyncLifetime
{
    private readonly SharedIntegrationTestFixture _fixture;
    private readonly ITestOutputHelper _output;
    private HttpClient? _client;
    private AuthHelper? _authHelper;
    private ApiClientHelper? _apiHelper;

    public DeploymentDiagnosticTests(SharedIntegrationTestFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
        _output = output ?? throw new ArgumentNullException(nameof(output));
    }

    public async Task InitializeAsync()
    {
        _client = _fixture.Factory.CreateClient();
        _authHelper = new AuthHelper(_client);
        _apiHelper = new ApiClientHelper(_client);

        var token = await _authHelper.GetDeployerTokenAsync();
        _authHelper.AddAuthorizationHeader(_client, token);
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        await Task.CompletedTask;
    }

    /// <summary>
    /// Test 1: Can ANY deployment complete? (Simple Direct deployment)
    /// </summary>
    [Fact]
    [Trait("Category", "Diagnostic")]
    public async Task Diagnostic_DirectDeployment_CompletesWithin10Seconds()
    {
        // Arrange
        var request = TestDataBuilder.ForDevelopment("diagnostic-direct")
            .WithVersion("1.0.0")
            .WithDescription("Diagnostic: Direct deployment baseline")
            .Build();

        // Act
        var startTime = DateTime.UtcNow;
        var deploymentResponse = await _apiHelper!.CreateDeploymentAsync(request);

        _output.WriteLine($"Deployment created: {deploymentResponse.ExecutionId} at {startTime:HH:mm:ss.fff}");

        var finalStatus = await _apiHelper.WaitForDeploymentCompletionAsync(
            deploymentResponse.ExecutionId.ToString(),
            timeout: TimeSpan.FromSeconds(10),
            pollInterval: TimeSpan.FromMilliseconds(500));

        var endTime = DateTime.UtcNow;
        var duration = endTime - startTime;

        // Assert
        _output.WriteLine($"Deployment completed: {finalStatus.Status} in {duration.TotalSeconds:F2}s");
        finalStatus.Status.Should().Be("Succeeded", "Direct deployment should complete quickly");
        duration.Should().BeLessThan(TimeSpan.FromSeconds(10), "Direct deployment should be fast");
    }

    /// <summary>
    /// Test 2: Does BlueGreen get stuck in a specific stage?
    /// </summary>
    [Fact]
    [Trait("Category", "Diagnostic")]
    public async Task Diagnostic_BlueGreen_ReportsProgressBeforeTimeout()
    {
        // Arrange
        var request = TestDataBuilder.ForStaging("diagnostic-bluegreen")
            .WithVersion("1.0.0")
            .WithDescription("Diagnostic: BlueGreen stage analysis")
            .Build();

        // Act
        var deploymentResponse = await _apiHelper!.CreateDeploymentAsync(request);
        _output.WriteLine($"BlueGreen deployment created: {deploymentResponse.ExecutionId}");

        // Poll frequently and log all state changes
        var pollingStart = DateTime.UtcNow;
        var lastStatus = "";
        var lastStage = "";
        var stuckCount = 0;

        for (int i = 0; i < 60; i++) // Poll for 30 seconds (500ms intervals)
        {
            var status = await _apiHelper.GetDeploymentStatusAsync(deploymentResponse.ExecutionId.ToString());

            if (status != null)
            {
                var currentStage = status.Stages.LastOrDefault()?.Name ?? "None";

                if (status.Status != lastStatus || currentStage != lastStage)
                {
                    var elapsed = (DateTime.UtcNow - pollingStart).TotalSeconds;
                    _output.WriteLine($"[{elapsed:F1}s] Status: {status.Status}, Stage: {currentStage}, StageCount: {status.Stages.Count}");
                    lastStatus = status.Status;
                    lastStage = currentStage;
                    stuckCount = 0;
                }
                else
                {
                    stuckCount++;
                    if (stuckCount >= 10) // Stuck for 5 seconds
                    {
                        var elapsed = (DateTime.UtcNow - pollingStart).TotalSeconds;
                        _output.WriteLine($"[{elapsed:F1}s] ⚠️  STUCK: Status={status.Status}, Stage={currentStage} for {stuckCount * 0.5}s");

                        // Log all stages
                        foreach (var stage in status.Stages)
                        {
                            _output.WriteLine($"    Stage: {stage.Name}, Status: {stage.Status}, Strategy: {stage.Strategy}");
                        }

                        break; // Found the blocking point
                    }
                }

                if (status.Status == "Succeeded" || status.Status == "Failed")
                {
                    var elapsed = (DateTime.UtcNow - pollingStart).TotalSeconds;
                    _output.WriteLine($"[{elapsed:F1}s] ✓ Deployment completed: {status.Status}");
                    break;
                }
            }

            await Task.Delay(500);
        }

        // Assert - just report findings, don't fail
        _output.WriteLine($"Final state after 30s: Status={lastStatus}, LastStage={lastStage}");

        // This test ALWAYS passes - it's just for diagnostics
        Assert.True(true, "Diagnostic test completed");
    }

    /// <summary>
    /// Test 3: Does Canary get stuck waiting for metrics analysis?
    /// </summary>
    [Fact]
    [Trait("Category", "Diagnostic")]
    public async Task Diagnostic_Canary_ReportsWaveProgressAndDelay()
    {
        // Arrange
        var request = TestDataBuilder.ForProduction("diagnostic-canary")
            .WithVersion("1.0.0")
            .WithDescription("Diagnostic: Canary wave analysis")
            .WithApprovalRequired(false) // Skip approval
            .Build();

        // Act
        var deploymentResponse = await _apiHelper!.CreateDeploymentAsync(request);
        _output.WriteLine($"Canary deployment created: {deploymentResponse.ExecutionId}");

        // Poll and watch for canary-specific delays
        var pollingStart = DateTime.UtcNow;
        var lastStatus = "";
        var previousStageCount = 0;

        for (int i = 0; i < 90; i++) // Poll for 45 seconds
        {
            var status = await _apiHelper.GetDeploymentStatusAsync(deploymentResponse.ExecutionId.ToString());

            if (status != null)
            {
                var elapsed = (DateTime.UtcNow - pollingStart).TotalSeconds;

                if (status.Status != lastStatus)
                {
                    _output.WriteLine($"[{elapsed:F1}s] Status changed: {lastStatus} → {status.Status}");
                    lastStatus = status.Status;
                }

                if (status.Stages.Count != previousStageCount)
                {
                    var newStage = status.Stages.LastOrDefault();
                    _output.WriteLine($"[{elapsed:F1}s] New stage: {newStage?.Name}, Strategy: {newStage?.Strategy}");
                    previousStageCount = status.Stages.Count;
                }

                // Check for long delays between poll responses
                var beforePoll = DateTime.UtcNow;
                await Task.Delay(500);
                var afterPoll = DateTime.UtcNow;
                var pollDelay = (afterPoll - beforePoll).TotalMilliseconds;

                if (pollDelay > 1000) // More than 1 second delay
                {
                    _output.WriteLine($"[{elapsed:F1}s] ⚠️  Long poll delay: {pollDelay}ms");
                }

                if (status.Status == "Succeeded" || status.Status == "Failed")
                {
                    _output.WriteLine($"[{elapsed:F1}s] ✓ Deployment completed: {status.Status}");
                    break;
                }
            }
            else
            {
                await Task.Delay(500);
            }
        }

        // Assert - just report findings
        _output.WriteLine($"Diagnostic completed after 45s");
        Assert.True(true, "Diagnostic test completed");
    }

    /// <summary>
    /// Test 4: Is the issue with pipeline state updates?
    /// </summary>
    [Fact]
    [Trait("Category", "Diagnostic")]
    public async Task Diagnostic_MultipleDeployments_ParallelExecution()
    {
        // Test if multiple deployments interfere with each other
        var tasks = new List<Task<(string Id, string Status, double DurationSeconds)>>();

        for (int i = 0; i < 3; i++)
        {
            var index = i;
            tasks.Add(Task.Run(async () =>
            {
                var request = TestDataBuilder.ForDevelopment($"diagnostic-parallel-{index}")
                    .WithVersion("1.0.0")
                    .Build();

                var start = DateTime.UtcNow;
                var response = await _apiHelper!.CreateDeploymentAsync(request);

                try
                {
                    var finalStatus = await _apiHelper.WaitForDeploymentCompletionAsync(
                        response.ExecutionId.ToString(),
                        timeout: TimeSpan.FromSeconds(15));

                    var duration = (DateTime.UtcNow - start).TotalSeconds;
                    return (response.ExecutionId.ToString(), finalStatus.Status, duration);
                }
                catch (TimeoutException)
                {
                    var duration = (DateTime.UtcNow - start).TotalSeconds;
                    return (response.ExecutionId.ToString(), "TIMEOUT", duration);
                }
            }));
        }

        var results = await Task.WhenAll(tasks);

        foreach (var result in results)
        {
            _output.WriteLine($"Deployment {result.Id}: {result.Status} in {result.DurationSeconds:F2}s");
        }

        // Check if any completed
        var completedCount = results.Count(r => r.Status == "Succeeded");
        var timeoutCount = results.Count(r => r.Status == "TIMEOUT");

        _output.WriteLine($"Results: {completedCount} succeeded, {timeoutCount} timed out");

        completedCount.Should().BeGreaterThan(0, "At least some deployments should complete");
    }
}
