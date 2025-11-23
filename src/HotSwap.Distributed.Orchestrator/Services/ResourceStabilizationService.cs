using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Interfaces;
using Microsoft.Extensions.Logging;

namespace HotSwap.Distributed.Orchestrator.Services;

/// <summary>
/// Service for waiting until deployed resources have stabilized based on metrics analysis.
/// </summary>
public class ResourceStabilizationService
{
    private readonly ILogger<ResourceStabilizationService> _logger;
    private readonly IMetricsProvider _metricsProvider;

    public ResourceStabilizationService(
        ILogger<ResourceStabilizationService> logger,
        IMetricsProvider metricsProvider)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _metricsProvider = metricsProvider ?? throw new ArgumentNullException(nameof(metricsProvider));
    }

    /// <summary>
    /// Waits for resources to stabilize within acceptable thresholds.
    /// Polls metrics periodically and requires N consecutive stable checks.
    /// Enforces minimum and maximum wait times.
    /// </summary>
    public async Task<ResourceStabilizationResult> WaitForStabilizationAsync(
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
