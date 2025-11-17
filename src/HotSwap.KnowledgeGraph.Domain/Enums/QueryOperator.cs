namespace HotSwap.KnowledgeGraph.Domain.Enums;

/// <summary>
/// Specifies comparison operators for filtering graph queries.
/// </summary>
public enum QueryOperator
{
    /// <summary>
    /// Equality comparison (=).
    /// Example: property = value
    /// </summary>
    Equals,

    /// <summary>
    /// Inequality comparison (!=, <>).
    /// Example: property != value
    /// </summary>
    NotEquals,

    /// <summary>
    /// Greater than comparison (>).
    /// Example: age > 18
    /// </summary>
    GreaterThan,

    /// <summary>
    /// Greater than or equal comparison (>=).
    /// Example: score >= 90
    /// </summary>
    GreaterThanOrEqual,

    /// <summary>
    /// Less than comparison (<).
    /// Example: price < 100
    /// </summary>
    LessThan,

    /// <summary>
    /// Less than or equal comparison (<=).
    /// Example: quantity <= 10
    /// </summary>
    LessThanOrEqual,

    /// <summary>
    /// Contains substring or element (LIKE, IN, @>).
    /// Example: name contains "john", tags contains "important"
    /// </summary>
    Contains,

    /// <summary>
    /// Does not contain substring or element (NOT LIKE, NOT IN).
    /// Example: name does not contain "test"
    /// </summary>
    NotContains,

    /// <summary>
    /// Starts with substring.
    /// Example: email starts with "admin"
    /// </summary>
    StartsWith,

    /// <summary>
    /// Ends with substring.
    /// Example: filename ends with ".pdf"
    /// </summary>
    EndsWith,

    /// <summary>
    /// Pattern matching with wildcards (LIKE, ILIKE).
    /// Example: name matches "john%"
    /// </summary>
    Matches,

    /// <summary>
    /// Value is in a list of values (IN).
    /// Example: status in ["active", "pending"]
    /// </summary>
    In,

    /// <summary>
    /// Value is not in a list of values (NOT IN).
    /// Example: role not in ["guest", "banned"]
    /// </summary>
    NotIn,

    /// <summary>
    /// Value is NULL.
    /// Example: deletedAt is null
    /// </summary>
    IsNull,

    /// <summary>
    /// Value is not NULL.
    /// Example: email is not null
    /// </summary>
    IsNotNull
}
