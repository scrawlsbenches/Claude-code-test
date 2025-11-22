using System.Diagnostics;
using HotSwap.Distributed.Api.Models;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Data.Entities;
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
    private readonly IAuditLogService? _auditLogService;

    public AuthenticationController(
        IUserRepository userRepository,
        IJwtTokenService jwtTokenService,
        ILogger<AuthenticationController> logger,
        IAuditLogService? auditLogService = null)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _jwtTokenService = jwtTokenService ?? throw new ArgumentNullException(nameof(jwtTokenService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _auditLogService = auditLogService;
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

            // Audit log: Failed login
            await LogAuthenticationEventAsync(
                "LoginFailed",
                "Failure",
                request.Username,
                userId: null,
                authenticationResult: "Failure",
                failureReason: "Invalid username or password",
                tokenIssued: false,
                tokenExpiresAt: null,
                cancellationToken: default);

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

        // Audit log: Successful login
        await LogAuthenticationEventAsync(
            "LoginSuccess",
            "Success",
            user.Username,
            userId: user.Id,
            authenticationResult: "Success",
            failureReason: null,
            tokenIssued: true,
            tokenExpiresAt: expiresAt,
            cancellationToken: default);

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

            // Audit log: Invalid token
            await LogAuthenticationEventAsync(
                "TokenValidationFailed",
                "Failure",
                username: "Unknown",
                userId: null,
                authenticationResult: "InvalidToken",
                failureReason: "Failed to extract user ID from JWT claims",
                tokenIssued: false,
                tokenExpiresAt: null,
                cancellationToken: default);

            return Unauthorized(new ErrorResponse
            {
                Error = "Invalid authentication token"
            });
        }

        var user = await _userRepository.FindByIdAsync(userId);

        if (user == null)
        {
            _logger.LogWarning("User {UserId} not found in repository", userId);

            // Audit log: User not found
            await LogAuthenticationEventAsync(
                "UserNotFound",
                "Failure",
                username: userId.ToString(),
                userId: userId,
                authenticationResult: "UserNotFound",
                failureReason: "User ID from token not found in repository",
                tokenIssued: false,
                tokenExpiresAt: null,
                cancellationToken: default);

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

    /// <summary>
    /// Helper method to log authentication events to the audit log.
    /// </summary>
    private async Task LogAuthenticationEventAsync(
        string eventType,
        string result,
        string username,
        Guid? userId,
        string authenticationResult,
        string? failureReason,
        bool tokenIssued,
        DateTime? tokenExpiresAt,
        CancellationToken cancellationToken)
    {
        if (_auditLogService == null)
        {
            return; // Audit logging is optional
        }

        try
        {
            // Extract IP address and user agent from HTTP context
            var sourceIp = HttpContext.Connection.RemoteIpAddress?.ToString();
            var userAgent = HttpContext.Request.Headers["User-Agent"].ToString();

            var auditLog = new AuditLog
            {
                EventId = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                EventType = eventType,
                EventCategory = "Authentication",
                Severity = result == "Success" ? "Information" : "Warning",
                UserId = userId,
                Username = username,
                UserEmail = null, // We don't have email at this point
                ResourceType = "Authentication",
                ResourceId = userId?.ToString() ?? username,
                Action = "Authenticate",
                Result = result,
                Message = failureReason ?? $"{eventType} for user {username}",
                TraceId = Activity.Current?.TraceId.ToString(),
                SpanId = Activity.Current?.SpanId.ToString(),
                SourceIp = sourceIp,
                UserAgent = userAgent,
                CreatedAt = DateTime.UtcNow
            };

            var authEvent = new AuthenticationAuditEvent
            {
                UserId = userId,
                Username = username,
                AuthenticationMethod = "JWT",
                AuthenticationResult = authenticationResult,
                FailureReason = failureReason,
                TokenIssued = tokenIssued,
                TokenExpiresAt = tokenExpiresAt,
                SourceIp = sourceIp,
                UserAgent = userAgent,
                GeoLocation = null, // Could be enhanced with IP geolocation service
                IsSuspicious = DetectSuspiciousActivity(authenticationResult, sourceIp, userAgent),
                CreatedAt = DateTime.UtcNow
            };

            await _auditLogService.LogAuthenticationEventAsync(auditLog, authEvent, cancellationToken);
        }
        catch (Exception ex)
        {
            // Log the error but don't fail the authentication due to audit logging issues
            _logger.LogError(ex, "Failed to write audit log for authentication event {EventType}", eventType);
        }
    }

    /// <summary>
    /// Detects suspicious authentication activity based on patterns.
    /// </summary>
    private bool DetectSuspiciousActivity(string authenticationResult, string? sourceIp, string? userAgent)
    {
        // Basic suspicious activity detection
        // In production, this would be more sophisticated with:
        // - Rate limiting checks
        // - Geolocation anomalies
        // - Known malicious IP databases
        // - User agent fingerprinting
        // - Behavioral analysis

        // Flag as suspicious if:
        // 1. Multiple failures (would need to track this in a cache/database)
        // 2. Missing user agent (automated tools)
        // 3. Localhost connections in production (potential testing/probing)

        var isMissingUserAgent = string.IsNullOrWhiteSpace(userAgent);
        var isLocalhost = sourceIp?.Contains("127.0.0.1") == true || sourceIp?.Contains("::1") == true;
        var isProduction = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")?.Equals("Production", StringComparison.OrdinalIgnoreCase) == true;

        return (isMissingUserAgent || (isLocalhost && isProduction));
    }
}
