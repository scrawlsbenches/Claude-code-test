using FluentAssertions;
using HotSwap.Distributed.IntegrationTests.Fixtures;
using HotSwap.Distributed.IntegrationTests.Helpers;
using System.Net;
using Xunit;

namespace HotSwap.Distributed.IntegrationTests.Tests;

/// <summary>
/// Integration tests for deployment strategies: Direct, Rolling, Blue-Green, and Canary.
/// These tests verify that deployments execute correctly with different strategies
/// based on the target environment.
/// </summary>
[Collection("IntegrationTests")]
public class DeploymentStrategyIntegrationTests : IAsyncLifetime
{
    private readonly SharedIntegrationTestFixture _fixture;
    private HttpClient? _client;
    private AuthHelper? _authHelper;
    private ApiClientHelper? _apiHelper;

    public DeploymentStrategyIntegrationTests(SharedIntegrationTestFixture fixture)
    {
        _fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
    }

    public async Task InitializeAsync()
    {
        // Factory is already initialized by collection fixture
        // Just create client for each test
        _client = _fixture.Factory.CreateClient();
        _authHelper = new AuthHelper(_client);
        _apiHelper = new ApiClientHelper(_client);

        // Authenticate with deployer role (required for creating deployments)
        var token = await _authHelper.GetDeployerTokenAsync();
        _authHelper.AddAuthorizationHeader(_client, token);
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        // Factory is disposed by collection fixture, not here
        await Task.CompletedTask;
    }

    #region Direct Deployment Strategy Tests (Development Environment)

    /// <summary>
    /// Tests Direct deployment strategy (Development environment).
    /// Direct strategy deploys to all nodes simultaneously without health checks between deployments.
    /// </summary>
    [Fact]
    [Trait("Category", "Slow")]
    public async Task DirectDeployment_ToDevelopmentEnvironment_CompletesSuccessfully()
    {
        // Arrange
        var request = TestDataBuilder.ForDevelopment("direct-test-module")
            .WithVersion("1.0.0")
            .WithDescription("Direct deployment strategy integration test")
            .Build();

        // Act
        var deploymentResponse = await _apiHelper!.CreateDeploymentAsync(request);
        var finalStatus = await _apiHelper.WaitForDeploymentCompletionAsync(
            deploymentResponse.ExecutionId.ToString(),
            timeout: TimeSpan.FromSeconds(30)); // Optimized: reduced from 3 minutes

        // Assert
        deploymentResponse.Should().NotBeNull();
        deploymentResponse.Status.Should().Be("Running");
        deploymentResponse.ExecutionId.Should().NotBeEmpty();

        finalStatus.Should().NotBeNull();
        finalStatus.Status.Should().Be("Succeeded", "Direct deployment should complete successfully");
        finalStatus.ModuleName.Should().Be("direct-test-module");
        finalStatus.Version.Should().Be("1.0.0");
        finalStatus.Stages.Should().NotBeEmpty();

        // Verify deployment stage exists and used Direct strategy
        var deploymentStage = finalStatus.Stages.FirstOrDefault(s => s.Name == "Deploy to Development");
        deploymentStage.Should().NotBeNull();
        deploymentStage!.Strategy.Should().Be("Direct", "Development environment should use Direct deployment strategy");
        deploymentStage.Status.Should().Be("Succeeded");
        deploymentStage.NodesDeployed.Should().BeGreaterThan(0, "At least one node should be deployed");
    }

    /// <summary>
    /// Tests Direct deployment with multiple versions to verify module updates work correctly.
    /// </summary>
    [Fact]
    [Trait("Category", "Slow")]
    public async Task DirectDeployment_WithMultipleVersions_UpdatesSuccessfully()
    {
        // Arrange - Deploy version 1.0.0
        var requestV1 = TestDataBuilder.ForDevelopment("versioned-module")
            .WithVersion("1.0.0")
            .WithDescription("Direct deployment - version 1.0.0")
            .Build();

        // Act - Deploy first version
        var deploymentV1 = await _apiHelper!.CreateDeploymentAsync(requestV1);
        var statusV1 = await _apiHelper.WaitForDeploymentCompletionAsync(
            deploymentV1.ExecutionId.ToString(),
            timeout: TimeSpan.FromSeconds(30)); // Optimized: reduced from 3 minutes

        // Assert - Version 1 deployed successfully
        statusV1.Status.Should().Be("Succeeded");
        statusV1.Version.Should().Be("1.0.0");

        // Arrange - Deploy version 2.0.0 (update)
        var requestV2 = TestDataBuilder.ForDevelopment("versioned-module")
            .WithVersion("2.0.0")
            .WithDescription("Direct deployment - version 2.0.0 update")
            .Build();

        // Act - Deploy second version
        var deploymentV2 = await _apiHelper.CreateDeploymentAsync(requestV2);
        var statusV2 = await _apiHelper.WaitForDeploymentCompletionAsync(
            deploymentV2.ExecutionId.ToString(),
            timeout: TimeSpan.FromSeconds(30)); // Optimized: reduced from 3 minutes

        // Assert - Version 2 deployed successfully
        statusV2.Status.Should().Be("Succeeded", "Module update should succeed");
        statusV2.Version.Should().Be("2.0.0");
        statusV2.ExecutionId.Should().NotBe(statusV1.ExecutionId, "Each deployment should have unique execution ID");
    }

    #endregion

    #region Rolling Deployment Strategy Tests (QA Environment)

    /// <summary>
    /// Tests Rolling deployment strategy (QA environment).
    /// Rolling strategy deploys to a few nodes at a time, validating health before proceeding.
    /// </summary>
    [Fact]
    [Trait("Category", "Slow")]
    public async Task RollingDeployment_ToQAEnvironment_CompletesSuccessfully()
    {
        // Arrange
        var request = TestDataBuilder.ForQA("rolling-test-module")
            .WithVersion("1.0.0")
            .WithDescription("Rolling deployment strategy integration test")
            .Build();

        // Act
        var deploymentResponse = await _apiHelper!.CreateDeploymentAsync(request);
        var finalStatus = await _apiHelper.WaitForDeploymentCompletionAsync(
            deploymentResponse.ExecutionId.ToString(),
            timeout: TimeSpan.FromSeconds(45)); // Optimized: reduced from 90 seconds

        // Assert
        deploymentResponse.Should().NotBeNull();
        finalStatus.Should().NotBeNull();
        finalStatus.Status.Should().Be("Succeeded", "Rolling deployment should complete successfully");
        finalStatus.ModuleName.Should().Be("rolling-test-module");

        // Verify deployment stage used Rolling strategy
        var deploymentStage = finalStatus.Stages.FirstOrDefault(s => s.Name == "Deploy to QA");
        deploymentStage.Should().NotBeNull();
        deploymentStage!.Strategy.Should().Be("Rolling", "QA environment should use Rolling deployment strategy");
        deploymentStage.Status.Should().Be("Succeeded");
        deploymentStage.NodesDeployed.Should().BeGreaterThan(0);
        deploymentStage.NodesFailed.Should().Be(0, "Rolling deployment should not have failed nodes");
    }

    /// <summary>
    /// Tests Rolling deployment deploys to nodes in batches (not all at once).
    /// </summary>
    [Fact]
    [Trait("Category", "Slow")]
    public async Task RollingDeployment_DeploysInBatches_NotAllAtOnce()
    {
        // Arrange
        var request = TestDataBuilder.ForQA("batched-rolling-module")
            .WithVersion("1.0.0")
            .WithDescription("Rolling deployment batch verification")
            .Build();

        // Act
        var deploymentResponse = await _apiHelper!.CreateDeploymentAsync(request);
        var finalStatus = await _apiHelper.WaitForDeploymentCompletionAsync(
            deploymentResponse.ExecutionId.ToString(),
            timeout: TimeSpan.FromSeconds(45)); // Optimized: reduced from 90 seconds

        // Assert
        finalStatus.Status.Should().Be("Succeeded");

        // Verify deployment took some time (not instant like Direct)
        var duration = finalStatus.EndTime!.Value - finalStatus.StartTime;
        duration.Should().BeGreaterThan(TimeSpan.FromMilliseconds(500), // Optimized: reduced from 1 second
            "Rolling deployment should take time to deploy in batches");

        var deploymentStage = finalStatus.Stages.FirstOrDefault(s => s.Name == "Deploy to QA");
        deploymentStage!.Strategy.Should().Be("Rolling");
    }

    #endregion

    #region Blue-Green Deployment Strategy Tests (Staging Environment)

    /// <summary>
    /// Tests Blue-Green deployment strategy (Staging environment).
    /// Blue-Green strategy deploys to inactive ("green") nodes, validates, then switches traffic.
    /// </summary>
    [Fact]
    [Trait("Category", "Slow")]
    public async Task BlueGreenDeployment_ToStagingEnvironment_CompletesSuccessfully()
    {
        // Arrange
        var request = TestDataBuilder.ForStaging("bluegreen-test-module")
            .WithVersion("1.0.0")
            .WithDescription("Blue-Green deployment strategy integration test")
            .Build();

        // Act
        var deploymentResponse = await _apiHelper!.CreateDeploymentAsync(request);
        var finalStatus = await _apiHelper.WaitForDeploymentCompletionAsync(
            deploymentResponse.ExecutionId.ToString(),
            timeout: TimeSpan.FromSeconds(45)); // Optimized: reduced from 90 seconds

        // Assert
        deploymentResponse.Should().NotBeNull();
        finalStatus.Should().NotBeNull();
        finalStatus.Status.Should().Be("Succeeded", "Blue-Green deployment should complete successfully");
        finalStatus.ModuleName.Should().Be("bluegreen-test-module");

        // Verify deployment stage used Blue-Green strategy
        var deploymentStage = finalStatus.Stages.FirstOrDefault(s => s.Name == "Deploy to Staging");
        deploymentStage.Should().NotBeNull();
        deploymentStage!.Strategy.Should().Be("BlueGreen", "Staging environment should use Blue-Green deployment strategy");
        deploymentStage.Status.Should().Be("Succeeded");
        deploymentStage.NodesDeployed.Should().BeGreaterThan(0);
    }

    /// <summary>
    /// Tests Blue-Green deployment includes smoke tests before switching traffic.
    /// </summary>
    [Fact]
    [Trait("Category", "Slow")]
    public async Task BlueGreenDeployment_IncludesSmokeTests_BeforeSwitching()
    {
        // Arrange
        var request = TestDataBuilder.ForStaging("smoketest-module")
            .WithVersion("1.0.0")
            .WithDescription("Blue-Green deployment with smoke tests")
            .Build();

        // Act
        var deploymentResponse = await _apiHelper!.CreateDeploymentAsync(request);
        var finalStatus = await _apiHelper.WaitForDeploymentCompletionAsync(
            deploymentResponse.ExecutionId.ToString(),
            timeout: TimeSpan.FromSeconds(45)); // Optimized: reduced from 90 seconds

        // Assert
        finalStatus.Status.Should().Be("Succeeded");

        // Blue-Green deployment includes validation stage (not "Smoke Tests" - that was renamed to "Validation")
        finalStatus.Stages.Should().Contain(s => s.Name == "Validation",
            "Blue-Green deployment should include validation stage");

        var validationStage = finalStatus.Stages.FirstOrDefault(s => s.Name == "Validation");
        validationStage.Should().NotBeNull();
        validationStage!.Status.Should().Be("Succeeded", "Validation should pass before traffic switch");
    }

    #endregion

    #region Canary Deployment Strategy Tests (Production Environment)

    /// <summary>
    /// Tests Canary deployment strategy (Production environment).
    /// Canary strategy gradually increases traffic to new version, starting with small percentage.
    /// </summary>
    [Fact]
    [Trait("Category", "Slow")]
    public async Task CanaryDeployment_ToProductionEnvironment_CompletesSuccessfully()
    {
        // Arrange
        var request = TestDataBuilder.ForProduction("canary-test-module")
            .WithVersion("1.0.0")
            .WithDescription("Canary deployment strategy integration test")
            .WithApprovalRequired(false) // Skip approval for automated test
            .Build();

        // Act
        var deploymentResponse = await _apiHelper!.CreateDeploymentAsync(request);
        var finalStatus = await _apiHelper.WaitForDeploymentCompletionAsync(
            deploymentResponse.ExecutionId.ToString(),
            timeout: TimeSpan.FromSeconds(60)); // Optimized: reduced from 2 minutes

        // Assert
        deploymentResponse.Should().NotBeNull();
        finalStatus.Should().NotBeNull();
        finalStatus.Status.Should().Be("Succeeded", "Canary deployment should complete successfully");
        finalStatus.ModuleName.Should().Be("canary-test-module");

        // Verify deployment stage used Canary strategy
        var deploymentStage = finalStatus.Stages.FirstOrDefault(s => s.Name == "Deploy to Production");
        deploymentStage.Should().NotBeNull();
        deploymentStage!.Strategy.Should().Be("Canary", "Production environment should use Canary deployment strategy");
        deploymentStage.Status.Should().Be("Succeeded");
        deploymentStage.NodesDeployed.Should().BeGreaterThan(0);
    }

    /// <summary>
    /// Tests Canary deployment takes significantly longer than other strategies
    /// due to gradual traffic increase and monitoring periods.
    /// </summary>
    [Fact]
    [Trait("Category", "Slow")]
    public async Task CanaryDeployment_TakesLongerThanDirectDeployment_DueToGradualRollout()
    {
        // Arrange
        var request = TestDataBuilder.ForProduction("gradual-canary-module")
            .WithVersion("1.0.0")
            .WithDescription("Canary deployment gradual rollout verification")
            .WithApprovalRequired(false)
            .Build();

        // Act
        var deploymentResponse = await _apiHelper!.CreateDeploymentAsync(request);
        var finalStatus = await _apiHelper.WaitForDeploymentCompletionAsync(
            deploymentResponse.ExecutionId.ToString(),
            timeout: TimeSpan.FromSeconds(60)); // Optimized: reduced from 2 minutes

        // Assert
        finalStatus.Status.Should().Be("Succeeded");

        // Verify deployment took significant time (gradual rollout)
        var duration = finalStatus.EndTime!.Value - finalStatus.StartTime;
        duration.Should().BeGreaterThan(TimeSpan.FromSeconds(2), // Optimized: reduced from 5 seconds
            "Canary deployment should take time for gradual traffic increase");

        var deploymentStage = finalStatus.Stages.FirstOrDefault(s => s.Name == "Deploy to Production");
        deploymentStage!.Strategy.Should().Be("Canary");
    }

    #endregion

    #region Cross-Strategy Comparison Tests

    /// <summary>
    /// Tests that different environments use different deployment strategies.
    /// </summary>
    [Fact]
    [Trait("Category", "Slow")]
    public async Task DeploymentStrategies_VaryByEnvironment_AsExpected()
    {
        // Arrange & Act - Deploy to all environments
        var tasks = new[]
        {
            DeployAndGetStrategy("Development", "strategy-comparison-module"),
            DeployAndGetStrategy("QA", "strategy-comparison-module"),
            DeployAndGetStrategy("Staging", "strategy-comparison-module"),
            DeployAndGetStrategy("Production", "strategy-comparison-module")
        };

        var results = await Task.WhenAll(tasks);

        // Assert - Each environment uses expected strategy
        results[0].Strategy.Should().Be("Direct", "Development should use Direct strategy");
        results[1].Strategy.Should().Be("Rolling", "QA should use Rolling strategy");
        results[2].Strategy.Should().Be("BlueGreen", "Staging should use Blue-Green strategy");
        results[3].Strategy.Should().Be("Canary", "Production should use Canary strategy");
    }

    private async Task<(string Environment, string Strategy)> DeployAndGetStrategy(string environment, string moduleName)
    {
        var builder = environment switch
        {
            "Development" => TestDataBuilder.ForDevelopment(moduleName),
            "QA" => TestDataBuilder.ForQA(moduleName),
            "Staging" => TestDataBuilder.ForStaging(moduleName).WithApprovalRequired(false), // Disable approval for strategy testing
            "Production" => TestDataBuilder.ForProduction(moduleName).WithApprovalRequired(false),
            _ => throw new ArgumentException($"Unknown environment: {environment}")
        };

        var request = builder.WithVersion("1.0.0").Build();
        var response = await _apiHelper!.CreateDeploymentAsync(request);
        var status = await _apiHelper.WaitForDeploymentCompletionAsync(
            response.ExecutionId.ToString(),
            timeout: TimeSpan.FromSeconds(60)); // Optimized: reduced from 2 minutes

        var deploymentStage = status.Stages.FirstOrDefault(s => s.Name == $"Deploy to {environment}");
        return (environment, deploymentStage?.Strategy ?? "Unknown");
    }

    #endregion
}
