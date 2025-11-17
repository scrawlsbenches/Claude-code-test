using HotSwap.Distributed.Domain.Models;

namespace HotSwap.Distributed.Orchestrator.Interfaces;

/// <summary>
/// Defines a strategy for routing messages to consumers (subscriptions).
/// Implementations determine how messages are distributed across available consumers.
/// </summary>
public interface IRoutingStrategy
{
    /// <summary>
    /// Gets the name of the routing strategy.
    /// </summary>
    string StrategyName { get; }

    /// <summary>
    /// Routes a message to one or more consumers based on the strategy's algorithm.
    /// </summary>
    /// <param name="message">The message to route.</param>
    /// <param name="availableSubscriptions">The list of subscriptions (consumers) that can receive the message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A RouteResult containing the selected consumer IDs and routing metadata.
    /// Returns a failure result if no consumers are available or routing fails.
    /// </returns>
    /// <remarks>
    /// Routing strategies must be:
    /// - Thread-safe (can be called concurrently)
    /// - Deterministic for the same input (for testing)
    /// - Fast (routing should complete in &lt;10ms)
    ///
    /// Common routing strategies:
    /// - Direct: Route to a single consumer (first available)
    /// - FanOut: Route to all consumers (pub/sub broadcast)
    /// - LoadBalanced: Route to one consumer using round-robin or least-loaded
    /// - Priority: Route to highest-priority consumer
    /// - ContentBased: Route based on message headers or payload content
    /// </remarks>
    Task<RouteResult> RouteAsync(
        Message message,
        List<Subscription> availableSubscriptions,
        CancellationToken cancellationToken = default);
}
