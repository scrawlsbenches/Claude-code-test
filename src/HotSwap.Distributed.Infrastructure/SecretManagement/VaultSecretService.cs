using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Interfaces;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using VaultSharp;
using VaultSharp.Core;
using VaultSharp.V1.AuthMethods;
using VaultSharp.V1.AuthMethods.AppRole;
using VaultSharp.V1.AuthMethods.Token;
using VaultSharp.V1.Commons;
using DomainSecretMetadata = HotSwap.Distributed.Domain.Models.SecretMetadata;
using VaultSecretMetadata = VaultSharp.V1.Commons.SecretMetadata;

namespace HotSwap.Distributed.Infrastructure.SecretManagement;

/// <summary>
/// HashiCorp Vault implementation of ISecretService using KV v2 secrets engine.
/// Supports versioning, automatic rotation, and expiration tracking.
/// </summary>
public class VaultSecretService : ISecretService
{
    private readonly VaultConfiguration _config;
    private readonly ILogger<VaultSecretService> _logger;
    private readonly IVaultClient _vaultClient;
    private readonly AsyncRetryPolicy _retryPolicy;
    private readonly string _mountPoint;

    public VaultSecretService(
        VaultConfiguration config,
        ILogger<VaultSecretService> logger)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        ValidateConfiguration();

        _mountPoint = _config.MountPoint ?? "secret";
        _vaultClient = CreateVaultClient();
        _retryPolicy = CreateRetryPolicy();

        _logger.LogInformation("VaultSecretService initialized. Vault URL: {VaultUrl}, Mount: {MountPoint}",
            _config.VaultUrl, _mountPoint);
    }

    /// <summary>
    /// Retrieves the current version of a secret from Vault.
    /// </summary>
    public async Task<SecretVersion?> GetSecretAsync(string secretId, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _retryPolicy.ExecuteAsync(async () =>
            {
                var secret = await _vaultClient.V1.Secrets.KeyValue.V2.ReadSecretAsync(
                    path: secretId,
                    mountPoint: _mountPoint);

                return secret?.Data?.Data;
            });

            if (result == null)
                return null;

            // Extract secret value and metadata
            var secretValue = result.ContainsKey("value") ? result["value"]?.ToString() : null;
            var version = result.ContainsKey("version") ? Convert.ToInt32(result["version"]) : 1;
            var createdAt = result.ContainsKey("created_at")
                ? DateTime.Parse(result["created_at"]?.ToString() ?? DateTime.UtcNow.ToString())
                : DateTime.UtcNow;
            var expiresAt = result.ContainsKey("expires_at") && result["expires_at"] != null
                ? DateTime.Parse(result["expires_at"]!.ToString()!)
                : (DateTime?)null;

            if (secretValue == null)
            {
                _logger.LogWarning("Secret {SecretId} found but has no value", secretId);
                return null;
            }

            _logger.LogDebug("Retrieved secret {SecretId} version {Version}", secretId, version);

            return new SecretVersion
            {
                SecretId = secretId,
                Version = version,
                Value = secretValue,
                CreatedAt = createdAt,
                ExpiresAt = expiresAt,
                IsActive = true,
                IsDeleted = false
            };
        }
        catch (VaultApiException ex) when (ex.StatusCode == 404)
        {
            _logger.LogWarning("Secret {SecretId} not found in Vault", secretId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve secret {SecretId} from Vault", secretId);
            throw;
        }
    }

    /// <summary>
    /// Retrieves a specific version of a secret from Vault.
    /// </summary>
    public async Task<SecretVersion?> GetSecretVersionAsync(
        string secretId,
        int version,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _retryPolicy.ExecuteAsync(async () =>
            {
                var secret = await _vaultClient.V1.Secrets.KeyValue.V2.ReadSecretAsync(
                    path: secretId,
                    version: version,
                    mountPoint: _mountPoint);

                return secret?.Data?.Data;
            });

            if (result == null)
                return null;

            var secretValue = result.ContainsKey("value") ? result["value"]?.ToString() : null;
            var createdAt = result.ContainsKey("created_at")
                ? DateTime.Parse(result["created_at"]?.ToString() ?? DateTime.UtcNow.ToString())
                : DateTime.UtcNow;
            var expiresAt = result.ContainsKey("expires_at") && result["expires_at"] != null
                ? DateTime.Parse(result["expires_at"]!.ToString()!)
                : (DateTime?)null;

            if (secretValue == null)
                return null;

            _logger.LogDebug("Retrieved secret {SecretId} version {Version}", secretId, version);

            return new SecretVersion
            {
                SecretId = secretId,
                Version = version,
                Value = secretValue,
                CreatedAt = createdAt,
                ExpiresAt = expiresAt,
                IsActive = false, // Older versions are not active
                IsDeleted = false
            };
        }
        catch (VaultApiException ex) when (ex.StatusCode == 404)
        {
            _logger.LogWarning("Secret {SecretId} version {Version} not found in Vault", secretId, version);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve secret {SecretId} version {Version} from Vault", secretId, version);
            throw;
        }
    }

    /// <summary>
    /// Retrieves metadata about a secret without fetching the secret value.
    /// </summary>
    public async Task<DomainSecretMetadata?> GetSecretMetadataAsync(
        string secretId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _retryPolicy.ExecuteAsync(async () =>
            {
                return await _vaultClient.V1.Secrets.KeyValue.V2.ReadSecretMetadataAsync(
                    path: secretId,
                    mountPoint: _mountPoint);
            });

            if (result == null)
                return null;

            var currentVersion = result.Data.CurrentVersion;
            // VaultSharp 1.17.5.1 returns CreatedTime as string, not DateTime
            var createdTime = DateTime.TryParse(result.Data.CreatedTime, out var parsedTime)
                ? parsedTime
                : DateTime.UtcNow;
            var customMetadata = result.Data.CustomMetadata ?? new Dictionary<string, string>();

            // Extract rotation policy from custom metadata
            RotationPolicy? rotationPolicy = null;
            if (customMetadata.ContainsKey("rotation_interval_days"))
            {
                rotationPolicy = new RotationPolicy
                {
                    RotationIntervalDays = int.TryParse(customMetadata["rotation_interval_days"], out var interval) ? interval : null,
                    MaxAgeDays = customMetadata.ContainsKey("max_age_days") && int.TryParse(customMetadata["max_age_days"], out var maxAge) ? maxAge : null,
                    NotificationThresholdDays = customMetadata.ContainsKey("notification_threshold_days") && int.TryParse(customMetadata["notification_threshold_days"], out var threshold) ? threshold : 7,
                    RotationWindowHours = customMetadata.ContainsKey("rotation_window_hours") && int.TryParse(customMetadata["rotation_window_hours"], out var window) ? window : 24,
                    EnableAutomaticRotation = customMetadata.ContainsKey("auto_rotate") && bool.TryParse(customMetadata["auto_rotate"], out var autoRotate) && autoRotate
                };
            }

            // Calculate expiration and rotation times
            DateTime? expiresAt = null;
            DateTime? nextRotationAt = null;
            if (rotationPolicy?.RotationIntervalDays.HasValue == true)
            {
                nextRotationAt = createdTime.AddDays(rotationPolicy.RotationIntervalDays.Value);
                if (rotationPolicy.MaxAgeDays.HasValue)
                {
                    expiresAt = createdTime.AddDays(rotationPolicy.MaxAgeDays.Value);
                }
            }

            var metadata = new DomainSecretMetadata
            {
                SecretId = secretId,
                CurrentVersion = currentVersion,
                PreviousVersion = customMetadata.ContainsKey("previous_version") && int.TryParse(customMetadata["previous_version"], out var prevVer) ? prevVer : null,
                CreatedAt = createdTime,
                ExpiresAt = expiresAt,
                LastRotatedAt = customMetadata.ContainsKey("last_rotated_at") && DateTime.TryParse(customMetadata["last_rotated_at"], out var lastRotated) ? lastRotated : null,
                NextRotationAt = nextRotationAt,
                RotationPolicy = rotationPolicy,
                Tags = customMetadata.Where(kv => !kv.Key.StartsWith("rotation_") && !kv.Key.StartsWith("last_") && kv.Key != "previous_version")
                    .ToDictionary(kv => kv.Key, kv => kv.Value)
            };

            _logger.LogDebug("Retrieved metadata for secret {SecretId}, version {Version}", secretId, currentVersion);

            return metadata;
        }
        catch (VaultApiException ex) when (ex.StatusCode == 404)
        {
            _logger.LogWarning("Secret metadata for {SecretId} not found in Vault", secretId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve metadata for secret {SecretId} from Vault", secretId);
            throw;
        }
    }

    /// <summary>
    /// Creates or updates a secret in Vault.
    /// </summary>
    public async Task<SecretVersion> SetSecretAsync(
        string secretId,
        string value,
        RotationPolicy? rotationPolicy = null,
        Dictionary<string, string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(secretId))
            throw new ArgumentException("Secret ID cannot be empty", nameof(secretId));
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Secret value cannot be empty", nameof(value));

        try
        {
            var data = new Dictionary<string, object>
            {
                ["value"] = value,
                ["created_at"] = DateTime.UtcNow.ToString("O"),
                ["version"] = 1
            };

            // Add expiration if specified in rotation policy
            if (rotationPolicy?.MaxAgeDays.HasValue == true)
            {
                data["expires_at"] = DateTime.UtcNow.AddDays(rotationPolicy.MaxAgeDays.Value).ToString("O");
            }

            // Write secret data
            var writeResult = await _retryPolicy.ExecuteAsync(async () =>
            {
                return await _vaultClient.V1.Secrets.KeyValue.V2.WriteSecretAsync(
                    path: secretId,
                    data: data,
                    mountPoint: _mountPoint);
            });

            var version = writeResult.Data.Version;

            // Update custom metadata with rotation policy and tags
            if (rotationPolicy != null || tags != null)
            {
                var customMetadata = tags != null ? new Dictionary<string, string>(tags) : new Dictionary<string, string>();

                if (rotationPolicy != null)
                {
                    if (rotationPolicy.RotationIntervalDays.HasValue)
                        customMetadata["rotation_interval_days"] = rotationPolicy.RotationIntervalDays.Value.ToString();
                    if (rotationPolicy.MaxAgeDays.HasValue)
                        customMetadata["max_age_days"] = rotationPolicy.MaxAgeDays.Value.ToString();
                    customMetadata["notification_threshold_days"] = rotationPolicy.NotificationThresholdDays.ToString();
                    customMetadata["rotation_window_hours"] = rotationPolicy.RotationWindowHours.ToString();
                    customMetadata["auto_rotate"] = rotationPolicy.EnableAutomaticRotation.ToString();
                }

                // VaultSharp 1.17.5.1 API: Requires Custom MetadataRequest with CustomMetadata property
                var metadataRequest = new VaultSharp.V1.SecretsEngines.KeyValue.V2.CustomMetadataRequest
                {
                    CustomMetadata = customMetadata
                };
                await _vaultClient.V1.Secrets.KeyValue.V2.WriteSecretMetadataAsync(
                    secretId,
                    metadataRequest,
                    _mountPoint);
            }

            _logger.LogInformation("Set secret {SecretId} version {Version} in Vault", secretId, version);

            return new SecretVersion
            {
                SecretId = secretId,
                Version = version,
                Value = value,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = rotationPolicy?.MaxAgeDays.HasValue == true ? DateTime.UtcNow.AddDays(rotationPolicy.MaxAgeDays.Value) : null,
                IsActive = true,
                IsDeleted = false
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set secret {SecretId} in Vault", secretId);
            throw;
        }
    }

    /// <summary>
    /// Rotates a secret by creating a new version.
    /// </summary>
    public async Task<SecretRotationResult> RotateSecretAsync(
        string secretId,
        string? newValue = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Get current metadata to retrieve rotation policy
            var metadata = await GetSecretMetadataAsync(secretId, cancellationToken);
            if (metadata == null)
            {
                return SecretRotationResult.CreateFailure(secretId, "Secret not found");
            }

            // Generate new secret value if not provided
            if (string.IsNullOrWhiteSpace(newValue))
            {
                newValue = GenerateRandomSecret();
                _logger.LogInformation("Generated new random value for secret {SecretId} rotation", secretId);
            }

            var previousVersion = metadata.CurrentVersion;
            var rotationWindowHours = metadata.RotationPolicy?.RotationWindowHours ?? 24;

            // Create new secret version
            var data = new Dictionary<string, object>
            {
                ["value"] = newValue,
                ["created_at"] = DateTime.UtcNow.ToString("O"),
                ["version"] = previousVersion + 1
            };

            if (metadata.RotationPolicy?.MaxAgeDays.HasValue == true)
            {
                data["expires_at"] = DateTime.UtcNow.AddDays(metadata.RotationPolicy.MaxAgeDays.Value).ToString("O");
            }

            var writeResult = await _retryPolicy.ExecuteAsync(async () =>
            {
                return await _vaultClient.V1.Secrets.KeyValue.V2.WriteSecretAsync(
                    path: secretId,
                    data: data,
                    mountPoint: _mountPoint);
            });

            var newVersion = writeResult.Data.Version;

            // Update metadata to track rotation
            var customMetadata = metadata.Tags != null ? new Dictionary<string, string>(metadata.Tags) : new Dictionary<string, string>();
            customMetadata["previous_version"] = previousVersion.ToString();
            customMetadata["last_rotated_at"] = DateTime.UtcNow.ToString("O");

            // Preserve rotation policy in metadata
            if (metadata.RotationPolicy != null)
            {
                if (metadata.RotationPolicy.RotationIntervalDays.HasValue)
                    customMetadata["rotation_interval_days"] = metadata.RotationPolicy.RotationIntervalDays.Value.ToString();
                if (metadata.RotationPolicy.MaxAgeDays.HasValue)
                    customMetadata["max_age_days"] = metadata.RotationPolicy.MaxAgeDays.Value.ToString();
                customMetadata["notification_threshold_days"] = metadata.RotationPolicy.NotificationThresholdDays.ToString();
                customMetadata["rotation_window_hours"] = metadata.RotationPolicy.RotationWindowHours.ToString();
                customMetadata["auto_rotate"] = metadata.RotationPolicy.EnableAutomaticRotation.ToString();
            }

            // VaultSharp 1.17.5.1 API: Requires CustomMetadataRequest with CustomMetadata property
            var metadataRequest = new VaultSharp.V1.SecretsEngines.KeyValue.V2.CustomMetadataRequest
            {
                CustomMetadata = customMetadata
            };
            await _vaultClient.V1.Secrets.KeyValue.V2.WriteSecretMetadataAsync(
                secretId,
                metadataRequest,
                _mountPoint);

            var rotationWindowEndsAt = DateTime.UtcNow.AddHours(rotationWindowHours);

            _logger.LogInformation("Rotated secret {SecretId} from version {PreviousVersion} to {NewVersion}. Rotation window ends at {EndsAt}",
                secretId, previousVersion, newVersion, rotationWindowEndsAt);

            return SecretRotationResult.CreateSuccess(secretId, newVersion, previousVersion, rotationWindowEndsAt);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rotate secret {SecretId}", secretId);
            return SecretRotationResult.CreateFailure(secretId, ex.Message);
        }
    }

    /// <summary>
    /// Deletes a secret and all its versions from Vault.
    /// </summary>
    public async Task<bool> DeleteSecretAsync(string secretId, CancellationToken cancellationToken = default)
    {
        try
        {
            await _retryPolicy.ExecuteAsync(async () =>
            {
                await _vaultClient.V1.Secrets.KeyValue.V2.DeleteMetadataAsync(
                    path: secretId,
                    mountPoint: _mountPoint);
            });

            _logger.LogInformation("Deleted secret {SecretId} and all versions from Vault", secretId);
            return true;
        }
        catch (VaultApiException ex) when (ex.StatusCode == 404)
        {
            _logger.LogWarning("Secret {SecretId} not found for deletion", secretId);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete secret {SecretId} from Vault", secretId);
            throw;
        }
    }

    /// <summary>
    /// Lists all secrets (metadata only).
    /// </summary>
    public async Task<List<DomainSecretMetadata>> ListSecretsAsync(
        Dictionary<string, string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _retryPolicy.ExecuteAsync(async () =>
            {
                return await _vaultClient.V1.Secrets.KeyValue.V2.ReadSecretPathsAsync(
                    path: "",
                    mountPoint: _mountPoint);
            });

            var secretList = new List<DomainSecretMetadata>();

            if (result?.Data?.Keys == null)
                return secretList;

            foreach (var secretId in result.Data.Keys)
            {
                var metadata = await GetSecretMetadataAsync(secretId, cancellationToken);
                if (metadata == null)
                    continue;

                // Filter by tags if specified
                if (tags != null && tags.Any())
                {
                    var matchesAllTags = tags.All(tagFilter =>
                        metadata.Tags.ContainsKey(tagFilter.Key) &&
                        metadata.Tags[tagFilter.Key] == tagFilter.Value);

                    if (!matchesAllTags)
                        continue;
                }

                secretList.Add(metadata);
            }

            _logger.LogDebug("Listed {Count} secrets from Vault", secretList.Count);
            return secretList;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list secrets from Vault");
            throw;
        }
    }

    /// <summary>
    /// Checks if a secret is approaching expiration.
    /// </summary>
    public async Task<bool> IsSecretExpiringAsync(string secretId, CancellationToken cancellationToken = default)
    {
        var metadata = await GetSecretMetadataAsync(secretId, cancellationToken);
        if (metadata == null)
            return false;

        var thresholdDays = metadata.RotationPolicy?.NotificationThresholdDays ?? 7;
        var daysUntilExpiration = metadata.DaysUntilExpiration;

        if (!daysUntilExpiration.HasValue)
            return false; // No expiration set

        var isExpiring = daysUntilExpiration.Value <= thresholdDays;

        if (isExpiring)
        {
            _logger.LogWarning("Secret {SecretId} is expiring in {Days} days (threshold: {Threshold} days)",
                secretId, daysUntilExpiration.Value, thresholdDays);
        }

        return isExpiring;
    }

    /// <summary>
    /// Extends the rotation window for a secret.
    /// </summary>
    public async Task<bool> ExtendRotationWindowAsync(
        string secretId,
        int additionalHours,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var metadata = await GetSecretMetadataAsync(secretId, cancellationToken);
            if (metadata == null)
                return false;

            if (!metadata.IsInRotationWindow)
            {
                _logger.LogWarning("Cannot extend rotation window for {SecretId}: not currently in rotation", secretId);
                return false;
            }

            // Update rotation window hours in metadata
            var customMetadata = metadata.Tags != null ? new Dictionary<string, string>(metadata.Tags) : new Dictionary<string, string>();

            var currentWindowHours = metadata.RotationPolicy?.RotationWindowHours ?? 24;
            var newWindowHours = currentWindowHours + additionalHours;

            customMetadata["rotation_window_hours"] = newWindowHours.ToString();
            customMetadata["previous_version"] = metadata.PreviousVersion.ToString()!;
            customMetadata["last_rotated_at"] = metadata.LastRotatedAt?.ToString("O") ?? DateTime.UtcNow.ToString("O");

            // Preserve other rotation policy settings
            if (metadata.RotationPolicy != null)
            {
                if (metadata.RotationPolicy.RotationIntervalDays.HasValue)
                    customMetadata["rotation_interval_days"] = metadata.RotationPolicy.RotationIntervalDays.Value.ToString();
                if (metadata.RotationPolicy.MaxAgeDays.HasValue)
                    customMetadata["max_age_days"] = metadata.RotationPolicy.MaxAgeDays.Value.ToString();
                customMetadata["notification_threshold_days"] = metadata.RotationPolicy.NotificationThresholdDays.ToString();
                customMetadata["auto_rotate"] = metadata.RotationPolicy.EnableAutomaticRotation.ToString();
            }

            // VaultSharp 1.17.5.1 API: Requires CustomMetadataRequest with CustomMetadata property
            var metadataRequest = new VaultSharp.V1.SecretsEngines.KeyValue.V2.CustomMetadataRequest
            {
                CustomMetadata = customMetadata
            };
            await _vaultClient.V1.Secrets.KeyValue.V2.WriteSecretMetadataAsync(
                secretId,
                metadataRequest,
                _mountPoint);

            _logger.LogInformation("Extended rotation window for {SecretId} by {Hours} hours (new window: {NewWindow} hours)",
                secretId, additionalHours, newWindowHours);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extend rotation window for {SecretId}", secretId);
            return false;
        }
    }

    #region Private Helper Methods

    private void ValidateConfiguration()
    {
        if (string.IsNullOrWhiteSpace(_config.VaultUrl))
            throw new ArgumentException("Vault URL is required", nameof(_config.VaultUrl));

        if (_config.AuthMethod == VaultAuthMethod.Token && string.IsNullOrWhiteSpace(_config.Token))
            throw new ArgumentException("Token is required for Token authentication", nameof(_config.Token));

        if (_config.AuthMethod == VaultAuthMethod.AppRole)
        {
            if (string.IsNullOrWhiteSpace(_config.AppRoleId))
                throw new ArgumentException("AppRole ID is required for AppRole authentication", nameof(_config.AppRoleId));
            if (string.IsNullOrWhiteSpace(_config.AppRoleSecretId))
                throw new ArgumentException("AppRole Secret ID is required for AppRole authentication", nameof(_config.AppRoleSecretId));
        }
    }

    private IVaultClient CreateVaultClient()
    {
        IAuthMethodInfo authMethod = _config.AuthMethod switch
        {
            VaultAuthMethod.Token => new TokenAuthMethodInfo(_config.Token!),
            VaultAuthMethod.AppRole => new AppRoleAuthMethodInfo(_config.AppRoleId!, _config.AppRoleSecretId!),
            _ => throw new NotSupportedException($"Auth method {_config.AuthMethod} is not supported")
        };

        var vaultClientSettings = new VaultClientSettings(_config.VaultUrl, authMethod)
        {
            Namespace = _config.Namespace,
            VaultServiceTimeout = TimeSpan.FromSeconds(_config.TimeoutSeconds)
        };

        // TLS certificate validation is always enabled for security
        return new VaultClient(vaultClientSettings);
    }

    private AsyncRetryPolicy CreateRetryPolicy()
    {
        return Policy
            .Handle<HttpRequestException>()
            .Or<VaultApiException>(ex => ex.StatusCode >= 500) // Retry on server errors
            .WaitAndRetryAsync(
                retryCount: _config.RetryAttempts,
                sleepDurationProvider: retryAttempt => TimeSpan.FromMilliseconds(_config.RetryDelayMs * Math.Pow(2, retryAttempt - 1)),
                onRetry: (exception, timeSpan, retryCount, context) =>
                {
                    _logger.LogWarning(exception,
                        "Vault operation failed. Retry {RetryCount}/{MaxRetries} in {Delay}ms",
                        retryCount, _config.RetryAttempts, timeSpan.TotalMilliseconds);
                });
    }

    private string GenerateRandomSecret()
    {
        const int length = 64;
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*()-_=+[]{}|;:,.<>?";

        using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();

        // Eliminate modulo bias by rejecting bytes that would cause uneven distribution
        // With 87 characters and 256 byte values, we reject bytes >= 261 (256 - (256 % 87))
        // This ensures each character has exactly the same probability of being selected
        var maxValid = 256 - (256 % chars.Length); // 261 for 87 chars
        var result = new char[length];
        var position = 0;

        while (position < length)
        {
            var buffer = new byte[length - position];
            rng.GetBytes(buffer);

            foreach (var b in buffer)
            {
                if (b < maxValid)
                {
                    result[position] = chars[b % chars.Length];
                    position++;
                    if (position >= length)
                        break;
                }
                // Reject bytes >= maxValid to eliminate modulo bias
            }
        }

        return new string(result);
    }

    #endregion
}
