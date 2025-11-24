using HotSwap.KnowledgeGraph.Api.Models;
using HotSwap.KnowledgeGraph.Domain.Models;

namespace HotSwap.KnowledgeGraph.Api.Services;

/// <summary>
/// Service for ingesting documents into the knowledge graph.
/// Handles parsing, entity creation, and relationship extraction.
/// </summary>
public interface IDocumentIngestionService
{
    /// <summary>
    /// Imports a single document into the knowledge graph.
    /// </summary>
    /// <param name="request">Document import request.</param>
    /// <param name="createdBy">User who is importing the document.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Import result with created entity IDs.</returns>
    Task<DocumentImportResult> ImportDocumentAsync(
        ImportDocumentRequest request,
        string? createdBy = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Imports multiple documents into the knowledge graph.
    /// </summary>
    /// <param name="request">Bulk import request.</param>
    /// <param name="createdBy">User who is importing the documents.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Import result with created entity IDs.</returns>
    Task<DocumentImportResult> ImportDocumentsAsync(
        BulkImportDocumentsRequest request,
        string? createdBy = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Imports documents from a directory.
    /// </summary>
    /// <param name="request">Directory import request.</param>
    /// <param name="createdBy">User who is importing the documents.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Import result with created entity IDs.</returns>
    Task<DocumentImportResult> ImportFromDirectoryAsync(
        ImportDirectoryRequest request,
        string? createdBy = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Extracts metadata from a markdown document.
    /// </summary>
    /// <param name="content">Markdown content.</param>
    /// <returns>Extracted metadata.</returns>
    DocumentMetadata ExtractMetadata(string content);
}

/// <summary>
/// Result of a document import operation.
/// </summary>
public class DocumentImportResult
{
    /// <summary>
    /// Whether the import was successful.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Number of documents imported.
    /// </summary>
    public int DocumentsImported { get; init; }

    /// <summary>
    /// Number of tags created.
    /// </summary>
    public int TagsCreated { get; init; }

    /// <summary>
    /// Number of authors created.
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
    /// IDs of created tag entities.
    /// </summary>
    public List<Guid> TagIds { get; init; } = new();

    /// <summary>
    /// IDs of created author entities.
    /// </summary>
    public List<Guid> AuthorIds { get; init; } = new();

    /// <summary>
    /// IDs of created relationships.
    /// </summary>
    public List<Guid> RelationshipIds { get; init; } = new();

    /// <summary>
    /// Errors encountered during import.
    /// </summary>
    public List<string> Errors { get; init; } = new();
}

/// <summary>
/// Metadata extracted from a document.
/// </summary>
public record DocumentMetadata
{
    /// <summary>
    /// Title extracted from the document.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// Description or summary.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Tags/keywords found in the document.
    /// </summary>
    public List<string> Tags { get; init; } = new();

    /// <summary>
    /// Author if found in front matter.
    /// </summary>
    public string? Author { get; init; }

    /// <summary>
    /// Category if found in front matter.
    /// </summary>
    public string? Category { get; init; }

    /// <summary>
    /// Date if found in front matter.
    /// </summary>
    public DateTimeOffset? Date { get; init; }

    /// <summary>
    /// Word count of the content.
    /// </summary>
    public int WordCount { get; init; }

    /// <summary>
    /// Headers/sections found in the document.
    /// </summary>
    public List<string> Headers { get; init; } = new();

    /// <summary>
    /// Links found in the document.
    /// </summary>
    public List<string> Links { get; init; } = new();

    /// <summary>
    /// Additional front matter properties.
    /// </summary>
    public Dictionary<string, object> FrontMatter { get; init; } = new();
}
