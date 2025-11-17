using System.Diagnostics;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Orchestrator.Interfaces;

namespace HotSwap.Distributed.Orchestrator.Routing;

/// <summary>
/// Routes messages using round-robin load balancing.
/// Distributes messages evenly across all active consumers.
/// </summary>
public class LoadBalancedRoutingStrategy : IRoutingStrategy
{
    private static readonly ActivitySource ActivitySource = new("HotSwap.Routing.LoadBalanced", "1.0.0");
    private int _currentIndex = 0;
    private readonly object _lock = new object();

    /// <inheritdoc/>
    public string StrategyName => "LoadBalanced";

    /// <summary>
    /// Routes a message to the next consumer in round-robin fashion.
    /// </summary>
    /// <param name="message">The message to route.</param>
    /// <param name="availableSubscriptions">The list of subscriptions (consumers) that can receive the message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A RouteResult containing the next consumer ID in round-robin order, or a failure result if no active consumers are available.
    /// </returns>
    public Task<RouteResult> RouteAsync(
        Message message,
        List<Subscription> availableSubscriptions,
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("LoadBalancedRouting.RouteMessage", ActivityKind.Internal);

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

        // Select next consumer using round-robin (thread-safe)
        Subscription selectedSubscription;
        int selectedIndex;

        lock (_lock)
        {
            selectedIndex = _currentIndex % activeSubscriptions.Count;
            selectedSubscription = activeSubscriptions[selectedIndex];

            // Increment for next call
            _currentIndex = (_currentIndex + 1) % activeSubscriptions.Count;
        }

        activity?.SetTag("consumer.id", selectedSubscription.SubscriptionId);
        activity?.SetTag("consumer.group", selectedSubscription.ConsumerGroup);
        activity?.SetTag("selected.index", selectedIndex);
        activity?.SetTag("routing.success", true);
        activity?.SetStatus(ActivityStatusCode.Ok, $"Routed to consumer at index {selectedIndex}");

        // Create success result with metadata
        var metadata = new Dictionary<string, object>
        {
            { "totalActive", activeSubscriptions.Count },
            { "selectedIndex", selectedIndex },
            { "algorithm", "round-robin" }
        };

        var result = RouteResult.CreateSuccess(
            StrategyName,
            new List<string> { selectedSubscription.SubscriptionId },
            reason: $"Load balanced using round-robin (index {selectedIndex} of {activeSubscriptions.Count} consumers)",
            metadata: metadata);

        return Task.FromResult(result);
    }
}
