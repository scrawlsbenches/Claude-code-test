using System.Diagnostics;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Orchestrator.Interfaces;

namespace HotSwap.Distributed.Orchestrator.Routing;

/// <summary>
/// Routes messages to ALL active consumers (broadcast/pub-sub pattern).
/// This strategy delivers the message to every active subscription in parallel.
/// </summary>
public class FanOutRoutingStrategy : IRoutingStrategy
{
    private static readonly ActivitySource ActivitySource = new("HotSwap.Routing.FanOut", "1.0.0");

    /// <inheritdoc/>
    public string StrategyName => "FanOut";

    /// <summary>
    /// Routes a message to ALL available active consumers.
    /// </summary>
    /// <param name="message">The message to route.</param>
    /// <param name="availableSubscriptions">The list of subscriptions (consumers) that can receive the message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A RouteResult containing ALL active consumer IDs, or a failure result if no active consumers are available.
    /// </returns>
    public Task<RouteResult> RouteAsync(
        Message message,
        List<Subscription> availableSubscriptions,
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("FanOutRouting.RouteMessage", ActivityKind.Internal);

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

        // Select ALL active subscriptions (fan-out / broadcast)
        var consumerIds = activeSubscriptions
            .Select(s => s.SubscriptionId)
            .ToList();

        activity?.SetTag("broadcast.count", consumerIds.Count);
        activity?.SetTag("routing.success", true);
        activity?.SetStatus(ActivityStatusCode.Ok, $"Broadcast to {consumerIds.Count} consumers");

        // Add consumer group information to tracing
        var consumerGroups = activeSubscriptions
            .Select(s => s.ConsumerGroup)
            .Distinct()
            .ToList();
        activity?.SetTag("consumer.groups", string.Join(",", consumerGroups));

        // Create success result with metadata
        var metadata = new Dictionary<string, object>
        {
            { "totalActive", activeSubscriptions.Count },
            { "broadcastCount", consumerIds.Count },
            { "consumerGroups", consumerGroups.Count }
        };

        var result = RouteResult.CreateSuccess(
            StrategyName,
            consumerIds,
            reason: $"Broadcast to all {consumerIds.Count} active consumers",
            metadata: metadata);

        return Task.FromResult(result);
    }
}
