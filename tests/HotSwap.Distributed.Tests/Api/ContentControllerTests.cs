using FluentAssertions;
using HotSwap.Distributed.Api.Controllers;
using HotSwap.Distributed.Api.Models;
using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace HotSwap.Distributed.Tests.Api;

public class ContentControllerTests
{
    private readonly Mock<IContentService> _mockContentService;
    private readonly Mock<IPageRepository> _mockPageRepository;
    private readonly Mock<IMediaRepository> _mockMediaRepository;
    private readonly Mock<ITenantContextService> _mockTenantContext;
    private readonly ContentController _controller;
    private readonly Guid _testTenantId = Guid.NewGuid();
    private readonly Guid _testWebsiteId = Guid.NewGuid();

    public ContentControllerTests()
    {
        _mockContentService = new Mock<IContentService>();
        _mockPageRepository = new Mock<IPageRepository>();
        _mockMediaRepository = new Mock<IMediaRepository>();
        _mockTenantContext = new Mock<ITenantContextService>();

        _controller = new ContentController(
            _mockContentService.Object,
            _mockPageRepository.Object,
            _mockMediaRepository.Object,
            _mockTenantContext.Object,
            NullLogger<ContentController>.Instance);
    }

    private Page CreateTestPage(Guid? pageId = null, Guid? websiteId = null)
    {
        return new Page
        {
            PageId = pageId ?? Guid.NewGuid(),
            WebsiteId = websiteId ?? _testWebsiteId,
            TenantId = _testTenantId,
            Title = "Test Page",
            Slug = "test-page",
            Content = "<p>Test content</p>",
            Template = "default",
            Status = PageStatus.Draft,
            Version = 1,
            CreatedAt = DateTime.UtcNow
        };
    }

    private MediaAsset CreateTestMediaAsset(Guid? mediaId = null, Guid? websiteId = null)
    {
        return new MediaAsset
        {
            MediaId = mediaId ?? Guid.NewGuid(),
            WebsiteId = websiteId ?? _testWebsiteId,
            TenantId = _testTenantId,
            FileName = "test-image.jpg",
            ContentType = "image/jpeg",
            SizeBytes = 1024,
            StorageUrl = "https://example.com/media/test-image.jpg",
            UploadedAt = DateTime.UtcNow
        };
    }

    #region CreatePage Tests

    [Fact]
    public async Task CreatePage_WithValidRequest_ReturnsCreated()
    {
        // Arrange
        var request = new CreatePageRequest
        {
            Title = "Test Page",
            Slug = "test-page",
            Content = "<p>Test content</p>",
            Template = "default"
        };

        var createdPage = CreateTestPage();

        _mockTenantContext.Setup(x => x.GetCurrentTenantId())
            .Returns(_testTenantId);

        _mockContentService.Setup(x => x.CreatePageAsync(
                It.IsAny<Page>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdPage);

        // Act
        var result = await _controller.CreatePage(_testWebsiteId, request, CancellationToken.None);

        // Assert
        var createdResult = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.ActionName.Should().Be(nameof(ContentController.GetPage));
        var response = createdResult.Value.Should().BeOfType<PageResponse>().Subject;
        response.PageId.Should().Be(createdPage.PageId);
        response.Title.Should().Be("Test Page");
    }

    [Fact]
    public async Task CreatePage_WithNoTenantContext_ReturnsBadRequest()
    {
        // Arrange
        var request = new CreatePageRequest
        {
            Title = "Test Page",
            Slug = "test-page",
            Content = "<p>Test content</p>"
        };

        _mockTenantContext.Setup(x => x.GetCurrentTenantId())
            .Returns((Guid?)null);

        // Act
        var result = await _controller.CreatePage(_testWebsiteId, request, CancellationToken.None);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var errorResponse = badRequestResult.Value.Should().BeOfType<ErrorResponse>().Subject;
        errorResponse.Error.Should().Be("Tenant context required");
    }

    [Fact]
    public async Task CreatePage_WithSeoMetadata_IncludesSeo()
    {
        // Arrange
        var request = new CreatePageRequest
        {
            Title = "Test Page",
            Slug = "test-page",
            Content = "<p>Test content</p>",
            Seo = new SeoMetadataRequest
            {
                MetaTitle = "SEO Title",
                MetaDescription = "SEO Description",
                MetaKeywords = "seo, test, page"
            }
        };

        var createdPage = CreateTestPage();

        _mockTenantContext.Setup(x => x.GetCurrentTenantId())
            .Returns(_testTenantId);

        Page? capturedPage = null;
        _mockContentService.Setup(x => x.CreatePageAsync(
                It.IsAny<Page>(),
                It.IsAny<CancellationToken>()))
            .Callback<Page, CancellationToken>((p, ct) => capturedPage = p)
            .ReturnsAsync(createdPage);

        // Act
        await _controller.CreatePage(_testWebsiteId, request, CancellationToken.None);

        // Assert
        capturedPage.Should().NotBeNull();
        capturedPage!.Seo.Should().NotBeNull();
        capturedPage.Seo!.MetaTitle.Should().Be("SEO Title");
        capturedPage.Seo.MetaDescription.Should().Be("SEO Description");
        capturedPage.Seo.MetaKeywords.Should().Be("seo, test, page");
    }

    [Fact]
    public async Task CreatePage_ConvertsSlugToLowercase()
    {
        // Arrange
        var request = new CreatePageRequest
        {
            Title = "Test Page",
            Slug = "TEST-PAGE",
            Content = "<p>Test content</p>"
        };

        var createdPage = CreateTestPage();

        _mockTenantContext.Setup(x => x.GetCurrentTenantId())
            .Returns(_testTenantId);

        Page? capturedPage = null;
        _mockContentService.Setup(x => x.CreatePageAsync(
                It.IsAny<Page>(),
                It.IsAny<CancellationToken>()))
            .Callback<Page, CancellationToken>((p, ct) => capturedPage = p)
            .ReturnsAsync(createdPage);

        // Act
        await _controller.CreatePage(_testWebsiteId, request, CancellationToken.None);

        // Assert
        capturedPage.Should().NotBeNull();
        capturedPage!.Slug.Should().Be("test-page");
    }

    [Fact]
    public async Task CreatePage_WithNoTemplate_UsesDefault()
    {
        // Arrange
        var request = new CreatePageRequest
        {
            Title = "Test Page",
            Slug = "test-page",
            Content = "<p>Test content</p>",
            Template = null
        };

        var createdPage = CreateTestPage();

        _mockTenantContext.Setup(x => x.GetCurrentTenantId())
            .Returns(_testTenantId);

        Page? capturedPage = null;
        _mockContentService.Setup(x => x.CreatePageAsync(
                It.IsAny<Page>(),
                It.IsAny<CancellationToken>()))
            .Callback<Page, CancellationToken>((p, ct) => capturedPage = p)
            .ReturnsAsync(createdPage);

        // Act
        await _controller.CreatePage(_testWebsiteId, request, CancellationToken.None);

        // Assert
        capturedPage.Should().NotBeNull();
        capturedPage!.Template.Should().Be("default");
    }

    #endregion

    #region GetPage Tests

    [Fact]
    public async Task GetPage_WithExistingPage_ReturnsOk()
    {
        // Arrange
        var pageId = Guid.NewGuid();
        var page = CreateTestPage(pageId, _testWebsiteId);

        _mockPageRepository.Setup(x => x.GetByIdAsync(pageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(page);

        // Act
        var result = await _controller.GetPage(_testWebsiteId, pageId, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<PageResponse>().Subject;
        response.PageId.Should().Be(pageId);
        response.Title.Should().Be("Test Page");
    }

    [Fact]
    public async Task GetPage_WithNonExistentPage_ReturnsNotFound()
    {
        // Arrange
        var pageId = Guid.NewGuid();

        _mockPageRepository.Setup(x => x.GetByIdAsync(pageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Page?)null);

        // Act
        var result = await _controller.GetPage(_testWebsiteId, pageId, CancellationToken.None);

        // Assert
        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        var errorResponse = notFoundResult.Value.Should().BeOfType<ErrorResponse>().Subject;
        errorResponse.Error.Should().Be("Page not found");
    }

    [Fact]
    public async Task GetPage_WithWrongWebsiteId_ReturnsNotFound()
    {
        // Arrange
        var pageId = Guid.NewGuid();
        var differentWebsiteId = Guid.NewGuid();
        var page = CreateTestPage(pageId, differentWebsiteId);

        _mockPageRepository.Setup(x => x.GetByIdAsync(pageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(page);

        // Act
        var result = await _controller.GetPage(_testWebsiteId, pageId, CancellationToken.None);

        // Assert
        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        var errorResponse = notFoundResult.Value.Should().BeOfType<ErrorResponse>().Subject;
        errorResponse.Error.Should().Be("Page not found");
    }

    #endregion

    #region ListPages Tests

    [Fact]
    public async Task ListPages_WithExistingPages_ReturnsOk()
    {
        // Arrange
        var pages = new List<Page>
        {
            CreateTestPage(),
            CreateTestPage()
        };

        _mockPageRepository.Setup(x => x.GetByWebsiteIdAsync(
                _testWebsiteId,
                It.IsAny<PageStatus?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(pages);

        // Act
        var result = await _controller.ListPages(_testWebsiteId, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var responses = okResult.Value.Should().BeOfType<List<PageResponse>>().Subject;
        responses.Should().HaveCount(2);
    }

    [Fact]
    public async Task ListPages_WithNoPages_ReturnsEmptyList()
    {
        // Arrange
        _mockPageRepository.Setup(x => x.GetByWebsiteIdAsync(
                _testWebsiteId,
                It.IsAny<PageStatus?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Page>());

        // Act
        var result = await _controller.ListPages(_testWebsiteId, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var responses = okResult.Value.Should().BeOfType<List<PageResponse>>().Subject;
        responses.Should().BeEmpty();
    }

    #endregion

    #region PublishPage Tests

    [Fact]
    public async Task PublishPage_WithValidPageId_ReturnsOk()
    {
        // Arrange
        var pageId = Guid.NewGuid();
        var publishedPage = CreateTestPage(pageId);
        publishedPage.Status = PageStatus.Published;
        publishedPage.PublishedAt = DateTime.UtcNow;

        _mockContentService.Setup(x => x.PublishPageAsync(pageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(publishedPage);

        // Act
        var result = await _controller.PublishPage(_testWebsiteId, pageId, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<PageResponse>().Subject;
        response.PageId.Should().Be(pageId);
        response.Status.Should().Be("Published");
    }

    #endregion

    #region UploadMedia Tests

    [Fact]
    public async Task UploadMedia_WithValidFile_ReturnsCreated()
    {
        // Arrange
        var mockFile = new Mock<IFormFile>();
        var fileName = "test-image.jpg";
        var content = "fake file content";
        var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));

        mockFile.Setup(f => f.FileName).Returns(fileName);
        mockFile.Setup(f => f.ContentType).Returns("image/jpeg");
        mockFile.Setup(f => f.Length).Returns(stream.Length);
        mockFile.Setup(f => f.OpenReadStream()).Returns(stream);

        var uploadedMedia = CreateTestMediaAsset();

        _mockContentService.Setup(x => x.UploadMediaAsync(
                _testWebsiteId,
                It.IsAny<Stream>(),
                fileName,
                "image/jpeg",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(uploadedMedia);

        // Act
        var result = await _controller.UploadMedia(_testWebsiteId, mockFile.Object, CancellationToken.None);

        // Assert
        var createdResult = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.ActionName.Should().Be(nameof(ContentController.GetMedia));
        var response = createdResult.Value.Should().BeOfType<MediaAssetResponse>().Subject;
        response.MediaId.Should().Be(uploadedMedia.MediaId);
        response.FileName.Should().Be("test-image.jpg");
    }

    [Fact]
    public async Task UploadMedia_WithNullFile_ReturnsBadRequest()
    {
        // Act
        var result = await _controller.UploadMedia(_testWebsiteId, null!, CancellationToken.None);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var errorResponse = badRequestResult.Value.Should().BeOfType<ErrorResponse>().Subject;
        errorResponse.Error.Should().Be("File is required");
    }

    [Fact]
    public async Task UploadMedia_WithEmptyFile_ReturnsBadRequest()
    {
        // Arrange
        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.Length).Returns(0);

        // Act
        var result = await _controller.UploadMedia(_testWebsiteId, mockFile.Object, CancellationToken.None);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var errorResponse = badRequestResult.Value.Should().BeOfType<ErrorResponse>().Subject;
        errorResponse.Error.Should().Be("File is required");
    }

    #endregion

    #region GetMedia Tests

    [Fact]
    public async Task GetMedia_WithExistingMedia_ReturnsOk()
    {
        // Arrange
        var mediaId = Guid.NewGuid();
        var media = CreateTestMediaAsset(mediaId, _testWebsiteId);

        _mockMediaRepository.Setup(x => x.GetByIdAsync(mediaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(media);

        // Act
        var result = await _controller.GetMedia(_testWebsiteId, mediaId, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<MediaAssetResponse>().Subject;
        response.MediaId.Should().Be(mediaId);
        response.FileName.Should().Be("test-image.jpg");
    }

    [Fact]
    public async Task GetMedia_WithNonExistentMedia_ReturnsNotFound()
    {
        // Arrange
        var mediaId = Guid.NewGuid();

        _mockMediaRepository.Setup(x => x.GetByIdAsync(mediaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MediaAsset?)null);

        // Act
        var result = await _controller.GetMedia(_testWebsiteId, mediaId, CancellationToken.None);

        // Assert
        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        var errorResponse = notFoundResult.Value.Should().BeOfType<ErrorResponse>().Subject;
        errorResponse.Error.Should().Be("Media not found");
    }

    [Fact]
    public async Task GetMedia_WithWrongWebsiteId_ReturnsNotFound()
    {
        // Arrange
        var mediaId = Guid.NewGuid();
        var differentWebsiteId = Guid.NewGuid();
        var media = CreateTestMediaAsset(mediaId, differentWebsiteId);

        _mockMediaRepository.Setup(x => x.GetByIdAsync(mediaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(media);

        // Act
        var result = await _controller.GetMedia(_testWebsiteId, mediaId, CancellationToken.None);

        // Assert
        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        var errorResponse = notFoundResult.Value.Should().BeOfType<ErrorResponse>().Subject;
        errorResponse.Error.Should().Be("Media not found");
    }

    #endregion

    #region ListMedia Tests

    [Fact]
    public async Task ListMedia_WithExistingMedia_ReturnsOk()
    {
        // Arrange
        var mediaList = new List<MediaAsset>
        {
            CreateTestMediaAsset(),
            CreateTestMediaAsset()
        };

        _mockMediaRepository.Setup(x => x.GetByWebsiteIdAsync(
                _testWebsiteId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mediaList);

        // Act
        var result = await _controller.ListMedia(_testWebsiteId, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var responses = okResult.Value.Should().BeOfType<List<MediaAssetResponse>>().Subject;
        responses.Should().HaveCount(2);
    }

    [Fact]
    public async Task ListMedia_WithNoMedia_ReturnsEmptyList()
    {
        // Arrange
        _mockMediaRepository.Setup(x => x.GetByWebsiteIdAsync(
                _testWebsiteId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MediaAsset>());

        // Act
        var result = await _controller.ListMedia(_testWebsiteId, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var responses = okResult.Value.Should().BeOfType<List<MediaAssetResponse>>().Subject;
        responses.Should().BeEmpty();
    }

    #endregion
}
