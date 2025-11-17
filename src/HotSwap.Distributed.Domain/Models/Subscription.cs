using HotSwap.Distributed.Domain.Enums;

namespace HotSwap.Distributed.Domain.Models;

/// <summary>
/// Represents a subscription to a topic.
/// </summary>
public class Subscription
{
    /// <summary>
    /// Unique subscription identifier (GUID format).
    /// </summary>
    public required string SubscriptionId { get; set; }

    /// <summary>
    /// Subscribed topic name.
    /// </summary>
    public required string TopicName { get; set; }

    /// <summary>
    /// Consumer group name (unique per topic).
    /// </summary>
    public required string ConsumerGroup { get; set; }

    /// <summary>
    /// Consumer endpoint (webhook URL for Push, null for Pull).
    /// </summary>
    public required string ConsumerEndpoint { get; set; }

    /// <summary>
    /// Subscription type (Push or Pull).
    /// </summary>
    public SubscriptionType Type { get; set; } = SubscriptionType.Pull;

    /// <summary>
    /// Message filter for content-based routing (optional).
    /// </summary>
    public MessageFilter? Filter { get; set; }

    /// <summary>
    /// Maximum delivery retry attempts before moving to DLQ.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Acknowledgment timeout (message requeued if not acked).
    /// </summary>
    public TimeSpan AckTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Whether the subscription is active (paused if false).
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Subscription creation timestamp (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last message consumption timestamp (UTC).
    /// </summary>
    public DateTime? LastConsumedAt { get; set; }

    /// <summary>
    /// Total messages consumed by this subscription.
    /// </summary>
    public long MessagesConsumed { get; set; } = 0;

    /// <summary>
    /// Total messages that failed delivery.
    /// </summary>
    public long MessagesFailed { get; set; } = 0;

    /// <summary>
    /// Validates the subscription configuration.
    /// </summary>
    public bool IsValid(out List<string> errors)
    {
        errors = new List<string>();

        if (string.IsNullOrWhiteSpace(SubscriptionId))
            errors.Add("SubscriptionId is required");

        if (string.IsNullOrWhiteSpace(TopicName))
            errors.Add("TopicName is required");

        if (string.IsNullOrWhiteSpace(ConsumerGroup))
            errors.Add("ConsumerGroup is required");

        if (Type == SubscriptionType.Push && string.IsNullOrWhiteSpace(ConsumerEndpoint))
            errors.Add("ConsumerEndpoint is required for Push subscriptions");

        if (Type == SubscriptionType.Push && ConsumerEndpoint != null && !Uri.IsWellFormedUriString(ConsumerEndpoint, UriKind.Absolute))
            errors.Add("ConsumerEndpoint must be a valid absolute URL");

        if (MaxRetries < 0 || MaxRetries > 10)
            errors.Add("MaxRetries must be between 0 and 10");

        if (AckTimeout < TimeSpan.FromSeconds(1) || AckTimeout > TimeSpan.FromMinutes(10))
            errors.Add("AckTimeout must be between 1 second and 10 minutes");

        return errors.Count == 0;
    }
}

/// <summary>
/// Message filter for content-based routing.
/// </summary>
public class MessageFilter
{
    /// <summary>
    /// Header key-value matches (all must match).
    /// </summary>
    public Dictionary<string, string> HeaderMatches { get; set; } = new();

    /// <summary>
    /// JSONPath query for payload filtering (optional).
    /// </summary>
    public string? PayloadQuery { get; set; }

    /// <summary>
    /// Evaluates whether a message matches this filter.
    /// </summary>
    public bool Matches(Message message)
    {
        // Check header matches
        foreach (var (key, value) in HeaderMatches)
        {
            if (!message.Headers.TryGetValue(key, out var messageValue) || messageValue != value)
                return false;
        }

        // Note: JSONPath query evaluation for PayloadQuery will be implemented in Epic 2
        // Current implementation: match succeeds if no payload query specified
        return string.IsNullOrWhiteSpace(PayloadQuery);
    }
}
