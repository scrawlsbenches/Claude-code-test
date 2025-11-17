namespace HotSwap.Distributed.Orchestrator.Interfaces;

/// <summary>
/// Defines operations for validating message payloads against JSON schemas.
/// </summary>
public interface ISchemaValidator
{
    /// <summary>
    /// Validates a JSON payload against a JSON schema definition.
    /// </summary>
    /// <param name="payload">The JSON payload to validate.</param>
    /// <param name="schemaDefinition">The JSON schema definition (JSON format).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Validation result containing success status and errors.</returns>
    Task<SchemaValidationResult> ValidateAsync(
        string payload,
        string schemaDefinition,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents the result of schema validation.
/// </summary>
public class SchemaValidationResult
{
    /// <summary>
    /// Indicates whether validation succeeded.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// List of validation errors (empty if validation succeeded).
    /// </summary>
    public List<ValidationError> Errors { get; set; } = new();

    /// <summary>
    /// Validation duration in milliseconds.
    /// </summary>
    public long ValidationTimeMs { get; set; }

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    public static SchemaValidationResult Success(long validationTimeMs = 0)
    {
        return new SchemaValidationResult
        {
            IsValid = true,
            ValidationTimeMs = validationTimeMs
        };
    }

    /// <summary>
    /// Creates a failed validation result with errors.
    /// </summary>
    public static SchemaValidationResult Failure(
        List<ValidationError> errors,
        long validationTimeMs = 0)
    {
        return new SchemaValidationResult
        {
            IsValid = false,
            Errors = errors,
            ValidationTimeMs = validationTimeMs
        };
    }
}

/// <summary>
/// Represents a single validation error.
/// </summary>
public class ValidationError
{
    /// <summary>
    /// JSON path to the invalid property (e.g., "$.user.email").
    /// </summary>
    public required string Path { get; set; }

    /// <summary>
    /// Error message describing the validation failure.
    /// </summary>
    public required string Message { get; set; }

    /// <summary>
    /// Error kind (e.g., "type", "required", "pattern").
    /// </summary>
    public string? Kind { get; set; }

    /// <summary>
    /// Expected value or constraint.
    /// </summary>
    public string? Expected { get; set; }

    /// <summary>
    /// Actual value that failed validation.
    /// </summary>
    public string? Actual { get; set; }
}
