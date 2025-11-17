using System.Text.RegularExpressions;

namespace HotSwap.KnowledgeGraph.Domain.Models;

/// <summary>
/// Represents a directed or undirected relationship between two entities in the knowledge graph.
/// Relationships are edges connecting nodes with a type, properties, and optional weight.
/// </summary>
public partial class Relationship : IEquatable<Relationship>
{
    private string _type = null!;
    private double _weight = 1.0;

    /// <summary>
    /// Unique identifier for the relationship.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// Type of the relationship (e.g., "AUTHORED_BY", "RELATED_TO", "DEPENDS_ON").
    /// Must be alphanumeric and no more than 100 characters.
    /// Convention: Use UPPER_CASE with underscores.
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
    /// ID of the source entity (origin of the relationship).
    /// </summary>
    public required Guid SourceEntityId { get; init; }

    /// <summary>
    /// ID of the target entity (destination of the relationship).
    /// </summary>
    public required Guid TargetEntityId { get; init; }

    /// <summary>
    /// Dynamic properties of the relationship stored as key-value pairs.
    /// Properties are stored as JSONB in PostgreSQL.
    /// </summary>
    public required Dictionary<string, object> Properties { get; init; }

    /// <summary>
    /// Weight or strength of the relationship, used for graph traversal algorithms.
    /// Must be non-negative. Default is 1.0.
    /// </summary>
    public double Weight
    {
        get => _weight;
        init
        {
            if (value < 0)
            {
                throw new ArgumentException("Weight cannot be negative.", nameof(Weight));
            }
            _weight = value;
        }
    }

    /// <summary>
    /// Indicates whether the relationship is directed (Source → Target).
    /// If false, the relationship is bidirectional (Source ↔ Target).
    /// </summary>
    public bool IsDirected { get; init; } = true;

    /// <summary>
    /// Timestamp when the relationship was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// Identifier of the user who created the relationship.
    /// </summary>
    public string? CreatedBy { get; init; }

    /// <summary>
    /// Validates the relationship type according to business rules.
    /// </summary>
    /// <param name="type">The relationship type to validate.</param>
    /// <exception cref="ArgumentException">Thrown when type is invalid.</exception>
    private static void ValidateType(string type)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            throw new ArgumentException("Relationship Type cannot be empty or whitespace.", nameof(type));
        }

        if (type.Length > 100)
        {
            throw new ArgumentException("Relationship Type must not exceed 100 characters.", nameof(type));
        }

        if (!AlphanumericRegex().IsMatch(type))
        {
            throw new ArgumentException("Relationship Type must contain only alphanumeric characters and underscores.", nameof(type));
        }
    }

    /// <summary>
    /// Regular expression for validating alphanumeric relationship types.
    /// Allows letters, digits, and underscores.
    /// </summary>
    [GeneratedRegex(@"^[a-zA-Z0-9_]+$", RegexOptions.Compiled)]
    private static partial Regex AlphanumericRegex();

    #region Equality

    /// <summary>
    /// Determines whether the specified relationship is equal to the current relationship.
    /// Equality is based on the Id property.
    /// </summary>
    public bool Equals(Relationship? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Id.Equals(other.Id);
    }

    /// <summary>
    /// Determines whether the specified object is equal to the current relationship.
    /// </summary>
    public override bool Equals(object? obj)
    {
        return Equals(obj as Relationship);
    }

    /// <summary>
    /// Returns the hash code for this relationship based on its Id.
    /// </summary>
    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }

    /// <summary>
    /// Equality operator for relationships.
    /// </summary>
    public static bool operator ==(Relationship? left, Relationship? right)
    {
        if (left is null) return right is null;
        return left.Equals(right);
    }

    /// <summary>
    /// Inequality operator for relationships.
    /// </summary>
    public static bool operator !=(Relationship? left, Relationship? right)
    {
        return !(left == right);
    }

    #endregion

    /// <summary>
    /// Returns a string representation of the relationship.
    /// </summary>
    public override string ToString()
    {
        var directionSymbol = IsDirected ? "→" : "↔";
        return $"Relationship [{Type}] {SourceEntityId} {directionSymbol} {TargetEntityId}, Weight={Weight}";
    }
}
