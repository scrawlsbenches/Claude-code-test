using System.Text.RegularExpressions;
using HotSwap.KnowledgeGraph.Api.Models;
using HotSwap.KnowledgeGraph.Domain.Models;
using HotSwap.KnowledgeGraph.Infrastructure.Repositories;
using Markdig;

namespace HotSwap.KnowledgeGraph.Api.Services;

/// <summary>
/// Service for ingesting documents into the knowledge graph.
/// Handles parsing markdown, extracting metadata, and creating entities/relationships.
/// </summary>
public partial class DocumentIngestionService : IDocumentIngestionService
{
    private readonly IGraphRepository _repository;
    private readonly ILogger<DocumentIngestionService> _logger;
    private readonly MarkdownPipeline _markdownPipeline;

    // Cache for tags and authors to avoid duplicates
    private readonly Dictionary<string, Guid> _tagCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Guid> _authorCache = new(StringComparer.OrdinalIgnoreCase);

    public DocumentIngestionService(
        IGraphRepository repository,
        ILogger<DocumentIngestionService> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _markdownPipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build();
    }

    public async Task<DocumentImportResult> ImportDocumentAsync(
        ImportDocumentRequest request,
        string? createdBy = null,
        CancellationToken cancellationToken = default)
    {
        var result = new DocumentImportResult { Success = true };
        var documentIds = new List<Guid>();
        var tagIds = new List<Guid>();
        var authorIds = new List<Guid>();
        var relationshipIds = new List<Guid>();
        var errors = new List<string>();

        try
        {
            // Extract metadata from content
            var metadata = ExtractMetadata(request.Content);

            // Use provided values or fall back to extracted metadata
            var title = request.Title ?? metadata.Title ?? "Untitled";
            var tags = request.Tags.Count > 0 ? request.Tags : metadata.Tags;
            var author = request.Author ?? metadata.Author;
            var category = request.Category ?? metadata.Category;

            // Create document entity
            var now = DateTimeOffset.UtcNow;
            var documentEntity = new Entity
            {
                Id = Guid.NewGuid(),
                Type = "Document",
                Properties = new Dictionary<string, object>
                {
                    ["title"] = title,
                    ["content"] = request.Content,
                    ["category"] = category ?? "Uncategorized",
                    ["tags"] = tags,
                    ["sourcePath"] = request.SourcePath ?? "",
                    ["wordCount"] = metadata.WordCount,
                    ["headers"] = metadata.Headers
                },
                CreatedAt = now,
                UpdatedAt = now,
                CreatedBy = createdBy
            };

            // Add additional metadata
            foreach (var kvp in request.Metadata)
            {
                documentEntity.Properties[kvp.Key] = kvp.Value;
            }

            await _repository.CreateEntityAsync(documentEntity, cancellationToken);
            documentIds.Add(documentEntity.Id);

            _logger.LogInformation("Created document entity {DocumentId} with title '{Title}'",
                documentEntity.Id, title);

            // Create tag entities and relationships
            foreach (var tag in tags)
            {
                var tagId = await GetOrCreateTagAsync(tag, createdBy, cancellationToken);
                if (!tagIds.Contains(tagId))
                {
                    tagIds.Add(tagId);
                }

                // Create TAGGED_WITH relationship
                var relationship = new Relationship
                {
                    Id = Guid.NewGuid(),
                    Type = "TAGGED_WITH",
                    SourceEntityId = documentEntity.Id,
                    TargetEntityId = tagId,
                    Properties = new Dictionary<string, object>(),
                    Weight = 1.0,
                    IsDirected = true,
                    CreatedAt = now,
                    CreatedBy = createdBy
                };

                await _repository.CreateRelationshipAsync(relationship, cancellationToken);
                relationshipIds.Add(relationship.Id);
            }

            // Create author entity and relationship if author is provided
            if (!string.IsNullOrWhiteSpace(author))
            {
                var authorId = await GetOrCreateAuthorAsync(author, createdBy, cancellationToken);
                if (!authorIds.Contains(authorId))
                {
                    authorIds.Add(authorId);
                }

                // Create AUTHORED_BY relationship
                var relationship = new Relationship
                {
                    Id = Guid.NewGuid(),
                    Type = "AUTHORED_BY",
                    SourceEntityId = documentEntity.Id,
                    TargetEntityId = authorId,
                    Properties = new Dictionary<string, object>(),
                    Weight = 1.0,
                    IsDirected = true,
                    CreatedAt = now,
                    CreatedBy = createdBy
                };

                await _repository.CreateRelationshipAsync(relationship, cancellationToken);
                relationshipIds.Add(relationship.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import document '{Title}'", request.Title);
            errors.Add($"Failed to import '{request.Title}': {ex.Message}");
        }

        return new DocumentImportResult
        {
            Success = errors.Count == 0,
            DocumentsImported = documentIds.Count,
            TagsCreated = tagIds.Count,
            AuthorsCreated = authorIds.Count,
            RelationshipsCreated = relationshipIds.Count,
            DocumentIds = documentIds,
            TagIds = tagIds,
            AuthorIds = authorIds,
            RelationshipIds = relationshipIds,
            Errors = errors
        };
    }

    public async Task<DocumentImportResult> ImportDocumentsAsync(
        BulkImportDocumentsRequest request,
        string? createdBy = null,
        CancellationToken cancellationToken = default)
    {
        var documentIds = new List<Guid>();
        var tagIds = new List<Guid>();
        var authorIds = new List<Guid>();
        var relationshipIds = new List<Guid>();
        var errors = new List<string>();

        _logger.LogInformation("Starting bulk import of {Count} documents", request.Documents.Count);

        foreach (var doc in request.Documents)
        {
            var result = await ImportDocumentAsync(doc, createdBy, cancellationToken);

            documentIds.AddRange(result.DocumentIds);
            tagIds.AddRange(result.TagIds.Except(tagIds));
            authorIds.AddRange(result.AuthorIds.Except(authorIds));
            relationshipIds.AddRange(result.RelationshipIds);
            errors.AddRange(result.Errors);
        }

        // Create relationships between documents that share tags
        if (request.CreateTagRelationships && documentIds.Count > 1)
        {
            var tagRelationships = await CreateTagBasedRelationshipsAsync(
                documentIds, createdBy, cancellationToken);
            relationshipIds.AddRange(tagRelationships);
        }

        _logger.LogInformation(
            "Bulk import completed: {Docs} documents, {Tags} tags, {Authors} authors, {Rels} relationships",
            documentIds.Count, tagIds.Count, authorIds.Count, relationshipIds.Count);

        return new DocumentImportResult
        {
            Success = errors.Count == 0,
            DocumentsImported = documentIds.Count,
            TagsCreated = tagIds.Count,
            AuthorsCreated = authorIds.Count,
            RelationshipsCreated = relationshipIds.Count,
            DocumentIds = documentIds,
            TagIds = tagIds,
            AuthorIds = authorIds,
            RelationshipIds = relationshipIds,
            Errors = errors
        };
    }

    public async Task<DocumentImportResult> ImportFromDirectoryAsync(
        ImportDirectoryRequest request,
        string? createdBy = null,
        CancellationToken cancellationToken = default)
    {
        var documents = new List<ImportDocumentRequest>();
        var errors = new List<string>();

        if (!Directory.Exists(request.DirectoryPath))
        {
            return new DocumentImportResult
            {
                Success = false,
                Errors = new List<string> { $"Directory not found: {request.DirectoryPath}" }
            };
        }

        _logger.LogInformation("Scanning directory {Path} for {Pattern} files",
            request.DirectoryPath, request.FilePattern);

        var searchOption = request.Recursive
            ? SearchOption.AllDirectories
            : SearchOption.TopDirectoryOnly;

        var files = Directory.GetFiles(request.DirectoryPath, request.FilePattern, searchOption);

        _logger.LogInformation("Found {Count} files to import", files.Length);

        foreach (var filePath in files)
        {
            try
            {
                var content = await File.ReadAllTextAsync(filePath, cancellationToken);
                var fileName = Path.GetFileNameWithoutExtension(filePath);
                var relativePath = Path.GetRelativePath(request.DirectoryPath, filePath);
                var metadata = ExtractMetadata(content);

                documents.Add(new ImportDocumentRequest
                {
                    Title = metadata.Title ?? fileName,
                    Content = content,
                    Category = request.Category ?? metadata.Category,
                    Tags = metadata.Tags,
                    Author = metadata.Author,
                    SourcePath = relativePath,
                    Metadata = new Dictionary<string, object>
                    {
                        ["fileName"] = Path.GetFileName(filePath),
                        ["importedAt"] = DateTimeOffset.UtcNow
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read file {FilePath}", filePath);
                errors.Add($"Failed to read '{filePath}': {ex.Message}");
            }
        }

        var result = await ImportDocumentsAsync(
            new BulkImportDocumentsRequest
            {
                Documents = documents,
                CreateTagRelationships = request.CreateTagRelationships
            },
            createdBy,
            cancellationToken);

        // Combine errors
        var allErrors = new List<string>(errors);
        allErrors.AddRange(result.Errors);

        return new DocumentImportResult
        {
            Success = allErrors.Count == 0,
            DocumentsImported = result.DocumentsImported,
            TagsCreated = result.TagsCreated,
            AuthorsCreated = result.AuthorsCreated,
            RelationshipsCreated = result.RelationshipsCreated,
            DocumentIds = result.DocumentIds,
            TagIds = result.TagIds,
            AuthorIds = result.AuthorIds,
            RelationshipIds = result.RelationshipIds,
            Errors = allErrors
        };
    }

    public DocumentMetadata ExtractMetadata(string content)
    {
        var metadata = new DocumentMetadata
        {
            Tags = new List<string>(),
            Headers = new List<string>(),
            Links = new List<string>(),
            FrontMatter = new Dictionary<string, object>()
        };

        if (string.IsNullOrWhiteSpace(content))
        {
            return metadata;
        }

        // Try to extract YAML front matter
        var frontMatterMatch = FrontMatterRegex().Match(content);
        string contentWithoutFrontMatter = content;

        if (frontMatterMatch.Success)
        {
            contentWithoutFrontMatter = content[frontMatterMatch.Length..].TrimStart();
            var frontMatterContent = frontMatterMatch.Groups[1].Value;
            metadata = ParseFrontMatter(frontMatterContent, metadata);
        }

        // Extract title from first H1 header if not in front matter
        if (string.IsNullOrEmpty(metadata.Title))
        {
            var titleMatch = TitleRegex().Match(contentWithoutFrontMatter);
            if (titleMatch.Success)
            {
                metadata = metadata with { Title = titleMatch.Groups[1].Value.Trim() };
            }
        }

        // Extract all headers
        var headerMatches = HeaderRegex().Matches(contentWithoutFrontMatter);
        var headers = new List<string>();
        foreach (Match match in headerMatches)
        {
            headers.Add(match.Groups[2].Value.Trim());
        }
        metadata = metadata with { Headers = headers };

        // Extract links
        var linkMatches = LinkRegex().Matches(contentWithoutFrontMatter);
        var links = new List<string>();
        foreach (Match match in linkMatches)
        {
            links.Add(match.Groups[2].Value);
        }
        metadata = metadata with { Links = links };

        // Calculate word count (simple approximation)
        var plainText = Markdown.ToPlainText(contentWithoutFrontMatter, _markdownPipeline);
        var wordCount = plainText.Split(new[] { ' ', '\n', '\r', '\t' },
            StringSplitOptions.RemoveEmptyEntries).Length;
        metadata = metadata with { WordCount = wordCount };

        // Extract tags from content if not in front matter
        if (metadata.Tags.Count == 0)
        {
            var tagMatches = TagRegex().Matches(content);
            var tags = new List<string>();
            foreach (Match match in tagMatches)
            {
                tags.Add(match.Groups[1].Value.ToLowerInvariant());
            }
            metadata = metadata with { Tags = tags.Distinct().ToList() };
        }

        return metadata;
    }

    private static DocumentMetadata ParseFrontMatter(string frontMatter, DocumentMetadata metadata)
    {
        // Simple YAML-like front matter parsing
        var lines = frontMatter.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var frontMatterDict = new Dictionary<string, object>();
        var tags = new List<string>();
        string? title = null;
        string? author = null;
        string? category = null;
        string? description = null;
        DateTimeOffset? date = null;

        foreach (var line in lines)
        {
            var colonIndex = line.IndexOf(':');
            if (colonIndex <= 0) continue;

            var key = line[..colonIndex].Trim().ToLowerInvariant();
            var value = line[(colonIndex + 1)..].Trim();

            // Remove quotes if present
            if (value.StartsWith('"') && value.EndsWith('"'))
            {
                value = value[1..^1];
            }

            switch (key)
            {
                case "title":
                    title = value;
                    break;
                case "author":
                    author = value;
                    break;
                case "category":
                case "categories":
                    category = value;
                    break;
                case "description":
                case "summary":
                    description = value;
                    break;
                case "date":
                case "created":
                case "published":
                    if (DateTimeOffset.TryParse(value, out var parsedDate))
                    {
                        date = parsedDate;
                    }
                    break;
                case "tags":
                case "keywords":
                    // Handle both array format [tag1, tag2] and comma-separated
                    var tagValue = value.Trim('[', ']');
                    var parsedTags = tagValue.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(t => t.Trim().Trim('"', '\'').ToLowerInvariant())
                        .Where(t => !string.IsNullOrEmpty(t));
                    tags.AddRange(parsedTags);
                    break;
                default:
                    frontMatterDict[key] = value;
                    break;
            }
        }

        // Return updated metadata with extracted values
        return metadata with
        {
            Title = title ?? metadata.Title,
            Author = author ?? metadata.Author,
            Category = category ?? metadata.Category,
            Description = description ?? metadata.Description,
            Date = date ?? metadata.Date,
            Tags = tags.Count > 0 ? tags : metadata.Tags,
            FrontMatter = frontMatterDict
        };
    }

    private async Task<Guid> GetOrCreateTagAsync(
        string tagName,
        string? createdBy,
        CancellationToken cancellationToken)
    {
        var normalizedTag = tagName.Trim().ToLowerInvariant();

        if (_tagCache.TryGetValue(normalizedTag, out var cachedId))
        {
            return cachedId;
        }

        // Check if tag already exists in database
        var existingTags = await _repository.GetEntitiesByTypeAsync("Tag", 0, 1000, cancellationToken);
        var existingTag = existingTags.FirstOrDefault(t =>
            t.Properties.TryGetValue("name", out var name) &&
            name?.ToString()?.Equals(normalizedTag, StringComparison.OrdinalIgnoreCase) == true);

        if (existingTag != null)
        {
            _tagCache[normalizedTag] = existingTag.Id;
            return existingTag.Id;
        }

        // Create new tag entity
        var now = DateTimeOffset.UtcNow;
        var tagEntity = new Entity
        {
            Id = Guid.NewGuid(),
            Type = "Tag",
            Properties = new Dictionary<string, object>
            {
                ["name"] = normalizedTag,
                ["displayName"] = tagName.Trim()
            },
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = createdBy
        };

        await _repository.CreateEntityAsync(tagEntity, cancellationToken);
        _tagCache[normalizedTag] = tagEntity.Id;

        _logger.LogDebug("Created tag entity {TagId} for '{TagName}'", tagEntity.Id, tagName);

        return tagEntity.Id;
    }

    private async Task<Guid> GetOrCreateAuthorAsync(
        string authorName,
        string? createdBy,
        CancellationToken cancellationToken)
    {
        var normalizedAuthor = authorName.Trim().ToLowerInvariant();

        if (_authorCache.TryGetValue(normalizedAuthor, out var cachedId))
        {
            return cachedId;
        }

        // Check if author already exists in database
        var existingAuthors = await _repository.GetEntitiesByTypeAsync("Author", 0, 1000, cancellationToken);
        var existingAuthor = existingAuthors.FirstOrDefault(a =>
            a.Properties.TryGetValue("name", out var name) &&
            name?.ToString()?.Equals(normalizedAuthor, StringComparison.OrdinalIgnoreCase) == true);

        if (existingAuthor != null)
        {
            _authorCache[normalizedAuthor] = existingAuthor.Id;
            return existingAuthor.Id;
        }

        // Create new author entity
        var now = DateTimeOffset.UtcNow;
        var authorEntity = new Entity
        {
            Id = Guid.NewGuid(),
            Type = "Author",
            Properties = new Dictionary<string, object>
            {
                ["name"] = normalizedAuthor,
                ["displayName"] = authorName.Trim()
            },
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = createdBy
        };

        await _repository.CreateEntityAsync(authorEntity, cancellationToken);
        _authorCache[normalizedAuthor] = authorEntity.Id;

        _logger.LogDebug("Created author entity {AuthorId} for '{AuthorName}'", authorEntity.Id, authorName);

        return authorEntity.Id;
    }

    private async Task<List<Guid>> CreateTagBasedRelationshipsAsync(
        List<Guid> documentIds,
        string? createdBy,
        CancellationToken cancellationToken)
    {
        var relationshipIds = new List<Guid>();
        var documentTags = new Dictionary<Guid, HashSet<string>>();

        // Collect tags for each document
        foreach (var docId in documentIds)
        {
            var relationships = await _repository.GetRelationshipsByEntityAsync(
                docId, includeOutgoing: true, includeIncoming: false, cancellationToken);

            var tagRelationships = relationships.Where(r => r.Type == "TAGGED_WITH");
            var tagSet = new HashSet<string>();

            foreach (var rel in tagRelationships)
            {
                var tagEntity = await _repository.GetEntityByIdAsync(rel.TargetEntityId, cancellationToken);
                if (tagEntity?.Properties.TryGetValue("name", out var tagName) == true)
                {
                    tagSet.Add(tagName.ToString() ?? "");
                }
            }

            documentTags[docId] = tagSet;
        }

        // Create RELATED_TO relationships for documents that share tags
        var now = DateTimeOffset.UtcNow;
        var processedPairs = new HashSet<(Guid, Guid)>();

        foreach (var doc1 in documentIds)
        {
            foreach (var doc2 in documentIds)
            {
                if (doc1 == doc2) continue;

                // Avoid duplicate relationships
                var pair = doc1.CompareTo(doc2) < 0 ? (doc1, doc2) : (doc2, doc1);
                if (processedPairs.Contains(pair)) continue;
                processedPairs.Add(pair);

                // Calculate shared tags
                var sharedTags = documentTags[doc1].Intersect(documentTags[doc2]).ToList();
                if (sharedTags.Count == 0) continue;

                // Create relationship with weight based on shared tags
                var relationship = new Relationship
                {
                    Id = Guid.NewGuid(),
                    Type = "RELATED_TO",
                    SourceEntityId = doc1,
                    TargetEntityId = doc2,
                    Properties = new Dictionary<string, object>
                    {
                        ["sharedTags"] = sharedTags,
                        ["sharedTagCount"] = sharedTags.Count
                    },
                    Weight = sharedTags.Count, // Higher weight = more shared tags
                    IsDirected = false, // Bidirectional relationship
                    CreatedAt = now,
                    CreatedBy = createdBy
                };

                await _repository.CreateRelationshipAsync(relationship, cancellationToken);
                relationshipIds.Add(relationship.Id);

                _logger.LogDebug(
                    "Created RELATED_TO relationship between {Doc1} and {Doc2} ({SharedTags} shared tags)",
                    doc1, doc2, sharedTags.Count);
            }
        }

        return relationshipIds;
    }

    [GeneratedRegex(@"^---\s*\n([\s\S]*?)\n---\s*\n", RegexOptions.Compiled)]
    private static partial Regex FrontMatterRegex();

    [GeneratedRegex(@"^#\s+(.+)$", RegexOptions.Multiline | RegexOptions.Compiled)]
    private static partial Regex TitleRegex();

    [GeneratedRegex(@"^(#{1,6})\s+(.+)$", RegexOptions.Multiline | RegexOptions.Compiled)]
    private static partial Regex HeaderRegex();

    [GeneratedRegex(@"\[([^\]]+)\]\(([^)]+)\)", RegexOptions.Compiled)]
    private static partial Regex LinkRegex();

    [GeneratedRegex(@"#(\w+)", RegexOptions.Compiled)]
    private static partial Regex TagRegex();
}
