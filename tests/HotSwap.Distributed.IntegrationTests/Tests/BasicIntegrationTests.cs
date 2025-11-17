using FluentAssertions;
using HotSwap.Distributed.IntegrationTests.Fixtures;
using HotSwap.Distributed.IntegrationTests.Helpers;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace HotSwap.Distributed.IntegrationTests.Tests;

/// <summary>
/// Basic integration tests for API endpoints: health, authentication, and cluster operations.
/// These tests verify the fundamental API functionality works end-to-end with real dependencies.
/// </summary>
[Collection("IntegrationTests")]
public class BasicIntegrationTests : IClassFixture<PostgreSqlContainerFixture>, IClassFixture<RedisContainerFixture>, IAsyncLifetime
{
    private readonly PostgreSqlContainerFixture _postgreSqlFixture;
    private readonly RedisContainerFixture _redisFixture;
    private IntegrationTestFactory? _factory;
    private HttpClient? _client;
    private AuthHelper? _authHelper;
    private ApiClientHelper? _apiHelper;

    public BasicIntegrationTests(
        PostgreSqlContainerFixture postgreSqlFixture,
        RedisContainerFixture redisFixture)
    {
        _postgreSqlFixture = postgreSqlFixture ?? throw new ArgumentNullException(nameof(postgreSqlFixture));
        _redisFixture = redisFixture ?? throw new ArgumentNullException(nameof(redisFixture));
    }

    public async Task InitializeAsync()
    {
        // Create factory and client for each test
        _factory = new IntegrationTestFactory(_postgreSqlFixture, _redisFixture);
        await _factory.InitializeAsync();

        _client = _factory.CreateClient();
        _authHelper = new AuthHelper(_client);
        _apiHelper = new ApiClientHelper(_client);
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        if (_factory != null)
        {
            await _factory.DisposeAsync();
        }
    }

    #region Health Check Tests

    [Fact]
    public async Task HealthCheck_ReturnsHealthy()
    {
        // Act
        var response = await _client!.GetAsync("/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Be("Healthy");
    }

    #endregion

    #region Authentication Tests

    [Fact]
    public async Task Login_WithValidAdminCredentials_ReturnsToken()
    {
        // Act
        var token = await _authHelper!.GetAdminTokenAsync();

        // Assert
        token.Should().NotBeNullOrEmpty();
        token.Should().MatchRegex(@"^[A-Za-z0-9-_=]+\.[A-Za-z0-9-_=]+\.?[A-Za-z0-9-_.+/=]*$"); // JWT format
    }

    [Fact]
    public async Task Login_WithValidDeployerCredentials_ReturnsToken()
    {
        // Act
        var token = await _authHelper!.GetDeployerTokenAsync();

        // Assert
        token.Should().NotBeNullOrEmpty();
        token.Should().MatchRegex(@"^[A-Za-z0-9-_=]+\.[A-Za-z0-9-_=]+\.?[A-Za-z0-9-_.+/=]*$"); // JWT format
    }

    [Fact]
    public async Task Login_WithValidViewerCredentials_ReturnsToken()
    {
        // Act
        var token = await _authHelper!.GetViewerTokenAsync();

        // Assert
        token.Should().NotBeNullOrEmpty();
        token.Should().MatchRegex(@"^[A-Za-z0-9-_=]+\.[A-Za-z0-9-_=]+\.?[A-Za-z0-9-_.+/=]*$"); // JWT format
    }

    [Fact]
    public async Task AuthenticatedRequest_WithValidToken_Succeeds()
    {
        // Arrange
        var token = await _authHelper!.GetAdminTokenAsync();
        _authHelper.AddAuthorizationHeader(_client!, token);

        // Act
        var clusters = await _apiHelper!.ListClustersAsync();

        // Assert
        clusters.Should().NotBeNull();
        clusters.Should().NotBeEmpty();
    }

    #endregion

    #region Cluster Tests

    [Fact]
    public async Task ListClusters_ReturnsAllEnvironments()
    {
        // Arrange
        var token = await _authHelper!.GetAdminTokenAsync();
        _authHelper.AddAuthorizationHeader(_client!, token);

        // Act
        var clusters = await _apiHelper!.ListClustersAsync();

        // Assert
        clusters.Should().NotBeNull();
        clusters.Should().HaveCountGreaterOrEqualTo(4); // Development, QA, Staging, Production

        var environments = clusters.Select(c => c.Environment).ToList();
        environments.Should().Contain("Development");
        environments.Should().Contain("QA");
        environments.Should().Contain("Staging");
        environments.Should().Contain("Production");
    }

    [Fact]
    public async Task GetClusterInfo_ForDevelopment_ReturnsDetails()
    {
        // Arrange
        var token = await _authHelper!.GetAdminTokenAsync();
        _authHelper.AddAuthorizationHeader(_client!, token);

        // Act
        var clusterInfo = await _apiHelper!.GetClusterInfoAsync("Development");

        // Assert
        clusterInfo.Should().NotBeNull();
        clusterInfo!.Environment.Should().Be("Development");
        clusterInfo.TotalNodes.Should().BeGreaterThan(0);
        clusterInfo.Nodes.Should().NotBeNull();
        clusterInfo.Metrics.Should().NotBeNull();
    }

    #endregion

    #region Authorization Tests

    [Fact]
    public async Task CreateDeployment_WithoutAuth_Returns401()
    {
        // Arrange
        var request = TestDataBuilder.ForDevelopment().Build();

        // Act
        var response = await _client!.PostAsJsonAsync("/api/v1/deployments", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateDeployment_WithViewerRole_Returns403()
    {
        // Arrange
        var token = await _authHelper!.GetViewerTokenAsync();
        _authHelper.AddAuthorizationHeader(_client!, token);
        var request = TestDataBuilder.ForDevelopment().Build();

        // Act
        var response = await _client!.PostAsJsonAsync("/api/v1/deployments", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    #endregion
}
