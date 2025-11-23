namespace HotSwap.Distributed.Domain.Models;

/// <summary>
/// Result of a resource stabilization check.
/// </summary>
public class ResourceStabilizationResult
{
    /// <summary>
    /// Whether resources have stabilized within acceptable thresholds.
    /// </summary>
    public bool IsStable { get; set; }

    /// <summary>
    /// Time spent waiting for stabilization.
    /// </summary>
    public TimeSpan ElapsedTime { get; set; }

    /// <summary>
    /// Number of consecutive stable checks achieved.
    /// </summary>
    public int ConsecutiveStableChecks { get; set; }

    /// <summary>
    /// Whether the maximum timeout was reached.
    /// </summary>
    public bool TimeoutReached { get; set; }

    /// <summary>
    /// Total number of metric checks performed.
    /// </summary>
    public int TotalChecks { get; set; }

    /// <summary>
    /// Optional message describing the stabilization result.
    /// </summary>
    public string? Message { get; set; }
}
