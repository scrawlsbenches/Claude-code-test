namespace HotSwap.Distributed.Domain.Enums;

/// <summary>
/// Represents schema compatibility mode.
/// </summary>
public enum SchemaCompatibility
{
    /// <summary>
    /// No compatibility checks.
    /// </summary>
    None,

    /// <summary>
    /// New schema can read old data.
    /// </summary>
    Backward,

    /// <summary>
    /// Old schema can read new data.
    /// </summary>
    Forward,

    /// <summary>
    /// Bidirectional compatibility (both backward and forward).
    /// </summary>
    Full
}
