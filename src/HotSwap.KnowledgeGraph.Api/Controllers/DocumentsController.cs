using HotSwap.KnowledgeGraph.Api.Models;
using HotSwap.KnowledgeGraph.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace HotSwap.KnowledgeGraph.Api.Controllers;

/// <summary>
/// API endpoints for importing documents into the knowledge graph.
/// </summary>
[ApiController]
[Route("api/v1/graph/[controller]")]
[Produces("application/json")]
public class DocumentsController : ControllerBase
{
    private readonly IDocumentIngestionService _ingestionService;
    private readonly ILogger<DocumentsController> _logger;

    public DocumentsController(
        IDocumentIngestionService ingestionService,
        ILogger<DocumentsController> logger)
    {
        _ingestionService = ingestionService;
        _logger = logger;
    }

    /// <summary>
    /// Imports a single document into the knowledge graph.
    /// Creates Document, Tag, and Author entities with relationships.
    /// </summary>
    /// <param name="request">Document import request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Import result with created entity IDs.</returns>
    [HttpPost("import")]
    [ProducesResponseType(typeof(DocumentImportResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ImportDocument(
        [FromBody] ImportDocumentRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Importing document '{Title}'", request.Title);

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return BadRequest(new ErrorResponse { Message = "Document title is required" });
        }

        if (string.IsNullOrWhiteSpace(request.Content))
        {
            return BadRequest(new ErrorResponse { Message = "Document content is required" });
        }

        var result = await _ingestionService.ImportDocumentAsync(request, null, cancellationToken);

        var response = new DocumentImportResponse
        {
            DocumentsImported = result.DocumentsImported,
            TagsCreated = result.TagsCreated,
            AuthorsCreated = result.AuthorsCreated,
            RelationshipsCreated = result.RelationshipsCreated,
            DocumentIds = result.DocumentIds,
            Errors = result.Errors
        };

        if (!result.Success)
        {
            return StatusCode(StatusCodes.Status207MultiStatus, response);
        }

        return StatusCode(StatusCodes.Status201Created, response);
    }

    /// <summary>
    /// Imports multiple documents into the knowledge graph.
    /// Creates Document, Tag, and Author entities with relationships.
    /// Optionally creates RELATED_TO relationships between documents that share tags.
    /// </summary>
    /// <param name="request">Bulk import request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Import result with created entity IDs.</returns>
    [HttpPost("import/bulk")]
    [ProducesResponseType(typeof(DocumentImportResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ImportDocumentsBulk(
        [FromBody] BulkImportDocumentsRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Bulk importing {Count} documents", request.Documents.Count);

        if (request.Documents.Count == 0)
        {
            return BadRequest(new ErrorResponse { Message = "At least one document is required" });
        }

        var result = await _ingestionService.ImportDocumentsAsync(request, null, cancellationToken);

        var response = new DocumentImportResponse
        {
            DocumentsImported = result.DocumentsImported,
            TagsCreated = result.TagsCreated,
            AuthorsCreated = result.AuthorsCreated,
            RelationshipsCreated = result.RelationshipsCreated,
            DocumentIds = result.DocumentIds,
            Errors = result.Errors
        };

        if (!result.Success)
        {
            return StatusCode(StatusCodes.Status207MultiStatus, response);
        }

        return StatusCode(StatusCodes.Status201Created, response);
    }

    /// <summary>
    /// Imports all markdown documents from a directory into the knowledge graph.
    /// Recursively scans the directory for files matching the specified pattern.
    /// </summary>
    /// <param name="request">Directory import request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Import result with created entity IDs.</returns>
    [HttpPost("import/directory")]
    [ProducesResponseType(typeof(DocumentImportResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ImportFromDirectory(
        [FromBody] ImportDirectoryRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Importing documents from directory '{Path}' with pattern '{Pattern}'",
            request.DirectoryPath, request.FilePattern);

        if (string.IsNullOrWhiteSpace(request.DirectoryPath))
        {
            return BadRequest(new ErrorResponse { Message = "Directory path is required" });
        }

        if (!Directory.Exists(request.DirectoryPath))
        {
            return NotFound(new ErrorResponse
            {
                Message = $"Directory not found: {request.DirectoryPath}"
            });
        }

        var result = await _ingestionService.ImportFromDirectoryAsync(request, null, cancellationToken);

        var response = new DocumentImportResponse
        {
            DocumentsImported = result.DocumentsImported,
            TagsCreated = result.TagsCreated,
            AuthorsCreated = result.AuthorsCreated,
            RelationshipsCreated = result.RelationshipsCreated,
            DocumentIds = result.DocumentIds,
            Errors = result.Errors
        };

        if (!result.Success)
        {
            return StatusCode(StatusCodes.Status207MultiStatus, response);
        }

        return StatusCode(StatusCodes.Status201Created, response);
    }

    /// <summary>
    /// Extracts metadata from a markdown document without importing it.
    /// Useful for previewing what will be extracted from a document.
    /// </summary>
    /// <param name="content">Markdown content to analyze.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Extracted metadata.</returns>
    [HttpPost("preview")]
    [ProducesResponseType(typeof(DocumentMetadata), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public IActionResult PreviewDocument(
        [FromBody] string content,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return BadRequest(new ErrorResponse { Message = "Content is required" });
        }

        var metadata = _ingestionService.ExtractMetadata(content);
        return Ok(metadata);
    }
}
