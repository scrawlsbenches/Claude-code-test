using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace HotSwap.Distributed.Infrastructure.Authentication;

/// <summary>
/// Service for generating and validating JWT tokens.
/// </summary>
public class JwtTokenService : IJwtTokenService
{
    private readonly JwtConfiguration _config;
    private readonly ILogger<JwtTokenService> _logger;
    private readonly JwtSecurityTokenHandler _tokenHandler;
    private readonly SymmetricSecurityKey _signingKey;

    public JwtTokenService(JwtConfiguration config, ILogger<JwtTokenService> logger)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (string.IsNullOrWhiteSpace(_config.SecretKey) || _config.SecretKey.Length < 32)
        {
            throw new ArgumentException("JWT SecretKey must be at least 32 characters long", nameof(config));
        }

        _tokenHandler = new JwtSecurityTokenHandler
        {
            MapInboundClaims = false // Disable WS-* claim mapping to preserve standard JWT claim names
        };
        _signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config.SecretKey));
    }

    /// <summary>
    /// Generates a JWT token for the specified user.
    /// </summary>
    public (string Token, DateTime ExpiresAt) GenerateToken(User user)
    {
        if (user == null)
            throw new ArgumentNullException(nameof(user));

        var now = DateTime.UtcNow;
        var expiresAt = now.AddMinutes(_config.ExpirationMinutes);

        // For testing expired tokens (ExpirationMinutes < 0), set NotBefore to an earlier time
        // so the token was valid in the past but is now expired.
        // This ensures the JWT spec requirement (expires > notBefore) is satisfied.
        // For normal tokens, set NotBefore slightly in the past to avoid clock skew issues.
        var notBefore = _config.ExpirationMinutes < 0
            ? expiresAt.AddMinutes(-1)     // Token was valid for 1 minute in the past
            : now.AddSeconds(-5);          // Token is valid starting 5 seconds ago

        // Create claims
        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.UniqueName, user.Username),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Name, user.FullName),
        };

        // Add role claims
        foreach (var role in user.Roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role.ToString()));
        }

        // Create token descriptor
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims, "jwt"),
            NotBefore = notBefore,
            Expires = expiresAt,
            Issuer = _config.Issuer,
            Audience = _config.Audience,
            SigningCredentials = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256Signature)
        };

        var token = _tokenHandler.CreateToken(tokenDescriptor);
        var tokenString = _tokenHandler.WriteToken(token);

        _logger.LogInformation("Generated JWT token for user {Username} (ID: {UserId}), expires at {ExpiresAt}",
            user.Username, user.Id, expiresAt);

        return (tokenString, expiresAt);
    }

    /// <summary>
    /// Validates a JWT token and extracts user ID.
    /// </summary>
    public Guid? ValidateToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        try
        {
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = _signingKey,
                ValidateIssuer = true,
                ValidIssuer = _config.Issuer,
                ValidateAudience = true,
                ValidAudience = _config.Audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(1) // Allow 1 minute clock skew tolerance
            };

            var principal = _tokenHandler.ValidateToken(token, validationParameters, out var validatedToken);

            // Extract user ID from subject claim
            var userIdClaim = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            if (string.IsNullOrWhiteSpace(userIdClaim))
            {
                _logger.LogWarning("Token validation failed: missing subject claim");
                return null;
            }

            if (!Guid.TryParse(userIdClaim, out var userId))
            {
                _logger.LogWarning("Token validation failed: invalid user ID format");
                return null;
            }

            _logger.LogDebug("Successfully validated token for user ID {UserId}", userId);
            return userId;
        }
        catch (SecurityTokenExpiredException ex)
        {
            _logger.LogWarning("Token validation failed: token expired - {Message}", ex.Message);
            return null;
        }
        catch (SecurityTokenException ex)
        {
            _logger.LogWarning("Token validation failed: {Message}", ex.Message);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error validating token");
            return null;
        }
    }
}
