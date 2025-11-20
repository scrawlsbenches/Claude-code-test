using FluentAssertions;
using HotSwap.Distributed.IntegrationTests.Fixtures;
using HotSwap.Distributed.IntegrationTests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace HotSwap.Distributed.IntegrationTests.Tests;

/// <summary>
/// Single BlueGreen test with verbose logging enabled to debug timeout issue.
/// This test is isolated to avoid log spam in other tests.
/// </summary>
[Collection("IntegrationTests")]
public class BlueGreenDebugTest : IAsyncLifetime
{
    private readonly SharedIntegrationTestFixture _fixture;
    private readonly ITestOutputHelper _output;
    private HttpClient? _client;
    private AuthHelper? _authHelper;
    private ApiClientHelper? _apiHelper;

    public BlueGreenDebugTest(SharedIntegrationTestFixture fixture, ITestOutputHelper output)
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

    [Fact]
    [Trait("Category", "Debug")]
    public async Task Debug_BlueGreenDeployment_WithDetailedPolling()
    {
        // Arrange
        var request = TestDataBuilder.ForStaging("debug-bluegreen")
            .WithVersion("1.0.0")
            .WithDescription("Debug test with detailed polling")
            .Build();

        _output.WriteLine("=== STARTING BLUEGREEN DEBUG TEST ===");
        _output.WriteLine($"Configuration: Staging environment, BlueGreen strategy");
        _output.WriteLine($"Expected: StagingSmokeTestTimeout = 10 seconds");
        _output.WriteLine("");

        // Act - Create deployment
        var startTime = DateTime.UtcNow;
        var deploymentResponse = await _apiHelper!.CreateDeploymentAsync(request);

        _output.WriteLine($"[{(DateTime.UtcNow - startTime).TotalSeconds:F2}s] Deployment created: {deploymentResponse.ExecutionId}");
        _output.WriteLine($"[{(DateTime.UtcNow - startTime).TotalSeconds:F2}s] Initial status: {deploymentResponse.Status}");
        _output.WriteLine("");

        // Poll with detailed logging
        string? lastStatus = null;
        string? lastStage = null;
        int lastStageCount = 0;
        int pollCount = 0;
        var consecutiveSameState = 0;

        for (int i = 0; i < 60; i++) // Poll for 30 seconds max (500ms intervals)
        {
            await Task.Delay(500);
            pollCount++;

            var status = await _apiHelper.GetDeploymentStatusAsync(deploymentResponse.ExecutionId.ToString());

            if (status == null)
            {
                _output.WriteLine($"[{(DateTime.UtcNow - startTime).TotalSeconds:F2}s] Poll #{pollCount}: STATUS IS NULL");
                continue;
            }

            var currentStatus = status.Status;
            var currentStageCount = status.Stages.Count;
            var currentStageName = status.Stages.LastOrDefault()?.Name ?? "None";
            var currentStageStatus = status.Stages.LastOrDefault()?.Status ?? "None";

            // Detect state changes
            if (currentStatus != lastStatus || currentStageName != lastStage || currentStageCount != lastStageCount)
            {
                _output.WriteLine($"[{(DateTime.UtcNow - startTime).TotalSeconds:F2}s] Poll #{pollCount}: STATUS CHANGED");
                _output.WriteLine($"  Overall Status: {currentStatus}");
                _output.WriteLine($"  Stage Count: {currentStageCount}");
                _output.WriteLine($"  Current Stage: {currentStageName} ({currentStageStatus})");

                // Log all stages
                if (status.Stages.Any())
                {
                    _output.WriteLine($"  All Stages:");
                    foreach (var stage in status.Stages)
                    {
                        var duration = stage.Duration ?? "N/A";
                        _output.WriteLine($"    - {stage.Name}: {stage.Status} ({duration}) [Strategy: {stage.Strategy}]");
                    }
                }
                _output.WriteLine("");

                lastStatus = currentStatus;
                lastStage = currentStageName;
                lastStageCount = currentStageCount;
                consecutiveSameState = 0;
            }
            else
            {
                consecutiveSameState++;

                // Log every 5 seconds if stuck
                if (consecutiveSameState % 10 == 0)
                {
                    _output.WriteLine($"[{(DateTime.UtcNow - startTime).TotalSeconds:F2}s] Poll #{pollCount}: NO CHANGE for {consecutiveSameState * 0.5}s");
                    _output.WriteLine($"  Still: {currentStatus} / {currentStageName} ({currentStageStatus})");
                }
            }

            // Check for terminal state
            if (currentStatus == "Succeeded" || currentStatus == "Failed" || currentStatus == "Cancelled")
            {
                var totalDuration = DateTime.UtcNow - startTime;
                _output.WriteLine($"");
                _output.WriteLine($"=== DEPLOYMENT COMPLETED ===");
                _output.WriteLine($"Final Status: {currentStatus}");
                _output.WriteLine($"Total Duration: {totalDuration.TotalSeconds:F2}s");
                _output.WriteLine($"Total Polls: {pollCount}");

                // Assert
                currentStatus.Should().Be("Succeeded", "BlueGreen deployment should succeed");
                totalDuration.Should().BeLessThan(TimeSpan.FromSeconds(30), "Should complete within 30 seconds");
                return;
            }
        }

        // If we get here, deployment timed out
        var finalDuration = DateTime.UtcNow - startTime;
        _output.WriteLine($"");
        _output.WriteLine($"=== DEPLOYMENT TIMED OUT ===");
        _output.WriteLine($"Last Known Status: {lastStatus}");
        _output.WriteLine($"Last Known Stage: {lastStage}");
        _output.WriteLine($"Duration: {finalDuration.TotalSeconds:F2}s");
        _output.WriteLine($"Total Polls: {pollCount}");
        _output.WriteLine($"Consecutive same state: {consecutiveSameState * 0.5}s");

        // Fail the test
        Assert.Fail($"Deployment did not complete within 30 seconds. Last state: {lastStatus} / {lastStage}");
    }
}
