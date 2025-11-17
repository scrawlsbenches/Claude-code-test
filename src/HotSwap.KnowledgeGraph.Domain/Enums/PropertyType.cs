namespace HotSwap.KnowledgeGraph.Domain.Enums;

/// <summary>
/// Specifies the data type for entity and relationship properties.
/// </summary>
public enum PropertyType
{
    /// <summary>
    /// String/text value.
    /// </summary>
    String,

    /// <summary>
    /// Integer number (32-bit or 64-bit).
    /// </summary>
    Integer,

    /// <summary>
    /// Double-precision floating-point number.
    /// </summary>
    Double,

    /// <summary>
    /// Boolean value (true/false).
    /// </summary>
    Boolean,

    /// <summary>
    /// Date and time value with timezone.
    /// </summary>
    DateTime,

    /// <summary>
    /// Date value without time component.
    /// </summary>
    Date,

    /// <summary>
    /// JSON object or array.
    /// Stored as JSONB in PostgreSQL for efficient querying.
    /// </summary>
    Json,

    /// <summary>
    /// Array of values.
    /// </summary>
    Array,

    /// <summary>
    /// Universally unique identifier (UUID/GUID).
    /// </summary>
    Guid,

    /// <summary>
    /// Binary large object (BLOB).
    /// </summary>
    Binary
}
