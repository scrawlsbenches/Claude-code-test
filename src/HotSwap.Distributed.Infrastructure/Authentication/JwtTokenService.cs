using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace HotSwap.Distributed.Infrastructure.Authentication;

/// <summary>
/// Service for generating and validating JWT tokens with support for secret rotation.
/// Integrates with ISecretService for automatic key rotation without service restart.
/// </summary>
public class JwtTokenService : IJwtTokenService
{
    private const string JWT_SIGNING_KEY_ID = "jwt-signing-key";
    private const int KEY_REFRESH_INTERVAL_MINUTES = 5;

    private readonly JwtConfiguration _config;
    private readonly ISecretService? _secretService;
    private readonly ILogger<JwtTokenService> _logger;
    private readonly JwtSecurityTokenHandler _tokenHandler;
    private readonly SemaphoreSlim _keyRefreshLock = new(1, 1);

    private SymmetricSecurityKey _currentSigningKey = null!;
    private List<SymmetricSecurityKey> _validationKeys = new();
    private DateTime _lastKeyRefresh = DateTime.MinValue;

    /// <summary>
    /// Initializes a new instance of JwtTokenService.
    /// If secretService is provided, keys will be loaded from the secret store and automatically refreshed.
    /// Otherwise, falls back to using the static SecretKey from configuration.
    /// </summary>
    public JwtTokenService(
        JwtConfiguration config,
        ILogger<JwtTokenService> logger,
        ISecretService? secretService = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _secretService = secretService;

        _tokenHandler = new JwtSecurityTokenHandler
        {
            MapInboundClaims = false // Disable WS-* claim mapping to preserve standard JWT claim names
        };

        // Initialize keys from secret service or configuration
        InitializeKeys();
    }

    private void InitializeKeys()
    {
        if (_secretService != null)
        {
            _logger.LogInformation("Initializing JWT signing keys from ISecretService");
            RefreshKeysFromSecretService();
        }
        else
        {
            _logger.LogInformation("Initializing JWT signing key from configuration (secret rotation not enabled)");

            if (string.IsNullOrWhiteSpace(_config.SecretKey) || _config.SecretKey.Length < 32)
            {
                throw new ArgumentException("JWT SecretKey must be at least 32 characters long", nameof(_config));
            }

            _currentSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config.SecretKey));
            _validationKeys = new List<SymmetricSecurityKey> { _currentSigningKey };
        }
    }

    private void RefreshKeysFromSecretService()
    {
        if (_secretService == null)
            return;

        try
        {
            // Load current secret for signing
            var currentSecretVersion = _secretService.GetSecretAsync(JWT_SIGNING_KEY_ID, CancellationToken.None)
                .GetAwaiter().GetResult();

            if (currentSecretVersion == null || string.IsNullOrWhiteSpace(currentSecretVersion.Value) || currentSecretVersion.Value.Length < 32)
            {
                throw new InvalidOperationException($"JWT signing key '{JWT_SIGNING_KEY_ID}' must be at least 32 characters long");
            }

            _currentSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(currentSecretVersion.Value));

            // Load metadata to check if we're in rotation window
            var metadata = _secretService.GetSecretMetadataAsync(JWT_SIGNING_KEY_ID, CancellationToken.None)
                .GetAwaiter().GetResult();

            var validationKeys = new List<SymmetricSecurityKey> { _currentSigningKey };

            // If in rotation window, also add previous version for validation
            if (metadata != null && metadata.IsInRotationWindow && metadata.CurrentVersion > 1)
            {
                _logger.LogInformation("JWT signing key is in rotation window - loading previous key version {PreviousVersion} for validation",
                    metadata.CurrentVersion - 1);

                var previousSecretVersion = _secretService.GetSecretVersionAsync(
                        JWT_SIGNING_KEY_ID,
                        metadata.CurrentVersion - 1,
                        CancellationToken.None)
                    .GetAwaiter().GetResult();

                if (previousSecretVersion != null && !string.IsNullOrWhiteSpace(previousSecretVersion.Value) && previousSecretVersion.Value.Length >= 32)
                {
                    var previousKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(previousSecretVersion.Value));
                    validationKeys.Add(previousKey);
                    _logger.LogInformation("Added previous JWT signing key version for validation during rotation window");
                }
            }

            _validationKeys = validationKeys;
            _lastKeyRefresh = DateTime.UtcNow;

            _logger.LogInformation("JWT signing keys refreshed successfully ({Count} validation keys loaded)",
                _validationKeys.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "JWT signing key not found in secret service - using configuration fallback");

            // Fallback to configuration if secret service fails
            if (string.IsNullOrWhiteSpace(_config.SecretKey) || _config.SecretKey.Length < 32)
            {
                throw new ArgumentException("JWT SecretKey must be at least 32 characters long", nameof(_config));
            }

            _currentSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config.SecretKey));
            _validationKeys = new List<SymmetricSecurityKey> { _currentSigningKey };
        }
    }

    /// <summary>
    /// Refreshes JWT signing keys from the secret service.
    /// Call this method after rotating the JWT signing key to pick up the new version without restarting the service.
    /// </summary>
    public void RefreshKeys()
    {
        if (_secretService == null)
        {
            _logger.LogDebug("RefreshKeys called but ISecretService is not configured - using static configuration");
            return;
        }

        _keyRefreshLock.Wait();
        try
        {
            _logger.LogInformation("Manually refreshing JWT signing keys");
            RefreshKeysFromSecretService();
        }
        finally
        {
            _keyRefreshLock.Release();
        }
    }

    private void EnsureKeysAreCurrent()
    {
        if (_secretService == null)
            return;

        // Automatically refresh keys every KEY_REFRESH_INTERVAL_MINUTES to pick up rotations
        if (DateTime.UtcNow - _lastKeyRefresh > TimeSpan.FromMinutes(KEY_REFRESH_INTERVAL_MINUTES))
        {
            // Try to acquire lock without blocking (fire and forget if another thread is already refreshing)
            if (_keyRefreshLock.Wait(0))
            {
                try
                {
                    // Double-check after acquiring lock
                    if (DateTime.UtcNow - _lastKeyRefresh > TimeSpan.FromMinutes(KEY_REFRESH_INTERVAL_MINUTES))
                    {
                        _logger.LogDebug("Auto-refreshing JWT signing keys (last refresh: {LastRefresh})", _lastKeyRefresh);
                        RefreshKeysFromSecretService();
                    }
                }
                finally
                {
                    _keyRefreshLock.Release();
                }
            }
        }
    }

    /// <summary>
    /// Generates a JWT token for the specified user.
    /// Always uses the current (latest) signing key version.
    /// </summary>
    public (string Token, DateTime ExpiresAt) GenerateToken(User user)
    {
        if (user == null)
            throw new ArgumentNullException(nameof(user));

        // Ensure keys are current before generating token
        EnsureKeysAreCurrent();

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

        // Create token descriptor - always use current signing key
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims, "jwt"),
            NotBefore = notBefore,
            Expires = expiresAt,
            Issuer = _config.Issuer,
            Audience = _config.Audience,
            SigningCredentials = new SigningCredentials(_currentSigningKey, SecurityAlgorithms.HmacSha256Signature)
        };

        var token = _tokenHandler.CreateToken(tokenDescriptor);
        var tokenString = _tokenHandler.WriteToken(token);

        _logger.LogInformation("Generated JWT token for user {Username} (ID: {UserId}), expires at {ExpiresAt}",
            user.Username, user.Id, expiresAt);

        return (tokenString, expiresAt);
    }

    /// <summary>
    /// Validates a JWT token and extracts user ID.
    /// During rotation window, tries validation with both current and previous signing keys.
    /// </summary>
    public Guid? ValidateToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        // Ensure keys are current before validating token
        EnsureKeysAreCurrent();

        // Try validation with each key (current first, then previous during rotation window)
        foreach (var key in _validationKeys)
        {
            var userId = TryValidateWithKey(token, key);
            if (userId.HasValue)
            {
                return userId;
            }
        }

        // Token could not be validated with any available key
        return null;
    }

    private Guid? TryValidateWithKey(string token, SymmetricSecurityKey signingKey)
    {
        try
        {
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = signingKey,
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
            _logger.LogDebug("Token validation failed with this key: token expired - {Message}", ex.Message);
            return null;
        }
        catch (SecurityTokenException ex)
        {
            _logger.LogDebug("Token validation failed with this key: {Message}", ex.Message);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unexpected error validating token with this key");
            return null;
        }
    }
}
