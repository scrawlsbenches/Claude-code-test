namespace HotSwap.Distributed.Domain.Models;

/// <summary>
/// Represents the result of a message routing operation.
/// Contains information about selected consumers and routing metadata.
/// </summary>
public class RouteResult
{
    /// <summary>
    /// Gets or sets the list of consumer IDs that should receive the message.
    /// </summary>
    public required List<string> ConsumerIds { get; set; } = new();

    /// <summary>
    /// Gets or sets the routing strategy name that was used.
    /// </summary>
    public required string StrategyName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the reason for the routing decision.
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// Gets or sets additional routing metadata (e.g., load balancing index, priority scores).
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the routing was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the error message if routing failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Creates a successful route result with selected consumers.
    /// </summary>
    /// <param name="strategyName">The name of the routing strategy used.</param>
    /// <param name="consumerIds">The list of consumer IDs to route to.</param>
    /// <param name="reason">Optional reason for the routing decision.</param>
    /// <param name="metadata">Optional routing metadata.</param>
    /// <returns>A successful RouteResult.</returns>
    public static RouteResult CreateSuccess(
        string strategyName,
        List<string> consumerIds,
        string? reason = null,
        Dictionary<string, object>? metadata = null)
    {
        return new RouteResult
        {
            Success = true,
            StrategyName = strategyName,
            ConsumerIds = consumerIds,
            Reason = reason,
            Metadata = metadata
        };
    }

    /// <summary>
    /// Creates a failed route result.
    /// </summary>
    /// <param name="strategyName">The name of the routing strategy used.</param>
    /// <param name="errorMessage">The error message explaining the failure.</param>
    /// <returns>A failed RouteResult.</returns>
    public static RouteResult CreateFailure(string strategyName, string errorMessage)
    {
        return new RouteResult
        {
            Success = false,
            StrategyName = strategyName,
            ConsumerIds = new List<string>(),
            ErrorMessage = errorMessage
        };
    }

    /// <summary>
    /// Checks if any consumers were selected for routing.
    /// </summary>
    /// <returns>True if at least one consumer was selected; otherwise, false.</returns>
    public bool HasConsumers() => ConsumerIds != null && ConsumerIds.Count > 0;

    /// <summary>
    /// Gets the count of consumers that will receive the message.
    /// </summary>
    /// <returns>The number of consumers.</returns>
    public int GetConsumerCount() => ConsumerIds?.Count ?? 0;
}
