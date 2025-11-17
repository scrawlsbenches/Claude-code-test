using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;

namespace HotSwap.Distributed.Orchestrator.Interfaces;

/// <summary>
/// Defines operations for schema storage and management.
/// Provides centralized schema validation and evolution capabilities.
/// </summary>
public interface ISchemaRegistry
{
    /// <summary>
    /// Registers a new message schema in the registry.
    /// </summary>
    /// <param name="schema">The schema to register.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The registered schema with assigned ID and timestamps.</returns>
    /// <exception cref="ArgumentException">If schema validation fails.</exception>
    /// <exception cref="InvalidOperationException">If schema ID already exists.</exception>
    Task<MessageSchema> RegisterSchemaAsync(
        MessageSchema schema,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a schema by its unique identifier.
    /// </summary>
    /// <param name="schemaId">The unique schema identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The message schema, or null if not found.</returns>
    Task<MessageSchema?> GetSchemaAsync(
        string schemaId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all schemas in the registry, optionally filtered by status.
    /// </summary>
    /// <param name="status">Optional status filter (null returns all schemas).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of message schemas matching the criteria.</returns>
    Task<List<MessageSchema>> ListSchemasAsync(
        SchemaStatus? status = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the approval status of a schema.
    /// </summary>
    /// <param name="schemaId">The schema identifier to update.</param>
    /// <param name="newStatus">The new status to set.</param>
    /// <param name="approvedBy">Admin user approving the schema (required for Approved status).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if updated successfully, false if schema not found.</returns>
    /// <exception cref="ArgumentException">If approvedBy is null when status is Approved.</exception>
    Task<bool> UpdateSchemaStatusAsync(
        string schemaId,
        SchemaStatus newStatus,
        string? approvedBy = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a schema from the registry.
    /// Only draft schemas can be deleted.
    /// </summary>
    /// <param name="schemaId">The schema identifier to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if deleted successfully, false if schema not found.</returns>
    /// <exception cref="InvalidOperationException">If attempting to delete a non-draft schema.</exception>
    Task<bool> DeleteSchemaAsync(
        string schemaId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a schema with the given ID exists.
    /// </summary>
    /// <param name="schemaId">The schema identifier to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the schema exists, false otherwise.</returns>
    Task<bool> SchemaExistsAsync(
        string schemaId,
        CancellationToken cancellationToken = default);
}
