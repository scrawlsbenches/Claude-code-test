using HotSwap.Distributed.Domain.Models;

namespace HotSwap.Distributed.Infrastructure.Interfaces;

/// <summary>
/// Service for generating and validating JWT tokens.
/// </summary>
public interface IJwtTokenService
{
    /// <summary>
    /// Generates a JWT token for the specified user.
    /// </summary>
    /// <param name="user">User to generate token for</param>
    /// <returns>JWT token string and expiration time</returns>
    (string Token, DateTime ExpiresAt) GenerateToken(User user);

    /// <summary>
    /// Validates a JWT token and extracts user information.
    /// </summary>
    /// <param name="token">JWT token to validate</param>
    /// <returns>User ID if valid, null otherwise</returns>
    Guid? ValidateToken(string token);
}
