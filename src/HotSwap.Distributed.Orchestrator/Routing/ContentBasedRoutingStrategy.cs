using System.Diagnostics;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Orchestrator.Interfaces;

namespace HotSwap.Distributed.Orchestrator.Routing;

/// <summary>
/// Routes messages based on header filters and content matching.
/// Subscriptions with filters receive only messages that match their criteria.
/// Subscriptions without filters receive all messages.
/// </summary>
public class ContentBasedRoutingStrategy : IRoutingStrategy
{
    private static readonly ActivitySource ActivitySource = new("HotSwap.Routing.ContentBased", "1.0.0");

    /// <inheritdoc/>
    public string StrategyName => "ContentBased";

    /// <summary>
    /// Routes a message to all subscriptions whose filters match the message content.
    /// </summary>
    /// <param name="message">The message to route.</param>
    /// <param name="availableSubscriptions">The list of subscriptions (consumers) that can receive the message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A RouteResult containing all consumer IDs whose filters match the message,
    /// or a failure result if no matching consumers are available.
    /// </returns>
    public Task<RouteResult> RouteAsync(
        Message message,
        List<Subscription> availableSubscriptions,
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("ContentBasedRouting.RouteMessage", ActivityKind.Internal);

        // Add telemetry tags
        activity?.SetTag("message.id", message.MessageId);
        activity?.SetTag("topic.name", message.TopicName);
        activity?.SetTag("strategy", StrategyName);
        activity?.SetTag("message.headers.count", message.Headers?.Count ?? 0);
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

        // Filter subscriptions based on their message filters
        var matchingSubscriptions = activeSubscriptions
            .Where(s => SubscriptionMatchesMessage(s, message))
            .ToList();

        activity?.SetTag("subscriptions.matched", matchingSubscriptions.Count);

        // Check if any subscriptions match the filter
        if (matchingSubscriptions.Count == 0)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "No matching consumers for message filters");
            activity?.SetTag("routing.success", false);

            return Task.FromResult(RouteResult.CreateFailure(
                StrategyName,
                "No matching consumers found for the message filters"));
        }

        // Select ALL matching subscriptions (content-based broadcast)
        var consumerIds = matchingSubscriptions
            .Select(s => s.SubscriptionId)
            .ToList();

        activity?.SetTag("broadcast.count", consumerIds.Count);
        activity?.SetTag("routing.success", true);
        activity?.SetStatus(ActivityStatusCode.Ok, $"Routed to {consumerIds.Count} matching consumers");

        // Add consumer group information to tracing
        var consumerGroups = matchingSubscriptions
            .Select(s => s.ConsumerGroup)
            .Distinct()
            .ToList();
        activity?.SetTag("consumer.groups", string.Join(",", consumerGroups));

        // Count subscriptions with filters vs without
        var withFilters = matchingSubscriptions.Count(s => s.Filter != null);
        var withoutFilters = matchingSubscriptions.Count(s => s.Filter == null);
        activity?.SetTag("matched.with_filters", withFilters);
        activity?.SetTag("matched.without_filters", withoutFilters);

        // Create success result with metadata
        var metadata = new Dictionary<string, object>
        {
            { "totalActive", activeSubscriptions.Count },
            { "matchedCount", matchingSubscriptions.Count },
            { "withFilters", withFilters },
            { "withoutFilters", withoutFilters },
            { "consumerGroups", consumerGroups.Count }
        };

        var result = RouteResult.CreateSuccess(
            StrategyName,
            consumerIds,
            reason: $"Routed to {matchingSubscriptions.Count} consumers matching filter criteria (out of {activeSubscriptions.Count} active)",
            metadata: metadata);

        return Task.FromResult(result);
    }

    /// <summary>
    /// Determines if a subscription's filter matches the message.
    /// Subscriptions without filters match all messages.
    /// </summary>
    private bool SubscriptionMatchesMessage(Subscription subscription, Message message)
    {
        // No filter means the subscription accepts all messages
        if (subscription.Filter == null)
        {
            return true;
        }

        // Use the Filter's Matches method to evaluate
        return subscription.Filter.Matches(message);
    }
}
