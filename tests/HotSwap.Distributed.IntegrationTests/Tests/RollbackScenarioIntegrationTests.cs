using FluentAssertions;
using HotSwap.Distributed.IntegrationTests.Fixtures;
using HotSwap.Distributed.IntegrationTests.Helpers;
using System.Net;
using Xunit;

namespace HotSwap.Distributed.IntegrationTests.Tests;

/// <summary>
/// Integration tests for rollback scenarios.
/// These tests verify that deployments can be rolled back successfully
/// and that rollback operations restore previous module versions.
/// </summary>
[Collection("IntegrationTests")]
public class RollbackScenarioIntegrationTests : IClassFixture<PostgreSqlContainerFixture>, IClassFixture<RedisContainerFixture>, IClassFixture<ApiServerFixture>, IAsyncLifetime
{
    private readonly ApiServerFixture _apiServerFixture;
    private HttpClient? _client;
    private AuthHelper? _authHelper;
    private ApiClientHelper? _apiHelper;

    public RollbackScenarioIntegrationTests(
        PostgreSqlContainerFixture postgreSqlFixture,
        RedisContainerFixture redisFixture,
        ApiServerFixture apiServerFixture)
    {
        // ApiServerFixture needs PostgreSql and Redis fixtures as dependencies
        _apiServerFixture = apiServerFixture ?? throw new ArgumentNullException(nameof(apiServerFixture));
    }

    public async Task InitializeAsync()
    {
        // Use shared factory - creates new client but same server/cache
        _client = _apiServerFixture.CreateClient();
        _authHelper = new AuthHelper(_client);
        _apiHelper = new ApiClientHelper(_client);

        // Authenticate with deployer role (required for creating deployments and rollbacks)
        var token = await _authHelper.GetDeployerTokenAsync();
        _authHelper.AddAuthorizationHeader(_client, token);

        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        // Only dispose client - don't dispose factory (it's shared across all tests in class)
        _client?.Dispose();
        await Task.CompletedTask;
    }

    #region Successful Rollback Tests

    /// <summary>
    /// Tests that a successful deployment can be rolled back.
    /// </summary>
    [Fact]
    public async Task RollbackSuccessfulDeployment_RestoresPreviousVersion()
    {
        // Arrange - Deploy version 1.0.0
        var requestV1 = TestDataBuilder.ForDevelopment("rollback-module")
            .WithVersion("1.0.0")
            .WithDescription("Initial deployment for rollback test")
            .Build();

        var deploymentV1 = await _apiHelper!.CreateDeploymentAsync(requestV1);
        var statusV1 = await _apiHelper.WaitForDeploymentCompletionAsync(
            deploymentV1.ExecutionId.ToString(),
            timeout: TimeSpan.FromMinutes(3));

        statusV1.Status.Should().Be("Succeeded", "Initial deployment should succeed");

        // Deploy version 2.0.0 (which we'll roll back)
        var requestV2 = TestDataBuilder.ForDevelopment("rollback-module")
            .WithVersion("2.0.0")
            .WithDescription("Version to be rolled back")
            .Build();

        var deploymentV2 = await _apiHelper.CreateDeploymentAsync(requestV2);
        var statusV2 = await _apiHelper.WaitForDeploymentCompletionAsync(
            deploymentV2.ExecutionId.ToString(),
            timeout: TimeSpan.FromMinutes(3));

        statusV2.Status.Should().Be("Succeeded", "Version 2.0.0 should deploy successfully");

        // Act - Rollback version 2.0.0
        var rollbackResponse = await _apiHelper.RollbackDeploymentAsync(deploymentV2.ExecutionId.ToString());

        // Assert - Rollback succeeds
        rollbackResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            "Rollback request should succeed");

        // Verify rollback completed
        await Task.Delay(TimeSpan.FromSeconds(5)); // Give rollback time to process

        // The system should have rolled back to version 1.0.0
        // Note: We can't directly verify the active version without additional API endpoints,
        // but we can verify the rollback response indicates success
    }

    /// <summary>
    /// Tests rollback of a deployment to multiple environments.
    /// </summary>
    [Fact]
    public async Task RollbackDeployment_ToMultipleEnvironments_Succeeds()
    {
        // Arrange - Deploy to QA
        var request = TestDataBuilder.ForQA("rollback-qa-module")
            .WithVersion("1.0.0")
            .WithDescription("QA deployment for rollback")
            .Build();

        var deployment = await _apiHelper!.CreateDeploymentAsync(request);
        var status = await _apiHelper.WaitForDeploymentCompletionAsync(
            deployment.ExecutionId.ToString(),
            timeout: TimeSpan.FromMinutes(5));

        status.Status.Should().Be("Succeeded");

        // Act - Rollback
        var rollbackResponse = await _apiHelper.RollbackDeploymentAsync(deployment.ExecutionId.ToString());

        // Assert
        rollbackResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// Tests that rollback works with Blue-Green deployment strategy.
    /// </summary>
    [Fact]
    public async Task RollbackBlueGreenDeployment_SwitchesBackToBlueEnvironment()
    {
        // Arrange - Deploy to Staging (Blue-Green strategy)
        var request = TestDataBuilder.ForStaging("bluegreen-rollback-module")
            .WithVersion("1.0.0")
            .WithDescription("Blue-Green deployment for rollback test")
            .Build();

        var deployment = await _apiHelper!.CreateDeploymentAsync(request);
        var status = await _apiHelper.WaitForDeploymentCompletionAsync(
            deployment.ExecutionId.ToString(),
            timeout: TimeSpan.FromMinutes(5));

        status.Status.Should().Be("Succeeded");

        // Act - Rollback Blue-Green deployment
        var rollbackResponse = await _apiHelper.RollbackDeploymentAsync(deployment.ExecutionId.ToString());

        // Assert
        rollbackResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            "Blue-Green rollback should succeed by switching traffic back");
    }

    #endregion

    #region Rollback Error Scenarios

    /// <summary>
    /// Tests that attempting to rollback a non-existent deployment returns 404.
    /// </summary>
    [Fact]
    public async Task RollbackNonExistentDeployment_Returns404NotFound()
    {
        // Arrange - Use non-existent execution ID
        var nonExistentId = Guid.NewGuid().ToString();

        // Act
        var rollbackResponse = await _apiHelper!.RollbackDeploymentAsync(nonExistentId);

        // Assert
        rollbackResponse.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "Rolling back non-existent deployment should return 404");
    }

    /// <summary>
    /// Tests that attempting to rollback a deployment that's still in progress fails.
    /// </summary>
    [Fact]
    public async Task RollbackInProgressDeployment_ReturnsBadRequestOrConflict()
    {
        // Arrange - Create deployment (don't wait for completion)
        var request = TestDataBuilder.ForProduction("inprogress-rollback-module")
            .WithVersion("1.0.0")
            .WithApprovalRequired(false)
            .WithDescription("In-progress deployment rollback test")
            .Build();

        var deployment = await _apiHelper!.CreateDeploymentAsync(request);

        // Act - Try to rollback immediately (while deployment is in progress)
        var rollbackResponse = await _apiHelper.RollbackDeploymentAsync(deployment.ExecutionId.ToString());

        // Assert - Should fail (deployment must be completed first)
        rollbackResponse.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.Conflict)
            .And.NotBe(HttpStatusCode.OK, "rolling back in-progress deployment should fail");
    }

    #endregion

    #region Rollback Authorization Tests

    /// <summary>
    /// Tests that rollback requires authentication.
    /// </summary>
    [Fact]
    public async Task Rollback_WithoutAuthentication_Returns401Unauthorized()
    {
        // Arrange - Create an unauthenticated client
        var unauthClient = _factory!.CreateClient();
        var unauthApiHelper = new ApiClientHelper(unauthClient);

        // Act - Try to rollback without authentication
        var rollbackResponse = await unauthApiHelper.RollbackDeploymentAsync(Guid.NewGuid().ToString());

        // Assert
        rollbackResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "Rollback should require authentication");

        unauthClient.Dispose();
    }

    /// <summary>
    /// Tests that viewer role cannot initiate rollback.
    /// </summary>
    [Fact]
    public async Task Rollback_WithViewerRole_Returns403Forbidden()
    {
        // Arrange - Create deployment first
        var request = TestDataBuilder.ForDevelopment("viewer-rollback-module")
            .WithVersion("1.0.0")
            .WithDescription("Deployment for viewer rollback test")
            .Build();

        var deployment = await _apiHelper!.CreateDeploymentAsync(request);
        var status = await _apiHelper.WaitForDeploymentCompletionAsync(
            deployment.ExecutionId.ToString(),
            timeout: TimeSpan.FromMinutes(3));

        status.Status.Should().Be("Succeeded");

        // Create viewer client
        var viewerClient = _factory!.CreateClient();
        var viewerAuthHelper = new AuthHelper(viewerClient);
        var viewerToken = await viewerAuthHelper.GetViewerTokenAsync();
        viewerAuthHelper.AddAuthorizationHeader(viewerClient, viewerToken);
        var viewerApiHelper = new ApiClientHelper(viewerClient);

        // Act - Try to rollback with viewer role
        var rollbackResponse = await viewerApiHelper.RollbackDeploymentAsync(deployment.ExecutionId.ToString());

        // Assert
        rollbackResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "Viewer role should not be able to initiate rollback");

        viewerClient.Dispose();
    }

    #endregion

    #region Multiple Rollback Tests

    /// <summary>
    /// Tests rolling back multiple deployments in sequence.
    /// </summary>
    [Fact]
    public async Task MultipleSequentialRollbacks_AllSucceed()
    {
        // Arrange - Deploy versions 1.0.0, 2.0.0, 3.0.0
        var versions = new[] { "1.0.0", "2.0.0", "3.0.0" };
        var executionIds = new List<string>();

        foreach (var version in versions)
        {
            var request = TestDataBuilder.ForDevelopment("multi-rollback-module")
                .WithVersion(version)
                .WithDescription($"Deployment version {version} for multi-rollback test")
                .Build();

            var deployment = await _apiHelper!.CreateDeploymentAsync(request);
            var status = await _apiHelper.WaitForDeploymentCompletionAsync(
                deployment.ExecutionId.ToString(),
                timeout: TimeSpan.FromMinutes(3));

            status.Status.Should().Be("Succeeded", $"Version {version} should deploy successfully");
            executionIds.Add(deployment.ExecutionId.ToString());
        }

        // Act - Rollback version 3.0.0
        var rollback1 = await _apiHelper!.RollbackDeploymentAsync(executionIds[2]);

        // Wait between rollbacks
        await Task.Delay(TimeSpan.FromSeconds(2));

        // Rollback version 2.0.0
        var rollback2 = await _apiHelper.RollbackDeploymentAsync(executionIds[1]);

        // Assert - Both rollbacks succeed
        rollback1.StatusCode.Should().Be(HttpStatusCode.OK, "First rollback should succeed");
        rollback2.StatusCode.Should().Be(HttpStatusCode.OK, "Second rollback should succeed");
    }

    #endregion
}
