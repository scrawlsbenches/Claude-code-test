using FluentAssertions;
using HotSwap.Distributed.IntegrationTests.Fixtures;
using HotSwap.Distributed.IntegrationTests.Helpers;
using System.Net;
using Xunit;

namespace HotSwap.Distributed.IntegrationTests.Tests;

/// <summary>
/// Integration tests for approval workflow.
/// These tests verify that deployments requiring approval work correctly,
/// including approval, rejection, and timeout scenarios.
/// </summary>
[Collection("IntegrationTests")]
public class ApprovalWorkflowIntegrationTests : IClassFixture<PostgreSqlContainerFixture>, IClassFixture<RedisContainerFixture>, IAsyncLifetime
{
    private readonly PostgreSqlContainerFixture _postgreSqlFixture;
    private readonly RedisContainerFixture _redisFixture;
    private IntegrationTestFactory? _factory;
    private HttpClient? _client;
    private HttpClient? _adminClient;
    private AuthHelper? _authHelper;
    private ApiClientHelper? _apiHelper;
    private ApiClientHelper? _adminApiHelper;

    public ApprovalWorkflowIntegrationTests(
        PostgreSqlContainerFixture postgreSqlFixture,
        RedisContainerFixture redisFixture)
    {
        _postgreSqlFixture = postgreSqlFixture ?? throw new ArgumentNullException(nameof(postgreSqlFixture));
        _redisFixture = redisFixture ?? throw new ArgumentNullException(nameof(redisFixture));
    }

    public async Task InitializeAsync()
    {
        // Create factory and clients for each test
        _factory = new IntegrationTestFactory(_postgreSqlFixture, _redisFixture);
        await _factory.InitializeAsync();

        // Create deployer client (for creating deployments)
        _client = _factory.CreateClient();
        _authHelper = new AuthHelper(_client);
        var deployerToken = await _authHelper.GetDeployerTokenAsync();
        _authHelper.AddAuthorizationHeader(_client, deployerToken);
        _apiHelper = new ApiClientHelper(_client);

        // Create admin client (for approving/rejecting deployments)
        _adminClient = _factory.CreateClient();
        var adminAuthHelper = new AuthHelper(_adminClient);
        var adminToken = await adminAuthHelper.GetAdminTokenAsync();
        adminAuthHelper.AddAuthorizationHeader(_adminClient, adminToken);
        _adminApiHelper = new ApiClientHelper(_adminClient);
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        _adminClient?.Dispose();
        if (_factory != null)
        {
            await _factory.DisposeAsync();
        }
    }

    #region Approval Creation Tests

    /// <summary>
    /// Tests that deployment requiring approval creates a pending approval request.
    /// </summary>
    [Fact]
    public async Task Deployment_RequiringApproval_CreatesPendingApprovalRequest()
    {
        // Arrange
        var request = TestDataBuilder.ForStaging("approval-required-module")
            .WithVersion("1.0.0")
            .WithApprovalRequired(true)
            .WithRequester("deployer@example.com")
            .WithDescription("Deployment requiring approval")
            .Build();

        // Act
        var deploymentResponse = await _apiHelper!.CreateDeploymentAsync(request);

        // Give the system a moment to create approval request
        await Task.Delay(TimeSpan.FromSeconds(2));

        // Check deployment status
        var deploymentStatus = await _apiHelper.GetDeploymentStatusAsync(deploymentResponse.ExecutionId.ToString());

        // Assert
        deploymentResponse.Should().NotBeNull();
        deploymentResponse.Status.Should().Be("Running");
        deploymentResponse.ExecutionId.Should().NotBeEmpty();

        deploymentStatus.Should().NotBeNull();
        deploymentStatus!.Status.Should().Be("PendingApproval",
            "Deployment requiring approval should be in PendingApproval status");

        // Verify approval stage exists
        var approvalStage = deploymentStatus.Stages.FirstOrDefault(s => s.Name == "Approval");
        approvalStage.Should().NotBeNull("Approval stage should exist for deployment requiring approval");
        approvalStage!.Status.Should().Be("Pending", "Approval should be pending");
    }

    #endregion

    #region Approval Tests

    /// <summary>
    /// Tests that approving a pending deployment allows it to proceed.
    /// </summary>
    [Fact]
    public async Task ApprovePendingDeployment_AllowsDeploymentToProceed_AndCompletes()
    {
        // Arrange - Create deployment requiring approval
        var request = TestDataBuilder.ForStaging("approve-test-module")
            .WithVersion("1.0.0")
            .WithApprovalRequired(true)
            .WithRequester("deployer@example.com")
            .WithDescription("Test deployment approval workflow")
            .Build();

        var deploymentResponse = await _apiHelper!.CreateDeploymentAsync(request);
        var executionId = deploymentResponse.ExecutionId.ToString();

        // Wait for approval request to be created
        await Task.Delay(TimeSpan.FromSeconds(2));

        // Act - Approve the deployment (using admin client)
        var approvalResponse = await _adminApiHelper!.ApproveDeploymentAsync(
            executionId,
            reason: "Approved for integration testing");

        // Assert - Approval succeeds
        approvalResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            "Approval request should succeed");

        // Wait for deployment to complete after approval
        var finalStatus = await _apiHelper.WaitForDeploymentCompletionAsync(
            executionId,
            timeout: TimeSpan.FromSeconds(90));

        // Assert - Deployment completes successfully
        finalStatus.Should().NotBeNull();
        finalStatus.Status.Should().Be("Succeeded",
            "Approved deployment should complete successfully");

        // Verify approval stage completed
        var approvalStage = finalStatus.Stages.FirstOrDefault(s => s.Name == "Approval");
        approvalStage.Should().NotBeNull();
        approvalStage!.Status.Should().Be("Succeeded", "Approval stage should be completed");

        // Verify deployment stage executed
        var deploymentStage = finalStatus.Stages.FirstOrDefault(s => s.Name == "Deploy to Staging");
        deploymentStage.Should().NotBeNull();
        deploymentStage!.Status.Should().Be("Succeeded", "Deployment should execute after approval");
    }

    /// <summary>
    /// Tests that only users with Admin role can approve deployments.
    /// </summary>
    [Fact]
    public async Task ApproveDeployment_WithDeployerRole_Returns403Forbidden()
    {
        // Arrange - Create deployment requiring approval
        var request = TestDataBuilder.ForStaging("approve-permission-module")
            .WithVersion("1.0.0")
            .WithApprovalRequired(true)
            .WithRequester("deployer@example.com")
            .Build();

        var deploymentResponse = await _apiHelper!.CreateDeploymentAsync(request);
        var executionId = deploymentResponse.ExecutionId.ToString();

        await Task.Delay(TimeSpan.FromSeconds(2));

        // Act - Try to approve with deployer role (should fail)
        var approvalResponse = await _apiHelper.ApproveDeploymentAsync(
            executionId,
            reason: "Attempting approval with deployer role");

        // Assert
        approvalResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "Only Admin role should be able to approve deployments");
    }

    #endregion

    #region Rejection Tests

    /// <summary>
    /// Tests that rejecting a pending deployment cancels it.
    /// </summary>
    [Fact]
    public async Task RejectPendingDeployment_CancelsDeployment_AndStopsExecution()
    {
        // Arrange - Create deployment requiring approval
        var request = TestDataBuilder.ForStaging("reject-test-module")
            .WithVersion("1.0.0")
            .WithApprovalRequired(true)
            .WithRequester("deployer@example.com")
            .WithDescription("Test deployment rejection workflow")
            .Build();

        var deploymentResponse = await _apiHelper!.CreateDeploymentAsync(request);
        var executionId = deploymentResponse.ExecutionId.ToString();

        // Wait for approval request to be created
        await Task.Delay(TimeSpan.FromSeconds(2));

        // Act - Reject the deployment (using admin client)
        var rejectionResponse = await _adminApiHelper!.RejectDeploymentAsync(
            executionId,
            reason: "Rejected for integration testing");

        // Assert - Rejection succeeds
        rejectionResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            "Rejection request should succeed");

        // Wait a moment for rejection to be processed
        await Task.Delay(TimeSpan.FromSeconds(2));

        // Check final deployment status
        var finalStatus = await _apiHelper.GetDeploymentStatusAsync(executionId);

        // Assert - Deployment is cancelled/failed
        finalStatus.Should().NotBeNull();
        finalStatus!.Status.Should().BeOneOf("Cancelled", "Failed",
            "Rejected deployment should be cancelled or failed");

        // Verify approval stage shows rejection
        var approvalStage = finalStatus.Stages.FirstOrDefault(s => s.Name == "Approval");
        approvalStage.Should().NotBeNull();
        approvalStage!.Status.Should().BeOneOf("Rejected", "Failed",
            "Approval stage should show rejection");

        // Verify deployment stage did NOT execute
        var deploymentStage = finalStatus.Stages.FirstOrDefault(s => s.Name == "Deploy to Staging");
        if (deploymentStage != null)
        {
            deploymentStage.Status.Should().NotBe("Succeeded",
                "Deployment should not complete after rejection");
        }
    }

    /// <summary>
    /// Tests that only users with Admin role can reject deployments.
    /// </summary>
    [Fact]
    public async Task RejectDeployment_WithDeployerRole_Returns403Forbidden()
    {
        // Arrange - Create deployment requiring approval
        var request = TestDataBuilder.ForStaging("reject-permission-module")
            .WithVersion("1.0.0")
            .WithApprovalRequired(true)
            .WithRequester("deployer@example.com")
            .Build();

        var deploymentResponse = await _apiHelper!.CreateDeploymentAsync(request);
        var executionId = deploymentResponse.ExecutionId.ToString();

        await Task.Delay(TimeSpan.FromSeconds(2));

        // Act - Try to reject with deployer role (should fail)
        var rejectionResponse = await _apiHelper.RejectDeploymentAsync(
            executionId,
            reason: "Attempting rejection with deployer role");

        // Assert
        rejectionResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "Only Admin role should be able to reject deployments");
    }

    #endregion

    #region Multiple Approval Tests

    /// <summary>
    /// Tests that multiple deployments can be approved independently.
    /// </summary>
    [Fact]
    public async Task MultipleDeployments_RequiringApproval_CanBeApprovedIndependently()
    {
        // Arrange - Create two deployments requiring approval
        var request1 = TestDataBuilder.ForStaging("multi-approve-module-1")
            .WithVersion("1.0.0")
            .WithApprovalRequired(true)
            .WithDescription("First deployment for multi-approval test")
            .Build();

        var request2 = TestDataBuilder.ForStaging("multi-approve-module-2")
            .WithVersion("1.0.0")
            .WithApprovalRequired(true)
            .WithDescription("Second deployment for multi-approval test")
            .Build();

        var deployment1 = await _apiHelper!.CreateDeploymentAsync(request1);
        var deployment2 = await _apiHelper.CreateDeploymentAsync(request2);

        await Task.Delay(TimeSpan.FromSeconds(2));

        // Act - Approve first deployment, reject second
        await _adminApiHelper!.ApproveDeploymentAsync(
            deployment1.ExecutionId.ToString(),
            reason: "Approving first deployment");

        await _adminApiHelper.RejectDeploymentAsync(
            deployment2.ExecutionId.ToString(),
            reason: "Rejecting second deployment");

        // Wait for first deployment to complete
        var status1 = await _apiHelper.WaitForDeploymentCompletionAsync(
            deployment1.ExecutionId.ToString(),
            timeout: TimeSpan.FromSeconds(90));

        await Task.Delay(TimeSpan.FromSeconds(2));
        var status2 = await _apiHelper.GetDeploymentStatusAsync(deployment2.ExecutionId.ToString());

        // Assert - First deployment succeeded, second was cancelled
        status1.Status.Should().Be("Succeeded", "Approved deployment should complete");
        status2!.Status.Should().BeOneOf("Cancelled", "Failed", "Rejected deployment should not complete");
    }

    #endregion

    #region Approval Without Requirement Tests

    /// <summary>
    /// Tests that deployments not requiring approval proceed immediately without approval stage.
    /// </summary>
    [Fact]
    public async Task Deployment_NotRequiringApproval_ProceedsImmediately_WithoutApprovalStage()
    {
        // Arrange
        var request = TestDataBuilder.ForDevelopment("no-approval-module")
            .WithVersion("1.0.0")
            .WithApprovalRequired(false)
            .WithDescription("Deployment not requiring approval")
            .Build();

        // Act
        var deploymentResponse = await _apiHelper!.CreateDeploymentAsync(request);
        var finalStatus = await _apiHelper.WaitForDeploymentCompletionAsync(
            deploymentResponse.ExecutionId.ToString(),
            timeout: TimeSpan.FromMinutes(3));

        // Assert
        finalStatus.Status.Should().Be("Succeeded", "Deployment should complete without approval");

        // Verify no approval stage exists
        var approvalStage = finalStatus.Stages.FirstOrDefault(s => s.Name == "Approval");
        approvalStage.Should().BeNull("Deployment not requiring approval should not have approval stage");

        // Verify deployment executed directly
        var deploymentStage = finalStatus.Stages.FirstOrDefault(s => s.Name == "Deploy to Development");
        deploymentStage.Should().NotBeNull();
        deploymentStage!.Status.Should().Be("Succeeded");
    }

    #endregion
}
