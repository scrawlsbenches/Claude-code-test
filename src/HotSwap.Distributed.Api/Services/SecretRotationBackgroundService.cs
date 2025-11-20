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
    private readonly ILogger<SecretRotationBackgroundService> _logger;
    private readonly SecretRotationConfiguration _config;

    public SecretRotationBackgroundService(
        ISecretService secretService,
        ILogger<SecretRotationBackgroundService> logger,
        IConfiguration configuration)
    {
        _secretService = secretService ?? throw new ArgumentNullException(nameof(secretService));
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
            // Initial check on startup
            await CheckAndRotateSecretsAsync(stoppingToken);

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
        if (metadata.RotationPolicy?.RotationIntervalDays == null)
            return false;

        // Calculate when rotation should occur
        var rotationDue = metadata.LastRotatedAt?.AddDays(metadata.RotationPolicy.RotationIntervalDays.Value)
            ?? metadata.CreatedAt.AddDays(metadata.RotationPolicy.RotationIntervalDays.Value);

        return DateTime.UtcNow >= rotationDue;
    }

    private Task SendRotationNotificationAsync(
        SecretMetadata metadata,
        SecretRotationResult result,
        CancellationToken cancellationToken)
    {
        var recipients = metadata.RotationPolicy?.NotificationRecipients;
        if (recipients == null || !recipients.Any())
            return Task.CompletedTask;

        var message = $"Secret '{metadata.SecretId}' has been automatically rotated. " +
                     $"Old Version: {result.PreviousVersion}, New Version: {result.NewVersion}, " +
                     $"Rotation Window Ends: {result.RotationWindowEndsAt:yyyy-MM-dd HH:mm:ss} UTC. " +
                     $"Both versions are valid until the rotation window ends.";

        _logger.LogWarning("NOTIFICATION REQUIRED: {Message} | Recipients: {Recipients}",
            message, string.Join(", ", recipients));

        // TODO: Integrate with actual notification service (email, Slack, etc.)
        // For now, notifications are logged for administrators to monitor

        return Task.CompletedTask;
    }

    private Task SendExpirationWarningAsync(
        SecretMetadata metadata,
        CancellationToken cancellationToken)
    {
        var recipients = metadata.RotationPolicy?.NotificationRecipients;
        if (recipients == null || !recipients.Any())
            return Task.CompletedTask;

        var daysRemaining = metadata.DaysUntilExpiration ?? 0;
        var message = $"Secret '{metadata.SecretId}' is approaching expiration. " +
                     $"Days Remaining: {daysRemaining}, " +
                     $"Expiration Date: {metadata.ExpiresAt:yyyy-MM-dd HH:mm:ss} UTC. " +
                     $"Please rotate this secret soon to avoid service interruption.";

        _logger.LogWarning("NOTIFICATION REQUIRED: {Message} | Recipients: {Recipients}",
            message, string.Join(", ", recipients));

        // TODO: Integrate with actual notification service (email, Slack, etc.)
        // For now, notifications are logged for administrators to monitor

        return Task.CompletedTask;
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
    /// </summary>
    public int CheckIntervalMinutes { get; set; } = 60;

    /// <summary>
    /// Default rotation policy applied to secrets without explicit policies.
    /// </summary>
    public RotationPolicy? DefaultRotationPolicy { get; set; }

    /// <summary>
    /// Specific rotation policy for JWT signing keys.
    /// </summary>
    public RotationPolicy? JwtSigningKeyPolicy { get; set; }
}
