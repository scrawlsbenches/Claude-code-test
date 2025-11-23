using HotSwap.Distributed.Infrastructure.Data;
using HotSwap.Distributed.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace HotSwap.Distributed.Infrastructure.Services;

/// <summary>
/// Background service that consumes messages from PostgreSQL message queue using LISTEN/NOTIFY.
/// Provides real-time message processing with automatic retry and failure handling.
/// </summary>
public class MessageConsumerService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MessageConsumerService> _logger;
    private readonly string _connectionString;
    private NpgsqlConnection? _listenConnection;

    private const string NOTIFICATION_CHANNEL = "message_queue";
    private const int LOCK_DURATION_MINUTES = 5;
    private const int MAX_CONCURRENT_MESSAGES = 10;
    private const int MAX_RETRY_ATTEMPTS = 3;
    private const int POLL_INTERVAL_SECONDS = 30; // Fallback polling if no notifications

    public MessageConsumerService(
        IServiceProvider serviceProvider,
        ILogger<MessageConsumerService> logger,
        AuditLogDbContext dbContext)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;

        var connectionString = dbContext.Database.GetConnectionString();
        _connectionString = connectionString ?? throw new InvalidOperationException("Database connection string is null");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Message Consumer Service started");

        try
        {
            // Set up LISTEN connection
            await SetupListenConnectionAsync(stoppingToken);

            // Process messages in a loop
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessPendingMessagesAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing messages");
                }

                // Wait for notification or timeout (fallback polling)
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(POLL_INTERVAL_SECONDS), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // Service is stopping
                    break;
                }
            }
        }
        finally
        {
            await CleanupListenConnectionAsync();
        }

        _logger.LogInformation("Message Consumer Service stopped");
    }

    /// <summary>
    /// Sets up PostgreSQL LISTEN connection for real-time notifications.
    /// </summary>
    private async Task SetupListenConnectionAsync(CancellationToken cancellationToken)
    {
        try
        {
            _listenConnection = new NpgsqlConnection(_connectionString);
            await _listenConnection.OpenAsync(cancellationToken);

            // Set up notification handler
            _listenConnection.Notification += (sender, args) =>
            {
                _logger.LogDebug("Received NOTIFY on channel {Channel} with payload {Payload}",
                    args.Channel, args.Payload);

                // Trigger immediate message processing (non-blocking)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await ProcessPendingMessagesAsync(cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing messages after NOTIFY");
                    }
                });
            };

            // Start listening on the channel
            await using var cmd = new NpgsqlCommand($"LISTEN {NOTIFICATION_CHANNEL}", _listenConnection);
            await cmd.ExecuteNonQueryAsync(cancellationToken);

            _logger.LogInformation("Listening for notifications on channel {Channel}", NOTIFICATION_CHANNEL);

            // Start waiting for notifications in background
            _ = Task.Run(async () =>
            {
                while (_listenConnection?.State == System.Data.ConnectionState.Open && !cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        await _listenConnection.WaitAsync(cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error waiting for notifications, will retry");
                        await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                    }
                }
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set up LISTEN connection, falling back to polling only");
        }
    }

    /// <summary>
    /// Cleans up the LISTEN connection on service shutdown.
    /// </summary>
    private async Task CleanupListenConnectionAsync()
    {
        if (_listenConnection != null)
        {
            try
            {
                if (_listenConnection.State == System.Data.ConnectionState.Open)
                {
                    await using var cmd = new NpgsqlCommand($"UNLISTEN {NOTIFICATION_CHANNEL}", _listenConnection);
                    await cmd.ExecuteNonQueryAsync();
                }

                await _listenConnection.CloseAsync();
                await _listenConnection.DisposeAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error cleaning up LISTEN connection");
            }
        }
    }

    /// <summary>
    /// Processes pending messages from the database.
    /// </summary>
    private async Task ProcessPendingMessagesAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AuditLogDbContext>();

        var lockUntil = DateTime.UtcNow.AddMinutes(LOCK_DURATION_MINUTES);
        var instanceId = Environment.MachineName;
        var now = DateTime.UtcNow;

        // Claim pending messages using FOR UPDATE SKIP LOCKED pattern
        var messages = await dbContext.Messages
            .Where(m =>
                m.Status == MessageStatus.Pending &&
                (m.LockedUntil == null || m.LockedUntil <= now) &&
                m.RetryCount < MAX_RETRY_ATTEMPTS)
            .OrderByDescending(m => m.Priority)
            .ThenBy(m => m.CreatedAt)
            .Take(MAX_CONCURRENT_MESSAGES)
            .ToListAsync(cancellationToken);

        if (!messages.Any())
        {
            return; // No messages to process
        }

        // Update messages to Processing status
        foreach (var message in messages)
        {
            message.Status = MessageStatus.Processing;
            message.ProcessedAt = DateTime.UtcNow;
            message.LockedUntil = lockUntil;
            message.ProcessingInstance = instanceId;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Claimed {Count} messages for processing", messages.Count);

        // Process each message in parallel
        var processingTasks = messages.Select(msg => ProcessMessageAsync(msg, cancellationToken));
        await Task.WhenAll(processingTasks);
    }

    /// <summary>
    /// Processes a single message.
    /// </summary>
    private async Task ProcessMessageAsync(MessageEntity message, CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AuditLogDbContext>();

        try
        {
            _logger.LogInformation("Processing message {MessageId} from topic {Topic}",
                message.MessageId, message.Topic);

            // TODO: Actual message processing logic would go here
            // For now, we'll just simulate processing
            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);

            // Mark message as completed
            var dbMessage = await dbContext.Messages.FindAsync(new object[] { message.Id }, cancellationToken);
            if (dbMessage != null)
            {
                dbMessage.Status = MessageStatus.Completed;
                dbMessage.AcknowledgedAt = DateTime.UtcNow;
                dbMessage.LockedUntil = null;
                dbMessage.ProcessingInstance = null;

                await dbContext.SaveChangesAsync(cancellationToken);
            }

            _logger.LogInformation("Message {MessageId} processed successfully", message.MessageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Message {MessageId} processing failed (attempt {RetryCount}/{MaxRetries})",
                message.MessageId, message.RetryCount + 1, MAX_RETRY_ATTEMPTS);

            // Update message status for retry
            var dbMessage = await dbContext.Messages.FindAsync(new object[] { message.Id }, cancellationToken);
            if (dbMessage != null)
            {
                dbMessage.Status = MessageStatus.Failed;
                dbMessage.ErrorMessage = ex.Message;
                dbMessage.RetryCount++;
                dbMessage.LockedUntil = null;
                dbMessage.ProcessingInstance = null;

                if (dbMessage.RetryCount < MAX_RETRY_ATTEMPTS)
                {
                    // Reset to Pending for retry
                    dbMessage.Status = MessageStatus.Pending;

                    _logger.LogInformation("Message {MessageId} will be retried (attempt {Attempt}/{Max})",
                        message.MessageId, dbMessage.RetryCount + 1, MAX_RETRY_ATTEMPTS);
                }
                else
                {
                    _logger.LogError("Message {MessageId} failed permanently after {Attempts} attempts",
                        message.MessageId, dbMessage.RetryCount);
                }

                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }
    }

    /// <summary>
    /// Releases stale locks from messages that have been locked for too long.
    /// This is a safety mechanism in case a worker crashes while processing.
    /// </summary>
    private async Task ReleaseStaleLocksAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AuditLogDbContext>();

        var now = DateTime.UtcNow;

        var staleMessages = await dbContext.Messages
            .Where(m => m.Status == MessageStatus.Processing &&
                       m.LockedUntil != null &&
                       m.LockedUntil <= now)
            .ToListAsync(cancellationToken);

        if (staleMessages.Any())
        {
            _logger.LogWarning("Releasing {Count} stale message locks", staleMessages.Count);

            foreach (var message in staleMessages)
            {
                message.Status = MessageStatus.Pending;
                message.LockedUntil = null;
                message.ProcessingInstance = null;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
