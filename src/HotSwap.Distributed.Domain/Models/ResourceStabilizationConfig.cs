namespace HotSwap.Distributed.Domain.Models;

/// <summary>
/// Configuration for resource-based deployment stabilization checks.
/// </summary>
public class ResourceStabilizationConfig
{
    /// <summary>
    /// Maximum acceptable CPU delta percentage from baseline (e.g., 10.0 = ±10%).
    /// </summary>
    public double CpuDeltaThreshold { get; set; } = 10.0;

    /// <summary>
    /// Maximum acceptable memory delta percentage from baseline (e.g., 10.0 = ±10%).
    /// </summary>
    public double MemoryDeltaThreshold { get; set; } = 10.0;

    /// <summary>
    /// Maximum acceptable latency delta percentage from baseline (e.g., 15.0 = ±15%).
    /// </summary>
    public double LatencyDeltaThreshold { get; set; } = 15.0;

    /// <summary>
    /// How often to poll metrics for stabilization checks.
    /// </summary>
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Number of consecutive stable checks required before considering resources stabilized.
    /// </summary>
    public int ConsecutiveStableChecks { get; set; } = 3;

    /// <summary>
    /// Minimum time to wait regardless of stabilization (safety minimum).
    /// </summary>
    public TimeSpan MinimumWaitTime { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Maximum time to wait before timing out (safety maximum).
    /// </summary>
    public TimeSpan MaximumWaitTime { get; set; } = TimeSpan.FromMinutes(30);
}
