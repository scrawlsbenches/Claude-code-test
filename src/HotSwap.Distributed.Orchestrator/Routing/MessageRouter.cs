using System.Diagnostics;
using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Orchestrator.Interfaces;
using Microsoft.Extensions.Logging;

namespace HotSwap.Distributed.Orchestrator.Routing;

/// <summary>
/// Orchestrates message routing by selecting and executing the appropriate routing strategy.
/// Supports strategy selection based on topic type with optional configuration overrides.
/// </summary>
public class MessageRouter
{
    private static readonly ActivitySource ActivitySource = new("HotSwap.MessageRouter", "1.0.0");
    private readonly ILogger<MessageRouter> _logger;
    private readonly Dictionary<string, IRoutingStrategy> _strategies;

    public MessageRouter(ILogger<MessageRouter> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Initialize all available routing strategies
        _strategies = new Dictionary<string, IRoutingStrategy>(StringComparer.OrdinalIgnoreCase)
        {
            { "Direct", new DirectRoutingStrategy() },
            { "FanOut", new FanOutRoutingStrategy() },
            { "LoadBalanced", new LoadBalancedRoutingStrategy() },
            { "Priority", new PriorityRoutingStrategy() },
            { "ContentBased", new ContentBasedRoutingStrategy() }
        };

        _logger.LogInformation("MessageRouter initialized with {StrategyCount} routing strategies",
            _strategies.Count);
    }

    /// <summary>
    /// Routes a message to appropriate consumers based on topic type and configuration.
    /// </summary>
    /// <param name="message">The message to route.</param>
    /// <param name="topic">The topic configuration.</param>
    /// <param name="subscriptions">Available subscriptions for the topic.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>RouteResult containing selected consumer IDs or failure information.</returns>
    public async Task<RouteResult> RouteMessageAsync(
        Message message,
        Topic topic,
        List<Subscription> subscriptions,
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("MessageRouter.RouteMessage", ActivityKind.Internal);

        // Add telemetry tags
        activity?.SetTag("message.id", message.MessageId);
        activity?.SetTag("topic.name", topic.Name);
        activity?.SetTag("topic.type", topic.Type.ToString());
        activity?.SetTag("subscriptions.count", subscriptions?.Count ?? 0);

        try
        {
            // Select the appropriate routing strategy
            var strategy = SelectStrategy(topic);
            activity?.SetTag("routing.strategy", strategy.StrategyName);

            _logger.LogDebug(
                "Routing message {MessageId} on topic {TopicName} using {StrategyName} strategy",
                message.MessageId,
                topic.Name,
                strategy.StrategyName);

            // Execute the routing strategy
            var result = await strategy.RouteAsync(message, subscriptions!, cancellationToken);

            if (result.Success)
            {
                _logger.LogInformation(
                    "Successfully routed message {MessageId} to {ConsumerCount} consumers using {StrategyName}",
                    message.MessageId,
                    result.GetConsumerCount(),
                    strategy.StrategyName);

                activity?.SetStatus(ActivityStatusCode.Ok,
                    $"Routed to {result.GetConsumerCount()} consumers");
            }
            else
            {
                _logger.LogWarning(
                    "Failed to route message {MessageId}: {ErrorMessage}",
                    message.MessageId,
                    result.ErrorMessage);

                activity?.SetStatus(ActivityStatusCode.Error, result.ErrorMessage ?? "Routing failed");
            }

            activity?.SetTag("routing.success", result.Success);
            activity?.SetTag("routing.consumer_count", result.GetConsumerCount());

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error routing message {MessageId} on topic {TopicName}",
                message.MessageId,
                topic.Name);

            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag("routing.success", false);
            activity?.SetTag("routing.error", ex.GetType().Name);

            // Return a failure result instead of throwing
            return RouteResult.CreateFailure(
                "MessageRouter",
                $"Routing error: {ex.Message}");
        }
    }

    /// <summary>
    /// Selects the appropriate routing strategy based on topic configuration.
    /// </summary>
    /// <param name="topic">The topic configuration.</param>
    /// <returns>The selected routing strategy.</returns>
    private IRoutingStrategy SelectStrategy(Topic topic)
    {
        // Check for explicit strategy configuration override
        if (topic.Config.TryGetValue("routingStrategy", out var strategyName))
        {
            // Attempt to use the configured strategy
            if (_strategies.TryGetValue(strategyName, out var configuredStrategy))
            {
                _logger.LogDebug(
                    "Using configured routing strategy {StrategyName} for topic {TopicName}",
                    strategyName,
                    topic.Name);

                return configuredStrategy;
            }

            // Log warning if configured strategy doesn't exist, fall back to default
            _logger.LogWarning(
                "Configured routing strategy '{StrategyName}' not found for topic {TopicName}, using default",
                strategyName,
                topic.Name);
        }

        // Default strategy selection based on topic type
        var defaultStrategy = topic.Type switch
        {
            TopicType.Queue => _strategies["LoadBalanced"], // Queue: Load balance across consumers
            TopicType.PubSub => _strategies["FanOut"],      // PubSub: Broadcast to all consumers
            _ => _strategies["Direct"]                       // Fallback: Direct to first consumer
        };

        _logger.LogDebug(
            "Using default routing strategy {StrategyName} for topic type {TopicType}",
            defaultStrategy.StrategyName,
            topic.Type);

        return defaultStrategy;
    }

    /// <summary>
    /// Gets the list of available routing strategy names.
    /// </summary>
    public IReadOnlyCollection<string> GetAvailableStrategies()
    {
        return _strategies.Keys.ToList().AsReadOnly();
    }
}
