namespace HotSwap.Distributed.Domain.Enums;

/// <summary>
/// Represents the type of a topic.
/// </summary>
public enum TopicType
{
    /// <summary>
    /// Point-to-point queue (single consumer per message).
    /// </summary>
    Queue,

    /// <summary>
    /// Publish-subscribe topic (all consumers receive message).
    /// </summary>
    PubSub
}
