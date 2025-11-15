using HotSwap.Distributed.Domain.Enums;

namespace HotSwap.Distributed.Domain.Models;

/// <summary>
/// Request model for user authentication.
/// </summary>
public class AuthenticationRequest
{
    /// <summary>
    /// Username for authentication.
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// User's password (plaintext, will be validated against hash).
    /// </summary>
    public string Password { get; set; } = string.Empty;
}

/// <summary>
/// Response model containing JWT token and user information.
/// </summary>
public class AuthenticationResponse
{
    /// <summary>
    /// JWT bearer token for API authentication.
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// Token expiration date and time (UTC).
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// User information.
    /// </summary>
    public UserInfo User { get; set; } = new();
}

/// <summary>
/// User information for authentication response (excludes sensitive data).
/// </summary>
public class UserInfo
{
    /// <summary>
    /// User's unique identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Username.
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// User's email address.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Full name.
    /// </summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>
    /// Assigned roles.
    /// </summary>
    public List<UserRole> Roles { get; set; } = new();
}

/// <summary>
/// JWT token configuration settings.
/// </summary>
public class JwtConfiguration
{
    /// <summary>
    /// Secret key for signing JWT tokens (should be stored securely).
    /// Minimum 256 bits (32 characters) recommended.
    /// </summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>
    /// Token issuer (identifies who created the token).
    /// </summary>
    public string Issuer { get; set; } = "DistributedKernelOrchestrator";

    /// <summary>
    /// Token audience (identifies who can use the token).
    /// </summary>
    public string Audience { get; set; } = "DistributedKernelApi";

    /// <summary>
    /// Token expiration time in minutes (default: 60 minutes).
    /// </summary>
    public int ExpirationMinutes { get; set; } = 60;
}
