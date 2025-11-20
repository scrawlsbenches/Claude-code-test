namespace HotSwap.Distributed.Domain.Models;

/// <summary>
/// Metadata about a managed secret, including version and expiration information.
/// </summary>
public class SecretMetadata
{
    /// <summary>
    /// Unique identifier for the secret (e.g., "jwt-signing-key", "database-password").
    /// </summary>
    public string SecretId { get; set; } = string.Empty;

    /// <summary>
    /// Current version number of the secret.
    /// </summary>
    public int CurrentVersion { get; set; }

    /// <summary>
    /// Previous version number (null if no previous version).
    /// Used during rotation window to validate tokens signed with old key.
    /// </summary>
    public int? PreviousVersion { get; set; }

    /// <summary>
    /// Timestamp when the current version was created (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Timestamp when the secret expires (UTC).
    /// Null if the secret has no expiration.
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// Timestamp when the secret was last rotated (UTC).
    /// </summary>
    public DateTime? LastRotatedAt { get; set; }

    /// <summary>
    /// Timestamp when the next rotation is scheduled (UTC).
    /// </summary>
    public DateTime? NextRotationAt { get; set; }

    /// <summary>
    /// Rotation policy associated with this secret.
    /// </summary>
    public RotationPolicy? RotationPolicy { get; set; }

    /// <summary>
    /// Tags for categorizing and filtering secrets.
    /// </summary>
    public Dictionary<string, string> Tags { get; set; } = new();

    /// <summary>
    /// Indicates if the secret is currently in a rotation window.
    /// During rotation, both current and previous versions are valid.
    /// </summary>
    public bool IsInRotationWindow => PreviousVersion.HasValue;

    /// <summary>
    /// Number of days until the secret expires.
    /// Null if the secret has no expiration.
    /// </summary>
    public int? DaysUntilExpiration => ExpiresAt.HasValue
        ? (int)(ExpiresAt.Value - DateTime.UtcNow).TotalDays
        : null;
}

/// <summary>
/// Represents a specific version of a secret.
/// </summary>
public class SecretVersion
{
    /// <summary>
    /// Secret identifier.
    /// </summary>
    public string SecretId { get; set; } = string.Empty;

    /// <summary>
    /// Version number.
    /// </summary>
    public int Version { get; set; }

    /// <summary>
    /// The actual secret value (will be encrypted in transit and at rest).
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when this version was created (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Timestamp when this version expires (UTC).
    /// Null if no expiration.
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// Indicates if this version is currently active.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Indicates if this version has been marked for deletion.
    /// </summary>
    public bool IsDeleted { get; set; }
}

/// <summary>
/// Policy defining how and when a secret should be rotated.
/// </summary>
public class RotationPolicy
{
    /// <summary>
    /// Interval in days between automatic rotations.
    /// Null to disable automatic rotation.
    /// </summary>
    public int? RotationIntervalDays { get; set; }

    /// <summary>
    /// Maximum age in days before a secret must be rotated.
    /// Null for no maximum age.
    /// </summary>
    public int? MaxAgeDays { get; set; }

    /// <summary>
    /// Number of days before expiration to send notification.
    /// Default: 7 days.
    /// </summary>
    public int NotificationThresholdDays { get; set; } = 7;

    /// <summary>
    /// Duration in hours that both old and new versions are valid during rotation.
    /// This allows for gradual rollover without service interruption.
    /// Default: 24 hours.
    /// </summary>
    public int RotationWindowHours { get; set; } = 24;

    /// <summary>
    /// Indicates if rotation should be automatic based on the policy.
    /// </summary>
    public bool EnableAutomaticRotation { get; set; }

    /// <summary>
    /// List of notification recipients (email addresses or webhook URLs).
    /// </summary>
    public List<string> NotificationRecipients { get; set; } = new();
}

/// <summary>
/// Result of a secret rotation operation.
/// </summary>
public class SecretRotationResult
{
    /// <summary>
    /// Indicates if the rotation was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Secret identifier that was rotated.
    /// </summary>
    public string SecretId { get; set; } = string.Empty;

    /// <summary>
    /// New version number after rotation.
    /// </summary>
    public int NewVersion { get; set; }

    /// <summary>
    /// Previous version number (now deprecated).
    /// </summary>
    public int? PreviousVersion { get; set; }

    /// <summary>
    /// Timestamp when the rotation occurred (UTC).
    /// </summary>
    public DateTime RotatedAt { get; set; }

    /// <summary>
    /// Timestamp when the rotation window ends and old version is no longer valid (UTC).
    /// </summary>
    public DateTime RotationWindowEndsAt { get; set; }

    /// <summary>
    /// Error message if rotation failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Additional details about the rotation operation.
    /// </summary>
    public Dictionary<string, string> Details { get; set; } = new();

    /// <summary>
    /// Creates a successful rotation result.
    /// </summary>
    public static SecretRotationResult CreateSuccess(
        string secretId,
        int newVersion,
        int? previousVersion,
        DateTime rotationWindowEndsAt)
    {
        return new SecretRotationResult
        {
            Success = true,
            SecretId = secretId,
            NewVersion = newVersion,
            PreviousVersion = previousVersion,
            RotatedAt = DateTime.UtcNow,
            RotationWindowEndsAt = rotationWindowEndsAt
        };
    }

    /// <summary>
    /// Creates a failed rotation result.
    /// </summary>
    public static SecretRotationResult CreateFailure(string secretId, string errorMessage)
    {
        return new SecretRotationResult
        {
            Success = false,
            SecretId = secretId,
            RotatedAt = DateTime.UtcNow,
            ErrorMessage = errorMessage
        };
    }
}
