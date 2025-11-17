using System.Diagnostics;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Orchestrator.Interfaces;

namespace HotSwap.Distributed.Orchestrator.Routing;

/// <summary>
/// Routes messages to a single consumer (the first available one).
/// This strategy minimizes latency by selecting the first active subscription.
/// </summary>
public class DirectRoutingStrategy : IRoutingStrategy
{
    private static readonly ActivitySource ActivitySource = new("HotSwap.Routing.Direct", "1.0.0");

    /// <inheritdoc/>
    public string StrategyName => "Direct";

    /// <summary>
    /// Routes a message to the first available active consumer.
    /// </summary>
    /// <param name="message">The message to route.</param>
    /// <param name="availableSubscriptions">The list of subscriptions (consumers) that can receive the message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A RouteResult containing the first active consumer ID, or a failure result if no active consumers are available.
    /// </returns>
    public Task<RouteResult> RouteAsync(
        Message message,
        List<Subscription> availableSubscriptions,
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("DirectRouting.RouteMessage", ActivityKind.Internal);

        // Add telemetry tags
        activity?.SetTag("message.id", message.MessageId);
        activity?.SetTag("topic.name", message.TopicName);
        activity?.SetTag("strategy", StrategyName);
        activity?.SetTag("subscriptions.total", availableSubscriptions?.Count ?? 0);

        // Filter for active subscriptions only
        var activeSubscriptions = availableSubscriptions?
            .Where(s => s.IsActive)
            .ToList() ?? new List<Subscription>();

        activity?.SetTag("subscriptions.active", activeSubscriptions.Count);

        // Check if any active subscriptions are available
        if (activeSubscriptions.Count == 0)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "No active consumers available");
            activity?.SetTag("routing.success", false);

            return Task.FromResult(RouteResult.CreateFailure(
                StrategyName,
                "No active consumers available for this topic"));
        }

        // Select the first active subscription
        var selectedSubscription = activeSubscriptions[0];

        activity?.SetTag("consumer.id", selectedSubscription.SubscriptionId);
        activity?.SetTag("consumer.group", selectedSubscription.ConsumerGroup);
        activity?.SetTag("routing.success", true);
        activity?.SetStatus(ActivityStatusCode.Ok, "Routed to consumer successfully");

        // Create success result with metadata
        var metadata = new Dictionary<string, object>
        {
            { "totalAvailable", activeSubscriptions.Count },
            { "selectedIndex", 0 }
        };

        var result = RouteResult.CreateSuccess(
            StrategyName,
            new List<string> { selectedSubscription.SubscriptionId },
            reason: $"Routed to first available consumer (out of {activeSubscriptions.Count} available)",
            metadata: metadata);

        return Task.FromResult(result);
    }
}
