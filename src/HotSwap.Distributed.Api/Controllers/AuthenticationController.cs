using HotSwap.Distributed.Api.Models;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotSwap.Distributed.Api.Controllers;

/// <summary>
/// API endpoints for user authentication and authorization.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public class AuthenticationController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly ILogger<AuthenticationController> _logger;

    public AuthenticationController(
        IUserRepository userRepository,
        IJwtTokenService jwtTokenService,
        ILogger<AuthenticationController> logger)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _jwtTokenService = jwtTokenService ?? throw new ArgumentNullException(nameof(jwtTokenService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Authenticates a user and returns a JWT bearer token.
    /// </summary>
    /// <param name="request">Authentication credentials (username and password)</param>
    /// <returns>JWT token and user information if successful</returns>
    /// <response code="200">Authentication successful, returns JWT token</response>
    /// <response code="400">Invalid request format</response>
    /// <response code="401">Authentication failed - invalid credentials</response>
    /// <response code="500">Server error</response>
    /// <remarks>
    /// Demo credentials for testing:
    ///
    /// Admin user (full access):
    /// - Username: admin
    /// - Password: Admin123!
    ///
    /// Deployer user (can create deployments):
    /// - Username: deployer
    /// - Password: Deploy123!
    ///
    /// Viewer user (read-only):
    /// - Username: viewer
    /// - Password: Viewer123!
    /// </remarks>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthenticationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Login([FromBody] AuthenticationRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new ErrorResponse
            {
                Error = "Username and password are required"
            });
        }

        _logger.LogInformation("Login attempt for user: {Username}", request.Username);

        // Validate credentials
        var user = await _userRepository.ValidateCredentialsAsync(request.Username, request.Password);

        if (user == null)
        {
            _logger.LogWarning("Failed login attempt for user: {Username}", request.Username);
            return Unauthorized(new ErrorResponse
            {
                Error = "Invalid username or password"
            });
        }

        // Generate JWT token
        var (token, expiresAt) = _jwtTokenService.GenerateToken(user);

        var response = new AuthenticationResponse
        {
            Token = token,
            ExpiresAt = expiresAt,
            User = new UserInfo
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                FullName = user.FullName,
                Roles = user.Roles
            }
        };

        _logger.LogInformation("User {Username} authenticated successfully, token expires at {ExpiresAt}",
            user.Username, expiresAt);

        return Ok(response);
    }

    /// <summary>
    /// Gets information about the currently authenticated user.
    /// </summary>
    /// <returns>Current user information</returns>
    /// <response code="200">User information retrieved successfully</response>
    /// <response code="401">Not authenticated</response>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(UserInfo), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetCurrentUser()
    {
        // Extract user ID from JWT claims
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                         ?? User.FindFirst("sub")?.Value;

        if (string.IsNullOrWhiteSpace(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            _logger.LogWarning("Failed to extract user ID from JWT claims");
            return Unauthorized(new ErrorResponse
            {
                Error = "Invalid authentication token"
            });
        }

        var user = await _userRepository.FindByIdAsync(userId);

        if (user == null)
        {
            _logger.LogWarning("User {UserId} not found in repository", userId);
            return Unauthorized(new ErrorResponse
            {
                Error = "User not found"
            });
        }

        var userInfo = new UserInfo
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            FullName = user.FullName,
            Roles = user.Roles
        };

        return Ok(userInfo);
    }

    /// <summary>
    /// Gets demo credentials for testing (only available in non-production environments).
    /// </summary>
    /// <returns>List of demo users and their credentials</returns>
    /// <response code="200">Demo credentials retrieved</response>
    /// <response code="403">Not available in production</response>
    [HttpGet("demo-credentials")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public IActionResult GetDemoCredentials()
    {
        // Only allow in development/staging environments
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        if (environment?.Equals("Production", StringComparison.OrdinalIgnoreCase) == true)
        {
            return StatusCode(403, new ErrorResponse
            {
                Error = "Demo credentials are not available in production"
            });
        }

        var demoCredentials = new
        {
            Message = "Demo credentials for testing (use with /api/v1/authentication/login)",
            Users = new[]
            {
                new
                {
                    Username = "admin",
                    Password = "Admin123!",
                    Roles = new[] { "Admin", "Deployer", "Viewer" },
                    Description = "Full administrative access - can manage deployments, approvals, and users"
                },
                new
                {
                    Username = "deployer",
                    Password = "Deploy123!",
                    Roles = new[] { "Deployer", "Viewer" },
                    Description = "Can create and manage deployments, view metrics and status"
                },
                new
                {
                    Username = "viewer",
                    Password = "Viewer123!",
                    Roles = new[] { "Viewer" },
                    Description = "Read-only access to deployments and metrics"
                }
            }
        };

        return Ok(demoCredentials);
    }
}
