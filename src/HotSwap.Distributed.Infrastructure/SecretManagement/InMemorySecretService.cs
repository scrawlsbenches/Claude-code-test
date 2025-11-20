using System.Collections.Concurrent;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Interfaces;
using Microsoft.Extensions.Logging;

namespace HotSwap.Distributed.Infrastructure.SecretManagement;

/// <summary>
/// In-memory implementation of ISecretService for development and testing.
/// Not suitable for production use.
/// </summary>
public class InMemorySecretService : ISecretService
{
    private readonly ConcurrentDictionary<string, List<SecretVersion>> _secrets = new();
    private readonly ConcurrentDictionary<string, SecretMetadata> _metadata = new();
    private readonly ILogger<InMemorySecretService> _logger;

    public InMemorySecretService(ILogger<InMemorySecretService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _logger.LogWarning("Using InMemorySecretService - NOT suitable for production");
    }

    public Task<SecretVersion?> GetSecretAsync(string secretId, CancellationToken cancellationToken = default)
    {
        if (!_secrets.TryGetValue(secretId, out var versions) || versions.Count == 0)
            return Task.FromResult<SecretVersion?>(null);

        var currentVersion = versions.OrderByDescending(v => v.Version).First();
        return Task.FromResult<SecretVersion?>(currentVersion);
    }

    public Task<SecretVersion?> GetSecretVersionAsync(string secretId, int version, CancellationToken cancellationToken = default)
    {
        if (!_secrets.TryGetValue(secretId, out var versions))
            return Task.FromResult<SecretVersion?>(null);

        var specificVersion = versions.FirstOrDefault(v => v.Version == version);
        return Task.FromResult(specificVersion);
    }

    public Task<SecretMetadata?> GetSecretMetadataAsync(string secretId, CancellationToken cancellationToken = default)
    {
        _metadata.TryGetValue(secretId, out var metadata);
        return Task.FromResult(metadata);
    }

    public Task<SecretVersion> SetSecretAsync(
        string secretId,
        string value,
        RotationPolicy? rotationPolicy = null,
        Dictionary<string, string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        var versions = _secrets.GetOrAdd(secretId, _ => new List<SecretVersion>());
        var newVersion = versions.Count + 1;

        var secretVersion = new SecretVersion
        {
            SecretId = secretId,
            Version = newVersion,
            Value = value,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = rotationPolicy?.MaxAgeDays.HasValue == true
                ? DateTime.UtcNow.AddDays(rotationPolicy.MaxAgeDays.Value)
                : null,
            IsActive = true,
            IsDeleted = false
        };

        versions.Add(secretVersion);

        var metadata = new SecretMetadata
        {
            SecretId = secretId,
            CurrentVersion = newVersion,
            PreviousVersion = newVersion > 1 ? newVersion - 1 : null,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = secretVersion.ExpiresAt,
            LastRotatedAt = null,
            NextRotationAt = rotationPolicy?.RotationIntervalDays.HasValue == true
                ? DateTime.UtcNow.AddDays(rotationPolicy.RotationIntervalDays.Value)
                : null,
            RotationPolicy = rotationPolicy,
            Tags = tags ?? new Dictionary<string, string>()
        };

        _metadata[secretId] = metadata;

        _logger.LogInformation("Set secret {SecretId} version {Version}", secretId, newVersion);

        return Task.FromResult(secretVersion);
    }

    public Task<SecretRotationResult> RotateSecretAsync(
        string secretId,
        string? newValue = null,
        CancellationToken cancellationToken = default)
    {
        if (!_metadata.TryGetValue(secretId, out var metadata))
            return Task.FromResult(SecretRotationResult.CreateFailure(secretId, "Secret not found"));

        var previousVersion = metadata.CurrentVersion;

        if (string.IsNullOrWhiteSpace(newValue))
            newValue = GenerateRandomSecret();

        var newSecretVersion = new SecretVersion
        {
            SecretId = secretId,
            Version = previousVersion + 1,
            Value = newValue,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = metadata.RotationPolicy?.MaxAgeDays.HasValue == true
                ? DateTime.UtcNow.AddDays(metadata.RotationPolicy.MaxAgeDays.Value)
                : null,
            IsActive = true,
            IsDeleted = false
        };

        var versions = _secrets[secretId];
        versions.Add(newSecretVersion);

        metadata.CurrentVersion = newSecretVersion.Version;
        metadata.PreviousVersion = previousVersion;
        metadata.LastRotatedAt = DateTime.UtcNow;

        var rotationWindowHours = metadata.RotationPolicy?.RotationWindowHours ?? 24;
        var rotationWindowEndsAt = DateTime.UtcNow.AddHours(rotationWindowHours);

        _logger.LogInformation("Rotated secret {SecretId} from version {PreviousVersion} to {NewVersion}",
            secretId, previousVersion, newSecretVersion.Version);

        return Task.FromResult(SecretRotationResult.CreateSuccess(
            secretId, newSecretVersion.Version, previousVersion, rotationWindowEndsAt));
    }

    public Task<bool> DeleteSecretAsync(string secretId, CancellationToken cancellationToken = default)
    {
        var removed = _secrets.TryRemove(secretId, out _);
        _metadata.TryRemove(secretId, out _);

        if (removed)
            _logger.LogInformation("Deleted secret {SecretId}", secretId);

        return Task.FromResult(removed);
    }

    public Task<List<SecretMetadata>> ListSecretsAsync(
        Dictionary<string, string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        var secretsList = _metadata.Values.ToList();

        if (tags != null && tags.Any())
        {
            secretsList = secretsList.Where(metadata =>
                tags.All(tagFilter =>
                    metadata.Tags.ContainsKey(tagFilter.Key) &&
                    metadata.Tags[tagFilter.Key] == tagFilter.Value))
                .ToList();
        }

        return Task.FromResult(secretsList);
    }

    public Task<bool> IsSecretExpiringAsync(string secretId, CancellationToken cancellationToken = default)
    {
        if (!_metadata.TryGetValue(secretId, out var metadata))
            return Task.FromResult(false);

        var thresholdDays = metadata.RotationPolicy?.NotificationThresholdDays ?? 7;
        var daysUntilExpiration = metadata.DaysUntilExpiration;

        if (!daysUntilExpiration.HasValue)
            return Task.FromResult(false);

        return Task.FromResult(daysUntilExpiration.Value <= thresholdDays);
    }

    public Task<bool> ExtendRotationWindowAsync(
        string secretId,
        int additionalHours,
        CancellationToken cancellationToken = default)
    {
        if (!_metadata.TryGetValue(secretId, out var metadata))
            return Task.FromResult(false);

        if (!metadata.IsInRotationWindow)
            return Task.FromResult(false);

        if (metadata.RotationPolicy != null)
        {
            metadata.RotationPolicy.RotationWindowHours += additionalHours;
            _logger.LogInformation("Extended rotation window for {SecretId} by {Hours} hours",
                secretId, additionalHours);
        }

        return Task.FromResult(true);
    }

    private string GenerateRandomSecret()
    {
        const int length = 64;
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*()-_=+[]{}|;:,.<>?";
        var random = new Random();
        return new string(Enumerable.Repeat(chars, length)
            .Select(s => s[random.Next(s.Length)]).ToArray());
    }
}
