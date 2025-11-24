namespace HotSwap.KnowledgeGraph.Api.Models;

/// <summary>
/// Request model for creating an entity.
/// </summary>
public class CreateEntityRequest
{
    /// <summary>
    /// Type of the entity (e.g., "Document", "Author", "Tag").
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Dynamic properties of the entity.
    /// </summary>
    public Dictionary<string, object> Properties { get; init; } = new();
}

/// <summary>
/// Request model for updating an entity.
/// </summary>
public class UpdateEntityRequest
{
    /// <summary>
    /// Updated properties of the entity.
    /// </summary>
    public Dictionary<string, object> Properties { get; init; } = new();
}

/// <summary>
/// Request model for creating a relationship.
/// </summary>
public class CreateRelationshipRequest
{
    /// <summary>
    /// Type of the relationship (e.g., "AUTHORED_BY", "TAGGED_WITH").
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// ID of the source entity.
    /// </summary>
    public required Guid SourceEntityId { get; init; }

    /// <summary>
    /// ID of the target entity.
    /// </summary>
    public required Guid TargetEntityId { get; init; }

    /// <summary>
    /// Dynamic properties of the relationship.
    /// </summary>
    public Dictionary<string, object> Properties { get; init; } = new();

    /// <summary>
    /// Weight of the relationship (for weighted graph algorithms).
    /// </summary>
    public double Weight { get; init; } = 1.0;

    /// <summary>
    /// Whether the relationship is directed.
    /// </summary>
    public bool IsDirected { get; init; } = true;
}

/// <summary>
/// Request model for bulk entity creation.
/// </summary>
public class BulkCreateEntitiesRequest
{
    /// <summary>
    /// List of entities to create.
    /// </summary>
    public List<CreateEntityRequest> Entities { get; init; } = new();
}

/// <summary>
/// Request model for bulk relationship creation.
/// </summary>
public class BulkCreateRelationshipsRequest
{
    /// <summary>
    /// List of relationships to create.
    /// </summary>
    public List<CreateRelationshipRequest> Relationships { get; init; } = new();
}

/// <summary>
/// Request model for importing documents.
/// </summary>
public class ImportDocumentRequest
{
    /// <summary>
    /// Title of the document.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Content of the document (Markdown or plain text).
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// Category of the document.
    /// </summary>
    public string? Category { get; init; }

    /// <summary>
    /// Tags associated with the document.
    /// </summary>
    public List<string> Tags { get; init; } = new();

    /// <summary>
    /// Author of the document.
    /// </summary>
    public string? Author { get; init; }

    /// <summary>
    /// Source path or URL of the document.
    /// </summary>
    public string? SourcePath { get; init; }

    /// <summary>
    /// Additional metadata for the document.
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = new();
}

/// <summary>
/// Request model for bulk document import.
/// </summary>
public class BulkImportDocumentsRequest
{
    /// <summary>
    /// List of documents to import.
    /// </summary>
    public List<ImportDocumentRequest> Documents { get; init; } = new();

    /// <summary>
    /// Whether to create relationships between documents with shared tags.
    /// </summary>
    public bool CreateTagRelationships { get; init; } = true;

    /// <summary>
    /// Whether to extract and link authors.
    /// </summary>
    public bool ExtractAuthors { get; init; } = true;
}

/// <summary>
/// Request model for importing documents from a directory.
/// </summary>
public class ImportDirectoryRequest
{
    /// <summary>
    /// Path to the directory containing documents.
    /// </summary>
    public required string DirectoryPath { get; init; }

    /// <summary>
    /// File pattern to match (e.g., "*.md", "*.txt").
    /// </summary>
    public string FilePattern { get; init; } = "*.md";

    /// <summary>
    /// Whether to search subdirectories.
    /// </summary>
    public bool Recursive { get; init; } = true;

    /// <summary>
    /// Category to assign to all imported documents.
    /// </summary>
    public string? Category { get; init; }

    /// <summary>
    /// Whether to create relationships between documents with shared tags.
    /// </summary>
    public bool CreateTagRelationships { get; init; } = true;
}

/// <summary>
/// Response model for entity operations.
/// </summary>
public class EntityResponse
{
    public required Guid Id { get; init; }
    public required string Type { get; init; }
    public Dictionary<string, object> Properties { get; init; } = new();
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
    public string? CreatedBy { get; init; }
    public int Version { get; init; }
}

/// <summary>
/// Response model for relationship operations.
/// </summary>
public class RelationshipResponse
{
    public required Guid Id { get; init; }
    public required string Type { get; init; }
    public required Guid SourceEntityId { get; init; }
    public required Guid TargetEntityId { get; init; }
    public Dictionary<string, object> Properties { get; init; } = new();
    public double Weight { get; init; }
    public bool IsDirected { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

/// <summary>
/// Response model for bulk operations.
/// </summary>
public class BulkOperationResponse
{
    /// <summary>
    /// Number of items successfully processed.
    /// </summary>
    public int SuccessCount { get; init; }

    /// <summary>
    /// Number of items that failed.
    /// </summary>
    public int FailureCount { get; init; }

    /// <summary>
    /// IDs of successfully created items.
    /// </summary>
    public List<Guid> CreatedIds { get; init; } = new();

    /// <summary>
    /// Error messages for failed items.
    /// </summary>
    public List<string> Errors { get; init; } = new();
}

/// <summary>
/// Response model for document import operations.
/// </summary>
public class DocumentImportResponse
{
    /// <summary>
    /// Number of documents successfully imported.
    /// </summary>
    public int DocumentsImported { get; init; }

    /// <summary>
    /// Number of tags created or linked.
    /// </summary>
    public int TagsCreated { get; init; }

    /// <summary>
    /// Number of authors created or linked.
    /// </summary>
    public int AuthorsCreated { get; init; }

    /// <summary>
    /// Number of relationships created.
    /// </summary>
    public int RelationshipsCreated { get; init; }

    /// <summary>
    /// IDs of imported document entities.
    /// </summary>
    public List<Guid> DocumentIds { get; init; } = new();

    /// <summary>
    /// Any errors encountered during import.
    /// </summary>
    public List<string> Errors { get; init; } = new();
}

/// <summary>
/// Response model for paginated results.
/// </summary>
public class PaginatedResponse<T>
{
    /// <summary>
    /// Items in the current page.
    /// </summary>
    public List<T> Items { get; init; } = new();

    /// <summary>
    /// Total count of items across all pages.
    /// </summary>
    public int TotalCount { get; init; }

    /// <summary>
    /// Current page number (0-based).
    /// </summary>
    public int Page { get; init; }

    /// <summary>
    /// Number of items per page.
    /// </summary>
    public int PageSize { get; init; }

    /// <summary>
    /// Whether there are more pages.
    /// </summary>
    public bool HasMore => (Page + 1) * PageSize < TotalCount;
}

/// <summary>
/// Standard error response.
/// </summary>
public class ErrorResponse
{
    /// <summary>
    /// Error message.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Error code for programmatic handling.
    /// </summary>
    public string? ErrorCode { get; init; }

    /// <summary>
    /// Additional details about the error.
    /// </summary>
    public Dictionary<string, object>? Details { get; init; }
}
