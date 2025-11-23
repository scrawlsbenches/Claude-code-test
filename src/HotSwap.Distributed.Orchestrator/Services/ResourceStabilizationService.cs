using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Interfaces;
using Microsoft.Extensions.Logging;

namespace HotSwap.Distributed.Orchestrator.Services;

/// <summary>
/// Service for waiting until deployed resources have stabilized based on metrics analysis.
/// Provides adaptive deployment timing by monitoring resource metrics instead of using fixed time delays.
/// </summary>
/// <remarks>
/// This service replaces fixed time-based waits (e.g., 15 minutes) with adaptive stabilization checks
/// that complete as soon as resources stabilize within acceptable thresholds. This approach:
/// <list type="bullet">
/// <item>Speeds up deployments when metrics stabilize quickly (2-3 minutes vs. 15 minutes fixed)</item>
/// <item>Provides adaptive safety by taking longer when needed (up to configurable maximum)</item>
/// <item>Requires consecutive stable checks to filter out metric spikes</item>
/// <item>Enforces minimum and maximum wait times as safety bounds</item>
/// </list>
/// </remarks>
public class ResourceStabilizationService
{
    private readonly ILogger<ResourceStabilizationService> _logger;
    private readonly IMetricsProvider _metricsProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="ResourceStabilizationService"/> class.
    /// </summary>
    /// <param name="logger">Logger for recording stabilization progress and decisions.</param>
    /// <param name="metricsProvider">Provider for retrieving node and cluster metrics.</param>
    /// <exception cref="ArgumentNullException">Thrown when logger or metricsProvider is null.</exception>
    public ResourceStabilizationService(
        ILogger<ResourceStabilizationService> logger,
        IMetricsProvider metricsProvider)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _metricsProvider = metricsProvider ?? throw new ArgumentNullException(nameof(metricsProvider));
    }

    /// <summary>
    /// Waits for resources to stabilize within acceptable thresholds by polling metrics periodically.
    /// </summary>
    /// <param name="nodeIds">The node IDs to monitor for stabilization.</param>
    /// <param name="baseline">Baseline metrics snapshot to compare against for calculating deltas.</param>
    /// <param name="config">Configuration specifying thresholds, polling interval, and time bounds.</param>
    /// <param name="cancellationToken">Token to cancel the stabilization wait operation.</param>
    /// <returns>
    /// A <see cref="ResourceStabilizationResult"/> indicating whether resources stabilized,
    /// how long it took, and the number of consecutive stable checks achieved.
    /// </returns>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled via the cancellation token.</exception>
    /// <remarks>
    /// <para>
    /// The method polls node metrics at the configured interval and calculates percentage deltas
    /// from the baseline for CPU, memory, and latency. Resources are considered stable when
    /// all metrics are within their respective thresholds for the required number of consecutive checks.
    /// </para>
    /// <para>
    /// Safety bounds are enforced:
    /// <list type="bullet">
    /// <item>Minimum wait time: Ensures resources have time to settle even if stable immediately</item>
    /// <item>Maximum wait time: Prevents indefinite waiting if resources never stabilize</item>
    /// </list>
    /// </para>
    /// <para>
    /// Example configuration for production canary deployment:
    /// <code>
    /// var config = new ResourceStabilizationConfig
    /// {
    ///     CpuDeltaThreshold = 10.0,           // ±10% CPU change acceptable
    ///     MemoryDeltaThreshold = 10.0,        // ±10% memory change acceptable
    ///     LatencyDeltaThreshold = 15.0,       // ±15% latency change acceptable
    ///     PollingInterval = TimeSpan.FromSeconds(30),
    ///     ConsecutiveStableChecks = 3,        // Must be stable 3 times in a row
    ///     MinimumWaitTime = TimeSpan.FromMinutes(2),
    ///     MaximumWaitTime = TimeSpan.FromMinutes(30)
    /// };
    /// </code>
    /// </para>
    /// </remarks>
    public virtual async Task<ResourceStabilizationResult> WaitForStabilizationAsync(
        IEnumerable<Guid> nodeIds,
        ClusterMetricsSnapshot baseline,
        ResourceStabilizationConfig config,
        CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;
        var consecutiveStableChecks = 0;
        var totalChecks = 0;
        var minimumWaitReached = false;

        _logger.LogInformation(
            "Starting resource stabilization monitoring. Min wait: {MinWait}, Max wait: {MaxWait}, Required stable checks: {StableChecks}",
            config.MinimumWaitTime,
            config.MaximumWaitTime,
            config.ConsecutiveStableChecks);

        while (true)
        {
            var elapsed = DateTime.UtcNow - startTime;

            // Check for maximum timeout
            if (elapsed >= config.MaximumWaitTime)
            {
                _logger.LogWarning(
                    "Resource stabilization maximum timeout reached ({MaxWait}). Consecutive stable checks: {StableChecks}/{Required}",
                    config.MaximumWaitTime,
                    consecutiveStableChecks,
                    config.ConsecutiveStableChecks);

                return new ResourceStabilizationResult
                {
                    IsStable = false,
                    ElapsedTime = elapsed,
                    ConsecutiveStableChecks = consecutiveStableChecks,
                    TimeoutReached = true,
                    TotalChecks = totalChecks,
                    Message = $"Maximum timeout reached after {elapsed.TotalSeconds:F1}s"
                };
            }

            // Track if minimum wait time has been reached
            if (elapsed >= config.MinimumWaitTime)
            {
                minimumWaitReached = true;
            }

            // Poll metrics
            cancellationToken.ThrowIfCancellationRequested();

            var currentMetrics = await _metricsProvider.GetNodesMetricsAsync(nodeIds, cancellationToken);
            totalChecks++;

            // Calculate average metrics across nodes
            var avgCpu = currentMetrics.Average(m => m.CpuUsagePercent);
            var avgMemory = currentMetrics.Average(m => m.MemoryUsagePercent);
            var avgLatency = currentMetrics.Average(m => m.LatencyMs);

            // Calculate deltas from baseline
            var cpuDelta = Math.Abs((avgCpu - baseline.AvgCpuUsage) / baseline.AvgCpuUsage * 100);
            var memoryDelta = Math.Abs((avgMemory - baseline.AvgMemoryUsage) / baseline.AvgMemoryUsage * 100);
            var latencyDelta = Math.Abs((avgLatency - baseline.AvgLatency) / baseline.AvgLatency * 100);

            _logger.LogDebug(
                "Metrics check #{Check}: CPU Δ{CpuDelta:F1}% (threshold: {CpuThreshold}%), " +
                "Memory Δ{MemoryDelta:F1}% (threshold: {MemoryThreshold}%), " +
                "Latency Δ{LatencyDelta:F1}% (threshold: {LatencyThreshold}%)",
                totalChecks,
                cpuDelta, config.CpuDeltaThreshold,
                memoryDelta, config.MemoryDeltaThreshold,
                latencyDelta, config.LatencyDeltaThreshold);

            // Check if metrics are within thresholds
            var isStable =
                cpuDelta <= config.CpuDeltaThreshold &&
                memoryDelta <= config.MemoryDeltaThreshold &&
                latencyDelta <= config.LatencyDeltaThreshold;

            if (isStable)
            {
                consecutiveStableChecks++;
                _logger.LogDebug(
                    "Metrics stable ({Consecutive}/{Required} consecutive checks)",
                    consecutiveStableChecks,
                    config.ConsecutiveStableChecks);

                // Check if we have enough consecutive stable checks AND minimum wait time met
                if (consecutiveStableChecks >= config.ConsecutiveStableChecks && minimumWaitReached)
                {
                    _logger.LogInformation(
                        "Resources stabilized after {Elapsed:F1}s ({Checks} checks, {Consecutive} consecutive stable)",
                        elapsed.TotalSeconds,
                        totalChecks,
                        consecutiveStableChecks);

                    return new ResourceStabilizationResult
                    {
                        IsStable = true,
                        ElapsedTime = elapsed,
                        ConsecutiveStableChecks = consecutiveStableChecks,
                        TimeoutReached = false,
                        TotalChecks = totalChecks,
                        Message = $"Stabilized after {elapsed.TotalSeconds:F1}s"
                    };
                }
            }
            else
            {
                // Metrics unstable - reset consecutive counter
                if (consecutiveStableChecks > 0)
                {
                    _logger.LogDebug(
                        "Metrics unstable - resetting consecutive stable count from {Count}",
                        consecutiveStableChecks);
                }
                consecutiveStableChecks = 0;
            }

            // Wait before next poll
            await Task.Delay(config.PollingInterval, cancellationToken);
        }
    }
}
