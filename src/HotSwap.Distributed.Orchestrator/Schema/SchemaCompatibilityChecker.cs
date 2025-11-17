using System.Diagnostics;
using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Orchestrator.Interfaces;
using Microsoft.Extensions.Logging;
using NJsonSchema;

namespace HotSwap.Distributed.Orchestrator.Schema;

/// <summary>
/// Checks schema compatibility and detects breaking changes between schema versions.
/// </summary>
public class SchemaCompatibilityChecker : ISchemaCompatibilityChecker
{
    private static readonly ActivitySource ActivitySource = new("HotSwap.SchemaCompatibilityChecker", "1.0.0");
    private readonly ILogger<SchemaCompatibilityChecker> _logger;

    public SchemaCompatibilityChecker(ILogger<SchemaCompatibilityChecker> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public Task<CompatibilityCheckResult> CheckCompatibilityAsync(
        MessageSchema existingSchema,
        MessageSchema newSchema,
        SchemaCompatibility compatibilityMode,
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("SchemaCompatibilityChecker.CheckCompatibility");

        activity?.SetTag("compatibility.mode", compatibilityMode.ToString());
        activity?.SetTag("existing.schema_id", existingSchema.SchemaId);
        activity?.SetTag("new.schema_id", newSchema.SchemaId);

        return compatibilityMode switch
        {
            SchemaCompatibility.None => Task.FromResult(CompatibilityCheckResult.Compatible(SchemaCompatibility.None)),
            SchemaCompatibility.Backward => CheckBackwardCompatibilityAsync(existingSchema, newSchema, cancellationToken),
            SchemaCompatibility.Forward => CheckForwardCompatibilityAsync(existingSchema, newSchema, cancellationToken),
            SchemaCompatibility.Full => CheckFullCompatibilityAsync(existingSchema, newSchema, cancellationToken),
            _ => Task.FromResult(CompatibilityCheckResult.Compatible(compatibilityMode))
        };
    }

    /// <inheritdoc/>
    public async Task<CompatibilityCheckResult> CheckBackwardCompatibilityAsync(
        MessageSchema oldSchema,
        MessageSchema newSchema,
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("SchemaCompatibilityChecker.CheckBackwardCompatibility");

        try
        {
            var oldJsonSchema = await JsonSchema.FromJsonAsync(oldSchema.SchemaDefinition, cancellationToken);
            var newJsonSchema = await JsonSchema.FromJsonAsync(newSchema.SchemaDefinition, cancellationToken);

            var breakingChanges = new List<BreakingChange>();

            // Check for added required fields (breaking for backward compatibility)
            DetectAddedRequiredFields(oldJsonSchema, newJsonSchema, breakingChanges, "$");

            // Check for type changes
            DetectTypeChanges(oldJsonSchema, newJsonSchema, breakingChanges, "$");

            // Check for removed enum values
            DetectRemovedEnumValues(oldJsonSchema, newJsonSchema, breakingChanges, "$");

            // Check for narrowed constraints
            DetectNarrowedConstraints(oldJsonSchema, newJsonSchema, breakingChanges, "$");

            var result = breakingChanges.Count == 0
                ? CompatibilityCheckResult.Compatible(SchemaCompatibility.Backward)
                : CompatibilityCheckResult.Incompatible(SchemaCompatibility.Backward, breakingChanges);

            activity?.SetTag("is_compatible", result.IsCompatible);
            activity?.SetTag("breaking_changes.count", breakingChanges.Count);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking backward compatibility");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<CompatibilityCheckResult> CheckForwardCompatibilityAsync(
        MessageSchema oldSchema,
        MessageSchema newSchema,
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("SchemaCompatibilityChecker.CheckForwardCompatibility");

        try
        {
            var oldJsonSchema = await JsonSchema.FromJsonAsync(oldSchema.SchemaDefinition, cancellationToken);
            var newJsonSchema = await JsonSchema.FromJsonAsync(newSchema.SchemaDefinition, cancellationToken);

            var breakingChanges = new List<BreakingChange>();

            // Check for removed required fields (breaking for forward compatibility)
            DetectRemovedRequiredFields(oldJsonSchema, newJsonSchema, breakingChanges, "$");

            // Check for type changes
            DetectTypeChanges(oldJsonSchema, newJsonSchema, breakingChanges, "$");

            var result = breakingChanges.Count == 0
                ? CompatibilityCheckResult.Compatible(SchemaCompatibility.Forward)
                : CompatibilityCheckResult.Incompatible(SchemaCompatibility.Forward, breakingChanges);

            activity?.SetTag("is_compatible", result.IsCompatible);
            activity?.SetTag("breaking_changes.count", breakingChanges.Count);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking forward compatibility");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<CompatibilityCheckResult> CheckFullCompatibilityAsync(
        MessageSchema oldSchema,
        MessageSchema newSchema,
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("SchemaCompatibilityChecker.CheckFullCompatibility");

        // Full compatibility = backward + forward compatible
        var backwardResult = await CheckBackwardCompatibilityAsync(oldSchema, newSchema, cancellationToken);
        var forwardResult = await CheckForwardCompatibilityAsync(oldSchema, newSchema, cancellationToken);

        var allBreakingChanges = backwardResult.BreakingChanges
            .Concat(forwardResult.BreakingChanges)
            .DistinctBy(c => $"{c.Path}:{c.ChangeType}")
            .ToList();

        var isCompatible = backwardResult.IsCompatible && forwardResult.IsCompatible;

        var result = isCompatible
            ? CompatibilityCheckResult.Compatible(SchemaCompatibility.Full)
            : CompatibilityCheckResult.Incompatible(SchemaCompatibility.Full, allBreakingChanges);

        activity?.SetTag("is_compatible", result.IsCompatible);
        activity?.SetTag("breaking_changes.count", allBreakingChanges.Count);

        return result;
    }

    private void DetectAddedRequiredFields(
        JsonSchema oldSchema,
        JsonSchema newSchema,
        List<BreakingChange> breakingChanges,
        string path)
    {
        if (newSchema.RequiredProperties == null || oldSchema.Properties == null || newSchema.Properties == null)
            return;

        foreach (var requiredProp in newSchema.RequiredProperties)
        {
            // If it's required in new but wasn't required (or didn't exist) in old
            if (!oldSchema.RequiredProperties.Contains(requiredProp))
            {
                var propertyPath = $"{path}.{requiredProp}";
                breakingChanges.Add(new BreakingChange
                {
                    ChangeType = BreakingChangeType.AddedRequiredField,
                    Path = propertyPath,
                    Description = $"Added required field '{requiredProp}' which breaks backward compatibility",
                    NewValue = "required"
                });
            }
        }

        // Recursively check nested objects
        foreach (var (propName, propSchema) in newSchema.Properties)
        {
            if (oldSchema.Properties.TryGetValue(propName, out var oldPropSchema))
            {
                if (propSchema.Type == JsonObjectType.Object && oldPropSchema.Type == JsonObjectType.Object)
                {
                    DetectAddedRequiredFields(oldPropSchema, propSchema, breakingChanges, $"{path}.{propName}");
                }
            }
        }
    }

    private void DetectRemovedRequiredFields(
        JsonSchema oldSchema,
        JsonSchema newSchema,
        List<BreakingChange> breakingChanges,
        string path)
    {
        if (oldSchema.RequiredProperties == null || newSchema.Properties == null)
            return;

        foreach (var requiredProp in oldSchema.RequiredProperties)
        {
            // If it was required in old but doesn't exist or isn't required in new
            if (!newSchema.Properties.ContainsKey(requiredProp))
            {
                var propertyPath = $"{path}.{requiredProp}";
                breakingChanges.Add(new BreakingChange
                {
                    ChangeType = BreakingChangeType.RemovedField,
                    Path = propertyPath,
                    Description = $"Removed required field '{requiredProp}' which breaks forward compatibility",
                    OldValue = "required"
                });
            }
        }

        // Recursively check nested objects
        if (oldSchema.Properties == null || newSchema.Properties == null)
            return;

        foreach (var (propName, oldPropSchema) in oldSchema.Properties)
        {
            if (newSchema.Properties.TryGetValue(propName, out var newPropSchema))
            {
                if (oldPropSchema.Type == JsonObjectType.Object && newPropSchema.Type == JsonObjectType.Object)
                {
                    DetectRemovedRequiredFields(oldPropSchema, newPropSchema, breakingChanges, $"{path}.{propName}");
                }
            }
        }
    }

    private void DetectTypeChanges(
        JsonSchema oldSchema,
        JsonSchema newSchema,
        List<BreakingChange> breakingChanges,
        string path)
    {
        if (oldSchema.Properties == null || newSchema.Properties == null)
            return;

        foreach (var (propName, oldPropSchema) in oldSchema.Properties)
        {
            if (newSchema.Properties.TryGetValue(propName, out var newPropSchema))
            {
                // Check if type changed
                if (oldPropSchema.Type != newPropSchema.Type)
                {
                    var propertyPath = $"{path}.{propName}";
                    breakingChanges.Add(new BreakingChange
                    {
                        ChangeType = BreakingChangeType.TypeChanged,
                        Path = propertyPath,
                        Description = $"Type changed from '{oldPropSchema.Type}' to '{newPropSchema.Type}'",
                        OldValue = oldPropSchema.Type.ToString(),
                        NewValue = newPropSchema.Type.ToString()
                    });
                }
                // Recursively check nested objects
                else if (oldPropSchema.Type == JsonObjectType.Object && newPropSchema.Type == JsonObjectType.Object)
                {
                    DetectTypeChanges(oldPropSchema, newPropSchema, breakingChanges, $"{path}.{propName}");
                }
                // Check array item type changes
                else if (oldPropSchema.Type == JsonObjectType.Array && newPropSchema.Type == JsonObjectType.Array)
                {
                    if (oldPropSchema.Item != null && newPropSchema.Item != null)
                    {
                        if (oldPropSchema.Item.Type != newPropSchema.Item.Type)
                        {
                            var propertyPath = $"{path}.{propName}[items]";
                            breakingChanges.Add(new BreakingChange
                            {
                                ChangeType = BreakingChangeType.TypeChanged,
                                Path = propertyPath,
                                Description = $"Array item type changed from '{oldPropSchema.Item.Type}' to '{newPropSchema.Item.Type}'",
                                OldValue = oldPropSchema.Item.Type.ToString(),
                                NewValue = newPropSchema.Item.Type.ToString()
                            });
                        }
                    }
                }
            }
        }
    }

    private void DetectRemovedEnumValues(
        JsonSchema oldSchema,
        JsonSchema newSchema,
        List<BreakingChange> breakingChanges,
        string path)
    {
        if (oldSchema.Properties == null || newSchema.Properties == null)
            return;

        foreach (var (propName, oldPropSchema) in oldSchema.Properties)
        {
            if (newSchema.Properties.TryGetValue(propName, out var newPropSchema))
            {
                if (oldPropSchema.Enumeration != null && oldPropSchema.Enumeration.Any())
                {
                    if (newPropSchema.Enumeration != null)
                    {
                        var removedValues = oldPropSchema.Enumeration
                            .Except(newPropSchema.Enumeration)
                            .ToList();

                        foreach (var removedValue in removedValues)
                        {
                            var propertyPath = $"{path}.{propName}";
                            breakingChanges.Add(new BreakingChange
                            {
                                ChangeType = BreakingChangeType.RemovedEnumValue,
                                Path = propertyPath,
                                Description = $"Removed enum value '{removedValue}' from field '{propName}'",
                                OldValue = removedValue?.ToString()
                            });
                        }
                    }
                }
            }
        }
    }

    private void DetectNarrowedConstraints(
        JsonSchema oldSchema,
        JsonSchema newSchema,
        List<BreakingChange> breakingChanges,
        string path)
    {
        if (oldSchema.Properties == null || newSchema.Properties == null)
            return;

        foreach (var (propName, oldPropSchema) in oldSchema.Properties)
        {
            if (newSchema.Properties.TryGetValue(propName, out var newPropSchema))
            {
                var propertyPath = $"{path}.{propName}";

                // Check string constraints
                if (oldPropSchema.Type == JsonObjectType.String && newPropSchema.Type == JsonObjectType.String)
                {
                    // MinLength increased (more restrictive)
                    if (newPropSchema.MinLength > oldPropSchema.MinLength)
                    {
                        breakingChanges.Add(new BreakingChange
                        {
                            ChangeType = BreakingChangeType.ConstraintNarrowed,
                            Path = propertyPath,
                            Description = $"MinLength constraint increased from {oldPropSchema.MinLength} to {newPropSchema.MinLength}",
                            OldValue = oldPropSchema.MinLength.ToString(),
                            NewValue = newPropSchema.MinLength.ToString()
                        });
                    }

                    // MaxLength decreased (more restrictive)
                    if (newPropSchema.MaxLength.HasValue && oldPropSchema.MaxLength.HasValue &&
                        newPropSchema.MaxLength < oldPropSchema.MaxLength)
                    {
                        breakingChanges.Add(new BreakingChange
                        {
                            ChangeType = BreakingChangeType.ConstraintNarrowed,
                            Path = propertyPath,
                            Description = $"MaxLength constraint decreased from {oldPropSchema.MaxLength} to {newPropSchema.MaxLength}",
                            OldValue = oldPropSchema.MaxLength.ToString(),
                            NewValue = newPropSchema.MaxLength.ToString()
                        });
                    }
                }

                // Check numeric constraints
                if ((oldPropSchema.Type == JsonObjectType.Integer || oldPropSchema.Type == JsonObjectType.Number) &&
                    (newPropSchema.Type == JsonObjectType.Integer || newPropSchema.Type == JsonObjectType.Number))
                {
                    // Minimum increased (more restrictive)
                    if (newPropSchema.Minimum > oldPropSchema.Minimum)
                    {
                        breakingChanges.Add(new BreakingChange
                        {
                            ChangeType = BreakingChangeType.ConstraintNarrowed,
                            Path = propertyPath,
                            Description = $"Minimum constraint increased from {oldPropSchema.Minimum} to {newPropSchema.Minimum}",
                            OldValue = oldPropSchema.Minimum.ToString(),
                            NewValue = newPropSchema.Minimum.ToString()
                        });
                    }

                    // Maximum decreased (more restrictive)
                    if (newPropSchema.Maximum.HasValue && oldPropSchema.Maximum.HasValue &&
                        newPropSchema.Maximum < oldPropSchema.Maximum)
                    {
                        breakingChanges.Add(new BreakingChange
                        {
                            ChangeType = BreakingChangeType.ConstraintNarrowed,
                            Path = propertyPath,
                            Description = $"Maximum constraint decreased from {oldPropSchema.Maximum} to {newPropSchema.Maximum}",
                            OldValue = oldPropSchema.Maximum.ToString(),
                            NewValue = newPropSchema.Maximum.ToString()
                        });
                    }
                }
            }
        }
    }
}
