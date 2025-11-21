using FluentAssertions;
using HotSwap.Distributed.Api.Controllers;
using HotSwap.Distributed.Api.Models;
using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Data.Entities;
using HotSwap.Distributed.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Security.Claims;
using Xunit;

namespace HotSwap.Distributed.Tests.Api;

public class AuthenticationControllerTests
{
    private readonly Mock<IUserRepository> _mockUserRepository;
    private readonly Mock<IJwtTokenService> _mockJwtTokenService;
    private readonly Mock<IAuditLogService> _mockAuditLogService;
    private readonly AuthenticationController _controller;

    public AuthenticationControllerTests()
    {
        _mockUserRepository = new Mock<IUserRepository>();
        _mockJwtTokenService = new Mock<IJwtTokenService>();
        _mockAuditLogService = new Mock<IAuditLogService>();
        _controller = new AuthenticationController(
            _mockUserRepository.Object,
            _mockJwtTokenService.Object,
            NullLogger<AuthenticationController>.Instance,
            _mockAuditLogService.Object);

        // Setup HttpContext for testing
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
    }

    private User CreateTestUser(string username = "testuser", string email = "test@example.com")
    {
        return new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            Email = email,
            FullName = "Test User",
            PasswordHash = "hashed_password",
            Roles = new List<UserRole> { UserRole.Viewer },
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    #region Login Tests

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsOkWithToken()
    {
        // Arrange
        var request = new AuthenticationRequest
        {
            Username = "testuser",
            Password = "Test123!"
        };

        var user = CreateTestUser("testuser");
        var token = "jwt_token_here";
        var expiresAt = DateTime.UtcNow.AddHours(1);

        _mockUserRepository
            .Setup(x => x.ValidateCredentialsAsync(request.Username, request.Password))
            .ReturnsAsync(user);

        _mockJwtTokenService
            .Setup(x => x.GenerateToken(user))
            .Returns((token, expiresAt));

        // Act
        var result = await _controller.Login(request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<AuthenticationResponse>().Subject;
        response.Token.Should().Be(token);
        response.ExpiresAt.Should().Be(expiresAt);
        response.User.Should().NotBeNull();
        response.User!.Username.Should().Be("testuser");
        response.User.Id.Should().Be(user.Id);
    }

    [Fact]
    public async Task Login_WithInvalidCredentials_ReturnsUnauthorized()
    {
        // Arrange
        var request = new AuthenticationRequest
        {
            Username = "testuser",
            Password = "WrongPassword"
        };

        _mockUserRepository
            .Setup(x => x.ValidateCredentialsAsync(request.Username, request.Password))
            .ReturnsAsync((User?)null);

        // Act
        var result = await _controller.Login(request);

        // Assert
        var unauthorizedResult = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        var errorResponse = unauthorizedResult.Value.Should().BeOfType<ErrorResponse>().Subject;
        errorResponse.Error.Should().Contain("Invalid username or password");
    }

    [Fact]
    public async Task Login_WithNullRequest_ReturnsBadRequest()
    {
        // Arrange
        AuthenticationRequest? request = null;

        // Act
        var result = await _controller.Login(request!);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var errorResponse = badRequestResult.Value.Should().BeOfType<ErrorResponse>().Subject;
        errorResponse.Error.Should().Contain("Username and password are required");
    }

    [Fact]
    public async Task Login_WithEmptyUsername_ReturnsBadRequest()
    {
        // Arrange
        var request = new AuthenticationRequest
        {
            Username = "",
            Password = "Test123!"
        };

        // Act
        var result = await _controller.Login(request);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var errorResponse = badRequestResult.Value.Should().BeOfType<ErrorResponse>().Subject;
        errorResponse.Error.Should().Contain("Username and password are required");
    }

    [Fact]
    public async Task Login_WithEmptyPassword_ReturnsBadRequest()
    {
        // Arrange
        var request = new AuthenticationRequest
        {
            Username = "testuser",
            Password = ""
        };

        // Act
        var result = await _controller.Login(request);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var errorResponse = badRequestResult.Value.Should().BeOfType<ErrorResponse>().Subject;
        errorResponse.Error.Should().Contain("Username and password are required");
    }

    [Fact]
    public async Task Login_WithWhitespaceUsername_ReturnsBadRequest()
    {
        // Arrange
        var request = new AuthenticationRequest
        {
            Username = "   ",
            Password = "Test123!"
        };

        // Act
        var result = await _controller.Login(request);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var errorResponse = badRequestResult.Value.Should().BeOfType<ErrorResponse>().Subject;
        errorResponse.Error.Should().Contain("Username and password are required");
    }

    [Fact]
    public async Task Login_WithValidCredentials_LogsAuditEvent()
    {
        // Arrange
        var request = new AuthenticationRequest
        {
            Username = "testuser",
            Password = "Test123!"
        };

        var user = CreateTestUser("testuser");
        var token = "jwt_token_here";
        var expiresAt = DateTime.UtcNow.AddHours(1);

        _mockUserRepository
            .Setup(x => x.ValidateCredentialsAsync(request.Username, request.Password))
            .ReturnsAsync(user);

        _mockJwtTokenService
            .Setup(x => x.GenerateToken(user))
            .Returns((token, expiresAt));

        // Act
        await _controller.Login(request);

        // Assert
        _mockAuditLogService.Verify(
            x => x.LogAuthenticationEventAsync(
                It.Is<AuditLog>(log => log.EventType == "LoginSuccess" && log.Result == "Success"),
                It.Is<AuthenticationAuditEvent>(evt => evt.Username == "testuser" && evt.TokenIssued == true),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Login_WithInvalidCredentials_LogsFailedAuditEvent()
    {
        // Arrange
        var request = new AuthenticationRequest
        {
            Username = "testuser",
            Password = "WrongPassword"
        };

        _mockUserRepository
            .Setup(x => x.ValidateCredentialsAsync(request.Username, request.Password))
            .ReturnsAsync((User?)null);

        // Act
        await _controller.Login(request);

        // Assert
        _mockAuditLogService.Verify(
            x => x.LogAuthenticationEventAsync(
                It.Is<AuditLog>(log => log.EventType == "LoginFailed" && log.Result == "Failure"),
                It.Is<AuthenticationAuditEvent>(evt => evt.Username == "testuser" && evt.TokenIssued == false),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region GetCurrentUser Tests

    [Fact]
    public async Task GetCurrentUser_WithValidToken_ReturnsUserInfo()
    {
        // Arrange
        var user = CreateTestUser("testuser");
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username)
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        _controller.ControllerContext.HttpContext.User = claimsPrincipal;

        _mockUserRepository
            .Setup(x => x.FindByIdAsync(user.Id))
            .ReturnsAsync(user);

        // Act
        var result = await _controller.GetCurrentUser();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var userInfo = okResult.Value.Should().BeOfType<UserInfo>().Subject;
        userInfo.Username.Should().Be("testuser");
        userInfo.Id.Should().Be(user.Id);
        userInfo.Email.Should().Be(user.Email);
    }

    [Fact]
    public async Task GetCurrentUser_WithMissingUserIdClaim_ReturnsUnauthorized()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, "testuser")
            // Missing NameIdentifier claim
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        _controller.ControllerContext.HttpContext.User = claimsPrincipal;

        // Act
        var result = await _controller.GetCurrentUser();

        // Assert
        var unauthorizedResult = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        var errorResponse = unauthorizedResult.Value.Should().BeOfType<ErrorResponse>().Subject;
        errorResponse.Error.Should().Contain("Invalid authentication token");
    }

    [Fact]
    public async Task GetCurrentUser_WithInvalidUserIdFormat_ReturnsUnauthorized()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "not-a-guid")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        _controller.ControllerContext.HttpContext.User = claimsPrincipal;

        // Act
        var result = await _controller.GetCurrentUser();

        // Assert
        var unauthorizedResult = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        var errorResponse = unauthorizedResult.Value.Should().BeOfType<ErrorResponse>().Subject;
        errorResponse.Error.Should().Contain("Invalid authentication token");
    }

    [Fact]
    public async Task GetCurrentUser_WithNonExistentUser_ReturnsUnauthorized()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        _controller.ControllerContext.HttpContext.User = claimsPrincipal;

        _mockUserRepository
            .Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync((User?)null);

        // Act
        var result = await _controller.GetCurrentUser();

        // Assert
        var unauthorizedResult = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        var errorResponse = unauthorizedResult.Value.Should().BeOfType<ErrorResponse>().Subject;
        errorResponse.Error.Should().Contain("User not found");
    }

    [Fact]
    public async Task GetCurrentUser_WithSubClaim_ReturnsUserInfo()
    {
        // Arrange
        var user = CreateTestUser("testuser");
        var claims = new List<Claim>
        {
            new Claim("sub", user.Id.ToString()) // JWT standard "sub" claim
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        _controller.ControllerContext.HttpContext.User = claimsPrincipal;

        _mockUserRepository
            .Setup(x => x.FindByIdAsync(user.Id))
            .ReturnsAsync(user);

        // Act
        var result = await _controller.GetCurrentUser();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var userInfo = okResult.Value.Should().BeOfType<UserInfo>().Subject;
        userInfo.Username.Should().Be("testuser");
        userInfo.Id.Should().Be(user.Id);
    }

    #endregion

    #region GetDemoCredentials Tests

    [Fact]
    public void GetDemoCredentials_InDevelopmentEnvironment_ReturnsCredentials()
    {
        // Arrange
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");

        // Act
        var result = _controller.GetDemoCredentials();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().NotBeNull();

        // Clean up
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", null);
    }

    [Fact]
    public void GetDemoCredentials_InStagingEnvironment_ReturnsCredentials()
    {
        // Arrange
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Staging");

        // Act
        var result = _controller.GetDemoCredentials();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().NotBeNull();

        // Clean up
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", null);
    }

    [Fact]
    public void GetDemoCredentials_InProductionEnvironment_ReturnsForbidden()
    {
        // Arrange
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Production");

        // Act
        var result = _controller.GetDemoCredentials();

        // Assert
        var statusCodeResult = result.Should().BeOfType<ObjectResult>().Subject;
        statusCodeResult.StatusCode.Should().Be(403);
        var errorResponse = statusCodeResult.Value.Should().BeOfType<ErrorResponse>().Subject;
        errorResponse.Error.Should().Contain("not available in production");

        // Clean up
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", null);
    }

    [Fact]
    public void GetDemoCredentials_WithNullEnvironment_ReturnsCredentials()
    {
        // Arrange
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", null);

        // Act
        var result = _controller.GetDemoCredentials();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().NotBeNull();
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullUserRepository_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new AuthenticationController(
                null!,
                _mockJwtTokenService.Object,
                NullLogger<AuthenticationController>.Instance));

        exception.ParamName.Should().Be("userRepository");
    }

    [Fact]
    public void Constructor_WithNullJwtTokenService_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new AuthenticationController(
                _mockUserRepository.Object,
                null!,
                NullLogger<AuthenticationController>.Instance));

        exception.ParamName.Should().Be("jwtTokenService");
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new AuthenticationController(
                _mockUserRepository.Object,
                _mockJwtTokenService.Object,
                null!));

        exception.ParamName.Should().Be("logger");
    }

    [Fact]
    public void Constructor_WithNullAuditLogService_DoesNotThrow()
    {
        // Act & Assert
        var controller = new AuthenticationController(
            _mockUserRepository.Object,
            _mockJwtTokenService.Object,
            NullLogger<AuthenticationController>.Instance,
            auditLogService: null);

        controller.Should().NotBeNull();
    }

    #endregion
}
