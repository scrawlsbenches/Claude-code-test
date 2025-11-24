using FluentAssertions;
using HotSwap.KnowledgeGraph.Api.Controllers;
using HotSwap.KnowledgeGraph.Api.Models;
using HotSwap.KnowledgeGraph.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace HotSwap.KnowledgeGraph.Tests.Api;

public class DocumentsControllerTests
{
    private readonly Mock<IDocumentIngestionService> _ingestionServiceMock;
    private readonly Mock<ILogger<DocumentsController>> _loggerMock;
    private readonly DocumentsController _controller;

    public DocumentsControllerTests()
    {
        _ingestionServiceMock = new Mock<IDocumentIngestionService>();
        _loggerMock = new Mock<ILogger<DocumentsController>>();
        _controller = new DocumentsController(_ingestionServiceMock.Object, _loggerMock.Object);
    }

    #region ImportDocument Tests

    [Fact]
    public async Task ImportDocument_ValidRequest_ReturnsCreatedResult()
    {
        // Arrange
        var request = new ImportDocumentRequest
        {
            Title = "Test Document",
            Content = "# Test\n\nContent here."
        };

        var importResult = new DocumentImportResult
        {
            Success = true,
            DocumentsImported = 1,
            TagsCreated = 0,
            AuthorsCreated = 0,
            RelationshipsCreated = 0,
            DocumentIds = new List<Guid> { Guid.NewGuid() }
        };

        _ingestionServiceMock
            .Setup(s => s.ImportDocumentAsync(request, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(importResult);

        // Act
        var result = await _controller.ImportDocument(request, CancellationToken.None);

        // Assert
        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(201);
        var response = objectResult.Value.Should().BeOfType<DocumentImportResponse>().Subject;
        response.DocumentsImported.Should().Be(1);
    }

    [Fact]
    public async Task ImportDocument_EmptyTitle_ReturnsBadRequest()
    {
        // Arrange
        var request = new ImportDocumentRequest
        {
            Title = "",
            Content = "Some content"
        };

        // Act
        var result = await _controller.ImportDocument(request, CancellationToken.None);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var error = badRequestResult.Value.Should().BeOfType<ErrorResponse>().Subject;
        error.Message.Should().Contain("title");
    }

    [Fact]
    public async Task ImportDocument_EmptyContent_ReturnsBadRequest()
    {
        // Arrange
        var request = new ImportDocumentRequest
        {
            Title = "Test",
            Content = ""
        };

        // Act
        var result = await _controller.ImportDocument(request, CancellationToken.None);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var error = badRequestResult.Value.Should().BeOfType<ErrorResponse>().Subject;
        error.Message.Should().Contain("content");
    }

    [Fact]
    public async Task ImportDocument_WithErrors_ReturnsMultiStatus()
    {
        // Arrange
        var request = new ImportDocumentRequest
        {
            Title = "Test Document",
            Content = "Content"
        };

        var importResult = new DocumentImportResult
        {
            Success = false,
            DocumentsImported = 0,
            Errors = new List<string> { "Some error occurred" }
        };

        _ingestionServiceMock
            .Setup(s => s.ImportDocumentAsync(request, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(importResult);

        // Act
        var result = await _controller.ImportDocument(request, CancellationToken.None);

        // Assert
        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(207); // Multi-Status
    }

    #endregion

    #region ImportDocumentsBulk Tests

    [Fact]
    public async Task ImportDocumentsBulk_ValidRequest_ReturnsCreatedResult()
    {
        // Arrange
        var request = new BulkImportDocumentsRequest
        {
            Documents = new List<ImportDocumentRequest>
            {
                new() { Title = "Doc 1", Content = "Content 1" },
                new() { Title = "Doc 2", Content = "Content 2" }
            }
        };

        var importResult = new DocumentImportResult
        {
            Success = true,
            DocumentsImported = 2,
            DocumentIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() }
        };

        _ingestionServiceMock
            .Setup(s => s.ImportDocumentsAsync(request, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(importResult);

        // Act
        var result = await _controller.ImportDocumentsBulk(request, CancellationToken.None);

        // Assert
        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(201);
        var response = objectResult.Value.Should().BeOfType<DocumentImportResponse>().Subject;
        response.DocumentsImported.Should().Be(2);
    }

    [Fact]
    public async Task ImportDocumentsBulk_EmptyList_ReturnsBadRequest()
    {
        // Arrange
        var request = new BulkImportDocumentsRequest
        {
            Documents = new List<ImportDocumentRequest>()
        };

        // Act
        var result = await _controller.ImportDocumentsBulk(request, CancellationToken.None);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var error = badRequestResult.Value.Should().BeOfType<ErrorResponse>().Subject;
        error.Message.Should().Contain("one document");
    }

    #endregion

    #region ImportFromDirectory Tests

    [Fact]
    public async Task ImportFromDirectory_EmptyPath_ReturnsBadRequest()
    {
        // Arrange
        var request = new ImportDirectoryRequest
        {
            DirectoryPath = ""
        };

        // Act
        var result = await _controller.ImportFromDirectory(request, CancellationToken.None);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var error = badRequestResult.Value.Should().BeOfType<ErrorResponse>().Subject;
        error.Message.Should().Contain("path");
    }

    [Fact]
    public async Task ImportFromDirectory_NonExistingDirectory_ReturnsNotFound()
    {
        // Arrange
        var request = new ImportDirectoryRequest
        {
            DirectoryPath = "/non/existing/path/that/does/not/exist"
        };

        // Act
        var result = await _controller.ImportFromDirectory(request, CancellationToken.None);

        // Assert
        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        var error = notFoundResult.Value.Should().BeOfType<ErrorResponse>().Subject;
        error.Message.Should().Contain("not found");
    }

    #endregion

    #region PreviewDocument Tests

    [Fact]
    public void PreviewDocument_ValidContent_ReturnsMetadata()
    {
        // Arrange
        var content = @"---
title: Test Document
author: John Doe
tags: [api, design]
---

# Main Content

This is the body.";

        var expectedMetadata = new DocumentMetadata
        {
            Title = "Test Document",
            Author = "John Doe",
            Tags = new List<string> { "api", "design" }
        };

        _ingestionServiceMock
            .Setup(s => s.ExtractMetadata(content))
            .Returns(expectedMetadata);

        // Act
        var result = _controller.PreviewDocument(content, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var metadata = okResult.Value.Should().BeOfType<DocumentMetadata>().Subject;
        metadata.Title.Should().Be("Test Document");
        metadata.Author.Should().Be("John Doe");
    }

    [Fact]
    public void PreviewDocument_EmptyContent_ReturnsBadRequest()
    {
        // Act
        var result = _controller.PreviewDocument("", CancellationToken.None);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var error = badRequestResult.Value.Should().BeOfType<ErrorResponse>().Subject;
        error.Message.Should().Contain("Content");
    }

    [Fact]
    public void PreviewDocument_WhitespaceContent_ReturnsBadRequest()
    {
        // Act
        var result = _controller.PreviewDocument("   \n\t  ", CancellationToken.None);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    #endregion
}
