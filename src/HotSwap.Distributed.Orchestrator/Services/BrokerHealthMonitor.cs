using System.Diagnostics;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Interfaces;
using HotSwap.Distributed.Infrastructure.Metrics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HotSwap.Distributed.Orchestrator.Services;

/// <summary>
/// Background service that monitors broker health and performance metrics.
/// Performs periodic health checks, tracks queue depth, and updates health status.
/// </summary>
public class BrokerHealthMonitor : BackgroundService
{
    private static readonly ActivitySource ActivitySource = new("HotSwap.BrokerHealth", "1.0.0");

    private readonly IMessageQueue _messageQueue;
    private readonly MessageMetricsProvider _metrics;
    private readonly ILogger<BrokerHealthMonitor> _logger;
    private readonly TimeSpan _checkInterval;

    // Health thresholds
    private const int HealthyQueueDepthThreshold = 500;
    private const int DegradedQueueDepthThreshold = 1000;

    private BrokerHealthStatus _currentHealthStatus;
    private readonly object _statusLock = new();

    /// <summary>
    /// Gets the current health status of the broker.
    /// </summary>
    public BrokerHealthStatus CurrentHealthStatus
    {
        get
        {
            lock (_statusLock)
            {
                return _currentHealthStatus;
            }
        }
        private set
        {
            lock (_statusLock)
            {
                _currentHealthStatus = value;
            }
        }
    }

    public BrokerHealthMonitor(
        IMessageQueue messageQueue,
        MessageMetricsProvider metrics,
        ILogger<BrokerHealthMonitor> logger,
        TimeSpan? checkInterval = null)
    {
        _messageQueue = messageQueue ?? throw new ArgumentNullException(nameof(messageQueue));
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _checkInterval = checkInterval ?? TimeSpan.FromSeconds(30);
        _currentHealthStatus = BrokerHealthStatus.Unknown;
    }

    /// <summary>
    /// Executes the background service, periodically checking broker health.
    /// </summary>
    /// <param name="stoppingToken">Cancellation token to stop the service.</param>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "BrokerHealthMonitor started. Check interval: {CheckInterval}",
            _checkInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var activity = ActivitySource.StartActivity("BrokerHealthCheck");

                await PerformHealthCheckAsync(stoppingToken);

                activity?.SetStatus(ActivityStatusCode.Ok);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Expected when service is stopping
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error performing broker health check. Will retry after {CheckInterval}",
                    _checkInterval);

                // On error, maintain current status but log the issue
            }

            try
            {
                await Task.Delay(_checkInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Expected when service is stopping
                break;
            }
        }

        _logger.LogInformation("BrokerHealthMonitor stopped");
    }

    /// <summary>
    /// Performs a single health check of the broker.
    /// </summary>
    private async Task PerformHealthCheckAsync(CancellationToken cancellationToken)
    {
        await Task.Run(() =>
        {
            // Check queue depth
            var queueDepth = _messageQueue.Count;

            _logger.LogDebug(
                "Broker health check: Queue depth = {QueueDepth}",
                queueDepth);

            // Update metrics
            _metrics.UpdateQueueDepth("default-broker", queueDepth);

            // Calculate consumer lag (simplified - in real scenario, track actual consumer positions)
            // For now, consumer lag equals queue depth (assuming consumers should be keeping up)
            var consumerLag = queueDepth;
            _metrics.UpdateConsumerLag("default-broker", consumerLag);

            // Determine health status based on queue depth
            var newStatus = DetermineHealthStatus(queueDepth);

            // Update status and log if it changed
            if (newStatus != CurrentHealthStatus)
            {
                var oldStatus = CurrentHealthStatus;
                CurrentHealthStatus = newStatus;

                _logger.LogInformation(
                    "Broker health status changed from {OldStatus} to {NewStatus}. Queue depth: {QueueDepth}",
                    oldStatus,
                    newStatus,
                    queueDepth);

                // Alert on unhealthy status
                if (newStatus == BrokerHealthStatus.Unhealthy)
                {
                    _logger.LogWarning(
                        "⚠️ ALERT: Broker is UNHEALTHY. Queue depth ({QueueDepth}) exceeds threshold ({Threshold})",
                        queueDepth,
                        DegradedQueueDepthThreshold);
                }
                else if (newStatus == BrokerHealthStatus.Degraded)
                {
                    _logger.LogWarning(
                        "Broker health is DEGRADED. Queue depth ({QueueDepth}) exceeds healthy threshold ({Threshold})",
                        queueDepth,
                        HealthyQueueDepthThreshold);
                }
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Determines the health status based on queue depth.
    /// </summary>
    /// <param name="queueDepth">Current queue depth.</param>
    /// <returns>Broker health status.</returns>
    private static BrokerHealthStatus DetermineHealthStatus(int queueDepth)
    {
        if (queueDepth < HealthyQueueDepthThreshold)
        {
            return BrokerHealthStatus.Healthy;
        }
        else if (queueDepth < DegradedQueueDepthThreshold)
        {
            return BrokerHealthStatus.Degraded;
        }
        else
        {
            return BrokerHealthStatus.Unhealthy;
        }
    }
}
