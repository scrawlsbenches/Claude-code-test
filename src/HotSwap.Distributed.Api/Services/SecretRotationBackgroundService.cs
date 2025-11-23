using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Interfaces;

namespace HotSwap.Distributed.Api.Services;

/// <summary>
/// Background service for automatically rotating secrets based on their rotation policies.
/// Monitors secret expiration and triggers rotation when thresholds are reached.
/// </summary>
public class SecretRotationBackgroundService : BackgroundService
{
    private readonly ISecretService _secretService;
    private readonly INotificationService _notificationService;
    private readonly ILogger<SecretRotationBackgroundService> _logger;
    private readonly SecretRotationConfiguration _config;

    public SecretRotationBackgroundService(
        ISecretService secretService,
        INotificationService notificationService,
        ILogger<SecretRotationBackgroundService> logger,
        IConfiguration configuration)
    {
        _secretService = secretService ?? throw new ArgumentNullException(nameof(secretService));
        _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _config = new SecretRotationConfiguration();
        configuration.GetSection("SecretRotation").Bind(_config);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_config.Enabled)
        {
            _logger.LogInformation("Secret rotation is disabled in configuration");
            return;
        }

        _logger.LogInformation("Secret rotation background service starting. Check interval: {Interval} minutes",
            _config.CheckIntervalMinutes);

        var checkInterval = TimeSpan.FromMinutes(_config.CheckIntervalMinutes);
        using var timer = new PeriodicTimer(checkInterval);

        try
        {
            // Wait for first interval before checking (no initial check on startup)
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await CheckAndRotateSecretsAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Secret rotation background service stopping");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in secret rotation background service");
            throw; // Let the application crash if critical service fails
        }
    }

    private async Task CheckAndRotateSecretsAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("Starting secret rotation check");

            var secrets = await _secretService.ListSecretsAsync(cancellationToken: cancellationToken);
            var rotationCount = 0;
            var notificationCount = 0;

            foreach (var secretMetadata in secrets)
            {
                try
                {
                    // Skip if automatic rotation is disabled for this secret
                    if (secretMetadata.RotationPolicy?.EnableAutomaticRotation != true)
                        continue;

                    // Check if secret needs rotation
                    var needsRotation = ShouldRotateSecret(secretMetadata);
                    if (needsRotation)
                    {
                        _logger.LogInformation("Rotating secret {SecretId} - rotation interval reached",
                            secretMetadata.SecretId);

                        var result = await _secretService.RotateSecretAsync(
                            secretMetadata.SecretId,
                            newValue: null, // Generate new value
                            cancellationToken);

                        if (result.Success)
                        {
                            rotationCount++;
                            _logger.LogInformation("Successfully rotated secret {SecretId} from version {OldVersion} to {NewVersion}",
                                secretMetadata.SecretId, result.PreviousVersion, result.NewVersion);

                            // Notify about successful rotation
                            await SendRotationNotificationAsync(secretMetadata, result, cancellationToken);
                        }
                        else
                        {
                            _logger.LogError("Failed to rotate secret {SecretId}: {Error}",
                                secretMetadata.SecretId, result.ErrorMessage);
                        }
                    }
                    // Check if secret is approaching expiration
                    else if (await _secretService.IsSecretExpiringAsync(secretMetadata.SecretId, cancellationToken))
                    {
                        _logger.LogWarning("Secret {SecretId} is approaching expiration: {Days} days remaining",
                            secretMetadata.SecretId, secretMetadata.DaysUntilExpiration);

                        await SendExpirationWarningAsync(secretMetadata, cancellationToken);
                        notificationCount++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing secret {SecretId} for rotation", secretMetadata.SecretId);
                    // Continue with next secret
                }
            }

            if (rotationCount > 0 || notificationCount > 0)
            {
                _logger.LogInformation("Secret rotation check completed: {RotationCount} rotated, {NotificationCount} warnings sent",
                    rotationCount, notificationCount);
            }
            else
            {
                _logger.LogDebug("Secret rotation check completed: no actions needed");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during secret rotation check");
            // Don't throw - allow service to continue on next interval
        }
    }

    private bool ShouldRotateSecret(SecretMetadata metadata)
    {
        // If NextRotationAt is explicitly set, use it
        if (metadata.NextRotationAt.HasValue)
        {
            return DateTime.UtcNow >= metadata.NextRotationAt.Value;
        }

        // Otherwise calculate based on rotation policy
        if (metadata.RotationPolicy?.RotationIntervalDays == null)
            return false;

        // Calculate when rotation should occur
        var rotationDue = metadata.LastRotatedAt?.AddDays(metadata.RotationPolicy.RotationIntervalDays.Value)
            ?? metadata.CreatedAt.AddDays(metadata.RotationPolicy.RotationIntervalDays.Value);

        return DateTime.UtcNow >= rotationDue;
    }

    private async Task SendRotationNotificationAsync(
        SecretMetadata metadata,
        SecretRotationResult result,
        CancellationToken cancellationToken)
    {
        var recipients = metadata.RotationPolicy?.NotificationRecipients;
        if (recipients == null || !recipients.Any())
            return;

        try
        {
            await _notificationService.SendSecretRotationNotificationAsync(
                recipients,
                metadata.SecretId,
                result.PreviousVersion ?? 0,
                result.NewVersion,
                result.RotationWindowEndsAt,
                cancellationToken);

            _logger.LogInformation("Secret rotation notification sent for {SecretId} to {Recipients}",
                metadata.SecretId, string.Join(", ", recipients));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send secret rotation notification for {SecretId}", metadata.SecretId);
            // Don't throw - notification failure shouldn't stop rotation process
        }
    }

    private async Task SendExpirationWarningAsync(
        SecretMetadata metadata,
        CancellationToken cancellationToken)
    {
        var recipients = metadata.RotationPolicy?.NotificationRecipients;
        if (recipients == null || !recipients.Any())
            return;

        var daysRemaining = metadata.DaysUntilExpiration ?? 0;
        var expiresAt = metadata.ExpiresAt ?? DateTime.UtcNow;

        try
        {
            await _notificationService.SendSecretExpirationWarningAsync(
                recipients,
                metadata.SecretId,
                daysRemaining,
                expiresAt,
                cancellationToken);

            _logger.LogInformation("Secret expiration warning sent for {SecretId} to {Recipients}",
                metadata.SecretId, string.Join(", ", recipients));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send secret expiration warning for {SecretId}", metadata.SecretId);
            // Don't throw - notification failure shouldn't stop monitoring process
        }
    }
}

/// <summary>
/// Configuration for secret rotation background service.
/// </summary>
public class SecretRotationConfiguration
{
    /// <summary>
    /// Enables or disables the secret rotation background service.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Interval in minutes between secret rotation checks.
    /// Default: 60 minutes (1 hour).
    /// Supports fractional minutes for testing (e.g., 0.00017 = ~10ms).
    /// </summary>
    public double CheckIntervalMinutes { get; set; } = 60;

    /// <summary>
    /// Default rotation policy applied to secrets without explicit policies.
    /// </summary>
    public RotationPolicy? DefaultRotationPolicy { get; set; }

    /// <summary>
    /// Specific rotation policy for JWT signing keys.
    /// </summary>
    public RotationPolicy? JwtSigningKeyPolicy { get; set; }
}
