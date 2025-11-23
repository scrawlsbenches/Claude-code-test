using FluentAssertions;
using HotSwap.Distributed.Api.Controllers;
using HotSwap.Distributed.Api.Models;
using HotSwap.Distributed.Infrastructure.Data.Entities;
using HotSwap.Distributed.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace HotSwap.Distributed.Tests.Api;

public class AuditLogsControllerTests
{
    private readonly Mock<IAuditLogService> _mockAuditLogService;
    private readonly AuditLogsController _controller;
    private readonly AuditLogsController _controllerWithoutService;

    public AuditLogsControllerTests()
    {
        _mockAuditLogService = new Mock<IAuditLogService>();
        _controller = new AuditLogsController(
            NullLogger<AuditLogsController>.Instance,
            _mockAuditLogService.Object);

        // Controller without service to test 503 scenarios
        _controllerWithoutService = new AuditLogsController(
            NullLogger<AuditLogsController>.Instance,
            null);
    }

    private AuditLog CreateTestAuditLog(long id = 1, string category = "Deployment")
    {
        return new AuditLog
        {
            Id = id,
            EventId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            EventType = "DeploymentStarted",
            EventCategory = category,
            Severity = "Info",
            Action = "Create",
            Result = "Success",
            Message = "Test audit log",
            TraceId = "test-trace-id"
        };
    }

    private DeploymentAuditEvent CreateTestDeploymentEvent(Guid executionId)
    {
        return new DeploymentAuditEvent
        {
            Id = 1,
            AuditLogId = 1,
            DeploymentExecutionId = executionId,
            ModuleName = "test-module",
            ModuleVersion = "1.0.0",
            TargetEnvironment = "Production",
            DeploymentStrategy = "Rolling",
            PipelineStage = "Deploy",
            StageStatus = "Succeeded"
        };
    }

    private ApprovalAuditEvent CreateTestApprovalEvent(Guid executionId)
    {
        return new ApprovalAuditEvent
        {
            Id = 1,
            AuditLogId = 1,
            ApprovalId = Guid.NewGuid(),
            DeploymentExecutionId = executionId,
            ModuleName = "test-module",
            ModuleVersion = "1.0.0",
            TargetEnvironment = "Production",
            RequesterEmail = "requester@test.com",
            ApprovalStatus = "Approved",
            TimeoutAt = DateTime.UtcNow.AddHours(1)
        };
    }

    private AuthenticationAuditEvent CreateTestAuthenticationEvent(string username = "testuser")
    {
        return new AuthenticationAuditEvent
        {
            Id = 1,
            AuditLogId = 1,
            UserId = Guid.NewGuid(),
            Username = username,
            AuthenticationMethod = "JWT",
            AuthenticationResult = "Success",
            TokenIssued = true,
            SourceIp = "192.168.1.1"
        };
    }

    #region GetAuditLogsByCategory Tests

    [Fact]
    public async Task GetAuditLogsByCategory_WithValidParameters_ReturnsOk()
    {
        // Arrange
        var auditLogs = new List<AuditLog>
        {
            CreateTestAuditLog(1, "Deployment"),
            CreateTestAuditLog(2, "Deployment")
        };

        _mockAuditLogService.Setup(x => x.GetAuditLogsByCategoryAsync(
                "Deployment",
                1,
                50,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(auditLogs);

        // Act
        var result = await _controller.GetAuditLogsByCategory("Deployment");

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedLogs = okResult.Value.Should().BeOfType<List<AuditLog>>().Subject;
        returnedLogs.Should().HaveCount(2);
        returnedLogs.Should().OnlyContain(l => l.EventCategory == "Deployment");
    }

    [Fact]
    public async Task GetAuditLogsByCategory_WithCustomPagination_UsesProvidedValues()
    {
        // Arrange
        var auditLogs = new List<AuditLog> { CreateTestAuditLog() };

        _mockAuditLogService.Setup(x => x.GetAuditLogsByCategoryAsync(
                "Deployment",
                3,
                25,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(auditLogs);

        // Act
        var result = await _controller.GetAuditLogsByCategory("Deployment", 3, 25);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockAuditLogService.Verify(x => x.GetAuditLogsByCategoryAsync(
            "Deployment",
            3,
            25,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAuditLogsByCategory_WithDefaultPagination_UsesDefaults()
    {
        // Arrange
        var auditLogs = new List<AuditLog> { CreateTestAuditLog() };

        _mockAuditLogService.Setup(x => x.GetAuditLogsByCategoryAsync(
                "Deployment",
                1,
                50,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(auditLogs);

        // Act
        var result = await _controller.GetAuditLogsByCategory("Deployment");

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockAuditLogService.Verify(x => x.GetAuditLogsByCategoryAsync(
            "Deployment",
            1,
            50,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAuditLogsByCategory_WithNullCategory_ReturnsBadRequest()
    {
        // Act
        var result = await _controller.GetAuditLogsByCategory(null!);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var errorResponse = badRequestResult.Value.Should().BeOfType<ErrorResponse>().Subject;
        errorResponse.Error.Should().Be("Category parameter is required");
    }

    [Fact]
    public async Task GetAuditLogsByCategory_WithEmptyCategory_ReturnsBadRequest()
    {
        // Act
        var result = await _controller.GetAuditLogsByCategory("");

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var errorResponse = badRequestResult.Value.Should().BeOfType<ErrorResponse>().Subject;
        errorResponse.Error.Should().Be("Category parameter is required");
    }

    [Fact]
    public async Task GetAuditLogsByCategory_WithWhitespaceCategory_ReturnsBadRequest()
    {
        // Act
        var result = await _controller.GetAuditLogsByCategory("   ");

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var errorResponse = badRequestResult.Value.Should().BeOfType<ErrorResponse>().Subject;
        errorResponse.Error.Should().Be("Category parameter is required");
    }

    [Fact]
    public async Task GetAuditLogsByCategory_WithInvalidPageNumber_ReturnsBadRequest()
    {
        // Act
        var result = await _controller.GetAuditLogsByCategory("Deployment", 0);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var errorResponse = badRequestResult.Value.Should().BeOfType<ErrorResponse>().Subject;
        errorResponse.Error.Should().Be("Page number must be greater than 0");
    }

    [Fact]
    public async Task GetAuditLogsByCategory_WithNegativePageNumber_ReturnsBadRequest()
    {
        // Act
        var result = await _controller.GetAuditLogsByCategory("Deployment", -1);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var errorResponse = badRequestResult.Value.Should().BeOfType<ErrorResponse>().Subject;
        errorResponse.Error.Should().Be("Page number must be greater than 0");
    }

    [Fact]
    public async Task GetAuditLogsByCategory_WithPageSizeTooSmall_ReturnsBadRequest()
    {
        // Act
        var result = await _controller.GetAuditLogsByCategory("Deployment", 1, 0);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var errorResponse = badRequestResult.Value.Should().BeOfType<ErrorResponse>().Subject;
        errorResponse.Error.Should().Be("Page size must be between 1 and 100");
    }

    [Fact]
    public async Task GetAuditLogsByCategory_WithPageSizeTooLarge_ReturnsBadRequest()
    {
        // Act
        var result = await _controller.GetAuditLogsByCategory("Deployment", 1, 101);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var errorResponse = badRequestResult.Value.Should().BeOfType<ErrorResponse>().Subject;
        errorResponse.Error.Should().Be("Page size must be between 1 and 100");
    }

    [Fact]
    public async Task GetAuditLogsByCategory_WithServiceNotAvailable_ReturnsServiceUnavailable()
    {
        // Act
        var result = await _controllerWithoutService.GetAuditLogsByCategory("Deployment");

        // Assert
        var statusCodeResult = result.Should().BeOfType<ObjectResult>().Subject;
        statusCodeResult.StatusCode.Should().Be(503);
        var errorResponse = statusCodeResult.Value.Should().BeOfType<ErrorResponse>().Subject;
        errorResponse.Error.Should().Be("Audit log service is not configured");
    }

    #endregion

    #region GetAuditLogsByTraceId Tests

    [Fact]
    public async Task GetAuditLogsByTraceId_WithValidTraceId_ReturnsOk()
    {
        // Arrange
        var traceId = "test-trace-123";
        var auditLogs = new List<AuditLog>
        {
            CreateTestAuditLog(1),
            CreateTestAuditLog(2)
        };

        _mockAuditLogService.Setup(x => x.GetAuditLogsByTraceIdAsync(
                traceId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(auditLogs);

        // Act
        var result = await _controller.GetAuditLogsByTraceId(traceId);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedLogs = okResult.Value.Should().BeOfType<List<AuditLog>>().Subject;
        returnedLogs.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAuditLogsByTraceId_WithNullTraceId_ReturnsBadRequest()
    {
        // Act
        var result = await _controller.GetAuditLogsByTraceId(null!);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var errorResponse = badRequestResult.Value.Should().BeOfType<ErrorResponse>().Subject;
        errorResponse.Error.Should().Be("Trace ID parameter is required");
    }

    [Fact]
    public async Task GetAuditLogsByTraceId_WithEmptyTraceId_ReturnsBadRequest()
    {
        // Act
        var result = await _controller.GetAuditLogsByTraceId("");

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var errorResponse = badRequestResult.Value.Should().BeOfType<ErrorResponse>().Subject;
        errorResponse.Error.Should().Be("Trace ID parameter is required");
    }

    [Fact]
    public async Task GetAuditLogsByTraceId_WithServiceNotAvailable_ReturnsServiceUnavailable()
    {
        // Act
        var result = await _controllerWithoutService.GetAuditLogsByTraceId("test-trace");

        // Assert
        var statusCodeResult = result.Should().BeOfType<ObjectResult>().Subject;
        statusCodeResult.StatusCode.Should().Be(503);
        var errorResponse = statusCodeResult.Value.Should().BeOfType<ErrorResponse>().Subject;
        errorResponse.Error.Should().Be("Audit log service is not configured");
    }

    #endregion

    #region GetDeploymentEvents Tests

    [Fact]
    public async Task GetDeploymentEvents_WithValidExecutionId_ReturnsOk()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        var deploymentEvents = new List<DeploymentAuditEvent>
        {
            CreateTestDeploymentEvent(executionId),
            CreateTestDeploymentEvent(executionId)
        };

        _mockAuditLogService.Setup(x => x.GetDeploymentEventsAsync(
                executionId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(deploymentEvents);

        // Act
        var result = await _controller.GetDeploymentEvents(executionId);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedEvents = okResult.Value.Should().BeOfType<List<DeploymentAuditEvent>>().Subject;
        returnedEvents.Should().HaveCount(2);
        returnedEvents.Should().OnlyContain(e => e.DeploymentExecutionId == executionId);
    }

    [Fact]
    public async Task GetDeploymentEvents_WithEmptyGuid_ReturnsBadRequest()
    {
        // Act
        var result = await _controller.GetDeploymentEvents(Guid.Empty);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var errorResponse = badRequestResult.Value.Should().BeOfType<ErrorResponse>().Subject;
        errorResponse.Error.Should().Be("Valid execution ID is required");
    }

    [Fact]
    public async Task GetDeploymentEvents_WithServiceNotAvailable_ReturnsServiceUnavailable()
    {
        // Act
        var result = await _controllerWithoutService.GetDeploymentEvents(Guid.NewGuid());

        // Assert
        var statusCodeResult = result.Should().BeOfType<ObjectResult>().Subject;
        statusCodeResult.StatusCode.Should().Be(503);
        var errorResponse = statusCodeResult.Value.Should().BeOfType<ErrorResponse>().Subject;
        errorResponse.Error.Should().Be("Audit log service is not configured");
    }

    #endregion

    #region GetApprovalEvents Tests

    [Fact]
    public async Task GetApprovalEvents_WithValidExecutionId_ReturnsOk()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        var approvalEvents = new List<ApprovalAuditEvent>
        {
            CreateTestApprovalEvent(executionId),
            CreateTestApprovalEvent(executionId)
        };

        _mockAuditLogService.Setup(x => x.GetApprovalEventsAsync(
                executionId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(approvalEvents);

        // Act
        var result = await _controller.GetApprovalEvents(executionId);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedEvents = okResult.Value.Should().BeOfType<List<ApprovalAuditEvent>>().Subject;
        returnedEvents.Should().HaveCount(2);
        returnedEvents.Should().OnlyContain(e => e.DeploymentExecutionId == executionId);
    }

    [Fact]
    public async Task GetApprovalEvents_WithEmptyGuid_ReturnsBadRequest()
    {
        // Act
        var result = await _controller.GetApprovalEvents(Guid.Empty);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var errorResponse = badRequestResult.Value.Should().BeOfType<ErrorResponse>().Subject;
        errorResponse.Error.Should().Be("Valid execution ID is required");
    }

    [Fact]
    public async Task GetApprovalEvents_WithServiceNotAvailable_ReturnsServiceUnavailable()
    {
        // Act
        var result = await _controllerWithoutService.GetApprovalEvents(Guid.NewGuid());

        // Assert
        var statusCodeResult = result.Should().BeOfType<ObjectResult>().Subject;
        statusCodeResult.StatusCode.Should().Be(503);
        var errorResponse = statusCodeResult.Value.Should().BeOfType<ErrorResponse>().Subject;
        errorResponse.Error.Should().Be("Audit log service is not configured");
    }

    #endregion

    #region GetAuthenticationEvents Tests

    [Fact]
    public async Task GetAuthenticationEvents_WithValidUsername_ReturnsOk()
    {
        // Arrange
        var username = "testuser";
        var authenticationEvents = new List<AuthenticationAuditEvent>
        {
            CreateTestAuthenticationEvent(username),
            CreateTestAuthenticationEvent(username)
        };

        _mockAuditLogService.Setup(x => x.GetAuthenticationEventsAsync(
                username,
                null,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(authenticationEvents);

        // Act
        var result = await _controller.GetAuthenticationEvents(username);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedEvents = okResult.Value.Should().BeOfType<List<AuthenticationAuditEvent>>().Subject;
        returnedEvents.Should().HaveCount(2);
        returnedEvents.Should().OnlyContain(e => e.Username == username);
    }

    [Fact]
    public async Task GetAuthenticationEvents_WithDateRange_ReturnsOk()
    {
        // Arrange
        var username = "testuser";
        var startDate = DateTime.UtcNow.AddDays(-7);
        var endDate = DateTime.UtcNow;
        var authenticationEvents = new List<AuthenticationAuditEvent>
        {
            CreateTestAuthenticationEvent(username)
        };

        _mockAuditLogService.Setup(x => x.GetAuthenticationEventsAsync(
                username,
                startDate,
                endDate,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(authenticationEvents);

        // Act
        var result = await _controller.GetAuthenticationEvents(username, startDate, endDate);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedEvents = okResult.Value.Should().BeOfType<List<AuthenticationAuditEvent>>().Subject;
        returnedEvents.Should().HaveCount(1);
        _mockAuditLogService.Verify(x => x.GetAuthenticationEventsAsync(
            username,
            startDate,
            endDate,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAuthenticationEvents_WithNullUsername_ReturnsBadRequest()
    {
        // Act
        var result = await _controller.GetAuthenticationEvents(null!);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var errorResponse = badRequestResult.Value.Should().BeOfType<ErrorResponse>().Subject;
        errorResponse.Error.Should().Be("Username parameter is required");
    }

    [Fact]
    public async Task GetAuthenticationEvents_WithEmptyUsername_ReturnsBadRequest()
    {
        // Act
        var result = await _controller.GetAuthenticationEvents("");

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var errorResponse = badRequestResult.Value.Should().BeOfType<ErrorResponse>().Subject;
        errorResponse.Error.Should().Be("Username parameter is required");
    }

    [Fact]
    public async Task GetAuthenticationEvents_WithStartDateAfterEndDate_ReturnsBadRequest()
    {
        // Arrange
        var startDate = DateTime.UtcNow;
        var endDate = DateTime.UtcNow.AddDays(-7);

        // Act
        var result = await _controller.GetAuthenticationEvents("testuser", startDate, endDate);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var errorResponse = badRequestResult.Value.Should().BeOfType<ErrorResponse>().Subject;
        errorResponse.Error.Should().Be("Start date must be before end date");
    }

    [Fact]
    public async Task GetAuthenticationEvents_WithServiceNotAvailable_ReturnsServiceUnavailable()
    {
        // Act
        var result = await _controllerWithoutService.GetAuthenticationEvents("testuser");

        // Assert
        var statusCodeResult = result.Should().BeOfType<ObjectResult>().Subject;
        statusCodeResult.StatusCode.Should().Be(503);
        var errorResponse = statusCodeResult.Value.Should().BeOfType<ErrorResponse>().Subject;
        errorResponse.Error.Should().Be("Audit log service is not configured");
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullAuditLogService_DoesNotThrow()
    {
        // Act & Assert - Should not throw, service is optional
        var controller = new AuditLogsController(
            NullLogger<AuditLogsController>.Instance,
            null);

        controller.Should().NotBeNull();
    }

    #endregion
}
