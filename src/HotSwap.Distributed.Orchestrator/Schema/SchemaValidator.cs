using System.Diagnostics;
using HotSwap.Distributed.Orchestrator.Interfaces;
using Microsoft.Extensions.Logging;
using NJsonSchema;

namespace HotSwap.Distributed.Orchestrator.Schema;

/// <summary>
/// Validates JSON payloads against JSON schemas using NJsonSchema library.
/// </summary>
public class SchemaValidator : ISchemaValidator
{
    private static readonly ActivitySource ActivitySource = new("HotSwap.SchemaValidator", "1.0.0");
    private readonly ILogger<SchemaValidator> _logger;

    public SchemaValidator(ILogger<SchemaValidator> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<SchemaValidationResult> ValidateAsync(
        string payload,
        string schemaDefinition,
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("SchemaValidator.Validate");
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Validate input parameters
            ArgumentNullException.ThrowIfNull(payload, nameof(payload));

            if (string.IsNullOrWhiteSpace(payload))
            {
                throw new ArgumentException("Payload cannot be empty", nameof(payload));
            }

            if (string.IsNullOrWhiteSpace(schemaDefinition))
            {
                throw new ArgumentException("Schema definition cannot be empty", nameof(schemaDefinition));
            }

            activity?.SetTag("payload.length", payload.Length);
            activity?.SetTag("schema.length", schemaDefinition.Length);

            // Parse the JSON schema
            JsonSchema schema;
            try
            {
                schema = await JsonSchema.FromJsonAsync(schemaDefinition, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Invalid JSON schema definition");
                activity?.SetStatus(ActivityStatusCode.Error, "Invalid schema");
                throw new ArgumentException($"Invalid JSON schema: {ex.Message}", nameof(schemaDefinition), ex);
            }

            // Parse the payload JSON
            try
            {
                System.Text.Json.JsonDocument.Parse(payload);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Invalid JSON payload");
                activity?.SetStatus(ActivityStatusCode.Error, "Invalid payload JSON");
                throw new ArgumentException($"Invalid JSON payload: {ex.Message}", nameof(payload), ex);
            }

            // Validate the payload against the schema
            var validationErrors = schema.Validate(payload);

            stopwatch.Stop();
            var validationTimeMs = stopwatch.ElapsedMilliseconds;

            if (validationErrors.Count == 0)
            {
                _logger.LogDebug("Validation succeeded in {ElapsedMs}ms", validationTimeMs);
                activity?.SetTag("validation.success", true);
                activity?.SetTag("validation.time_ms", validationTimeMs);
                activity?.SetStatus(ActivityStatusCode.Ok);

                return SchemaValidationResult.Success(validationTimeMs);
            }

            // Convert NJsonSchema validation errors to our format
            var errors = validationErrors.Select(error => new ValidationError
            {
                Path = error.Path ?? "$",
                Message = error.ToString(),
                Kind = error.Kind.ToString(),
                Expected = error.Property != null ? $"{error.Property} constraint" : null,
                Actual = null // NJsonSchema doesn't always provide actual value
            }).ToList();

            _logger.LogWarning("Validation failed with {ErrorCount} errors in {ElapsedMs}ms",
                errors.Count,
                validationTimeMs);

            activity?.SetTag("validation.success", false);
            activity?.SetTag("validation.error_count", errors.Count);
            activity?.SetTag("validation.time_ms", validationTimeMs);
            activity?.SetStatus(ActivityStatusCode.Ok); // Validation ran successfully, just found errors

            return SchemaValidationResult.Failure(errors, validationTimeMs);
        }
        catch (ArgumentNullException)
        {
            // Re-throw null argument exceptions
            throw;
        }
        catch (ArgumentException)
        {
            // Re-throw argument exceptions (invalid schema, invalid payload)
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Unexpected error during schema validation");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

            // Return failure result with generic error
            return SchemaValidationResult.Failure(
                new List<ValidationError>
                {
                    new ValidationError
                    {
                        Path = "$",
                        Message = $"Validation error: {ex.Message}",
                        Kind = "ValidationException"
                    }
                },
                stopwatch.ElapsedMilliseconds);
        }
    }
}
