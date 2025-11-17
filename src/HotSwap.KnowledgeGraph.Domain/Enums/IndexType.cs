namespace HotSwap.KnowledgeGraph.Domain.Enums;

/// <summary>
/// Specifies the type of database index for optimizing queries.
/// </summary>
public enum IndexType
{
    /// <summary>
    /// B-Tree index for ordered data and range queries.
    /// Best for: Equality and range comparisons (=, <, >, <=, >=, BETWEEN).
    /// </summary>
    BTree,

    /// <summary>
    /// Hash index for exact matches.
    /// Best for: Equality comparisons (=).
    /// </summary>
    Hash,

    /// <summary>
    /// GIN (Generalized Inverted Index) for complex data types.
    /// Best for: JSONB, arrays, full-text search.
    /// </summary>
    GIN,

    /// <summary>
    /// GiST (Generalized Search Tree) for geometric and full-text data.
    /// Best for: Geometric types, full-text search, custom operators.
    /// </summary>
    GiST,

    /// <summary>
    /// Full-text search index for text search operations.
    /// Best for: Full-text search with ranking and stemming.
    /// </summary>
    FullText
}
