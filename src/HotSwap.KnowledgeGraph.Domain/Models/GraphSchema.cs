using System.Text.RegularExpressions;

namespace HotSwap.KnowledgeGraph.Domain.Models;

/// <summary>
/// Represents the schema definition for a knowledge graph, including entity types,
/// relationship types, and validation rules. Supports versioning and backward compatibility.
/// </summary>
public partial class GraphSchema
{
    private string _version = null!;

    /// <summary>
    /// Semantic version of the schema (e.g., "1.0.0", "2.1.5").
    /// Must follow semantic versioning format: MAJOR.MINOR.PATCH
    /// </summary>
    public required string Version
    {
        get => _version;
        init
        {
            ValidateVersion(value);
            _version = value;
        }
    }

    /// <summary>
    /// Dictionary of entity type definitions, keyed by entity type name.
    /// </summary>
    public required Dictionary<string, EntityTypeDefinition> EntityTypes { get; init; }

    /// <summary>
    /// Dictionary of relationship type definitions, keyed by relationship type name.
    /// </summary>
    public required Dictionary<string, RelationshipTypeDefinition> RelationshipTypes { get; init; }

    /// <summary>
    /// Timestamp when the schema was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// Identifier of the user who created the schema.
    /// </summary>
    public string? CreatedBy { get; init; }

    /// <summary>
    /// Validates the semantic version format.
    /// </summary>
    private static void ValidateVersion(string version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            throw new ArgumentException("Schema version cannot be empty.", nameof(version));
        }

        if (!SemanticVersionRegex().IsMatch(version))
        {
            throw new ArgumentException(
                "Schema version must follow semantic versioning format (e.g., '1.0.0', '2.1.5').",
                nameof(version));
        }
    }

    /// <summary>
    /// Regular expression for validating semantic version (MAJOR.MINOR.PATCH).
    /// </summary>
    [GeneratedRegex(@"^\d+\.\d+\.\d+$", RegexOptions.Compiled)]
    private static partial Regex SemanticVersionRegex();

    /// <summary>
    /// Checks if this schema is backward compatible with another schema.
    /// Compatibility is based on major version numbers (same major version = compatible).
    /// </summary>
    public bool IsCompatibleWith(GraphSchema other)
    {
        if (other == null) return false;

        var thisMajor = GetMajorVersion(Version);
        var otherMajor = GetMajorVersion(other.Version);

        return thisMajor == otherMajor;
    }

    /// <summary>
    /// Extracts the major version number from a semantic version string.
    /// </summary>
    private static int GetMajorVersion(string version)
    {
        var parts = version.Split('.');
        return int.Parse(parts[0]);
    }

    /// <summary>
    /// Validates an entity against this schema.
    /// </summary>
    /// <param name="entity">The entity to validate.</param>
    /// <returns>True if the entity is valid according to this schema; otherwise, false.</returns>
    public bool ValidateEntity(Entity entity)
    {
        if (entity == null) return false;

        // Check if entity type is defined in schema
        if (!EntityTypes.TryGetValue(entity.Type, out var entityTypeDef))
        {
            return false;
        }

        // Validate required properties
        foreach (var (propName, propDef) in entityTypeDef.Properties)
        {
            if (propDef.IsRequired && !entity.Properties.ContainsKey(propName))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Validates a relationship against this schema.
    /// </summary>
    /// <param name="relationship">The relationship to validate.</param>
    /// <param name="sourceEntityType">The type of the source entity.</param>
    /// <param name="targetEntityType">The type of the target entity.</param>
    /// <returns>True if the relationship is valid according to this schema; otherwise, false.</returns>
    public bool ValidateRelationship(Relationship relationship, string sourceEntityType, string targetEntityType)
    {
        if (relationship == null) return false;

        // Check if relationship type is defined in schema
        if (!RelationshipTypes.TryGetValue(relationship.Type, out var relTypeDef))
        {
            return false;
        }

        // Validate source and target entity types
        var isSourceValid = relTypeDef.AllowedSourceTypes.Contains(sourceEntityType);
        var isTargetValid = relTypeDef.AllowedTargetTypes.Contains(targetEntityType);

        return isSourceValid && isTargetValid;
    }
}

/// <summary>
/// Defines the structure and constraints for an entity type in the graph.
/// </summary>
public class EntityTypeDefinition
{
    /// <summary>
    /// Name of the entity type (e.g., "Person", "Document").
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Dictionary of property definitions for this entity type.
    /// </summary>
    public required Dictionary<string, PropertyDefinition> Properties { get; init; }

    /// <summary>
    /// List of property names that should be indexed for fast queries.
    /// </summary>
    public List<string>? Indexes { get; init; }

    /// <summary>
    /// Optional description of this entity type.
    /// </summary>
    public string? Description { get; init; }
}

/// <summary>
/// Defines the structure and validation rules for a property.
/// </summary>
public class PropertyDefinition
{
    /// <summary>
    /// Name of the property (e.g., "name", "email", "age").
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Data type of the property (e.g., "String", "Integer", "Boolean", "DateTime").
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Indicates whether this property is required.
    /// </summary>
    public bool IsRequired { get; init; }

    /// <summary>
    /// Optional regular expression pattern for validating string values.
    /// </summary>
    public string? ValidationPattern { get; init; }

    /// <summary>
    /// Optional minimum value for numeric properties.
    /// </summary>
    public double? MinValue { get; init; }

    /// <summary>
    /// Optional maximum value for numeric properties.
    /// </summary>
    public double? MaxValue { get; init; }

    /// <summary>
    /// Optional description of this property.
    /// </summary>
    public string? Description { get; init; }
}

/// <summary>
/// Defines the structure and constraints for a relationship type in the graph.
/// </summary>
public class RelationshipTypeDefinition
{
    /// <summary>
    /// Name of the relationship type (e.g., "AUTHORED_BY", "KNOWS", "DEPENDS_ON").
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// List of allowed source entity types for this relationship.
    /// </summary>
    public required List<string> AllowedSourceTypes { get; init; }

    /// <summary>
    /// List of allowed target entity types for this relationship.
    /// </summary>
    public required List<string> AllowedTargetTypes { get; init; }

    /// <summary>
    /// Indicates whether this relationship is directed.
    /// </summary>
    public bool IsDirected { get; init; } = true;

    /// <summary>
    /// Optional description of this relationship type.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Dictionary of property definitions for this relationship type.
    /// </summary>
    public Dictionary<string, PropertyDefinition>? Properties { get; init; }
}
