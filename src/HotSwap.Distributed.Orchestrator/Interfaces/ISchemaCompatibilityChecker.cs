using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;

namespace HotSwap.Distributed.Orchestrator.Interfaces;

/// <summary>
/// Defines operations for checking schema compatibility and detecting breaking changes.
/// </summary>
public interface ISchemaCompatibilityChecker
{
    /// <summary>
    /// Checks if a new schema is compatible with an existing schema based on compatibility mode.
    /// </summary>
    /// <param name="existingSchema">The current schema.</param>
    /// <param name="newSchema">The proposed new schema.</param>
    /// <param name="compatibilityMode">The compatibility mode to enforce.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Compatibility check result with any breaking changes detected.</returns>
    Task<CompatibilityCheckResult> CheckCompatibilityAsync(
        MessageSchema existingSchema,
        MessageSchema newSchema,
        SchemaCompatibility compatibilityMode,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks backward compatibility: new schema can read data written with old schema.
    /// </summary>
    /// <param name="oldSchema">The old schema.</param>
    /// <param name="newSchema">The new schema.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Compatibility check result.</returns>
    Task<CompatibilityCheckResult> CheckBackwardCompatibilityAsync(
        MessageSchema oldSchema,
        MessageSchema newSchema,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks forward compatibility: old schema can read data written with new schema.
    /// </summary>
    /// <param name="oldSchema">The old schema.</param>
    /// <param name="newSchema">The new schema.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Compatibility check result.</returns>
    Task<CompatibilityCheckResult> CheckForwardCompatibilityAsync(
        MessageSchema oldSchema,
        MessageSchema newSchema,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks full compatibility: schemas are both forward and backward compatible.
    /// </summary>
    /// <param name="oldSchema">The old schema.</param>
    /// <param name="newSchema">The new schema.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Compatibility check result.</returns>
    Task<CompatibilityCheckResult> CheckFullCompatibilityAsync(
        MessageSchema oldSchema,
        MessageSchema newSchema,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents the result of a schema compatibility check.
/// </summary>
public class CompatibilityCheckResult
{
    /// <summary>
    /// Indicates whether the schemas are compatible.
    /// </summary>
    public bool IsCompatible { get; set; }

    /// <summary>
    /// List of breaking changes detected (empty if compatible).
    /// </summary>
    public List<BreakingChange> BreakingChanges { get; set; } = new();

    /// <summary>
    /// Compatibility mode that was checked.
    /// </summary>
    public SchemaCompatibility CompatibilityMode { get; set; }

    /// <summary>
    /// Creates a compatible result.
    /// </summary>
    public static CompatibilityCheckResult Compatible(SchemaCompatibility mode)
    {
        return new CompatibilityCheckResult
        {
            IsCompatible = true,
            CompatibilityMode = mode
        };
    }

    /// <summary>
    /// Creates an incompatible result with breaking changes.
    /// </summary>
    public static CompatibilityCheckResult Incompatible(
        SchemaCompatibility mode,
        List<BreakingChange> breakingChanges)
    {
        return new CompatibilityCheckResult
        {
            IsCompatible = false,
            CompatibilityMode = mode,
            BreakingChanges = breakingChanges
        };
    }
}

/// <summary>
/// Represents a breaking change between schemas.
/// </summary>
public class BreakingChange
{
    /// <summary>
    /// Type of breaking change.
    /// </summary>
    public required BreakingChangeType ChangeType { get; set; }

    /// <summary>
    /// JSON path to the affected property (e.g., "$.user.email").
    /// </summary>
    public required string Path { get; set; }

    /// <summary>
    /// Description of the breaking change.
    /// </summary>
    public required string Description { get; set; }

    /// <summary>
    /// Old value (before change).
    /// </summary>
    public string? OldValue { get; set; }

    /// <summary>
    /// New value (after change).
    /// </summary>
    public string? NewValue { get; set; }
}

/// <summary>
/// Types of breaking changes.
/// </summary>
public enum BreakingChangeType
{
    /// <summary>
    /// A required field was added.
    /// </summary>
    AddedRequiredField,

    /// <summary>
    /// A field was removed.
    /// </summary>
    RemovedField,

    /// <summary>
    /// A field's type was changed.
    /// </summary>
    TypeChanged,

    /// <summary>
    /// An enum value was removed.
    /// </summary>
    RemovedEnumValue,

    /// <summary>
    /// A constraint was made more restrictive.
    /// </summary>
    ConstraintNarrowed,

    /// <summary>
    /// A field was made required that was previously optional.
    /// </summary>
    FieldMadeRequired,

    /// <summary>
    /// Other breaking change.
    /// </summary>
    Other
}
