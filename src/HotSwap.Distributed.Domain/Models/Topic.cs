using System.Text.RegularExpressions;
using HotSwap.Distributed.Domain.Enums;

namespace HotSwap.Distributed.Domain.Models;

/// <summary>
/// Represents a messaging topic.
/// </summary>
public class Topic
{
    /// <summary>
    /// Unique topic name (alphanumeric, dots, dashes allowed).
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Human-readable description (optional).
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Current schema identifier for message validation.
    /// </summary>
    public required string SchemaId { get; set; }

    /// <summary>
    /// Topic type (Queue or PubSub).
    /// </summary>
    public TopicType Type { get; set; } = TopicType.PubSub;

    /// <summary>
    /// Delivery guarantee mode.
    /// </summary>
    public DeliveryGuarantee DeliveryGuarantee { get; set; } = DeliveryGuarantee.AtLeastOnce;

    /// <summary>
    /// Message retention period (default: 7 days).
    /// </summary>
    public TimeSpan RetentionPeriod { get; set; } = TimeSpan.FromDays(7);

    /// <summary>
    /// Number of partitions for parallel consumption (1-16).
    /// </summary>
    public int PartitionCount { get; set; } = 1;

    /// <summary>
    /// Replication factor for high availability (1-3).
    /// </summary>
    public int ReplicationFactor { get; set; } = 2;

    /// <summary>
    /// Topic-specific configuration settings.
    /// </summary>
    public Dictionary<string, string> Config { get; set; } = new();

    /// <summary>
    /// Topic creation timestamp (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last update timestamp (UTC).
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Total messages currently in the topic (all partitions).
    /// </summary>
    public long MessageCount { get; set; } = 0;

    /// <summary>
    /// Number of active subscriptions.
    /// </summary>
    public int SubscriptionCount { get; set; } = 0;

    /// <summary>
    /// Validates the topic configuration.
    /// </summary>
    public bool IsValid(out List<string> errors)
    {
        errors = new List<string>();

        if (string.IsNullOrWhiteSpace(Name))
            errors.Add("Name is required");
        else if (!Regex.IsMatch(Name, @"^[a-zA-Z0-9._-]+$"))
            errors.Add("Name must contain only alphanumeric characters, dots, dashes, and underscores");

        if (string.IsNullOrWhiteSpace(SchemaId))
            errors.Add("SchemaId is required");

        if (PartitionCount < 1 || PartitionCount > 16)
            errors.Add("PartitionCount must be between 1 and 16");

        if (ReplicationFactor < 1 || ReplicationFactor > 3)
            errors.Add("ReplicationFactor must be between 1 and 3");

        if (RetentionPeriod < TimeSpan.FromHours(1))
            errors.Add("RetentionPeriod must be at least 1 hour");

        return errors.Count == 0;
    }

    /// <summary>
    /// Gets the dead letter queue name for this topic.
    /// </summary>
    public string GetDLQName() => $"{Name}.dlq";
}
