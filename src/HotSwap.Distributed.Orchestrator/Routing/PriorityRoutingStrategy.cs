using System.Diagnostics;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Orchestrator.Interfaces;

namespace HotSwap.Distributed.Orchestrator.Routing;

/// <summary>
/// Routes messages based on priority tiers to optimize SLA compliance.
/// High-priority messages (7-9) route to first consumer for low latency.
/// Medium-priority messages (4-6) use round-robin load balancing.
/// Low-priority messages (0-3) route to last consumer to deprioritize.
/// </summary>
public class PriorityRoutingStrategy : IRoutingStrategy
{
    private static readonly ActivitySource ActivitySource = new("HotSwap.Routing.Priority", "1.0.0");
    private int _mediumPriorityIndex = 0;
    private readonly object _lock = new object();

    /// <inheritdoc/>
    public string StrategyName => "Priority";

    /// <summary>
    /// Routes a message based on its priority tier.
    /// </summary>
    /// <param name="message">The message to route.</param>
    /// <param name="availableSubscriptions">The list of subscriptions (consumers) that can receive the message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A RouteResult containing the selected consumer ID based on priority tier,
    /// or a failure result if no active consumers are available.
    /// </returns>
    public Task<RouteResult> RouteAsync(
        Message message,
        List<Subscription> availableSubscriptions,
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("PriorityRouting.RouteMessage", ActivityKind.Internal);

        // Add telemetry tags
        activity?.SetTag("message.id", message.MessageId);
        activity?.SetTag("topic.name", message.TopicName);
        activity?.SetTag("strategy", StrategyName);
        activity?.SetTag("message.priority", message.Priority);
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

        // Determine priority tier and select consumer
        Subscription selectedSubscription;
        int selectedIndex;
        string tier;

        if (message.Priority >= 7)
        {
            // High priority (7-9): Route to first consumer for low latency
            tier = "high";
            selectedIndex = 0;
            selectedSubscription = activeSubscriptions[0];

            activity?.SetTag("priority.tier", tier);
            activity?.SetTag("routing.strategy", "first-consumer");
        }
        else if (message.Priority >= 4)
        {
            // Medium priority (4-6): Use round-robin load balancing
            tier = "medium";

            lock (_lock)
            {
                selectedIndex = _mediumPriorityIndex % activeSubscriptions.Count;
                selectedSubscription = activeSubscriptions[selectedIndex];

                // Increment for next medium-priority message
                _mediumPriorityIndex = (_mediumPriorityIndex + 1) % activeSubscriptions.Count;
            }

            activity?.SetTag("priority.tier", tier);
            activity?.SetTag("routing.strategy", "round-robin");
        }
        else
        {
            // Low priority (0-3): Route to last consumer to deprioritize
            tier = "low";
            selectedIndex = activeSubscriptions.Count - 1;
            selectedSubscription = activeSubscriptions[selectedIndex];

            activity?.SetTag("priority.tier", tier);
            activity?.SetTag("routing.strategy", "last-consumer");
        }

        activity?.SetTag("consumer.id", selectedSubscription.SubscriptionId);
        activity?.SetTag("consumer.group", selectedSubscription.ConsumerGroup);
        activity?.SetTag("selected.index", selectedIndex);
        activity?.SetTag("routing.success", true);
        activity?.SetStatus(ActivityStatusCode.Ok, $"Routed {tier}-priority message to consumer at index {selectedIndex}");

        // Create success result with metadata
        var metadata = new Dictionary<string, object>
        {
            { "totalActive", activeSubscriptions.Count },
            { "messagePriority", message.Priority },
            { "priorityTier", tier },
            { "selectedIndex", selectedIndex },
            { "routingStrategy", tier == "high" ? "first-consumer" : tier == "medium" ? "round-robin" : "last-consumer" }
        };

        var result = RouteResult.CreateSuccess(
            StrategyName,
            new List<string> { selectedSubscription.SubscriptionId },
            reason: $"Routed {tier}-priority message (priority={message.Priority}) to consumer at index {selectedIndex} of {activeSubscriptions.Count}",
            metadata: metadata);

        return Task.FromResult(result);
    }
}
