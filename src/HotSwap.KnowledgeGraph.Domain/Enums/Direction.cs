namespace HotSwap.KnowledgeGraph.Domain.Enums;

/// <summary>
/// Specifies the direction for relationship traversal in graph queries.
/// </summary>
public enum Direction
{
    /// <summary>
    /// Follow relationships from source to target (outgoing edges).
    /// Example: A -[KNOWS]-> B (from A's perspective)
    /// </summary>
    Outgoing,

    /// <summary>
    /// Follow relationships from target to source (incoming edges).
    /// Example: A <-[KNOWS]- B (from B's perspective)
    /// </summary>
    Incoming,

    /// <summary>
    /// Follow relationships in both directions (bidirectional).
    /// Example: A <-[KNOWS]-> B (from either perspective)
    /// </summary>
    Both
}
