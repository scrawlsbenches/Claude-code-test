using System.Text.RegularExpressions;

namespace HotSwap.KnowledgeGraph.Domain.Models;

/// <summary>
/// Represents an entity in the knowledge graph.
/// Entities are nodes with a type, properties, and metadata.
/// </summary>
public partial class Entity : IEquatable<Entity>
{
    private string _type = null!;

    /// <summary>
    /// Unique identifier for the entity.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// Type of the entity (e.g., "Person", "Document", "Organization").
    /// Must be alphanumeric and no more than 100 characters.
    /// </summary>
    public required string Type
    {
        get => _type;
        init
        {
            ValidateType(value);
            _type = value;
        }
    }

    /// <summary>
    /// Dynamic properties of the entity stored as key-value pairs.
    /// Properties are stored as JSONB in PostgreSQL.
    /// </summary>
    public required Dictionary<string, object> Properties { get; init; }

    /// <summary>
    /// Timestamp when the entity was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// Timestamp when the entity was last updated.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// Identifier of the user who created the entity.
    /// </summary>
    public string? CreatedBy { get; init; }

    /// <summary>
    /// Identifier of the user who last updated the entity.
    /// </summary>
    public string? UpdatedBy { get; set; }

    /// <summary>
    /// Version number for optimistic concurrency control.
    /// Incremented on each update.
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// Validates the entity type according to business rules.
    /// </summary>
    /// <param name="type">The entity type to validate.</param>
    /// <exception cref="ArgumentException">Thrown when type is invalid.</exception>
    private static void ValidateType(string type)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            throw new ArgumentException("Entity Type cannot be empty or whitespace.", nameof(type));
        }

        if (type.Length > 100)
        {
            throw new ArgumentException("Entity Type must not exceed 100 characters.", nameof(type));
        }

        if (!AlphanumericRegex().IsMatch(type))
        {
            throw new ArgumentException("Entity Type must contain only alphanumeric characters and underscores.", nameof(type));
        }
    }

    /// <summary>
    /// Regular expression for validating alphanumeric entity types.
    /// Allows letters, digits, and underscores.
    /// </summary>
    [GeneratedRegex(@"^[a-zA-Z0-9_]+$", RegexOptions.Compiled)]
    private static partial Regex AlphanumericRegex();

    #region Equality

    /// <summary>
    /// Determines whether the specified entity is equal to the current entity.
    /// Equality is based on the Id property.
    /// </summary>
    public bool Equals(Entity? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Id.Equals(other.Id);
    }

    /// <summary>
    /// Determines whether the specified object is equal to the current entity.
    /// </summary>
    public override bool Equals(object? obj)
    {
        return Equals(obj as Entity);
    }

    /// <summary>
    /// Returns the hash code for this entity based on its Id.
    /// </summary>
    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }

    /// <summary>
    /// Equality operator for entities.
    /// </summary>
    public static bool operator ==(Entity? left, Entity? right)
    {
        if (left is null) return right is null;
        return left.Equals(right);
    }

    /// <summary>
    /// Inequality operator for entities.
    /// </summary>
    public static bool operator !=(Entity? left, Entity? right)
    {
        return !(left == right);
    }

    #endregion

    /// <summary>
    /// Returns a string representation of the entity.
    /// </summary>
    public override string ToString()
    {
        return $"Entity [{Type}] Id={Id}, Properties={Properties.Count}, Version={Version}";
    }
}
