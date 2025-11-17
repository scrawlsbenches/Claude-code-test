using HotSwap.Distributed.Domain.Enums;
using System.Text.Json;

namespace HotSwap.Distributed.Domain.Models;

/// <summary>
/// Represents a message schema for validation and evolution.
/// </summary>
public class MessageSchema
{
    /// <summary>
    /// Unique schema identifier (e.g., "deployment.event.v1").
    /// </summary>
    public required string SchemaId { get; set; }

    /// <summary>
    /// JSON Schema definition (JSON format).
    /// </summary>
    public required string SchemaDefinition { get; set; }

    /// <summary>
    /// Schema version number (e.g., "1.0", "2.0").
    /// </summary>
    public string Version { get; set; } = "1.0";

    /// <summary>
    /// Schema compatibility mode.
    /// </summary>
    public SchemaCompatibility Compatibility { get; set; } = SchemaCompatibility.None;

    /// <summary>
    /// Current schema status (approval workflow).
    /// </summary>
    public SchemaStatus Status { get; set; } = SchemaStatus.Draft;

    /// <summary>
    /// Admin user who approved the schema (if approved).
    /// </summary>
    public string? ApprovedBy { get; set; }

    /// <summary>
    /// Approval timestamp (UTC).
    /// </summary>
    public DateTime? ApprovedAt { get; set; }

    /// <summary>
    /// Schema creation timestamp (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Schema deprecation timestamp (UTC).
    /// </summary>
    public DateTime? DeprecatedAt { get; set; }

    /// <summary>
    /// Deprecation reason (if deprecated).
    /// </summary>
    public string? DeprecationReason { get; set; }

    /// <summary>
    /// Migration guide URL (if deprecated).
    /// </summary>
    public string? MigrationGuide { get; set; }

    /// <summary>
    /// Validates the schema configuration.
    /// </summary>
    public bool IsValid(out List<string> errors)
    {
        errors = new List<string>();

        if (string.IsNullOrWhiteSpace(SchemaId))
            errors.Add("SchemaId is required");

        if (string.IsNullOrWhiteSpace(SchemaDefinition))
            errors.Add("SchemaDefinition is required");

        if (string.IsNullOrWhiteSpace(Version))
            errors.Add("Version is required");

        // Validate JSON Schema format
        if (!string.IsNullOrWhiteSpace(SchemaDefinition))
        {
            try
            {
                JsonDocument.Parse(SchemaDefinition);
            }
            catch
            {
                errors.Add("SchemaDefinition must be valid JSON");
            }
        }

        return errors.Count == 0;
    }

    /// <summary>
    /// Checks if the schema is approved for production use.
    /// </summary>
    public bool IsApproved() => Status == SchemaStatus.Approved;

    /// <summary>
    /// Checks if the schema is deprecated.
    /// </summary>
    public bool IsDeprecated() => Status == SchemaStatus.Deprecated;
}
