using System.Security.Claims;
using FluentAssertions;
using HotSwap.Distributed.Api.Controllers;
using HotSwap.Distributed.Api.Models;
using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace HotSwap.Distributed.Tests.Api;

public class TenantDeploymentsControllerTests
{
    private readonly Mock<ITenantDeploymentService> _mockDeploymentService;
    private readonly Mock<ITenantContextService> _mockTenantContext;
    private readonly TenantDeploymentsController _controller;
    private readonly Guid _testTenantId = Guid.NewGuid();

    public TenantDeploymentsControllerTests()
    {
        _mockDeploymentService = new Mock<ITenantDeploymentService>();
        _mockTenantContext = new Mock<ITenantContextService>();
        _controller = new TenantDeploymentsController(
            _mockDeploymentService.Object,
            _mockTenantContext.Object,
            NullLogger<TenantDeploymentsController>.Instance);

        // Set up user identity
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Name, "testuser@example.com")
        }, "TestAuthentication"));

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };
    }

    private TenantDeploymentResult CreateTestDeploymentResult(
        Guid? deploymentId = null,
        bool success = true)
    {
        return new TenantDeploymentResult
        {
            DeploymentId = deploymentId ?? Guid.NewGuid(),
            TenantId = _testTenantId,
            Success = success,
            Message = success ? "Deployment successful" : "Deployment failed",
            AffectedWebsites = new List<Guid> { Guid.NewGuid() },
            StartTime = DateTime.UtcNow.AddMinutes(-5),
            EndTime = DateTime.UtcNow,
            Errors = success ? new List<string>() : new List<string> { "Error message" }
        };
    }

    #region DeployTheme Tests

    [Fact]
    public async Task DeployTheme_WithValidRequest_ReturnsOk()
    {
        // Arrange
        var themeId = Guid.NewGuid();
        var websiteId = Guid.NewGuid();
        var request = new DeployThemeRequest
        {
            ThemeId = themeId,
            WebsiteId = websiteId
        };

        var deploymentResult = CreateTestDeploymentResult();

        _mockTenantContext.Setup(x => x.GetCurrentTenantId())
            .Returns(_testTenantId);

        _mockDeploymentService.Setup(x => x.DeployAsync(
                It.IsAny<TenantDeploymentRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(deploymentResult);

        // Act
        var result = await _controller.DeployTheme(request, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<TenantDeploymentResultResponse>().Subject;
        response.Success.Should().BeTrue();
        response.TenantId.Should().Be(_testTenantId);
        response.DeploymentId.Should().Be(deploymentResult.DeploymentId);
    }

    [Fact]
    public async Task DeployTheme_WithNoTenantContext_ReturnsBadRequest()
    {
        // Arrange
        var request = new DeployThemeRequest
        {
            ThemeId = Guid.NewGuid()
        };

        _mockTenantContext.Setup(x => x.GetCurrentTenantId())
            .Returns((Guid?)null);

        // Act
        var result = await _controller.DeployTheme(request, CancellationToken.None);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var errorResponse = badRequestResult.Value.Should().BeOfType<ErrorResponse>().Subject;
        errorResponse.Error.Should().Be("Tenant context required");
    }

    [Fact]
    public async Task DeployTheme_WithSpecificWebsite_UsesSingleWebsiteScope()
    {
        // Arrange
        var websiteId = Guid.NewGuid();
        var request = new DeployThemeRequest
        {
            ThemeId = Guid.NewGuid(),
            WebsiteId = websiteId
        };

        var deploymentResult = CreateTestDeploymentResult();

        _mockTenantContext.Setup(x => x.GetCurrentTenantId())
            .Returns(_testTenantId);

        TenantDeploymentRequest? capturedRequest = null;
        _mockDeploymentService.Setup(x => x.DeployAsync(
                It.IsAny<TenantDeploymentRequest>(),
                It.IsAny<CancellationToken>()))
            .Callback<TenantDeploymentRequest, CancellationToken>((req, ct) => capturedRequest = req)
            .ReturnsAsync(deploymentResult);

        // Act
        await _controller.DeployTheme(request, CancellationToken.None);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Scope.Should().Be(DeploymentScope.SingleWebsite);
        capturedRequest.WebsiteId.Should().Be(websiteId);
        capturedRequest.ModuleType.Should().Be(WebsiteModuleType.Theme);
    }

    [Fact]
    public async Task DeployTheme_WithNoWebsiteId_UsesAllTenantWebsitesScope()
    {
        // Arrange
        var request = new DeployThemeRequest
        {
            ThemeId = Guid.NewGuid(),
            WebsiteId = null
        };

        var deploymentResult = CreateTestDeploymentResult();

        _mockTenantContext.Setup(x => x.GetCurrentTenantId())
            .Returns(_testTenantId);

        TenantDeploymentRequest? capturedRequest = null;
        _mockDeploymentService.Setup(x => x.DeployAsync(
                It.IsAny<TenantDeploymentRequest>(),
                It.IsAny<CancellationToken>()))
            .Callback<TenantDeploymentRequest, CancellationToken>((req, ct) => capturedRequest = req)
            .ReturnsAsync(deploymentResult);

        // Act
        await _controller.DeployTheme(request, CancellationToken.None);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Scope.Should().Be(DeploymentScope.AllTenantWebsites);
        capturedRequest.WebsiteId.Should().BeNull();
    }

    #endregion

    #region DeployPlugin Tests

    [Fact]
    public async Task DeployPlugin_WithValidRequest_ReturnsOk()
    {
        // Arrange
        var pluginId = Guid.NewGuid();
        var websiteId = Guid.NewGuid();
        var request = new DeployPluginRequest
        {
            PluginId = pluginId,
            WebsiteId = websiteId
        };

        var deploymentResult = CreateTestDeploymentResult();

        _mockTenantContext.Setup(x => x.GetCurrentTenantId())
            .Returns(_testTenantId);

        _mockDeploymentService.Setup(x => x.DeployAsync(
                It.IsAny<TenantDeploymentRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(deploymentResult);

        // Act
        var result = await _controller.DeployPlugin(request, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<TenantDeploymentResultResponse>().Subject;
        response.Success.Should().BeTrue();
        response.TenantId.Should().Be(_testTenantId);
    }

    [Fact]
    public async Task DeployPlugin_WithNoTenantContext_ReturnsBadRequest()
    {
        // Arrange
        var request = new DeployPluginRequest
        {
            PluginId = Guid.NewGuid()
        };

        _mockTenantContext.Setup(x => x.GetCurrentTenantId())
            .Returns((Guid?)null);

        // Act
        var result = await _controller.DeployPlugin(request, CancellationToken.None);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var errorResponse = badRequestResult.Value.Should().BeOfType<ErrorResponse>().Subject;
        errorResponse.Error.Should().Be("Tenant context required");
    }

    [Fact]
    public async Task DeployPlugin_WithSpecificWebsite_UsesSingleWebsiteScope()
    {
        // Arrange
        var websiteId = Guid.NewGuid();
        var request = new DeployPluginRequest
        {
            PluginId = Guid.NewGuid(),
            WebsiteId = websiteId
        };

        var deploymentResult = CreateTestDeploymentResult();

        _mockTenantContext.Setup(x => x.GetCurrentTenantId())
            .Returns(_testTenantId);

        TenantDeploymentRequest? capturedRequest = null;
        _mockDeploymentService.Setup(x => x.DeployAsync(
                It.IsAny<TenantDeploymentRequest>(),
                It.IsAny<CancellationToken>()))
            .Callback<TenantDeploymentRequest, CancellationToken>((req, ct) => capturedRequest = req)
            .ReturnsAsync(deploymentResult);

        // Act
        await _controller.DeployPlugin(request, CancellationToken.None);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Scope.Should().Be(DeploymentScope.SingleWebsite);
        capturedRequest.WebsiteId.Should().Be(websiteId);
        capturedRequest.ModuleType.Should().Be(WebsiteModuleType.Plugin);
    }

    [Fact]
    public async Task DeployPlugin_WithNoWebsiteId_UsesAllTenantWebsitesScope()
    {
        // Arrange
        var request = new DeployPluginRequest
        {
            PluginId = Guid.NewGuid(),
            WebsiteId = null
        };

        var deploymentResult = CreateTestDeploymentResult();

        _mockTenantContext.Setup(x => x.GetCurrentTenantId())
            .Returns(_testTenantId);

        TenantDeploymentRequest? capturedRequest = null;
        _mockDeploymentService.Setup(x => x.DeployAsync(
                It.IsAny<TenantDeploymentRequest>(),
                It.IsAny<CancellationToken>()))
            .Callback<TenantDeploymentRequest, CancellationToken>((req, ct) => capturedRequest = req)
            .ReturnsAsync(deploymentResult);

        // Act
        await _controller.DeployPlugin(request, CancellationToken.None);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Scope.Should().Be(DeploymentScope.AllTenantWebsites);
        capturedRequest.WebsiteId.Should().BeNull();
    }

    #endregion

    #region GetDeploymentStatus Tests

    [Fact]
    public async Task GetDeploymentStatus_WithExistingDeployment_ReturnsOk()
    {
        // Arrange
        var deploymentId = Guid.NewGuid();
        var deploymentResult = CreateTestDeploymentResult(deploymentId);

        _mockDeploymentService.Setup(x => x.GetDeploymentStatusAsync(
                deploymentId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(deploymentResult);

        // Act
        var result = await _controller.GetDeploymentStatus(deploymentId, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<TenantDeploymentResultResponse>().Subject;
        response.DeploymentId.Should().Be(deploymentId);
        response.Success.Should().BeTrue();
    }

    [Fact]
    public async Task GetDeploymentStatus_WithNonExistentDeployment_ReturnsNotFound()
    {
        // Arrange
        var deploymentId = Guid.NewGuid();

        _mockDeploymentService.Setup(x => x.GetDeploymentStatusAsync(
                deploymentId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((TenantDeploymentResult?)null);

        // Act
        var result = await _controller.GetDeploymentStatus(deploymentId, CancellationToken.None);

        // Assert
        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        var errorResponse = notFoundResult.Value.Should().BeOfType<ErrorResponse>().Subject;
        errorResponse.Error.Should().Be("Deployment not found");
    }

    #endregion

    #region ListDeployments Tests

    [Fact]
    public async Task ListDeployments_WithValidTenantContext_ReturnsDeployments()
    {
        // Arrange
        var deployments = new List<TenantDeploymentResult>
        {
            CreateTestDeploymentResult(),
            CreateTestDeploymentResult()
        };

        _mockTenantContext.Setup(x => x.GetCurrentTenantId())
            .Returns(_testTenantId);

        _mockDeploymentService.Setup(x => x.GetDeploymentsForTenantAsync(
                _testTenantId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(deployments);

        // Act
        var result = await _controller.ListDeployments(CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var responses = okResult.Value.Should().BeOfType<List<TenantDeploymentResultResponse>>().Subject;
        responses.Should().HaveCount(2);
        responses.Should().AllSatisfy(r => r.TenantId.Should().Be(_testTenantId));
    }

    [Fact]
    public async Task ListDeployments_WithNoTenantContext_ReturnsBadRequest()
    {
        // Arrange
        _mockTenantContext.Setup(x => x.GetCurrentTenantId())
            .Returns((Guid?)null);

        // Act
        var result = await _controller.ListDeployments(CancellationToken.None);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var errorResponse = badRequestResult.Value.Should().BeOfType<ErrorResponse>().Subject;
        errorResponse.Error.Should().Be("Tenant context required");
    }

    [Fact]
    public async Task ListDeployments_WithEmptyList_ReturnsEmptyList()
    {
        // Arrange
        _mockTenantContext.Setup(x => x.GetCurrentTenantId())
            .Returns(_testTenantId);

        _mockDeploymentService.Setup(x => x.GetDeploymentsForTenantAsync(
                _testTenantId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TenantDeploymentResult>());

        // Act
        var result = await _controller.ListDeployments(CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var responses = okResult.Value.Should().BeOfType<List<TenantDeploymentResultResponse>>().Subject;
        responses.Should().BeEmpty();
    }

    #endregion

    #region RollbackDeployment Tests

    [Fact]
    public async Task RollbackDeployment_WithExistingDeployment_ReturnsOk()
    {
        // Arrange
        var deploymentId = Guid.NewGuid();

        _mockDeploymentService.Setup(x => x.RollbackDeploymentAsync(
                deploymentId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.RollbackDeployment(deploymentId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockDeploymentService.Verify(x => x.RollbackDeploymentAsync(
            deploymentId,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RollbackDeployment_WithNonExistentDeployment_ReturnsNotFound()
    {
        // Arrange
        var deploymentId = Guid.NewGuid();

        _mockDeploymentService.Setup(x => x.RollbackDeploymentAsync(
                deploymentId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.RollbackDeployment(deploymentId, CancellationToken.None);

        // Assert
        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        var errorResponse = notFoundResult.Value.Should().BeOfType<ErrorResponse>().Subject;
        errorResponse.Error.Should().Be("Deployment not found");
    }

    #endregion
}
