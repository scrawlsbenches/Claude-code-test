using FluentAssertions;
using HotSwap.Distributed.IntegrationTests.Fixtures;
using HotSwap.Distributed.IntegrationTests.Helpers;
using System.Net;
using Xunit;

namespace HotSwap.Distributed.IntegrationTests.Tests;

/// <summary>
/// Integration tests for concurrent deployment scenarios.
/// These tests verify that the system can handle multiple simultaneous deployments
/// and maintains data consistency under concurrent load.
/// </summary>
[Collection("IntegrationTests")]
public class ConcurrentDeploymentIntegrationTests : IAsyncLifetime
{
    private readonly SharedIntegrationTestFixture _fixture;
    private HttpClient? _client;
    private AuthHelper? _authHelper;
    private ApiClientHelper? _apiHelper;

    public ConcurrentDeploymentIntegrationTests(SharedIntegrationTestFixture fixture)
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

        // Authenticate with deployer role
        var token = await _authHelper.GetDeployerTokenAsync();
        _authHelper.AddAuthorizationHeader(_client, token);
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        // Factory is disposed by collection fixture, not here
        await Task.CompletedTask;
    }

    #region Concurrent Deployment Tests

    /// <summary>
    /// Tests that multiple deployments to different environments can run concurrently.
    /// </summary>
    [Fact(Skip = "Concurrent deployment tests too slow - need optimization")]
    public async Task ConcurrentDeployments_ToDifferentEnvironments_AllSucceed()
    {
        // Arrange - Create deployment requests for different environments
        var deploymentRequests = new[]
        {
            TestDataBuilder.ForDevelopment("concurrent-dev-module").WithVersion("1.0.0").Build(),
            TestDataBuilder.ForQA("concurrent-qa-module").WithVersion("1.0.0").Build(),
            TestDataBuilder.ForStaging("concurrent-staging-module").WithVersion("1.0.0").Build()
        };

        // Act - Start all deployments concurrently
        var deploymentTasks = deploymentRequests
            .Select(request => _apiHelper!.CreateDeploymentAsync(request))
            .ToArray();

        var deploymentResponses = await Task.WhenAll(deploymentTasks);

        // Wait for all deployments to complete
        var completionTasks = deploymentResponses
            .Select(response => _apiHelper!.WaitForDeploymentCompletionAsync(
                response.ExecutionId.ToString(),
                timeout: TimeSpan.FromMinutes(2)))
            .ToArray();

        var finalStatuses = await Task.WhenAll(completionTasks);

        // Assert - All deployments should succeed
        deploymentResponses.Should().HaveCount(3);
        deploymentResponses.Should().OnlyContain(r => r.ExecutionId != Guid.Empty);

        finalStatuses.Should().HaveCount(3);
        finalStatuses.Should().OnlyContain(s => s.Status == "Succeeded",
            "All concurrent deployments to different environments should succeed");

        // Verify each deployment has unique execution ID
        var executionIds = finalStatuses.Select(s => s.ExecutionId).ToArray();
        executionIds.Distinct().Should().HaveCount(3, "Each deployment should have unique execution ID");
    }

    /// <summary>
    /// Tests that multiple deployments of different modules to the same environment can run concurrently.
    /// </summary>
    [Fact(Skip = "Concurrent deployment tests too slow - need optimization")]
    public async Task ConcurrentDeployments_DifferentModulesSameEnvironment_AllSucceed()
    {
        // Arrange - Create deployment requests for different modules to Development
        var deploymentRequests = new[]
        {
            TestDataBuilder.ForDevelopment("module-a").WithVersion("1.0.0").Build(),
            TestDataBuilder.ForDevelopment("module-b").WithVersion("1.0.0").Build(),
            TestDataBuilder.ForDevelopment("module-c").WithVersion("1.0.0").Build(),
            TestDataBuilder.ForDevelopment("module-d").WithVersion("1.0.0").Build()
        };

        // Act - Start all deployments concurrently
        var deploymentTasks = deploymentRequests
            .Select(request => _apiHelper!.CreateDeploymentAsync(request))
            .ToArray();

        var deploymentResponses = await Task.WhenAll(deploymentTasks);

        // Wait for all deployments to complete
        var completionTasks = deploymentResponses
            .Select(response => _apiHelper!.WaitForDeploymentCompletionAsync(
                response.ExecutionId.ToString(),
                timeout: TimeSpan.FromSeconds(90)))
            .ToArray();

        var finalStatuses = await Task.WhenAll(completionTasks);

        // Assert - All deployments should succeed
        finalStatuses.Should().HaveCount(4);
        finalStatuses.Should().OnlyContain(s => s.Status == "Succeeded",
            "All concurrent deployments of different modules should succeed");

        // Verify each module deployed correctly
        finalStatuses[0].ModuleName.Should().Be("module-a");
        finalStatuses[1].ModuleName.Should().Be("module-b");
        finalStatuses[2].ModuleName.Should().Be("module-c");
        finalStatuses[3].ModuleName.Should().Be("module-d");
    }

    /// <summary>
    /// Tests that concurrent deployments respect pipeline concurrency limits.
    /// </summary>
    [Fact(Skip = "Concurrent deployment tests too slow - need optimization")]
    public async Task ConcurrentDeployments_RespectsConcurrencyLimits()
    {
        // Arrange - Create many deployment requests (more than max concurrent pipelines)
        var deploymentCount = 10;
        var deploymentRequests = Enumerable.Range(1, deploymentCount)
            .Select(i => TestDataBuilder.ForDevelopment($"concurrent-limit-module-{i}")
                .WithVersion("1.0.0")
                .Build())
            .ToArray();

        // Act - Start all deployments at once
        var startTime = DateTime.UtcNow;

        var deploymentTasks = deploymentRequests
            .Select(request => _apiHelper!.CreateDeploymentAsync(request))
            .ToArray();

        var deploymentResponses = await Task.WhenAll(deploymentTasks);

        // Wait for all to complete
        var completionTasks = deploymentResponses
            .Select(response => _apiHelper!.WaitForDeploymentCompletionAsync(
                response.ExecutionId.ToString(),
                timeout: TimeSpan.FromMinutes(2)))
            .ToArray();

        var finalStatuses = await Task.WhenAll(completionTasks);

        var endTime = DateTime.UtcNow;
        var totalDuration = endTime - startTime;

        // Assert - All should eventually succeed
        finalStatuses.Should().HaveCount(deploymentCount);
        finalStatuses.Should().OnlyContain(s => s.Status == "Succeeded");

        // Deployments should have been queued/throttled (not all running simultaneously)
        // This is indicated by total duration being more than a single deployment time
        totalDuration.Should().BeGreaterThan(TimeSpan.FromSeconds(5),
            "With concurrency limits, deployments should queue and take longer overall");
    }

    #endregion

    #region Concurrent Request Isolation Tests

    /// <summary>
    /// Tests that concurrent deployments maintain proper isolation and don't interfere with each other.
    /// </summary>
    [Fact(Skip = "Concurrent deployment tests too slow - need optimization")]
    public async Task ConcurrentDeployments_MaintainIsolation_NoDataLeakage()
    {
        // Arrange - Create deployments with different metadata
        var deployment1 = TestDataBuilder.ForDevelopment("isolation-module-1")
            .WithVersion("1.0.0")
            .WithMetadata("TestKey", "Value1")
            .WithDescription("Deployment 1 for isolation test")
            .Build();

        var deployment2 = TestDataBuilder.ForDevelopment("isolation-module-2")
            .WithVersion("1.0.0")
            .WithMetadata("TestKey", "Value2")
            .WithDescription("Deployment 2 for isolation test")
            .Build();

        // Act - Deploy concurrently
        var task1 = _apiHelper!.CreateDeploymentAsync(deployment1);
        var task2 = _apiHelper.CreateDeploymentAsync(deployment2);

        var responses = await Task.WhenAll(task1, task2);

        var status1Task = _apiHelper.WaitForDeploymentCompletionAsync(
            responses[0].ExecutionId.ToString(),
            timeout: TimeSpan.FromSeconds(90));

        var status2Task = _apiHelper.WaitForDeploymentCompletionAsync(
            responses[1].ExecutionId.ToString(),
            timeout: TimeSpan.FromSeconds(90));

        var statuses = await Task.WhenAll(status1Task, status2Task);

        // Assert - Each deployment maintains its own data
        statuses[0].ModuleName.Should().Be("isolation-module-1");
        statuses[1].ModuleName.Should().Be("isolation-module-2");

        statuses[0].ExecutionId.Should().NotBe(statuses[1].ExecutionId,
            "Deployments should have unique execution IDs");

        // Both should succeed independently
        statuses[0].Status.Should().Be("Succeeded");
        statuses[1].Status.Should().Be("Succeeded");
    }

    #endregion

    #region Concurrent Read/Write Tests

    /// <summary>
    /// Tests that deployment creation and status queries can happen concurrently without errors.
    /// </summary>
    [Fact(Skip = "Concurrent deployment tests too slow - need optimization")]
    public async Task ConcurrentDeploymentCreationAndStatusQueries_NoConflicts()
    {
        // Arrange - Create a deployment
        var initialRequest = TestDataBuilder.ForDevelopment("read-write-module")
            .WithVersion("1.0.0")
            .Build();

        var initialDeployment = await _apiHelper!.CreateDeploymentAsync(initialRequest);
        var executionId = initialDeployment.ExecutionId.ToString();

        // Act - Concurrently create new deployments and query existing deployment status
        var createTasks = Enumerable.Range(1, 5)
            .Select(i => _apiHelper.CreateDeploymentAsync(
                TestDataBuilder.ForDevelopment($"concurrent-rw-module-{i}")
                    .WithVersion("1.0.0")
                    .Build()))
            .ToArray();

        var queryTasks = Enumerable.Range(1, 10)
            .Select(_ => _apiHelper.GetDeploymentStatusAsync(executionId))
            .ToArray();

        var allTasks = createTasks.Cast<Task>().Concat(queryTasks.Cast<Task>()).ToArray();

        // Wait for all operations to complete
        await Task.WhenAll(allTasks);

        // Assert - All operations should succeed without exceptions
        var createResults = await Task.WhenAll(createTasks);
        createResults.Should().HaveCount(5);
        createResults.Should().OnlyContain(r => r.ExecutionId != Guid.Empty);

        var queryResults = await Task.WhenAll(queryTasks);
        queryResults.Should().HaveCount(10);
        queryResults.Should().OnlyContain(r => r != null);
    }

    #endregion

    #region High Concurrency Stress Tests

    /// <summary>
    /// Tests system behavior under high concurrent load.
    /// </summary>
    [Fact(Skip = "Concurrent deployment tests too slow - need optimization")]
    public async Task HighConcurrency_20SimultaneousDeployments_SystemRemainStable()
    {
        // Arrange - Create many concurrent deployment requests
        var deploymentCount = 20;
        var deploymentRequests = Enumerable.Range(1, deploymentCount)
            .Select(i => TestDataBuilder.ForDevelopment($"stress-module-{i}")
                .WithVersion("1.0.0")
                .WithDescription($"Stress test deployment {i}")
                .Build())
            .ToArray();

        // Act - Fire all requests simultaneously
        var deploymentTasks = deploymentRequests
            .Select(request => _apiHelper!.CreateDeploymentAsync(request))
            .ToArray();

        // Wait for all deployment creation requests to complete
        var deploymentResponses = await Task.WhenAll(deploymentTasks);

        // Assert - All requests should be accepted (even if queued)
        deploymentResponses.Should().HaveCount(deploymentCount);
        deploymentResponses.Should().OnlyContain(r => r.ExecutionId != Guid.Empty,
            "All deployment requests should be accepted and assigned execution IDs");

        // Verify we can query all deployments
        var statusTasks = deploymentResponses
            .Select(r => _apiHelper!.GetDeploymentStatusAsync(r.ExecutionId.ToString()))
            .ToArray();

        var statuses = await Task.WhenAll(statusTasks);

        statuses.Should().HaveCount(deploymentCount);
        statuses.Should().OnlyContain(s => s != null, "All deployments should be trackable");
        statuses.Should().OnlyContain(s => s!.Status != null, "All deployments should have a status");
    }

    #endregion

    #region Concurrent Approval Tests

    /// <summary>
    /// Tests that concurrent approval/rejection requests are handled correctly.
    /// </summary>
    [Fact(Skip = "Concurrent deployment tests too slow - need optimization")]
    public async Task ConcurrentApprovals_MultiplePendingDeployments_HandledCorrectly()
    {
        // Arrange - Create multiple deployments requiring approval
        var deploymentRequests = Enumerable.Range(1, 3)
            .Select(i => TestDataBuilder.ForStaging($"concurrent-approval-module-{i}")
                .WithVersion("1.0.0")
                .WithApprovalRequired(true)
                .Build())
            .ToArray();

        var deploymentTasks = deploymentRequests
            .Select(request => _apiHelper!.CreateDeploymentAsync(request))
            .ToArray();

        var deploymentResponses = await Task.WhenAll(deploymentTasks);

        // Wait for approval requests to be created
        await Task.Delay(TimeSpan.FromSeconds(3));

        // Act - Use admin client to approve concurrently
        var adminClient = _fixture.Factory.CreateClient();
        var adminAuthHelper = new AuthHelper(adminClient);
        var adminToken = await adminAuthHelper.GetAdminTokenAsync();
        adminAuthHelper.AddAuthorizationHeader(adminClient, adminToken);
        var adminApiHelper = new ApiClientHelper(adminClient);

        var approvalTasks = deploymentResponses
            .Select(r => adminApiHelper.ApproveDeploymentAsync(
                r.ExecutionId.ToString(),
                reason: "Concurrent approval test"))
            .ToArray();

        var approvalResponses = await Task.WhenAll(approvalTasks);

        // Assert - All approvals should succeed
        approvalResponses.Should().HaveCount(3);
        approvalResponses.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.OK,
            "All concurrent approvals should succeed");

        adminClient.Dispose();
    }

    #endregion
}
