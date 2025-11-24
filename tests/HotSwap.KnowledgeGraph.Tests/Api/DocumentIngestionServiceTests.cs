using FluentAssertions;
using HotSwap.KnowledgeGraph.Api.Models;
using HotSwap.KnowledgeGraph.Api.Services;
using HotSwap.KnowledgeGraph.Domain.Models;
using HotSwap.KnowledgeGraph.Infrastructure.Data;
using HotSwap.KnowledgeGraph.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace HotSwap.KnowledgeGraph.Tests.Api;

/// <summary>
/// Tests for the DocumentIngestionService.
/// </summary>
public class DocumentIngestionServiceTests : IDisposable
{
    private readonly GraphDbContext _dbContext;
    private readonly IGraphRepository _repository;
    private readonly DocumentIngestionService _service;
    private readonly Mock<ILogger<DocumentIngestionService>> _loggerMock;

    public DocumentIngestionServiceTests()
    {
        var options = new DbContextOptionsBuilder<GraphDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new GraphDbContext(options);
        _repository = new PostgresGraphRepository(_dbContext);
        _loggerMock = new Mock<ILogger<DocumentIngestionService>>();
        _service = new DocumentIngestionService(_repository, _loggerMock.Object);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    #region ExtractMetadata Tests

    [Fact]
    public void ExtractMetadata_EmptyContent_ReturnsEmptyMetadata()
    {
        // Act
        var result = _service.ExtractMetadata("");

        // Assert
        result.Title.Should().BeNull();
        result.Tags.Should().BeEmpty();
        result.WordCount.Should().Be(0);
    }

    [Fact]
    public void ExtractMetadata_WithTitle_ExtractsTitle()
    {
        // Arrange
        var content = "# My Document Title\n\nSome content here.";

        // Act
        var result = _service.ExtractMetadata(content);

        // Assert
        result.Title.Should().Be("My Document Title");
    }

    [Fact]
    public void ExtractMetadata_WithFrontMatter_ExtractsTitleAndTags()
    {
        // Arrange
        var content = @"---
title: Front Matter Title
author: John Doe
tags: [api, design, rest]
category: Engineering
---

# Actual Header

This is the content.";

        // Act
        var result = _service.ExtractMetadata(content);

        // Assert
        result.Title.Should().Be("Front Matter Title");
        result.Author.Should().Be("John Doe");
        result.Category.Should().Be("Engineering");
        result.Tags.Should().Contain("api");
        result.Tags.Should().Contain("design");
        result.Tags.Should().Contain("rest");
    }

    [Fact]
    public void ExtractMetadata_WithHeaders_ExtractsAllHeaders()
    {
        // Arrange
        var content = @"# Main Title
## Section One
### Subsection A
## Section Two";

        // Act
        var result = _service.ExtractMetadata(content);

        // Assert
        result.Headers.Should().HaveCount(4);
        result.Headers.Should().Contain("Main Title");
        result.Headers.Should().Contain("Section One");
        result.Headers.Should().Contain("Subsection A");
        result.Headers.Should().Contain("Section Two");
    }

    [Fact]
    public void ExtractMetadata_WithLinks_ExtractsAllLinks()
    {
        // Arrange
        var content = @"# Title
Check out [Google](https://google.com) and [GitHub](https://github.com).";

        // Act
        var result = _service.ExtractMetadata(content);

        // Assert
        result.Links.Should().HaveCount(2);
        result.Links.Should().Contain("https://google.com");
        result.Links.Should().Contain("https://github.com");
    }

    [Fact]
    public void ExtractMetadata_CalculatesWordCount()
    {
        // Arrange
        var content = "# Title\n\nOne two three four five.";

        // Act
        var result = _service.ExtractMetadata(content);

        // Assert
        result.WordCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void ExtractMetadata_WithHashtags_ExtractsTags()
    {
        // Arrange
        var content = @"# Article
This article covers #api and #rest design patterns.";

        // Act
        var result = _service.ExtractMetadata(content);

        // Assert
        result.Tags.Should().Contain("api");
        result.Tags.Should().Contain("rest");
    }

    #endregion

    #region ImportDocument Tests

    [Fact]
    public async Task ImportDocumentAsync_ValidRequest_CreatesDocumentEntity()
    {
        // Arrange
        var request = new ImportDocumentRequest
        {
            Title = "Test Document",
            Content = "# Test Document\n\nThis is a test document.",
            Category = "Testing",
            Tags = new List<string> { "test", "example" }
        };

        // Act
        var result = await _service.ImportDocumentAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        result.DocumentsImported.Should().Be(1);
        result.DocumentIds.Should().HaveCount(1);

        // Verify entity was created
        var entity = await _repository.GetEntityByIdAsync(result.DocumentIds[0]);
        entity.Should().NotBeNull();
        entity!.Type.Should().Be("Document");
        entity.Properties["title"].ToString().Should().Be("Test Document");
    }

    [Fact]
    public async Task ImportDocumentAsync_WithTags_CreatesTagEntitiesAndRelationships()
    {
        // Arrange
        var request = new ImportDocumentRequest
        {
            Title = "Tagged Document",
            Content = "Content here",
            Tags = new List<string> { "api", "design" }
        };

        // Act
        var result = await _service.ImportDocumentAsync(request);

        // Assert
        result.Errors.Should().BeEmpty($"Expected no errors but got: {string.Join(", ", result.Errors)}");
        result.Success.Should().BeTrue();
        result.TagsCreated.Should().Be(2);
        result.RelationshipsCreated.Should().Be(2);

        // Verify tag entities were created
        var tags = await _repository.GetEntitiesByTypeAsync("Tag");
        tags.Should().HaveCount(2);
    }

    [Fact]
    public async Task ImportDocumentAsync_WithAuthor_CreatesAuthorEntityAndRelationship()
    {
        // Arrange
        var request = new ImportDocumentRequest
        {
            Title = "Authored Document",
            Content = "Content here",
            Author = "Jane Doe"
        };

        // Act
        var result = await _service.ImportDocumentAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        result.AuthorsCreated.Should().Be(1);
        result.RelationshipsCreated.Should().Be(1);

        // Verify author entity was created
        var authors = await _repository.GetEntitiesByTypeAsync("Author");
        authors.Should().HaveCount(1);
        authors[0].Properties["name"].ToString().Should().Be("jane doe");
    }

    [Fact]
    public async Task ImportDocumentAsync_DuplicateTags_ReusesSameTagEntity()
    {
        // Arrange
        var request1 = new ImportDocumentRequest
        {
            Title = "Document 1",
            Content = "Content",
            Tags = new List<string> { "api" }
        };

        var request2 = new ImportDocumentRequest
        {
            Title = "Document 2",
            Content = "Content",
            Tags = new List<string> { "api" }
        };

        // Act
        await _service.ImportDocumentAsync(request1);
        var result = await _service.ImportDocumentAsync(request2);

        // Assert
        result.Success.Should().BeTrue();

        // Should still only have 1 tag entity
        var tags = await _repository.GetEntitiesByTypeAsync("Tag");
        tags.Should().HaveCount(1);
    }

    #endregion

    #region BulkImport Tests

    [Fact]
    public async Task ImportDocumentsAsync_MultipleDocuments_CreatesAllEntities()
    {
        // Arrange
        var request = new BulkImportDocumentsRequest
        {
            Documents = new List<ImportDocumentRequest>
            {
                new() { Title = "Doc 1", Content = "Content 1", Tags = new List<string> { "tag1" } },
                new() { Title = "Doc 2", Content = "Content 2", Tags = new List<string> { "tag1", "tag2" } },
                new() { Title = "Doc 3", Content = "Content 3", Tags = new List<string> { "tag2" } }
            },
            CreateTagRelationships = false
        };

        // Act
        var result = await _service.ImportDocumentsAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        result.DocumentsImported.Should().Be(3);
        result.TagsCreated.Should().Be(2); // tag1 and tag2

        var documents = await _repository.GetEntitiesByTypeAsync("Document");
        documents.Should().HaveCount(3);
    }

    [Fact]
    public async Task ImportDocumentsAsync_WithTagRelationships_CreatesRelatedToRelationships()
    {
        // Arrange
        var request = new BulkImportDocumentsRequest
        {
            Documents = new List<ImportDocumentRequest>
            {
                new() { Title = "Doc 1", Content = "Content 1", Tags = new List<string> { "shared" } },
                new() { Title = "Doc 2", Content = "Content 2", Tags = new List<string> { "shared" } }
            },
            CreateTagRelationships = true
        };

        // Act
        var result = await _service.ImportDocumentsAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        result.DocumentsImported.Should().Be(2);

        // Should have TAGGED_WITH relationships + RELATED_TO relationship
        // 2 TAGGED_WITH + 1 RELATED_TO = 3 total
        result.RelationshipsCreated.Should().BeGreaterThanOrEqualTo(3);
    }

    #endregion

    #region ExtractMetadata Edge Cases

    [Fact]
    public void ExtractMetadata_CommaSeparatedTags_ParsesCorrectly()
    {
        // Arrange
        var content = @"---
tags: api, rest, design
---

Content";

        // Act
        var result = _service.ExtractMetadata(content);

        // Assert
        result.Tags.Should().HaveCount(3);
    }

    [Fact]
    public void ExtractMetadata_MixedCaseTags_NormalizesToLowercase()
    {
        // Arrange
        var content = @"---
tags: [API, REST, Design]
---

Content";

        // Act
        var result = _service.ExtractMetadata(content);

        // Assert
        result.Tags.Should().OnlyContain(t => t == t.ToLowerInvariant());
    }

    [Fact]
    public void ExtractMetadata_DateInFrontMatter_ParsesDate()
    {
        // Arrange
        var content = @"---
date: 2024-01-15
---

Content";

        // Act
        var result = _service.ExtractMetadata(content);

        // Assert
        result.Date.Should().NotBeNull();
        result.Date!.Value.Year.Should().Be(2024);
        result.Date.Value.Month.Should().Be(1);
        result.Date.Value.Day.Should().Be(15);
    }

    #endregion
}
