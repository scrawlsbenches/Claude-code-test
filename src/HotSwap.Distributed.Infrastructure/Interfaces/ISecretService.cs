using HotSwap.Distributed.Domain.Models;

namespace HotSwap.Distributed.Infrastructure.Interfaces;

/// <summary>
/// Service for managing secrets with versioning, rotation, and expiration tracking.
/// Abstracts the underlying secret store (HashiCorp Vault, Kubernetes Secrets, etc.).
/// </summary>
public interface ISecretService
{
    /// <summary>
    /// Retrieves the current version of a secret.
    /// </summary>
    /// <param name="secretId">Unique identifier for the secret</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Secret value and metadata, or null if not found</returns>
    Task<SecretVersion?> GetSecretAsync(string secretId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a specific version of a secret.
    /// </summary>
    /// <param name="secretId">Unique identifier for the secret</param>
    /// <param name="version">Version number to retrieve</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Secret value and metadata for the specified version, or null if not found</returns>
    Task<SecretVersion?> GetSecretVersionAsync(string secretId, int version, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves metadata about a secret without fetching the secret value.
    /// </summary>
    /// <param name="secretId">Unique identifier for the secret</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Secret metadata, or null if not found</returns>
    Task<SecretMetadata?> GetSecretMetadataAsync(string secretId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates or updates a secret.
    /// </summary>
    /// <param name="secretId">Unique identifier for the secret</param>
    /// <param name="value">Secret value to store</param>
    /// <param name="rotationPolicy">Optional rotation policy for the secret</param>
    /// <param name="tags">Optional tags for categorizing the secret</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The created secret version</returns>
    Task<SecretVersion> SetSecretAsync(
        string secretId,
        string value,
        RotationPolicy? rotationPolicy = null,
        Dictionary<string, string>? tags = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Rotates a secret by creating a new version and marking the previous version as deprecated.
    /// Both versions remain valid during the rotation window period.
    /// </summary>
    /// <param name="secretId">Unique identifier for the secret to rotate</param>
    /// <param name="newValue">New secret value (if null, generates a random value)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Rotation result with new and previous version information</returns>
    Task<SecretRotationResult> RotateSecretAsync(
        string secretId,
        string? newValue = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a secret and all its versions.
    /// </summary>
    /// <param name="secretId">Unique identifier for the secret to delete</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if deleted successfully, false if not found</returns>
    Task<bool> DeleteSecretAsync(string secretId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all secrets (metadata only, without values).
    /// </summary>
    /// <param name="tags">Optional tags to filter by</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of secret metadata</returns>
    Task<List<SecretMetadata>> ListSecretsAsync(
        Dictionary<string, string>? tags = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a secret is approaching expiration based on its rotation policy.
    /// </summary>
    /// <param name="secretId">Unique identifier for the secret</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if secret should be rotated soon, false otherwise</returns>
    Task<bool> IsSecretExpiringAsync(string secretId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Extends the rotation window for a secret, keeping both old and new versions valid.
    /// Useful when rollback or extended migration is needed.
    /// </summary>
    /// <param name="secretId">Unique identifier for the secret</param>
    /// <param name="additionalHours">Number of hours to extend the rotation window</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if extended successfully, false if not found or not in rotation</returns>
    Task<bool> ExtendRotationWindowAsync(
        string secretId,
        int additionalHours,
        CancellationToken cancellationToken = default);
}
