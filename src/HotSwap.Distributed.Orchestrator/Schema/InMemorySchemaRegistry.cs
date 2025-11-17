using System.Collections.Concurrent;
using System.Diagnostics;
using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Orchestrator.Interfaces;
using Microsoft.Extensions.Logging;

namespace HotSwap.Distributed.Orchestrator.Schema;

/// <summary>
/// In-memory implementation of schema registry for testing and development.
/// Thread-safe using ConcurrentDictionary.
/// </summary>
public class InMemorySchemaRegistry : ISchemaRegistry
{
    private static readonly ActivitySource ActivitySource = new("HotSwap.SchemaRegistry", "1.0.0");
    private readonly ILogger<InMemorySchemaRegistry> _logger;
    private readonly ConcurrentDictionary<string, MessageSchema> _schemas;

    public InMemorySchemaRegistry(ILogger<InMemorySchemaRegistry> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _schemas = new ConcurrentDictionary<string, MessageSchema>();

        _logger.LogInformation("InMemorySchemaRegistry initialized");
    }

    /// <inheritdoc/>
    public Task<MessageSchema> RegisterSchemaAsync(
        MessageSchema schema,
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("SchemaRegistry.RegisterSchema");

        ArgumentNullException.ThrowIfNull(schema);

        activity?.SetTag("schema.id", schema.SchemaId);
        activity?.SetTag("schema.version", schema.Version);

        // Validate schema
        if (!schema.IsValid(out var errors))
        {
            var errorMessage = $"Schema validation failed: {string.Join(", ", errors)}";
            _logger.LogError("Failed to register schema {SchemaId}: {Errors}",
                schema.SchemaId,
                errorMessage);

            activity?.SetStatus(ActivityStatusCode.Error, errorMessage);
            throw new ArgumentException(errorMessage, nameof(schema));
        }

        // Check for duplicate
        if (_schemas.ContainsKey(schema.SchemaId))
        {
            var errorMessage = $"Schema with ID '{schema.SchemaId}' already exists";
            _logger.LogError("Duplicate schema ID: {SchemaId}", schema.SchemaId);

            activity?.SetStatus(ActivityStatusCode.Error, errorMessage);
            throw new InvalidOperationException(errorMessage);
        }

        // Set timestamps
        schema.CreatedAt = DateTime.UtcNow;

        // Add to registry
        if (!_schemas.TryAdd(schema.SchemaId, schema))
        {
            var errorMessage = $"Failed to add schema '{schema.SchemaId}' to registry";
            _logger.LogError("Concurrent modification detected for schema {SchemaId}", schema.SchemaId);

            activity?.SetStatus(ActivityStatusCode.Error, errorMessage);
            throw new InvalidOperationException(errorMessage);
        }

        _logger.LogInformation("Registered schema {SchemaId} version {Version}",
            schema.SchemaId,
            schema.Version);

        activity?.SetStatus(ActivityStatusCode.Ok);
        return Task.FromResult(schema);
    }

    /// <inheritdoc/>
    public Task<MessageSchema?> GetSchemaAsync(
        string schemaId,
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("SchemaRegistry.GetSchema");

        if (string.IsNullOrWhiteSpace(schemaId))
        {
            throw new ArgumentException("SchemaId cannot be null or empty", nameof(schemaId));
        }

        activity?.SetTag("schema.id", schemaId);

        _schemas.TryGetValue(schemaId, out var schema);

        if (schema != null)
        {
            activity?.SetTag("schema.found", true);
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        else
        {
            activity?.SetTag("schema.found", false);
            _logger.LogWarning("Schema {SchemaId} not found", schemaId);
        }

        return Task.FromResult(schema);
    }

    /// <inheritdoc/>
    public Task<List<MessageSchema>> ListSchemasAsync(
        SchemaStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("SchemaRegistry.ListSchemas");

        activity?.SetTag("filter.status", status?.ToString() ?? "all");

        var schemas = _schemas.Values.AsEnumerable();

        if (status.HasValue)
        {
            schemas = schemas.Where(s => s.Status == status.Value);
        }

        var result = schemas.ToList();

        activity?.SetTag("schemas.count", result.Count);
        activity?.SetStatus(ActivityStatusCode.Ok);

        _logger.LogDebug("Listed {Count} schemas (filter: {Status})",
            result.Count,
            status?.ToString() ?? "all");

        return Task.FromResult(result);
    }

    /// <inheritdoc/>
    public Task<bool> UpdateSchemaStatusAsync(
        string schemaId,
        SchemaStatus newStatus,
        string? approvedBy = null,
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("SchemaRegistry.UpdateSchemaStatus");

        ArgumentException.ThrowIfNullOrWhiteSpace(schemaId, nameof(schemaId));

        activity?.SetTag("schema.id", schemaId);
        activity?.SetTag("new.status", newStatus.ToString());

        // Validate approvedBy for Approved status
        if (newStatus == SchemaStatus.Approved && string.IsNullOrWhiteSpace(approvedBy))
        {
            var errorMessage = "ApprovedBy is required when setting status to Approved";
            _logger.LogError("Approval without approver for schema {SchemaId}", schemaId);

            activity?.SetStatus(ActivityStatusCode.Error, errorMessage);
            throw new ArgumentException(errorMessage, nameof(approvedBy));
        }

        if (!_schemas.TryGetValue(schemaId, out var schema))
        {
            _logger.LogWarning("Cannot update status: schema {SchemaId} not found", schemaId);
            activity?.SetStatus(ActivityStatusCode.Error, "Schema not found");
            return Task.FromResult(false);
        }

        // Update status
        schema.Status = newStatus;

        // Set approval metadata if applicable
        if (newStatus == SchemaStatus.Approved)
        {
            schema.ApprovedBy = approvedBy;
            schema.ApprovedAt = DateTime.UtcNow;
        }

        // Update the schema in the dictionary
        _schemas[schemaId] = schema;

        _logger.LogInformation("Updated schema {SchemaId} status to {Status}",
            schemaId,
            newStatus);

        activity?.SetStatus(ActivityStatusCode.Ok);
        return Task.FromResult(true);
    }

    /// <inheritdoc/>
    public Task<bool> DeleteSchemaAsync(
        string schemaId,
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("SchemaRegistry.DeleteSchema");

        ArgumentException.ThrowIfNullOrWhiteSpace(schemaId, nameof(schemaId));

        activity?.SetTag("schema.id", schemaId);

        if (!_schemas.TryGetValue(schemaId, out var schema))
        {
            _logger.LogWarning("Cannot delete: schema {SchemaId} not found", schemaId);
            activity?.SetStatus(ActivityStatusCode.Error, "Schema not found");
            return Task.FromResult(false);
        }

        // Only draft schemas can be deleted
        if (schema.Status != SchemaStatus.Draft)
        {
            var errorMessage = $"Cannot delete schema '{schemaId}' with status '{schema.Status}'. Only Draft schemas can be deleted.";
            _logger.LogError("Attempted to delete non-draft schema: {SchemaId} (status: {Status})",
                schemaId,
                schema.Status);

            activity?.SetStatus(ActivityStatusCode.Error, errorMessage);
            throw new InvalidOperationException(errorMessage);
        }

        // Remove from registry
        if (!_schemas.TryRemove(schemaId, out _))
        {
            _logger.LogError("Failed to remove schema {SchemaId} from registry", schemaId);
            activity?.SetStatus(ActivityStatusCode.Error, "Failed to remove schema");
            return Task.FromResult(false);
        }

        _logger.LogInformation("Deleted schema {SchemaId}", schemaId);
        activity?.SetStatus(ActivityStatusCode.Ok);
        return Task.FromResult(true);
    }

    /// <inheritdoc/>
    public Task<bool> SchemaExistsAsync(
        string schemaId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaId, nameof(schemaId));

        var exists = _schemas.ContainsKey(schemaId);

        _logger.LogDebug("Schema {SchemaId} exists: {Exists}", schemaId, exists);

        return Task.FromResult(exists);
    }
}
