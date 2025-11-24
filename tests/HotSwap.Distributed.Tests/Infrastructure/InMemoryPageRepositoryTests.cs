using FluentAssertions;
using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Websites;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HotSwap.Distributed.Tests.Infrastructure;

public class InMemoryPageRepositoryTests
{
    private readonly InMemoryPageRepository _repository;
    private readonly Guid _websiteId = Guid.NewGuid();

    public InMemoryPageRepositoryTests()
    {
        _repository = new InMemoryPageRepository(
            NullLogger<InMemoryPageRepository>.Instance);
    }

    #region Create Tests

    [Fact]
    public async Task CreateAsync_WithValidPage_ShouldCreatePage()
    {
        // Arrange
        var page = CreateTestPage();

        // Act
        var created = await _repository.CreateAsync(page);

        // Assert
        created.Should().NotBeNull();
        created.PageId.Should().NotBe(Guid.Empty);
        created.Title.Should().Be("Test Page");
        created.Slug.Should().Be("test-page");
        created.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task CreateAsync_WithEmptyPageId_ShouldGenerateId()
    {
        // Arrange
        var page = CreateTestPage();
        page.PageId = Guid.Empty;

        // Act
        var created = await _repository.CreateAsync(page);

        // Assert
        created.PageId.Should().NotBe(Guid.Empty);
    }

    #endregion

    #region GetById Tests

    [Fact]
    public async Task GetByIdAsync_WithExistingPage_ShouldReturnPage()
    {
        // Arrange
        var page = CreateTestPage();
        var created = await _repository.CreateAsync(page);

        // Act
        var retrieved = await _repository.GetByIdAsync(created.PageId);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.PageId.Should().Be(created.PageId);
        retrieved.Title.Should().Be(created.Title);
    }

    [Fact]
    public async Task GetByIdAsync_WithNonExistentPage_ShouldReturnNull()
    {
        // Arrange
        var pageId = Guid.NewGuid();

        // Act
        var retrieved = await _repository.GetByIdAsync(pageId);

        // Assert
        retrieved.Should().BeNull();
    }

    #endregion

    #region GetBySlug Tests

    [Fact]
    public async Task GetBySlugAsync_WithExistingSlug_ShouldReturnPage()
    {
        // Arrange
        var page = CreateTestPage();
        await _repository.CreateAsync(page);

        // Act
        var retrieved = await _repository.GetBySlugAsync(_websiteId, "test-page");

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.Slug.Should().Be("test-page");
    }

    [Fact]
    public async Task GetBySlugAsync_WithDifferentCase_ShouldReturnPage()
    {
        // Arrange
        var page = CreateTestPage();
        page.Slug = "test-page";
        await _repository.CreateAsync(page);

        // Act
        var retrieved = await _repository.GetBySlugAsync(_websiteId, "TEST-PAGE");

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.Slug.Should().Be("test-page");
    }

    [Fact]
    public async Task GetBySlugAsync_WithDifferentWebsite_ShouldReturnNull()
    {
        // Arrange
        var page = CreateTestPage();
        await _repository.CreateAsync(page);

        // Act
        var retrieved = await _repository.GetBySlugAsync(Guid.NewGuid(), "test-page");

        // Assert
        retrieved.Should().BeNull();
    }

    [Fact]
    public async Task GetBySlugAsync_WithNonExistentSlug_ShouldReturnNull()
    {
        // Act
        var retrieved = await _repository.GetBySlugAsync(_websiteId, "nonexistent");

        // Assert
        retrieved.Should().BeNull();
    }

    #endregion

    #region GetByWebsiteId Tests

    [Fact]
    public async Task GetByWebsiteIdAsync_WithNoFilter_ShouldReturnAllPagesForWebsite()
    {
        // Arrange
        var page1 = CreateTestPage("Page 1", "page-1");
        var page2 = CreateTestPage("Page 2", "page-2");
        var page3 = CreateTestPage("Page 3", "page-3");
        page3.WebsiteId = Guid.NewGuid(); // Different website

        await _repository.CreateAsync(page1);
        await _repository.CreateAsync(page2);
        await _repository.CreateAsync(page3);

        // Act
        var pages = await _repository.GetByWebsiteIdAsync(_websiteId);

        // Assert
        pages.Should().HaveCount(2);
        pages.Should().Contain(p => p.Title == "Page 1");
        pages.Should().Contain(p => p.Title == "Page 2");
        pages.Should().NotContain(p => p.Title == "Page 3");
    }

    [Fact]
    public async Task GetByWebsiteIdAsync_WithStatusFilter_ShouldReturnFilteredPages()
    {
        // Arrange
        var page1 = CreateTestPage("Published 1", "published-1");
        page1.Status = PageStatus.Published;

        var page2 = CreateTestPage("Draft", "draft");
        page2.Status = PageStatus.Draft;

        var page3 = CreateTestPage("Published 2", "published-2");
        page3.Status = PageStatus.Published;

        await _repository.CreateAsync(page1);
        await _repository.CreateAsync(page2);
        await _repository.CreateAsync(page3);

        // Act
        var publishedPages = await _repository.GetByWebsiteIdAsync(_websiteId, PageStatus.Published);

        // Assert
        publishedPages.Should().HaveCount(2);
        publishedPages.Should().AllSatisfy(p => p.Status.Should().Be(PageStatus.Published));
    }

    [Fact]
    public async Task GetByWebsiteIdAsync_WhenNoPages_ShouldReturnEmptyList()
    {
        // Act
        var pages = await _repository.GetByWebsiteIdAsync(_websiteId);

        // Assert
        pages.Should().BeEmpty();
    }

    #endregion

    #region Update Tests

    [Fact]
    public async Task UpdateAsync_WithExistingPage_ShouldUpdatePageAndIncrementVersion()
    {
        // Arrange
        var page = CreateTestPage();
        var created = await _repository.CreateAsync(page);
        var originalVersion = created.Version;

        created.Title = "Updated Title";
        created.Content = "Updated content";

        // Act
        var updated = await _repository.UpdateAsync(created);

        // Assert
        updated.Title.Should().Be("Updated Title");
        updated.Content.Should().Be("Updated content");
        updated.Version.Should().Be(originalVersion + 1);
        updated.UpdatedAt.Should().NotBeNull();
        updated.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));

        var retrieved = await _repository.GetByIdAsync(created.PageId);
        retrieved!.Title.Should().Be("Updated Title");
        retrieved.Version.Should().Be(originalVersion + 1);
    }

    [Fact]
    public async Task UpdateAsync_WithNonExistentPage_ShouldThrowException()
    {
        // Arrange
        var page = CreateTestPage();
        page.PageId = Guid.NewGuid();

        // Act
        Func<Task> act = async () => await _repository.UpdateAsync(page);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*not found*");
    }

    #endregion

    #region Delete Tests

    [Fact]
    public async Task DeleteAsync_WithExistingPage_ShouldHardDeletePage()
    {
        // Arrange
        var page = CreateTestPage();
        var created = await _repository.CreateAsync(page);

        // Act
        var result = await _repository.DeleteAsync(created.PageId);

        // Assert
        result.Should().BeTrue();

        var retrieved = await _repository.GetByIdAsync(created.PageId);
        retrieved.Should().BeNull(); // Hard delete, not soft delete
    }

    [Fact]
    public async Task DeleteAsync_WithNonExistentPage_ShouldReturnFalse()
    {
        // Arrange
        var pageId = Guid.NewGuid();

        // Act
        var result = await _repository.DeleteAsync(pageId);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region GetPublishedPages Tests

    [Fact]
    public async Task GetPublishedPagesAsync_ShouldReturnOnlyPublishedPages()
    {
        // Arrange
        var page1 = CreateTestPage("Published 1", "published-1");
        page1.Status = PageStatus.Published;

        var page2 = CreateTestPage("Draft", "draft");
        page2.Status = PageStatus.Draft;

        var page3 = CreateTestPage("Published 2", "published-2");
        page3.Status = PageStatus.Published;

        var page4 = CreateTestPage("Archived", "archived");
        page4.Status = PageStatus.Archived;

        await _repository.CreateAsync(page1);
        await _repository.CreateAsync(page2);
        await _repository.CreateAsync(page3);
        await _repository.CreateAsync(page4);

        // Act
        var publishedPages = await _repository.GetPublishedPagesAsync(_websiteId);

        // Assert
        publishedPages.Should().HaveCount(2);
        publishedPages.Should().AllSatisfy(p => p.Status.Should().Be(PageStatus.Published));
        publishedPages.Should().Contain(p => p.Title == "Published 1");
        publishedPages.Should().Contain(p => p.Title == "Published 2");
    }

    [Fact]
    public async Task GetPublishedPagesAsync_WhenNoPublishedPages_ShouldReturnEmptyList()
    {
        // Arrange
        var page = CreateTestPage();
        page.Status = PageStatus.Draft;
        await _repository.CreateAsync(page);

        // Act
        var publishedPages = await _repository.GetPublishedPagesAsync(_websiteId);

        // Assert
        publishedPages.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPublishedPagesAsync_WithDifferentWebsite_ShouldNotIncludeOtherWebsitePages()
    {
        // Arrange
        var page1 = CreateTestPage("Published 1", "published-1");
        page1.Status = PageStatus.Published;

        var page2 = CreateTestPage("Published 2", "published-2");
        page2.WebsiteId = Guid.NewGuid(); // Different website
        page2.Status = PageStatus.Published;

        await _repository.CreateAsync(page1);
        await _repository.CreateAsync(page2);

        // Act
        var publishedPages = await _repository.GetPublishedPagesAsync(_websiteId);

        // Assert
        publishedPages.Should().HaveCount(1);
        publishedPages.Should().Contain(p => p.Title == "Published 1");
        publishedPages.Should().NotContain(p => p.Title == "Published 2");
    }

    #endregion

    #region Helper Methods

    private Page CreateTestPage(string title = "Test Page", string slug = "test-page")
    {
        return new Page
        {
            PageId = Guid.NewGuid(),
            WebsiteId = _websiteId,
            Title = title,
            Slug = slug,
            Content = "Test content",
            Status = PageStatus.Draft,
            Version = 1
        };
    }

    #endregion
}
